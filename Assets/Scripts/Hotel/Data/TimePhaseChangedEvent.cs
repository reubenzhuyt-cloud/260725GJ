using UnityEngine;
using System;

[System.Obsolete("Deprecated: Use new phase system instead")]
[CreateAssetMenu(fileName = "TimePhaseChangedEvent", menuName = "Events/TimePhaseChangedEvent")]
public class TimePhaseChangedEvent : GameEvent<PhaseData> {}

[Serializable]
public struct PhaseData
{
    public int day;
    public int hour;
    public int minute;
    public TimePhase phase;
}