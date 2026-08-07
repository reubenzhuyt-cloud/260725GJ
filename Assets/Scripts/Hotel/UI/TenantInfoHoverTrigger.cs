using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI-driven hover / pinned info trigger. Hover shows a small panel after
/// hoverDelay seconds; right-click opens the pinned (large) panel.
/// Works purely through UI pointer events - no world colliders or Physics2D.
/// </summary>
public class TenantInfoHoverTrigger : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public TenantInfoPanel hoverInfoPanel;
    public TenantInfoPanel pinnedInfoPanel;
    public float hoverDelay = 0.5f;
    public float hideDelay = 0.15f;
    public bool preferLeftPlacement;

    /// <summary>When true, right-click opens the pinned panel (UI mode).</summary>
    public bool enableUiRightClick;

    public TenantInfoPanel.DisplaySource source;

    public Func<string> tenantIdProvider;

    private bool _hovering;
    private float _hoverStart;
    private float _hidePendingStart;
    private string _shownHoverTenantId;

    private void Update()
    {
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
        if (Input.GetMouseButton(0))
        {
            // While the left button is held (drag/click in progress) never open hover.
            _hoverStart = Time.unscaledTime;
            return;
        }
        string tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
            return;
        if (hoverInfoPanel.IsShowing && hoverInfoPanel.CurrentTenantId == tenantId)
            return;
        if (Time.unscaledTime - _hoverStart < hoverDelay)
            return;
        hoverInfoPanel.ShowHover(tenantId, Input.mousePosition, preferLeftPlacement, source);
        _shownHoverTenantId = tenantId;
    }

    private void UpdateHoverHide()
    {
        if (hoverInfoPanel == null)
            return;
        if (!hoverInfoPanel.IsShowing || hoverInfoPanel.Mode != TenantInfoPanel.PanelMode.Hover)
        {
            _hidePendingStart = 0f;
            _shownHoverTenantId = null;
            return;
        }
        string tenantId = GetTenantId();
        if (string.IsNullOrEmpty(tenantId))
        {
            if (!string.IsNullOrEmpty(_shownHoverTenantId) && hoverInfoPanel.CurrentTenantId == _shownHoverTenantId)
                hoverInfoPanel.Hide();
            _shownHoverTenantId = null;
            _hidePendingStart = 0f;
            return;
        }
        if (tenantId != hoverInfoPanel.CurrentTenantId)
        {
            if (!string.IsNullOrEmpty(_shownHoverTenantId) && hoverInfoPanel.CurrentTenantId == _shownHoverTenantId)
                hoverInfoPanel.Hide();
            _shownHoverTenantId = null;
            _hidePendingStart = 0f;
            return;
        }
        _shownHoverTenantId = tenantId;
        if (_hovering)
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
        pinnedInfoPanel.ShowPinned(tenantId, Input.mousePosition, source);
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
        TryHideOwnedHover();
    }

    private void OnDestroy()
    {
        TryHideOwnedHover();
    }

    private void TryHideOwnedHover()
    {
        if (hoverInfoPanel == null)
            return;
        if (hoverInfoPanel.IsShowing
            && hoverInfoPanel.Mode == TenantInfoPanel.PanelMode.Hover
            && !string.IsNullOrEmpty(_shownHoverTenantId)
            && hoverInfoPanel.CurrentTenantId == _shownHoverTenantId)
        {
            hoverInfoPanel.Hide();
        }
        _shownHoverTenantId = null;
        _hidePendingStart = 0f;
    }

    private string GetTenantId()
    {
        return tenantIdProvider != null ? tenantIdProvider() : null;
    }
}
