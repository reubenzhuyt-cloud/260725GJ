using System.Collections.Generic;
using Hotel.Authoring.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TruthItemInfoPanel : MonoBehaviour
{
    private enum Tab
    {
        Outlook,
        ReadContent,
        FoundPlace
    }

    public Image itemImage;
    public TextMeshProUGUI nameLabel;
    public Button outlookButton;
    public Button readContentButton;
    public Button foundPlaceButton;
    public TextMeshProUGUI bodyText;
    public ScrollRect scrollRect;

    private ItemDefinition _currentItem;
    private Tab _currentTab = Tab.Outlook;

    private void Awake()
    {
        if (outlookButton != null)
            outlookButton.onClick.AddListener(() => SwitchTab(Tab.Outlook));
        if (readContentButton != null)
            readContentButton.onClick.AddListener(() => SwitchTab(Tab.ReadContent));
        if (foundPlaceButton != null)
            foundPlaceButton.onClick.AddListener(() => SwitchTab(Tab.FoundPlace));
    }

    public void Show(ItemDefinition item)
    {
        if (item == null)
            return;

        if (ItemUseManager.Instance != null && ItemUseManager.Instance.infoPanel != null)
            ItemUseManager.Instance.infoPanel.Hide();

        _currentItem = item;
        gameObject.SetActive(true);

        if (nameLabel != null)
            nameLabel.text = item.displayName;

        if (itemImage != null)
        {
            if (item.icon != null)
            {
                itemImage.sprite = item.icon;
                itemImage.enabled = true;
            }
            else
            {
                itemImage.sprite = null;
                itemImage.enabled = false;
            }
        }

        SwitchTab(Tab.Outlook);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _currentItem = null;
    }

    private void SwitchTab(Tab tab)
    {
        _currentTab = tab;
        UpdateBodyText();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void UpdateBodyText()
    {
        if (bodyText == null || _currentItem == null)
            return;

        switch (_currentTab)
        {
            case Tab.Outlook:
                bodyText.text = _currentItem.description ?? string.Empty;
                break;
            case Tab.ReadContent:
                bodyText.text = _currentItem.readableContent ?? string.Empty;
                break;
            case Tab.FoundPlace:
                bodyText.text = _currentItem.discoveryScene ?? string.Empty;
                break;
        }
    }

    private void Update()
    {
        if (!gameObject.activeSelf)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (IsPointerOverPanel())
            return;

        Hide();
    }

    private bool IsPointerOverPanel()
    {
        EventSystem current = EventSystem.current;
        if (current == null)
            return false;

        var pointerData = new PointerEventData(current)
        {
            position = Input.mousePosition
        };

        var results = new List<RaycastResult>();
        current.RaycastAll(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hit = results[i].gameObject;
            if (hit != null && hit.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }
}
