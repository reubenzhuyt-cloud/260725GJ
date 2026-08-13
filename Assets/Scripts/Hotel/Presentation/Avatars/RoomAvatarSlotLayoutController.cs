using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(21)]
public class RoomAvatarSlotLayoutController : MonoBehaviour
{
    [SerializeField] private List<RoomTenantAvatarSlot> views = new List<RoomTenantAvatarSlot>();
    [SerializeField, Min(1f)] private float screenSize = 120f;
    [SerializeField, Min(0f)] private float spacing = 8f;

    private void LateUpdate()
    {
        if (views == null || views.Count == 0)
            return;

        string roomId = null;
        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != null)
            {
                roomId = views[i].RoomId;
                break;
            }
        }
        if (string.IsNullOrEmpty(roomId))
            return;

        RectTransform panel = transform as RectTransform;
        if (panel == null)
            return;

        int capacity = 1;
        int assignedCount = 0;
        if (TenantAssignmentCoordinator.Instance != null)
        {
            TenantAssignmentCoordinator.Instance.TryGetRoomCapacity(roomId, out capacity);
            assignedCount = TenantAssignmentCoordinator.Instance.GetRoomOccupantCount(roomId);
        }

        int visibleCount = Mathf.Clamp(assignedCount, 1, capacity);
        float size = Mathf.Max(screenSize, 1f);
        float step = size + spacing;

        panel.sizeDelta = new Vector2(visibleCount * size + (visibleCount - 1) * spacing, size);

        for (int v = 0; v < views.Count; v++)
        {
            RoomTenantAvatarSlot viewSlot = views[v];
            RectTransform view = viewSlot != null ? viewSlot.transform as RectTransform : null;
            if (view == null)
                continue;

            bool visible = v < visibleCount;
            if (view.gameObject.activeSelf != visible)
                view.gameObject.SetActive(visible);
            if (!visible)
                continue;

            view.anchorMin = new Vector2(0.5f, 0.5f);
            view.anchorMax = new Vector2(0.5f, 0.5f);
            view.pivot = new Vector2(0.5f, 0.5f);
            view.sizeDelta = new Vector2(size, size);
            view.anchoredPosition = new Vector2((v - (visibleCount - 1) * 0.5f) * step, 0f);
        }
    }
}
