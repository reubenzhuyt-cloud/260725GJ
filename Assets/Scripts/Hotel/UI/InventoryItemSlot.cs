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
    [SerializeField] private float hoverStillDelay = 0.3f;
    [SerializeField] private float moveThreshold = 2f;

    private ItemDefinition _item;
    private int _count;
    private float _lastClickTime;
    private bool _hovered;
    private bool _shown;
    private Vector2 _lastMousePosition;
    private float _hoverStillTime;
    private RectTransform _rectTransform;

    public ItemDefinition Item => _item;
    public int Count => _count;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Bind(ItemDefinition item, int count)
    {
        _item = item;
        _count = count;

        if (item != null)
        {
            if (iconImage != null)
            {
                if (item.icon != null)
                {
                    iconImage.sprite = item.icon;
                    iconImage.preserveAspect = true;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                }
            }
            if (nameLabel != null)
                nameLabel.text = item.displayName;
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            if (nameLabel != null)
                nameLabel.text = string.Empty;
        }
        if (countLabel != null)
        {
            if (item != null && item.acquisition == ItemAcquisition.TruthChain)
                countLabel.text = "特殊剧情道具";
            else
                countLabel.text = count.ToString();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        _shown = false;
        _lastMousePosition = Input.mousePosition;
        _hoverStillTime = 0f;
    }

    private void HidePanel()
    {
        _hovered = false;
        _shown = false;
        _hoverStillTime = 0f;
        ItemInfoPanel panel = GetInfoPanel();
        if (panel != null)
            panel.Hide();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData != null && eventData.pointerEnter != null && eventData.pointerEnter.transform.IsChildOf(transform))
            return;

        HidePanel();
    }

    private void OnDisable()
    {
        HidePanel();
    }

    private void Update()
    {
        if (!_hovered || _item == null)
            return;

        if (_rectTransform != null && !RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, Input.mousePosition, null))
        {
            HidePanel();
            return;
        }

        if (_shown)
            return;

        Vector2 mousePosition = Input.mousePosition;
        if (Vector2.Distance(mousePosition, _lastMousePosition) > moveThreshold)
        {
            _lastMousePosition = mousePosition;
            _hoverStillTime = 0f;
            return;
        }

        _hoverStillTime += Time.unscaledDeltaTime;
        if (_hoverStillTime < hoverStillDelay)
            return;

        _shown = true;
        ItemInfoPanel hoverPanel = GetInfoPanel();
        if (hoverPanel != null)
            hoverPanel.Show(_item, mousePosition);
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

            if (_item.acquisition == ItemAcquisition.TruthChain)
            {
                TruthItemInfoPanel truthPanel = GetTruthInfoPanel();
                if (truthPanel != null)
                    truthPanel.Show(_item);
                return;
            }

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

    private static TruthItemInfoPanel GetTruthInfoPanel()
    {
        ItemUseManager manager = ItemUseManager.Instance;
        return manager != null ? manager.truthInfoPanel : null;
    }
}
