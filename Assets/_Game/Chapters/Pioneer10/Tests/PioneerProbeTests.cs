using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>PIO-002: prefab da Pioneer 10 — origem rastreável, escala, orçamento, rig (spin e hastes), placa presa e contrato de sockets.</summary>
    public class PioneerProbeTests
    {
        const string OriginalGlb = "ArtSource/Pioneer10/PIO-002/original/Pioneer 10.glb";

        GameObject instance;
        PioneerProbeRig rig;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PioneerProbeBuilder.PrefabAssetPath);
            Assert.That(prefab, Is.Not.Null, "prefab ausente: rode o builder pioneer10.probe (CORE-006)");
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
            rig = instance.GetComponent<PioneerProbeRig>();
        }

        [TearDown]
        public void TearDown() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        [Test]
        public void TheRigIsValidAndTheModelPassesItsOwnChecks()
        {
            Assert.That(rig.ValidateSetup(), Is.Empty);
            var report = PioneerProbeChecks.Run(rig);
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Triangles, Is.EqualTo(8199), "8.187 do modelo + 12 da placa provisória");
            Assert.That(report.Triangles, Is.LessThanOrEqualTo(PioneerProbeBuilder.TriangleBudget));
            Assert.That(report.Materials, Is.LessThanOrEqualTo(3), "meta: até 3 materiais");
            Assert.That(report.DishDiameterMeters, Is.EqualTo(PioneerProbeBuilder.DishDiameterMeters).Within(PioneerProbeChecks.DishToleranceMeters), "refletor de 2,74 m (P-S05)");
            Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(instance.transform.rotation, Is.EqualTo(Quaternion.identity), "rotação 0 / escala 1 na raiz (IMPORT_RULES §3)");
        }

        [Test]
        public void OriginProvenanceIsTraceableAndTheManifestMatchesTheOriginalFile()
        {
            Assert.That(File.Exists(OriginalGlb), "original preservado em ArtSource");
            string hash;
            using (var stream = File.OpenRead(OriginalGlb))
                hash = string.Concat(SHA256.Create().ComputeHash(stream).Select(b => b.ToString("x2")));

            var manifest = File.ReadAllText(PioneerProbeBuilder.ManifestPath);
            Assert.That(manifest, Does.Contain(hash), "o SHA-256 do manifesto é o do arquivo original");
            Assert.That(File.ReadAllText("ArtSource/Pioneer10/PIO-002/SOURCE.md"), Does.Contain(hash));
            Assert.That(File.ReadAllText("ASSET_CREDITS.md"), Does.Contain(hash), "ASSET_CREDITS registra o hash do original");
        }

        [Test]
        public void EveryTextureIsResolvedFromOneAtlasWithAWebOverride()
        {
            var materials = instance.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToList();
            Assert.That(materials, Has.None.Null);
            var atlas = materials.Select(m => m.GetTexture("_BaseMap")).Where(t => t != null).Distinct().ToList();
            Assert.That(atlas, Has.Count.EqualTo(1), "um único atlas; a placa usa cor sólida");
            Assert.That(AssetDatabase.GetAssetPath(atlas[0]), Is.EqualTo(PioneerProbeBuilder.AtlasPath));
            Assert.That(atlas[0].width, Is.EqualTo(2048));
            Assert.That(materials.All(m => m.shader.name == "Universal Render Pipeline/Lit"), "URP/Lit, sem shader importado do glTF");

            var importer = (TextureImporter)AssetImporter.GetAtPath(PioneerProbeBuilder.AtlasPath);
            var web = importer.GetPlatformTextureSettings("WebGL");
            Assert.That(web.overridden, Is.True, "override Web");
            Assert.That(web.maxTextureSize, Is.LessThanOrEqualTo(2048));
            Assert.That(importer.sRGBTexture, Is.True);
        }

        [Test]
        public void HierarchyFollowsTheImportRulesAndTheStageSocketContract()
        {
            var spin = rig.SpinRoot;
            Assert.That(spin.name, Is.EqualTo("Spin_Root"));
            Assert.That(rig.Plaque.name, Is.EqualTo("Plaque"), "a placa é um nó próprio para o close");
            Assert.That(rig.Plaque.IsChildOf(spin), "a placa gira com a sonda");
            foreach (var part in new[] { "Bus", "Dish", "Feed", "RtgBoomA", "RtgBoomB", "MagnetometerBoom" })
                Assert.That(Find(spin, part).IsChildOf(spin), part);

            // O palco da PIO-003 usa estes mesmos nomes: trocar a silhueta pelo modelo não muda as atividades.
            var stage = AssetDatabase.LoadAssetAtPath<GameObject>(PioneerStageBuilder.PrefabPath);
            var stageSockets = stage.GetComponentsInChildren<Transform>(true).Where(t => t.parent != null && t.parent.name == "Sockets").Select(t => t.name).ToList();
            Assert.That(stageSockets, Is.Not.Empty);
            foreach (var name in PioneerProbeRig.RequiredSockets)
            {
                Assert.That(stageSockets, Does.Contain(name), "o palco tem o socket " + name);
                Assert.That(rig.TryGetSocket(name, out _), Is.True, "o rig tem o socket " + name);
            }
        }

        [Test]
        public void TheAntennaPointsAlongPlusZAndTheDishOpensTowardIt()
        {
            Assert.That(rig.TryGetSocket("Feed", out var feed), Is.True);
            Assert.That(rig.TryGetSocket("EquipmentCompartment", out var bus), Is.True);
            Assert.That(feed.localPosition.z, Is.GreaterThan(1f), "o alimentador fica na frente do refletor (+Z)");
            Assert.That(bus.localPosition.z, Is.LessThan(0f), "o corpo fica atrás do vértice do refletor");
            Assert.That(new Vector2(feed.localPosition.x, feed.localPosition.y).magnitude, Is.LessThan(0.05f), "o alimentador está sobre o eixo de spin");

            var dish = Find(rig.SpinRoot, "Dish").GetComponent<MeshRenderer>().bounds;
            Assert.That(dish.min.z, Is.EqualTo(0f).Within(0.01f), "vértice do refletor em z = 0");
            Assert.That(dish.center.x, Is.EqualTo(0f).Within(0.02f));
            Assert.That(dish.center.y, Is.EqualTo(0f).Within(0.05f), "centrado no eixo de spin");
        }

        [Test]
        public void SpinRotatesEverythingAroundTheAntennaAxisAndLeavesTheAxisAlone()
        {
            rig.TryGetSocket("RtgBoomA", out var tip);
            rig.TryGetSocket("Feed", out var feed);
            var tipStart = tip.position;
            var feedStart = feed.position;
            var radius = new Vector2(tipStart.x, tipStart.y).magnitude;

            rig.SetSpinDegrees(90f);

            Assert.That(new Vector2(tip.position.x, tip.position.y).magnitude, Is.EqualTo(radius).Within(1e-3f), "gira em círculo em torno do eixo");
            Assert.That(tip.position.z, Is.EqualTo(tipStart.z).Within(1e-4f), "sem deslocamento axial");
            Assert.That(Vector3.Angle(new Vector3(tipStart.x, tipStart.y, 0f), new Vector3(tip.position.x, tip.position.y, 0f)), Is.EqualTo(90f).Within(0.05f));
            Assert.That(Vector3.Distance(feed.position, feedStart), Is.LessThan(0.05f), "o alimentador está no eixo: não se move");

            rig.SetSpinDegrees(0f);
            Assert.That(Vector3.Distance(tip.position, tipStart), Is.LessThan(1e-4f), "voltar a 0 restaura a pose");
        }

        [Test]
        public void ABoomSwingsAroundItsHingeWithoutTearingFromTheBodyAndCruiseRestoresIt()
        {
            var bus = Find(rig.SpinRoot, "Bus");
            var busPose = (bus.position, bus.rotation);
            var hinge = rig.FindHinge(PioneerBoom.RtgBoomA);
            Assert.That(hinge.axisVerified, Is.False, "o eixo de articulação é hipótese e está declarado assim");
            rig.TryGetSocket("RtgBoomA", out var tip);
            var tipCruise = tip.position;
            var hingeWorld = hinge.pivot.position;
            var length = Vector3.Distance(tipCruise, hingeWorld);

            Assert.That(rig.SetBoomDegrees(PioneerBoom.RtgBoomA, 45f), Is.True);

            Assert.That(Vector3.Distance(tip.position, hingeWorld), Is.EqualTo(length).Within(1e-3f), "a haste é rígida: gira em torno da dobradiça");
            Assert.That(Vector3.Distance(tip.position, tipCruise), Is.GreaterThan(0.5f), "a ponta se move");
            Assert.That(hinge.pivot.position, Is.EqualTo(hingeWorld), "a dobradiça não se move");
            Assert.That((bus.position, bus.rotation), Is.EqualTo(busPose), "o corpo não se move com a haste");
            Assert.That(PioneerProbeChecks.Run(rig).Errors, Has.None.Contains("RtgBoomB"), "a outra haste não é afetada");

            rig.SetBoomDegrees(PioneerBoom.RtgBoomA, 500f);
            var clamped = Quaternion.Angle(Quaternion.identity, hinge.pivot.localRotation);
            Assert.That(clamped, Is.LessThanOrEqualTo(hinge.maxDegrees + 0.01f), "limite do rig");

            rig.SetCruise();
            Assert.That(Vector3.Distance(tip.position, tipCruise), Is.LessThan(1e-4f), "cruzeiro = pose do modelo");
        }

        [Test]
        public void ThePlaqueIsPinnedToTwoAntennaStrutsAndTheCheckCatchesAFloatingOne()
        {
            var report = PioneerProbeChecks.Run(rig);
            Assert.That(report.Lines.First(l => l.StartsWith("Plaque")), Does.Contain("8 de 8 cantos"));
            Assert.That(PioneerProbeChecks.Run(rig).Errors, Is.Empty);

            rig.Plaque.localPosition += Vector3.up * 0.8f; // solta no espaço
            Assert.That(string.Join("\n", PioneerProbeChecks.Run(rig).Errors), Does.Contain("não está presa aos suportes"));
        }

        [Test]
        public void TheChecksCatchAWrongScaleAndAFloatingBoom()
        {
            Find(rig.SpinRoot, "Dish").localScale = Vector3.one * 1.2f;
            Assert.That(string.Join("\n", PioneerProbeChecks.Run(rig).Errors), Does.Contain("refletor com"));
            Find(rig.SpinRoot, "Dish").localScale = Vector3.one;

            Find(rig.SpinRoot, "MagnetometerBoom").localPosition += Vector3.down * 1.5f;
            Assert.That(string.Join("\n", PioneerProbeChecks.Run(rig).Errors), Does.Contain("MagnetometerBoom' flutuando"));
        }

        [Test]
        public void RebuildingTheProbeLeavesEveryGeneratedFileIdentical()
        {
            var files = Directory.GetFiles(PioneerProbeBuilder.ProbeFolder, "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(PioneerProbeBuilder.MaterialFolder, "*", SearchOption.AllDirectories))
                .Concat(new[] { PioneerProbeBuilder.PrefabAssetPath })
                .Select(p => p.Replace('\\', '/')).Distinct().OrderBy(p => p).ToList(); // a cena de prévia já está em ProbeFolder
            var before = files.ToDictionary(p => p, File.ReadAllBytes);
            var guids = files.Where(p => !p.EndsWith(".meta")).ToDictionary(p => p, AssetDatabase.AssetPathToGUID);

            var result = SpaceAppsSceneBuilder.Run(new PioneerProbeBuilder());

            Assert.That(result.Succeeded, Is.True, result.ToString());
            Assert.That(result.Changes, Is.Empty, "a segunda execução não cria nada");
            foreach (var path in files)
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before[path]), path + " mudou ao reconstruir");
            foreach (var pair in guids)
                Assert.That(AssetDatabase.AssetPathToGUID(pair.Key), Is.EqualTo(pair.Value), pair.Key);
        }
    }
}
