using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public static class NoticeTextFormatter
{
    private static readonly Dictionary<string, string> DefaultResourceNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "food", "食物" },
            { "currency", "货币" },
            { "ingredients", "食材" },
            { "resources", "物资" },
            { "medicine", "药品" }
        };

    public static Dictionary<string, int> MergeDeltas(params IReadOnlyDictionary<string, int>[] sources)
    {
        var merged = new Dictionary<string, int>(StringComparer.Ordinal);
        if (sources == null)
            return merged;
        for (int s = 0; s < sources.Length; s++)
        {
            if (sources[s] == null)
                continue;
            foreach (KeyValuePair<string, int> pair in sources[s])
            {
                merged[pair.Key] = merged.TryGetValue(pair.Key, out int current)
                    ? current + pair.Value
                    : pair.Value;
            }
        }
        return merged;
    }

    public static string FormatHalfDaySettlement(
        IReadOnlyDictionary<string, int> deltas,
        Func<string, string> nameResolver = null)
    {
        List<string> parts = BuildResourceParts(deltas, nameResolver);
        if (parts.Count == 0)
            return string.Empty;
        return $"半天结算：{string.Join("，", parts)}";
    }

    public static string BuildEventNotice(
        IReadOnlyList<EventEffect> effects,
        IReadOnlyList<RunChange> changes,
        GameRunState state,
        string ownerTenantId,
        float negativeEffectMultiplier,
        Func<string, string> nameResolver = null)
    {
        var parts = new List<string>();
        var resourceDeltas = new Dictionary<string, int>(StringComparer.Ordinal);
        var buffParts = new List<string>();

        if (changes != null)
        {
            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i] is AdjustResourceChange resource)
                {
                    resourceDeltas[resource.ResourceId] = resourceDeltas.TryGetValue(resource.ResourceId, out int current)
                        ? current + resource.Delta
                        : resource.Delta;
                }
                else if (changes[i] is AddBuffChange buff)
                {
                    string buffText = FormatBuffNotice(buff.Value, nameResolver);
                    if (!string.IsNullOrEmpty(buffText))
                        buffParts.Add(buffText);
                }
            }
        }

        parts.AddRange(BuildResourceParts(resourceDeltas, nameResolver));

        if (effects != null && state != null)
        {
            var erosionCounts = new Dictionary<(string Prefix, float Delta), int>();
            var erosionOrder = new List<(string Prefix, float Delta)>();
            for (int i = 0; i < effects.Count; i++)
            {
                EventEffect effect = effects[i];
                if (effect == null || effect.effectType != EffectType.ModifyTenantErosion)
                    continue;
                float delta = effect.floatValue > 0f
                    ? effect.floatValue * Mathf.Clamp01(negativeEffectMultiplier)
                    : effect.floatValue;
                if (delta == 0f)
                    continue;
                List<string> targets = EventEffectExecutor.ResolveTargets(
                    effect.target, state, ownerTenantId, effect.intValue, i, RoomFloorRegistry.Instance);
                if (targets == null || targets.Count == 0)
                    continue;
                string prefix = TargetPrefix(effect.target);
                var key = (prefix, delta);
                if (erosionCounts.TryGetValue(key, out int count))
                    erosionCounts[key] = count + targets.Count;
                else
                {
                    erosionCounts[key] = targets.Count;
                    erosionOrder.Add(key);
                }
            }
            for (int i = 0; i < erosionOrder.Count; i++)
            {
                (string prefix, float delta) = erosionOrder[i];
                int count = erosionCounts[erosionOrder[i]];
                string text = string.IsNullOrEmpty(prefix)
                    ? $"侵蚀度 {FormatFloat(delta)}"
                    : $"{prefix}侵蚀度 {FormatFloat(delta)}";
                if (count > 1)
                    text += $"（{count}人）";
                parts.Add(text);
            }
        }

        parts.AddRange(buffParts);
        if (parts.Count == 0)
            return string.Empty;
        return $"事件效果：{string.Join("，", parts)}";
    }

    private static List<string> BuildResourceParts(
        IReadOnlyDictionary<string, int> deltas,
        Func<string, string> nameResolver)
    {
        var parts = new List<string>();
        if (deltas == null || deltas.Count == 0)
            return parts;

        var sorted = new List<string>();
        foreach (KeyValuePair<string, int> pair in deltas)
        {
            if (pair.Value == 0)
                continue;
            sorted.Add(pair.Key);
        }
        sorted.Sort((a, b) =>
        {
            int keyA = ResourceSortKey(a);
            int keyB = ResourceSortKey(b);
            if (keyA != keyB)
                return keyA.CompareTo(keyB);
            return string.CompareOrdinal(a, b);
        });

        for (int i = 0; i < sorted.Count; i++)
            parts.Add($"{ResolveResourceName(sorted[i], nameResolver)} {FormatInt(deltas[sorted[i]])}");
        return parts;
    }

    private static string FormatBuffNotice(BuffRunState buff, Func<string, string> nameResolver)
    {
        if (buff == null)
            return string.Empty;
        string duration = buff.RemainingTicks > 0 ? $"剩余 {buff.RemainingTicks} 天" : "持续生效";
        var tickParts = new List<string>();
        if (buff.ErosionPerTick != 0f)
            tickParts.Add($"每周期侵蚀度 {FormatFloat(buff.ErosionPerTick)}");
        if (buff.ResourceDeltaPerTick != 0 && !string.IsNullOrEmpty(buff.ResourceId))
            tickParts.Add($"每周期{ResolveResourceName(buff.ResourceId, nameResolver)} {FormatInt(buff.ResourceDeltaPerTick)}");
        string detail = tickParts.Count > 0
            ? $"（{duration}，{string.Join("，", tickParts)}）"
            : $"（{duration}）";
        string prefix = TargetPrefix(buff.Target);
        return string.IsNullOrEmpty(prefix) ? $"获得状态{detail}" : $"获得{prefix}状态{detail}";
    }

    private static string TargetPrefix(EffectTarget target)
    {
        switch (target)
        {
            case EffectTarget.AllAssignedTenants: return "全楼";
            case EffectTarget.SameRoomOtherTenants: return "同房";
            case EffectTarget.SameFloorTenants: return "同层";
            case EffectTarget.ByPlayerFlag: return "指定";
            case EffectTarget.RandomAssignedTenants: return "随机";
            default: return string.Empty;
        }
    }

    private static int ResourceSortKey(string resourceId)
    {
        if (resourceId == "food")
            return 0;
        if (resourceId == "currency")
            return 1;
        return 2;
    }

    private static string ResolveResourceName(string resourceId, Func<string, string> nameResolver)
    {
        if (!string.IsNullOrEmpty(resourceId) && nameResolver != null)
        {
            string resolved = nameResolver(resourceId);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }
        if (!string.IsNullOrEmpty(resourceId) && DefaultResourceNames.TryGetValue(resourceId, out string name))
            return name;
        return string.IsNullOrEmpty(resourceId) ? string.Empty : resourceId;
    }

    private static string FormatInt(int value)
    {
        return value.ToString("+#;-#;0", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("+#.#;-#.#;0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
