using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-020: auditoria visual e de apoio do capítulo de Marte. Instancia os prefabs reais, amostra pontos (não confia em
    /// uma olhada) e grava o resultado em <see cref="ReportPath"/>. Cobre: (1) câmeras dos cortes do pouso acima do chão nos dois
    /// palcos, (2) apoios dos destroços do escudo e meteorito no chão, (3) afloramentos e rochas de Eagle sem vão nem flutuando,
    /// (4) recuo da RAT sem atravessar o piso, (5) shaders, normais e sombras para a plataforma Web, (6) perfis leve e equilibrado
    /// e (7) rastro do rover da cena antiga.
    ///
    /// Não substitui o olho: mancha branca, ciência legível e clipping fino continuam nas capturas de ângulo baixo de
    /// <c>Assets/_Game/Chapters/Mars/Tests/MarsVisualChecklist.md</c>. Não altera nenhum asset: só lê e instancia na memória.
    /// </summary>
    public static class MarsSupportAudit
    {
        public const string ReportPath = "QA/MAR-020/support-audit.txt";

        const string MarsPath = "Assets/_Game/Chapters/Mars";
        const string EaglePath = MarsPath + "/Prefabs/EagleEnvironment.prefab";
        const string ShieldFieldPath = MarsPath + "/Prefabs/ShieldFieldEnvironment.prefab";
        const string RatPath = MarsPath + "/Prefabs/RatActivity.prefab";
        const string OpportunityRoverPath = MarsPath + "/Prefabs/OpportunityVisual.prefab";
        const string SpiritRoverPath = MarsPath + "/Prefabs/SpiritVisual.prefab";
        const string OldRoverControllerPath = MarsPath + "/Opportunity/Scripts/RoverController.cs";
        // Mesmo caminho de WebBaselineBuilder.Profile (assembly Web.Editor, não referenciado aqui).
        const string LightProfilePath = "Assets/_Game/Settings/Web/WebLight.asset";

        // Tolerâncias: as mesmas dos testes de MarsShieldFieldTests / MarsEagleTests / MarsLandingTests, para o relatório
        // e os testes nunca discordarem sobre o que é "vão".
        const float ShieldSupportBelow = -0.15f, ShieldSupportAbove = 0.12f;
        const float ShieldDeepestSink = 0.15f, ShieldSunkFraction = 0.35f;
        const float CameraStepSeconds = 0.02f, CameraGroundSlack = 0.02f;
        const float ColliderCameraFloorMeters = 0.05f;
        const float RatBelowPivotMeters = 0.005f;

        /// <summary>Falhas da última execução (0 = auditoria limpa).</summary>
        public static int LastFailureCount { get; private set; }

        sealed class Report
        {
            public readonly List<string> Lines = new List<string>();
            public int Failures;
            public int Warnings;

            public void Section(string title)
            {
                Lines.Add(string.Empty);
                Lines.Add("## " + title);
            }

            public void Pass(string text) => Lines.Add("[OK]     " + text);
            public void Info(string text) => Lines.Add("[info]   " + text);

            public void Warn(string text)
            {
                Warnings++;
                Lines.Add("[AVISO]  " + text);
            }

            public void Fail(string text)
            {
                Failures++;
                Lines.Add("[FALHA]  " + text);
            }

            public void Check(bool ok, string passText, string failText)
            {
                if (ok)
                    Pass(passText);
                else
                    Fail(failText);
            }
        }

        static string F(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        // ---------- Entrada ----------

        [MenuItem("Abandoned Space Apps/Marte/MAR-020 Auditar apoios e câmeras")]
        public static void RunFromMenu()
        {
            var failures = Run();
            var message = failures == 0
                ? "MAR-020: auditoria limpa. Relatório em " + ReportPath
                : "MAR-020: " + failures + " falha(s). Veja " + ReportPath;
            if (failures == 0)
                Debug.Log("[MarsSupportAudit] " + message);
            else
                Debug.LogError("[MarsSupportAudit] " + message);
        }

        /// <summary>Unity -batchmode -executeMethod Abandoned.SpaceApps.Chapters.Mars.EditorTools.MarsSupportAudit.RunFromCommandLine</summary>
        public static void RunFromCommandLine()
        {
            var failures = Run();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        /// <summary>Roda tudo, grava o relatório e devolve o número de falhas.</summary>
        public static int Run()
        {
            var report = new Report();
            report.Lines.Add("MAR-020: auditoria de apoios, câmeras de pouso, RAT e Web");
            report.Lines.Add("Unity " + Application.unityVersion + " | alvo ativo do Editor: " + EditorUserBuildSettings.activeBuildTarget + " | " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));

            Guard(report, "Câmeras dos cortes do pouso", AuditLandingCameras);
            Guard(report, "Apoios do campo do escudo", AuditShieldField);
            Guard(report, "Afloramentos e rochas de Eagle", AuditEagle);
            Guard(report, "Recuo da RAT", AuditRat);
            Guard(report, "Shaders, normais e sombras (Web)", AuditWebMaterials);
            Guard(report, "Perfis leve e equilibrado", AuditQualityProfiles);
            Guard(report, "Rastro do rover (cena antiga)", AuditTrailSource);

            report.Lines.Add(string.Empty);
            report.Lines.Add("Resumo: " + report.Failures + " falha(s), " + report.Warnings + " aviso(s).");
            report.Lines.Add(report.Failures == 0
                ? "Resultado: sem falhas nos pontos amostrados. Falta o olho: capturas de ângulo baixo (MarsVisualChecklist.md)."
                : "Resultado: corrija as falhas acima, reconstrua o builder correspondente e rode de novo.");

            var directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(ReportPath, string.Join("\n", report.Lines) + "\n", new UTF8Encoding(false));

            LastFailureCount = report.Failures;
            return report.Failures;
        }

        static void Guard(Report report, string name, Action<Report> audit)
        {
            report.Section(name);
            try
            {
                audit(report);
            }
            catch (Exception exception)
            {
                report.Fail(name + ": exceção " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        static GameObject Spawn(Report report, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report.Fail("prefab ausente: " + path + " (rode o builder correspondente antes)");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Physics.SyncTransforms();
            return instance;
        }

        static void Destroy(GameObject instance)
        {
            if (instance != null)
                UnityEngine.Object.DestroyImmediate(instance);
        }

        static bool TryGroundY(MeshCollider collider, Vector3 world, out float y)
        {
            if (collider != null && collider.Raycast(new Ray(new Vector3(world.x, 50f, world.z), Vector3.down), out var hit, 100f))
            {
                y = hit.point.y;
                return true;
            }

            y = 0f;
            return false;
        }

        static MeshCollider ColliderAt(Transform root, string path)
        {
            var child = root.Find(path);
            return child != null ? child.GetComponent<MeshCollider>() : null;
        }

        // ---------- 1. Câmeras dos cortes do pouso ----------

        static void AuditLandingCameras(Report report)
        {
            var root = Spawn(report, MerLandingBuilder.PrefabPath);
            if (root == null)
                return;

            try
            {
                var director = root.GetComponent<MarsDualLandingDirector>();
                if (director == null || director.Opportunity == null || director.Spirit == null)
                {
                    report.Fail("o prefab do pouso não tem o diretor e os dois palcos ligados");
                    return;
                }

                var stages = new[]
                {
                    new KeyValuePair<string, MerLandingAssembly>("Opportunity", director.Opportunity),
                    new KeyValuePair<string, MerLandingAssembly>("Spirit", director.Spirit),
                };

                foreach (var step in new[] { "M04", "M05", "M06" })
                {
                    var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MerLandingBuilder.SequencePath(step));
                    if (sequence == null)
                    {
                        report.Fail(step + ": sequência ausente em " + MerLandingBuilder.SequencePath(step));
                        continue;
                    }

                    foreach (var stage in stages)
                    {
                        var assembly = stage.Value;
                        var site = stage.Key == "Opportunity" ? MerLandingBuilder.OpportunitySite : assembly.Site;
                        var groundCollider = ColliderAt(assembly.transform, "Ground");
                        foreach (var shot in sequence.Shots)
                            AuditShot(report, step, stage.Key, assembly, site, groundCollider, shot);
                    }
                }
            }
            finally
            {
                Destroy(root);
            }
        }

        static void AuditShot(Report report, string step, string stage, MerLandingAssembly assembly, MerLandingSite site, MeshCollider groundCollider, ShotDefinition shot)
        {
            float minimum = float.MaxValue, minimumAt = 0f, atStart = 0f, atEnd = 0f;
            for (var t = 0f; t <= shot.DurationSeconds + 1e-4f; t += CameraStepSeconds)
            {
                var time = Mathf.Min(t, shot.DurationSeconds);
                CameraDirector.SampleKeyframes(shot, time, out var position, out _, out _);
                var clearance = position.y - MerLandingChoreography.Ground(site, position.x, position.z);
                if (t <= 0f)
                    atStart = clearance;
                atEnd = clearance;
                if (clearance < minimum)
                {
                    minimum = clearance;
                    minimumAt = time;
                }
            }

            var required = shot.MinGroundClearanceMeters - CameraGroundSlack;
            var label = step + "/" + shot.ShotId + " " + stage;
            report.Check(minimum >= required,
                label + ": mínimo " + F(minimum) + " m acima do terreno (exigido " + F(shot.MinGroundClearanceMeters) + " m); cortes: início " + F(atStart) + " m, fim " + F(atEnd) + " m",
                label + ": a câmera chega a " + F(minimum) + " m do terreno em t=" + F(minimumAt) + " s, abaixo dos " + F(shot.MinGroundClearanceMeters) + " m exigidos");

            // O que o jogador vê é o colisor do palco, não a função de altura: confere os mesmos pontos contra ele.
            if (groundCollider == null)
            {
                report.Warn(label + ": palco sem colisor 'Ground'; checagem contra o colisor pulada");
                return;
            }

            float worstCollider = float.MaxValue, worstAt = 0f;
            var outside = 0;
            for (var t = 0f; t <= shot.DurationSeconds + 1e-4f; t += 0.1f)
            {
                var time = Mathf.Min(t, shot.DurationSeconds);
                CameraDirector.SampleKeyframes(shot, time, out var local, out _, out _);
                var world = assembly.transform.TransformPoint(local);
                if (!groundCollider.Raycast(new Ray(world + Vector3.up * 60f, Vector3.down), out var hit, 200f))
                {
                    outside++;
                    continue;
                }

                var clearance = world.y - hit.point.y;
                if (clearance < worstCollider)
                {
                    worstCollider = clearance;
                    worstAt = time;
                }
            }

            if (worstCollider == float.MaxValue)
                report.Warn(label + ": nenhuma amostra caiu dentro do colisor (câmera fora do trecho de colisão)");
            else
                report.Check(worstCollider >= ColliderCameraFloorMeters,
                    label + ": acima do colisor em todas as amostras (mínimo " + F(worstCollider) + " m)" + (outside > 0 ? "; " + outside + " amostra(s) fora do trecho" : string.Empty),
                    label + ": câmera a " + F(worstCollider) + " m do colisor em t=" + F(worstAt) + " s (sob o chão)");
        }

        // ---------- 2. Apoios do campo do escudo ----------

        static void AuditShieldField(Report report)
        {
            var environment = Spawn(report, ShieldFieldPath);
            if (environment == null)
                return;

            try
            {
                var terrain = ColliderAt(environment.transform, "Terrain/Near_24m");
                if (terrain == null)
                {
                    report.Fail("ShieldFieldEnvironment sem 'Terrain/Near_24m' com MeshCollider");
                    return;
                }

                foreach (var name in new[] { "HeatShield_Main", "HeatShield_Flank" })
                {
                    var piece = environment.transform.Find("Debris/" + name);
                    var supportsRoot = piece != null ? piece.Find("Supports") : null;
                    if (supportsRoot == null)
                    {
                        report.Fail(name + ": sem Debris/" + name + "/Supports");
                        continue;
                    }

                    var supports = supportsRoot.Cast<Transform>().ToList();
                    report.Check(supports.Count >= 3, name + ": " + supports.Count + " pontos de apoio", name + ": só " + supports.Count + " ponto(s) de apoio (mínimo 3)");

                    var bad = new List<string>();
                    float worstAbove = float.MinValue, worstBelow = float.MaxValue;
                    foreach (var support in supports)
                    {
                        if (!TryGroundY(terrain, support.position, out var ground))
                        {
                            bad.Add(support.name + " sem terreno");
                            continue;
                        }

                        var offset = support.position.y - ground;
                        worstAbove = Mathf.Max(worstAbove, offset);
                        worstBelow = Mathf.Min(worstBelow, offset);
                        if (offset < ShieldSupportBelow || offset > ShieldSupportAbove)
                            bad.Add(support.name + " a " + F(offset) + " m");
                    }

                    report.Check(bad.Count == 0,
                        name + ": todos os apoios tocam o terreno (de " + F(worstBelow) + " a " + F(worstAbove) + " m)",
                        name + ": apoio(s) fora de [" + F(ShieldSupportBelow) + ", " + F(ShieldSupportAbove) + "] m: " + string.Join(", ", bad));

                    AuditShieldMesh(report, name, piece, terrain);
                }

                AuditMeteorite(report, environment.transform, terrain);
            }
            finally
            {
                Destroy(environment);
            }
        }

        static void AuditShieldMesh(Report report, string name, Transform piece, MeshCollider terrain)
        {
            var lod = piece.Find("LOD0");
            var filter = lod != null ? lod.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
            {
                report.Fail(name + ": sem LOD0 com malha");
                return;
            }

            var vertices = filter.sharedMesh.vertices;
            float deepest = 0f;
            var below = 0;
            foreach (var local in vertices)
            {
                var world = lod.TransformPoint(local);
                if (!TryGroundY(terrain, world, out var ground))
                    continue;
                var depth = ground - world.y;
                deepest = Mathf.Max(deepest, depth);
                if (depth > 0.03f)
                    below++;
            }

            var fraction = below / (float)Mathf.Max(1, vertices.Length);
            report.Check(deepest < ShieldDeepestSink && fraction < ShieldSunkFraction,
                name + ": parte mais funda " + F(deepest) + " m, " + Mathf.RoundToInt(fraction * 100f) + "% dos vértices abaixo do chão (assentada, não engolida)",
                name + ": afunda " + F(deepest) + " m e " + Mathf.RoundToInt(fraction * 100f) + "% dos vértices ficam abaixo do chão (limites " + F(ShieldDeepestSink) + " m e " + Mathf.RoundToInt(ShieldSunkFraction * 100f) + "%)");
        }

        static void AuditMeteorite(Report report, Transform environment, MeshCollider terrain)
        {
            var rock = environment.Find("Meteorite");
            if (rock == null)
            {
                report.Fail("Meteorite ausente no campo do escudo");
                return;
            }

            if (!TryGroundY(terrain, rock.position, out var ground))
            {
                report.Fail("Meteorite fora do terreno com colisor");
                return;
            }

            var offset = rock.position.y - ground;
            report.Check(Mathf.Abs(offset) <= 0.04f, "Meteorite assentado no chão (" + F(offset) + " m)", "Meteorite a " + F(offset) + " m do chão (limite 0,04 m)");

            var scatter = environment.Find("Scatter");
            if (scatter == null)
                return;

            var floating = new List<string>();
            foreach (Transform scatterRock in scatter)
            {
                var p = scatterRock.position;
                var inCollider = Mathf.Abs(p.x) < 11.9f && Mathf.Abs(p.z) < 11.9f;
                var groundY = inCollider ? (TryGroundY(terrain, p, out var y) ? y : p.y) : ShieldFieldGeometry.Height(p.x, p.z);
                var difference = p.y - groundY;
                if (difference < (inCollider ? -0.1f : -0.25f) || difference > 0.2f)
                    floating.Add(scatterRock.name + " (" + F(difference) + " m)");
            }

            report.Check(floating.Count == 0,
                scatter.childCount + " rochas espalhadas assentadas no terreno",
                floating.Count + " rocha(s) espalhada(s) flutuando ou enterrada(s): " + string.Join(", ", floating.Take(8)));
        }

        // ---------- 3. Eagle: afloramentos e rochas ----------

        static void AuditEagle(Report report)
        {
            var environment = Spawn(report, EaglePath);
            if (environment == null)
                return;

            try
            {
                var terrain = ColliderAt(environment.transform, "Terrain/Near_32m");
                if (terrain == null)
                {
                    report.Fail("EagleEnvironment sem 'Terrain/Near_32m' com MeshCollider");
                    return;
                }

                AuditLedgeGaps(report, environment.transform);
                AuditOutcropBase(report, environment.transform, terrain);
                AuditRocksSettled(report, environment.transform);
            }
            finally
            {
                Destroy(environment);
            }
        }

        static void AuditLedgeGaps(Report report, Transform environment)
        {
            var ledgeCollider = ColliderAt(environment, "Outcrops/Blueberries_Ledge");
            var spurCollider = ColliderAt(environment, "Outcrops/Spur");
            if (ledgeCollider == null || spurCollider == null)
            {
                report.Fail("faltam os colisores de Outcrops/Blueberries_Ledge e/ou Outcrops/Spur");
                return;
            }

            // Raios baixos de frente: cada um que passa direto é um vão onde a câmera ou o rover enxergam através da rocha.
            var ledge = EagleGeometry.BlueberriesLedge();
            var into = new Vector3(ledge.Normal.x, 0f, ledge.Normal.y);
            int ledgeRays = 0, ledgeGaps = 0;
            string firstLedgeGap = null;
            for (var u = -ledge.HalfWidth * 0.8f; u <= ledge.HalfWidth * 0.8f; u += 0.4f)
            {
                var front = ledge.Point(0, u);
                var ground = EagleGeometry.Height(front);
                var top = EagleGeometry.TerraceTop(ledge, 0, u);
                for (var y = ground + 0.04f; y < top - 0.03f; y += 0.06f)
                {
                    ledgeRays++;
                    var ray = new Ray(new Vector3(front.x, y, front.y) - into * 2.5f, into);
                    if (ledgeCollider.Raycast(ray, out _, 4f))
                        continue;
                    ledgeGaps++;
                    if (firstLedgeGap == null)
                        firstLedgeGap = "u=" + F(u) + ", y=" + F(y);
                }
            }

            report.Check(ledgeGaps == 0,
                "Blueberries_Ledge: " + ledgeRays + " raios de frente, nenhum vão",
                "Blueberries_Ledge: " + ledgeGaps + " de " + ledgeRays + " raios atravessam a rocha (primeiro em " + firstLedgeGap + ")");

            var spurCenter = new Vector3(EagleGeometry.SpurCenter.x, 0f, EagleGeometry.SpurCenter.y);
            int spurRays = 0, spurGaps = 0;
            string firstSpurGap = null;
            for (var angle = 0f; angle < 360f; angle += 30f)
            {
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var ground = EagleGeometry.Height(EagleGeometry.SpurCenter.x + direction.x * 1.2f, EagleGeometry.SpurCenter.y + direction.z * 1.2f);
                for (var y = ground + 0.05f; y < ground + 1.2f; y += 0.1f)
                {
                    spurRays++;
                    var origin = spurCenter + direction * 3f + Vector3.up * y;
                    if (spurCollider.Raycast(new Ray(origin, -direction), out _, 4f))
                        continue;
                    spurGaps++;
                    if (firstSpurGap == null)
                        firstSpurGap = F(angle) + " graus, y=" + F(y);
                }
            }

            report.Check(spurGaps == 0,
                "Spur: " + spurRays + " raios em volta, nenhum vão",
                "Spur: " + spurGaps + " de " + spurRays + " raios atravessam a rocha (primeiro a " + firstSpurGap + ")");
        }

        static void AuditOutcropBase(Report report, Transform environment, MeshCollider terrain)
        {
            foreach (var path in new[] { "Outcrops/Blueberries_Ledge/LOD0", "Outcrops/McKittrick_Ledge/LOD0", "Outcrops/Spur/LOD0" })
            {
                var t = environment.Find(path);
                var filter = t != null ? t.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null)
                {
                    report.Fail(path + ": sem malha");
                    continue;
                }

                var lowest = float.MaxValue;
                var highestGround = float.MinValue;
                var buried = 0;
                var count = 0;
                foreach (var local in filter.sharedMesh.vertices)
                {
                    var world = t.TransformPoint(local);
                    lowest = Mathf.Min(lowest, world.y);
                    count++;
                    if (Mathf.Abs(world.x) < 15.5f && Mathf.Abs(world.z) < 15.5f && TryGroundY(terrain, world, out var ground))
                    {
                        highestGround = Mathf.Max(highestGround, ground);
                        if (world.y < ground - 0.1f)
                            buried++;
                    }
                }

                // A saia desce abaixo do chão: sem ela há um vão visível sob a rocha em ângulo baixo.
                var ok = lowest < highestGround - 0.2f && buried > count / 10;
                report.Check(ok,
                    path + ": base enterrada (mais baixo " + F(lowest) + " m, chão mais alto " + F(highestGround) + " m, " + buried + " vértices enterrados)",
                    path + ": a base não desce o bastante abaixo do chão (mais baixo " + F(lowest) + " m, chão mais alto " + F(highestGround) + " m, " + buried + " vértices enterrados): vão sob a rocha");
            }
        }

        static void AuditRocksSettled(Report report, Transform environment)
        {
            var rocks = environment.Find("Rocks");
            if (rocks == null)
            {
                report.Fail("EagleEnvironment sem o grupo 'Rocks'");
                return;
            }

            var floating = new List<string>();
            var outsideCorridor = new List<string>();
            var large = 0;
            foreach (Transform rock in rocks)
            {
                var footprint = Mathf.Max(rock.localScale.x, rock.localScale.z) * 0.5f * 1.25f;
                var normalUp = rock.up;
                var worstEdge = float.MaxValue;
                for (var i = 0; i < 12; i++)
                {
                    var a = i * Mathf.PI * 2f / 12f;
                    var p = rock.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * footprint * 0.95f;
                    var sample = new Vector3(p.x, EagleGeometry.Height(p.x, p.z), p.z);
                    worstEdge = Mathf.Min(worstEdge, Vector3.Dot(sample - rock.position, normalUp));
                }

                var centerOffset = rock.position.y - EagleGeometry.Height(rock.position.x, rock.position.z);
                if (worstEdge < -0.03f || centerOffset < -0.6f || centerOffset > 0.05f)
                    floating.Add(rock.name + " (borda " + F(worstEdge) + ", centro " + F(centerOffset) + ")");

                if (rock.GetComponent<LODGroup>() != null)
                {
                    large++;
                    if (EagleGeometry.DistanceToRoute(new Vector2(rock.position.x, rock.position.z)) <= EagleGeometry.CorridorWidthMeters * 0.5f)
                        outsideCorridor.Add(rock.name);
                }
            }

            report.Check(floating.Count == 0,
                rocks.childCount + " rochas assentadas pelas bordas, nenhuma flutuando",
                floating.Count + " rocha(s) flutuando ou fora do chão: " + string.Join(", ", floating.Take(8)));
            report.Check(outsideCorridor.Count == 0,
                large + " rochas grandes, todas fora do corredor do rover",
                outsideCorridor.Count + " rocha(s) grande(s) dentro do corredor do rover: " + string.Join(", ", outsideCorridor.Take(8)));
        }

        // ---------- 4. Recuo da RAT ----------

        static void AuditRat(Report report)
        {
            var root = Spawn(report, RatPath);
            if (root == null)
                return;

            try
            {
                var rat = root.GetComponent<MarsRatActivity>();
                if (rat == null || rat.Tool == null || rat.Surface == null)
                {
                    report.Fail("RatActivity sem a ferramenta e a superfície ligadas");
                    return;
                }

                var apply = typeof(MarsRatActivity).GetMethod("ApplyToolPose", BindingFlags.Instance | BindingFlags.NonPublic);
                if (apply == null)
                {
                    report.Warn("MarsRatActivity.ApplyToolPose não encontrado (renomeado?); recuo da RAT não amostrado");
                    return;
                }

                var retractProperty = new SerializedObject(rat).FindProperty("retractDistanceMeters");
                var retract = retractProperty != null ? retractProperty.floatValue : 0.25f;

                // Pior caso: cavidade na profundidade final, que é quando a ferramenta recua.
                var surface = rat.Surface;
                surface.SetProgress(1f);
                var origin = surface.transform.position;
                var normal = surface.Normal;
                var filters = rat.Tool.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToList();
                if (filters.Count == 0)
                {
                    report.Fail("a ferramenta da RAT não tem malha para medir");
                    return;
                }

                const int steps = 25;
                var previousLowest = float.MinValue;
                var monotonic = true;
                var belowCount = 0;
                float worstBelow = 0f, worstAtOffset = 0f;
                for (var i = 0; i <= steps; i++)
                {
                    var offset = retract * i / steps;
                    apply.Invoke(rat, new object[] { offset });
                    Physics.SyncTransforms();

                    var lowest = float.MaxValue;
                    foreach (var filter in filters)
                    {
                        var matrix = filter.transform.localToWorldMatrix;
                        foreach (var vertex in filter.sharedMesh.vertices)
                            lowest = Mathf.Min(lowest, Vector3.Dot(matrix.MultiplyPoint3x4(vertex) - origin, normal));
                    }

                    // O piso da ponta acompanha a cavidade: 100% da profundidade encostada, 0 a partir de 2 cm de subida.
                    var floor = surface.DeepestMeters * (1f - Mathf.Clamp01(offset / 0.02f));
                    var below = -floor - lowest;
                    if (below > RatBelowPivotMeters)
                    {
                        belowCount++;
                        if (below > worstBelow)
                        {
                            worstBelow = below;
                            worstAtOffset = offset;
                        }
                    }

                    if (lowest < previousLowest - 1e-5f)
                        monotonic = false;
                    previousLowest = lowest;
                }

                report.Check(belowCount == 0,
                    "RAT: nenhum vértice da ferramenta passa abaixo do piso em " + (steps + 1) + " pontos do recuo (0 a " + F(retract) + " m)",
                    "RAT: vértice " + F(worstBelow * 1000f) + " mm abaixo do piso a " + F(worstAtOffset) + " m de recuo (" + belowCount + " ponto(s)): o pivô não está na ponta ou a malha desce além dele");
                report.Check(monotonic, "RAT: o recuo só se afasta da rocha, sem voltar a descer", "RAT: a altura da ferramenta diminui durante o recuo");
                report.Check(Vector3.Dot(rat.Tool.position - origin, normal) >= 0.2f,
                    "RAT: recolhida a " + F(Vector3.Dot(rat.Tool.position - origin, normal)) + " m da superfície (mínimo 0,20 m)",
                    "RAT: recolhida a só " + F(Vector3.Dot(rat.Tool.position - origin, normal)) + " m da superfície (mínimo 0,20 m)");
                report.Info("o contato da RAT com a McKittrick no jogo (rover, braço, rocha) só se vê nas capturas de ângulo baixo do checklist");
            }
            finally
            {
                Destroy(root);
            }
        }

        // ---------- 5. Shaders, normais e sombras para a Web ----------

        static void AuditWebMaterials(Report report)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.WebGL)
                report.Pass("plataforma ativa do Editor: WebGL");
            else
                report.Warn("plataforma ativa do Editor: " + target + ". Troque para WebGL, espere a importação e rode de novo: sem isso os shaders da Web não foram conferidos (Docs/04_WEB_AND_QA.md)");

            var materials = new Dictionary<Material, string>();
            foreach (var path in new[] { OpportunityRoverPath, SpiritRoverPath, EaglePath, ShieldFieldPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    report.Fail("prefab ausente: " + path);
                    continue;
                }

                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null && !materials.ContainsKey(material))
                            materials[material] = Path.GetFileNameWithoutExtension(path);
            }

            var broken = new List<string>();
            var nonUrp = new List<string>();
            var normalAsColor = new List<string>();
            var normalMaps = 0;
            foreach (var pair in materials)
            {
                var material = pair.Key;
                var shader = material.shader;
                if (shader == null || shader.name.StartsWith("Hidden/InternalErrorShader", StringComparison.Ordinal) || !shader.isSupported)
                {
                    broken.Add(pair.Value + "/" + material.name);
                    continue;
                }

                if (!shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) && !shader.name.StartsWith("Shader Graphs/", StringComparison.Ordinal))
                    nonUrp.Add(pair.Value + "/" + material.name + " (" + shader.name + ")");

                if (!material.HasProperty("_BumpMap"))
                    continue;
                var normal = material.GetTexture("_BumpMap");
                if (normal == null)
                    continue;
                normalMaps++;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap && material.IsKeywordEnabled("_NORMALMAP"))
                    normalAsColor.Add(pair.Value + "/" + material.name + " (" + normal.name + ")");
            }

            report.Check(broken.Count == 0,
                materials.Count + " materiais de rover, Eagle e campo do escudo com shader válido",
                broken.Count + " material(is) com shader ausente ou não suportado: " + string.Join(", ", broken.Take(10)));
            if (nonUrp.Count > 0)
                report.Warn(nonUrp.Count + " material(is) fora do URP, confira névoa e sombra na Web: " + string.Join(", ", nonUrp.Take(10)));
            else
                report.Pass("todos os materiais usam shaders do URP (alcançados pela névoa e pelas sombras)");
            if (normalAsColor.Count > 0)
                report.Warn(normalAsColor.Count + " normal(is) ligada(s) a textura que não está importada como Normal map: " + string.Join(", ", normalAsColor.Take(10)) + ". Se o glTF exige normal RGB, confirme no navegador (Docs/04_WEB_AND_QA.md)");
            else
                report.Info(normalMaps + " material(is) com normal map; nenhum lido como cor pelo importador");

            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                report.Fail("o pipeline ativo não é o URP");
                return;
            }

            var settings = new SerializedObject(pipeline);
            var shadows = settings.FindProperty("m_MainLightShadowsSupported");
            report.Check(shadows != null && shadows.boolValue, "sombra da luz principal ligada no pipeline ativo (" + pipeline.name + ")", "sombra da luz principal desligada no pipeline ativo (" + pipeline.name + ")");
        }

        // ---------- 6. Perfis leve e equilibrado ----------

        static void AuditQualityProfiles(Report report)
        {
            var profiles = new[]
            {
                new KeyValuePair<string, string>("leve", LightProfilePath),
                new KeyValuePair<string, string>("equilibrado", ProjectSetupSpec.PipelineAssetPath),
            };

            foreach (var profile in profiles)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(profile.Value);
                if (asset == null)
                {
                    report.Fail("perfil " + profile.Key + " ausente em " + profile.Value);
                    continue;
                }

                var serialized = new SerializedObject(asset);
                var differences = new List<string>();
                foreach (var setting in ProjectSetupSpec.PipelineSettings)
                {
                    var property = serialized.FindProperty(setting.PropertyPath);
                    if (property == null)
                        continue;
                    string actual;
                    switch (setting.Kind)
                    {
                        case ProjectSetupSpec.SettingKind.Bool:
                            actual = property.boolValue.ToString();
                            break;
                        case ProjectSetupSpec.SettingKind.Int:
                            actual = property.intValue.ToString(CultureInfo.InvariantCulture);
                            break;
                        default:
                            actual = property.floatValue.ToString(CultureInfo.InvariantCulture);
                            break;
                    }

                    if (actual != setting.ExpectedText)
                        differences.Add(setting.PropertyPath + "=" + actual + " (base " + setting.ExpectedText + ")");
                }

                if (differences.Count == 0)
                    report.Pass("perfil " + profile.Key + " (" + asset.name + "): igual à linha de base do URP");
                else
                    report.Warn("perfil " + profile.Key + " (" + asset.name + ") difere da linha de base: " + string.Join(", ", differences));

                AuditProfileFeatures(report, profile.Key, serialized);
                AuditProfileVolume(report, profile.Key, serialized);
            }

            report.Info("mancha branca e ciência legível nos dois perfis só se confirmam nas capturas do checklist, no Editor e no navegador");
        }

        static void AuditProfileFeatures(Report report, string label, SerializedObject asset)
        {
            var list = asset.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray)
                return;

            var heavy = new List<string>();
            for (var i = 0; i < list.arraySize; i++)
            {
                var data = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (data == null)
                    continue;
                var features = new SerializedObject(data).FindProperty("m_RendererFeatures");
                if (features == null || !features.isArray)
                    continue;
                for (var f = 0; f < features.arraySize; f++)
                {
                    var feature = features.GetArrayElementAtIndex(f).objectReferenceValue;
                    if (feature == null)
                        continue;
                    var name = feature.name;
                    if (name.IndexOf("SSAO", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0)
                        heavy.Add(data.name + ": " + name);
                }
            }

            if (label == "leve")
                report.Check(heavy.Count == 0, "perfil leve sem SSAO nos renderers", "perfil leve com SSAO: " + string.Join(", ", heavy));
            else if (heavy.Count > 0)
                report.Info("perfil " + label + " usa " + string.Join(", ", heavy));
        }

        static void AuditProfileVolume(Report report, string label, SerializedObject asset)
        {
            var volumeProperty = asset.FindProperty("m_VolumeProfile");
            var volume = volumeProperty != null ? volumeProperty.objectReferenceValue as VolumeProfile : null;
            if (volume == null)
            {
                report.Info("perfil " + label + ": sem perfil de volume padrão");
                return;
            }

            var parts = new List<string>();
            if (volume.TryGet(out Bloom bloom) && bloom.active)
                parts.Add("Bloom intensidade " + F(bloom.intensity.value) + ", limiar " + F(bloom.threshold.value));
            if (volume.TryGet(out Tonemapping tonemapping) && tonemapping.active)
                parts.Add("Tonemapping " + tonemapping.mode.value);
            if (volume.TryGet(out DepthOfField depthOfField) && depthOfField.active)
                parts.Add("DepthOfField ligado");
            report.Info("perfil " + label + " (volume padrão " + volume.name + "): " + (parts.Count == 0 ? "sem Bloom, Tonemapping nem DepthOfField ativos" : string.Join("; ", parts)));
            if (label == "leve" && volume.TryGet(out DepthOfField dof) && dof.active)
                report.Fail("perfil leve com DepthOfField ligado (Docs/04_WEB_AND_QA.md: perfil leve sem SSAO/DOF)");
        }

        // ---------- 7. Rastro do rover da cena antiga ----------

        static void AuditTrailSource(Report report)
        {
            if (!File.Exists(OldRoverControllerPath))
            {
                report.Info("RoverController da cena antiga não está no projeto; rastro sem o que auditar");
                return;
            }

            var source = File.ReadAllText(OldRoverControllerPath);
            var teleport = source.IndexOf("public void Teleport", StringComparison.Ordinal);
            if (teleport < 0)
            {
                report.Warn("RoverController.Teleport não encontrado; confira a limpeza do rastro no teleporte à mão");
                return;
            }

            var body = source.Substring(teleport, Mathf.Min(700, source.Length - teleport));
            report.Check(body.Contains(".Clear()"), "Teleport limpa o rastro (TrailRenderer.Clear)", "Teleport não limpa o rastro: uma linha liga o ponto antigo ao novo");
            report.Check(source.Contains("TerrainFor(") && source.Contains("terrain.Raycast("),
                "o rastro posiciona nos colisores exclusivos de terreno (TerrainFor + MeshCollider.Raycast), não em Physics.Raycast geral",
                "o rastro não usa o colisor exclusivo do terreno: pode subir no rover ou no módulo");
            report.Info("o rover novo de Marte (MarsRoverController) ainda não tem TrailRenderer; esta checagem é só do código da cena antiga");
        }
    }
}
