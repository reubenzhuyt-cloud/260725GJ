using UnityEngine;
using System;

[CreateAssetMenu(fileName = "GamePopupEvent", menuName = "Events/GamePopupEvent")]
public class GamePopupEvent : GameEvent<PopupData> {}

[Serializable]
public struct PopupData
{
    public int eventIndex;
    public string eventId;
    public string title;
    public string description;
    public Sprite image;
    public GameEventType eventType;
    public EventEffect[] confirmEffects;
    public string[] choiceTexts;
    public string[] choiceResults;
    public EventEffect[][] choiceEffects;
}
