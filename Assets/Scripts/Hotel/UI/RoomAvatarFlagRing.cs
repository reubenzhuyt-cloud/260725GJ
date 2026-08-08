using Hotel.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class RoomAvatarFlagRing : MonoBehaviour
{
    [SerializeField] private Image ringImage;

    private RoomTenantAvatarSlot _slot;
    private bool _subscribed;

    private void Awake()
    {
        _slot = GetComponentInParent<RoomTenantAvatarSlot>();
    }

    private void OnEnable()
    {
        Subscribe();
        TenantInfoPanel.TenantFlagChanged += OnTenantFlagChanged;
        Refresh();
    }

    private void Start()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        TenantInfoPanel.TenantFlagChanged -= OnTenantFlagChanged;
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;
        if (TenantAssignmentCoordinator.Instance != null)
        {
            TenantAssignmentCoordinator.Instance.AssignmentChanged += OnAssignmentChanged;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged -= OnAssignmentChanged;
        _subscribed = false;
    }

    private void OnAssignmentChanged()
    {
        Refresh();
    }

    private void OnTenantFlagChanged(string tenantId, int flag)
    {
        if (_slot != null && tenantId == _slot.GetOccupantId())
            Refresh();
    }

    public void Refresh()
    {
        if (ringImage == null)
            return;

        string occupantId = _slot != null ? _slot.GetOccupantId() : null;
        if (string.IsNullOrEmpty(occupantId))
        {
            ringImage.enabled = false;
            return;
        }

        ringImage.enabled = true;
        ringImage.color = GetFlagColor(ReadPlayerFlag(occupantId));
    }

    private static int ReadPlayerFlag(string tenantId)
    {
        SettlementBridge bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null)
            return 0;
        if (bridge.RunState.Tenants.TryGetValue(tenantId, out TenantRunState tenant))
            return tenant.PlayerFlag;
        return 0;
    }

    private static Color GetFlagColor(int flag)
    {
        switch (flag)
        {
            case 1: return new Color(0f, 0.8f, 0.2f, 1f);
            case 2: return new Color(0.9f, 0.8f, 0.1f, 1f);
            case 3: return new Color(0.9f, 0.2f, 0.1f, 1f);
            default: return Color.black;
        }
    }
}
