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
        if (hoverTrigger != null)
            hoverTrigger.tenantIdProvider = () => GetOccupantId();
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
            coloredCircle.gameObject.SetActive(occupied);
            if (occupied)
            {
                string occupantId = TenantAssignmentCoordinator.Instance.GetRoomOccupantId(roomId);
                if (occupantId != null &&
                    TenantAssignmentCoordinator.Instance.TryGetTenantColor(occupantId, out Color color))
                    coloredCircle.SetColor(color);
            }
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
