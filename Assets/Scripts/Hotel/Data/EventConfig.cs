using UnityEngine;
using System.Collections.Generic;

public enum GamePhase { Day, Dawn, Night, Dusk }

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
    [Header("Identity")]
    public int eventIndex;
    public string eventId;

    [Header("Trigger")]
    public GamePhase triggerPhase = GamePhase.Day;
    public string triggerCondition; // reserved for future use, leave empty

    [Header("Content")]
    public string eventTitle;
    [TextArea] public string eventDescription;
    public Sprite eventImage;
    public GameEventType eventType = GameEventType.Confirm;

    [Header("Confirm Effects")]
    public List<EventEffect> confirmEffects = new List<EventEffect>();

    [Header("Choice Options")]
    public List<ChoiceOption> choices = new List<ChoiceOption>();
}

[System.Serializable]
public class ChoiceOption
{
    public string choiceId;
    public string choiceText;
    [TextArea] public string choiceResult;
    public List<EventEffect> choiceEffects = new List<EventEffect>();
}
