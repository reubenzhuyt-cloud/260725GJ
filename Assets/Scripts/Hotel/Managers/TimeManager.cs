using UnityEngine;

public enum TimeSpeed { Normal = 1, Fast = 2, Faster = 3, Fastest = 5 }

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [Header("Time State")]
    public TimeState timeState = new TimeState();

    [Header("Event Channels (SO assets)")]
    public TimePhaseChangedEvent onPhaseChanged;
    public DayStartedEvent onDayStarted;
    public TimeSpeedChangedEvent onTimeSpeedChanged;

    [Header("Time Control")]
    public bool isPaused = false;
    public TimeSpeed currentSpeed = TimeSpeed.Normal;

    // Clock ticking accumulator
    private float minuteAccumulator = 0f;

    // Speed multiplier for direct access
    public int speedMultiplier => (int)currentSpeed;

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
        if (isPaused) return;

        // Apply speed multiplier: 1 real second = 20 game minutes * speed
        float gameMinutesThisFrame = Time.deltaTime * 20f * (int)currentSpeed;
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

    #region Time Pause

    public void PauseTime()
    {
        isPaused = true;
        RaiseTimeSpeedChanged();
    }

    public void ResumeTime()
    {
        isPaused = false;
        RaiseTimeSpeedChanged();
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        RaiseTimeSpeedChanged();
    }

    #endregion

    #region Time Speed

    public void SetSpeed(int multiplier)
    {
        switch (multiplier)
        {
            case 1: currentSpeed = TimeSpeed.Normal; break;
            case 2: currentSpeed = TimeSpeed.Fast; break;
            case 3: currentSpeed = TimeSpeed.Faster; break;
            case 4: currentSpeed = TimeSpeed.Faster; break;
            case 5: currentSpeed = TimeSpeed.Fastest; break;
            default: currentSpeed = TimeSpeed.Normal; break;
        }
        RaiseTimeSpeedChanged();
    }

    public void SetTimeSpeed(TimeSpeed speed)
    {
        currentSpeed = speed;
        RaiseTimeSpeedChanged();
    }

    public void SetTimeSpeed(int multiplier)
    {
        SetSpeed(multiplier);
    }

    public void ResetSpeed()
    {
        currentSpeed = TimeSpeed.Normal;
        RaiseTimeSpeedChanged();
    }

    #endregion

    #region Event Helpers

    private void RaiseTimeSpeedChanged()
    {
        if (onTimeSpeedChanged != null)
        {
            onTimeSpeedChanged.Raise(new TimeSpeedData
            {
                speedMultiplier = (int)currentSpeed,
                isPaused = isPaused,
                isWaitingAtNode = false
            });
        }
    }

    #endregion
}