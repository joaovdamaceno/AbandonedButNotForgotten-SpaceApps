using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-012: cratera Eagle. Prova (geometria e contrato, não o gosto visual): a bacia casa com a da MAR-014, o solo tem
    /// várias escalas, os afloramentos não têm vão (raios baixos acertam o colisor), as rochas estão assentadas pelas bordas, a rota
    /// de ~10 m é livre para o rover, a McKittrick só aparece depois da curva, os dois alvos são distintos e há orçamento de
    /// triângulos, LODs e materiais. Exige os prefabs gerados por <c>MAR-012 Reconstruir Eagle</c>.
    /// </summary>
    public class MarsEagleTests
    {
        const string EnvironmentPath = "Assets/_Game/Chapters/Mars/Prefabs/EagleEnvironment.prefab";
        const string BlueberriesPath = "Assets/_Game/Chapters/Mars/Prefabs/BlueberriesVisual.prefab";
        const string McKittrickPath = "Assets/_Game/Chapters/Mars/Prefabs/McKittrickVisual.prefab";
        const string UrpLit = "Universal Render Pipeline/Lit";

        readonly List<GameObject> hosts = new List<GameObject>();
        GameObject environment;
        EagleAnchors anchors;
        MeshCollider terrain;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath);
            Assert.That(prefab, Is.Not.Null, EnvironmentPath);
            environment = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            hosts.Add(environment);
            anchors = environment.GetComponent<EagleAnchors>();
            terrain = environment.transform.Find("Terrain/Near_32m").GetComponent<MeshCollider>();
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

        MeshCollider ColliderOf(string path)
        {
            var found = environment.transform.Find(path);
            Assert.That(found, Is.Not.Null, path);
            var collider = found.GetComponent<MeshCollider>();
            Assert.That(collider, Is.Not.Null, path + " tem colisor");
            return collider;
        }

        static IEnumerable<LODGroup> Groups(Transform root) => root.GetComponentsInChildren<LODGroup>(true);

        // ---------- Âncoras e rota ----------

        [Test]
        public void AnchorsAreWiredAndTheRouteMatchesTheScript()
        {
            Assert.That(anchors.ValidateSetup(), Is.Empty, ContractValidation.Describe(anchors.ValidateSetup()));
            Assert.That(anchors.StartToBlueberriesMeters, Is.GreaterThan(4.5f), "M08: o E só aparece a 4 m; há um trecho de verdade até as esferas");
            Assert.That(anchors.BlueberriesToMcKittrickMeters, Is.InRange(8.5f, 11.5f), "M10: cerca de 10 m");
            Assert.That(anchors.TargetSeparationMeters, Is.GreaterThan(6f), "dois alvos distintos, em lugares distintos");
        }

        [Test]
        public void BowlMatchesTheLandingStageSoTheCutFromM05DoesNotJump()
        {
            Assert.That(anchors.RouteStart.position.y, Is.EqualTo(MerLandingChoreography.Ground(MerLandingSite.Meridiani, 0f, 0f)).Within(0.2f), "piso no ponto de parada");
            foreach (var p in new[] { new Vector2(0f, 6f), new Vector2(-5f, -5f), new Vector2(8f, 0f), new Vector2(0f, -10f) })
            {
                var expected = MerLandingChoreography.Ground(MerLandingSite.Meridiani, p.x, p.y);
                Assert.That(EagleGeometry.Height(p.x, p.y), Is.EqualTo(expected).Within(0.35f), $"perfil em ({p.x:0}, {p.y:0})");
            }

            Assert.That(EagleGeometry.Height(25f, 25f), Is.EqualTo(0f).Within(0.2f), "planície em volta da bacia");
        }

        [Test]
        public void RouteSamplesSitOnTheGroundAndAreAboutHalfAMeterApart()
        {
            var samples = anchors.RouteSamples;
            for (var i = 0; i < samples.Count; i++)
            {
                Assert.That(samples[i].position.y, Is.EqualTo(TerrainY(samples[i].position)).Within(0.04f), "amostra " + i);
                if (i > 0)
                    Assert.That(Vector3.Distance(samples[i].position, samples[i - 1].position), Is.InRange(0.3f, 0.75f), "espaçamento na amostra " + i);
            }
        }

        [Test]
        public void RouteIsDrivableWithKeysAloneAndCarriesNoCollider()
        {
            var samples = anchors.RouteSamples;
            for (var i = 0; i < samples.Count; i++)
            {
                var p = samples[i].position;
                Assert.That(Vector3.Angle(EagleGeometry.Normal(p.x, p.z), Vector3.up), Is.LessThan(30f), $"inclinação na amostra {i} (limite do rover: 35°)");

                var side = samples[i].right;
                foreach (var lateral in new[] { -0.8f, 0f, 0.8f })
                {
                    // Pegada do rover (raio 0,8 m) em toda a largura do corredor, acima do degrau de 0,3 m.
                    var center = p + side * lateral + Vector3.up * (0.3f + EagleAnchors.RoverRadiusMeters);
                    var found = Physics.OverlapSphere(center, EagleAnchors.RoverRadiusMeters).Where(c => c != terrain).Select(c => c.name).ToList();
                    Assert.That(found, Is.Empty, $"obstáculo na amostra {i}, deslocamento {lateral:0.0} m");
                }
            }
        }

        [Test]
        public void McKittrickIsHiddenFromTheSpheresStationAndRevealedByTheBendBeforeTheStation()
        {
            var samples = anchors.RouteSamples;
            var target = anchors.McKittrick.PromptAnchor.position;
            var first = anchors.BlueberriesStationIndex;
            Assert.That(Physics.Linecast(samples[first].position + Vector3.up * 1.5f, target), Is.True, "da estação das esferas a pedra está escondida");

            var revealed = -1;
            for (var i = first; i <= anchors.McKittrickStationIndex; i++)
            {
                if (!Physics.Linecast(samples[i].position + Vector3.up * 1.5f, target))
                {
                    revealed = i;
                    break;
                }
            }

            Assert.That(revealed, Is.GreaterThan(0), "a rota acaba revelando a McKittrick");
            Assert.That(anchors.RouteLengthMeters(first, revealed), Is.GreaterThan(1.5f), "a revelação vem depois da curva, não na saída");
            Assert.That(anchors.RouteLengthMeters(revealed, anchors.McKittrickStationIndex), Is.GreaterThan(3f), "ainda sobra caminho para ver a pedra se aproximar");
        }

        // ---------- Alvos ----------

        [Test]
        public void PromptsAreVisibleFromTheirStationsAndStationsLookAtTheirTargets()
        {
            foreach (var target in new[] { anchors.Blueberries, anchors.McKittrick })
            {
                var eye = target.StationAnchor.position + Vector3.up * 1.5f;
                Assert.That(Physics.Linecast(eye, target.PromptAnchor.position), Is.False, target.name + ": o círculo E é visível da estação");
                var toTarget = target.PromptAnchor.position - target.StationAnchor.position;
                toTarget.y = 0f;
                Assert.That(Vector3.Angle(target.StationAnchor.forward, toTarget), Is.LessThan(25f), target.name + ": a estação olha para o alvo");
                Assert.That(toTarget.magnitude, Is.InRange(1.2f, 3.5f), target.name + ": ao alcance do E e da RAT (3,5 m)");
            }
        }

        [Test]
        public void StationsAreOnTheRouteAndTheTargetsAreOffIt()
        {
            var samples = anchors.RouteSamples;
            Assert.That(Vector3.Distance(samples[anchors.BlueberriesStationIndex].position, anchors.Blueberries.StationAnchor.position), Is.LessThan(0.8f));
            Assert.That(Vector3.Distance(samples[anchors.McKittrickStationIndex].position, anchors.McKittrick.StationAnchor.position), Is.LessThan(0.8f));
            foreach (var target in new[] { anchors.Blueberries, anchors.McKittrick })
            {
                var distance = samples.Min(s => Vector2.Distance(new Vector2(s.position.x, s.position.z), new Vector2(target.PromptAnchor.position.x, target.PromptAnchor.position.z)));
                Assert.That(distance, Is.GreaterThan(1.3f), target.name + ": o rover pára diante do alvo, nunca sobre ele");
            }
        }

        [Test]
        public void McKittrickHasAFlatTiltedWorkingDiscForTheRat()
        {
            var mount = anchors.McKittrick.RatSurfaceMount;
            var angle = Vector3.Angle(mount.up, Vector3.up);
            Assert.That(angle, Is.InRange(EagleGeometry.RatPatchTiltDegrees - 2f, EagleGeometry.RatPatchTiltDegrees + 2f), "inclinada para o rover, não vertical");
            var tiltDirection = new Vector3(mount.up.x, 0f, mount.up.z);
            var towardRover = anchors.McKittrick.StationAnchor.position - mount.position;
            towardRover.y = 0f;
            Assert.That(Vector3.Dot(tiltDirection.normalized, towardRover.normalized), Is.GreaterThan(0f), "a normal se inclina para o lado do rover");
            Assert.That((mount.lossyScale - Vector3.one).magnitude, Is.LessThan(1e-4f), "escala 1: a RatCavitySurface exige metros reais");

            // O colisor do jogo usa o LOD1; a planura do disco de 45 mm se mede na malha fina (LOD0).
            var fine = environment.transform.Find("McKittrickVisual/LOD0");
            Assert.That(fine, Is.Not.Null);
            var collider = fine.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = fine.GetComponent<MeshFilter>().sharedMesh;
            Physics.SyncTransforms();
            var offsets = new[] { Vector3.zero, mount.right * 0.0225f, -mount.right * 0.0225f, mount.forward * 0.0225f, -mount.forward * 0.0225f };
            foreach (var offset in offsets)
            {
                var origin = mount.position + offset + mount.up * 0.2f;
                Assert.That(collider.Raycast(new Ray(origin, -mount.up), out var hit, 0.5f), Is.True, "o disco está sobre a pedra");
                Assert.That(hit.distance, Is.EqualTo(0.2f).Within(0.004f), "o disco de 45 mm é plano (4 mm de tolerância)");
            }
        }

        [Test]
        public void BlueberriesAreSmallDarkSpheresLooseOnSoilAndEmbeddedInTheRockFace()
        {
            var visual = environment.transform.Find("BlueberriesVisual");
            var loose = visual.Find("LooseSpheres").GetComponent<MeshFilter>().sharedMesh;
            var embedded = visual.Find("EmbeddedSpheres").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(loose.vertexCount, Is.GreaterThanOrEqualTo(120 * 12), "soltas no solo (centenas de esferas)");
            Assert.That(embedded.vertexCount, Is.GreaterThanOrEqualTo(60 * 42), "presas na rocha");
            Assert.That(loose.bounds.size.y, Is.LessThan(1.0f), "ficam rente ao chão (a mancha acompanha a encosta da bacia)");
            Assert.That(embedded.bounds.size.x, Is.LessThan(1.2f), "uma mancha de rocha, não o afloramento todo");
            var material = visual.Find("LooseSpheres").GetComponent<MeshRenderer>().sharedMaterial;
            var color = material.GetColor("_BaseColor");
            Assert.That(color.b, Is.LessThanOrEqualTo(color.r + 0.02f), "cinza-escuro, nada de azul saturado (01_MARS_SOURCES)");

            // Cada esfera presa tem metade para fora do plano da face: a face (z local 0) corta os centros.
            foreach (var v in embedded.vertices)
                Assert.That(Mathf.Abs(v.z), Is.LessThanOrEqualTo(EagleGeometry.SphereMaxRadius + 0.002f), "esfera encostada na face");
        }

        // ---------- Terreno e solo ----------

        [Test]
        public void SoilHasSeveralScalesAndNoSingleOneDominates()
        {
            // Trajeto na planície (fora da bacia), amostrado a cada 5 cm; a banda de cada escala é a diferença entre duas médias móveis.
            const int count = 800;
            var h = new float[count];
            for (var i = 0; i < count; i++)
                h[i] = EagleGeometry.Height(16f + i * 0.05f, 24f);

            float[] Smooth(float[] data, int window)
            {
                var result = new float[data.Length];
                for (var i = 0; i < data.Length; i++)
                {
                    var from = Mathf.Max(0, i - window);
                    var to = Mathf.Min(data.Length - 1, i + window);
                    var sum = 0f;
                    for (var k = from; k <= to; k++)
                        sum += data[k];

                    result[i] = sum / (to - from + 1);
                }

                return result;
            }

            float Std(float[] a, float[] b)
            {
                var diff = a.Select((x, i) => x - b[i]).ToArray();
                var mean = diff.Average();
                return Mathf.Sqrt(diff.Select(x => (x - mean) * (x - mean)).Average());
            }

            var s1 = Smooth(h, 2);      // ±10 cm
            var s2 = Smooth(h, 10);     // ±50 cm
            var s3 = Smooth(h, 40);     // ±2 m
            var bands = new[] { Std(h, s1), Std(s1, s2), Std(s2, s3) };
            foreach (var band in bands)
                Assert.That(band, Is.GreaterThan(0.0003f), "há relevo em cada escala (3 cm, 30 cm e 1–2 m)");

            var total = bands.Sum(b => b * b);
            Assert.That(bands.Max(b => b * b) / total, Is.LessThan(0.9f), "nenhuma escala domina o solo");
        }

        [Test]
        public void TerrainMaterialsAreTwoAndTheNearOneHasAFineDetailMap()
        {
            var terrainRoot = environment.transform.Find("Terrain");
            var materials = terrainRoot.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial).Distinct().ToList();
            Assert.That(materials.Count, Is.LessThanOrEqualTo(2), "terreno ≤ 2 materiais (ART-001)");
            var near = terrainRoot.Find("Near_32m").GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(near.GetTexture("_DetailAlbedoMap"), Is.Not.Null, "detalhe fino sobre o mapa de 3 cm/px");
            Assert.That(near.GetTextureScale("_DetailAlbedoMap").x, Is.GreaterThan(20f), "o detalhe repete em escala bem menor que o mapa");
        }

        // ---------- Afloramentos e rochas ----------

        [Test]
        public void LedgesAndSpurHaveNoGapsFromTheFront()
        {
            var ledge = EagleGeometry.BlueberriesLedge();
            var collider = ColliderOf("Outcrops/Blueberries_Ledge");
            var into = new Vector3(ledge.Normal.x, 0f, ledge.Normal.y);
            for (var u = -ledge.HalfWidth * 0.8f; u <= ledge.HalfWidth * 0.8f; u += 0.4f)
            {
                var front = ledge.Point(0, u);
                var ground = EagleGeometry.Height(front);
                var top = EagleGeometry.TerraceTop(ledge, 0, u);
                for (var y = ground + 0.04f; y < top - 0.03f; y += 0.06f)
                {
                    var ray = new Ray(new Vector3(front.x, y, front.y) - into * 2.5f, into);
                    Assert.That(collider.Raycast(ray, out _, 4f), Is.True, $"vão na frente do afloramento em u={u:0.0}, y={y:0.00}");
                }
            }

            var spur = ColliderOf("Outcrops/Spur");
            var spurCenter = new Vector3(EagleGeometry.SpurCenter.x, 0f, EagleGeometry.SpurCenter.y);
            for (var angle = 0f; angle < 360f; angle += 30f)
            {
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var ground = EagleGeometry.Height(EagleGeometry.SpurCenter.x + direction.x * 1.2f, EagleGeometry.SpurCenter.y + direction.z * 1.2f);
                for (var y = ground + 0.05f; y < ground + 1.2f; y += 0.1f)
                {
                    var origin = spurCenter + direction * 3f + Vector3.up * y;
                    Assert.That(spur.Raycast(new Ray(origin, -direction), out _, 4f), Is.True, $"vão no esporão a {angle:0}°, y={y:0.00}");
                }
            }
        }

        [Test]
        public void OutcropMeshesAreBuriedBelowTheTerrainAtTheirBase()
        {
            foreach (var path in new[] { "Outcrops/Blueberries_Ledge/LOD0", "Outcrops/McKittrick_Ledge/LOD0", "Outcrops/Spur/LOD0" })
            {
                var t = environment.transform.Find(path);
                var mesh = t.GetComponent<MeshFilter>().sharedMesh;
                var lowest = mesh.vertices.Min(v => t.TransformPoint(v).y);
                var highestGround = mesh.vertices.Select(v => t.TransformPoint(v)).Where(w => Mathf.Abs(w.x) < 15.5f && Mathf.Abs(w.z) < 15.5f).Max(w => TerrainY(w));
                Assert.That(lowest, Is.LessThan(highestGround - 0.2f), path + ": a saia desce abaixo do chão (sem vão sob a rocha)");
                var belowGround = mesh.vertices.Count(v => { var w = t.TransformPoint(v); return w.y < TerrainY(w) - 0.1f; });
                Assert.That(belowGround, Is.GreaterThan(mesh.vertexCount / 10), path + ": parte da malha está enterrada");
            }
        }

        [Test]
        public void EveryRockIsSettledByItsEdgesAndNothingFloats()
        {
            var rocks = environment.transform.Find("Rocks");
            Assert.That(rocks.childCount, Is.GreaterThanOrEqualTo(40), "sem planície vazia");
            var large = 0;
            foreach (Transform rock in rocks)
            {
                var footprint = Mathf.Max(rock.localScale.x, rock.localScale.z) * 0.5f * 1.25f;
                var normalUp = rock.up;
                var samples = new List<Vector3> { rock.position };
                for (var i = 0; i < 12; i++)
                {
                    var a = i * Mathf.PI * 2f / 12f;
                    var p = rock.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * footprint * 0.95f;
                    samples.Add(new Vector3(p.x, EagleGeometry.Height(p.x, p.z), p.z));
                }

                foreach (var sample in samples)
                    Assert.That(Vector3.Dot(sample - rock.position, normalUp), Is.GreaterThanOrEqualTo(-0.03f), $"{rock.name}: base acima do chão (flutua)");

                var ground = EagleGeometry.Height(rock.position.x, rock.position.z);
                Assert.That(rock.position.y - ground, Is.InRange(-0.6f, 0.05f), rock.name + ": a base não fica acima do chão no centro");

                if (rock.GetComponent<LODGroup>() != null)
                {
                    large++;
                    var distance = EagleGeometry.DistanceToRoute(new Vector2(rock.position.x, rock.position.z));
                    Assert.That(distance, Is.GreaterThan(EagleGeometry.CorridorWidthMeters * 0.5f), rock.name + ": rocha grande fora do corredor");
                }
            }

            Assert.That(large, Is.GreaterThanOrEqualTo(15), "rochas grandes com LOD e colisor");
        }

        // ---------- Orçamentos, LODs e materiais ----------

        [Test]
        public void LodsReduceCostButKeepIdentityAndEveryLargeObjectHasACollider()
        {
            var groups = Groups(environment.transform).Where(g => g.GetLODs().Length == 2).ToList();
            Assert.That(groups.Count, Is.GreaterThanOrEqualTo(4 + 15), "três afloramentos, a McKittrick e as rochas grandes");
            foreach (var group in groups)
            {
                var lods = group.GetLODs();
                var near = lods[0].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                var far = lods[1].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Assert.That(far.triangles.Length, Is.LessThan(near.triangles.Length), group.name + ": LOD1 mais barato");
                Assert.That(lods[1].renderers[0].sharedMaterial, Is.SameAs(lods[0].renderers[0].sharedMaterial), group.name + ": mesma identidade de material");
                Assert.That(near.triangles.Length / 3, Is.LessThanOrEqualTo(3500), group.name + ": sem malha densa por objeto");
                Assert.That(group.GetComponent<MeshCollider>(), Is.Not.Null, group.name + ": colisor");
            }
        }

        [Test]
        public void MaterialsAreFewShareUrpLitAndTexturesStayWithinTheBudget()
        {
            var renderers = environment.GetComponentsInChildren<MeshRenderer>(true);
            var materials = renderers.Select(r => r.sharedMaterial).Distinct().ToList();
            Assert.That(materials.Count, Is.LessThanOrEqualTo(5), "solo próximo, solo longe, afloramento, rochas e esferas");
            foreach (var material in materials)
            {
                Assert.That(material.shader.name, Is.EqualTo(UrpLit), material.name);
                var map = material.GetTexture("_BaseMap");
                if (map != null)
                    Assert.That(map.width, Is.LessThanOrEqualTo(1024), material.name);
            }

            Assert.That(environment.GetComponentsInChildren<Light>().Length, Is.EqualTo(0), "a luz é da cena, não do prefab");
            Assert.That(environment.GetComponentsInChildren<Camera>().Length, Is.EqualTo(0), "a câmera é da cena, não do prefab");
        }

        [Test]
        public void SceneStaysUnderTheTriangleBudgetCountingOnlyLod0()
        {
            var triangles = 0;
            foreach (var renderer in environment.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name == "LOD1")
                    continue;

                triangles += renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
            }

            Assert.That(triangles, Is.LessThan(120000), "Eagle inteira bem abaixo da referência de 250 mil (Docs/04_WEB_AND_QA.md)");
        }

        [Test]
        public void VisualPrefabsAloneKeepScaleOneAndTheirAnchors()
        {
            foreach (var path in new[] { BlueberriesPath, McKittrickPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one), path);
                var target = prefab.GetComponent<EagleTargetAnchors>();
                Assert.That(target, Is.Not.Null, path);
                Assert.That(target.ValidateSetup(path == McKittrickPath), Is.Empty, path);
            }

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(McKittrickPath).transform.Find("Anchors/RatSurfaceMount"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(BlueberriesPath).transform.Find("Anchors/PromptAnchor"), Is.Not.Null);
        }
    }
}
