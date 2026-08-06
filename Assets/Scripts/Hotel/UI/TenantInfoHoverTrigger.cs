using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TenantInfoHoverTrigger : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public float hoverDelay = 0.5f;
    public bool hideOnLeftClick = true;
    public bool useWorldHitTest = false;
    public LayerMask hitMask = ~0;

    public Func<string> tenantIdProvider;

    private float _hoverStart;
    private bool _hovering;

    private void Update()
    {
        if (useWorldHitTest)
        {
            bool over = IsPointerOverWorldTarget();
            if (over && !_hovering)
            {
                _hovering = true;
                _hoverStart = Time.unscaledTime;
                if (TenantInfoPanel.Instance != null)
                    TenantInfoPanel.Instance.SetTriggerHover(true);
            }
            else if (!over && _hovering)
            {
                _hovering = false;
                if (TenantInfoPanel.Instance != null)
                    TenantInfoPanel.Instance.SetTriggerHover(false);
            }
        }

        TenantInfoPanel panel = TenantInfoPanel.Instance;
        if (panel != null && panel.OpenedByRightClick && Input.GetMouseButtonDown(0))
        {
            HandleExternalClick();
        }

        if (_hovering && panel != null && !panel.IsShowing && !panel.OpenedByRightClick)
        {
            if (Time.unscaledTime - _hoverStart >= hoverDelay)
            {
                ShowHoverPanel();
            }
        }
    }

    private void HandleExternalClick()
    {
        TenantInfoPanel panel = TenantInfoPanel.Instance;
        if (panel == null)
            return;
        var hits = RaycastAllUnderPointer();
        if (hits == null)
        {
            panel.Hide();
            return;
        }
        for (int i = 0; i < hits.Count; i++)
        {
            GameObject hit = hits[i].gameObject;
            if (panel.IsInternalHit(hit))
                return;
            if (hit == gameObject || hit.transform.IsChildOf(transform))
                return;
        }
        panel.Hide();
    }

    private static List<RaycastResult> RaycastAllUnderPointer()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return null;
        PointerEventData ped = new PointerEventData(eventSystem);
        ped.position = Input.mousePosition;
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(ped, results);
        return results;
    }

    private bool IsPointerOverWorldTarget()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(worldPos.x, worldPos.y);
        Collider2D hit = Physics2D.OverlapPoint(point, hitMask);
        if (hit == null)
            return false;
        return hit.transform == transform || hit.transform.IsChildOf(transform) || transform.IsChildOf(hit.transform);
    }

    private void ShowHoverPanel()
    {
        TenantInfoPanel panel = TenantInfoPanel.Instance;
        if (panel == null || panel.OpenedByRightClick)
            return;
        string tenantId = tenantIdProvider != null ? tenantIdProvider() : null;
        if (string.IsNullOrEmpty(tenantId))
            return;
        panel.SetTriggerHover(true);
        panel.ShowHover(tenantId, Input.mousePosition);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        _hoverStart = Time.unscaledTime;
        if (TenantInfoPanel.Instance != null)
            TenantInfoPanel.Instance.SetTriggerHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
        if (TenantInfoPanel.Instance != null)
            TenantInfoPanel.Instance.SetTriggerHover(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            string tenantId = tenantIdProvider != null ? tenantIdProvider() : null;
            if (string.IsNullOrEmpty(tenantId))
                return;
            TenantInfoPanel panel = TenantInfoPanel.Instance;
            if (panel != null)
            {
                panel.SetTriggerHover(true);
                panel.ShowPinned(tenantId, Input.mousePosition);
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Left && hideOnLeftClick)
        {
            _hovering = false;
            TenantInfoPanel panel = TenantInfoPanel.Instance;
            if (panel != null)
            {
                panel.SetTriggerHover(false);
                if (!panel.OpenedByRightClick)
                    panel.Hide();
            }
        }
    }
}
