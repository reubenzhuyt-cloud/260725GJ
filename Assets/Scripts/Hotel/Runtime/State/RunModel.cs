
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Hotel.Runtime
{
    [Serializable]
    public readonly struct RunId
    {
        [SerializeField] private readonly string value;

        public RunId(string value)
        {
            this.value = value;
        }

        public string Value => value;

        public override bool Equals(object obj)
        {
            return obj is RunId other && value == other.value;
        }

        public override int GetHashCode()
        {
            return value != null ? value.GetHashCode() : 0;
        }
    }

    public enum HotelPhase
    {
        Dawn,
        Day,
        Dusk,
        Night
    }

    public enum PlayerLogCategory
    {
        EventChoice,
        SpecialStory,
        EffectSettlement,
        BuffTick,
        TenantRecruit,
        TenantReject,
        RoomAssignment,
        ResourceFood,
        PhaseTransition
    }

    public enum TenantLogCategory
    {
        Recruit,
        RoomAssignment,
        RoomMove,
        Behavior
    }

    public enum ReviewDecision
    {
        Recruit,
        Reject
    }

    public enum TenantAbility
    {
        None,
        Doctor,
        Cook,
        Engineer,
        NightWatch,
        FormerEmployee,
        Merchant,
        Carpenter,
        Farmer,
        Driver = 9,
        Teacher = 10
    }

    public enum TenantActivityType
    {
        DayActive,
        NightActive,
        AllDay
    }

    public enum PhaseLifecycleState
    {
        Entered,
        Settled,
        WaitingForDecisions,
        Exiting,
        Completed
    }

    public interface IPhaseCycle
    {
        HotelPhase GetNext(HotelPhase phase);
    }

    [Serializable]
    public sealed class PhaseRunState
    {
        public HotelPhase Current = HotelPhase.Dawn;
        public PhaseLifecycleState Lifecycle = PhaseLifecycleState.Entered;
        public int Occurrence = 1;
    }

    [Serializable]
    public sealed class DecisionRunState
    {
        public string DecisionId;
        public HotelPhase Phase;
        public int Day;
        public bool IsBlocking;
        public string SourceId;
        public bool IsCompleted;
    }

    [Serializable]
    public sealed class EventHistoryRecord
    {
        public string EventId;
        public string DefinitionId;
        public int Day;
        public HotelPhase Phase;
        public int Occurrence;
        public bool RequiresDecision;
        public bool Resolved;
        public string OptionId;
    }

    [Serializable]
    public sealed class ReviewDecisionRecord
    {
        public string CandidateId;
        public ReviewDecision Decision;
        public int Day;
        public HotelPhase Phase;
        public float InitialErosion;
    }

    [Serializable]
    public sealed class PlayerLogEntry
    {
        public int Sequence;
        public int Day;
        public HotelPhase Phase;
        public PlayerLogCategory Category;
        public string Title;
        public string Summary;
        public string DetailKey;
        public string TenantId;
    }

    [Serializable]
    public sealed class TenantLogEntry
    {
        public int Sequence;
        public int Day;
        public HotelPhase Phase;
        public TenantLogCategory Category;
        public string Summary;
        public string DetailKey;
    }

    [Serializable]
    public sealed class TenantRunState
    {
        public string TenantId;
        public string DefinitionId;
        public float TrueErosion;
        public bool PlayerMarked;
        public int PlayerFlag;
        public string RoomId;
        public string JobId;
        public string AvatarKey;
        public bool Vulnerable;
    }

    [Serializable]
    public sealed class RoomRunState
    {
        public string RoomId;
        public string DefinitionId;
        public List<string> OccupantIds = new List<string>();
    }

    [Serializable]
    public sealed class ResourceRunState
    {
        public string ResourceId;
        public string DefinitionId;
        public int Amount;
    }

    public enum BuffTickTiming
    {
        Dawn
    }

    public enum EffectTarget
    {
        OwnerTenant,
        AllAssignedTenants,
        SameRoomOtherTenants,
        SameFloorTenants,
        ByPlayerFlag,
        RandomAssignedTenants
    }

    [Serializable]
    public sealed class BuffRunState
    {
        public string BuffId;
        public string SourceEventId;
        public string OwnerTenantId;
        public EffectTarget Target = EffectTarget.OwnerTenant;
        public float ErosionPerTick;
        public string ResourceId;
        public int ResourceDeltaPerTick;
        public int TargetParam;
        public int TargetSeedIndex;
        public BuffTickTiming TickTiming = BuffTickTiming.Dawn;
        public int RemainingTicks;
        public int StartDay;
        public int LastTickDay;
        public List<string> TargetTenantIds = new List<string>();

        public BuffRunState Clone()
        {
            return new BuffRunState
            {
                BuffId = BuffId,
                SourceEventId = SourceEventId,
                OwnerTenantId = OwnerTenantId,
                Target = Target,
                ErosionPerTick = ErosionPerTick,
                ResourceId = ResourceId,
                ResourceDeltaPerTick = ResourceDeltaPerTick,
                TargetParam = TargetParam,
                TargetSeedIndex = TargetSeedIndex,
                TickTiming = TickTiming,
                RemainingTicks = RemainingTicks,
                StartDay = StartDay,
                LastTickDay = LastTickDay,
                TargetTenantIds = TargetTenantIds != null
                    ? new List<string>(TargetTenantIds)
                    : new List<string>()
            };
        }
    }

    [Serializable]
    public sealed class RunSummaryState
    {
        public bool IsComplete;
        public int CompletedDay;
        public int MisclassificationCount;
        public int FinalTenantCount;
    }

    [Serializable]
    public sealed class GameRunState
    {
        public RunId RunId;
        public long StateVersion;
        public int Day;
        public int Seed;
        public PhaseRunState Phase = new PhaseRunState();
        public List<DecisionRunState> Decisions = new List<DecisionRunState>();
        public List<EventHistoryRecord> EventHistory = new List<EventHistoryRecord>();
        public List<string> AuditLog = new List<string>();
        public Dictionary<string, TenantRunState> Tenants = new Dictionary<string, TenantRunState>();
        public Dictionary<string, RoomRunState> Rooms = new Dictionary<string, RoomRunState>();
        public Dictionary<string, ResourceRunState> Resources = new Dictionary<string, ResourceRunState>();
        public Dictionary<string, BuffRunState> Buffs = new Dictionary<string, BuffRunState>();
        public RunSummaryState Summary = new RunSummaryState();
        public List<string> ResolvedReviewCandidateIds = new List<string>();
        public List<ReviewDecisionRecord> ReviewHistory = new List<ReviewDecisionRecord>();
        public List<PlayerLogEntry> PlayerLogs = new List<PlayerLogEntry>();
public bool HotelHasMirror = true;
        public bool IsStorm;
        public Dictionary<string, List<TenantLogEntry>> TenantLogs = new Dictionary<string, List<TenantLogEntry>>();

        public static GameRunState New(RunId id, int seed = 1)
        {
            return new GameRunState
            {
                RunId = id,
                Day = 1,
                Seed = seed,
                StateVersion = 0
            };
        }
    }
}
