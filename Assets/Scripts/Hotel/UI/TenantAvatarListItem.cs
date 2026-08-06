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

    private bool _isDragging;
    private bool _dragFinished;
    private float _authoredAlpha;

    public void Initialize(string tenantId, string displayName, Color color)
    {
        _tenantId = tenantId;
        _itemColor = color;
        _authoredAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        if (hoverTrigger != null)
            hoverTrigger.tenantIdProvider = () => _tenantId;

        if (avatarImage != null)
            avatarImage.color = color;
        if (nameLabel != null)
            nameLabel.text = displayName;
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

            if (TenantAssignmentCoordinator.Instance != null)
                TenantAssignmentCoordinator.Instance.SetDragging(false);

            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 worldPoint = new Vector2(worldPos.x, worldPos.y);
            Collider2D hit = Physics2D.OverlapPoint(worldPoint);

            if (hit != null)
            {
                AnchorDropTarget target = hit.GetComponent<AnchorDropTarget>();
                if (target == null)
                    target = hit.GetComponentInParent<AnchorDropTarget>();

                if (target != null)
                {
                    if (TenantAssignmentCoordinator.Instance != null)
                        TenantAssignmentCoordinator.Instance.TryAssign(_tenantId, target.RoomId);
                }
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
