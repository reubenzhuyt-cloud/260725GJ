using UnityEngine;
using UnityEngine.UI;

public class TimeControlUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button pauseButton;
    public Button speed1xButton;
    public Button speed2xButton;
    public Button stopButton;

    public enum SpeedState { Normal, Fast, Faster }

    [HideInInspector]
    public SpeedState currentState = SpeedState.Normal;

    [HideInInspector]
    public bool isPaused = false;

    private void Start()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveAllListeners();
            pauseButton.onClick.AddListener(OnPauseClicked);
        }

        if (speed1xButton != null)
        {
            speed1xButton.onClick.RemoveAllListeners();
            speed1xButton.onClick.AddListener(OnSpeed1xClicked);
        }

        if (speed2xButton != null)
        {
            speed2xButton.onClick.RemoveAllListeners();
            speed2xButton.onClick.AddListener(OnSpeed2xClicked);
        }

        if (stopButton != null)
        {
            stopButton.onClick.RemoveAllListeners();
            stopButton.onClick.AddListener(OnStopClicked);
        }

        // Select 1x button by default so it shows its Selected color
        if (speed1xButton != null)
            speed1xButton.Select();
    }

    private void OnPauseClicked()
    {
        if (isPaused)
        {
            // Already paused → unpause, go to 1x
            isPaused = false;
            SetState(SpeedState.Normal);
        }
        else
        {
            isPaused = true;
        }
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = isPaused;
    }

    private void OnSpeed1xClicked()
    {
        // 1x again → no effect
        if (currentState == SpeedState.Normal && !isPaused) return;

        isPaused = false;
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = false;
        SetState(SpeedState.Normal);
    }

    private void OnSpeed2xClicked()
    {
        if (currentState == SpeedState.Fast)
        {
            // Already 2x → back to 1x
            SetState(SpeedState.Normal);
            return;
        }
        isPaused = false;
        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = false;
        SetState(SpeedState.Fast);
    }

    private void OnStopClicked()
    {
        if (currentState == SpeedState.Faster)
        {
            // Already 4x → back to 1x
            SetState(SpeedState.Normal);
            return;
        }
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
    }
}
