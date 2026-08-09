using System.Collections.Generic;
using UnityEngine;

public class TenantAssignmentPanel : MonoBehaviour
{
    [SerializeField] private Transform listContainer;
    [SerializeField] private TenantAvatarListItem avatarItemPrefab;

    private readonly List<GameObject> _spawnedItems = new List<GameObject>();

    private void OnEnable()
    {
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged += Refresh;
    }

    private void OnDisable()
    {
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged -= Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        for (int i = _spawnedItems.Count - 1; i >= 0; i--)
        {
            if (_spawnedItems[i] != null)
                Destroy(_spawnedItems[i]);
        }
        _spawnedItems.Clear();

        if (TenantAssignmentCoordinator.Instance == null)
            return;

        if (avatarItemPrefab == null || listContainer == null)
            return;

        IReadOnlyList<TenantAssignmentItemView> tenants =
            TenantAssignmentCoordinator.Instance.UnassignedTenants;

        for (int i = 0; i < tenants.Count; i++)
        {
            TenantAssignmentItemView data = tenants[i];
            TenantAvatarListItem item = Instantiate(avatarItemPrefab, listContainer);
            item.gameObject.SetActive(true);
            item.Initialize(data.TenantId, data.DisplayName, data.Color, data.AvatarKey);
            _spawnedItems.Add(item.gameObject);
        }
    }

    public static void RefreshAll()
    {
        if (TenantAssignmentCoordinator.Instance == null)
            return;
        TenantAssignmentPanel[] all = FindObjectsOfType<TenantAssignmentPanel>(true);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].Refresh();
        }
    }
}
