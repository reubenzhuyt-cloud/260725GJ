using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public class TenantAssignmentCoordinator : MonoBehaviour
{
    public static TenantAssignmentCoordinator Instance { get; private set; }

    public event Action AssignmentChanged;
    public bool IsDragging { get; private set; }

    private StateReducer _reducer;
    private GameRunState _runState;

    private readonly Dictionary<string, TenantAssignmentItemView> _displayLookup =
        new Dictionary<string, TenantAssignmentItemView>();

    private readonly List<string> _tenantOrder = new List<string>();

    private readonly List<TenantAssignmentItemView> _unassignedTenants =
        new List<TenantAssignmentItemView>();

    public IReadOnlyList<TenantAssignmentItemView> UnassignedTenants => _unassignedTenants;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (SettlementBridge.Instance == null)
        {
            Debug.LogError("[TenantAssignmentCoordinator] SettlementBridge.Instance is null!");
            return;
        }

        _reducer = SettlementBridge.Instance.Reducer;
        _runState = SettlementBridge.Instance.RunState;

        for (int i = 1; i <= 9; i++)
        {
            string roomId = string.Format("room_{0:D2}", i);
            _runState.Rooms[roomId] = new RoomRunState
            {
                RoomId = roomId,
                DefinitionId = roomId
            };
        }

        RebuildUnassigned();

        AnchorDropTarget.RefreshAll();
        TenantAssignmentPanel.RefreshAll();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void AddTenant(string tenantId, string displayName, Color color)
    {
        _runState.Tenants[tenantId] = new TenantRunState
        {
            TenantId = tenantId,
            DefinitionId = tenantId
        };
        _displayLookup[tenantId] = new TenantAssignmentItemView(tenantId, displayName, color);
        _tenantOrder.Add(tenantId);
    }

    public void RegisterTenant(string tenantId, string displayName, Color color)
    {
        if (_displayLookup.ContainsKey(tenantId)) return;
        _displayLookup[tenantId] = new TenantAssignmentItemView(tenantId, displayName, color);
        _tenantOrder.Add(tenantId);
        RebuildUnassigned();
        AnchorDropTarget.RefreshAll();
        TenantAssignmentPanel.RefreshAll();
    }

    private void RebuildUnassigned()
    {
        _unassignedTenants.Clear();
        for (int i = 0; i < _tenantOrder.Count; i++)
        {
            string id = _tenantOrder[i];
            TenantRunState tenant = _runState.Tenants[id];
            if (string.IsNullOrEmpty(tenant.RoomId))
                _unassignedTenants.Add(_displayLookup[id]);
        }
    }

    public void SetDragging(bool value)
    {
        IsDragging = value;
    }

    public bool TryAssign(string tenantId, string roomId)
    {
        if (_runState == null)
            return false;

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(roomId))
            return false;

        if (!_runState.Tenants.ContainsKey(tenantId) || !_runState.Rooms.ContainsKey(roomId))
            return false;

        if (IsRoomOccupied(roomId))
            return false;

        if (!string.IsNullOrEmpty(_runState.Tenants[tenantId].RoomId))
            return false;

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantAssignmentCoordinator",
            "AssignRoom");
        changeSet.Add(new AssignRoomChange(tenantId, roomId));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);

        if (result.Succeeded)
        {
            RebuildUnassigned();
            AssignmentChanged?.Invoke();
            AnchorDropTarget.RefreshAll();
            TenantAssignmentPanel.RefreshAll();
        }

        return result.Succeeded;
    }

    public bool TryGetTenantColor(string tenantId, out Color color)
    {
        if (_displayLookup.TryGetValue(tenantId, out TenantAssignmentItemView view))
        {
            color = view.Color;
            return true;
        }
        color = default;
        return false;
    }

    public bool IsRoomOccupied(string roomId)
    {
        if (_runState == null)
            return false;
        if (!_runState.Rooms.ContainsKey(roomId))
            return false;
        return _runState.Rooms[roomId].OccupantIds.Count > 0;
    }

    public string GetRoomOccupantId(string roomId)
    {
        if (_runState == null)
            return null;
        if (!_runState.Rooms.ContainsKey(roomId))
            return null;
        var occupants = _runState.Rooms[roomId].OccupantIds;
        return occupants.Count > 0 ? occupants[0] : null;
    }
}
