using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using G = Abandoned.SpaceApps.Chapters.Mars.EditorTools.LateMarsGeometry;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-016 (R4): monta os palcos de Victoria (2007) e do Perseverance Valley (2018) por prefabs, o perfil atmosférico por
    /// evento e uma cena de prévia, reconstruindo sem perder GUIDs. Não toca em scripts de interação, Boot ou BuildSettings.
    /// </summary>
    public sealed class LateMarsBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string ArtPath = MarsPath + "/Art/LateMars";
        public const string GeneratedPath = ArtPath + "/Generated";
        public const string VictoriaPrefabPath = MarsPath + "/Prefabs/VictoriaStage.prefab";
        public const string ValleyPrefabPath = MarsPath + "/Prefabs/PerseveranceValleyStage.prefab";
        public const string ProfilePath = ArtPath + "/MarsStormProfile.asset";
        public const string PreviewScenePath = ArtPath + "/Preview/LateMarsPreview.unity";

        public static readonly Vector3 ValleyOffset = new Vector3(5000f, 0f, 0f);
        public static readonly Vector3 SunDirection = new Vector3(0.25f, 0.5f, 0.83f).normalized;

        public override string Id => "mars.lateMars";
        public override string Owner => "R4 · MAR-016";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-016] Late Mars";
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-016 Reconstruir Victoria e o vale")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new LateMarsBuilder());
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

        sealed class Materials
        {
            public Material victoriaGround, valleyGround, rimBand, rocks, sunDisc, dust;
        }

        public override void Build(SceneBuildContext context)
        {
            var victoriaTex = Texture("GroundVictoria", 512, true, true, VictoriaGroundPixel);
            var valleyTex = Texture("GroundValley", 512, true, true, ValleyGroundPixel);
            var rimTex = Texture("RimBand", 512, true, true, RimPixel);
            var rockTex = Texture("LateRocks", 128, true, true, RockPixel);
            var sunTex = Texture("SunDisc", 128, true, false, SunPixel);
            var dustTex = Texture("DustPuff", 64, true, false, DustPixel);

            var m = new Materials
            {
                victoriaGround = context.Material(GeneratedPath + "/VictoriaGround.mat", "Universal Render Pipeline/Lit"),
                valleyGround = context.Material(GeneratedPath + "/ValleyGround.mat", "Universal Render Pipeline/Lit"),
                rimBand = context.Material(GeneratedPath + "/RimBand.mat", "Universal Render Pipeline/Lit"),
                rocks = context.Material(GeneratedPath + "/LateRocks.mat", "Universal Render Pipeline/Lit"),
                sunDisc = context.Material(GeneratedPath + "/SunDisc.mat", "Universal Render Pipeline/Unlit"),
                dust = context.Material(GeneratedPath + "/DustPuff.mat", "Universal Render Pipeline/Particles/Unlit"),
            };
            Lit(m.victoriaGround, victoriaTex, 0.04f);
            Lit(m.valleyGround, valleyTex, 0.05f);
            Lit(m.rimBand, rimTex, 0.08f);
            Lit(m.rocks, rockTex, 0.1f);
            Transparent(m.sunDisc, sunTex, Color.white);
            Transparent(m.dust, dustTex, new Color(0.7f, 0.5f, 0.36f, 0.3f));

            var profile = context.Asset(ProfilePath, DefaultProfile);
            var victoria = context.Prefab(VictoriaPrefabPath, root => BuildStage(root, false, m, profile));
            var valley = context.Prefab(ValleyPrefabPath, root => BuildStage(root, true, m, profile));

            var victoriaInstance = context.PrefabInstance(context.Root.transform, victoria, "VictoriaStage");
            victoriaInstance.transform.localPosition = Vector3.zero;
            var valleyInstance = context.PrefabInstance(context.Root.transform, valley, "PerseveranceValleyStage");
            valleyInstance.transform.localPosition = ValleyOffset;

            BuildPreviewRig(context, victoriaInstance, valleyInstance);
            AssetDatabase.SaveAssets();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            foreach (var anchors in context.Root.GetComponentsInChildren<LateMarsStageAnchors>())
                errors.AddRange(anchors.ValidateSetup());

            foreach (var controller in context.Root.GetComponentsInChildren<MarsAtmosphereController>())
                errors.AddRange(controller.ValidateSetup());
        }

        // ---------- Perfil por evento ----------

        static MarsStormProfileData DefaultProfile()
        {
            var p = ScriptableObject.CreateInstance<MarsStormProfileData>();
            p.victoria = new AtmosphereEventProfile
            {
                id = MarsStormProfileData.Victoria2007, drivenByStorm = true, endsAtDust = 0.1f,
                clear = new AtmosphereState
                {
                    fogColor = new Color(0.66f, 0.5f, 0.4f), fogStartMeters = 140f, fogEndMeters = 2400f, sunIntensity = 1.5f, sunColor = new Color(1f, 0.9f, 0.78f),
                    ambient = new Color(0.34f, 0.27f, 0.23f), sky = new Color(0.66f, 0.5f, 0.4f), particlesPerSecond = 0f, sunDiscAlpha = 1f, shakeMeters = 0f,
                },
                dense = new AtmosphereState
                {
                    fogColor = new Color(0.36f, 0.25f, 0.18f), fogStartMeters = 4f, fogEndMeters = 95f, sunIntensity = 0.5f, sunColor = new Color(0.9f, 0.68f, 0.48f),
                    ambient = new Color(0.22f, 0.16f, 0.12f), sky = new Color(0.32f, 0.22f, 0.16f), particlesPerSecond = 120f, sunDiscAlpha = 0.3f, shakeMeters = 0f,
                },
            };
            p.valley = new AtmosphereEventProfile
            {
                id = MarsStormProfileData.Valley2018, drivenByStorm = false, endsAtDust = 1f,
                clear = new AtmosphereState
                {
                    fogColor = new Color(0.6f, 0.5f, 0.42f), fogStartMeters = 150f, fogEndMeters = 2600f, sunIntensity = 1.4f, sunColor = new Color(1f, 0.92f, 0.82f),
                    ambient = new Color(0.34f, 0.28f, 0.24f), sky = new Color(0.6f, 0.5f, 0.42f), particlesPerSecond = 0f, sunDiscAlpha = 1f, shakeMeters = 0f,
                },
                dense = new AtmosphereState
                {
                    fogColor = new Color(0.3f, 0.22f, 0.17f), fogStartMeters = 3f, fogEndMeters = 62f, sunIntensity = 0.32f, sunColor = new Color(0.85f, 0.62f, 0.42f),
                    ambient = new Color(0.2f, 0.15f, 0.12f), sky = new Color(0.27f, 0.19f, 0.14f), particlesPerSecond = 150f, sunDiscAlpha = 0.22f, shakeMeters = 0.012f,
                },
                keys = new List<DustKey>
                {
                    new DustKey { stepId = "M23", dust = 0.12f, seconds = 0f },
                    new DustKey { stepId = "M27", dust = 0.7f, seconds = 6f },
                    new DustKey { stepId = "M29", dust = 0.8f, seconds = 6f },
                    new DustKey { stepId = "M31", dust = 1f, seconds = 24f },
                },
            };
            p.name = "MarsStormProfile";
            return p;
        }

        // ---------- Materiais e texturas ----------

        static void Lit(Material m, Texture2D map, float smoothness)
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", map);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        static void Transparent(Material m, Texture2D map, Color color)
        {
            m.SetTexture("_BaseMap", map);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            // O próprio URP configura mistura, keywords, ZWrite e fila: setar as propriedades à mão deixa o canal alfa ignorado.
            UnityEditor.BaseShaderGUI.SetupMaterialBlendMode(m);
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
        }

        static Texture2D Texture(string name, int size, bool srgb, bool repeat, Func<float, float, Color> pixel)
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
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.sRGBTexture != srgb || importer.maxTextureSize != size || !importer.mipmapEnabled
                || importer.wrapMode != (repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp)))
            {
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = size;
                importer.mipmapEnabled = true;
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float T(float u, float v, float scale) => G.TileNoise(u, v, scale);

        // smoothstep(edge0, edge1, x) do HLSL. Mathf.SmoothStep(from, to, t) da Unity NÃO é isto: ela interpola entre from e to.
        static float Edge(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // Areia ocre-avermelhada de Meridiani com ripples tileáveis.
        static Color VictoriaGroundPixel(float u, float v)
        {
            var sand = Color.Lerp(new Color(0.53f, 0.37f, 0.26f), new Color(0.6f, 0.43f, 0.3f), T(u, v, 9f));
            sand *= 1f + 0.045f * Mathf.Sin((u * 10f + v * 4f + 1.4f * T(u, v, 8f)) * Mathf.PI * 2f);
            sand *= 0.95f + 0.1f * T(u, v, 26f);
            sand.a = 1f;
            return sand;
        }

        // Cascalho fino acinzentado do vale (referência: imagens Navcam de Perseverance Valley).
        static Color ValleyGroundPixel(float u, float v)
        {
            var g = Color.Lerp(new Color(0.46f, 0.38f, 0.3f), new Color(0.54f, 0.45f, 0.36f), T(u, v, 10f));
            g *= 0.93f + 0.14f * T(u, v, 36f);
            g = Color.Lerp(g, new Color(0.36f, 0.3f, 0.25f), Edge(0.65f, 0.9f, T(u, v, 18f)) * 0.3f);
            g.a = 1f;
            return g;
        }

        // Leito rochoso claro e estratificado da borda de Victoria.
        static Color RimPixel(float u, float v)
        {
            var layer = 0.5f + 0.5f * Mathf.Sin((v * 18f + 0.6f * T(u, v, 6f)) * Mathf.PI * 2f);
            var c = Color.Lerp(new Color(0.62f, 0.5f, 0.38f), new Color(0.78f, 0.66f, 0.5f), layer * 0.6f + 0.3f * T(u, v, 12f));
            c *= 0.92f + 0.14f * T(u, v, 30f);
            c.a = 1f;
            return c;
        }

        static Color RockPixel(float u, float v)
        {
            var c = Color.Lerp(new Color(0.3f, 0.22f, 0.17f), new Color(0.5f, 0.38f, 0.28f), T(u, v, 5f));
            c *= 0.88f + 0.24f * T(u, v, 18f);
            c.a = 1f;
            return c;
        }

        static Color SunPixel(float u, float v)
        {
            var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            var core = 1f - Edge(0.25f, 0.55f, d);
            var halo = (1f - Edge(0.4f, 1f, d)) * 0.35f;
            return new Color(1f, 0.93f, 0.8f, Mathf.Clamp01(core + halo));
        }

        static Color DustPixel(float u, float v)
        {
            var d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            return new Color(1f, 1f, 1f, 1f - Edge(0.1f, 1f, d));
        }

        // ---------- Malhas ----------

        static Mesh SaveMesh(string name, G.Draft draft)
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

        // ---------- Hierarquia ----------

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

        static MeshRenderer Surface(Transform t, Mesh mesh, Material material, bool shadows)
        {
            Get<MeshFilter>(t.gameObject).sharedMesh = mesh;
            var renderer = Get<MeshRenderer>(t.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return renderer;
        }

        static void BuildStage(GameObject root, bool valley, Materials m, MarsStormProfileData profile)
        {
            Func<float, float, float> height = valley ? (Func<float, float, float>)G.ValleyHeight : G.VictoriaHeight;
            var prefix = valley ? "Valley" : "Victoria";
            var maxRadius = valley ? 1100f : 1000f;

            var terrain = Child(root.transform, "Terrain");
            var visual = SaveMesh(prefix + "_Polar", G.Polar(height, G.RingRadii(maxRadius, 0.4f, 16f, 0.05f), 160));
            Surface(Child(terrain, "Polar"), visual, valley ? m.valleyGround : m.victoriaGround, true);
            var collisionMesh = SaveMesh(prefix + "_Collider", G.Polar(height, G.RingRadii(45f, 0.9f, 45f, 0.06f), 64));
            Get<MeshCollider>(Child(terrain, "Collider").gameObject).sharedMesh = collisionMesh;

            if (!valley)
            {
                var band = SaveMesh("Victoria_RimBand", G.VictoriaRimBand(192));
                Surface(Child(terrain, "RimBand"), band, m.rimBand, false);
            }

            BuildRocks(Child(root.transform, "Rocks"), valley, height, m.rocks);
            BuildAtmosphere(root, valley, m, profile);
            BuildAnchors(root, valley, height);
        }

        static bool InClearZone(Vector2 p, bool valley, float margin)
        {
            if (p.magnitude < 3f + margin)
                return true; // junto do ponto de partida do rover

            if (!valley)
                return false;

            // Percurso do rover até o mirante e a área do mirante.
            return Mathf.Abs(p.x) < 1.6f + margin && p.y > -9f && p.y < 6f;
        }

        static void BuildRocks(Transform parent, bool valley, Func<float, float, float> height, Material material)
        {
            var variants = new Mesh[6];
            for (var i = 0; i < variants.Length; i++)
                variants[i] = SaveMesh("LateRock_" + i, ShieldFieldGeometry.FieldRock(60 + i + (valley ? 10 : 0)));

            var rng = new System.Random(valley ? 2018 : 2007);
            var count = 0;
            var target = valley ? 96 : 48;
            for (var attempt = 0; attempt < 4000 && count < target; attempt++)
            {
                var half = valley ? 34f : 40f;
                var p = new Vector2(((float)rng.NextDouble() * 2f - 1f) * half, ((float)rng.NextDouble() * 2f - 1f) * half + (valley ? 6f : 0f));
                var size = Mathf.Lerp(0.04f, valley ? 0.3f : 0.22f, Mathf.Pow((float)rng.NextDouble(), 2.4f));
                if (valley && count < 7)
                    size = Mathf.Lerp(0.3f, 0.55f, (float)rng.NextDouble()); // poucos matacões
                if (InClearZone(p, valley, size))
                    continue;

                var t = Child(parent, "Rock_" + count.ToString("00"));
                var ground = G.Ground(height, p.x, p.y);
                t.localPosition = ground + Vector3.down * size * 0.1f;
                t.localRotation = Quaternion.FromToRotation(Vector3.up, G.Normal(height, p.x, p.y)) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                t.localScale = new Vector3(size * 2f, size * Mathf.Lerp(1.3f, 2f, (float)rng.NextDouble()), size * 2f);
                Surface(t, variants[count % variants.Length], material, true);
                if (size > 0.12f)
                {
                    var sphere = Get<SphereCollider>(t.gameObject);
                    sphere.center = new Vector3(0f, 0.12f, 0f);
                    sphere.radius = 0.4f;
                }

                count++;
            }

            for (var i = parent.childCount - 1; i >= count; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        static void BuildAtmosphere(GameObject root, bool valley, Materials m, MarsStormProfileData profile)
        {
            var atmosphere = Child(root.transform, "Atmosphere");
            var sunQuad = SaveMesh("SunQuad", G.Quad(1f));
            var disc = Child(atmosphere, "SunDisc");
            var direction = SunDirection;
            disc.localPosition = direction * 40f;
            disc.localRotation = Quaternion.LookRotation(direction, Vector3.up); // a face visível (−Z do quad) olha para quem está na origem
            disc.localScale = Vector3.one * 4.5f; // estilizado: o disco real seria minúsculo; precisa ficar legível
            Surface(disc, sunQuad, m.sunDisc, false);

            var dust = Child(atmosphere, "DustDrift");
            dust.localPosition = new Vector3(0f, 1.5f, valley ? -2f : 0f);
            var ps = Get<ParticleSystem>(dust.gameObject);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 6f;
            main.startSize = 0.45f;
            main.startSpeed = 0.35f;
            main.maxParticles = profile != null ? profile.maxParticlesNormal : 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.7f, 0.5f, 0.36f, 0.3f);
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50f, 5f, 50f);
            var renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = m.dust;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var controller = Get<MarsAtmosphereController>(root);
            var so = new SerializedObject(controller);
            so.FindProperty("profile").objectReferenceValue = profile;
            so.FindProperty("eventId").stringValue = valley ? MarsStormProfileData.Valley2018 : MarsStormProfileData.Victoria2007;
            so.FindProperty("sunDisc").objectReferenceValue = disc.GetComponent<Renderer>();
            so.FindProperty("dustParticles").objectReferenceValue = ps;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildAnchors(GameObject root, bool valley, Func<float, float, float> height)
        {
            var anchors = Child(root.transform, "Anchors");
            var start = Child(anchors, "RoverStart");
            var startXZ = valley ? new Vector2(0f, -6f) : Vector2.zero;
            start.localPosition = G.Ground(height, startXZ.x, startXZ.y);
            start.localRotation = Quaternion.LookRotation(valley ? Vector3.forward : new Vector3(G.VictoriaCenter.x, 0f, G.VictoriaCenter.y).normalized, Vector3.up);

            var component = Get<LateMarsStageAnchors>(root);
            var so = new SerializedObject(component);
            so.FindProperty("kind").enumValueIndex = valley ? (int)LateMarsStageAnchors.StageKind.Valley : (int)LateMarsStageAnchors.StageKind.Victoria;
            so.FindProperty("roverStart").objectReferenceValue = start;

            if (valley)
            {
                var overlook = Child(anchors, "Overlook");
                overlook.localPosition = G.Ground(height, 0f, 3f);
                var sunView = Child(anchors, "SunView");
                sunView.localPosition = overlook.localPosition + new Vector3(0.6f, 1.55f, -1.2f);
                sunView.localRotation = Quaternion.LookRotation(SunDirection, Vector3.up);
                var valleyView = Child(anchors, "ValleyView");
                valleyView.localPosition = overlook.localPosition + new Vector3(0.5f, 1.6f, -0.8f);
                valleyView.localRotation = Quaternion.LookRotation(new Vector3(0f, -9f, 60f), Vector3.up);
                var earth = Child(anchors, "EarthTarget");
                earth.localPosition = new Vector3(-70f, 60f, -40f);
                so.FindProperty("overlook").objectReferenceValue = overlook;
                so.FindProperty("sunView").objectReferenceValue = sunView;
                so.FindProperty("valleyView").objectReferenceValue = valleyView;
                so.FindProperty("earthTarget").objectReferenceValue = earth;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Prévia ----------

        static void BuildPreviewRig(SceneBuildContext context, GameObject victoria, GameObject valley)
        {
            var sunObject = context.Child(context.Root.transform, "Sun · single shadow light");
            sunObject.transform.rotation = Quaternion.LookRotation(-SunDirection, Vector3.up);
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.3f;

            var cameraObject = context.Child(context.Root.transform, "Preview Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 4000f;
            context.Component<UniversalAdditionalCameraData>(cameraObject).renderPostProcessing = false;
            var start = victoria.GetComponent<LateMarsStageAnchors>().RoverStart;
            cameraObject.transform.SetPositionAndRotation(start.position + new Vector3(0f, 2.4f, -6f), Quaternion.LookRotation(new Vector3(0f, -0.05f, 1f)));

            foreach (var stage in new[] { victoria, valley })
            {
                var so = new SerializedObject(stage.GetComponent<MarsAtmosphereController>());
                so.FindProperty("sun").objectReferenceValue = sun;
                so.FindProperty("skyCamera").objectReferenceValue = camera;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Só um controlador dita o ambiente da prévia por vez: o do vale fica desligado até alguém pedir.
            valley.GetComponent<MarsAtmosphereController>().enabled = false;
            RenderSettings.skybox = null;
        }
    }
}
