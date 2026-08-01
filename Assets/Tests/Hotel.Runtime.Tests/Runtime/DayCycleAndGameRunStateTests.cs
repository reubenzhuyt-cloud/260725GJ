using Hotel.Authoring.DayCycle;
using Hotel.Runtime;
using NUnit.Framework;

using UnityEditor;
using UnityEngine;

namespace Hotel.Runtime.Tests
{
    public sealed class DayCycleAndGameRunStateTests
    {
        [Test]
        public void ValidCycle_UsesDawnDayDuskNightOrder()
        {
            var cycle = ScriptableObject.CreateInstance<DayCycleDefinition>();
            ConfigurePhases(cycle, HotelPhase.Dawn, HotelPhase.Day, HotelPhase.Dusk, HotelPhase.Night);

            Assert.That(cycle.Validate(), Is.Empty);
            Assert.That(cycle.GetNext(HotelPhase.Dawn), Is.EqualTo(HotelPhase.Day));
            Assert.That(cycle.GetNext(HotelPhase.Night), Is.EqualTo(HotelPhase.Dawn));

            Object.DestroyImmediate(cycle);
        }

        [Test]
        public void InvalidCycle_WithDuplicatePhaseIsRejected()
        {
            var cycle = ScriptableObject.CreateInstance<DayCycleDefinition>();
            ConfigurePhases(cycle, HotelPhase.Dawn, HotelPhase.Day, HotelPhase.Dusk, HotelPhase.Dusk);

            Assert.That(cycle.Validate(), Is.Not.Empty);

            Object.DestroyImmediate(cycle);
        }

        [Test]
        public void NewRun_RetainsRunIdThroughUnityJsonSerialization()
        {
            var state = GameRunState.New(new RunId("run-serialized"));

            var json = EditorJsonUtility.ToJson(state);

            Assert.That(json, Does.Contain("run-serialized"));
        }

        [Test]
        public void DayCycle_RuntimeConfigurationIsReadOnly()
        {
            var property = typeof(DayCycleDefinition).GetProperty("OrderedPhases");
            var setter = typeof(DayCycleDefinition).GetMethod("SetOrderedPhases");

            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanWrite, Is.False);
            Assert.That(setter, Is.Null);
        }

        [Test]
        public void NewRun_InitializesDayOneAtDawn()
        {
            var state = GameRunState.New(new RunId("run-1"));

            Assert.That(state.Day, Is.EqualTo(1));
            Assert.That(state.Phase.Current, Is.EqualTo(HotelPhase.Dawn));
        }

        [Test]
        public void DayCycleDefinition_ImplementsIPhaseCycle()
        {
            Assert.That(typeof(IPhaseCycle).IsAssignableFrom(typeof(DayCycleDefinition)), Is.True);
        }

        [Test]
        public void CreateDefault_ReturnsValidDawnDayDuskNightOrder()
        {
            var cycle = DayCycleDefinition.CreateDefault();

            Assert.That(cycle, Is.Not.Null);
            Assert.That(cycle.Validate(), Is.Empty);
            Assert.That(cycle.GetNext(HotelPhase.Dawn), Is.EqualTo(HotelPhase.Day));
            Assert.That(cycle.GetNext(HotelPhase.Day), Is.EqualTo(HotelPhase.Dusk));
            Assert.That(cycle.GetNext(HotelPhase.Dusk), Is.EqualTo(HotelPhase.Night));
            Assert.That(cycle.GetNext(HotelPhase.Night), Is.EqualTo(HotelPhase.Dawn));

            Object.DestroyImmediate(cycle);
        }

        private static void ConfigurePhases(DayCycleDefinition cycle, params HotelPhase[] phases)
        {
            var serializedObject = new SerializedObject(cycle);
            var property = serializedObject.FindProperty("ordered");
            property.arraySize = phases.Length;

            for (var index = 0; index < phases.Length; index++)
            {
                property.GetArrayElementAtIndex(index).enumValueIndex = (int)phases[index];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}