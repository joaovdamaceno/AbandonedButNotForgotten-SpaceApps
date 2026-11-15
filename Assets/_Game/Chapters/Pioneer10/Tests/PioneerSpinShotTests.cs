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
    /// Roteiro v2, cena 4 (versão final): o jogo da foto em ponto de vista. A câmera gira com a sonda, Júpiter cruza a mira uma vez por volta,
    /// um toque de E na janela registra uma foto (quadro vermelho e azul juntos), cinco fotos se juntam sozinhas e o ruído cai.
    /// </summary>
    public class PioneerSpinShotTests : InputTestFixture
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
        int assembled, missed;
        List<(int n, ImagingChannel channel)> frames;
        List<int> shotsSeen;

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
            imaging.SpinShotMode = true;

            assembled = missed = 0;
            frames = new List<(int, ImagingChannel)>();
            shotsSeen = new List<int>();
            imaging.AllStripsCaptured += () => assembled++;
            imaging.PassMissed += () => missed++;
            imaging.StripCaptured += (n, channel) => frames.Add((n, channel));
            imaging.ShotCaptured += n => shotsSeen.Add(n);
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

        void RunUntil(System.Func<bool> condition, float limitSeconds = 120f, float frame = 1f / 30f)
        {
            for (var t = 0f; t < limitSeconds && !condition(); t += frame)
                Step(frame);
            Assert.That(condition(), Is.True, "condição não alcançada");
        }

        void Tap()
        {
            Press(keyboard.eKey);
            Release(keyboard.eKey);
            Step(1f / 30f);
        }

        /// <summary>Espera Júpiter entrar na mira e toca E.</summary>
        void ShootNext()
        {
            var before = imaging.ShotsCaptured;
            RunUntil(() => imaging.InSpinWindow && !imaging.SpinState().PassCaptured);
            Tap();
            Assert.That(imaging.ShotsCaptured, Is.EqualTo(before + 1), "a foto foi registrada com Júpiter na mira");
        }

        void StartSweeping()
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
        }

        [Test]
        public void TheDemonstrationRegistersNothingAndTheSweepStartsAfterIt()
        {
            imaging.Begin();
            Assert.That(imaging.SpinState().Phase, Is.EqualTo(SpinShotPhase.Learn));
            RunUntil(() => imaging.InSpinWindow);
            Tap();
            Assert.That(imaging.ShotsCaptured, Is.Zero, "na demonstração o toque não registra nada");
            RunUntil(() => imaging.Phase == ImagingPhase.Sweep);
            Assert.That(imaging.SpinState().Phase, Is.EqualTo(SpinShotPhase.Shoot));
        }

        [Test]
        public void ATapInsideTheWindowTakesOnePhotoWithARedAndABlueFrameInterleaved()
        {
            StartSweeping();
            ShootNext();
            Assert.That(frames, Is.EqualTo(new[] { (1, ImagingChannel.Red), (2, ImagingChannel.Blue) }), "os dois canais saem da mesma passagem, vermelho e depois azul");
            Assert.That(shotsSeen, Is.EqualTo(new[] { 1 }));
            Tap();
            Assert.That(imaging.ShotsCaptured, Is.EqualTo(1), "uma foto por passagem");
        }

        [Test]
        public void ATapOutsideTheWindowOnlyGivesFeedbackAndNothingIsLost()
        {
            StartSweeping();
            RunUntil(() => !imaging.InSpinWindow && Mathf.Abs(imaging.SpinErrorDegrees) > 60f);
            Tap();
            Assert.That(imaging.ShotsCaptured, Is.Zero);
            Assert.That(imaging.SpinState().PressedOutside, Is.True);
            ShootNext();
            Assert.That(imaging.ShotsCaptured, Is.EqualTo(1), "sem punição: a próxima volta vale");
        }

        [Test]
        public void MissingAWindowCountsAMissedPassAndTheHelpWidensTheWindowAfterTwelveSeconds()
        {
            StartSweeping();
            var narrow = imaging.WindowHalfDegreesNow;
            Assert.That(narrow, Is.EqualTo(PioneerImaging.SpinWindowHalfDegrees));
            RunUntil(() => missed > 0);
            RunUntil(() => imaging.Assisted, 30f);
            Assert.That(imaging.WindowHalfDegreesNow, Is.EqualTo(PioneerImaging.SpinWindowHalfDegrees * PioneerImaging.SpinAssistedScale).Within(1e-3f));
            Assert.That(imaging.ShotsCaptured, Is.Zero, "perder voltas nunca apaga nada");
        }

        [Test]
        public void FivePhotosJoinThemselvesWithoutAnyKeyAndFinishOnce()
        {
            StartSweeping();
            for (var i = 0; i < PioneerImaging.SpinShotCount; i++)
                ShootNext();

            Assert.That(imaging.IsCombining, Is.True, "depois da quinta foto a junção começa sozinha");
            Assert.That(imaging.SpinState().Phase, Is.EqualTo(SpinShotPhase.Combine));
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.SpinShotCount * 2), "cinco fotos, dez quadros");
            Assert.That(frames.Select(f => f.channel), Is.EqualTo(Enumerable.Range(0, 10).Select(i => i % 2 == 0 ? ImagingChannel.Red : ImagingChannel.Blue)), "vermelho e azul entrelaçados");
            Assert.That(assembled, Is.Zero);

            // Nenhuma tecla: só o tempo junta as fotos.
            RunUntil(() => imaging.Phase == ImagingPhase.Assembled, PioneerImaging.CombineSeconds + 2f);
            Assert.That(assembled, Is.EqualTo(1));
            Step(1f);
            Assert.That(assembled, Is.EqualTo(1), "uma vez só");
            Assert.That(imaging.SpinState().Phase, Is.EqualTo(SpinShotPhase.Done));
        }

        [Test]
        public void TheCameraStaysStillWithJupiterCenteredWhileOnlyTheBarMoves()
        {
            StartSweeping();
            var camera = presentation.CameraDirector.TargetCamera;
            Assert.That(camera, Is.Not.Null);
            camera.aspect = 16f / 9f;
            Assert.That(presentation.ProbeSpin.gameObject.activeInHierarchy, Is.False, "a câmera está dentro da sonda: o modelo some");

            // Teste de jogo: a sonda não gira mais antes da foto. Júpiter fica no meio do quadro o tempo todo e só a barra (o erro da volta) anda.
            var sawBarAtBothSides = new[] { false, false };
            for (var i = 0; i < 6; i++)
            {
                RunUntil(() => Mathf.Abs(imaging.SpinErrorDegrees - (i % 2 == 0 ? -120f : 120f)) < 6f);
                Step(1f / 60f);
                var viewport = camera.WorldToViewportPoint(PioneerStageBuilder.JupiterPosition);
                Assert.That(viewport.z, Is.GreaterThan(0f));
                Assert.That(viewport.x, Is.EqualTo(0.5f).Within(0.12f), "Júpiter fica centrado mesmo com a barra longe da zona");
                Assert.That(viewport.y, Is.EqualTo(0.5f).Within(0.12f));
                sawBarAtBothSides[imaging.SpinErrorDegrees < 0f ? 0 : 1] = true;
                Assert.That(imaging.PovYawDegrees, Is.EqualTo(0f), "a câmera não gira");
            }

            Assert.That(sawBarAtBothSides, Is.EqualTo(new[] { true, true }), "a barra passou pelos dois lados");
        }

        [Test]
        public void TheWindowIsAWideZoneOnTheBarWithEnoughTimeToReact()
        {
            Assert.That(SpinShotPanelView.PovFov, Is.EqualTo(PioneerStageBuilder.ImagingPovFov), "a mira e a câmera usam o mesmo campo de visão");
            Assert.That(PioneerImaging.SpinWindowHalfDegrees * 2f / 360f * PioneerImaging.SpinPeriodSeconds, Is.GreaterThan(0.6f), "tempo de reação de pelo menos 0,6 s");
        }

        [Test]
        public void TheSequenceGatesStillHoldUntilTheWorkIsDone()
        {
            StartSweeping();
            RunUntil(() => imaging.IsGateWaiting, 60f);
            Assert.That(imaging.WaitingGate, Is.EqualTo(PioneerImaging.EventGateChannels), "a legenda do ruído espera duas fotos");
            ShootNext();
            ShootNext();
            RunUntil(() => !imaging.IsGateWaiting, 5f);
            Assert.That(director.IsPlaying, Is.True);
        }

        [Test]
        public void ValidateSetupIsEmptyOnTheBuiltScene()
        {
            Assert.That(imaging.ValidateSetup(), Is.Empty);
        }
    }

    public class ImagingFrameSynthTests
    {
        [Test]
        public void ThePlanetIsDeterministicDifferentPerChannelAndEveryFrameIsDifferent()
        {
            CollectionAssert.AreEqual(ImagingFrameSynth.Truth(ImagingChannel.Red), ImagingFrameSynth.Truth(ImagingChannel.Red));
            CollectionAssert.AreNotEqual(ImagingFrameSynth.Truth(ImagingChannel.Red), ImagingFrameSynth.Truth(ImagingChannel.Blue), "o vermelho e o azul veem o planeta de modos diferentes");
            CollectionAssert.AreNotEqual(ImagingFrameSynth.Frame(ImagingChannel.Red, 0), ImagingFrameSynth.Frame(ImagingChannel.Red, 1), "cada foto tem o seu ruído");
            CollectionAssert.AreEqual(ImagingFrameSynth.Frame(ImagingChannel.Blue, 2), ImagingFrameSynth.Frame(ImagingChannel.Blue, 2), "e é sempre o mesmo");
        }

        [Test]
        public void AddingPhotosShrinksTheNoiseByTheSquareRootOfTheCount()
        {
            foreach (var channel in new[] { ImagingChannel.Red, ImagingChannel.Blue })
            {
                var one = ImagingFrameSynth.NoiseOf(ImagingFrameSynth.Average(channel, 1), channel);
                var five = ImagingFrameSynth.NoiseOf(ImagingFrameSynth.Average(channel, 5), channel);
                Assert.That(one, Is.EqualTo(ImagingFrameSynth.NoiseSigma).Within(0.03f), "uma foto sozinha tem o ruído cheio");
                Assert.That(five, Is.EqualTo(ImagingFrameSynth.NoiseSigma / Mathf.Sqrt(5f)).Within(0.03f), "cinco fotos: ruído dividido por √5");
                Assert.That(five, Is.LessThan(one * 0.6f));
            }
        }

        [Test]
        public void TheTexturesAreTintedByChannelAndTheCompositeLeavesOutGreen()
        {
            var red = ImagingFrameSynth.ChannelTexture(ImagingFrameSynth.Average(ImagingChannel.Red, 5), ImagingChannel.Red, "t");
            var blue = ImagingFrameSynth.ChannelTexture(ImagingFrameSynth.Average(ImagingChannel.Blue, 5), ImagingChannel.Blue, "t");
            var both = ImagingFrameSynth.CompositeTexture(ImagingFrameSynth.Average(ImagingChannel.Red, 5), ImagingFrameSynth.Average(ImagingChannel.Blue, 5), "t");
            try
            {
                var center = ImagingFrameSynth.Size / 2;
                var r = red.GetPixel(center, center);
                var b = blue.GetPixel(center, center);
                var c = both.GetPixel(center, center);
                Assert.That(r.r, Is.GreaterThan(r.b), "vermelho");
                Assert.That(b.b, Is.GreaterThan(b.r), "azul");
                Assert.That(c.g, Is.Zero, "o verde não foi medido");
                Assert.That(c.r + c.b, Is.GreaterThan(0.2f), "a imagem somada tem as duas cores");
            }
            finally
            {
                Object.DestroyImmediate(red);
                Object.DestroyImmediate(blue);
                Object.DestroyImmediate(both);
            }
        }

        [Test]
        public void TheCombineStepsAddOnePhotoAtATimeAndFinishBeforeTheEnd()
        {
            Assert.That(SpinShotPanelView.CombineStep(0f), Is.EqualTo(1));
            Assert.That(SpinShotPanelView.CombineStep(0.76f), Is.EqualTo(PioneerImaging.SpinShotCount));
            Assert.That(SpinShotPanelView.CombineStep(1f), Is.EqualTo(PioneerImaging.SpinShotCount));
            var previous = 0;
            for (var p = 0f; p <= 1f; p += 0.05f)
            {
                var step = SpinShotPanelView.CombineStep(p);
                Assert.That(step, Is.GreaterThanOrEqualTo(previous));
                previous = step;
            }
        }
    }
}
