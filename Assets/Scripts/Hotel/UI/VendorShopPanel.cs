using System;
using System.Collections.Generic;
using Hotel.Authoring.Items;
using Hotel.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class VendorShopPanel : MonoBehaviour
{
    [Header("Layout")]
    public GameObject root;
    public Transform itemGrid;
    public GameObject itemSlotTemplate;
    public Button closeButton;

    [Header("Data")]
    public List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

    [Header("Dependencies")]
    public ItemUseConfirmPanel confirmPanel;
    public UIManager uiManager;
    public ResourceAdjustedEvent onResourceAdjusted;

    private readonly List<VendorShopItemSlot> _slots = new List<VendorShopItemSlot>();
    private readonly List<string> _purchasedItemNames = new List<string>();
    private int _totalSpent;
    private EventProcessedEvent _onEventProcessed;
    private string _eventId;
    private string _optionId;
    private bool _active;
    private bool _confirmSubscribed;
    private VendorShopItemSlot _pendingSlot;
    private ItemDefinition _pendingDefinition;

    private bool _initialized;

    public bool IsActive => _active;

    private void Awake()
    {
        InitializeIfNeeded();
        if (root != null && root != gameObject)
            root.SetActive(false);
        else if (root == null)
            gameObject.SetActive(false);
    }

    private void InitializeIfNeeded()
    {
        if (_initialized)
            return;
        _initialized = true;

        if (itemSlotTemplate != null)
            itemSlotTemplate.SetActive(false);
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        UnsubscribeConfirm();
    }

    public void Show(string eventId, string optionId, EventProcessedEvent processedEvent)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        InitializeIfNeeded();

        if (_active)
            ForceClose();

        _eventId = eventId;
        _optionId = optionId;
        _onEventProcessed = processedEvent;
        _active = true;
        _purchasedItemNames.Clear();
        _totalSpent = 0;

        ClearSlots();
        PopulateShop();

        if (root != null && !root.activeSelf)
            root.SetActive(true);
    }

    private void ForceClose()
    {
        _active = false;
        _pendingSlot = null;
        _pendingDefinition = null;
        if (confirmPanel != null)
            confirmPanel.Hide();
        UnsubscribeConfirm();
        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
        ClearSlots();
    }

    private void PopulateShop()
    {
        List<ItemDefinition> pool = GetMerchantItems();
        int count = Mathf.Min(4, pool.Count);

        GameRunState state = SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null;
        int seed = state != null ? state.Seed + state.Day : Environment.TickCount;
        var rng = new System.Random(seed);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = 0; i < count; i++)
            CreateSlot(pool[i]);

        UpdateAffordability();
    }

    private List<ItemDefinition> GetMerchantItems()
    {
        var result = new List<ItemDefinition>();
        if (itemDefinitions == null)
            return result;
        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            ItemDefinition def = itemDefinitions[i];
            if (def == null)
                continue;
            if (def.acquisition == ItemAcquisition.Merchant || def.acquisition == ItemAcquisition.MerchantAndEngineerEvent)
                result.Add(def);
        }
        return result;
    }

    private void CreateSlot(ItemDefinition definition)
    {
        if (itemSlotTemplate == null || itemGrid == null)
            return;

        GameObject instance = Instantiate(itemSlotTemplate, itemGrid);
        instance.SetActive(true);

        VendorShopItemSlot slot = instance.GetComponent<VendorShopItemSlot>();
        if (slot == null)
            slot = instance.AddComponent<VendorShopItemSlot>();

        slot.Bind(definition);

        if (slot.buyButton != null)
        {
            ItemDefinition captured = definition;
            VendorShopItemSlot capturedSlot = slot;
            slot.buyButton.onClick.AddListener(() => OnBuyClicked(capturedSlot, captured));
        }

        _slots.Add(slot);
    }

    private void OnBuyClicked(VendorShopItemSlot slot, ItemDefinition definition)
    {
        if (slot == null || slot.IsSold || definition == null)
            return;

        GameRunState state = SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null;
        if (state == null)
            return;

        int currency = GetCurrency(state);
        if (currency < definition.merchantPrice)
            return;

        if (confirmPanel == null)
        {
            CommitPurchase(definition, slot);
            return;
        }

        _pendingSlot = slot;
        _pendingDefinition = definition;
        SubscribeConfirm();
        confirmPanel.ShowPurchase(definition.displayName, definition.merchantPrice);
    }

    private void SubscribeConfirm()
    {
        if (_confirmSubscribed || confirmPanel == null)
            return;
        confirmPanel.Accepted += OnConfirmAccepted;
        confirmPanel.Cancelled += OnConfirmCancelled;
        _confirmSubscribed = true;
    }

    private void UnsubscribeConfirm()
    {
        if (!_confirmSubscribed || confirmPanel == null)
            return;
        confirmPanel.Accepted -= OnConfirmAccepted;
        confirmPanel.Cancelled -= OnConfirmCancelled;
        _confirmSubscribed = false;
    }

    private void OnConfirmAccepted()
    {
        if (!_active)
            return;

        VendorShopItemSlot slot = _pendingSlot;
        ItemDefinition definition = _pendingDefinition;
        _pendingSlot = null;
        _pendingDefinition = null;

        if (confirmPanel != null)
            confirmPanel.Hide();
        UnsubscribeConfirm();

        if (slot == null || slot.IsSold || definition == null)
            return;

        GameRunState state = SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null;
        if (state == null)
            return;

        if (GetCurrency(state) < definition.merchantPrice)
            return;

        CommitPurchase(definition, slot);
    }

    private void OnConfirmCancelled()
    {
        if (!_active)
            return;

        _pendingSlot = null;
        _pendingDefinition = null;

        if (confirmPanel != null)
            confirmPanel.Hide();
        UnsubscribeConfirm();
    }

    private void CommitPurchase(ItemDefinition definition, VendorShopItemSlot slot)
    {
        GameRunState state = SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null;
        StateReducer reducer = SettlementBridge.Instance != null ? SettlementBridge.Instance.Reducer : null;
        if (state == null || reducer == null)
            return;

        if (slot.IsSold)
            return;
        if (GetCurrency(state) < definition.merchantPrice)
            return;

        int currentCount = GetItemCount(state, definition.itemId);
        if (definition.maxStack > 0 && currentCount >= definition.maxStack)
        {
            slot.SetSoldOut(true);
            UpdateAffordability();
            ShowNotice($"已拥有「{definition.displayName}」，无法再次购买");
            return;
        }

        var set = AuthorizedChangeSet.Domain(
            state.RunId,
            state.StateVersion,
            "VendorShopPanel",
            $"Purchase:{definition.itemId}");
        set.Add(new AdjustResourceChange("currency", -definition.merchantPrice));
        set.Add(new AdjustItemChange(definition.itemId, 1));

        CommitResult result = reducer.TryCommit(state, set);
        if (!result.Succeeded)
        {
            Debug.LogWarning($"[VendorShopPanel] Purchase commit failed for '{definition.itemId}' (stateVersion={state.StateVersion}).");
            UpdateAffordability();
            ShowNotice($"购买「{definition.displayName}」失败，请重试");
            return;
        }

        slot.SetSoldOut(true);
        UpdateAffordability();
        ItemUseManager.NotifyInventoryChanged();

        int newCurrency = GetCurrency(state);
        if (onResourceAdjusted != null)
        {
            onResourceAdjusted.Raise(new ResourceAdjustedData
            {
                resourceId = "currency",
                delta = -definition.merchantPrice,
                newAmount = newCurrency
            });
        }

        _purchasedItemNames.Add(definition.displayName);
        _totalSpent += definition.merchantPrice;

        ShowNotice($"已购买「{definition.displayName}」，花费 {definition.merchantPrice} 货币");
    }

    private void UpdateAffordability()
    {
        GameRunState state = SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null;
        int currency = state != null ? GetCurrency(state) : 0;

        for (int i = 0; i < _slots.Count; i++)
        {
            VendorShopItemSlot slot = _slots[i];
            if (slot == null || slot.IsSold)
                continue;
            ItemDefinition def = slot.Definition;
            if (def == null)
            {
                slot.SetInteractable(false);
                continue;
            }
            int owned = state != null ? GetItemCount(state, def.itemId) : 0;
            bool atCapacity = def.maxStack > 0 && owned >= def.maxStack;
            if (atCapacity)
            {
                slot.SetSoldOut(true);
                continue;
            }
            slot.SetInteractable(currency >= def.merchantPrice);
        }
    }

    private void OnCloseClicked()
    {
        Close();
    }

    private void Close()
    {
        if (!_active)
            return;

        _active = false;

        _pendingSlot = null;
        _pendingDefinition = null;
        if (confirmPanel != null)
            confirmPanel.Hide();
        UnsubscribeConfirm();

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
        ClearSlots();

        if (_onEventProcessed != null && !string.IsNullOrEmpty(_eventId))
        {
            string eventId = _eventId;
            string optionId = _optionId;
            _eventId = null;
            _optionId = null;

            string summaryNotice;
            if (_purchasedItemNames.Count == 0)
            {
                summaryNotice = "交易结束";
            }
            else
            {
                summaryNotice = $"交易结束：购买了 {string.Join("、", _purchasedItemNames)}，共花费 {_totalSpent} 货币";
            }

            _purchasedItemNames.Clear();
            _totalSpent = 0;

            _onEventProcessed.RaiseProcessed(new EventProcessedData
            {
                eventId = eventId,
                optionId = optionId,
                effects = null,
                noticeText = summaryNotice
            });
            _onEventProcessed = null;
        }
        else
        {
            _eventId = null;
            _optionId = null;
            _onEventProcessed = null;
            _purchasedItemNames.Clear();
            _totalSpent = 0;
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null && _slots[i].gameObject != null)
                Destroy(_slots[i].gameObject);
        }
        _slots.Clear();
    }

    private static int GetCurrency(GameRunState state)
    {
        if (state == null || state.Resources == null)
            return 0;
        return state.Resources.TryGetValue("currency", out ResourceRunState res) ? res.Amount : 0;
    }

    private static int GetItemCount(GameRunState state, string itemId)
    {
        if (state == null || state.Inventory == null || string.IsNullOrEmpty(itemId))
            return 0;
        return state.Inventory.TryGetValue(itemId, out int count) ? count : 0;
    }

    private void ShowNotice(string message)
    {
        if (uiManager != null)
            uiManager.ShowNotice(message);
        else
            Debug.LogWarning("[VendorShopPanel] " + message);
    }
}
