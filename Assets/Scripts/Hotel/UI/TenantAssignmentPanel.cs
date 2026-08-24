using System.Collections.Generic;
using UnityEngine;

public class TenantAssignmentPanel : MonoBehaviour
{
    private static readonly List<TenantAssignmentPanel> AllPanels = new List<TenantAssignmentPanel>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AllPanels.Clear();
    }

    [SerializeField] private Transform listContainer;
    [SerializeField] private TenantAvatarListItem avatarItemPrefab;

    private readonly List<GameObject> _spawnedItems = new List<GameObject>();

    private void OnEnable()
    {
        if (!AllPanels.Contains(this))
            AllPanels.Add(this);

        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        AllPanels.Remove(this);

        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged -= Refresh;
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
            TenantAssignmentCoordinator.Instance.PanelTenants;

        for (int i = 0; i < tenants.Count; i++)
        {
            TenantAssignmentItemView data = tenants[i];
            TenantAvatarListItem item = Instantiate(avatarItemPrefab, listContainer);
            item.gameObject.SetActive(true);
            item.Initialize(data.TenantId, data.DisplayName, data.Color, data.AvatarKey, data.IsAssigned);
            _spawnedItems.Add(item.gameObject);
        }
    }

    public static void RefreshAll()
    {
        for (int i = 0; i < AllPanels.Count; i++)
        {
            if (AllPanels[i] != null)
                AllPanels[i].Refresh();
        }
    }
}
