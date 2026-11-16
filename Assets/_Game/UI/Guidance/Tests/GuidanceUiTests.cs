using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// UI-005: a placa desenha o tutorial em PT/EN, sobe o reforço e a oferta, some fora do jogo e nunca captura o mouse; os
    /// textos estão completos e o prefab está ligado aos assets certos. Usa os textos e os estilos reais.
    /// </summary>
    public class GuidanceUiTests
    {
        const string TextsPath = "Assets/_Game/UI/Guidance/GuidanceTexts.json";
        const string StylePath = "Assets/_Game/UI/Guidance/Guidance.uss";
        const string PrefabPath = "Assets/_Game/UI/Guidance/Guidance.prefab";
        const string TutorialId = "tst.tutorial";

        sealed class MemoryStorage : SettingsStore.IStorage
        {
            readonly Dictionary<string, string> values = new Dictionary<string, string>();

            public string Read(string key) => values.TryGetValue(key, out var value) ? value : null;
            public void Write(string key, string value) => values[key] = value;
            public void Delete(string key) => values.Remove(key);
        }

        GameObject host;
        InputRouter router;
        InteractionService service;
        UI.GuidanceController guidance;
        UI.TutorialPresenter presenter;
        SettingsStore settings;
        VisualElement column;
        System.IDisposable gameplay;

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset != null, Is.True, $"asset ausente: {path}");
            return asset;
        }

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("guidance-ui-tests");
            router = host.AddComponent<InputRouter>();
            service = host.AddComponent<InteractionService>();
            service.Initialize(router, null);
            gameplay = router.Acquire(InputContext.Gameplay, "guidance-ui-tests");

            settings = new SettingsStore(new MemoryStorage());
            settings.Load();
            settings.Apply(s => s.languageCode = SettingsData.Portuguese); // o padrão do jogo é inglês; estes testes conferem o texto em português
            guidance = host.AddComponent<UI.GuidanceController>();
            guidance.Initialize(router, service, null, null, settings);

            presenter = host.AddComponent<UI.TutorialPresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("uiTexts").objectReferenceValue = Load<TextAsset>(TextsPath);
            serialized.FindProperty("styleSheet").objectReferenceValue = Load<StyleSheet>(StylePath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            column = new VisualElement();
            presenter.Initialize(guidance, column, router, null, settings);
        }

        [TearDown]
        public void TearDown()
        {
            gameplay?.Dispose();
            UnityEngine.Object.DestroyImmediate(host);
        }

        // ---------- Presenter ----------

        [Test]
        public void NothingIsShownUntilGuidanceStarts()
        {
            Assert.That(presenter.IsBound, Is.True);
            Assert.That(presenter.IsShowing, Is.False);
        }

        [Test]
        public void TutorialShowsMoveAndInteractInPortugueseWithAKeyboardFirstWording()
        {
            guidance.BeginTutorial(TutorialId);
            Assert.That(presenter.IsShowing, Is.True);
            Assert.That(presenter.VisibleLineCount, Is.EqualTo(2));
            StringAssert.Contains("W A S D", presenter.LineText(0));
            StringAssert.Contains("E", presenter.LineText(1));
        }

        [Test]
        public void ChangingLanguageRedrawsTheSameInstructionsInEnglish()
        {
            guidance.BeginTutorial(TutorialId);
            var before = presenter.LineText(0);
            settings.Apply(s => s.languageCode = "pt"); // o jogo só tem inglês: "pt" volta para inglês e o texto não muda
            Assert.That(presenter.LineText(0), Is.EqualTo(before));
            StringAssert.StartsWith("Move with", presenter.LineText(0));
        }

        [Test]
        public void ReinforceChangesTheFirstPendingInstructionAndOfferAddsTheChapterHelp()
        {
            guidance.BeginTutorial(TutorialId, new UI.GuidanceOptions { HelpAvailable = true, CorridorAvailable = true });
            var normal = presenter.LineText(0);
            Assert.That(presenter.HasHelpOffer, Is.False);

            guidance.Tick(12.1f);
            Assert.That(presenter.LineText(0), Is.Not.EqualTo(normal));
            Assert.That(presenter.HasHelpOffer, Is.False);

            guidance.Tick(18f);
            Assert.That(presenter.HasHelpOffer, Is.True);
            Assert.That(presenter.HasCorridorOffer, Is.True);
        }

        [Test]
        public void OfferDoesNotAppearForHelpTheChapterDoesNotHave()
        {
            guidance.BeginTutorial(TutorialId);
            guidance.Tick(31f);
            Assert.That(presenter.HasHelpOffer, Is.False);
            Assert.That(presenter.HasCorridorOffer, Is.False);
        }

        [Test]
        public void ExtraAssistanceShowsTheHelpHintFromTheStart()
        {
            settings.Apply(s => s.extraAssistance = true);
            guidance.BeginTutorial(TutorialId, new UI.GuidanceOptions { HelpAvailable = true });
            Assert.That(presenter.HasHelpOffer, Is.True);
        }

        [Test]
        public void PresenterHidesDuringPauseAndCinematicAndReturnsAfterwards()
        {
            guidance.BeginTutorial(TutorialId);
            using (router.Acquire(InputContext.Paused, "pause"))
                Assert.That(presenter.IsShowing, Is.False);

            using (router.Acquire(InputContext.Cinematic, "cinematic"))
                Assert.That(presenter.IsShowing, Is.False);

            Assert.That(presenter.IsShowing, Is.True);
        }

        [Test]
        public void CompletedInstructionStaysAsDoneUntilTheLingerEndsWithoutAnyClick()
        {
            guidance.BeginTutorial(TutorialId);
            router.FeedMove(Vector2.up);
            guidance.Tick(1f);

            Assert.That(presenter.VisibleLineCount, Is.EqualTo(2), "move cumprido ainda aparece com 'feito ✓'");
            StringAssert.Contains("✓", presenter.LineText(0));
        }

        [Test]
        public void NoElementOfThePlateCapturesTheMouse()
        {
            guidance.BeginTutorial(TutorialId, new UI.GuidanceOptions { HelpAvailable = true, CorridorAvailable = true });
            guidance.Tick(31f);

            var plate = column.Q<VisualElement>("guidance");
            Assert.That(plate != null, Is.True);
            Assert.That(plate.pickingMode, Is.EqualTo(PickingMode.Ignore));
            plate.Query<VisualElement>().ForEach(element =>
                Assert.That(element.pickingMode, Is.EqualTo(PickingMode.Ignore), element.name + "/" + string.Join(" ", element.GetClasses())));
        }

        [Test]
        public void PlateSitsAboveTheOtherChildrenOfTheColumn()
        {
            var other = new VisualElement { name = "prompt" };
            column.Add(other);
            presenter.Initialize(guidance, column, router, null, settings);
            Assert.That(column.IndexOf(column.Q<VisualElement>("guidance")), Is.EqualTo(0));
        }

        [Test]
        public void EndedGuidanceHidesThePlate()
        {
            guidance.BeginTutorial(TutorialId);
            guidance.End();
            Assert.That(presenter.IsShowing, Is.False);
        }

        // ---------- Textos e prefab ----------

        [Test]
        public void EveryKeyHasPortugueseAndEnglishText()
        {
            var texts = new UI.DossierTextCatalog();
            UI.DossierTextTables.Load(Load<TextAsset>(TextsPath), texts);
            Assert.That(UI.GuidanceContentCheck.Validate(texts), Is.Empty);
        }

        [Test]
        public void ContentCheckReportsAMissingKey()
        {
            var texts = new UI.DossierTextCatalog();
            UI.DossierTextTables.Load(Load<TextAsset>(TextsPath), texts);
            texts.Add(UI.GuidanceKeys.OfferHelp, "só PT", "");
            var problems = UI.GuidanceContentCheck.Validate(texts);
            Assert.That(problems.Count, Is.EqualTo(1));
            StringAssert.Contains(UI.GuidanceKeys.OfferHelp, problems[0]);
        }

        [Test]
        public void NoKeyIsEmptyOrDuplicated()
        {
            var seen = new HashSet<string>();
            foreach (var key in UI.GuidanceKeys.All)
            {
                Assert.That(ContractValidation.IsValidId(key), Is.True, key);
                Assert.That(seen.Add(key), Is.True, "repetida: " + key);
            }
        }

        [Test]
        public void PrefabIsWiredToTheTextsAndTheStyles()
        {
            var prefab = Load<GameObject>(PrefabPath);
            var controller = prefab.GetComponent<UI.GuidanceController>();
            var prefabPresenter = prefab.GetComponent<UI.TutorialPresenter>();
            Assert.That(controller != null, Is.True, "GuidanceController no prefab");
            Assert.That(prefabPresenter != null, Is.True, "TutorialPresenter no prefab");
            Assert.That(prefabPresenter.ValidateSetup(), Is.Empty);

            var serialized = new SerializedObject(prefabPresenter);
            Assert.That(serialized.FindProperty("uiTexts").objectReferenceValue, Is.EqualTo(Load<TextAsset>(TextsPath)));
            Assert.That(serialized.FindProperty("styleSheet").objectReferenceValue, Is.EqualTo(Load<StyleSheet>(StylePath)));
        }

        [Test]
        public void PrefabHasNoCameraNoAudioAndNoDocumentSoItNeverDuplicatesThem()
        {
            var prefab = Load<GameObject>(PrefabPath);
            Assert.That(prefab.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<AudioListener>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<UIDocument>(true).Length, Is.EqualTo(0), "entra no documento do HUD");
        }
    }
}
