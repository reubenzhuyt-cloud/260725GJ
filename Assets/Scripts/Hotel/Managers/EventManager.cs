using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }
    public event System.Action PhaseProcessingStarted;
    public bool IsPhaseComplete { get; private set; } = true;

    [Header("Event Catalog (single list; filtered at runtime by TriggerSpec)")]
    public List<EventConfig> allEvents = new List<EventConfig>();

    [Header("Probability Settings")]
    [Tooltip("Chance (percent) that the Day phase enqueues an event when eligible candidates exist. Defaults inside the 40-60 band.")]
    [Range(0, 100)] public int dayEventChance = 50;
    [Tooltip("Chance (percent) that Dawn/Dusk hidden phases enqueue an event when eligible candidates exist. Legacy behavior.")]
    [Range(0, 100)] public int hiddenPhaseChance = 50;

    [Header("SO Channels")]
    public GamePopupEvent onPopupEvent;
    public EventProcessedEvent onEventProcessed;
    public EventQueueEmptyEvent onEventQueueEmpty;

    [Header("Listener")]
    public PhaseEnteredEvent onPhaseEntered;

    private readonly Queue<EventConfig> eventQueue = new Queue<EventConfig>();
    private EventConfig _currentConfig;
    private string _currentProtagonistTenantId;
    private EventProcessedData _pendingPayload;
    private EventEffectManager _effectManager;
    private bool waitingForPhaseGate;

    private readonly Dictionary<GamePhase, List<EventConfig>> preGeneratedEvents =
        new Dictionary<GamePhase, List<EventConfig>>();

    /// <summary>
    /// Per-run runtime weight multipliers keyed by eventId. Never serialized into
    /// EventConfig assets; cleared whenever a session starts so modifiers cannot
    /// leak into a different run.
    /// </summary>
    private readonly Dictionary<string, float> runtimeWeightModifiers = new Dictionary<string, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _effectManager = new EventEffectManager();
        runtimeWeightModifiers.Clear();
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
        _pendingPayload = null;

        if (preGeneratedEvents.TryGetValue(data.phase, out var planned))
        {
            foreach (var config in planned)
            {
                if (config != null) eventQueue.Enqueue(config);
            }
            // Consume the plan: a duplicate OnPhaseEntered for this phase must not replay it.
            preGeneratedEvents.Remove(data.phase);
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
    /// Re-plans events for the entire day by filtering the catalog per phase and
    /// applying phase policies: Night guarantees at least one eligible Normal event;
    /// Day rolls against dayEventChance; Dawn/Dusk keep the legacy hidden-phase
    /// chance. Called once when the Day phase begins. Always reads the live run
    /// seed/state from SettlementBridge; defers (with a log) if unavailable so a
    /// stale default seed is never silently used. Results are cached so
    /// HasPreGeneratedEvents keeps GamePhaseManager's hidden-phase gating working,
    /// and are consumed on each phase entry.
    /// </summary>
    public void PreGenerateDayEvents(int day)
    {
        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
        {
            Debug.LogWarning("[EventManager] SettlementBridge/RunState unavailable; skipping day event planning.");
            preGeneratedEvents.Clear();
            return;
        }

        var state = bridge.RunState;
        IReadOnlyList<EventHistoryRecord> history = state.EventHistory;
        int occurrence = history != null ? history.Count : 0;

        TenantReviewCoordinator coordinator = TenantReviewCoordinator.Instance;
        IReadOnlyList<TenantReviewCandidateSO> candidates =
            coordinator != null ? coordinator.candidates : null;

        preGeneratedEvents.Clear();

        foreach (GamePhase phase in System.Enum.GetValues(typeof(GamePhase)))
        {
            // Dawn belongs to the next calendar day in the phase cycle.
            int phaseDay = phase == GamePhase.Dawn ? day + 1 : day;

            List<EventConfig> candidatesForPhase = EventSelectionService.FilterCandidates(
                allEvents,
                phaseDay,
                phase,
                history,
                bridge.RunState,
                candidates,
                RoomFloorRegistry.Instance);
            if (candidatesForPhase == null || candidatesForPhase.Count == 0) continue;

            int baseSeed = EventSelectionService.ComputeSelectionSeed(state.Seed, phaseDay, phase, occurrence);

            if (phase == GamePhase.Night)
            {
                // Black night guarantees at least one eligible Normal event.
                preGeneratedEvents[phase] = PickEvents(candidatesForPhase, baseSeed, bridge.RunState);
            }
            else if (phase == GamePhase.Day)
            {
                int rollSeed = EventSelectionService.DeriveSeed(baseSeed, EventSelectionService.SaltRoll);
                if (RollPercent(rollSeed) < dayEventChance)
                    preGeneratedEvents[phase] = PickEvents(candidatesForPhase, baseSeed, bridge.RunState);
            }
            else
            {
                // Dawn/Dusk retain the legacy hidden-phase chance behavior.
                int rollSeed = EventSelectionService.DeriveSeed(baseSeed, EventSelectionService.SaltRoll);
                if (RollPercent(rollSeed) < hiddenPhaseChance)
                    preGeneratedEvents[phase] = PickEvents(candidatesForPhase, baseSeed, bridge.RunState);
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
        return preGeneratedEvents.TryGetValue(phase, out var list) && list != null && list.Count > 0;
    }

    /// <summary>
    /// Deterministic weighted pick of 1-2 distinct events from candidates. Derives
    /// the count and per-pick seeds from the base selection seed via the mixing
    /// helper. Robustly guarantees at least one event whenever candidates existed,
    /// even if an unexpected invalid effective weight makes every weighted pick fail.
    /// </summary>
    private List<EventConfig> PickEvents(List<EventConfig> candidates, int baseSeed, GameRunState state)
    {
        var picked = new List<EventConfig>();
        if (candidates == null || candidates.Count == 0) return picked;

        var remaining = new List<EventConfig>(candidates);
        int count = 1;
        if (remaining.Count >= 2)
        {
            int countSeed = EventSelectionService.DeriveSeed(baseSeed, EventSelectionService.SaltCount);
            var rng = new System.Random(countSeed);
            count = rng.NextDouble() < 0.5 ? 1 : 2;
        }

        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            runtimeWeightModifiers.Clear();
            for (int j = 0; j < remaining.Count; j++)
            {
                runtimeWeightModifiers[remaining[j].eventId] =
                    EventConditionEvaluator.ComputeWeightModifier(remaining[j].trigger, state);
            }

            int pickSeed = EventSelectionService.DeriveSeed(baseSeed, i + 1);
            EventConfig config = EventSelectionService.PickWeighted(remaining, runtimeWeightModifiers, pickSeed);
            if (config == null) break;
            picked.Add(config);
            remaining.Remove(config);
        }

        // Night (and any phase) guarantee: never return an empty list when candidates
        // existed. A runtime modifier must never be able to disable the guaranteed event.
        if (picked.Count == 0 && candidates.Count > 0)
        {
            const int FallbackSalt = unchecked((int)0xDEADBEEF);
            int fallbackSeed = EventSelectionService.DeriveSeed(baseSeed, FallbackSalt);
            int fallbackIndex = new System.Random(fallbackSeed).Next(candidates.Count);
            picked.Add(candidates[fallbackIndex]);
        }

        return picked;
    }

    private static int RollPercent(int seed)
    {
        return new System.Random(seed).Next(0, 100);
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
        _currentConfig = config;

        var bridge = SettlementBridge.Instance;
        _currentProtagonistTenantId = ResolveProtagonist(bridge != null ? bridge.RunState : null);

        // Record the event as planned (unresolved) before it is shown, so that
        // OncePerRun / cooldown selection on later days excludes it. The record is
        // only resolved once the player completes the popup (OnEventProcessed).
        RecordEventPlanned(config);

        PopupData data = new PopupData
        {
            eventIndex = config.eventIndex,
            eventId = config.eventId,
            title = config.eventTitle,
            description = config.eventDescription,
            image = config.eventImage,
            eventType = config.eventType,
            eventKind = config.trigger != null ? config.trigger.kind : EventKind.Normal,
        };

        if (config.eventType == GameEventType.Confirm)
        {
            data.confirmEffects = config.confirmEffects.ToArray();
        }
        else if (config.eventType == GameEventType.Choice && config.choices.Count > 0)
        {
            data.choiceTexts = new string[config.choices.Count];
            data.choiceResults = new string[config.choices.Count];
            data.choiceEffectTexts = new string[config.choices.Count];
            data.choiceIds = new string[config.choices.Count];
            data.choiceRequiredTags = new TenantAbility[config.choices.Count][];
            data.choiceEffects = new EventEffect[config.choices.Count][];

            for (int i = 0; i < config.choices.Count; i++)
            {
                data.choiceTexts[i] = config.choices[i].choiceText;
                data.choiceResults[i] = config.choices[i].choiceResult;
                data.choiceEffectTexts[i] = config.choices[i].effectText;
                data.choiceIds[i] = config.choices[i].choiceId;
                data.choiceRequiredTags[i] = config.choices[i].requiredTags != null
                    ? config.choices[i].requiredTags.ToArray()
                    : new TenantAbility[0];
                data.choiceEffects[i] = config.choices[i].choiceEffects.ToArray();
            }
        }

        onPopupEvent.Raise(data);
        Debug.Log($"[EventManager] Triggered: {config.eventTitle}");
    }

    private void OnEventProcessed(string eventId)
    {
        if (_pendingPayload != null)
            return;

        Debug.Log($"[EventManager] Event processed: {eventId}");
        EventProcessedData payload = ResolveProcessedPayload(eventId);
        if (TrySettleProcessedEvent(payload))
        {
            _pendingPayload = null;
            _currentConfig = null;
            _currentProtagonistTenantId = null;
            ProcessNextEvent();
        }
        else
        {
            _pendingPayload = payload;
        }
    }

    private void Update()
    {
        if (_pendingPayload == null)
            return;

        if (TrySettleProcessedEvent(_pendingPayload))
        {
            _pendingPayload = null;
            _currentConfig = null;
            _currentProtagonistTenantId = null;
            ProcessNextEvent();
        }
    }

    private EventProcessedData ResolveProcessedPayload(string eventId)
    {
        EventProcessedData data;
        if (onEventProcessed != null
            && onEventProcessed.LastProcessedData != null
            && onEventProcessed.LastProcessedData.eventId == eventId)
        {
            EventProcessedData source = onEventProcessed.LastProcessedData;
            data = new EventProcessedData
            {
                eventId = source.eventId,
                optionId = source.optionId,
                effects = source.effects,
                ownerTenantId = source.ownerTenantId
            };
        }
        else
        {
            data = new EventProcessedData { eventId = eventId, optionId = string.Empty, effects = null };
        }

        if (string.IsNullOrEmpty(data.ownerTenantId))
            data.ownerTenantId = _currentProtagonistTenantId;
        return data;
    }

    private bool TrySettleProcessedEvent(EventProcessedData payload)
    {
        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
            return false;
        if (_effectManager == null)
            return false;

        if (_effectManager.TrySettle(bridge.RunState, bridge.Reducer, payload, out PlayerLogWriteDto effectSummary, out bool committed) != EventSettleResult.Settled)
            return false;
        if (!committed)
            return true;

        RecordEventLog(bridge.RunState, payload);
        if (effectSummary.Summary != null)
            PlayerLogManager.Record(bridge.RunState, effectSummary);
        return true;
    }

    private void RecordEventLog(GameRunState state, EventProcessedData payload)
    {
        if (_currentConfig == null)
            return;

        EventKind kind = _currentConfig.trigger != null ? _currentConfig.trigger.kind : EventKind.Normal;
        PlayerLogCategory category = kind == EventKind.Normal
            ? PlayerLogCategory.EventChoice
            : PlayerLogCategory.SpecialStory;
        string optionText = ResolveOptionText(payload.optionId);

        PlayerLogManager.Record(state, new PlayerLogWriteDto(
            category,
            state.Day,
            state.Phase.Current,
            _currentConfig.eventTitle,
            $"选择『{optionText}』",
            _currentConfig.eventId));
    }

    private string ResolveOptionText(string optionId)
    {
        if (string.IsNullOrEmpty(optionId))
            return "确认";
        if (_currentConfig == null)
            return optionId;
        for (int i = 0; i < _currentConfig.choices.Count; i++)
        {
            ChoiceOption choice = _currentConfig.choices[i];
            if (choice != null && choice.choiceId == optionId && !string.IsNullOrEmpty(choice.choiceText))
                return choice.choiceText;
        }
        return optionId;
    }

    private string ResolveProtagonist(GameRunState state)
    {
        if (state == null)
            return null;

        string preferred = _currentConfig != null && _currentConfig.trigger != null
            ? _currentConfig.trigger.requiresTenantId
            : null;
        if (!string.IsNullOrEmpty(preferred)
            && state.Tenants.TryGetValue(preferred, out TenantRunState preferredState)
            && !string.IsNullOrEmpty(preferredState.RoomId))
        {
            return preferred;
        }

        var assigned = new List<string>();
        foreach (var pair in state.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                continue;
            if (string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            assigned.Add(pair.Key);
        }
        if (assigned.Count == 0)
            return null;

        assigned.Sort(System.StringComparer.Ordinal);
        string eventId = _currentConfig != null ? _currentConfig.eventId : null;
        int seed = ComputeProtagonistSeed(state, eventId);
        int index = new System.Random(seed).Next(assigned.Count);
        return assigned[index];
    }

    private static int ComputeProtagonistSeed(GameRunState state, string eventId)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + state.Seed;
            h = h * 31 + state.Day;
            h = h * 31 + (int)state.Phase.Current;
            h = h * 31 + state.Phase.Occurrence;
            if (eventId != null)
            {
                for (int i = 0; i < eventId.Length; i++)
                    h = h * 31 + eventId[i];
            }
            return h;
        }
    }

    private void NotifyQueueEmpty()
    {
        IsPhaseComplete = true;
        Debug.Log("[EventManager] Queue empty");
        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Raise(0);
    }

    /// <summary>
    /// Sets a positive runtime weight multiplier for an eventId. Effective weight
    /// becomes baseWeight * modifier. Modifiers are per-run only and are never
    /// written into EventConfig assets. Empty, non-positive, or NaN inputs are
    /// rejected so a user-facing modifier can never zero out or corrupt weights.
    /// </summary>
    public void SetRuntimeWeightModifier(string eventId, float multiplier)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            Debug.LogWarning("[EventManager] SetRuntimeWeightModifier: eventId is empty.");
            return;
        }
        if (float.IsNaN(multiplier) || multiplier <= 0f)
        {
            Debug.LogWarning($"[EventManager] SetRuntimeWeightModifier: multiplier must be positive, got {multiplier} for '{eventId}'.");
            return;
        }
        runtimeWeightModifiers[eventId] = multiplier;
    }

    /// <summary>Removes a runtime weight modifier, restoring the default multiplier of 1.</summary>
    public void ClearRuntimeWeightModifier(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId))
            runtimeWeightModifiers.Remove(eventId);
    }

    /// <summary>Clears all runtime weight modifiers (called implicitly on session start).</summary>
    public void ClearAllRuntimeWeightModifiers()
    {
        runtimeWeightModifiers.Clear();
    }

    /// <summary>Returns the current runtime weight multiplier for an eventId (default 1).</summary>
    public float GetRuntimeWeightModifier(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return 1f;
        return runtimeWeightModifiers.TryGetValue(eventId, out var value) ? value : 1f;
    }

    /// <summary>
    /// Records an event as planned (unresolved) in GameRunState.EventHistory through
    /// the StateReducer, mirroring the authorizer pattern used by the tenant
    /// coordinators. Skipped silently when the event is already tracked this run
    /// (the reducer forbids duplicate EventIds). Never blocks the popup.
    /// </summary>
    private void RecordEventPlanned(EventConfig config)
    {
        if (config == null) return;

        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
        {
            Debug.Log("[EventManager] SettlementBridge/RunState unavailable; event history not recorded.");
            return;
        }

        var state = bridge.RunState;
        if (EventSelectionService.HasOccurred(state.EventHistory, config.eventId))
            return;

        var record = new EventHistoryRecord
        {
            EventId = config.eventId,
            DefinitionId = config.eventId,
            Day = state.Day,
            Phase = state.Phase.Current,
            Occurrence = 1,
            RequiresDecision = config.eventType == GameEventType.Choice,
            Resolved = false
        };

        var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "EventManager", "PlanEvent");
        set.Add(new PlanEventHistoryChange(record));
        CommitResult result = bridge.Reducer.TryCommit(state, set);
        if (!result.Succeeded)
            Debug.Log($"[EventManager] Event history plan rejected for '{config.eventId}'.");
    }
}
