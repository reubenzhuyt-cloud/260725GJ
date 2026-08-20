using Hotel.Authoring.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VendorShopItemSlot : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public Button buyButton;
    public GameObject soldOutOverlay;

    private ItemDefinition _definition;
    private bool _sold;

    public ItemDefinition Definition => _definition;
    public bool IsSold => _sold;

    public void Bind(ItemDefinition definition)
    {
        _definition = definition;
        _sold = false;

        if (iconImage != null)
            iconImage.sprite = definition != null ? definition.icon : null;
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
}
