using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-001: fluxo das 32 etapas de Marte com os assets reais (MarsChapter.asset e MarsSubtitles.json).
    /// Cobre ordem sem salto, eventos inválidos/repetidos, legenda de descoberta só depois da medição, leitura mínima,
    /// pausa, reinício e validação dos dados. Arquivo previsto pela MAR-010, que pode ampliar estes casos.
    /// </summary>
    public class MarsFlowTests
    {
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const float Frame = 0.05f;

        readonly List<GameObject> hosts = new List<GameObject>();
        ChapterDefinition chapter;
        string json;
        MarsChapterFlow.SubtitleTable table;

        MockChapterDirector director;
        MarsChapterFlow flow;
        List<string> log;
        HashSet<string> openCues;

        [SetUp]
        public void SetUp()
        {
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath);
            Assert.That(chapter, Is.Not.Null, ChapterPath);
            Assert.That(text, Is.Not.Null, SubtitlesPath);
            json = text.text;
            table = MarsChapterFlow.ParseSubtitles(json, new List<string>());
            Assert.That(table, Is.Not.Null, "JsonUtility leu MarsSubtitles.json");
        }

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

        // ---------- Dados ----------

        [Test]
        public void RealAssetsValidateWithoutDivergences()
        {
            var errors = MarsChapterFlow.Validate(chapter, table, MarsChapterFlow.CreateDefaultBindings());
            Assert.That(errors, Is.Empty, ContractValidation.Describe(errors));
        }

        [Test]
        public void ChapterAssetPassesTheGlobalContractValidator()
        {
            var errors = ContractAssetValidator.Collect().Where(e => e.Contains("Chapters/Mars/") || e.Contains("'mars'")).ToList();
            Assert.That(errors, Is.Empty, ContractValidation.Describe(errors));
        }

        [Test]
        public void ChapterHas32ExplicitStepsAnd360SecondReference()
        {
            Assert.That(chapter.ChapterId, Is.EqualTo("mars"));
            Assert.That(chapter.Steps.Count, Is.EqualTo(32));
            Assert.That(chapter.InitialStepId, Is.EqualTo("M01"));
            Assert.That(chapter.IsTerminalStep("M32"), Is.True);
            for (var i = 1; i < 32; i++)
                Assert.That(chapter.CanTransition(Step(i), Step(i + 1)), Is.True, Step(i) + " → " + Step(i + 1));

            Assert.That(MarsChapterFlow.CreateDefaultBindings().Sum(b => b.ReferenceSeconds), Is.EqualTo(360f));
            Assert.That(chapter.TargetDurationSeconds, Is.EqualTo(360f));
        }

        [Test]
        public void SubtitlesHave49UniqueCuesWithPortugueseAndEnglish()
        {
            var ids = table.cues.Select(c => c.cueId).ToList();
            Assert.That(ids, Is.EqualTo(Enumerable.Range(1, 49).Select(i => $"MAR.C{i:00}").ToList()));
            foreach (var cue in table.cues)
            {
                Assert.That(string.IsNullOrWhiteSpace(cue.pt), Is.False, cue.cueId + " PT");
                Assert.That(string.IsNullOrWhiteSpace(cue.en), Is.False, cue.cueId + " EN");
            }

            Assert.That(table.cues.Count(c => c.clock == MarsChapterFlow.ClockSequence), Is.EqualTo(28));
            Assert.That(table.cues.Count(c => c.clock == MarsChapterFlow.ClockStep), Is.EqualTo(14));
            Assert.That(table.cues.Count(c => c.clock == MarsChapterFlow.ClockTrigger), Is.EqualTo(7));
            Assert.That(MarsChapterFlow.BuildSequenceCues(table, "M22").Count, Is.EqualTo(8), "Spirit: oito cartões");
        }

        [Test]
        public void ValidationRejectsBrokenData()
        {
            var bad = MarsChapterFlow.ParseSubtitles(json, new List<string>());
            bad.cues[11].triggerId = MarsChapterFlow.Events.ApxsMeasured; // C12 com gatilho de outra etapa
            bad.cues[0].en = "";
            bad.cues[1].cueId = "MAR.C01";
            bad.cues[9].clock = MarsChapterFlow.ClockSequence;            // C10 é de etapa de gameplay
            bad.cues[13].endSeconds = 40f;                                 // C14 além da referência de M12
            var errors = MarsChapterFlow.Validate(chapter, bad, MarsChapterFlow.CreateDefaultBindings());
            Assert.That(errors.Any(e => e.Contains("não é evento da etapa")), Is.True, "gatilho fora da etapa");
            Assert.That(errors.Any(e => e.Contains("sem texto EN")), Is.True, "EN ausente");
            Assert.That(errors.Any(e => e.Contains("duplicada")), Is.True, "cue duplicada");
            Assert.That(errors.Any(e => e.Contains("clock=sequence só vale")), Is.True, "relógio incoerente");
            Assert.That(errors.Any(e => e.Contains("depois da referência")), Is.True, "janela além da referência");

            var bindings = MarsChapterFlow.CreateDefaultBindings();
            bindings.RemoveAt(5);
            var missing = MarsChapterFlow.Validate(chapter, MarsChapterFlow.ParseSubtitles(json, new List<string>()), bindings);
            Assert.That(missing.Any(e => e.Contains("'M06' de MarsChapter.asset sem binding")), Is.True);

            var parseErrors = new List<string>();
            Assert.That(MarsChapterFlow.ParseSubtitles("{ nada", parseErrors), Is.Null);
            Assert.That(parseErrors.Count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => NewFlow().Initialize(new MockChapterDirector(), null, chapter, "{}"));
        }

        // ---------- Fluxo ----------

        [Test]
        public void FullRunEntersEachStepOnceInOrderAndFinishesOnce()
        {
            StartRun();
            var guard = 0;
            while (!director.IsFinished && guard++ < 100)
                PlayCurrentStep();

            var entered = log.Where(l => l.StartsWith("enter:")).Select(l => l.Substring(6)).ToList();
            Assert.That(entered, Is.EqualTo(Enumerable.Range(1, 32).Select(Step).ToList()));
            Assert.That(log.Count(l => l == "finished:mars"), Is.EqualTo(1));
            Assert.That(log.Any(l => l.StartsWith("ignored:")), Is.False, "nenhum evento válido recusado");
            Assert.That(openCues, Is.Empty, "nenhuma legenda aberta no fim");
            Assert.That(flow.CurrentStepId, Is.Null);

            var shown = log.Where(l => l.StartsWith("cue+:")).Select(l => l.Substring(5)).ToList();
            var expected = table.cues.Where(c => c.clock != MarsChapterFlow.ClockSequence).Select(c => c.cueId).ToList();
            Assert.That(shown.OrderBy(s => s).ToList(), Is.EqualTo(expected.OrderBy(s => s).ToList()), "21 legendas de etapa, uma vez cada");
        }

        [Test]
        public void DiscoveryCuesOpenOnlyAfterTheirMeasurement()
        {
            StartRun();
            var guard = 0;
            while (!director.IsFinished && guard++ < 100)
                PlayCurrentStep();

            foreach (var cue in table.cues.Where(c => c.clock == MarsChapterFlow.ClockTrigger))
            {
                var measured = log.IndexOf("event:" + cue.triggerId);
                var opened = log.IndexOf("cue+:" + cue.cueId);
                Assert.That(measured, Is.GreaterThanOrEqualTo(0), cue.triggerId);
                Assert.That(opened, Is.GreaterThan(measured), cue.cueId + " depois de " + cue.triggerId);
            }

            var hematite = log.IndexOf("cue+:MAR.C12");
            Assert.That(hematite, Is.GreaterThan(log.IndexOf("event:" + MarsChapterFlow.Events.SpheresSoilRecorded)));
            Assert.That(hematite, Is.GreaterThan(log.IndexOf("event:" + MarsChapterFlow.Events.SpheresRockRecorded)));
        }

        [Test]
        public void InvalidForeignOutOfOrderAndRepeatedEventsAreIgnored()
        {
            StartRun();
            Assert.That(flow.Raise(MarsChapterFlow.Events.SpheresSoilRecorded), Is.False, "evento de M09 em M01");
            Assert.That(flow.Raise("evento.inexistente"), Is.False);
            Assert.That(flow.Raise(null), Is.False);
            Assert.That(flow.NotifySequenceFinished(Sequence("mars.seq.M02", SequenceExitReason.Completed)), Is.False, "sequência de outra etapa");
            Assert.That(director.CurrentStepId, Is.EqualTo("M01"));

            Assert.That(flow.NotifySequenceFinished(Sequence("mars.seq.M01", SequenceExitReason.Completed)), Is.True);
            Assert.That(flow.NotifySequenceFinished(Sequence("mars.seq.M01", SequenceExitReason.Completed)), Is.False, "repetido");
            Assert.That(director.CurrentStepId, Is.EqualTo("M02"));

            PlayUntil("M09");
            Assert.That(flow.Raise(MarsChapterFlow.Events.SpheresRockRecorded), Is.False, "rocha antes do solo");
            Assert.That(flow.Raise(MarsChapterFlow.Events.SpheresHematiteIdentified), Is.False, "resultado antes das medições");
            Assert.That(log.Contains("cue+:MAR.C12"), Is.False, "descoberta não antecede a medição");
            Assert.That(flow.Raise(MarsChapterFlow.Events.SpheresSoilRecorded), Is.True);
            Assert.That(flow.Raise(MarsChapterFlow.Events.SpheresSoilRecorded), Is.False, "solo repetido");
            Assert.That(flow.Raise(MarsChapterFlow.Events.ApxsMeasured), Is.False, "etapa futura");
            Assert.That(director.CurrentStepId, Is.EqualTo("M09"));
            Assert.That(flow.NextExpectedEvent, Is.EqualTo(MarsChapterFlow.Events.SpheresRockRecorded));
        }

        [Test]
        public void CanceledSequenceNeverCompletesAndSkippedDoes()
        {
            StartRun();
            Assert.That(flow.NotifySequenceFinished(Sequence("mars.seq.M01", SequenceExitReason.Canceled)), Is.False);
            Assert.That(director.CurrentStepId, Is.EqualTo("M01"));
            Assert.That(flow.NotifySequenceFinished(Sequence("mars.seq.M01", SequenceExitReason.Skipped)), Is.True);
            Assert.That(director.CurrentStepId, Is.EqualTo("M02"));
        }

        [Test]
        public void CompletionWaitsForResultReadingAndPauseFreezesIt()
        {
            StartRun();
            PlayUntil("M09");
            Advance(6.5f); // C11 (0–6 s) já terminou
            flow.Raise(MarsChapterFlow.Events.SpheresSoilRecorded);
            flow.Raise(MarsChapterFlow.Events.SpheresRockRecorded);
            flow.Raise(MarsChapterFlow.Events.SpheresHematiteIdentified);
            Advance(1f);
            flow.Raise(MarsChapterFlow.Events.SpheresDone);
            Assert.That(flow.IsAwaitingCueReading, Is.True);

            for (var i = 0; i < 100; i++)
                flow.Tick(0f); // pausa: tempo ativo zero
            Assert.That(director.CurrentStepId, Is.EqualTo("M09"), "pausa não conclui");
            Assert.That(openCues.Contains("MAR.C12"), Is.True, "pausa não fecha a legenda");

            Advance(2.9f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M09"), "C12 ainda em leitura (3,9 s)");
            Advance(0.2f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M10"), "conclui após 4 s ativos de leitura");
            Assert.That(log.Count(l => l == "enter:M10"), Is.EqualTo(1));
        }

        [Test]
        public void StepClockCueHoldsCompletionUntilItEnds()
        {
            StartRun();
            PlayUntil("M10");
            Assert.That(openCues.Contains("MAR.C13"), Is.True, "C13 (0–7 s) abre na entrada");
            Advance(2f);
            flow.Raise(MarsChapterFlow.Events.OutcropReached);
            Assert.That(director.CurrentStepId, Is.EqualTo("M10"), "não corta C13");
            Advance(5.1f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M11"));
            Assert.That(openCues, Is.Empty);
        }

        [Test]
        public void RestartMidRunClosesCuesAndReentersOnce()
        {
            StartRun();
            PlayUntil("M13");
            flow.Raise(MarsChapterFlow.Events.ApxsMeasured);
            Assert.That(openCues.Contains("MAR.C16"), Is.True);

            log.Clear();
            director.RestartChapter();
            Assert.That(openCues, Is.Empty, "reinício fecha C16");
            Assert.That(log.IndexOf("end:M13"), Is.LessThan(log.IndexOf("start:M01")));
            Assert.That(log.Count(l => l == "start:M01"), Is.EqualTo(1));
            Assert.That(flow.NextExpectedEvent, Is.EqualTo("mars.seq.M01"));
            Assert.That(flow.Raise(MarsChapterFlow.Events.MossbauerMeasured), Is.False, "nada herdado da execução anterior");

            var guard = 0;
            while (!director.IsFinished && guard++ < 100)
                PlayCurrentStep();
            var entered = log.Where(l => l.StartsWith("enter:")).ToList();
            Assert.That(entered.Count, Is.EqualTo(32));
            Assert.That(entered.Distinct().Count(), Is.EqualTo(32), "nenhuma etapa duas vezes");
            Assert.That(log.Count(l => l == "finished:mars"), Is.EqualTo(1));
        }

        [Test]
        public void LateBindingCatchesUpAndShutdownReleases()
        {
            director = new MockChapterDirector();
            director.Begin(chapter);
            director.CompleteStep("M01");

            flow = NewFlow();
            var started = new List<string>();
            var ended = new List<string>();
            flow.StepStarted += b => started.Add(b.StepId);
            flow.StepEnded += ended.Add;
            flow.Initialize(director, null, chapter, json);
            Assert.That(started, Is.EqualTo(new List<string> { "M02" }), "cena carregada depois do Begin alcança a etapa");

            flow.Shutdown();
            Assert.That(ended, Is.EqualTo(new List<string> { "M02" }));
            Assert.That(flow.IsBound, Is.False);
            Assert.That(flow.NotifySequenceFinished(Sequence("mars.seq.M02", SequenceExitReason.Completed)), Is.False);
            Assert.That(director.CurrentStepId, Is.EqualTo("M02"));
        }

        [Test]
        public void IgnoresOtherChapters()
        {
            director = new MockChapterDirector();
            flow = NewFlow();
            flow.Initialize(director, null, chapter, json);
            var other = ChapterDefinition.Create("pioneer10", "P.TITLE", "P.DESC", "P.Q", "Pioneer10", 360f, "pio.intro",
                new[] { new ChapterStep("M01", false, "Done"), new ChapterStep("Done", true) });
            try
            {
                director.Begin(other);
                Assert.That(flow.CurrentStepId, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void RealDirectorPauseFreezesStepClockAndRestartReentersOnce()
        {
            var app = new GameObject("app (MarsFlowTests)");
            hosts.Add(app);
            var clock = app.AddComponent<SessionClock>();
            var real = app.AddComponent<ChapterDirector>();
            real.Initialize(clock, null, new ImmediateLoader(), new EvidenceStore());
            real.OpenDossier();
            real.Begin(chapter);
            Assert.That(real.State, Is.EqualTo(AppState.Playing));

            flow = NewFlow();
            var entered = new List<string>();
            flow.StepStarted += b => entered.Add(b.StepId);
            flow.Initialize(real, clock, chapter, json);
            Assert.That(flow.CurrentStepId, Is.EqualTo("M01"));

            while (real.CurrentStepId != "M08")
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                    flow.NotifySequenceFinished(Sequence(binding.SequenceId, SequenceExitReason.Completed));
                else
                    foreach (var e in binding.CompletionEvents)
                        flow.Raise(e);
                for (var i = 0; i < 300 && real.CurrentStepId == binding.StepId; i++)
                    TickWith(clock, Frame);
            }

            TickWith(clock, 1f);
            Assert.That(flow.StepActiveSeconds, Is.EqualTo(1f).Within(0.001f));
            real.Pause("teste");
            TickWith(clock, 1f);
            Assert.That(flow.StepActiveSeconds, Is.EqualTo(1f).Within(0.001f), "pausa congela o relógio da etapa");
            real.Resume("teste");
            TickWith(clock, 0.5f);
            Assert.That(flow.StepActiveSeconds, Is.EqualTo(1.5f).Within(0.001f));

            entered.Clear();
            real.RestartChapter();
            Assert.That(real.CurrentStepId, Is.EqualTo("M01"));
            Assert.That(entered, Is.EqualTo(new List<string> { "M01" }), "reinício entra em M01 uma única vez");
            Assert.That(flow.StepActiveSeconds, Is.EqualTo(0f));
        }

        // ---------- Apoio ----------

        static string Step(int number) => $"M{number:00}";

        static SequenceResult Sequence(string id, SequenceExitReason reason) => new SequenceResult(id, reason, 1f);

        MarsChapterFlow NewFlow()
        {
            var host = new GameObject("MarsChapterFlow (teste)");
            hosts.Add(host);
            return host.AddComponent<MarsChapterFlow>();
        }

        void StartRun()
        {
            director = new MockChapterDirector();
            flow = NewFlow();
            flow.Initialize(director, null, chapter, json);
            log = new List<string>();
            openCues = new HashSet<string>();
            director.StepEntered += id => log.Add("enter:" + id);
            director.ChapterFinished += id => log.Add("finished:" + id);
            flow.StepStarted += b => log.Add("start:" + b.StepId);
            flow.StepEnded += id => log.Add("end:" + id);
            flow.EventAccepted += (step, e) => log.Add("event:" + e);
            flow.EventIgnored += e => log.Add("ignored:" + e);
            flow.CueEntered += c =>
            {
                log.Add("cue+:" + c.CueId);
                Assert.That(openCues.Add(c.CueId), Is.True, "abriu duas vezes: " + c.CueId);
            };
            flow.CueExited += c =>
            {
                log.Add("cue-:" + c.CueId);
                Assert.That(openCues.Remove(c.CueId), Is.True, "fechou sem abrir: " + c.CueId);
            };
            director.Begin(chapter);
        }

        /// <summary>Joga a etapa atual no ritmo de referência: eventos distribuídos na duração editorial.</summary>
        void PlayCurrentStep()
        {
            var binding = flow.CurrentBinding;
            Assert.That(binding, Is.Not.Null, "fluxo sem etapa ativa");
            if (binding.IsCinematic)
            {
                Assert.That(flow.NotifySequenceFinished(Sequence(binding.SequenceId, SequenceExitReason.Completed)), Is.True, binding.StepId);
                return;
            }

            var gap = binding.ReferenceSeconds / binding.CompletionEvents.Count;
            foreach (var e in binding.CompletionEvents)
            {
                Advance(gap);
                Assert.That(flow.Raise(e), Is.True, e);
            }

            for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                flow.Tick(Frame);
            Assert.That(director.CurrentStepId, Is.Not.EqualTo(binding.StepId), binding.StepId + " não concluiu");
        }

        void PlayUntil(string stepId)
        {
            var guard = 0;
            while (director.CurrentStepId != stepId && guard++ < 40)
                PlayCurrentStep();
            Assert.That(director.CurrentStepId, Is.EqualTo(stepId));
        }

        void Advance(float seconds)
        {
            for (var t = 0f; t < seconds - 0.0001f; t += Frame)
                flow.Tick(Frame);
        }

        void TickWith(SessionClock clock, float seconds)
        {
            for (var t = 0f; t < seconds - 0.0001f; t += Frame)
            {
                clock.Tick(Frame);
                flow.Tick(clock.ActiveDeltaSeconds);
            }
        }

        sealed class ImmediateLoader : ChapterDirector.ISceneLoader
        {
            public void Load(string sceneName, Action onLoaded, Action<string> onFailed) => onLoaded();

            public void Unload(string sceneName, Action onUnloaded) => onUnloaded();
        }
    }
}
