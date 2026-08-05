using Hotel.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private const string GameSceneName = "MainScene";
    private const float ConfirmationSeconds = 4f;

    private static readonly Color MutedTextColor = new(0.67f, 0.65f, 0.62f, 1f);
    private static readonly Color WarningColor = new(0.94f, 0.62f, 0.49f, 1f);

    [SerializeField] private TextMeshProUGUI saveInfoText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button continueButton;

    private float newGameConfirmationUntil;
    private float deleteConfirmationUntil;

    private void Awake()
    {
        RefreshSaveInformation();
    }

    public void OnNewGamePressed()
    {
        if (SaveGameService.HasSave && Time.unscaledTime > newGameConfirmationUntil)
        {
            newGameConfirmationUntil = Time.unscaledTime + ConfirmationSeconds;
            SetStatus("再次点击确认：新游戏将在黎明覆盖当前存档", true);
            return;
        }

        GameLaunchContext.StartNewGame();
        SceneManager.LoadScene(GameSceneName);
    }

    public void OnContinuePressed()
    {
        if (!SaveGameService.TryLoad(out var state, out var error))
        {
            SetStatus($"读取失败：{error}", true);
            RefreshSaveInformation();
            return;
        }

        GameLaunchContext.ContinueWith(state);
        SceneManager.LoadScene(GameSceneName);
    }

    public void OnDeleteSavePressed()
    {
        if (!SaveGameService.HasSave)
        {
            SetStatus("没有可删除的存档", false);
            return;
        }

        if (Time.unscaledTime > deleteConfirmationUntil)
        {
            deleteConfirmationUntil = Time.unscaledTime + ConfirmationSeconds;
            SetStatus("再次点击确认删除存档", true);
            return;
        }

        if (!SaveGameService.DeleteSave(out var error))
        {
            SetStatus($"删除失败：{error}", true);
            return;
        }

        SetStatus("存档已删除", false);
        RefreshSaveInformation();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RefreshSaveInformation()
    {
        bool hasSummary = SaveGameService.TryGetSummary(out var summary);
        if (continueButton != null) continueButton.interactable = hasSummary;
        if (saveInfoText == null) return;

        if (!hasSummary)
        {
            saveInfoText.text = "暂无存档";
            return;
        }

        saveInfoText.text = $"第 {summary.Day} 天 · {GetPhaseName(summary.Phase)} · {summary.TenantCount} 位房客\n{summary.SavedAtLocal:yyyy-MM-dd HH:mm}";
    }

    private void SetStatus(string message, bool warning)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = warning ? WarningColor : MutedTextColor;
    }

    private static string GetPhaseName(HotelPhase phase)
    {
        return phase switch
        {
            HotelPhase.Dawn => "黎明",
            HotelPhase.Day => "白昼",
            HotelPhase.Dusk => "黄昏",
            HotelPhase.Night => "黑夜",
            _ => phase.ToString(),
        };

    }
}