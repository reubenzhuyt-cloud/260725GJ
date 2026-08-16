using Hotel.Authoring.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image iconImage;
    public TextMeshProUGUI nameLabel;
    public TextMeshProUGUI countLabel;

    [SerializeField] private float doubleClickInterval = 0.3f;

    private ItemDefinition _item;
    private int _count;
    private float _lastClickTime;

    public ItemDefinition Item => _item;
    public int Count => _count;

    public void Bind(ItemDefinition item, int count)
    {
        _item = item;
        _count = count;

        if (item != null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = item.icon != null;
            }
            if (nameLabel != null)
                nameLabel.text = item.displayName;
        }
        if (countLabel != null)
            countLabel.text = count.ToString();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemInfoPanel panel = GetInfoPanel();
        if (panel != null && _item != null)
            panel.Show(_item, Input.mousePosition);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemInfoPanel panel = GetInfoPanel();
        if (panel != null)
            panel.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (_item == null)
            return;

        float now = Time.unscaledTime;
        if (now - _lastClickTime <= doubleClickInterval)
        {
            _lastClickTime = 0f;
            ItemUseManager manager = ItemUseManager.Instance;
            if (manager != null)
                manager.TryBeginUse(_item.itemId);
        }
        else
        {
            _lastClickTime = now;
        }
    }

    private static ItemInfoPanel GetInfoPanel()
    {
        ItemUseManager manager = ItemUseManager.Instance;
        return manager != null ? manager.infoPanel : null;
    }
}
