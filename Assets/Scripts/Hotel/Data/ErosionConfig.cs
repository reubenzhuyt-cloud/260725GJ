using UnityEngine;

[CreateAssetMenu(fileName = "ErosionConfig", menuName = "Configs/ErosionConfig")]
public class ErosionConfig : ScriptableObject
{
    [Header("Erosion Rate Per Phase")]
    public float dawnRate = 0f;
    public float daytimeRate = 0f;
    public float duskRate = 0f;
    public float nightRate = 2f;
}