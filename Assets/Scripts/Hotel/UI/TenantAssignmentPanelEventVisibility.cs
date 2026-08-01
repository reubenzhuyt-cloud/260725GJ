using UnityEngine;

public class TenantAssignmentPanelEventVisibility : MonoBehaviour
{
    [SerializeField] private GameObject tenantAssignmentPanel;
    [SerializeField] private GamePopupEvent onPopupEvent;
    [SerializeField] private EventQueueEmptyEvent onEventQueueEmpty;

    private void OnEnable()
    {
        if (onPopupEvent != null)
            onPopupEvent.Register(OnPopupEvent);

        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Register(OnEventQueueEmpty);
    }

    private void OnDisable()
    {
        if (onPopupEvent != null)
            onPopupEvent.Unregister(OnPopupEvent);

        if (onEventQueueEmpty != null)
            onEventQueueEmpty.Unregister(OnEventQueueEmpty);
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
}
