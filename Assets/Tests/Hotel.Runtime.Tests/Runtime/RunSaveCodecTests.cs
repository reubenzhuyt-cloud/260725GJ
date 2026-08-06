using System;
using Hotel.Runtime;
using NUnit.Framework;

namespace Hotel.Runtime.Tests
{
    public sealed class RunSaveCodecTests
    {
        [Test]
        public void RoundTrip_PreservesCurrentPlayableRunState()
        {
            var state = GameRunState.New(new RunId("save-test"), 2468);
            state.StateVersion = 17;
            state.Day = 6;
            state.Phase.Current = HotelPhase.Dawn;
            state.Resources["food"] = new ResourceRunState
            {
                ResourceId = "food",
                DefinitionId = "Food",
                Amount = 12
            };
            state.Tenants["tenant_alpha"] = new TenantRunState
            {
                TenantId = "tenant_alpha",
                DefinitionId = "tenant_alpha",
                TrueErosion = 23.5f,
                PlayerMarked = true,
                PlayerFlag = 3,
                RoomId = "room_01",
                JobId = "kitchen"
            };
            state.Rooms["room_01"] = new RoomRunState
            {
                RoomId = "room_01",
                DefinitionId = "room_01",
                OccupantIds = { "tenant_alpha" }
            };
            state.ResolvedReviewCandidateIds.Add("tenant_alpha");
            state.ReviewHistory.Add(new ReviewDecisionRecord
            {
                CandidateId = "tenant_alpha",
                Decision = ReviewDecision.Recruit,
                Day = 1,
                Phase = HotelPhase.Dawn,
                InitialErosion = 23.5f
            });

            var json = RunSaveCodec.ToJson(state, new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc));
            var restored = RunSaveCodec.FromJson(json);

            Assert.That(restored.RunId.Value, Is.EqualTo("save-test"));
            Assert.That(restored.StateVersion, Is.EqualTo(17));
            Assert.That(restored.Day, Is.EqualTo(6));
            Assert.That(restored.Phase.Current, Is.EqualTo(HotelPhase.Dawn));
            Assert.That(restored.Resources["food"].Amount, Is.EqualTo(12));
            Assert.That(restored.Tenants["tenant_alpha"].TrueErosion, Is.EqualTo(23.5f));
            Assert.That(restored.Tenants["tenant_alpha"].RoomId, Is.EqualTo("room_01"));
            Assert.That(restored.Tenants["tenant_alpha"].JobId, Is.EqualTo("kitchen"));
            Assert.That(restored.Tenants["tenant_alpha"].PlayerFlag, Is.EqualTo(3));
            Assert.That(restored.Rooms["room_01"].OccupantIds, Is.EqualTo(new[] { "tenant_alpha" }));
            Assert.That(restored.ResolvedReviewCandidateIds, Is.EqualTo(new[] { "tenant_alpha" }));
            Assert.That(restored.ReviewHistory[0].Decision, Is.EqualTo(ReviewDecision.Recruit));
        }

        [Test]
        public void FromJson_RejectsUnknownSchema()
        {
            const string json = "{\"SchemaVersion\":999,\"RunId\":\"future\"}";
            Assert.Throws<InvalidOperationException>(() => RunSaveCodec.FromJson(json));
        }
    }
}
