using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-002 · Verificações do prefab da sonda, sem alterar nada: escala (refletor de 2,74 m), orçamento de triângulos,
    /// material único, nenhuma peça flutuando (cada peça encosta no corpo) e placa presa aos suportes da antena.
    /// </summary>
    public static class PioneerProbeChecks
    {
        public const float DishToleranceMeters = 0.02f;
        public const float ContactToleranceMeters = 0.05f;
        public const float PlaqueContactToleranceMeters = 0.03f;

        public sealed class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Lines = new List<string>();
            /// <summary>Triângulos do LOD0 (o LOD1 não entra na conta).</summary>
            public int Triangles;
            public int Lod1Triangles;
            public int Materials;
            public float DishDiameterMeters;
        }

        public static Report Run(PioneerProbeRig rig)
        {
            var report = new Report();
            var spin = rig.SpinRoot;
            if (spin == null)
            {
                report.Errors.Add("Spin_Root ausente.");
                return report;
            }

            var renderers = spin.GetComponentsInChildren<MeshRenderer>(true);
            var materials = new HashSet<Material>();
            Bounds? lod1Bounds = null;
            foreach (var renderer in renderers)
            {
                if (renderer.name == PioneerProbeBuilder.Lod1Name)
                {
                    var lodFilter = renderer.GetComponent<MeshFilter>();
                    if (lodFilter == null || lodFilter.sharedMesh == null)
                        report.Errors.Add("Lod1: renderer sem malha.");
                    else
                    {
                        report.Lod1Triangles = (int)(lodFilter.sharedMesh.GetIndexCount(0) / 3);
                        lod1Bounds = renderer.bounds;
                    }

                    foreach (var lodMaterial in renderer.sharedMaterials)
                    {
                        if (lodMaterial == null)
                            report.Errors.Add("Lod1: material ausente.");
                        else
                            materials.Add(lodMaterial);
                    }

                    continue;
                }

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                {
                    report.Errors.Add($"{renderer.name}: renderer sem malha.");
                    continue;
                }

                report.Triangles += (int)(filter.sharedMesh.GetIndexCount(0) / 3);
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                        report.Errors.Add($"{renderer.name}: material ausente.");
                    else
                    {
                        materials.Add(material);
                        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == null && material.name.Contains("Atlas"))
                            report.Errors.Add($"{material.name}: textura do atlas ausente.");
                    }
                }
            }

            report.Materials = materials.Count;
            if (report.Triangles > PioneerProbeBuilder.TriangleBudget)
                report.Errors.Add($"{report.Triangles} triângulos (meta do LOD0: {PioneerProbeBuilder.TriangleBudget}).");
            if (report.Materials > 3)
                report.Errors.Add($"{report.Materials} materiais (meta: até 3, IMPORT_RULES §5).");

            // LOD1 (IMPORT_RULES §5, "sim (planos distantes)"): LODGroup com LOD0 = peças e LOD1 = malha única, bem menor,
            // com a mesma silhueta.
            var group = rig.GetComponentInParent<LODGroup>(true);
            if (group == null)
                report.Errors.Add("LODGroup ausente na raiz da sonda (LOD1 exigido pela IMPORT_RULES §5).");
            else
            {
                var lods = group.GetLODs();
                if (lods.Length != 2 || lods[0].renderers.Length == 0 || lods[1].renderers.Length != 1 || lods[1].renderers[0] == null || lods[1].renderers[0].name != PioneerProbeBuilder.Lod1Name)
                    report.Errors.Add("LODGroup deve ter LOD0 = peças do modelo e LOD1 = a malha única 'Lod1'.");
                else if (lods[0].renderers.Any(r => r == null || r.name == PioneerProbeBuilder.Lod1Name))
                    report.Errors.Add("o LOD0 não pode conter renderer nulo nem o 'Lod1'.");
            }

            if (report.Lod1Triangles <= 0)
                report.Errors.Add("Lod1 sem triângulos.");
            else if (report.Lod1Triangles > report.Triangles * PioneerProbeBuilder.Lod1MaxTriangleFraction)
                report.Errors.Add($"Lod1 com {report.Lod1Triangles} triângulos; o limite é {PioneerProbeBuilder.Lod1MaxTriangleFraction:P0} do LOD0 ({report.Triangles}).");

            var bounds = new Dictionary<string, Bounds>();
            foreach (var name in new[] { "Bus", "Dish", "Feed", "RtgBoomA", "RtgBoomB", "MagnetometerBoom", "Plaque" })
            {
                var part = FindDeep(spin, name);
                var renderer = part != null ? part.GetComponent<MeshRenderer>() : null;
                if (renderer == null)
                {
                    report.Errors.Add($"peça '{name}' ausente.");
                    continue;
                }

                bounds[name] = renderer.bounds;
            }

            if (bounds.Count < 7)
                return report;

            // Silhueta do LOD1: a caixa do LOD1 não pode diferir da do LOD0 em mais de duas células da grade, em nenhum eixo.
            if (lod1Bounds.HasValue)
            {
                var whole = bounds.Values.Aggregate((a, b) => { a.Encapsulate(b); return a; });
                var tolerance = PioneerProbeBuilder.Lod1CellMeters * 2f;
                var delta = lod1Bounds.Value.size - whole.size;
                report.Lines.Add($"Lod1: {report.Lod1Triangles} triângulos ({(float)report.Lod1Triangles / Mathf.Max(1, report.Triangles):P0} do LOD0); caixa difere do LOD0 em {Mathf.Abs(delta.x):0.000}/{Mathf.Abs(delta.y):0.000}/{Mathf.Abs(delta.z):0.000} m");
                if (Mathf.Abs(delta.x) > tolerance || Mathf.Abs(delta.y) > tolerance || Mathf.Abs(delta.z) > tolerance)
                    report.Errors.Add($"a silhueta do Lod1 difere do LOD0 em {delta} m (tolerância {tolerance:0.00} m por eixo).");
            }

            // Escala: o refletor mede 2,74 m (P-S05) no eixo X.
            report.DishDiameterMeters = bounds["Dish"].size.x;
            if (Mathf.Abs(report.DishDiameterMeters - PioneerProbeBuilder.DishDiameterMeters) > DishToleranceMeters)
                report.Errors.Add($"refletor com {report.DishDiameterMeters:0.000} m; esperado {PioneerProbeBuilder.DishDiameterMeters:0.00} m (P-S05).");

            // Peças flutuando: distância REAL (ponto-aresta) entre os vértices da peça e a malha do corpo/refletor. A caixa de
            // limites do corpo envolve o modelo inteiro e não prova contato.
            var bodyEdges = EdgesOf(FindDeep(spin, "Bus"));
            var dishEdges = EdgesOf(FindDeep(spin, "Dish"));
            foreach (var name in new[] { "Dish", "Feed", "RtgBoomA", "RtgBoomB", "MagnetometerBoom" })
            {
                var points = VerticesOf(FindDeep(spin, name));
                var gap = Mathf.Min(MinDistance(points, bodyEdges), name == "Dish" ? float.MaxValue : MinDistance(points, dishEdges));
                report.Lines.Add($"{name}: distância até o corpo/refletor {gap:0.000} m");
                if (gap > ContactToleranceMeters)
                    report.Errors.Add($"peça '{name}' flutuando: {gap:0.000} m do corpo.");
            }

            // Placa: pelo menos dois cantos tocam os suportes da antena (malha do corpo), sem peça solta.
            var plaqueCorners = CornersOf(FindDeep(spin, "Plaque"));
            var touching = plaqueCorners.Count(c => MinDistance(new[] { c }, bodyEdges) <= PlaqueContactToleranceMeters);
            var plaqueGap = MinDistance(plaqueCorners, bodyEdges);
            report.Lines.Add($"Plaque: {touching} de {plaqueCorners.Length} cantos a ≤ {PlaqueContactToleranceMeters:0.000} m dos suportes; menor distância {plaqueGap:0.000} m");
            if (touching < 2)
                report.Errors.Add($"a placa não está presa aos suportes da antena (só {touching} canto(s) a ≤ {PlaqueContactToleranceMeters:0.000} m).");

            // Dobradiça dentro da caixa da própria haste (o pivô não pode estar solto no espaço).
            foreach (var hinge in rig.Hinges)
            {
                if (hinge?.pivot == null)
                    continue;

                var box = bounds[hinge.boom.ToString()];
                box.Expand(0.1f);
                if (!box.Contains(hinge.pivot.position))
                    report.Errors.Add($"dobradiça '{hinge.boom}' fora da própria haste.");
            }

            foreach (var name in PioneerProbeRig.RequiredSockets)
            {
                if (rig.TryGetSocket(name, out var socket))
                {
                    var all = bounds.Values.Aggregate((a, b) => { a.Encapsulate(b); return a; });
                    all.Expand(0.5f);
                    if (!all.Contains(socket.position))
                        report.Errors.Add($"socket '{name}' fora do modelo.");
                }
            }

            return report;
        }

        // ---------- Geometria ----------

        /// <summary>Vértices da malha da peça, em coordenadas de mundo.</summary>
        static Vector3[] VerticesOf(Transform part)
        {
            var filter = part != null ? part.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                return new Vector3[0];

            var vertices = filter.sharedMesh.vertices;
            var toWorld = part.localToWorldMatrix;
            for (var i = 0; i < vertices.Length; i++)
                vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
            return vertices;
        }

        /// <summary>Cantos da caixa da malha da peça (para a placa, que é uma caixa), em mundo.</summary>
        static Vector3[] CornersOf(Transform part)
        {
            var filter = part != null ? part.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                return new Vector3[0];

            var b = filter.sharedMesh.bounds;
            var corners = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                var local = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                corners[i] = part.TransformPoint(local);
            }

            return corners;
        }

        /// <summary>Arestas (pares de pontos em mundo) de todos os triângulos da malha: enxerga hastes finas.</summary>
        static (Vector3 a, Vector3 b)[] EdgesOf(Transform part)
        {
            var filter = part != null ? part.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null)
                return new (Vector3, Vector3)[0];

            var mesh = filter.sharedMesh;
            var vertices = VerticesOf(part);
            var triangles = mesh.triangles;
            var edges = new (Vector3, Vector3)[triangles.Length];
            for (var i = 0; i < triangles.Length; i += 3)
            {
                edges[i] = (vertices[triangles[i]], vertices[triangles[i + 1]]);
                edges[i + 1] = (vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
                edges[i + 2] = (vertices[triangles[i + 2]], vertices[triangles[i]]);
            }

            return edges;
        }

        static float MinDistance(IReadOnlyList<Vector3> points, (Vector3 a, Vector3 b)[] edges)
        {
            var best = float.MaxValue;
            foreach (var point in points)
            {
                foreach (var (a, b) in edges)
                {
                    var ab = b - a;
                    var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-12f));
                    var d = (a + ab * t - point).sqrMagnitude;
                    if (d < best)
                        best = d;
                }
            }

            return best == float.MaxValue ? float.MaxValue : Mathf.Sqrt(best);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            return null;
        }

        /// <summary>Distância entre duas caixas alinhadas aos eixos (0 se se tocam ou se sobrepõem).</summary>
        public static float Gap(Bounds a, Bounds b)
        {
            var dx = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
            var dy = Mathf.Max(0f, Mathf.Max(a.min.y - b.max.y, b.min.y - a.max.y));
            var dz = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static string Describe(Report report) =>
            string.Format(CultureInfo.InvariantCulture, "{0} triângulos · {1} material(is) · refletor {2:0.000} m", report.Triangles, report.Materials, report.DishDiameterMeters);
    }
}
