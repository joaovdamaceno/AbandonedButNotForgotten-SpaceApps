using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Roteiro v2, cena 5 (o mais perto possível): as três luas galileanas em 3D, o medidor de radiação com a estática, a comparação de
    /// distâncias no ponto mais próximo e a trajetória que curva em volta de Júpiter.
    /// </summary>
    public class PioneerClosestCinemaTests
    {
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
        public void TheRadiationMeterRisesWithTheStaticOnlyDuringTheRadiationShot()
        {
            Assert.That(PioneerClosestHud.RadiationVisibleAt(5.9f), Is.False);
            Assert.That(PioneerClosestHud.RadiationVisibleAt(6f), Is.True);
            Assert.That(PioneerClosestHud.RadiationVisibleAt(16.9f), Is.True);
            Assert.That(PioneerClosestHud.RadiationVisibleAt(17f), Is.False, "o plano da radiação termina em 17 s");
            Assert.That(PioneerClosestHud.RadiationFillAt(6f), Is.EqualTo(0.08f).Within(1e-4f));
            Assert.That(PioneerClosestHud.RadiationFillAt(PioneerClosestHud.RadiationRiseEndSeconds), Is.EqualTo(1f).Within(1e-4f));
            var previous = 0f;
            for (var t = 6f; t <= 15f; t += 0.5f)
            {
                var fill = PioneerClosestHud.RadiationFillAt(t);
                Assert.That(fill, Is.GreaterThan(previous), $"o medidor só sobe ({t} s)");
                previous = fill;
            }

            Assert.That(PioneerClosestHud.StaticOpacityAt(2f), Is.Zero, "sem estática fora do plano da radiação");
            Assert.That(PioneerClosestHud.StaticOpacityAt(7f), Is.InRange(0.05f, 0.2f), "chuvisco leve no começo");
            Assert.That(PioneerClosestHud.StaticOpacityAt(16f), Is.InRange(0.3f, 0.4f), "bem visível no pico, sem apagar o quadro");
            Assert.That(PioneerClosestHud.StaticOffsetAt(9.01f), Is.EqualTo(PioneerClosestHud.StaticOffsetAt(9.01f)), "determinístico");
            Assert.That(PioneerClosestHud.StaticOffsetAt(9.00f), Is.Not.EqualTo(PioneerClosestHud.StaticOffsetAt(9.05f)), "o chuvisco treme de um quadro para o outro");
            var offset = PioneerClosestHud.StaticOffsetAt(12.3f);
            Assert.That(offset.x, Is.InRange(0f, 127f));
            Assert.That(offset.y, Is.InRange(0f, 127f));
        }

        [Test]
        public void TheDistanceBarsShowTheMoonThenTheThreeTimesShorterLineToTheClouds()
        {
            Assert.That(PioneerClosestHud.DistancesVisibleAt(23.9f), Is.False);
            Assert.That(PioneerClosestHud.DistancesVisibleAt(24f), Is.True);
            Assert.That(PioneerClosestHud.DistancesVisibleAt(28.9f), Is.True);
            Assert.That(PioneerClosestHud.DistancesVisibleAt(29f), Is.False);
            Assert.That(PioneerClosestHud.MoonFillAt(24f), Is.Zero);
            Assert.That(PioneerClosestHud.MoonFillAt(PioneerClosestHud.MoonBarFullSeconds), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(PioneerClosestHud.CloudsFillAt(PioneerClosestHud.MoonBarFullSeconds), Is.Zero, "a barra das nuvens só cresce depois da barra da Lua");
            Assert.That(PioneerClosestHud.CloudsFillAt(PioneerClosestHud.CloudsBarFullSeconds), Is.EqualTo(PioneerClosestHud.CloudsFraction).Within(1e-4f));
            Assert.That(1f / PioneerClosestHud.CloudsFraction, Is.EqualTo(3f).Within(0.1f), "130.000 km é cerca de 3 vezes menos que 384.400 km");
        }

        [Test]
        public void TheMoonsAndTheTrajectoryShowOnlyInTheirOwnAnchors()
        {
            string[] moonAnchors = { "PIO.Closest.Ganymede", "PIO.Closest.Callisto", "PIO.Closest.Europa" };
            foreach (var id in moonAnchors)
            {
                presentation.ShowAnchor(id);
                Assert.That((presentation.Moons.gameObject.activeSelf, presentation.Trajectory.gameObject.activeSelf), Is.EqualTo((true, false)), id);
            }

            presentation.ShowAnchor("PIO.Closest.Bend");
            Assert.That((presentation.Moons.gameObject.activeSelf, presentation.Trajectory.gameObject.activeSelf, presentation.Jupiter.gameObject.activeSelf), Is.EqualTo((false, true, true)), "a curva em volta de Júpiter");
            presentation.ShowAnchor("PIO.Closest");
            Assert.That((presentation.Moons.gameObject.activeSelf, presentation.Trajectory.gameObject.activeSelf), Is.EqualTo((false, false)), "o ponto mais próximo não leva a linha");
            presentation.ShowAnchor("PIO.Launch");
            Assert.That((presentation.Moons.gameObject.activeSelf, presentation.Trajectory.gameObject.activeSelf), Is.EqualTo((false, false)));
        }

        [Test]
        public void EachMoonIsCenteredInItsOwnShotAndTheRadiiKeepTheRealRatios()
        {
            var specs = PioneerStageBuilder.MoonSpecs;
            Assert.That(specs.Select(m => m.Name), Is.EqualTo(new[] { "Ganymede", "Callisto", "Europa" }));
            Assert.That(specs[0].RadiusMeters / specs[2].RadiusMeters, Is.EqualTo(2634f / 1561f).Within(0.05f * 2634f / 1561f), "Ganimedes e Europa na razão real");
            Assert.That(specs[1].RadiusMeters / specs[2].RadiusMeters, Is.EqualTo(2410f / 1561f).Within(0.05f * 2410f / 1561f), "Calisto e Europa na razão real");

            presentation.RegisterWithCameraDirector();
            string[] moonAnchors = { "PIO.Closest.Ganymede", "PIO.Closest.Callisto", "PIO.Closest.Europa" };
            var camera = new GameObject("test camera").AddComponent<Camera>();
            try
            {
                for (var i = 0; i < moonAnchors.Length; i++)
                {
                    Assert.That(presentation.TryGetAnchor(moonAnchors[i], out var anchor), Is.True);
                    camera.transform.SetPositionAndRotation(anchor.pose.position, anchor.pose.rotation);
                    camera.fieldOfView = anchor.fieldOfViewDegrees;
                    camera.aspect = 16f / 9f;
                    var moon = presentation.Moons.GetChild(i);
                    var viewport = camera.WorldToViewportPoint(moon.position);
                    Assert.That(viewport.z, Is.GreaterThan(0f), moonAnchors[i]);
                    Assert.That(viewport.x, Is.InRange(0.4f, 0.6f), moonAnchors[i] + " no centro");
                    Assert.That(viewport.y, Is.InRange(0.4f, 0.6f), moonAnchors[i]);
                    var radius = Mathf.Asin(specs[i].RadiusMeters / (moon.position - camera.transform.position).magnitude) * Mathf.Rad2Deg;
                    Assert.That(radius, Is.InRange(5f, 12f), moonAnchors[i] + ": grande no quadro, sem sair dele");
                }
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void TheTrajectoryBendsAboutThirtyEightDegreesAroundJupiterAndShowsInTheBendShot()
        {
            var points = PioneerStageBuilder.TrajectoryPoints();
            Assert.That(points.Length, Is.GreaterThan(100));
            var first = (points[10] - points[0]).normalized;
            var last = (points[points.Length - 1] - points[points.Length - 11]).normalized;
            Assert.That(Vector3.Angle(first, last), Is.EqualTo(38f).Within(1.5f), "a gravidade curva o caminho");
            var jupiter = PioneerStageBuilder.JupiterPosition;
            var closest = points.Min(p => Vector3.Distance(p, jupiter));
            Assert.That(closest, Is.InRange(95f, 135f), "passa rente às nuvens (raio de 80 m) sem entrar no planeta");
            Assert.That(closest, Is.GreaterThan(PioneerStageBuilder.JupiterRadiusMeters + 10f));

            presentation.RegisterWithCameraDirector();
            Assert.That(presentation.TryGetAnchor("PIO.Closest.Bend", out var anchor), Is.True);
            var camera = new GameObject("test camera").AddComponent<Camera>();
            try
            {
                camera.transform.SetPositionAndRotation(anchor.pose.position, anchor.pose.rotation);
                camera.fieldOfView = anchor.fieldOfViewDegrees;
                camera.aspect = 16f / 9f;
                var inside = points.Count(p =>
                {
                    var v = camera.WorldToViewportPoint(p);
                    return v.z > 0f && v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
                });
                Assert.That(inside, Is.GreaterThan(points.Length / 5), "uma parte boa da linha aparece no plano alto");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void TheClosestSequenceStillLastsFortySecondsWithThreeMoonShotsFirstAndTheHudIsWired()
        {
            var sequence = controller.ClosestSequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(40f).Within(1e-4f));
            Assert.That(sequence.Shots.Select(s => s.ShotId), Is.EqualTo(new[]
            {
                "PIO09.Ganymede", "PIO09.Callisto", "PIO09.Europa", "PIO09.Radiation", "PIO09.Pass", "PIO09.Bend", "PIO09.Leaving",
            }));
            Assert.That(sequence.Shots.Take(3).Select(s => s.DurationSeconds), Is.EqualTo(new[] { 2f, 2f, 2f }));
            Assert.That(sequence.Shots.Take(3).Sum(s => s.DurationSeconds), Is.EqualTo(6f), "as luas ocupam os 6 s da legenda 09.01");
            Assert.That(sequence.Shots.Single(s => s.ShotId == "PIO09.Radiation").DurationSeconds, Is.EqualTo(PioneerClosestHud.RadiationEndSeconds - PioneerClosestHud.RadiationStartSeconds));
            Assert.That(sequence.Shots.All(s => presentation.TryGetAnchor(s.RigId, out _)), Is.True, "todo plano parte de um anchor do palco");
            Assert.That(controller.ValidateSetup(), Is.Empty);
        }
    }
}
