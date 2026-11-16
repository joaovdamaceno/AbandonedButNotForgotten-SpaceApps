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
        public void TheBeltIsASparseFieldOfSmallRocksFarApartAndFarFromTheCameraPath()
        {
            var centers = PioneerStageBuilder.BeltRockCenters();
            Assert.That(centers, Has.Count.EqualTo(PioneerStageBuilder.BeltRockCount));
            Assert.That(centers.Select(c => c.magnitude).Min(), Is.GreaterThanOrEqualTo(PioneerStageBuilder.BeltClearRadiusMeters - 0.01f), "nenhuma pedra perto da sonda");
            Assert.That(centers.Select(c => c.magnitude).Max(), Is.LessThanOrEqualTo(PioneerStageBuilder.BeltOuterRadiusMeters + 0.01f));
            var closestPair = float.MaxValue;
            for (var i = 0; i < centers.Count; i++)
            {
                for (var j = i + 1; j < centers.Count; j++)
                    closestPair = Mathf.Min(closestPair, Vector3.Distance(centers[i], centers[j]));
            }

            Assert.That(closestPair, Is.GreaterThanOrEqualTo(PioneerStageBuilder.BeltRockSeparationMeters - 0.01f), "as pedras bem separadas, sem amontoar");
            Assert.That(PioneerStageBuilder.BeltRockMaxMeters, Is.LessThanOrEqualTo(40f), "pedras de tamanhos variados, sem rochedos gigantes (a maioria é bem menor que o máximo)");
            Assert.That(PioneerStageBuilder.BeltRockCenters(), Is.EqualTo(centers), "determinístico");
            var axis = PioneerStageBuilder.BeltFieldCamera.normalized;
            foreach (var center in centers)
            {
                var along = Mathf.Clamp(Vector3.Dot(center, axis), 0f, PioneerStageBuilder.BeltFieldCameraMeters);
                Assert.That(Vector3.Distance(center, axis * along), Is.GreaterThanOrEqualTo(PioneerStageBuilder.BeltCameraClearanceMeters - 0.01f), "nenhuma pedra no caminho da câmera");
            }
        }

        [Test]
        public void TheDaysShotIsThePointOfViewInsideTheBeltAndEveryRockStaysClear()
        {
            presentation.RegisterWithCameraDirector();
            var sequence = controller.BeltCinemaSequence;
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(50f).Within(1e-4f));
            var days = sequence.Shots.Single(s => s.ShotId == "PIO02.Days");
            Assert.That(days.RigId, Is.EqualTo(PioneerStageBuilder.BeltPovAnchorId), "ponto de vista da sonda, não mais o zoom de fora");
            var centers = PioneerStageBuilder.BeltRockCenters();
            var closest = float.MaxValue;
            var start = presentation.CameraDirector.Evaluate(days, 0f, null).Position;
            var end = presentation.CameraDirector.Evaluate(days, 11f, null).Position;
            Assert.That(start.magnitude, Is.GreaterThan(PioneerStageBuilder.BeltOuterRadiusMeters), "começa fora do campo: de longe as pedras parecem muitas e juntas");
            Assert.That(end.magnitude, Is.LessThan(PioneerStageBuilder.BeltClearRadiusMeters * 0.65f), "termina perto do centro do campo, onde há espaço vazio");
            for (var t = 0f; t <= 11f; t += 0.5f)
            {
                var pose = presentation.CameraDirector.Evaluate(days, t, null);
                Assert.That(pose.FieldOfViewDegrees, Is.InRange(60f, 82f), $"campo largo de primeira pessoa ({t} s)");
                closest = Mathf.Min(closest, centers.Select(c => Vector3.Distance(c, pose.Position)).Min());
            }

            var travelled = Vector3.Distance(start, end);
            Assert.That(travelled, Is.GreaterThan(PioneerStageBuilder.BeltOuterRadiusMeters), "a sonda atravessa o cinturão de verdade, das pedras agrupadas ao espaço vazio");
            Vector3 At(float t) => presentation.CameraDirector.Evaluate(days, t, null).Position;
            var lastSecond = Vector3.Distance(At(9f), At(10f));
            var firstSecond = Vector3.Distance(At(0f), At(1f));
            Assert.That(lastSecond, Is.GreaterThan(firstSecond * 5f), "a velocidade acelera junto com os dias contados (no fim, bem mais que no começo)");
            Assert.That(Vector3.Distance(start, At(5.5f)), Is.LessThan(travelled * 0.4f), "metade do tempo percorre menos de 40% do caminho: avanço exponencial como o contador");
            Assert.That(closest, Is.GreaterThan(PioneerStageBuilder.BeltClearRadiusMeters * 0.4f), "nenhuma pedra encosta na câmera");
        }

        [Test]
        public void TheNearestRockNoteAppearsOnlyInTheLastSecondsOfTheZoom()
        {
            // Teste de jogo: a nota saiu (a legenda já diz que a pedra mais próxima ficou longe).
            Assert.That(PioneerBeltCounter.NoteVisibleAt(PioneerBeltCounter.NoteStartSeconds), Is.False);
            Assert.That(PioneerBeltCounter.NoteVisibleAt(PioneerBeltCounter.DaysRampEndSeconds - 0.1f), Is.False);
            Assert.That(PioneerBeltCounter.NoteVisibleAt(PioneerBeltCounter.DaysRampEndSeconds), Is.False, "a revelação do 307 Nike assume o quadro");
        }

        [Test]
        public void MarsShowsOnlyInItsAnchorAndSitsRightOfTheProbeLitByTheCruiseLight()
        {
            presentation.ShowAnchor("PIO.Belt");
            Assert.That(presentation.Mars.gameObject.activeSelf, Is.False);
            presentation.ShowAnchor("PIO.Belt.Mars");
            Assert.That(presentation.Mars.gameObject.activeSelf, Is.True);
            Assert.That(presentation.BeltField.gameObject.activeSelf || presentation.BeltNike.gameObject.activeSelf, Is.False);
            presentation.ShowAnchor("PIO.Belt.Field");
            Assert.That(presentation.Mars.gameObject.activeSelf, Is.False);

            presentation.RegisterWithCameraDirector();
            Assert.That(presentation.TryGetAnchor("PIO.Belt.Mars", out var anchor), Is.True);
            var camera = new GameObject("test camera").AddComponent<Camera>();
            try
            {
                camera.transform.SetPositionAndRotation(anchor.pose.position, anchor.pose.rotation);
                camera.fieldOfView = anchor.fieldOfViewDegrees;
                camera.aspect = 16f / 9f;
                var mars = camera.WorldToViewportPoint(presentation.Mars.position);
                var probe = camera.WorldToViewportPoint(Vector3.zero);
                Assert.That(mars.z, Is.GreaterThan(0f));
                Assert.That(mars.x, Is.InRange(0.62f, 0.95f), "Marte à direita do centro");
                Assert.That(mars.y, Is.InRange(0.25f, 0.6f));
                Assert.That(probe.x, Is.InRange(0.4f, 0.6f), "a sonda segue no centro");

                var toMars = (presentation.Mars.position - camera.transform.position);
                var angularRadius = Mathf.Asin(PioneerStageBuilder.MarsRadiusMeters / toMars.magnitude) * Mathf.Rad2Deg;
                Assert.That(angularRadius, Is.InRange(3f, 10f), "grande, mas sem encher o quadro");
                Assert.That(Vector3.Angle(toMars, -camera.transform.position), Is.GreaterThan(angularRadius + 2f), "Marte não cobre a sonda");

                var towardLight = -new Vector3(-0.55f, -0.25f, -0.8f).normalized; // PIO.Light.Cruise: a luz viaja nesta direção
                var normalToCamera = (camera.transform.position - presentation.Mars.position).normalized;
                Assert.That(Vector3.Dot(normalToCamera, towardLight), Is.GreaterThan(0.6f), "a face voltada para a câmera está iluminada");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }

            Assert.That(controller.BeltCinemaSequence.Shots.First().RigId, Is.EqualTo("PIO.Belt.Mars"), "o primeiro plano do cinturão abre em Marte");
        }

        [Test]
        public void TheMoonAndMarsSurfacesAreDeterministicNeutralGreyAndOchre()
        {
            var moon = PioneerStageArt.MoonSurface(128, 64, 1969);
            var moonAgain = PioneerStageArt.MoonSurface(128, 64, 1969);
            var mars = PioneerStageArt.MarsSurface(128, 64, 1976);
            var marsAgain = PioneerStageArt.MarsSurface(128, 64, 1976);
            try
            {
                Assert.That(moon.GetPixels32(), Is.EqualTo(moonAgain.GetPixels32()), "mesma semente, mesma Lua");
                Assert.That(mars.GetPixels32(), Is.EqualTo(marsAgain.GetPixels32()), "mesma semente, mesmo Marte");
                Color Average(Texture2D t) => t.GetPixels().Aggregate(Color.black, (sum, c) => sum + c) / (t.width * t.height);
                var moonAverage = Average(moon);
                var marsAverage = Average(mars);
                Assert.That(moonAverage.r, Is.InRange(0.3f, 0.62f), $"a Lua não é nem preta nem branca ({moonAverage})");
                Assert.That(Mathf.Abs(moonAverage.r - moonAverage.b), Is.LessThan(0.05f), "cinza neutro");
                Assert.That(marsAverage.r, Is.GreaterThan(marsAverage.g + 0.1f), $"média de Marte {marsAverage}");
                Assert.That(marsAverage.g, Is.GreaterThan(marsAverage.b + 0.05f), "ocre-alaranjado: vermelho > verde > azul");
                Assert.That(moon.GetPixels32().Select(p => (int)p.r).Distinct().Count(), Is.GreaterThan(30), "há relevo, não um cinza chapado");
            }
            finally
            {
                Object.DestroyImmediate(moon);
                Object.DestroyImmediate(moonAgain);
                Object.DestroyImmediate(mars);
                Object.DestroyImmediate(marsAgain);
            }
        }

        [Test]
        public void TheCinemaSequenceHasTheFiveShotsTheSixCaptionsAndTheRealityEvent()
        {
            var sequence = controller.BeltCinemaSequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(sequence.Shots.Select(s => s.ShotId), Is.EqualTo(new[] { "PIO02.Mars", "PIO02.Fear", "PIO02.Days", "PIO02.Nike", "PIO02.Exit" }));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 8f, 11f, 11f, 10f, 10f }));
            Assert.That(sequence.Cues.Select(c => c.TextKey), Is.EqualTo(new[] { "PIO.02.01", "PIO.02.02", "PIO.02.03", "PIO.02.07", "PIO.02.04", "PIO.02.05", "PIO.02.06" }));
            Assert.That(sequence.Shots.Single(s => s.ShotId == "PIO02.Nike").Events.Select(e => e.eventId), Does.Contain(BeltInvestigation.EventReality), "a música baixa na revelação");
            Assert.That(PioneerBeltCounter.NikeVisibleAt(30.2f) && PioneerBeltCounter.NikeVisibleAt(39.9f) && !PioneerBeltCounter.NikeVisibleAt(40.1f), Is.True,
                "o rótulo acompanha o plano da revelação (30–40 s)");
        }
    }
}
