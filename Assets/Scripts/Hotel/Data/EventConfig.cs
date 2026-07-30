using UnityEngine;
using System.Collections.Generic;

public enum GameEventType { Confirm, Choice }

public enum EffectType { None, ModifyErosion }

[System.Serializable]
public class EventEffect
{
    public EffectType effectType = EffectType.None;
    public float floatValue;
}

[CreateAssetMenu(fileName = "EventConfig", menuName = "Configs/EventConfig")]
public class EventConfig : ScriptableObject
{
    [Header("Event ID")]
    public int eventIndex;
    public string eventId;
    public string eventTitle;
    [TextArea] public string eventDescription;
    public Sprite eventImage;
    public int triggerHour;
    public int triggerMinute;
    public GameEventType eventType = GameEventType.Confirm;

    [Header("Confirm Effects (applied on confirm)")]
    public List<EventEffect> confirmEffects = new List<EventEffect>();

    [Header("Choice Options (only for Choice type)")]
    public List<ChoiceOption> choices = new List<ChoiceOption>();
}

[System.Serializable]
public class ChoiceOption
{
    public string choiceId;
    public string choiceText;
    [TextArea] public string choiceResult;

    [Header("Effects for this choice")]
    public List<EventEffect> choiceEffects = new List<EventEffect>();
}
