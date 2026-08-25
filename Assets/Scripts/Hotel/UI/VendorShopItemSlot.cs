using Hotel.Authoring.Items;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VendorShopItemSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public Button buyButton;
    public GameObject soldOutOverlay;

    [SerializeField] private float hoverStillDelay = 0.3f;
    [SerializeField] private float moveThreshold = 2f;

    private ItemDefinition _definition;
    private bool _sold;
    private bool _hovered;
    private bool _shown;
    private Vector2 _lastMousePosition;
    private float _hoverStillTime;
    private RectTransform _rectTransform;

    public ItemDefinition Definition => _definition;
    public bool IsSold => _sold;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void Bind(ItemDefinition definition)
    {
        _definition = definition;
        _sold = false;

        if (iconImage != null)
        {
            if (definition != null && definition.icon != null)
            {
                iconImage.sprite = definition.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }
        if (nameText != null)
            nameText.text = definition != null ? definition.displayName : string.Empty;
        if (priceText != null)
            priceText.text = definition != null ? definition.merchantPrice.ToString() : "0";

        SetSoldOut(false);
    }

    public void SetInteractable(bool interactable)
    {
        if (buyButton != null)
            buyButton.interactable = interactable && !_sold;
    }

    public void SetSoldOut(bool sold)
    {
        _sold = sold;
        if (soldOutOverlay != null)
            soldOutOverlay.SetActive(sold);
        if (buyButton != null)
            buyButton.interactable = !sold;
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
        if (!_hovered || _definition == null)
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
            hoverPanel.Show(_definition, mousePosition);
    }

    private static ItemInfoPanel GetInfoPanel()
    {
        ItemUseManager manager = ItemUseManager.Instance;
        return manager != null ? manager.infoPanel : null;
    }
}
