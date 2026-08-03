using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public sealed class StateReducerTests
    {
        [Test]
        public void CoordinatorLifecycleCommit_IsReducerOnlyMutation()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Coordinator(state.RunId, state.StateVersion, "enter");
            set.Add(new SetPhaseLifecycleChange(PhaseLifecycleState.Entered));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.StateVersion, Is.EqualTo(1));
            Assert.That(state.Phase.Lifecycle, Is.EqualTo(PhaseLifecycleState.Entered));
        }

        [Test]
        public void DomainSet_CannotSubmitPhaseLifecycle()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "events", "bad");
            set.Add(new SetPhaseLifecycleChange(PhaseLifecycleState.Settled));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Phase.Lifecycle, Is.EqualTo(PhaseLifecycleState.Entered));
        }

        [Test]
        public void DomainSet_CannotSubmitCurrentPhase()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "events", "bad");
            set.Add(new SetCurrentPhaseChange(HotelPhase.Day, 1, 1));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Phase.Current, Is.EqualTo(HotelPhase.Dawn));
        }

        [Test]
        public void DomainSet_CannotSubmitRunSummary()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "events", "bad");
            set.Add(new SetRunSummaryChange(new RunSummaryState { IsComplete = true }));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Summary.IsComplete, Is.False);
        }

        [Test]
        public void MixedValidPlusMissingTenantChange_HasNoWrites()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Coordinator(state.RunId, state.StateVersion, "enter");
            set.Add(new SetPhaseLifecycleChange(PhaseLifecycleState.Entered));
            set.Add(new AdjustTenantErosionChange("ghost-tenant", 5f));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Phase.Lifecycle, Is.EqualTo(PhaseLifecycleState.Entered));
        }

        [Test]
        public void DuplicateEventId_RejectsChange()
        {
            var state = GameRunState.New(new RunId("r"));
            var record = new EventHistoryRecord { EventId = "ev-1", DefinitionId = "def-1", Day = 1, Phase = HotelPhase.Dawn, Occurrence = 1 };
            state.EventHistory.Add(record);

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "events", "plan");
            set.Add(new PlanEventHistoryChange(new EventHistoryRecord { EventId = "ev-1", DefinitionId = "def-2", Day = 1, Phase = HotelPhase.Day, Occurrence = 1 }));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.EventHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public void ResolvingAbsentEvent_RejectsChange()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "events", "resolve");
            set.Add(new ResolveEventHistoryChange("no-such-event", "opt-a"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.EventHistory.Count, Is.EqualTo(0));
        }

        [Test]
        public void CompletingAlreadyCompletedDecision_RejectsChange()
        {
            var state = GameRunState.New(new RunId("r"));
            var decision = new DecisionRunState { DecisionId = "d-1", Phase = HotelPhase.Day, Day = 1, IsBlocking = true, IsCompleted = true };
            state.Decisions.Add(decision);

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "events", "complete");
            set.Add(new CompleteDecisionChange("d-1"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Decisions[0].IsCompleted, Is.True);
        }

        [Test]
        public void RunIdMismatch_RejectsChange()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Coordinator(new RunId("other"), state.StateVersion, "enter");
            set.Add(new SetPhaseLifecycleChange(PhaseLifecycleState.Entered));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Phase.Lifecycle, Is.EqualTo(PhaseLifecycleState.Entered));
        }

        [Test]
        public void VersionMismatch_RejectsChange()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Coordinator(state.RunId, state.StateVersion + 1, "enter");
            set.Add(new SetPhaseLifecycleChange(PhaseLifecycleState.Entered));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Phase.Lifecycle, Is.EqualTo(PhaseLifecycleState.Entered));
        }

        [Test]
        public void ValidOrderedRoomAndResourceCommit_Succeeds()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Tenants["t-1"] = new TenantRunState { TenantId = "t-1", DefinitionId = "td-1" };
            state.Rooms["rm-1"] = new RoomRunState { RoomId = "rm-1", DefinitionId = "rd-1" };
            state.Resources["res-1"] = new ResourceRunState { ResourceId = "res-1", DefinitionId = "resd-1", Amount = 10 };

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "rooms", "assign");
            set.Add(new AssignRoomChange("t-1", "rm-1"));
            set.Add(new AdjustResourceChange("res-1", -3));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.StateVersion, Is.EqualTo(1));
            Assert.That(state.Tenants["t-1"].RoomId, Is.EqualTo("rm-1"));
            Assert.That(state.Rooms["rm-1"].OccupantIds, Does.Contain("t-1"));
            Assert.That(state.Resources["res-1"].Amount, Is.EqualTo(7));
        }

        [Test]
        public void Erosion_LowerClampedToZero()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Tenants["t-1"] = new TenantRunState { TenantId = "t-1", DefinitionId = "td-1", TrueErosion = 5f };

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "erosion", "adjust");
            set.Add(new AdjustTenantErosionChange("t-1", -20f));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.Tenants["t-1"].TrueErosion, Is.EqualTo(0f));
        }

        [Test]
        public void Erosion_UpperClampedToHundred()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Tenants["t-1"] = new TenantRunState { TenantId = "t-1", DefinitionId = "td-1", TrueErosion = 95f };

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "erosion", "adjust");
            set.Add(new AdjustTenantErosionChange("t-1", 20f));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.Tenants["t-1"].TrueErosion, Is.EqualTo(100f));
        }

        [Test]
        public void AuditLog_AppendsWithCommit()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "system", "log");
            set.Add(new AppendAuditLogChange("first entry"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.AuditLog.Count, Is.EqualTo(1));
            Assert.That(state.AuditLog[0], Is.EqualTo("first entry"));
            Assert.That(state.StateVersion, Is.EqualTo(1));
        }

        [Test]
        public void MissingTenant_RejectsRoomAssignment()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Rooms["rm-1"] = new RoomRunState { RoomId = "rm-1", DefinitionId = "rd-1" };

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "rooms", "assign");
            set.Add(new AssignRoomChange("no-tenant", "rm-1"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Rooms["rm-1"].OccupantIds.Count, Is.EqualTo(0));
        }

        [Test]
        public void MissingRoom_RejectsRoomAssignment()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Tenants["t-1"] = new TenantRunState { TenantId = "t-1", DefinitionId = "td-1" };

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "rooms", "assign");
            set.Add(new AssignRoomChange("t-1", "no-room"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Tenants["t-1"].RoomId, Is.Null);
        }

        [Test]
        public void MissingResource_RejectsResourceAdjust()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "resources", "adjust");
            set.Add(new AdjustResourceChange("no-resource", 5));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateRoomAssignment_InOneChangeset_RejectsAtomically()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Tenants["t-1"] = new TenantRunState { TenantId = "t-1", DefinitionId = "td-1" };
            state.Rooms["rm-1"] = new RoomRunState { RoomId = "rm-1", DefinitionId = "rd-1" };

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "rooms", "assign");
            set.Add(new AssignRoomChange("t-1", "rm-1"));
            set.Add(new AssignRoomChange("t-1", "rm-1"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.Tenants["t-1"].RoomId, Is.Null);
            Assert.That(state.Rooms["rm-1"].OccupantIds.Count, Is.EqualTo(0));
        }

        [Test]
        public void ReassignSameRoom_DoesNotDuplicateOccupant()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Tenants["t-1"] = new TenantRunState { TenantId = "t-1", DefinitionId = "td-1" };
            state.Rooms["rm-1"] = new RoomRunState { RoomId = "rm-1", DefinitionId = "rd-1" };

            var firstSet = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "rooms", "assign");
            firstSet.Add(new AssignRoomChange("t-1", "rm-1"));
            new StateReducer().TryCommit(state, firstSet);

            var secondSet = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "rooms", "reassign");
            secondSet.Add(new AssignRoomChange("t-1", "rm-1"));

            var result = new StateReducer().TryCommit(state, secondSet);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.StateVersion, Is.EqualTo(2));
            Assert.That(state.Rooms["rm-1"].OccupantIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void MultiChangeSet_IncrementsVersionExactlyOnce()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Tenants["t-1"] = new TenantRunState { TenantId = "t-1", DefinitionId = "td-1" };
            state.Rooms["rm-1"] = new RoomRunState { RoomId = "rm-1", DefinitionId = "rd-1" };
            state.Resources["res-1"] = new ResourceRunState { ResourceId = "res-1", DefinitionId = "resd-1", Amount = 10 };

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "system", "multi");
            set.Add(new AssignRoomChange("t-1", "rm-1"));
            set.Add(new AdjustResourceChange("res-1", 5));
            set.Add(new AdjustTenantErosionChange("t-1", 10f));
            set.Add(new AppendAuditLogChange("multi change"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.StateVersion, Is.EqualTo(1));
        }

        [Test]
        public void AddTenant_SucceedsWhenNotDuplicate()
        {
            var state = GameRunState.New(new RunId("r"));

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "confirm");
            set.Add(new AddTenantChange("tenant_new", "tenant_new", 27f));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.Tenants.ContainsKey("tenant_new"), Is.True);
            Assert.That(state.Tenants["tenant_new"].TenantId, Is.EqualTo("tenant_new"));
            Assert.That(state.Tenants["tenant_new"].TrueErosion, Is.EqualTo(27f));
        }

        [Test]
        public void AddTenant_RejectsDuplicateId()
        {
            var state = GameRunState.New(new RunId("r"));
            state.Tenants["tenant_existing"] = new TenantRunState { TenantId = "tenant_existing", DefinitionId = "tenant_existing" };

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "confirm");
            set.Add(new AddTenantChange("tenant_existing", "tenant_existing"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
        }

        [Test]
        public void ResolveCandidate_SucceedsWhenNotDuplicate()
        {
            var state = GameRunState.New(new RunId("r"));

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "resolve");
            set.Add(new ResolveCandidateChange("candidate_01"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.ResolvedReviewCandidateIds, Does.Contain("candidate_01"));
        }

        [Test]
        public void ResolveCandidate_RejectsAlreadyResolved()
        {
            var state = GameRunState.New(new RunId("r"));
            state.ResolvedReviewCandidateIds.Add("candidate_01");

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "resolve");
            set.Add(new ResolveCandidateChange("candidate_01"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
        }

        [Test]
        public void ConfirmCandidate_AddsTenantAndResolvesAtomically()
        {
            var state = GameRunState.New(new RunId("r"));

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "confirm");
            set.Add(new AddTenantChange("tenant_alpha", "tenant_alpha", 31f));
            set.Add(new ResolveCandidateChange(new ReviewDecisionRecord
            {
                CandidateId = "tenant_alpha",
                Decision = ReviewDecision.Recruit,
                Day = 1,
                Phase = HotelPhase.Dawn,
                InitialErosion = 31f
            }));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.StateVersion, Is.EqualTo(1));
            Assert.That(state.Tenants.ContainsKey("tenant_alpha"), Is.True);
            Assert.That(state.Tenants["tenant_alpha"].TrueErosion, Is.EqualTo(31f));
            Assert.That(state.ResolvedReviewCandidateIds, Does.Contain("tenant_alpha"));
            Assert.That(state.ReviewHistory.Count, Is.EqualTo(1));
            Assert.That(state.ReviewHistory[0].Decision, Is.EqualTo(ReviewDecision.Recruit));
        }

        [Test]
        public void RejectCandidate_ResolvesWithoutAddingTenant()
        {
            var state = GameRunState.New(new RunId("r"));

            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "reject");
            set.Add(new ResolveCandidateChange(new ReviewDecisionRecord
            {
                CandidateId = "tenant_beta",
                Decision = ReviewDecision.Reject,
                Day = 1,
                Phase = HotelPhase.Dawn,
                InitialErosion = 12f
            }));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(state.StateVersion, Is.EqualTo(1));
            Assert.That(state.Tenants.ContainsKey("tenant_beta"), Is.False);
            Assert.That(state.ResolvedReviewCandidateIds, Does.Contain("tenant_beta"));
            Assert.That(state.ReviewHistory.Count, Is.EqualTo(1));
            Assert.That(state.ReviewHistory[0].Decision, Is.EqualTo(ReviewDecision.Reject));
        }

        [Test]
        public void InvalidInitialErosion_RejectsRecruitAtomically()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "confirm");
            set.Add(new AddTenantChange("tenant_alpha", "tenant_alpha", 101f));
            set.Add(new ResolveCandidateChange(new ReviewDecisionRecord
            {
                CandidateId = "tenant_alpha",
                Decision = ReviewDecision.Recruit,
                Day = 1,
                Phase = HotelPhase.Dawn,
                InitialErosion = 101f
            }));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.Tenants, Is.Empty);
            Assert.That(state.ResolvedReviewCandidateIds, Is.Empty);
            Assert.That(state.ReviewHistory, Is.Empty);
        }

        [Test]
        public void DuplicateCandidateWithinChangeSet_RejectsAtomically()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "reject");
            set.Add(new ResolveCandidateChange("tenant_beta"));
            set.Add(new ResolveCandidateChange("tenant_beta"));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.StateVersion, Is.EqualTo(0));
            Assert.That(state.ResolvedReviewCandidateIds, Is.Empty);
        }

        [Test]
        public void RecruitRecordWithDifferentErosion_RejectsAtomically()
        {
            var state = GameRunState.New(new RunId("r"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "review", "confirm");
            set.Add(new AddTenantChange("tenant_alpha", "tenant_alpha", 10f));
            set.Add(new ResolveCandidateChange(new ReviewDecisionRecord
            {
                CandidateId = "tenant_alpha",
                Decision = ReviewDecision.Recruit,
                Day = 1,
                Phase = HotelPhase.Dawn,
                InitialErosion = 11f
            }));

            var result = new StateReducer().TryCommit(state, set);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(state.Tenants, Is.Empty);
            Assert.That(state.ReviewHistory, Is.Empty);
        }
    }
}
