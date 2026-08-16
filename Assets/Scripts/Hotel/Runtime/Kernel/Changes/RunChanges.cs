using System.Collections.Generic;

namespace Hotel.Runtime
{
    public abstract class RunChange
    {
    }

    public sealed class SetPhaseLifecycleChange : RunChange
    {
        public SetPhaseLifecycleChange(PhaseLifecycleState value) { Value = value; }
        public PhaseLifecycleState Value { get; }
    }

    public sealed class SetCurrentPhaseChange : RunChange
    {
        public SetCurrentPhaseChange(HotelPhase phase, int day, int occurrence) { Phase = phase; Day = day; Occurrence = occurrence; }
        public HotelPhase Phase { get; }
        public int Day { get; }
        public int Occurrence { get; }
    }

    public sealed class CreateDecisionChange : RunChange
    {
        public CreateDecisionChange(DecisionRunState value) { Value = value; }
        public DecisionRunState Value { get; }
    }

    public sealed class CompleteDecisionChange : RunChange
    {
        public CompleteDecisionChange(string id) { DecisionId = id; }
        public string DecisionId { get; }
    }

    public sealed class AppendAuditLogChange : RunChange
    {
        public AppendAuditLogChange(string value) { Value = value; }
        public string Value { get; }
    }

    public sealed class SetRunSummaryChange : RunChange
    {
        public SetRunSummaryChange(RunSummaryState value) { Value = value; }
        public RunSummaryState Value { get; }
    }

    public sealed class PlanEventHistoryChange : RunChange
    {
        public PlanEventHistoryChange(EventHistoryRecord value) { Value = value; }
        public EventHistoryRecord Value { get; }
    }

    public sealed class ResolveEventHistoryChange : RunChange
    {
        public ResolveEventHistoryChange(string id, string option) { EventId = id; OptionId = option; }
        public string EventId { get; }
        public string OptionId { get; }
    }

    public sealed class SetTenantMarkChange : RunChange
    {
        public SetTenantMarkChange(string id, bool value) { TenantId = id; Value = value; }
        public string TenantId { get; }
        public bool Value { get; }
    }

    public sealed class SetTenantFlagChange : RunChange
    {
        public SetTenantFlagChange(string id, int value) { TenantId = id; Flag = value; }
        public string TenantId { get; }
        public int Flag { get; }
    }

    public sealed class AdjustTenantErosionChange : RunChange
    {
        public AdjustTenantErosionChange(string id, float delta) { TenantId = id; Delta = delta; }
        public string TenantId { get; }
        public float Delta { get; }
    }

    public sealed class AssignRoomChange : RunChange
    {
        public AssignRoomChange(string tenant, string room) { TenantId = tenant; RoomId = room; }
        public string TenantId { get; }
        public string RoomId { get; }
    }

    public sealed class AssignJobChange : RunChange
    {
        public AssignJobChange(string tenant, string job) { TenantId = tenant; JobId = job; }
        public string TenantId { get; }
        public string JobId { get; }
    }

    public sealed class EvictTenantChange : RunChange
    {
        public EvictTenantChange(string id) { TenantId = id; }
        public string TenantId { get; }
    }

    public sealed class AdjustResourceChange : RunChange
    {
        public AdjustResourceChange(string id, int delta) { ResourceId = id; Delta = delta; }
        public string ResourceId { get; }
        public int Delta { get; }
    }

    public sealed class AddBuffChange : RunChange
    {
        public AddBuffChange(BuffRunState value) { Value = value; }
        public BuffRunState Value { get; }
    }

    public sealed class RemoveBuffChange : RunChange
    {
        public RemoveBuffChange(string buffId) { BuffId = buffId; }
        public string BuffId { get; }
    }

    public sealed class UpdateBuffTicksChange : RunChange
    {
        public UpdateBuffTicksChange(string buffId, int remainingTicks, int lastTickDay)
        {
            BuffId = buffId;
            RemainingTicks = remainingTicks;
            LastTickDay = lastTickDay;
        }
        public string BuffId { get; }
        public int RemainingTicks { get; }
        public int LastTickDay { get; }
    }

    public sealed class AddTenantChange : RunChange
    {
        public AddTenantChange(string tenantId, string definitionId, float initialErosion = 0f, string avatarKey = null)
        {
            TenantId = tenantId;
            DefinitionId = definitionId;
            InitialErosion = initialErosion;
            AvatarKey = avatarKey;
        }
        public string TenantId { get; }
        public string DefinitionId { get; }
        public float InitialErosion { get; }
        public string AvatarKey { get; }
    }

    public sealed class ResolveCandidateChange : RunChange
    {
        public ResolveCandidateChange(string candidateId) { CandidateId = candidateId; }
        public ResolveCandidateChange(ReviewDecisionRecord record)
        {
            Record = record;
            CandidateId = record?.CandidateId;
        }
        public string CandidateId { get; }
        public ReviewDecisionRecord Record { get; }
    }

    public sealed class AuthorizedChangeSet
    {
        private readonly List<RunChange> _changes = new List<RunChange>();

        private AuthorizedChangeSet(RunId run, long version, string authorizer, string command)
        {
            RunId = run;
            ExpectedStateVersion = version;
            AuthorizerId = authorizer;
            CommandId = command;
        }

        public RunId RunId { get; }
        public long ExpectedStateVersion { get; }
        public string AuthorizerId { get; }
        public string CommandId { get; }
        public IReadOnlyList<RunChange> Changes => _changes;

        public static AuthorizedChangeSet Coordinator(RunId r, long v, string command)
        {
            return new AuthorizedChangeSet(r, v, "GamePhaseCoordinator", command);
        }

        public static AuthorizedChangeSet Domain(RunId r, long v, string authorizer, string command)
        {
            return new AuthorizedChangeSet(r, v, authorizer, command);
        }

        public void Add(RunChange change)
        {
            _changes.Add(change);
        }
    }

    public readonly struct CommitResult
    {
        public CommitResult(bool succeeded) { Succeeded = succeeded; }
        public bool Succeeded { get; }
    }

    public interface IStateReducer
    {
        CommitResult TryCommit(GameRunState state, AuthorizedChangeSet changes);
    }
}
