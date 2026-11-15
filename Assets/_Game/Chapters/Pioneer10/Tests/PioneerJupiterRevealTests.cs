using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Roteiro v2, cena 3 (o rei dos planetas): Júpiter cresce de um ponto até o tamanho do palco com a sonda em silhueta no primeiro
    /// plano, e uma Terra 11,2 vezes menor entra ao lado para a comparação de tamanho.
    /// </summary>
    public class PioneerJupiterRevealTests
    {
        PioneerPresentation presentation;
        PioneerChapterController controller;
        PioneerJupiterReveal reveal;

        [SetUp]
        public void SetUp()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerChapterController>(true)).Single();
            reveal = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerJupiterReveal>(true)).Single();
        }

        [TearDown]
        public void TearDown()
        {
            presentation?.UnregisterFromCameraDirector();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void JupiterGrowsFromAPointAtAConstantPerceivedRateThenStaysFull()
        {
            Assert.That(PioneerJupiterReveal.JupiterScaleAt(0f), Is.EqualTo(PioneerJupiterReveal.MinScale).Within(1e-5f), "começa num ponto");
            Assert.That(PioneerJupiterReveal.JupiterScaleAt(PioneerJupiterReveal.GrowSeconds), Is.EqualTo(1f));
            Assert.That(PioneerJupiterReveal.JupiterScaleAt(PioneerJupiterReveal.GrowSeconds + 5f), Is.EqualTo(1f), "depois do crescimento fica no tamanho do palco");
            var previous = 0f;
            for (var t = 0f; t <= PioneerJupiterReveal.GrowSeconds; t += 0.5f)
            {
                var scale = PioneerJupiterReveal.JupiterScaleAt(t);
                Assert.That(scale, Is.GreaterThan(previous), $"cresce o tempo todo ({t} s)");
                previous = scale;
            }

            var ratioEarly = PioneerJupiterReveal.JupiterScaleAt(2f) / PioneerJupiterReveal.JupiterScaleAt(1f);
            var ratioLate = PioneerJupiterReveal.JupiterScaleAt(8f) / PioneerJupiterReveal.JupiterScaleAt(7f);
            Assert.That(ratioLate, Is.EqualTo(ratioEarly).Within(ratioEarly * 0.01f), "mesma razão por segundo: o olho vê uma taxa constante");
        }

        [Test]
        public void TheDriverScalesJupiterAndPutsTheEarthInOnlyDuringTheOpenShotThenRestoresEverything()
        {
            var jupiter = presentation.Jupiter;
            var full = jupiter.localScale.x;
            reveal.Initialize(null);
            reveal.Apply(1f);
            Assert.That(jupiter.localScale.x, Is.LessThan(full * 0.05f));
            Assert.That(presentation.Earth.gameObject.activeSelf, Is.False, "a Terra não aparece enquanto Júpiter cresce");
            reveal.Apply(PioneerJupiterReveal.EarthEnterSeconds + 0.1f);
            Assert.That(jupiter.localScale.x, Is.EqualTo(full).Within(1e-4f));
            Assert.That(presentation.Earth.gameObject.activeSelf, Is.True);
            var entering = presentation.Earth.localPosition;
            reveal.Apply(PioneerJupiterReveal.EarthArriveSeconds + 0.5f);
            var resting = presentation.Earth.localPosition;
            Assert.That(Vector3.Distance(resting, PioneerStageBuilder.EarthCompareTo), Is.LessThan(0.01f), "a Terra para ao lado de Júpiter");
            Assert.That(Vector3.Distance(entering, resting), Is.GreaterThan(5f), "a Terra anda de verdade até parar");
            reveal.Apply(PioneerJupiterReveal.EarthLeaveSeconds + 0.5f);
            Assert.That(presentation.Earth.gameObject.activeSelf, Is.False, "no corte seguinte a Terra sai");

            reveal.Apply(3f);
            reveal.Hide();
            Assert.That(jupiter.localScale.x, Is.EqualTo(full).Within(1e-6f), "Hide devolve o tamanho de Júpiter a quem usa o planeta depois");
            Assert.That(presentation.Earth.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void TheEarthIsElevenPointTwoTimesSmallerThanJupiterAndSitsInTheFrameWithoutTouchingIt()
        {
            Assert.That(presentation.Jupiter.localScale.x / presentation.Earth.localScale.x, Is.EqualTo(11.2f).Within(0.01f), "a razão real dos diâmetros");

            presentation.RegisterWithCameraDirector();
            Assert.That(presentation.TryGetAnchor("PIO.JupiterApproach", out var anchor), Is.True);
            Assert.That(anchor.pose.position.magnitude, Is.LessThan(15f), "a sonda segue em silhueta no primeiro plano (câmera perto dela)");
            var camera = new GameObject("test camera").AddComponent<Camera>();
            try
            {
                camera.transform.SetPositionAndRotation(anchor.pose.position, anchor.pose.rotation);
                camera.fieldOfView = PioneerStageBuilder.ApproachOpenFov;
                camera.aspect = 16f / 9f;
                var earthPosition = PioneerStageBuilder.EarthCompareTo;
                var earth = camera.WorldToViewportPoint(earthPosition);
                var expected = PioneerStageBuilder.EarthCompareViewport;
                Assert.That(earth.z, Is.GreaterThan(0f));
                Assert.That(earth.x, Is.EqualTo(expected.x).Within(0.02f));
                Assert.That(1f - earth.y, Is.EqualTo(expected.y).Within(0.02f), "a Terra cai onde o roteiro a põe, ao lado de Júpiter");

                var toJupiter = presentation.Jupiter.position - camera.transform.position;
                var toEarth = earthPosition - camera.transform.position;
                var jupiterRadius = Mathf.Asin(PioneerStageBuilder.JupiterRadiusMeters / toJupiter.magnitude) * Mathf.Rad2Deg;
                var earthRadius = Mathf.Asin(PioneerStageBuilder.EarthCompareRadiusMeters / toEarth.magnitude) * Mathf.Rad2Deg;
                Assert.That(Vector3.Angle(toJupiter, toEarth), Is.GreaterThan(jupiterRadius + earthRadius + 1f), "a Terra não encosta em Júpiter");
                Assert.That(2f * jupiterRadius / PioneerStageBuilder.ApproachOpenFov, Is.GreaterThanOrEqualTo(0.7f), "Júpiter ocupa 70% ou mais da altura do quadro");
                Assert.That(earthRadius, Is.InRange(0.5f, 3f), "pequenina, mas visível");

                var offscreen = camera.WorldToViewportPoint(PioneerStageBuilder.EarthCompareFrom);
                Assert.That(offscreen.x, Is.LessThan(0f), "a Terra entra por fora do quadro");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void TheGrowShotStartsWideAndEndsOnTheOpenFieldOfViewAndTheControllerHoldsTheDriver()
        {
            var sequence = controller.JupiterSequence;
            var grow = sequence.Shots.Single(s => s.ShotId == "PIO03.Grow");
            Assert.That((grow.Keyframes.First().fieldOfViewDegrees, grow.Keyframes.Last().fieldOfViewDegrees), Is.EqualTo((70f, PioneerStageBuilder.ApproachOpenFov)));
            Assert.That(grow.DurationSeconds, Is.EqualTo(PioneerJupiterReveal.GrowSeconds).Within(1e-4f), "o crescimento dura o plano inteiro");
            Assert.That(controller.ValidateSetup(), Is.Empty);
        }
    }
}
