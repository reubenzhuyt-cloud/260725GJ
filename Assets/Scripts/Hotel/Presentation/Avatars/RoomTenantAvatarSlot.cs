using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(20)]
public class RoomTenantAvatarSlot : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private Image avatarImage;
    [SerializeField] private TenantInfoHoverTrigger hoverTrigger;
    [SerializeField] private Transform positionAnchor;
    [SerializeField] private float baseWorldSize = 0.96f;
    [SerializeField] private float lodDetailThreshold = 20f;
    [SerializeField] private float lodClosestZoom = 12f;
    [SerializeField] private float lodMaxScale = 2f;

    private static readonly List<RoomTenantAvatarSlot> AllSlots = new List<RoomTenantAvatarSlot>();

    private bool _isDragVisual;

    public string RoomId => roomId;

    public Transform PositionAnchor => positionAnchor;

    private void Awake()
    {
        if (hoverTrigger == null)
            hoverTrigger = GetComponent<TenantInfoHoverTrigger>();
        if (hoverTrigger != null)
        {
            hoverTrigger.tenantIdProvider = GetOccupantId;
            hoverTrigger.enableUiRightClick = true;
            hoverTrigger.source = TenantInfoPanel.DisplaySource.RoomSlot;
        }
        if (GetComponent<RoomTenantSlotDragTrigger>() == null)
            gameObject.AddComponent<RoomTenantSlotDragTrigger>();
    }

    private void OnEnable()
    {
        if (!AllSlots.Contains(this))
            AllSlots.Add(this);
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        // If OnEnable ran before TenantAssignmentCoordinator.Awake,
        // re-subscribe so AssignmentChanged is still received.
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        AllSlots.Remove(this);
        Unsubscribe();
    }

    private bool _subscribed;

    private void Subscribe()
    {
        if (_subscribed)
            return;
        if (TenantAssignmentCoordinator.Instance != null)
        {
            TenantAssignmentCoordinator.Instance.AssignmentChanged += Refresh;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged -= Refresh;
        _subscribed = false;
    }

    private void LateUpdate()
    {
        TrackAnchorPosition();
        UpdateSizeForZoom();
    }

    public string GetOccupantId()
    {
        if (TenantAssignmentCoordinator.Instance == null)
            return null;
        return TenantAssignmentCoordinator.Instance.GetRoomOccupantId(roomId);
    }

    public void Refresh()
    {
        if (avatarImage == null)
            return;

        string occupantId = GetOccupantId();
        bool occupied = !string.IsNullOrEmpty(occupantId);

        // The Image stays enabled at all times so the slot remains a valid
        // UI drop target and pointer surface even when the room is empty.
        // "Hidden" is expressed via a transparent color, not SetActive(false).
        if (occupied && TenantAssignmentCoordinator.Instance.TryGetTenantColor(occupantId, out Color color))
        {
            color.a = 1f;
            avatarImage.color = color;
            avatarImage.enabled = true;
        }
        else
        {
            Color c = avatarImage.color;
            c.a = 0f;
            avatarImage.color = c;
            avatarImage.enabled = true;
        }

        if (_isDragVisual && occupied)
        {
            Color dragColor = avatarImage.color;
            dragColor.a *= 0.4f;
            avatarImage.color = dragColor;
        }
    }

    public void SetDragVisual(bool active)
    {
        if (_isDragVisual == active)
            return;
        _isDragVisual = active;
        Refresh();
    }

    private void TrackAnchorPosition()
    {
        if (positionAnchor == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform self = transform as RectTransform;
        if (canvasRect == null || self == null)
            return;

        Vector2 screenPoint = cam.WorldToScreenPoint(positionAnchor.position);
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, eventCamera, out Vector2 local))
        {
            self.anchoredPosition = local;
        }
    }

    private void UpdateSizeForZoom()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic)
            return;

        float zoom = cam.orthographicSize;
        if (zoom <= 0f)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.scaleFactor <= 0f)
            return;

        RectTransform self = transform as RectTransform;
        if (self == null)
            return;

        float multiplier = 1f;
        float range = lodDetailThreshold - lodClosestZoom;
        if (range > 0f)
            multiplier = Mathf.Lerp(1f, lodMaxScale,
                Mathf.InverseLerp(lodDetailThreshold, lodClosestZoom, zoom));

        float effectiveWorldSize = baseWorldSize * multiplier;
        float screenPixels = effectiveWorldSize * Screen.height / (2f * zoom);
        float canvasUnits = screenPixels / canvas.scaleFactor;

        self.sizeDelta = new Vector2(canvasUnits, canvasUnits);
    }

    public static IReadOnlyList<RoomTenantAvatarSlot> GetSlotsForRoom(string roomId)
    {
        List<RoomTenantAvatarSlot> result = new List<RoomTenantAvatarSlot>();
        for (int i = 0; i < AllSlots.Count; i++)
        {
            if (AllSlots[i] != null && AllSlots[i].roomId == roomId)
                result.Add(AllSlots[i]);
        }
        return result;
    }

    public static void RefreshAll()
    {
        for (int i = 0; i < AllSlots.Count; i++)
        {
            if (AllSlots[i] != null)
                AllSlots[i].Refresh();
        }
    }
}
