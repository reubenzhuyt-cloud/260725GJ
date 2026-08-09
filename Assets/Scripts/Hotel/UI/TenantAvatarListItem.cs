using UnityEngine;
using UnityEngine.UI;

public class TenantAvatarListItem : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMPro.TextMeshProUGUI nameLabel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TenantInfoHoverTrigger hoverTrigger;

    private string _tenantId;
    private Color _itemColor;
    private Sprite _placeholderSprite;

    private bool _isDragging;
    private bool _dragFinished;
    private float _authoredAlpha;

    private void Awake()
    {
        if (avatarImage != null)
            _placeholderSprite = avatarImage.sprite;
    }

    public void Initialize(string tenantId, string displayName, Color color, string avatarKey)
    {
        _tenantId = tenantId;
        _itemColor = color;
        _authoredAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        if (hoverTrigger != null)
        {
            hoverTrigger.tenantIdProvider = () => _tenantId;
            hoverTrigger.source = TenantInfoPanel.DisplaySource.ListItem;
        }

        if (avatarImage != null)
        {
            if (!string.IsNullOrEmpty(avatarKey) && TenantAvatarResolver.TryResolve(avatarKey, out Sprite avatar))
            {
                avatarImage.sprite = avatar;
                avatarImage.color = Color.white;
            }
            else
            {
                avatarImage.sprite = _placeholderSprite;
                avatarImage.color = color;
            }
        }
        if (nameLabel != null)
            nameLabel.text = displayName;
    }

    public void OpenPinnedFromTrigger()
    {
        if (hoverTrigger != null)
            hoverTrigger.OpenPinned();
    }

    private void LateUpdate()
    {
        if (_isDragging && TenantDragOverlay.Instance != null)
            TenantDragOverlay.Instance.UpdatePosition(Input.mousePosition);
    }

    public void BeginAvatarHold()
    {
        _isDragging = true;
        _dragFinished = false;

        if (hoverTrigger != null)
        {
            hoverTrigger.HideHoverPanel();
            hoverTrigger.ClosePinned();
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0.45f;

        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.SetDragging(true);

        if (TenantDragOverlay.Instance != null)
            TenantDragOverlay.Instance.Show(_itemColor);
    }

    public void EndAvatarHold()
    {
        FinishDrag();
    }

    public void FinishDrag()
    {
        if (_dragFinished)
            return;

        _dragFinished = true;

        bool wasDragging = _isDragging;
        _isDragging = false;

        if (wasDragging)
        {
            if (TenantDragOverlay.Instance != null)
                TenantDragOverlay.Instance.Hide();

            TenantAssignmentCoordinator coordinator = TenantAssignmentCoordinator.Instance;
            if (coordinator != null)
            {
                coordinator.SetDragging(false);

                if (RoomWorldHitArea.TryResolveRoomUnderPointer(Input.mousePosition, out string roomId))
                    coordinator.TryAssign(_tenantId, roomId);
            }
        }

        RestoreAlpha();
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
        if (_isDragging)
        {
            if (TenantDragOverlay.Instance != null)
                TenantDragOverlay.Instance.Hide();

            if (TenantAssignmentCoordinator.Instance != null)
                TenantAssignmentCoordinator.Instance.SetDragging(false);
        }

        _isDragging = false;
        _dragFinished = false;
        RestoreAlpha();
    }

    private void RestoreAlpha()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = _authoredAlpha;
    }
}
