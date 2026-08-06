using UnityEngine;
using UnityEngine.EventSystems;

public class TenantAssignmentPanelReveal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private float retractedOffset = 64f;
    [SerializeField] private float slideDuration = 0.2f;
    [SerializeField] private float retractDelay = 0.15f;
    [SerializeField] private GameObject eventPanel;
    [SerializeField] private GameObject reviewPanel;
    [SerializeField] private TenantInfoPanel hoverInfoPanel;
    [SerializeField] private TenantInfoPanel pinnedInfoPanel;

    private Vector2 expandedPosition;
    private int hoverCount;
    private float exitTime;
    private bool active;

    private void Awake()
    {
        if (panel == null)
        {
            active = false;
            return;
        }

        active = true;
        expandedPosition = panel.anchoredPosition;
        panel.anchoredPosition = expandedPosition + Vector2.right * retractedOffset;
    }

    private void OnDisable()
    {
        hoverCount = 0;
        exitTime = 0f;

        if (panel != null)
            panel.anchoredPosition = expandedPosition + Vector2.right * retractedOffset;
    }

    private void Update()
    {
        if (!active)
            return;

        bool infoPanelShowing = (hoverInfoPanel != null && hoverInfoPanel.IsShowing)
            || (pinnedInfoPanel != null && pinnedInfoPanel.IsShowing);

        bool blockAutoExpand = (eventPanel != null && eventPanel.activeInHierarchy)
            || (reviewPanel != null && reviewPanel.activeInHierarchy);

        bool shouldExpand = (hoverCount > 0
            || Time.unscaledTime < exitTime + retractDelay
            || infoPanelShowing)
            && !blockAutoExpand;
        Vector2 target = shouldExpand
            ? expandedPosition
            : expandedPosition + Vector2.right * retractedOffset;

        float maxDelta = slideDuration > 0f
            ? retractedOffset / slideDuration * Time.unscaledDeltaTime
            : retractedOffset;

        panel.anchoredPosition = Vector2.MoveTowards(panel.anchoredPosition, target, maxDelta);
        panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, expandedPosition.y);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PointerEntered();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExited();
    }

    public void PointerEntered()
    {
        hoverCount++;
    }

    public void PointerExited()
    {
        hoverCount--;

        if (hoverCount <= 0)
        {
            hoverCount = 0;
            exitTime = Time.unscaledTime;
        }
    }
}
