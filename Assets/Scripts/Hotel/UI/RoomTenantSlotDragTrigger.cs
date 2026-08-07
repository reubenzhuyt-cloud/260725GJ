using UnityEngine;
using UnityEngine.EventSystems;

public class RoomTenantSlotDragTrigger : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private float holdDuration = 0.4f;

    private RoomTenantAvatarSlot _slot;
    private TenantInfoHoverTrigger _hoverTrigger;

    private string _pressedTenantId;
    private Color _overlayColor;
    private float _pressTime;
    private bool _dragStarted;

    private void Awake()
    {
        _slot = GetComponent<RoomTenantAvatarSlot>();
        _hoverTrigger = GetComponent<TenantInfoHoverTrigger>();
    }

    private void Update()
    {
        if (!_dragStarted && !string.IsNullOrEmpty(_pressedTenantId)
            && Input.GetMouseButton(0)
            && Time.unscaledTime - _pressTime >= holdDuration)
        {
            StartDrag();
        }

        if (_dragStarted)
        {
            if (!Input.GetMouseButton(0))
            {
                EndDrag();
                return;
            }
            if (TenantDragOverlay.Instance != null)
                TenantDragOverlay.Instance.UpdatePosition(Input.mousePosition);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (_slot == null)
            return;
        string occupantId = _slot.GetOccupantId();
        if (string.IsNullOrEmpty(occupantId))
            return;

        _pressedTenantId = occupantId;
        _pressTime = Time.unscaledTime;
        _dragStarted = false;

        Color color = Color.white;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.TryGetTenantColor(occupantId, out color);
        _overlayColor = color;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        ReleaseDrag();
    }

    private void StartDrag()
    {
        if (_dragStarted)
            return;
        if (TenantAssignmentCoordinator.Instance == null || TenantDragOverlay.Instance == null)
        {
            _pressedTenantId = null;
            return;
        }

        _dragStarted = true;

        if (_slot != null)
            _slot.SetDragVisual(true);

        if (_hoverTrigger != null)
        {
            _hoverTrigger.HideHoverPanel();
            _hoverTrigger.ClosePinned();
        }

        TenantAssignmentCoordinator.Instance.SetDragging(true);
        TenantDragOverlay.Instance.Show(_overlayColor);
    }

    private void ReleaseDrag()
    {
        if (!_dragStarted)
        {
            _pressedTenantId = null;
            return;
        }
        EndDrag();
    }

    private void EndDrag()
    {
        if (!_dragStarted)
            return;
        _dragStarted = false;

        if (_slot != null)
            _slot.SetDragVisual(false);

        if (TenantDragOverlay.Instance != null)
            TenantDragOverlay.Instance.Hide();
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.SetDragging(false);

        string tenantId = _pressedTenantId;
        _pressedTenantId = null;
        if (string.IsNullOrEmpty(tenantId))
            return;

        TenantAssignmentCoordinator coordinator = TenantAssignmentCoordinator.Instance;
        if (coordinator == null)
            return;

        RoomTenantAvatarSlot target = TenantAvatarListItem.FindRoomSlotUnderPointer();
        if (target == null || target == _slot)
            return;
        if (!string.IsNullOrEmpty(target.GetOccupantId()))
            return;

        coordinator.TryMoveToEmptyRoom(tenantId, target.RoomId);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ReleaseDrag();
    }

    private void OnDisable()
    {
        CleanupDrag();
    }

    private void OnDestroy()
    {
        CleanupDrag();
    }

    private void CleanupDrag()
    {
        if (_slot != null)
            _slot.SetDragVisual(false);
        if (_dragStarted)
        {
            _dragStarted = false;
            if (TenantDragOverlay.Instance != null)
                TenantDragOverlay.Instance.Hide();
            if (TenantAssignmentCoordinator.Instance != null)
                TenantAssignmentCoordinator.Instance.SetDragging(false);
        }
        _pressedTenantId = null;
    }
}
