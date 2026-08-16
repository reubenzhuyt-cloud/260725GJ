using UnityEngine;
using System;
using Hotel.Runtime;

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
    public EventKind eventKind;
    public EventEffect[] confirmEffects;
    public string[] choiceTexts;
    public string[] choiceResults;
    public string[] choiceEffectTexts;
    public string[] choiceIds;
    public TenantAbility[][] choiceRequiredTags;
    public EventEffect[][] choiceEffects;
    public bool[] choiceLocked;
}
