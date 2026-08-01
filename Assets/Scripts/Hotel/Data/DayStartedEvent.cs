using UnityEngine;
using System;

[System.Obsolete("Deprecated: Use new phase system instead")]
[CreateAssetMenu(fileName = "DayStartedEvent", menuName = "Events/DayStartedEvent")]
public class DayStartedEvent : GameEvent<DayData> {}

[Serializable]
public struct DayData
{
    public int day;
}