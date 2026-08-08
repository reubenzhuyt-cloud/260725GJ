using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    private void OnEnable()
    {
        if (onResourceAdjusted != null)
            onResourceAdjusted.Register(OnResourceAdjusted);
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
    }

    private void OnDisable()
    {
        if (onResourceAdjusted != null)
            onResourceAdjusted.Unregister(OnResourceAdjusted);
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
    }

    private void Start()
    {
        RefreshDisplay();
    }

    private void OnResourceAdjusted(ResourceAdjustedData data)
    {
        if (data.resourceId == "food" && foodAmountText != null)
            foodAmountText.text = data.newAmount.ToString();
        else if (data.resourceId == "currency" && currencyAmountText != null)
            currencyAmountText.text = data.newAmount.ToString();
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        if (data.phase == GamePhase.Dawn)
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
