
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
        TenantEvict,
        RoomAssignment,
        ResourceFood,
        PhaseTransition,
        WorkAssignment
    }

    public enum TenantLogCategory
    {
        Recruit,
        RoomAssignment,
        RoomMove,
        Behavior,
        WorkAssignment
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

    public sealed class JobDefinition
    {
        public JobDefinition(string id, string displayName, params TenantAbility[] suitableAbilities)
        {
            Id = id;
            DisplayName = displayName;
            SuitableAbilities = suitableAbilities ?? Array.Empty<TenantAbility>();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<TenantAbility> SuitableAbilities { get; }

        public bool IsSuitableFor(TenantAbility ability)
        {
            for (int i = 0; i < SuitableAbilities.Count; i++)
            {
                if (SuitableAbilities[i] == ability)
                    return true;
            }
            return false;
        }
    }

    public static class JobCatalog
    {
        public const string Cooking = "cooking";
        public const string Medical = "medical";
        public const string Repair = "repair";
        public const string NightWatch = "night_watch";
        public const string Patrol = "patrol";
        public const string Trading = "trading";
        public const string Farming = "farming";
        public const string Exploration = "exploration";
        public const string Organization = "organization";
        public const string Chores = "chores";

        private static readonly JobDefinition[] Definitions =
        {
            new(Cooking, "烹饪", TenantAbility.Cook),
            new(Medical, "医疗", TenantAbility.Doctor),
            new(Repair, "维修", TenantAbility.Engineer, TenantAbility.Carpenter),
            new(NightWatch, "守夜", TenantAbility.NightWatch, TenantAbility.FormerEmployee),
            new(Patrol, "巡逻", TenantAbility.FormerEmployee),
            new(Trading, "交易", TenantAbility.Merchant),
            new(Farming, "种植", TenantAbility.Farmer),
            new(Exploration, "探索", TenantAbility.Driver),
            new(Organization, "组织活动", TenantAbility.Teacher),
            new(Chores, "杂务")
        };

        public static IReadOnlyList<JobDefinition> All => Definitions;

        public static bool IsValid(string jobId)
        {
            return string.IsNullOrEmpty(jobId) || TryGet(jobId, out _);
        }

        public static bool TryGet(string jobId, out JobDefinition definition)
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (string.Equals(Definitions[i].Id, jobId, StringComparison.Ordinal))
                {
                    definition = Definitions[i];
                    return true;
                }
            }
            definition = null;
            return false;
        }

        public static string GetDisplayName(string jobId)
        {
            return TryGet(jobId, out JobDefinition definition)
                ? definition.DisplayName
                : "未安排";
        }
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
        public int CheckInDay;
        public bool ErosionLocked;
        public float ErosionLockValue;
    }

    [Serializable]
    public sealed class RoomRunState
    {
        public string RoomId;
        public string DefinitionId;
        public List<string> OccupantIds = new();
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
        public List<string> TargetTenantIds = new();

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
    public sealed class ChainRunState
    {
        public string ChainId;
        public int NextStepToPresent = 1;
        public string TargetTenantId;
        public int StartDay;
        /// <summary>Persisted day the chain's first event is due (never rerolled after load; 0 = legacy/missing).</summary>
        public int FirstTriggerDay;
        /// <summary>Persisted exact day the current step is due (advanced atomically on settlement; 0 = legacy/missing).</summary>
        public int NextDueDay;
        public List<string> Flags = new();
        public bool Completed;
        public bool Failed;

        public ChainRunState Clone()
        {
            return new ChainRunState
            {
                ChainId = ChainId,
                NextStepToPresent = NextStepToPresent,
                TargetTenantId = TargetTenantId,
                StartDay = StartDay,
                FirstTriggerDay = FirstTriggerDay,
                NextDueDay = NextDueDay,
                Flags = Flags != null ? new List<string>(Flags) : new List<string>(),
                Completed = Completed,
                Failed = Failed
            };
        }
    }

    [Serializable]
    public sealed class GameRunState
    {
        public RunId RunId;
        public long StateVersion;
        public int Day;
        public int Seed;
        public PhaseRunState Phase = new();
        public List<DecisionRunState> Decisions = new();
        public List<EventHistoryRecord> EventHistory = new();
        public List<string> AuditLog = new();
        public Dictionary<string, TenantRunState> Tenants = new();
        public Dictionary<string, RoomRunState> Rooms = new();
        public Dictionary<string, ResourceRunState> Resources = new();
        public Dictionary<string, int> Inventory = new();
        public Dictionary<string, BuffRunState> Buffs = new();
        public RunSummaryState Summary = new();
        public List<string> ResolvedReviewCandidateIds = new();
        public List<ReviewDecisionRecord> ReviewHistory = new();
        public List<PlayerLogEntry> PlayerLogs = new();
public bool HotelHasMirror = true;
        public bool IsStorm;
        public Dictionary<string, List<TenantLogEntry>> TenantLogs = new();
        public Dictionary<string, ChainRunState> Chains = new();
        public List<string> RunFlags = new();

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
