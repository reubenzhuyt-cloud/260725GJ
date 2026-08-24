using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Hotel.Runtime;

public class InfoPanelResourceDisplay : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI foodAmountText;
    public TextMeshProUGUI currencyAmountText;
    public Image foodIcon;
    public Image currencyIcon;

    [Header("Event Channels")]
    public ResourceAdjustedEvent onResourceAdjusted;
    public PhaseEnteredEvent onPhaseEntered;

    private bool _runStateRestoredSubscribed;

    private void Awake()
    {
        if (foodAmountText == null)
        {
            Transform food = transform.Find("FoodPanel/FoodAmountText");
            if (food != null) foodAmountText = food.GetComponent<TextMeshProUGUI>();
            if (foodAmountText == null)
            {
                var all = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in all)
                {
                    if (t.name.IndexOf("Food", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foodAmountText = t;
                        break;
                    }
                }
            }
        }
        if (currencyAmountText == null)
        {
            Transform coins = transform.Find("CoinsPanel/CoinsAmountText");
            if (coins != null) currencyAmountText = coins.GetComponent<TextMeshProUGUI>();
            if (currencyAmountText == null)
            {
                var all = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in all)
                {
                    if (t.name.IndexOf("Coin", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.name.IndexOf("Currency", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        currencyAmountText = t;
                        break;
                    }
                }
            }
        }

        if (onResourceAdjusted == null && SettlementBridge.Instance != null)
            onResourceAdjusted = SettlementBridge.Instance.onResourceAdjusted;
    }

    private void OnEnable()
    {
        if (onResourceAdjusted == null && SettlementBridge.Instance != null)
            onResourceAdjusted = SettlementBridge.Instance.onResourceAdjusted;

        if (onResourceAdjusted != null)
            onResourceAdjusted.Register(OnResourceAdjusted);
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
        SubscribeRunStateRestored();
    }

    private void OnDisable()
    {
        if (onResourceAdjusted != null)
            onResourceAdjusted.Unregister(OnResourceAdjusted);
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
        UnsubscribeRunStateRestored();
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

    private void Start()
    {
        RefreshDisplay();
    }

    private void OnResourceAdjusted(ResourceAdjustedData data)
    {
        if (string.Equals(data.resourceId, "food", System.StringComparison.OrdinalIgnoreCase) && foodAmountText != null)
            foodAmountText.text = data.newAmount.ToString();
        else if (string.Equals(data.resourceId, "currency", System.StringComparison.OrdinalIgnoreCase) && currencyAmountText != null)
            currencyAmountText.text = data.newAmount.ToString();
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

    private void RefreshDisplay()
    {
        if (SettlementBridge.Instance == null)
        {
            Debug.LogWarning("[InfoPanelResourceDisplay] SettlementBridge.Instance is null");
            return;
        }

        if (foodAmountText != null)
            foodAmountText.text = SettlementBridge.Instance.GetResourceAmount("food").ToString();
        if (currencyAmountText != null)
            currencyAmountText.text = SettlementBridge.Instance.GetResourceAmount("currency").ToString();
    }
}
