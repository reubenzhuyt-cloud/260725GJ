using UnityEngine;
using System;

[CreateAssetMenu(fileName = "DayStartedEvent", menuName = "Events/DayStartedEvent")]
public class DayStartedEvent : GameEvent<DayData> {}

[Serializable]
public struct DayData
{
    public int day;
}