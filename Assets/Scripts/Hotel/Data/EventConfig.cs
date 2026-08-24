using UnityEngine;
using System.Collections.Generic;
using Hotel.Runtime;

public enum GamePhase { Day, Dawn, Night, Dusk }

public enum GameEventType { Confirm, Choice }

public enum EffectType { None = 0, ModifyTenantErosion = 1, ModifyResource = 2, ApplyBuff = 3, GrantItem = 4, ChainSetFlag = 5, ChainLockErosion = 6, ChainRemoveTenant = 7, ChainConditionalErosion = 8, ChainIdentifyYellowTenant = 9, ChainReserveChildRoom = 10, ChainReleaseChildRoom = 11 }

/// <summary>
/// Condition kinds used by EffectType.ChainConditionalErosion. Evaluated at
/// settlement time against the live run state. Data is authored in code
/// (ChainRuntimeCatalog) as a bridge until Unity assets carry richer metadata.
/// </summary>
public enum ChainConditionKind
{
    None = 0,
    /// <summary>Applies when any assigned tenant has TrueErosion &gt; effect.intValue.</summary>
    AnyTenantErosionAbove = 1,
    /// <summary>Applies when any tenant owns any of the abilities in effect.stringValue (comma separated).</summary>
    AbilityAny = 2,
    /// <summary>Applies to the yellow tenant identified by a prior ChainIdentifyYellowTenant step of the same chain.</summary>
    IdentifiedYellow = 3
}

[System.Flags]
public enum EventPhase
{
    Day = 1,
    Night = 2,
    Dawn = 4,
    Dusk = 8,
}

public enum EventKind { Normal, ChainStep, Personal, SpecialVisitor }

public enum RepeatPolicy { OncePerRun, Repeatable }

/// <summary>
/// State-dependent eligibility conditions (EVENTS.md 触发条件 column).
/// Conditions are evaluated against the live GameRunState at event-planning time.
/// </summary>
public enum ConditionType
{
    None = 0,
    YellowTenantExists = 1,
    RedTenantExists = 2,
    RedCountAtLeast = 3,
    YellowCountAtLeast = 4,
    GreenRedSameFloor = 5,
    RedYellowSameFloor = 6,
    TenantErosionAbove = 7,
    TenantErosionBelow = 8,
    FoodBelowDays = 9,
    FoodOrCurrencyAbove = 10,
    TenantWithAbility = 11,
    SpecificTenantPresent = 12,
    VulnerableTenantExists = 13,
    HotelHasMirror = 14,
    IsStorm = 15,
    ResourceAtLeast = 16
}

/// <summary>
/// Optional state-correlated selection-weight scaling. When set, the event's
/// effective weight becomes baseWeight * max(1, matching tenant count), so
/// events like Nightmare Spread grow likelier as Red tenants accumulate.
/// </summary>
public enum EventWeightScale
{
    None = 0,
    RedTenantCount = 1,
    YellowTenantCount = 2
}

[System.Serializable]
public class EventCondition
{
    public ConditionType condition = ConditionType.None;
    public int intValue;
    public float floatValue;
    public string stringValue = "";
}

[System.Serializable]
public class EventEffect
{
    public EffectType effectType = EffectType.None;
    public float floatValue;
    public EffectTarget target = EffectTarget.OwnerTenant;
    public string stringValue = "";
    public int intValue;
    public int durationTicks;
    public ChainConditionKind conditionKind = ChainConditionKind.None;
}

/// <summary>
/// Per-event trigger/selection specification.
/// </summary>
[System.Serializable]
public class TriggerSpec
{
    [Header("Eligibility")]
    [Tooltip("Phases this event may fire in (at least one required).")]
    public EventPhase eligiblePhases = EventPhase.Day | EventPhase.Night;

    [Tooltip("Kind of event. Only Normal is selectable by the current generator; ChainStep/Personal/SpecialVisitor are reserved for their owning systems.")]
    public EventKind kind = EventKind.Normal;

    [Header("Day Window")]
    [Tooltip("Earliest day this event may fire (must be >= 1).")]
    public int minDay = 1;

    [Tooltip("Latest day this event may fire. 0 means no upper limit.")]
    public int maxDay = 0;

    [Header("Repeat")]
    [Tooltip("OncePerRun fires at most once per run; Repeatable may fire again after cooldownDays.")]
    public RepeatPolicy repeatPolicy = RepeatPolicy.Repeatable;

    [Tooltip("Minimum days between repeat occurrences. 0 means no cooldown.")]
    public int cooldownDays = 0;

    [Header("Personal Events (reserved)")]
    [Tooltip("Definition id of the tenant this personal event targets. Unused while kind != Personal.")]
    public string requiresTenantId = "";

    [Tooltip("Optional profile reference for the targeted tenant. Unused while kind != Personal.")]
    public TenantReviewCandidateSO requiresTenant;

    [Header("Chains (reserved)")]
    [Tooltip("Chain this event belongs to. Required when kind == ChainStep.")]
    public string chainId = "";

    [Tooltip("1-based step within the chain. Required when kind == ChainStep.")]
    public int chainStep = 0;

    [Header("Conditions")]
    [Tooltip("State-dependent eligibility conditions (EVENTS.md 触发条件). Empty list = always eligible (随机). When state is unavailable these make the event ineligible.")]
    public List<EventCondition> conditions = new List<EventCondition>();

    [Tooltip("true: every condition must pass (AND). false: any passing condition is enough (OR).")]
    public bool requireAll = true;

    [Tooltip("Optional state-correlated weight scaling: baseWeight * max(1, count of matching tenants).")]
    public EventWeightScale weightScale = EventWeightScale.None;

    [Header("Selection")]
    [Tooltip("Relative selection weight among eligible candidates (must be >= 1).")]
    public int baseWeight = 10;

    [Header("Authoring")]
    [Tooltip("Free-form category label for authoring organization only. Never used for pooling.")]
    public string category = "";

    public bool IsChain => kind == EventKind.ChainStep;
    public bool IsPersonal => kind == EventKind.Personal;

    public bool AllowsPhase(EventPhase phase)
    {
        return (eligiblePhases & phase) != 0;
    }
}

[CreateAssetMenu(fileName = "EventConfig", menuName = "Configs/EventConfig")]
public class EventConfig : ScriptableObject
{
    [Header("Identity")]
    public int eventIndex;
    public string eventId;

    [Header("Trigger")]
    public TriggerSpec trigger = new TriggerSpec();

    [Header("Content")]
    public string eventTitle;
    [TextArea] public string eventDescription;
    public Sprite eventImage;
    public GameEventType eventType = GameEventType.Confirm;

    [Header("Confirm Effects")]
    public List<EventEffect> confirmEffects = new List<EventEffect>();

    [Header("Choice Options")]
    public List<ChoiceOption> choices = new List<ChoiceOption>();

    private void OnValidate()
    {
        ValidateTrigger();
        ValidateContent();
    }

    private void ValidateTrigger()
    {
        TriggerSpec t = trigger;
        if (t == null) return;

        bool hasError = false;

        if (t.eligiblePhases == 0)
        {
            Debug.LogError($"[EventConfig:{name}] Trigger.eligiblePhases is empty; at least one phase must be allowed.", this);
            hasError = true;
        }

        if (t.minDay < 1)
        {
            int oldMinDay = t.minDay;
            t.minDay = 1;
            Debug.LogWarning($"[EventConfig:{name}] Trigger.minDay ({oldMinDay}) clamped to 1.", this);
        }

        if (t.maxDay != 0 && t.maxDay < t.minDay)
        {
            Debug.LogError($"[EventConfig:{name}] Trigger.maxDay ({t.maxDay}) is before minDay ({t.minDay}).", this);
            hasError = true;
        }

        if (t.baseWeight < 1)
        {
            int oldWeight = t.baseWeight;
            t.baseWeight = 1;
            Debug.LogWarning($"[EventConfig:{name}] Trigger.baseWeight ({oldWeight}) clamped to 1.", this);
        }

        if (t.cooldownDays < 0)
        {
            int oldCooldown = t.cooldownDays;
            t.cooldownDays = 0;
            Debug.LogWarning($"[EventConfig:{name}] Trigger.cooldownDays ({oldCooldown}) clamped to 0.", this);
        }

        if (t.conditions != null)
        {
            for (int i = 0; i < t.conditions.Count; i++)
            {
                EventCondition c = t.conditions[i];
                if (c == null) continue;
                if (c.condition == ConditionType.TenantWithAbility &&
                    !System.Enum.IsDefined(typeof(TenantAbility), c.stringValue))
                {
                    Debug.LogWarning($"[EventConfig:{name}] Condition[{i}] TenantWithAbility references unknown ability '{c.stringValue}'; condition can never pass.", this);
                }

                if (c.condition == ConditionType.FoodBelowDays && c.intValue <= 0)
                {
                    Debug.LogWarning($"[EventConfig:{name}] Condition[{i}] FoodBelowDays requires intValue >= 1 (found {c.intValue}); with zero or negative days the condition can never pass.", this);
                }

                if (c.condition == ConditionType.FoodOrCurrencyAbove && c.floatValue <= 0f)
                {
                    Debug.LogWarning($"[EventConfig:{name}] Condition[{i}] FoodOrCurrencyAbove requires floatValue > 0 (found {c.floatValue}); with a zero or negative threshold the condition is trivially true.", this);
                }

                if (c.condition == ConditionType.ResourceAtLeast &&
                    (string.IsNullOrEmpty(c.stringValue) || c.intValue <= 0))
                {
                    Debug.LogWarning($"[EventConfig:{name}] Condition[{i}] ResourceAtLeast requires a resource id and intValue >= 1.", this);
                }

                if ((c.condition == ConditionType.TenantErosionAbove || c.condition == ConditionType.TenantErosionBelow) && c.floatValue <= 0f)
                {
                    Debug.LogWarning($"[EventConfig:{name}] Condition[{i}] {c.condition} requires a positive erosion threshold (found {c.floatValue}); zero or negative is outside the valid [0,100] erosion range.", this);
                }

                if ((c.condition == ConditionType.RedCountAtLeast || c.condition == ConditionType.YellowCountAtLeast) && c.intValue <= 0)
                {
                    Debug.LogWarning($"[EventConfig:{name}] Condition[{i}] {c.condition} requires intValue >= 1 (found {c.intValue}); with a zero or negative count the condition is trivially true.", this);
                }
            }
        }

        if (t.kind == EventKind.ChainStep)
        {
            if (string.IsNullOrEmpty(t.chainId) || t.chainStep < 1)
            {
                Debug.LogError($"[EventConfig:{name}] ChainStep event requires non-empty chainId and chainStep >= 1.", this);
                hasError = true;
            }

            if (t.repeatPolicy != RepeatPolicy.OncePerRun)
            {
                Debug.LogError($"[EventConfig:{name}] ChainStep events must use RepeatPolicy.OncePerRun.", this);
                hasError = true;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(t.chainId) || t.chainStep != 0)
            {
                Debug.LogWarning($"[EventConfig:{name}] Non-chain event has chainId/chainStep set; ignored while kind != ChainStep.", this);
            }
        }

        if (t.kind == EventKind.Personal)
        {
            if (string.IsNullOrEmpty(t.requiresTenantId) && t.requiresTenant == null)
            {
                Debug.LogError($"[EventConfig:{name}] Personal event requires requiresTenantId or a tenant profile reference.", this);
                hasError = true;
            }

            if (t.repeatPolicy != RepeatPolicy.OncePerRun)
            {
                Debug.LogError($"[EventConfig:{name}] Personal events must use RepeatPolicy.OncePerRun.", this);
                hasError = true;
            }
        }

        if (hasError)
        {
            Debug.LogError($"[EventConfig:{name}] TriggerSpec has invalid combinations; event may be excluded from selection.", this);
        }
    }

    private void ValidateContent()
    {
        if (string.IsNullOrEmpty(eventId))
        {
            Debug.LogWarning($"[EventConfig:{name}] eventId is empty; event cannot be uniquely tracked.", this);
        }

        if (eventType == GameEventType.Choice && (choices == null || choices.Count == 0))
        {
            Debug.LogError($"[EventConfig:{name}] Choice event has no choices.", this);
        }
    }
}

[System.Serializable]
public class ChoiceOption
{
    public string choiceId;
    public string choiceText;
    [TextArea] public string choiceResult;
    [TextArea] public string effectText;
    public List<TenantAbility> requiredTags = new List<TenantAbility>();
    public List<EventEffect> choiceEffects = new List<EventEffect>();
}
