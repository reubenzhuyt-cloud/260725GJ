using UnityEditor;
using UnityEngine;
using Hotel.Runtime;

/// <summary>
/// 访客池开发工具（仅编辑器，不进构建）。
/// 菜单 Tools/Hotel 下提供：
///  - 打印本局组合：Play 模式下输出当前局 40 个访客档案；编辑模式下用示例种子生成演示。
///  - 检查 pool.json：验证池数据加载与分布。
/// </summary>
public static class HotelPoolTools
{
    private const string MenuRoot = "Tools/Hotel/";

    [MenuItem(MenuRoot + "访客池：打印本局组合", priority = 1)]
    public static void PrintRunProfiles()
    {
        if (Application.isPlaying)
        {
            if (TenantPoolManager.NormalProfiles.Count == 0)
            {
                Debug.LogWarning("[HotelPoolTools] 当前局没有池档案（可能未启用生成池或尚未启动）。");
                return;
            }
            DumpProfiles(TenantPoolManager.NormalProfiles, "本局");
            if (TenantPoolManager.SpecialProfiles.Count > 0)
                DumpProfiles(TenantPoolManager.SpecialProfiles, "特殊NPC");
        }
        else
        {
            Debug.Log("[HotelPoolTools] 编辑模式：用示例种子 424242 生成演示组合...");
            if (!TenantPoolManager.BuildRun(424242, ErosionWeightProfile.Default, 40))
            {
                Debug.LogError("[HotelPoolTools] 池数据加载失败，请先检查 Assets/Resources/Pool/pool.json");
                return;
            }
            DumpProfiles(TenantPoolManager.NormalProfiles, "演示(种子424242)");
        }
    }

    [MenuItem(MenuRoot + "访客池：检查 pool.json", priority = 2)]
    public static void CheckPoolData()
    {
        if (!TenantPoolManager.TryLoad())
        {
            Debug.LogError("[HotelPoolTools] pool.json 加载失败！检查 Assets/Resources/Pool/pool.json 是否存在且格式正确。");
            return;
        }
        var pool = TenantPoolManager.Pool;
        int male = 0, female = 0;
        for (int i = 0; i < pool.names.Count; i++)
        {
            if (pool.names[i].gender == "f") female++; else male++;
        }
        Debug.Log($"[HotelPoolTools] 池状态：名字 {pool.names.Count}（男{male}/女{female}），文案 {pool.copy.Count}，特殊NPC模板 {pool.specials.Count}");
        var tiers = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < pool.copy.Count; i++)
        {
            if (!tiers.ContainsKey(pool.copy[i].tier)) tiers.Add(pool.copy[i].tier, 0);
            tiers[pool.copy[i].tier]++;
        }
        foreach (var kv in tiers)
            Debug.Log($"[HotelPoolTools]   文案档位 {kv.Key}: {kv.Value} 条");
    }

    private static void DumpProfiles(System.Collections.Generic.IReadOnlyList<TenantCandidateProfile> profiles, string tag)
    {
        Debug.Log($"[HotelPoolTools] === {tag}访客组合（{profiles.Count} 个）===");
        for (int i = 0; i < profiles.Count; i++)
        {
            var p = profiles[i];
            Debug.Log($"[HotelPoolTools] {i + 1:00}. {p.candidateId} | {p.displayName} | {p.ability} | {p.tier} | {p.activityType} | 头像:{p.avatarKey}\n    {p.shortDescription}");
        }
    }
}
