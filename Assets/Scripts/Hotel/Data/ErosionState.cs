using System;

[Serializable]
public class ErosionState
{
    public float erosionValue = 0f;
    public const float MinValue = 0f;
    public const float MaxValue = 100f;

    public void Set(float value)
    {
        erosionValue = UnityEngine.Mathf.Clamp(value, MinValue, MaxValue);
    }

    public void Add(float delta)
    {
        Set(erosionValue + delta);
    }

    public string GetDisplayText()
    {
        return $"{erosionValue:F1}%";
    }
}