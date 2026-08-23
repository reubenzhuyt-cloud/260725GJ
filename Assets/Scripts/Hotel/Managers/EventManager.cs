using System;
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

    [Header("Special Visitors")]
    public List<EventConfig> specialVisitorConfigs = new List<EventConfig>();
    [SerializeField] private bool testForceDay1Merchant = false;

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

    [Header("UI")]
    [SerializeField] private UIManager uiManager;

    private readonly Queue<EventConfig> eventQueue = new Queue<EventConfig>();
    private EventConfig _currentConfig;
    private string _currentProtagonistTenantId;
    private EventProcessedData _pendingPayload;
    private EventEffectManager _effectManager;
    private bool waitingForPhaseGate;
    private int _activeDay;
    private GamePhase _activePhase;
    private int _plannedDay = -1;
    private int _settleRetryCount;
    private bool _settlementBlocked;
    private string _abandonedCleanupEventId;
    private int _triggerDay;
    private GamePhase _triggerPhase;
    private const int MaxSettleRetries = 5;

    private enum EventTriggerResult
    {
        Triggered,
        Deferred,
        Skipped
    }

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
        SpecialVisitorManager.ForceDay1MerchantTest = testForceDay1Merchant;
        _effectManager = new EventEffectManager();
        runtimeWeightModifiers.Clear();
        _plannedDay = -1;
        ChainManager.NoticeProvider = text => { if (uiManager != null) uiManager.ShowNotice(text); };
        ChainManager.ResetSessionState();
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
        _settleRetryCount = 0;
        _settlementBlocked = false;
        _abandonedCleanupEventId = null;
        if (_pendingPayload != null)
        {
            EventSettleResult pendingResult = EventSettleResult.Pending;
            try
            {
                pendingResult = TrySettleProcessedEvent(_pendingPayload);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[EventManager] Phase change settlement attempt for '{_pendingPayload.eventId}' threw: {exception}; will keep retrying.");
            }
            if (pendingResult == EventSettleResult.Settled)
            {
                Debug.Log($"[EventManager] Phase change resolved pending settlement for '{_pendingPayload.eventId}'.");
                _pendingPayload = null;
                _currentConfig = null;
                _currentProtagonistTenantId = null;
            }
            else if (pendingResult == EventSettleResult.Rejected)
            {
                EventProcessedData rejectedPayload = _pendingPayload;
                _pendingPayload = null;
                HandleRejectedSettlement(rejectedPayload);
            }
            else
            {
                Debug.LogWarning($"[EventManager] Phase change preserved pending settlement for '{_pendingPayload.eventId}'; will keep retrying.");
            }
        }
        _activeDay = data.day;
        _activePhase = data.phase;

        if (preGeneratedEvents.TryGetValue(data.phase, out var planned))
        {
            foreach (var config in planned)
            {
                if (config != null) eventQueue.Enqueue(config);
            }
            // Consume the plan: a duplicate OnPhaseEntered for this phase must not replay it.
            preGeneratedEvents.Remove(data.phase);
        }

        if (data.phase == GamePhase.Day)
        {
            EnqueueDueSpecialVisitorEvents(data.day);
            EnqueueDueChainEvents(data.day);
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
        if (_plannedDay == day)
        {
            Debug.Log($"[EventManager] Day {day} already planned; duplicate Day phase entry ignored.");
            return;
        }

        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
        {
            Debug.LogWarning("[EventManager] SettlementBridge/RunState unavailable; skipping day event planning.");
            preGeneratedEvents.Clear();
            return;
        }

        _plannedDay = day;

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
            var effective = new Dictionary<string, float>(remaining.Count);
            for (int j = 0; j < remaining.Count; j++)
            {
                string id = remaining[j].eventId;
                float external = runtimeWeightModifiers.TryGetValue(id, out float externalValue) ? externalValue : 1f;
                float computed = EventConditionEvaluator.ComputeWeightModifier(remaining[j].trigger, state);
                effective[id] = external * computed;
            }

            int pickSeed = EventSelectionService.DeriveSeed(baseSeed, i + 1);
            EventConfig config = EventSelectionService.PickWeighted(remaining, effective, pickSeed);
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
        IsPhaseComplete = false;
        while (eventQueue.Count > 0)
        {
            EventConfig next = eventQueue.Peek();
            if (next == null)
            {
                eventQueue.Dequeue();
                continue;
            }
            if (!IsEventStillEligible(next))
            {
                eventQueue.Dequeue();
                Debug.Log($"[EventManager] Skipping event no longer eligible at dequeue: {next.eventId}");
                continue;
            }
            if (_currentConfig != null)
            {
                Debug.LogError($"[EventManager] Cannot trigger '{next.eventId}': popup '{_currentConfig.eventId}' is still active. Deferred; event stays queued.");
                return;
            }
            if (onPopupEvent == null)
            {
                eventQueue.Dequeue();
                Debug.LogError("[EventManager] onPopupEvent channel is null; cannot display popup. Skipping event safely so the phase can complete.");
                continue;
            }

            var bridge = SettlementBridge.Instance;
            if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
            {
                Debug.LogWarning($"[EventManager] SettlementBridge unavailable when dequeuing '{next.eventId}'; deferring presentation.");
                return;
            }

            EventConfig config = eventQueue.Dequeue();
            EventTriggerResult triggerResult = TriggerEvent(config);
            if (triggerResult == EventTriggerResult.Deferred)
            {
                RequeueEvent(config);
                return;
            }
            if (triggerResult == EventTriggerResult.Triggered)
            {
                return;
            }
        }
        NotifyQueueEmpty();
    }

    private EventTriggerResult TriggerEvent(EventConfig config)
    {
        if (onPopupEvent == null)
        {
            Debug.LogError("[EventManager] onPopupEvent is null; popup cannot be shown.");
            return EventTriggerResult.Deferred;
        }
        if (_currentConfig != null)
        {
            Debug.LogError($"[EventManager] Cannot trigger '{config.eventId}': popup '{_currentConfig.eventId}' is still active. Deferred; event stays queued.");
            return EventTriggerResult.Deferred;
        }

        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
        {
            Debug.LogWarning($"[EventManager] Cannot trigger '{config.eventId}': SettlementBridge/RunState/Reducer is unavailable. Requeueing event.");
            return EventTriggerResult.Deferred;
        }

        if (!RecordEventPlanned(config))
        {
            Debug.LogWarning($"[EventManager] RecordEventPlanned failed for '{config.eventId}'. Event skipped.");
            return EventTriggerResult.Skipped;
        }

        DisplayEventPopup(config, bridge);
        return EventTriggerResult.Triggered;
    }

    private void DisplayEventPopup(EventConfig config, SettlementBridge bridge)
    {
        _currentConfig = config;
        _triggerDay = _activeDay > 0 ? _activeDay : (bridge != null && bridge.RunState != null ? bridge.RunState.Day : 0);
        _triggerPhase = _activeDay > 0 ? _activePhase : ToGamePhase(bridge != null && bridge.RunState != null ? bridge.RunState.Phase.Current : HotelPhase.Day);
        _currentProtagonistTenantId = ResolveProtagonist(bridge != null ? bridge.RunState : null);

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

        MergeChainRuntimePatch(config, ref data);

        onPopupEvent.Raise(data);
        Debug.Log($"[EventManager] Triggered: {config.eventTitle}");
    }

    /// <summary>
    /// Merges the chain compatibility layer (ChainRuntimeCatalog) into the popup
    /// payload: extra effects per confirm/choice plus locked choices. The merged
    /// arrays flow into EventProcessedData so settlement applies them through the
    /// standard EventEffectManager -> EventEffectExecutor path.
    /// </summary>
    private static void MergeChainRuntimePatch(EventConfig config, ref PopupData data)
    {
        if (config == null || config.trigger == null || config.trigger.kind != EventKind.ChainStep)
            return;
        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
            return;
        ChainRuntimePatch patch = ChainManager.BuildRuntimePatch(
            bridge.RunState,
            config,
            TenantReviewCoordinator.Instance != null ? TenantReviewCoordinator.Instance.candidates : null);
        if (patch == null)
            return;

        if (patch.ConfirmEffects != null && patch.ConfirmEffects.Length > 0)
            data.confirmEffects = AppendEffects(data.confirmEffects, patch.ConfirmEffects);

        if (patch.ChoiceLocked != null)
            data.choiceLocked = patch.ChoiceLocked;

        if (patch.ChoiceEffects != null && data.choiceEffects != null)
        {
            for (int i = 0; i < data.choiceEffects.Length; i++)
            {
                if (patch.ChoiceEffects.TryGetValue(i, out EventEffect[] extra) && extra != null && extra.Length > 0)
                    data.choiceEffects[i] = AppendEffects(data.choiceEffects[i], extra);
            }
        }
    }

    private static EventEffect[] AppendEffects(EventEffect[] baseEffects, EventEffect[] extra)
    {
        int baseCount = baseEffects != null ? baseEffects.Length : 0;
        if (extra == null || extra.Length == 0)
            return baseEffects;
        var result = new EventEffect[baseCount + extra.Length];
        if (baseCount > 0)
            Array.Copy(baseEffects, result, baseCount);
        Array.Copy(extra, 0, result, baseCount, extra.Length);
        return result;
    }

    private void OnEventProcessed(string eventId)
    {
        if (!string.IsNullOrEmpty(_abandonedCleanupEventId))
        {
            Debug.LogWarning($"[EventManager] Ignoring processed event '{eventId}' while abandoned cleanup for '{_abandonedCleanupEventId}' is pending.");
            return;
        }

        if (_pendingPayload != null)
        {
            Debug.LogWarning($"[EventManager] Ignoring processed event '{eventId}' while settlement for '{_pendingPayload.eventId}' is pending.");
            return;
        }

        Debug.Log($"[EventManager] Event processed: {eventId}");

        EventProcessedData payload;
        try
        {
            payload = ResolveProcessedPayload(eventId);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[EventManager] Payload resolution for '{eventId}' threw: {exception}");
            ClearActiveEvent();
            AdvanceQueue();
            return;
        }

        if (payload == null || string.IsNullOrEmpty(payload.eventId))
        {
            Debug.LogError($"[EventManager] Invalid processed payload for '{eventId}'; refusing to settle. Clearing active event and advancing.");
            ClearActiveEvent();
            AdvanceQueue();
            return;
        }

        try
        {
            EventSettleResult settleResult = TrySettleProcessedEvent(payload);
            if (settleResult == EventSettleResult.Settled)
            {
                ClearActiveEvent();
                AdvanceQueue();
                return;
            }
            if (settleResult == EventSettleResult.Rejected)
            {
                HandleRejectedSettlement(payload);
                return;
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[EventManager] Settlement of '{payload.eventId}' threw: {exception}; preserving payload for bounded retry.");
            _pendingPayload = payload;
            _settleRetryCount = 1;
            _settlementBlocked = false;
            return;
        }

        _pendingPayload = payload;
        _settleRetryCount = 1;
        _settlementBlocked = false;
    }

    private void Update()
    {
        if (!string.IsNullOrEmpty(_abandonedCleanupEventId))
        {
            var bridge = SettlementBridge.Instance;
            if (bridge != null && bridge.RunState != null && bridge.Reducer != null)
            {
                var set = AuthorizedChangeSet.Domain(bridge.RunState.RunId, bridge.RunState.StateVersion, "EventManager", "ResolveAbandonedEvent");
                set.Add(new ResolveEventHistoryChange(_abandonedCleanupEventId, string.Empty));
                CommitResult cleanupResult = bridge.Reducer.TryCommit(bridge.RunState, set);
                if (cleanupResult.Succeeded)
                {
                    _abandonedCleanupEventId = null;
                    _pendingPayload = null;
                    ClearActiveEvent();
                    AdvanceQueue();
                    return;
                }
                Debug.LogWarning($"[EventManager] Retry resolving abandoned event history for '{_abandonedCleanupEventId}' failed; will retry next frame.");
            }
            return;
        }

        if (_pendingPayload == null || _settlementBlocked)
            return;

        if (_settleRetryCount >= MaxSettleRetries)
        {
            _settlementBlocked = true;
            Debug.LogError($"[EventManager] Settlement of '{_pendingPayload.eventId}' failed after {MaxSettleRetries} attempts; abandoning retry. History record remains unresolved and is flagged for explicit cleanup.");

            _abandonedCleanupEventId = _pendingPayload.eventId;
            var bridge = SettlementBridge.Instance;
            if (bridge != null && bridge.RunState != null && bridge.Reducer != null)
            {
                var set = AuthorizedChangeSet.Domain(bridge.RunState.RunId, bridge.RunState.StateVersion, "EventManager", "ResolveAbandonedEvent");
                set.Add(new ResolveEventHistoryChange(_abandonedCleanupEventId, string.Empty));
                CommitResult cleanupResult = bridge.Reducer.TryCommit(bridge.RunState, set);
                if (cleanupResult.Succeeded)
                {
                    _abandonedCleanupEventId = null;
                    _pendingPayload = null;
                    ClearActiveEvent();
                    AdvanceQueue();
                    return;
                }
                Debug.LogWarning($"[EventManager] Initial cleanup commit failed for '{_abandonedCleanupEventId}'; will retry in subsequent frames.");
            }
            return;
        }

        _settleRetryCount++;
        try
        {
            EventSettleResult retryResult = TrySettleProcessedEvent(_pendingPayload);
            if (retryResult == EventSettleResult.Settled)
            {
                _pendingPayload = null;
                ClearActiveEvent();
                AdvanceQueue();
            }
            else if (retryResult == EventSettleResult.Rejected)
            {
                EventProcessedData rejectedPayload = _pendingPayload;
                _pendingPayload = null;
                HandleRejectedSettlement(rejectedPayload);
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[EventManager] Settlement attempt {_settleRetryCount} of {MaxSettleRetries} for '{_pendingPayload.eventId}' threw: {exception}; will retry.");
        }
    }

    private EventProcessedData ResolveProcessedPayload(string eventId)
    {
        if (_currentConfig == null || _currentConfig.eventId != eventId)
        {
            Debug.LogError($"[EventManager] Processed event '{eventId}' does not match active popup '{(_currentConfig != null ? _currentConfig.eventId : "(none)")}'; ignored.");
            return null;
        }
        if (onEventProcessed == null
            || onEventProcessed.LastProcessedData == null
            || onEventProcessed.LastProcessedData.eventId != eventId)
        {
            Debug.LogError($"[EventManager] No processed payload available for '{eventId}'; refusing to settle without effects.");
            return null;
        }

        EventProcessedData source = onEventProcessed.LastProcessedData;
        EventProcessedData data = new EventProcessedData
        {
            eventId = source.eventId,
            optionId = source.optionId,
            effects = source.effects,
            ownerTenantId = source.ownerTenantId,
            requiredTags = source.requiredTags,
            noticeText = source.noticeText
        };
        if (string.IsNullOrEmpty(data.ownerTenantId))
            data.ownerTenantId = _currentProtagonistTenantId;
        return data;
    }

    private void ClearActiveEvent()
    {
        _currentConfig = null;
        _currentProtagonistTenantId = null;
    }

    private void AdvanceQueue()
    {
        if (eventQueue.Count > 0)
        {
            ProcessNextEvent();
        }
        else if (!IsPhaseComplete)
        {
            NotifyQueueEmpty();
        }
    }

    private void RequeueEvent(EventConfig config)
    {
        var reordered = new List<EventConfig>(eventQueue.Count + 1) { config };
        reordered.AddRange(eventQueue);
        eventQueue.Clear();
        for (int i = 0; i < reordered.Count; i++)
            eventQueue.Enqueue(reordered[i]);
    }

    private bool IsEventStillEligible(EventConfig config)
    {
        if (config == null || string.IsNullOrEmpty(config.eventId) || config.trigger == null)
            return false;

        var bridge = SettlementBridge.Instance;
        GameRunState state = bridge != null ? bridge.RunState : null;
        if (state == null)
            return true;

        if (config.trigger.kind == EventKind.ChainStep)
            return ChainManager.IsChainStepStillEligible(state, config, _activeDay);

        if (config.trigger.kind == EventKind.SpecialVisitor)
            return SpecialVisitorManager.IsSpecialVisitorStillEligible(state, config, _activeDay, _activePhase);

        if (_activeDay <= 0)
            return true;

        int day = _activeDay;
        GamePhase phase = _activePhase;
        List<EventConfig> matches = EventSelectionService.FilterCandidates(
            new[] { config },
            day,
            phase,
            state.EventHistory,
            state,
            TenantReviewCoordinator.Instance != null ? TenantReviewCoordinator.Instance.candidates : null,
            RoomFloorRegistry.Instance);
        return matches != null && matches.Count > 0;
    }

    private static GamePhase ToGamePhase(HotelPhase phase)
    {
        switch (phase)
        {
            case HotelPhase.Dawn: return GamePhase.Dawn;
            case HotelPhase.Dusk: return GamePhase.Dusk;
            case HotelPhase.Night: return GamePhase.Night;
            default: return GamePhase.Day;
        }
    }

    private static HotelPhase ToHotelPhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Dawn: return HotelPhase.Dawn;
            case GamePhase.Dusk: return HotelPhase.Dusk;
            case GamePhase.Night: return HotelPhase.Night;
            default: return HotelPhase.Day;
        }
    }

    private EventSettleResult TrySettleProcessedEvent(EventProcessedData payload)
    {
        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
            return EventSettleResult.Pending;
        if (_effectManager == null)
            _effectManager = new EventEffectManager();

        int logDay = _triggerDay > 0 ? _triggerDay : 0;
        HotelPhase? logPhase = _triggerDay > 0 ? ToHotelPhase(_triggerPhase) : (HotelPhase?)null;
        EventSettleResult result = _effectManager.TrySettle(
            bridge.RunState,
            bridge.Reducer,
            payload,
            out PlayerLogWriteDto effectSummary,
            out bool committed,
            out string effectNoticeText,
            logDay,
            logPhase,
            TenantReviewCoordinator.Instance != null ? TenantReviewCoordinator.Instance.candidates : null);
        if (result != EventSettleResult.Settled)
            return result;
        if (!committed)
            return EventSettleResult.Settled;

        if (uiManager != null)
        {
            string finalNotice = !string.IsNullOrEmpty(payload.noticeText)
                ? payload.noticeText
                : (string.IsNullOrEmpty(effectNoticeText) ? "无事发生" : effectNoticeText);
            uiManager.ShowNotice(finalNotice);
        }

        RecordEventLog(bridge.RunState, payload);
        if (effectSummary.Summary != null)
            PlayerLogManager.Record(bridge.RunState, effectSummary);
        return EventSettleResult.Settled;
    }

    private void HandleRejectedSettlement(EventProcessedData payload)
    {
        _pendingPayload = null;
        _settleRetryCount = 0;
        if (payload != null)
            Debug.LogWarning($"[EventManager] Settlement of '{payload.eventId}' rejected: selected option is not valid in the current state (unaffordable or missing required ability). Reopening event for a new selection.");
        else
            Debug.LogWarning("[EventManager] Settlement rejected: selected option is not valid in the current state. Reopening event for a new selection.");

        if (_currentConfig == null)
        {
            AdvanceQueue();
            return;
        }

        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null || onPopupEvent == null)
        {
            Debug.LogWarning($"[EventManager] Cannot reopen rejected event '{_currentConfig.eventId}': dependencies unavailable.");
            ClearActiveEvent();
            AdvanceQueue();
            return;
        }

        DisplayEventPopup(_currentConfig, bridge);
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

        int day = _triggerDay > 0 ? _triggerDay : state.Day;
        HotelPhase phase = _triggerDay > 0 ? ToHotelPhase(_triggerPhase) : state.Phase.Current;
        PlayerLogManager.Record(state, new PlayerLogWriteDto(
            category,
            day,
            phase,
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

        if (_currentConfig != null && _currentConfig.trigger != null && _currentConfig.trigger.kind == EventKind.ChainStep)
        {
            string chainOwner = ChainManager.ResolveChainOwner(state, _currentConfig);
            if (!string.IsNullOrEmpty(chainOwner))
                return chainOwner;
        }

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
    /// Public injection point used by ChainManager. Enqueues an event outside the
    /// random selection flow. The event goes through the same popup/settlement
    /// pipeline as selected events. Duplicates are ignored both by object
    /// reference and by non-empty eventId, so the same event can never sit in the
    /// queue twice; distinct events are unaffected.
    /// </summary>
    public void EnqueueEvent(EventConfig config)
    {
        if (config == null)
            return;
        if (_currentConfig != null && _currentConfig.eventId == config.eventId)
            return;
        foreach (EventConfig queued in eventQueue)
        {
            if (queued == config)
                return;
            if (queued != null
                && !string.IsNullOrEmpty(queued.eventId)
                && queued.eventId == config.eventId)
                return;
        }
        eventQueue.Enqueue(config);
        Debug.Log($"[EventManager] Enqueued injected event: {config.eventId}");
    }

    /// <summary>True while a ChainStep event is pending or being presented.</summary>
    public bool HasPendingChainEvent()
    {
        if (_currentConfig != null && _currentConfig.trigger != null && _currentConfig.trigger.kind == EventKind.ChainStep)
            return true;
        foreach (EventConfig config in eventQueue)
        {
            if (config != null && config.trigger != null && config.trigger.kind == EventKind.ChainStep)
                return true;
        }
        return false;
    }

    private void EnqueueDueSpecialVisitorEvents(int day)
    {
        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
            return;

        if (specialVisitorConfigs == null || specialVisitorConfigs.Count == 0)
            return;

        var state = bridge.RunState;
        for (int i = 0; i < specialVisitorConfigs.Count; i++)
        {
            EventConfig config = specialVisitorConfigs[i];
            if (config == null || string.IsNullOrEmpty(config.eventId) || config.trigger == null)
                continue;

            if (config.trigger.kind != EventKind.SpecialVisitor)
                continue;

            if (!config.trigger.AllowsPhase(EventPhase.Day))
                continue;

            if (day < config.trigger.minDay)
                continue;

            if (config.trigger.maxDay > 0 && day > config.trigger.maxDay)
                continue;

            if (SpecialVisitorManager.IsDueOnDay(state, config.eventId, day))
            {
                EnqueueEvent(config);
            }
        }
    }

    /// <summary>
    /// Requests the single chain step due on this Day and injects it through
    /// EnqueueEvent, bypassing random selection entirely (ChainStep events are
    /// never part of the random filter). No-op when SettlementBridge is missing.
    /// </summary>
    private void EnqueueDueChainEvents(int day)
    {
        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
            return;
        List<EventConfig> due = ChainManager.BuildDueChainEvents(
            bridge.RunState,
            bridge.Reducer,
            day,
            allEvents,
            TenantReviewCoordinator.Instance != null ? TenantReviewCoordinator.Instance.candidates : null);
        for (int i = 0; i < due.Count; i++)
            EnqueueEvent(due[i]);
    }

    /// <summary>
    /// Records an event as planned (unresolved) in GameRunState.EventHistory through
    /// the StateReducer, mirroring the authorizer pattern used by the tenant
    /// coordinators. Skipped silently when the event is already tracked this run
    /// (the reducer forbids duplicate EventIds). Never blocks the popup.
    /// </summary>
    private bool RecordEventPlanned(EventConfig config)
    {
        if (config == null) return false;

        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
        {
            Debug.Log("[EventManager] SettlementBridge/RunState/Reducer unavailable; event history not recorded.");
            return false;
        }

        var state = bridge.RunState;
        int day = _activeDay > 0 ? _activeDay : state.Day;

        if (config.trigger != null && config.trigger.repeatPolicy == RepeatPolicy.Repeatable)
        {
            if (SpecialVisitorManager.HasOccurredOnDay(state.EventHistory, config.eventId, day))
                return false;
        }
        else
        {
            if (EventSelectionService.HasOccurred(state.EventHistory, config.eventId))
                return false;
        }

        string instanceEventId = config.trigger != null && config.trigger.repeatPolicy == RepeatPolicy.Repeatable
            ? $"{config.eventId}#D{day}"
            : config.eventId;

        if (EventSelectionService.HasOccurred(state.EventHistory, instanceEventId))
            return false;

        var record = new EventHistoryRecord
        {
            EventId = instanceEventId,
            DefinitionId = config.eventId,
            Day = day,
            Phase = _activeDay > 0 ? ToHotelPhase(_activePhase) : state.Phase.Current,
            Occurrence = 1,
            RequiresDecision = config.eventType == GameEventType.Choice,
            Resolved = false
        };

        var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "EventManager", "PlanEvent");
        set.Add(new PlanEventHistoryChange(record));
        CommitResult result = bridge.Reducer.TryCommit(state, set);
        if (!result.Succeeded)
        {
            Debug.LogWarning($"[EventManager] Event history plan rejected for '{config.eventId}'.");
            return false;
        }

        return true;
    }
}
