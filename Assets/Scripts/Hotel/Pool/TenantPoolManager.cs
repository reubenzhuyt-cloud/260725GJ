using System;
using System.Collections.Generic;
using UnityEngine;
using Hotel.Runtime;

/// <summary>
/// 访客生成池管理器（策划案 4.2.5 的 Unity 实现）。
/// - 普通访客：每局从 名字池 × 文案池(职业×档位) × 头像池 确定性组合出 40 个档案；
/// - 特殊 NPC：独立模板池（预留接口，事件侧接入在后续迭代）；
/// - 全部随机基于 System.Random(seed) 固定顺序推进 → 同 seed 同结果（存档恢复可重建）。
/// - 池内容支持运行时增删改（Add/Remove/Reload），权重开放接口。
/// 数据源：Assets/Resources/Pool/pool.json（由飞书 4.2.5 三张表导出）。
/// </summary>
public static class TenantPoolManager
{
    // ---------- JSON 数据结构 ----------
    [Serializable]
    public class PoolData
    {
        public int version;
        public List<NameEntry> names = new List<NameEntry>();
        public List<CopyEntry> copy = new List<CopyEntry>();
        public List<SpecialTemplate> specials = new List<SpecialTemplate>();
    }

    [Serializable]
    public class NameEntry
    {
        public string id;
        public string gender; // m / f
    }

    [Serializable]
    public class CopyEntry
    {
        public string job;  // 医生/厨师/守夜人/工程师/前员工/商贩/农民/司机/教师/通用
        public string tier; // 绿/黄/红/通用
        public string text;
    }

    [Serializable]
    public class SpecialTemplate
    {
        public string id;          // 模板 id → 生成 sp_<id>
        public string job;         // 职业（决定 ability 与文案池键）
        public string chainId;     // 预留：关联事件链 id
        public List<string> nameCandidates = new List<string>();
        public List<string> avatarCandidates = new List<string>();
        public List<string> copyCandidates = new List<string>();
    }

    // ---------- 状态 ----------
    private static PoolData _pool;
    private static bool _loaded;
    private static List<TenantCandidateProfile> _normalProfiles;
    private static List<TenantCandidateProfile> _specialProfiles;

    public static IReadOnlyList<TenantCandidateProfile> NormalProfiles => _normalProfiles ?? (IReadOnlyList<TenantCandidateProfile>)Array.Empty<TenantCandidateProfile>();
    public static IReadOnlyList<TenantCandidateProfile> SpecialProfiles => _specialProfiles ?? (IReadOnlyList<TenantCandidateProfile>)Array.Empty<TenantCandidateProfile>();
    public static bool IsReady => _loaded && _pool != null;
    public static PoolData Pool => _pool;

    private static readonly Dictionary<string, TenantAbility> JobToAbility = new Dictionary<string, TenantAbility>
    {
        { "医生", TenantAbility.Doctor },
        { "厨师", TenantAbility.Cook },
        { "工程师", TenantAbility.Engineer },
        { "守夜人", TenantAbility.NightWatch },
        { "前员工", TenantAbility.FormerEmployee },
        { "商贩", TenantAbility.Merchant },
        { "农民", TenantAbility.Farmer },
        { "司机", TenantAbility.Driver },
        { "教师", TenantAbility.Teacher },
        { "木工", TenantAbility.Carpenter },
    };

    private static readonly string[] NormalJobKeys =
    {
        "医生", "厨师", "守夜人", "工程师", "前员工", "商贩", "农民", "司机", "教师", "木工",
    };

    private static readonly Dictionary<string, string> TierKey = new Dictionary<string, string>
    {
        { nameof(TenantErosionTier.Green), "绿" },
        { nameof(TenantErosionTier.Yellow), "黄" },
        { nameof(TenantErosionTier.Red), "红" },
    };

    // ---------- 加载 ----------
    public static bool TryLoad()
    {
        if (_loaded) return _pool != null;
        _loaded = true;
        var asset = Resources.Load<TextAsset>("Pool/pool");
        if (asset == null)
        {
            Debug.LogWarning("[TenantPoolManager] pool.json 未找到（Resources/Pool/pool）。将回退到旧候选人路径。");
            return false;
        }
        try
        {
            _pool = JsonUtility.FromJson<PoolData>(asset.text);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TenantPoolManager] pool.json 解析失败: {e.Message}");
            _pool = null;
            return false;
        }
        if (_pool == null || _pool.names == null || _pool.names.Count == 0 || _pool.copy == null || _pool.copy.Count == 0)
        {
            Debug.LogWarning("[TenantPoolManager] pool.json 内容为空。将回退到旧候选人路径。");
            _pool = null;
            return false;
        }
        return true;
    }

    /// <summary>重新从 Resources 加载池数据（运行时修改后调用）。</summary>
    public static bool Reload()
    {
        _loaded = false;
        return TryLoad();
    }

    // ---------- 每局构建（确定性） ----------
    /// <summary>为本局构建普通访客档案（默认 40 个）与特殊 NPC 档案。同 seed 结果一致。</summary>
    public static bool BuildRun(int seed, ErosionWeightProfile weights, int normalCount = 40)
    {
        if (!TryLoad()) return false;
        if (normalCount < 1) normalCount = 1;

        var rng = new System.Random(seed);
        var names = new List<NameEntry>(_pool.names);
        var profile = weights.IsZero ? ErosionWeightProfile.Default : weights;

        var maleAvatars = GetAdultAvatarKeys("m");
        var femaleAvatars = GetAdultAvatarKeys("f");
        if (maleAvatars.Count == 0 || femaleAvatars.Count == 0)
        {
            Debug.LogError("[TenantPoolManager] 头像池为空，无法生成。");
            return false;
        }

        Shuffle(rng, maleAvatars);
        Shuffle(rng, femaleAvatars);
        int maleAvatarIndex = 0;
        int femaleAvatarIndex = 0;

        _normalProfiles = new List<TenantCandidateProfile>(normalCount);
        for (int i = 0; i < normalCount; i++)
        {
            var tier = RollTier(rng, profile);
            var jobKey = NormalJobKeys[rng.Next(NormalJobKeys.Length)];
            var ability = JobToAbility.TryGetValue(jobKey, out var a) ? a : TenantAbility.None;
            var copy = PickCopy(rng, jobKey, tier);
            var name = TakeName(rng, names);
            if (name == null) break;
            string avatarKey;
            if (name.gender == "f")
            {
                if (femaleAvatarIndex >= femaleAvatars.Count)
                {
                    Shuffle(rng, femaleAvatars);
                    femaleAvatarIndex = 0;
                }
                avatarKey = femaleAvatars[femaleAvatarIndex++];
            }
            else
            {
                if (maleAvatarIndex >= maleAvatars.Count)
                {
                    Shuffle(rng, maleAvatars);
                    maleAvatarIndex = 0;
                }
                avatarKey = maleAvatars[maleAvatarIndex++];
            }

            _normalProfiles.Add(new TenantCandidateProfile
            {
                candidateId = $"tenant_pool_{i + 1:000}",
                displayName = name.id,
                avatarKey = avatarKey,
                avatarColor = NeutralColor(rng),
                ability = ability,
                activityType = RollActivityType(rng),
                tier = tier,
                shortDescription = copy != null ? copy.text : string.Empty,
                detailedDescription = string.Empty,
            });
        }

        BuildSpecialProfiles(rng);
        return _normalProfiles.Count > 0;
    }

    // ---------- 随机抽取（确定性） ----------
    private static TenantErosionTier RollTier(System.Random rng, ErosionWeightProfile w)
    {
        int total = w.green + w.yellow + w.red;
        if (total <= 0) return TenantErosionTier.Green;
        int roll = rng.Next(total);
        if (roll < w.green) return TenantErosionTier.Green;
        if (roll < w.green + w.yellow) return TenantErosionTier.Yellow;
        return TenantErosionTier.Red;
    }

    private static TenantActivityType RollActivityType(System.Random rng)
    {
        // 与现有候选人分布一致：日行 60% / 夜行 30% / 全天 10%
        int roll = rng.Next(10);
        if (roll < 6) return TenantActivityType.DayActive;
        if (roll < 9) return TenantActivityType.NightActive;
        return TenantActivityType.AllDay;
    }

    private static CopyEntry PickCopy(System.Random rng, string jobKey, TenantErosionTier tier)
    {
        string tierKey = TierKey.TryGetValue(tier.ToString(), out var t) ? t : null;
        var exact = new List<CopyEntry>();
        var jobAny = new List<CopyEntry>();
        var generic = new List<CopyEntry>();
        for (int i = 0; i < _pool.copy.Count; i++)
        {
            var c = _pool.copy[i];
            if (c == null || string.IsNullOrEmpty(c.text)) continue;
            if (c.job == "通用") { generic.Add(c); continue; }
            if (c.job != jobKey) continue;
            if (tierKey != null && c.tier == tierKey) exact.Add(c);
            jobAny.Add(c);
        }
        var source = exact.Count > 0 ? exact : (jobAny.Count > 0 ? jobAny : generic);
        if (source.Count == 0) return null;
        return source[rng.Next(source.Count)];
    }

    private static NameEntry TakeName(System.Random rng, List<NameEntry> names)
    {
        if (names.Count == 0) return null;
        int idx = rng.Next(names.Count);
        var entry = names[idx];
        names.RemoveAt(idx);
        return entry;
    }

    // ---------- 头像 ----------
    private static List<string> GetAdultAvatarKeys(string gender)
    {
        var result = new List<string>();
        var sprites = Resources.LoadAll<Sprite>("CharacterPhoto/Characters");
        for (int i = 0; i < sprites.Length; i++)
        {
            var key = sprites[i].name;
            if (key.StartsWith("child_")) continue; // 儿童头像留给特殊 NPC
            bool female = key.StartsWith("female_") || key.StartsWith("elder_female_");
            if (gender == "f" && female) result.Add(key);
            else if (gender == "m" && !female) result.Add(key);
        }
        return result;
    }

    private static void Shuffle(System.Random rng, List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            string temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    private static Color NeutralColor(System.Random rng)
    {
        // 柔和低饱和色相，避免与档位语义冲突
        return Color.HSVToRGB(rng.Next(360) / 360f, 0.25f, 0.85f);
    }

    // ---------- 特殊 NPC（预留） ----------
    private static void BuildSpecialProfiles(System.Random rng)
    {
        _specialProfiles = new List<TenantCandidateProfile>();
        if (_pool.specials == null) return;
        for (int i = 0; i < _pool.specials.Count; i++)
        {
            var t = _pool.specials[i];
            if (t == null || string.IsNullOrEmpty(t.id)) continue;
            string name = PickAny(rng, t.nameCandidates);
            string avatar = PickAny(rng, t.avatarCandidates);
            string copy = PickAny(rng, t.copyCandidates);
            if (string.IsNullOrEmpty(name)) continue;
            var maleFallback = GetAdultAvatarKeys("m");
            _specialProfiles.Add(new TenantCandidateProfile
            {
                candidateId = "sp_" + t.id,
                displayName = name,
                avatarKey = string.IsNullOrEmpty(avatar)
                    ? (maleFallback.Count > 0 ? maleFallback[rng.Next(maleFallback.Count)] : string.Empty)
                    : avatar,
                avatarColor = NeutralColor(rng),
                ability = JobToAbility.TryGetValue(t.job, out var a) ? a : TenantAbility.None,
                activityType = RollActivityType(rng),
                tier = TenantErosionTier.Any,
                shortDescription = copy ?? string.Empty,
                detailedDescription = string.Empty,
            });
        }
    }

    private static string PickAny(System.Random rng, List<string> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }

    // ---------- 池内容运行时增删改（下次 BuildRun 生效） ----------
    public static void AddName(string name, string gender)
    {
        EnsurePool();
        _pool.names.Add(new NameEntry { id = name, gender = string.IsNullOrEmpty(gender) ? "m" : gender });
    }

    public static bool RemoveName(string name)
    {
        EnsurePool();
        return _pool.names.RemoveAll(n => n.id == name) > 0;
    }

    public static void AddCopyEntry(string job, string tier, string text)
    {
        EnsurePool();
        _pool.copy.Add(new CopyEntry { job = job, tier = tier, text = text });
    }

    public static bool RemoveCopyEntries(Predicate<CopyEntry> predicate)
    {
        EnsurePool();
        return _pool.copy.RemoveAll(predicate) > 0;
    }

    public static void AddSpecialTemplate(SpecialTemplate template)
    {
        EnsurePool();
        if (_pool.specials == null) _pool.specials = new List<SpecialTemplate>();
        _pool.specials.Add(template);
    }

    public static bool RemoveSpecialTemplate(string templateId)
    {
        EnsurePool();
        if (_pool.specials == null) return false;
        return _pool.specials.RemoveAll(t => t != null && t.id == templateId) > 0;
    }

    private static void EnsurePool()
    {
        if (!TryLoad()) _pool = new PoolData();
    }
}
