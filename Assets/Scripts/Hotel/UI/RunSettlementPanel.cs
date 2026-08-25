using Hotel.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-authored result overlay. Settlement rules remain outside this presentation component.</summary>
public sealed class RunSettlementPanel : MonoBehaviour
{
    [Header("Artwork")]
    [SerializeField] private Image endingImage;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI endingText;
    [SerializeField] private TextMeshProUGUI storyText;
    [SerializeField] private TextMeshProUGUI statisticsText;
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("Buttons")]
    [SerializeField] private Button mainMenuButton;

    private void Awake()
    {
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    private void OnDestroy()
    {
        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
    }

    public void Show(RunSummaryState summary)
    {
        if (summary == null || !ValidateReferences())
            return;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ApplyEndingImage(summary.Ending);
        titleText.text = "第三十天结束  /  DAY 30 COMPLETE";
        endingText.text = GetEndingTitle(summary.Ending);
        endingText.color = GetEndingColor(summary.Ending);
        storyText.text = GetEndingStory(summary.Ending);
        statisticsText.text = BuildStats(summary);
        hintText.text = "结算结果已保存  /  Result saved";
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    private void ApplyEndingImage(RunEnding ending)
    {
        if (endingImage == null)
        {
            Debug.LogWarning("[RunSettlementPanel] No ending image is assigned; showing the text-only result screen.", this);
            return;
        }

        string resourceName = ending switch
        {
            RunEnding.Truth => "真",
            RunEnding.Good => "好",
            RunEnding.Normal => "普通",
            _ => "坏"
        };

        Sprite sprite = Resources.Load<Sprite>($"Endings/{resourceName}");
        if (sprite == null)
        {
            Debug.LogWarning($"[RunSettlementPanel] Ending artwork 'Resources/Endings/{resourceName}' was not found.", this);
            return;
        }

        endingImage.sprite = sprite;
    }

    private bool ValidateReferences()
    {
        if (endingImage != null
            && titleText != null
            && endingText != null
            && storyText != null
            && statisticsText != null
            && hintText != null
            && mainMenuButton != null)
        {
            return true;
        }

        Debug.LogError(
            "[RunSettlementPanel] Scene references are incomplete. Assign all text and button fields in the Inspector.",
            this);
        return false;
    }

    private static string BuildStats(RunSummaryState s)
    {
        string highest = ResolveTenantName(s.HighestErosionTenantId);
        string lowest = ResolveTenantName(s.LowestErosionTenantId);
        float mistakePercent = s.MisclassificationRate * 100f;

        return
            $"幸存人数 / Survivors                         {s.FinalTenantCount}\n"
            + $"平均侵蚀度 / Average corruption             {s.AverageErosion:0.0}\n"
            + $"误判率 / Misjudgment rate                   {mistakePercent:0.#}%  "
            + $"({s.MisclassificationCount}/{s.ClassifiedTenantCount})\n"
            + $"最高侵蚀房客 / Most corrupted               {highest}  ({s.HighestErosion:0.0})\n"
            + $"最低侵蚀房客 / Least corrupted              {lowest}  ({s.LowestErosion:0.0})\n"
            + $"真相道具 / Truth items                      {s.TruthItemCount}/3\n"
            + $"完成事件链 / Completed narrative chains     {s.CompletedChainCount}";
    }

    private static string ResolveTenantName(string tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
            return "—";
        if (TenantAssignmentCoordinator.Instance != null
            && TenantAssignmentCoordinator.Instance.TryGetTenantDisplayName(tenantId, out string displayName))
            return displayName;
        if (TenantReviewCoordinator.Instance != null
            && TenantReviewCoordinator.Instance.TryGetCandidatePresentation(tenantId, out string candidateName, out _, out _))
            return candidateName;
        return tenantId;
    }

    private static string GetEndingTitle(RunEnding ending)
    {
        return ending switch
        {
            RunEnding.Truth => "真相结局  /  TRUTH ENDING",
            RunEnding.Good => "好结局  /  GOOD ENDING",
            RunEnding.Normal => "普通结局  /  NORMAL ENDING",
            _ => "坏结局  /  BAD ENDING"
        };
    }

    private static string GetEndingStory(RunEnding ending)
    {
        return ending switch
        {
            RunEnding.Truth => "你们活了下来，并带着真相。\nYou survived—and carried the truth with you.",
            RunEnding.Good => "净化信号到来了。你们挺过来了。\nThe purification signal arrived. You made it through.",
            RunEnding.Normal => "信号来了。有些人还认识你，有些人已经不认识了。但旅馆还在。\nThe signal came. Some still knew you; some did not. The hotel remains.",
            _ => "信号来的时候，旅馆里已经没有认识你的人了。\nWhen the signal arrived, nobody in the hotel recognized you."
        };
    }

    private static Color GetEndingColor(RunEnding ending)
    {
        return ending switch
        {
            RunEnding.Truth => new Color(0.72f, 0.86f, 0.95f, 1f),
            RunEnding.Good => new Color(0.67f, 0.86f, 0.58f, 1f),
            RunEnding.Normal => new Color(0.88f, 0.75f, 0.45f, 1f),
            _ => new Color(0.82f, 0.35f, 0.3f, 1f)
        };
    }

}
