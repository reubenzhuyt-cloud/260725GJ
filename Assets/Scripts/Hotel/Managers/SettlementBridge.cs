using System.Collections.Generic;
using Hotel.Runtime;
using Hotel.Authoring.Resources;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class SettlementBridge : MonoBehaviour
{
    public static SettlementBridge Instance { get; private set; }

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
        _runState = GameRunState.New(new RunId("main_run"), 1);

        _lastPhase = GamePhase.Day;

        foreach (var def in resourceDefinitions)
        {
            if (def == null) continue;
            _runState.Resources[def.resourceId] = new ResourceRunState
            {
                ResourceId = def.resourceId,
                DefinitionId = def.name,
                Amount = def.initialAmount
            };
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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

        if (_lastPhase == GamePhase.Night && data.day > _lastSettlementDay)
        {
            if (ExecuteFoodSettlement(data.day))
                _lastSettlementDay = data.day;
        }

        _lastPhase = data.phase;
    }

    private bool ExecuteFoodSettlement(int day)
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
}
