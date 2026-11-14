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
    /// MAR-004: RAT acessível. Prova: sem E não há progresso; soltar E preserva o trabalho; 18 s úteis dentro de 27 s;
    /// cavidade circular, rasa (≤ 5 mm) e em três faixas; a ferramenta segue o piso e nunca o atravessa; só uma conclusão
    /// de cada evento; pausa, rearme do E, reinício e partículas reduzidas. Não avalia o visual com o modelo final.
    /// </summary>
    public class MarsRatTests
    {
        const string PrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/RatActivity.prefab";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Data/MarsRatTexts.json";
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
        MarsRatActivity rat;
        RatCavitySurface surface;
        MarsChapterFlow flow;
        MockChapterDirector director;
        IDisposable gameplay;
        List<string> events;

        [SetUp]
        public void SetUp()
        {
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);

            var services = Track(new GameObject("services (MarsRatTests)"));
            router = services.AddComponent<InputRouter>();
            interaction = services.AddComponent<InteractionService>();
            time = new FakeTime();
            interaction.Initialize(router, time);
            gameplay = router.Acquire(InputContext.Gameplay, "teste");

            var root = Track((GameObject)PrefabUtility.InstantiatePrefab(prefab));
            rat = root.GetComponent<MarsRatActivity>();
            surface = root.GetComponentInChildren<RatCavitySurface>();

            director = new MockChapterDirector();
            flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            events = new List<string>();
            flow.EventAccepted += (_, e) => events.Add(e);
            rat.Initialize(flow, interaction, time);
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
            rat.Tick(dt);
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

        /// <summary>Entra em M12 com o braço assentado pelo M11 real (E segurado e solto).</summary>
        void ReachGrinding()
        {
            PlayUntil("M11");
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(1.1f);
            Feed(router, "FeedInteract", false);
            Run(3.2f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M12"));
            Run(3.1f); // apresentação de 3 s
            Assert.That(rat.Phase, Is.EqualTo(MarsRatPhase.Grinding));
            Step(Frame);
        }

        float TipHeight() => Vector3.Dot(rat.Tool.position - surface.transform.position, surface.Normal);

        // ---------- Prefab, dados e superfície ----------

        [Test]
        public void PrefabIsWiredAndMatchesTheRealMeasurements()
        {
            Assert.That(rat.ValidateSetup(), Is.Empty, ContractValidation.Describe(rat.ValidateSetup()));
            Assert.That(surface.RadiusMeters * 2f, Is.EqualTo(0.045f).Within(1e-5f), "~45 mm de diâmetro");
            Assert.That(surface.MaxDepthMeters, Is.LessThanOrEqualTo(0.005f), "até 5 mm");
            Assert.That(rat.GrindSeconds, Is.EqualTo(18f));
        }

        [Test]
        public void EveryRatKeyHasPortugueseAndEnglish()
        {
            var table = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath).text);
            foreach (var key in new[] { MarsRatActivity.PlaceVerbKey, MarsRatActivity.GrindVerbKey, MarsRatActivity.RockObjectKey })
            {
                var entry = table.texts.FirstOrDefault(t => t.key == key);
                Assert.That(entry, Is.Not.Null, key);
                Assert.That(string.IsNullOrWhiteSpace(entry.pt), Is.False, key + " PT");
                Assert.That(string.IsNullOrWhiteSpace(entry.en), Is.False, key + " EN");
            }
        }

        [Test]
        public void CavityIsAShallowDiscInThreeBandsThatDeepenInSequence()
        {
            surface.SetProgress(0f);
            Assert.That(surface.DeepestMeters, Is.EqualTo(0f), "sem progresso a superfície não muda");
            var flat = surface.Mesh.vertices;
            Assert.That(flat.All(v => Mathf.Abs(v.y) < 1e-7f), Is.True);

            surface.SetProgress(0.3f);
            Assert.That(surface.BandDepthMeters(0), Is.GreaterThan(surface.BandDepthMeters(1)), "a borda se define primeiro");
            Assert.That(surface.BandDepthMeters(1), Is.GreaterThan(surface.BandDepthMeters(2)));

            var previous = 0f;
            for (var i = 1; i <= 20; i++)
            {
                surface.SetProgress(i / 20f);
                Assert.That(surface.DeepestMeters, Is.GreaterThanOrEqualTo(previous - 1e-7f), "só aprofunda");
                previous = surface.DeepestMeters;
            }

            surface.SetProgress(1f);
            for (var band = 0; band < RatCavitySurface.BandCount; band++)
                Assert.That(surface.BandDepthMeters(band), Is.EqualTo(0.005f).Within(1e-6f), "faixa " + band);

            foreach (var v in surface.Mesh.vertices)
            {
                Assert.That(new Vector2(v.x, v.z).magnitude, Is.LessThanOrEqualTo(surface.RadiusMeters + 1e-6f), "tudo dentro do disco");
                Assert.That(v.y, Is.InRange(-0.005f - 1e-6f, 1e-6f), "raso: no máximo 5 mm");
            }

            Assert.That(surface.SampleDepthMeters(surface.RadiusMeters), Is.EqualTo(0f), "na borda do disco a profundidade é zero");
            Assert.That(surface.SampleDepthMeters(surface.RadiusMeters * 2f), Is.EqualTo(0f), "fora do disco não há cavidade");
        }

        [Test]
        public void AnalysisPointsCannotLeaveTheDisc()
        {
            surface.SetProgress(1f);
            var far = surface.transform.position + new Vector3(0.5f, 0f, 0.5f);
            Assert.That(surface.Contains(far), Is.False);
            var clamped = surface.ClampToDisc(far);
            Assert.That(surface.Contains(clamped), Is.True);
            Assert.That(surface.Contains(surface.transform.position + Vector3.up * 0.3f), Is.False, "longe da superfície");
        }

        [Test]
        public void ResettingTheSurfaceFlattensItAgain()
        {
            surface.SetProgress(1f);
            surface.ResetSurface();
            Assert.That(surface.DeepestMeters, Is.EqualTo(0f));
            Assert.That(surface.Progress01, Is.EqualTo(0f));
        }

        // ---------- M11 ----------

        [Test]
        public void M11NeedsEThenApproachesGraduallyAndReportsTheArmOnce()
        {
            PlayUntil("M11");
            Step(Frame);
            Assert.That(interaction.TryGetActive(out var active), Is.True);
            Assert.That(active.Id, Is.EqualTo(MarsRatActivity.PlaceCandidateId));
            Assert.That(active.VerbKey, Is.EqualTo(MarsRatActivity.PlaceVerbKey));

            Run(10f);
            Assert.That(rat.Phase, Is.EqualTo(MarsRatPhase.WaitingToPlace), "sem E nada acontece");
            Assert.That(director.CurrentStepId, Is.EqualTo("M11"));

            var states = new List<bool>();
            rat.EngagedChanged += states.Add;
            Feed(router, "FeedInteract", true);
            Run(1.1f);
            Feed(router, "FeedInteract", false);
            Assert.That(rat.Phase, Is.EqualTo(MarsRatPhase.Approaching));
            Assert.That(rat.IsEngaged, Is.True, "o rover deve ficar parado");

            var start = TipHeight();
            Run(0.5f);
            var mid = TipHeight();
            Assert.That(mid, Is.LessThan(start).And.GreaterThan(0.02f), "o braço desce aos poucos, sem salto");
            Run(3f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ArmPlaced), Is.EqualTo(1));
            Assert.That(director.CurrentStepId, Is.EqualTo("M12"));
            Assert.That(states, Is.EqualTo(new[] { true }));
        }

        [Test]
        public void ArmApproachNeverMovesFasterThanAFewCentimetresPerFrame()
        {
            PlayUntil("M11");
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(1.1f);
            Feed(router, "FeedInteract", false);
            var last = TipHeight();
            for (var i = 0; i < 60; i++)
            {
                Step(Frame);
                var now = TipHeight();
                Assert.That(Mathf.Abs(now - last), Is.LessThan(0.015f), "quadro " + i);
                last = now;
            }
        }

        // ---------- M12 ----------

        [Test]
        public void GrindingNeedsEAndNeverCompletesByTime()
        {
            ReachGrinding();
            Assert.That(interaction.TryGetActive(out var active), Is.True);
            Assert.That(active.Id, Is.EqualTo(MarsRatActivity.GrindCandidateId));
            Assert.That(active.HoldSeconds, Is.EqualTo(18f).Within(0.01f));

            Run(40f);
            Assert.That(rat.WorkSeconds, Is.EqualTo(0f));
            Assert.That(surface.Progress01, Is.EqualTo(0f));
            Assert.That(rat.SpinFraction, Is.EqualTo(0f));
            Assert.That(events, Does.Not.Contain(MarsChapterFlow.Events.RatGround));
            Assert.That(director.CurrentStepId, Is.EqualTo("M12"));
        }

        [Test]
        public void ReleasingEPausesAndKeepsTheWorkThenResumesWithTheRemainder()
        {
            ReachGrinding();
            Feed(router, "FeedInteract", true);
            Run(5f);
            var worked = rat.WorkSeconds;
            Assert.That(worked, Is.EqualTo(5f).Within(0.15f));
            Assert.That(surface.Progress01, Is.EqualTo(worked / 18f).Within(0.01f), "a cavidade acompanha o trabalho real");
            var deepAfterFive = surface.DeepestMeters;
            Assert.That(rat.SpinFraction, Is.EqualTo(1f).Within(0.01f));

            Feed(router, "FeedInteract", false);
            Step(Frame);
            Assert.That(rat.SpinFraction, Is.LessThan(1f).And.GreaterThan(0f), "desacelera ao soltar, sem parar na hora");
            Run(2f);
            Assert.That(rat.SpinFraction, Is.EqualTo(0f));
            Assert.That(rat.WorkSeconds, Is.EqualTo(worked).Within(1e-4f), "soltar preserva o trabalho");
            Assert.That(surface.DeepestMeters, Is.EqualTo(deepAfterFive).Within(1e-6f), "e a cavidade fica como estava");

            Assert.That(interaction.TryGetActive(out var active), Is.True);
            Assert.That(active.HoldSeconds, Is.EqualTo(18f - worked).Within(0.1f), "o próximo hold pede só o que falta");

            Feed(router, "FeedInteract", true);
            Run(2f);
            Assert.That(rat.WorkSeconds, Is.EqualTo(worked + 2f).Within(0.15f), "retoma de onde parou");
            Feed(router, "FeedInteract", false);
        }

        [Test]
        public void PauseFreezesTheWorkEvenWithEHeld()
        {
            ReachGrinding();
            Feed(router, "FeedInteract", true);
            Run(2f);
            var worked = rat.WorkSeconds;
            for (var i = 0; i < 60; i++)
                Step(0f);

            Assert.That(rat.WorkSeconds, Is.EqualTo(worked), "pausa");
            Run(1f);
            Assert.That(rat.WorkSeconds, Is.GreaterThan(worked + 0.8f), "volta a contar depois");
            Feed(router, "FeedInteract", false);
        }

        [Test]
        public void FocusLossStopsTheToolAndKeepsTheWork()
        {
            ReachGrinding();
            Feed(router, "FeedInteract", true);
            Run(3f);
            var worked = rat.WorkSeconds;
            router.NotifyFocusLost();
            Run(5f);
            Assert.That(rat.WorkSeconds, Is.EqualTo(worked).Within(1e-4f));
            Assert.That(rat.IsHolding, Is.False);
            Assert.That(rat.SpinFraction, Is.EqualTo(0f));
        }

        [Test]
        public void ContinuousWorkFitsTheTwentySevenSecondStepWithExactlyOneOfEachEvent()
        {
            PlayUntil("M11");
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(1.1f);
            Feed(router, "FeedInteract", false);
            for (var i = 0; i < 400 && director.CurrentStepId == "M11"; i++)
                Step(Frame);
            Assert.That(director.CurrentStepId, Is.EqualTo("M12"));

            var elapsed = 0f; // conta a partir da entrada em M12
            var holding = false;
            while (director.CurrentStepId == "M12" && elapsed < 60f)
            {
                if (rat.Phase == MarsRatPhase.Grinding && !holding)
                {
                    Feed(router, "FeedInteract", true);
                    holding = true;
                }

                Step(Frame);
                elapsed += Frame;
            }

            Feed(router, "FeedInteract", false);
            Assert.That(elapsed, Is.EqualTo(27f).Within(0.4f), "3 + 18 + 3 + 3 s de tempo ativo");
            Assert.That(director.CurrentStepId, Is.EqualTo("M13"));
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.RatGround), Is.EqualTo(1));
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.RatRetracted), Is.EqualTo(1));
            Assert.That(events.IndexOf(MarsChapterFlow.Events.RatGround), Is.LessThan(events.IndexOf(MarsChapterFlow.Events.RatRetracted)));
            Assert.That(rat.Phase, Is.EqualTo(MarsRatPhase.Done));
            Assert.That(rat.IsEngaged, Is.False, "rover liberado só depois do recolhimento");
            Assert.That(surface.DeepestMeters, Is.EqualTo(0.005f).Within(1e-6f));
            Assert.That(interaction.TryGetActive(out _), Is.False, "nenhum E sobra para uma segunda conclusão");

            Run(10f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.RatGround), Is.EqualTo(1), "continua uma só");
        }

        [Test]
        public void EHeldFromTheArmPlacementDoesNotStartGrindingUntilReleasedAndPressedAgain()
        {
            PlayUntil("M11");
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(8f); // segura E por toda a aproximação e a apresentação
            Assert.That(rat.Phase, Is.EqualTo(MarsRatPhase.Grinding));
            Assert.That(rat.WorkSeconds, Is.EqualTo(0f), "o E que abriu não conta para a medição seguinte");
            Assert.That(interaction.CanBeginHold(MarsRatActivity.GrindCandidateId), Is.False);

            Feed(router, "FeedInteract", false);
            Step(Frame);
            Assert.That(interaction.CanBeginHold(MarsRatActivity.GrindCandidateId), Is.True);
            Feed(router, "FeedInteract", true);
            Run(1f);
            Assert.That(rat.WorkSeconds, Is.GreaterThan(0.8f));
            Feed(router, "FeedInteract", false);
        }

        [Test]
        public void ToolFollowsTheFloorAndNeverGoesThroughItNorJumps()
        {
            ReachGrinding();
            Feed(router, "FeedInteract", true);
            var last = TipHeight();
            var guard = 0;
            while (director.CurrentStepId == "M12" && guard++ < 1000)
            {
                Step(Frame);
                var tip = TipHeight();
                Assert.That(tip, Is.GreaterThanOrEqualTo(-surface.DeepestMeters - 1e-5f), "a ponta nunca fica abaixo do piso");
                Assert.That(tip, Is.GreaterThanOrEqualTo(-0.005f - 1e-5f));
                Assert.That(Mathf.Abs(tip - last), Is.LessThan(0.015f), "sem salto entre quadros, inclusive ao começar a recolher");
                last = tip;
            }

            Feed(router, "FeedInteract", false);
            Assert.That(director.CurrentStepId, Is.EqualTo("M13"));
            Assert.That(TipHeight(), Is.GreaterThanOrEqualTo(0.2f), "recolhida pela normal");
        }

        [Test]
        public void ReducedParticlesCutTheDustButNotTheProgress()
        {
            ReachGrinding();
            var dust = rat.GetComponentInChildren<ParticleSystem>();
            Assert.That(dust, Is.Not.Null);
            Feed(router, "FeedInteract", true);
            Run(2f);
            var full = dust.emission.rateOverTime.constant;
            Assert.That(full, Is.GreaterThan(0f), "poeira responde no mesmo trabalho");
            var worked = rat.WorkSeconds;

            rat.SetReducedParticles(true);
            Run(1f);
            Assert.That(dust.emission.rateOverTime.constant, Is.LessThan(full * 0.5f));
            Assert.That(rat.WorkSeconds, Is.GreaterThan(worked + 0.8f), "o progresso não depende das partículas");

            Feed(router, "FeedInteract", false);
            Run(3f);
            Assert.That(dust.emission.rateOverTime.constant, Is.EqualTo(0f), "sem giro, sem poeira");
        }

        [Test]
        public void PromptAppearsOnlyWhenTheOperatorIsInRange()
        {
            var rover = Track(new GameObject("operador"));
            rat.Initialize(flow, interaction, time, rover.transform);
            rover.transform.position = surface.transform.position + new Vector3(0f, 0f, 20f);
            PlayUntil("M11");
            Step(Frame);
            Assert.That(interaction.TryGetActive(out _), Is.False, "longe: nenhum E");
            Assert.That(interaction.CanBeginHold(MarsRatActivity.PlaceCandidateId), Is.False);

            rover.transform.position = surface.transform.position + new Vector3(0f, 0f, 2f);
            Step(Frame);
            Assert.That(interaction.CanBeginHold(MarsRatActivity.PlaceCandidateId), Is.True);
        }

        [Test]
        public void RestartingInTheMiddleOfTheApproachReleasesTheRoverAndFlattensTheRock()
        {
            PlayUntil("M11");
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(1.1f);
            Feed(router, "FeedInteract", false);
            Run(1f);
            Assert.That(rat.IsEngaged, Is.True);

            director.RestartChapter();
            Assert.That(rat.IsEngaged, Is.False);
            Assert.That(rat.Phase, Is.EqualTo(MarsRatPhase.Idle));
            Assert.That(interaction.TryGetActive(out _), Is.False);

            PlayUntil("M12");
            Assert.That(surface.DeepestMeters, Is.EqualTo(0f));
        }

        [Test]
        public void RestartingMidGrindDropsPartialWorkAndAllowsAFreshRun()
        {
            ReachGrinding();
            Feed(router, "FeedInteract", true);
            Run(6f);
            Feed(router, "FeedInteract", false);
            Assert.That(surface.BandDepthMeters(0), Is.GreaterThan(0f), "a borda já foi aberta");

            director.RestartChapter();
            Assert.That(rat.WorkSeconds, Is.EqualTo(0f));
            Assert.That(surface.DeepestMeters, Is.EqualTo(0f));
            Assert.That(rat.IsEngaged, Is.False);
            Assert.That(interaction.TryGetActive(out _), Is.False);

            events.Clear();
            ReachGrinding();
            Assert.That(rat.WorkSeconds, Is.EqualTo(0f));
        }
    }
}
