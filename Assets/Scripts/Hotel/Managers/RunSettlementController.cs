using Hotel.Runtime;
using TMPro;
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
            settlementPanel = Object.FindObjectOfType<RunSettlementPanel>(true);
            if (settlementPanel == null)
            {
                settlementPanel = CreateFallbackSettlementUI();
            }
        }

        if (settlementPanel != null)
        {
            settlementPanel.Show(summary);
        }
        else
        {
            Debug.LogError("[RunSettlementController] Failed to display settlement UI.");
        }
    }

    private static RunSettlementPanel CreateFallbackSettlementUI()
    {
        // 1. Root Canvas
        GameObject canvasGo = new GameObject("RunSettlement_FallbackCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 2. Modal Overlay Panel
        GameObject panelGo = new GameObject("RunSettlementPanel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImg = panelGo.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);

        RunSettlementPanel panelComp = panelGo.AddComponent<RunSettlementPanel>();

        // Content Card Container
        GameObject cardGo = new GameObject("ContentCard");
        cardGo.transform.SetParent(panelGo.transform, false);
        var cardRect = cardGo.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(1000, 750);
        var cardImg = cardGo.AddComponent<UnityEngine.UI.Image>();
        cardImg.color = new Color(0.13f, 0.15f, 0.18f, 0.98f);

        // Title Text
        var titleText = CreateText(cardGo.transform, "TitleText", new Vector2(0, 310), new Vector2(900, 50), 28, TextAlignmentOptions.Center, new Color(0.9f, 0.9f, 0.9f));

        // Ending Text
        var endingText = CreateText(cardGo.transform, "EndingText", new Vector2(0, 240), new Vector2(900, 60), 38, TextAlignmentOptions.Center, Color.white);
        endingText.fontStyle = FontStyles.Bold;

        // Story Text
        var storyText = CreateText(cardGo.transform, "StoryText", new Vector2(0, 140), new Vector2(850, 110), 22, TextAlignmentOptions.Center, new Color(0.8f, 0.82f, 0.85f));

        // Statistics Text
        var statsText = CreateText(cardGo.transform, "StatisticsText", new Vector2(0, -60), new Vector2(850, 230), 20, TextAlignmentOptions.Left, new Color(0.75f, 0.78f, 0.8f));

        // Hint Text
        var hintText = CreateText(cardGo.transform, "HintText", new Vector2(0, -220), new Vector2(850, 40), 18, TextAlignmentOptions.Center, new Color(0.55f, 0.58f, 0.6f));

        // Main Menu Button
        GameObject btnGo = new GameObject("MainMenuButton");
        btnGo.transform.SetParent(cardGo.transform, false);
        var btnRect = btnGo.AddComponent<RectTransform>();
        btnRect.anchoredPosition = new Vector2(0, -295);
        btnRect.sizeDelta = new Vector2(280, 60);

        var btnImg = btnGo.AddComponent<UnityEngine.UI.Image>();
        btnImg.color = new Color(0.22f, 0.35f, 0.48f, 1f);
        var button = btnGo.AddComponent<UnityEngine.UI.Button>();

        var btnText = CreateText(btnGo.transform, "BtnText", Vector2.zero, new Vector2(280, 60), 22, TextAlignmentOptions.Center, Color.white);
        btnText.text = "返回主菜单 / Main Menu";

        panelComp.SetupReferences(titleText, endingText, storyText, statsText, hintText, button);
        return panelComp;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textGo = new GameObject(name);
        textGo.transform.SetParent(parent, false);
        var rect = textGo.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        return tmp;
    }
}
