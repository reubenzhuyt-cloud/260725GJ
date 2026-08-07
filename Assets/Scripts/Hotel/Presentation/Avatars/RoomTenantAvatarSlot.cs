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

    private static readonly List<RoomTenantAvatarSlot> AllSlots = new List<RoomTenantAvatarSlot>();

    public string RoomId => roomId;

    private void Awake()
    {
        if (hoverTrigger == null)
            hoverTrigger = GetComponent<TenantInfoHoverTrigger>();
        if (hoverTrigger != null)
        {
            hoverTrigger.tenantIdProvider = GetOccupantId;
            hoverTrigger.enableUiRightClick = true;
        }
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
