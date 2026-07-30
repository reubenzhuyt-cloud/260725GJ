using UnityEngine;
using TMPro;

public class TimeUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI clockText;
    public TextMeshProUGUI phaseText;

    [Header("Event Listeners")]
    public TimePhaseChangedEvent onPhaseChanged;
    public DayStartedEvent onDayStarted;

    private void OnEnable()
    {
        if (onPhaseChanged != null)
            onPhaseChanged.Register(OnPhaseChanged);
        if (onDayStarted != null)
            onDayStarted.Register(OnDayStarted);
    }

    private void OnDisable()
    {
        if (onPhaseChanged != null)
            onPhaseChanged.Unregister(OnPhaseChanged);
        if (onDayStarted != null)
            onDayStarted.Unregister(OnDayStarted);
    }

    private void Start()
    {
        // Initialize display with current state
        if (TimeManager.Instance != null)
        {
            UpdateDisplay(TimeManager.Instance.GetTimeState());
        }
    }

    private void Update()
    {
        // Poll clock every frame for smooth ticking
        if (TimeManager.Instance != null)
        {
            UpdateClockText(TimeManager.Instance.GetTimeString());
        }
    }

    private void OnPhaseChanged(PhaseData data)
    {
        UpdateDayText(data.day);
        UpdateClockText($"{data.hour:D2}:{data.minute:D2}");
        UpdatePhaseText(data.phase);
    }

    private void OnDayStarted(DayData data)
    {
        UpdateDayText(data.day);
    }

    private void UpdateDisplay(TimeState state)
    {
        UpdateDayText(state.currentDay);
        UpdateClockText(state.GetTimeString());
        UpdatePhaseText(state.currentPhase);
    }

    private void UpdateDayText(int day)
    {
        if (dayText != null)
            dayText.text = $"Day {day}";
    }

    private void UpdateClockText(string timeString)
    {
        if (clockText != null)
            clockText.text = timeString;
    }

    private void UpdatePhaseText(TimePhase phase)
    {
        if (phaseText != null)
            phaseText.text = TimeState.GetPhaseName(phase);
    }
}