using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class TenantInfoHoverTrigger : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    public TenantInfoPanel hoverInfoPanel;
    public TenantInfoPanel pinnedInfoPanel;
    public float hoverDelay = 0.5f;
    public float hideDelay = 0.15f;
    public bool useWorldHitTest = false;
    public LayerMask hitMask = ~0;
    public bool preferLeftPlacement;

    public Func<string> tenantIdProvider;

    private static readonly Collider2D[] _hitBuffer = new Collider2D[32];

    private bool _hovering;
    private float _hoverStart;
    private float _hidePendingStart;
    private string _shownHoverTenantId;
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

            if (over)
            {
                if (Input.GetMouseButtonDown(1))
                    OpenPinned();
                else if (Input.GetMouseButtonDown(0))
                    HideHoverPanel();
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
        if (Input.GetMouseButton(0))
        {
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
        hoverInfoPanel.ShowHover(tenantId, Input.mousePosition, preferLeftPlacement);
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
        if (tenantIdProvider != null)
            return tenantIdProvider();
        if (_cachedAnchor == null)
            _cachedAnchor = GetComponent<AnchorDropTarget>();
        return _cachedAnchor != null ? _cachedAnchor.GetOccupantId() : null;
    }

    private bool IsPointerOverWorldTarget()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return false;
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(worldPos.x, worldPos.y);
        int count = Physics2D.OverlapPointNonAlloc(point, _hitBuffer, hitMask);
        for (int i = 0; i < count; i++)
        {
            Transform t = _hitBuffer[i].transform;
            if (t == transform || t.IsChildOf(transform))
                return !string.IsNullOrEmpty(GetTenantId());
        }
        return false;
    }
}
