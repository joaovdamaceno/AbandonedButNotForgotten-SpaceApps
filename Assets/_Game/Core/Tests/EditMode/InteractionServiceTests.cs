using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>CORE-003: o serviço liga E aos candidatos sem vazar comandos entre atividades, pausa ou foco.</summary>
    public class InteractionServiceTests
    {
        GameObject host;
        InputRouter router;
        InteractionService service;
        List<string> log;
        IDisposable gameplay;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("interaction");
            router = host.AddComponent<InputRouter>();
            service = host.AddComponent<InteractionService>();
            service.Initialize(router, null);
            gameplay = router.Acquire(InputContext.Gameplay, "test");

            log = new List<string>();
            service.HoldStarted += id => log.Add("start:" + id);
            service.HoldProgress += (id, p) => log.Add($"progress:{id}:{p.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}");
            service.HoldCompleted += id => log.Add("done:" + id);
            service.HoldCanceled += id => log.Add("cancel:" + id);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        static InteractionCandidate Candidate(string id, float hold = 2f, int priority = 0, float distance = 1f, bool available = true) =>
            new InteractionCandidate { Id = id, VerbKey = "V.verb", ObjectKey = "O.obj", Available = available, DistanceMeters = distance, HoldSeconds = hold, Priority = priority };

        [Test]
        public void PressHoldAndCompleteRaisesEachEventOnce()
        {
            service.SetCandidate(Candidate("a"));
            router.FeedInteract(true);
            service.Tick(1f);
            service.Tick(1f);

            Assert.That(log, Is.EqualTo(new[] { "start:a", "progress:a:0.50", "progress:a:1.00", "done:a" }));
            Assert.That(service.HoldingId, Is.Null);
        }

        [Test]
        public void KeepingEPressedNeverCrossesToTheNextActivity()
        {
            service.SetCandidate(Candidate("a", hold: 1f));
            service.HoldCompleted += id =>
            {
                if (id != "a")
                    return;

                service.ClearCandidate("a");
                service.SetCandidate(Candidate("b", hold: 1f)); // a próxima atividade aparece com E ainda pressionado
            };

            router.FeedInteract(true);
            service.Tick(1f);
            Assert.That(service.AwaitingRelease, Is.True);
            Assert.That(service.CanBeginHold("b"), Is.False, "prompt e execução concordam: E não está disponível");
            service.Tick(5f);
            Assert.That(log, Does.Not.Contain("start:b"));

            router.FeedInteract(false);
            Assert.That(service.AwaitingRelease, Is.False);
            router.FeedInteract(true);
            Assert.That(log, Does.Contain("start:b"), "só um novo pressionamento inicia a atividade seguinte");
        }

        [Test]
        public void ZeroSecondHoldCompletesOnTheNextTickAndStillRequiresRearm()
        {
            service.SetCandidate(Candidate("panel", hold: 0f));
            router.FeedInteract(true);
            Assert.That(service.HoldingId, Is.EqualTo("panel"));

            service.Tick(0f);
            Assert.That(log, Is.EqualTo(new[] { "start:panel", "progress:panel:1.00", "done:panel" }));
            Assert.That(service.AwaitingRelease, Is.True, "o E que abre o painel não inicia a medição seguinte");
        }

        [Test]
        public void ReleasingEPausesTheOperationWithoutCompletingIt()
        {
            service.SetCandidate(Candidate("a", hold: 4f));
            router.FeedInteract(true);
            service.Tick(1f);
            router.FeedInteract(false);
            service.Tick(10f);

            Assert.That(log, Is.EqualTo(new[] { "start:a", "progress:a:0.25", "cancel:a" }));
            Assert.That(service.AwaitingRelease, Is.False);
        }

        [Test]
        public void EHeldBeforeTheTargetIsAvailableDoesNotStartIt()
        {
            router.FeedInteract(true);
            service.SetCandidate(Candidate("a"));

            Assert.That(service.CanBeginHold("a"), Is.True, "o alvo está disponível, mas não houve um pressionamento novo");
            Assert.That(log, Is.Empty, "continuar segurando E ao entrar na área não inicia nada");
        }

        [Test]
        public void PauseCancelsTheHoldAndResumingWithEHeldStartsNothing()
        {
            service.SetCandidate(Candidate("a", hold: 5f));
            router.FeedInteract(true);
            service.Tick(1f);

            var paused = router.Acquire(InputContext.Paused, "pause");
            Assert.That(log, Does.Contain("cancel:a"));
            service.Tick(5f);
            Assert.That(log, Does.Not.Contain("done:a"));

            paused.Dispose();
            Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay));
            Assert.That(service.HoldingId, Is.Null, "a ferramenta não volta ligada");
            Assert.That(router.InteractHeld, Is.False);

            router.FeedInteract(false);
            router.FeedInteract(true);
            Assert.That(service.HoldingId, Is.EqualTo("a"), "retomar exige soltar e pressionar de novo");
        }

        [Test]
        public void FocusLossCancelsTheHoldOnce()
        {
            service.SetCandidate(Candidate("a", hold: 5f));
            router.FeedInteract(true);
            router.NotifyFocusLost();
            router.NotifyFocusLost();

            Assert.That(log.FindAll(x => x == "cancel:a").Count, Is.EqualTo(1));
            Assert.That(service.HoldingId, Is.Null);
        }

        [Test]
        public void CinematicBlocksPromptAndExecution()
        {
            service.SetCandidate(Candidate("a"));
            Assert.That(service.CanBeginHold("a"), Is.True);

            using (router.Acquire(InputContext.Cinematic, "cutscene"))
            {
                Assert.That(service.CanBeginHold("a"), Is.False);
                router.FeedInteract(true);
                Assert.That(service.BeginHold("a"), Is.False);
            }

            Assert.That(log, Is.Empty);
        }

        [Test]
        public void FrozenClockDoesNotAdvanceAndProgressIsReportedOnlyWhenItChanges()
        {
            service.SetCandidate(Candidate("a", hold: 2f));
            router.FeedInteract(true);

            service.Tick(0f);
            service.Tick(0f);
            service.Tick(1f);
            service.Tick(0f);
            service.Tick(0f);

            Assert.That(log, Is.EqualTo(new[] { "start:a", "progress:a:0.00", "progress:a:0.50" }));
        }

        [Test]
        public void ToggleModeStartsOnPressStopsOnSecondPressAndIgnoresRelease()
        {
            service.Mode = HoldMode.Toggle;
            service.SetCandidate(Candidate("a", hold: 5f));

            router.FeedInteract(true);
            router.FeedInteract(false);
            service.Tick(1f);
            Assert.That(service.HoldingId, Is.EqualTo("a"), "soltar não interrompe no modo alternar");

            router.FeedInteract(true);
            Assert.That(log, Does.Contain("cancel:a"));
            Assert.That(service.HoldingId, Is.Null);
        }

        [Test]
        public void ToggleModeStillCancelsOnPauseAndCompletionNeedsNoRearmWhenTheKeyIsUp()
        {
            service.Mode = HoldMode.Toggle;
            service.SetCandidate(Candidate("a", hold: 2f));
            router.FeedInteract(true);
            router.FeedInteract(false);
            service.Tick(1f);

            using (router.Acquire(InputContext.Paused, "pause"))
                Assert.That(log, Does.Contain("cancel:a"));

            service.ClearCandidate("a");
            service.SetCandidate(Candidate("b", hold: 1f));
            router.FeedInteract(true);
            router.FeedInteract(false);
            service.Tick(1f);

            Assert.That(log, Does.Contain("done:b"));
            Assert.That(service.AwaitingRelease, Is.False, "a tecla já estava solta: nada a esperar");
        }

        [Test]
        public void ChangingModeCancelsARunningHold()
        {
            service.SetCandidate(Candidate("a", hold: 5f));
            router.FeedInteract(true);
            service.Mode = HoldMode.Toggle;
            Assert.That(log, Does.Contain("cancel:a"));
        }

        [Test]
        public void UpdatingTheActiveCandidateEveryFrameDoesNotCancelItsHold()
        {
            service.SetCandidate(Candidate("a", hold: 3f, distance: 2f));
            router.FeedInteract(true);
            for (var i = 0; i < 5; i++)
                service.SetCandidate(Candidate("a", hold: 3f, distance: 2f - i * 0.1f));

            Assert.That(log, Is.EqualTo(new[] { "start:a" }));
        }

        [Test]
        public void OptionalTargetsDoNotStealTheMainAction()
        {
            service.SetCandidate(Candidate("main", priority: 10, distance: 3f));
            service.SetCandidate(Candidate("optional", priority: 1, distance: 0.2f));
            service.SetCandidate(Candidate("blocked", priority: 99, available: false));

            Assert.That(service.TryGetActive(out var active) && active.Id == "main", Is.True);
            Assert.That(service.CanBeginHold("optional"), Is.False);

            router.FeedInteract(true);
            Assert.That(service.HoldingId, Is.EqualTo("main"));
        }

        [Test]
        public void ClearingTheCandidateCancelsTheHoldAndAnInvalidOneIsRejected()
        {
            service.SetCandidate(Candidate("a", hold: 5f));
            router.FeedInteract(true);
            service.ClearCandidate("a");
            Assert.That(log, Is.EqualTo(new[] { "start:a", "cancel:a" }));

            var ex = Assert.Throws<ArgumentException>(() => service.SetCandidate(new InteractionCandidate()));
            Assert.That(ex.Message, Does.Contain("Id"));
        }

        [Test]
        public void RequireReleaseOnlyWaitsWhileTheKeyIsDown()
        {
            service.RequireRelease();
            Assert.That(service.AwaitingRelease, Is.False, "E solto: não há o que esperar");

            router.FeedInteract(true);
            service.RequireRelease();
            Assert.That(service.AwaitingRelease, Is.True);
            router.FeedInteract(false);
            Assert.That(service.AwaitingRelease, Is.False);
        }

        [Test]
        public void HoldActionTracksProgressAndCompletesOnlyOnce()
        {
            var action = new HoldAction();
            Assert.That(action.Advance(1f), Is.False, "inativo não conclui");

            action.Begin("a", 2f);
            Assert.That(action.Advance(-5f), Is.False, "tempo negativo é ignorado");
            Assert.That(action.Advance(1f), Is.False);
            Assert.That(action.Progress, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(action.Advance(1f), Is.True);
            Assert.That(action.Advance(1f), Is.False);
            Assert.That(action.IsActive, Is.False);
        }
    }
}
