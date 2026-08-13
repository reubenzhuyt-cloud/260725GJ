using System.Collections.Generic;
using System.Reflection;
using Hotel.Runtime;
using NUnit.Framework;
using UnityEngine;

public class RoommateLogicTests
{
    private GameObject _coordinatorGo;

    [TearDown]
    public void TearDown()
    {
        if (_coordinatorGo != null)
        {
            Object.DestroyImmediate(_coordinatorGo);
            _coordinatorGo = null;
        }
    }

    [Test]
    public void SameRoomOtherTenants_ReturnsAllOtherOccupantsInOwnersRoom()
    {
        GameRunState state = GameRunState.New(new RunId("same-room-targets"), 1);
        var room = new RoomRunState { RoomId = "room_01" };
        room.OccupantIds.AddRange(new[] { "owner", "tenant_a", "tenant_b" });
        state.Rooms["room_01"] = room;
        state.Tenants["owner"] = Tenant("owner", "room_01");
        state.Tenants["tenant_a"] = Tenant("tenant_a", "room_01");
        state.Tenants["tenant_b"] = Tenant("tenant_b", "room_01");

        List<string> targets = EventEffectExecutor.ResolveTargets(
            EffectTarget.SameRoomOtherTenants, state, "owner", 0, 0, null);

        Assert.IsNotNull(targets);
        CollectionAssert.AreEquivalent(new[] { "tenant_a", "tenant_b" }, targets);
        CollectionAssert.DoesNotContain(targets, "owner");
    }

    [Test]
    public void SameRoomOtherTenants_ReturnsNull_WhenOwnerHasNoRoom()
    {
        GameRunState state = GameRunState.New(new RunId("same-room-no-room"), 1);
        state.Tenants["owner"] = Tenant("owner", null);

        List<string> targets = EventEffectExecutor.ResolveTargets(
            EffectTarget.SameRoomOtherTenants, state, "owner", 0, 0, null);

        Assert.IsNull(targets);
    }

    [Test]
    public void SameRoomOtherTenants_ReturnsEmptyList_WhenOwnerIsAlone()
    {
        GameRunState state = GameRunState.New(new RunId("same-room-alone"), 1);
        var room = new RoomRunState { RoomId = "room_01" };
        room.OccupantIds.Add("owner");
        state.Rooms["room_01"] = room;
        state.Tenants["owner"] = Tenant("owner", "room_01");

        List<string> targets = EventEffectExecutor.ResolveTargets(
            EffectTarget.SameRoomOtherTenants, state, "owner", 0, 0, null);

        Assert.IsNotNull(targets);
        Assert.IsEmpty(targets);
    }

    [Test]
    public void TryCommit_RejectsDuplicateAddBuffChangesWithSameBuffIdInOneSet()
    {
        GameRunState state = GameRunState.New(new RunId("buff-duplicate"), 1);
        AuthorizedChangeSet set = AuthorizedChangeSet.Domain(
            state.RunId, state.StateVersion, "RoommateLogicTests", "AddDuplicateBuffs");
        set.Add(new AddBuffChange(new BuffRunState { BuffId = "buff_shared", RemainingTicks = -1 }));
        set.Add(new AddBuffChange(new BuffRunState { BuffId = "buff_shared", RemainingTicks = 3 }));

        CommitResult result = new StateReducer().TryCommit(state, set);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(state.Buffs.ContainsKey("buff_shared"));
    }

    [Test]
    public void TryCommit_AcceptsAddBuffChangesWithDistinctBuffIdsInOneSet()
    {
        GameRunState state = GameRunState.New(new RunId("buff-distinct"), 1);
        AuthorizedChangeSet set = AuthorizedChangeSet.Domain(
            state.RunId, state.StateVersion, "RoommateLogicTests", "AddDistinctBuffs");
        set.Add(new AddBuffChange(new BuffRunState { BuffId = "buff_a", RemainingTicks = -1 }));
        set.Add(new AddBuffChange(new BuffRunState { BuffId = "buff_b", RemainingTicks = -1 }));

        CommitResult result = new StateReducer().TryCommit(state, set);

        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(state.Buffs.ContainsKey("buff_a"));
        Assert.IsTrue(state.Buffs.ContainsKey("buff_b"));
    }

    [Test]
    public void AvailableCapacity_CountsOnlyAssignedTenants_NotAllTenants()
    {
        TenantAssignmentCoordinator coordinator = CreateCoordinator(StateWithRooms(3, 2, 1));

        Assert.AreEqual(1, coordinator.AvailableCapacity);
    }

    [Test]
    public void AvailableCapacity_WithOnlyUnassignedTenants_EqualsTotalRoomCapacity()
    {
        TenantAssignmentCoordinator coordinator = CreateCoordinator(StateWithRooms(3, 0, 2));

        Assert.AreEqual(3, coordinator.AvailableCapacity);
    }

    [Test]
    public void AvailableCapacity_WithNoTenants_EqualsTotalRoomCapacity()
    {
        TenantAssignmentCoordinator coordinator = CreateCoordinator(StateWithRooms(3, 0, 0));

        Assert.AreEqual(3, coordinator.AvailableCapacity);
    }

    [Test]
    public void AvailableCapacity_WithAllRoomsFull_IsZero()
    {
        TenantAssignmentCoordinator coordinator = CreateCoordinator(StateWithRooms(3, 3, 0));

        Assert.AreEqual(0, coordinator.AvailableCapacity);
    }

    private static TenantRunState Tenant(string id, string roomId)
    {
        return new TenantRunState { TenantId = id, RoomId = roomId };
    }

    private TenantAssignmentCoordinator CreateCoordinator(GameRunState state)
    {
        _coordinatorGo = new GameObject("RoommateLogicTestsCoordinator");
        TenantAssignmentCoordinator coordinator = _coordinatorGo.AddComponent<TenantAssignmentCoordinator>();
        FieldInfo runStateField = typeof(TenantAssignmentCoordinator)
            .GetField("_runState", BindingFlags.Instance | BindingFlags.NonPublic);
        runStateField.SetValue(coordinator, state);
        return coordinator;
    }

    private static GameRunState StateWithRooms(int roomCount, int assignedCount, int unassignedCount)
    {
        GameRunState state = GameRunState.New(new RunId("capacity-state"), 1);
        for (int i = 1; i <= roomCount; i++)
        {
            string roomId = string.Format("room_{0:D2}", i);
            state.Rooms[roomId] = new RoomRunState { RoomId = roomId };
        }
        for (int i = 0; i < assignedCount; i++)
        {
            string id = "assigned_" + i;
            string roomId = string.Format("room_{0:D2}", (i % roomCount) + 1);
            state.Tenants[id] = new TenantRunState { TenantId = id, RoomId = roomId };
            state.Rooms[roomId].OccupantIds.Add(id);
        }
        for (int i = 0; i < unassignedCount; i++)
        {
            string id = "unassigned_" + i;
            state.Tenants[id] = new TenantRunState { TenantId = id };
        }
        return state;
    }
}
