using UnityEngine;
using TMPro;
using Hotel.Runtime;

public class PhaseUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI phaseText;

    [Header("Phase Names")]
    public string dayName = "白天";
    public string dawnName = "黎明";
    public string nightName = "黑夜";
    public string duskName = "黄昏";

    [Header("Event Listener")]
    public PhaseEnteredEvent onPhaseEntered;

    private bool _runStateRestoredSubscribed;

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
        SubscribeRunStateRestored();
    }

    private void OnDisable()
    {
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
        if (GamePhaseManager.Instance != null)
            UpdateDisplay(GamePhaseManager.Instance.currentDay, GamePhaseManager.Instance.currentPhase);
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        UpdateDisplay(data.day, data.phase);
    }

    private void OnRunStateRestored(GameRunState state)
    {
        if (state == null || state.Phase == null)
            return;
        UpdateDisplay(state.Day, ToGamePhase(state.Phase.Current));
    }

    private static GamePhase ToGamePhase(HotelPhase phase)
    {
        switch (phase)
        {
            case HotelPhase.Dawn: return GamePhase.Dawn;
            case HotelPhase.Dusk: return GamePhase.Dusk;
            case HotelPhase.Night: return GamePhase.Night;
            default: return GamePhase.Day;
        }
    }

    private void UpdateDisplay(int day, GamePhase phase)
    {
        if (dayText != null)
            dayText.text = $"Day {day}";

        if (phaseText != null)
        {
            switch (phase)
            {
                case GamePhase.Day:   phaseText.text = dayName; break;
                case GamePhase.Dawn:  phaseText.text = dawnName; break;
                case GamePhase.Night: phaseText.text = nightName; break;
                case GamePhase.Dusk:  phaseText.text = duskName; break;
            }
        }
    }
}
