using UnityEngine;
using System;

[System.Obsolete("Deprecated: Use new phase system instead")]
[CreateAssetMenu(fileName = "TimeSpeedChangedEvent", menuName = "Events/TimeSpeedChangedEvent")]
public class TimeSpeedChangedEvent : GameEvent<TimeSpeedData> {}

[Serializable]
public struct TimeSpeedData
{
    public int speedMultiplier;
    public bool isPaused;
    public bool isWaitingAtNode;
}