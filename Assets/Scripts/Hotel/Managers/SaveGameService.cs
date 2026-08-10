using System;
using System.IO;
using Hotel.Runtime;
using UnityEngine;

public static class GameLaunchContext
{
    private const int DefaultSlot = 1;
    private static GameRunState pendingState;
    private static bool forceNewGame;

    /// <summary>The save slot the current run reads from and writes to (1..SaveGameService.MaxSlots).</summary>
    public static int ActiveSlot { get; private set; } = DefaultSlot;

    public static void StartNewGame(int slot = DefaultSlot)
    {
        pendingState = null;
        forceNewGame = true;
        ActiveSlot = slot;
    }

    public static void ContinueWith(GameRunState state)
    {
        ContinueWith(DefaultSlot, state);
    }

    public static void ContinueWith(int slot, GameRunState state)
    {
        pendingState = state ?? throw new ArgumentNullException(nameof(state));
        forceNewGame = false;
        ActiveSlot = slot;
    }

    public static bool TryConsume(out GameRunState state, out bool startFresh)
    {
        state = pendingState;
        startFresh = forceNewGame;
        pendingState = null;
        forceNewGame = false;
        return state != null || startFresh;
    }
}

public readonly struct SaveSlotSummary
{
    public SaveSlotSummary(int day, HotelPhase phase, int tenantCount, DateTime savedAtLocal)
    {
        Day = day;
        Phase = phase;
        TenantCount = tenantCount;
        SavedAtLocal = savedAtLocal;
    }

    public int Day { get; }
    public HotelPhase Phase { get; }
    public int TenantCount { get; }
    public DateTime SavedAtLocal { get; }
}

public static class SaveGameService
{
    public const int MaxSlots = 3;
    private const string BackupSuffix = ".bak";
    private const string TemporarySuffix = ".tmp";

    public static string SlotPath(int slot)
    {
        slot = Mathf.Clamp(slot, 1, MaxSlots);
        return Path.Combine(Application.persistentDataPath, $"hotel-save-slot-{slot}.json");
    }

    public static bool HasSave(int slot = 1)
    {
        string path = SlotPath(slot);
        return File.Exists(path) || File.Exists(path + BackupSuffix);
    }

    public static bool TrySave(GameRunState state, out string error)
    {
        return TrySave(GameLaunchContext.ActiveSlot, state, out error);
    }

    public static bool TrySave(int slot, GameRunState state, out string error)
    {
        error = null;
        if (state == null)
        {
            error = "There is no active run to save.";
            return false;
        }

        var path = SlotPath(slot);
        var temporaryPath = path + TemporarySuffix;
        var backupPath = path + BackupSuffix;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temporaryPath, RunSaveCodec.ToJson(state, DateTime.UtcNow));

            if (File.Exists(path))
                File.Replace(temporaryPath, path, backupPath);
            else
                File.Move(temporaryPath, path);

            Debug.Log($"[SaveGameService] Saved Day {state.Day} {state.Phase.Current} to slot {slot} ({path})");
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogError($"[SaveGameService] Save failed: {exception}");
            TryDeleteTemporaryFile(temporaryPath);
            return false;
        }
    }

    public static bool TryLoad(out GameRunState state, out string error)
    {
        return TryLoad(GameLaunchContext.ActiveSlot, out state, out error);
    }

    public static bool TryLoad(int slot, out GameRunState state, out string error)
    {
        string path = SlotPath(slot);
        if (TryLoadPath(path, out state, out error)) return true;

        var primaryError = error;
        if (TryLoadPath(path + BackupSuffix, out state, out error))
        {
            Debug.LogWarning($"[SaveGameService] Primary save failed; loaded backup instead. {primaryError}");
            return true;
        }

        if (string.IsNullOrEmpty(error)) error = primaryError;
        return false;
    }

    public static bool TryGetSummary(out SaveSlotSummary summary)
    {
        return TryGetSummary(GameLaunchContext.ActiveSlot, out summary);
    }

    public static bool TryGetSummary(int slot, out SaveSlotSummary summary)
    {
        summary = default;
        string path = SlotPath(slot);
        if (!TryReadSaveData(path, out var save) && !TryReadSaveData(path + BackupSuffix, out save))
            return false;

        DateTime savedAt;
        if (!DateTime.TryParse(save.SavedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out savedAt))
            savedAt = File.GetLastWriteTimeUtc(path);

        summary = new SaveSlotSummary(
            Math.Max(1, save.Day),
            save.Phase,
            save.Tenants != null ? save.Tenants.Count : 0,
            savedAt.ToLocalTime());
        return true;
    }

    /// <summary>Summaries of all slots in order 1..MaxSlots; null when a slot is empty.</summary>
    public static SaveSlotSummary?[] GetAllSummaries()
    {
        var result = new SaveSlotSummary?[MaxSlots];
        for (int i = 0; i < MaxSlots; i++)
        {
            if (TryGetSummary(i + 1, out SaveSlotSummary summary))
                result[i] = summary;
        }
        return result;
    }

    public static bool DeleteSave(out string error)
    {
        return DeleteSave(GameLaunchContext.ActiveSlot, out error);
    }

    public static bool DeleteSave(int slot, out string error)
    {
        error = null;
        string path = SlotPath(slot);
        try
        {
            DeleteIfPresent(path);
            DeleteIfPresent(path + BackupSuffix);
            DeleteIfPresent(path + TemporarySuffix);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogError($"[SaveGameService] Delete failed: {exception}");
            return false;
        }
    }

    private static bool TryLoadPath(string path, out GameRunState state, out string error)
    {
        state = null;
        error = null;
        if (!File.Exists(path))
        {
            error = "No save file exists.";
            return false;
        }

        try
        {
            state = RunSaveCodec.FromJson(File.ReadAllText(path));
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogError($"[SaveGameService] Could not load {path}: {exception}");
            return false;
        }
    }

    private static bool TryReadSaveData(string path, out RunSaveData data)
    {
        data = null;
        if (!File.Exists(path)) return false;

        try
        {
            data = RunSaveCodec.ReadMetadata(File.ReadAllText(path));
            return data != null;
        }
        catch
        {
            return false;
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try { DeleteIfPresent(path); }
        catch { }
    }
}
