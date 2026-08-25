using Hotel.Runtime;
using UnityEngine;

/// <summary>
/// Owns the one-time Day-30 settlement transaction and opens the result UI.
/// The controller and its result panel are wired explicitly in MainScene.
/// </summary>
public sealed class RunSettlementController : MonoBehaviour
{
    private static RunSettlementController _instance;

    public static RunSettlementController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindObjectOfType<RunSettlementController>(true);
                if (_instance == null)
                {
                    var go = new GameObject("RunSettlementController");
                    _instance = go.AddComponent<RunSettlementController>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [SerializeField] private RunSettlementPanel settlementPanel;

    public bool IsSettlementActive => settlementPanel != null && settlementPanel.gameObject.activeSelf;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        GameRunState state = SettlementBridge.Instance != null
            ? SettlementBridge.Instance.RunState
            : null;
        if (state != null && state.Summary != null && state.Summary.IsComplete)
            Show(state.Summary);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool TryCompleteRun()
    {
        SettlementBridge bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
        {
            Debug.LogError("[RunSettlementController] Cannot settle without an authoritative run state.");
            return false;
        }

        GameRunState state = bridge.RunState;
        if (state.Summary != null && state.Summary.IsComplete)
        {
            Show(state.Summary);
            return true;
        }

        if (state.Day < RunSettlementCalculator.FinalDay
            || state.Phase.Current != HotelPhase.Night)
        {
            Debug.LogWarning("[RunSettlementController] Settlement is only allowed after Night 30.");
            return false;
        }

        RunSummaryState summary = RunSettlementCalculator.Calculate(state, requireCompletedChain: true);
        var set = AuthorizedChangeSet.Coordinator(
            state.RunId,
            state.StateVersion,
            "CompleteRun");
        set.Add(new SetRunSummaryChange(summary));

        CommitResult result = bridge.Reducer.TryCommit(state, set);
        if (!result.Succeeded)
        {
            Debug.LogError("[RunSettlementController] Final settlement transaction failed.");
            return false;
        }

        if (!SaveGameService.TrySave(GameLaunchContext.ActiveSlot, state, out string error))
            Debug.LogError($"[RunSettlementController] Result was calculated but could not be saved: {error}");

        Debug.Log(
            $"[RunSettlementController] {summary.Ending}: survivors={summary.FinalTenantCount}, "
            + $"averageErosion={summary.AverageErosion:0.0}, mistakes="
            + $"{summary.MisclassificationCount}/{summary.ClassifiedTenantCount}, "
            + $"truthItems={summary.TruthItemCount}, completedChains={summary.CompletedChainCount}");

        Show(summary);
        return true;
    }

    private void Show(RunSummaryState summary)
    {
        if (settlementPanel == null)
        {
            Debug.LogError(
                "[RunSettlementController] Settlement Panel is not assigned. Wire the scene-authored panel in the Inspector.",
                this);
            return;
        }

        settlementPanel.Show(summary);
    }
}
