using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private void Awake()
    {
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

    public void Show(string displayName, Color color, string shortDescription, string detailedDescription, Action onConfirm, Action onReject)
    {
        _onConfirm = onConfirm;
        _onReject = onReject;

        if (avatarImage != null)
            avatarImage.color = color;
        if (nameLabel != null)
            nameLabel.text = displayName;
        if (shortDescriptionLabel != null)
            shortDescriptionLabel.text = shortDescription ?? string.Empty;
        if (detailedDescriptionLabel != null)
            detailedDescriptionLabel.text = detailedDescription ?? string.Empty;

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        _onConfirm = null;
        _onReject = null;
        gameObject.SetActive(false);
    }

    private void HandleConfirm()
    {
        var callback = _onConfirm;
        Hide();
        callback?.Invoke();
    }

    private void HandleReject()
    {
        var callback = _onReject;
        Hide();
        callback?.Invoke();
    }
}
