using UnityEngine;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [Header("Scheduled Events")]
    public List<EventConfig> scheduledEvents = new List<EventConfig>();

    [Header("Event Channel")]
    public GamePopupEvent onPopupEvent;

    private HashSet<string> firedToday = new HashSet<string>();
    private int lastCheckedMinute = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (TimeManager.Instance == null || TimeManager.Instance.isPaused) return;

        var state = TimeManager.Instance.timeState;
        int currentMinute = state.hour * 60 + state.minute;

        if (currentMinute != lastCheckedMinute)
        {
            lastCheckedMinute = currentMinute;
            CheckTimeEvents(state.hour, state.minute, state.currentDay);
        }
    }

    public void CheckTimeEvents(int hour, int minute, int day)
    {
        foreach (var config in scheduledEvents)
        {
            if (config == null) continue;
            string key = $"{config.eventId}_day{day}";
            if (firedToday.Contains(key)) continue;

            if (config.triggerHour == hour && config.triggerMinute == minute)
            {
                firedToday.Add(key);
                TriggerEvent(config);
            }
        }
    }

    private void TriggerEvent(EventConfig config)
    {
        if (onPopupEvent == null) return;

        if (TimeManager.Instance != null)
            TimeManager.Instance.isPaused = true;

        PopupData data = new PopupData
        {
            eventIndex = config.eventIndex,
            eventId = config.eventId,
            title = config.eventTitle,
            description = config.eventDescription,
            image = config.eventImage,
            eventType = config.eventType,
        };

        if (config.eventType == GameEventType.Confirm)
        {
            data.confirmEffects = config.confirmEffects.ToArray();
        }
        else if (config.eventType == GameEventType.Choice && config.choices.Count > 0)
        {
            data.choiceTexts = new string[config.choices.Count];
            data.choiceResults = new string[config.choices.Count];
            data.choiceEffects = new EventEffect[config.choices.Count][];

            for (int i = 0; i < config.choices.Count; i++)
            {
                data.choiceTexts[i] = config.choices[i].choiceText;
                data.choiceResults[i] = config.choices[i].choiceResult;
                data.choiceEffects[i] = config.choices[i].choiceEffects.ToArray();
            }
        }

        onPopupEvent.Raise(data);
        Debug.Log($"[EventManager] Triggered: {config.eventTitle} at {config.triggerHour:D2}:{config.triggerMinute:D2}");
    }

    public void ResetToday()
    {
        firedToday.Clear();
    }
}
