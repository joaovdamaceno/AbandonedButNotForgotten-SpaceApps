using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// UI-005 com o <see cref="InputRouter"/> e o <see cref="InteractionService"/> reais: cada instrução só cumpre por uma
    /// ação observada, a ajuda sobe a 12 s e 30 s de tempo ATIVO sem entrar em loop, não pega a entrada do instrumento e o
    /// tutorial repete mesmo depois de visto.
    /// </summary>
    public class GuidanceControllerTests
    {
        const string TutorialId = "tst.tutorial";
        const string CandidateId = "tst.inspect";

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
        SettingsStore settings;
        IDisposable gameplay;
        readonly List<UI.GuidanceLevel> levels = new List<UI.GuidanceLevel>();
        readonly List<string> completedSteps = new List<string>();
        readonly List<string> completedSessions = new List<string>();
        int changed;

        [SetUp]
        public void SetUp()
        {
            // O NUnit reaproveita a instância da classe entre os testes: os registros precisam zerar a cada um.
            levels.Clear();
            completedSteps.Clear();
            completedSessions.Clear();
            changed = 0;
            host = new GameObject("guidance-tests");
            router = host.AddComponent<InputRouter>();
            service = host.AddComponent<InteractionService>();
            service.Initialize(router, null);
            gameplay = router.Acquire(InputContext.Gameplay, "guidance-tests");

            settings = new SettingsStore(new MemoryStorage());
            settings.Load();
            guidance = host.AddComponent<UI.GuidanceController>();
            guidance.Initialize(router, service, null, null, settings);
            guidance.LevelChanged += levels.Add;
            guidance.StepCompleted += completedSteps.Add;
            guidance.Completed += completedSessions.Add;
            guidance.Changed += () => changed++;
        }

        [TearDown]
        public void TearDown()
        {
            gameplay?.Dispose();
            UnityEngine.Object.DestroyImmediate(host);
        }

        static InteractionCandidate Candidate(string id = CandidateId, float hold = 0.4f) => new InteractionCandidate
        {
            Id = id,
            VerbKey = "TST.VERB.inspect",
            ObjectKey = "TST.OBJ.module",
            Available = true,
            DistanceMeters = 1f,
            HoldSeconds = hold,
            Priority = 1,
        };

        void HoldE(float seconds)
        {
            router.FeedInteract(true);
            service.Tick(seconds);
            router.FeedInteract(false);
        }

        // ---------- Cada instrução só some por uma ação ----------

        [Test]
        public void MoveIsDoneOnlyAfterRealMovementInputAndNeverByTime()
        {
            Assert.That(guidance.BeginTutorial(TutorialId), Is.True);
            guidance.Tick(60f);
            Assert.That(completedSteps, Is.Empty, "60 s parado não cumprem nada");

            router.FeedMove(Vector2.up);
            guidance.Tick(0.3f);
            Assert.That(completedSteps, Is.Empty, "ainda abaixo da meta de movimento");
            guidance.Tick(0.3f);
            Assert.That(completedSteps, Is.EqualTo(new[] { UI.GuidanceController.MoveStepId }));
            Assert.That(guidance.IsComplete, Is.False, "falta interagir");
            Assert.That(guidance.IsRunning, Is.True);
        }

        [Test]
        public void ATapOnTheWrongKeyOrStickDriftDoesNotCountAsMovement()
        {
            guidance.BeginTutorial(TutorialId);
            router.FeedMove(new Vector2(0.2f, 0f));
            guidance.Tick(5f);
            Assert.That(completedSteps, Is.Empty);
        }

        [Test]
        public void InteractIsDoneOnlyWhenTheChapterHoldCompletes()
        {
            service.SetCandidate(Candidate());
            guidance.BeginTutorial(TutorialId);

            router.FeedInteract(true);
            service.Tick(0.2f);
            guidance.Tick(0.2f);
            Assert.That(completedSteps, Is.Empty, "segurar não basta: tem de concluir");

            service.Tick(0.3f);
            Assert.That(completedSteps, Is.EqualTo(new[] { UI.GuidanceController.InteractStepId }));
            router.FeedInteract(false);
        }

        [Test]
        public void InteractFilterIgnoresOtherCandidates()
        {
            service.SetCandidate(Candidate("tst.other"));
            guidance.BeginTutorial(TutorialId, new UI.GuidanceOptions { InteractCandidateId = CandidateId });
            HoldE(0.5f);
            Assert.That(completedSteps, Is.Empty);

            service.ClearCandidate("tst.other");
            service.SetCandidate(Candidate());
            HoldE(0.5f);
            Assert.That(completedSteps, Is.EqualTo(new[] { UI.GuidanceController.InteractStepId }));
        }

        [Test]
        public void TutorialCompletesInAnyOrderRaisesCompletedOnceAndMarksSeen()
        {
            service.SetCandidate(Candidate());
            guidance.BeginTutorial(TutorialId);
            HoldE(0.5f);
            router.FeedMove(Vector2.right);
            guidance.Tick(1f);

            Assert.That(guidance.IsComplete, Is.True);
            Assert.That(guidance.IsRunning, Is.False);
            Assert.That(completedSessions, Is.EqualTo(new[] { TutorialId }));
            Assert.That(settings.HasSeenTutorial(TutorialId), Is.True);

            guidance.Tick(5f);
            Assert.That(completedSessions.Count, Is.EqualTo(1), "não conclui de novo");
        }

        [Test]
        public void MovementProbeIgnoresInputThatDoesNotMoveTheRover()
        {
            var meters = 0f;
            guidance.SetMovementProbe(() => meters);
            guidance.BeginTutorial(TutorialId);

            router.FeedMove(Vector2.up);
            guidance.Tick(10f);
            Assert.That(completedSteps, Is.Empty, "empurrar a parede com a tecla não é mover");

            meters = 0.6f;
            guidance.Tick(0.1f);
            Assert.That(completedSteps, Is.Empty);
            meters = 1.2f;
            guidance.Tick(0.1f);
            Assert.That(completedSteps, Is.EqualTo(new[] { UI.GuidanceController.MoveStepId }));
        }

        // ---------- Ajuda sem progresso ----------

        [Test]
        public void HelpEscalatesAtTwelveAndThirtyActiveSecondsAndNeverCompletesAnything()
        {
            guidance.BeginTutorial(TutorialId);
            guidance.Tick(11.9f);
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.None));
            guidance.Tick(0.2f);
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.Reinforce));
            guidance.Tick(17.8f);
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.Reinforce));
            guidance.Tick(0.2f);
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.Offer));
            Assert.That(completedSteps, Is.Empty);
            Assert.That(guidance.IsComplete, Is.False);
        }

        [Test]
        public void HelpDoesNotLoopAndDoesNotSpamChangeEvents()
        {
            guidance.BeginTutorial(TutorialId);
            var baseline = changed;
            for (var i = 0; i < 2000; i++)
                guidance.Tick(0.5f);

            Assert.That(levels, Is.EqualTo(new[] { UI.GuidanceLevel.Reinforce, UI.GuidanceLevel.Offer }));
            Assert.That(changed - baseline, Is.EqualTo(2), "uma notificação por nível, nada por quadro");
        }

        [Test]
        public void PauseCinematicAndPanelContextsDoNotCountAsIdleTime()
        {
            guidance.BeginTutorial(TutorialId);
            using (router.Acquire(InputContext.Paused, "pause"))
                guidance.Tick(60f);

            using (router.Acquire(InputContext.Cinematic, "cinematic"))
                guidance.Tick(60f);

            using (router.Acquire(InputContext.Panel, "panel"))
                guidance.Tick(60f);

            Assert.That(guidance.IdleSeconds, Is.EqualTo(0f));
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.None));
        }

        [Test]
        public void RealProgressResetsTheHelpButDoesNotCompleteTheStep()
        {
            guidance.BeginTutorial(TutorialId);
            guidance.Tick(31f);
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.Offer));

            router.FeedMove(Vector2.up);
            guidance.Tick(0.1f);
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.None));
            Assert.That(completedSteps, Is.Empty);
        }

        [Test]
        public void HelpKeyCountsAsParticipationOnlyWhenTheChapterHandlesIt()
        {
            guidance.BeginTutorial(TutorialId);
            var helps = 0;
            guidance.HelpUsed += () => helps++;
            guidance.Tick(31f);
            router.FeedHelp();
            Assert.That(helps, Is.EqualTo(0));
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.Offer));

            guidance.BeginTutorial(TutorialId, new UI.GuidanceOptions { HelpAvailable = true }, force: true);
            guidance.Tick(31f);
            router.FeedHelp();
            Assert.That(helps, Is.EqualTo(1));
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.None));
            Assert.That(completedSteps, Is.Empty, "H enquadra, não cumpre o tutorial");
        }

        // ---------- Não rouba a entrada ----------

        [Test]
        public void GuidanceNeverTakesAnInputContextOrTheInstrumentHold()
        {
            var leasesBefore = router.DescribeLeases().Count;
            service.SetCandidate(Candidate(hold: 2f));
            guidance.BeginTutorial(TutorialId);
            guidance.Tick(40f);
            Assert.That(router.DescribeLeases().Count, Is.EqualTo(leasesBefore), "nenhum contexto pedido");

            router.FeedInteract(true);
            Assert.That(service.HoldingId, Is.EqualTo(CandidateId), "o E continua sendo do instrumento");
            service.Tick(0.5f);
            guidance.Tick(0.5f);
            Assert.That(service.HoldingId, Is.EqualTo(CandidateId), "a ajuda não cancela nem completa o hold");
            router.FeedInteract(false);
        }

        [Test]
        public void GuidanceNeverStartsAHoldByItselfAfterTheTutorialInteraction()
        {
            service.SetCandidate(Candidate());
            guidance.BeginTutorial(TutorialId);
            HoldE(0.5f);
            Assert.That(completedSteps.Count, Is.EqualTo(1));

            guidance.Tick(40f);
            Assert.That(service.HoldingId, Is.Null, "a ajuda não inicia hold nenhum");
        }

        // ---------- Repetir ----------

        [Test]
        public void SeenTutorialDoesNotAutoStartButCanAlwaysBeRepeated()
        {
            settings.MarkTutorialSeen(TutorialId);
            Assert.That(guidance.ShouldAutoStart(TutorialId), Is.False);
            Assert.That(guidance.BeginTutorial(TutorialId), Is.False);
            Assert.That(guidance.IsRunning, Is.False);

            guidance.Repeat(TutorialId);
            Assert.That(guidance.IsRunning, Is.True);
            Assert.That(guidance.IsComplete, Is.False);

            router.FeedMove(Vector2.up);
            guidance.Tick(1f);
            Assert.That(completedSteps, Is.EqualTo(new[] { UI.GuidanceController.MoveStepId }));
        }

        [Test]
        public void RepeatingAfterCompletionStartsFromScratch()
        {
            service.SetCandidate(Candidate());
            guidance.BeginTutorial(TutorialId);
            HoldE(0.5f);
            router.FeedMove(Vector2.up);
            guidance.Tick(1f);
            Assert.That(guidance.IsComplete, Is.True);

            guidance.Repeat(TutorialId);
            Assert.That(guidance.IsRunning, Is.True);
            Assert.That(guidance.IsComplete, Is.False);
            Assert.That(guidance.IdleSeconds, Is.EqualTo(0f));
            Assert.That(guidance.Snapshot.Checklist.PendingCount, Is.EqualTo(2));
        }

        [Test]
        public void InvalidTutorialIdIsRejected()
        {
            Assert.Throws<ArgumentException>(() => guidance.BeginTutorial("id inválido"));
        }

        // ---------- Atividades (placa, sintonia, varredura) ----------

        [Test]
        public void ActivityAssistCompletesOnlyByTheChapterContractAndHelpEscalates()
        {
            var task = UI.GuidanceTask.Tuning(new UI.GuidanceOptions { HelpAvailable = true });
            guidance.BeginAssist(task);
            guidance.Tick(31f);
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.Offer));
            Assert.That(guidance.IsComplete, Is.False, "nunca conclui por tempo");

            guidance.NoteProgress();
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.None));

            Assert.That(guidance.CompleteStep(task.Step.Id), Is.True);
            Assert.That(guidance.CompleteStep(task.Step.Id), Is.False);
            Assert.That(completedSessions, Is.EqualTo(new[] { task.Step.Id }));
            Assert.That(settings.HasSeenTutorial(task.Step.Id), Is.False, "assistência de atividade não marca tutorial visto");
        }

        [Test]
        public void PanelActivitiesCountIdleTimeInPanelContextButTutorialDoesNot()
        {
            guidance.BeginAssist(UI.GuidanceTask.Sweep());
            using (router.Acquire(InputContext.Panel, "imaging"))
                guidance.Tick(13f);

            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.Reinforce));

            guidance.BeginTutorial(TutorialId);
            using (router.Acquire(InputContext.Panel, "imaging"))
                guidance.Tick(13f);

            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.None));
        }

        // ---------- Fim ----------

        [Test]
        public void EndClearsEverythingAndStopsObserving()
        {
            guidance.BeginTutorial(TutorialId);
            guidance.Tick(31f);
            guidance.End();

            Assert.That(guidance.IsRunning, Is.False);
            Assert.That(guidance.Snapshot.Checklist, Is.Null);
            router.FeedMove(Vector2.up);
            guidance.Tick(5f);
            Assert.That(completedSteps, Is.Empty);
            Assert.That(guidance.Level, Is.EqualTo(UI.GuidanceLevel.None));
        }

        [Test]
        public void ValidateSetupReportsMissingTimeSourceAndNothingElseForAWiredController()
        {
            var errors = guidance.ValidateSetup();
            Assert.That(errors.Count, Is.EqualTo(1));
            StringAssert.Contains("IActiveTimeSource", errors[0]);
        }
    }
}
