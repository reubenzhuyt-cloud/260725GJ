using Hotel.Runtime;
using UnityEngine;

public class TenantAssignmentPanelEventVisibility : MonoBehaviour
{
    [SerializeField] private GameObject tenantAssignmentPanel;
    [SerializeField] private GamePopupEvent onPopupEvent;
    [SerializeField] private EventQueueEmptyEvent onEventQueueEmpty;

    private bool _runStateRestoredSubscribed;

    private void OnEnable()
    {
        if (onPopupEvent != null)
            onPopupEvent.Register(OnPopupEvent);

        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Register(OnEventQueueEmpty);

        if (!_runStateRestoredSubscribed)
        {
            SettlementBridge.RunStateRestored += OnRunStateRestored;
            _runStateRestoredSubscribed = true;
        }
    }

    private void Start()
    {
        SyncFromAuthoritativeState();
    }

    private void OnDisable()
    {
        if (onPopupEvent != null)
            onPopupEvent.Unregister(OnPopupEvent);

        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Unregister(OnEventQueueEmpty);

        if (_runStateRestoredSubscribed)
        {
            SettlementBridge.RunStateRestored -= OnRunStateRestored;
            _runStateRestoredSubscribed = false;
        }
    }

    private void OnPopupEvent(PopupData data)
    {
        if (tenantAssignmentPanel != null)
            tenantAssignmentPanel.SetActive(false);
    }

    private void OnEventQueueEmpty(int data)
    {
        if (tenantAssignmentPanel != null)
            tenantAssignmentPanel.SetActive(true);
    }

    private void OnRunStateRestored(GameRunState state)
    {
        SyncFromAuthoritativeState();
    }

    private void SyncFromAuthoritativeState()
    {
        if (EventManager.Instance == null)
            return;

        if (tenantAssignmentPanel != null)
            tenantAssignmentPanel.SetActive(EventManager.Instance.IsPhaseComplete);
    }
}
