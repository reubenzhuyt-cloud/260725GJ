using UnityEngine;
using UnityEngine.EventSystems;

public class TenantAvatarDragTrigger : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TenantAvatarListItem owner;

    private bool _finished;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        _finished = false;
        if (owner != null)
            owner.BeginAvatarHold();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (_finished)
            return;
        _finished = true;
        if (owner != null)
            owner.EndAvatarHold();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (_finished)
            return;
        _finished = true;
        if (owner != null)
            owner.EndAvatarHold();
    }
}
