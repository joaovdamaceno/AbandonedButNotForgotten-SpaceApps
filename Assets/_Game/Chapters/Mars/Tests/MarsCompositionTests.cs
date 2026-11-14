using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-005: APXS e Mössbauer na mesma cavidade da RAT (M13). Prova: nada se mede antes de concluir a RAT; 4 s de E em cada
    /// instrumento dentro de blocos de 6 s; elemento × mineral em evidências distintas; a ferramenta só anda pela normal sobre o
    /// centro do disco, recua 10 cm ou mais antes de girar e nunca fica abaixo da superfície; descoberta sem água atual nem vida;
    /// pausa, foco, reinício e o ciclo real RAT → composição.
    /// </summary>
    public class MarsCompositionTests
    {
        const string PrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/CompositionActivity.prefab";
        const string RatPath = "Assets/_Game/Chapters/Mars/Prefabs/RatActivity.prefab";
        const string DataPath = "Assets/_Game/Chapters/Mars/Data/CompositionResults.asset";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Data/MarsCompositionTexts.json";
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
        MarsCompositionActivity comp;
        MarsRatActivity rat;
        RatCavitySurface cavity;
        CompositionResultsData data;
        MarsChapterFlow flow;
        MockChapterDirector director;
        EvidenceStore store;
        IDisposable gameplay;
        List<string> events;
        bool completeRatOnGround = true;

        [SetUp]
        public void SetUp()
        {
            completeRatOnGround = true; // o NUnit reaproveita a instância: um teste que desliga isto não pode vazar para os outros
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            data = AssetDatabase.LoadAssetAtPath<CompositionResultsData>(DataPath);
            Assert.That(data, Is.Not.Null, DataPath);

            var services = Track(new GameObject("services (MarsCompositionTests)"));
            router = services.AddComponent<InputRouter>();
            interaction = services.AddComponent<InteractionService>();
            time = new FakeTime();
            interaction.Initialize(router, time);
            gameplay = router.Acquire(InputContext.Gameplay, "teste");

            var ratRoot = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RatPath)));
            rat = ratRoot.GetComponent<MarsRatActivity>();
            cavity = ratRoot.GetComponentInChildren<RatCavitySurface>();
            comp = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))).GetComponent<MarsCompositionActivity>();

            director = new MockChapterDirector();
            flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            events = new List<string>();
            flow.EventAccepted += (_, e) => events.Add(e);
            // O ciclo real da RAT é provado à parte; nos demais testes a RAT "conclui" ao entrar em M13 (depois que o fim de
            // M12 reiniciou a cavidade, já que seus eventos são injetados). Registrado ANTES da atividade para valer primeiro.
            flow.StepStarted += binding =>
            {
                if (completeRatOnGround && binding.StepId == MarsCompositionActivity.StepId)
                    cavity.SetProgress(1f);
            };
            store = new EvidenceStore();
            rat.Initialize(flow, interaction, time);
            comp.Initialize(flow, interaction, time, cavity, store);
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

        static void Feed(InputRouter target, string method, params object[] args) =>
            typeof(InputRouter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        void Step(float dt)
        {
            time.ActiveDeltaSeconds = dt;
            interaction.Tick(dt);
            rat.Tick(dt);
            comp.Tick(dt);
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

        void Hold(float seconds)
        {
            Feed(router, "FeedInteract", true);
            Run(seconds);
            Feed(router, "FeedInteract", false);
            Step(Frame);
        }

        /// <summary>Em M13, com a ferramenta em contato e o E do APXS pronto.</summary>
        void ReachApxs()
        {
            PlayUntil("M13");
            Step(Frame);
            Run(data.approachSeconds + 0.1f);
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.ApxsMeasure));
            Step(Frame);
        }

        /// <summary>APXS concluído; espera o recuo e o giro e chega ao E do Mössbauer.</summary>
        void ReachMossbauer()
        {
            ReachApxs();
            Hold(4.2f);
            Run(data.retractSeconds + data.rotateSeconds + data.approachSeconds + 0.2f);
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.MossbauerMeasure));
            Step(Frame);
        }

        float Lateral(Vector3 p) => Vector3.ProjectOnPlane(p - cavity.transform.position, cavity.Normal).magnitude;

        float Along(Vector3 p) => Vector3.Dot(p - cavity.transform.position, cavity.Normal);

        // ---------- Dados ----------

        [Test]
        public void DataAndPrefabValidateAndTheClearanceCanNeverBeUnderTenCentimetres()
        {
            Assert.That(comp.ValidateWiring(), Is.Empty, ContractValidation.Describe(comp.ValidateWiring()));
            Assert.That(data.TotalSeconds, Is.EqualTo(18f).Within(0.01f), "M13: 18 s");
            Assert.That(data.apxsHoldSeconds, Is.EqualTo(4f));
            Assert.That(data.mossbauerHoldSeconds, Is.EqualTo(4f));
            Assert.That(data.BlockSeconds(true), Is.EqualTo(6f).Within(0.01f), "APXS ocupa 6 s");
            Assert.That(data.retractClearanceMeters, Is.GreaterThanOrEqualTo(0.10f));

            var tight = UnityEngine.Object.Instantiate(data);
            tight.retractClearanceMeters = 0.05f;
            Assert.That(tight.Validate(), Is.Not.Empty, "folga menor que 10 cm é recusada");
            UnityEngine.Object.DestroyImmediate(tight);
        }

        [Test]
        public void TextsSeparateElementFromMineralAndNeverClaimCurrentWaterOrLifeOrNumbers()
        {
            var table = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath).text);
            foreach (var key in new[] { data.apxsVerbKey, data.mossbauerVerbKey, data.cavityObjectKey, data.apxsResultKey, data.mossbauerResultKey, data.noteKey })
            {
                var entry = table.texts.FirstOrDefault(t => t.key == key);
                Assert.That(entry, Is.Not.Null, key);
                Assert.That(string.IsNullOrWhiteSpace(entry.pt), Is.False, key + " PT");
                Assert.That(string.IsNullOrWhiteSpace(entry.en), Is.False, key + " EN");
            }

            var apxs = table.texts.First(t => t.key == data.apxsResultKey);
            var mb = table.texts.First(t => t.key == data.mossbauerResultKey);
            Assert.That(apxs.pt.ToLowerInvariant(), Does.Contain("elemento").And.Contain("enxofre"));
            Assert.That(mb.pt.ToLowerInvariant(), Does.Contain("mineral").And.Contain("jarosita"));
            Assert.That(apxs.en.ToLowerInvariant(), Does.Contain("element"));
            Assert.That(mb.en.ToLowerInvariant(), Does.Contain("mineral"));

            Assert.That(flow.Subtitles.TryGetText(data.discoveryResultKey, "pt", out var water), Is.True);
            Assert.That(water.ToLowerInvariant(), Does.Contain("antiga"));
            foreach (var text in table.texts.Select(t => t.pt + " " + t.en).Append(water).Select(t => t.ToLowerInvariant()))
            {
                Assert.That(Regex.IsMatch(text, @"vida|life"), Is.False, "não afirma vida: " + text);
                Assert.That(Regex.IsMatch(text, @"atual|current|now"), Is.False, "não afirma água atual: " + text);
                Assert.That(Regex.IsMatch(text, @"\d"), Is.False, "nenhum espectro numérico inventado: " + text);
            }
        }

        // ---------- RAT primeiro ----------

        [Test]
        public void NothingIsMeasuredBeforeTheRatIsDone()
        {
            completeRatOnGround = false;
            PlayUntil("M13");
            Step(Frame);
            Assert.That(comp.RatCompleted, Is.False);
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.BlockedByRat));
            Run(30f);
            Assert.That(interaction.TryGetActive(out _), Is.False, "nenhum E sem a cavidade da RAT");
            Assert.That(events.Where(e => e == MarsChapterFlow.Events.ApxsMeasured || e == MarsChapterFlow.Events.MossbauerMeasured), Is.Empty);
            Assert.That(director.CurrentStepId, Is.EqualTo("M13"));
        }

        [Test]
        public void RealRatCycleThenCompositionOnTheSameCavity()
        {
            completeRatOnGround = false; // aqui a RAT é a real
            PlayUntil("M11");
            Step(Frame);
            Feed(router, "FeedInteract", true);
            Run(1.1f);
            Feed(router, "FeedInteract", false);
            Run(3.3f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M12"));
            Run(3.2f);
            Assert.That(cavity.Progress01, Is.LessThan(0.01f));
            Feed(router, "FeedInteract", true);
            Run(18.3f);
            Feed(router, "FeedInteract", false);
            Run(7f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M13"));
            Assert.That(cavity.Progress01, Is.EqualTo(1f), "a RAT abriu a janela");
            Assert.That(comp.Phase, Is.Not.EqualTo(CompositionPhase.BlockedByRat));
            Run(1.2f);
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.ApxsMeasure));
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MarsCompositionActivity.ApxsCandidateId));
            Assert.That(c.Anchor, Is.SameAs(cavity.transform), "a âncora é a cavidade da RAT");
        }

        // ---------- APXS e Mössbauer ----------

        [Test]
        public void ApxsNeedsFourSecondsOfEAndKeepsWorkWhenReleased()
        {
            ReachApxs();
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MarsCompositionActivity.ApxsCandidateId));
            Assert.That(c.VerbKey, Is.EqualTo(data.apxsVerbKey));
            Assert.That(c.ObjectKey, Is.EqualTo(data.cavityObjectKey));
            Run(5f);
            Assert.That(comp.ApxsMeasured, Is.False, "sem E nada acontece");

            Feed(router, "FeedInteract", true);
            Run(1.5f);
            Feed(router, "FeedInteract", false);
            Run(1f);
            Assert.That(comp.WorkSeconds, Is.EqualTo(1.5f).Within(0.15f), "soltar conserva");
            Hold(2.7f);
            Assert.That(comp.ApxsMeasured, Is.True);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ApxsMeasured), Is.EqualTo(1));
            Assert.That(events, Does.Not.Contain(MarsChapterFlow.Events.MossbauerMeasured), "o APXS não mede o mineral");
        }

        [Test]
        public void MossbauerOnlyAfterTheArmRetractedAndRotatedAndEventsComeInOrder()
        {
            ReachApxs();
            Hold(4.2f);
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.ApxsRetract));
            Assert.That(interaction.TryGetActive(out _), Is.False, "durante o recuo não há E");
            Run(data.retractSeconds + data.rotateSeconds + data.approachSeconds + 0.2f);
            Step(Frame);
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MarsCompositionActivity.MossbauerCandidateId));
            Assert.That(c.VerbKey, Is.EqualTo(data.mossbauerVerbKey), "outro verbo: mineral, não elemento");
            Hold(4.2f);
            Assert.That(comp.MossbauerMeasured, Is.True);
            Run(14f);
            Assert.That(events.Where(e => e.StartsWith("mars.apxs.") || e.StartsWith("mars.mossbauer.") || e.StartsWith("mars.composition.")).ToList(),
                Is.EqualTo(new[] { MarsChapterFlow.Events.ApxsMeasured, MarsChapterFlow.Events.MossbauerMeasured, MarsChapterFlow.Events.CompositionDone }),
                "uma vez cada, na ordem");
            Assert.That(director.CurrentStepId, Is.EqualTo("M14"));
            Assert.That(comp.IsEngaged, Is.False);
        }

        [Test]
        public void StepTakesEighteenSecondsOfActiveTimeWhenEIsHeldStraightAway()
        {
            PlayUntil("M13");
            var elapsed = 0f;
            while (director.CurrentStepId == "M13" && elapsed < 60f)
            {
                var wantsE = comp.Phase == CompositionPhase.ApxsMeasure || comp.Phase == CompositionPhase.MossbauerMeasure;
                Feed(router, "FeedInteract", wantsE && !interaction.AwaitingRelease);
                Step(Frame);
                elapsed += Frame;
            }

            Feed(router, "FeedInteract", false);
            Assert.That(elapsed, Is.InRange(17.5f, 19.5f), "6 + 6 + 6 s");
            Assert.That(director.CurrentStepId, Is.EqualTo("M14"));
        }

        // ---------- Evidências ----------

        [Test]
        public void ElementAndMineralAreDistinctEvidenceAndTheDiscoveryIsAncientWaterOnly()
        {
            ReachMossbauer();
            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(), Is.EqualTo(new[] { data.apxsEvidenceId }), "primeiro só o elemento");
            Hold(4.2f);
            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(), Is.EqualTo(new[] { data.apxsEvidenceId, data.mossbauerEvidenceId }), "depois o mineral");
            Run(14f);
            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(), Is.EqualTo(new[] { data.apxsEvidenceId, data.mossbauerEvidenceId, data.discoveryEvidenceId }));

            var apxs = store.Records[0];
            var mb = store.Records[1];
            var water = store.Records[2];
            Assert.That(apxs.Kind, Is.EqualTo(EvidenceKind.Composition));
            Assert.That(mb.Kind, Is.EqualTo(EvidenceKind.Composition));
            Assert.That(apxs.ResultKey, Is.Not.EqualTo(mb.ResultKey), "elemento × mineral");
            Assert.That(water.Kind, Is.EqualTo(EvidenceKind.Observation));
            Assert.That(water.ResultKey, Is.EqualTo("MAR.RESULT03"), "evidências de água antiga");
            Assert.That(store.Records.All(r => !r.CanBePresentedAsPlayers), Is.True);
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(0), "nenhuma foto do jogador gasta");
            foreach (var record in store.Records)
                Assert.That(record.Validate(), Is.Empty, record.EvidenceId);
        }

        // ---------- Trajetória da ferramenta ----------

        [Test]
        public void ToolOnlyMovesAlongTheNormalOverTheDiscCenterNeverBelowTheSurfaceAndRotatesOnlyClearOfIt()
        {
            PlayUntil("M13");
            var rotatePhases = new[] { CompositionPhase.RotateToMossbauer, CompositionPhase.RotateToStow };
            var minAlong = float.MaxValue;
            var maxLateral = 0f;
            var lastYaw = comp.Tool.GetComponentInChildren<Transform>().localEulerAngles.y;
            var sawRotation = false;
            var turret = comp.Tool.GetChild(0);
            var previousRotating = false;
            var guard = 0;
            while (director.CurrentStepId == "M13" && guard++ < 900)
            {
                var wantsE = comp.Phase == CompositionPhase.ApxsMeasure || comp.Phase == CompositionPhase.MossbauerMeasure;
                Feed(router, "FeedInteract", wantsE && !interaction.AwaitingRelease);
                Step(Frame);

                var tip = comp.Tool.position;
                minAlong = Mathf.Min(minAlong, Along(tip));
                maxLateral = Mathf.Max(maxLateral, Lateral(tip));
                var yaw = turret.localEulerAngles.y;
                var rotating = rotatePhases.Contains(comp.Phase);
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.01f)
                {
                    // O quadro que termina o giro já aparece na fase seguinte: vale o giro do quadro anterior.
                    Assert.That(rotating || previousRotating, Is.True, "o revólver só gira nas fases de giro: " + comp.Phase);
                    Assert.That(Along(tip), Is.GreaterThanOrEqualTo(0.10f - 1e-3f), "só gira com a folga de 10 cm: " + comp.Phase);
                    sawRotation = true;
                }

                lastYaw = yaw;
                previousRotating = rotating;
                if (rotating)
                    Assert.That(Along(tip), Is.GreaterThanOrEqualTo(0.10f - 1e-3f));
            }

            Feed(router, "FeedInteract", false);
            Assert.That(sawRotation, Is.True);
            Assert.That(minAlong, Is.GreaterThanOrEqualTo(data.contactStandoffMeters - 1e-3f), "nunca abaixo da superfície: sem interseção na entrada nem na saída");
            Assert.That(maxLateral, Is.LessThan(1e-3f), "a ferramenta fica sobre o centro do disco: nenhum ponto espalhado");
        }

        [Test]
        public void TrajectoryFollowsTheAnchorWhenTheRockIsPlacedAtAnotherOrientation()
        {
            cavity.transform.SetPositionAndRotation(new Vector3(3.2f, 1.1f, -4f), Quaternion.Euler(25f, 70f, 10f));
            ReachApxs();
            Assert.That(Lateral(comp.Tool.position), Is.LessThan(1e-3f));
            Assert.That(Along(comp.Tool.position), Is.EqualTo(data.contactStandoffMeters).Within(1e-3f));
            Assert.That(Vector3.Angle(comp.Tool.up, cavity.Normal), Is.LessThan(0.1f), "a ferramenta está alinhada à normal da rocha");
            Hold(4.2f);
            Run(0.4f);
            Assert.That(Lateral(comp.Tool.position), Is.LessThan(1e-3f));
            Assert.That(Along(comp.Tool.position), Is.GreaterThan(data.contactStandoffMeters));
        }

        [Test]
        public void HeadsSwapWhileRetractedAndTheTurretReturnsToStow()
        {
            ReachApxs();
            var apxs = comp.Tool.Find("Turret/ApxsHead (placeholder)").gameObject;
            var mb = comp.Tool.Find("Turret/MossbauerHead (placeholder)").gameObject;
            Assert.That(apxs.activeSelf, Is.True);
            Assert.That(mb.activeSelf, Is.False);
            Hold(4.2f);
            Run(data.retractSeconds + data.rotateSeconds + 0.1f);
            Assert.That(apxs.activeSelf, Is.False);
            Assert.That(mb.activeSelf, Is.True, "o Mössbauer está sob a ponta depois do giro");
            Run(2f);
            Hold(4.2f);
            Run(data.retractSeconds + data.rotateSeconds + 0.1f);
            Assert.That(apxs.activeSelf, Is.True, "recolhido: de volta ao APXS");
            Assert.That(mb.activeSelf, Is.False);
        }

        // ---------- Pausa, foco e reinício ----------

        [Test]
        public void PauseFreezesTheHoldTheRetractAndTheFinalReading()
        {
            ReachApxs();
            Feed(router, "FeedInteract", true);
            Run(2f);
            var worked = comp.WorkSeconds;
            for (var i = 0; i < 60; i++)
                Step(0f);
            Assert.That(comp.WorkSeconds, Is.EqualTo(worked), "pausa durante o E");
            Run(2.3f);
            Feed(router, "FeedInteract", false);
            Step(Frame);
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.ApxsRetract));
            Run(0.3f);
            var offset = comp.ToolOffsetMeters;
            for (var i = 0; i < 100; i++)
                Step(0f);
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.ApxsRetract), "pausa durante o recuo");
            Assert.That(comp.ToolOffsetMeters, Is.EqualTo(offset));
        }

        [Test]
        public void FocusLossKeepsTheWork()
        {
            ReachApxs();
            Feed(router, "FeedInteract", true);
            Run(2.5f);
            var worked = comp.WorkSeconds;
            router.NotifyFocusLost();
            Run(3f);
            Assert.That(comp.IsHolding, Is.False);
            Assert.That(comp.WorkSeconds, Is.EqualTo(worked).Within(1e-4f));
            Assert.That(comp.ApxsMeasured, Is.False);
        }

        [Test]
        public void RestartingBetweenApxsAndMossbauerResetsEverythingAndAllowsAFreshRun()
        {
            ReachMossbauer();
            Assert.That(comp.ApxsMeasured, Is.True);
            director.RestartChapter();
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.Idle));
            Assert.That(comp.ApxsMeasured, Is.False);
            Assert.That(comp.IsEngaged, Is.False);
            Assert.That(interaction.TryGetActive(out _), Is.False);
            Assert.That(comp.ApxsActive, Is.True);

            events.Clear();
            store.Clear();
            ReachApxs();
            Assert.That(comp.WorkSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void RestartingAfterTheCompositionEndedLeavesNoResidualState()
        {
            ReachMossbauer();
            Hold(4.2f);
            Run(14f);
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.Done));
            director.RestartChapter();
            Assert.That(comp.Phase, Is.EqualTo(CompositionPhase.Idle));
            Assert.That(comp.MossbauerMeasured, Is.False);
            Assert.That(comp.IsEngaged, Is.False);
        }
    }
}
