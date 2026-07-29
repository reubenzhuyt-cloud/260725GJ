using UnityEngine;
using System;

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