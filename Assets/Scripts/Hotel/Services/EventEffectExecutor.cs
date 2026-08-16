using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public static class EventEffectExecutor
{
    public static List<RunChange> BuildChanges(
        EventEffect[] effects,
        GameRunState state,
        string ownerTenantId,
        string eventId,
        string optionId,
        int day,
        RoomFloorRegistry floorRegistry,
        float negativeEffectMultiplier = 1f,
        IReadOnlyList<TenantReviewCandidateSO> candidates = null)
    {
        var changes = new List<RunChange>();
        if (state == null)
            return changes;

        // Stale-chain gate: a chain event whose parsed chain/step no longer matches
        // the persisted ChainRunState (advanced, completed/failed, or with its bound
        // target gone) must never apply effects without the matching advance. Return
        // before any effects are built so the settlement is atomic. Normal events
        // (non-chain event ids) are unaffected.
        if (ChainRuntimeCatalog.TryParseEvent(eventId, out string gateChainId, out int gateStep)
            && IsStaleChainEvent(state, gateChainId, gateStep))
            return changes;

        if (effects != null)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                EventEffect effect = effects[i];
                if (effect == null)
                {
                    Debug.LogWarning($"[EventEffectExecutor] effects[{i}] is null; skipped");
                    continue;
                }

                if (effect.effectType == EffectType.ModifyTenantErosion)
                    AddErosionChanges(effect, state, ownerTenantId, i, changes, floorRegistry, negativeEffectMultiplier, eventId);
                else if (effect.effectType == EffectType.ModifyResource)
                    AddResourceChange(effect, state, changes, negativeEffectMultiplier);
                else if (effect.effectType == EffectType.ApplyBuff)
                    AddBuffChange(effect, state, ownerTenantId, eventId, optionId, i, day, changes, floorRegistry);
                else if (effect.effectType == EffectType.GrantItem)
                    AddItemChange(effect, changes);
                else if (effect.effectType == EffectType.ChainSetFlag)
                    AddChainFlagChange(effect, eventId, changes);
                else if (effect.effectType == EffectType.ChainLockErosion)
                    AddChainLockErosion(effect, state, ownerTenantId, i, changes, floorRegistry, eventId);
                else if (effect.effectType == EffectType.ChainRemoveTenant)
                    AddChainRemoveTenant(effect, state, ownerTenantId, i, changes, floorRegistry, eventId);
                else if (effect.effectType == EffectType.ChainConditionalErosion)
                    AddChainConditionalErosion(effect, state, ownerTenantId, i, changes, floorRegistry, negativeEffectMultiplier, candidates, eventId);
                else if (effect.effectType == EffectType.ChainIdentifyYellowTenant)
                    AddChainIdentifyYellow(effect, eventId, state, changes);
                else if (effect.effectType == EffectType.ChainReserveChildRoom)
                    AddChainReserveChildRoom(effect, eventId, state, changes);
                else if (effect.effectType == EffectType.ChainReleaseChildRoom)
                    AddChainReleaseChildRoom(effect, eventId, state, changes);
                else
                    Debug.LogWarning($"[EventEffectExecutor] effects[{i}] unsupported effectType={effect.effectType}; skipped");
            }
        }

        AddChainAdvance(state, eventId, changes);
        return changes;
    }

    public static List<string> ResolveTargets(
        EffectTarget target,
        GameRunState state,
        string ownerTenantId,
        int intValue,
        int effectIndex,
        RoomFloorRegistry floorRegistry,
        string eventContext = null)
    {
        switch (target)
        {
            case EffectTarget.AllAssignedTenants:
                return AssignedTenants(state, null);
            case EffectTarget.SameRoomOtherTenants:
                return SameRoomOtherTenants(state, ownerTenantId);
            case EffectTarget.SameFloorTenants:
                return SameFloorTenants(state, ownerTenantId, floorRegistry);
            case EffectTarget.ByPlayerFlag:
                return ByPlayerFlag(state, intValue);
            case EffectTarget.RandomAssignedTenants:
                return RandomAssigned(state, ownerTenantId, effectIndex, intValue, eventContext);
            default:
                return OwnerTenant(state, ownerTenantId);
        }
    }

    private static void AddErosionChanges(EventEffect effect, GameRunState state, string ownerTenantId, int effectIndex, List<RunChange> changes, RoomFloorRegistry floorRegistry, float negativeEffectMultiplier, string eventId)
    {
        List<string> targets = ResolveTargets(effect.target, state, ownerTenantId, effect.intValue, effectIndex, floorRegistry, eventId);
        if (targets == null)
            return;
        float delta = effect.floatValue > 0f
            ? effect.floatValue * Mathf.Clamp01(negativeEffectMultiplier)
            : effect.floatValue;
        for (int i = 0; i < targets.Count; i++)
            changes.Add(new AdjustTenantErosionChange(targets[i], delta));
    }

    private static void AddResourceChange(EventEffect effect, GameRunState state, List<RunChange> changes, float negativeEffectMultiplier)
    {
        if (string.IsNullOrEmpty(effect.stringValue))
        {
            Debug.LogWarning("[EventEffectExecutor] ModifyResource effect has empty resource id; skipped");
            return;
        }
        if (!state.Resources.ContainsKey(effect.stringValue))
        {
            Debug.LogWarning($"[EventEffectExecutor] ModifyResource effect references unknown resource '{effect.stringValue}'; skipped");
            return;
        }
        float delta = effect.floatValue < 0f
            ? effect.floatValue * Mathf.Clamp01(negativeEffectMultiplier)
            : effect.floatValue;
        changes.Add(new AdjustResourceChange(effect.stringValue, SafeToInt(delta)));
    }

    private static void AddItemChange(EventEffect effect, List<RunChange> changes)
    {
        if (string.IsNullOrEmpty(effect.stringValue))
        {
            Debug.LogWarning("[EventEffectExecutor] GrantItem effect has empty item id; skipped");
            return;
        }
        int delta = SafeToInt(effect.floatValue);
        if (delta <= 0)
        {
            Debug.LogWarning($"[EventEffectExecutor] GrantItem effect for item '{effect.stringValue}' has non-positive quantity {delta}; skipped");
            return;
        }
        changes.Add(new AdjustItemChange(effect.stringValue, delta));
    }

    private static void AddBuffChange(EventEffect effect, GameRunState state, string ownerTenantId, string eventId, string optionId, int effectIndex, int day, List<RunChange> changes, RoomFloorRegistry floorRegistry)
    {
        List<string> targets = ResolveTargets(effect.target, state, ownerTenantId, effect.intValue, effectIndex, floorRegistry, eventId);
        var targetList = new List<string>();
        if (targets != null)
            targetList.AddRange(targets);

        string buffId = string.Format(
            "{0}|{1}|{2}|{3}|{4}",
            eventId ?? "?",
            optionId ?? "?",
            effectIndex,
            ownerTenantId ?? "?",
            day);

        if (!string.IsNullOrEmpty(effect.stringValue)
            && (state.Resources == null || !state.Resources.ContainsKey(effect.stringValue)))
        {
            Debug.LogWarning($"[EventEffectExecutor] ApplyBuff effect references unknown resource '{effect.stringValue}'; buff created without resource tick.");
        }

        changes.Add(new AddBuffChange(new BuffRunState
        {
            BuffId = buffId,
            SourceEventId = eventId,
            OwnerTenantId = ownerTenantId,
            Target = effect.target,
            ErosionPerTick = effect.floatValue,
            ResourceId = effect.stringValue,
            ResourceDeltaPerTick = effect.intValue,
            TargetParam = effect.intValue,
            TargetSeedIndex = effectIndex,
            TickTiming = BuffTickTiming.Dawn,
            RemainingTicks = effect.durationTicks > 0 ? effect.durationTicks : -1,
            StartDay = day,
            LastTickDay = day,
            TargetTenantIds = targetList
        }));
    }

    // ------------------------------------------------------------------ chain

    private static void AddChainAdvance(GameRunState state, string eventId, List<RunChange> changes)
    {
        if (state.Chains == null)
            return;
        if (!ChainRuntimeCatalog.TryParseEvent(eventId, out string chainId, out int step))
            return;
        if (!state.Chains.TryGetValue(chainId, out ChainRunState chain))
            return;
        if (chain.Completed || chain.Failed)
            return;
        if (chain.NextStepToPresent != step)
            return;
        int nextStep = step + 1;
        bool completed = !ChainRuntimeCatalog.HasStep(chainId, nextStep);
        // A step that removes the chain's bound tenant (vanishingguest/walldiary
        // finals) cannot leave a pending next step bound to an evicted tenant:
        // the chain must complete in this same atomic change set.
        if (!completed && ChainTargetRemovedInSet(chain, changes))
            completed = true;
        // For a non-completing advance, persist the exact next due day derived from
        // the persisted first trigger day plus the authored step offset. On completion
        // there is no next step, so persist NextDueDay = 0 (no invented due day); the
        // first trigger day remains stored for narrative history.
        int firstTriggerDay = chain.FirstTriggerDay > 0 ? chain.FirstTriggerDay
            : chain.StartDay > 0 ? chain.StartDay : 1;
        int nextDueDay = completed
            ? 0
            : firstTriggerDay + ChainRuntimeCatalog.GetStepDayOffset(chainId, nextStep);
        changes.Add(new AdvanceChainStepChange(chainId, nextStep, completed, nextDueDay));
    }

    private static bool ChainTargetRemovedInSet(ChainRunState chain, List<RunChange> changes)
    {
        if (chain == null || string.IsNullOrEmpty(chain.TargetTenantId))
            return false;
        for (int i = 0; i < changes.Count; i++)
        {
            if (changes[i] is EvictTenantChange evict && evict.TenantId == chain.TargetTenantId)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when a chain event is stale: the persisted chain is missing, already
    /// completed/failed, no longer awaiting this exact step, or has lost its bound
    /// target tenant. BuildChanges drops the whole settlement in that case.
    /// </summary>
    private static bool IsStaleChainEvent(GameRunState state, string chainId, int step)
    {
        if (state.Chains == null || !state.Chains.TryGetValue(chainId, out ChainRunState chain))
            return true;
        if (chain.Completed || chain.Failed)
            return true;
        if (chain.NextStepToPresent != step)
            return true;
        if (string.IsNullOrEmpty(chain.TargetTenantId) || !state.Tenants.ContainsKey(chain.TargetTenantId))
            return true;
        return false;
    }

    private static void AddChainFlagChange(EventEffect effect, string eventId, List<RunChange> changes)
    {
        if (string.IsNullOrEmpty(effect.stringValue))
        {
            Debug.LogWarning("[EventEffectExecutor] ChainSetFlag effect has empty flag; skipped");
            return;
        }
        if (!ChainRuntimeCatalog.TryParseEvent(eventId, out string chainId, out _))
            return;
        changes.Add(new SetChainFlagChange(chainId, effect.stringValue));
    }

    private static void AddChainLockErosion(EventEffect effect, GameRunState state, string ownerTenantId, int effectIndex, List<RunChange> changes, RoomFloorRegistry floorRegistry, string eventId)
    {
        List<string> targets = ResolveTargets(effect.target, state, ownerTenantId, effect.intValue, effectIndex, floorRegistry, eventId);
        if (targets == null)
            return;
        for (int i = 0; i < targets.Count; i++)
        {
            if (state.Tenants.ContainsKey(targets[i]))
                changes.Add(new LockTenantErosionChange(targets[i], effect.floatValue));
        }
    }

    private static void AddChainRemoveTenant(EventEffect effect, GameRunState state, string ownerTenantId, int effectIndex, List<RunChange> changes, RoomFloorRegistry floorRegistry, string eventId)
    {
        List<string> targets = ResolveTargets(effect.target, state, ownerTenantId, effect.intValue, effectIndex, floorRegistry, eventId);
        if (targets == null)
            return;
        for (int i = 0; i < targets.Count; i++)
        {
            if (state.Tenants.ContainsKey(targets[i]))
                changes.Add(new EvictTenantChange(targets[i]));
        }
    }

    private static void AddChainConditionalErosion(EventEffect effect, GameRunState state, string ownerTenantId, int effectIndex, List<RunChange> changes, RoomFloorRegistry floorRegistry, float negativeEffectMultiplier, IReadOnlyList<TenantReviewCandidateSO> candidates, string eventId)
    {
        float delta = effect.floatValue > 0f
            ? effect.floatValue * Mathf.Clamp01(negativeEffectMultiplier)
            : effect.floatValue;

        switch (effect.conditionKind)
        {
            case ChainConditionKind.AnyTenantErosionAbove:
            {
                if (!AnyAssignedErosionAbove(state, effect.intValue))
                    return;
                List<string> targets = ResolveTargets(EffectTarget.AllAssignedTenants, state, null, 0, effectIndex, floorRegistry, eventId);
                AddErosionDelta(targets, state, delta, changes);
                break;
            }
            case ChainConditionKind.AbilityAny:
            {
                if (!AnyOwnedAbility(state, candidates, effect.stringValue))
                    return;
                List<string> targets = ResolveTargets(EffectTarget.AllAssignedTenants, state, null, 0, effectIndex, floorRegistry, eventId);
                AddErosionDelta(targets, state, delta, changes);
                break;
            }
            case ChainConditionKind.IdentifiedYellow:
            {
                string tenantId = ResolveIdentifiedYellow(state, eventId);
                if (tenantId == null)
                    return;
                if (state.Tenants.ContainsKey(tenantId))
                    changes.Add(new AdjustTenantErosionChange(tenantId, delta));
                break;
            }
        }
    }

    private static void AddChainIdentifyYellow(EventEffect effect, string eventId, GameRunState state, List<RunChange> changes)
    {
        if (!ChainRuntimeCatalog.TryParseEvent(eventId, out string chainId, out int step))
            return;
        string tenantId = PickYellowTenant(state, chainId, step);
        if (tenantId == null)
        {
            Debug.Log("[EventEffectExecutor] ChainIdentifyYellowTenant: no yellow assigned tenant; no flag stored.");
            return;
        }
        changes.Add(new SetChainFlagChange(chainId, ChainRuntimeCatalog.IdentifiedYellowPrefix + tenantId));
    }

    private static void AddChainReserveChildRoom(EventEffect effect, string eventId, GameRunState state, List<RunChange> changes)
    {
        if (!ChainRuntimeCatalog.TryParseEvent(eventId, out string chainId, out int step))
            return;
        int seed = EventSelectionService.DeriveSeed(state.Seed, StableHash((chainId ?? string.Empty) + "|" + step + "|childroom"));
        string roomId = ChainRoomState.PickVacantRoom(state, seed);
        if (roomId == null)
        {
            Debug.LogWarning("[EventEffectExecutor] ChainReserveChildRoom: no vacant room to reserve; skipped.");
            return;
        }
        changes.Add(new AddRoomOccupantChange(roomId, ChainRuntimeCatalog.ChildOccupantId));
        changes.Add(new SetChainFlagChange(chainId, ChainRuntimeCatalog.ChildRoomPrefix + roomId));
    }

    private static void AddChainReleaseChildRoom(EventEffect effect, string eventId, GameRunState state, List<RunChange> changes)
    {
        if (!ChainRuntimeCatalog.TryParseEvent(eventId, out string chainId, out _))
            return;
        if (!state.Chains.TryGetValue(chainId, out ChainRunState chain) || chain.Flags == null)
            return;
        string roomId = null;
        for (int i = 0; i < chain.Flags.Count; i++)
        {
            if (chain.Flags[i] != null && chain.Flags[i].StartsWith(ChainRuntimeCatalog.ChildRoomPrefix, StringComparison.Ordinal))
            {
                roomId = chain.Flags[i].Substring(ChainRuntimeCatalog.ChildRoomPrefix.Length);
                break;
            }
        }
        if (string.IsNullOrEmpty(roomId))
            return;
        changes.Add(new RemoveRoomOccupantChange(roomId, ChainRuntimeCatalog.ChildOccupantId));
    }

    private static void AddErosionDelta(List<string> targets, GameRunState state, float delta, List<RunChange> changes)
    {
        if (targets == null)
            return;
        for (int i = 0; i < targets.Count; i++)
        {
            if (state.Tenants.ContainsKey(targets[i]))
                changes.Add(new AdjustTenantErosionChange(targets[i], delta));
        }
    }

    private static bool AnyAssignedErosionAbove(GameRunState state, int threshold)
    {
        if (state.Tenants == null)
            return false;
        foreach (var pair in state.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            if (pair.Value.TrueErosion > threshold)
                return true;
        }
        return false;
    }

    private static bool AnyOwnedAbility(GameRunState state, IReadOnlyList<TenantReviewCandidateSO> candidates, string abilityList)
    {
        if (string.IsNullOrEmpty(abilityList))
            return false;
        var wanted = new HashSet<TenantAbility>();
        string[] names = abilityList.Split(',');
        for (int i = 0; i < names.Length; i++)
        {
            if (System.Enum.TryParse(names[i].Trim(), true, out TenantAbility ability) && ability != TenantAbility.None)
                wanted.Add(ability);
        }
        if (wanted.Count == 0)
            return false;
        HashSet<TenantAbility> owned = TenantAbilityResolver.GetOwnedAbilities(state, candidates);
        foreach (TenantAbility ability in wanted)
        {
            if (owned.Contains(ability))
                return true;
        }
        return false;
    }

    private static string PickYellowTenant(GameRunState state, string chainId, int step)
    {
        if (state.Tenants == null)
            return null;
        var yellow = new List<string>();
        foreach (var pair in state.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                continue;
            if (string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            if (EventConditionEvaluator.ColorOf(pair.Value.TrueErosion) != EventConditionEvaluator.ErosionColor.Yellow)
                continue;
            yellow.Add(pair.Key);
        }
        if (yellow.Count == 0)
            return null;
        // Deterministic selection: explicit ordinal sort before picking, seeded by
        // the run seed plus the chain/step context so the same state always picks
        // the same tenant. The chosen tenant is persisted as a chain flag, so later
        // steps never re-pick or destabilize it.
        yellow.Sort(StringComparer.Ordinal);
        int seed = EventSelectionService.DeriveSeed(state.Seed, StableHash((chainId ?? string.Empty) + "|" + step + "|yellow"));
        return yellow[(int)(((uint)seed & 0x7FFFFFFFu) % (uint)yellow.Count)];
    }

    private static string ResolveIdentifiedYellow(GameRunState state, string eventId)
    {
        if (state.Chains == null)
            return null;
        if (!ChainRuntimeCatalog.TryParseEvent(eventId, out string chainId, out _))
            return null;
        if (!state.Chains.TryGetValue(chainId, out ChainRunState chain) || chain.Flags == null)
            return null;
        for (int i = 0; i < chain.Flags.Count; i++)
        {
            string flag = chain.Flags[i];
            if (flag != null && flag.StartsWith(ChainRuntimeCatalog.IdentifiedYellowPrefix, StringComparison.Ordinal))
                return flag.Substring(ChainRuntimeCatalog.IdentifiedYellowPrefix.Length);
        }
        return null;
    }

    private static List<string> OwnerTenant(GameRunState state, string ownerTenantId)
    {
        if (string.IsNullOrEmpty(ownerTenantId) || !state.Tenants.ContainsKey(ownerTenantId))
            return null;
        return new List<string> { ownerTenantId };
    }

    private static List<string> AssignedTenants(GameRunState state, string excludeTenantId)
    {
        var result = new List<string>();
        foreach (var pair in state.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                continue;
            if (string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            if (excludeTenantId != null && pair.Key == excludeTenantId)
                continue;
            result.Add(pair.Key);
        }
        return result;
    }

    private static List<string> SameRoomOtherTenants(GameRunState state, string ownerTenantId)
    {
        if (string.IsNullOrEmpty(ownerTenantId))
            return null;
        if (!state.Tenants.TryGetValue(ownerTenantId, out TenantRunState owner))
            return null;
        if (string.IsNullOrEmpty(owner.RoomId))
            return null;
        if (!state.Rooms.TryGetValue(owner.RoomId, out RoomRunState room))
            return null;

        var result = new List<string>();
        if (room.OccupantIds == null)
            return result;

        var seen = new HashSet<string>();
        for (int i = 0; i < room.OccupantIds.Count; i++)
        {
            string occupantId = room.OccupantIds[i];
            if (string.IsNullOrEmpty(occupantId))
                continue;
            if (occupantId == ownerTenantId)
                continue;
            if (!state.Tenants.ContainsKey(occupantId))
                continue;
            if (!seen.Add(occupantId))
                continue;
            result.Add(occupantId);
        }
        return result;
    }

    private static List<string> SameFloorTenants(GameRunState state, string ownerTenantId, RoomFloorRegistry floorRegistry)
    {
        if (floorRegistry == null || string.IsNullOrEmpty(ownerTenantId))
            return null;
        if (!state.Tenants.TryGetValue(ownerTenantId, out TenantRunState owner))
            return null;
        if (string.IsNullOrEmpty(owner.RoomId))
            return null;
        if (!TryGetFloorForRoom(owner.RoomId, floorRegistry, out int ownerFloor))
            return null;

        var result = new List<string>();
        foreach (var pair in state.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                continue;
            if (string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            if (TryGetFloorForRoom(pair.Value.RoomId, floorRegistry, out int floor) && floor == ownerFloor)
                result.Add(pair.Key);
        }
        return result;
    }

    private static bool TryGetFloorForRoom(string roomId, RoomFloorRegistry floorRegistry, out int floor)
    {
        floor = 0;
        if (floorRegistry == null || string.IsNullOrEmpty(roomId))
            return false;
        IReadOnlyList<RoomTenantAvatarSlot> slots = RoomTenantAvatarSlot.GetSlotsForRoom(roomId);
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && floorRegistry.TryGetFloorForSlot(slots[i], out floor))
                return true;
        }
        return false;
    }

    private static List<string> ByPlayerFlag(GameRunState state, int flag)
    {
        var result = new List<string>();
        foreach (var pair in state.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                continue;
            if (string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            if (pair.Value.PlayerFlag != flag)
                continue;
            result.Add(pair.Key);
        }
        return result;
    }

    private static List<string> RandomAssigned(GameRunState state, string ownerTenantId, int effectIndex, int requestedCount, string eventContext = null)
    {
        List<string> pool = AssignedTenants(state, ownerTenantId);
        if (pool.Count == 0)
            return pool;
        pool.Sort(StringComparer.Ordinal);

        int take = Math.Max(1, requestedCount);
        if (take > pool.Count)
            take = pool.Count;

        // Platform-stable multi-draw: derive one context seed from the run seed plus
        // the event/owner/index context, then a distinct child seed per draw ordinal.
        // Drawing without replacement (RemoveAt) keeps targets unique.
        string context = (eventContext ?? string.Empty) + "|" + (ownerTenantId ?? string.Empty) + "|" + effectIndex;
        int baseSeed = EventSelectionService.DeriveSeed(state.Seed, StableHash(context));
        var result = new List<string>(take);
        for (int i = 0; i < take; i++)
        {
            if (pool.Count == 0)
                break;
            int drawSeed = EventSelectionService.DeriveSeed(baseSeed, StableHash("draw|" + i));
            int index = (int)(((uint)drawSeed & 0x7FFFFFFFu) % (uint)pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return result;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int h = 17;
            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                    h = h * 31 + value[i];
            }
            return h;
        }
    }

    public static int SafeToInt(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            Debug.LogWarning($"[EventEffectExecutor] SafeToInt: non-finite value {value}; using 0");
            return 0;
        }
        if (value >= 2147483647f) return int.MaxValue;
        if (value <= -2147483648f) return int.MinValue;
        return Convert.ToInt32(value);
    }
}
