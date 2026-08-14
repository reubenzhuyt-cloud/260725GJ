using System.Collections.Generic;
using Hotel.Runtime;

public static class EventAffordability
{
    public static Dictionary<string, int> ComputeResourceCosts(EventEffect[] effects)
    {
        var costs = new Dictionary<string, int>();
        if (effects == null)
            return costs;
        for (int i = 0; i < effects.Length; i++)
        {
            EventEffect effect = effects[i];
            if (effect == null || effect.effectType != EffectType.ModifyResource)
                continue;
            if (string.IsNullOrEmpty(effect.stringValue))
                continue;
            int delta = EventEffectExecutor.SafeToInt(effect.floatValue);
            if (delta >= 0)
                continue;
            int current = costs.TryGetValue(effect.stringValue, out int existing) ? existing : 0;
            costs[effect.stringValue] = current - delta;
        }
        return costs;
    }

    public static bool CanAfford(EventEffect[] effects, GameRunState state)
    {
        if (state == null)
            return true;
        return CanAfford(effects, state.Resources);
    }

    public static bool CanAfford(EventEffect[] effects, IReadOnlyDictionary<string, ResourceRunState> resources)
    {
        Dictionary<string, int> costs = ComputeResourceCosts(effects);
        if (costs.Count == 0)
            return true;
        if (resources == null)
            return false;
        foreach (KeyValuePair<string, int> pair in costs)
        {
            if (!resources.TryGetValue(pair.Key, out ResourceRunState resource))
                return false;
            if (resource.Amount < pair.Value)
                return false;
        }
        return true;
    }
}
