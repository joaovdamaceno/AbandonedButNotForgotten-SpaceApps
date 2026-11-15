using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// Orçamento Web dos terrenos importados (MAR-012/MAR-020): poucas rochas e só perto da rota, LOD que corta cedo, texturas de até 1024 px e
    /// materiais URP/Lit (o shader próprio é caro e não entra). Conta os triângulos que sobram e avisa se passarem do limite.
    /// </summary>
    public static class MarsClassicBudget
    {
        public const int MaxVisibleTriangles = 250000;
        public const int MaxCloseTriangles = 400000;
        public const int MaxTextureSize = 1024;
        const float KeepRadiusMeters = 38f;
        const float FarRadiusMeters = 110f;

        static readonly string[] RockPrefixes = { "Geology", "Fracture", "Talus", "Small fragments", "Sediment", "Shield •" };

        /// <summary>Solta o prefab importado e tira as rochas distantes: ficam as de até 38 m do ponto de partida e, de 38 a 110 m, só 1 em cada 6; as pedras de pé embutidas ("Fracture") saem todas.</summary>
        public static int Trim(GameObject mapRoot, Vector3 center)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(mapRoot))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetNearestPrefabInstanceRoot(mapRoot), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var removed = 0;
            var farIndex = 0;
            foreach (var t in mapRoot.GetComponentsInChildren<Transform>(true).Where(t => t != mapRoot.transform && IsRock(t.name)).ToList())
            {
                if (t == null)
                    continue;

                var renderers = t.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    continue;

                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);

                var flat = new Vector2(bounds.center.x - center.x, bounds.center.z - center.z).magnitude;
                // As rochas escaneadas ("scan") são as mais pesadas: ficam só as de até 14 m.
                var keep = t.name.Contains("scan") ? 14f : KeepRadiusMeters;
                var drop = t.name.StartsWith("Fracture", System.StringComparison.Ordinal) || flat > FarRadiusMeters || (flat > keep && (t.name.Contains("scan") || farIndex++ % 6 != 0));
                if (drop)
                {
                    Object.DestroyImmediate(t.gameObject);
                    removed++;
                }
            }

            // LOD que corta cedo: o último nível some a partir de 3 % da tela.
            foreach (var group in mapRoot.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                if (lods.Length == 0)
                    continue;

                lods[lods.Length - 1].screenRelativeTransitionHeight = Mathf.Max(lods[lods.Length - 1].screenRelativeTransitionHeight, 0.03f);
                group.SetLODs(lods);
            }

            return removed;
        }

        static bool IsRock(string name) => RockPrefixes.Any(p => name.StartsWith(p, System.StringComparison.Ordinal));

        /// <summary>Troca o shader próprio por URP/Lit, mantendo mapa base, normal e cor, e limita as texturas a 1024 px.</summary>
        public static void ConvertMaterialsAndTextures(GameObject mapRoot)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var renderer in mapRoot.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null || !material.shader.name.Contains("Mars Surface"))
                        continue;

                    var baseMap = material.GetTexture("_BaseMap");
                    var normal = material.HasProperty("_NormalMap") ? material.GetTexture("_NormalMap") : null;
                    var tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                    var smooth = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0.12f;
                    var scale = material.HasProperty("_Scale") ? Mathf.Max(0.01f, material.GetFloat("_Scale")) : 0.35f;
                    material.shader = lit;
                    material.SetTexture("_BaseMap", baseMap);
                    material.SetColor("_BaseColor", tint);
                    material.SetFloat("_Smoothness", smooth);
                    material.SetFloat("_Metallic", 0f);
                    material.SetTextureScale("_BaseMap", new Vector2(scale, scale));
                    if (normal != null)
                    {
                        material.SetTexture("_BumpMap", normal);
                        material.SetTextureScale("_BumpMap", new Vector2(scale, scale));
                        material.EnableKeyword("_NORMALMAP");
                    }

                    EditorUtility.SetDirty(material);
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { MarsClassicMapBuilder.ClassicRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.maxTextureSize > MaxTextureSize)
                {
                    importer.maxTextureSize = MaxTextureSize;
                    importer.SaveAndReimport();
                }
            }
        }

        public static int VisibleTriangles(GameObject root)
        {
            long total = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var group = filter.GetComponentInParent<LODGroup>();
                if (filter.sharedMesh == null || (group != null && filter.GetComponent<Renderer>() != group.GetLODs()[0].renderers.FirstOrDefault()))
                    continue;

                total += filter.sharedMesh.triangles.Length / 3;
            }

            return (int)total;
        }

        public static string Report(string name, GameObject root)
        {
            var triangles = VisibleTriangles(root);
            var sb = new StringBuilder($"{name}: {triangles / 1000} mil triângulos");
            sb.Append(triangles > MaxCloseTriangles ? $" (ACIMA do limite de perto, {MaxCloseTriangles / 1000} mil: reduza mais)" : triangles > MaxVisibleTriangles ? $" (acima dos {MaxVisibleTriangles / 1000} mil visíveis, dentro do limite de perto de {MaxCloseTriangles / 1000} mil)" : " (dentro do orçamento)");
            var top = root.GetComponentsInChildren<MeshFilter>(false)
                .Where(f => f.sharedMesh != null)
                .GroupBy(f => f.transform.parent != null && f.transform.parent != root.transform && f.transform.parent.parent == root.transform ? f.name : (f.transform.parent != null ? f.transform.parent.name : f.name))
                .Select(g => (name: g.Key, tris: g.Sum(f => f.sharedMesh.triangles.Length / 3), count: g.Count()))
                .OrderByDescending(x => x.tris).Take(8);
            foreach (var (n, t, c) in top)
                sb.Append($" | {n}: {t / 1000} mil ({c})");
            return sb.ToString();
        }
    }
}
