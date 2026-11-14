using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-009: a cena <c>Mars.unity</c> montada. Prova, com os serviços REAIS do Core (ChapterDirector, SessionClock, InputRouter,
    /// InteractionService, EvidenceStore): referências sem nulos, Dossiê → M01 → M32 → arquivo, uma região por vez, reinício antes/depois da
    /// RAT e durante 2018 com recomeço completo, pausa global, datas/lugares coerentes e reconstrução que preserva a pose dos objetos.
    /// </summary>
    public class MarsChapterTests
    {
        const string ScenePath = MarsChapterAssembly.ScenePathValue;
        const float Step = 0.05f;

        sealed class ImmediateLoader : ChapterDirector.ISceneLoader
        {
            public void Load(string sceneName, Action onLoaded, Action<string> onFailed) => onLoaded();
            public void Unload(string sceneName, Action onUnloaded) => onUnloaded();
        }

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        MarsChapterBindings bindings;
        ChapterDirector director;
        SessionClock clock;
        InputRouter router;
        InteractionService interactions;
        EvidenceStore evidence;
        ChapterDefinition chapter;
        readonly List<string> entered = new List<string>();
        int controlsRaised;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            bindings = UnityEngine.Object.FindFirstObjectByType<MarsChapterBindings>(FindObjectsInactive.Include);
            Assert.That(bindings, Is.Not.Null, "a cena de Marte tem o MarsChapterBindings");
            chapter = bindings.Bindings.Chapter;
            var host = new GameObject("global services");
            created.Add(host);
            clock = host.AddComponent<SessionClock>();
            router = host.AddComponent<InputRouter>();
            interactions = host.AddComponent<InteractionService>();
            interactions.Initialize(router, clock);
            evidence = new EvidenceStore();
            director = host.AddComponent<ChapterDirector>();
            director.Initialize(clock, router, new ImmediateLoader(), evidence);
            director.StepEntered += id => entered.Add(id);
            bindings.Initialize(director, clock, router, interactions, evidence);
            bindings.ArchiveControlsRequested += () => controlsRaised++;
        }

        [TearDown]
        public void TearDown()
        {
            if (bindings != null)
                bindings.Teardown();
            foreach (var o in created)
                if (o != null)
                    UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        void LateUpdates()
        {
            foreach (var d in new MonoBehaviour[] { bindings.Landing, bindings.Spirit, bindings.Memorial })
            {
                if (d != null && d.gameObject.activeInHierarchy)
                    d.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, null);
            }

            foreach (var d in UnityEngine.Object.FindObjectsByType<MarsOriginDirector>(FindObjectsInactive.Exclude))
                d.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, null);
        }

        void Frame()
        {
            clock.Tick(Step);
            var dt = clock.ActiveDeltaSeconds;
            bindings.SequenceDirector.Tick(dt);
            LateUpdates();
            bindings.Flow.Tick(dt);
        }

        int ActiveRegions() => bindings.Regions.Count(r => r.roots.Any(g => g != null && g.activeSelf));

        int RenderedCameras()
        {
            var count = bindings.SceneCamera.enabled ? 1 : 0;
            foreach (var c in new[] { bindings.Landing.SpiritCamera, bindings.Spirit.SpiritCamera })
                if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                    count++;
            return count;
        }

        /// <summary>Joga o capítulo do primeiro quadro ao fim (cinemáticas pelo relógio; gameplay pelos eventos do fluxo).</summary>
        void PlayToEnd(Action<MarsChapterFlow.StepBinding> perFrame = null, Func<MarsChapterFlow.StepBinding, bool> stopAt = null)
        {
            var guard = 0;
            while (director.State == AppState.Playing && !director.IsFinished && guard++ < 400000)
            {
                var binding = bindings.Flow.CurrentBinding;
                if (binding == null)
                    break;

                if (stopAt != null && stopAt(binding))
                    return;

                if (!binding.IsCinematic)
                    foreach (var e in binding.CompletionEvents)
                        bindings.Flow.Raise(e);

                Frame();
                if (bindings.Flow.CurrentBinding != null)
                    perFrame?.Invoke(bindings.Flow.CurrentBinding);
            }
        }

        // ---------- Montagem ----------

        [Test]
        public void TheSceneIsWiredWithSerializedReferencesAndNoNulls()
        {
            Assert.That(bindings.ValidateSetup(), Is.Empty, ContractValidation.Describe(bindings.ValidateSetup()));
            Assert.That(bindings.Bindings.Validate(), Is.Empty, ContractValidation.Describe(bindings.Bindings.Validate()));
            Assert.That(bindings.Conductor.ValidateSetup(), Is.Empty, ContractValidation.Describe(bindings.Conductor.ValidateSetup()));
            Assert.That(bindings.IsWired, Is.True);
            Assert.That(bindings.Regions.Count, Is.EqualTo(8));
            Assert.That(ActiveRegions(), Is.EqualTo(1), "só a abertura ativa");
            Assert.That(bindings.Regions.Single(r => r.roots[0].activeSelf).regionId, Is.EqualTo(MarsChapterBindings.RegionOrigin));
            Assert.That(bindings.Rover.gameObject.activeSelf, Is.False);
            Assert.That(bindings.EaglePlaceholder, Is.True, "Eagle ainda é piso provisório identificado (MAR-012 pendente)");
            Assert.That(bindings.Rover.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(10), "o modelo da MAR-011 está no VisualRoot");

            var sums = MarsChapterFlow.CreateDefaultBindings().Sum(b => b.ReferenceSeconds);
            Assert.That(sums, Is.EqualTo(375.8f).Within(0.01f), "orçamento de 375,8 s");
            Assert.That(bindings.Bindings.Chapter.TargetDurationSeconds, Is.EqualTo(375.8f).Within(0.01f));
            Assert.That(bindings.Bindings.Chapter.SceneName, Is.EqualTo("Mars"));
        }

        [Test]
        public void EveryStepHasACoherentRegionForItsDatesAndPlaces()
        {
            var expected = new Dictionary<string, string>();
            foreach (var s in new[] { "M01", "M02", "M03" }) expected[s] = MarsChapterBindings.RegionOrigin;
            foreach (var s in new[] { "M04", "M05", "M06" }) expected[s] = MarsChapterBindings.RegionLanding;
            foreach (var s in Enumerable.Range(7, 9).Select(i => $"M{i:00}")) expected[s] = MarsChapterBindings.RegionEagle;
            foreach (var s in Enumerable.Range(16, 5).Select(i => $"M{i:00}")) expected[s] = MarsChapterBindings.RegionShield;
            expected["M21"] = MarsChapterBindings.RegionVictoria;
            expected["M22"] = MarsChapterBindings.RegionSpirit;
            foreach (var s in Enumerable.Range(23, 8).Select(i => $"M{i:00}")) expected[s] = MarsChapterBindings.RegionValley;
            expected["M31"] = expected["M32"] = MarsChapterBindings.RegionMemorial;
            Assert.That(expected.Count, Is.EqualTo(32));
            foreach (var pair in expected)
                Assert.That(bindings.Bindings.RegionOf(pair.Key), Is.EqualTo(pair.Value), pair.Key);

            var texts = JsonUtility.FromJson<MarsChapterFlow.SubtitleTable>(File.ReadAllText("Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json")).texts.ToDictionary(t => t.key, t => t.pt);
            foreach (var b in MarsChapterFlow.CreateDefaultBindings().Where(b => !string.IsNullOrEmpty(b.StampKey)))
                Assert.That(texts.ContainsKey(b.StampKey), Is.True, $"{b.StepId}: selo {b.StampKey}");
            Assert.That(texts["MAR.T05"], Does.Contain("2005"));
            Assert.That(texts["MAR.T06"], Does.Contain("2007"));
            Assert.That(texts["MAR.T07"], Does.Contain("GUSEV"));
            Assert.That(texts["MAR.T08"], Does.Contain("2018").And.Contain("PERSEVERANCE VALLEY"));
        }

        // ---------- Capítulo inteiro ----------

        [Test]
        public void DossierToM01ToM32ToArchiveWorksWithOneRegionAtATimeAndRealCoreServices()
        {
            director.OpenDossier();
            Assert.That(director.State, Is.EqualTo(AppState.Dossier));
            director.Begin(chapter);
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(entered.First(), Is.EqualTo("M01"));

            var maxCameras = new Dictionary<string, int>();
            var regionProblems = new List<string>();
            PlayToEnd(binding =>
            {
                maxCameras[binding.StepId] = Mathf.Max(maxCameras.TryGetValue(binding.StepId, out var m) ? m : 0, RenderedCameras());
                if (ActiveRegions() != 1)
                    regionProblems.Add($"{binding.StepId}: {ActiveRegions()} regiões");
                if (bindings.Bindings.RegionOf(binding.StepId) != bindings.ActiveRegionId)
                    regionProblems.Add($"{binding.StepId}: região {bindings.ActiveRegionId}");
                Assert.That(controlsRaised, Is.EqualTo(0), "controles de arquivo só depois do título");
            });

            Assert.That(director.IsFinished, Is.True, "M32 conclui o capítulo");
            Assert.That(entered, Is.EqualTo(Enumerable.Range(1, 32).Select(i => $"M{i:00}")), "as 32 etapas na ordem da tabela");
            Assert.That(regionProblems, Is.Empty, string.Join("; ", regionProblems.Take(5)));
            Assert.That(controlsRaised, Is.EqualTo(1), "controles de arquivo/replay pedidos uma vez, depois dos 4 s finais");
            Assert.That(maxCameras.Where(p => !new[] { "M04", "M05", "M22" }.Contains(p.Key)).All(p => p.Value == 1), Is.True, "somente split renderiza dois pontos de vista");
            Assert.That(maxCameras["M04"], Is.EqualTo(2));
            Assert.That(maxCameras["M22"], Is.EqualTo(2));

            Assert.That(bindings.CameraDirector.IsControlled, Is.False, "câmera devolvida");
            Assert.That(director.State, Is.EqualTo(AppState.Completion).Or.EqualTo(AppState.Playing));
            Assert.That(director.ReturnToDossier(), Is.True);
            Assert.That(director.State, Is.EqualTo(AppState.Dossier), "volta ao arquivo");
        }

        // ---------- Reinício ----------

        [TestCase("M12", TestName = "RestartDuringTheRatThenReplayToTheEnd")]
        [TestCase("M14", TestName = "RestartAfterTheRatThenReplayToTheEnd")]
        [TestCase("M25", TestName = "RestartDuring2018ThenReplayToTheEnd")]
        [TestCase("M31", TestName = "RestartInTheMemorialThenReplayToTheEnd")]
        public void RestartAtAnyPointRebuildsAFullFreshRun(string stopStep)
        {
            director.OpenDossier();
            director.Begin(chapter);
            PlayToEnd(stopAt: b => b.StepId == stopStep);
            Assert.That(bindings.Flow.CurrentBinding.StepId, Is.EqualTo(stopStep));
            evidence.Add(new EvidenceRecord("mars.test.photo", EvidenceKind.Photo, "MAR.MEMORY.valley", "MS26", EvidenceAuthorship.PlayerCapture));
            for (var i = 0; i < 20; i++)
                Frame();

            entered.Clear();
            director.RestartChapter();
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(entered.First(), Is.EqualTo("M01"), "recomeça pela primeira etapa");
            Assert.That(evidence.Records, Is.Empty, "evidências limpas pelo ChapterDirector");
            Assert.That(bindings.ActiveRegionId, Is.EqualTo(MarsChapterBindings.RegionOrigin));
            Assert.That(ActiveRegions(), Is.EqualTo(1));
            Assert.That(bindings.Rover.gameObject.activeSelf, Is.False, "rover recolhido");
            Assert.That(bindings.SequenceDirector.Current.SequenceId, Is.EqualTo("mars.seq.M01"), "M01 toca de novo");
            Assert.That(bindings.Memorial.Memory, Is.Null, "a memória escolhida foi esquecida");

            controlsRaised = 0;
            PlayToEnd();
            Assert.That(director.IsFinished, Is.True, "a segunda partida chega ao fim");
            Assert.That(entered, Is.EqualTo(Enumerable.Range(1, 32).Select(i => $"M{i:00}")));
            Assert.That(controlsRaised, Is.EqualTo(1));
        }

        // ---------- Pausa e luz ----------

        [Test]
        public void GlobalPauseFreezesTheCinematicAndTheExposureReturnsAfterward()
        {
            director.OpenDossier();
            director.Begin(chapter);
            for (var i = 0; i < 40; i++)
                Frame();

            var camera = bindings.SceneCamera.transform.position;
            var time = bindings.SequenceDirector.TimeSeconds;
            director.Pause("menu");
            for (var i = 0; i < 80; i++)
                Frame();

            Assert.That(bindings.SceneCamera.transform.position, Is.EqualTo(camera), "pausa global congela a câmera");
            Assert.That(bindings.SequenceDirector.TimeSeconds, Is.EqualTo(time), "e a sequência");
            director.Resume("menu");
            for (var i = 0; i < 20; i++)
                Frame();
            Assert.That(bindings.SequenceDirector.TimeSeconds, Is.GreaterThan(time), "retomar continua do mesmo ponto");

            var original = bindings.Sun.intensity;
            PlayToEnd();
            Assert.That(bindings.Sun.intensity, Is.EqualTo(0.77f).Or.Not.EqualTo(0f), "Sol nunca apagado");
            Assert.That(original, Is.GreaterThan(0f));
        }

        [Test]
        public void OnlyOneRegionCarriesCollidersAndShadowLightsAtATime()
        {
            director.OpenDossier();
            director.Begin(chapter);
            var problems = new List<string>();
            PlayToEnd(binding =>
            {
                var shadowLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Count(l => l.enabled && l.shadows != LightShadows.None);
                if (shadowLights > 1)
                    problems.Add($"{binding.StepId}: {shadowLights} luzes com sombra");
                var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Count(c => c.enabled);
                if (cameras > 2)
                    problems.Add($"{binding.StepId}: {cameras} câmeras");
                var inactiveRegionColliders = bindings.Regions.Where(r => !r.roots[0].activeSelf).SelectMany(r => r.roots[0].GetComponentsInChildren<Collider>(true)).Count(c => c.enabled && c.gameObject.activeInHierarchy);
                if (inactiveRegionColliders > 0)
                    problems.Add($"{binding.StepId}: colisores de região inativa ativos");
            });

            Assert.That(problems, Is.Empty, string.Join("; ", problems.Distinct().Take(5)));
        }

        static GameObject FindByName(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(t => t.gameObject).FirstOrDefault(g => g.name == name);

        // ---------- Reconstrução ----------

        [Test]
        public void RebuildingTheAssemblyPreservesTheObjectsPoseAndStaysValid()
        {
            bindings.Teardown();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var original = File.ReadAllBytes(ScenePath);
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var floor = FindByName("Eagle · PLACEHOLDER (MAR-012 pendente)");
                Assert.That(floor, Is.Not.Null);
                floor.transform.position = new Vector3(3f, 0.25f, -2f);
                var rover = UnityEngine.Object.FindFirstObjectByType<MarsRoverController>(FindObjectsInactive.Include).transform;
                rover.position = new Vector3(1f, 0.4f, 7f);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

                var result = SpaceAppsSceneBuilder.Run(new MarsChapterAssembly());
                Assert.That(result.Succeeded, Is.True, result.ToString());

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var floorAfter = FindByName("Eagle · PLACEHOLDER (MAR-012 pendente)");
                Assert.That(floorAfter.transform.position, Is.EqualTo(new Vector3(3f, 0.25f, -2f)), "a pose do piso foi preservada");
                Assert.That(UnityEngine.Object.FindFirstObjectByType<MarsRoverController>(FindObjectsInactive.Include).transform.position, Is.EqualTo(new Vector3(1f, 0.4f, 7f)), "e a do rover");
                var after = UnityEngine.Object.FindFirstObjectByType<MarsChapterBindings>(FindObjectsInactive.Include);
                Assert.That(after.ValidateSetup(), Is.Empty, ContractValidation.Describe(after.ValidateSetup()));
                Assert.That(UnityEngine.Object.FindObjectsByType<MarsChapterBindings>(FindObjectsInactive.Include).Length, Is.EqualTo(1), "sem duplicar a raiz");
                Assert.That(UnityEngine.Object.FindObjectsByType<MarsSequenceConductor>(FindObjectsInactive.Include).Length, Is.EqualTo(1));
                Assert.That(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Count(l => l.type == LightType.Directional), Is.EqualTo(1), "sem duplicar luzes");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.WriteAllBytes(ScenePath, original);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
