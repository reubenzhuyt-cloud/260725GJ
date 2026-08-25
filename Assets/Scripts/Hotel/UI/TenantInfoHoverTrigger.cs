using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI-driven hover / pinned info trigger. Hover shows a small panel after
/// hovering still for hoverDelay seconds; right-click opens the pinned (large) panel.
/// Works purely through UI pointer events and RectTransform boundary checks.
/// </summary>
public class TenantInfoHoverTrigger : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public TenantInfoPanel hoverInfoPanel;
    public TenantInfoPanel pinnedInfoPanel;
    public float hoverDelay = 0.3f;
    public float hideDelay = 0.15f;
    public float moveThreshold = 2f;
    public bool preferLeftPlacement;

    /// <summary>When true, right-click opens the pinned panel (UI mode).</summary>
    public bool enableUiRightClick;

    public TenantInfoPanel.DisplaySource source;

    public Func<string> tenantIdProvider;

    private bool _hovered;
    private bool _shown;
    private Vector2 _lastMousePosition;
    private float _hoverStillTime;
    private RectTransform _rectTransform;
    private Canvas _canvas;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        if (!_hovered)
            return;

        string tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
        {
            HideHoverPanel();
            return;
        }

        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        if (_rectTransform != null)
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, Input.mousePosition, cam))
            {
                HideHoverPanel();
                return;
            }
        }

        if (pinnedInfoPanel != null && pinnedInfoPanel.IsShowing)
        {
            _hoverStillTime = 0f;
            return;
        }

        if (Input.GetMouseButton(0))
        {
            // While left mouse button is held (drag in progress), don't open
            _hoverStillTime = 0f;
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
        if (_hoverStillTime < hoverDelay)
            return;

        _shown = true;
        if (hoverInfoPanel != null)
        {
            hoverInfoPanel.ShowHover(tenantId, mousePosition, preferLeftPlacement, source);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        _shown = false;
        _lastMousePosition = Input.mousePosition;
        _hoverStillTime = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (eventData != null && eventData.pointerEnter != null && eventData.pointerEnter.transform.IsChildOf(transform))
            return;

        HideHoverPanel();
    }

    public void HideHoverPanel()
    {
        _hovered = false;
        _shown = false;
        _hoverStillTime = 0f;

        if (hoverInfoPanel != null && hoverInfoPanel.IsShowing && hoverInfoPanel.Mode == TenantInfoPanel.PanelMode.Hover)
        {
            string tenantId = GetTenantId();
            if (string.IsNullOrEmpty(tenantId) || hoverInfoPanel.CurrentTenantId == tenantId)
            {
                hoverInfoPanel.Hide();
            }
        }
    }

    public void OpenPinned()
    {
        string tenantId = GetTenantId();
        if (pinnedInfoPanel == null || string.IsNullOrEmpty(tenantId))
            return;

        HideHoverPanel();
        pinnedInfoPanel.ShowPinned(tenantId, Input.mousePosition, source);
    }

    public void ClosePinned()
    {
        if (pinnedInfoPanel != null)
            pinnedInfoPanel.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
            return;
        if (!enableUiRightClick)
            return;
        OpenPinned();
    }

    private void OnDisable()
    {
        HideHoverPanel();
    }

    private void OnDestroy()
    {
        HideHoverPanel();
    }

    private string GetTenantId()
    {
        return tenantIdProvider != null ? tenantIdProvider() : null;
    }
}
