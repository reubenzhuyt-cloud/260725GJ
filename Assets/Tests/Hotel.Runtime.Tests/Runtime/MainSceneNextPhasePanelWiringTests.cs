using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hotel.Runtime.Tests
{
    public sealed class MainSceneNextPhasePanelWiringTests
    {
        [Test]
        public void MainScene_NextPhasePanelIsHostedOnlyByButtonPanel()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
            var canvas = FindRoot(scene, "GameCanvas");
            var buttonPanel = canvas.transform.Find("NextPhasePanel").gameObject;
            var uiManager = canvas.transform.Find("UIManager").gameObject;

            Assert.That(buttonPanel.GetComponent("NextPhasePanel"), Is.Not.Null);
            Assert.That(buttonPanel.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(buttonPanel.GetComponentInChildren<UnityEngine.UI.Button>(true), Is.Not.Null);
            Assert.That(uiManager.GetComponent("NextPhasePanel"), Is.Null);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                    return root;
            }

            Assert.Fail($"Missing root GameObject '{name}'.");
            return null;
        }
    }
}