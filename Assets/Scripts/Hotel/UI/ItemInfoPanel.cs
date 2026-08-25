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
    private CanvasGroup _canvasGroup;
    private ItemDefinition _currentItem;

    private void Awake()
    {
        EnsureInitialized();
    }

    public void Show(ItemDefinition item, Vector2 screenPoint)
    {
        if (item == null)
        {
            Hide();
            return;
        }

        EnsureInitialized();

        bool needsRefresh = !gameObject.activeSelf || _currentItem != item;
        if (needsRefresh)
        {
            _currentItem = item;
            gameObject.SetActive(true);

            if (nameText != null)
                nameText.text = item.displayName;
            if (descriptionText != null)
            {
                string desc = !string.IsNullOrEmpty(item.hoverDescription) ? item.hoverDescription : item.description;
                descriptionText.text = desc;
            }
            if (priceText != null)
                priceText.text = BuildPriceText(item);
            if (effectText != null)
                effectText.text = BuildEffectText(item);
            if (acquisitionText != null)
                acquisitionText.text = BuildAcquisitionText(item);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_selfRect);
        }

        PositionAt(screenPoint);
    }

    public void Hide()
    {
        _currentItem = null;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        _currentItem = null;
    }

    private void EnsureInitialized()
    {
        if (_selfRect == null)
            _selfRect = GetComponent<RectTransform>();
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
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

        Rect panelRect = _selfRect.rect;
        Vector2 size = new Vector2(panelRect.width, panelRect.height);
        if (size.x <= 0f || size.y <= 0f)
            size = new Vector2(260f, 140f);

        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 pivot = _selfRect.pivot;
        Vector2 half = canvasSize * 0.5f;

        const float offset = 12f;

        float topLeftX = local.x + offset;
        float topLeftY = local.y - offset;

        float minTopLeftX = -half.x;
        float maxTopLeftX = half.x - size.x;
        float minTopLeftY = -half.y + size.y;
        float maxTopLeftY = half.y;

        if (minTopLeftX > maxTopLeftX)
            topLeftX = -size.x * 0.5f;
        else
            topLeftX = Mathf.Clamp(topLeftX, minTopLeftX, maxTopLeftX);

        if (minTopLeftY > maxTopLeftY)
            topLeftY = size.y * 0.5f;
        else
            topLeftY = Mathf.Clamp(topLeftY, minTopLeftY, maxTopLeftY);

        float targetX = topLeftX + pivot.x * size.x;
        float targetY = topLeftY - (1f - pivot.y) * size.y;

        _selfRect.anchoredPosition = new Vector2(targetX, targetY);
    }

    private static string BuildPriceText(ItemDefinition item)
    {
        if (item.acquisition == ItemAcquisition.TruthChain)
            return string.Empty;
        return item.merchantPrice > 0 ? $"{item.merchantPrice} 货币" : string.Empty;
    }

    private static string BuildAcquisitionText(ItemDefinition item)
    {
        switch (item.acquisition)
        {
            case ItemAcquisition.Merchant: return "商人购买";
            case ItemAcquisition.EngineerEvent: return "工程师事件获得";
            case ItemAcquisition.MerchantAndEngineerEvent: return "商人购买 / 工程师事件获得";
            case ItemAcquisition.TruthChain: return "剧情探索获得";
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
            case ItemEffectType.TruthClue:
                return "真相线索道具（不可使用）";
            default:
                return string.Empty;
        }
    }
}
