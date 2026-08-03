using UnityEngine;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }
    public event System.Action PhaseProcessingStarted;
    public bool IsPhaseComplete { get; private set; }

    [Header("Event Pools (drag SO assets here)")]
    public List<EventConfig> dayEvents = new List<EventConfig>();
    public List<EventConfig> nightEvents = new List<EventConfig>();
    public List<EventConfig> dawnEvents = new List<EventConfig>();
    public List<EventConfig> duskEvents = new List<EventConfig>();

    [Header("Probability Settings")]
    [Range(0, 100)] public int normalPhaseChance = 70;   // Day/Night
    [Range(0, 100)] public int hiddenPhaseChance = 50;   // Dawn/Dusk

    [Header("SO Channels")]
    public GamePopupEvent onPopupEvent;
    public EventProcessedEvent onEventProcessed;
    public EventQueueEmptyEvent onEventQueueEmpty;

    [Header("Listener")]
    public PhaseEnteredEvent onPhaseEntered;

    private Queue<EventConfig> eventQueue = new Queue<EventConfig>();
    private bool waitingForPhaseGate;

    private Dictionary<GamePhase, List<EventConfig>> preGeneratedEvents = 
        new Dictionary<GamePhase, List<EventConfig>>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
        if (onEventProcessed != null)
            onEventProcessed.Register(OnEventProcessed);
    }

    private void Start()
    {
        if (TenantReviewCoordinator.Instance != null)
            TenantReviewCoordinator.Instance.ReviewBatchCompleted += OnReviewBatchCompleted;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged += OnAssignmentChanged;
    }

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
        if (onEventProcessed != null)
            onEventProcessed.Unregister(OnEventProcessed);
    }

    private void OnDestroy()
    {
        if (TenantReviewCoordinator.Instance != null)
            TenantReviewCoordinator.Instance.ReviewBatchCompleted -= OnReviewBatchCompleted;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged -= OnAssignmentChanged;
        if (Instance == this)
            Instance = null;
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        IsPhaseComplete = false;
        waitingForPhaseGate = false;
        PhaseProcessingStarted?.Invoke();
        eventQueue.Clear();

        if (preGeneratedEvents.ContainsKey(data.phase))
        {
            foreach (var config in preGeneratedEvents[data.phase])
                eventQueue.Enqueue(config);
        }

        bool hasEvents = eventQueue.Count > 0;
        Debug.Log($"[EventManager] Phase {data.phase}: hasEvents={hasEvents}, queue={eventQueue.Count}");

        var reviewPending = TenantReviewCoordinator.Instance != null
            && TenantReviewCoordinator.Instance.HasScheduledReview(data.day, data.phase);
        var assignmentPending = TenantAssignmentCoordinator.Instance != null
            && TenantAssignmentCoordinator.Instance.HasUnassignedTenants;

        if (reviewPending || assignmentPending)
        {
            waitingForPhaseGate = true;
            Debug.Log($"[EventManager] Waiting for phase gate: review={reviewPending}, assignment={assignmentPending}");
        }
        else if (hasEvents)
        {
            ProcessNextEvent();
        }
        else
        {
            NotifyQueueEmpty();
        }
    }

    private void OnReviewBatchCompleted()
    {
        TryReleasePhaseGate();
    }

    private void OnAssignmentChanged()
    {
        TryReleasePhaseGate();
    }

    private void TryReleasePhaseGate()
    {
        if (!waitingForPhaseGate)
            return;
        if (TenantReviewCoordinator.Instance != null && TenantReviewCoordinator.Instance.IsReviewActive)
            return;
        if (TenantAssignmentCoordinator.Instance != null && TenantAssignmentCoordinator.Instance.HasUnassignedTenants)
            return;

        waitingForPhaseGate = false;
        if (eventQueue.Count > 0)
        {
            ProcessNextEvent();
        }
        else
        {
            NotifyQueueEmpty();
        }
    }

    /// <summary>
    /// Pre-generate all events for an entire day. Called once when Day phase begins.
    /// </summary>
    public void PreGenerateDayEvents(int day)
    {
        preGeneratedEvents.Clear();

        foreach (GamePhase phase in System.Enum.GetValues(typeof(GamePhase)))
        {
            List<EventConfig> pool = GetPoolForPhase(phase);
            if (pool == null || pool.Count == 0) continue;

            int chance = IsHiddenPhase(phase) ? hiddenPhaseChance : normalPhaseChance;
            int roll = Random.Range(0, 100);

            if (roll < chance)
            {
                int count = Random.Range(1, Mathf.Min(3, pool.Count + 1));
                preGeneratedEvents[phase] = PickRandom(pool, count);
            }
        }

        Debug.Log($"[EventManager] Pre-generated events for day {day}: " +
            string.Join(", ", preGeneratedEvents.Keys));
    }

    /// <summary>
    /// Check if a phase has pre-generated events. Used by GamePhaseManager for deterministic skip.
    /// </summary>
    public bool HasPreGeneratedEvents(GamePhase phase)
    {
        return preGeneratedEvents.ContainsKey(phase) && 
               preGeneratedEvents[phase].Count > 0;
    }

    private List<EventConfig> GetPoolForPhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Day:   return dayEvents;
            case GamePhase.Night: return nightEvents;
            case GamePhase.Dawn:  return dawnEvents;
            case GamePhase.Dusk:  return duskEvents;
            default: return null;
        }
    }

    private bool IsHiddenPhase(GamePhase phase)
    {
        return phase == GamePhase.Dawn || phase == GamePhase.Dusk;
    }

    private List<EventConfig> PickRandom(List<EventConfig> pool, int count)
    {
        List<EventConfig> source = new List<EventConfig>(pool);
        List<EventConfig> result = new List<EventConfig>();

        for (int i = 0; i < count && source.Count > 0; i++)
        {
            int idx = Random.Range(0, source.Count);
            result.Add(source[idx]);
            source.RemoveAt(idx);
        }

        return result;
    }

    private void ProcessNextEvent()
    {
        if (eventQueue.Count == 0)
        {
            NotifyQueueEmpty();
            return;
        }

        EventConfig config = eventQueue.Dequeue();
        TriggerEvent(config);
    }

    private void TriggerEvent(EventConfig config)
    {
        if (onPopupEvent == null) return;

        PopupData data = new PopupData
        {
            eventIndex = config.eventIndex,
            eventId = config.eventId,
            title = config.eventTitle,
            description = config.eventDescription,
            image = config.eventImage,
            eventType = config.eventType,
        };

        if (config.eventType == GameEventType.Confirm)
        {
            data.confirmEffects = config.confirmEffects.ToArray();
        }
        else if (config.eventType == GameEventType.Choice && config.choices.Count > 0)
        {
            data.choiceTexts = new string[config.choices.Count];
            data.choiceResults = new string[config.choices.Count];
            data.choiceEffects = new EventEffect[config.choices.Count][];

            for (int i = 0; i < config.choices.Count; i++)
            {
                data.choiceTexts[i] = config.choices[i].choiceText;
                data.choiceResults[i] = config.choices[i].choiceResult;
                data.choiceEffects[i] = config.choices[i].choiceEffects.ToArray();
            }
        }

        onPopupEvent.Raise(data);
        Debug.Log($"[EventManager] Triggered: {config.eventTitle}");
    }

    private void OnEventProcessed(string eventId)
    {
        Debug.Log($"[EventManager] Event processed: {eventId}");
        ProcessNextEvent();
    }

    private void NotifyQueueEmpty()
    {
        IsPhaseComplete = true;
        Debug.Log("[EventManager] Queue empty");
        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Raise(0);
    }


}
