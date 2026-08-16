using UnityEngine;
using Hotel.Runtime;

/// <summary>
/// 运行时访客档案（生成池产物，不落资产、不序列化）。
/// 字段与 TenantReviewCandidateSO 对齐，物化后交给审查协调器使用。
/// </summary>
[System.Serializable]
public class TenantCandidateProfile
{
    public string candidateId;
    public string displayName;
    public string avatarKey;
    public Color avatarColor = Color.white;
    public TenantAbility ability;
    public TenantActivityType activityType;
    public TenantErosionTier tier;
    public string shortDescription;
    public string detailedDescription;

    public Sprite ResolvePortrait()
    {
        return TenantAvatarResolver.TryResolve(avatarKey, out var sprite) ? sprite : null;
    }
}

/// <summary>
/// 侵蚀档位分布权重（开放接口）。默认 绿60/黄30/红10（策划 4.2.5 说明表）。
/// 多周目难度可通过传入不同权重改变整体侵蚀度倾向（如整体偏红）。
/// </summary>
[System.Serializable]
public struct ErosionWeightProfile
{
    public int green;
    public int yellow;
    public int red;

    public ErosionWeightProfile(int g, int y, int r)
    {
        green = g;
        yellow = y;
        red = r;
    }

    public static ErosionWeightProfile Default => new ErosionWeightProfile(60, 30, 10);

    public bool IsZero => green <= 0 && yellow <= 0 && red <= 0;
}
