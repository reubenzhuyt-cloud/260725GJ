using System.Collections.Generic;
using Hotel.Runtime;

/// <summary>
/// Evaluates the state-dependent eligibility conditions authored in TriggerSpec
/// against the live GameRunState.
///
///   Green: TrueErosion < 30, Yellow: 30..70, Red: > 70
///
/// Conditions that depend on systems with no state yet (e.g. IsStorm until the
/// weather system exists) simply never pass
/// </summary>
public static class EventConditionEvaluator
{
    public const float YellowThreshold = 31f;
    public const float RedThreshold = 61f;

    public enum ErosionColor
    {
        Green,
        Yellow,
        Red
    }

    public static ErosionColor ColorOf(float erosion)
    {
        if (erosion >= RedThreshold) return ErosionColor.Red;
        if (erosion >= YellowThreshold) return ErosionColor.Yellow;
        return ErosionColor.Green;
    }

    /// <summary>
    /// True when the trigger's condition list passes. An empty list is always
    /// eligible. When requireAll is true every condition must pass, otherwise a
    /// single passing condition is enough. A null state makes any non-empty
    /// condition list fail.
    /// </summary>
    public static bool Matches(
        TriggerSpec trigger,
        GameRunState state,
        IReadOnlyList<TenantReviewCandidateSO> candidates,
        RoomFloorRegistry floorRegistry)
    {
        if (trigger == null) return true;
        List<EventCondition> conditions = trigger.conditions;
        if (conditions == null || conditions.Count == 0) return true;
        if (state == null) return false;

        if (trigger.requireAll)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!Evaluate(conditions[i], state, candidates, floorRegistry))
                    return false;
            }
            return true;
        }

        for (int i = 0; i < conditions.Count; i++)
        {
            if (Evaluate(conditions[i], state, candidates, floorRegistry))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Weight multiplier for TriggerSpec.weightScale: baseWeight * max(1, count).
    /// Returns 1f when scaling is disabled.
    /// </summary>
    public static float ComputeWeightModifier(TriggerSpec trigger, GameRunState state)
    {
        if (trigger == null || state == null || state.Tenants == null)
            return 1f;

        int count;
        switch (trigger.weightScale)
        {
            case EventWeightScale.RedTenantCount:
                count = CountColor(state, ErosionColor.Red);
                break;
            case EventWeightScale.YellowTenantCount:
                count = CountColor(state, ErosionColor.Yellow);
                break;
            default:
                return 1f;
        }

        return count > 1 ? count : 1f;
    }

    private static bool Evaluate(
        EventCondition c,
        GameRunState state,
        IReadOnlyList<TenantReviewCandidateSO> candidates,
        RoomFloorRegistry floorRegistry)
    {
        if (c == null) return false;

        switch (c.condition)
        {
            case ConditionType.None:
                return true;

            case ConditionType.YellowTenantExists:
                return CountColor(state, ErosionColor.Yellow) > 0;

            case ConditionType.RedTenantExists:
                return CountColor(state, ErosionColor.Red) > 0;

            case ConditionType.RedCountAtLeast:
                return CountColor(state, ErosionColor.Red) >= c.intValue;

            case ConditionType.YellowCountAtLeast:
                return CountColor(state, ErosionColor.Yellow) >= c.intValue;

            case ConditionType.GreenRedSameFloor:
                return HasColorPairSameFloor(state, ErosionColor.Green, ErosionColor.Red, floorRegistry);

            case ConditionType.RedYellowSameFloor:
                return HasColorPairSameFloor(state, ErosionColor.Red, ErosionColor.Yellow, floorRegistry);

            case ConditionType.TenantErosionAbove:
                return AnyErosionAbove(state, c.floatValue);

            case ConditionType.TenantErosionBelow:
                return AnyErosionBelow(state, c.floatValue);

            case ConditionType.FoodBelowDays:
                return FoodBelowDays(state, c.intValue);

            case ConditionType.FoodOrCurrencyAbove:
                return FoodOrCurrencyAbove(state, c.floatValue);

            case ConditionType.TenantWithAbility:
                return AnyTenantWithAbility(state, candidates, c.stringValue);

            case ConditionType.SpecificTenantPresent:
                return !string.IsNullOrEmpty(c.stringValue) && state.Tenants.ContainsKey(c.stringValue);

            case ConditionType.VulnerableTenantExists:
                return AnyVulnerableTenant(state);

            case ConditionType.HotelHasMirror:
                return state.HotelHasMirror;

            case ConditionType.IsStorm:
                return state.IsStorm;

            default:
                return false;
        }
    }

    private static int CountColor(GameRunState state, ErosionColor color)
    {
        if (state.Tenants == null) return 0;
        int count = 0;
        foreach (TenantRunState tenant in state.Tenants.Values)
        {
            if (tenant != null && ColorOf(tenant.TrueErosion) == color)
                count++;
        }
        return count;
    }

    private static bool AnyErosionAbove(GameRunState state, float threshold)
    {
        if (state.Tenants == null) return false;
        foreach (TenantRunState tenant in state.Tenants.Values)
        {
            if (tenant != null && tenant.TrueErosion > threshold)
                return true;
        }
        return false;
    }

    private static bool AnyErosionBelow(GameRunState state, float threshold)
    {
        if (state.Tenants == null) return false;
        foreach (TenantRunState tenant in state.Tenants.Values)
        {
            if (tenant != null && tenant.TrueErosion < threshold)
                return true;
        }
        return false;
    }

    private static bool FoodBelowDays(GameRunState state, int days)
    {
        if (state.Tenants == null || state.Tenants.Count == 0) return false;
        if (!state.Resources.TryGetValue("food", out ResourceRunState food)) return false;
        return food.Amount < days * state.Tenants.Count;
    }

    private static bool FoodOrCurrencyAbove(GameRunState state, float threshold)
    {
        bool Above(string id)
        {
            return state.Resources.TryGetValue(id, out ResourceRunState r) && r.Amount >= threshold;
        }
        return Above("food") || Above("currency");
    }

    private static bool AnyTenantWithAbility(
        GameRunState state,
        IReadOnlyList<TenantReviewCandidateSO> candidates,
        string abilityName)
    {
        if (state.Tenants == null || state.Tenants.Count == 0) return false;
        if (string.IsNullOrEmpty(abilityName)) return false;

        TenantAbility wanted;
        if (!System.Enum.TryParse(abilityName, true, out wanted))
            return false;
        if (!System.Enum.IsDefined(typeof(TenantAbility), wanted))
            return false;
        if (wanted == TenantAbility.None)
            return false;

        foreach (var pair in state.Tenants)
        {
            TenantAbility ability = ResolveAbility(pair.Key, candidates);
            if (ability == wanted)
                return true;
        }
        return false;
    }

    private static TenantAbility ResolveAbility(string tenantId, IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        if (candidates == null) return TenantAbility.None;
        for (int i = 0; i < candidates.Count; i++)
        {
            TenantReviewCandidateSO candidate = candidates[i];
            if (candidate == null || candidate.candidateId != tenantId)
                continue;
            return candidate.ability;
        }
        return TenantAbility.None;
    }

    private static bool AnyVulnerableTenant(GameRunState state)
    {
        if (state.Tenants == null) return false;
        foreach (TenantRunState tenant in state.Tenants.Values)
        {
            if (tenant != null && tenant.Vulnerable)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True when some tenant of colorA and some tenant of colorB
    /// are assigned to rooms on the same floor. Requires the scene
    /// RoomFloorRegistry; returns false when unavailable or floor-unknown.
    /// </summary>
    private static bool HasColorPairSameFloor(
        GameRunState state,
        ErosionColor colorA,
        ErosionColor colorB,
        RoomFloorRegistry floorRegistry)
    {
        if (state.Tenants == null || state.Tenants.Count < 2) return false;
        if (floorRegistry == null) return false;

        var floorsOf = new Dictionary<ErosionColor, HashSet<int>>();
        foreach (TenantRunState tenant in state.Tenants.Values)
        {
            if (tenant == null || string.IsNullOrEmpty(tenant.RoomId)) continue;
            if (!TryGetFloorForRoom(tenant.RoomId, floorRegistry, out int floor)) continue;

            if (!floorsOf.TryGetValue(ColorOf(tenant.TrueErosion), out HashSet<int> floors))
            {
                floors = new HashSet<int>();
                floorsOf[ColorOf(tenant.TrueErosion)] = floors;
            }
            floors.Add(floor);
        }

        if (!floorsOf.TryGetValue(colorA, out HashSet<int> floorsA)) return false;
        if (!floorsOf.TryGetValue(colorB, out HashSet<int> floorsB)) return false;
        foreach (int floor in floorsA)
        {
            if (floorsB.Contains(floor))
                return true;
        }
        return false;
    }

    private static bool TryGetFloorForRoom(string roomId, RoomFloorRegistry floorRegistry, out int floor)
    {
        floor = 0;
        if (floorRegistry == null || string.IsNullOrEmpty(roomId)) return false;
        IReadOnlyList<RoomTenantAvatarSlot> slots = RoomTenantAvatarSlot.GetSlotsForRoom(roomId);
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && floorRegistry.TryGetFloorForSlot(slots[i], out floor))
                return true;
        }
        return false;
    }
}