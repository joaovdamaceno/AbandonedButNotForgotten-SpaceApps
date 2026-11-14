using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-003: observação guiada das esferas (M08–M09). Prova: chegada a 4 m sem E em M08; E que abre exige soltar antes
    /// de operar; 4 s + 4 s de E com pausa que conserva o trabalho; resultado mineralógico à parte das imagens; evidências
    /// que não gastam fotos do jogador; macrovisões enquadradas; uma só conclusão por evento; pausa, foco e reinício.
    /// </summary>
    public class MarsBlueberriesTests
    {
        const string PrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/BlueberriesInteraction.prefab";
        const string DataPath = "Assets/_Game/Chapters/Mars/Data/BlueberriesActivity.asset";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Data/MarsBlueberriesTexts.json";
        const float Frame = 0.05f;

        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        [Serializable]
        sealed class TextTable
        {
            [Serializable]
            public sealed class Entry
            {
                public string key;
                public string pt;
                public string en;
            }

            public List<Entry> texts = new List<Entry>();
        }

        readonly List<GameObject> hosts = new List<GameObject>();
        ChapterDefinition chapter;
        string subtitles;
        InputRouter router;
        InteractionService interaction;
        FakeTime time;
        BlueberriesActivity blue;
        BlueberriesActivityData data;
        Transform op;
        MarsChapterFlow flow;
        MockChapterDirector director;
        EvidenceStore store;
        IDisposable gameplay;
        List<string> events;

        [SetUp]
        public void SetUp()
        {
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            data = AssetDatabase.LoadAssetAtPath<BlueberriesActivityData>(DataPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            Assert.That(data, Is.Not.Null, DataPath);

            var services = Track(new GameObject("services (MarsBlueberriesTests)"));
            router = services.AddComponent<InputRouter>();
            interaction = services.AddComponent<InteractionService>();
            time = new FakeTime();
            interaction.Initialize(router, time);
            gameplay = router.Acquire(InputContext.Gameplay, "teste");

            var root = Track((GameObject)PrefabUtility.InstantiatePrefab(prefab));
            blue = root.GetComponent<BlueberriesActivity>();
            op = Track(new GameObject("operador")).transform;
            op.position = new Vector3(0f, 0f, -8f);

            director = new MockChapterDirector();
            flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            events = new List<string>();
            flow.EventAccepted += (_, e) => events.Add(e);
            store = new EvidenceStore();
            blue.Initialize(flow, interaction, time, op, store);
        }

        [TearDown]
        public void TearDown()
        {
            gameplay?.Dispose();
            foreach (var host in hosts)
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            }

            hosts.Clear();
        }

        GameObject Track(GameObject go)
        {
            hosts.Add(go);
            return go;
        }

        // O InputRouter só expõe os pontos de entrada ao assembly de testes do Core: ponte por reflexão, só em teste.
        static void Feed(InputRouter target, string method, params object[] args) =>
            typeof(InputRouter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        void Step(float dt)
        {
            time.ActiveDeltaSeconds = dt;
            interaction.Tick(dt);
            blue.Tick(dt);
            flow.Tick(dt);
            Feed(router, "EndFrame");
        }

        void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame)
                Step(Frame);
        }

        void PlayUntil(string stepId)
        {
            director.Begin(chapter);
            var guard = 0;
            while (director.CurrentStepId != stepId && guard++ < 64)
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                {
                    flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
                    continue;
                }

                foreach (var e in binding.CompletionEvents)
                    flow.Raise(e);
                for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                    flow.Tick(Frame);
            }

            Assert.That(director.CurrentStepId, Is.EqualTo(stepId));
        }

        void Near() => op.position = blue.OutcropAnchor.position + new Vector3(0f, 0f, -3f);

        /// <summary>Chega a M09 com E aberto e solto (pronto para operar).</summary>
        void ReachOpened()
        {
            PlayUntil("M08");
            Step(Frame);
            Near();
            Run(7f); // a fala C10 (6 s) é lida com o rover parado antes de M09
            Assert.That(director.CurrentStepId, Is.EqualTo("M09"));
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Step(Frame);
            Feed(router, "FeedInteract", false);
            Step(Frame);
            Assert.That(blue.Phase, Is.EqualTo(BlueberriesPhase.Soil));
        }

        // ---------- Prefab e dados ----------

        [Test]
        public void PrefabAndDataValidateAndSamplesAreMacroSized()
        {
            Assert.That(blue.ValidateSetup(), Is.Empty, ContractValidation.Describe(blue.ValidateSetup()));
            Assert.That(data.Validate(), Is.Empty);
            Assert.That(data.soilHoldSeconds, Is.EqualTo(4f));
            Assert.That(data.rockHoldSeconds, Is.EqualTo(4f));
            Assert.That(data.reachDistanceMeters, Is.EqualTo(4f), "prompt de E a 4 m");

            var soilSpheres = blue.transform.Find("Soil").GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Sphere")).ToList();
            var rockSpheres = blue.transform.Find("Rock").GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("Sphere")).ToList();
            Assert.That(soilSpheres.Count, Is.EqualTo(data.soilSphereCount));
            Assert.That(rockSpheres.Count, Is.EqualTo(data.rockSphereCount));
            foreach (var sphere in soilSpheres.Concat(rockSpheres))
                Assert.That(sphere.bounds.size.x, Is.InRange(0.004f, 0.016f), "esferas de milímetros, não bolas decorativas");
        }

        [Test]
        public void EveryTextKeyHasPortugueseAndEnglish()
        {
            var table = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath).text);
            var keys = new[] { data.openVerbKey, data.soilVerbKey, data.rockVerbKey, data.soilObjectKey, data.rockObjectKey, data.outcropObjectKey,
                data.soilResultKey, data.rockResultKey, "MAR.BLUE.mossbauer", "MAR.BLUE.note" };
            foreach (var key in keys)
            {
                var entry = table.texts.FirstOrDefault(t => t.key == key);
                Assert.That(entry, Is.Not.Null, key);
                Assert.That(string.IsNullOrWhiteSpace(entry.pt), Is.False, key + " PT");
                Assert.That(string.IsNullOrWhiteSpace(entry.en), Is.False, key + " EN");
            }

            Assert.That(flow.Subtitles.TryGetText(data.mineralResultKey, "pt", out var result), Is.True, "resultado final vem de MarsSubtitles.json");
            Assert.That(result, Does.Contain("Hematita"));
            var mi = table.texts.Where(t => t.key.StartsWith("MAR.BLUE.mi.")).Select(t => t.pt.ToLowerInvariant());
            foreach (var text in mi)
            {
                Assert.That(text, Does.Contain("forma e distribuição"), "imagem: forma e distribuição");
                Assert.That(text, Does.Not.Contain("hematita"), "a imagem não determina o mineral");
            }
        }

        // ---------- M08 ----------

        [Test]
        public void M08RequiresArrivingWithin4MetersAndShowsNoPrompt()
        {
            PlayUntil("M08");
            Step(Frame);
            Run(3f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M08"), "o tempo sozinho não conclui");
            Assert.That(interaction.TryGetActive(out _), Is.False, "M08 não tem E: o prompt vem depois");

            op.position = blue.OutcropAnchor.position + new Vector3(0f, 0f, -4.5f);
            Run(1f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M08"), "4,5 m ainda é longe");

            op.position = blue.OutcropAnchor.position + new Vector3(0f, 0f, -3.9f);
            Run(0.3f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.SpheresReached), Is.EqualTo(1));
            Assert.That(blue.IsEngaged, Is.True, "rover parado enquanto a fala sobre as esferas é lida");
            Assert.That(director.CurrentStepId, Is.EqualTo("M08"), "a etapa espera a leitura da legenda C10");
            Run(6.5f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M09"));
        }

        // ---------- M09 ----------

        [Test]
        public void OpenPromptAppearsOnlyInRangeAndTheOpeningEDoesNotOperate()
        {
            PlayUntil("M08");
            Step(Frame);
            Near();
            Run(7f);
            Step(Frame);
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(BlueberriesActivity.OpenCandidateId));
            Assert.That(c.VerbKey, Is.EqualTo(data.openVerbKey));

            op.position = blue.OutcropAnchor.position + new Vector3(0f, 0f, -6f);
            Step(Frame);
            Assert.That(interaction.CanBeginHold(BlueberriesActivity.OpenCandidateId), Is.False, "fora de 4 m não há E");

            Near();
            Step(Frame);
            Feed(router, "FeedInteract", true); // o E que abre e fica pressionado
            Step(Frame);
            Assert.That(blue.Phase, Is.EqualTo(BlueberriesPhase.Soil));
            Assert.That(blue.IsEngaged, Is.True, "rover parado durante a observação");
            Run(6f);
            Assert.That(blue.WorkSeconds, Is.EqualTo(0f), "o E que abriu não opera: é preciso soltar");
            Assert.That(interaction.CanBeginHold(BlueberriesActivity.SoilCandidateId), Is.False);

            Feed(router, "FeedInteract", false);
            Step(Frame);
            Assert.That(interaction.CanBeginHold(BlueberriesActivity.SoilCandidateId), Is.True);
        }

        [Test]
        public void ReleasingEPausesWithoutLosingWorkAndResumesWithTheRemainder()
        {
            ReachOpened();
            Feed(router, "FeedInteract", true);
            Run(2f);
            Assert.That(blue.WorkSeconds, Is.EqualTo(2f).Within(0.15f));
            Feed(router, "FeedInteract", false);
            Run(1f);
            var worked = blue.WorkSeconds;
            Assert.That(worked, Is.EqualTo(2f).Within(0.15f), "soltar não apaga");
            Assert.That(blue.SoilRecorded, Is.False);

            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.HoldSeconds, Is.EqualTo(4f - worked).Within(0.1f), "pede só o que falta");
            Feed(router, "FeedInteract", true);
            Run(2.3f);
            Feed(router, "FeedInteract", false);
            Assert.That(blue.SoilRecorded, Is.True);
            Assert.That(blue.Phase, Is.EqualTo(BlueberriesPhase.Rock));
        }

        [Test]
        public void MashingEDoesNotDoubleRegisterOrSkipSteps()
        {
            ReachOpened();
            for (var i = 0; i < 40; i++)
            {
                Feed(router, "FeedInteract", true);
                Step(0.02f);
                Feed(router, "FeedInteract", false);
                Step(0.02f);
            }

            Assert.That(blue.SoilRecorded, Is.False, "40 toques de 0,02 s somam menos de 1 s de trabalho real: não registram");
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.SpheresSoilRecorded), Is.LessThanOrEqualTo(1));
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.SpheresRockRecorded), Is.EqualTo(0));
            Assert.That(blue.WorkSeconds, Is.LessThanOrEqualTo(4f + 1e-3f));
        }

        [Test]
        public void FullRunRecordsSoilThenRockThenMineralThenFinishesOnce()
        {
            ReachOpened();
            var phases = new List<BlueberriesPhase>();
            blue.PhaseChanged += phases.Add;

            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Assert.That(blue.SoilRecorded, Is.True);
            Assert.That(blue.RockRecorded, Is.False);
            Assert.That(blue.MineralIdentified, Is.False, "o mineral não é identificado só com a imagem do solo");

            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Assert.That(blue.RockRecorded, Is.True);
            Assert.That(blue.MineralIdentified, Is.True, "Mössbauer, medição separada, depois das duas imagens");
            Assert.That(director.CurrentStepId, Is.EqualTo("M09"), "ainda mostra o resultado");

            Run(5f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M10"));
            var order = new[] { MarsChapterFlow.Events.SpheresReached, MarsChapterFlow.Events.SpheresSoilRecorded, MarsChapterFlow.Events.SpheresRockRecorded,
                MarsChapterFlow.Events.SpheresHematiteIdentified, MarsChapterFlow.Events.SpheresDone };
            Assert.That(events.Where(e => e.StartsWith("mars.spheres.")).ToList(), Is.EqualTo(order), "uma vez cada, na ordem");
            Assert.That(phases, Is.EqualTo(new[] { BlueberriesPhase.Rock, BlueberriesPhase.Result, BlueberriesPhase.Done }));
            Assert.That(blue.IsEngaged, Is.False, "rover liberado");
            Assert.That(interaction.TryGetActive(out _), Is.False, "nenhum E sobra");

            Run(10f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.SpheresDone), Is.EqualTo(1));
        }

        [Test]
        public void ResultIsPresentedForFourSecondsBeforeTheStepCompletes()
        {
            ReachOpened();
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(4.1f);
            Feed(router, "FeedInteract", false);
            Assert.That(blue.Phase, Is.EqualTo(BlueberriesPhase.Result));
            Run(3.5f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M09"), "resultado de 4 s ainda na tela");
            Run(1.5f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M10"));
        }

        // ---------- Evidências ----------

        [Test]
        public void EvidenceKeepsImagesAsShapeAndDistributionAndTheMineralSeparateWithoutSpendingPlayerPhotos()
        {
            ReachOpened();
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(), Is.EqualTo(new[] { data.soilEvidenceId }), "antes do solo registrado só há a imagem do solo");

            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);

            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(), Is.EqualTo(new[] { data.soilEvidenceId, data.rockEvidenceId, data.mineralEvidenceId }),
                "as duas imagens do MI vêm antes do resultado mineralógico");
            var soil = store.Records[0];
            var rock = store.Records[1];
            var mineral = store.Records[2];
            Assert.That(soil.Kind, Is.EqualTo(EvidenceKind.Observation));
            Assert.That(rock.Kind, Is.EqualTo(EvidenceKind.Observation));
            Assert.That(mineral.Kind, Is.EqualTo(EvidenceKind.Composition), "mineral é composição medida, separada");
            Assert.That(soil.ScienceRef, Is.EqualTo("MS13"));
            Assert.That(mineral.ScienceRef, Is.EqualTo("MS03"));
            Assert.That(store.Records.All(r => !r.CanBePresentedAsPlayers), Is.True, "nada aqui é apresentado como foto do jogador");
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(0), "as 3 fotos do jogador ficam para escudo, Sol e vale");
            foreach (var record in store.Records)
                Assert.That(record.Validate(), Is.Empty, record.EvidenceId);
        }

        // ---------- Macrovisões e marcas ----------

        [Test]
        public void MacroViewsAreGuidedAndFramedOnTheirSamples()
        {
            var views = new List<Transform>();
            blue.ViewChanged += views.Add;
            ReachOpened();
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Run(5f);

            Assert.That(views.Count, Is.EqualTo(3), "solo, rocha e fim");
            Assert.That(views[0].parent.name, Is.EqualTo("Soil"));
            Assert.That(views[1].parent.name, Is.EqualTo("Rock"));
            Assert.That(views[2], Is.Null);

            foreach (var (view, sample) in new[] { (views[0], "Soil"), (views[1], "Rock") })
            {
                var target = view.parent.position + Vector3.up * 0.06f;
                Assert.That(Vector3.Distance(view.position, target), Is.InRange(0.25f, 0.5f), sample + ": macro a poucos centímetros");
                Assert.That(Vector3.Angle(view.forward, target - view.position), Is.LessThan(25f), sample + ": já enquadrada, sem mira");
            }
        }

        [Test]
        public void RegistrationMarksSitOnTheSampleAndAppearOnlyAfterEachRecord()
        {
            var soilMark = blue.transform.Find("Soil/Mark").gameObject;
            var rockMark = blue.transform.Find("Rock/Mark").gameObject;
            Assert.That(soilMark.activeSelf, Is.False);
            Assert.That(rockMark.activeSelf, Is.False);
            ReachOpened();
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Assert.That(soilMark.activeSelf, Is.True);
            Assert.That(rockMark.activeSelf, Is.False);
            Assert.That(soilMark.transform.position.y - blue.transform.position.y, Is.LessThan(0.02f), "assentada no chão, não flutuando");
            Assert.That(soilMark.transform.GetComponent<Collider>(), Is.Null);
        }

        // ---------- Pausa, foco e reinício ----------

        [Test]
        public void PauseFreezesTheWorkAndFocusLossKeepsIt()
        {
            ReachOpened();
            Feed(router, "FeedInteract", true);
            Run(1.5f);
            var worked = blue.WorkSeconds;
            for (var i = 0; i < 60; i++)
                Step(0f);
            Assert.That(blue.WorkSeconds, Is.EqualTo(worked), "pausa");

            router.NotifyFocusLost();
            Run(3f);
            Assert.That(blue.IsHolding, Is.False);
            Assert.That(blue.WorkSeconds, Is.EqualTo(worked).Within(1e-4f), "perder foco conserva o trabalho");
            Assert.That(blue.SoilRecorded, Is.False);
        }

        [Test]
        public void RestartingAfterACompletedObservationLeavesNoResidualState()
        {
            ReachOpened();
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Run(5f);
            Assert.That(blue.Phase, Is.EqualTo(BlueberriesPhase.Done));
            Assert.That(blue.transform.Find("Soil/Mark").gameObject.activeSelf, Is.True);

            director.RestartChapter();
            Assert.That(blue.Phase, Is.EqualTo(BlueberriesPhase.Idle), "reinício limpa também a observação já concluída");
            Assert.That(blue.SoilRecorded, Is.False);
            Assert.That(blue.RockRecorded, Is.False);
            Assert.That(blue.MineralIdentified, Is.False);
            Assert.That(blue.transform.Find("Soil/Mark").gameObject.activeSelf, Is.False);
            Assert.That(blue.transform.Find("Rock/Mark").gameObject.activeSelf, Is.False);
            Assert.That(blue.CurrentView, Is.Null);
            Assert.That(blue.IsEngaged, Is.False);
            Assert.That(interaction.TryGetActive(out _), Is.False);
        }

        [Test]
        public void RestartingMidObservationClearsTheMarksReleasesTheRoverAndAllowsAFreshRun()
        {
            ReachOpened();
            Feed(router, "FeedInteract", true);
            Run(4.2f);
            Feed(router, "FeedInteract", false);
            Assert.That(blue.SoilRecorded, Is.True);

            director.RestartChapter();
            Assert.That(blue.Phase, Is.EqualTo(BlueberriesPhase.Idle));
            Assert.That(blue.IsEngaged, Is.False);
            Assert.That(blue.SoilRecorded, Is.False);
            Assert.That(blue.CurrentView, Is.Null);
            Assert.That(blue.transform.Find("Soil/Mark").gameObject.activeSelf, Is.False);
            Assert.That(interaction.TryGetActive(out _), Is.False);

            events.Clear();
            ReachOpened();
            Assert.That(blue.WorkSeconds, Is.EqualTo(0f));
        }
    }
}
