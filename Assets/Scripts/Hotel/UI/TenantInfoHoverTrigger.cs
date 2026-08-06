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
        if (panel.IsSuppressingExternalClose())
        {
            Debug.Log($"[TenantInfo] HandleExternalClick suppress-return frame={Time.frameCount}");
            return;
        }
        var hits = RaycastAllUnderPointer();
        if (hits != null)
            Debug.Log($"[TenantInfo] HandleExternalClick uiHits={hits.Count} frame={Time.frameCount}");
        if (hits != null && hits.Count > 0)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                GameObject hit = hits[i].gameObject;
                if (hit == null)
                    continue;
                Debug.Log($"[TenantInfo]   hit[{i}] {TransformPath(hit.transform)}");
            }
            return;
        }
        bool worldHit = IsPointerOverWorldObject();
        Debug.Log($"[TenantInfo] HandleExternalClick no-ui worldHit={worldHit} frame={Time.frameCount}");
        if (worldHit)
            return;
        Debug.Log($"[TenantInfo] HandleExternalClick closing explicit-blank frame={Time.frameCount}");
        panel.Hide("explicit-blank-click");
    }

    private static string TransformPath(Transform t)
    {
        if (t == null)
            return "null";
        var parts = new List<string>();
        Transform cur = t;
        while (cur != null)
        {
            parts.Add(cur.name);
            cur = cur.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
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

    private static bool IsPointerOverWorldObject()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return false;
        Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(worldPos.x, worldPos.y);
        Collider2D hit = Physics2D.OverlapPoint(point);
        return hit != null;
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
        TenantInfoPanel panel = TenantInfoPanel.Instance;
        Debug.Log($"[TenantInfo] Trigger.OnPointerDown button={eventData.button} trigger={gameObject.name} rightClick={panel != null && panel.OpenedByRightClick} frame={Time.frameCount}");
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            string tenantId = tenantIdProvider != null ? tenantIdProvider() : null;
            if (string.IsNullOrEmpty(tenantId))
                return;
            if (panel != null)
            {
                panel.SetTriggerHover(true);
                panel.ShowPinned(tenantId, Input.mousePosition);
            }
        }
        else if (eventData.button == PointerEventData.InputButton.Left && hideOnLeftClick)
        {
            _hovering = false;
            if (panel != null)
            {
                panel.SetTriggerHover(false);
                panel.Hide("left-trigger-down");
            }
        }
    }
}
