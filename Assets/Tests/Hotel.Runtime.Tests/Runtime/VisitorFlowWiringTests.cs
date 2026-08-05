using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hotel.Runtime.Tests
{
    public sealed class VisitorFlowWiringTests
    {
        [Test]
        public void BuildStartsFromMainMenuThenIncludesGameScene()
        {
            var enabledScenes = new List<EditorBuildSettingsScene>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    enabledScenes.Add(scene);
            }

            Assert.That(enabledScenes.Count, Is.EqualTo(2));
            Assert.That(enabledScenes[0].path, Is.EqualTo("Assets/Scenes/MainMenu.unity"));
            Assert.That(enabledScenes[1].path, Is.EqualTo("Assets/Scenes/MainScene.unity"));
        }

        [Test]
        public void MainMenuScene_HasMenuController()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            var coordinator = FindComponentByTypeName(scene, "MainMenuController");
            Assert.That(coordinator, Is.Not.Null);
        }

        [Test]
        public void MainScene_HasTwentyCompleteUniqueVisitorProfiles()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
            var coordinator = FindComponentByTypeName(scene, "TenantReviewCoordinator");
            Assert.That(coordinator, Is.Not.Null);

            var coordinatorData = new SerializedObject(coordinator);
            var reviewPanel = coordinatorData.FindProperty("reviewPanel");
            var candidates = coordinatorData.FindProperty("candidates");

            Assert.That(reviewPanel.objectReferenceValue, Is.Not.Null);
            Assert.That(candidates.arraySize, Is.EqualTo(20));

            var ids = new HashSet<string>();
            for (var index = 0; index < candidates.arraySize; index++)
            {
                var candidate = candidates.GetArrayElementAtIndex(index).objectReferenceValue;
                Assert.That(candidate, Is.Not.Null);

                var candidateData = new SerializedObject(candidate);
                var id = candidateData.FindProperty("candidateId").stringValue;
                var displayName = candidateData.FindProperty("displayName").stringValue;
                var ability = candidateData.FindProperty("ability").enumValueIndex;
                var activity = candidateData.FindProperty("activityType").enumValueIndex;

                Assert.That(id, Is.Not.Empty);
                Assert.That(displayName, Is.Not.Empty);
                Assert.That(ids.Add(id), Is.True, $"Duplicate candidate ID: {id}");
                Assert.That(ability, Is.InRange(0, 8));
                Assert.That(activity, Is.InRange(0, 2));
            }
        }

        private static MonoBehaviour FindComponentByTypeName(Scene scene, string typeName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component != null && component.GetType().Name == typeName)
                        return component;
                }
            }

            return null;
        }
    }
}
