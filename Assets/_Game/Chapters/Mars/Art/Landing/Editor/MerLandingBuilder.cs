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
    /// MAR-014 (R4): conjunto de pouso (prefab), pouso paralelo M04–M06 como três SequenceDefinitions com keyframes de câmera densos,
    /// dados de datas/letreiros e cena de prévia. Reconstrói sem perder GUIDs de assets. Não toca em Boot, BuildSettings nem scripts de jogo.
    /// </summary>
    public sealed class MerLandingBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string ArtPath = MarsPath + "/Art/Landing";
        public const string GeneratedPath = ArtPath + "/Generated";
        public const string PrefabPath = MarsPath + "/Prefabs/MerLandingAssembly.prefab";
        public const string DataPath = ArtPath + "/MarsDualLanding.asset";
        public const string SequenceFolder = MarsPath + "/Timelines";
        public const string PreviewScenePath = ArtPath + "/Preview/MerLandingPreview.unity";
        public const string SpiritRoverPath = MarsPath + "/Prefabs/SpiritVisual.prefab";
        public const string OpportunityRoverPath = MarsPath + "/Prefabs/OpportunityVisual.prefab";
        const string SubtitlesPath = MarsPath + "/Data/MarsSubtitles.json";

        public static readonly Vector3 SpiritStageOffset = new Vector3(4000f, 0f, 0f);
        public static readonly Vector3 SunDirection = new Vector3(0.3f, 0.55f, 0.78f).normalized;

        /// <summary>Com o mapa importado importado (Art/Classic), o Opportunity pousa no terreno dele (bacia rasa); sem ele, na bacia da MAR-012.</summary>
        public static MerLandingSite OpportunitySite =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Chapters/Mars/Art/Classic/Export/ClassicEagle.prefab") != null ? MerLandingSite.MeridianiClassic : MerLandingSite.Meridiani;

        public static string SequencePath(string stepId) => SequenceFolder + "/MarsDualLanding_" + stepId + ".asset";

        public override string Id => "mars.landing";
        public override string Owner => "R4 · MAR-014";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-014] Landing";
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-014 Reconstruir pouso split")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new MerLandingBuilder());
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
            public Material shield, backshell, parachute, lines, metal, airbag, tether, plume, flame, ground;
        }

        sealed class SequenceSet
        {
            public SequenceDefinition split, eagle, verification;
        }

        public override void Build(SceneBuildContext context)
        {
            var m = BuildMaterials(context);
            var data = context.Asset(DataPath, ScriptableObject.CreateInstance<MarsDualLandingData>);
            data.SetLabels(DefaultLabels());
            EditorUtility.SetDirty(data);

            var sequences = BuildSequences(context, data);
            var meshes = BuildMeshes();
            var prefab = context.Prefab(PrefabPath, root => BuildPrefab(root, m, meshes, data, sequences));

            var instance = context.PrefabInstance(context.Root.transform, prefab, "MerLandingAssembly");
            instance.transform.localPosition = Vector3.zero;
            BuildPreviewRig(context, instance);
            AssetDatabase.SaveAssets();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            foreach (var director in context.Root.GetComponentsInChildren<MarsDualLandingDirector>(true))
                errors.AddRange(director.ValidateSetup());
        }

        // ---------- Rótulos e sequências ----------

        static List<MarsLandingLabel> DefaultLabels() => new List<MarsLandingLabel>
        {
            new MarsLandingLabel("MAR.L01", 0.4f, 18f, MarsLandingLabelAnchor.LeftHalf),
            new MarsLandingLabel("MAR.L02", 0.4f, 20f, MarsLandingLabelAnchor.RightHalf),
            new MarsLandingLabel("MAR.T02", 1f, 17.5f, MarsLandingLabelAnchor.Separator),
            new MarsLandingLabel("MAR.L03", 22.2f, 27f, MarsLandingLabelAnchor.Full),
        };

        static SequenceSet BuildSequences(SceneBuildContext context, MarsDualLandingData data)
        {
            var errors = new List<string>();
            var json = File.ReadAllText(SubtitlesPath);
            var table = MarsChapterFlow.ParseSubtitles(json, errors);
            if (table == null)
                throw new InvalidOperationException("MarsSubtitles.json inválido:" + ContractValidation.Describe(errors));

            var site = OpportunitySite;
            ShotDefinition Shot(string id, MerLandingCameraPath.Segment segment, float minClearance, IEnumerable<ShotEvent> events, bool continueFromPrevious = false)
            {
                var (start, end) = MerLandingCameraPath.Window(segment);
                return new ShotDefinition(id, end - start, MarsDualLandingDirector.OpportunityRigId, MerLandingCameraPath.Bake(segment, site, data),
                    null, ShotEasing.Linear, minClearance, events, continueFromPrevious);
            }

            var split = SequenceDefinition.Create(data.SplitSequenceId,
                new[]
                {
                    Shot("mars.landing.entry", MerLandingCameraPath.Segment.Entry, 0.3f, new[] { new ShotEvent(0.3f, "mars.landing.entry.fireball") }),
                    Shot("mars.landing.chute", MerLandingCameraPath.Segment.Parachute, 0.3f, new[]
                    {
                        new ShotEvent(0f, "mars.landing.chute.deploy"), new ShotEvent(2.6f, "mars.landing.heatshield.separate"), new ShotEvent(4f, "mars.landing.tether.deploy"),
                    }),
                    Shot("mars.landing.airbags", MerLandingCameraPath.Segment.Airbags, 1.5f, new[]
                    {
                        new ShotEvent(0.2f, "mars.landing.airbags.inflate"), new ShotEvent(1.1f, "mars.landing.retro.fire"), new ShotEvent(1.9f, "mars.landing.tether.cut"),
                        new ShotEvent(2.8f, "mars.landing.impact.first"), new ShotEvent(4.5f, "mars.landing.impact.second"), new ShotEvent(5.7f, "mars.landing.stop"),
                    }),
                }, MarsChapterFlow.BuildSequenceCues(table, "M04"));
            var eagle = SequenceDefinition.Create(data.EagleSequenceId,
                new[] { Shot("mars.landing.eagle", MerLandingCameraPath.Segment.Eagle, 1.5f, new[] { new ShotEvent(0f, "mars.landing.eagle.rest") }, continueFromPrevious: true) },
                MarsChapterFlow.BuildSequenceCues(table, "M05"));
            var verification = SequenceDefinition.Create(data.VerificationSequenceId,
                new[]
                {
                    Shot("mars.landing.checks", MerLandingCameraPath.Segment.Checks, 1.5f, new[]
                    {
                        new ShotEvent(0f, "mars.landing.airbags.deflate"), new ShotEvent(0.2f, "mars.landing.egress.date"), new ShotEvent(2.6f, "mars.landing.petals.open"),
                    }, continueFromPrevious: true),
                    Shot("mars.landing.ramp", MerLandingCameraPath.Segment.Ramp, 0.4f, new[]
                    {
                        new ShotEvent(0.2f, "mars.landing.rover.unfold"),
                    }, continueFromPrevious: true),
                    Shot("mars.landing.egress", MerLandingCameraPath.Segment.Egress, 0.4f, new[]
                    {
                        new ShotEvent(0f, "mars.landing.egress.drive"), new ShotEvent(MerLandingCameraPath.Window(MerLandingCameraPath.Segment.Egress).end - MerLandingCameraPath.Window(MerLandingCameraPath.Segment.Egress).start - 0.2f, "mars.landing.handoff"),
                    }, continueFromPrevious: true),
                }, MarsChapterFlow.BuildSequenceCues(table, "M06"));

            return new SequenceSet
            {
                split = Store(context, SequencePath("M04"), split),
                eagle = Store(context, SequencePath("M05"), eagle),
                verification = Store(context, SequencePath("M06"), verification),
            };
        }

        static T Store<T>(SceneBuildContext context, string path, T template) where T : Object
        {
            var value = context.Asset(path, () => template);
            if (value != template)
            {
                EditorUtility.CopySerialized(template, value);
                Object.DestroyImmediate(template);
            }

            EditorUtility.SetDirty(value);
            return value;
        }

        // ---------- Texturas e materiais ----------

        static float T(float u, float v, float scale) => G.TileNoise(u, v, scale);

        static Color ShieldPixel(float u, float v)
        {
            var c = Color.Lerp(new Color(0.13f, 0.09f, 0.07f), new Color(0.24f, 0.17f, 0.12f), T(u, v, 9f));
            c *= 0.9f + 0.2f * T(u, v, 30f);
            var seam = Mathf.Min(Frac(u * 16f), Frac(v * 16f));
            return seam < 0.04f ? c * 0.55f : c;
        }

        static Color BackshellPixel(float u, float v)
        {
            var c = Color.Lerp(new Color(0.78f, 0.76f, 0.71f), new Color(0.88f, 0.86f, 0.82f), T(u, v, 8f));
            c *= 0.94f + 0.1f * T(u, v, 36f);
            c = Color.Lerp(c, new Color(0.62f, 0.46f, 0.34f), Mathf.Clamp01(T(u, v, 5f) - 0.55f) * 0.9f);
            var seam = Frac(u * 6f) < 0.02f || Frac(v * 5f) < 0.025f;
            return seam ? c * 0.6f : c;
        }

        static Color ParachutePixel(float u, float v)
        {
            var band = Mathf.Floor(Frac(u) * 2f) % 2f < 1f;
            var c = band ? new Color(0.9f, 0.42f, 0.13f) : new Color(0.92f, 0.9f, 0.85f);
            var weave = 0.96f + 0.04f * Mathf.Sin(u * 640f) * Mathf.Sin(v * 640f);
            return c * weave * (0.92f + 0.1f * T(u, v, 14f));
        }

        static Color MetalPixel(float u, float v)
        {
            var c = Color.Lerp(new Color(0.55f, 0.54f, 0.5f), new Color(0.78f, 0.66f, 0.4f), Mathf.Clamp01(T(u, v, 6f) * 1.4f - 0.2f));
            c *= 0.92f + 0.14f * T(u, v, 40f);
            var edge = Mathf.Min(Mathf.Min(u, 1f - u), v);
            var cell = Mathf.Abs(Mathf.Sin(u * 90f)) * Mathf.Abs(Mathf.Sin(v * 90f));
            return edge < 0.03f ? c * 0.5f : c * (0.93f + 0.07f * cell);
        }

        static Color AirbagPixel(float u, float v)
        {
            var c = Color.Lerp(new Color(0.82f, 0.78f, 0.7f), new Color(0.93f, 0.9f, 0.83f), T(u, v, 7f));
            var weave = 0.93f + 0.07f * (Mathf.Abs(Mathf.Sin(u * 120f)) + Mathf.Abs(Mathf.Sin(v * 120f))) * 0.5f;
            var seam = Mathf.Abs(Frac(u * 6f) - 0.5f) > 0.485f || Mathf.Abs(Frac(v * 4f) - 0.5f) > 0.49f;
            c *= weave * (0.94f + 0.08f * T(u, v, 24f));
            c = Color.Lerp(c, new Color(0.58f, 0.43f, 0.32f), Mathf.Clamp01(T(u, v, 5f) - 0.62f) * 0.8f);
            return seam ? c * 0.72f : c;
        }

        static Color PlumePixel(float u, float v)
        {
            var alpha = Mathf.Pow(1f - v, 1.4f) * (0.75f + 0.25f * T(u, v, 6f));
            var c = Color.Lerp(new Color(1f, 0.92f, 0.62f), new Color(1f, 0.42f, 0.14f), v);
            return new Color(c.r, c.g, c.b, alpha);
        }

        static Color GroundPixel(float u, float v)
        {
            var sand = Color.Lerp(new Color(0.53f, 0.37f, 0.26f), new Color(0.6f, 0.43f, 0.3f), T(u, v, 9f));
            sand *= 0.95f + 0.1f * T(u, v, 26f);
            return Color.Lerp(sand, new Color(0.42f, 0.32f, 0.25f), Mathf.Clamp01(T(u, v, 17f) - 0.6f) * 0.6f);
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        Materials BuildMaterials(SceneBuildContext context)
        {
            var shieldTex = Texture("LandingHeatShield", 256, true, true, ShieldPixel);
            var backTex = Texture("LandingBackshell", 256, true, true, BackshellPixel);
            var chuteTex = Texture("LandingParachute", 256, true, true, ParachutePixel);
            var metalTex = Texture("LandingMetal", 256, true, true, MetalPixel);
            var bagTex = Texture("LandingAirbag", 256, true, true, AirbagPixel);
            var plumeTex = Texture("LandingPlume", 64, true, false, PlumePixel);
            var groundTex = Texture("LandingGround", 512, true, true, GroundPixel);

            var m = new Materials
            {
                shield = context.Material(GeneratedPath + "/HeatShield.mat", "Universal Render Pipeline/Lit"),
                backshell = context.Material(GeneratedPath + "/Backshell.mat", "Universal Render Pipeline/Lit"),
                parachute = context.Material(GeneratedPath + "/Parachute.mat", "Universal Render Pipeline/Lit"),
                lines = context.Material(GeneratedPath + "/Suspension.mat", "Universal Render Pipeline/Unlit"),
                metal = context.Material(GeneratedPath + "/LanderMetal.mat", "Universal Render Pipeline/Lit"),
                airbag = context.Material(GeneratedPath + "/Airbag.mat", "Universal Render Pipeline/Lit"),
                tether = context.Material(GeneratedPath + "/Tether.mat", "Universal Render Pipeline/Lit"),
                plume = context.Material(GeneratedPath + "/Plume.mat", "Universal Render Pipeline/Unlit"),
                flame = context.Material(GeneratedPath + "/Flame.mat", "Universal Render Pipeline/Unlit"),
                ground = context.Material(GeneratedPath + "/LandingGround.mat", "Universal Render Pipeline/Lit"),
            };
            Lit(m.shield, shieldTex, 0.12f, 0f, false);
            Lit(m.backshell, backTex, 0.2f, 0f, true);
            Lit(m.parachute, chuteTex, 0.15f, 0f, true);
            Lit(m.metal, metalTex, 0.3f, 0.35f, false);
            Lit(m.airbag, bagTex, 0.3f, 0f, false);
            Lit(m.tether, backTex, 0.1f, 0f, false);
            m.tether.SetColor("_BaseColor", new Color(0.2f, 0.17f, 0.14f));
            Lit(m.ground, groundTex, 0.04f, 0f, false);
            m.lines.SetColor("_BaseColor", new Color(0.95f, 0.86f, 0.72f));
            Transparent(m.plume, plumeTex, new Color(1f, 0.62f, 0.3f, 0.6f));
            Transparent(m.flame, plumeTex, new Color(1f, 0.8f, 0.45f, 0.9f));
            return m;
        }

        static void Lit(Material m, Texture2D map, float smoothness, float metallic, bool doubleSided)
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_BaseMap", map);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Cull", doubleSided ? 0f : 2f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        static void Transparent(Material m, Texture2D map, Color color)
        {
            m.SetTexture("_BaseMap", map);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            BaseShaderGUI.SetupMaterialBlendMode(m);
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
                importer.alphaIsTransparency = !repeat;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Malhas ----------

        sealed class MeshSet
        {
            public Mesh heatShield, backshell, plume, flame, canopy, lines, tether, petal, basePetal, lobe, groundSpirit, groundOpportunity, colliderSpirit, colliderOpportunity;
        }

        static MeshSet BuildMeshes()
        {
            var layoutBase = new[] { MerLandingLayout.VertexA, MerLandingLayout.VertexC, MerLandingLayout.VertexB };
            return new MeshSet
            {
                heatShield = SaveMesh("HeatShield", MerLandingGeometry.HeatShield()),
                backshell = SaveMesh("Backshell", MerLandingGeometry.Backshell()),
                plume = SaveMesh("Plume", MerLandingGeometry.Plume()),
                flame = SaveMesh("Flame", MerLandingGeometry.Flame()),
                canopy = SaveMesh("Canopy", MerLandingGeometry.Canopy()),
                lines = SaveRaw("SuspensionLines", MerLandingGeometry.SuspensionLines()),
                tether = SaveMesh("Tether", MerLandingGeometry.Cylinder(0.06f, 2f, 8)),
                petal = SaveMesh("Petal", MerLandingGeometry.Prism(MerLandingLayout.PetalTriangleLocal(), MerLandingLayout.PetalThicknessMeters)),
                basePetal = SaveMesh("BasePetal", MerLandingGeometry.Prism(layoutBase, MerLandingLayout.PetalThicknessMeters)),
                lobe = SaveMesh("AirbagLobe", MerLandingGeometry.Sphere(1f, 20, 12)),
                groundSpirit = SaveMesh("GroundGusev", MerLandingGeometry.Ground(MerLandingSite.Gusev, 520f, false)),
                groundOpportunity = SaveMesh("GroundMeridiani", MerLandingGeometry.Ground(OpportunitySite, 520f, false)),
                colliderSpirit = SaveMesh("ColliderGusev", MerLandingGeometry.Ground(MerLandingSite.Gusev, 520f, true)),
                colliderOpportunity = SaveMesh("ColliderMeridiani", MerLandingGeometry.Ground(OpportunitySite, 520f, true)),
            };
        }

        static Mesh SaveMesh(string name, G.Draft draft)
        {
            var mesh = LoadOrCreateMesh(name);
            draft.Write(mesh);
            mesh.name = name;
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Mesh SaveRaw(string name, Mesh source)
        {
            var mesh = LoadOrCreateMesh(name);
            EditorUtility.CopySerialized(source, mesh);
            Object.DestroyImmediate(source);
            mesh.name = name;
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Mesh LoadOrCreateMesh(string name)
        {
            var path = GeneratedPath + "/" + name + ".asset";
            Directory.CreateDirectory(GeneratedPath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            return mesh;
        }

        // ---------- Prefab ----------

        static T Get<T>(GameObject go) where T : Component => go.GetComponent<T>() != null ? go.GetComponent<T>() : go.AddComponent<T>();

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static MeshRenderer Surface(Transform t, Mesh mesh, Material material, bool shadows)
        {
            Get<MeshFilter>(t.gameObject).sharedMesh = mesh;
            var renderer = Get<MeshRenderer>(t.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = shadows;
            return renderer;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }

            return null;
        }

        static void BuildPrefab(GameObject root, Materials m, MeshSet meshes, MarsDualLandingData data, SequenceSet sequences)
        {
            for (var i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            foreach (var component in root.GetComponents<MonoBehaviour>())
                Object.DestroyImmediate(component);

            var spirit = BuildStage(root.transform, "SpiritStage", MerLandingSite.Gusev, m, meshes, SpiritRoverPath);
            spirit.transform.localPosition = SpiritStageOffset;
            var opportunity = BuildStage(root.transform, "OpportunityStage", OpportunitySite, m, meshes, OpportunityRoverPath);
            opportunity.transform.localPosition = Vector3.zero;

            var cameraObject = new GameObject("SpiritCamera");
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 20f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1500f;
            camera.enabled = false;
            var extra = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            extra.renderPostProcessing = false;

            var director = root.AddComponent<MarsDualLandingDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("spirit").objectReferenceValue = spirit;
            so.FindProperty("opportunity").objectReferenceValue = opportunity;
            so.FindProperty("spiritCamera").objectReferenceValue = camera;
            so.FindProperty("splitSequence").objectReferenceValue = sequences.split;
            so.FindProperty("eagleSequence").objectReferenceValue = sequences.eagle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static MerLandingAssembly BuildStage(Transform parent, string name, MerLandingSite site, Materials m, MeshSet meshes, string roverPath)
        {
            var stage = Child(parent, name);
            var assembly = stage.gameObject.AddComponent<MerLandingAssembly>();

            var ground = Child(stage, "Ground");
            Surface(ground, site == MerLandingSite.Gusev ? meshes.groundSpirit : meshes.groundOpportunity, m.ground, false);
            Get<MeshCollider>(ground.gameObject).sharedMesh = site == MerLandingSite.Gusev ? meshes.colliderSpirit : meshes.colliderOpportunity;

            var capsule = Child(stage, "Capsule");
            Surface(Child(capsule, "Backshell"), meshes.backshell, m.backshell, true);
            var fireball = Child(capsule, "Fireball");
            var fireRenderer = Surface(fireball, meshes.plume, m.plume, false);
            var flames = new List<Transform>();
            for (var i = 0; i < 3; i++)
            {
                var flame = Child(capsule, "Retro_" + i);
                var angle = i * 120f * Mathf.Deg2Rad;
                flame.localPosition = new Vector3(Mathf.Cos(angle) * 2.15f, 0.7f, Mathf.Sin(angle) * 2.15f) * MerLandingLayout.HardwareScale;
                Surface(flame, meshes.flame, m.flame, false);
                flame.gameObject.SetActive(false);
                flames.Add(flame);
            }

            fireball.gameObject.SetActive(false);

            var shield = Child(stage, "HeatShield");
            Surface(shield, meshes.heatShield, m.shield, true);

            var lander = Child(stage, "Lander");
            Surface(Child(lander, "BasePetal"), meshes.basePetal, m.metal, true).transform.localPosition = Vector3.zero;
            var layoutPetals = MerLandingLayout.Petals();
            var petals = new Transform[3];
            for (var i = 0; i < 3; i++)
            {
                var petal = Child(lander, "Petal_" + layoutPetals[i].Name);
                petal.localPosition = layoutPetals[i].HingeMidpoint;
                petal.localRotation = layoutPetals[i].ClosedRotation;
                Surface(petal, meshes.petal, m.metal, true);
                petals[i] = petal;
            }

            var core = Child(lander, "AirbagCore");
            Surface(core, meshes.lobe, m.airbag, false);
            var lobes = new List<Transform>();
            var layoutLobes = MerLandingLayout.Lobes();
            for (var i = 0; i < layoutLobes.Count; i++)
            {
                var lobe = Child(lander, "Airbag_" + i.ToString("00"));
                lobe.localPosition = layoutLobes[i].Center;
                Surface(lobe, meshes.lobe, m.airbag, true);
                lobes.Add(lobe);
            }

            var roverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(roverPath);
            if (roverPrefab == null)
                throw new InvalidOperationException("Prefab do rover ausente: " + roverPath);

            var rover = (GameObject)PrefabUtility.InstantiatePrefab(roverPrefab);
            rover.name = "Rover";
            rover.transform.SetParent(lander, false);

            var parachute = Child(stage, "Parachute");
            Surface(parachute, meshes.canopy, m.parachute, false);
            var lines = Child(parachute, "Suspension");
            Surface(lines, meshes.lines, m.lines, false);
            parachute.gameObject.SetActive(false);

            var tether = Child(stage, "Tether");
            Surface(tether, meshes.tether, m.tether, false);
            tether.gameObject.SetActive(false);

            // Onde o rover para depois de sair do módulo (quadro do palco): o jogo continua daqui, sem corte.
            var egress = Child(stage, "EgressStart");
            MerLandingChoreography.RoverWorld(MerLandingChoreography.Sample(MerLandingChoreography.TotalSeconds, site), out var egressPosition, out var egressRotation, out _, site);
            egress.localPosition = egressPosition;
            egress.localRotation = egressRotation;

            var so = new SerializedObject(assembly);
            so.FindProperty("site").enumValueIndex = (int)site;
            Set(so, "egressAnchor", egress);
            Set(so, "capsule", capsule);
            Set(so, "heatShield", shield);
            Set(so, "lander", lander);
            Set(so, "parachute", parachute);
            Set(so, "tether", tether);
            Set(so, "fireball", fireball);
            so.FindProperty("fireballRenderer").objectReferenceValue = fireRenderer;
            Set(so, "core", core);
            Set(so, "roverRoot", rover.transform);
            SetArray(so, "retroFlames", flames);
            SetArray(so, "lobes", lobes);
            SetArray(so, "petals", petals);
            Set(so, "rockerLeft", Find(rover.transform, "Rocker_L"));
            Set(so, "rockerRight", Find(rover.transform, "Rocker_R"));
            Set(so, "bogieLeft", Find(rover.transform, "Bogie_L"));
            Set(so, "bogieRight", Find(rover.transform, "Bogie_R"));
            Set(so, "mastHead", Find(rover.transform, "PMA_Head"));
            Set(so, "antennaDish", Find(rover.transform, "HGA_Dish"));
            so.ApplyModifiedPropertiesWithoutUndo();
            assembly.Apply(0f);
            return assembly;
        }

        static void Set(SerializedObject so, string field, Transform value) => so.FindProperty(field).objectReferenceValue = value;

        static void SetArray(SerializedObject so, string field, IList<Transform> values)
        {
            var property = so.FindProperty(field);
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // ---------- Prévia ----------

        static void BuildPreviewRig(SceneBuildContext context, GameObject instance)
        {
            var sunObject = context.Child(context.Root.transform, "Sun · single shadow light");
            sunObject.transform.rotation = Quaternion.LookRotation(-SunDirection, Vector3.up);
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.intensity = 1.4f;
            sun.color = new Color(1f, 0.92f, 0.82f);

            var cameraObject = context.Child(context.Root.transform, "Preview Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.47f, 0.37f);
            camera.fieldOfView = 25f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1500f;
            context.Component<UniversalAdditionalCameraData>(cameraObject).renderPostProcessing = false;
            context.Component<CameraDirector>(cameraObject);
            context.Component<SequenceDirector>(cameraObject).enabled = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.29f, 0.25f);
        }
    }
}
