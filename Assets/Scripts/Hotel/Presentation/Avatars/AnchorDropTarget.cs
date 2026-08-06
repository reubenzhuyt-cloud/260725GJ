using UnityEngine;

[DefaultExecutionOrder(10)]
public class AnchorDropTarget : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private TenantAvatarDisplay coloredCircle;
    [SerializeField] private SpriteRenderer detailBackground;
    [SerializeField] private TenantInfoHoverTrigger hoverTrigger;

    public string RoomId => roomId;

    private void OnEnable()
    {
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged += Refresh;
    }

    private void OnDisable()
    {
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged -= Refresh;
    }

    private void Awake()
    {
        // The avatar itself now owns hover. Disable the anchor's scene trigger so it
        // cannot compete with (or shadow) the avatar's own trigger.
        var selfTrigger = GetComponent<TenantInfoHoverTrigger>();
        if (selfTrigger != null)
            selfTrigger.enabled = false;

        // Configure the avatar's own trigger from the scene-wired anchor trigger
        // (panel references, delays, placement). The avatar trigger binds its
        // tenantId at runtime via SetOccupant/ClearOccupant.
        if (hoverTrigger != null && coloredCircle != null)
        {
            TenantInfoHoverTrigger avatarTrigger = coloredCircle.GetOrCreateTrigger();
            avatarTrigger.hoverInfoPanel = hoverTrigger.hoverInfoPanel;
            avatarTrigger.pinnedInfoPanel = hoverTrigger.pinnedInfoPanel;
            avatarTrigger.hoverDelay = hoverTrigger.hoverDelay;
            avatarTrigger.hideDelay = hoverTrigger.hideDelay;
            avatarTrigger.hitMask = hoverTrigger.hitMask;
            avatarTrigger.preferLeftPlacement = hoverTrigger.preferLeftPlacement;
        }
    }

    private void Start()
    {
        Refresh();
    }

    public string GetOccupantId()
    {
        if (TenantAssignmentCoordinator.Instance == null)
            return null;
        return TenantAssignmentCoordinator.Instance.GetRoomOccupantId(roomId);
    }

    public void Refresh()
    {
        if (TenantAssignmentCoordinator.Instance == null)
            return;

        bool occupied = TenantAssignmentCoordinator.Instance.IsRoomOccupied(roomId);

        if (coloredCircle != null)
        {
            if (occupied)
            {
                string occupantId = TenantAssignmentCoordinator.Instance.GetRoomOccupantId(roomId);
                coloredCircle.SetOccupant(occupantId);
                if (occupantId != null &&
                    TenantAssignmentCoordinator.Instance.TryGetTenantColor(occupantId, out Color color))
                    coloredCircle.SetColor(color);
            }
            else
            {
                coloredCircle.ClearOccupant();
            }
            coloredCircle.gameObject.SetActive(occupied);
        }

        if (detailBackground != null)
            detailBackground.gameObject.SetActive(true);
    }

    public static void RefreshAll()
    {
        if (TenantAssignmentCoordinator.Instance == null)
            return;
        AnchorDropTarget[] all = FindObjectsOfType<AnchorDropTarget>(true);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].Refresh();
        }
    }
}
