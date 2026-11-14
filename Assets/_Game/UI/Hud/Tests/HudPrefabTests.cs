using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>UI-003: o prefab do HUD está ligado aos assets certos e não traz câmera nem áudio.</summary>
    public class HudPrefabTests
    {
        const string PrefabPath = "Assets/_Game/UI/Hud/MissionHUD.prefab";
        const string UxmlPath = "Assets/_Game/UI/Hud/MissionHud.uxml";
        const string TextsPath = "Assets/_Game/UI/Hud/HudTexts.json";

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset != null, Is.True, $"asset ausente: {path}");
            return asset;
        }

        [Test]
        public void PrefabIsWiredToTheUxmlTheTextsAndThePanelSettings()
        {
            var prefab = Load<GameObject>(PrefabPath);
            var hud = prefab.GetComponent<UI.MissionHUD>();
            Assert.That(hud != null, Is.True, "MissionHUD no prefab");

            var document = prefab.GetComponent<UIDocument>();
            Assert.That(document != null, Is.True);
            Assert.That(document.visualTreeAsset, Is.EqualTo(Load<VisualTreeAsset>(UxmlPath)));
            Assert.That(document.panelSettings != null, Is.True);
            Assert.That(prefab.GetComponent<UI.ResponsiveLayoutClass>() != null, Is.True, "variante compacta 4:3");

            var serialized = new SerializedObject(hud);
            Assert.That(serialized.FindProperty("uiTexts").objectReferenceValue, Is.EqualTo(Load<TextAsset>(TextsPath)));
            Assert.That(serialized.FindProperty("document").objectReferenceValue, Is.EqualTo(document));
        }

        [Test]
        public void PrefabHasNoCameraAndNoAudioSoItNeverDuplicatesThem()
        {
            var prefab = Load<GameObject>(PrefabPath);
            Assert.That(prefab.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<AudioListener>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(0));
        }

        [Test]
        public void HudSortsBelowTheDossierAndTheFutureModalLayers()
        {
            var document = Load<GameObject>(PrefabPath).GetComponent<UIDocument>();
            Assert.That(document.sortingOrder, Is.LessThan(10f), "o dossiê (10) fica por cima do HUD");
        }
    }
}
