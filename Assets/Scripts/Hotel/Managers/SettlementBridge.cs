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

    public GameRunState RunState => _runState;
    public StateReducer Reducer => _reducer;

    private GameRunState _runState;
    private StateReducer _reducer;
    private int _lastSettlementDay;
    private GamePhase _lastPhase;

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

        PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
            PlayerLogCategory.PhaseTransition,
            data.day,
            ToHotelPhase(data.phase),
            "阶段推进",
            $"第 {data.day} 天 · {PhaseName(data.phase)} 开始",
            null));

        bool crossedNewDayBoundary = _lastPhase == GamePhase.Night && data.day > _lastSettlementDay;
        bool completedNewDaySettlement = false;
        if (crossedNewDayBoundary)
        {
            if (ExecuteFoodSettlement(data.day, ToHotelPhase(data.phase)))
            {
                _lastSettlementDay = data.day;
                completedNewDaySettlement = true;
            }
        }

        _runState.Day = data.day;
        _runState.Phase.Current = ToHotelPhase(data.phase);
        _runState.Phase.Lifecycle = PhaseLifecycleState.Entered;
        _lastPhase = data.phase;

        if (data.phase == GamePhase.Dawn)
            EventEffectManager.TickBuffs(_runState, _reducer, RoomFloorRegistry.Instance);

        bool shouldAutosave = data.phase == GamePhase.Dawn || completedNewDaySettlement;
        if (shouldAutosave && !SaveGameService.TrySave(GameLaunchContext.ActiveSlot, _runState, out var error))
            Debug.LogError($"[SettlementBridge] Dawn autosave failed: {error}");
    }

    private bool ExecuteFoodSettlement(int day, HotelPhase phase)
    {
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
        return JobSettlementService.TrySettle(
            _runState,
            _reducer,
            day,
            ToHotelPhase(phase),
            candidates,
            RoomFloorRegistry.Instance,
            onResourceAdjusted);
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
