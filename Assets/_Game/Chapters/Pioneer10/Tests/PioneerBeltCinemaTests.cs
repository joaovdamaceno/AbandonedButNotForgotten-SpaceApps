using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Roteiro v2, cena 2 (cinturão como cinema): o contador de dias com datas reais, o campo de pedras e a pedra solitária só nos
    /// anchors que os pedem, e o dolly zoom que mantém a sonda do mesmo tamanho enquanto a parede de pedras se abre.
    /// </summary>
    public class PioneerBeltCinemaTests
    {
        const string Months = "JAN FEV MAR ABR MAI JUN JUL AGO SET OUT NOV DEZ";
        PioneerPresentation presentation;
        PioneerChapterController controller;

        [SetUp]
        public void SetUp()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerChapterController>(true)).Single();
        }

        [TearDown]
        public void TearDown()
        {
            presentation?.UnregisterFromCameraDirector();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void TheDayCounterUsesTheRealDatesAndAcceleratesBetweenThem()
        {
            Assert.That(PioneerBeltCounter.DateText(0, Months), Is.EqualTo("15 JUL 1972"), "a travessia começa em 15/7/1972");
            Assert.That(PioneerBeltCounter.DateText(PioneerBeltCounter.StopDay, Months), Is.EqualTo("2 DEZ 1972"), "dia 140: 307 Nike");
            Assert.That(PioneerBeltCounter.DateText(PioneerBeltCounter.FinalDay, Months), Is.EqualTo("15 FEV 1973"), "dia 215: a saída");

            Assert.That(PioneerBeltCounter.DayAt(PioneerBeltCounter.DaysStartSeconds - 1f), Is.Zero);
            Assert.That(PioneerBeltCounter.DayAt(PioneerBeltCounter.DaysRampEndSeconds + 0.01f), Is.EqualTo(PioneerBeltCounter.StopDay));
            Assert.That(PioneerBeltCounter.DayAt(PioneerBeltCounter.HoldEndSeconds - 0.01f), Is.EqualTo(PioneerBeltCounter.StopDay), "a pausa em 140");
            Assert.That(PioneerBeltCounter.DayAt(PioneerBeltCounter.ExitEndSeconds + 1f), Is.EqualTo(PioneerBeltCounter.FinalDay));

            var previous = 0;
            var previousRate = 0f;
            for (var t = PioneerBeltCounter.DaysStartSeconds; t < PioneerBeltCounter.DaysRampEndSeconds - 1f; t += 1f)
            {
                var day = PioneerBeltCounter.DayAt(t + 1f);
                Assert.That(day, Is.GreaterThanOrEqualTo(previous), "o contador nunca volta");
                var rate = day - PioneerBeltCounter.DayAt(t);
                Assert.That(rate, Is.GreaterThanOrEqualTo(previousRate), "cada vez mais rápido");
                previous = day;
                previousRate = rate;
            }

            Assert.That(PioneerBeltCounter.DateText(5, "x"), Does.StartWith("20 "), "sem os 12 meses a chave aparece como número, nunca uma frase");
        }

        [Test]
        public void TheRockFieldAndTheLoneAsteroidShowOnlyWhereTheAnchorAsksForThem()
        {
            Assert.That(presentation.BeltField, Is.Not.Null);
            Assert.That(presentation.BeltNike, Is.Not.Null);
            presentation.ShowAnchor("PIO.Belt");
            Assert.That((presentation.BeltField.gameObject.activeSelf, presentation.BeltNike.gameObject.activeSelf), Is.EqualTo((false, false)));
            presentation.ShowAnchor("PIO.Belt.Field");
            Assert.That((presentation.BeltField.gameObject.activeSelf, presentation.BeltNike.gameObject.activeSelf), Is.EqualTo((true, false)), "a parede de pedras");
            presentation.ShowAnchor("PIO.Belt.Nike");
            Assert.That((presentation.BeltField.gameObject.activeSelf, presentation.BeltNike.gameObject.activeSelf), Is.EqualTo((false, true)), "o espaço vazio e uma pedra só");
            presentation.ShowAnchor("PIO.Launch");
            Assert.That((presentation.BeltField.gameObject.activeSelf, presentation.BeltNike.gameObject.activeSelf), Is.EqualTo((false, false)));

            var triangles = presentation.BeltField.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
            Assert.That(triangles, Is.EqualTo(PioneerStageBuilder.BeltRockCount * 20), "uma malha só (uma chamada de desenho), 20 faces por pedra");
        }

        [Test]
        public void TheLoneAsteroidIsWhereTheLabelPointsInTheFrame()
        {
            presentation.RegisterWithCameraDirector();
            Assert.That(presentation.TryGetAnchor("PIO.Belt.Nike", out var anchor), Is.True);
            var camera = new GameObject("test camera").AddComponent<Camera>();
            try
            {
                camera.transform.SetPositionAndRotation(anchor.pose.position, anchor.pose.rotation);
                camera.fieldOfView = anchor.fieldOfViewDegrees;
                camera.aspect = 16f / 9f;
                var viewport = camera.WorldToViewportPoint(presentation.BeltNike.position);
                var expected = PioneerStageBuilder.NikeViewport;
                Assert.That(viewport.z, Is.GreaterThan(0f));
                Assert.That(viewport.x, Is.EqualTo(expected.x).Within(0.02f));
                Assert.That(1f - viewport.y, Is.EqualTo(expected.y).Within(0.02f), "o rótulo aponta para a pedra");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void TheDollyZoomKeepsTheProbeTheSameSizeWhileTheFieldOfViewOpens()
        {
            presentation.RegisterWithCameraDirector();
            var sequence = controller.BeltCinemaSequence;
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(54f).Within(1e-4f));
            var days = sequence.Shots.Single(s => s.ShotId == "PIO02.Days");
            var reference = -1f;
            foreach (var t in new[] { 0f, 3f, 6f, 9f, 12f, 15f })
            {
                var pose = presentation.CameraDirector.Evaluate(days, t, null);
                var size = pose.Position.magnitude * Mathf.Tan(pose.FieldOfViewDegrees * 0.5f * Mathf.Deg2Rad); // metros cobertos no plano da sonda
                if (reference < 0f)
                    reference = size;
                Assert.That(size, Is.EqualTo(reference).Within(reference * 0.02f), $"dolly zoom em {t} s: a sonda não muda de tamanho");
            }

            var first = presentation.CameraDirector.Evaluate(days, 0f, null);
            var last = presentation.CameraDirector.Evaluate(days, 15f, null);
            Assert.That((first.FieldOfViewDegrees, last.FieldOfViewDegrees), Is.EqualTo((10f, 30f)), "da teleobjetiva ao plano aberto");
            Assert.That(first.Position.magnitude, Is.GreaterThan(last.Position.magnitude * 2.5f), "a câmera se aproxima enquanto o campo de visão abre");
        }

        [Test]
        public void TheCinemaSequenceHasTheFiveShotsTheSixCaptionsAndTheRealityEvent()
        {
            var sequence = controller.BeltCinemaSequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(sequence.Shots.Select(s => s.ShotId), Is.EqualTo(new[] { "PIO02.Mars", "PIO02.Fear", "PIO02.Days", "PIO02.Nike", "PIO02.Exit" }));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 8f, 11f, 15f, 10f, 10f }));
            Assert.That(sequence.Cues.Select(c => c.TextKey), Is.EqualTo(new[] { "PIO.02.01", "PIO.02.02", "PIO.02.03", "PIO.02.04", "PIO.02.05", "PIO.02.06" }));
            Assert.That(sequence.Shots.Single(s => s.ShotId == "PIO02.Nike").Events.Select(e => e.eventId), Does.Contain(BeltInvestigation.EventReality), "a música baixa na revelação");
            Assert.That(PioneerBeltCounter.NikeVisibleAt(34.2f) && PioneerBeltCounter.NikeVisibleAt(43.9f) && !PioneerBeltCounter.NikeVisibleAt(44.1f), Is.True,
                "o rótulo acompanha o plano da revelação (34–44 s)");
        }
    }
}
