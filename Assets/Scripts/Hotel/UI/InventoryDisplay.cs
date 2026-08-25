using System;
using System.Collections.Generic;
using Hotel.Authoring.Items;
using Hotel.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;

public class InventoryDisplay : MonoBehaviour
{
    [Header("Configuration")]
    public RectTransform slotContainer;
    public InventoryItemSlot slotPrefab;
    public List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

    [Header("Events")]
    public PhaseEnteredEvent onPhaseEntered;

    private readonly List<InventoryItemSlot> _slotPool = new List<InventoryItemSlot>();
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    private bool _runStateRestoredSubscribed;
    private bool _inventoryChangedSubscribed;
    private bool _warnedMissingPrefab;
    private bool _hasStarted;
    private int _openedFrame = -1;

    private static readonly Dictionary<string, ItemDefinition> ResItemCache = new Dictionary<string, ItemDefinition>();
    private static bool _resItemsLoaded;

    private void OnEnable()
    {
        _openedFrame = Time.frameCount;
        SubscribeRunStateRestored();
        SubscribeInventoryChanged();
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
        if (_hasStarted)
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
        _hasStarted = true;
        RefreshDisplay();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (Time.frameCount == _openedFrame)
                return;

            if (ItemUseManager.Instance != null && ItemUseManager.Instance.IsAwaitingTarget)
                return;

            if (!IsPointerInsideInventoryOrPopups())
            {
                CloseInventory();
            }
        }
    }

    private void CloseInventory()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.SetInventoryPanelVisible(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private bool IsPointerInsideInventoryOrPopups()
    {
        EventSystem current = EventSystem.current;
        if (current != null)
        {
            var pointerData = new PointerEventData(current)
            {
                position = Input.mousePosition
            };
            _raycastResults.Clear();
            current.RaycastAll(pointerData, _raycastResults);

            for (int i = 0; i < _raycastResults.Count; i++)
            {
                GameObject hit = _raycastResults[i].gameObject;
                if (hit == null)
                    continue;

                if (hit == gameObject || hit.transform.IsChildOf(transform))
                    return true;

                if (ItemUseManager.Instance != null)
                {
                    if (ItemUseManager.Instance.confirmPanel != null && ItemUseManager.Instance.confirmPanel.IsShowing)
                    {
                        if (hit.transform.IsChildOf(ItemUseManager.Instance.confirmPanel.transform))
                            return true;
                    }
                    if (ItemUseManager.Instance.truthInfoPanel != null && ItemUseManager.Instance.truthInfoPanel.gameObject.activeSelf)
                    {
                        if (hit.transform.IsChildOf(ItemUseManager.Instance.truthInfoPanel.transform))
                            return true;
                    }
                    if (ItemUseManager.Instance.infoPanel != null && ItemUseManager.Instance.infoPanel.gameObject.activeSelf)
                    {
                        if (hit.transform.IsChildOf(ItemUseManager.Instance.infoPanel.transform))
                            return true;
                    }
                }
            }
        }

        RectTransform selfRect = GetComponent<RectTransform>();
        if (selfRect != null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = canvas.worldCamera;

            if (RectTransformUtility.RectangleContainsScreenPoint(selfRect, Input.mousePosition, eventCamera))
                return true;
        }

        return false;
    }

    public void RefreshDisplay()
    {
        DeactivateAllSlots();

        if (slotContainer == null || slotPrefab == null)
        {
            if (!_warnedMissingPrefab)
            {
                _warnedMissingPrefab = true;
                Debug.LogWarning("[InventoryDisplay] slotContainer or slotPrefab is not configured; inventory slots will not be shown.");
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

        int activeCount = 0;
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

            InventoryItemSlot slot = GetOrCreateSlot(activeCount);
            if (slot == null)
                continue;

            slot.name = "ItemSlot_" + itemId;
            slot.Bind(definition, count);
            slot.gameObject.SetActive(true);
            activeCount++;
        }
    }

    private InventoryItemSlot GetOrCreateSlot(int index)
    {
        while (_slotPool.Count <= index)
        {
            InventoryItemSlot newSlot = Instantiate(slotPrefab, slotContainer);
            if (newSlot == null)
                return null;
            newSlot.gameObject.SetActive(false);
            _slotPool.Add(newSlot);
        }

        InventoryItemSlot slot = _slotPool[index];
        if (slot == null)
        {
            slot = Instantiate(slotPrefab, slotContainer);
            _slotPool[index] = slot;
        }
        return slot;
    }

    private void DeactivateAllSlots()
    {
        for (int i = 0; i < _slotPool.Count; i++)
        {
            InventoryItemSlot slot = _slotPool[i];
            if (slot != null && slot.gameObject != null)
                slot.gameObject.SetActive(false);
        }
    }

    private ItemDefinition FindDefinition(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return null;

        for (int i = 0; i < itemDefinitions.Count; i++)
        {
            ItemDefinition definition = itemDefinitions[i];
            if (definition != null && definition.itemId == itemId)
                return definition;
        }

        if (ItemUseManager.Instance != null)
        {
            ItemDefinition definition = ItemUseManager.Instance.FindDefinition(itemId);
            if (definition != null)
                return definition;
        }

        EnsureResItemCacheLoaded();
        if (ResItemCache.TryGetValue(itemId, out ItemDefinition cachedDef))
            return cachedDef;

        ItemDefinition resItem = Resources.Load<ItemDefinition>($"Items/{itemId}");
        if (resItem != null)
        {
            ResItemCache[itemId] = resItem;
            return resItem;
        }

        return null;
    }

    private static void EnsureResItemCacheLoaded()
    {
        if (_resItemsLoaded)
            return;

        ItemDefinition[] allResItems = Resources.LoadAll<ItemDefinition>("Items");
        if (allResItems != null)
        {
            for (int i = 0; i < allResItems.Length; i++)
            {
                ItemDefinition item = allResItems[i];
                if (item != null && !string.IsNullOrEmpty(item.itemId))
                {
                    ResItemCache[item.itemId] = item;
                }
            }
        }
        _resItemsLoaded = true;
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
