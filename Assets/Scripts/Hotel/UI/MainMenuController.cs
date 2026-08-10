using Hotel.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private const string GameSceneName = "MainScene";

    [System.Serializable]
    public sealed class SlotCardView
    {
        public Button cardButton;
        public TextMeshProUGUI numberText;
        public TextMeshProUGUI dayText;
        public TextMeshProUGUI timeText;
        public Button deleteButton;
    }

    [Header("Slot Grid")]
    [SerializeField] private GameObject slotGridPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private SlotCardView[] slotCards = new SlotCardView[SaveGameService.MaxSlots];

    [Header("Confirm Overlay")]
    [SerializeField] private GameObject confirmOverlay;
    [SerializeField] private TextMeshProUGUI confirmTitle;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    private enum PendingAction : byte { None, NewGame, Delete }
    private PendingAction pendingAction;
    private int pendingSlotIndex;

    private static readonly Color MutedTextColor = new(0.67f, 0.65f, 0.62f, 1f);
    private static readonly Color WarningColor = new(0.94f, 0.62f, 0.49f, 1f);

    private void Awake()
    {
        if (confirmNoButton != null) confirmNoButton.onClick.AddListener(OnConfirmNo);
        if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);

        if (slotGridPanel == null)
            slotGridPanel = FindGameObjectByName("SlotGridPanel");

        if (backButton == null)
            TryFindButtonByName("BackButton", out backButton);
        if (backButton != null)
            backButton.onClick.AddListener(OnBackPressed);

        CloseSlotGrid();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (confirmOverlay != null && confirmOverlay.activeSelf)
            OnConfirmNo();
        else if (slotGridPanel != null && slotGridPanel.activeSelf)
            OnBackPressed();
    }

    private static GameObject FindGameObjectByName(string name)
    {
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == name)
                return go;
        }
        return null;
    }

    private static bool TryFindButtonByName(string name, out Button button)
    {
        button = null;
        var go = FindGameObjectByName(name);
        if (go == null) return false;
        button = go.GetComponent<Button>();
        return button != null;
    }

    /// <summary>Closes the confirm overlay, then the slot grid (also bound to ESC and the grid's Back button).</summary>
    public void OnBackPressed()
    {
        if (confirmOverlay != null && confirmOverlay.activeSelf)
        {
            OnConfirmNo();
            return;
        }

        CloseSlotGrid();
        SetStatus(string.Empty, false);
    }

    /// <summary>Single entry point: opens the grid; each card decides on its own what to do.</summary>
    public void OnNewGamePressed()
    {
        OpenSlotGrid();
    }

    /// <summary>Same as OnNewGamePressed — kept for the old Continue binding, now redundant.</summary>
    public void OnContinuePressed()
    {
        OpenSlotGrid();
    }

    /// <summary>Deleting happens per slot card inside the grid.</summary>
    public void OnDeleteSavePressed()
    {
        OpenSlotGrid();
        SetStatus("点击存档位卡片上的删除按钮", false);
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OpenSlotGrid()
    {
        pendingAction = PendingAction.None;
        RefreshSlots();
        if (slotGridPanel != null) slotGridPanel.SetActive(true);
        if (backButton != null) backButton.gameObject.SetActive(true);
    }

    private void CloseSlotGrid()
    {
        pendingAction = PendingAction.None;
        if (slotGridPanel != null) slotGridPanel.SetActive(false);
        if (backButton != null) backButton.gameObject.SetActive(false);
    }

    private void OnSlotPressed(int index)
    {
        int slot = index + 1;

        if (SaveGameService.HasSave(slot))
        {
            if (!SaveGameService.TryLoad(slot, out var state, out var error))
            {
                SetStatus($"读取失败：{error}", true);
                RefreshSlots();
                return;
            }

            GameLaunchContext.ContinueWith(slot, state);
            SceneManager.LoadScene(GameSceneName);
            return;
        }

        pendingAction = PendingAction.NewGame;
        pendingSlotIndex = index;
        confirmTitle.text = $"新游戏将保存在存档位 {slot}？";
        confirmOverlay.SetActive(true);
    }

    private void OnDeletePressed(int index)
    {
        int slot = index + 1;
        if (!SaveGameService.HasSave(slot))
        {
            SetStatus("该存档位没有存档", false);
            return;
        }

        pendingAction = PendingAction.Delete;
        pendingSlotIndex = index;
        confirmTitle.text = $"删除存档位 {slot} 的存档？";
        confirmOverlay.SetActive(true);
    }

    private void OnConfirmYes()
    {
        if (confirmOverlay != null) confirmOverlay.SetActive(false);

        int slot = pendingSlotIndex + 1;
        switch (pendingAction)
        {
            case PendingAction.NewGame:
                GameLaunchContext.StartNewGame(slot);
                SceneManager.LoadScene(GameSceneName);
                return;

            case PendingAction.Delete:
                if (SaveGameService.DeleteSave(slot, out var error))
                    SetStatus("存档已删除", false);
                else
                    SetStatus($"删除失败：{error}", true);
                RefreshSlots();
                break;
        }

        pendingAction = PendingAction.None;
    }

    private void OnConfirmNo()
    {
        pendingAction = PendingAction.None;
        if (confirmOverlay != null) confirmOverlay.SetActive(false);
    }

    private void RefreshSlots()
    {
        var summaries = SaveGameService.GetAllSummaries();
        int count = slotCards != null ? Mathf.Min(slotCards.Length, SaveGameService.MaxSlots) : 0;

        for (int i = 0; i < count; i++)
        {
            SlotCardView card = slotCards[i];
            if (card == null) continue;

            int slot = i + 1;
            SaveSlotSummary? summary = summaries != null && i < summaries.Length ? summaries[i] : null;
            bool hasSave = summary.HasValue;

            if (card.numberText != null) card.numberText.text = $"存档位 {slot}";

            if (card.dayText != null)
                card.dayText.text = hasSave
                    ? $"第 {summary.Value.Day} 天 · {GetPhaseName(summary.Value.Phase)} · {summary.Value.TenantCount} 位房客"
                    : "空存档";

            if (card.timeText != null)
                card.timeText.text = hasSave
                    ? summary.Value.SavedAtLocal.ToString("MM-dd HH:mm")
                    : "点击开始新游戏";

            if (card.cardButton != null)
            {
                card.cardButton.onClick.RemoveAllListeners();
                int captured = i;
                card.cardButton.onClick.AddListener(() => OnSlotPressed(captured));
            }

            if (card.deleteButton != null)
            {
                card.deleteButton.interactable = hasSave;
                card.deleteButton.onClick.RemoveAllListeners();
                int captured = i;
                card.deleteButton.onClick.AddListener(() => OnDeletePressed(captured));
            }
        }
    }

    private void SetStatus(string message, bool isWarning)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = isWarning ? WarningColor : MutedTextColor;
    }

    private static string GetPhaseName(HotelPhase phase)
    {
        return phase switch
        {
            HotelPhase.Dawn => "黎明",
            HotelPhase.Day => "白天",
            HotelPhase.Dusk => "黄昏",
            HotelPhase.Night => "黑夜",
            _ => "未知"
        };
    }
}