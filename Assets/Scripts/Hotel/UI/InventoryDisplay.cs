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
    private readonly List<GameObject> _createdRoots = new List<GameObject>();
    private bool _runStateRestoredSubscribed;
    private bool _inventoryChangedSubscribed;
    private bool _warnedMissingTemplate;

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

        Transform panelTemplate = slotTemplate.transform.Find("ItemPanel");
        if (panelTemplate == null)
        {
            if (!_warnedMissingTemplate)
            {
                _warnedMissingTemplate = true;
                Debug.LogWarning("[InventoryDisplay] slotTemplate has no 'ItemPanel' child; inventory slots will not be shown.");
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

            GameObject instance = Instantiate(panelTemplate.gameObject, slotContainer);
            if (instance == null)
                continue;
            instance.name = "ItemSlot_" + itemId;
            instance.SetActive(true);

            InventoryItemSlot slot = instance.GetComponent<InventoryItemSlot>();
            if (slot == null)
            {
                slot = instance.AddComponent<InventoryItemSlot>();
                Transform iconTf = instance.transform.Find("Icon");
                Transform nameTf = instance.transform.Find("NameLabel");
                Transform countTf = instance.transform.Find("CountLabel");
                slot.iconImage = iconTf != null ? iconTf.GetComponent<UnityEngine.UI.Image>() : null;
                slot.nameLabel = nameTf != null ? nameTf.GetComponent<TMPro.TextMeshProUGUI>() : null;
                slot.countLabel = countTf != null ? countTf.GetComponent<TMPro.TextMeshProUGUI>() : null;
            }
            slot.Bind(definition, count);
            _createdSlots.Add(slot);
            _createdRoots.Add(instance);
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
        for (int i = 0; i < _createdRoots.Count; i++)
        {
            GameObject root = _createdRoots[i];
            if (root != null)
                Destroy(root);
        }
        _createdSlots.Clear();
        _createdRoots.Clear();
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
