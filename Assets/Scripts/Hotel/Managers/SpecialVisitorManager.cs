using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public static class SpecialVisitorManager
{
    public const string MerchantVisitorId = "d12_merchant";

    public static bool ForceDay1MerchantTest { get; set; } = false;

    public static int DeriveVisitorSeed(int runSeed, string visitorId, int visitIndex)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + runSeed;
            if (!string.IsNullOrEmpty(visitorId))
            {
                for (int i = 0; i < visitorId.Length; i++)
                    h = h * 31 + visitorId[i];
            }
            h = h * 31 + visitIndex;
            return EventSelectionService.DeriveSeed(h, visitIndex + 1);
        }
    }

    public static int CalculateInterval(int seed)
    {
        int rawMod = seed % 3;
        int nonNegativeMod = (rawMod + 3) % 3;
        return 3 + nonNegativeMod;
    }

    public static bool HasOccurredOnDay(IReadOnlyList<EventHistoryRecord> history, string eventId, int day)
    {
        if (history == null || string.IsNullOrEmpty(eventId))
            return false;

        for (int i = 0; i < history.Count; i++)
        {
            EventHistoryRecord record = history[i];
            if (record == null)
                continue;

            if (record.Day == day && (record.EventId == eventId || record.DefinitionId == eventId))
                return true;
        }

        return false;
    }

    public static bool IsDueOnDay(GameRunState state, string eventId, int day)
    {
        if (state == null || string.IsNullOrEmpty(eventId) || day < 1)
            return false;

        if (state.EventHistory != null)
        {
            for (int i = 0; i < state.EventHistory.Count; i++)
            {
                EventHistoryRecord r = state.EventHistory[i];
                if (r != null && !r.Resolved && (r.EventId == eventId || r.DefinitionId == eventId))
                    return false;
            }
        }

        if (HasOccurredOnDay(state.EventHistory, eventId, day))
            return false;

        if (day == 1 && ForceDay1MerchantTest && eventId == MerchantVisitorId)
            return true;

        int targetDay = 0;
        int visitIndex = 0;

        while (targetDay < day)
        {
            int seed = DeriveVisitorSeed(state.Seed, eventId, visitIndex);
            int interval = CalculateInterval(seed);
            targetDay += interval;
            visitIndex++;

            if (targetDay == day)
                return true;
        }

        return false;
    }

    public static bool IsSpecialVisitorStillEligible(GameRunState state, EventConfig config, int day, GamePhase phase)
    {
        if (state == null || config == null || string.IsNullOrEmpty(config.eventId) || config.trigger == null)
            return false;

        if (config.trigger.kind != EventKind.SpecialVisitor)
            return false;

        EventPhase phaseFlag = EventSelectionService.ToEventPhase(phase);
        if (!config.trigger.AllowsPhase(phaseFlag))
            return false;

        if (day < config.trigger.minDay)
            return false;

        if (config.trigger.maxDay > 0 && day > config.trigger.maxDay)
            return false;

        if (HasOccurredOnDay(state.EventHistory, config.eventId, day))
            return true;

        return IsDueOnDay(state, config.eventId, day);
    }
}
