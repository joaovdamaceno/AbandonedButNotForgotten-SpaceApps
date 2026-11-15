using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-021 (R4): acabamento macro das amostras do Eagle. Reaproveita os materiais já gerados (<c>EagleOutcrop</c>,
    /// <c>EagleRocks</c>) e acrescenta, nos mapas de detalhe do URP/Lit, uma microvariação de cor e de relevo repetível
    /// de 256 px (sem textura 4K, sem material novo, sem mudar malhas, LODs, controles ou tempos). Idempotente: pode ser
    /// reaplicado depois de "MAR-012 Reconstruir Eagle". Escreve só em <c>MacroFinish/Generated</c> e nos dois materiais.
    /// </summary>
    public static class EagleMacroFinish
    {
        public const string FolderPath = EagleBuilder.ArtPath + "/MacroFinish";
        public const string GeneratedPath = FolderPath + "/Generated";
        public const string CaptureRoot = "QA/MAR-021";
        const int Size = 256;
        const float NormalStrength = 3.2f;

        struct Target
        {
            public string Material;
            public float Tiling;
            public float AlbedoScale;
            public float NormalScale;
        }

        // Tiling em voltas por unidade de UV. O afloramento tem ~0,7–1,1 UV/m: 4 voltas = um tijolo de detalhe a cada ~30 cm, grande o bastante para não virar grade.
        static readonly Target[] Targets =
        {
            new Target { Material = "EagleOutcrop", Tiling = 4f, AlbedoScale = 1f, NormalScale = 0.7f },
            new Target { Material = "EagleRocks", Tiling = 2.5f, AlbedoScale = 1f, NormalScale = 0.6f },
        };

        [MenuItem("Abandoned Space Apps/Marte/MAR-021 Aplicar acabamento macro")]
        public static void ApplyFromMenu() => Apply();

        [MenuItem("Abandoned Space Apps/Marte/MAR-021 Remover acabamento macro")]
        public static void RemoveFromMenu() => Remove();

        [MenuItem("Abandoned Space Apps/Marte/MAR-021 Capturar frames ANTES")]
        public static void CaptureBeforeFromMenu() => CaptureTo("antes");

        [MenuItem("Abandoned Space Apps/Marte/MAR-021 Capturar frames DEPOIS")]
        public static void CaptureAfterFromMenu() => CaptureTo("depois");

        public static void Apply()
        {
            Directory.CreateDirectory(GeneratedPath);
            var albedo = SaveTexture("EagleMacroDetail", true, false, AlbedoPixels());
            var normal = SaveTexture("EagleMacroNormal", false, true, NormalPixels());

            foreach (var target in Targets)
            {
                var material = LoadMaterial(target.Material);
                if (material == null)
                    continue;

                var tiling = new Vector2(target.Tiling, target.Tiling);
                material.SetTexture("_DetailAlbedoMap", albedo);
                material.SetTextureScale("_DetailAlbedoMap", tiling);
                material.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
                material.SetTexture("_DetailNormalMap", normal);
                material.SetFloat("_DetailAlbedoMapScale", target.AlbedoScale);
                material.SetFloat("_DetailNormalMapScale", target.NormalScale);
                material.EnableKeyword("_DETAIL_MULX2");
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MAR-021] acabamento macro aplicado (detalhe {Size}px; {FileSize("EagleMacroDetail")} + {FileSize("EagleMacroNormal")} bytes de PNG).");
        }

        public static void Remove()
        {
            foreach (var target in Targets)
            {
                var material = LoadMaterial(target.Material);
                if (material == null)
                    continue;

                material.SetTexture("_DetailAlbedoMap", null);
                material.SetTexture("_DetailNormalMap", null);
                material.DisableKeyword("_DETAIL_MULX2");
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[MAR-021] acabamento macro removido (materiais voltam ao estado do MAR-012).");
        }

        static void CaptureTo(string label)
        {
            var root = Path.GetDirectoryName(Application.dataPath);
            EagleCapture.Capture(Path.Combine(root, CaptureRoot, label));
        }

        static Material LoadMaterial(string name)
        {
            var path = EagleBuilder.GeneratedPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                Debug.LogWarning("[MAR-021] material ausente (rode MAR-012 Reconstruir Eagle antes): " + path);

            return material;
        }

        static long FileSize(string name)
        {
            var path = GeneratedPath + "/" + name + ".png";
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }

        // ---------- Ruído repetível ----------

        static float Noise(float x, float y) => Mathf.PerlinNoise(x + 17.3f, y + 91.7f);

        /// <summary>Ruído em (0–1)² que emenda nas bordas para qualquer frequência (mistura das quatro cópias deslocadas).</summary>
        static float Tile(float u, float v, float frequency)
        {
            float Sample(float a, float b) => Noise(a * frequency, b * frequency);
            return Sample(u, v) * (1f - u) * (1f - v) + Sample(u - 1f, v) * u * (1f - v) + Sample(u, v - 1f) * (1f - u) * v + Sample(u - 1f, v - 1f) * u * v;
        }

        // Altura do micro-relevo: grão em três escalas, laminação fina (sete voltas, emenda) e pequenas cavidades.
        static float Height(float u, float v)
        {
            var grain = 0.55f * Tile(u, v, 14f) + 0.30f * Tile(u, v, 33f) + 0.15f * Tile(u, v, 61f);
            var bedding = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 7f + 2.5f * Tile(u, v, 3f));
            var pit = Mathf.SmoothStep(0.74f, 0.9f, Tile(u, v, 11f));
            return grain + 0.06f * bedding - 0.18f * pit;
        }

        // Cinza médio (MULX2 deixa a cor neutra) com variação de poucos por cento e um leve desvio quente/frio por mancha.
        static Color[] AlbedoPixels()
        {
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var u = (x + 0.5f) / Size;
                    var v = (y + 0.5f) / Size;
                    var a = Tile(u, v, 10f) - 0.5f;
                    var b = Tile(u, v, 27f) - 0.5f;
                    var c = Tile(u, v, 58f) - 0.5f;
                    var gray = 0.605f + 0.14f * a + 0.09f * b + 0.05f * c;
                    var tint = Tile(u, v, 5f) - 0.5f;
                    pixels[y * Size + x] = new Color(gray + 0.03f * tint, gray, gray - 0.04f * tint, 1f);
                }
            }

            return pixels;
        }

        static Color[] NormalPixels()
        {
            var height = new float[Size * Size];
            for (var y = 0; y < Size; y++)
                for (var x = 0; x < Size; x++)
                    height[y * Size + x] = Height((x + 0.5f) / Size, (y + 0.5f) / Size);

            float At(int x, int y) => height[((y % Size + Size) % Size) * Size + ((x % Size + Size) % Size)];
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var dx = (At(x + 1, y) - At(x - 1, y)) * 0.5f;
                    var dy = (At(x, y + 1) - At(x, y - 1)) * 0.5f;
                    var n = new Vector3(-dx * NormalStrength, -dy * NormalStrength, 1f).normalized;
                    pixels[y * Size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }

            return pixels;
        }

        static Texture2D SaveTexture(string name, bool srgb, bool normalMap, Color[] pixels)
        {
            var path = GeneratedPath + "/" + name + ".png";
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, !srgb);
            texture.SetPixels(pixels);
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            // Regrava só quando o conteúdo muda: o PNG existente mantém o GUID e não gera diferença à toa.
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var wanted = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != wanted || importer.sRGBTexture != srgb || importer.maxTextureSize != Size || importer.wrapMode != TextureWrapMode.Repeat)
                {
                    importer.textureType = wanted;
                    importer.sRGBTexture = srgb;
                    importer.maxTextureSize = Size;
                    importer.mipmapEnabled = true;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
