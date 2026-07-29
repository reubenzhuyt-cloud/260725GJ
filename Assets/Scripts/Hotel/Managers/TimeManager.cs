using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time State")]
    public TimeState timeState = new TimeState();

    [Header("Event Channels (SO assets)")]
    public TimePhaseChangedEvent onPhaseChanged;
    public DayStartedEvent onDayStarted;

    // Clock ticking accumulator
    private float minuteAccumulator = 0f;

    // Phase boundaries (hour thresholds)
    private const int DAWN_HOUR = 5;
    private const int DAYTIME_HOUR = 7;
    private const int DUSK_HOUR = 17;
    private const int NIGHT_HOUR = 19;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        // 1 real second = 20 game minutes
        float gameMinutesThisFrame = Time.deltaTime * 20f;
        minuteAccumulator += gameMinutesThisFrame;

        // Process complete minutes
        while (minuteAccumulator >= 1f)
        {
            minuteAccumulator -= 1f;
            AdvanceMinute();
        }
    }

    private void AdvanceMinute()
    {
        timeState.minute++;

        // Roll over minutes to hours
        if (timeState.minute >= 60)
        {
            timeState.minute = 0;
            timeState.hour++;

            // Roll over hours (24-hour clock)
            if (timeState.hour >= 24)
            {
                timeState.hour = 0;
                // New day starts when clock wraps from 23:59 to 00:00
                timeState.currentDay++;

                if (onDayStarted != null)
                {
                    onDayStarted.Raise(new DayData
                    {
                        day = timeState.currentDay
                    });
                }

                Debug.Log($"[TimeManager] New day: {timeState.currentDay}");
            }

            // Check phase transitions based on hour
            CheckPhaseTransition();
        }
    }

    private void CheckPhaseTransition()
    {
        TimePhase newPhase = timeState.currentPhase;

        if (timeState.hour == DAWN_HOUR && timeState.minute == 0)
            newPhase = TimePhase.Dawn;
        else if (timeState.hour == DAYTIME_HOUR && timeState.minute == 0)
            newPhase = TimePhase.Daytime;
        else if (timeState.hour == DUSK_HOUR && timeState.minute == 0)
            newPhase = TimePhase.Dusk;
        else if (timeState.hour == NIGHT_HOUR && timeState.minute == 0)
            newPhase = TimePhase.Night;

        if (newPhase != timeState.currentPhase)
        {
            timeState.currentPhase = newPhase;

            if (onPhaseChanged != null)
            {
                onPhaseChanged.Raise(new PhaseData
                {
                    day = timeState.currentDay,
                    hour = timeState.hour,
                    minute = timeState.minute,
                    phase = timeState.currentPhase
                });
            }

            Debug.Log($"[TimeManager] Phase changed: {timeState}");
        }
    }

    [ContextMenu("Advance Phase")]
    public void AdvancePhase()
    {
        TimePhase oldPhase = timeState.currentPhase;
        timeState.currentPhase = (TimePhase)(((int)timeState.currentPhase + 1) % 4);

        // Raise phase changed event
        if (onPhaseChanged != null)
        {
            onPhaseChanged.Raise(new PhaseData
            {
                day = timeState.currentDay,
                hour = timeState.hour,
                minute = timeState.minute,
                phase = timeState.currentPhase
            });
        }

        // If we wrapped back to Dawn, it's a new day
        if (timeState.currentPhase == TimePhase.Dawn && oldPhase == TimePhase.Night)
        {
            timeState.currentDay++;

            if (onDayStarted != null)
            {
                onDayStarted.Raise(new DayData
                {
                    day = timeState.currentDay
                });
            }
        }

        Debug.Log($"[TimeManager] {timeState}");
    }

    // For UI to read current state
    public TimeState GetTimeState()
    {
        return timeState;
    }

    // Get formatted time string
    public string GetTimeString()
    {
        return timeState.GetTimeString();
    }
}