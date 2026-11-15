using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Pedido do teste em Play Mode (15/11/2026): nas vistas por trás da sonda a câmera não cola nela (o modelo tem poucos polígonos).
    /// Os planos livres da abertura nunca chegam perto do modelo.
    /// </summary>
    public class PioneerProbeCameraDistanceTests
    {
        const float MinimumMeters = 9f;
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

        [TestCase("PIO.Launch.Reveal")]
        [TestCase("PIO.Launch")]
        public void TheFreeProbeShotsOfTheLaunchStayAtLeastNineMetersAway(string anchorId)
        {
            presentation.RegisterWithCameraDirector();
            Assert.That(presentation.TryGetAnchor(anchorId, out var anchor), Is.True);
            Assert.That(anchor.pose.position.magnitude, Is.GreaterThanOrEqualTo(MinimumMeters), anchorId);
        }

        [Test]
        public void EveryFrameOfTheFreeProbeLaunchShotsKeepsTheDistance()
        {
            presentation.RegisterWithCameraDirector();
            var sequence = AssetDatabase_LoadLaunch();
            foreach (var shotId in new[] { "PIO01.A", "PIO01.D" })
            {
                var shot = sequence.Shots.Single(s => s.ShotId == shotId);
                for (var t = 0f; t <= shot.DurationSeconds; t += 0.5f)
                {
                    var pose = presentation.CameraDirector.Evaluate(shot, t, null);
                    Assert.That(pose.Position.magnitude, Is.GreaterThanOrEqualTo(MinimumMeters - 0.5f), $"{shotId} a {t} s");
                }
            }
        }

        static SequenceDefinition AssetDatabase_LoadLaunch() =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<SequenceDefinition>("Assets/_Game/Chapters/Pioneer10/Sequences/PIO_Launch.asset");
    }
}
