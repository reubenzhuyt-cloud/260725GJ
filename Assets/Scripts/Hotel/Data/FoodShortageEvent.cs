using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Events/FoodShortageEvent")]
public class FoodShortageEvent : GameEvent<FoodShortageData> {}

[Serializable]
public struct FoodShortageData
{
    public int day;
    public int shortageAmount;
}
