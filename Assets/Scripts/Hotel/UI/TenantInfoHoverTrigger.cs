using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class TenantInfoHoverTrigger : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public TenantInfoPanel hoverInfoPanel;
    public TenantInfoPanel pinnedInfoPanel;
    public float hoverDelay = 0.5f;
    public float hideDelay = 0.15f;
    public bool useWorldHitTest = false;
    public LayerMask hitMask = ~0;
    public bool preferLeftPlacement;

    public Func<string> tenantIdProvider;

    private bool _hovering;
    private float _hoverStart;
    private float _hidePendingStart;
    private AnchorDropTarget _cachedAnchor;

    private void Update()
    {
        if (useWorldHitTest)
        {
            bool over = IsPointerOverWorldTarget();
            if (over && !_hovering)
            {
                _hovering = true;
                _hoverStart = Time.unscaledTime;
            }
            else if (!over && _hovering)
            {
                _hovering = false;
            }
        }

        TryOpenHover();
        UpdateHoverHide();
    }

    private void TryOpenHover()
    {
        if (!_hovering)
            return;
        if (hoverInfoPanel == null || pinnedInfoPanel == null)
            return;
        if (pinnedInfoPanel.IsShowing)
        {
            _hoverStart = Time.unscaledTime;
            return;
        }
        if (hoverInfoPanel.IsShowing)
            return;
        string tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
            return;
        if (Time.unscaledTime - _hoverStart < hoverDelay)
            return;
        hoverInfoPanel.ShowHover(tenantId, Input.mousePosition, preferLeftPlacement);
    }

    private void UpdateHoverHide()
    {
        if (hoverInfoPanel == null)
            return;
        if (!hoverInfoPanel.IsShowing || hoverInfoPanel.Mode != TenantInfoPanel.PanelMode.Hover)
            return;
        if (_hovering || hoverInfoPanel.IsPointerOver)
        {
            _hidePendingStart = 0f;
            return;
        }
        if (_hidePendingStart <= 0f)
            _hidePendingStart = Time.unscaledTime;
        else if (Time.unscaledTime - _hidePendingStart >= hideDelay)
            hoverInfoPanel.Hide();
    }

    public void OpenPinned()
    {
        string tenantId = GetTenantId();
        if (pinnedInfoPanel == null || string.IsNullOrEmpty(tenantId))
            return;
        _hovering = false;
        _hidePendingStart = 0f;
        if (hoverInfoPanel != null)
            hoverInfoPanel.Hide();
        pinnedInfoPanel.ShowPinned(tenantId, Input.mousePosition);
    }

    public void HideHoverPanel()
    {
        if (hoverInfoPanel != null)
            hoverInfoPanel.Hide();
    }

    public void ClosePinned()
    {
        if (pinnedInfoPanel != null)
            pinnedInfoPanel.Hide();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        _hoverStart = Time.unscaledTime;
        _hidePendingStart = 0f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            OpenPinned();
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            HideHoverPanel();
        }
    }

    private string GetTenantId()
    {
        if (tenantIdProvider != null)
            return tenantIdProvider();
        if (_cachedAnchor == null)
            _cachedAnchor = GetComponent<AnchorDropTarget>();
        return _cachedAnchor != null ? _cachedAnchor.GetOccupantId() : null;
    }

    private bool IsPointerOverWorldTarget()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(worldPos.x, worldPos.y);
        Collider2D hit = Physics2D.OverlapPoint(point, hitMask);
        if (hit == null)
            return false;
        if (hit.transform != transform && !hit.transform.IsChildOf(transform) && !transform.IsChildOf(hit.transform))
            return false;
        return !string.IsNullOrEmpty(GetTenantId());
    }
}
