using Hotel.Runtime;
using NUnit.Framework;

namespace Hotel.Runtime.Tests
{
    public sealed class VisitorArrivalSchedulerTests
    {
        [Test]
        public void FirstBatch_IsDayOneDawnWithTwoOrThreeVisitors()
        {
            var schedule = VisitorArrivalScheduler.CreateSchedule(17, 20);

            Assert.That(schedule, Is.Not.Empty);
            Assert.That(schedule[0].Day, Is.EqualTo(1));
            Assert.That(schedule[0].Phase, Is.EqualTo(HotelPhase.Dawn));
            Assert.That(schedule[0].VisitorCount, Is.InRange(2, 3));
        }

        [Test]
        public void SameSeed_ProducesSameSchedule()
        {
            var first = VisitorArrivalScheduler.CreateSchedule(42, 20);
            var second = VisitorArrivalScheduler.CreateSchedule(42, 20);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (var index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].Day, Is.EqualTo(first[index].Day));
                Assert.That(second[index].Phase, Is.EqualTo(first[index].Phase));
                Assert.That(second[index].VisitorCount, Is.EqualTo(first[index].VisitorCount));
            }
        }

        [Test]
        public void LaterBatches_RespectFrequencyPhaseAndSizeRules()
        {
            var schedule = VisitorArrivalScheduler.CreateSchedule(91, 20);

            for (var index = 1; index < schedule.Count; index++)
            {
                Assert.That(schedule[index].Day - schedule[index - 1].Day, Is.InRange(1, 3));
                Assert.That(
                    schedule[index].Phase == HotelPhase.Dawn || schedule[index].Phase == HotelPhase.Dusk,
                    Is.True);
                Assert.That(schedule[index].VisitorCount, Is.InRange(1, 3));
            }
        }

        [Test]
        public void Schedule_NeverUsesMoreVisitorsThanExist()
        {
            const int availableVisitors = 7;
            var schedule = VisitorArrivalScheduler.CreateSchedule(8, availableVisitors);
            var scheduledVisitors = 0;

            foreach (var arrival in schedule)
                scheduledVisitors += arrival.VisitorCount;

            Assert.That(scheduledVisitors, Is.LessThanOrEqualTo(availableVisitors));
        }

        [Test]
        public void Schedule_DoesNotCreateArrivalsAfterLastDay()
        {
            const int lastDay = 3;
            var schedule = VisitorArrivalScheduler.CreateSchedule(5, 100, lastDay);

            foreach (var arrival in schedule)
                Assert.That(arrival.Day, Is.LessThanOrEqualTo(lastDay));
        }

        [Test]
        public void InitialErosion_IsStableAndWithinVisitorRange()
        {
            var first = VisitorArrivalScheduler.GetInitialErosion(12, "tenant_alpha");
            var second = VisitorArrivalScheduler.GetInitialErosion(12, "tenant_alpha");

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.InRange(0f, 40f));
        }
    }
}
