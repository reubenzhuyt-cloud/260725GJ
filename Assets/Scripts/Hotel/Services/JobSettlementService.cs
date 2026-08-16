using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public static class JobSettlementService
{
    private const string AuditPrefix = "JobSettlement:";
    private const float AbilityMatchMultiplier = 1.2f;
    private const float AbilityMismatchMultiplier = 0.8f;
    private const float YellowErosionMultiplier = 0.9f;
    private const float RedErosionMultiplier = 1.2f;
    private const float WrongActivityMultiplier = 0.5f;
    private const float AllDayMultiplier = 0.8f;
    private const float NightWatchLossMultiplier = 0.6f;
    private const float SecurityTeamLossMultiplier = 0.4f;
    private const float ForcedActivityErosion = 5f;
    private const float RedWorkerContamination = 1f;

    private sealed class PendingLog
    {
        public string TenantId;
        public string JobId;
        public string Summary;
    }

    public static bool TrySettle(
        GameRunState state,
        StateReducer reducer,
        int day,
        HotelPhase phase,
        IReadOnlyList<TenantReviewCandidateSO> candidates,
        RoomFloorRegistry floorRegistry,
        ResourceAdjustedEvent resourceAdjusted,
        out Dictionary<string, int> settledResourceDeltas)
    {
        settledResourceDeltas = new Dictionary<string, int>(StringComparer.Ordinal);
        if (state == null || reducer == null)
            return false;
        if (phase != HotelPhase.Day && phase != HotelPhase.Night)
            return true;

        string marker = BuildMarker(day, phase);
        if (HasAuditMarker(state, marker))
            return true;

        var changes = new List<RunChange>();
        var logs = new List<PendingLog>();
        var resourceDeltas = new Dictionary<string, int>(StringComparer.Ordinal);
        var tenantIds = new List<string>(state.Tenants.Keys);
        tenantIds.Sort(StringComparer.Ordinal);

        for (int i = 0; i < tenantIds.Count; i++)
        {
            string tenantId = tenantIds[i];
            if (!state.Tenants.TryGetValue(tenantId, out TenantRunState tenant) || tenant == null)
                continue;
            if (string.IsNullOrEmpty(tenant.RoomId) || string.IsNullOrEmpty(tenant.JobId))
                continue;
            if (!JobCatalog.TryGet(tenant.JobId, out JobDefinition job))
                continue;

            TenantAbility ability = TenantAbilityResolver.ResolveAbility(tenantId, candidates);
            TenantActivityType activity = TenantAbilityResolver.ResolveActivityType(tenantId, candidates);
            float efficiency = CalculateEfficiency(job, ability, tenant.TrueErosion, activity, phase);
            string summary = BuildJobChanges(
                state, tenantId, job, efficiency, day, phase, floorRegistry, changes, resourceDeltas);

            if (IsWrongActivityPeriod(activity, phase))
            {
                changes.Add(new AdjustTenantErosionChange(tenantId, ForcedActivityErosion));
                summary += $"；非活跃时段，侵蚀 +{ForcedActivityErosion:0}";
            }

            if (EventConditionEvaluator.ColorOf(tenant.TrueErosion) == EventConditionEvaluator.ErosionColor.Red)
            {
                List<string> neighbours = EventEffectExecutor.ResolveTargets(
                    EffectTarget.SameRoomOtherTenants, state, tenantId, 0, 0, floorRegistry);
                if (neighbours != null)
                {
                    for (int n = 0; n < neighbours.Count; n++)
                        changes.Add(new AdjustTenantErosionChange(neighbours[n], RedWorkerContamination));
                    if (neighbours.Count > 0)
                        summary += $"；同住者侵蚀 +{RedWorkerContamination:0}";
                }
            }

            logs.Add(new PendingLog
            {
                TenantId = tenantId,
                JobId = job.Id,
                Summary = summary
            });
        }

        changes.Add(new AppendAuditLogChange(marker));
        var set = AuthorizedChangeSet.Domain(
            state.RunId,
            state.StateVersion,
            "JobSettlementService",
            $"SettleJobs:{day}:{phase}");
        for (int i = 0; i < changes.Count; i++)
            set.Add(changes[i]);

        CommitResult result = reducer.TryCommit(state, set);
        if (!result.Succeeded)
        {
            Debug.LogError($"[JobSettlementService] Settlement failed for day={day}, phase={phase}; it will retry on the next matching notification.");
            return false;
        }

        settledResourceDeltas = resourceDeltas;

        foreach (KeyValuePair<string, int> resource in resourceDeltas)
        {
            if (resourceAdjusted == null || !state.Resources.TryGetValue(resource.Key, out ResourceRunState current))
                continue;
            resourceAdjusted.Raise(new ResourceAdjustedData
            {
                resourceId = resource.Key,
                delta = resource.Value,
                newAmount = current.Amount
            });
        }

        for (int i = 0; i < logs.Count; i++)
        {
            PendingLog log = logs[i];
            string displayName = ResolveDisplayName(log.TenantId, candidates);
            PlayerLogManager.Record(state, new PlayerLogWriteDto(
                PlayerLogCategory.WorkAssignment,
                day,
                phase,
                "工作结算",
                $"{displayName}：{log.Summary}",
                log.JobId,
                log.TenantId));
            TenantLogManager.Record(state, new TenantLogWriteDto(
                log.TenantId,
                TenantLogCategory.WorkAssignment,
                day,
                phase,
                log.Summary,
                log.JobId));
        }

        return true;
    }

    public static float CalculateEfficiency(
        JobDefinition job,
        TenantAbility ability,
        float erosion,
        TenantActivityType activity,
        HotelPhase phase)
    {
        float multiplier = 1f;
        if (job != null && job.Id != JobCatalog.Chores)
            multiplier *= job.IsSuitableFor(ability) ? AbilityMatchMultiplier : AbilityMismatchMultiplier;

        EventConditionEvaluator.ErosionColor color = EventConditionEvaluator.ColorOf(erosion);
        if (color == EventConditionEvaluator.ErosionColor.Yellow)
            multiplier *= YellowErosionMultiplier;
        else if (color == EventConditionEvaluator.ErosionColor.Red)
            multiplier *= RedErosionMultiplier;

        if (activity == TenantActivityType.AllDay)
            multiplier *= AllDayMultiplier;
        else if (IsWrongActivityPeriod(activity, phase))
            multiplier *= WrongActivityMultiplier;
        return multiplier;
    }

    public static float GetNightEventLossMultiplier(
        GameRunState state,
        IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        if (state == null || state.Tenants == null)
            return 1f;

        bool hasWatchSpecialist = false;
        bool hasFormerEmployee = false;
        foreach (KeyValuePair<string, TenantRunState> pair in state.Tenants)
        {
            TenantRunState tenant = pair.Value;
            if (tenant == null || string.IsNullOrEmpty(tenant.RoomId)
                || tenant.JobId != JobCatalog.NightWatch)
                continue;
            TenantAbility ability = TenantAbilityResolver.ResolveAbility(pair.Key, candidates);
            if (ability == TenantAbility.NightWatch)
                hasWatchSpecialist = true;
            else if (ability == TenantAbility.FormerEmployee)
                hasFormerEmployee = true;
        }

        if (hasWatchSpecialist && hasFormerEmployee)
            return SecurityTeamLossMultiplier;
        if (hasWatchSpecialist || hasFormerEmployee)
            return NightWatchLossMultiplier;
        return 1f;
    }

    private static string BuildJobChanges(
        GameRunState state,
        string tenantId,
        JobDefinition job,
        float efficiency,
        int day,
        HotelPhase phase,
        RoomFloorRegistry floorRegistry,
        List<RunChange> changes,
        Dictionary<string, int> resourceDeltas)
    {
        switch (job.Id)
        {
            case JobCatalog.Cooking:
                return AddResourceOutput(state, "food", 2, efficiency, job.DisplayName, changes, resourceDeltas);
            case JobCatalog.Farming:
                return AddResourceOutput(state, "food", 2, efficiency, job.DisplayName, changes, resourceDeltas);
            case JobCatalog.Trading:
                return AddResourceOutput(state, "currency", 4, efficiency, job.DisplayName, changes, resourceDeltas);
            case JobCatalog.Chores:
                return AddResourceOutput(state, "currency", 2, efficiency, job.DisplayName, changes, resourceDeltas);
            case JobCatalog.Exploration:
            {
                bool yieldsFood = StableChoice(tenantId, day, phase);
                string resourceId = yieldsFood ? "food" : "currency";
                int baseAmount = yieldsFood ? 2 : 3;
                return AddResourceOutput(state, resourceId, baseAmount, efficiency, job.DisplayName, changes, resourceDeltas);
            }
            case JobCatalog.Medical:
            {
                string targetId = FindHighestErosionTenant(state);
                int amount = CalculateOutput(4, efficiency);
                if (string.IsNullOrEmpty(targetId) || amount <= 0)
                    return $"{job.DisplayName}：没有需要治疗的房客";
                changes.Add(new AdjustTenantErosionChange(targetId, -amount));
                return $"{job.DisplayName}：治疗 {targetId}，侵蚀 -{amount}";
            }
            case JobCatalog.Patrol:
            {
                List<string> targets = EventEffectExecutor.ResolveTargets(
                    EffectTarget.SameFloorTenants, state, tenantId, 0, 0, floorRegistry);
                int amount = CalculateOutput(1, efficiency);
                int count = AddErosionReduction(targets, amount, changes);
                return count > 0
                    ? $"{job.DisplayName}：本层 {count} 人侵蚀 -{amount}"
                    : $"{job.DisplayName}：未找到同层目标";
            }
            case JobCatalog.Organization:
            {
                List<string> targets = EventEffectExecutor.ResolveTargets(
                    EffectTarget.AllAssignedTenants, state, tenantId, 0, 0, floorRegistry);
                int amount = CalculateOutput(1, efficiency);
                int count = AddErosionReduction(targets, amount, changes);
                return count > 0
                    ? $"{job.DisplayName}：全旅馆 {count} 人侵蚀 -{amount}"
                    : $"{job.DisplayName}：没有可参与的房客";
            }
            case JobCatalog.NightWatch:
                return phase == HotelPhase.Night
                    ? $"{job.DisplayName}：夜间防护已生效"
                    : $"{job.DisplayName}：仅在夜间提供防护";
            case JobCatalog.Repair:
                return $"{job.DisplayName}：设施耐久系统尚未开放，本阶段无产出";
            default:
                return $"{job.DisplayName}：本阶段无产出";
        }
    }

    private static string AddResourceOutput(
        GameRunState state,
        string resourceId,
        int baseAmount,
        float efficiency,
        string jobName,
        List<RunChange> changes,
        Dictionary<string, int> resourceDeltas)
    {
        if (state.Resources == null || !state.Resources.ContainsKey(resourceId))
            return $"{jobName}：缺少资源配置 {resourceId}";
        int amount = CalculateOutput(baseAmount, efficiency);
        changes.Add(new AdjustResourceChange(resourceId, amount));
        resourceDeltas[resourceId] = resourceDeltas.TryGetValue(resourceId, out int current)
            ? current + amount
            : amount;
        return $"{jobName}：{ResourceName(resourceId)} +{amount}";
    }

    private static int AddErosionReduction(List<string> targets, int amount, List<RunChange> changes)
    {
        if (targets == null || amount <= 0)
            return 0;
        int count = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            if (string.IsNullOrEmpty(targets[i]))
                continue;
            changes.Add(new AdjustTenantErosionChange(targets[i], -amount));
            count++;
        }
        return count;
    }

    private static int CalculateOutput(int baseAmount, float efficiency)
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseAmount * Mathf.Max(0f, efficiency)));
    }

    private static bool IsWrongActivityPeriod(TenantActivityType activity, HotelPhase phase)
    {
        return (activity == TenantActivityType.DayActive && phase == HotelPhase.Night)
            || (activity == TenantActivityType.NightActive && phase == HotelPhase.Day);
    }

    private static string FindHighestErosionTenant(GameRunState state)
    {
        string targetId = null;
        float highest = 0f;
        foreach (KeyValuePair<string, TenantRunState> pair in state.Tenants)
        {
            TenantRunState tenant = pair.Value;
            if (tenant == null || string.IsNullOrEmpty(tenant.RoomId) || tenant.TrueErosion <= highest)
                continue;
            highest = tenant.TrueErosion;
            targetId = pair.Key;
        }
        return targetId;
    }

    private static bool HasAuditMarker(GameRunState state, string marker)
    {
        if (state.AuditLog == null)
            return false;
        for (int i = 0; i < state.AuditLog.Count; i++)
        {
            if (string.Equals(state.AuditLog[i], marker, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string BuildMarker(int day, HotelPhase phase)
    {
        return $"{AuditPrefix}{day}:{phase}";
    }

    private static bool StableChoice(string tenantId, int day, HotelPhase phase)
    {
        unchecked
        {
            int hash = 17;
            if (tenantId != null)
            {
                for (int i = 0; i < tenantId.Length; i++)
                    hash = hash * 31 + tenantId[i];
            }
            hash = hash * 31 + day;
            hash = hash * 31 + (int)phase;
            return (hash & 1) == 0;
        }
    }

    private static string ResolveDisplayName(string tenantId, IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        if (candidates != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                TenantReviewCandidateSO candidate = candidates[i];
                if (candidate != null && candidate.candidateId == tenantId)
                    return string.IsNullOrEmpty(candidate.displayName) ? tenantId : candidate.displayName;
            }
        }
        return tenantId;
    }

    private static string ResourceName(string resourceId)
    {
        return resourceId == "food" ? "食物" : resourceId == "currency" ? "货币" : resourceId;
    }
}
