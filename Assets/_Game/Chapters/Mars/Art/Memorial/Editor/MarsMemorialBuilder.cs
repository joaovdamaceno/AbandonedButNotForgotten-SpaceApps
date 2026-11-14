using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using static Abandoned.SpaceApps.MarsMemorialChoreography;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-019 (R4): memorial M31–M32 por prefab e duas SequenceDefinitions (30 s + 4 s). Quatro regiões separadas (Marte, retrato de
    /// recorte, imagem escolhida, díptico); só a do bloco atual fica ativa. Uma câmera e uma luz de recorte sem sombra.
    /// </summary>
    public sealed class MarsMemorialBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string ArtPath = MarsPath + "/Art/Memorial";
        public const string GeneratedPath = ArtPath + "/Generated";
        public const string PrefabPath = MarsPath + "/Prefabs/MarsMemorialStage.prefab";
        public const string MainSequencePath = MarsPath + "/Timelines/MarsMemorial_M31.asset";
        public const string TitleSequencePath = MarsPath + "/Timelines/MarsMemorial_M32.asset";
        public const string PreviewScenePath = ArtPath + "/Preview/MarsMemorialPreview.unity";
        const string SubtitlesPath = MarsPath + "/Data/MarsSubtitles.json";

        public static readonly Vector3 PortraitOffset = new Vector3(300f, 0f, 0f);
        public static readonly Vector3 FrameOffset = new Vector3(600f, 0f, 0f);
        public static readonly Vector3 DiptychOffset = new Vector3(900f, 0f, 0f);
        public const float FrameWidth = 8f;

        public override string Id => "mars.memorial";
        public override string Owner => "R4 · MAR-019";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-019] Memorial";
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-019 Reconstruir memorial")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new MarsMemorialBuilder());
            if (!result.Succeeded)
                throw new InvalidOperationException(result.ToString());
            Debug.Log(result.ToString());
        }

        public static void BuildFromCommandLine()
        {
            try { BuildFromMenu(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------- Câmera ----------

        struct View { public Vector3 Position, Target; public float Fov; }

        static IEnumerable<(string id, float seconds, Func<float, View> path, ShotEvent[] events)> Shots(string sequence)
        {
            Vector3 L(Vector3 a, Vector3 b, float u) => Vector3.Lerp(a, b, MerLandingChoreography.Smooth(u));
            if (sequence == MarsMemorialDirector.SequenceM31)
            {
                yield return ("mars.memorial.mechanisms", 6f, u => new View { Position = L(new Vector3(-7f, 1.8f, -9f), new Vector3(-6.5f, 1.7f, -8.6f), u), Target = new Vector3(0f, 1f, 0f), Fov = 36f },
                    new[] { new ShotEvent(MechanismsStopSeconds, "mars.memorial.mechanisms.stop") });
                yield return ("mars.memorial.contact", 6f, u => new View { Position = L(new Vector3(-1.8f, 1.5f, -2.6f), new Vector3(-1.5f, 1.4f, -2.3f), u), Target = new Vector3(-0.41f, 0.9f, -0.23f), Fov = 30f },
                    new[] { new ShotEvent(HudHideSeconds - BlockSeconds, "mars.memorial.hud.hide") });
                yield return ("mars.memorial.portrait", 6f, u => new View { Position = PortraitOffset + L(new Vector3(-3.2f, 1.1f, -4.4f), new Vector3(-2.2f, 1f, -4.6f), u), Target = PortraitOffset + new Vector3(0f, 0.9f, 0f), Fov = 30f },
                    new[] { new ShotEvent(0f, "mars.memorial.portrait.rim") });
                yield return ("mars.memorial.image", 6f, u => new View { Position = FrameOffset + new Vector3(0f, 0f, Mathf.Lerp(-7.4f, -7.1f, MerLandingChoreography.Smooth(u))), Target = FrameOffset, Fov = 36f },
                    new[] { new ShotEvent(0f, "mars.memorial.memory.show") });
                yield return ("mars.memorial.diptych", 6f, u => DiptychView(), new[] { new ShotEvent(0f, "mars.memorial.diptych") });
            }
            else
            {
                yield return ("mars.memorial.title", TitleSeconds, u => DiptychView(), new[] { new ShotEvent(0f, "mars.memorial.title") });
            }
        }

        static View DiptychView() => new View { Position = DiptychOffset + new Vector3(0f, 1.9f, -10.5f), Target = DiptychOffset + new Vector3(0f, 0.9f, 0f), Fov = 38f };

        static List<ShotKeyframe> Bake(float seconds, Func<float, View> path)
        {
            var count = Mathf.Max(2, Mathf.CeilToInt(seconds / 0.25f));
            var keys = new List<ShotKeyframe>();
            for (var i = 0; i <= count; i++)
            {
                var v = path(i / (float)count);
                keys.Add(new ShotKeyframe(seconds * i / count, v.Position, Quaternion.LookRotation(v.Target - v.Position, Vector3.up).eulerAngles, v.Fov));
            }

            return keys;
        }

        static List<MarsLandingLabel> DefaultLabels() => new List<MarsLandingLabel>
        {
            new MarsLandingLabel("MAR.MEM.01", 6.3f, 11.7f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.MEM.02", 12.3f, 17.7f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.MEM.03", 24.3f, 34f, MarsLandingLabelAnchor.LeftHalf),
            new MarsLandingLabel("MAR.MEM.04", 24.3f, 34f, MarsLandingLabelAnchor.RightHalf),
            new MarsLandingLabel("MAR.MEM.05", 30.2f, 34f, MarsLandingLabelAnchor.LeftHalf),
            new MarsLandingLabel("MAR.MEM.06", 30.2f, 34f, MarsLandingLabelAnchor.RightHalf),
        };

        static T Store<T>(SceneBuildContext context, string path, T template) where T : Object
        {
            var value = context.Asset(path, () => template);
            if (value != template) { EditorUtility.CopySerialized(template, value); Object.DestroyImmediate(template); }
            EditorUtility.SetDirty(value);
            return value;
        }

        public override void Build(SceneBuildContext context)
        {
            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            if (table == null)
                throw new InvalidOperationException("MarsSubtitles.json inválido:" + ContractValidation.Describe(errors));

            foreach (var (id, step, path) in new[] { (MarsMemorialDirector.SequenceM31, "M31", MainSequencePath), (MarsMemorialDirector.SequenceM32, "M32", TitleSequencePath) })
            {
                var shots = Shots(id).Select(s => new ShotDefinition(s.id, s.seconds, MarsMemorialDirector.RigId, Bake(s.seconds, s.path), null, ShotEasing.Linear, 0f, s.events)).ToList();
                Store(context, path, SequenceDefinition.Create(id, shots, MarsChapterFlow.BuildSequenceCues(table, step)));
            }

            var fallback = FallbackTexture();
            var prefab = context.Prefab(PrefabPath, root => BuildPrefab(root, fallback));
            context.PrefabInstance(context.Root.transform, prefab, "MarsMemorialStage");
            BuildPreview(context);
            AssetDatabase.SaveAssets();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            foreach (var d in context.Root.GetComponentsInChildren<MarsMemorialDirector>(true))
                errors.AddRange(d.ValidateSetup());
        }

        static Texture2D FallbackTexture()
        {
            var path = GeneratedPath + "/MemoryFallback.png";
            Directory.CreateDirectory(GeneratedPath);
            const int w = 256, h = 144;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var edge = x < 4 || y < 4 || x >= w - 4 || y >= h - 4;
                    var diagonal = Mathf.Abs(x * h - y * w) < 600 || Mathf.Abs((w - x) * h - y * w) < 600;
                    t.SetPixel(x, y, edge ? new Color(0.55f, 0.5f, 0.45f) : diagonal ? new Color(0.2f, 0.2f, 0.22f) : new Color(0.08f, 0.08f, 0.1f));
                }

            var bytes = t.EncodeToPNG();
            Object.DestroyImmediate(t);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(string name, string shader, Color color)
        {
            var path = GeneratedPath + "/" + name + ".mat";
            Directory.CreateDirectory(GeneratedPath);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, bool shadows = false)
        {
            var t = Child(parent, name);
            t.localPosition = position;
            t.localScale = scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return t;
        }

        static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        static GameObject Rover(Transform parent, string name, string prefabName, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarsPath + "/Prefabs/" + prefabName + ".prefab");
            if (prefab == null)
                throw new InvalidOperationException("Prefab ausente: " + prefabName);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return instance;
        }

        static void BuildPrefab(GameObject root, Texture2D fallback)
        {
            for (var i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            foreach (var c in root.GetComponents<MonoBehaviour>())
                Object.DestroyImmediate(c);

            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var dust = Mat("DustBackdrop", "Universal Render Pipeline/Unlit", new Color(0.42f, 0.29f, 0.2f));
            var black = Mat("BlackBackdrop", "Universal Render Pipeline/Unlit", new Color(0.005f, 0.005f, 0.008f));
            var valleyGround = Mat("ValleyGround", "Universal Render Pipeline/Lit", new Color(0.45f, 0.32f, 0.23f));
            var darkGround = Mat("DarkGround", "Universal Render Pipeline/Lit", new Color(0.05f, 0.05f, 0.06f));
            var gusevGround = Mat("GusevGround", "Universal Render Pipeline/Lit", new Color(0.36f, 0.3f, 0.26f));
            var frameMat = Mat("MemoryFrame", "Universal Render Pipeline/Unlit", Color.white);
            frameMat.SetTexture("_BaseMap", fallback);

            Transform Region(string name, Vector3 offset, Material backdrop)
            {
                var region = Child(root.transform, name);
                region.localPosition = offset;
                Part(region, "Backdrop", quad, backdrop, new Vector3(0f, 6f, 30f), new Vector3(120f, 60f, 1f));
                return region;
            }

            var valley = Region("Region_Valley", Vector3.zero, dust);
            Part(valley, "Ground", cube, valleyGround, new Vector3(0f, -0.05f, 0f), new Vector3(60f, 0.1f, 60f), true);
            var opportunity = Rover(valley, "Opportunity_Valley", "OpportunityVisual", Vector3.zero, 0f);

            var portrait = Region("Region_Portrait", PortraitOffset, black);
            Part(portrait, "Ground", cube, darkGround, new Vector3(0f, -0.05f, 0f), new Vector3(10f, 0.1f, 10f));
            Rover(portrait, "Opportunity_Portrait", "OpportunityVisual", Vector3.zero, 25f);
            var lightObject = Child(portrait, "RimLight");
            lightObject.localPosition = new Vector3(2.6f, 2.2f, 3.4f);
            lightObject.LookAt(portrait.position + new Vector3(0f, 0.8f, 0f));
            var rim = lightObject.gameObject.AddComponent<Light>();
            rim.type = LightType.Spot;
            rim.range = 14f;
            rim.spotAngle = 55f;
            rim.color = new Color(1f, 0.8f, 0.6f);
            rim.shadows = LightShadows.None;
            rim.enabled = false;

            var frameRegion = Region("Region_Frame", FrameOffset, black);
            var frame = Part(frameRegion, "MemoryFrame", quad, frameMat, Vector3.zero, new Vector3(FrameWidth, FrameWidth * 9f / 16f, 1f));

            var diptych = Region("Region_Diptych", DiptychOffset, black);
            Part(diptych, "Ground_Spirit", cube, gusevGround, new Vector3(-3.6f, -0.05f, 0f), new Vector3(5.2f, 0.1f, 5.2f), true);
            Part(diptych, "Ground_Opportunity", cube, valleyGround, new Vector3(3.6f, -0.05f, 0f), new Vector3(5.2f, 0.1f, 5.2f), true);
            Rover(diptych, "Spirit_Gusev", "SpiritVisual", new Vector3(-3.6f, 0f, 0f), 20f);
            Rover(diptych, "Opportunity_Valley", "OpportunityVisual", new Vector3(3.6f, 0f, 0f), -20f);

            var wheels = new[] { "Wheel_FL", "Wheel_FR", "Wheel_ML", "Wheel_MR", "Wheel_RL", "Wheel_RR" }.Select(n => Find(opportunity.transform, n)).ToArray();
            var director = root.AddComponent<MarsMemorialDirector>();
            var so = new SerializedObject(director);
            var regions = so.FindProperty("regions");
            regions.arraySize = 4;
            for (var i = 0; i < 4; i++)
                regions.GetArrayElementAtIndex(i).objectReferenceValue = new[] { valley, portrait, frameRegion, diptych }[i];
            var wheelProp = so.FindProperty("wheels");
            wheelProp.arraySize = wheels.Length;
            for (var i = 0; i < wheels.Length; i++)
                wheelProp.GetArrayElementAtIndex(i).objectReferenceValue = wheels[i];
            so.FindProperty("mast").objectReferenceValue = Find(opportunity.transform, "PMA_Mast");
            so.FindProperty("gimbal").objectReferenceValue = Find(opportunity.transform, "HGA_Gimbal");
            so.FindProperty("memoryFrame").objectReferenceValue = frame.GetComponent<MeshRenderer>();
            so.FindProperty("fallbackTexture").objectReferenceValue = fallback;
            so.FindProperty("rimLight").objectReferenceValue = rim;
            so.FindProperty("frameWidthMeters").floatValue = FrameWidth;
            var labels = so.FindProperty("labels");
            var defaults = DefaultLabels();
            labels.arraySize = defaults.Count;
            for (var i = 0; i < defaults.Count; i++)
            {
                var item = labels.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("textKey").stringValue = defaults[i].TextKey;
                item.FindPropertyRelative("startSeconds").floatValue = defaults[i].StartSeconds;
                item.FindPropertyRelative("endSeconds").floatValue = defaults[i].EndSeconds;
                item.FindPropertyRelative("anchor").enumValueIndex = (int)defaults[i].Anchor;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildPreview(SceneBuildContext context)
        {
            var sunObject = context.Child(context.Root.transform, "Sun · single shadow light");
            sunObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.45f, -0.5f, 0.75f), Vector3.up);
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.2f;
            var cameraObject = context.Child(context.Root.transform, "Preview Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            context.Component<UniversalAdditionalCameraData>(cameraObject).renderPostProcessing = false;
            context.Component<CameraDirector>(cameraObject);
            context.Component<SequenceDirector>(cameraObject).enabled = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.24f, 0.22f);
        }
    }
}
