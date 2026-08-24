using System;
using System.Collections.Generic;
using Hotel.Runtime;

/// <summary>
/// Per-chain-step runtime bridge data. Keyed by chainId + step. This is a code-only
/// compatibility layer: the authored Unity assets carry only the base effects that the
/// pipeline can already express, while every mechanic the asset fields cannot model is
/// described here and materialized into normal EventEffect entries that flow through
/// EventEffectExecutor.BuildChanges like any other effect. The registry is the single
/// place to delete once the Unity assets are re-authored with these effects inline.
/// </summary>
public sealed class ChainStepRuntimeSpec
{
    public int Step = 1;

    /// <summary>
    /// Authored narrative timing: days after the chain's first event (offset 0 is
    /// the first event itself). The persisted NextDueDay for a step is the chain's
    /// FirstTriggerDay plus this offset.
    /// </summary>
    public int DayOffsetAfterFirstEvent = 0;

    /// <summary>Flags that must all be present before this step may be launched.</summary>
    public string[] RequireFlags = Array.Empty<string>();

    /// <summary>Extra effects injected when the step is a Confirm event.</summary>
    public List<EventEffect> ConfirmEffects = new List<EventEffect>();

    /// <summary>Extra effects injected per choiceId for Choice events.</summary>
    public Dictionary<string, List<EventEffect>> ChoiceEffects = new Dictionary<string, List<EventEffect>>();

    /// <summary>Flags that must all be present before a specific choiceId may be selected.</summary>
    public Dictionary<string, string[]> RequireFlagsByChoice = new Dictionary<string, string[]>();

    /// <summary>When true, resolving this step releases the child room reserved by an earlier step.</summary>
    public bool ReleasesChildRoom;

    /// <summary>
    /// Choice ids (e.g. UninvitedChild step 1 收留 "A") that are locked while no
    /// vacant room exists. Per-choice so the manager never assumes a fixed id.
    /// </summary>
    public string[] ChoicesRequiringVacantRoom = Array.Empty<string>();
}

public static class ChainRuntimeCatalog
{
    public const string SymbolClueItem = "clue_symbol";
    public const string OtherGroupRecordItem = "clue_other_tenants";
    public const string ChildOccupantId = "sp_uninvitedchild";
    public const string IdentifiedYellowPrefix = "identified_yellow:";
    public const string ChildRoomPrefix = "child_room:";
    public const string LargeSuppliesFood = "food";
    public const int LargeSuppliesAmount = 20;

    public const string TruthT01 = "T01";
    public const string TruthT02 = "T02";
    public const string TruthT03 = "T03";
    public const string TruthT04 = "T04";
    public const string TruthT05 = "T05";
    public const string TruthT06 = "T06";
    public const string TruthT07 = "T07";
    public const string TruthT08 = "T08";

    private static readonly string[] ChainOrder =
    {
        "silentdiary",
        "basementsound",
        "vanishingguest",
        "taintedsupplies",
        "uninvitedchild",
        "walldiary"
    };

    /// <summary>Narrative ability preference used only to pick a more fitting target when available.</summary>
    private static readonly Dictionary<string, TenantAbility> PreferredAbilityByChain =
        new Dictionary<string, TenantAbility>
        {
            { "silentdiary", TenantAbility.FormerEmployee },
            { "basementsound", TenantAbility.FormerEmployee },
            { "walldiary", TenantAbility.FormerEmployee },
            { "taintedsupplies", TenantAbility.Cook }
        };

    private static readonly Dictionary<string, ChainStepRuntimeSpec[]> StepsByChain = BuildSteps();

    public static IReadOnlyList<string> ChainIds => ChainOrder;

    public static bool TryGetSteps(string chainId, out ChainStepRuntimeSpec[] steps)
    {
        return StepsByChain.TryGetValue(chainId, out steps);
    }

    public static bool HasStep(string chainId, int step)
    {
        if (!StepsByChain.TryGetValue(chainId, out ChainStepRuntimeSpec[] steps))
            return false;
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i].Step == step)
                return true;
        }
        return false;
    }

    public static ChainStepRuntimeSpec GetStep(string chainId, int step)
    {
        if (!StepsByChain.TryGetValue(chainId, out ChainStepRuntimeSpec[] steps))
            return null;
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i].Step == step)
                return steps[i];
        }
        return null;
    }

    public static int GetStepDayOffset(string chainId, int step)
    {
        ChainStepRuntimeSpec spec = GetStep(chainId, step);
        int offset = spec != null ? spec.DayOffsetAfterFirstEvent : 0;
        if (offset < 0)
            offset = 0;
        return offset;
    }

    public static TenantAbility GetPreferredAbility(string chainId)
    {
        return PreferredAbilityByChain.TryGetValue(chainId, out TenantAbility ability)
            ? ability
            : TenantAbility.None;
    }

    /// <summary>Finds the authored EventConfig for a chain step in the live catalog.</summary>
    public static EventConfig FindEvent(string chainId, int step, IReadOnlyList<EventConfig> catalog)
    {
        if (catalog == null)
            return null;
        for (int i = 0; i < catalog.Count; i++)
        {
            EventConfig config = catalog[i];
            if (config == null || config.trigger == null)
                continue;
            if (config.trigger.kind != EventKind.ChainStep)
                continue;
            if (config.trigger.chainId == chainId && config.trigger.chainStep == step)
                return config;
        }
        return null;
    }

    /// <summary>Parses "chain_&lt;chainId&gt;_&lt;step&gt;" event ids into chainId + step.</summary>
    public static bool TryParseEvent(string eventId, out string chainId, out int step)
    {
        chainId = null;
        step = 0;
        if (string.IsNullOrEmpty(eventId) || !eventId.StartsWith("chain_", StringComparison.Ordinal))
            return false;
        int lastUnderscore = eventId.LastIndexOf('_');
        if (lastUnderscore <= "chain_".Length)
            return false;
        if (!int.TryParse(eventId.Substring(lastUnderscore + 1), out step))
            return false;
        chainId = eventId.Substring("chain_".Length, lastUnderscore - "chain_".Length);
        return !string.IsNullOrEmpty(chainId);
    }

    private static Dictionary<string, ChainStepRuntimeSpec[]> BuildSteps()
    {
        var steps = new Dictionary<string, ChainStepRuntimeSpec[]>();

        // 沉默者日记 (SilentDiary) offsets: 0,2,4,6,7
        steps["silentdiary"] = new[]
        {
            Step(1, 0),
            Step(2, 2,
                choice: new Dictionary<string, List<EventEffect>>
                {
                    { "A", Fx(ChainFx.SetFlag(SymbolClueItem), ChainFx.GrantItem(SymbolClueItem)) }
                }),
            Step(3, 4),
            Step(4, 6,
                requireFlagsByChoice: new Dictionary<string, string[]> { { "A", new[] { SymbolClueItem } } },
                choice: new Dictionary<string, List<EventEffect>>
                {
                    { "A", Fx(ChainFx.GrantItem(TruthT04)) }
                }),
            Step(5, 7,
                confirm: Fx(ChainFx.GrantItem(TruthT01), ChainFx.LockErosion(60f)))
        };

        // 地下室的神秘声音 (BasementSound) offsets: 0,1,2,3,4
        steps["basementsound"] = new[]
        {
            Step(1, 0),
            Step(2, 1),
            Step(3, 2),
            Step(4, 3),
            Step(5, 4,
                choice: new Dictionary<string, List<EventEffect>>
                {
                    { "A", Fx(ChainFx.Food(LargeSuppliesAmount)) },
                    { "B", Fx(ChainFx.GrantItem(OtherGroupRecordItem)) }
                })
        };

        // 逐渐消失的房客 (VanishingGuest) offsets: 0,1,2,3
        steps["vanishingguest"] = new[]
        {
            Step(1, 0),
            Step(2, 1),
            Step(3, 2),
            Step(4, 3, confirm: Fx(ChainFx.RemoveOwnerTenant()))
        };

        // 被污染的物资 (TaintedSupplies) offsets: 0,1,2
        steps["taintedsupplies"] = new[]
        {
            Step(1, 0),
            Step(2, 1),
            Step(3, 2,
                choice: new Dictionary<string, List<EventEffect>>
                {
                    { "A", Fx(ChainFx.GrantItem(TruthT02)) }
                })
        };

        // 不请自来的孩子 (UninvitedChild) offsets: 0,1,2,3
        steps["uninvitedchild"] = new[]
        {
            Step(1, 0,
                choice: new Dictionary<string, List<EventEffect>>
                {
                    { "A", Fx(ChainFx.ReserveChildRoom(), ChainFx.ConditionalErosion(-2f, ChainConditionKind.AbilityAny, abilities: "Teacher,Doctor")) }
                },
                choicesRequiringVacantRoom: new[] { "A" }),
            Step(2, 1,
                confirm: Fx(ChainFx.ConditionalErosion(1f, ChainConditionKind.AnyTenantErosionAbove, threshold: 40))),
            Step(3, 2,
                choice: new Dictionary<string, List<EventEffect>>
                {
                    { "A", Fx(ChainFx.IdentifyYellowTenant()) }
                }),
            Step(4, 3,
                confirm: Fx(ChainFx.ReleaseChildRoom(), ChainFx.ConditionalErosion(-5f, ChainConditionKind.IdentifiedYellow)),
                releasesChildRoom: true)
        };

        // 墙里的日记本 (WallDiary) offsets: 0,1,2,3,4
        steps["walldiary"] = new[]
        {
            Step(1, 0),
            Step(2, 1),
            Step(3, 2),
            Step(4, 3, confirm: Fx(ChainFx.GrantItem(TruthT07))),
            Step(5, 4, confirm: Fx(ChainFx.GrantItem(TruthT06), ChainFx.RemoveOwnerTenant()))
        };

        return steps;
    }

    private static List<EventEffect> Fx(params EventEffect[] effects)
    {
        return new List<EventEffect>(effects);
    }

    private static ChainStepRuntimeSpec Step(
        int step,
        int dayOffset = 0,
        List<EventEffect> confirm = null,
        Dictionary<string, List<EventEffect>> choice = null,
        Dictionary<string, string[]> requireFlagsByChoice = null,
        bool releasesChildRoom = false,
        string[] choicesRequiringVacantRoom = null)
    {
        return new ChainStepRuntimeSpec
        {
            Step = step,
            DayOffsetAfterFirstEvent = dayOffset,
            ConfirmEffects = confirm ?? new List<EventEffect>(),
            ChoiceEffects = choice ?? new Dictionary<string, List<EventEffect>>(),
            RequireFlagsByChoice = requireFlagsByChoice ?? new Dictionary<string, string[]>(),
            ReleasesChildRoom = releasesChildRoom,
            ChoicesRequiringVacantRoom = choicesRequiringVacantRoom ?? Array.Empty<string>()
        };
    }
}

/// <summary>Shared helpers used by the chain scheduler and the effect executor.</summary>
public static class ChainRoomState
{
    public static bool HasVacantRoom(GameRunState state)
    {
        return PickVacantRoom(state, 0) != null;
    }

    /// <summary>Deterministic pick of an empty room (single-occupancy model), or null.</summary>
    public static string PickVacantRoom(GameRunState state, int seed)
    {
        if (state == null || state.Rooms == null || state.Rooms.Count == 0)
            return null;
        var roomIds = new List<string>();
        foreach (var pair in state.Rooms)
        {
            if (string.IsNullOrEmpty(pair.Key))
                continue;
            if (pair.Value == null || pair.Value.OccupantIds == null || pair.Value.OccupantIds.Count == 0)
                roomIds.Add(pair.Key);
        }
        if (roomIds.Count == 0)
            return null;
        roomIds.Sort(StringComparer.Ordinal);
        if (roomIds.Count == 1)
            return roomIds[0];
        int index = (int)(((uint)seed & 0x7FFFFFFFu) % (uint)roomIds.Count);
        return roomIds[index];
    }
}

/// <summary>Builder helpers that keep the compatibility registry readable.</summary>
public static class ChainFx
{
    public static EventEffect GrantItem(string itemId, int amount = 1)
    {
        return new EventEffect { effectType = EffectType.GrantItem, stringValue = itemId, floatValue = amount };
    }

    public static EventEffect Food(int delta)
    {
        return new EventEffect { effectType = EffectType.ModifyResource, stringValue = "food", floatValue = delta };
    }

    public static EventEffect SetFlag(string flag)
    {
        return new EventEffect { effectType = EffectType.ChainSetFlag, stringValue = flag };
    }

    public static EventEffect LockErosion(float value)
    {
        return new EventEffect { effectType = EffectType.ChainLockErosion, floatValue = value };
    }

    public static EventEffect RemoveOwnerTenant()
    {
        return new EventEffect { effectType = EffectType.ChainRemoveTenant, target = EffectTarget.OwnerTenant };
    }

    public static EventEffect ConditionalErosion(float delta, ChainConditionKind kind, int threshold = 0, string abilities = null)
    {
        return new EventEffect
        {
            effectType = EffectType.ChainConditionalErosion,
            conditionKind = kind,
            floatValue = delta,
            intValue = threshold,
            stringValue = abilities ?? string.Empty,
            target = EffectTarget.AllAssignedTenants
        };
    }

    public static EventEffect IdentifyYellowTenant()
    {
        return new EventEffect { effectType = EffectType.ChainIdentifyYellowTenant };
    }

    public static EventEffect ReserveChildRoom()
    {
        return new EventEffect { effectType = EffectType.ChainReserveChildRoom };
    }

    public static EventEffect ReleaseChildRoom()
    {
        return new EventEffect { effectType = EffectType.ChainReleaseChildRoom };
    }
}
