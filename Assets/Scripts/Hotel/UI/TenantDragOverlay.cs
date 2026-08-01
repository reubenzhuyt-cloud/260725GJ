using UnityEngine;
using UnityEngine.UI;

public class TenantDragOverlay : MonoBehaviour
{
    public static TenantDragOverlay Instance { get; private set; }

    [SerializeField] private Image overlayImage;

    private RectTransform _rectTransform;

    private void Awake()
    {
        Instance = this;
        _rectTransform = GetComponent<RectTransform>();
        gameObject.SetActive(false);
    }

    public void Show(Color color)
    {
        if (overlayImage != null)
        {
            Color displayColor = color;
            displayColor.a = 0.6f;
            overlayImage.color = displayColor;
        }
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdatePosition(Vector2 screenPosition)
    {
        if (_rectTransform == null)
            return;

        RectTransform parentRect = _rectTransform.parent as RectTransform;
        if (parentRect == null)
            return;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, screenPosition, null, out localPoint))
        {
            _rectTransform.anchoredPosition = localPoint;
        }
    }
}
