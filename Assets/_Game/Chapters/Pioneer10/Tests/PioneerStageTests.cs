using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>PIO-003: palco único da Pioneer 10 — anchors, câmera única, perfil leve, rotação, alinhamento e reconstrução.</summary>
    public class PioneerStageTests
    {
        PioneerPresentation presentation;

        [SetUp]
        public void SetUp()
        {
            Assert.That(File.Exists(PioneerStageBuilder.SceneAssetPath), "cena ausente: rode o builder pioneer10.stage (CORE-006)");
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
        }

        [TearDown]
        public void TearDown()
        {
            presentation?.UnregisterFromCameraDirector();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // descarta mudanças da prévia
        }

        [Test]
        public void SceneOpensAloneWithTheEightStateAnchorsAndAValidStage()
        {
            Assert.That(presentation.ValidateSetup(), Is.Empty);
            foreach (var state in PioneerPresentation.StateIds)
            {
                Assert.That(presentation.TryGetAnchor(state, out var anchor), Is.True, state);
                Assert.That(anchor.IsStateAnchor, Is.True);
                Assert.That(anchor.pose, Is.Not.Null);
            }

            Assert.That(presentation.Anchors.Select(a => a.stateId).Distinct(), Is.EqualTo(PioneerPresentation.StateIds), "anchors na ordem do roteiro");
            var checks = PioneerStageChecks.Run(presentation);
            Assert.That(checks.Errors, Is.Empty);
            Assert.That(checks.LargestCoordinateMeters, Is.LessThanOrEqualTo(PioneerStageChecks.MaxCoordinateMeters), "sem coordenadas enormes");
        }

        [Test]
        public void OneMainCameraOneListenerAndTheLightProfile()
        {
            var scene = SceneManager.GetActiveScene();
            var cameras = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToList();
            Assert.That(cameras, Has.Count.EqualTo(1), "uma câmera 3D principal; o quadro de imagem é UI");
            Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).Count(), Is.EqualTo(1));
            Assert.That(cameras[0].GetComponent<CameraDirector>().TargetCamera, Is.SameAs(cameras[0]));

            var cameraData = cameras[0].GetComponents<Component>().Single(c => c.GetType().Name == "UniversalAdditionalCameraData");
            Assert.That(new SerializedObject(cameraData).FindProperty("m_RenderPostProcessing").boolValue, Is.False,
                "sem pós-processamento na câmera: nenhum SSAO/DOF/motion blur entra no perfil leve");

            var lights = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true)).ToList();
            Assert.That(lights, Has.Count.EqualTo(2), "uma key e um fill");
            Assert.That(lights.All(l => l.shadows == LightShadows.None), "nenhuma luz com sombra no espaço");

            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableRendererData", new[] { "Assets/_Game/Settings" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                var features = new SerializedObject(data).FindProperty("m_RendererFeatures");
                for (var i = 0; features != null && i < features.arraySize; i++)
                {
                    var feature = features.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(feature == null || !feature.GetType().Name.Contains("AmbientOcclusion"), $"SSAO em {data.name}");
                }
            }
        }

        [Test]
        public void ShowingAnAnchorActivatesOnlyItsRegionLightAndJupiterAndPlacesTheCamera()
        {
            var camera = presentation.CameraDirector.TargetCamera;
            var regions = presentation.transform.Find("Regions");
            foreach (var anchor in presentation.Anchors)
            {
                Assert.That(presentation.ShowAnchor(anchor.anchorId), Is.True);
                var active = regions.Cast<Transform>().Where(r => r.gameObject.activeSelf).Select(r => r.name).ToList();
                Assert.That(active, Is.EqualTo(new[] { anchor.region.ToString() }), anchor.anchorId);
                Assert.That(presentation.Jupiter.gameObject.activeInHierarchy, Is.EqualTo(anchor.showJupiter), anchor.anchorId);
                Assert.That(presentation.AldebaranMarker.gameObject.activeSelf, Is.EqualTo(anchor.showAldebaranMarker), anchor.anchorId);
                Assert.That(Vector3.Distance(camera.transform.position, anchor.pose.position), Is.LessThan(1e-4f));
                Assert.That(Quaternion.Angle(camera.transform.rotation, anchor.pose.rotation), Is.LessThan(0.01f));
                Assert.That(camera.fieldOfView, Is.EqualTo(anchor.fieldOfViewDegrees).Within(1e-4f));
                Assert.That(Vector3.Distance(presentation.Sky.position, camera.transform.position), Is.LessThan(1e-3f), "céu centrado na câmera");
            }

            Assert.That(presentation.ShowAnchor("PIO.Inexistente"), Is.False);
        }

        [Test]
        public void ThePlaqueFacesTheCameraAndTheInstrumentFacesJupiterWhenTheirAnchorsOpen()
        {
            var axis = presentation.ProbeSpin.forward;
            var center = presentation.ProbeSpin.position;
            float Misalignment(string socket, Vector3 target)
            {
                presentation.TryGetSocket(socket, out var t);
                return Vector3.Angle(Vector3.ProjectOnPlane(t.position - center, axis), Vector3.ProjectOnPlane(target - center, axis));
            }

            presentation.ProbeSpin.Rotate(axis, 137f, Space.World); // qualquer fase de rotação
            presentation.ShowAnchor("PIO.Plaque");
            presentation.TryGetAnchor("PIO.Plaque", out var plaque);
            Assert.That(Misalignment("Plaque", plaque.pose.position), Is.LessThan(0.5f));
            Assert.That(presentation.EffectiveSpinDegreesPerSecond, Is.Zero, "rotação suspensa só no close da placa");

            presentation.ProbeSpin.Rotate(axis, -71f, Space.World);
            presentation.ShowAnchor("PIO.Imaging");
            Assert.That(Misalignment("Photopolarimeter", presentation.Jupiter.position), Is.LessThan(0.5f));
            Assert.That(presentation.TryGetUiFrame("PIO.Imaging", out var frame), Is.True, "quadro de imagem reservado na UI");
            Assert.That(frame.xMin, Is.GreaterThanOrEqualTo(0.5f), "quadro à direita; a sonda e Júpiter à esquerda");
            Assert.That(Vector3.Angle(presentation.ProbeSpin.forward, axis), Is.LessThan(0.01f), "alinhar não inclina o eixo de rotação");
        }

        [Test]
        public void SpinFreezesOnPauseSlowsWithReducedMotionAndNeverMovesTheCamera()
        {
            presentation.ShowAnchor("PIO.Launch");
            var camera = presentation.CameraDirector.TargetCamera.transform;
            var cameraPose = (camera.position, camera.rotation);
            var axis = presentation.ProbeSpin.forward;

            var before = presentation.ProbeSpin.rotation;
            presentation.Tick(0f); // pausa: SessionClock entrega delta ativo zero
            Assert.That(Quaternion.Angle(before, presentation.ProbeSpin.rotation), Is.Zero);

            presentation.Tick(1f);
            var cruise = Quaternion.Angle(before, presentation.ProbeSpin.rotation);
            Assert.That(cruise, Is.EqualTo(presentation.SpinDegreesPerSecond).Within(0.01f));

            presentation.SetReducedMotion(true);
            before = presentation.ProbeSpin.rotation;
            presentation.Tick(1f);
            var reduced = Quaternion.Angle(before, presentation.ProbeSpin.rotation);
            Assert.That(reduced, Is.LessThan(cruise * 0.5f).And.GreaterThan(0f), "movimento reduzido: rotação lenta, nunca vibração");

            Assert.That(Vector3.Angle(presentation.ProbeSpin.forward, axis), Is.LessThan(0.01f), "eixo de rotação estável");
            Assert.That(camera.position, Is.EqualTo(cameraPose.position), "a rotação da sonda não move a câmera");
            Assert.That(Quaternion.Angle(camera.rotation, cameraPose.rotation), Is.Zero);
        }

        [Test]
        public void AnchorsAreCameraRigsAndAHoldShotEvaluatesToTheAnchorPose()
        {
            presentation.RegisterWithCameraDirector();
            var director = presentation.CameraDirector;
            Assert.That(director.HasLookTarget(PioneerPresentation.JupiterTargetId), Is.True);
            Assert.That(director.HasLookTarget("PIO.Probe.Plaque"), Is.True);
            foreach (var anchor in presentation.Anchors)
            {
                Assert.That(director.HasRig(anchor.anchorId), Is.True, anchor.anchorId);
                var shot = presentation.CreateHoldShot(anchor.anchorId, 6f);
                Assert.That(shot.Validate(), Is.Empty);
                var pose = director.Evaluate(shot, 3f, null);
                Assert.That(Vector3.Distance(pose.Position, anchor.pose.position), Is.LessThan(1e-3f), anchor.anchorId);
                Assert.That(Quaternion.Angle(pose.Rotation, anchor.pose.rotation), Is.LessThan(0.05f), anchor.anchorId);
            }

            presentation.UnregisterFromCameraDirector();
            Assert.That(director.HasRig("PIO.Launch"), Is.False, "desregistrar não deixa rigs órfãos");
        }

        [Test]
        public void ChecksRejectACameraInsideTheSpinningProbeAndNearIdenticalConsecutiveShots()
        {
            presentation.TryGetAnchor("PIO.Launch", out var launch);
            presentation.TryGetAnchor("PIO.Belt", out var belt);
            launch.pose.position = new Vector3(0f, 2.9f, -0.26f); // no círculo varrido pela haste do RTG
            Assert.That(string.Join("\n", PioneerStageChecks.Run(presentation).Errors), Does.Contain("PIO.Launch").And.Contain("da sonda girando"));

            launch.pose.position = new Vector3(20f, 5f, 20f);
            belt.pose.position = launch.pose.position + new Vector3(0.5f, 0f, 0f);
            belt.pose.rotation = launch.pose.rotation;
            belt.fieldOfViewDegrees = launch.fieldOfViewDegrees + 2f;
            Assert.That(string.Join("\n", PioneerStageChecks.Run(presentation).Errors), Does.Contain("quase iguais: PIO.Launch → PIO.Belt"));
        }

        [Test]
        public void GeneratedMeshesFaceOutwardAndStayWithinTheWebBudget()
        {
            var total = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { PioneerStageBuilder.StageFolder + "/Meshes" }))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                var v = mesh.vertices;
                var n = mesh.normals;
                var t = mesh.triangles;
                var inverted = 0;
                var degenerate = 0;
                for (var i = 0; i < t.Length; i += 3)
                {
                    var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                    if (face.sqrMagnitude < 1e-14f)
                        degenerate++;
                    else if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) <= 0f)
                        inverted++;
                }

                Assert.That(inverted, Is.Zero, $"{mesh.name}: faces invertidas (o lado visível não coincide com a normal)");
                Assert.That(degenerate, Is.Zero, $"{mesh.name}: triângulos de área zero");
                total += t.Length / 3;
            }

            Assert.That(total, Is.LessThan(60000), "silhueta provisória + esferas muito abaixo da meta de 60 mil triângulos do prefab");
        }

        [Test]
        public void RebuildingTheStageLeavesEveryFileIdentical()
        {
            var files = Directory.GetFiles(PioneerStageBuilder.StageFolder, "*", SearchOption.AllDirectories)
                .Concat(new[] { PioneerStageBuilder.PrefabPath, PioneerStageBuilder.SceneAssetPath })
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
            var before = files.ToDictionary(p => p, File.ReadAllBytes);
            var guids = files.Where(p => !p.EndsWith(".meta")).ToDictionary(p => p, AssetDatabase.AssetPathToGUID);

            var result = SpaceAppsSceneBuilder.Run(new PioneerStageBuilder());

            Assert.That(result.Succeeded, Is.True, result.ToString());
            Assert.That(result.Changes, Is.Empty, "a segunda execução não cria nada");
            foreach (var path in files)
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before[path]), path + " mudou ao reconstruir");
            foreach (var pair in guids)
                Assert.That(AssetDatabase.AssetPathToGUID(pair.Key), Is.EqualTo(pair.Value), pair.Key);
        }
    }
}
