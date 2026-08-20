using System;
using System.Collections;
using System.Collections.Generic;
using Hotel.Runtime;
using Hotel.Authoring.Resources;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class SettlementBridge : MonoBehaviour
{
    public static SettlementBridge Instance { get; private set; }

    public static event Action<GameRunState> RunStateRestored;

    [Header("Resource Definitions")]
    public List<ResourceDefinition> resourceDefinitions = new List<ResourceDefinition>();

    [Header("Event Channels")]
    public PhaseEnteredEvent onPhaseEntered;
    public FoodShortageEvent onFoodShortage;
    public ResourceAdjustedEvent onResourceAdjusted;

    [Header("UI")]
    [SerializeField] private UIManager uiManager;

    public GameRunState RunState => _runState;
    public StateReducer Reducer => _reducer;

    private GameRunState _runState;
    private StateReducer _reducer;
    private int _lastSettlementDay;
    private GamePhase _lastPhase;
    private Dictionary<string, int> _pendingSettlementDeltas;
    private Dictionary<string, string> _resourceDisplayNames;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _reducer = new StateReducer();
        GameLaunchContext.TryConsume(out var loadedState, out _);
        _runState = loadedState ?? GameRunState.New(new RunId(Guid.NewGuid().ToString("N")), Environment.TickCount);

        _lastSettlementDay = _runState.Day;
        _lastPhase = ToGamePhase(_runState.Phase.Current);

        MigrateLegacyMedicineToCurrency(_runState);

        foreach (var def in resourceDefinitions)
        {
            if (def == null) continue;
            if (_runState.Resources.ContainsKey(def.resourceId)) continue;
            _runState.Resources[def.resourceId] = new ResourceRunState
            {
                ResourceId = def.resourceId,
                DefinitionId = def.name,
                Amount = def.initialAmount
            };
        }

        _resourceDisplayNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var def in resourceDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.resourceId) || string.IsNullOrEmpty(def.displayName))
                continue;
            _resourceDisplayNames[def.resourceId] = def.displayName;
        }

        StartCoroutine(DispatchRunStateRestored());
    }

    private IEnumerator DispatchRunStateRestored()
    {
        yield return null;
        if (_runState == null)
            yield break;
        RunStateRestored?.Invoke(_runState);
    }

    private static void MigrateLegacyMedicineToCurrency(GameRunState state)
    {
        if (state == null) return;
        if (!state.Resources.TryGetValue("medicine", out ResourceRunState legacy))
            return;
        if (state.Resources.ContainsKey("currency"))
            return;
        state.Resources["currency"] = new ResourceRunState
        {
            ResourceId = "currency",
            DefinitionId = "currency",
            Amount = legacy.Amount
        };
        state.Resources.Remove("medicine");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            RunStateRestored = null;
        }
    }

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
    }

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        if (_runState == null)
        {
            Debug.LogError("[SettlementBridge] GameRunState is null, skipping settlement");
            return;
        }

        GamePhase previousPhase = _lastPhase;

        PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
            PlayerLogCategory.PhaseTransition,
            data.day,
            ToHotelPhase(data.phase),
            "阶段推进",
            $"第 {data.day} 天 · {PhaseName(data.phase)} 开始",
            null));

        bool crossedNewDayBoundary = previousPhase == GamePhase.Night && data.day > _lastSettlementDay;
        bool completedNewDaySettlement = false;
        Dictionary<string, int> foodDeltas = new Dictionary<string, int>(StringComparer.Ordinal);
        if (crossedNewDayBoundary)
        {
            if (ExecuteFoodSettlement(data.day, ToHotelPhase(data.phase), out foodDeltas))
            {
                _lastSettlementDay = data.day;
                completedNewDaySettlement = true;
            }
        }

        _runState.Day = data.day;
        _runState.Phase.Current = ToHotelPhase(data.phase);
        _runState.Phase.Lifecycle = PhaseLifecycleState.Entered;
        _lastPhase = data.phase;

        Dictionary<string, int> buffDeltas = null;
        if (crossedNewDayBoundary)
            EventEffectManager.TickBuffs(_runState, _reducer, RoomFloorRegistry.Instance, out buffDeltas);

        bool shouldAutosave = data.phase == GamePhase.Dawn || completedNewDaySettlement;
        if (shouldAutosave && !SaveGameService.TrySave(GameLaunchContext.ActiveSlot, _runState, out var error))
            Debug.LogError($"[SettlementBridge] Dawn autosave failed: {error}");

        PublishHalfDayNotice(previousPhase, foodDeltas, buffDeltas);
    }

    private bool ExecuteFoodSettlement(int day, HotelPhase phase, out Dictionary<string, int> settledDeltas)
    {
        settledDeltas = new Dictionary<string, int>(StringComparer.Ordinal);
        int countTenants = 0;
        foreach (var kvp in _runState.Tenants)
        {
            if (!string.IsNullOrEmpty(kvp.Value.RoomId))
                countTenants++;
        }

        if (countTenants == 0)
        {
            Debug.Log("[SettlementBridge] No assigned tenants, skipping food settlement");
            return true;
        }

        int available = 0;
        if (_runState.Resources.TryGetValue("food", out var foodRes))
            available = foodRes.Amount;

        int consumed = Mathf.Min(countTenants, available);
        int shortage = Mathf.Max(0, countTenants - available);

        var changeSet = AuthorizedChangeSet.Coordinator(
            _runState.RunId,
            _runState.StateVersion,
            $"Day{day}FoodSettlement");
        changeSet.Add(new AdjustResourceChange("food", -consumed));
        changeSet.Add(new AppendAuditLogChange($"Day {day} food settlement: consumed {consumed}, shortage {shortage}"));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);

        if (result.Succeeded)
        {
            if (consumed != 0)
                settledDeltas["food"] = -consumed;

            PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
                PlayerLogCategory.ResourceFood,
                day,
                phase,
                "食物结算",
                shortage > 0
                    ? $"第 {day} 天食物结算：消耗 {consumed}、短缺 {shortage}"
                    : $"第 {day} 天食物结算：消耗 {consumed}",
                "food"));

            if (onResourceAdjusted != null && _runState.Resources.TryGetValue("food", out var foodAfter))
            {
                onResourceAdjusted.Raise(new ResourceAdjustedData
                {
                    resourceId = "food",
                    delta = -consumed,
                    newAmount = foodAfter.Amount
                });
            }

            if (shortage > 0 && onFoodShortage != null)
            {
                onFoodShortage.Raise(new FoodShortageData
                {
                    day = day,
                    shortageAmount = shortage
                });
            }

            Debug.Log($"[SettlementBridge] Day {day} settlement: consumed={consumed}, shortage={shortage}");
            return true;
        }
        else
        {
            Debug.LogError("[SettlementBridge] Food settlement commit failed");
            return false;
        }
    }

    public int GetResourceAmount(string resourceId)
    {
        return ResourceService.GetAmount(_runState, resourceId);
    }

    public bool TrySettleJobs(int day, GamePhase phase)
    {
        if (phase != GamePhase.Day && phase != GamePhase.Night)
            return true;
        IReadOnlyList<TenantReviewCandidateSO> candidates = TenantReviewCoordinator.Instance != null
            ? TenantReviewCoordinator.Instance.candidates
            : null;
        bool success = JobSettlementService.TrySettle(
            _runState,
            _reducer,
            day,
            ToHotelPhase(phase),
            candidates,
            RoomFloorRegistry.Instance,
            onResourceAdjusted,
            out Dictionary<string, int> settledDeltas);
        _pendingSettlementDeltas = success && settledDeltas != null && settledDeltas.Count > 0
            ? new Dictionary<string, int>(settledDeltas, StringComparer.Ordinal)
            : null;
        return success;
    }

    private void PublishHalfDayNotice(
        GamePhase previousPhase,
        Dictionary<string, int> foodDeltas,
        Dictionary<string, int> buffDeltas)
    {
        if (_pendingSettlementDeltas == null)
            return;

        bool nightSettlement = previousPhase == GamePhase.Night;
        Dictionary<string, int> merged = NoticeTextFormatter.MergeDeltas(
            _pendingSettlementDeltas,
            nightSettlement ? foodDeltas : null,
            nightSettlement ? buffDeltas : null);

        _pendingSettlementDeltas = null;

        string text = NoticeTextFormatter.FormatHalfDaySettlement(merged, ResolveResourceName);
        if (uiManager == null || string.IsNullOrEmpty(text))
            return;
        uiManager.ShowNotice(text);
    }

    private string ResolveResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return null;
        if (_resourceDisplayNames != null && _resourceDisplayNames.TryGetValue(resourceId, out string name))
            return name;
        return null;
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

    private static string PhaseName(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Dawn: return "黎明";
            case GamePhase.Dusk: return "黄昏";
            case GamePhase.Night: return "黑夜";
            default: return "白天";
        }
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
}
