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
                        break;
                    }
                case AdjustResourceChange resource:
                {
                    if (!s.Resources.ContainsKey(resource.ResourceId))
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
                    var tenant = s.Tenants[x.TenantId];
                    var clamped = tenant.TrueErosion + x.Delta;
                    if (clamped < 0f) clamped = 0f;
                    if (clamped > 100f) clamped = 100f;
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
                case AdjustResourceChange x:
                    s.Resources[x.ResourceId].Amount += x.Delta;
                    break;
                case AddTenantChange x:
                    s.Tenants[x.TenantId] = new TenantRunState
                    {
                        TenantId = x.TenantId,
                        DefinitionId = x.DefinitionId,
                        TrueErosion = x.InitialErosion
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
