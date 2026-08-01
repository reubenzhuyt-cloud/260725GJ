using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Events/ResourceAdjustedEvent")]
public class ResourceAdjustedEvent : GameEvent<ResourceAdjustedData> {}

[Serializable]
public struct ResourceAdjustedData
{
    public string resourceId;
    public int delta;
    public int newAmount;
}
