using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

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

    [Header("Tenant Review")]
    public TenantReviewCoordinator tenantReviewCoordinator;
    public EventQueueEmptyEvent onEventQueueEmpty;

    [Header("Listener")]
    public PhaseEnteredEvent onPhaseEntered;

    private Queue<EventConfig> eventQueue = new Queue<EventConfig>();
    private Coroutine _advanceRoutine;
    private bool isProcessing = false;

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

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
        if (onEventProcessed != null)
            onEventProcessed.Unregister(OnEventProcessed);
    }

private void OnPhaseEntered(PhaseEnterData data)
    {
        if (_advanceRoutine != null)
        {
            StopCoroutine(_advanceRoutine);
            _advanceRoutine = null;
        }

        eventQueue.Clear();

        if (preGeneratedEvents.ContainsKey(data.phase))
        {
            foreach (var config in preGeneratedEvents[data.phase])
                eventQueue.Enqueue(config);
        }

        Debug.Log($"[EventManager] Phase {data.phase}: queue={eventQueue.Count}");

        if (data.phase == GamePhase.Dawn && tenantReviewCoordinator != null && tenantReviewCoordinator.TryBeginReview(OnTenantReviewResolved))
        {
            isProcessing = true;
            return;
        }

        if (eventQueue.Count > 0)
        {
            isProcessing = true;
            ProcessNextEvent();
        }
        else
        {
            isProcessing = false;
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
            isProcessing = false;
            NotifyQueueEmpty();
            return;
        }

        EventConfig config = eventQueue.Dequeue();
        TriggerEvent(config);
    }

private void TriggerEvent(EventConfig config)
    {
        if (onPopupEvent == null)
        {
            Debug.LogError("[EventManager] Missing popup event channel; cannot present event");
            isProcessing = false;
            NotifyQueueEmpty();
            return;
        }

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
        BeginAdvanceAfterDelay();
    }

    private void OnTenantReviewResolved()
    {
        Debug.Log("[EventManager] Tenant review resolved");
        BeginAdvanceAfterDelay();
    }

    private void BeginAdvanceAfterDelay()
    {
        if (_advanceRoutine != null)
            StopCoroutine(_advanceRoutine);
        _advanceRoutine = StartCoroutine(AdvanceAfterDelay());
    }

    private IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSeconds(0.3f);
        _advanceRoutine = null;
        ProcessNextEvent();
    }

    private void NotifyQueueEmpty()
    {
        Debug.Log("[EventManager] Queue empty");
        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Raise(0);
    }


}
