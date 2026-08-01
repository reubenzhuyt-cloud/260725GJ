using UnityEngine;
using UnityEngine.EventSystems;

public class TenantAssignmentPanelRevealRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TenantAssignmentPanelReveal controller;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (controller != null)
            controller.PointerEntered();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (controller != null)
            controller.PointerExited();
    }
}
