using System.Collections.Generic;
using Hotel.Runtime;

/// <summary>
/// Pure, deterministic event candidate filtering and weighted selection.
/// No UnityEngine.Random global state is used; all randomness derives from a
/// caller-supplied seed (computed from run seed / day / phase / history occurrence).
/// Assembly note: this file lives in the default assembly (Assembly-CSharp) because
/// it depends on EventConfig (which is not visible from Hotel.Runtime); the runtime
/// state types it reads (EventHistoryRecord) come from Hotel.Runtime.
/// </summary>
public static class EventSelectionService
{
    /// <summary>Salt used to derive the day / hidden-phase chance-roll seed from the base selection seed.</summary>
    public const int SaltRoll = 0x5A1D;
    /// <summary>Salt used to derive the number-of-picks seed from the base selection seed.</summary>
    public const int SaltCount = 0xC4A7;

    /// <summary>Maps the presentation GamePhase onto the trigger flags enum.</summary>
    public static EventPhase ToEventPhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Day: return EventPhase.Day;
            case GamePhase.Night: return EventPhase.Night;
            case GamePhase.Dawn: return EventPhase.Dawn;
            case GamePhase.Dusk: return EventPhase.Dusk;
            default: return 0;
        }
    }

    /// <summary>
    /// Returns configs from <paramref name="catalog"/> eligible to fire on the given
    /// day/phase, in catalog order. In this phase only EventKind.Normal events are
    /// eligible; ChainStep/Personal/SpecialVisitor are excluded until their owning
    /// systems exist. RepeatPolicy.OncePerRun excludes events already present in
    /// history; Repeatable applies cooldownDays from the last occurrence.
    /// Invalid/missing configs are skipped gracefully.
    /// </summary>
    public static List<EventConfig> FilterCandidates(
        IReadOnlyList<EventConfig> catalog,
        int day,
        GamePhase phase,
        IReadOnlyList<EventHistoryRecord> history)
    {
        var result = new List<EventConfig>();
        if (catalog == null) return result;

        EventPhase phaseFlag = ToEventPhase(phase);
        if (phaseFlag == 0) return result;

        for (int i = 0; i < catalog.Count; i++)
        {
            EventConfig config = catalog[i];
            if (config == null) continue;
            if (string.IsNullOrEmpty(config.eventId)) continue;

            TriggerSpec trigger = config.trigger;
            if (trigger == null) continue;

            // Only Normal events are selectable until owning systems are implemented.
            if (trigger.kind != EventKind.Normal) continue;

            if (!trigger.AllowsPhase(phaseFlag)) continue;

            if (day < trigger.minDay) continue;
            if (trigger.maxDay > 0 && day > trigger.maxDay) continue;

            if (trigger.repeatPolicy == RepeatPolicy.OncePerRun)
            {
                if (HasOccurred(history, config.eventId)) continue;
            }
            else if (trigger.cooldownDays > 0)
            {
                int lastDay = LastOccurrenceDay(history, config.eventId);
                if (lastDay > 0 && day <= lastDay + trigger.cooldownDays) continue;
            }

            result.Add(config);
        }

        return result;
    }

    /// <summary>
    /// Deterministic weighted pick from <paramref name="candidates"/> using effective
    /// weights (baseWeight * runtime modifier). Invalid/missing ids and non-positive
    /// or NaN effective weights are skipped gracefully. Returns null when no candidate
    /// is selectable. Cumulative totals are kept in double precision throughout.
    /// The seed must be derived deterministically by the caller.
    /// </summary>
    public static EventConfig PickWeighted(
        IReadOnlyList<EventConfig> candidates,
        IReadOnlyDictionary<string, float> modifiers,
        int seed)
    {
        if (candidates == null || candidates.Count == 0) return null;

        var weighted = new List<KeyValuePair<EventConfig, double>>(candidates.Count);
        double total = 0.0;

        for (int i = 0; i < candidates.Count; i++)
        {
            EventConfig config = candidates[i];
            if (config == null) continue;
            if (string.IsNullOrEmpty(config.eventId)) continue;

            TriggerSpec trigger = config.trigger;
            if (trigger == null || trigger.baseWeight < 1) continue;

            float modifier = 1f;
            if (modifiers != null && !modifiers.TryGetValue(config.eventId, out modifier))
                modifier = 1f;
            if (float.IsNaN(modifier) || modifier <= 0f) continue;

            double effective = (double)trigger.baseWeight * modifier;
            total += effective;
            weighted.Add(new KeyValuePair<EventConfig, double>(config, total));
        }

        if (weighted.Count == 0) return null;

        var rng = new System.Random(seed);
        double roll = rng.NextDouble() * total;

        for (int i = 0; i < weighted.Count; i++)
        {
            if (roll < weighted[i].Value)
                return weighted[i].Key;
        }

        return weighted[weighted.Count - 1].Key;
    }

    /// <summary>
    /// Deterministic per-phase selection seed derived from run seed, day, phase, and
    /// the number of history occurrences so far. The phase is shifted by +1 so the
    /// Day phase (enum value 0) still contributes a distinct value.
    /// </summary>
    public static int ComputeSelectionSeed(int runSeed, int day, GamePhase phase, int historyOccurrence)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + runSeed;
            h = h * 31 + day;
            h = h * 31 + ((int)phase + 1);
            h = h * 31 + historyOccurrence;
            return h;
        }
    }

    /// <summary>
    /// Robust deterministic avalanche mix (MurmurHash3 fmix-style) used to derive
    /// independent child seeds (chance roll, pick #1, pick #2, ...) from one base
    /// selection seed. Distinct salts produce uncorrelated seeds.
    /// </summary>
    public static int DeriveSeed(int seed, int salt)
    {
        unchecked
        {
            uint z = (uint)seed ^ (uint)salt;
            z += 0x9E3779B9u;
            z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
            z = (z ^ (z >> 13)) * 0xC2B2AE35u;
            z ^= z >> 16;
            return (int)z;
        }
    }

    /// <summary>True if any history record (resolved or not) exists for the eventId.</summary>
    public static bool HasOccurred(IReadOnlyList<EventHistoryRecord> history, string eventId)
    {
        if (history == null) return false;
        for (int i = 0; i < history.Count; i++)
        {
            if (history[i] != null && history[i].EventId == eventId) return true;
        }
        return false;
    }

    /// <summary>True if a not-yet-resolved history record exists for the eventId.</summary>
    public static bool HasUnresolvedOccurrence(IReadOnlyList<EventHistoryRecord> history, string eventId)
    {
        if (history == null) return false;
        for (int i = 0; i < history.Count; i++)
        {
            EventHistoryRecord record = history[i];
            if (record != null && record.EventId == eventId && !record.Resolved) return true;
        }
        return false;
    }

    private static int LastOccurrenceDay(IReadOnlyList<EventHistoryRecord> history, string eventId)
    {
        if (history == null) return 0;
        int last = 0;
        for (int i = 0; i < history.Count; i++)
        {
            EventHistoryRecord record = history[i];
            if (record != null && record.EventId == eventId && record.Day > last)
                last = record.Day;
        }
        return last;
    }
}
