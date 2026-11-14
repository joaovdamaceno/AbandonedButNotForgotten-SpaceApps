using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-015: campo do escudo e meteorito. Prova (geometria e contrato, não o gosto visual): nenhum fragmento flutua nem
    /// afunda no terreno, o corredor do rover está livre, a rocha é do tamanho de uma bola de basquete, distinta do hardware
    /// e a até 5 m da estação do escudo, orçamentos de triângulos, LODs e materiais.
    /// </summary>
    public class MarsShieldFieldTests
    {
        const string EnvironmentPath = "Assets/_Game/Chapters/Mars/Prefabs/ShieldFieldEnvironment.prefab";
        const string RockPath = "Assets/_Game/Chapters/Mars/Prefabs/HeatShieldRockVisual.prefab";
        const string UrpLit = "Universal Render Pipeline/Lit";

        readonly List<GameObject> hosts = new List<GameObject>();
        GameObject environment;
        ShieldFieldAnchors anchors;
        MeshCollider terrain;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath);
            Assert.That(prefab, Is.Not.Null, EnvironmentPath);
            environment = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            hosts.Add(environment);
            anchors = environment.GetComponent<ShieldFieldAnchors>();
            terrain = environment.transform.Find("Terrain/Near_24m").GetComponent<MeshCollider>();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var host in hosts)
            {
                if (host != null)
                    Object.DestroyImmediate(host);
            }

            hosts.Clear();
        }

        float TerrainY(Vector3 world)
        {
            var ray = new Ray(new Vector3(world.x, 50f, world.z), Vector3.down);
            Assert.That(terrain.Raycast(ray, out var hit, 100f), Is.True, $"sem terreno em ({world.x:0.00}, {world.z:0.00})");
            return hit.point.y;
        }

        static IEnumerable<LODGroup> Groups(Transform root) => root.GetComponentsInChildren<LODGroup>();

        // ---------- Âncoras e medidas ----------

        [Test]
        public void AnchorsAreWiredAndTheMetalRockIsWithinFiveMetersOfTheShieldStation()
        {
            Assert.That(anchors.ValidateSetup(), Is.Empty, ContractValidation.Describe(anchors.ValidateSetup()));
            Assert.That(anchors.ShieldStationToMeteoriteMeters, Is.LessThanOrEqualTo(5f), "M18: no máximo cinco metros");
            Assert.That(anchors.ShieldStationToMeteoriteMeters, Is.GreaterThan(3f), "mas há um percurso de verdade");
        }

        [Test]
        public void AnchorsSitOnTheGroundAndLookAtTheirTargets()
        {
            foreach (var station in new[] { anchors.ApproachStart, anchors.ShieldStation, anchors.MeteoriteStation })
                Assert.That(station.position.y, Is.EqualTo(TerrainY(station.position)).Within(0.03f), station.name);

            var toTarget = (anchors.ShieldPhotoTarget.position - anchors.ShieldStation.position);
            toTarget.y = 0f;
            Assert.That(Vector3.Angle(anchors.ShieldStation.forward, toTarget), Is.LessThan(2f), "a estação do escudo olha para o destroço");

            var toRock = anchors.MeteoriteMeasurePoint.position - anchors.MeteoriteStation.position;
            toRock.y = 0f;
            Assert.That(Vector3.Angle(anchors.MeteoriteStation.forward, toRock), Is.LessThan(2f), "a estação da rocha olha para a rocha");
            Assert.That(toRock.magnitude, Is.InRange(1f, 2f), "ao alcance do E");
        }

        [Test]
        public void ShieldAndMeteoriteAreFramedApartSoTheyAreNotConfused()
        {
            var shieldView = anchors.ShieldCameraAnchor;
            var toShield = anchors.ShieldPhotoTarget.position - shieldView.position;
            var toRock = anchors.MeteoritePhotoTarget.position - shieldView.position;
            Assert.That(Vector3.Angle(toShield, toRock), Is.GreaterThan(40f), "a foto do escudo não inclui a rocha como se fosse hardware");

            var rockView = anchors.MeteoriteCameraAnchor;
            var rockToRock = anchors.MeteoritePhotoTarget.position - rockView.position;
            var rockToShield = anchors.ShieldPhotoTarget.position - rockView.position;
            Assert.That(Vector3.Angle(rockToRock, rockToShield), Is.LessThan(35f), "o escudo fica ao fundo da rocha: conecta hardware e ciência");
            Assert.That(rockToRock.magnitude, Is.LessThan(rockToShield.magnitude), "a rocha é o primeiro plano");
        }

        // ---------- Meteorito ----------

        [Test]
        public void MeteoriteIsBasketballSizedMetallicAndNotTheHardware()
        {
            var rock = environment.transform.Find("Meteorite");
            Assert.That(rock, Is.Not.Null);
            var lod0 = rock.Find("LOD0").GetComponent<MeshRenderer>();
            var size = lod0.localBounds.size;
            Assert.That(Mathf.Max(size.x, size.y, size.z), Is.InRange(0.22f, 0.32f), "bola de basquete ≈ 24 cm");
            Assert.That(size.y, Is.LessThan(Mathf.Max(size.x, size.z)), "base achatada onde repousa");
            Assert.That(lod0.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.LessThanOrEqualTo(1500), "≤ 1,5 mil triângulos");

            var material = lod0.sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo(UrpLit));
            Assert.That(material.GetFloat("_Metallic"), Is.GreaterThan(0.4f), "ferro e níquel");

            var others = environment.GetComponentsInChildren<Renderer>().Where(r => !r.transform.IsChildOf(rock)).Select(r => r.sharedMaterial).Distinct();
            Assert.That(others.Contains(material), Is.False, "o meteorito tem material próprio: não é hardware, terreno nem outra rocha");
        }

        [Test]
        public void MeteoriteRestsOnTheGroundAndHasMeasurePointOnTop()
        {
            var rock = environment.transform.Find("Meteorite");
            Assert.That(rock.position.y, Is.EqualTo(TerrainY(rock.position)).Within(0.04f));
            var measure = anchors.MeteoriteMeasurePoint;
            var bounds = rock.Find("LOD0").GetComponent<MeshRenderer>().bounds;
            Assert.That(measure.position.y, Is.GreaterThan(bounds.center.y), "ponto de medição na parte de cima");
            Assert.That(measure.position.y, Is.LessThan(bounds.max.y + 0.1f));
        }

        [Test]
        public void RockPrefabAloneKeepsItsAnchorsAndScale()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockPath);
            Assert.That(prefab, Is.Not.Null, RockPath);
            Assert.That(prefab.transform.Find("Anchors/MeasurePoint"), Is.Not.Null);
            Assert.That(prefab.transform.Find("Anchors/PhotoTarget"), Is.Not.Null);
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(prefab.GetComponent<SphereCollider>(), Is.Not.Null);
        }

        // ---------- Apoio ----------

        [Test]
        public void EveryDebrisPieceTouchesTheGroundAtSeveralPointsAndNothingFloats()
        {
            foreach (var name in new[] { "HeatShield_Main", "HeatShield_Flank" })
            {
                var piece = environment.transform.Find("Debris/" + name);
                var supports = piece.Find("Supports").Cast<Transform>().ToList();
                Assert.That(supports.Count, Is.GreaterThanOrEqualTo(3), name + ": três ou mais pontos de apoio");
                foreach (var support in supports)
                {
                    // O vértice da calota afunda na areia (até 15 cm); nenhum apoio fica no ar (> 12 cm): a malha de 25 cm não alcança o pico da duna junto ao aro.
                    Assert.That(support.position.y - TerrainY(support.position), Is.InRange(-0.15f, 0.12f), $"{name}/{support.name} toca o terreno");
                }
            }
        }

        [Test]
        public void NoVertexSinksDeepIntoTheTerrainOrIsEngulfed()
        {
            foreach (var name in new[] { "HeatShield_Main", "HeatShield_Flank" })
            {
                var piece = environment.transform.Find("Debris/" + name);
                var mesh = piece.Find("LOD0").GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                var deepest = 0f;
                var below = 0;
                foreach (var local in vertices)
                {
                    var world = piece.Find("LOD0").TransformPoint(local);
                    var depth = TerrainY(world) - world.y;
                    deepest = Mathf.Max(deepest, depth);
                    if (depth > 0.03f)
                        below++;
                }

                Assert.That(deepest, Is.LessThan(0.15f), name + ": nenhuma parte afunda mais que 15 cm (areia acumulada, não soterramento)");
                Assert.That(below / (float)vertices.Length, Is.LessThan(0.35f), name + ": a carcaça está parcialmente enterrada na areia, não engolida");
            }
        }

        [Test]
        public void MainPieceIsAConvexDomeRestingOnItsRimWithTheBrokenSideOpen()
        {
            // PIA07402: o destroço principal é uma cúpula convexa apoiada pela borda, com o lado rompido aberto.
            var piece = environment.transform.Find("Debris/HeatShield_Main");
            var supports = piece.Find("Supports").Cast<Transform>().ToList();
            var rim = supports.Where(s => s.name.StartsWith("Support_Rim")).ToList();
            Assert.That(rim.Count, Is.GreaterThanOrEqualTo(8), "o aro se apoia em vários pontos");
            Assert.That(supports.Any(s => s.name == "Support_CutA") && supports.Any(s => s.name == "Support_CutB"), Is.True, "as pontas do corte também descansam");

            var bounds = piece.Find("LOD0").GetComponent<MeshRenderer>().bounds;
            var lowestRim = rim.Min(s => s.position.y);
            Assert.That(bounds.max.y - lowestRim, Is.GreaterThan(0.45f), "cúpula: o topo passa bem acima do aro");

            // O ponto mais alto fica sobre o miolo, longe da borda: cúpula, não calota.
            var mesh = piece.Find("LOD0").GetComponent<MeshFilter>().sharedMesh;
            var topLocal = mesh.vertices.OrderByDescending(v => piece.Find("LOD0").TransformPoint(v).y).First();
            var top = piece.Find("LOD0").TransformPoint(topLocal);
            var center = new Vector2(bounds.center.x, bounds.center.z);
            Assert.That(Vector2.Distance(new Vector2(top.x, top.z), center), Is.LessThan(0.7f), "topo no miolo da cúpula");
            var spread = rim.Max(s => Vector2.Distance(new Vector2(top.x, top.z), new Vector2(s.position.x, s.position.z)));
            Assert.That(spread, Is.InRange(1.0f, 1.8f), "raio de ~1,3 m (aproximação de 2,65 m de diâmetro)");
        }

        [Test]
        public void ScatterRocksAndMeteoriteRestOnTheTerrain()
        {
            foreach (Transform rock in environment.transform.Find("Scatter"))
            {
                var p = rock.position;
                var inCollider = Mathf.Abs(p.x) < 11.9f && Mathf.Abs(p.z) < 11.9f;
                var ground = inCollider ? TerrainY(p) : ShieldFieldGeometry.Height(p.x, p.z); // além do trecho com colisor: malha grosseira
                Assert.That(p.y - ground, Is.InRange(inCollider ? -0.1f : -0.25f, 0.2f), rock.name);
            }

            Assert.That(environment.transform.Find("Scatter").childCount, Is.GreaterThanOrEqualTo(40), "sem planície vazia");
        }

        // ---------- Rover e terreno ----------

        [Test]
        public void RoverCorridorIsFreeOfDebrisAndRocks()
        {
            var points = new[] { anchors.ApproachStart.position, anchors.ShieldStation.position, anchors.MeteoriteStation.position };
            var checks = new List<Vector3>();
            for (var leg = 0; leg < points.Length - 1; leg++)
                for (var t = 0f; t <= 1f; t += 0.05f)
                    checks.Add(Vector3.Lerp(points[leg], points[leg + 1], t));

            foreach (var p in checks)
            {
                var found = Physics.OverlapCapsule(p + Vector3.up * 0.35f, p + Vector3.up * 1.5f, 0.85f)
                    .Where(c => c != terrain && !c.transform.IsChildOf(environment.transform.Find("Terrain"))).ToList();
                Assert.That(found.Select(c => c.name), Is.Empty, $"obstáculo no caminho em ({p.x:0.0}, {p.z:0.0})");
            }
        }

        [Test]
        public void ApproachToTheMeteoriteIsClearAndTheRockIsReachable()
        {
            var station = anchors.MeteoriteStation.position;
            var found = Physics.OverlapSphere(station + Vector3.up * 0.6f, 1.0f).Where(c => c != terrain).Select(c => c.name).ToList();
            Assert.That(found, Is.Empty, "área de aproximação livre junto da estação");
        }

        [Test]
        public void RouteIsGentleAndTheDivotIsShallow()
        {
            var a = anchors.ApproachStart.position;
            var b = anchors.MeteoriteStation.position;
            for (var t = 0f; t <= 1f; t += 0.05f)
            {
                var p = Vector3.Lerp(a, b, t);
                Assert.That(Vector3.Angle(ShieldFieldGeometry.Normal(p.x, p.z), Vector3.up), Is.LessThan(12f), $"inclinação em ({p.x:0.0}, {p.z:0.0})");
            }

            var divot = ShieldFieldGeometry.Divot;
            var center = ShieldFieldGeometry.Height(divot.x, divot.y);
            var around = new[] { new Vector2(1.7f, 0f), new Vector2(-1.7f, 0f), new Vector2(0f, 1.7f), new Vector2(0f, -1.7f) }
                .Select(o => ShieldFieldGeometry.Height(divot.x + o.x, divot.y + o.y)).Average();
            Assert.That(around - center, Is.InRange(0.03f, 0.14f), "marca do impacto: depressão rasa, não uma cratera");
        }

        // ---------- Orçamentos, LODs e materiais ----------

        [Test]
        public void ShieldPiecesRespectTheSixThousandTriangleBudgetAndKeepOneMaterial()
        {
            var triangles = 0;
            var materials = new HashSet<Material>();
            foreach (var name in new[] { "HeatShield_Main", "HeatShield_Flank" })
            {
                var piece = environment.transform.Find("Debris/" + name);
                var lod0 = piece.Find("LOD0").GetComponent<MeshFilter>().sharedMesh;
                triangles += lod0.triangles.Length / 3;
                foreach (var r in piece.GetComponentsInChildren<MeshRenderer>())
                    materials.Add(r.sharedMaterial);
            }

            Assert.That(triangles, Is.LessThanOrEqualTo(6000), "escudo e fragmentos ≤ 6 mil");
            Assert.That(materials.Count, Is.EqualTo(1), "um material para o hardware (LOD0 e LOD1 iguais)");
        }

        [Test]
        public void LodsReduceCostButKeepIdentity()
        {
            var groups = Groups(environment.transform).ToList();
            Assert.That(groups.Count, Is.EqualTo(3), "escudo, fragmento e meteorito");
            foreach (var group in groups)
            {
                var lods = group.GetLODs();
                Assert.That(lods.Length, Is.EqualTo(2), group.name);
                var near = lods[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                var far = lods[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Assert.That(far.triangles.Length, Is.LessThan(near.triangles.Length), group.name + ": LOD1 mais barato");
                Assert.That(lods[1].renderers[0].sharedMaterial, Is.SameAs(lods[0].renderers[0].sharedMaterial), group.name + ": mesma identidade de material");
                Assert.That(far.bounds.size.x, Is.EqualTo(near.bounds.size.x).Within(near.bounds.size.x * 0.15f), group.name + ": mesma silhueta");
            }
        }

        [Test]
        public void MaterialsAreUrpLitWithSmallTexturesAndTheTotalStaysInBudget()
        {
            var renderers = environment.GetComponentsInChildren<MeshRenderer>(true);
            var totalTriangles = 0;
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(UrpLit), renderer.name);
                var map = renderer.sharedMaterial.GetTexture("_BaseMap");
                Assert.That(map, Is.Not.Null, renderer.name);
                Assert.That(map.width, Is.LessThanOrEqualTo(1024), renderer.name);
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                if (!renderer.name.StartsWith("LOD1"))
                    totalTriangles += mesh.triangles.Length / 3;
            }

            Assert.That(totalTriangles, Is.LessThan(120000), "palco inteiro bem abaixo da referência de 250 mil");
            Assert.That(environment.GetComponentsInChildren<Light>().Length, Is.EqualTo(0), "a luz é da cena, não do prefab");
        }
    }
}
