using UnityEngine;
using UnityEngine.UI;

[System.Obsolete("Deprecated: Use new phase system instead")]
public class TimeControlUI : MonoBehaviour
{
    [Header("Toggles")]
    public Toggle pauseToggle;
    public Toggle speed1xToggle;
    public Toggle speed2xToggle;
    public Toggle speed4xToggle;

    [Header("Event Listener")]
    public GamePopupEvent onPopupEvent;

    public enum SpeedState { Normal, Fast, Faster }

    [HideInInspector]
    public SpeedState currentState = SpeedState.Normal;

    [HideInInspector]
    public bool isPaused = false;

    private SpeedState stateBeforeEvent = SpeedState.Normal;

    private void OnEnable()
    {
        if (onPopupEvent != null)
            onPopupEvent.Register(OnEventTriggered);
    }

    private void OnDisable()
    {
        if (onPopupEvent != null)
            onPopupEvent.Unregister(OnEventTriggered);
    }

    private void Start()
    {
        if (pauseToggle != null)
        {
            pauseToggle.onValueChanged.RemoveAllListeners();
            pauseToggle.onValueChanged.AddListener(OnPauseChanged);
        }

        if (speed1xToggle != null)
        {
            speed1xToggle.onValueChanged.RemoveAllListeners();
            speed1xToggle.onValueChanged.AddListener(On1xChanged);
        }

        if (speed2xToggle != null)
        {
            speed2xToggle.onValueChanged.RemoveAllListeners();
            speed2xToggle.onValueChanged.AddListener(On2xChanged);
        }

        if (speed4xToggle != null)
        {
            speed4xToggle.onValueChanged.RemoveAllListeners();
            speed4xToggle.onValueChanged.AddListener(On4xChanged);
        }

        // Start paused
        SetState(SpeedState.Normal);

        isPaused = true;
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = true;

        // Set pause toggle after UpdateToggles resets it
        if (pauseToggle != null)
            SetToggleWithoutCallback(pauseToggle, OnPauseChanged, true);
    }

    private void OnEventTriggered(PopupData data)
    {
        stateBeforeEvent = currentState;
        // Don't call SetState — it resets isPaused via UpdateToggles.
        // EventManager already paused time; just update speed and visuals.
        currentState = SpeedState.Normal;
        if (TimeManager.Instance != null)
            TimeManager.Instance.SetSpeed(1);
        SetToggleWithoutCallback(speed1xToggle, On1xChanged, true);
        SetToggleWithoutCallback(speed2xToggle, On2xChanged, false);
        SetToggleWithoutCallback(speed4xToggle, On4xChanged, false);
    }

    public void OnEventClosed()
    {
        SpeedState restoreState = stateBeforeEvent;

        switch (stateBeforeEvent)
        {
            case SpeedState.Faster:
                restoreState = SpeedState.Fast;
                break;
        }

        if (isPaused)
        {
            isPaused = false;
            restoreState = SpeedState.Normal;
        }

        SetState(restoreState);
    }

    private void OnPauseChanged(bool isOn)
    {
        isPaused = isOn;
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = isOn;
    }

    private void On1xChanged(bool isOn)
    {
        if (!isOn) return;
        isPaused = false;
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = false;
        SetState(SpeedState.Normal);
    }

    private void On2xChanged(bool isOn)
    {
        if (!isOn) return;
        isPaused = false;
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = false;
        SetState(SpeedState.Fast);
    }

    private void On4xChanged(bool isOn)
    {
        if (!isOn) return;
        isPaused = false;
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = false;
        SetState(SpeedState.Faster);
    }

    private void SetState(SpeedState newState)
    {
        currentState = newState;

        if (TimeManager.Instance != null)
        {
            switch (newState)
            {
                case SpeedState.Normal: TimeManager.Instance.SetSpeed(1); break;
                case SpeedState.Fast: TimeManager.Instance.SetSpeed(2); break;
                case SpeedState.Faster: TimeManager.Instance.SetSpeed(4); break;
            }
        }

        UpdateToggles();
    }

    private void UpdateToggles()
    {
        SetToggleWithoutCallback(pauseToggle, OnPauseChanged, false);
        SetToggleWithoutCallback(speed1xToggle, On1xChanged, currentState == SpeedState.Normal);
        SetToggleWithoutCallback(speed2xToggle, On2xChanged, currentState == SpeedState.Fast);
        SetToggleWithoutCallback(speed4xToggle, On4xChanged, currentState == SpeedState.Faster);

        isPaused = false;
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = false;
    }

    private void SetToggleWithoutCallback(Toggle toggle, UnityEngine.Events.UnityAction<bool> callback, bool value)
    {
        if (toggle == null) return;
        toggle.onValueChanged.RemoveListener(callback);
        toggle.isOn = value;
        toggle.onValueChanged.AddListener(callback);
    }
}
