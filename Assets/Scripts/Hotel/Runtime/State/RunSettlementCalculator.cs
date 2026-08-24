using System;
using System.Collections.Generic;

namespace Hotel.Runtime
{
    /// <summary>
    /// Pure end-of-run calculation based on GD_Hotel.pdf sections 7.1 and 7.2.
    /// It does not mutate the run state, so callers can safely inspect a result before committing it.
    /// </summary>
    public static class RunSettlementCalculator
    {
        public const int FinalDay = 30;

        private static readonly string[] TruthItemIds = { "T01", "T02", "T07" };

        public static RunSummaryState Calculate(GameRunState state, bool requireCompletedChain = true)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var summary = new RunSummaryState
            {
                IsComplete = true,
                CompletedDay = state.Day
            };

            CalculateTenantStatistics(state.Tenants, summary);
            summary.TruthItemCount = CountTruthItems(state.Inventory);
            summary.CompletedChainCount = CountCompletedChains(state.Chains);
            summary.Ending = DetermineEnding(summary, requireCompletedChain);
            return summary;
        }

        public static RunEnding DetermineEnding(RunSummaryState summary, bool requireCompletedChain = true)
        {
            if (summary == null)
                throw new ArgumentNullException(nameof(summary));

            bool isGood = summary.FinalTenantCount >= 5 && summary.AverageErosion < 40f;
            bool hasTruthRequirements = summary.TruthItemCount >= 3
                && (!requireCompletedChain || summary.CompletedChainCount >= 1);

            if (isGood && hasTruthRequirements)
                return RunEnding.Truth;
            if (isGood)
                return RunEnding.Good;
            if (summary.FinalTenantCount >= 3 && summary.AverageErosion < 60f)
                return RunEnding.Normal;
            return RunEnding.Bad;
        }

        public static int GetTrueColorFlag(float erosion)
        {
            if (erosion <= 30f) return 1;
            if (erosion <= 60f) return 2;
            return 3;
        }

        private static void CalculateTenantStatistics(
            IReadOnlyDictionary<string, TenantRunState> tenants,
            RunSummaryState summary)
        {
            if (tenants == null || tenants.Count == 0)
                return;

            float erosionTotal = 0f;
            bool hasTenant = false;

            foreach (KeyValuePair<string, TenantRunState> pair in tenants)
            {
                TenantRunState tenant = pair.Value;
                if (tenant == null)
                    continue;

                float erosion = ClampErosion(tenant.TrueErosion);
                summary.FinalTenantCount++;
                erosionTotal += erosion;

                if (!hasTenant || erosion > summary.HighestErosion
                    || (ApproximatelyEqual(erosion, summary.HighestErosion)
                        && string.CompareOrdinal(pair.Key, summary.HighestErosionTenantId) < 0))
                {
                    summary.HighestErosion = erosion;
                    summary.HighestErosionTenantId = pair.Key;
                }

                if (!hasTenant || erosion < summary.LowestErosion
                    || (ApproximatelyEqual(erosion, summary.LowestErosion)
                        && string.CompareOrdinal(pair.Key, summary.LowestErosionTenantId) < 0))
                {
                    summary.LowestErosion = erosion;
                    summary.LowestErosionTenantId = pair.Key;
                }

                hasTenant = true;

                // 0 means "unknown"; only explicit green/yellow/red guesses are scored.
                if (tenant.PlayerFlag < 1 || tenant.PlayerFlag > 3)
                    continue;

                summary.ClassifiedTenantCount++;
                if (tenant.PlayerFlag != GetTrueColorFlag(erosion))
                    summary.MisclassificationCount++;
            }

            if (summary.FinalTenantCount > 0)
                summary.AverageErosion = erosionTotal / summary.FinalTenantCount;
            if (summary.ClassifiedTenantCount > 0)
                summary.MisclassificationRate = (float)summary.MisclassificationCount
                    / summary.ClassifiedTenantCount;
        }

        private static int CountTruthItems(IReadOnlyDictionary<string, int> inventory)
        {
            if (inventory == null)
                return 0;

            int count = 0;
            for (int i = 0; i < TruthItemIds.Length; i++)
            {
                if (inventory.TryGetValue(TruthItemIds[i], out int amount) && amount > 0)
                    count++;
            }
            return count;
        }

        private static int CountCompletedChains(IReadOnlyDictionary<string, ChainRunState> chains)
        {
            if (chains == null)
                return 0;

            int count = 0;
            foreach (ChainRunState chain in chains.Values)
            {
                if (chain != null && chain.Completed && !chain.Failed)
                    count++;
            }
            return count;
        }

        private static float ClampErosion(float erosion)
        {
            if (float.IsNaN(erosion) || float.IsInfinity(erosion))
                return 0f;
            if (erosion < 0f) return 0f;
            return erosion > 100f ? 100f : erosion;
        }

        private static bool ApproximatelyEqual(float left, float right)
        {
            return Math.Abs(left - right) < 0.0001f;
        }
    }
}
