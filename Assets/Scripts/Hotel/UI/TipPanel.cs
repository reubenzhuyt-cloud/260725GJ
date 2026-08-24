using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TipPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI tipText;

    public bool IsShowing => gameObject.activeSelf;

    private Canvas _canvas;
    private RectTransform _selfRect;

    private void Awake()
    {
        EnsureInitialized();
    }

    public void Show(string tip, Vector2 screenPoint)
    {
        if (string.IsNullOrEmpty(tip))
        {
            Hide();
            return;
        }

        gameObject.SetActive(true);
        EnsureInitialized();

        if (tipText != null)
            tipText.text = tip;

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
            size = new Vector2(100f, 40f);

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
}
