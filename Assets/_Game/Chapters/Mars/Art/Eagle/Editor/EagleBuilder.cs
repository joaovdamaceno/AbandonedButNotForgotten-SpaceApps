using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using G = Abandoned.SpaceApps.Chapters.Mars.EditorTools.EagleGeometry;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-012 (R4): monta a cratera Eagle por prefabs, reconstruível sem perder GUIDs. Gera <c>EagleEnvironment</c>,
    /// <c>BlueberriesVisual</c> e <c>McKittrickVisual</c> e uma cena de prévia própria. Não toca em scripts de interação
    /// (MAR-003/004/005), no pouso (MAR-014) nem no Boot/Dossier/BuildSettings.
    /// </summary>
    public sealed class EagleBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string ArtPath = MarsPath + "/Art/Eagle";
        public const string GeneratedPath = ArtPath + "/Generated";
        public const string EnvironmentPrefabPath = MarsPath + "/Prefabs/EagleEnvironment.prefab";
        public const string BlueberriesPrefabPath = MarsPath + "/Prefabs/BlueberriesVisual.prefab";
        public const string McKittrickPrefabPath = MarsPath + "/Prefabs/McKittrickVisual.prefab";
        public const string PreviewScenePath = ArtPath + "/Preview/EaglePreview.unity";

        public override string Id => "mars.eagle";
        public override string Owner => "R4 · MAR-012";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-012] Eagle";

        // Escreve só em Art/Eagle e nos três prefabs nomeados na tarefa.
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-012 Reconstruir Eagle")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new EagleBuilder());
            if (!result.Succeeded)
                throw new InvalidOperationException(result.ToString());

            Debug.Log(result.ToString());
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildFromMenu();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public override void Build(SceneBuildContext context)
        {
            var soilNearTexture = Texture("SoilNearColor", 1024, true, SoilNearPixel, TextureWrapMode.Clamp);
            var soilFarTexture = Texture("SoilFarColor", 256, true, SoilFarPixel, TextureWrapMode.Clamp);
            var detailTexture = Texture("SoilDetail", 256, true, SoilDetailPixel);
            var strataTexture = Texture("OutcropStrata", 512, true, StrataPixel);
            var rockTexture = Texture("EagleRockColor", 128, true, RockPixel);

            var soilNear = context.Material(GeneratedPath + "/EagleSoilNear.mat", "Universal Render Pipeline/Lit");
            Configure(soilNear, new Color(1f, 1f, 1f), 0f, 0.05f, soilNearTexture);
            soilNear.SetTexture("_DetailAlbedoMap", detailTexture);
            soilNear.SetTextureScale("_DetailAlbedoMap", new Vector2(35f, 35f));
            soilNear.SetFloat("_DetailAlbedoMapScale", 1f);
            soilNear.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(soilNear);

            var soilFar = context.Material(GeneratedPath + "/EagleSoilFar.mat", "Universal Render Pipeline/Lit");
            Configure(soilFar, Color.white, 0f, 0.05f, soilFarTexture);
            var outcrop = context.Material(GeneratedPath + "/EagleOutcrop.mat", "Universal Render Pipeline/Lit");
            Configure(outcrop, Color.white, 0f, 0.12f, strataTexture);
            var rocks = context.Material(GeneratedPath + "/EagleRocks.mat", "Universal Render Pipeline/Lit");
            Configure(rocks, Color.white, 0f, 0.08f, rockTexture);
            var berries = context.Material(GeneratedPath + "/EagleBerries.mat", "Universal Render Pipeline/Lit");
            Configure(berries, new Color(0.17f, 0.145f, 0.14f), 0.3f, 0.5f, null); // cinza-escuro, nada de azul saturado (01_MARS_SOURCES)

            var blueberries = context.Prefab(BlueberriesPrefabPath, root => BuildBlueberries(root, berries));
            var mcKittrick = context.Prefab(McKittrickPrefabPath, root => BuildMcKittrick(root, outcrop));
            var environment = context.Prefab(EnvironmentPrefabPath, root => BuildEnvironment(root, soilNear, soilFar, outcrop, rocks, blueberries, mcKittrick));

            var instance = context.PrefabInstance(context.Root.transform, environment, "EagleEnvironment");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            BuildPreviewRig(context, instance.GetComponent<EagleAnchors>());
            AssetDatabase.SaveAssets();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var anchors = context.Root.GetComponentInChildren<EagleAnchors>();
            if (anchors == null)
                errors.Add("EagleAnchors ausente na prévia.");
            else
                errors.AddRange(anchors.ValidateSetup());
        }

        // ---------- Materiais e texturas ----------

        static void Configure(Material m, Color color, float metallic, float smoothness, Texture2D map)
        {
            m.SetColor("_BaseColor", color);
            if (map != null)
                m.SetTexture("_BaseMap", map);

            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        static Texture2D Texture(string name, int size, bool srgb, Func<float, float, Color> pixel, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var path = GeneratedPath + "/" + name + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);

            texture.SetPixels(pixels);
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            // Regrava só quando o conteúdo muda (o PNG existente mantém o GUID e não gera diferença à toa).
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.sRGBTexture != srgb || importer.maxTextureSize != size || !importer.mipmapEnabled || importer.wrapMode != wrap))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = size;
                importer.mipmapEnabled = true;
                importer.wrapMode = wrap;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float N(float x, float z) => G.PerlinNoise(x, z);

        // Centro do trecho de solo escuro (esferas soltas), em metros.
        static readonly Vector2 DarkSoilCenter = G.BlueberriesToe - G.BlueberriesLedge().Normal * 0.55f;

        /// <summary>Ruído repetível em (0–1)²: mistura as quatro cópias deslocadas, para a textura emendar sem costura.</summary>
        static float TilingNoise(float u, float v, float frequency)
        {
            float Sample(float a, float b) => N(a * frequency, b * frequency);
            return Sample(u, v) * (1f - u) * (1f - v) + Sample(u - 1f, v) * u * (1f - v) + Sample(u, v - 1f) * (1f - u) * v + Sample(u - 1f, v - 1f) * u * v;
        }

        // Solo de Eagle: areia basáltica escura e avermelhada, vales dos ripples mais escuros, manchas em quatro escalas
        // e um trecho mais escuro (esferas soltas) junto ao pé do afloramento. 1024 px sobre 32 m (3 cm/px).
        static Color SoilNearPixel(float u, float v)
        {
            var x = (u - 0.5f) * G.NearHalfSize * 2f;
            var z = (v - 0.5f) * G.NearHalfSize * 2f;
            var sand = Color.Lerp(new Color(0.27f, 0.20f, 0.17f), new Color(0.41f, 0.30f, 0.23f), N(x * 0.4f, z * 0.4f));
            var ripple = Mathf.Sin(G.RipplePhase(x, z)) * G.RippleStrength(x, z);
            sand *= 1f + 0.12f * ripple;
            sand *= 0.92f + 0.10f * N(x * 2.3f + 5f, z * 2.3f + 9f);
            sand *= 0.95f + 0.08f * N(x * 9f + 31f, z * 9f + 3f);
            sand *= 0.96f + 0.07f * N(x * 35f, z * 35f + 12f);

            var r = Mathf.Sqrt(x * x + z * z);
            sand *= 1f + 0.06f * Mathf.Clamp01(1f - r / G.FloorRadiusMeters);

            var dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.2f + 0.3f * N(x * 3f, z * 3f), 0.3f, Vector2.Distance(new Vector2(x, z), DarkSoilCenter)));
            sand = Color.Lerp(sand, new Color(0.15f, 0.125f, 0.12f), dark * 0.7f);
            sand.a = 1f;
            return sand;
        }

        static Color SoilFarPixel(float u, float v)
        {
            var x = (u - 0.5f) * G.NearHalfSize * 6f;
            var z = (v - 0.5f) * G.NearHalfSize * 6f;
            var sand = Color.Lerp(new Color(0.30f, 0.22f, 0.18f), new Color(0.39f, 0.28f, 0.21f), N(x * 0.06f, z * 0.06f));
            sand *= 0.93f + 0.1f * N(x * 0.5f + 3f, z * 0.5f + 7f);
            sand.a = 1f;
            return sand;
        }

        // Detalhe (1 volta a cada ~0,9 m, repetível): cinza médio, de modo que MULX2 deixa a cor neutra.
        static Color SoilDetailPixel(float u, float v)
        {
            var value = 0.5f + 0.2f * (TilingNoise(u, v, 6f) - 0.5f) + 0.12f * (TilingNoise(u, v, 19f) - 0.5f) + 0.06f * (TilingNoise(u, v, 47f) - 0.5f);
            return new Color(value, value, value, 1f);
        }

        // Estratos: areia clara, laminação fina e paralela (faixas em V, número inteiro de voltas para emendar) e cavidades em disco.
        static Color StrataPixel(float u, float v)
        {
            var baseColor = Color.Lerp(new Color(0.55f, 0.43f, 0.34f), new Color(0.72f, 0.60f, 0.47f), TilingNoise(u, v, 3f));
            var lamination = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 23f + 3f * TilingNoise(u, v, 4f));
            var fine = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 61f + 5f * TilingNoise(u, v, 9f));
            var c = baseColor * (0.86f + 0.10f * lamination + 0.06f * fine);
            c *= 0.94f + 0.10f * TilingNoise(u, v, 24f);
            var vug = Mathf.SmoothStep(0.82f, 0.95f, TilingNoise(u, v, 17f));
            c = Color.Lerp(c, c * 0.55f, vug * 0.5f);
            return new Color(c.r, c.g, c.b, 1f);
        }

        static Color RockPixel(float u, float v)
        {
            var c = Color.Lerp(new Color(0.27f, 0.21f, 0.18f), new Color(0.42f, 0.32f, 0.26f), N(u * 8f, v * 8f));
            c *= 0.88f + 0.24f * N(u * 40f, v * 40f);
            return new Color(c.r, c.g, c.b, 1f);
        }

        // ---------- Malhas e hierarquia ----------

        static Mesh SaveMesh(string name, ShieldFieldGeometry.Draft draft)
        {
            var path = GeneratedPath + "/" + name + ".asset";
            Directory.CreateDirectory(GeneratedPath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            draft.Write(mesh);
            mesh.name = name;
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Transform Child(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null)
                return found;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static T Get<T>(GameObject go) where T : Component => go.GetComponent<T>() != null ? go.GetComponent<T>() : go.AddComponent<T>();

        static MeshRenderer Surface(Transform t, Mesh mesh, Material material, bool shadows = true)
        {
            Get<MeshFilter>(t.gameObject).sharedMesh = mesh;
            var renderer = Get<MeshRenderer>(t.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return renderer;
        }

        static void TrimChildren(Transform parent, HashSet<string> wanted)
        {
            // Reconstruir não pode deixar filhos de uma versão anterior da disposição.
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                if (!wanted.Contains(parent.GetChild(i).name))
                    Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        static void WithLods(Transform root, Mesh high, Mesh low, Material material, float highThreshold, float lowThreshold, bool collide)
        {
            var group = Get<LODGroup>(root.gameObject);
            var r0 = Surface(Child(root, "LOD0"), high, material);
            var r1 = Surface(Child(root, "LOD1"), low, material);
            group.SetLODs(new[] { new LOD(highThreshold, new Renderer[] { r0 }), new LOD(lowThreshold, new Renderer[] { r1 }) });
            group.RecalculateBounds();
            if (collide)
                Get<MeshCollider>(root.gameObject).sharedMesh = low;
        }

        static Matrix4x4 FrameOf(Vector3 position, Vector2 forwardXz) =>
            Matrix4x4.TRS(position, Quaternion.LookRotation(new Vector3(forwardXz.x, 0f, forwardXz.y), Vector3.up), Vector3.one);

        // ---------- BlueberriesVisual (M08–M09) ----------

        static void BuildBlueberries(GameObject root, Material berries)
        {
            var ledge = G.BlueberriesLedge();
            var frame = FrameOf(G.Ground(G.BlueberriesToe), ledge.Normal);
            var toLocal = frame.inverse;
            var rng = new System.Random(1612);

            // Esferas soltas: um aglomerado denso (a "tigela") e um espalhamento ao redor, sobre o solo escuro.
            var soilCenter = G.BlueberriesToe - ledge.Normal * 0.55f + ledge.Tangent * 0.1f;
            var loose = new ShieldFieldGeometry.Draft();
            var placed = new List<Vector2>();
            for (var attempt = 0; attempt < 6000 && placed.Count < G.LooseSphereCount; attempt++)
            {
                var clustered = placed.Count < G.LooseSphereCount * 0.4f;
                var radius = (clustered ? 0.22f : 0.8f) * Mathf.Sqrt((float)rng.NextDouble());
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var p = soilCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (placed.Any(o => Vector2.Distance(o, p) < 0.02f))
                    continue;

                placed.Add(p);
                var sphereRadius = Mathf.Lerp(G.SphereMinRadius, G.SphereMaxRadius, (float)rng.NextDouble());
                var world = new Vector3(p.x, G.Height(p) + sphereRadius * 0.65f, p.y);
                var near = Vector2.Distance(p, soilCenter) < 0.35f;
                G.AddSphere(loose, toLocal.MultiplyPoint3x4(world), sphereRadius, near ? 1 : 0, new Vector2(0.5f, 0.5f));
            }

            // Esferas presas: meia-esfera aflorando da face da primeira camada (z local = 0), em fileiras que seguem os estratos.
            var embedded = new ShieldFieldGeometry.Draft();
            var faceRows = new[] { 0.11f, 0.22f, 0.31f };
            var faceSpheres = new List<Vector2>();
            for (var attempt = 0; attempt < 6000 && faceSpheres.Count < G.EmbeddedSphereCount; attempt++)
            {
                var u = ((float)rng.NextDouble() * 2f - 1f) * 0.52f;
                var ground = G.Height(G.BlueberriesToe + ledge.Tangent * u);
                var top = G.TerraceTop(ledge, 0, u);
                float height;
                if (rng.NextDouble() < 0.6)
                    height = ground + faceRows[rng.Next(faceRows.Length)] + ((float)rng.NextDouble() - 0.5f) * 0.024f;
                else
                    height = Mathf.Lerp(ground + 0.05f, top - 0.04f, (float)rng.NextDouble());

                height = Mathf.Min(height, top - 0.03f);
                var p = new Vector2(u, height);
                if (faceSpheres.Any(o => Vector2.Distance(o, p) < 0.022f))
                    continue;

                faceSpheres.Add(p);
                var sphereRadius = Mathf.Lerp(G.SphereMinRadius, G.SphereMaxRadius, (float)rng.NextDouble());
                var world = new Vector3(G.BlueberriesToe.x + ledge.Tangent.x * u, height, G.BlueberriesToe.y + ledge.Tangent.y * u);
                var local = toLocal.MultiplyPoint3x4(world);
                local.z = 0.001f;
                G.AddSphere(embedded, local, sphereRadius, 1, new Vector2(0.5f, 0.5f));
            }

            var group = Get<LODGroup>(root);
            var rLoose = Surface(Child(root.transform, "LooseSpheres"), SaveMesh("Blueberries_Loose", loose), berries, false);
            var rEmbedded = Surface(Child(root.transform, "EmbeddedSpheres"), SaveMesh("Blueberries_Embedded", embedded), berries, false);
            group.SetLODs(new[] { new LOD(0.01f, new Renderer[] { rLoose, rEmbedded }) });
            group.RecalculateBounds();

            var anchors = Child(root.transform, "Anchors");
            var prompt = Child(anchors, "PromptAnchor");
            prompt.localPosition = toLocal.MultiplyPoint3x4(G.Ground(G.BlueberriesToe - ledge.Normal * 0.15f) + Vector3.up * 0.1f);

            var station = Child(anchors, "StationAnchor");
            station.localPosition = toLocal.MultiplyPoint3x4(G.Ground(G.BlueberriesStation));
            station.localRotation = Quaternion.LookRotation(toLocal.MultiplyVector(new Vector3(G.BlueberriesToe.x - G.BlueberriesStation.x, 0f, G.BlueberriesToe.y - G.BlueberriesStation.y)), Vector3.up);

            var soilView = Child(anchors, "SoilView");
            var soilEye = G.Ground(soilCenter - ledge.Normal * 0.45f) + Vector3.up * 0.62f;
            var soilTarget = G.Ground(soilCenter) + Vector3.up * 0.01f;
            Aim(soilView, toLocal, soilEye, soilTarget);

            var rockView = Child(anchors, "RockView");
            var rockEye = G.Ground(G.BlueberriesToe - ledge.Normal * 0.8f) + Vector3.up * 0.32f;
            var rockTarget = G.Ground(G.BlueberriesToe) + Vector3.up * 0.2f;
            Aim(rockView, toLocal, rockEye, rockTarget);

            var component = Get<EagleTargetAnchors>(root);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("promptAnchor").objectReferenceValue = prompt;
            serialized.FindProperty("stationAnchor").objectReferenceValue = station;
            serialized.FindProperty("primaryView").objectReferenceValue = soilView;
            serialized.FindProperty("secondaryView").objectReferenceValue = rockView;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Aim(Transform t, Matrix4x4 toLocal, Vector3 worldEye, Vector3 worldTarget)
        {
            t.localPosition = toLocal.MultiplyPoint3x4(worldEye);
            t.localRotation = Quaternion.LookRotation(toLocal.MultiplyVector((worldTarget - worldEye).normalized), toLocal.MultiplyVector(Vector3.up));
        }

        // ---------- McKittrickVisual (M10–M14) ----------

        static void BuildMcKittrick(GameObject root, Material outcrop)
        {
            var normal = -G.McKittrickFaceDirection;
            var frame = FrameOf(G.Ground(G.McKittrick) + Vector3.up * G.McKittrickPatchLift, normal);
            var toLocal = frame.inverse;

            var high = SaveMesh("McKittrick_LOD0", G.McKittrickBlock(30, 20));
            var low = SaveMesh("McKittrick_LOD1", G.McKittrickBlock(10, 7));
            WithLods(root.transform, high, low, outcrop, 0.2f, 0.02f, true);

            var anchors = Child(root.transform, "Anchors");
            var mount = Child(anchors, "RatSurfaceMount");
            mount.localPosition = Vector3.zero;
            mount.localRotation = Quaternion.FromToRotation(Vector3.up, G.McKittrickPatchNormal);

            var prompt = Child(anchors, "PromptAnchor");
            prompt.localPosition = G.McKittrickPatchNormal * 0.32f;

            var station = Child(anchors, "StationAnchor");
            station.localPosition = toLocal.MultiplyPoint3x4(G.Ground(G.McKittrickStation));
            station.localRotation = Quaternion.LookRotation(toLocal.MultiplyVector(new Vector3(G.McKittrick.x - G.McKittrickStation.x, 0f, G.McKittrick.y - G.McKittrickStation.y)), Vector3.up);

            var macro = Child(anchors, "MacroView");
            var eye = G.McKittrickPatchNormal * 0.48f + new Vector3(0f, 0f, -0.12f);
            macro.localPosition = eye;
            macro.localRotation = Quaternion.LookRotation(-eye.normalized, Vector3.up);

            var component = Get<EagleTargetAnchors>(root);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("promptAnchor").objectReferenceValue = prompt;
            serialized.FindProperty("stationAnchor").objectReferenceValue = station;
            serialized.FindProperty("primaryView").objectReferenceValue = macro;
            serialized.FindProperty("ratSurfaceMount").objectReferenceValue = mount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- EagleEnvironment ----------

        static void BuildEnvironment(GameObject root, Material soilNear, Material soilFar, Material outcrop, Material rocks, GameObject blueberriesPrefab, GameObject mcKittrickPrefab)
        {
            BuildTerrain(Child(root.transform, "Terrain"), soilNear, soilFar);

            var outcrops = Child(root.transform, "Outcrops");
            WithLods(Child(outcrops, "Blueberries_Ledge"), SaveMesh("Ledge_Blueberries_LOD0", G.Terraces(G.BlueberriesLedge(), 0.25f)), SaveMesh("Ledge_Blueberries_LOD1", G.Terraces(G.BlueberriesLedge(), 0.6f)), outcrop, 0.18f, 0.02f, true);
            WithLods(Child(outcrops, "McKittrick_Ledge"), SaveMesh("Ledge_McKittrick_LOD0", G.Terraces(G.McKittrickLedge(), 0.25f)), SaveMesh("Ledge_McKittrick_LOD1", G.Terraces(G.McKittrickLedge(), 0.6f)), outcrop, 0.18f, 0.02f, true);
            WithLods(Child(outcrops, "Spur"), SaveMesh("Spur_LOD0", G.Spur(28)), SaveMesh("Spur_LOD1", G.Spur(12)), outcrop, 0.18f, 0.02f, true);

            BuildRocks(Child(root.transform, "Rocks"), rocks);

            var blueberries = Instance(root, blueberriesPrefab, "BlueberriesVisual");
            var ledge = G.BlueberriesLedge();
            blueberries.localPosition = G.Ground(G.BlueberriesToe);
            blueberries.localRotation = Quaternion.LookRotation(new Vector3(ledge.Normal.x, 0f, ledge.Normal.y), Vector3.up);
            blueberries.localScale = Vector3.one;

            var mcKittrick = Instance(root, mcKittrickPrefab, "McKittrickVisual");
            var face = -G.McKittrickFaceDirection;
            mcKittrick.localPosition = G.Ground(G.McKittrick) + Vector3.up * G.McKittrickPatchLift;
            mcKittrick.localRotation = Quaternion.LookRotation(new Vector3(face.x, 0f, face.y), Vector3.up);
            mcKittrick.localScale = Vector3.one;

            BuildAnchors(root, blueberries, mcKittrick);
        }

        static Transform Instance(GameObject root, GameObject prefab, string name)
        {
            var existing = root.transform.Find(name);
            if (existing != null)
                return existing;

            var created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
            created.name = name;
            created.transform.SetParent(root.transform, false);
            return created.transform;
        }

        static void BuildTerrain(Transform terrain, Material near, Material far)
        {
            var size = G.NearHalfSize * 2f;
            var cells = Mathf.RoundToInt(size / G.NearCellMeters);
            var nearMesh = SaveMesh("Terrain_Near", G.Terrain(-G.NearHalfSize, -G.NearHalfSize, size, cells, G.RingCoarseStep, -G.NearHalfSize, -G.NearHalfSize, size));
            var nearObject = Child(terrain, "Near_32m");
            Surface(nearObject, nearMesh, near, true);
            Get<MeshCollider>(nearObject.gameObject).sharedMesh = nearMesh;

            var wanted = new HashSet<string> { "Near_32m" };
            var ringCells = Mathf.RoundToInt(size / G.RingCoarseStep);
            for (var iz = -1; iz <= 1; iz++)
            {
                for (var ix = -1; ix <= 1; ix++)
                {
                    if (ix == 0 && iz == 0)
                        continue;

                    var name = $"Ring_{ix + 1}_{iz + 1}";
                    wanted.Add(name);
                    var mesh = SaveMesh("Terrain_" + name, G.Terrain(ix * size - size * 0.5f, iz * size - size * 0.5f, size, ringCells, 0f, -size * 1.5f, -size * 1.5f, size * 3f));
                    Surface(Child(terrain, name), mesh, far, false);
                }
            }

            TrimChildren(terrain, wanted);
        }

        static void BuildRocks(Transform parent, Material material)
        {
            var high = new Mesh[G.RockVariants];
            var low = new Mesh[G.RockVariants];
            for (var i = 0; i < G.RockVariants; i++)
            {
                high[i] = SaveMesh("EagleRock_" + i + "_LOD0", G.Rock(60 + i, 2));
                low[i] = SaveMesh("EagleRock_" + i + "_LOD1", G.Rock(60 + i, 1));
            }

            var placements = G.Rocks();
            var wanted = new HashSet<string>();
            for (var i = 0; i < placements.Count; i++)
            {
                var rock = placements[i];
                var name = "Rock_" + i.ToString("00");
                wanted.Add(name);
                var t = Child(parent, name);
                t.localPosition = rock.Position;
                t.localRotation = rock.Rotation;
                t.localScale = new Vector3(rock.Size * 2f, rock.Size * (1.4f + 0.8f * G.PerlinNoise(i * 1.7f, 3.1f)), rock.Size * 2f);
                if (rock.Large)
                    WithLods(t, high[rock.Variant], low[rock.Variant], material, 0.1f, 0.015f, true);
                else
                    Surface(t, low[rock.Variant], material, false);
            }

            TrimChildren(parent, wanted);
        }

        static void BuildAnchors(GameObject root, Transform blueberries, Transform mcKittrick)
        {
            var anchors = Child(root.transform, "Anchors");
            var start = Child(anchors, "RouteStart");
            var plan = G.RoutePlan;
            start.localPosition = G.Ground(plan[0]);
            start.localRotation = Quaternion.LookRotation(new Vector3(plan[1].x - plan[0].x, 0f, plan[1].y - plan[0].y), Vector3.up);

            var route = Child(root.transform, "Route");
            var samples = new Transform[plan.Count];
            var wanted = new HashSet<string>();
            for (var i = 0; i < plan.Count; i++)
            {
                var name = "Sample_" + i.ToString("000");
                wanted.Add(name);
                var t = Child(route, name);
                t.localPosition = G.Ground(plan[i]);
                var next = plan[Mathf.Min(i + 1, plan.Count - 1)];
                var previous = plan[Mathf.Max(i - 1, 0)];
                var forward = next - previous;
                if (forward.sqrMagnitude > 1e-6f)
                    t.localRotation = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y), Vector3.up);

                samples[i] = t;
            }

            TrimChildren(route, wanted);

            var overview = Child(anchors, "OverviewCamera");
            var eye = G.Ground(new Vector2(-6.0f, -8.6f)) + Vector3.up * 3.6f;
            var target = Child(anchors, "OverviewTarget");
            target.localPosition = G.Ground(G.BlueberriesToe) + Vector3.up * 0.5f;
            overview.localPosition = eye;
            overview.localRotation = Quaternion.LookRotation(target.localPosition - eye, Vector3.up);

            var component = Get<EagleAnchors>(root);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("routeStart").objectReferenceValue = start;
            var array = serialized.FindProperty("routeSamples");
            array.arraySize = samples.Length;
            for (var i = 0; i < samples.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = samples[i];

            serialized.FindProperty("blueberriesStationIndex").intValue = G.BlueberriesStationIndex;
            serialized.FindProperty("mcKittrickStationIndex").intValue = G.McKittrickStationIndex;
            serialized.FindProperty("corridorWidthMeters").floatValue = G.CorridorWidthMeters;
            serialized.FindProperty("blueberries").objectReferenceValue = blueberries.GetComponent<EagleTargetAnchors>();
            serialized.FindProperty("mcKittrick").objectReferenceValue = mcKittrick.GetComponent<EagleTargetAnchors>();
            serialized.FindProperty("overviewCamera").objectReferenceValue = overview;
            serialized.FindProperty("overviewTarget").objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Prévia ----------

        static void BuildPreviewRig(SceneBuildContext context, EagleAnchors anchors)
        {
            var sunObject = context.Child(context.Root.transform, "Sun · single shadow light");
            sunObject.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.3f;

            var cameraObject = context.Child(context.Root.transform, "Preview Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.66f, 0.5f, 0.4f);
            camera.fieldOfView = 46f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 300f;
            var data = context.Component<UniversalAdditionalCameraData>(cameraObject);
            data.renderPostProcessing = false;
            cameraObject.transform.SetPositionAndRotation(anchors.OverviewCamera.position, anchors.OverviewCamera.rotation);

            RenderSettings.skybox = null;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.5f, 0.4f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 120f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.27f, 0.23f);
        }
    }
}
