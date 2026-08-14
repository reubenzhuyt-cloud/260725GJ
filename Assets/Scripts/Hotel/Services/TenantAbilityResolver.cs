using System.Collections.Generic;
using Hotel.Runtime;

public static class TenantAbilityResolver
{
    public static HashSet<TenantAbility> GetOwnedAbilities(GameRunState state, IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        var owned = new HashSet<TenantAbility>();
        if (state == null || state.Tenants == null)
            return owned;
        foreach (KeyValuePair<string, TenantRunState> pair in state.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Key))
                continue;
            TenantAbility ability = ResolveAbility(pair.Key, candidates);
            if (ability != TenantAbility.None)
                owned.Add(ability);
        }
        return owned;
    }

    public static TenantAbility ResolveAbility(string tenantId, IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        if (candidates == null)
            return TenantAbility.None;
        for (int i = 0; i < candidates.Count; i++)
        {
            TenantReviewCandidateSO candidate = candidates[i];
            if (candidate == null || candidate.candidateId != tenantId)
                continue;
            return candidate.ability;
        }
        return TenantAbility.None;
    }

    public static bool HasAllRequiredTags(TenantAbility[] required, HashSet<TenantAbility> owned)
    {
        if (required == null || required.Length == 0)
            return true;
        if (owned == null)
            return false;
        foreach (TenantAbility tag in required)
        {
            if (!owned.Contains(tag))
                return false;
        }
        return true;
    }

    public static bool HasAllRequiredTags(TenantAbility[] required, GameRunState state, IReadOnlyList<TenantReviewCandidateSO> candidates)
    {
        return HasAllRequiredTags(required, GetOwnedAbilities(state, candidates));
    }
}
