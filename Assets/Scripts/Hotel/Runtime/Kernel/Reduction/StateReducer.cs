using System;
using System.Collections.Generic;

namespace Hotel.Runtime
{
    public sealed class StateReducer : IStateReducer
    {
        public CommitResult TryCommit(GameRunState state, AuthorizedChangeSet set)
        {
            if (state == null || set == null)
                return new CommitResult(false);

            if (state.RunId.Value != set.RunId.Value)
                return new CommitResult(false);

            if (state.StateVersion != set.ExpectedStateVersion)
                return new CommitResult(false);

            if (!Validate(state, set))
                return new CommitResult(false);

            foreach (var c in set.Changes)
                Apply(state, c);

            state.StateVersion++;
            return new CommitResult(true);
        }

        private static bool Validate(GameRunState s, AuthorizedChangeSet set)
        {
            var plannedDecisionIds = new HashSet<string>();
            var plannedEventIds = new HashSet<string>();
            var assignedTenants = new HashSet<string>();
            var plannedTenantIds = new HashSet<string>();
            var plannedCandidateIds = new HashSet<string>();
            var plannedTenantErosion = new Dictionary<string, float>();
            var plannedBuffIds = new HashSet<string>();
            var plannedChainIds = new HashSet<string>();
            var reviewRecords = new List<ReviewDecisionRecord>();

            foreach (var c in set.Changes)
            {
                if ((c is SetPhaseLifecycleChange || c is SetCurrentPhaseChange || c is SetRunSummaryChange) && set.AuthorizerId != "GamePhaseCoordinator")
                    return false;

                switch (c)
                {
                    case CompleteDecisionChange done:
                    {
                        bool existsInState = false;
                        foreach (var d in s.Decisions)
                        {
                            if (d.DecisionId == done.DecisionId)
                            {
                                if (d.IsCompleted)
                                    return false;
                                existsInState = true;
                                break;
                            }
                        }
                        if (!existsInState)
                        {
                            if (!plannedDecisionIds.Contains(done.DecisionId))
                                return false;
                        }
                        break;
                    }
                    case CreateDecisionChange create:
                    {
                        if (!plannedDecisionIds.Add(create.Value.DecisionId))
                            return false;
                        break;
                    }
                    case PlanEventHistoryChange plan:
                    {
                        foreach (var e in s.EventHistory)
                        {
                            if (e.EventId == plan.Value.EventId)
                                return false;
                        }
                        if (!plannedEventIds.Add(plan.Value.EventId))
                            return false;
                        break;
                    }
                    case ResolveEventHistoryChange resolved:
                    {
                        bool found = false;
                        foreach (var e in s.EventHistory)
                        {
                            if (e.EventId == resolved.EventId)
                            {
                                if (e.Resolved)
                                    return false;
                                found = true;
                                break;
                            }
                        }
                        if (!found && !plannedEventIds.Contains(resolved.EventId))
                            return false;
                        break;
                    }
                    case SetTenantMarkChange mark:
                    {
                        if (!s.Tenants.ContainsKey(mark.TenantId))
                            return false;
                        break;
                    }
                    case SetTenantFlagChange flag:
                    {
                        if (!s.Tenants.ContainsKey(flag.TenantId))
                            return false;
                        if (flag.Flag < 0 || flag.Flag > 3)
                            return false;
                        break;
                    }
                    case AdjustTenantErosionChange erosion:
                    {
                        if (!s.Tenants.ContainsKey(erosion.TenantId))
                            return false;
                        if (float.IsNaN(erosion.Delta) || float.IsInfinity(erosion.Delta))
                            return false;
                        break;
                    }
                    case AssignRoomChange room:
                    {
                        if (!s.Tenants.ContainsKey(room.TenantId))
                            return false;
                        if (!s.Rooms.ContainsKey(room.RoomId))
                            return false;
                        if (!assignedTenants.Add(room.TenantId))
                            return false;
                        break;
                    }
                    case AssignJobChange job:
                    {
                        if (!s.Tenants.ContainsKey(job.TenantId))
                            return false;
                        if (!JobCatalog.IsValid(job.JobId))
                            return false;
                        break;
                    }
                    case EvictTenantChange evict:
                    {
                        if (!s.Tenants.ContainsKey(evict.TenantId))
                            return false;
                        break;
                    }
                    case AdjustResourceChange resource:
                    {
                        if (!s.Resources.ContainsKey(resource.ResourceId))
                            return false;
                        break;
                    }
                    case AdjustItemChange item:
                    {
                        if (string.IsNullOrEmpty(item.ItemId))
                            return false;
                        if (item.Delta == 0)
                            return false;
                        if (s.Inventory == null)
                            return false;
                        if (item.Delta < 0)
                        {
                            if (!s.Inventory.TryGetValue(item.ItemId, out int current))
                                return false;
                            if (current + item.Delta < 0)
                                return false;
                        }
                        break;
                    }
                    case StartChainChange start:
                    {
                        if (string.IsNullOrEmpty(start.ChainId))
                            return false;
                        if (start.StartDay < 1)
                            return false;
                        if (start.FirstTriggerDay < 1)
                            return false;
                        if (start.NextDueDay > 0 && start.NextDueDay < start.FirstTriggerDay)
                            return false;
                        if (!s.Tenants.ContainsKey(start.TenantId))
                            return false;
                        if (s.Chains.ContainsKey(start.ChainId))
                            return false;
                        if (!plannedChainIds.Add(start.ChainId))
                            return false;
                        break;
                    }
                    case SetChainFlagChange flag:
                    {
                        if (string.IsNullOrEmpty(flag.ChainId) || string.IsNullOrEmpty(flag.Flag))
                            return false;
                        if (!s.Chains.ContainsKey(flag.ChainId) && !plannedChainIds.Contains(flag.ChainId))
                            return false;
                        break;
                    }
                    case AdvanceChainStepChange advance:
                    {
                        if (string.IsNullOrEmpty(advance.ChainId))
                            return false;
                        if (!s.Chains.TryGetValue(advance.ChainId, out ChainRunState chain))
                            return false;
                        if (chain.Completed || chain.Failed)
                            return false;
                        if (advance.NextStep != chain.NextStepToPresent + 1)
                            return false;
                        if (advance.Completed)
                        {
                            if (advance.NextDueDay != 0)
                                return false;
                        }
                        else
                        {
                            if (advance.NextDueDay < 1)
                                return false;
                            if (chain.FirstTriggerDay > 0 && advance.NextDueDay < chain.FirstTriggerDay)
                                return false;
                        }
                        break;
                    }
                    case SetChainScheduleChange schedule:
                    {
                        if (string.IsNullOrEmpty(schedule.ChainId))
                            return false;
                        if (!s.Chains.TryGetValue(schedule.ChainId, out ChainRunState chain))
                            return false;
                        if (chain.Completed || chain.Failed)
                            return false;
                        if (schedule.FirstTriggerDay < 1)
                            return false;
                        if (schedule.NextDueDay < schedule.FirstTriggerDay)
                            return false;
                        break;
                    }
                    case FailChainChange fail:
                    {
                        if (string.IsNullOrEmpty(fail.ChainId))
                            return false;
                        if (!s.Chains.TryGetValue(fail.ChainId, out ChainRunState chain))
                            return false;
                        if (chain.Completed || chain.Failed)
                            return false;
                        break;
                    }
                    case LockTenantErosionChange lockErosion:
                    {
                        if (!s.Tenants.ContainsKey(lockErosion.TenantId))
                            return false;
                        if (float.IsNaN(lockErosion.Value) || float.IsInfinity(lockErosion.Value))
                            return false;
                        if (lockErosion.Value < 0f || lockErosion.Value > 100f)
                            return false;
                        break;
                    }
                    case SetTenantCheckInChange checkIn:
                    {
                        if (!s.Tenants.ContainsKey(checkIn.TenantId))
                            return false;
                        if (checkIn.Day < 1)
                            return false;
                        break;
                    }
                    case SetRunFlagChange runFlag:
                    {
                        if (string.IsNullOrEmpty(runFlag.Flag))
                            return false;
                        if (s.RunFlags != null && s.RunFlags.Contains(runFlag.Flag))
                            return false;
                        break;
                    }
                    case AddRoomOccupantChange addOcc:
                    {
                        if (string.IsNullOrEmpty(addOcc.RoomId) || string.IsNullOrEmpty(addOcc.OccupantId))
                            return false;
                        if (!s.Rooms.TryGetValue(addOcc.RoomId, out RoomRunState room))
                            return false;
                        if (room.OccupantIds != null && room.OccupantIds.Contains(addOcc.OccupantId))
                            return false;
                        break;
                    }
                    case RemoveRoomOccupantChange removeOcc:
                    {
                        if (string.IsNullOrEmpty(removeOcc.RoomId) || string.IsNullOrEmpty(removeOcc.OccupantId))
                            return false;
                        if (!s.Rooms.TryGetValue(removeOcc.RoomId, out RoomRunState room))
                            return false;
                        if (room.OccupantIds == null || !room.OccupantIds.Contains(removeOcc.OccupantId))
                            return false;
                        break;
                    }
                    case AddBuffChange add:
                    {
                        if (add.Value == null || string.IsNullOrEmpty(add.Value.BuffId))
                            return false;
                        if (s.Buffs == null || s.Buffs.ContainsKey(add.Value.BuffId))
                            return false;
                        if (!plannedBuffIds.Add(add.Value.BuffId))
                            return false;
                        break;
                    }
                    case RemoveBuffChange remove:
                    {
                        if (string.IsNullOrEmpty(remove.BuffId))
                            return false;
                        if (s.Buffs == null || !s.Buffs.ContainsKey(remove.BuffId))
                            return false;
                        break;
                    }
                    case UpdateBuffTicksChange update:
                    {
                        if (string.IsNullOrEmpty(update.BuffId))
                            return false;
                        if (s.Buffs == null || !s.Buffs.ContainsKey(update.BuffId))
                            return false;
                        if (update.RemainingTicks < -1)
                            return false;
                        if (update.LastTickDay < 0)
                            return false;
                        if (update.LastTickDay > s.Day)
                            return false;
                        break;
                    }
                    case AddTenantChange add:
                    {
                        if (s.Tenants.ContainsKey(add.TenantId))
                            return false;
                        if (!plannedTenantIds.Add(add.TenantId))
                            return false;
                        plannedTenantErosion.Add(add.TenantId, add.InitialErosion);
                        break;
                    }
                    case ResolveCandidateChange resolve:
                    {
                        if (s.ResolvedReviewCandidateIds.Contains(resolve.CandidateId))
                            return false;
                        if (!plannedCandidateIds.Add(resolve.CandidateId))
                            return false;
                        if (resolve.Record != null) reviewRecords.Add(resolve.Record);
                        break;
                    }
                }
            }

            foreach (var record in reviewRecords)
            {
                if (record.Decision == ReviewDecision.Recruit)
                {
                    if (!plannedTenantErosion.TryGetValue(record.CandidateId, out var erosion)
                        || erosion != record.InitialErosion)
                        return false;
                }
                else if (plannedTenantIds.Contains(record.CandidateId)) return false;
            }

            return true;
        }

        private static void Apply(GameRunState s, RunChange c)
        {
            switch (c)
            {
                case SetPhaseLifecycleChange x:
                    s.Phase.Lifecycle = x.Value;
                    break;
                case SetCurrentPhaseChange x:
                    s.Phase.Current = x.Phase;
                    s.Day = x.Day;
                    s.Phase.Occurrence = x.Occurrence;
                    break;
                case CreateDecisionChange x:
                    s.Decisions.Add(x.Value);
                    break;
                case CompleteDecisionChange x:
                    foreach (var d in s.Decisions)
                    {
                        if (d.DecisionId == x.DecisionId)
                        {
                            d.IsCompleted = true;
                            break;
                        }
                    }
                    break;
                case AppendAuditLogChange x:
                    s.AuditLog.Add(x.Value);
                    break;
                case SetRunSummaryChange x:
                    s.Summary = x.Value;
                    break;
                case PlanEventHistoryChange x:
                    s.EventHistory.Add(x.Value);
                    break;
                case ResolveEventHistoryChange x:
                    foreach (var e in s.EventHistory)
                    {
                        if (e.EventId == x.EventId)
                        {
                            e.Resolved = true;
                            e.OptionId = x.OptionId;
                            break;
                        }
                    }
                    break;
                case SetTenantMarkChange x:
                    s.Tenants[x.TenantId].PlayerMarked = x.Value;
                    break;
                case SetTenantFlagChange x:
                    s.Tenants[x.TenantId].PlayerFlag = x.Flag;
                    break;
                case AdjustTenantErosionChange x:
                {
                    if (float.IsNaN(x.Delta) || float.IsInfinity(x.Delta))
                        break;
                    var tenant = s.Tenants[x.TenantId];
                    if (tenant.ErosionLocked)
                        break;
                    var clamped = tenant.TrueErosion + x.Delta;
                    if (clamped < 0f) clamped = 0f;
                    if (clamped > 100f) clamped = 100f;
                    if (float.IsNaN(clamped) || float.IsInfinity(clamped))
                        break;
                    tenant.TrueErosion = clamped;
                    break;
                }
                case AssignRoomChange x:
                {
                    var tenant = s.Tenants[x.TenantId];
                    if (tenant.RoomId != null && tenant.RoomId != x.RoomId && s.Rooms.ContainsKey(tenant.RoomId))
                    {
                        s.Rooms[tenant.RoomId].OccupantIds.Remove(x.TenantId);
                    }
                    tenant.RoomId = x.RoomId;
                    s.Rooms[x.RoomId].OccupantIds.Add(x.TenantId);
                    break;
                }
                case AssignJobChange x:
                    s.Tenants[x.TenantId].JobId = x.JobId;
                    break;
                case EvictTenantChange x:
                {
                    if (s.Tenants.TryGetValue(x.TenantId, out TenantRunState tenant))
                    {
                        if (!string.IsNullOrEmpty(tenant.RoomId)
                            && s.Rooms.TryGetValue(tenant.RoomId, out RoomRunState room))
                        {
                            room.OccupantIds.Remove(x.TenantId);
                        }
                        s.Tenants.Remove(x.TenantId);
                    }
                    break;
                }
                case AdjustResourceChange x:
                {
                    var resource = s.Resources[x.ResourceId];
                    var amount = resource.Amount + x.Delta;
                    if (amount < 0) amount = 0;
                    resource.Amount = amount;
                    break;
                }
                case AdjustItemChange x:
                {
                    int current = s.Inventory.TryGetValue(x.ItemId, out int existing) ? existing : 0;
                    long combined = (long)current + x.Delta;
                    int amount = combined > int.MaxValue ? int.MaxValue : (int)combined;
                    if (amount == 0)
                        s.Inventory.Remove(x.ItemId);
                    else
                        s.Inventory[x.ItemId] = amount;
                    break;
                }
                case StartChainChange x:
                    s.Chains[x.ChainId] = new ChainRunState
                    {
                        ChainId = x.ChainId,
                        NextStepToPresent = 1,
                        TargetTenantId = x.TenantId,
                        StartDay = x.StartDay,
                        FirstTriggerDay = x.FirstTriggerDay,
                        NextDueDay = x.NextDueDay > 0 ? x.NextDueDay : x.FirstTriggerDay
                    };
                    break;
                case SetChainFlagChange x:
                {
                    if (!s.Chains.TryGetValue(x.ChainId, out ChainRunState chain))
                        break;
                    if (chain.Flags == null)
                        chain.Flags = new List<string>();
                    if (!chain.Flags.Contains(x.Flag))
                        chain.Flags.Add(x.Flag);
                    break;
                }
                case AdvanceChainStepChange x:
                {
                    if (!s.Chains.TryGetValue(x.ChainId, out ChainRunState chain))
                        break;
                    if (chain.Failed)
                        break;
                    chain.NextStepToPresent = x.NextStep;
                    if (x.Completed)
                    {
                        chain.NextDueDay = 0;
                        chain.Completed = true;
                    }
                    else if (x.NextDueDay > 0)
                    {
                        chain.NextDueDay = x.NextDueDay;
                    }
                    break;
                }
                case SetChainScheduleChange x:
                {
                    if (!s.Chains.TryGetValue(x.ChainId, out ChainRunState chain))
                        break;
                    if (chain.FirstTriggerDay < 1)
                        chain.FirstTriggerDay = x.FirstTriggerDay;
                    if (chain.NextDueDay < 1)
                        chain.NextDueDay = x.NextDueDay;
                    break;
                }
                case FailChainChange x:
                {
                    if (s.Chains.TryGetValue(x.ChainId, out ChainRunState chain))
                        chain.Failed = true;
                    break;
                }
                case LockTenantErosionChange x:
                {
                    if (!s.Tenants.TryGetValue(x.TenantId, out TenantRunState tenant))
                        break;
                    tenant.ErosionLocked = true;
                    tenant.ErosionLockValue = x.Value;
                    if (tenant.TrueErosion != x.Value)
                        tenant.TrueErosion = x.Value;
                    break;
                }
                case SetTenantCheckInChange x:
                {
                    if (s.Tenants.TryGetValue(x.TenantId, out TenantRunState tenant))
                        tenant.CheckInDay = x.Day;
                    break;
                }
                case SetRunFlagChange x:
                {
                    if (s.RunFlags == null)
                        s.RunFlags = new List<string>();
                    if (!s.RunFlags.Contains(x.Flag))
                        s.RunFlags.Add(x.Flag);
                    break;
                }
                case AddRoomOccupantChange x:
                {
                    if (!s.Rooms.TryGetValue(x.RoomId, out RoomRunState room))
                        break;
                    if (room.OccupantIds == null)
                        room.OccupantIds = new List<string>();
                    if (!room.OccupantIds.Contains(x.OccupantId))
                        room.OccupantIds.Add(x.OccupantId);
                    break;
                }
                case RemoveRoomOccupantChange x:
                {
                    if (s.Rooms.TryGetValue(x.RoomId, out RoomRunState room) && room.OccupantIds != null)
                        room.OccupantIds.Remove(x.OccupantId);
                    break;
                }
                case AddBuffChange x:
                    s.Buffs[x.Value.BuffId] = x.Value.Clone();
                    break;
                case RemoveBuffChange x:
                    s.Buffs.Remove(x.BuffId);
                    break;
                case UpdateBuffTicksChange x:
                {
                    if (s.Buffs.TryGetValue(x.BuffId, out BuffRunState buff))
                    {
                        buff.RemainingTicks = x.RemainingTicks;
                        buff.LastTickDay = x.LastTickDay;
                    }
                    break;
                }
                case AddTenantChange x:
                    s.Tenants[x.TenantId] = new TenantRunState
                    {
                        TenantId = x.TenantId,
                        DefinitionId = x.DefinitionId,
                        TrueErosion = x.InitialErosion,
                        AvatarKey = x.AvatarKey
                    };
                    break;
                case ResolveCandidateChange x:
                    s.ResolvedReviewCandidateIds.Add(x.CandidateId);
                    if (x.Record != null)
                    {
                        s.ReviewHistory.Add(new ReviewDecisionRecord
                        {
                            CandidateId = x.Record.CandidateId,
                            Decision = x.Record.Decision,
                            Day = x.Record.Day,
                            Phase = x.Record.Phase,
                            InitialErosion = x.Record.InitialErosion
                        });
                    }
                    break;
            }
        }
    }
}
