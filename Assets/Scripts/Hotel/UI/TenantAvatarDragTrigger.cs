using Hotel.Audio;
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
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _finished = false;
            AudioManager.Instance?.PlayUISound(UISoundType.Click);
            if (owner != null)
                owner.BeginAvatarHold();
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            AudioManager.Instance?.PlayUISound(UISoundType.Click);
            if (owner != null)
                owner.OpenPinnedFromTrigger();
        }
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
