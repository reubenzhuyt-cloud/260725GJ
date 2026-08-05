using System;
using System.IO;
using Hotel.Runtime;
using UnityEngine;

public static class GameLaunchContext
{
    private static GameRunState pendingState;
    private static bool forceNewGame;

    public static void StartNewGame()
    {
        pendingState = null;
        forceNewGame = true;
    }

    public static void ContinueWith(GameRunState state)
    {
        pendingState = state ?? throw new ArgumentNullException(nameof(state));
        forceNewGame = false;
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
    public const string SaveFileName = "hotel-save-slot-1.json";
    private const string BackupSuffix = ".bak";
    private const string TemporarySuffix = ".tmp";

    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    public static bool HasSave => File.Exists(SavePath) || File.Exists(SavePath + BackupSuffix);

    public static bool TrySave(GameRunState state, out string error)
    {
        error = null;
        if (state == null)
        {
            error = "There is no active run to save.";
            return false;
        }

        var path = SavePath;
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

            Debug.Log($"[SaveGameService] Saved Day {state.Day} {state.Phase.Current} to {path}");
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
        if (TryLoadPath(SavePath, out state, out error)) return true;

        var primaryError = error;
        if (TryLoadPath(SavePath + BackupSuffix, out state, out error))
        {
            Debug.LogWarning($"[SaveGameService] Primary save failed; loaded backup instead. {primaryError}");
            return true;
        }

        if (string.IsNullOrEmpty(error)) error = primaryError;
        return false;
    }

    public static bool TryGetSummary(out SaveSlotSummary summary)
    {
        summary = default;
        if (!TryReadSaveData(SavePath, out var save) && !TryReadSaveData(SavePath + BackupSuffix, out save))
            return false;

        DateTime savedAt;
        if (!DateTime.TryParse(save.SavedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out savedAt))
            savedAt = File.GetLastWriteTimeUtc(SavePath);

        summary = new SaveSlotSummary(
            Math.Max(1, save.Day),
            save.Phase,
            save.Tenants != null ? save.Tenants.Count : 0,
            savedAt.ToLocalTime());
        return true;
    }

    public static bool DeleteSave(out string error)
    {
        error = null;
        try
        {
            DeleteIfPresent(SavePath);
            DeleteIfPresent(SavePath + BackupSuffix);
            DeleteIfPresent(SavePath + TemporarySuffix);
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
