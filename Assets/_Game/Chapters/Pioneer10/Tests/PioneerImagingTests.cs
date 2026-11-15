using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// PIO-006: varredura IPP em oito faixas e dois canais. Teclado simulado e relógio ativo manual: hold antecipado,
    /// soltar e retomar, passagem perdida sem apagar faixas, ajuda, 15/30/60 FPS, 6/8 e 8/8 uma só vez, pausa e reinício.
    /// </summary>
    public class PioneerImagingTests : InputTestFixture
    {
        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        Keyboard keyboard;
        GameObject host;
        InputRouter router;
        InteractionService interaction;
        SequenceDirector director;
        PioneerPresentation presentation;
        PioneerImaging imaging;
        int completed, interruptions, recoveries, assembled, reveals, missed;
        List<(int n, ImagingChannel channel)> strips;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            imaging = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerImaging>(true)).Single();

            host = new GameObject("services");
            var time = new FakeTime();
            router = host.AddComponent<InputRouter>();
            interaction = host.AddComponent<InteractionService>();
            director = host.AddComponent<SequenceDirector>();
            router.Enable();
            interaction.Initialize(router, time);
            presentation.CameraDirector.GroundProbe = _ => null;
            director.Initialize(presentation.CameraDirector, router, time);
            presentation.RegisterWithCameraDirector();
            imaging.Initialize(director, interaction, router, time, key => "«" + key + "»");

            completed = interruptions = recoveries = assembled = reveals = missed = 0;
            strips = new List<(int, ImagingChannel)>();
            imaging.Completed += () => completed++;
            imaging.RadiationInterruption += () => interruptions++;
            imaging.Recovered += () => recoveries++;
            imaging.AllStripsCaptured += () => assembled++;
            imaging.RevealStarted += () => reveals++;
            imaging.PassMissed += () => missed++;
            imaging.StripCaptured += (n, channel) => strips.Add((n, channel));
        }

        public override void TearDown()
        {
            if (director.IsPlaying)
                director.Cancel();
            presentation.UnregisterFromCameraDirector();
            router.Disable();
            Object.DestroyImmediate(host);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            base.TearDown();
        }

        void Step(float dt)
        {
            director.Tick(dt);
            interaction.Tick(dt);
            imaging.Tick(dt);
        }

        void Run(float seconds, float frame = 1f / 30f)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += frame)
                Step(frame);
        }

        void RunUntil(System.Func<bool> condition, float limitSeconds = 120f, float frame = 1f / 30f)
        {
            for (var t = 0f; t < limitSeconds && !condition(); t += frame)
                Step(frame);
            Assert.That(condition(), Is.True, "condição não alcançada");
        }

        [Test]
        public void TheSequenceHasThreeShotsTheMilestonesAndNineSubtitles()
        {
            var sequence = imaging.Sequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(director.Validate(sequence), Is.Empty);
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(64f).Within(1e-4f));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 10f, 28f, 26f }));
            Assert.That(sequence.Cues.Select(c => (c.TextKey, c.StartSeconds, c.EndSeconds)), Is.EqualTo(new[]
            {
                ("PIO.04.01", 0f, 3.7f), ("PIO.04.02", 3.8f, 9.8f), ("PIO.04.03", 21f, 27f), ("PIO.04.04", 27f, 31.5f),
                ("PIO.04.05", 31.5f, 37f), ("PIO.04.06", 38f, 45f), ("PIO.04.07", 45f, 53f), ("PIO.04.08", 53f, 59f), ("PIO.04.09", 59f, 64f),
            }));
            Assert.That(presentation.TryGetUiFrame(PioneerImaging.StateId, out var frame), Is.True);
            Assert.That(frame.xMin, Is.GreaterThan(0.5f), "quadro de UI à direita");
        }

        [Test]
        public void TheThreeStepPhotoNeedsTwoHoldsAndTwoTapsAndNeverTheRadiationInterruption()
        {
            imaging.ThreeStepMode = true;
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Assert.That(imaging.Step, Is.EqualTo(ImagingStep.Red));
            Assert.That(imaging.ViewState().StepNumber, Is.EqualTo(1));

            // Passo 1/3: E mantido gira; quatro faixas vermelhas em 8 s.
            Press(keyboard.eKey);
            RunUntil(() => imaging.Step == ImagingStep.SwitchToBlue);
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.ChannelStripCount));
            Assert.That(strips.All(s => s.channel == ImagingChannel.Red), Is.True);
            Run(3f); // E continua mantido: o E antigo não troca o filtro sozinho
            Assert.That(imaging.Step, Is.EqualTo(ImagingStep.SwitchToBlue), "é preciso soltar e tocar");
            Release(keyboard.eKey);
            Run(1f);
            Assert.That(imaging.Step, Is.EqualTo(ImagingStep.SwitchToBlue), "esperar não troca por tempo");
            Assert.That(imaging.ViewState().StepNumber, Is.EqualTo(2));

            // Passo 2/3: um toque troca para azul; E mantido gira e entram as quatro faixas azuis.
            Press(keyboard.eKey);
            Release(keyboard.eKey);
            Step(1f / 30f);
            Assert.That(imaging.Step, Is.EqualTo(ImagingStep.Blue));
            Press(keyboard.eKey);
            RunUntil(() => imaging.Step == ImagingStep.Combine);
            Release(keyboard.eKey);
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.StripCount));
            Assert.That(strips.Skip(4).All(s => s.channel == ImagingChannel.Blue), Is.True);
            Assert.That(assembled, Is.Zero, "as oito faixas não bastam: falta juntar");
            Assert.That(imaging.ViewState().StepNumber, Is.EqualTo(3));

            // Passo 3/3: um toque junta as duas imagens.
            Press(keyboard.eKey);
            Release(keyboard.eKey);
            Step(1f / 30f);
            Assert.That(imaging.Phase, Is.EqualTo(ImagingPhase.Assembled));
            Assert.That((interruptions, recoveries, assembled), Is.EqualTo((0, 0, 1)), "sem radiação nesta cena; 8/8 dispara uma vez");
        }

        [Test]
        public void TheThreeStepPhotoSpinsByItselfAfterTwelveIdleSecondsWithoutPunishment()
        {
            imaging.ThreeStepMode = true;
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Run(12f + 8.5f); // 12 s sem E; depois a ajuda gira sozinha pelas quatro faixas (8 s)
            Assert.That(imaging.Step, Is.EqualTo(ImagingStep.SwitchToBlue));
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.ChannelStripCount));
            Assert.That(imaging.MissedPasses, Is.Zero, "não há passagem perdida nesta versão");
        }

        [Test]
        public void LearningShowsTheCursorButRecordsNothing()
        {
            imaging.Begin();
            Press(keyboard.eKey); // hold iniciado durante o aprendizado
            Run(8f); // o aprendizado dura 10 s
            Assert.That(imaging.Phase, Is.EqualTo(ImagingPhase.Learn));
            Assert.That(imaging.CapturedCount, Is.Zero);
            Assert.That(imaging.MissedPasses, Is.Zero, "a demonstração não conta passagens");
            Assert.That(imaging.ViewState().PassProgress, Is.InRange(0f, 1f));
            Release(keyboard.eKey);
        }

        [Test]
        public void HoldingEBeforeTheWindowReachesEightOfEightWithoutFineTiming()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Learn);
            Press(keyboard.eKey); // E mantido desde antes da primeira janela
            RunUntil(() => imaging.IsInterrupted);
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.InterruptionStrip));
            Assert.That(imaging.MissedPasses, Is.Zero, "com E mantido nenhuma passagem se perde");
            Assert.That(imaging.ViewState().Interrupted, Is.True);

            Assert.That(imaging.ResumeAfterRecovery(), Is.True);
            RunUntil(() => imaging.Phase == ImagingPhase.Assembled);
            Release(keyboard.eKey);

            Assert.That(strips.Select(s => s.n), Is.EqualTo(Enumerable.Range(1, 8)));
            Assert.That(strips.Take(4).All(s => s.channel == ImagingChannel.Red), Is.True, "quatro faixas vermelhas");
            Assert.That(strips.Skip(4).All(s => s.channel == ImagingChannel.Blue), Is.True, "quatro faixas azuis");
            Assert.That((interruptions, recoveries, assembled), Is.EqualTo((1, 1, 1)), "6/8 e 8/8 disparam uma vez");
        }

        [Test]
        public void ReleasingEMissesOnlyThatPassAndNeverErasesStrips()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Press(keyboard.eKey);
            RunUntil(() => imaging.CapturedCount == 2);
            Release(keyboard.eKey);

            Run(PioneerImaging.PassSeconds * 3f + 0.1f);
            Assert.That(imaging.CapturedCount, Is.EqualTo(2), "sem E nada entra");
            Assert.That(imaging.MissedPasses, Is.GreaterThanOrEqualTo(2), "a volta seguinte é oferecida, sem punição");
            Assert.That(missed, Is.EqualTo(imaging.MissedPasses));
            Assert.That(imaging.ViewState().Captured, Is.EqualTo(2), "faixas preservadas");

            Press(keyboard.eKey);
            RunUntil(() => imaging.CapturedCount == 3);
            Release(keyboard.eKey);
            Assert.That(strips.Select(s => s.n), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void ATapInsideTheWindowIsNotMissedBetweenFrames()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            RunUntil(() => imaging.ViewState().InWindow);
            Press(keyboard.eKey);
            Release(keyboard.eKey); // pressionou e soltou antes do próximo quadro
            Step(1f / 30f);
            Assert.That(imaging.CapturedCount, Is.EqualTo(1));
        }

        [Test]
        public void AssistedWindowWidensAfterTwelveSecondsWithoutAStrip()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            var narrow = imaging.ViewState();
            Assert.That(narrow.WindowEnd - narrow.WindowStart, Is.EqualTo(PioneerImaging.WindowEnd - PioneerImaging.WindowStart).Within(1e-4f));

            Run(6.1f);
            Assert.That(imaging.ViewState().HelpLevel, Is.EqualTo(1), "6 s: reforça o controle");
            Run(6f);
            var wide = imaging.ViewState();
            Assert.That(wide.HelpLevel, Is.EqualTo(2));
            Assert.That(imaging.Assisted, Is.True);
            Assert.That(wide.WindowStart, Is.Zero);
            Assert.That(wide.WindowEnd, Is.EqualTo(1f), "toda a passagem sobre o planeta");
            Assert.That(imaging.CapturedCount, Is.Zero, "a ajuda não registra sozinha");

            Press(keyboard.eKey);
            Step(1f / 30f); // um quadro com E mantido basta, em qualquer ponto da passagem
            Release(keyboard.eKey);
            Assert.That(imaging.CapturedCount, Is.EqualTo(1));
        }

        [TestCase(15)]
        [TestCase(30)]
        [TestCase(60)]
        public void TheSameWindowsAreFoundAtFifteenThirtyAndSixtyFps(int fps)
        {
            var frame = 1f / fps;
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep, 60f, frame);
            Press(keyboard.eKey);
            RunUntil(() => imaging.IsInterrupted, 60f, frame);
            Release(keyboard.eKey);
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.InterruptionStrip), $"{fps} FPS");
            Assert.That(imaging.MissedPasses, Is.Zero, $"{fps} FPS");
        }

        [Test]
        public void TheInterruptionPausesKeepsTheImageAndResumeIsIdempotent()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Press(keyboard.eKey);
            RunUntil(() => imaging.IsInterrupted);
            Release(keyboard.eKey);

            RunUntil(() => imaging.IsGateWaiting && imaging.WaitingGate == PioneerImaging.EventGateRecovery);
            var waitingAt = director.TimeSeconds;
            var captured = imaging.CapturedCount;
            Run(20f);
            Press(keyboard.eKey);
            Run(5f); // E não faz nada durante a interrupção; nada passa por inatividade
            Release(keyboard.eKey);
            Assert.That(imaging.CapturedCount, Is.EqualTo(captured), "a imagem é preservada");
            Assert.That(director.TimeSeconds, Is.EqualTo(waitingAt).Within(1e-4f), "a sequência espera a recuperação");
            Assert.That(imaging.IsInterrupted, Is.True);

            Assert.That(imaging.ResumeAfterRecovery(), Is.True);
            Assert.That(imaging.ResumeAfterRecovery(), Is.False, "retomada idempotente");
            Assert.That(imaging.ResumeAfterRecovery(), Is.False);
            Assert.That((recoveries, interruptions), Is.EqualTo((1, 1)));
            Assert.That(imaging.CapturedCount, Is.EqualTo(captured));
            Assert.That(imaging.Phase, Is.EqualTo(ImagingPhase.Sweep));
        }

        [Test]
        public void ResumingWithoutAnInterruptionDoesNothing()
        {
            Assert.That(imaging.ResumeAfterRecovery(), Is.False, "sem execução");
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Assert.That(imaging.ResumeAfterRecovery(), Is.False, "antes da faixa 6");
            Assert.That(recoveries, Is.Zero);
        }

        [Test]
        public void PausingFreezesTheCursorAndHeldEIsRearmedAfterwards()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Press(keyboard.eKey);
            RunUntil(() => imaging.CapturedCount == 2);
            var progress = imaging.PassProgress;
            for (var i = 0; i < 90; i++)
                Step(0f); // pausa: tempo ativo zero
            Assert.That(imaging.PassProgress, Is.EqualTo(progress), "o cursor congela");
            Assert.That(imaging.CapturedCount, Is.EqualTo(2));
            Release(keyboard.eKey);
        }

        [Test]
        public void TheChapterGatesWaitForTheFourthStripBeforeTheChannelCue()
        {
            imaging.Begin();
            RunUntil(() => imaging.IsGateWaiting && imaging.WaitingGate == PioneerImaging.EventGateChannels);
            Assert.That(director.TimeSeconds, Is.EqualTo(21f).Within(0.1f), "sem E, a sequência espera a faixa 4 em 21 s");
            Press(keyboard.eKey);
            RunUntil(() => imaging.CapturedCount >= 4);
            RunUntil(() => !imaging.IsGateWaiting || imaging.WaitingGate != PioneerImaging.EventGateChannels);
            Release(keyboard.eKey);
        }

        [Test]
        public void FullRunCompletesOnceWithNoLeaksAndRestartsClean()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Press(keyboard.eKey);
            RunUntil(() => imaging.IsInterrupted);
            imaging.ResumeAfterRecovery();
            RunUntil(() => completed > 0, 200f);
            Release(keyboard.eKey);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That((interruptions, recoveries, assembled, reveals), Is.EqualTo((1, 1, 1, 1)));
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False);

            strips.Clear();
            imaging.Begin();
            Assert.That(imaging.CapturedCount, Is.Zero, "nova execução começa do zero");
            Assert.That(imaging.IsInterrupted, Is.False);
            imaging.Cancel();
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(strips, Is.Empty);
        }

        [Test]
        public void SkippingCompletesOnceWithoutOpeningInputs()
        {
            imaging.Begin(skippable: true);
            Run(5f);
            Assert.That(director.Skip(), Is.True);
            Run(1f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False);
        }

        [Test]
        public void CancelingDuringTheInterruptionReleasesEverything()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Press(keyboard.eKey);
            RunUntil(() => imaging.IsInterrupted);
            Release(keyboard.eKey);
            imaging.Cancel();
            Assert.That(imaging.IsRunning, Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False);
            Assert.That(imaging.ResumeAfterRecovery(), Is.False, "cancelado: nada a retomar");
            Assert.That(completed, Is.Zero);
        }

        [Test]
        public void ValidateSetupIsEmptyOnTheBuiltSceneAndComplainsAboutMissingReferences()
        {
            Assert.That(imaging.ValidateSetup(), Is.Empty);

            var bare = new GameObject("bare").AddComponent<PioneerImaging>();
            var errors = bare.ValidateSetup();
            Object.DestroyImmediate(bare.gameObject);
            Assert.That(errors.Any(e => e.Contains("sequence")), Is.True);
            Assert.That(errors.Any(e => e.Contains("presentation")), Is.True);
            Assert.That(errors.Any(e => e.Contains("panel")), Is.True);
        }

        [Test]
        public void ThePanelPrefabHasTheResponsiveClassAndEveryElementTheViewUses()
        {
            var panel = Object.FindAnyObjectByType<ImagingPanelView>(FindObjectsInactive.Include);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.GetComponent<Abandoned.SpaceApps.UI.ResponsiveLayoutClass>(), Is.Not.Null, "UI_SPEC: todo UIDocument de tela cheia");
            Assert.That(panel.ValidateSetup(), Is.Empty);
        }

        [Test]
        public void ThePromptStateFollowsAvailableRearmHoldingPausedAndDone()
        {
            Press(keyboard.eKey); // E já pressionado ao abrir a atividade
            imaging.Begin();
            Step(1f / 30f);
            Assert.That(imaging.ViewState().Hold, Is.EqualTo(ImagingHoldState.Rearm), "o E que abriu não conta");
            Release(keyboard.eKey);
            Step(1f / 30f);
            Assert.That(imaging.ViewState().Hold, Is.EqualTo(ImagingHoldState.Available));

            Press(keyboard.eKey);
            Step(1f / 30f);
            Assert.That(imaging.ViewState().Hold, Is.EqualTo(ImagingHoldState.Holding));
            RunUntil(() => imaging.CapturedCount >= 1);
            Release(keyboard.eKey);
            Step(1f / 30f);
            Assert.That(imaging.ViewState().Hold, Is.EqualTo(ImagingHoldState.Paused), "soltou com faixas registradas");

            Press(keyboard.eKey);
            RunUntil(() => imaging.IsInterrupted);
            Assert.That(imaging.ViewState().Hold, Is.EqualTo(ImagingHoldState.Paused), "interrompida: o progresso está preservado");
            imaging.ResumeAfterRecovery();
            RunUntil(() => imaging.Phase == ImagingPhase.Assembled);
            Release(keyboard.eKey);
            Assert.That(imaging.ViewState().Hold, Is.EqualTo(ImagingHoldState.Done));
        }

        [Test]
        public void ReleasingBeforeAnyStripIsNotPausedBecauseNothingIsPreserved()
        {
            imaging.Begin();
            Step(1f / 30f);
            Press(keyboard.eKey);
            Step(1f / 30f);
            Release(keyboard.eKey);
            Step(1f / 30f);
            Assert.That(imaging.CapturedCount, Is.Zero);
            Assert.That(imaging.ViewState().Hold, Is.EqualTo(ImagingHoldState.Available));
        }
    }
}
