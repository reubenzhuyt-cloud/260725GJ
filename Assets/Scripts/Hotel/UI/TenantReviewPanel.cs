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
    public ScrollRect detailedDescriptionScroll;
    public Button confirmButton;
    public Button rejectButton;

    [Header("Ability Tags")]
    public GameObject tagPanel;
    public GameObject tagPrefab;
    public string tagTextPath = "Text (TMP)";

    private Action _onConfirm;
    private Action _onReject;
    private Sprite _fallbackAvatarSprite;

    private void Awake()
    {
        if (avatarImage != null)
            _fallbackAvatarSprite = avatarImage.sprite;

        // Activation is controlled externally (TenantReviewCoordinator), following
        // the Event popup pattern — the panel is inactive by default in the scene.

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
        RefreshTagPanel(ability == TenantAbility.None
            ? new TenantAbility[0]
            : new[] { ability });
        if (shortDescriptionLabel != null)
            shortDescriptionLabel.text = $"能力：{AbilityDisplayName.Get(ability)}　活跃：{GetActivityLabel(activityType)}\n{shortDescription ?? string.Empty}";
        if (detailedDescriptionLabel != null)
        {
            detailedDescriptionLabel.text = detailedDescription ?? string.Empty;
            if (!canRecruit && !string.IsNullOrWhiteSpace(recruitUnavailableReason))
                detailedDescriptionLabel.text += $"\n\n<color=#E59682>{recruitUnavailableReason}</color>";
        }
        if (confirmButton != null)
            confirmButton.interactable = canRecruit;

        // NOTE: Activation (SetActive) is handled by the external controller
        // (TenantReviewCoordinator) before calling Show, matching the Event popup
        // pattern. The panel must already be active for the layout reset below.

        // Force the layout pass so the ContentSizeFitter inside the scroll view has
        // its final content height before resetting, otherwise the deferred layout
        // rebuild would overwrite the scroll position. Default to the top on show.
        if (detailedDescriptionScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            detailedDescriptionScroll.verticalNormalizedPosition = 1f;
        }
    }

    private void RefreshTagPanel(TenantAbility[] abilities)
    {
        if (tagPanel == null || tagPrefab == null)
        {
            if (tagPanel != null) tagPanel.SetActive(false);
            return;
        }

        tagPrefab.SetActive(false);

        for (int i = tagPanel.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = tagPanel.transform.GetChild(i);
            if (child.gameObject == tagPrefab)
                continue;
            Destroy(child.gameObject);
        }

        int generated = 0;
        if (abilities != null)
        {
            for (int i = 0; i < abilities.Length; i++)
            {
                TenantAbility ability = abilities[i];
                if (ability == TenantAbility.None) continue;

                GameObject clone = Instantiate(tagPrefab, tagPanel.transform);
                clone.gameObject.SetActive(true);

                TextMeshProUGUI label = FindTMP(clone, tagTextPath);
                if (label != null)
                    label.text = AbilityDisplayName.Get(ability);
                generated++;
            }
        }

        tagPanel.SetActive(generated > 0);
    }

    private static TextMeshProUGUI FindTMP(GameObject root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;
        Transform target = root.transform.Find(path);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
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
        // Deactivation is handled externally by TenantReviewCoordinator.
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
