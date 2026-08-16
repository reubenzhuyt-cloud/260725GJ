using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public enum EventSettleResult { Settled, Pending, Rejected }

public class EventEffectManager
{
    private string _lastFailureKey;

    public EventSettleResult TrySettle(GameRunState state, StateReducer reducer, EventProcessedData payload, out PlayerLogWriteDto effectSummary, out bool committed, out string effectNoticeText, int logDay = 0, HotelPhase? logPhase = null, IReadOnlyList<TenantReviewCandidateSO> candidates = null)
    {
        effectSummary = default;
        committed = false;
        effectNoticeText = null;
        if (payload == null || string.IsNullOrEmpty(payload.eventId))
            return EventSettleResult.Pending;
        if (state == null || reducer == null)
            return EventSettleResult.Pending;

        if (!EventSelectionService.HasUnresolvedOccurrence(state.EventHistory, payload.eventId))
            return EventSettleResult.Settled;

        string eventId = payload.eventId;
        string optionId = payload.optionId ?? string.Empty;

        if (!EventAffordability.CanAfford(payload.effects, state))
        {
            Debug.LogWarning($"[EventEffectManager] event={eventId} option={optionId}: cannot afford resource cost of selected option; rejecting settlement.");
            return EventSettleResult.Rejected;
        }

        if (payload.requiredTags != null && payload.requiredTags.Length > 0
            && !TenantAbilityResolver.HasAllRequiredTags(payload.requiredTags, state, candidates))
        {
            string reason = candidates == null
                ? "tenant candidate config unavailable"
                : "missing required tenant ability";
            Debug.LogWarning($"[EventEffectManager] event={eventId} option={optionId}: {reason}; rejecting settlement.");
            return EventSettleResult.Rejected;
        }

        if (ChainRuntimeCatalog.TryParseEvent(eventId, out _, out _)
            && !ChainManager.IsOptionAvailable(state, eventId, optionId, candidates))
        {
            Debug.LogWarning($"[EventEffectManager] event={eventId} option={optionId}: chain option not available in current state; rejecting settlement.");
            return EventSettleResult.Rejected;
        }

        var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "EventEffectManager", "ResolveEvent");
        set.Add(new ResolveEventHistoryChange(eventId, optionId));

        EventEffect[] effects = payload.effects;
        int effectCount = effects != null ? effects.Length : 0;
        PlayerLogWriteDto pendingEffectSummary = default;
        List<RunChange> changes = null;
        string ownerTenantId = payload.ownerTenantId;
        if (!string.IsNullOrEmpty(ownerTenantId) && !state.Tenants.ContainsKey(ownerTenantId))
            ownerTenantId = null;
        float negativeEffectMultiplier = state.Phase.Current == HotelPhase.Night
            ? JobSettlementService.GetNightEventLossMultiplier(state, candidates)
            : 1f;
        changes = EventEffectExecutor.BuildChanges(
            effects,
            state,
            ownerTenantId,
            eventId,
            optionId,
            state.Day,
            RoomFloorRegistry.Instance,
            negativeEffectMultiplier,
            candidates);
        if (effectCount > 0)
        {
            LogEffects(state, eventId, optionId, effects, changes, ownerTenantId);
        }
        else
        {
            Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: no effects to apply");
        }
        for (int i = 0; i < changes.Count; i++)
            set.Add(changes[i]);
        if (changes.Count > 0)
        {
            int summaryDay = logDay > 0 ? logDay : state.Day;
            HotelPhase summaryPhase = logPhase.HasValue ? logPhase.Value : state.Phase.Current;
            pendingEffectSummary = new PlayerLogWriteDto(
                PlayerLogCategory.EffectSettlement,
                summaryDay,
                summaryPhase,
                "效果结算",
                BuildEffectSummaryText(changes),
                eventId);
        }

        CommitResult result = reducer.TryCommit(state, set);
        if (result.Succeeded)
        {
            committed = true;
            effectSummary = pendingEffectSummary;
            effectNoticeText = NoticeTextFormatter.BuildEventNotice(
                effects, changes, state, ownerTenantId, negativeEffectMultiplier);
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

    public static bool TickBuffs(GameRunState state, StateReducer reducer, RoomFloorRegistry floorRegistry, out Dictionary<string, int> settledResourceDeltas)
    {
        settledResourceDeltas = new Dictionary<string, int>(StringComparer.Ordinal);
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
            if (string.IsNullOrEmpty(buff.BuffId))
                continue;
            if (buff.TickTiming != BuffTickTiming.Dawn)
                continue;
            if (buff.LastTickDay == state.Day)
                continue;

            List<string> targets = ResolveBuffTargets(state, buff, floorRegistry);
            int validTargetCount = 0;
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    string tenantId = targets[i];
                    if (string.IsNullOrEmpty(tenantId) || !state.Tenants.ContainsKey(tenantId))
                        continue;
                    changes.Add(new AdjustTenantErosionChange(tenantId, buff.ErosionPerTick));
                    validTargetCount++;
                }
            }

            bool hasTenantEffect = buff.ErosionPerTick != 0f
                || (buff.TargetTenantIds != null && buff.TargetTenantIds.Count > 0);
            bool hasResource = !string.IsNullOrEmpty(buff.ResourceId)
                && state.Resources != null && state.Resources.ContainsKey(buff.ResourceId);
            bool applyResource = hasResource && (!hasTenantEffect || validTargetCount > 0);
            if (applyResource)
            {
                changes.Add(new AdjustResourceChange(buff.ResourceId, buff.ResourceDeltaPerTick));
            }
            else if (hasResource && hasTenantEffect && validTargetCount == 0)
            {
                Debug.LogWarning($"[EventEffectManager] buff={buff.BuffId}: no valid tenant targets; skipping resource tick");
            }

            bool expiresNow = buff.RemainingTicks >= 0 && buff.RemainingTicks <= 1;
            int newRemaining = buff.RemainingTicks > 0 ? buff.RemainingTicks - 1 : buff.RemainingTicks;
            if (expiresNow)
            {
                expired.Add(buff.BuffId);
            }
            else
            {
                changes.Add(new UpdateBuffTicksChange(buff.BuffId, newRemaining, state.Day));
            }

            string status = expiresNow
                ? $"{buff.BuffId}：已到期移除"
                : buff.RemainingTicks < 0
                    ? $"{buff.BuffId}：效果已生效 / 持续生效"
                    : $"{buff.BuffId}：效果已生效 / 剩余 {newRemaining} 天";
            pendingBuffs.Add(new PlayerLogWriteDto(
                PlayerLogCategory.BuffTick,
                state.Day,
                state.Phase.Current,
                "Buff 结算",
                status,
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
            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i] is AdjustResourceChange resource)
                {
                    settledResourceDeltas[resource.ResourceId] = settledResourceDeltas.TryGetValue(resource.ResourceId, out int current)
                        ? current + resource.Delta
                        : resource.Delta;
                }
            }
            for (int i = 0; i < pendingBuffs.Count; i++)
                PlayerLogManager.Record(state, pendingBuffs[i]);
            return true;
        }
        Debug.LogError($"[EventEffectManager] TickBuffs day={state.Day}: commit failed with {state.Buffs.Count} buffs; buff ticks deferred and will retry on next dawn");
        return false;
    }

    private static List<string> ResolveBuffTargets(GameRunState state, BuffRunState buff, RoomFloorRegistry floorRegistry)
    {
        if (state.Tenants == null)
            return new List<string>();
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
            else if (change is AdjustItemChange item)
                Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: item={item.ItemId} delta={item.Delta}");
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
            else if (change is AdjustItemChange item)
                parts.Add($"物品「{item.ItemId}」{item.Delta:+#;-#;0}");
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
