using Hotel.Runtime;
using UnityEngine;

public class PlayerLogCardItem : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI categoryTagText;
    [SerializeField] private TMPro.TextMeshProUGUI dayPhaseText;
    [SerializeField] private TMPro.TextMeshProUGUI titleText;
    [SerializeField] private TMPro.TextMeshProUGUI summaryText;

    public void Bind(PlayerLogCardView view)
    {
        if (view == null)
            return;

        TMPro.TextMeshProUGUI category = ResolveTMP(categoryTagText, "CategoryTag");
        if (category != null)
            category.text = CategoryTagText(view.Category);

        TMPro.TextMeshProUGUI dayPhase = ResolveTMP(dayPhaseText, "DayPhase");
        if (dayPhase != null)
            dayPhase.text = DayPhaseText(view);

        TMPro.TextMeshProUGUI title = ResolveTMP(titleText, "Title");
        if (title != null)
            title.text = view.Title ?? string.Empty;

        TMPro.TextMeshProUGUI summary = ResolveTMP(summaryText, "Summary");
        if (summary != null)
            summary.text = view.Summary ?? string.Empty;
    }

    private TMPro.TextMeshProUGUI ResolveTMP(TMPro.TextMeshProUGUI reference, string childName)
    {
        if (reference != null)
            return reference;
        if (string.IsNullOrEmpty(childName))
            return null;
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMPro.TextMeshProUGUI>() : null;
    }

    private static string CategoryTagText(PlayerLogCategory category)
    {
        switch (category)
        {
            case PlayerLogCategory.EventChoice: return "事件";
            case PlayerLogCategory.SpecialStory: return "故事";
            case PlayerLogCategory.EffectSettlement: return "结算";
            case PlayerLogCategory.BuffTick: return "状态";
            case PlayerLogCategory.TenantRecruit: return "招募";
            case PlayerLogCategory.TenantReject: return "婉拒";
            case PlayerLogCategory.RoomAssignment: return "入住";
            case PlayerLogCategory.ResourceFood: return "资源";
            case PlayerLogCategory.PhaseTransition: return "阶段";
            default: return "日志";
        }
    }

    private static string DayPhaseText(PlayerLogCardView view)
    {
        string dayPhase = view.Day > 0 ? $"第{view.Day}天" : string.Empty;
        if (string.IsNullOrEmpty(view.PhaseText))
            return dayPhase;
        return string.IsNullOrEmpty(dayPhase) ? view.PhaseText : $"{dayPhase} · {view.PhaseText}";
    }
}
