using UnityEngine;
using System;

[CreateAssetMenu(fileName = "ErosionChangedEvent", menuName = "Events/ErosionChangedEvent")]
public class ErosionChangedEvent : GameEvent<ErosionData> {}

[Serializable]
public struct ErosionData
{
    public float oldValue;
    public float newValue;
    public float delta;
}