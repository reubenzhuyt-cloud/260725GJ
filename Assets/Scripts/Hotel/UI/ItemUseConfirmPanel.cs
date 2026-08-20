using System;
using Hotel.Authoring.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemUseConfirmPanel : MonoBehaviour
{
    public GameObject root;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Button acceptButton;
    public Button cancelButton;

    public event Action Accepted;
    public event Action Cancelled;

    public bool IsShowing => Root != null && Root.activeSelf;

    private GameObject Root => root != null ? root : gameObject;

    private void Awake()
    {
        if (acceptButton != null)
            acceptButton.onClick.AddListener(OnAcceptClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);
    }

    private void OnDestroy()
    {
        if (acceptButton != null)
            acceptButton.onClick.RemoveListener(OnAcceptClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);
    }

    public void Show(ItemDefinition item)
    {
        if (item != null)
        {
            if (titleText != null)
                titleText.text = item.displayName;
            if (descriptionText != null)
                descriptionText.text = item.description;
        }
        if (Root != null)
            Root.SetActive(true);
    }

    public void ShowPurchase(string displayName, int price)
    {
        if (titleText != null)
            titleText.text = displayName;
        if (descriptionText != null)
            descriptionText.text = $"确认购买 {displayName}，花费 {price} 货币？";
        if (Root != null)
            Root.SetActive(true);
    }

    public void Hide()
    {
        if (Root != null)
            Root.SetActive(false);
    }

    private void OnAcceptClicked()
    {
        Accepted?.Invoke();
    }

    private void OnCancelClicked()
    {
        Cancelled?.Invoke();
    }
}
