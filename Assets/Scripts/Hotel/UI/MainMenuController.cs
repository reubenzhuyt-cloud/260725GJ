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

    [Header("Screens")]
    [SerializeField] private GameObject menuCard;
    [SerializeField] private GameObject slotGridPanel;

    [Header("Save Slots")]
    [SerializeField] private SlotCardView[] slotCards = new SlotCardView[SaveGameService.MaxSlots];
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Confirmation")]
    [SerializeField] private GameObject confirmOverlay;
    [SerializeField] private TextMeshProUGUI confirmTitle;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    private enum PendingAction : byte { None, NewGame, Delete }

    private PendingAction pendingAction;
    private int pendingSlotIndex;

    private static readonly Color MutedTextColor = new(0.67f, 0.65f, 0.62f, 1f);
    private static readonly Color WarningColor = new(0.94f, 0.62f, 0.49f, 1f);

    private void Awake()
    {
        if (confirmYesButton != null)
            confirmYesButton.onClick.AddListener(OnConfirmYes);
        if (confirmNoButton != null)
            confirmNoButton.onClick.AddListener(OnConfirmNo);

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

    public void OnPlayPressed()
    {
        OpenSlotGrid();
    }

    // Kept while the existing Play button still uses this older scene callback.
    public void OnDeleteSavePressed()
    {
        OnPlayPressed();
    }

    // Kept for the disabled legacy Continue button in the scene.
    public void OnContinuePressed()
    {
        if (!SaveGameService.HasSave(1))
        {
            SetStatus("暂无存档可继续", true);
            return;
        }

        LoadSlot(1);
    }

    public void OnBackPressed()
    {
        if (confirmOverlay != null && confirmOverlay.activeSelf)
        {
            OnConfirmNo();
            return;
        }

        CloseSlotGrid();
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
        SetStatus(string.Empty, false);
        RefreshSlots();

        if (menuCard != null) menuCard.SetActive(false);
        if (slotGridPanel != null) slotGridPanel.SetActive(true);
    }

    private void CloseSlotGrid()
    {
        pendingAction = PendingAction.None;
        if (confirmOverlay != null) confirmOverlay.SetActive(false);
        if (slotGridPanel != null) slotGridPanel.SetActive(false);
        if (menuCard != null) menuCard.SetActive(true);
    }

    private void OnSlotPressed(int index)
    {
        int slot = index + 1;
        if (SaveGameService.HasSave(slot))
        {
            LoadSlot(slot);
            return;
        }

        pendingAction = PendingAction.NewGame;
        pendingSlotIndex = index;
        ShowConfirmation($"新游戏将保存在存档位 {slot}？");
    }

    private void LoadSlot(int slot)
    {
        if (!SaveGameService.TryLoad(slot, out var state, out var error))
        {
            SetStatus($"读取失败：{error}", true);
            RefreshSlots();
            return;
        }

        GameLaunchContext.ContinueWith(slot, state);
        SceneManager.LoadScene(GameSceneName);
    }

    private void OnDeletePressed(int index)
    {
        int slot = index + 1;
        if (!SaveGameService.HasSave(slot)) return;

        pendingAction = PendingAction.Delete;
        pendingSlotIndex = index;
        ShowConfirmation($"删除存档位 {slot} 的存档？此操作无法撤销。");
    }

    private void ShowConfirmation(string message)
    {
        if (confirmTitle != null) confirmTitle.text = message;
        if (confirmOverlay != null) confirmOverlay.SetActive(true);
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
        SaveSlotSummary?[] summaries = SaveGameService.GetAllSummaries();
        int count = slotCards != null ? Mathf.Min(slotCards.Length, SaveGameService.MaxSlots) : 0;

        for (int i = 0; i < count; i++)
        {
            SlotCardView card = slotCards[i];
            if (card == null) continue;

            int slot = i + 1;
            SaveSlotSummary? summary = summaries[i];
            bool hasValidSave = summary.HasValue;
            bool hasSaveFile = SaveGameService.HasSave(slot);

            if (card.numberText != null)
                card.numberText.text = $"存档位 {slot}";
            if (card.dayText != null)
                card.dayText.text = hasValidSave
                    ? $"第 {summary.Value.Day} 天 · {GetPhaseName(summary.Value.Phase)} · {summary.Value.TenantCount} 位房客"
                    : hasSaveFile ? "存档无法读取" : "空存档";
            if (card.timeText != null)
                card.timeText.text = hasValidSave
                    ? summary.Value.SavedAtLocal.ToString("yyyy-MM-dd HH:mm")
                    : hasSaveFile ? "可以删除后重新开始" : "点击开始新游戏";

            if (card.cardButton != null)
            {
                card.cardButton.onClick.RemoveAllListeners();
                int captured = i;
                card.cardButton.onClick.AddListener(() => OnSlotPressed(captured));
            }

            if (card.deleteButton != null)
            {
                card.deleteButton.gameObject.SetActive(hasSaveFile);
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
