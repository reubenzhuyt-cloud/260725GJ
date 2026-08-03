using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hotel.Runtime;

public class TenantReviewPanel : MonoBehaviour
{
    [Header("UI References")]
    public Image avatarImage;
    public TextMeshProUGUI nameLabel;
    public TextMeshProUGUI shortDescriptionLabel;
    public TextMeshProUGUI detailedDescriptionLabel;
    public Button confirmButton;
    public Button rejectButton;

    private Action _onConfirm;
    private Action _onReject;
    private Sprite _fallbackAvatarSprite;

    private void Awake()
    {
        if (avatarImage != null)
            _fallbackAvatarSprite = avatarImage.sprite;

        gameObject.SetActive(false);

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(HandleConfirm);
        }

        if (rejectButton != null)
        {
            rejectButton.onClick.RemoveAllListeners();
            rejectButton.onClick.AddListener(HandleReject);
        }
    }

    public void Show(
        string displayName,
        Sprite portrait,
        Color color,
        TenantAbility ability,
        TenantActivityType activityType,
        string shortDescription,
        string detailedDescription,
        bool canRecruit,
        string recruitUnavailableReason,
        Action onConfirm,
        Action onReject)
    {
        _onConfirm = onConfirm;
        _onReject = onReject;

        if (avatarImage != null)
        {
            avatarImage.sprite = portrait != null ? portrait : _fallbackAvatarSprite;
            avatarImage.color = portrait != null ? Color.white : color;
        }
        if (nameLabel != null)
            nameLabel.text = displayName;
        if (shortDescriptionLabel != null)
            shortDescriptionLabel.text = $"能力：{GetAbilityLabel(ability)}　活跃：{GetActivityLabel(activityType)}\n{shortDescription ?? string.Empty}";
        if (detailedDescriptionLabel != null)
        {
            detailedDescriptionLabel.text = detailedDescription ?? string.Empty;
            if (!canRecruit && !string.IsNullOrWhiteSpace(recruitUnavailableReason))
                detailedDescriptionLabel.text += $"\n\n<color=#E59682>{recruitUnavailableReason}</color>";
        }
        if (confirmButton != null)
            confirmButton.interactable = canRecruit;

        gameObject.SetActive(true);
    }

    private static string GetAbilityLabel(TenantAbility ability)
    {
        return ability switch
        {
            TenantAbility.Doctor => "医生",
            TenantAbility.Cook => "厨师",
            TenantAbility.Engineer => "工程师",
            TenantAbility.NightWatch => "守夜人",
            TenantAbility.FormerEmployee => "前员工",
            TenantAbility.Merchant => "商贩",
            TenantAbility.Carpenter => "木工",
            TenantAbility.Farmer => "农民",
            _ => "无标签",
        };

    }

    private static string GetActivityLabel(TenantActivityType activityType)
    {
        return activityType switch
        {
            TenantActivityType.NightActive => "夜行",
            TenantActivityType.AllDay => "全天",
            _ => "日行",
        };

    }

    public void Hide()
    {
        _onConfirm = null;
        _onReject = null;
        gameObject.SetActive(false);
    }

    private void HandleConfirm()
    {
        if (confirmButton != null && !confirmButton.interactable)
            return;

        var callback = _onConfirm;
        callback?.Invoke();
    }

    private void HandleReject()
    {
        var callback = _onReject;
        callback?.Invoke();
    }
}
