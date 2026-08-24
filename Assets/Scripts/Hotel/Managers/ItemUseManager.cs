using System;
using System.Collections.Generic;
using Hotel.Authoring.Items;
using Hotel.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;

public class ItemUseManager : MonoBehaviour
{
    public enum ItemUseState
    {
        Idle,
        AwaitingConfirm,
        AwaitingTarget
    }

    public static ItemUseManager Instance { get; private set; }

    public static event Action InventoryChanged;

    public const string FlashlightFlag = "item_flashlight";

    public static float FlashlightLossMultiplier { get; private set; } = 1f;

    [Header("Configuration")]
    public List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

    [Header("UI")]
    public ItemUseConfirmPanel confirmPanel;
    public ItemInfoPanel infoPanel;
    public TruthItemInfoPanel truthInfoPanel;
    public UIManager uiManager;

    public ItemUseState State => _state;
    public bool IsActive => _state != ItemUseState.Idle;
    public bool IsAwaitingTarget => _state == ItemUseState.AwaitingTarget;
    public ItemDefinition PendingItem => _pendingItem;
    public string PendingItemId => _pendingItemId;

    private ItemUseState _state = ItemUseState.Idle;
    private ItemDefinition _pendingItem;
    private string _pendingItemId;
    private EventSystem _eventSystem;
    private bool _eventSystemWasEnabled;
    private bool _eventSystemSuppressed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BindConfirmPanel();
        RefreshItemConfig();
    }

    private void Start()
    {
        BindConfirmPanel();
        RefreshItemConfig();
    }

    private void OnDestroy()
    {
        if (confirmPanel != null)
        {
            confirmPanel.Accepted -= OnConfirmAccepted;
            confirmPanel.Cancelled -= OnConfirmCancelled;
        }
        if (Instance == this)
            Instance = null;
    }

    private void BindConfirmPanel()
    {
        if (confirmPanel == null)
            return;
        confirmPanel.Accepted -= OnConfirmAccepted;
        confirmPanel.Cancelled -= OnConfirmCancelled;
        confirmPanel.Accepted += OnConfirmAccepted;
        confirmPanel.Cancelled += OnConfirmCancelled;
    }

    private void OnDisable()
    {
        ExitTargetSelection();
        if (_state == ItemUseState.Idle)
            return;
        _state = ItemUseState.Idle;
        _pendingItem = null;
        _pendingItemId = null;
        if (confirmPanel != null)
            confirmPanel.Hide();
        if (infoPanel != null)
            infoPanel.Hide();
    }

    public bool TryBeginUse(string itemId)
    {
        if (_state != ItemUseState.Idle)
            return false;
        if (string.IsNullOrEmpty(itemId))
            return false;

        ItemDefinition definition = FindDefinition(itemId);
        if (definition == null)
        {
            ShowMessage($"未找到物品「{itemId}」的配置");
            return false;
        }

        if (definition.IsTruthItem)
        {
            return false;
        }

        if (!IsValidDefinition(definition, out string configError))
        {
            ShowMessage(configError);
            return false;
        }

        GameRunState state = GetRunState();
        if (state == null)
        {
            ShowMessage("当前没有可用的运行状态");
            return false;
        }

        if (GetItemCount(state, itemId) <= 0)
        {
            ShowMessage($"「{definition.displayName}」库存不足");
            return false;
        }

        if (!string.IsNullOrEmpty(definition.effectFlag)
            && HasRunFlag(state, definition.effectFlag))
        {
            ShowMessage($"「{definition.displayName}」已在本局使用过");
            return false;
        }

        _pendingItem = definition;
        _pendingItemId = itemId;
        _state = ItemUseState.AwaitingConfirm;

        if (confirmPanel != null)
        {
            if (infoPanel != null)
                infoPanel.Hide();
            confirmPanel.Show(definition);
            return true;
        }

        OnConfirmAccepted();
        return true;
    }

    public void OnConfirmAccepted()
    {
        if (_state != ItemUseState.AwaitingConfirm)
            return;
        ItemDefinition definition = _pendingItem;
        if (definition == null)
        {
            ResetPending();
            return;
        }

        if (definition.targeting == ItemTargeting.None)
        {
            bool succeeded = TryCommitGlobalUse(definition, out string failureMessage);
            if (confirmPanel != null)
                confirmPanel.Hide();
            if (succeeded)
            {
                ShowMessage($"已使用「{definition.displayName}」");
                RaiseInventoryChanged();
            }
            else
            {
                ShowMessage(string.IsNullOrEmpty(failureMessage)
                    ? $"「{definition.displayName}」使用失败，请重试"
                    : failureMessage);
            }
            ResetPending();
            return;
        }

        EnterTargetSelection(definition);
    }

    public void OnConfirmCancelled()
    {
        if (_state != ItemUseState.AwaitingConfirm)
            return;
        if (confirmPanel != null)
            confirmPanel.Hide();
        ShowMessage("已取消使用");
        ResetPending();
    }

    public void CancelTargeting()
    {
        if (_state != ItemUseState.AwaitingTarget)
            return;
        ShowMessage("已取消选择目标");
        ResetPending();
    }

    public bool IsItemUsable(ItemDefinition definition)
    {
        if (definition == null)
            return false;
        if (definition.IsTruthItem || definition.effectType == ItemEffectType.None)
            return false;
        GameRunState state = GetRunState();
        if (state == null)
            return false;
        if (GetItemCount(state, definition.itemId) <= 0)
            return false;
        if (!string.IsNullOrEmpty(definition.effectFlag)
            && HasRunFlag(state, definition.effectFlag))
            return false;
        return true;
    }

    public ItemDefinition FindDefinition(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;
        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            ItemDefinition definition = itemDefinitions[i];
            if (definition != null && definition.itemId == itemId)
                return definition;
        }

#if UNITY_EDITOR
        ItemDefinition editorItem = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Data/Items/{itemId}.asset");
        if (editorItem != null)
            return editorItem;
#endif

        ItemDefinition resItem = Resources.Load<ItemDefinition>($"Items/{itemId}");
        if (resItem != null)
            return resItem;

        return null;
    }

    private void RefreshItemConfig()
    {
        float multiplier = 1f;
        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            ItemDefinition definition = itemDefinitions[i];
            if (definition == null)
                continue;
            if (definition.effectFlag != FlashlightFlag)
                continue;
            float candidate = 1f + definition.effectValue;
            if (definition.effectValue < 0f && candidate > 0f && candidate <= 1f)
            {
                multiplier = candidate;
                break;
            }
        }
        FlashlightLossMultiplier = multiplier;
    }

    private static bool IsValidDefinition(ItemDefinition definition, out string error)
    {
        error = null;
        if (definition == null)
        {
            error = "物品配置无效：缺少物品定义";
            return false;
        }

        switch (definition.targeting)
        {
            case ItemTargeting.None:
                if (definition.effectType != ItemEffectType.ErosionAll
                    && definition.effectType != ItemEffectType.NightLoss
                    && definition.effectType != ItemEffectType.ExtraClue)
                {
                    error = "物品配置无效：全局物品仅支持全房客侵蚀 / 夜间减损 / 额外线索效果";
                    return false;
                }
                break;
            case ItemTargeting.SingleTenant:
                if (definition.effectType != ItemEffectType.ErosionSingle)
                {
                    error = "物品配置无效：单体目标物品仅支持单体侵蚀效果";
                    return false;
                }
                break;
            case ItemTargeting.EngineerTenant:
                if (definition.effectType != ItemEffectType.EngineerBoost)
                {
                    error = "物品配置无效：工程师目标物品仅支持工程效率增益效果";
                    return false;
                }
                break;
            default:
                error = "物品配置无效：未知目标类型";
                return false;
        }

        switch (definition.effectType)
        {
            case ItemEffectType.ErosionSingle:
            case ItemEffectType.ErosionAll:
            case ItemEffectType.EngineerBoost:
                if (definition.effectValue == 0f)
                {
                    error = "物品配置无效：效果数值不能为零";
                    return false;
                }
                break;
            case ItemEffectType.NightLoss:
                if (!(definition.effectValue < 0f) || !(1f + definition.effectValue > 0f))
                {
                    error = "物品配置无效：夜间减损效果数值必须在 -1 到 0 之间";
                    return false;
                }
                break;
        }

        return true;
    }

    private void EnterTargetSelection(ItemDefinition definition)
    {
        _state = ItemUseState.AwaitingTarget;
        if (confirmPanel != null)
            confirmPanel.Hide();
        if (_eventSystem == null)
            _eventSystem = EventSystem.current;
        if (_eventSystem != null)
        {
            _eventSystemWasEnabled = _eventSystem.enabled;
            _eventSystem.enabled = false;
            _eventSystemSuppressed = true;
        }
        if (uiManager != null)
            uiManager.SetInventoryPanelVisible(false);
        ShowMessage($"请点击一名房客以使用「{definition.displayName}」（右键取消）");
    }

    private void ExitTargetSelection()
    {
        if (!_eventSystemSuppressed || _eventSystem == null)
            return;
        _eventSystem.enabled = _eventSystemWasEnabled;
        _eventSystemSuppressed = false;
    }

    private void Update()
    {
        if (_state != ItemUseState.AwaitingTarget)
            return;

        if (Input.GetMouseButtonDown(1))
        {
            CancelTargeting();
            return;
        }

        if (!Input.GetMouseButtonDown(0))
            return;

        ItemDefinition definition = _pendingItem;
        if (definition == null)
        {
            ResetPending();
            return;
        }

        if (!TryResolveTargetTenant(out string tenantId))
            return;

        if (!TryCommitTargetedUse(definition, tenantId, out string failureMessage))
        {
            ShowMessage(failureMessage);
            return;
        }

        ShowMessage($"已对目标房客使用「{definition.displayName}」");
        RaiseInventoryChanged();
        ResetPending();
    }

    private bool TryCommitGlobalUse(ItemDefinition definition, out string failureMessage)
    {
        failureMessage = null;
        if (!IsValidDefinition(definition, out failureMessage))
            return false;

        GameRunState state = GetRunState();
        if (state == null || SettlementBridge.Instance == null)
        {
            failureMessage = "当前没有可用的运行状态";
            return false;
        }
        StateReducer reducer = SettlementBridge.Instance.Reducer;
        if (reducer == null)
        {
            failureMessage = "状态变更器不可用";
            return false;
        }

        if (!string.IsNullOrEmpty(definition.effectFlag)
            && state.RunFlags != null
            && state.RunFlags.Contains(definition.effectFlag))
        {
            failureMessage = "该道具已在本局使用过";
            return false;
        }

        var set = AuthorizedChangeSet.Domain(
            state.RunId,
            state.StateVersion,
            "ItemUseManager",
            $"UseItem:{definition.itemId}");
        set.Add(new AdjustItemChange(definition.itemId, -1));

        switch (definition.effectType)
        {
            case ItemEffectType.ErosionAll:
                foreach (KeyValuePair<string, TenantRunState> pair in state.Tenants)
                {
                    if (pair.Value != null && !string.IsNullOrEmpty(pair.Key))
                        set.Add(new AdjustTenantErosionChange(pair.Key, definition.effectValue));
                }
                break;
        }

        if (!string.IsNullOrEmpty(definition.effectFlag))
            set.Add(new SetRunFlagChange(definition.effectFlag));

        CommitResult result = reducer.TryCommit(state, set);
        if (!result.Succeeded)
        {
            failureMessage = "使用失败，请重试";
            return false;
        }

        if (SettlementBridge.Instance != null)
        {
            PlayerLogManager.Record(state, new PlayerLogWriteDto(
                PlayerLogCategory.EffectSettlement,
                state.Day,
                state.Phase.Current,
                "物品使用",
                $"已使用「{definition.displayName}」",
                definition.itemId));
        }
        return true;
    }

    private bool TryCommitTargetedUse(ItemDefinition definition, string tenantId, out string failureMessage)
    {
        failureMessage = null;
        if (!IsValidDefinition(definition, out failureMessage))
            return false;

        GameRunState state = GetRunState();
        if (state == null || SettlementBridge.Instance == null)
        {
            failureMessage = "当前没有可用的运行状态";
            return false;
        }
        if (string.IsNullOrEmpty(tenantId) || !state.Tenants.ContainsKey(tenantId))
        {
            failureMessage = "目标房客不存在";
            return false;
        }
        StateReducer reducer = SettlementBridge.Instance.Reducer;
        if (reducer == null)
        {
            failureMessage = "状态变更器不可用";
            return false;
        }

        if (definition.targeting == ItemTargeting.EngineerTenant)
        {
            IReadOnlyList<TenantReviewCandidateSO> candidates = GetCandidates();
            TenantAbility ability = TenantAbilityResolver.ResolveAbility(tenantId, candidates);
            if (ability != TenantAbility.Engineer)
            {
                failureMessage = "该房客不是工程师，工具箱仅能对工程师使用";
                return false;
            }
        }

        if (definition.effectType == ItemEffectType.EngineerBoost
            && state.Buffs != null
            && state.Buffs.ContainsKey(BuildToolboxBuffId(tenantId)))
        {
            failureMessage = "该房客已使用过工具箱";
            return false;
        }

        var set = AuthorizedChangeSet.Domain(
            state.RunId,
            state.StateVersion,
            "ItemUseManager",
            $"UseItem:{definition.itemId}:{tenantId}");
        set.Add(new AdjustItemChange(definition.itemId, -1));

        switch (definition.effectType)
        {
            case ItemEffectType.ErosionSingle:
                set.Add(new AdjustTenantErosionChange(tenantId, definition.effectValue));
                break;
            case ItemEffectType.EngineerBoost:
            {
                var buff = new BuffRunState
                {
                    BuffId = BuildToolboxBuffId(tenantId),
                    SourceEventId = definition.itemId,
                    OwnerTenantId = tenantId,
                    Target = EffectTarget.OwnerTenant,
                    ErosionPerTick = 0f,
                    TickTiming = BuffTickTiming.Dawn,
                    RemainingTicks = -1,
                    StartDay = state.Day,
                    LastTickDay = state.Day,
                    TargetTenantIds = new List<string> { tenantId }
                };
                set.Add(new AddBuffChange(buff));
                break;
            }
        }

        CommitResult result = reducer.TryCommit(state, set);
        if (!result.Succeeded)
        {
            failureMessage = "使用失败，请重试";
            return false;
        }

        if (SettlementBridge.Instance != null)
        {
            PlayerLogManager.Record(state, new PlayerLogWriteDto(
                PlayerLogCategory.EffectSettlement,
                state.Day,
                state.Phase.Current,
                "物品使用",
                $"已对房客 {tenantId} 使用「{definition.displayName}」",
                definition.itemId,
                tenantId));
        }
        return true;
    }

    private static string BuildToolboxBuffId(string tenantId)
    {
        return "item_toolbox_" + tenantId;
    }

    private bool TryResolveTargetTenant(out string tenantId)
    {
        tenantId = null;
        EventSystem eventSystem = _eventSystem != null ? _eventSystem : EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointer = new PointerEventData(eventSystem)
        {
            position = Input.mousePosition
        };
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, results);
        if (results.Count == 0)
            return false;

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            if (hitObject == null)
                continue;

            RoomTenantAvatarSlot slot = hitObject.GetComponentInParent<RoomTenantAvatarSlot>();
            if (slot != null)
            {
                string occupantId = slot.GetOccupantId();
                if (!string.IsNullOrEmpty(occupantId))
                {
                    tenantId = occupantId;
                    return true;
                }
                continue;
            }

            TenantAvatarListItem item = hitObject.GetComponentInParent<TenantAvatarListItem>();
            if (item != null && !string.IsNullOrEmpty(item.TenantId))
            {
                tenantId = item.TenantId;
                return true;
            }
        }

        return false;
    }

    private void ResetPending()
    {
        ExitTargetSelection();
        _state = ItemUseState.Idle;
        _pendingItem = null;
        _pendingItemId = null;
        if (confirmPanel != null)
            confirmPanel.Hide();
        if (infoPanel != null)
            infoPanel.Hide();
        if (uiManager != null)
            uiManager.SetInventoryPanelVisible(true);
    }

    private void RaiseInventoryChanged()
    {
        InventoryChanged?.Invoke();
    }

    public static void NotifyInventoryChanged()
    {
        InventoryChanged?.Invoke();
    }

    private void ShowMessage(string message)
    {
        if (uiManager != null)
            uiManager.ShowNotice(message);
    }

    private static GameRunState GetRunState()
    {
        return SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null;
    }

    private static int GetItemCount(GameRunState state, string itemId)
    {
        if (state == null || state.Inventory == null || string.IsNullOrEmpty(itemId))
            return 0;
        return state.Inventory.TryGetValue(itemId, out int count) ? count : 0;
    }

    private static bool HasRunFlag(GameRunState state, string flag)
    {
        return state != null && state.RunFlags != null
            && !string.IsNullOrEmpty(flag) && state.RunFlags.Contains(flag);
    }

    private static IReadOnlyList<TenantReviewCandidateSO> GetCandidates()
    {
        return TenantReviewCoordinator.Instance != null
            ? TenantReviewCoordinator.Instance.candidates
            : null;
    }
}
