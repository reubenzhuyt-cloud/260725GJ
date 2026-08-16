using Hotel.Authoring.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemInfoPanel : MonoBehaviour
{
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI effectText;
    public TextMeshProUGUI acquisitionText;

    public bool IsShowing => gameObject.activeSelf;

    private Canvas _canvas;
    private RectTransform _selfRect;

    public void Show(ItemDefinition item, Vector2 screenPoint)
    {
        if (item == null)
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        EnsureInitialized();

        if (nameText != null)
            nameText.text = item.displayName;
        if (descriptionText != null)
            descriptionText.text = item.description;
        if (priceText != null)
            priceText.text = BuildPriceText(item);
        if (effectText != null)
            effectText.text = BuildEffectText(item);
        if (acquisitionText != null)
            acquisitionText.text = BuildAcquisitionText(item);

        PositionAt(screenPoint);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void EnsureInitialized()
    {
        if (_selfRect == null)
            _selfRect = GetComponent<RectTransform>();
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();
    }

    private void PositionAt(Vector2 screenPoint)
    {
        if (_canvas == null || _selfRect == null)
            return;

        RectTransform canvasRect = _canvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, _canvas.worldCamera, out local))
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_selfRect);
        Rect panelRect = _selfRect.rect;
        Vector2 size = new Vector2(panelRect.width, panelRect.height);
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(260f, 140f);

        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 pivot = _selfRect.pivot;
        Vector2 half = canvasSize * 0.5f;

        float leftExtent = pivot.x * size.x;
        float rightExtent = (1f - pivot.x) * size.x;
        float bottomExtent = pivot.y * size.y;
        float topExtent = (1f - pivot.y) * size.y;

        bool overRight = local.x + rightExtent > half.x;
        bool overBottom = local.y - bottomExtent < -half.y;

        float targetX = overRight
            ? local.x - rightExtent
            : local.x + leftExtent;
        float targetY = overBottom
            ? local.y + bottomExtent
            : local.y - topExtent;

        float minX = -half.x + leftExtent;
        float maxX = half.x - rightExtent;
        float minY = -half.y + bottomExtent;
        float maxY = half.y - topExtent;

        targetX = minX < maxX ? Mathf.Clamp(targetX, minX, maxX) : 0f;
        targetY = minY < maxY ? Mathf.Clamp(targetY, minY, maxY) : 0f;

        _selfRect.anchoredPosition = new Vector2(targetX, targetY);
    }

    private static string BuildPriceText(ItemDefinition item)
    {
        return item.merchantPrice > 0 ? $"{item.merchantPrice} 货币" : string.Empty;
    }

    private static string BuildAcquisitionText(ItemDefinition item)
    {
        switch (item.acquisition)
        {
            case ItemAcquisition.Merchant: return "商人购买";
            case ItemAcquisition.EngineerEvent: return "工程师事件获得";
            case ItemAcquisition.MerchantAndEngineerEvent: return "商人购买 / 工程师事件获得";
            default: return string.Empty;
        }
    }

    private static string BuildEffectText(ItemDefinition item)
    {
        switch (item.effectType)
        {
            case ItemEffectType.ErosionSingle:
                return $"对目标房客侵蚀 {item.effectValue:+#;-#;0}";
            case ItemEffectType.ErosionAll:
                return $"对所有当前房客侵蚀 {item.effectValue:+#;-#;0}（立即生效）";
            case ItemEffectType.NightLoss:
            {
                int percent = Mathf.RoundToInt(Mathf.Abs(item.effectValue) * 100f);
                return $"夜间事件损失额外降低 {percent}%（本局持续）";
            }
            case ItemEffectType.ExtraClue:
                return "解锁额外线索（本局持续）";
            case ItemEffectType.EngineerBoost:
                return "工程师工作效率 +30%（仅下一次工作结算）";
            default:
                return string.Empty;
        }
    }
}
