using Hotel.Runtime;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(22)]
public class RoomAvatarFlagRing : MonoBehaviour
{
    [SerializeField] private Image ringImage;
    [SerializeField] private int occupantIndex;

    private RoomTenantAvatarSlot _slot;
    private bool _subscribed;
    private bool _runStateRestoredSubscribed;

    public int OccupantIndex
    {
        get => occupantIndex;
        set => occupantIndex = value;
    }

    private void Awake()
    {
        ResolveSlot();
    }

    private void ResolveSlot()
    {
        _slot = null;
        Transform container = transform.parent;
        if (container == null)
            return;
        RoomTenantAvatarSlot[] slots = container.GetComponentsInChildren<RoomTenantAvatarSlot>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null || slots[i].transform == container)
                continue;
            if (slots[i].OccupantIndex == occupantIndex)
            {
                _slot = slots[i];
                break;
            }
        }
    }

    private void LateUpdate()
    {
        if (_slot == null)
            return;
        RectTransform slotRect = _slot.transform as RectTransform;
        RectTransform selfRect = transform as RectTransform;
        if (slotRect == null || selfRect == null)
            return;
        selfRect.anchoredPosition = slotRect.anchoredPosition;
    }

    private void OnEnable()
    {
        Subscribe();
        SubscribeRunStateRestored();
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
        UnsubscribeRunStateRestored();
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

    private void SubscribeRunStateRestored()
    {
        if (_runStateRestoredSubscribed)
            return;
        SettlementBridge.RunStateRestored += OnRunStateRestored;
        _runStateRestoredSubscribed = true;
    }

    private void UnsubscribeRunStateRestored()
    {
        if (!_runStateRestoredSubscribed)
            return;
        SettlementBridge.RunStateRestored -= OnRunStateRestored;
        _runStateRestoredSubscribed = false;
    }

    private void OnRunStateRestored(GameRunState state)
    {
        Refresh();
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
