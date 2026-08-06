
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
        Farmer
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
    public sealed class TenantRunState
    {
        public string TenantId;
        public string DefinitionId;
        public float TrueErosion;
        public bool PlayerMarked;
        public int PlayerFlag;
        public string RoomId;
        public string JobId;
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
        public RunSummaryState Summary = new RunSummaryState();
        public List<string> ResolvedReviewCandidateIds = new List<string>();
        public List<ReviewDecisionRecord> ReviewHistory = new List<ReviewDecisionRecord>();

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
