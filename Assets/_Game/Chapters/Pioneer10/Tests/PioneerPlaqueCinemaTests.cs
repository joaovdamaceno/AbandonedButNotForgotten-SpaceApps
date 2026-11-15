using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Roteiro v2, cena 6 como cutscene pura: a placa dourada em 3D com as cinco gravuras e a câmera que a mostra inteira e dá um
    /// zoom moderado em cada peça, no ritmo das legendas (sem encaixe e sem barra de E).
    /// </summary>
    public class PioneerPlaqueCinemaTests
    {
        PioneerPresentation presentation;
        PioneerPlaqueActivity plaque;

        [SetUp]
        public void SetUp()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            plaque = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPlaqueActivity>(true)).Single();
        }

        [TearDown]
        public void TearDown()
        {
            presentation?.UnregisterFromCameraDirector();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void TheBoardTextureIsGoldWithEveryPieceEngravedInItsOwnSlotAndIsDeterministic()
        {
            const int width = 150;
            const int height = 225;
            var first = PioneerStageArt.PlaqueBoard(width, height);
            var second = PioneerStageArt.PlaqueBoard(width, height);
            try
            {
                CollectionAssert.AreEqual(first.GetPixels32(), second.GetPixels32(), "mesma textura sempre");
                var corner = first.GetPixel(width / 2, 1 + height / 150 * 2); // a faixa de ouro logo dentro da borda
                Assert.That(corner.r, Is.GreaterThan(0.7f).And.GreaterThan(corner.b + 0.2f), "ouro, não cinza");

                var scale = width / (float)PioneerPlaqueArt.BoardWidth;
                for (var piece = 0; piece < PioneerPlaqueArt.SlotRects.Length; piece++)
                {
                    var rect = PioneerPlaqueArt.SlotRects[piece];
                    var dark = 0;
                    var total = 0;
                    for (var y = Mathf.FloorToInt(rect.y * scale); y < Mathf.CeilToInt(rect.yMax * scale); y++)
                    {
                        for (var x = Mathf.FloorToInt(rect.x * scale); x < Mathf.CeilToInt(rect.xMax * scale); x++)
                        {
                            var pixel = first.GetPixel(x, height - 1 - y); // textura: linha 0 embaixo
                            total++;
                            if (pixel.r < 0.55f)
                                dark++;
                        }
                    }

                    Assert.That(dark / (float)total, Is.InRange(0.01f, 0.6f), $"a gravura da peça {piece} está no seu encaixe");
                }
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void TheGoldenPlateOnlyShowsInTheBoardAnchorAndOverviewFramesTheWholePlate()
        {
            Assert.That(presentation.PlaqueHero, Is.Not.Null);
            presentation.ShowAnchor("PIO.Plaque");
            Assert.That(presentation.PlaqueHero.gameObject.activeSelf, Is.False, "o close da sonda não mostra a placa grande");
            presentation.ShowAnchor("PIO.Plaque.Board");
            Assert.That(presentation.PlaqueHero.gameObject.activeSelf, Is.True);

            presentation.RegisterWithCameraDirector();
            Assert.That(presentation.TryGetAnchor("PIO.Plaque.Board", out var anchor), Is.True);
            var camera = new GameObject("test camera").AddComponent<Camera>();
            try
            {
                camera.transform.SetPositionAndRotation(anchor.pose.position, anchor.pose.rotation);
                camera.fieldOfView = anchor.fieldOfViewDegrees;
                camera.aspect = 16f / 9f;
                var center = PioneerStageBuilder.PlaqueHeroCenter;
                var top = camera.WorldToViewportPoint(center + new Vector3(0f, PioneerStageBuilder.PlaqueHeroHeightMeters * 0.5f, 0f));
                var bottom = camera.WorldToViewportPoint(center - new Vector3(0f, PioneerStageBuilder.PlaqueHeroHeightMeters * 0.5f, 0f));
                Assert.That(top.y, Is.InRange(0.88f, 0.99f), "a placa inteira cabe no quadro");
                Assert.That(bottom.y, Is.InRange(0.01f, 0.12f));
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void EachPieceIsFramedAtTheMomentItsCaptionOpensAndTheCameraNeverGetsTooClose()
        {
            presentation.RegisterWithCameraDirector();
            var sequence = plaque.Sequence;
            var fit = sequence.Shots.Single(s => s.ShotId == "PIO05.Fit");
            Assert.That(fit.RigId, Is.EqualTo("PIO.Plaque.Board"));
            var camera = new GameObject("test camera").AddComponent<Camera>();
            try
            {
                camera.aspect = 16f / 9f;
                for (var piece = 0; piece < 5; piece++)
                {
                    var t = piece * PioneerPlaqueActivity.CinemaPieceIntervalSeconds + PioneerPlaqueActivity.CinemaFirstPieceSeconds + 0.3f;
                    var pose = presentation.CameraDirector.Evaluate(fit, t, null);
                    camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                    camera.fieldOfView = pose.FieldOfViewDegrees;

                    var local = PioneerStageBuilder.PlaquePieceCenter(piece);
                    var rect = PioneerPlaqueArt.SlotRects[piece];
                    var side = Mathf.Min(rect.width, rect.height) * 0.01f;
                    var center = PioneerStageBuilder.PlaqueHeroCenter + new Vector3(local.x, local.y, 0f);
                    var viewport = camera.WorldToViewportPoint(center);
                    Assert.That(viewport.x, Is.InRange(0.46f, 0.54f), $"peça {piece} centrada na horizontal");
                    Assert.That(viewport.y, Is.InRange(0.46f, 0.54f), $"peça {piece} centrada na vertical");

                    var up = camera.WorldToViewportPoint(center + new Vector3(0f, side * 0.5f, 0f));
                    var heightInFrame = (up.y - viewport.y) * 2f;
                    Assert.That(heightInFrame, Is.InRange(0.45f, 0.75f), $"zoom moderado na peça {piece}");
                    Assert.That(Vector3.Distance(pose.Position, PioneerStageBuilder.PlaqueHeroCenter), Is.GreaterThan(2f), "a câmera não encosta na placa");
                }
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void TheCameraMovesToTheNextPieceBeforeTheNextCaptionOpens()
        {
            presentation.RegisterWithCameraDirector();
            var fit = plaque.Sequence.Shots.Single(s => s.ShotId == "PIO05.Fit");
            for (var piece = 1; piece < 5; piece++)
            {
                var trigger = piece * PioneerPlaqueActivity.CinemaPieceIntervalSeconds + PioneerPlaqueActivity.CinemaFirstPieceSeconds;
                var before = presentation.CameraDirector.Evaluate(fit, trigger - 1.5f, null).Position;
                var at = presentation.CameraDirector.Evaluate(fit, trigger, null).Position;
                var expected = PioneerStageBuilder.PlaqueHeroCenter + new Vector3(0f, 0f, 0f);
                Assert.That(Vector2.Distance(new Vector2(before.x, before.y), new Vector2(at.x, at.y)), Is.GreaterThan(0.05f), $"a câmera já saiu da peça anterior ({piece})");
                var local = PioneerStageBuilder.PlaquePieceCenter(piece);
                Assert.That(at.x - expected.x, Is.EqualTo(local.x).Within(0.05f), $"a câmera já está na peça {piece} quando a legenda abre");
                Assert.That(at.y - expected.y, Is.EqualTo(local.y).Within(0.05f));
            }
        }
    }
}
