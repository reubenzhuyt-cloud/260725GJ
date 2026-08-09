using UnityEngine;
using Hotel.Runtime;

[CreateAssetMenu(fileName = "TenantReviewCandidate", menuName = "Configs/TenantReviewCandidate")]
public class TenantReviewCandidateSO : ScriptableObject
{
    [Header("Identity")]
    public string candidateId;

    [Header("Display")]
    public string displayName;
    public string avatarKey;
    public Sprite portrait;
    public Color avatarColor = Color.white;

    [Header("Gameplay Information")]
    public TenantAbility ability;
    public TenantActivityType activityType;

    [Header("Descriptions")]
    [TextArea(2, 4)]
    public string shortDescription;

    [TextArea(6, 12)]
    public string detailedDescription;
}
