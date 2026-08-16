using System;
using System.Collections.Generic;

namespace Hotel.Runtime
{
    /// <summary>侵蚀档位（对应颜色标签区间，策划 4.4.2 / 4.2.5）。</summary>
    public enum TenantErosionTier
    {
        Any,    // 全范围 0-70
        Green,  // 0-30
        Yellow, // 31-60
        Red,    // 61-70
    }

    [Serializable]
    public readonly struct VisitorArrival
    {
        public VisitorArrival(int day, HotelPhase phase, int visitorCount)
        {
            Day = day;
            Phase = phase;
            VisitorCount = visitorCount;
        }

        public int Day { get; }
        public HotelPhase Phase { get; }
        public int VisitorCount { get; }
    }

    public static class VisitorArrivalScheduler
    {
        public static float GetInitialErosion(int seed, string candidateId)
        {
            return GetInitialErosion(seed, candidateId, TenantErosionTier.Any);
        }

        /// <summary>按档位生成初始侵蚀度；Any 为全范围 0-70（策划 v1.4 更新，原 0-40）。</summary>
        public static float GetInitialErosion(int seed, string candidateId, TenantErosionTier tier)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
                throw new ArgumentException("A candidate ID is required.", nameof(candidateId));

            unchecked
            {
                var stableHash = 17;
                for (var index = 0; index < candidateId.Length; index++)
                    stableHash = stableHash * 31 + candidateId[index];

                var random = new Random(seed ^ stableHash);
                switch (tier)
                {
                    case TenantErosionTier.Green: return random.Next(0, 31);
                    case TenantErosionTier.Yellow: return random.Next(31, 61);
                    case TenantErosionTier.Red: return random.Next(61, 71);
                    default: return random.Next(0, 71);
                }
            }
        }

        public static IReadOnlyList<VisitorArrival> CreateSchedule(int seed, int totalVisitors, int lastDay = 30)
        {
            if (totalVisitors < 0)
                throw new ArgumentOutOfRangeException(nameof(totalVisitors));
            if (lastDay < 1)
                throw new ArgumentOutOfRangeException(nameof(lastDay));

            var schedule = new List<VisitorArrival>();
            if (totalVisitors == 0)
                return schedule;

            var random = new Random(seed);
            var remaining = totalVisitors;
            var firstCount = Math.Min(remaining, random.Next(2, 4));
            schedule.Add(new VisitorArrival(1, HotelPhase.Dawn, firstCount));
            remaining -= firstCount;

            var day = 1;
            while (remaining > 0)
            {
                day += random.Next(1, 4);
                if (day > lastDay)
                    break;

                var phase = random.Next(0, 2) == 0 ? HotelPhase.Dawn : HotelPhase.Dusk;
                var count = Math.Min(remaining, random.Next(1, 4));
                schedule.Add(new VisitorArrival(day, phase, count));
                remaining -= count;
            }

            return schedule;
        }
    }
}