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
/// Narrative chains are strictly serialized (at most one active chain at a time).
///
/// Scheduling rules:
///  - The very first chain starts deterministically on Day 3 (FirstTriggerDay = 3).
///  - If no chain is active:
///      * On Day 3 (or Day >= 3 if no chain has ever been started), pick and start a new chain.
///      * When a chain completes or fails, a cool-down is calculated:
///        NextChainAvailableDay = completedDay + Random(1, 3) (1 to 3 days interval, deterministic derived from run seed).
///      * On subsequent days (Day >= NextChainAvailableDay), when no active chain exists,
///        pick the next unstarted/uncompleted chain using tenant attribute matching / weighting,
///        and start it with FirstTriggerDay = currentDay.
///  - While a chain is active:
///      * Its steps advance strictly according to ChainStepRuntimeSpec.DayOffsetAfterFirstEvent.
///      * Only the currently active chain can inject steps.
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

        string activeChainId = GetActiveChainId(state);
        if (string.IsNullOrEmpty(activeChainId))
            return result;

        ChainRunState chain = state.Chains[activeChainId];
        if (chain == null || chain.Completed || chain.Failed)
            return result;

        ChainStepRuntimeSpec spec = ChainRuntimeCatalog.GetStep(activeChainId, chain.NextStepToPresent);
        if (spec == null)
            return result;

        if (string.IsNullOrEmpty(chain.TargetTenantId) || !state.Tenants.ContainsKey(chain.TargetTenantId))
        {
            FailChain(state, reducer, activeChainId);
            return result;
        }

        if (!AreFlagsPresent(chain, spec.RequireFlags))
            return result;

        if (chain.NextDueDay < 1 || chain.NextDueDay > day)
            return result;

        EventConfig config = ChainRuntimeCatalog.FindEvent(activeChainId, chain.NextStepToPresent, catalog);
        if (config == null)
            return result;
        if (EventSelectionService.HasOccurred(state.EventHistory, config.eventId))
            return result;

        result.Add(config);
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

    /// <summary>Returns the chainId of the currently active (in-progress) chain, or null if none.</summary>
    public static string GetActiveChainId(GameRunState state)
    {
        if (state == null || state.Chains == null || state.Chains.Count == 0)
            return null;

        foreach (var pair in state.Chains)
        {
            ChainRunState chain = pair.Value;
            if (chain != null && !chain.Completed && !chain.Failed)
                return pair.Key;
        }

        return null;
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
        if (state == null || reducer == null)
            return;

        // 1. Strict Single Active Chain Guard
        if (state.Chains != null)
        {
            foreach (var pair in state.Chains)
            {
                ChainRunState chain = pair.Value;
                if (chain != null && !chain.Completed && !chain.Failed)
                    return;
            }
        }

        // Count ended chains and find the latest end day
        int endedCount = 0;
        int lastChainEndDay = 0;
        bool hasAnyEndedChain = false;

        if (state.Chains != null)
        {
            foreach (var pair in state.Chains)
            {
                ChainRunState chain = pair.Value;
                if (chain != null && (chain.Completed || chain.Failed))
                {
                    endedCount++;
                    hasAnyEndedChain = true;
                    int endDay = GetChainEndDay(state, chain);
                    if (endDay > lastChainEndDay)
                        lastChainEndDay = endDay;
                }
            }
        }

        int targetFirstTriggerDay;
        int targetNextDueDay;

        if (!hasAnyEndedChain)
        {
            // 2. Fixed First Chain on Day 3
            if (day < 3)
                return;

            targetFirstTriggerDay = 3;
            targetNextDueDay = 3;
        }
        else
        {
            // 3. 1-3 Days Cooldown Between Chains
            int cooldownDerived = EventSelectionService.DeriveSeed(state.Seed, StableHash("chain_cooldown_" + endedCount));
            int cooldown = 1 + (int)(((uint)cooldownDerived & 0x7FFFFFFFu) % 3u);

            if (day < lastChainEndDay + cooldown)
                return;

            targetFirstTriggerDay = day;
            targetNextDueDay = day;
        }

        // 4. Ability-Weighted Chain & Tenant Selection
        var candidatePairs = GetEligibleChainTenantPairs(state, day, candidates);
        if (candidatePairs.Count == 0)
            return;

        int selectionSeed = EventSelectionService.DeriveSeed(state.Seed, StableHash("chain_select_day_" + day + "_ended_" + endedCount));
        ChainCandidate selected = SelectBestChainCandidate(candidatePairs, selectionSeed);
        if (selected == null || string.IsNullOrEmpty(selected.ChainId) || string.IsNullOrEmpty(selected.TenantId))
            return;

        // 5. State Reducer Safety via AuthorizedChangeSet.Coordinator("ChainManager", ...)
        var set = AuthorizedChangeSet.Coordinator(state.RunId, state.StateVersion, "StartChain");
        set.Add(new StartChainChange(selected.ChainId, selected.TenantId, day, targetFirstTriggerDay, targetNextDueDay));
        CommitResult result = reducer.TryCommit(state, set);
        if (!result.Succeeded)
            Debug.LogWarning($"[ChainManager] StartChain commit failed for chain '{selected.ChainId}'.");
    }

    private sealed class ChainCandidate
    {
        public string ChainId;
        public string TenantId;
        public int Weight;
    }

    private static List<ChainCandidate> GetEligibleChainTenantPairs(
        GameRunState state,
        int day,
        IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        var result = new List<ChainCandidate>();
        if (state == null)
            return result;

        IReadOnlyList<string> chainIds = ChainRuntimeCatalog.ChainIds;
        if (chainIds == null || chainIds.Count == 0)
            return result;

        for (int c = 0; c < chainIds.Count; c++)
        {
            string chainId = chainIds[c];
            if (string.IsNullOrEmpty(chainId))
                continue;
            if (state.Chains != null && state.Chains.ContainsKey(chainId))
                continue;

            string tenantId = SelectTargetTenant(state, chainId, day, candidates, out int matchWeight);
            if (string.IsNullOrEmpty(tenantId))
                continue;

            result.Add(new ChainCandidate
            {
                ChainId = chainId,
                TenantId = tenantId,
                Weight = matchWeight
            });
        }

        return result;
    }

    private static ChainCandidate SelectBestChainCandidate(List<ChainCandidate> candidatePairs, int seed)
    {
        if (candidatePairs == null || candidatePairs.Count == 0)
            return null;
        if (candidatePairs.Count == 1)
            return candidatePairs[0];

        // Sort candidates deterministically before weighted roll
        candidatePairs.Sort((a, b) =>
        {
            int cmp = StringComparer.Ordinal.Compare(a.ChainId, b.ChainId);
            if (cmp != 0) return cmp;
            return StringComparer.Ordinal.Compare(a.TenantId, b.TenantId);
        });

        int totalWeight = 0;
        for (int i = 0; i < candidatePairs.Count; i++)
        {
            int w = candidatePairs[i].Weight > 0 ? candidatePairs[i].Weight : 1;
            totalWeight += w;
        }

        if (totalWeight <= 0)
            totalWeight = candidatePairs.Count;

        int roll = (int)(((uint)seed & 0x7FFFFFFFu) % (uint)totalWeight);
        int accumulated = 0;
        for (int i = 0; i < candidatePairs.Count; i++)
        {
            int w = candidatePairs[i].Weight > 0 ? candidatePairs[i].Weight : 1;
            accumulated += w;
            if (roll < accumulated)
                return candidatePairs[i];
        }

        return candidatePairs[0];
    }

    private static int GetChainEndDay(GameRunState state, ChainRunState chain)
    {
        if (chain == null)
            return 0;

        int lastDay = 0;
        if (state.EventHistory != null)
        {
            for (int i = 0; i < state.EventHistory.Count; i++)
            {
                EventHistoryRecord record = state.EventHistory[i];
                if (record == null)
                    continue;

                if (ChainRuntimeCatalog.TryParseEvent(record.EventId, out string chainId, out _)
                    && chainId == chain.ChainId)
                {
                    if (record.Day > lastDay)
                        lastDay = record.Day;
                }
            }
        }

        if (lastDay > 0)
            return lastDay;

        if (chain.FirstTriggerDay > 0)
            return chain.FirstTriggerDay;
        if (chain.StartDay > 0)
            return chain.StartDay;

        return 0;
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
            var set = AuthorizedChangeSet.Coordinator(state.RunId, state.StateVersion, "MigrateChainSchedule");
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
        return SelectTargetTenant(state, chainId, day, candidates, out _);
    }

    private static string SelectTargetTenant(
        GameRunState state,
        string chainId,
        int day,
        IReadOnlyList<TenantReviewCandidateSO> candidates,
        out int matchWeight)
    {
        matchWeight = 1;
        if (state.Tenants == null || state.Tenants.Count == 0)
            return null;

        var used = new HashSet<string>();
        if (state.Chains != null)
        {
            foreach (var pair in state.Chains)
            {
                if (pair.Value != null && !string.IsNullOrEmpty(pair.Value.TargetTenantId))
                    used.Add(pair.Value.TargetTenantId);
            }
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
                matchWeight = 10;
                return preferredPool[PickIndex(seed, "preferred:" + chainId, preferredPool.Count)];
            }
        }

        var pool = fresh.Count > 0 ? fresh : eligible;
        pool.Sort(StringComparer.Ordinal);
        matchWeight = 1;
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
        var set = AuthorizedChangeSet.Coordinator(state.RunId, state.StateVersion, "FailChain");
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
