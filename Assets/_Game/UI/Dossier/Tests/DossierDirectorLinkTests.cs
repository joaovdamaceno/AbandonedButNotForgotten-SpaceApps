using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// UI-002 — "Como verificar", com o <see cref="ChapterDirector"/> real e um carregador falso e diferido:
    /// entrar e sair de cada capítulo duas vezes; clicar rápido e trocar de idioma durante o carregamento,
    /// conferindo que a cena abre uma única vez.
    /// </summary>
    public class DossierDirectorLinkTests
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        GameObject host;
        ChapterDirector director;
        DeferredLoader loader;
        UI.ChapterCatalog catalog;
        UI.DossierDirectorLink link;
        List<string> acceptedSlots;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("dossier-link-tests");
            var clock = host.AddComponent<SessionClock>();
            director = host.AddComponent<ChapterDirector>();
            loader = new DeferredLoader();
            director.Initialize(clock, null, loader, new EvidenceStore());
            director.OpenDossier();

            catalog = UI.ChapterCatalog.Create(new[]
            {
                UI.ChapterCatalog.Entry.Create("mars", Chapter("mars", "Mars"), "sa-chapter--mars"),
                UI.ChapterCatalog.Entry.Create("pioneer10", Chapter("pioneer10", "Pioneer10"), "sa-chapter--pioneer"),
                UI.ChapterCatalog.Entry.Create("surveyor3", Chapter("surveyor3", "Surveyor3"), "sa-chapter--surveyor"),
            });
            created.Add(catalog);

            link = new UI.DossierDirectorLink(director, catalog, "pt");
            acceptedSlots = new List<string>();
            link.StartAccepted += entry => acceptedSlots.Add(entry.SlotId);
        }

        [TearDown]
        public void TearDown()
        {
            link?.Dispose();
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
                new ChapterStep("Intro", false, "End"),
                new ChapterStep("End", true),
            });
            created.Add(chapter);
            return chapter;
        }

        // ---------- Início ----------

        [Test]
        public void RapidClicksOpenTheSceneExactlyOnce()
        {
            var results = new List<UI.StartResult>();
            for (var click = 0; click < 5; click++)
                results.Add(link.Start("mars"));

            Assert.That(loader.LoadCalls, Is.EqualTo(1), "cinco cliques rápidos, uma cena");
            Assert.That(results[0], Is.EqualTo(UI.StartResult.Accepted));
            Assert.That(results.FindAll(r => r == UI.StartResult.Accepted).Count, Is.EqualTo(1));
            Assert.That(director.State, Is.EqualTo(AppState.Loading));
            Assert.That(link.Flow.IsBusy, Is.True);

            loader.CompleteLoad();
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(loader.LoadCalls, Is.EqualTo(1), "terminar o carregamento não abre outra cena");
            Assert.That(acceptedSlots.Count, Is.EqualTo(1), "o gesto da pessoa é entregue uma vez");
        }

        [Test]
        public void ClickingAnotherFolderDuringLoadingDoesNotOpenASecondScene()
        {
            Assert.That(link.Start("mars"), Is.EqualTo(UI.StartResult.Accepted));
            Assert.That(link.Start("pioneer10"), Is.EqualTo(UI.StartResult.Busy));
            Assert.That(link.Start("surveyor3"), Is.EqualTo(UI.StartResult.Busy));
            Assert.That(loader.LoadCalls, Is.EqualTo(1));
            Assert.That(loader.LastScene, Is.EqualTo("Mars"));
        }

        [Test]
        public void LanguageSwitchDuringLoadingIsIgnoredAndTheSceneStillOpensOnce()
        {
            link.Start("mars");
            Assert.That(link.Flow.TrySetLanguage("en"), Is.False);
            Assert.That(link.Flow.LanguageCode, Is.EqualTo("pt"));

            loader.CompleteLoad();
            Assert.That(loader.LoadCalls, Is.EqualTo(1));

            Assert.That(director.ReturnToDossier(), Is.True);
            Assert.That(director.State, Is.EqualTo(AppState.Dossier));
            Assert.That(link.Flow.TrySetLanguage("en"), Is.True, "de volta ao arquivo o idioma muda");
        }

        [Test]
        public void MissionClockOnlyStartsAfterTheSceneIsLoaded()
        {
            link.Start("mars");
            Assert.That(director.State, Is.EqualTo(AppState.Loading));
            Assert.That(director.Clock.IsRunning, Is.False, "durante o carregamento o relógio da missão não corre");

            loader.CompleteLoad();
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(director.Clock.IsRunning, Is.True);
        }

        // ---------- Entrar e sair duas vezes, sem pré-requisito ----------

        [Test]
        public void EachChapterCanBeEnteredAndLeftTwiceInAnyOrderWithoutPrerequisites()
        {
            var expectedLoads = 0;
            foreach (var round in new[] { 1, 2 })
            {
                foreach (var slot in new[] { "surveyor3", "mars", "pioneer10" })
                {
                    Assert.That(link.Flow.Phase, Is.EqualTo(UI.DossierPhase.Browsing), $"rodada {round}: {slot}");
                    Assert.That(link.Start(slot), Is.EqualTo(UI.StartResult.Accepted), $"rodada {round}: {slot}");
                    expectedLoads++;
                    Assert.That(loader.LoadCalls, Is.EqualTo(expectedLoads), "uma abertura por entrada");

                    loader.CompleteLoad();
                    Assert.That(director.State, Is.EqualTo(AppState.Playing));
                    Assert.That(link.Flow.Phase, Is.EqualTo(UI.DossierPhase.Hidden), "dossiê some durante o capítulo");

                    Assert.That(director.ReturnToDossier(), Is.True);
                    Assert.That(director.State, Is.EqualTo(AppState.Dossier));
                    Assert.That(link.Flow.Phase, Is.EqualTo(UI.DossierPhase.Browsing));
                    Assert.That(link.Flow.IsBusy, Is.False);
                }
            }

            Assert.That(loader.UnloadCalls, Is.EqualTo(6), "cada saída descarrega a cena");
            Assert.That(acceptedSlots.Count, Is.EqualTo(6));
        }

        [Test]
        public void StartingWhileTheSceneIsStillUnloadingIsRejected()
        {
            loader.AutoUnload = false;
            link.Start("mars");
            loader.CompleteLoad();
            director.ReturnToDossier();

            Assert.That(director.State, Is.EqualTo(AppState.Unloading));
            Assert.That(link.Flow.Phase, Is.EqualTo(UI.DossierPhase.Loading), "indicação de carregamento ao voltar");
            Assert.That(link.Start("pioneer10"), Is.Not.EqualTo(UI.StartResult.Accepted));
            Assert.That(loader.LoadCalls, Is.EqualTo(1));

            loader.CompleteUnload();
            Assert.That(director.State, Is.EqualTo(AppState.Dossier));
            Assert.That(link.Start("pioneer10"), Is.EqualTo(UI.StartResult.Accepted));
            Assert.That(loader.LoadCalls, Is.EqualTo(2));
        }

        // ---------- Falhas e capítulos indisponíveis ----------

        [Test]
        public void LoadFailureReturnsToTheArchiveShowsTheErrorAndAllowsRetry()
        {
            link.Start("mars");
            loader.FailLoad("cena ausente");

            Assert.That(director.State, Is.EqualTo(AppState.Dossier));
            Assert.That(link.Flow.ErrorMessage, Is.Not.Null);
            Assert.That(link.Flow.IsBusy, Is.False);

            Assert.That(link.Start("mars"), Is.EqualTo(UI.StartResult.Accepted), "tentar de novo");
            Assert.That(loader.LoadCalls, Is.EqualTo(2));
            Assert.That(link.Flow.ErrorMessage, Is.Null);
        }

        [Test]
        public void ChapterWithoutDefinitionNeverReachesTheDirector()
        {
            var partial = UI.ChapterCatalog.Create(new[]
            {
                UI.ChapterCatalog.Entry.Create("mars", Chapter("mars", "Mars"), "sa-chapter--mars"),
                UI.ChapterCatalog.Entry.Create("pioneer10", null, "sa-chapter--pioneer"),
            });
            created.Add(partial);

            using (var partialLink = new UI.DossierDirectorLink(director, partial, "pt"))
            {
                Assert.That(partialLink.Start("pioneer10"), Is.EqualTo(UI.StartResult.NotAvailable));
                Assert.That(loader.LoadCalls, Is.EqualTo(0));
                Assert.That(director.State, Is.EqualTo(AppState.Dossier));
                Assert.That(partialLink.Start("mars"), Is.EqualTo(UI.StartResult.Accepted), "o disponível continua iniciando");
            }
        }

        [Test]
        public void InvalidCatalogIsRejectedBeforeAnythingIsWired()
        {
            var broken = UI.ChapterCatalog.Create(new[]
            {
                UI.ChapterCatalog.Entry.Create("mars", null, "sem-prefixo"),
            });
            created.Add(broken);

            Assert.Throws<ArgumentException>(() => new UI.DossierDirectorLink(director, broken, "pt"));
            Assert.Throws<ArgumentNullException>(() => new UI.DossierDirectorLink(null, catalog, "pt"));
            Assert.Throws<ArgumentNullException>(() => new UI.DossierDirectorLink(director, null, "pt"));
        }

        [Test]
        public void DisposedLinkStopsFollowingTheDirector()
        {
            link.Dispose();
            director.Begin(catalog.Entries[0].Chapter);

            Assert.That(link.Flow.Phase, Is.EqualTo(UI.DossierPhase.Browsing), "o fluxo não acompanha mais o diretor");
        }

        [Test]
        public void LinkStartsInTheDirectorsCurrentState()
        {
            link.Start("mars");
            loader.CompleteLoad();

            using (var late = new UI.DossierDirectorLink(director, catalog, "en"))
            {
                Assert.That(late.Flow.Phase, Is.EqualTo(UI.DossierPhase.Hidden), "capítulo em andamento: dossiê oculto");
                Assert.That(late.Flow.LanguageCode, Is.EqualTo("en"));
            }
        }

        /// <summary>Carregador de cenas que só conclui quando o teste manda, como o SceneManager assíncrono.</summary>
        sealed class DeferredLoader : ChapterDirector.ISceneLoader
        {
            Action onLoaded;
            Action<string> onFailed;
            Action onUnloaded;

            public int LoadCalls;
            public int UnloadCalls;
            public string LastScene;
            public bool AutoUnload = true;

            public void Load(string sceneName, Action loaded, Action<string> failed)
            {
                LoadCalls++;
                LastScene = sceneName;
                onLoaded = loaded;
                onFailed = failed;
            }

            public void Unload(string sceneName, Action unloaded)
            {
                UnloadCalls++;
                if (AutoUnload)
                    unloaded?.Invoke();
                else
                    onUnloaded = unloaded;
            }

            public void CompleteLoad()
            {
                var callback = onLoaded;
                onLoaded = null;
                onFailed = null;
                callback?.Invoke();
            }

            public void FailLoad(string message)
            {
                var callback = onFailed;
                onLoaded = null;
                onFailed = null;
                callback?.Invoke(message);
            }

            public void CompleteUnload()
            {
                var callback = onUnloaded;
                onUnloaded = null;
                callback?.Invoke();
            }
        }
    }
}
