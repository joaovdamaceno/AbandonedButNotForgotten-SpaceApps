using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using Abandoned.SpaceApps.Integration;
using Abandoned.SpaceApps.Integration.EditorTools;
using Abandoned.SpaceApps.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// INT-001: o Boot e as três pastas. Confere os ativos REAIS (Build Settings, catálogo, Boot.unity) e o fluxo com o
    /// <see cref="ChapterDirector"/> real e as definições reais dos três capítulos, com um carregador falso (as cenas de verdade são
    /// exercitadas em <c>Integration/Tests/PlayMode</c>).
    /// </summary>
    public class BootIntegrationTests
    {
        static readonly string[] ChapterIds = { "mars", "pioneer10", "surveyor3" };

        readonly List<GameObject> hosts = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var host in hosts)
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            }

            hosts.Clear();
        }

        // ---------- Build Settings e catálogo ----------

        [Test]
        public void BuildSettingsHaveExactlyTheFiveGameScenesWithBootFirst()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Select(s => s.path), Is.EqualTo(PlayableBootBuilder.GameScenes), "Boot, Mars, Opportunity, Pioneer10 e SurveyorChapter, nessa ordem");
            Assert.That(scenes.All(s => s.enabled), Is.True);
            Assert.That(scenes[0].path, Is.EqualTo("Assets/_Game/Scenes/Boot.unity"), "Boot é o índice 0");
        }

        [Test]
        public void BuildSettingsHaveNoPreviewPlaytestOrIsolatedEntryScene()
        {
            var paths = EditorBuildSettings.scenes.Select(s => s.path).ToList();
            foreach (var forbidden in new[] { "Preview", "Playtest", "SurveyorArchive", "ContractExample", "/Surveyor/Scenes/Surveyor.unity" })
                Assert.That(paths, Has.None.Contains(forbidden), forbidden + " não entra no jogo");
        }

        [Test]
        public void EverySceneInBuildSettingsExists()
        {
            foreach (var path in PlayableBootBuilder.GameScenes)
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
        }

        [Test]
        public void CatalogHasTheThreeFoldersAvailableAndValid()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChapterCatalog>(PlayableBootBuilder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Validate(), Is.Empty);
            Assert.That(catalog.Entries.Select(e => e.SlotId), Is.EqualTo(new[] { "mars", "pioneer10", "surveyor3" }), "ordem do dossiê");
            foreach (var entry in catalog.Entries)
            {
                Assert.That(entry.IsAvailable, Is.True, entry.SlotId + " disponível");
                Assert.That(entry.Chapter.ChapterId, Is.EqualTo(entry.SlotId), "slotId igual ao chapterId");
                if (entry.Chapter.Cover != null)
                    Assert.That(entry.CoverCredit, Is.Not.Empty, entry.SlotId + ": capa sem crédito");
            }
        }

        [Test]
        public void EveryChapterSceneIsInBuildSettings()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChapterCatalog>(PlayableBootBuilder.CatalogPath);
            var names = EditorBuildSettings.scenes.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path)).ToList();
            foreach (var entry in catalog.Entries)
                Assert.That(names, Has.Member(entry.Chapter.SceneName), entry.SlotId + " usa a cena " + entry.Chapter.SceneName);

            Assert.That(names, Has.Member("Opportunity"), "Marte carrega Opportunity em modo aditivo");
        }

        [Test]
        public void DossierContentIsCompleteInBothLanguages()
        {
            Assert.That(DossierAssetsBuilder.Validate(), Is.Empty);
        }

        [Test]
        public void BootSceneHasOneHostOneCameraAndOneListener()
        {
            var scene = EditorSceneManager.OpenScene(PlayableBootBuilder.BootPath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var hostsInScene = roots.SelectMany(r => r.GetComponentsInChildren<GameHost>(true)).ToList();
                Assert.That(hostsInScene.Count, Is.EqualTo(1));
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Count(), Is.EqualTo(1), "só a câmera de fundo do dossiê");
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).Count(), Is.EqualTo(1), "um só AudioListener no Boot");

                var serialized = new SerializedObject(hostsInScene[0]);
                foreach (var field in new[] { "catalog", "dossierPrefab", "missionHudPrefab", "subtitleLayerPrefab", "guidancePrefab", "pauseMenuPrefab", "audioDirectorPrefab", "audioCatalog", "bootCamera" })
                    Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);

                var tables = serialized.FindProperty("textTables");
                var tableNames = Enumerable.Range(0, tables.arraySize).Select(i => tables.GetArrayElementAtIndex(i).objectReferenceValue?.name).ToList();
                Assert.That(tableNames, Has.Member("Pioneer10Texts"), "legendas da Pioneer");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ---------- Fluxo com o diretor real e as definições reais ----------

        sealed class Setup
        {
            public ChapterDirector Director;
            public FakeLoader Loader;
            public EvidenceStore Evidence;
            public Dictionary<string, ChapterDefinition> Chapters;
            public readonly List<string> Started = new List<string>();
            public readonly List<string> Unloading = new List<string>();
            public readonly List<string> Steps = new List<string>();
        }

        Setup NewSetup(bool deferred = false)
        {
            var host = new GameObject("app");
            hosts.Add(host);
            var clock = host.AddComponent<SessionClock>();
            var router = host.AddComponent<InputRouter>();
            var setup = new Setup
            {
                Director = host.AddComponent<ChapterDirector>(),
                Loader = new FakeLoader { Deferred = deferred },
                Evidence = new EvidenceStore(),
            };
            setup.Director.Initialize(clock, router, setup.Loader, setup.Evidence);
            setup.Director.OpenDossier();
            var catalog = AssetDatabase.LoadAssetAtPath<ChapterCatalog>(PlayableBootBuilder.CatalogPath);
            setup.Chapters = ChapterIds.ToDictionary(id => id, id => catalog.Find(id).Chapter);
            setup.Director.ChapterStarted += id => setup.Started.Add(id);
            setup.Director.ChapterUnloading += id => setup.Unloading.Add(id);
            setup.Director.StepEntered += id => setup.Steps.Add(id);
            return setup;
        }

        static void PlayToTheEnd(ChapterDirector director)
        {
            var guard = 0;
            while (director.State == AppState.Playing && !string.IsNullOrEmpty(director.CurrentStepId) && guard++ < 64)
                director.CompleteStep(director.CurrentStepId);
        }

        [Test]
        public void RealChaptersOpenInAnyOrderTwiceWithoutAnotherCompleted()
        {
            var setup = NewSetup();
            foreach (var order in new[] { new[] { "pioneer10", "mars", "surveyor3" }, new[] { "surveyor3", "pioneer10", "mars" } })
            {
                foreach (var id in order)
                {
                    setup.Director.Begin(setup.Chapters[id]);
                    Assert.That(setup.Director.State, Is.EqualTo(AppState.Playing), id);
                    Assert.That(setup.Director.CurrentStepId, Is.EqualTo(setup.Chapters[id].InitialStepId), id + " começa na primeira etapa");
                    Assert.That(setup.Loader.Loaded, Is.EqualTo(new[] { setup.Chapters[id].SceneName }), "uma só cena de capítulo carregada");
                    setup.Evidence.Add(new EvidenceRecord(id + ".obs", EvidenceKind.Observation, "R.result", "MS10", EvidenceAuthorship.Reconstruction));
                    PlayToTheEnd(setup.Director);
                    Assert.That(setup.Director.State, Is.EqualTo(AppState.Completion), id + " chega ao próprio final");
                    Assert.That(setup.Director.ReturnToDossier(), Is.True);
                    Assert.That(setup.Director.State, Is.EqualTo(AppState.Dossier));
                    Assert.That(setup.Loader.Loaded, Is.Empty, "no dossiê nenhuma cena de capítulo fica carregada");
                    Assert.That(setup.Evidence.Records, Is.Empty, "evidências limpas ao voltar ao dossiê");
                }
            }

            Assert.That(setup.Started.Count, Is.EqualTo(6));
            Assert.That(setup.Unloading.Count, Is.EqualTo(6));
        }

        [Test]
        public void RapidClicksAndLanguageSwitchWhileLoadingOpenTheSceneOnce()
        {
            var setup = NewSetup(deferred: true);
            foreach (var id in ChapterIds)
            {
                setup.Loader.Log.Clear();
                setup.Director.Begin(setup.Chapters[id]);
                for (var i = 0; i < 5; i++)
                {
                    setup.Director.Begin(setup.Chapters[id]);
                    setup.Director.Begin(setup.Chapters[ChapterIds.First(other => other != id)]);
                }

                Assert.That(setup.Director.State, Is.EqualTo(AppState.Loading));
                Assert.That(setup.Loader.Log.Count(l => l.StartsWith("load:")), Is.EqualTo(1), id + ": a cena abre uma única vez");
                setup.Loader.Flush();
                Assert.That(setup.Started.Count(s => s == id), Is.EqualTo(1), id + ": um só ChapterStarted apesar dos cliques repetidos");
                Assert.That(setup.Director.State, Is.EqualTo(AppState.Playing));
                Assert.That(setup.Director.Current.ChapterId, Is.EqualTo(id));
                setup.Director.ReturnToDossier();
                setup.Loader.Flush();
                Assert.That(setup.Director.State, Is.EqualTo(AppState.Dossier));
            }
        }

        [Test]
        public void RestartInTheMiddleReloadsOnceAndEnteredFirstStepOnce()
        {
            foreach (var id in ChapterIds)
            {
                var setup = NewSetup();
                setup.Director.Begin(setup.Chapters[id]);
                setup.Director.CompleteStep(setup.Director.CurrentStepId);
                var secondStep = setup.Director.CurrentStepId;
                setup.Evidence.Add(new EvidenceRecord(id + ".mid", EvidenceKind.Observation, "R.result", "MS10", EvidenceAuthorship.Reconstruction));
                setup.Steps.Clear();
                setup.Started.Clear();

                setup.Director.RestartChapter();

                Assert.That(setup.Director.State, Is.EqualTo(AppState.Playing), id);
                Assert.That(setup.Director.CurrentStepId, Is.EqualTo(setup.Chapters[id].InitialStepId), id + " volta à primeira etapa");
                Assert.That(setup.Steps, Is.EqualTo(new[] { setup.Chapters[id].InitialStepId }), id + ": uma só entrada na etapa inicial (nenhum ouvinte duplicado)");
                Assert.That(setup.Started, Is.EqualTo(new[] { id }), id + ": um só ChapterStarted");
                Assert.That(setup.Evidence.Records, Is.Empty, "evidência do meio da execução não sobra");
                Assert.That(setup.Loader.Loaded.Count, Is.EqualTo(1));
                Assert.That(secondStep, Is.Not.EqualTo(setup.Chapters[id].InitialStepId));
                UnityEngine.Object.DestroyImmediate(setup.Director.gameObject);
            }
        }

        [Test]
        public void EvidenceOfOneChapterNeverReachesTheNext()
        {
            var setup = NewSetup();
            setup.Director.Begin(setup.Chapters["pioneer10"]);
            setup.Evidence.Add(new EvidenceRecord("pio.obs", EvidenceKind.Observation, "R.result", "MS10", EvidenceAuthorship.Reconstruction));
            setup.Director.ReturnToDossier();
            setup.Director.Begin(setup.Chapters["surveyor3"]);
            Assert.That(setup.Evidence.Records, Is.Empty);
            setup.Evidence.Add(new EvidenceRecord("sur.obs", EvidenceKind.Observation, "R.result", "MS10", EvidenceAuthorship.Reconstruction));
            Assert.That(setup.Evidence.Records.Count, Is.EqualTo(1), "a evidência do capítulo novo fica");
        }

        [Test]
        public void FailedBindingHookReturnsToDossierWithoutKeepingTheChapter()
        {
            var setup = NewSetup();
            setup.Loader.FailNext = "gancho recusou";
            var failures = new List<string>();
            setup.Director.LoadFailed += failures.Add;
            setup.Director.Begin(setup.Chapters["surveyor3"]);
            Assert.That(setup.Director.State, Is.EqualTo(AppState.Dossier));
            Assert.That(failures.Count, Is.EqualTo(1));
            Assert.That(setup.Loader.Loaded, Is.Empty);
            setup.Director.Begin(setup.Chapters["surveyor3"]);
            Assert.That(setup.Director.State, Is.EqualTo(AppState.Playing), "dá para tentar de novo");
        }

        sealed class FakeLoader : ChapterDirector.ISceneLoader
        {
            readonly List<Action> pending = new List<Action>();
            public readonly List<string> Loaded = new List<string>();
            public readonly List<string> Log = new List<string>();
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

            public void Flush()
            {
                while (pending.Count > 0)
                {
                    var next = pending[0];
                    pending.RemoveAt(0);
                    next();
                }
            }

            void Run(Action action)
            {
                if (Deferred)
                    pending.Add(action);
                else
                    action();
            }
        }
    }
}
