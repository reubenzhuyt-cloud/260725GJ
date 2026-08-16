using System;
using System.Collections.Generic;
using Hotel.Authoring.Items;
using Hotel.Runtime;
using UnityEngine;

public class InventoryDisplay : MonoBehaviour
{
    [Header("Configuration")]
    public RectTransform slotContainer;
    public GameObject slotTemplate;
    public List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

    [Header("Events")]
    public PhaseEnteredEvent onPhaseEntered;

    private readonly List<InventoryItemSlot> _createdSlots = new List<InventoryItemSlot>();
    private bool _runStateRestoredSubscribed;
    private bool _inventoryChangedSubscribed;
    private bool _warnedMissingTemplate;
    private bool _warnedMissingSlotComponent;

    private void OnEnable()
    {
        SubscribeRunStateRestored();
        SubscribeInventoryChanged();
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
        RefreshDisplay();
    }

    private void OnDisable()
    {
        UnsubscribeRunStateRestored();
        UnsubscribeInventoryChanged();
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
    }

    private void Start()
    {
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        ClearCreatedSlots();

        if (slotContainer == null || slotTemplate == null)
        {
            if (!_warnedMissingTemplate)
            {
                _warnedMissingTemplate = true;
                Debug.LogWarning("[InventoryDisplay] slotContainer or slotTemplate is not configured; inventory slots will not be shown.");
            }
            return;
        }

        GameRunState state = SettlementBridge.Instance != null
            ? SettlementBridge.Instance.RunState
            : null;
        if (state == null || state.Inventory == null || state.Inventory.Count == 0)
            return;

        var ownedIds = new List<string>(state.Inventory.Keys);
        ownedIds.Sort(StringComparer.Ordinal);

        for (int i = 0; i < ownedIds.Count; i++)
        {
            string itemId = ownedIds[i];
            if (string.IsNullOrEmpty(itemId))
                continue;
            int count = state.Inventory[itemId];
            if (count <= 0)
                continue;
            ItemDefinition definition = FindDefinition(itemId);
            if (definition == null)
                continue;

            GameObject instance = Instantiate(slotTemplate, slotContainer);
            if (instance == null)
                continue;
            instance.name = "ItemSlot_" + itemId;
            instance.SetActive(true);

            InventoryItemSlot slot = instance.GetComponent<InventoryItemSlot>();
            if (slot == null)
            {
                if (!_warnedMissingSlotComponent)
                {
                    _warnedMissingSlotComponent = true;
                    Debug.LogWarning("[InventoryDisplay] slotTemplate has no InventoryItemSlot component; added at runtime. Wire the iconImage/nameLabel/countLabel serialized references on the slot template for visuals.");
                }
                slot = instance.AddComponent<InventoryItemSlot>();
            }
            slot.Bind(definition, count);
            _createdSlots.Add(slot);
        }
    }

    private ItemDefinition FindDefinition(string itemId)
    {
        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            ItemDefinition definition = itemDefinitions[i];
            if (definition != null && definition.itemId == itemId)
                return definition;
        }
        return null;
    }

    private void ClearCreatedSlots()
    {
        for (int i = 0; i < _createdSlots.Count; i++)
        {
            InventoryItemSlot slot = _createdSlots[i];
            if (slot != null && slot.gameObject != null)
                Destroy(slot.gameObject);
        }
        _createdSlots.Clear();
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        if (data.phase == GamePhase.Dawn)
            RefreshDisplay();
    }

    private void OnRunStateRestored(GameRunState state)
    {
        if (state == null)
            return;
        RefreshDisplay();
    }

    private void OnInventoryChanged()
    {
        RefreshDisplay();
    }

    private void SubscribeRunStateRestored()
    {
        if (_runStateRestoredSubscribed)
            return;
        SettlementBridge.RunStateRestored += OnRunStateRestored;
        _runStateRestoredSubscribed = true;
    }

    private void UnsubscribeRunStateRestored()
    {
        if (!_runStateRestoredSubscribed)
            return;
        SettlementBridge.RunStateRestored -= OnRunStateRestored;
        _runStateRestoredSubscribed = false;
    }

    private void SubscribeInventoryChanged()
    {
        if (_inventoryChangedSubscribed)
            return;
        ItemUseManager.InventoryChanged += OnInventoryChanged;
        _inventoryChangedSubscribed = true;
    }

    private void UnsubscribeInventoryChanged()
    {
        if (!_inventoryChangedSubscribed)
            return;
        ItemUseManager.InventoryChanged -= OnInventoryChanged;
        _inventoryChangedSubscribed = false;
    }
}
