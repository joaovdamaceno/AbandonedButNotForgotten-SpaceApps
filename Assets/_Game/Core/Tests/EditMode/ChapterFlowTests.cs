using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>CORE-005: fluxo do aplicativo, relógio ativo, evidências, preferências e descarte entre capítulos.</summary>
    public class ChapterFlowTests
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        GameObject host;
        ChapterDirector director;
        SessionClock clock;
        InputRouter router;
        FakeLoader loader;
        EvidenceStore evidence;
        List<string> log;
        ChapterDefinition mars;
        ChapterDefinition pioneer;
        ChapterDefinition surveyor;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("app");
            clock = host.AddComponent<SessionClock>();
            router = host.AddComponent<InputRouter>();
            director = host.AddComponent<ChapterDirector>();
            loader = new FakeLoader();
            evidence = new EvidenceStore();
            director.Initialize(clock, router, loader, evidence);
            director.OpenDossier();

            mars = Chapter("mars", "Mars");
            pioneer = Chapter("pioneer10", "Pioneer10");
            surveyor = Chapter("surveyor3", "Surveyor3");

            log = new List<string>();
            loader.Log = log;
            director.StateChanged += s => log.Add("state:" + s);
            director.ChapterStarted += id => log.Add("started:" + id);
            director.ChapterUnloading += id => log.Add("unloading:" + id);
            director.StepEntered += id => log.Add("step:" + id);
            director.ChapterFinished += id => log.Add("finished:" + id);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            foreach (var obj in created)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }

            created.Clear();
        }

        ChapterDefinition Chapter(string id, string scene)
        {
            var chapter = ChapterDefinition.Create(id, id + ".TITLE", id + ".DESC", id + ".Q", scene, 360f, id + ".intro", new[]
            {
                new ChapterStep("Intro", false, "Work"),
                new ChapterStep("Work", false, "End"),
                new ChapterStep("End", true),
            });
            created.Add(chapter);
            return chapter;
        }

        void PlayThrough()
        {
            director.CompleteStep("Intro");
            director.CompleteStep("Work");
            director.CompleteStep("End");
        }

        static EvidenceRecord Observation(string id) =>
            new EvidenceRecord(id, EvidenceKind.Observation, "R.result", "MS10", EvidenceAuthorship.Reconstruction);

        EvidenceRecord PlayerPhoto(string id, out Texture2D texture)
        {
            texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
            return new EvidenceRecord(id, EvidenceKind.Photo, "R.photo", "MS26", EvidenceAuthorship.PlayerCapture, sprite);
        }

        // ---------- Fluxo e independência dos capítulos ----------

        [Test]
        public void AnyChapterStartsWithoutAnotherCompletedInOrder2Then1Then3Twice()
        {
            foreach (var round in new[] { 1, 2 })
            {
                foreach (var chapter in new[] { pioneer, mars, surveyor })
                {
                    director.Begin(chapter);
                    Assert.That(director.State, Is.EqualTo(AppState.Playing), $"rodada {round}: {chapter.ChapterId}");
                    Assert.That(director.CurrentStepId, Is.EqualTo("Intro"));
                    Assert.That(evidence.Records, Is.Empty, "nada da execução anterior");
                    evidence.Add(Observation(chapter.ChapterId + ".obs"));
                    PlayThrough();
                    Assert.That(director.State, Is.EqualTo(AppState.Completion));
                    Assert.That(director.ReturnToDossier(), Is.True);
                    Assert.That(director.State, Is.EqualTo(AppState.Dossier));
                }
            }

            Assert.That(loader.Loaded, Is.Empty, "nenhuma cena de capítulo fica carregada no dossiê");
        }

        [Test]
        public void SwitchingChapterUnloadsTheOldSceneBeforeLoadingTheNext()
        {
            director.Begin(mars);
            PlayThrough();
            log.Clear();

            director.Begin(pioneer);

            Assert.That(log.Take(5), Is.EqualTo(new[] { "state:Unloading", "unloading:mars", "unload:Mars", "state:Loading", "load:Pioneer10" }));
            Assert.That(loader.Loaded, Is.EqualTo(new[] { "Pioneer10" }));
        }

        [Test]
        public void ReturningAndSwitchingDestroyPlayerPhotosAndClearEvidence()
        {
            director.Begin(mars);
            evidence.Add(PlayerPhoto("photo.valley", out var texture));
            evidence.Add(Observation("obs"));
            Assert.That(evidence.Records.Count, Is.EqualTo(2));

            director.ReturnToDossier();

            Assert.That(evidence.Records, Is.Empty);
            Assert.That(texture == null, Is.True, "a textura da foto foi destruída (memória liberada)");
        }

        [Test]
        public void RapidDoubleBeginDuringLoadingOpensTheSceneOnce()
        {
            loader.Deferred = true;
            director.Begin(mars);
            director.Begin(mars);
            director.Begin(pioneer);
            Assert.That(director.State, Is.EqualTo(AppState.Loading));

            loader.CompleteAll();

            Assert.That(log.Count(x => x == "load:Mars"), Is.EqualTo(1));
            Assert.That(log, Does.Not.Contain("load:Pioneer10"));
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(director.ReturnToDossier(), Is.True);
        }

        [Test]
        public void LoadFailureReturnsToTheDossierWithAMessage()
        {
            string message = null;
            director.LoadFailed += m => message = m;
            loader.FailNext = "sem cena";

            director.Begin(mars);

            Assert.That(director.State, Is.EqualTo(AppState.Dossier));
            Assert.That(director.Current, Is.Null);
            Assert.That(message, Does.Contain("Mars").And.Contain("sem cena"));
            Assert.That(clock.IsRunning, Is.False);
        }

        // ---------- Relógio ativo ----------

        [Test]
        public void ActiveTimeCountsOnlyWhilePlayingAndNotPaused()
        {
            clock.Tick(0.05f); // dossiê
            loader.Deferred = true;
            director.Begin(mars);
            clock.Tick(0.05f); // carregamento
            Assert.That(clock.ActiveSeconds, Is.EqualTo(0f));

            loader.CompleteAll();
            for (var i = 0; i < 10; i++)
                clock.Tick(0.05f);
            Assert.That(clock.ActiveSeconds, Is.EqualTo(0.5f).Within(1e-4f));

            PlayThrough();
            clock.Tick(0.05f); // tela de conclusão
            Assert.That(clock.ActiveSeconds, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(director.LastRunActiveSeconds, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void LongPauseNeverIncreasesActiveSeconds()
        {
            director.Begin(mars);
            clock.Tick(0.1f);

            director.Pause("menu");
            for (var i = 0; i < 36000; i++)
                clock.Tick(0.1f); // uma hora de pausa
            Assert.That(clock.ActiveSeconds, Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(clock.ActiveDeltaSeconds, Is.EqualTo(0f));

            director.Resume("menu");
            clock.Tick(0.1f);
            Assert.That(clock.ActiveSeconds, Is.EqualTo(0.2f).Within(1e-4f));
        }

        [Test]
        public void PauseIsLayeredAndHoldsThePausedInputContext()
        {
            director.Begin(mars);
            Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay));

            director.Pause("menu");
            director.Pause("focus");
            Assert.That(router.Context, Is.EqualTo(InputContext.Paused));
            director.Resume("menu");
            Assert.That(director.IsPaused, Is.True, "ainda pausado pela perda de foco");
            director.Resume("focus");

            Assert.That(director.IsPaused, Is.False);
            Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay));
            Assert.That(director.CurrentStepId, Is.EqualTo("Intro"), "pausa preserva o subestado");
        }

        [Test]
        public void HugeFrameAfterASuspendedTabIsClamped()
        {
            director.Begin(mars);
            clock.Tick(30f);
            Assert.That(clock.ActiveDeltaSeconds, Is.EqualTo(SessionClock.MaxFrameDeltaSeconds));
            Assert.That(clock.ActiveSeconds, Is.EqualTo(SessionClock.MaxFrameDeltaSeconds).Within(1e-5f));
        }

        [Test]
        public void RestartClearsEvidenceAndActiveTimeAndReloadsTheScene()
        {
            director.Begin(mars);
            director.CompleteStep("Intro");
            evidence.Add(Observation("obs"));
            clock.Tick(0.1f);
            director.Pause("menu");
            log.Clear();

            director.RestartChapter();

            Assert.That(log.Take(5), Is.EqualTo(new[] { "state:Unloading", "unloading:mars", "unload:Mars", "state:Loading", "load:Mars" }));
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(director.CurrentStepId, Is.EqualTo("Intro"));
            Assert.That(evidence.Records, Is.Empty);
            Assert.That(clock.ActiveSeconds, Is.EqualTo(0f));
            Assert.That(director.IsPaused, Is.False);
            Assert.That(router.DescribeLeases(), Is.EqualTo(new[] { "Gameplay:Chapter:mars" }), "um único contexto, sem pausa residual");
            Assert.That(director.CompleteStep("Intro"), Is.True, "a etapa pode ser concluída de novo após reiniciar");
        }

        // ---------- Etapas ----------

        [Test]
        public void StepsFollowTheTableAndAreIdempotent()
        {
            director.Begin(mars);
            Assert.That(director.CompleteStep("Work"), Is.False, "etapa futura");
            Assert.That(director.CompleteStep("Intro"), Is.True);
            Assert.That(director.CompleteStep("Intro"), Is.False, "repetida");
            director.FinishChapter();
            Assert.That(director.IsFinished, Is.False, "fim só na etapa terminal");
            director.CompleteStep("Work");
            director.CompleteStep("End");
            director.FinishChapter();

            Assert.That(log.Count(x => x == "finished:mars"), Is.EqualTo(1));
            Assert.That(director.CompleteStep("End"), Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty, "a conclusão devolve a entrada à interface");
        }

        [Test]
        public void StepsAreRejectedOutsidePlayingAndInvalidChaptersAreRefused()
        {
            Assert.That(director.CompleteStep("Intro"), Is.False);
            Assert.Throws<ArgumentNullException>(() => director.Begin(null));
            var invalid = ScriptableObject.CreateInstance<ChapterDefinition>();
            created.Add(invalid);
            Assert.Throws<ArgumentException>(() => director.Begin(invalid));
            Assert.That(director.State, Is.EqualTo(AppState.Dossier));
        }

        // ---------- EvidenceStore ----------

        [Test]
        public void EvidenceStoreLimitsPlayerPhotosAndKeepsArchiveImages()
        {
            for (var i = 0; i < EvidenceStore.MaxPlayerPhotos; i++)
                evidence.Add(PlayerPhoto("photo" + i, out _));

            Assert.Throws<InvalidOperationException>(() => evidence.Add(PlayerPhoto("photo.extra", out _)));
            Assert.That(evidence.Add(PlayerPhoto("photo0", out _)), Is.False, "ID repetido não conta no limite");
            Assert.That(evidence.Add(new EvidenceRecord("archive", EvidenceKind.Photo, "R.a", "P-S09", EvidenceAuthorship.Archive, creditKey: "CREDIT.nasa")), Is.True);
            Assert.That(evidence.PlayerPhotoCount, Is.EqualTo(EvidenceStore.MaxPlayerPhotos));
        }

        // ---------- SettingsStore ----------

        [Test]
        public void SettingsDefaultWhenMissingOrCorruptAndRoundTrip()
        {
            var storage = new MemoryStorage();
            var store = new SettingsStore(storage);
            Assert.That(store.Load(), Is.False);
            Assert.That(store.Current.languageCode, Is.EqualTo("en"), "o padrão do jogo é inglês");

            store.Apply(s =>
            {
                s.languageCode = "pt";
                s.musicVolume = 0.25f;
                s.holdMode = HoldMode.Toggle;
            });

            var reloaded = new SettingsStore(storage);
            Assert.That(reloaded.Load(), Is.True);
            Assert.That(reloaded.Current.languageCode, Is.EqualTo("pt"), "a escolha em português persiste");
            Assert.That(reloaded.Current.musicVolume, Is.EqualTo(0.25f));
            Assert.That(reloaded.Current.holdMode, Is.EqualTo(HoldMode.Toggle));

            storage.Values[SettingsStore.StorageKey] = "{ isto não é json";
            var corrupt = new SettingsStore(storage);
            Assert.DoesNotThrow(() => corrupt.Load());
            Assert.That(corrupt.Current.languageCode, Is.EqualTo("en"));
        }

        [Test]
        public void SettingsAreSanitizedAndCopiesCannotChangeTheStore()
        {
            var store = new SettingsStore(new MemoryStorage());
            store.Apply(s =>
            {
                s.languageCode = "fr";
                s.masterVolume = 7f;
                s.textScale = float.NaN;
                s.lookSensitivity = -3f;
                s.tutorialsSeen.Add("mars");
                s.tutorialsSeen.Add("mars");
                s.tutorialsSeen.Add("bad id");
            });

            var current = store.Current;
            Assert.That(current.languageCode, Is.EqualTo("en"));
            Assert.That(current.masterVolume, Is.EqualTo(1f));
            Assert.That(current.textScale, Is.EqualTo(1f));
            Assert.That(current.lookSensitivity, Is.EqualTo(0.1f));
            Assert.That(current.tutorialsSeen, Is.EqualTo(new[] { "mars" }));

            current.musicVolume = 0f;
            Assert.That(store.Current.musicVolume, Is.EqualTo(0.8f), "alterar a cópia não altera o store");
        }

        [Test]
        public void TutorialSeenIsInformationOnlyAndProgressIsNeverPersisted()
        {
            var storage = new MemoryStorage();
            var store = new SettingsStore(storage);
            store.MarkTutorialSeen("mars");
            store.MarkTutorialSeen("mars");
            Assert.That(store.HasSeenTutorial("mars"), Is.True);

            var json = storage.Values[SettingsStore.StorageKey];
            Assert.That(json, Does.Not.Contain("evidence").And.Not.Contain("step"), "nenhum progresso de partida é gravado");

            // Uma instalação com a marca ainda começa o capítulo completo do início.
            director.Begin(mars);
            Assert.That(director.CurrentStepId, Is.EqualTo("Intro"));

            store.ResetToDefaults();
            Assert.That(store.HasSeenTutorial("mars"), Is.False);
            Assert.That(storage.Values.ContainsKey(SettingsStore.StorageKey), Is.False);
        }

        // ---------- Dublês ----------

        sealed class FakeLoader : ChapterDirector.ISceneLoader
        {
            readonly List<Action> pending = new List<Action>();
            public readonly List<string> Loaded = new List<string>();
            public List<string> Log = new List<string>();
            public bool Deferred;
            public string FailNext;

            public void Load(string sceneName, Action onLoaded, Action<string> onFailed)
            {
                Log.Add("load:" + sceneName);
                if (FailNext != null)
                {
                    var error = FailNext;
                    FailNext = null;
                    onFailed(error);
                    return;
                }

                Run(() =>
                {
                    Loaded.Add(sceneName);
                    onLoaded();
                });
            }

            public void Unload(string sceneName, Action onUnloaded)
            {
                Log.Add("unload:" + sceneName);
                Run(() =>
                {
                    Loaded.Remove(sceneName);
                    onUnloaded();
                });
            }

            void Run(Action action)
            {
                if (Deferred)
                    pending.Add(action);
                else
                    action();
            }

            public void CompleteAll()
            {
                Deferred = false;
                while (pending.Count > 0)
                {
                    var next = pending[0];
                    pending.RemoveAt(0);
                    next();
                }
            }
        }

        sealed class MemoryStorage : SettingsStore.IStorage
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();

            public string Read(string key) => Values.TryGetValue(key, out var value) ? value : null;

            public void Write(string key, string value) => Values[key] = value;

            public void Delete(string key) => Values.Remove(key);
        }
    }
}
