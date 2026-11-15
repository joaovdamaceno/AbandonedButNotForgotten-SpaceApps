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
    /// PIO-004: lançamento de 40 s com o primeiro hold de 3 s. Teclado simulado (Input System), relógio ativo manual:
    /// rearme do E, progresso preservado ao soltar, pausa, cancelamento, pular e conclusão única.
    /// </summary>
    public class PioneerLaunchTests : InputTestFixture
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
        PioneerLaunchActivity activity;
        int completed;
        List<SequenceResult> results;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            activity = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerLaunchActivity>(true)).Single();

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
            activity.Initialize(director, interaction, router, time);

            completed = 0;
            results = new List<SequenceResult>();
            activity.Completed += () => completed++;
            director.Finished += r => results.Add(r);
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
            activity.Tick(dt);
        }

        void Run(float seconds, float dt = Frame)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += dt)
                Step(dt);
        }

        void RunUntilHold()
        {
            for (var i = 0; i < 3000 && !activity.IsAwaitingHold; i++)
                Step(Frame);
            Assert.That(activity.IsAwaitingHold, Is.True, "a sequência chega ao cartão do RTG");
        }

        [Test]
        public void TheSequenceFollowsTheFourPlansSubtitlesAndTheStageAnchors()
        {
            var sequence = activity.Sequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(director.Validate(sequence), Is.Empty, "rigs, alvo e atores registrados na cena real");
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(40f).Within(1e-4f));
            Assert.That(sequence.Shots.Select(s => s.ShotId), Is.EqualTo(new[] { "PIO01.A", "PIO01.B", "PIO01.C", "PIO01.D" }));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 7f, 10f, 9f, 14f }));
            Assert.That(sequence.Shots.All(s => presentation.TryGetAnchor(s.RigId, out _)), "todo plano parte de um anchor do palco");
            Assert.That(sequence.Cues.Select(c => c.TextKey), Is.EqualTo(new[] { "PIO.01.01", "PIO.01.02", "PIO.01.03", "PIO.01.04", "PIO.01.05" }));
            Assert.That(sequence.Cues.Select(c => (c.StartSeconds, c.EndSeconds)),
                Is.EqualTo(new[] { (0f, 7f), (8f, 16f), (18f, 25f), (26f, 32.8f), (33f, 40f) }), "janelas do roteiro");

            var atlas = sequence.ActorMarks.Where(m => m.actorId == PioneerLaunchActivity.AtlasActorId).ToList();
            Assert.That(atlas.Where(m => m.timeSeconds <= PioneerLaunchBuilder.IgnitionSeconds).All(m => m.positionMeters == Vector3.zero),
                "o Atlas não sai antes da ignição");
        }

        [Test]
        public void TheTrajectoryCrossesTheLunarOrbitFarFromTheMoon()
        {
            // Cruzamento do raio da órbita (34 m no gráfico) e ângulo em relação à Lua (125°): sem "encontro próximo".
            var earth = new Vector3(-26f, -6f, 0f);
            var crossing = Enumerable.Range(0, 2001).Select(i => PioneerLaunchBuilder.TrajectoryPoint(i / 2000f))
                .First(p => (p - earth).magnitude >= 34f);
            var angle = Mathf.Atan2(crossing.y - earth.y, crossing.x - earth.x) * Mathf.Rad2Deg;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(angle, 125f)), Is.GreaterThan(60f));
        }

        [Test]
        public void AnEHeldBeforeTheCardDoesNotCountAndTheSequenceWaitsForTheHold()
        {
            Press(keyboard.eKey); // segurado desde antes de tudo
            activity.Begin();
            RunUntilHold();
            var waitingAt = director.TimeSeconds;

            Run(5f);
            Assert.That(activity.HoldProgress, Is.Zero, "o E antigo não inicia o hold (rearme)");
            Assert.That(director.TimeSeconds, Is.EqualTo(waitingAt).Within(1e-4f), "o relógio da sequência espera o hold");
            Assert.That(completed, Is.Zero);

            Release(keyboard.eKey);
            Press(keyboard.eKey);
            Run(PioneerLaunchActivity.HoldSeconds + 0.1f);
            Release(keyboard.eKey);
            Assert.That(activity.HoldDone, Is.True);
            Assert.That(activity.IsAwaitingHold, Is.False);
            Assert.That(router.Context, Is.EqualTo(InputContext.Cinematic), "o contexto do hold foi devolvido à sequência");

            Run(40f);
            Assert.That(completed, Is.EqualTo(1), "Belt começa uma única vez");
            Assert.That(results.Select(r => r.Reason), Is.EqualTo(new[] { SequenceExitReason.Completed }));
            Assert.That(router.DescribeLeases(), Is.Empty, "nenhum contexto de entrada esquecido");
        }

        [Test]
        public void ReleasingEPausesTheProgressWithoutLosingIt()
        {
            activity.Begin();
            RunUntilHold();
            Press(keyboard.eKey);
            Run(1.5f);
            Release(keyboard.eKey);
            var kept = activity.HoldProgress;
            Assert.That(kept, Is.EqualTo(0.5f).Within(0.05f));

            Run(4f);
            Assert.That(activity.HoldProgress, Is.EqualTo(kept).Within(1e-4f), "soltar pausa, não apaga");

            Press(keyboard.eKey);
            Run(1.5f + 0.1f);
            Assert.That(activity.HoldDone, Is.True, "o segundo hold cobre só o restante (3 s no total)");
            Release(keyboard.eKey);
        }

        [Test]
        public void ShellPauseFreezesTheHoldAndResumingNeedsAFreshPress()
        {
            activity.Begin();
            RunUntilHold();
            Press(keyboard.eKey);
            Run(1f);
            var before = activity.HoldProgress;

            for (var i = 0; i < 60; i++)
                Step(0f); // pausa: tempo ativo zero
            Assert.That(activity.HoldProgress, Is.EqualTo(before).Within(1e-4f));

            using (router.Acquire(InputContext.Paused, "shell"))
                Run(1f);
            Assert.That(activity.HoldProgress, Is.EqualTo(before).Within(0.02f), "pausa do shell não avança nem apaga");
            Run(1f);
            Assert.That(activity.HoldProgress, Is.EqualTo(before).Within(0.02f), "E mantido durante a pausa não retoma sozinho");

            Release(keyboard.eKey);
            Press(keyboard.eKey);
            Run(PioneerLaunchActivity.HoldSeconds);
            Assert.That(activity.HoldDone, Is.True);
            Release(keyboard.eKey);
        }

        [Test]
        public void CancelingDuringTheHoldStopsParticlesHoldsAndInputWithoutCompleting()
        {
            activity.Begin();
            RunUntilHold();
            Assert.That(activity.Ignited, Is.True);
            Press(keyboard.eKey);
            Run(1f);

            activity.Cancel();

            Assert.That(activity.IsRunning, Is.False);
            Assert.That(activity.IsAwaitingHold, Is.False);
            Assert.That(interaction.TryGetActive(out _), Is.False, "candidato do RTG removido");
            Assert.That(interaction.HoldingId, Is.Null);
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(activity.Smoke.particleCount, Is.Zero);
            Assert.That(activity.Smoke.emission.enabled, Is.False);
            Assert.That(activity.Flame.activeSelf, Is.False);
            Assert.That(completed, Is.Zero, "cancelar não conclui o estado");
            Release(keyboard.eKey);
        }

        [Test]
        public void SkippingCompletesOnceWithoutOpeningTheHold()
        {
            activity.Begin(skippable: true);
            Run(10f);
            Assert.That(director.Skip(), Is.True);
            Run(2f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(activity.IsAwaitingHold, Is.False, "pular dispara os eventos restantes sem abrir o cartão");
            Assert.That(interaction.TryGetActive(out _), Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);

            activity.Begin(skippable: true); // repetir o capítulo: nova execução, nova conclusão única
            director.Skip();
            Assert.That(completed, Is.EqualTo(2));
        }

        [Test]
        public void SmokeStaysWithinEightyParticlesAndTheCameraNeverShakes()
        {
            activity.Begin();
            var camera = presentation.CameraDirector.TargetCamera.transform;
            var peak = 0;
            var previous = camera.rotation;
            var previousShot = director.CurrentShotId;
            var worst = 0f;
            for (var i = 0; i < 3000 && !activity.IsAwaitingHold; i++)
            {
                Step(Frame);
                peak = Mathf.Max(peak, activity.Smoke.particleCount);
                if (director.CurrentShotId == previousShot)
                    worst = Mathf.Max(worst, Quaternion.Angle(previous, camera.rotation));
                previous = camera.rotation;
                previousShot = director.CurrentShotId;
            }

            Assert.That(peak, Is.GreaterThan(0).And.LessThanOrEqualTo(80));
            Assert.That(worst, Is.LessThan(1.5f), "sem tremor: dentro de um plano a câmera gira no máximo 1,5° por quadro (30 FPS)");
        }
    }
}
