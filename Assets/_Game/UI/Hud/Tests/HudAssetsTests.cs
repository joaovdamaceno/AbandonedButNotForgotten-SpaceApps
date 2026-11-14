using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>UI-003: UXML e textos batem com o código (nomes, tipos, PT/EN, marcadores) e o HUD nunca captura o mouse.</summary>
    public class HudAssetsTests
    {
        const string UxmlPath = "Assets/_Game/UI/Hud/MissionHud.uxml";
        const string TextsPath = "Assets/_Game/UI/Hud/HudTexts.json";

        /// <summary>Elementos (nome, tipo) que MissionHUD, InteractionPrompt e ObjectiveIndicator exigem. Mantenha igual ao código.</summary>
        static readonly KeyValuePair<string, Type>[] Required =
        {
            Pair("hud-root", typeof(VisualElement)),
            Pair("objective", typeof(VisualElement)), Pair("objective-stamp", typeof(Label)), Pair("objective-label", typeof(Label)),
            Pair("objective-meta", typeof(Label)),
            Pair("hud-right", typeof(VisualElement)), Pair("hud-bottom", typeof(VisualElement)),
            Pair("energy", typeof(VisualElement)), Pair("energy-label", typeof(Label)), Pair("energy-fill", typeof(VisualElement)),
            Pair("anchor-marker", typeof(VisualElement)), Pair("anchor-label", typeof(Label)),
            Pair("edge-cue", typeof(VisualElement)), Pair("edge-arrow", typeof(VisualElement)), Pair("edge-label", typeof(Label)),
            Pair("prompt", typeof(VisualElement)), Pair("prompt-label", typeof(Label)), Pair("prompt-status", typeof(Label)),
            Pair("prompt-fill", typeof(VisualElement)),
        };

        static KeyValuePair<string, Type> Pair(string name, Type type) => new KeyValuePair<string, Type>(name, type);

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset != null, Is.True, $"asset ausente: {path}");
            return asset;
        }

        static void Walk(VisualElement element, Action<VisualElement> visit)
        {
            visit(element);
            foreach (var child in element.Children())
                Walk(child, visit);
        }

        [Test]
        public void UxmlHasEveryElementThePresentersRequireWithTheRightType()
        {
            var tree = Load<VisualTreeAsset>(UxmlPath).Instantiate();
            foreach (var pair in Required)
            {
                var found = tree.Q<VisualElement>(pair.Key);
                Assert.That(found != null, Is.True, $"MissionHud.uxml sem '{pair.Key}'");
                Assert.That(pair.Value.IsInstanceOfType(found), Is.True, $"'{pair.Key}' deveria ser {pair.Value.Name} mas é {found.GetType().Name}");
            }
        }

        [Test]
        public void EveryElementIgnoresThePointerSoTheHudNeverStealsTheMouse()
        {
            var tree = Load<VisualTreeAsset>(UxmlPath).Instantiate();
            var offenders = new List<string>();
            Walk(tree, element =>
            {
                if (element != tree && element.pickingMode != PickingMode.Ignore)
                    offenders.Add(string.IsNullOrEmpty(element.name) ? element.GetType().Name : element.name);
            });

            Assert.That(string.Join(", ", offenders), Is.EqualTo(string.Empty), "o HUD é só apresentação: nenhum elemento captura o mouse");
        }

        [Test]
        public void HudDoesNotShipThePauseButtonOrTheSubtitleThatBelongToOtherTasks()
        {
            var tree = Load<VisualTreeAsset>(UxmlPath).Instantiate();
            Assert.That(tree.Q<VisualElement>(className: "sa-pause") == null, Is.True, "a pausa é da UI-006");
            Assert.That(tree.Q<VisualElement>(className: "sa-subtitle") == null, Is.True, "a legenda é da UI-004");
            Assert.That(tree.Q<VisualElement>("hud-right") != null && tree.Q<VisualElement>("hud-bottom") != null, Is.True, "mas as colunas para elas existem");
        }

        [Test]
        public void EveryHudTextExistsInPortugueseAndEnglishAndKeepsItsPlaceholders()
        {
            var texts = new UI.DossierTextCatalog();
            Assert.That(UI.DossierTextTables.Load(Load<TextAsset>(TextsPath), texts), Is.GreaterThan(20));
            Assert.That(string.Join("\n", UI.HudContentCheck.Validate(texts)), Is.EqualTo(string.Empty));
        }

        [Test]
        public void RealTextsRenderTheDocumentedPromptStatesInBothLanguages()
        {
            var texts = new UI.DossierTextCatalog();
            UI.DossierTextTables.Load(Load<TextAsset>(TextsPath), texts);
            Assert.That(texts.Get(UI.HudKeys.StatusDone, "pt"), Is.EqualTo("CONCLUÍDO ✓"));
            Assert.That(texts.Get(UI.HudKeys.StatusPaused, "en"), Is.EqualTo("PAUSED · {percent}% KEPT"));
            Assert.That(texts.Get(UI.HudKeys.CommandTransit, "pt"), Is.EqualTo("Comando em trânsito · {seconds} s"));
            Assert.That(texts.Get(UI.HudKeys.CommandTransit, "en"), Is.EqualTo("Command in transit · {seconds} s"));
        }

        [Test]
        public void HudInputContextMirrorsInputContextNamesAndValues()
        {
            Assert.That(string.Join(",", Enum.GetNames(typeof(UI.HudInputContext))), Is.EqualTo(string.Join(",", Enum.GetNames(typeof(InputContext)))));
            foreach (var name in Enum.GetNames(typeof(InputContext)))
                Assert.That((int)Enum.Parse(typeof(UI.HudInputContext), name), Is.EqualTo((int)Enum.Parse(typeof(InputContext), name)), name);
        }

        [Test]
        public void PromptStateClassesCoverEveryNonHiddenState()
        {
            var classes = new HashSet<string>(UI.PromptViewBuilder.AllStateClasses);
            Assert.That(classes.Count, Is.EqualTo(Enum.GetValues(typeof(UI.PromptState)).Length - 1), "uma classe por estado visível, todas distintas");
        }
    }
}
