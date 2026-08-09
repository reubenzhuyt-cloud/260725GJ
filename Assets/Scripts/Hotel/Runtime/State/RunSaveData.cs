using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotel.Runtime
{
    [Serializable]
    public sealed class RunSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public string SavedAtUtc;
        public string RunId;
        public long StateVersion;
        public int Day;
        public int Seed;
        public HotelPhase Phase;
        public PhaseLifecycleState PhaseLifecycle;
        public int PhaseOccurrence;
        public List<DecisionRunState> Decisions = new List<DecisionRunState>();
        public List<EventHistoryRecord> EventHistory = new List<EventHistoryRecord>();
        public List<string> AuditLog = new List<string>();
        public List<TenantRunState> Tenants = new List<TenantRunState>();
        public List<RoomRunState> Rooms = new List<RoomRunState>();
        public List<ResourceRunState> Resources = new List<ResourceRunState>();
        public List<BuffRunState> Buffs = new List<BuffRunState>();
        public RunSummaryState Summary = new RunSummaryState();
        public List<string> ResolvedReviewCandidateIds = new List<string>();
        public List<ReviewDecisionRecord> ReviewHistory = new List<ReviewDecisionRecord>();
        public List<PlayerLogEntry> PlayerLogs = new List<PlayerLogEntry>();
    }

    public static class RunSaveCodec
    {
        public static string ToJson(GameRunState state, DateTime savedAtUtc, bool prettyPrint = true)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return JsonUtility.ToJson(CreateSnapshot(state, savedAtUtc), prettyPrint);
        }

        public static GameRunState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Save JSON is empty.", nameof(json));

            var save = JsonUtility.FromJson<RunSaveData>(json);
            if (save == null)
                throw new InvalidOperationException("Save JSON could not be read.");
            if (save.SchemaVersion != RunSaveData.CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported save schema {save.SchemaVersion}.");
            if (string.IsNullOrWhiteSpace(save.RunId))
                throw new InvalidOperationException("Save is missing its run id.");

            return RestoreSnapshot(save);
        }

        public static RunSaveData ReadMetadata(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var save = JsonUtility.FromJson<RunSaveData>(json);
            return save != null && save.SchemaVersion == RunSaveData.CurrentSchemaVersion ? save : null;
        }

        private static RunSaveData CreateSnapshot(GameRunState state, DateTime savedAtUtc)
        {
            var save = new RunSaveData
            {
                SavedAtUtc = savedAtUtc.ToUniversalTime().ToString("O"),
                RunId = state.RunId.Value,
                StateVersion = state.StateVersion,
                Day = state.Day,
                Seed = state.Seed,
                Phase = state.Phase.Current,
                PhaseLifecycle = state.Phase.Lifecycle,
                PhaseOccurrence = state.Phase.Occurrence,
                Summary = CloneSummary(state.Summary)
            };

            save.Decisions.AddRange(state.Decisions);
            save.EventHistory.AddRange(state.EventHistory);
            save.AuditLog.AddRange(state.AuditLog);
            save.ResolvedReviewCandidateIds.AddRange(state.ResolvedReviewCandidateIds);
            save.ReviewHistory.AddRange(state.ReviewHistory);

            foreach (var entry in state.PlayerLogs)
                save.PlayerLogs.Add(CloneLogEntry(entry));

            foreach (var pair in state.Tenants)
                save.Tenants.Add(CloneTenant(pair.Value));
            foreach (var pair in state.Rooms)
                save.Rooms.Add(CloneRoom(pair.Value));
            foreach (var pair in state.Resources)
                save.Resources.Add(CloneResource(pair.Value));
            foreach (var pair in state.Buffs)
                save.Buffs.Add(CloneBuff(pair.Value));

            save.Tenants.Sort((a, b) => string.CompareOrdinal(a.TenantId, b.TenantId));
            save.Rooms.Sort((a, b) => string.CompareOrdinal(a.RoomId, b.RoomId));
            save.Resources.Sort((a, b) => string.CompareOrdinal(a.ResourceId, b.ResourceId));
            save.Buffs.Sort((a, b) => string.CompareOrdinal(a.BuffId, b.BuffId));
            return save;
        }

        private static GameRunState RestoreSnapshot(RunSaveData save)
        {
            var state = GameRunState.New(new RunId(save.RunId), save.Seed);
            state.StateVersion = save.StateVersion;
            state.Day = Math.Max(1, save.Day);
            state.Phase.Current = save.Phase;
            state.Phase.Lifecycle = save.PhaseLifecycle;
            state.Phase.Occurrence = Math.Max(1, save.PhaseOccurrence);
            state.Decisions = save.Decisions ?? new List<DecisionRunState>();
            state.EventHistory = save.EventHistory ?? new List<EventHistoryRecord>();
            state.AuditLog = save.AuditLog ?? new List<string>();
            state.Summary = save.Summary ?? new RunSummaryState();
            state.ResolvedReviewCandidateIds = save.ResolvedReviewCandidateIds ?? new List<string>();
            state.ReviewHistory = save.ReviewHistory ?? new List<ReviewDecisionRecord>();

            state.PlayerLogs = new List<PlayerLogEntry>();
            if (save.PlayerLogs != null)
            {
                foreach (var entry in save.PlayerLogs)
                {
                    if (entry == null)
                        continue;
                    state.PlayerLogs.Add(CloneLogEntry(entry));
                }
            }

            if (save.Tenants != null)
            {
                foreach (var tenant in save.Tenants)
                {
                    if (tenant != null && !string.IsNullOrEmpty(tenant.TenantId))
                        state.Tenants[tenant.TenantId] = CloneTenant(tenant);
                }
            }

            if (save.Rooms != null)
            {
                foreach (var room in save.Rooms)
                {
                    if (room != null && !string.IsNullOrEmpty(room.RoomId))
                        state.Rooms[room.RoomId] = CloneRoom(room);
                }
            }

            if (save.Resources != null)
            {
                foreach (var resource in save.Resources)
                {
                    if (resource != null && !string.IsNullOrEmpty(resource.ResourceId))
                        state.Resources[resource.ResourceId] = CloneResource(resource);
                }
            }

            if (save.Buffs != null)
            {
                foreach (var buff in save.Buffs)
                {
                    if (buff != null && !string.IsNullOrEmpty(buff.BuffId))
                        state.Buffs[buff.BuffId] = CloneBuff(buff);
                }
            }

            return state;
        }

        private static TenantRunState CloneTenant(TenantRunState value)
        {
            return new TenantRunState
            {
                TenantId = value.TenantId,
                DefinitionId = value.DefinitionId,
                TrueErosion = value.TrueErosion,
                PlayerMarked = value.PlayerMarked,
                PlayerFlag = value.PlayerFlag,
                RoomId = value.RoomId,
                JobId = value.JobId,
                AvatarKey = value.AvatarKey
            };
        }

        private static RoomRunState CloneRoom(RoomRunState value)
        {
            return new RoomRunState
            {
                RoomId = value.RoomId,
                DefinitionId = value.DefinitionId,
                OccupantIds = value.OccupantIds != null
                    ? new List<string>(value.OccupantIds)
                    : new List<string>()
            };
        }

        private static ResourceRunState CloneResource(ResourceRunState value)
        {
            return new ResourceRunState
            {
                ResourceId = value.ResourceId,
                DefinitionId = value.DefinitionId,
                Amount = value.Amount
            };
        }

        private static BuffRunState CloneBuff(BuffRunState value)
        {
            return value.Clone();
        }

        private static RunSummaryState CloneSummary(RunSummaryState value)
        {
            if (value == null) return new RunSummaryState();
            return new RunSummaryState
            {
                IsComplete = value.IsComplete,
                CompletedDay = value.CompletedDay,
                MisclassificationCount = value.MisclassificationCount,
                FinalTenantCount = value.FinalTenantCount
            };
        }

        private static PlayerLogEntry CloneLogEntry(PlayerLogEntry value)
        {
            if (value == null)
                return null;
            return new PlayerLogEntry
            {
                Sequence = value.Sequence,
                Day = value.Day,
                Phase = value.Phase,
                Category = value.Category,
                Title = value.Title,
                Summary = value.Summary,
                DetailKey = value.DetailKey
            };
        }
    }
}
