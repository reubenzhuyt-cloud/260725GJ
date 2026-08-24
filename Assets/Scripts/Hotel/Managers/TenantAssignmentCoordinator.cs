using System;
using System.Collections.Generic;
using Hotel.Audio;
using Hotel.Runtime;
using UnityEngine;

public class TenantAssignmentCoordinator : MonoBehaviour
{
    public static TenantAssignmentCoordinator Instance { get; private set; }

    public event Action AssignmentChanged;
    public event Action JobAssignmentChanged;
    public bool IsDragging { get; private set; }

    private static readonly HashSet<string> _warnedMissingRoomProperties = new HashSet<string>();

    [SerializeField] private UIManager uiManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _warnedMissingRoomProperties.Clear();
    }

    private StateReducer _reducer;
    private GameRunState _runState;
    private bool _runStateRestoredSubscribed;

    private readonly Dictionary<string, TenantAssignmentItemView> _displayLookup =
        new Dictionary<string, TenantAssignmentItemView>();

    private readonly List<string> _tenantOrder = new List<string>();

    private readonly List<TenantAssignmentItemView> _unassignedTenants =
        new List<TenantAssignmentItemView>();

    private readonly List<TenantAssignmentItemView> _panelTenants =
        new List<TenantAssignmentItemView>();

    private readonly List<TenantAssignmentItemView> _assignedBuffer =
        new List<TenantAssignmentItemView>();

    public IReadOnlyList<TenantAssignmentItemView> UnassignedTenants => _unassignedTenants;
    public IReadOnlyList<TenantAssignmentItemView> PanelTenants => _panelTenants;
    public int UnassignedCount => _unassignedTenants.Count;
    public bool IsAssignmentActive => HasUnassignedTenants || IsDragging;
    public int AvailableCapacity
    {
        get
        {
            if (_runState == null)
                return 0;
            int totalCapacity = 0;
            foreach (string roomId in _runState.Rooms.Keys)
            {
                TryGetRoomCapacity(roomId, out int capacity);
                totalCapacity += capacity;
            }
            int assignedCount = 0;
            foreach (var pair in _runState.Tenants)
            {
                if (pair.Value == null)
                    continue;
                if (string.IsNullOrEmpty(pair.Value.RoomId))
                    continue;
                assignedCount++;
            }
            return Mathf.Max(0, totalCapacity - assignedCount);
        }
    }
    public bool HasUnassignedTenants => UnassignedCount > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        TryBindRuntimeState();
    }

    private void OnEnable()
    {
        if (_runStateRestoredSubscribed)
            return;

        SettlementBridge.RunStateRestored += OnRunStateRestored;
        _runStateRestoredSubscribed = true;
    }

    private void OnDisable()
    {
        if (!_runStateRestoredSubscribed)
            return;

        SettlementBridge.RunStateRestored -= OnRunStateRestored;
        _runStateRestoredSubscribed = false;
    }

    private void Start()
    {
        TryBindRuntimeState();

        if (_runState == null)
        {
            Debug.LogError("[TenantAssignmentCoordinator] SettlementBridge.Instance is null!");
            return;
        }

        RebuildLoadedTenantViews();
        RebuildUnassigned();
        RoomTenantAvatarSlot.RefreshAll();
        AssignmentChanged?.Invoke();
    }

    private void TryBindRuntimeState()
    {
        if (_runState != null)
            return;
        if (SettlementBridge.Instance == null)
            return;

        _reducer = SettlementBridge.Instance.Reducer;
        _runState = SettlementBridge.Instance.RunState;

        EnsureRooms();

        RebuildLoadedTenantViews();
        RebuildUnassigned();
    }

    private void OnRunStateRestored(GameRunState state)
    {
        if (state == null)
            return;

        if (SettlementBridge.Instance != null)
            _reducer = SettlementBridge.Instance.Reducer;

        _runState = state;

        RebuildLoadedTenantViews();
        RebuildUnassigned();
        RoomTenantAvatarSlot.RefreshAll();
        AssignmentChanged?.Invoke();
    }

    private void EnsureRooms()
    {
        for (int i = 1; i <= 10; i++)
        {
            string roomId = string.Format("room_{0:D2}", i);
            if (_runState.Rooms.ContainsKey(roomId)) continue;
            _runState.Rooms[roomId] = new RoomRunState
            {
                RoomId = roomId,
                DefinitionId = roomId
            };
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void AddTenant(string tenantId, string displayName, Color color, string avatarKey)
    {
        _runState.Tenants[tenantId] = new TenantRunState
        {
            TenantId = tenantId,
            DefinitionId = tenantId,
            AvatarKey = avatarKey
        };
        _displayLookup[tenantId] = new TenantAssignmentItemView(tenantId, displayName, color, avatarKey);
        _tenantOrder.Add(tenantId);
    }

    private void RebuildLoadedTenantViews()
    {
        _displayLookup.Clear();
        _tenantOrder.Clear();

        foreach (var pair in _runState.Tenants)
        {
            string displayName = pair.Key;
            Color color = Color.white;
            string avatarKey = null;
            if (TenantReviewCoordinator.Instance != null)
                TenantReviewCoordinator.Instance.TryGetCandidatePresentation(pair.Key, out displayName, out color, out avatarKey);

            if (!string.IsNullOrEmpty(pair.Value.AvatarKey))
                avatarKey = pair.Value.AvatarKey;

            _displayLookup[pair.Key] = new TenantAssignmentItemView(pair.Key, displayName, color, avatarKey);
            _tenantOrder.Add(pair.Key);
        }

        _tenantOrder.Sort(StringComparer.Ordinal);
    }

    public void RegisterTenant(string tenantId, string displayName, Color color, string avatarKey)
    {
        if (_displayLookup.ContainsKey(tenantId)) return;

        if (_runState == null)
        {
            TryBindRuntimeState();
        }

        if (_runState != null && !_runState.Tenants.ContainsKey(tenantId))
        {
            _runState.Tenants[tenantId] = new TenantRunState
            {
                TenantId = tenantId,
                DefinitionId = tenantId,
                AvatarKey = avatarKey
            };
        }

        _displayLookup[tenantId] = new TenantAssignmentItemView(tenantId, displayName, color, avatarKey);
        _tenantOrder.Add(tenantId);
        _tenantOrder.Sort(StringComparer.Ordinal);
        RebuildUnassigned();
        RoomTenantAvatarSlot.RefreshAll();
        TenantAssignmentPanel.RefreshAll();
        AssignmentChanged?.Invoke();
    }

    private void RebuildUnassigned()
    {
        _unassignedTenants.Clear();
        _panelTenants.Clear();
        _assignedBuffer.Clear();

        for (int i = 0; i < _tenantOrder.Count; i++)
        {
            string id = _tenantOrder[i];
            if (!_runState.Tenants.TryGetValue(id, out TenantRunState tenant) || tenant == null)
                continue;

            if (!_displayLookup.TryGetValue(id, out TenantAssignmentItemView baseView))
                continue;

            bool isAssigned = !string.IsNullOrEmpty(tenant.RoomId);
            TenantAssignmentItemView view = new TenantAssignmentItemView(
                baseView.TenantId,
                baseView.DisplayName,
                baseView.Color,
                baseView.AvatarKey,
                isAssigned);

            if (isAssigned)
            {
                _assignedBuffer.Add(view);
            }
            else
            {
                _unassignedTenants.Add(view);
                _panelTenants.Add(view);
            }
        }

        _panelTenants.AddRange(_assignedBuffer);
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

        if (!CanAssign(roomId))
            return false;

        if (!string.IsNullOrEmpty(_runState.Tenants[tenantId].RoomId))
            return false;

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantAssignmentCoordinator",
            "AssignRoom");
        changeSet.Add(new AssignRoomChange(tenantId, roomId));

        bool needsCheckIn = _runState.Tenants.TryGetValue(tenantId, out TenantRunState targetTenant)
            && targetTenant != null && targetTenant.CheckInDay <= 0;
        if (needsCheckIn)
        {
            changeSet.Add(new SetTenantCheckInChange(tenantId, _runState.Day));
        }

        CommitResult result = _reducer.TryCommit(_runState, changeSet);

        if (result.Succeeded)
        {
            AudioManager.Instance?.PlayUISound(UISoundType.Click);
            RebuildUnassigned();
            TenantAssignmentPanel.RefreshAll();
            AssignmentChanged?.Invoke();
            RoomTenantAvatarSlot.RefreshAll();

            string displayName = tenantId;
            if (_displayLookup.TryGetValue(tenantId, out TenantAssignmentItemView view))
                displayName = view.DisplayName;

            uiManager?.ShowNotice($"{displayName} 入住 {roomId}");

            PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
                PlayerLogCategory.RoomAssignment,
                _runState.Day,
                _runState.Phase.Current,
                "房间分配",
                $"{displayName} → {roomId}",
                tenantId,
                tenantId));

            TenantLogManager.Record(_runState, new TenantLogWriteDto(
                tenantId,
                TenantLogCategory.RoomAssignment,
                _runState.Day,
                _runState.Phase.Current,
                $"{displayName} → {roomId}",
                tenantId));
        }

        return result.Succeeded;
    }

    public bool TryMoveToEmptyRoom(string tenantId, string targetRoomId)
    {
        if (_runState == null)
            return false;

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(targetRoomId))
            return false;

        if (!_runState.Tenants.ContainsKey(tenantId) || !_runState.Rooms.ContainsKey(targetRoomId))
            return false;

        string currentRoomId = _runState.Tenants[tenantId].RoomId;
        if (string.IsNullOrEmpty(currentRoomId))
            return false;

        if (currentRoomId == targetRoomId)
            return false;

        if (!CanAssign(targetRoomId))
            return false;

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantAssignmentCoordinator",
            "MoveRoom");
        changeSet.Add(new AssignRoomChange(tenantId, targetRoomId));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);

        if (result.Succeeded)
        {
            AudioManager.Instance?.PlayUISound(UISoundType.Click);
            RebuildUnassigned();
            TenantAssignmentPanel.RefreshAll();
            AssignmentChanged?.Invoke();
            RoomTenantAvatarSlot.RefreshAll();

            string displayName = tenantId;
            if (_displayLookup.TryGetValue(tenantId, out TenantAssignmentItemView view))
                displayName = view.DisplayName;

            uiManager?.ShowNotice($"{displayName} 从 {currentRoomId} 搬至 {targetRoomId}");

            PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
                PlayerLogCategory.RoomAssignment,
                _runState.Day,
                _runState.Phase.Current,
                "房间移动",
                $"{displayName}：{currentRoomId} → {targetRoomId}",
                tenantId,
                tenantId));

            TenantLogManager.Record(_runState, new TenantLogWriteDto(
                tenantId,
                TenantLogCategory.RoomMove,
                _runState.Day,
                _runState.Phase.Current,
                $"{displayName}：{currentRoomId} → {targetRoomId}",
                tenantId));
        }

        return result.Succeeded;
    }

    public bool TryAssignJob(string tenantId, string jobId)
    {
        if (_runState == null || _reducer == null)
            return false;
        if (string.IsNullOrEmpty(tenantId) || !_runState.Tenants.ContainsKey(tenantId))
            return false;
        if (!JobCatalog.IsValid(jobId))
            return false;

        TenantRunState tenant = _runState.Tenants[tenantId];
        jobId ??= string.Empty;
        if (string.Equals(tenant.JobId ?? string.Empty, jobId, StringComparison.Ordinal))
            return true;

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantAssignmentCoordinator",
            "AssignJob");
        changeSet.Add(new AssignJobChange(tenantId, jobId));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);
        if (!result.Succeeded)
            return false;

        JobAssignmentChanged?.Invoke();

        string displayName = tenantId;
        if (_displayLookup.TryGetValue(tenantId, out TenantAssignmentItemView view))
            displayName = view.DisplayName;
        string jobName = JobCatalog.GetDisplayName(jobId);

        PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
            PlayerLogCategory.WorkAssignment,
            _runState.Day,
            _runState.Phase.Current,
            "工作安排",
            $"{displayName} → {jobName}",
            jobId,
            tenantId));

        TenantLogManager.Record(_runState, new TenantLogWriteDto(
            tenantId,
            TenantLogCategory.WorkAssignment,
            _runState.Day,
            _runState.Phase.Current,
            $"工作安排：{jobName}",
            jobId));

        return true;
    }

    public bool TryEvict(string tenantId)
    {
        if (_runState == null || _reducer == null)
            return false;
        if (string.IsNullOrEmpty(tenantId) || !_runState.Tenants.ContainsKey(tenantId))
            return false;

        string displayName = tenantId;
        if (_displayLookup.TryGetValue(tenantId, out TenantAssignmentItemView view))
            displayName = view.DisplayName;

        var changeSet = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "TenantAssignmentCoordinator",
            "EvictTenant");
        changeSet.Add(new EvictTenantChange(tenantId));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);
        if (!result.Succeeded)
            return false;

        _displayLookup.Remove(tenantId);
        _tenantOrder.Remove(tenantId);
        RebuildUnassigned();
        RoomTenantAvatarSlot.RefreshAll();
        TenantAssignmentPanel.RefreshAll();
        AssignmentChanged?.Invoke();

        PlayerLogManager.Record(_runState, new PlayerLogWriteDto(
            PlayerLogCategory.TenantEvict,
            _runState.Day,
            _runState.Phase.Current,
            "驱逐租客",
            $"{displayName} 离开了旅馆",
            tenantId,
            tenantId));

        return true;
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

    public bool TryGetTenantAvatar(string tenantId, out Sprite avatar)
    {
        avatar = null;
        if (_displayLookup.TryGetValue(tenantId, out TenantAssignmentItemView view)
            && !string.IsNullOrEmpty(view.AvatarKey))
        {
            return TenantAvatarResolver.TryResolve(view.AvatarKey, out avatar);
        }
        return false;
    }

    public bool TryGetTenantDisplayName(string tenantId, out string displayName)
    {
        if (!string.IsNullOrEmpty(tenantId) && _displayLookup.TryGetValue(tenantId, out TenantAssignmentItemView view))
        {
            displayName = view.DisplayName;
            return true;
        }
        displayName = tenantId;
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

    public bool TryGetRoomCapacity(string roomId, out int capacity)
    {
        if (RoomAvatarProperty.TryGetCapacity(roomId, out capacity))
            return true;

        capacity = 1;
        if (!string.IsNullOrEmpty(roomId) && _warnedMissingRoomProperties.Add(roomId))
        {
            Debug.LogWarning($"[TenantAssignmentCoordinator] RoomAvatarProperty missing or invalid for room '{roomId}'; falling back to capacity 1 (single occupancy).", this);
        }
        return false;
    }

    public bool CanAssign(string roomId)
    {
        if (_runState == null)
            return false;
        if (!_runState.Rooms.ContainsKey(roomId))
            return false;
        TryGetRoomCapacity(roomId, out int capacity);
        return _runState.Rooms[roomId].OccupantIds.Count < capacity;
    }

    public IReadOnlyList<string> GetRoomOccupantIds(string roomId)
    {
        if (_runState == null)
            return Array.Empty<string>();
        if (!_runState.Rooms.TryGetValue(roomId, out RoomRunState room))
            return Array.Empty<string>();
        return new List<string>(room.OccupantIds);
    }

    public int GetRoomOccupantCount(string roomId)
    {
        if (_runState == null)
            return 0;
        if (!_runState.Rooms.TryGetValue(roomId, out RoomRunState room))
            return 0;
        return room.OccupantIds.Count;
    }
}
