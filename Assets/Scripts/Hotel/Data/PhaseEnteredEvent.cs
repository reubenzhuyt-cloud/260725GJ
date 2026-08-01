using UnityEngine;
using System;

[CreateAssetMenu(fileName = "PhaseEnteredEvent", menuName = "Events/PhaseEnteredEvent")]
public class PhaseEnteredEvent : GameEvent<PhaseEnterData> {}

[Serializable]
public struct PhaseEnterData
{
    public int day;
    public GamePhase phase;
}
