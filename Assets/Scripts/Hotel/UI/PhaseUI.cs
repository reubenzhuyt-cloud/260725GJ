using UnityEngine;
using TMPro;

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

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
    }

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
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
