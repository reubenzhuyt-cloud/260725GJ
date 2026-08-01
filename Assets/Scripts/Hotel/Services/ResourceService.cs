using Hotel.Runtime;

public static class ResourceService
{
    public static int GetAmount(GameRunState state, string resourceId)
    {
        if (state == null) return 0;
        if (!state.Resources.TryGetValue(resourceId, out var res)) return 0;
        return res.Amount;
    }

    public static CommitResult TryAdjust(
        GameRunState state,
        StateReducer reducer,
        string resourceId,
        int delta,
        string authorizer,
        ResourceAdjustedEvent channel)
    {
        if (state == null || reducer == null)
            return new CommitResult(false);

        if (!state.Resources.ContainsKey(resourceId))
            return new CommitResult(false);

        var changeSet = AuthorizedChangeSet.Domain(
            state.RunId,
            state.StateVersion,
            authorizer,
            "ResourceAdjust");
        changeSet.Add(new AdjustResourceChange(resourceId, delta));

        CommitResult result = reducer.TryCommit(state, changeSet);

        if (result.Succeeded && channel != null)
        {
            channel.Raise(new ResourceAdjustedData
            {
                resourceId = resourceId,
                delta = delta,
                newAmount = state.Resources[resourceId].Amount
            });
        }

        return result;
    }
}
