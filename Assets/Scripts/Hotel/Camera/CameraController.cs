using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [Header("Map Bounds")]
    public SpriteRenderer hotelMapRenderer;
    [HideInInspector] public Vector2 mapSize = new Vector2(200f, 160f);
    private Vector2 clampSize; // expanded size for camera clamping (matches camera aspect)
    private Vector2 mapOrigin;

    [Header("Zoom")]
    public float zoomSpeed = 15f;
    public float minZoom = 3f;
    public float maxZoom = 30f;
    public float zoomSmoothTime = 0.1f;

    [Header("Drag")]
    public float dragSpeed = 1f;
    public bool naturalDrag = true;

    private Camera cam;
    private bool isDragging;
    private Vector3 dragStartPos;
    private Vector3 dragOrigin;
    private float targetZoom;
    private float zoomVelocity;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;

        targetZoom = cam.orthographicSize;

        // Calculate map size from HotelMap sprite
        if (hotelMapRenderer == null)
        {
            GameObject hotelMap = GameObject.Find("HotelMap");
            if (hotelMap != null)
                hotelMapRenderer = hotelMap.GetComponent<SpriteRenderer>();
        }

        if (hotelMapRenderer != null)
        {
            Bounds bounds = hotelMapRenderer.bounds;
            mapSize = new Vector2(bounds.size.x, bounds.size.y);
            mapOrigin = new Vector2(bounds.min.x, bounds.min.y);
        }

        // Calculate clamp size based on camera aspect
        RecalculateClampSize();

        // Set initial camera position
        cam.transform.position = new Vector3(-20f, 0f, -10f);

        ClampCamera();
    }

    private void Update()
    {
        HandleZoomInput();
        HandleDragInput();
        ApplyZoom();
        ClampCamera();
    }

    private float GetEffectiveMaxZoom()
    {
        // Don't zoom out beyond clamp size
        float maxHeight = clampSize.y / 2f;
        float maxWidth = clampSize.x / (2f * cam.aspect);
        return Mathf.Min(maxZoom, Mathf.Min(maxHeight, maxWidth));
    }

    private void HandleZoomInput()
    {
        if (cam == null)
            return;
        if (EventSystem.current != null && IsPointerOverBlockingUi())
            return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        float effectiveMax = GetEffectiveMaxZoom();
        targetZoom -= scroll * zoomSpeed;
        targetZoom = Mathf.Clamp(targetZoom, minZoom, effectiveMax);
    }

    private static bool IsPointerOverBlockingUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;
        if (!eventSystem.IsPointerOverGameObject())
            return false;

        PointerEventData pointerEventData = new PointerEventData(eventSystem);
        pointerEventData.position = Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerEventData, results);
        if (results.Count == 0)
            return false;

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            if (hitObject == null)
                continue;
            if (!IsZoomWhitelisted(hitObject))
                return true;
        }
        return false;
    }

    private static bool IsZoomWhitelisted(GameObject hitObject)
    {
        if (hitObject.GetComponentInParent<TenantAssignmentPanelReveal>() != null)
            return false;
        if (hitObject.GetComponentInParent<RoomTenantAvatarSlot>() != null)
            return true;
        TenantInfoPanel panel = hitObject.GetComponentInParent<TenantInfoPanel>();
        return panel != null
            && panel.Mode == TenantInfoPanel.PanelMode.Hover
            && panel.Source == TenantInfoPanel.DisplaySource.RoomSlot;
    }

    private void ApplyZoom()
    {
        if (cam == null)
            return;

        float effectiveMax = GetEffectiveMaxZoom();
        targetZoom = Mathf.Clamp(targetZoom, minZoom, effectiveMax);

        Vector3 worldBefore = GetMouseWorldPoint();

        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);

        Vector3 worldAfter = GetMouseWorldPoint();
        Vector3 offset = worldBefore - worldAfter;
        if (offset.sqrMagnitude > 0.000001f)
        {
            Transform cameraTransform = cam.transform;
            cameraTransform.position = cameraTransform.position + offset;
        }
    }

    private Vector3 GetMouseWorldPoint()
    {
        if (cam == null || !cam.orthographic)
            return Vector3.zero;
        Vector3 screenPoint = Input.mousePosition;
        screenPoint.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(screenPoint);
    }

    private void HandleDragInput()
    {
        if (TenantAssignmentCoordinator.Instance != null && TenantAssignmentCoordinator.Instance.IsDragging)
        {
            isDragging = false;
            return;
        }

        // Don't drag if mouse is over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            isDragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartPos = Input.mousePosition;
            dragOrigin = cam.transform.position;
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Vector3 delta = Input.mousePosition - dragStartPos;

            // Convert pixel delta to world units
            float worldPerPixel = cam.orthographicSize * 2f / Screen.height;

            // Natural drag: mouse right → world moves right → camera moves left
            Vector3 newPos = dragOrigin + new Vector3(
                -delta.x * worldPerPixel * dragSpeed,
                -delta.y * worldPerPixel * dragSpeed,
                0f);

            cam.transform.position = newPos;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    private void RecalculateClampSize()
    {
        if (mapSize.y <= 0f) return;

        float mapAspect = mapSize.x / mapSize.y;
        float camAspect = cam.aspect;

        if (mapAspect < camAspect)
        {
            // Map is taller relative to camera → expand width
            float expandedWidth = mapSize.y * camAspect;
            clampSize = new Vector2(expandedWidth, mapSize.y);
        }
        else
        {
            // Map is wider relative to camera → expand height
            float expandedHeight = mapSize.x / camAspect;
            clampSize = new Vector2(mapSize.x, expandedHeight);
        }

        Debug.Log($"[CameraController] mapSize={mapSize}, clampSize={clampSize}, mapAspect={mapAspect:F2}, camAspect={camAspect:F2}");
    }

    private void ClampCamera()
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        Vector3 pos = cam.transform.position;

        // Use clampSize (expanded to match camera aspect)
        float xMin = mapOrigin.x + halfWidth;
        float xMax = mapOrigin.x + clampSize.x - halfWidth;
        float yMin = mapOrigin.y + halfHeight;
        float yMax = mapOrigin.y + clampSize.y - halfHeight;

        // If map is smaller than view, center the camera
        if (xMin >= xMax)
            pos.x = mapOrigin.x + clampSize.x / 2f;
        else
            pos.x = Mathf.Clamp(pos.x, xMin, xMax);

        if (yMin >= yMax)
            pos.y = mapOrigin.y + clampSize.y / 2f;
        else
            pos.y = Mathf.Clamp(pos.y, yMin, yMax);

        pos.z = -10f; // Keep camera at z=-10 for 2D

        cam.transform.position = pos;
    }

    // Call this when switching rooms/maps
    public void SetMapBounds(Vector2 newSize, Vector2 newOrigin)
    {
        mapSize = newSize;
        mapOrigin = newOrigin;
        RecalculateClampSize();
        ClampCamera();
    }

    // For debug: draw map bounds in Scene view
    private void OnDrawGizmosSelected()
    {
        // Map bounds - green
        Gizmos.color = Color.green;
        Vector3 mapCenter = new Vector3(mapOrigin.x + mapSize.x / 2f, mapOrigin.y + mapSize.y / 2f, 0f);
        Gizmos.DrawWireCube(mapCenter, new Vector3(mapSize.x, mapSize.y, 0f));

        // Clamp bounds - yellow
        Gizmos.color = Color.yellow;
        Vector3 clampCenter = new Vector3(mapOrigin.x + clampSize.x / 2f, mapOrigin.y + clampSize.y / 2f, 0f);
        Gizmos.DrawWireCube(clampCenter, new Vector3(clampSize.x, clampSize.y, 0f));
    }
}
