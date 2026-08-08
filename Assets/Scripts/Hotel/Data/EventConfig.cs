using UnityEngine;
using System.Collections.Generic;
using Hotel.Runtime;

public enum GamePhase { Day, Dawn, Night, Dusk }

public enum GameEventType { Confirm, Choice }

public enum EffectType { None, ModifyTenantErosion, ModifyResource, ApplyBuff }

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

[System.Serializable]
public class EventEffect
{
    public EffectType effectType = EffectType.None;
    public float floatValue;
    public EffectTarget target = EffectTarget.OwnerTenant;
    public string stringValue = "";
    public int intValue;
    public int durationTicks;
}

/// <summary>
/// Per-event trigger/selection specification. Replaces the legacy single
/// triggerPhase field and dead triggerCondition string.
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

        if (eventType == GameEventType.Choice && choices.Count == 0)
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
