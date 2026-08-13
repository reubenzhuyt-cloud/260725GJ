using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public enum EventSettleResult { Settled, Pending }

public class EventEffectManager
{
    private string _lastFailureKey;

    public EventSettleResult TrySettle(GameRunState state, StateReducer reducer, EventProcessedData payload, out PlayerLogWriteDto effectSummary, out bool committed)
    {
        effectSummary = default;
        committed = false;
        if (payload == null || string.IsNullOrEmpty(payload.eventId))
            return EventSettleResult.Settled;
        if (state == null || reducer == null)
            return EventSettleResult.Pending;

        if (!EventSelectionService.HasUnresolvedOccurrence(state.EventHistory, payload.eventId))
            return EventSettleResult.Settled;

        string eventId = payload.eventId;
        string optionId = payload.optionId ?? string.Empty;

        var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "EventEffectManager", "ResolveEvent");
        set.Add(new ResolveEventHistoryChange(eventId, optionId));

        EventEffect[] effects = payload.effects;
        int effectCount = effects != null ? effects.Length : 0;
        PlayerLogWriteDto pendingEffectSummary = default;
        if (effectCount == 0)
        {
            Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: no effects to apply");
        }
        else
        {
            string ownerTenantId = payload.ownerTenantId;
            if (!string.IsNullOrEmpty(ownerTenantId) && !state.Tenants.ContainsKey(ownerTenantId))
                ownerTenantId = null;
            List<RunChange> changes = EventEffectExecutor.BuildChanges(
                effects, state, ownerTenantId, eventId, optionId, state.Day, RoomFloorRegistry.Instance);
            LogEffects(state, eventId, optionId, effects, changes, ownerTenantId);
            for (int i = 0; i < changes.Count; i++)
                set.Add(changes[i]);
            if (changes.Count > 0)
            {
                pendingEffectSummary = new PlayerLogWriteDto(
                    PlayerLogCategory.EffectSettlement,
                    state.Day,
                    state.Phase.Current,
                    "效果结算",
                    BuildEffectSummaryText(changes),
                    eventId);
            }
        }

        CommitResult result = reducer.TryCommit(state, set);
        if (result.Succeeded)
        {
            committed = true;
            effectSummary = pendingEffectSummary;
            return EventSettleResult.Settled;
        }

        var resolveOnly = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "EventEffectManager", "ResolveEventHistoryOnly");
        resolveOnly.Add(new ResolveEventHistoryChange(eventId, optionId));
        CommitResult degraded = reducer.TryCommit(state, resolveOnly);
        if (degraded.Succeeded)
        {
            committed = true;
            return EventSettleResult.Settled;
        }

        string failureKey = $"{eventId}|{optionId}|{state.StateVersion}";
        if (_lastFailureKey != failureKey)
        {
            _lastFailureKey = failureKey;
            Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: settle commit failed; pending retry");
        }
        return EventSettleResult.Pending;
    }

    public static bool TickBuffs(GameRunState state, StateReducer reducer, RoomFloorRegistry floorRegistry)
    {
        if (state == null || reducer == null)
            return false;
        if (state.Buffs == null || state.Buffs.Count == 0)
            return true;

        var changes = new List<RunChange>();
        var expired = new List<string>();
        var pendingBuffs = new List<PlayerLogWriteDto>();

        foreach (var pair in state.Buffs)
        {
            BuffRunState buff = pair.Value;
            if (buff == null)
                continue;
            if (buff.TickTiming != BuffTickTiming.Dawn)
                continue;
            if (buff.LastTickDay == state.Day)
                continue;

            List<string> targets = ResolveBuffTargets(state, buff, floorRegistry);
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                    changes.Add(new AdjustTenantErosionChange(targets[i], buff.ErosionPerTick));
            }
            if (!string.IsNullOrEmpty(buff.ResourceId) && state.Resources.ContainsKey(buff.ResourceId))
                changes.Add(new AdjustResourceChange(buff.ResourceId, buff.ResourceDeltaPerTick));

            int newRemaining = buff.RemainingTicks;
            if (buff.RemainingTicks > 0)
                newRemaining = buff.RemainingTicks - 1;
            changes.Add(new UpdateBuffTicksChange(buff.BuffId, newRemaining, state.Day));
            if (buff.RemainingTicks > 0 && newRemaining == 0)
                expired.Add(buff.BuffId);

            bool willExpire = buff.RemainingTicks > 0 && newRemaining == 0;
            pendingBuffs.Add(new PlayerLogWriteDto(
                PlayerLogCategory.BuffTick,
                state.Day,
                state.Phase.Current,
                "Buff 结算",
                willExpire
                    ? $"{buff.BuffId}：已到期移除"
                    : $"{buff.BuffId}：效果已生效 / 剩余 {newRemaining} 天",
                buff.BuffId));

            string targetsText = targets != null ? string.Join(",", targets) : "none";
            Debug.Log($"[EventEffectManager] buff={buff.BuffId} day={state.Day} target={buff.Target} tenants=[{targetsText}] erosion={buff.ErosionPerTick} resource={buff.ResourceId ?? string.Empty} resDelta={buff.ResourceDeltaPerTick} remaining={newRemaining}");
        }

        for (int i = 0; i < expired.Count; i++)
        {
            Debug.Log($"[EventEffectManager] buff={expired[i]} day={state.Day}: expired, removed");
            changes.Add(new RemoveBuffChange(expired[i]));
        }

        if (changes.Count == 0)
            return true;

        var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "EventEffectManager", "TickBuffs");
        for (int i = 0; i < changes.Count; i++)
            set.Add(changes[i]);
        CommitResult result = reducer.TryCommit(state, set);
        if (result.Succeeded)
        {
            for (int i = 0; i < pendingBuffs.Count; i++)
                PlayerLogManager.Record(state, pendingBuffs[i]);
        }
        return result.Succeeded;
    }

    private static List<string> ResolveBuffTargets(GameRunState state, BuffRunState buff, RoomFloorRegistry floorRegistry)
    {
        List<string> frozen = buff.TargetTenantIds;
        if (frozen != null && frozen.Count > 0)
        {
            var result = new List<string>(frozen.Count);
            for (int i = 0; i < frozen.Count; i++)
            {
                string tenantId = frozen[i];
                if (string.IsNullOrEmpty(tenantId))
                    continue;
                if (!state.Tenants.TryGetValue(tenantId, out TenantRunState tenant))
                    continue;
                if (string.IsNullOrEmpty(tenant.RoomId))
                    continue;
                result.Add(tenantId);
            }
            if (result.Count > 0)
                return result;
        }
        return EventEffectExecutor.ResolveTargets(buff.Target, state, buff.OwnerTenantId, buff.TargetParam, buff.TargetSeedIndex, floorRegistry);
    }

    private static void LogEffects(GameRunState state, string eventId, string optionId, EventEffect[] effects, List<RunChange> changes, string ownerTenantId)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            EventEffect effect = effects[i];
            if (effect == null)
                continue;

            if (effect.target == EffectTarget.OwnerTenant && string.IsNullOrEmpty(ownerTenantId))
            {
                Debug.Log($"[EventEffectManager] event={eventId} option={optionId} effect[{i}] type={effect.effectType} target={effect.target}: no owner tenant, 0 changes");
                continue;
            }

            List<string> targets = EventEffectExecutor.ResolveTargets(
                effect.target, state, ownerTenantId, effect.intValue, i, RoomFloorRegistry.Instance);
            if (targets == null || targets.Count == 0)
            {
                string reason = effect.target == EffectTarget.SameFloorTenants ? "no floor/owner info" : "no targets";
                Debug.Log($"[EventEffectManager] event={eventId} option={optionId} effect[{i}] type={effect.effectType} target={effect.target}: {reason}, 0 changes");
                continue;
            }
            Debug.Log($"[EventEffectManager] event={eventId} option={optionId} effect[{i}] type={effect.effectType} target={effect.target}: targets={targets.Count}");
        }

        if (changes == null)
            return;
        for (int i = 0; i < changes.Count; i++)
        {
            RunChange change = changes[i];
            if (change is AdjustTenantErosionChange erosion)
                Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: tenant={erosion.TenantId} erosionDelta={erosion.Delta}");
            else if (change is AdjustResourceChange resource)
                Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: resource={resource.ResourceId} delta={resource.Delta}");
            else if (change is AddBuffChange buff)
                Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: buff={buff.Value.BuffId} target={buff.Value.Target} remaining={buff.Value.RemainingTicks}");
        }
    }

    private static string BuildEffectSummaryText(List<RunChange> changes)
    {
        var parts = new List<string>();
        for (int i = 0; i < changes.Count; i++)
        {
            RunChange change = changes[i];
            if (change is AdjustTenantErosionChange erosion)
                parts.Add("效果已生效");
            else if (change is AdjustResourceChange resource)
                parts.Add($"{ResourceName(resource.ResourceId)} {resource.Delta:+#;-#;0}");
            else if (change is AddBuffChange buff)
                parts.Add($"状态「{buff.Value.BuffId}」{buff.Value.RemainingTicks} 天");
        }
        return string.Join("；", parts);
    }

    private static string ResourceName(string resourceId)
    {
        switch (resourceId)
        {
            case "food": return "食物";
            case "currency": return "货币";
            case "ingredients": return "食材";
            case "resources": return "物资";
            case "medicine": return "药品";
            default: return resourceId;
        }
    }
}
