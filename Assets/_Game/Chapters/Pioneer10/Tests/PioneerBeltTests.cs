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
    /// PIO-005: fantasia identificada de 15 s sem dano, contraste factual e cinco registros guiados. Teclado simulado,
    /// relógio ativo manual: rearme, E mantido, clique, ajuda após 6/12 s, trava 5/5, pausa e reinício sem duplicar.
    /// </summary>
    public class PioneerBeltTests : InputTestFixture
    {
        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        const float Frame = 1f / 30f;
        Keyboard keyboard;
        GameObject host;
        InputRouter router;
        InteractionService interaction;
        SequenceDirector director;
        PioneerPresentation presentation;
        BeltInvestigation belt;
        int completed;
        List<int> recorded;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            belt = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BeltInvestigation>(true)).Single();

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
            belt.Initialize(director, interaction, router, time, key => "«" + key + "»");

            completed = 0;
            recorded = new List<int>();
            belt.Completed += () => completed++;
            belt.Recorded += n => recorded.Add(n);
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
            belt.Tick(dt);
        }

        void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame)
                Step(Frame);
        }

        void RunUntil(System.Func<bool> condition, float limitSeconds = 120f)
        {
            for (var t = 0f; t < limitSeconds && !condition(); t += Frame)
                Step(Frame);
            Assert.That(condition(), Is.True, "condição não alcançada");
        }

        [Test]
        public void TheSequenceHasTheFourPartsTheSubtitlesAndValidRigs()
        {
            var sequence = belt.Sequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(director.Validate(sequence), Is.Empty);
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(58f).Within(1e-4f));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 12f, 15f, 13f, 18f }), "fantasia de 15 s");
            Assert.That(sequence.Cues.Select(c => (c.TextKey, c.StartSeconds, c.EndSeconds)), Is.EqualTo(new[]
            {
                ("PIO.02.01", 0f, 6f), ("PIO.02.02", 6f, 12f), ("PIO.02.03", 13f, 20f),
                ("PIO.02.04", 27f, 34f), ("PIO.02.05", 34f, 40f), ("PIO.02.06", 51f, 58f),
            }));
        }

        [Test]
        public void TheFantasyEndsOnItsOwnWithTheProbeStillAndTheSealOnEveryFrame()
        {
            belt.Begin();
            RunUntil(() => belt.Phase == BeltPhase.Fantasy);
            Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay), "setas/WASD só na fantasia");
            var start = belt.ProbePosition;
            var frames = 0;
            while (belt.Phase == BeltPhase.Fantasy && frames < 30 * 30)
            {
                Assert.That(belt.ViewState().SealVisible, Is.True, "selo IMAGINAÇÃO em todos os quadros");
                Step(Frame);
                frames++;
            }

            Assert.That(frames / 30f, Is.EqualTo(15f).Within(0.2f), "15 s, mesmo sem habilidade");
            Assert.That(belt.Phase, Is.EqualTo(BeltPhase.Reality));
            Assert.That(belt.ProbePosition, Is.EqualTo(start), "parada, a sonda conclui a fantasia");
            Assert.That(belt.ViewState().SealVisible, Is.False, "o selo some antes do espaço real");
            Assert.That(router.Context, Is.EqualTo(InputContext.Cinematic));
        }

        [Test]
        public void ArrowsMoveTheProbeAndContactsAreHarmless()
        {
            var contacts = 0;
            belt.Contact += () => contacts++;
            belt.Begin();
            RunUntil(() => belt.Phase == BeltPhase.Fantasy);
            var start = belt.ProbePosition;
            Press(keyboard.dKey);
            Run(2f);
            Release(keyboard.dKey);
            Assert.That(belt.ProbePosition.x, Is.GreaterThan(start.x + 0.3f));

            RunUntil(() => belt.Phase == BeltPhase.Reality);
            Assert.That(contacts, Is.EqualTo(belt.Contacts));
            Assert.That(belt.IsRunning, Is.True, "contato não encerra, não pune e não repete o labirinto");
        }

        [Test]
        public void FiveRecordsWithEHeldAfterReleasingTheOpeningCommand()
        {
            belt.Begin();
            RunUntil(() => belt.Phase == BeltPhase.Reality);
            Press(keyboard.eKey); // E pressionado antes da abertura do instrumento
            RunUntil(() => belt.Phase == BeltPhase.Record);
            Run(3f);
            Assert.That(belt.AcquiredSeconds, Is.Zero, "o E antigo não registra (rearme)");

            Release(keyboard.eKey);
            Press(keyboard.eKey);
            Run(BeltInvestigation.TotalRecordSeconds + 0.2f);
            Release(keyboard.eKey);

            Assert.That(recorded, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }), "um registro a cada 2 s, sem duplicar");
            RunUntil(() => completed > 0);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(router.DescribeLeases(), Is.Empty);
        }

        [Test]
        public void TheGateWaitsForFiveAndHelpComesAfterSixAndTwelveSeconds()
        {
            belt.Begin();
            RunUntil(() => belt.Phase == BeltPhase.Record);
            Run(6.2f);
            Assert.That(belt.HelpLevel, Is.EqualTo(1), "6 s: reforça o controle");
            Run(6f);
            Assert.That(belt.HelpLevel, Is.EqualTo(2), "12 s: oferece a ajuda de alinhamento");

            RunUntil(() => belt.IsGateWaiting);
            var waitingAt = director.TimeSeconds;
            Run(20f);
            Assert.That(director.TimeSeconds, Is.EqualTo(waitingAt).Within(1e-4f), "PIO.02.06 espera 5/5; nada passa por inatividade");
            Assert.That(belt.RecordedCount, Is.Zero);

            belt.RequestHelp(); // o jogador confirma a ajuda
            Run(BeltInvestigation.TotalRecordSeconds + 0.2f);
            Assert.That(belt.RecordedCount, Is.EqualTo(5));
            Assert.That(belt.IsGateWaiting, Is.False);
            RunUntil(() => completed > 0);
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void AClickRecordsOnlyTheFocusedEvent()
        {
            belt.Begin();
            RunUntil(() => belt.Phase == BeltPhase.Record);
            belt.ClickRecord();
            Run(3f);
            Assert.That(recorded, Is.EqualTo(new[] { 1 }));
            belt.ClickRecord();
            Run(2.2f);
            Assert.That(recorded, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void PausingAndRestartingAtThreeOfFiveLeavesNoDuplicateOrOldCallback()
        {
            belt.Begin();
            RunUntil(() => belt.Phase == BeltPhase.Record);
            Press(keyboard.eKey);
            Run(6.1f);
            Assert.That(belt.RecordedCount, Is.EqualTo(3));
            for (var i = 0; i < 60; i++)
                Step(0f); // pausa
            Assert.That(belt.RecordedCount, Is.EqualTo(3));

            belt.Cancel(); // reiniciar o capítulo
            Release(keyboard.eKey);
            Assert.That(interaction.TryGetActive(out _), Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);

            recorded.Clear();
            belt.Begin();
            Assert.That(belt.RecordedCount, Is.Zero);
            RunUntil(() => belt.Phase == BeltPhase.Record);
            Press(keyboard.eKey);
            Run(2.1f);
            Release(keyboard.eKey);
            Assert.That(recorded, Is.EqualTo(new[] { 1 }), "nova execução começa do zero, sem eventos da anterior");
            Assert.That(completed, Is.Zero);
        }

        [Test]
        public void SkippingCompletesOnceWithoutOpeningInputs()
        {
            belt.Begin(skippable: true);
            Run(5f);
            Assert.That(director.Skip(), Is.True);
            Run(1f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False);
        }
    }
}
