using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

/// <summary>
/// Runtime effects/material to merge into a chain step popup before it is shown.
/// Produced by ChainManager and consumed by EventManager/EventUI. Everything here
/// is a plain EventEffect so the standard settlement pipeline applies unchanged.
/// </summary>
public sealed class ChainRuntimePatch
{
    public EventEffect[] ConfirmEffects;
    public Dictionary<int, EventEffect[]> ChoiceEffects;
    public bool[] ChoiceLocked;
}

/// <summary>
/// Deterministic per-run orchestrator for the continuous narrative chains.
///
/// The manager is deliberately stateless: every piece of chain state lives in
/// GameRunState (ChainRunState per chainId), so scheduling, binding, flags and
/// completion all survive save/load and never depend on scene objects. All
/// mutations go through AuthorizedChangeSet -> StateReducer.
///
/// Scheduling rules:
///  - A chain binds to a deterministically chosen assigned tenant on the first
///    Day phase where that tenant is available (checked in on a previous day).
///    Its first trigger day is persisted as FirstTriggerDay = tenant.CheckInDay
///    + 2 or +3 (day 3/4 after check-in), chosen deterministically from the run
///    seed plus the chain/target ids. The exact due day of every step
///    (FirstTriggerDay + authored step offset) is persisted as NextDueDay and
///    only advanced atomically with a successful settlement.
///  - At most one chain event is injected per Day phase entry; EventManager also
///    refuses a second chain event while one is pending, so chain steps never
///    duplicate and never stack. A due event stays due until it settles.
///  - When several chains are due at once the earliest persisted due day wins,
///    with a seed-derived tie breaker (no static catalog starvation).
///  - A step that was already presented (recorded in EventHistory, resolved or
///    not) is never re-injected, which keeps event ids unique across save/load.
/// </summary>
public static class ChainManager
{
    /// <summary>Wired by EventManager so blocked steps can surface a player-visible reason.</summary>
    public static Action<string> NoticeProvider;

    // ------------------------------------------------------------------ public

    /// <summary>
    /// Called from EventManager.OnPhaseEntered for the Day phase. Starts pending
    /// chains, migrates legacy schedules, and returns the single chain event due
    /// now (or an empty list). The caller enqueues the returned configs.
    /// </summary>
    public static List<EventConfig> BuildDueChainEvents(
        GameRunState state,
        StateReducer reducer,
        int day,
        IReadOnlyList<EventConfig> catalog,
        IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        var result = new List<EventConfig>();
        if (state == null || reducer == null || catalog == null || day < 1)
            return result;

        TryStartPendingChains(state, reducer, day, candidates);
        EnsureLegacySchedules(state, reducer, day);

        if (EventManager.Instance != null && EventManager.Instance.HasPendingChainEvent())
            return result;

        var chainIds = new List<string>(state.Chains.Keys);
        chainIds.Sort(StringComparer.Ordinal);

        string bestChainId = null;
        int bestDueDay = int.MaxValue;
        uint bestTieBreak = uint.MaxValue;

        for (int i = 0; i < chainIds.Count; i++)
        {
            string chainId = chainIds[i];
            ChainRunState chain = state.Chains[chainId];
            if (chain == null || chain.Completed || chain.Failed)
                continue;

            ChainStepRuntimeSpec spec = ChainRuntimeCatalog.GetStep(chainId, chain.NextStepToPresent);
            if (spec == null)
                continue;

            if (string.IsNullOrEmpty(chain.TargetTenantId) || !state.Tenants.ContainsKey(chain.TargetTenantId))
            {
                FailChain(state, reducer, chainId);
                continue;
            }

            if (!AreFlagsPresent(chain, spec.RequireFlags))
                continue;

            if (chain.NextDueDay < 1 || chain.NextDueDay > day)
                continue;

            EventConfig config = ChainRuntimeCatalog.FindEvent(chainId, chain.NextStepToPresent, catalog);
            if (config == null)
                continue;
            if (EventSelectionService.HasOccurred(state.EventHistory, config.eventId))
                continue;

            uint tieBreak = DeriveTieBreak(state.Seed, chainId);
            if (chain.NextDueDay < bestDueDay
                || (chain.NextDueDay == bestDueDay && tieBreak < bestTieBreak))
            {
                bestDueDay = chain.NextDueDay;
                bestTieBreak = tieBreak;
                bestChainId = chainId;
            }
        }

        if (bestChainId != null)
        {
            ChainRunState best = state.Chains[bestChainId];
            EventConfig config = ChainRuntimeCatalog.FindEvent(bestChainId, best.NextStepToPresent, catalog);
            if (config != null)
                result.Add(config);
        }

        return result;
    }

    /// <summary>Owner tenant bound to a chain step (used as the event protagonist).</summary>
    public static string ResolveChainOwner(GameRunState state, EventConfig config)
    {
        if (state == null || config == null || config.trigger == null)
            return null;
        if (config.trigger.kind != EventKind.ChainStep)
            return null;
        if (string.IsNullOrEmpty(config.trigger.chainId))
            return null;
        if (!state.Chains.TryGetValue(config.trigger.chainId, out ChainRunState chain))
            return null;
        if (string.IsNullOrEmpty(chain.TargetTenantId) || !state.Tenants.ContainsKey(chain.TargetTenantId))
            return null;
        return chain.TargetTenantId;
    }

    /// <summary>
    /// Extra effects and choice locking to merge into a chain step popup. Called by
    /// EventManager.TriggerEvent before the popup is raised, so the merged effects
    /// flow through the standard EventUI -> EventProcessedData -> settlement path.
    /// </summary>
    public static ChainRuntimePatch BuildRuntimePatch(
        GameRunState state,
        EventConfig config,
        IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        if (state == null || config == null || config.trigger == null || config.trigger.kind != EventKind.ChainStep)
            return null;
        ChainStepRuntimeSpec spec = ChainRuntimeCatalog.GetStep(config.trigger.chainId, config.trigger.chainStep);
        if (spec == null)
            return null;
        if (!state.Chains.TryGetValue(config.trigger.chainId, out ChainRunState chain))
            return null;

        var patch = new ChainRuntimePatch();
        if (config.eventType == GameEventType.Confirm)
        {
            if (spec.ConfirmEffects.Count > 0)
                patch.ConfirmEffects = spec.ConfirmEffects.ToArray();
        }
        else if (config.eventType == GameEventType.Choice)
        {
            patch.ChoiceEffects = new Dictionary<int, EventEffect[]>();
            for (int i = 0; i < config.choices.Count; i++)
            {
                ChoiceOption choice = config.choices[i];
                if (choice == null || string.IsNullOrEmpty(choice.choiceId))
                    continue;
                if (spec.ChoiceEffects.TryGetValue(choice.choiceId, out List<EventEffect> extra) && extra.Count > 0)
                    patch.ChoiceEffects[i] = extra.ToArray();
            }
            patch.ChoiceLocked = BuildChoiceLock(state, chain, config, spec);
        }
        return patch;
    }

    /// <summary>Dequeue-time eligibility for an injected chain step.</summary>
    public static bool IsChainStepStillEligible(GameRunState state, EventConfig config, int day)
    {
        if (state == null || config == null || config.trigger == null)
            return true;
        if (config.trigger.kind != EventKind.ChainStep)
            return true;
        string chainId = config.trigger.chainId;
        int step = config.trigger.chainStep;
        if (!state.Chains.TryGetValue(chainId, out ChainRunState chain))
            return false;
        if (chain.Completed || chain.Failed)
            return false;
        if (chain.NextStepToPresent != step)
            return false;
        ChainStepRuntimeSpec spec = ChainRuntimeCatalog.GetStep(chainId, step);
        if (spec == null)
            return false;
        if (string.IsNullOrEmpty(chain.TargetTenantId) || !state.Tenants.ContainsKey(chain.TargetTenantId))
            return false;
        if (!AreFlagsPresent(chain, spec.RequireFlags))
            return false;
        if (EventSelectionService.HasOccurred(state.EventHistory, config.eventId))
            return false;
        return true;
    }

    /// <summary>
    /// Settlement-time guard: a chain choice locked by missing prior-choice flags
    /// (or an unavailable child room) is rejected instead of silently settling.
    /// </summary>
    public static bool IsOptionAvailable(
        GameRunState state,
        string eventId,
        string optionId,
        IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        if (state == null)
            return true;
        if (!ChainRuntimeCatalog.TryParseEvent(eventId, out string chainId, out int step))
            return true;
        if (!state.Chains.TryGetValue(chainId, out ChainRunState chain))
            return false;
        if (chain.NextStepToPresent != step)
            return false;
        ChainStepRuntimeSpec spec = ChainRuntimeCatalog.GetStep(chainId, step);
        if (spec == null)
            return true;
        if (string.IsNullOrEmpty(optionId))
            return true;
        if (spec.RequireFlagsByChoice.TryGetValue(optionId, out string[] required) && !AreFlagsPresent(chain, required))
            return false;
        if (ChoiceRequiresVacantRoom(spec, optionId) && !ChainRoomState.HasVacantRoom(state))
            return false;
        return true;
    }

    /// <summary>Clears any once-per-session notice bookkeeping.</summary>
    public static void ResetSessionState()
    {
    }

    // ------------------------------------------------------------------ private

    private static void TryStartPendingChains(
        GameRunState state,
        StateReducer reducer,
        int day,
        IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        IReadOnlyList<string> chainIds = ChainRuntimeCatalog.ChainIds;
        for (int c = 0; c < chainIds.Count; c++)
        {
            string chainId = chainIds[c];
            if (state.Chains.ContainsKey(chainId))
                continue;
            string tenantId = SelectTargetTenant(state, chainId, day, candidates);
            if (tenantId == null)
                continue;
            int firstTriggerDay = ComputeFirstTriggerDay(state, chainId, tenantId, day);
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "ChainManager", "StartChain");
            set.Add(new StartChainChange(chainId, tenantId, day, firstTriggerDay, firstTriggerDay));
            CommitResult result = reducer.TryCommit(state, set);
            if (!result.Succeeded)
                Debug.LogWarning($"[ChainManager] StartChain commit failed for chain '{chainId}'.");
        }
    }

    /// <summary>
    /// Deterministic day-3/day-4 first trigger: tenant.CheckInDay + 2 or +3,
    /// chosen from the run seed plus stable chain/target salts. Persisted in the
    /// StartChain commit so it is never rerolled after a save/load.
    /// </summary>
    private static int ComputeFirstTriggerDay(GameRunState state, string chainId, string tenantId, int day)
    {
        int checkIn = day - 1;
        if (state.Tenants.TryGetValue(tenantId, out TenantRunState tenant) && tenant.CheckInDay > 0)
            checkIn = tenant.CheckInDay;
        int derived = EventSelectionService.DeriveSeed(state.Seed, StableHash(chainId + "|" + tenantId + "|firsttrigger"));
        int roll = (int)(((uint)derived & 0x7FFFFFFFu) % 2u);
        return checkIn + 2 + roll;
    }

    /// <summary>
    /// Backward-safe migration for active chains persisted before FirstTriggerDay
    /// and NextDueDay existed. Derives the schedule from the persisted start day
    /// relationship and commits it through an AuthorizedChangeSet before any due
    /// checks; a chain is only enqueued once its derived due day is reached.
    /// Never rerolls an already persisted value.
    /// </summary>
    private static void EnsureLegacySchedules(GameRunState state, StateReducer reducer, int day)
    {
        if (state.Chains == null || state.Chains.Count == 0)
            return;
        var chainIds = new List<string>(state.Chains.Keys);
        chainIds.Sort(StringComparer.Ordinal);
        for (int i = 0; i < chainIds.Count; i++)
        {
            string chainId = chainIds[i];
            ChainRunState chain = state.Chains[chainId];
            if (chain == null || chain.Completed || chain.Failed)
                continue;
            if (chain.FirstTriggerDay > 0 && chain.NextDueDay > 0)
                continue;
            int firstTriggerDay = chain.FirstTriggerDay > 0
                ? chain.FirstTriggerDay
                : chain.StartDay > 0 ? chain.StartDay : day;
            int nextDueDay = chain.NextDueDay > 0
                ? chain.NextDueDay
                : firstTriggerDay + ChainRuntimeCatalog.GetStepDayOffset(chainId, chain.NextStepToPresent);
            if (firstTriggerDay < 1)
                firstTriggerDay = day;
            if (nextDueDay < firstTriggerDay)
                nextDueDay = firstTriggerDay;
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "ChainManager", "MigrateChainSchedule");
            set.Add(new SetChainScheduleChange(chainId, firstTriggerDay, nextDueDay));
            CommitResult result = reducer.TryCommit(state, set);
            if (!result.Succeeded)
                Debug.LogWarning($"[ChainManager] SetChainSchedule commit failed for chain '{chainId}'.");
        }
    }

    private static string SelectTargetTenant(
        GameRunState state,
        string chainId,
        int day,
        IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        if (state.Tenants == null || state.Tenants.Count == 0)
            return null;

        var used = new HashSet<string>();
        foreach (var pair in state.Chains)
        {
            if (pair.Value != null && !string.IsNullOrEmpty(pair.Value.TargetTenantId))
                used.Add(pair.Value.TargetTenantId);
        }

        var eligible = new List<string>();
        var fresh = new List<string>();
        foreach (var pair in state.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                continue;
            if (string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            if (used.Contains(pair.Key))
                continue;
            int checkIn = pair.Value.CheckInDay;
            if (checkIn > 0 && checkIn > day - 1)
                continue;
            eligible.Add(pair.Key);
            if (checkIn == day - 1)
                fresh.Add(pair.Key);
        }
        if (eligible.Count == 0)
            return null;

        int seed = EventSelectionService.DeriveSeed(state.Seed, StableHash(chainId));
        TenantAbility preferred = ChainRuntimeCatalog.GetPreferredAbility(chainId);
        if (preferred != TenantAbility.None)
        {
            var preferredPool = new List<string>();
            for (int i = 0; i < eligible.Count; i++)
            {
                if (TenantAbilityResolver.ResolveAbility(eligible[i], candidates) == preferred)
                    preferredPool.Add(eligible[i]);
            }
            if (preferredPool.Count > 0)
            {
                preferredPool.Sort(StringComparer.Ordinal);
                return preferredPool[PickIndex(seed, "preferred:" + chainId, preferredPool.Count)];
            }
        }

        var pool = fresh.Count > 0 ? fresh : eligible;
        pool.Sort(StringComparer.Ordinal);
        return pool[PickIndex(seed, chainId, pool.Count)];
    }

    private static bool[] BuildChoiceLock(GameRunState state, ChainRunState chain, EventConfig config, ChainStepRuntimeSpec spec)
    {
        var locked = new bool[config.choices.Count];
        for (int i = 0; i < config.choices.Count; i++)
        {
            ChoiceOption choice = config.choices[i];
            if (choice == null || string.IsNullOrEmpty(choice.choiceId))
                continue;
            if (spec.RequireFlagsByChoice.TryGetValue(choice.choiceId, out string[] required)
                && !AreFlagsPresent(chain, required))
            {
                locked[i] = true;
                continue;
            }
            if (ChoiceRequiresVacantRoom(spec, choice.choiceId) && !ChainRoomState.HasVacantRoom(state))
                locked[i] = true;
        }
        return locked;
    }

    private static bool ChoiceRequiresVacantRoom(ChainStepRuntimeSpec spec, string choiceId)
    {
        if (spec == null || spec.ChoicesRequiringVacantRoom == null || string.IsNullOrEmpty(choiceId))
            return false;
        for (int i = 0; i < spec.ChoicesRequiringVacantRoom.Length; i++)
        {
            if (spec.ChoicesRequiringVacantRoom[i] == choiceId)
                return true;
        }
        return false;
    }

    private static bool AreFlagsPresent(ChainRunState chain, string[] required)
    {
        if (required == null || required.Length == 0)
            return true;
        if (chain == null || chain.Flags == null)
            return false;
        for (int i = 0; i < required.Length; i++)
        {
            if (!chain.Flags.Contains(required[i]))
                return false;
        }
        return true;
    }

    private static void FailChain(GameRunState state, StateReducer reducer, string chainId)
    {
        var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "ChainManager", "FailChain");
        set.Add(new FailChainChange(chainId));
        CommitResult result = reducer.TryCommit(state, set);
        if (!result.Succeeded)
            Debug.LogWarning($"[ChainManager] FailChain commit failed for chain '{chainId}'.");
    }

    private static void Notify(string text)
    {
        try
        {
            if (NoticeProvider != null)
                NoticeProvider(text);
            else
                Debug.Log($"[ChainManager] {text}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[ChainManager] notice failed: {exception.Message}");
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
            }
            return hash;
        }
    }

    /// <summary>Deterministic, platform-stable index in [0, count) from a derived seed.</summary>
    private static int PickIndex(int seed, string salt, int count)
    {
        if (count <= 1)
            return 0;
        int derived = EventSelectionService.DeriveSeed(seed, StableHash(salt));
        return (int)(((uint)derived & 0x7FFFFFFFu) % (uint)count);
    }

    /// <summary>Stable per-chain arbitration value (seed-derived, no dictionary order).</summary>
    private static uint DeriveTieBreak(int runSeed, string chainId)
    {
        int derived = EventSelectionService.DeriveSeed(runSeed, StableHash(chainId + "|arbitration"));
        return (uint)derived;
    }
}
