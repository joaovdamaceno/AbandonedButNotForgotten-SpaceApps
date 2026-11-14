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
    /// <summary>CORE-006: builders com dono, GUIDs estáveis, isolamento entre donos, validação e pré-verificação do build.</summary>
    public class SceneBuilderTests
    {
        const string TempRoot = "Assets/_Game/Core/Tests/EditMode/CORE006_Temp";
        const string ScenePath = TempRoot + "/Shared.unity";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(TempRoot);
        }

        abstract class TestModule : SceneBuilderModule
        {
            public override string ScenePath => SceneBuilderTests.ScenePath;
        }

        sealed class RoverModule : TestModule
        {
            public override string Id => "test.rover";
            public override string Owner => "R3 · teste";
            public override string RootName => "[R3] Rover";
            public override string AssetFolder => TempRoot + "/R3";

            public override void Build(SceneBuildContext context)
            {
                var material = context.Material(AssetFolder + "/RoverBody.mat", "Universal Render Pipeline/Lit");
                var prefab = context.Prefab(AssetFolder + "/Rover.prefab", root =>
                {
                    var body = root.transform.Find("Body")?.gameObject;
                    if (body == null)
                    {
                        body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        body.name = "Body";
                        body.transform.SetParent(root.transform, false);
                        Object.DestroyImmediate(body.GetComponent<Collider>());
                    }

                    body.GetComponent<MeshRenderer>().sharedMaterial = material;
                });

                context.PrefabInstance(context.Root.transform, prefab, "Rover");
                context.Child(context.Root.transform, "Spawn").transform.localPosition = new Vector3(0, 0, 2);
            }
        }

        sealed class EnvironmentModule : TestModule
        {
            public override string Id => "test.environment";
            public override string Owner => "R4 · teste";
            public override string RootName => "[R4] Environment";
            public override string AssetFolder => TempRoot + "/R4";

            public override void Build(SceneBuildContext context) =>
                context.Child(context.Root.transform, "Ground").transform.localScale = new Vector3(10, 1, 10);
        }

        sealed class IntruderModule : TestModule
        {
            public override string Id => "test.intruder";
            public override string Owner => "R5 · teste";
            public override string RootName => "[R5] Intruder";
            public override string AssetFolder => TempRoot + "/R5";

            public override void Build(SceneBuildContext context)
            {
                var other = context.Scene.GetRootGameObjects().First(r => r.name == "[R3] Rover");
                other.transform.position = new Vector3(99, 0, 0); // mexe na raiz de outro dono
            }
        }

        sealed class OutsideAssetModule : TestModule
        {
            public override string Id => "test.outside";
            public override string Owner => "R5 · teste";
            public override string RootName => "[R5] Outside";
            public override string AssetFolder => TempRoot + "/R5";

            public override void Build(SceneBuildContext context) =>
                context.Material(TempRoot + "/R3/Stolen.mat", "Universal Render Pipeline/Lit");
        }

        sealed class BrokenMaterialModule : TestModule
        {
            public override string Id => "test.broken";
            public override string Owner => "R6 · teste";
            public override string RootName => "[R6] Broken";
            public override string AssetFolder => TempRoot + "/R6";

            public override void Build(SceneBuildContext context)
            {
                var child = context.Child(context.Root.transform, "NoMaterial");
                context.Component<MeshFilter>(child);
                context.Component<MeshRenderer>(child).sharedMaterial = null;
            }
        }

        static string Gid(Object obj) => GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();

        static GameObject Root(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == name);

        [Test]
        public void RebuildingTwiceKeepsGuidsFileIdsAndTheSameFiles()
        {
            var first = SceneBuilder(new RoverModule());
            Assert.That(first.Succeeded, Is.True, first.ToString());
            var sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath);
            var prefabGuid = AssetDatabase.AssetPathToGUID(TempRoot + "/R3/Rover.prefab");
            var materialGuid = AssetDatabase.AssetPathToGUID(TempRoot + "/R3/RoverBody.mat");
            var instanceId = Gid(Root("[R3] Rover").transform.Find("Rover").gameObject);
            var spawnId = Gid(Root("[R3] Rover").transform.Find("Spawn").gameObject);
            var sceneText = File.ReadAllText(ScenePath);
            var prefabText = File.ReadAllText(TempRoot + "/R3/Rover.prefab");

            var second = SceneBuilder(new RoverModule());

            Assert.That(second.Succeeded, Is.True, second.ToString());
            Assert.That(second.Changes, Is.Empty, "a segunda execução não cria nada");
            Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.EqualTo(sceneGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(TempRoot + "/R3/Rover.prefab"), Is.EqualTo(prefabGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(TempRoot + "/R3/RoverBody.mat"), Is.EqualTo(materialGuid));
            Assert.That(Gid(Root("[R3] Rover").transform.Find("Rover").gameObject), Is.EqualTo(instanceId));
            Assert.That(Gid(Root("[R3] Rover").transform.Find("Spawn").gameObject), Is.EqualTo(spawnId));
            Assert.That(File.ReadAllText(ScenePath), Is.EqualTo(sceneText), "cena idêntica byte a byte");
            Assert.That(File.ReadAllText(TempRoot + "/R3/Rover.prefab"), Is.EqualTo(prefabText), "prefab idêntico byte a byte");
        }

        [Test]
        public void TwoOwnersShareASceneWithoutTouchingEachOther()
        {
            Assert.That(SceneBuilder(new RoverModule()).Succeeded, Is.True);
            Assert.That(SceneBuilder(new EnvironmentModule()).Succeeded, Is.True);
            var environment = File.ReadAllText(ScenePath);

            Assert.That(SceneBuilder(new RoverModule()).Succeeded, Is.True);

            Assert.That(Root("[R3] Rover"), Is.Not.Null);
            Assert.That(Root("[R4] Environment").transform.Find("Ground").localScale, Is.EqualTo(new Vector3(10, 1, 10)));
            Assert.That(File.ReadAllText(ScenePath), Is.EqualTo(environment), "reconstruir R3 não altera o arquivo com a parte de R4");
        }

        [Test]
        public void ABuilderThatTouchesAnotherOwnersRootIsRejectedAndNothingIsSaved()
        {
            SceneBuilder(new RoverModule());
            var before = File.ReadAllText(ScenePath);

            var result = SceneBuilder(new IntruderModule());

            Assert.That(result.Saved, Is.False);
            Assert.That(string.Join("\n", result.Errors), Does.Contain("fora de '[R5] Intruder'"));
            Assert.That(File.ReadAllText(ScenePath), Is.EqualTo(before));
            Assert.That(Root("[R5] Intruder"), Is.Null, "a cena foi recarregada do disco");
            Assert.That(Root("[R3] Rover").transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ABuilderCannotWriteAssetsInAnotherOwnersFolder()
        {
            var result = SceneBuilder(new OutsideAssetModule());
            Assert.That(result.Saved, Is.False);
            Assert.That(string.Join("\n", result.Errors), Does.Contain("só pode escrever assets em"));
            Assert.That(File.Exists(TempRoot + "/R3/Stolen.mat"), Is.False);
        }

        [Test]
        public void MissingMaterialIsReportedBeforeSaving()
        {
            var result = SceneBuilder(new BrokenMaterialModule());
            Assert.That(result.Saved, Is.False);
            Assert.That(string.Join("\n", result.Errors), Does.Contain("NoMaterial").And.Contain("material 0 ausente"));
        }

        [Test]
        public void ModulesMustHaveUniqueIdsAndRootsAndOwnedPaths()
        {
            var errors = SpaceAppsSceneBuilder.CheckModules(new SceneBuilderModule[] { new RoverModule(), new RoverModule() });
            Assert.That(string.Join("\n", errors), Does.Contain("duplicado").And.Contain("já pertence"));

            var discovered = SpaceAppsSceneBuilder.Discover(out var discoveryErrors);
            Assert.That(discoveryErrors, Is.Empty);
            Assert.That(discovered.Any(m => m.Id.StartsWith("test.")), Is.False, "módulos de teste não entram na reconstrução real");
        }

        [Test]
        public void PreflightBlocksWrongTargetEmptySceneListAndBrokenScenes()
        {
            var inactive = SpaceAppsBuild.IsActive(SpaceAppsBuild.Platform.WebGL) ? SpaceAppsBuild.Platform.Windows : SpaceAppsBuild.Platform.WebGL;
            var active = inactive == SpaceAppsBuild.Platform.WebGL ? SpaceAppsBuild.Platform.Windows : SpaceAppsBuild.Platform.WebGL;

            var wrongTarget = new SpaceAppsBuild.BuildReportData();
            SpaceAppsBuild.Preflight(new SpaceAppsBuild.BuildRequest { Platform = inactive, Scenes = new string[0] }, wrongTarget);
            Assert.That(string.Join("\n", wrongTarget.errors), Does.Contain("troque para").And.Contain("Nenhuma cena"));

            // Cena salva com renderer sem material: a pré-verificação abre, acusa e não altera nada.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var broken = new GameObject("Broken");
            broken.AddComponent<MeshFilter>();
            broken.AddComponent<MeshRenderer>();
            Directory.CreateDirectory(TempRoot);
            EditorSceneManager.SaveScene(scene, TempRoot + "/Broken.unity");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var report = new SpaceAppsBuild.BuildReportData();
            SpaceAppsBuild.Preflight(new SpaceAppsBuild.BuildRequest { Platform = active, Scenes = new[] { TempRoot + "/Broken.unity" } }, report);
            Assert.That(string.Join("\n", report.errors), Does.Contain("Broken.unity").And.Contain("material 0 ausente"));
            Assert.That(report.renderPipeline, Does.Contain("SpaceApps_URP"), "o pipeline efetivo é reportado");
        }

        [Test]
        public void WebGLWithoutTheQaProfileWarnsAndNamesThePipelineUsed()
        {
            var report = new SpaceAppsBuild.BuildReportData();
            SpaceAppsBuild.Preflight(new SpaceAppsBuild.BuildRequest { Platform = SpaceAppsBuild.Platform.WebGL, Scenes = new string[0] }, report);

            var hasWebProfile = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { SpaceAppsBuild.WebProfileFolder }).Length > 0;
            if (!hasWebProfile)
                Assert.That(string.Join("\n", report.warnings), Does.Contain("Perfil Web (QA-001) ainda não existe"));
            Assert.That(report.renderPipeline, Does.StartWith("Assets/_Game/Settings/"));
        }

        static SceneBuildResult SceneBuilder(SceneBuilderModule module) => SpaceAppsSceneBuilder.Run(module);
    }
}
