using System;
using System.Collections.Generic;
using Hotel.Runtime;

public static class EventEffectExecutor
{
    public static List<RunChange> BuildChanges(
        EventEffect[] effects,
        GameRunState state,
        string ownerTenantId,
        string eventId,
        string optionId,
        int day,
        RoomFloorRegistry floorRegistry)
    {
        var changes = new List<RunChange>();
        if (effects == null || state == null)
            return changes;

        for (int i = 0; i < effects.Length; i++)
        {
            EventEffect effect = effects[i];
            if (effect == null)
                continue;

            if (effect.effectType == EffectType.ModifyTenantErosion)
                AddErosionChanges(effect, state, ownerTenantId, i, changes, floorRegistry);
            else if (effect.effectType == EffectType.ModifyResource)
                AddResourceChange(effect, state, changes);
            else if (effect.effectType == EffectType.ApplyBuff)
                AddBuffChange(effect, state, ownerTenantId, eventId, optionId, i, day, changes, floorRegistry);
        }

        return changes;
    }

    public static List<string> ResolveTargets(
        EffectTarget target,
        GameRunState state,
        string ownerTenantId,
        int intValue,
        int effectIndex,
        RoomFloorRegistry floorRegistry)
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
                return RandomAssigned(state, ownerTenantId, effectIndex, intValue);
            default:
                return OwnerTenant(state, ownerTenantId);
        }
    }

    private static void AddErosionChanges(EventEffect effect, GameRunState state, string ownerTenantId, int effectIndex, List<RunChange> changes, RoomFloorRegistry floorRegistry)
    {
        List<string> targets = ResolveTargets(effect.target, state, ownerTenantId, effect.intValue, effectIndex, floorRegistry);
        if (targets == null)
            return;
        for (int i = 0; i < targets.Count; i++)
            changes.Add(new AdjustTenantErosionChange(targets[i], effect.floatValue));
    }

    private static void AddResourceChange(EventEffect effect, GameRunState state, List<RunChange> changes)
    {
        if (string.IsNullOrEmpty(effect.stringValue))
            return;
        if (!state.Resources.ContainsKey(effect.stringValue))
            return;
        changes.Add(new AdjustResourceChange(effect.stringValue, SafeToInt(effect.floatValue)));
    }

    private static void AddBuffChange(EventEffect effect, GameRunState state, string ownerTenantId, string eventId, string optionId, int effectIndex, int day, List<RunChange> changes, RoomFloorRegistry floorRegistry)
    {
        List<string> targets = ResolveTargets(effect.target, state, ownerTenantId, effect.intValue, effectIndex, floorRegistry);
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
            LastTickDay = 0,
            TargetTenantIds = targetList
        }));
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

    private static List<string> RandomAssigned(GameRunState state, string ownerTenantId, int effectIndex, int requestedCount)
    {
        List<string> pool = AssignedTenants(state, ownerTenantId);
        if (pool.Count == 0)
            return pool;
        pool.Sort(StringComparer.Ordinal);

        int take = Math.Max(1, requestedCount);
        if (take > pool.Count)
            take = pool.Count;

        var rng = new Random(StableHash(ownerTenantId, effectIndex));
        var result = new List<string>(take);
        for (int i = 0; i < take; i++)
        {
            int index = rng.Next(pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return result;
    }

    private static int StableHash(string ownerTenantId, int effectIndex)
    {
        unchecked
        {
            int h = 17;
            if (ownerTenantId != null)
            {
                for (int i = 0; i < ownerTenantId.Length; i++)
                    h = h * 31 + ownerTenantId[i];
            }
            h = h * 31 + effectIndex;
            return h;
        }
    }

    private static int SafeToInt(float value)
    {
        if (value >= 2147483647f) return int.MaxValue;
        if (value <= -2147483648f) return int.MinValue;
        return Convert.ToInt32(value);
    }
}
