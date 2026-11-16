using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// UI-006: regras da pausa (P/Esc/botão/foco), retomada deliberada sem reaplicar teclas, preferências persistidas e
    /// conteúdo (textos PT/EN e UXML). Usa o ChapterDirector e o InputRouter reais com carregador falso.
    /// </summary>
    public class PauseTests
    {
        const string UxmlPath = "Assets/_Game/UI/Pause/Pause.uxml";
        const string TextsPath = "Assets/_Game/UI/Pause/PauseTexts.json";

        readonly List<Object> created = new List<Object>();
        GameObject host;
        ChapterDirector director;
        InputRouter router;
        UI.PauseController controller;
        ChapterDefinition chapter;
        int qualityBefore;

        [SetUp]
        public void SetUp()
        {
            qualityBefore = QualitySettings.GetQualityLevel();
            host = new GameObject("pause-tests");
            var clock = host.AddComponent<SessionClock>();
            router = host.AddComponent<InputRouter>();
            director = host.AddComponent<ChapterDirector>();
            director.Initialize(clock, router, new InstantLoader(), new EvidenceStore());
            director.OpenDossier();
            controller = new UI.PauseController(director, router);

            chapter = ChapterDefinition.Create("mars", "mars.TITLE", "mars.DESC", "mars.Q", "Mars", 360f, "mars.intro", new[]
            {
                new ChapterStep("Intro", false, "End"),
                new ChapterStep("End", true),
            });
            created.Add(chapter);
        }

        [TearDown]
        public void TearDown()
        {
            controller?.Dispose();
            Object.DestroyImmediate(host);
            foreach (var obj in created)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }

            created.Clear();
            QualitySettings.SetQualityLevel(qualityBefore, true);
        }

        void StartChapter()
        {
            director.Begin(chapter);
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
        }

        // ---------- Quem pode pausar ----------

        [Test]
        public void NothingToPauseOutsideAChapter()
        {
            Assert.That(controller.CanPause, Is.False);
            Assert.That(controller.RequestPause(), Is.False);
            Assert.That(controller.KeyToggle(), Is.False);
            controller.FocusLost();
            Assert.That(director.IsPaused, Is.False, "perder o foco no dossiê não pausa nada");
            Assert.That(controller.AwaitingClick, Is.False);
        }

        [Test]
        public void ButtonAndKeyPauseAndKeyResumes()
        {
            StartChapter();
            Assert.That(controller.RequestPause(), Is.True);
            Assert.That(director.IsPaused, Is.True);
            Assert.That(router.Context, Is.EqualTo(InputContext.Paused));

            Assert.That(controller.KeyToggle(), Is.True, "P/Esc retomam uma pausa do menu");
            Assert.That(director.IsPaused, Is.False);
            Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay));

            Assert.That(controller.KeyToggle(), Is.True, "P/Esc pausam");
            Assert.That(director.IsPaused, Is.True);
        }

        [Test]
        public void PausedClockDoesNotAdvance()
        {
            StartChapter();
            director.Clock.Tick(0.05f);
            controller.RequestPause();
            var before = director.Clock.ActiveSeconds;
            director.Clock.Tick(0.05f);
            Assert.That(director.Clock.ActiveSeconds, Is.EqualTo(before));
        }

        // ---------- Foco ----------

        [Test]
        public void FocusLossPausesAndOnlyAClickResumes()
        {
            StartChapter();
            controller.FocusLost();
            Assert.That(director.IsPaused, Is.True);
            Assert.That(controller.AwaitingClick, Is.True);

            Assert.That(controller.KeyToggle(), Is.False, "P/Esc não retomam depois de perder o foco");
            Assert.That(director.IsPaused, Is.True);

            controller.Continue();
            Assert.That(director.IsPaused, Is.False);
            Assert.That(controller.AwaitingClick, Is.False);
        }

        [Test]
        public void FocusLossWhileMenuIsOpenStillNeedsTheClick()
        {
            StartChapter();
            controller.RequestPause();
            controller.FocusLost();
            controller.Continue();
            Assert.That(director.IsPaused, Is.False, "Continuar remove as duas fontes de pausa");
        }

        [Test]
        public void HeldKeysAtFocusLossAreNotReappliedOnResume()
        {
            StartChapter();
            router.FeedMove(new Vector2(0f, 1f));
            router.FeedInteract(true);
            Assert.That(router.InteractHeld, Is.True);

            controller.FocusLost();
            Assert.That(router.Move, Is.EqualTo(Vector2.zero));
            Assert.That(router.InteractHeld, Is.False);

            controller.Continue();
            Assert.That(router.Move, Is.EqualTo(Vector2.zero), "o veículo não anda sozinho");
            Assert.That(router.InteractHeld, Is.False, "nenhuma ferramenta liga sozinha");
        }

        [Test]
        public void EStillPressedThroughAMenuPauseNeedsARelease()
        {
            StartChapter();
            router.FeedInteract(true);
            controller.RequestPause();
            controller.Continue();
            Assert.That(router.IsInteractPhysicallyHeld, Is.True);
            Assert.That(router.InteractHeld, Is.False, "E mantido durante a pausa só vale depois de soltar");

            router.FeedInteract(false);
            router.FeedInteract(true);
            Assert.That(router.InteractHeld, Is.True);
        }

        [Test]
        public void RestartingFromThePauseLeavesNoPausedStateBehind()
        {
            StartChapter();
            controller.RequestPause();
            controller.FocusLost();
            director.RestartChapter();
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(director.IsPaused, Is.False);
            Assert.That(controller.AwaitingClick, Is.False);
            Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay));
        }

        [Test]
        public void FocusPauseComponentRoutesTheFocusLossToTheController()
        {
            StartChapter();
            var focus = host.AddComponent<FocusPause>();
            focus.Initialize(controller, director);
            focus.HandleFocusLost();
            Assert.That(director.IsPaused, Is.True);
            Assert.That(controller.AwaitingClick, Is.True);
        }

        // ---------- Preferências ----------

        [Test]
        public void RulesForStepsAndFormatting()
        {
            Assert.That(UI.SettingsPanel.StepVolume(0.8f, 1), Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(UI.SettingsPanel.StepVolume(1f, 1), Is.EqualTo(1f));
            Assert.That(UI.SettingsPanel.StepVolume(0f, -1), Is.EqualTo(0f));
            Assert.That(UI.SettingsPanel.StepSensitivity(1f, 1), Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(UI.SettingsPanel.StepSensitivity(3f, 1), Is.EqualTo(3f));
            Assert.That(UI.SettingsPanel.StepSensitivity(0.1f, -1), Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(UI.SettingsPanel.StepQuality(0, 3, -1), Is.EqualTo(0));
            Assert.That(UI.SettingsPanel.StepQuality(2, 3, 1), Is.EqualTo(2));
            Assert.That(UI.SettingsPanel.StepQuality(1, 3, 1), Is.EqualTo(2));
            Assert.That(UI.SettingsPanel.OtherLanguage("pt"), Is.EqualTo("en"));
            Assert.That(UI.SettingsPanel.OtherLanguage("en"), Is.EqualTo("pt"));
            Assert.That(UI.SettingsPanel.FormatSensitivity(1.5f, "pt"), Is.EqualTo("1,5×"));
            Assert.That(UI.SettingsPanel.FormatSensitivity(1.5f, "en"), Is.EqualTo("1.5×"));
            Assert.That(UI.SettingsPanel.FormatPercent(0.85f), Is.EqualTo("85%"));
        }

        [Test]
        public void PreferencesPersistAndAFreshStoreReadsThemBack()
        {
            var storage = new MemoryStorage();
            var store = new SettingsStore(storage);
            store.Load();
            var texts = LoadTexts();
            var hostElement = new VisualElement();
            using (var panel = new UI.SettingsPanel(hostElement, store, texts))
            {
                Assert.That(panel.Focusables.Count, Is.EqualTo(10), "4 volumes, idioma, sensibilidade, movimento, qualidade, tela cheia, restaurar");
                Assert.That(panel.TryAdjust(panel.Focusables[1], 1), Is.True); // música
                Assert.That(panel.TryAdjust(panel.Focusables[4], 1), Is.True); // idioma
                Assert.That(panel.TryAdjust(panel.Focusables[5], 1), Is.True); // sensibilidade
                Assert.That(panel.TryActivate(panel.Focusables[6]), Is.True);  // movimento reduzido
                Assert.That(panel.TryActivate(panel.Focusables[0]), Is.False, "volume não tem ação de Enter");
            }

            var reloaded = new SettingsStore(storage);
            Assert.That(reloaded.Load(), Is.True);
            var data = reloaded.Current;
            Assert.That(data.musicVolume, Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(data.languageCode, Is.EqualTo("en"));
            Assert.That(data.lookSensitivity, Is.EqualTo(1.1f).Within(0.0001f));
            Assert.That(data.reducedMotion, Is.True);
        }

        [Test]
        public void RestoreDefaultsBringsEverythingBack()
        {
            var store = new SettingsStore(new MemoryStorage());
            store.Load();
            store.Apply(s => { s.musicVolume = 0.1f; s.languageCode = "en"; s.reducedMotion = true; });
            using (var panel = new UI.SettingsPanel(new VisualElement(), store, LoadTexts()))
                Assert.That(panel.TryActivate(panel.Focusables[9]), Is.True);

            var data = store.Current;
            Assert.That(data.musicVolume, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(data.languageCode, Is.EqualTo("en"));
            Assert.That(data.reducedMotion, Is.False);
        }

        // ---------- Conteúdo ----------

        [Test]
        public void EveryKeyHasPortugueseAndEnglishText()
        {
            var texts = LoadTexts();
            foreach (var key in UI.PauseKeys.All)
            {
                Assert.That(texts.TryGet(key, "pt", out var pt) && !string.IsNullOrWhiteSpace(pt), Is.True, $"PT: {key}");
                Assert.That(texts.TryGet(key, "en", out var en) && !string.IsNullOrWhiteSpace(en), Is.True, $"EN: {key}");
            }

            Assert.That(texts.KeysMissingLanguage(), Is.Empty);
        }

        [Test]
        public void UxmlHasEveryElementThePauseMenuBinds()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(uxml != null, Is.True, $"asset ausente: {UxmlPath}");
            var tree = uxml.CloneTree();
            foreach (var name in new[]
            {
                "pause-root", "pause-corner", "pause-button", "pause-button-label", "pause-button-key", "pause-overlay",
                "pause-title", "pause-subtitle", "view-main", "btn-resume", "btn-settings", "btn-restart",
                "btn-dossier", "pause-hint", "view-settings", "settings-title", "btn-back", "pause-navigation",
            })
                Assert.That(tree.Q<VisualElement>(name) != null, Is.True, name);

            Assert.That(tree.Q<ScrollView>("settings-scroll") != null, Is.True, "settings-scroll");
        }

        [Test]
        public void TheOverlayCentersTheCardOnBothAxes()
        {
            // Centralização por âncoras (flex), não por posição absoluta: vale para qualquer proporção do painel.
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_Game/UI/Pause/Pause.uss");
            Assert.That(uss != null, Is.True);
            var text = System.IO.File.ReadAllText("Assets/_Game/UI/Pause/Pause.uss");
            var overlay = text.Substring(text.IndexOf(".sa-pausemenu__overlay {", System.StringComparison.Ordinal));
            overlay = overlay.Substring(0, overlay.IndexOf('}'));
            Assert.That(overlay, Does.Contain("justify-content: center"));
            Assert.That(overlay, Does.Contain("align-items: center"));
            Assert.That(overlay, Does.Contain("right: 0"));
            Assert.That(overlay, Does.Contain("bottom: 0"));
        }

        static UI.DossierTextCatalog LoadTexts()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath);
            Assert.That(asset != null, Is.True, $"asset ausente: {TextsPath}");
            var texts = new UI.DossierTextCatalog();
            UI.DossierTextTables.Load(asset, texts);
            return texts;
        }

        sealed class InstantLoader : ChapterDirector.ISceneLoader
        {
            public void Load(string sceneName, System.Action onLoaded, System.Action<string> onFailed) => onLoaded();

            public void Unload(string sceneName, System.Action onUnloaded) => onUnloaded();
        }

        sealed class MemoryStorage : SettingsStore.IStorage
        {
            readonly Dictionary<string, string> values = new Dictionary<string, string>();

            public string Read(string key) => values.TryGetValue(key, out var value) ? value : null;

            public void Write(string key, string value) => values[key] = value;

            public void Delete(string key) => values.Remove(key);
        }
    }
}
