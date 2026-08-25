using System.Collections.Generic;
using Hotel.UI;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(20)]
public class RoomTenantAvatarSlot : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private Image avatarImage;
    [SerializeField] private TenantInfoHoverTrigger hoverTrigger;
    [SerializeField] private Transform positionAnchor;
    [SerializeField, Min(1f)] private float screenSize = 120f;
    [SerializeField] private int occupantIndex;

    private static readonly List<RoomTenantAvatarSlot> AllSlots = new List<RoomTenantAvatarSlot>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AllSlots.Clear();
    }

    private bool _isDragVisual;
    private Sprite _placeholderSprite;
    private RoomAvatarSlotLayoutController _parentLayoutController;
    private TenantAssignmentCoordinator _cachedCoordinator;
    private RedDotIndicator _instantiatedRedDot;

    public string RoomId => roomId;

    public int OccupantIndex => occupantIndex;

    public Transform PositionAnchor => positionAnchor;

    private void Awake()
    {
        if (avatarImage != null)
            _placeholderSprite = avatarImage.sprite;
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
        _parentLayoutController = transform.parent != null
            ? transform.parent.GetComponentInParent<RoomAvatarSlotLayoutController>()
            : null;

        InitializeRedDotIndicator();
    }

    private void InitializeRedDotIndicator()
    {
        if (GetComponent<RoomAvatarSlotLayoutController>() != null) return;

        RedDotIndicator prefab = Resources.Load<RedDotIndicator>("UI/RedDotIndicator");
        if (prefab != null)
        {
            _instantiatedRedDot = Instantiate(prefab, transform, false);
            RectTransform rect = _instantiatedRedDot.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-2f, -2f);
            }
            _instantiatedRedDot.transform.SetAsLastSibling();
            _instantiatedRedDot.SetVisible(false);
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
        if (Subscribe())
            Refresh();
    }

    private void OnDisable()
    {
        AllSlots.Remove(this);
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private bool _subscribed;

    private bool Subscribe()
    {
        if (_subscribed)
            return false;
        if (TenantAssignmentCoordinator.Instance != null)
        {
            _cachedCoordinator = TenantAssignmentCoordinator.Instance;
            _cachedCoordinator.AssignmentChanged += Refresh;
            _cachedCoordinator.JobAssignmentChanged += Refresh;
            _subscribed = true;
            return true;
        }
        return false;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;
        if (_cachedCoordinator != null)
        {
            _cachedCoordinator.AssignmentChanged -= Refresh;
            _cachedCoordinator.JobAssignmentChanged -= Refresh;
            _cachedCoordinator = null;
        }
        _subscribed = false;
    }

    private void LateUpdate()
    {
        if (_parentLayoutController != null)
            return;
        TrackAnchorPosition();
        UpdateFixedScreenSize();
    }

    public string GetOccupantId()
    {
        if (TenantAssignmentCoordinator.Instance == null)
            return null;
        IReadOnlyList<string> occupants = TenantAssignmentCoordinator.Instance.GetRoomOccupantIds(roomId);
        if (occupantIndex < 0 || occupantIndex >= occupants.Count)
            return null;
        return occupants[occupantIndex];
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
        if (occupied && TenantAssignmentCoordinator.Instance.TryGetTenantAvatar(occupantId, out Sprite avatar))
        {
            avatarImage.sprite = avatar;
            avatarImage.color = Color.white;
            avatarImage.enabled = true;
        }
        else if (occupied && TenantAssignmentCoordinator.Instance.TryGetTenantColor(occupantId, out Color color))
        {
            avatarImage.sprite = _placeholderSprite;
            color.a = 1f;
            avatarImage.color = color;
            avatarImage.enabled = true;
        }
        else
        {
            avatarImage.sprite = _placeholderSprite;
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

        UpdateRedDotVisibility(occupantId);
    }

    private void UpdateRedDotVisibility(string occupantId)
    {
        if (_instantiatedRedDot == null)
            return;

        bool show = false;
        if (!string.IsNullOrEmpty(occupantId))
        {
            SettlementBridge bridge = SettlementBridge.Instance;
            if (bridge != null && bridge.RunState != null && bridge.RunState.Tenants != null && bridge.RunState.Tenants.TryGetValue(occupantId, out var tenant))
            {
                if (tenant != null && !string.IsNullOrEmpty(tenant.RoomId) && tenant.RoomId == roomId && string.IsNullOrEmpty(tenant.JobId))
                {
                    show = true;
                }
            }
        }

        _instantiatedRedDot.SetVisible(show);
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

    private void UpdateFixedScreenSize()
    {
        RectTransform self = transform as RectTransform;
        if (self == null)
            return;

        float size = Mathf.Max(screenSize, 1f);
        self.sizeDelta = new Vector2(size, size);
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
