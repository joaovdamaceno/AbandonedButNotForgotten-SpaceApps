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
using static Abandoned.SpaceApps.SpiritInterludeChoreography;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-017 (R4): M22 (45 s), Spirit com cinco imagens oficiais NASA/JPL, odômetro, rover inclinado no inverno e Opportunity quieta, por
    /// prefab e duas SequenceDefinitions (a principal com cues da tabela; a dos cartões só com os planos da câmera do Spirit).
    /// </summary>
    public sealed class SpiritInterludeBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string ArtPath = MarsPath + "/Art/SpiritArchive";
        public const string GeneratedPath = ArtPath + "/Generated";
        public const string PrefabPath = MarsPath + "/Prefabs/SpiritInterludeStage.prefab";
        public const string DataPath = ArtPath + "/SpiritInterlude.asset";
        public const string MainSequencePath = MarsPath + "/Timelines/SpiritInterlude_M22.asset";
        public const string CardsSequencePath = MarsPath + "/Timelines/SpiritInterlude_Cards.asset";
        public const string PreviewScenePath = ArtPath + "/Preview/SpiritInterludePreview.unity";
        const string SubtitlesPath = MarsPath + "/Data/MarsSubtitles.json";

        public static readonly Vector3 SpiritOffset = new Vector3(4000f, 0f, 0f);
        const float Hfov = 50f;

        public static readonly (string file, float width, float aspect)[] Cards =
        {
            ("Archive_HusbandHill_PIA17760.jpg", 8f, 1024f / 576f),
            ("Archive_Comanche_PIA13175.jpg", 8f, 907f / 678f),
            ("Archive_Silica_PIA11758.jpg", 8f, 6174f / 2818f),
            ("Archive_HomePlate_PIA08064.jpg", 5f, 1f),
            ("Archive_Troy_PIA12142.jpg", 8f, 5712f / 1067f),
        };

        public override string Id => "mars.spiritInterlude";
        public override string Owner => "R4 · MAR-017";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-017] Spirit Interlude";
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-017 Reconstruir Spirit em 45 s")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new SpiritInterludeBuilder());
            if (!result.Succeeded)
                throw new InvalidOperationException(result.ToString());
            Debug.Log(result.ToString());
        }

        public static void BuildFromCommandLine()
        {
            try { BuildFromMenu(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static float CardX(int index) => index * 40f;
        static readonly Vector3 OdometerCenter = new Vector3(200f, 0f, 0f);
        static readonly Vector3 SlopeCenter = new Vector3(300f, 0f, 0f);
        static float Distance(float width) => width * 0.5f / Mathf.Tan(Hfov * 0.5f * Mathf.Deg2Rad);

        struct View { public Vector3 Position, Target; public float Fov; public bool Fixed; }

        static float SplitVfov => 2f * Mathf.Atan(Mathf.Tan(Hfov * 0.5f * Mathf.Deg2Rad) / (16f / 9f / 0.5f)) * Mathf.Rad2Deg;

        static IEnumerable<(string id, float seconds, Func<float, View> path)> CardShots()
        {
            Vector3 L(Vector3 a, Vector3 b, float u) => Vector3.Lerp(a, b, MerLandingChoreography.Smooth(u));
            View Pan(int card, float h, float panHalf, float u)
            {
                var d = Distance(Cards[card].width);
                var y = Mathf.Lerp(panHalf, -panHalf, MerLandingChoreography.Smooth(u));
                var p = new Vector3(CardX(card), y, -Mathf.Lerp(d, d * 0.96f, u));
                return new View { Position = p, Target = p + Vector3.forward, Fov = SplitVfov, Fixed = true };
            }

            float Half(int card)
            {
                var visible = 2f * Distance(Cards[card].width) * Mathf.Tan(SplitVfov * 0.5f * Mathf.Deg2Rad);
                var height = Cards[card].width / Cards[card].aspect;
                return Mathf.Max(0f, (height - visible) * 0.5f);
            }

            yield return ("mars.spirit.husbandHill", 5f, u => Pan(0, 0f, Half(0), u));
            yield return ("mars.spirit.comanche", 6f, u => Pan(1, 0f, Half(1), u));
            yield return ("mars.spirit.silica", 6f, u => Pan(2, 0f, Half(2), u));
            yield return ("mars.spirit.homePlate", 5f, u => Pan(3, 0f, Half(3), u));
            var d5 = Distance(8f) * 1.05f;
            yield return ("mars.spirit.odometer", 5f, u => { var p = OdometerCenter + new Vector3(0f, 0f, -Mathf.Lerp(d5, d5 * 0.97f, u)); return new View { Position = p, Target = p + Vector3.forward, Fov = SplitVfov, Fixed = true }; });
            yield return ("mars.spirit.troy", 5f, u => Pan(4, 0f, Half(4), u));
            var rover = SlopeCenter + new Vector3(0f, 0.9f, 0f);
            yield return ("mars.spirit.winter", 7f, u => new View { Position = SlopeCenter + L(new Vector3(-6f, 1.8f, -7f), new Vector3(-3.5f, 1.3f, -4.5f), u), Target = rover, Fov = Mathf.Lerp(22f, 20f, u) });
            yield return ("mars.spirit.lastContact", 6f, u => new View { Position = SlopeCenter + L(new Vector3(-3.5f, 1.3f, -4.5f), new Vector3(-7f, 2f, -7.5f), u), Target = rover, Fov = Mathf.Lerp(20f, 24f, u) });
        }

        static List<ShotKeyframe> Bake(float seconds, Func<float, View> path)
        {
            var count = Mathf.Max(2, Mathf.CeilToInt(seconds / 0.25f));
            var keys = new List<ShotKeyframe>();
            for (var i = 0; i <= count; i++)
            {
                var v = path(i / (float)count);
                var forward = v.Target - v.Position;
                keys.Add(new ShotKeyframe(seconds * i / count, v.Position, Quaternion.LookRotation(forward, Vector3.up).eulerAngles, v.Fov));
            }

            return keys;
        }

        static List<MarsLandingLabel> DefaultLabels()
        {
            MarsLandingLabel L(string key, float a, float b, MarsLandingLabelAnchor anchor = MarsLandingLabelAnchor.Full) => new MarsLandingLabel(key, a, b, anchor);
            return new List<MarsLandingLabel>
            {
                L("MAR.SPI.01", 0.3f, 5f), L("MAR.SPI.CR1", 0.3f, 5f),
                L("MAR.SPI.02", 5.3f, 11f), L("MAR.SPI.CR2", 5.3f, 11f),
                L("MAR.SPI.03", 11.3f, 17f), L("MAR.SPI.CR3", 11.3f, 17f),
                L("MAR.SPI.04", 17.3f, 22f), L("MAR.SPI.CR4", 17.3f, 22f),
                L("MAR.SPI.05", 22.3f, 27f),
                L("MAR.SPI.06", 27.3f, 29.5f), L("MAR.SPI.07", 29.5f, 32f), L("MAR.SPI.CR5", 27.3f, 32f),
                L("MAR.SPI.08", 32.3f, 39f),
                L("MAR.SPI.09", 39.3f, 41f), L("MAR.SPI.10", 41f, 42.7f),
                L("MAR.SPI.12", 42.7f, 45f, MarsLandingLabelAnchor.UpperHalf), L("MAR.SPI.11", 43.5f, 45f, MarsLandingLabelAnchor.LowerHalf),
            };
        }

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

            var data = context.Asset(DataPath, ScriptableObject.CreateInstance<SpiritInterludeData>);
            data.Configure(new[] { "MAR.SPI.CR1", "MAR.SPI.CR2", "MAR.SPI.CR3", "MAR.SPI.CR4", "MAR.SPI.CR5" }, DefaultLabels());
            EditorUtility.SetDirty(data);

            var cardShots = CardShots().Select(s => new ShotDefinition(s.id, s.seconds, "mars.spirit.cards", Bake(s.seconds, s.path), null, ShotEasing.Linear, 0f)).ToList();
            var cards = Store(context, CardsSequencePath, SequenceDefinition.Create("mars.spirit.cards", cardShots));

            var opportunity = new ShotDefinition("mars.spirit.opportunity.quiet", TotalSeconds, SpiritInterludeDirector.OpportunityRigId,
                Bake(TotalSeconds, u =>
                {
                    var t = u * TotalSeconds;
                    var aspect = OpportunityAspect(t);
                    var splitAspect = 16f / 9f / 0.5f;
                    var v0 = 22f;
                    var fov = 2f * Mathf.Atan(Mathf.Tan(v0 * 0.5f * Mathf.Deg2Rad) * splitAspect / aspect) * Mathf.Rad2Deg;
                    return new View { Position = Vector3.Lerp(new Vector3(-6f, 1.6f, -7f), new Vector3(-5f, 1.5f, -6f), u), Target = new Vector3(0f, 1f, 0f), Fov = fov };
                }), null, ShotEasing.Linear, 0f);
            Store(context, MainSequencePath, SequenceDefinition.Create(data.SequenceId, new[] { opportunity }, MarsChapterFlow.BuildSequenceCues(table, "M22")));

            var m = BuildMaterials();
            var prefab = context.Prefab(PrefabPath, root => BuildPrefab(root, m, data, cards));
            context.PrefabInstance(context.Root.transform, prefab, "SpiritInterludeStage");
            BuildPreview(context);
            AssetDatabase.SaveAssets();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            foreach (var d in context.Root.GetComponentsInChildren<SpiritInterludeDirector>(true))
                errors.AddRange(d.ValidateSetup());
        }

        sealed class Mats { public Material[] cards; public Material cover, bar, track, ground, slope, veil; }

        static Mats BuildMaterials()
        {
            var m = new Mats { cards = new Material[Cards.Length] };
            for (var i = 0; i < Cards.Length; i++)
            {
                var path = GeneratedPath + "/Card" + i + ".mat";
                Directory.CreateDirectory(GeneratedPath);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "/" + Cards[i].file);
                if (texture == null) throw new InvalidOperationException("Imagem ausente: " + Cards[i].file);
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(material);
                m.cards[i] = material;
            }

            Material Make(string name, string shader, Color color, bool transparent)
            {
                var path = GeneratedPath + "/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(material, path); }
                material.SetColor("_BaseColor", color);
                if (transparent)
                {
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_Blend", 0f);
                    BaseShaderGUI.SetupMaterialBlendMode(material);
                    material.SetShaderPassEnabled("ShadowCaster", false);
                }

                EditorUtility.SetDirty(material);
                return material;
            }

            m.cover = Make("SilicaCover", "Universal Render Pipeline/Unlit", new Color(0.02f, 0.02f, 0.025f, 1f), false);
            m.bar = Make("OdometerBar", "Universal Render Pipeline/Unlit", new Color(1f, 0.78f, 0.35f, 1f), false);
            m.track = Make("OdometerTrack", "Universal Render Pipeline/Unlit", new Color(0.25f, 0.25f, 0.28f, 1f), false);
            m.ground = Make("QuietGround", "Universal Render Pipeline/Lit", new Color(0.5f, 0.36f, 0.27f, 1f), false);
            m.slope = Make("WinterSlope", "Universal Render Pipeline/Lit", new Color(0.52f, 0.4f, 0.32f, 1f), false);
            m.veil = Make("DarkVeil", "Universal Render Pipeline/Unlit", new Color(0f, 0f, 0.01f, 0f), true);
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

        static void BuildPrefab(GameObject root, Mats m, SpiritInterludeData data, SequenceDefinition cards)
        {
            for (var i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            foreach (var c in root.GetComponents<MonoBehaviour>())
                Object.DestroyImmediate(c);

            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            var spirit = Child(root.transform, "SpiritStage");
            spirit.localPosition = SpiritOffset;
            for (var i = 0; i < Cards.Length; i++)
                Part(spirit, "Card_" + Path.GetFileNameWithoutExtension(Cards[i].file), quad, m.cards[i], new Vector3(CardX(i), 0f, 0f), new Vector3(Cards[i].width, Cards[i].width / Cards[i].aspect, 1f));
            var silica = Cards[2];
            var cover = Part(spirit, "Cover_SilicaDrag", quad, m.cover, new Vector3(CardX(2), 0f, -0.02f), new Vector3(silica.width, silica.width / silica.aspect, 1f));

            var odometer = Child(spirit, "Odometer");
            odometer.localPosition = OdometerCenter;
            Part(odometer, "Track", cube, m.track, Vector3.zero, new Vector3(8f, 0.06f, 0.05f));
            for (var i = 0; i <= 6; i++)
                Part(odometer, "Tick_" + (2004 + i), cube, m.track, new Vector3(-4f + 8f * i / 6f, 0f, -0.01f), new Vector3(0.05f, 0.5f, 0.05f));
            var bar = Part(odometer, "Bar_Km", cube, m.bar, new Vector3(-4f, 0f, -0.03f), new Vector3(0.001f, 0.28f, 0.1f));

            var slope = Child(spirit, "WinterSlope");
            slope.localPosition = SlopeCenter;
            slope.localRotation = Quaternion.Euler(RoverTiltDegrees, 0f, 0f);
            Part(slope, "Ground", cube, m.slope, new Vector3(0f, -0.05f, 0f), new Vector3(40f, 0.1f, 40f), true);
            var roverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarsPath + "/Prefabs/SpiritVisual.prefab");
            if (roverPrefab == null) throw new InvalidOperationException("SpiritVisual.prefab ausente.");
            var rover = (GameObject)PrefabUtility.InstantiatePrefab(roverPrefab);
            rover.name = "Spirit_Rover";
            rover.transform.SetParent(slope, false);

            var opportunity = Child(root.transform, "OpportunityStage");
            Part(opportunity, "Ground", cube, m.ground, new Vector3(0f, -0.05f, 0f), new Vector3(60f, 0.1f, 60f), true);
            var oppPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarsPath + "/Prefabs/OpportunityVisual.prefab");
            if (oppPrefab == null) throw new InvalidOperationException("OpportunityVisual.prefab ausente.");
            var opp = (GameObject)PrefabUtility.InstantiatePrefab(oppPrefab);
            opp.name = "Opportunity_Rover";
            opp.transform.SetParent(opportunity, false);

            var cameraObject = Child(root.transform, "SpiritCamera");
            var camera = cameraObject.gameObject.AddComponent<Camera>();
            camera.fieldOfView = SplitVfov;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            camera.enabled = false;
            cameraObject.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            var veil = Part(cameraObject, "DarkVeil", quad, m.veil, new Vector3(0f, 0f, 0.3f), new Vector3(2f, 2f, 1f));

            var director = root.AddComponent<SpiritInterludeDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("spiritStage").objectReferenceValue = spirit;
            so.FindProperty("opportunityStage").objectReferenceValue = opportunity;
            so.FindProperty("spiritCamera").objectReferenceValue = camera;
            so.FindProperty("cardShots").objectReferenceValue = cards;
            so.FindProperty("silicaCover").objectReferenceValue = cover;
            so.FindProperty("odometerBar").objectReferenceValue = bar;
            so.FindProperty("darkVeil").objectReferenceValue = veil.GetComponent<MeshRenderer>();
            so.FindProperty("spiritRover").objectReferenceValue = rover.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildPreview(SceneBuildContext context)
        {
            var sunObject = context.Child(context.Root.transform, "Sun · single shadow light");
            sunObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.5f, -0.5f, 0.7f), Vector3.up);
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.3f;
            var cameraObject = context.Child(context.Root.transform, "Preview Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            camera.fieldOfView = 22f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            context.Component<UniversalAdditionalCameraData>(cameraObject).renderPostProcessing = false;
            context.Component<CameraDirector>(cameraObject);
            context.Component<SequenceDirector>(cameraObject).enabled = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.26f, 0.24f);
        }
    }
}
