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

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-018 (R4): catálogo de sequências das 14 etapas cinemáticas, as cinco sequências que só a edição possui (M14, M15, M20, M23, M27),
    /// âncoras de câmera, perfis de exposição por região e o condutor que liga fluxo, região, luz e sequência. Não toca em cena global,
    /// Boot nem BuildSettings (R1/MAR-009 montam).
    /// </summary>
    public sealed class MarsEditBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string CatalogPath = MarsPath + "/Timelines/MarsChapterSequences.asset";
        public const string LightingPath = MarsPath + "/Art/MarsLightingBindings.asset";
        public const string PrefabPath = MarsPath + "/Prefabs/MarsCameraAnchors.prefab";
        public const string PreviewScenePath = MarsPath + "/Art/Cinema/Preview/MarsEditPreview.unity";
        const string SubtitlesPath = MarsPath + "/Data/MarsSubtitles.json";

        public static string AuthoredPath(string stepId) => MarsPath + "/Timelines/MarsEdit_" + stepId + ".asset";

        // Regiões (um ou mais palcos por região; a cena do capítulo registra os objetos reais).
        public const string RegionOrigin = "mars.origin";
        public const string RegionLanding = "mars.landing";
        public const string RegionEagle = "mars.eagle";
        public const string RegionShield = "mars.shield";
        public const string RegionSpirit = "mars.spirit";
        public const string RegionValley = "mars.valley";
        public const string RegionMemorial = "mars.memorial";

        public override string Id => "mars.edit";
        public override string Owner => "R4 · MAR-018";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-018] Edit";
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-018 Reconstruir edição e câmeras")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new MarsEditBuilder());
            if (!result.Succeeded)
                throw new InvalidOperationException(result.ToString());
            Debug.Log(result.ToString());
        }

        public static void BuildFromCommandLine()
        {
            try { BuildFromMenu(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // ---------- Planos autorais (rig: sujeito na origem, +Z à frente) ----------

        struct View { public Vector3 Position, Target; public float Fov; }

        static Func<float, View> Move(Vector3 p0, Vector3 p1, Vector3 t0, Vector3 t1, float fov0, float fov1) => u =>
        {
            var e = MerLandingChoreography.Smooth(u);
            return new View { Position = Vector3.Lerp(p0, p1, e), Target = Vector3.Lerp(t0, t1, e), Fov = Mathf.Lerp(fov0, fov1, e) };
        };

        static IEnumerable<(string id, float seconds, Func<float, View> path, ShotEvent[] events)> Authored(string step)
        {
            switch (step)
            {
                case "M14":
                    yield return ("mars.cin.M14.layers", 7f, Move(new Vector3(-2.4f, 1.3f, -3.2f), new Vector3(-1.6f, 1.1f, -2.5f), new Vector3(0f, 0.25f, 0f), new Vector3(0f, 0.25f, 0f), 34f, 34f), new[] { new ShotEvent(0f, "mars.cin.evidence.layers") });
                    yield return ("mars.cin.M14.story", 7f, Move(new Vector3(2.6f, 1.9f, -4.6f), new Vector3(2.1f, 1.7f, -4f), new Vector3(0f, 0.5f, 0.6f), new Vector3(0f, 0.5f, 0.6f), 38f, 38f), new[] { new ShotEvent(0f, "mars.cin.evidence.water") });
                    break;
                case "M15":
                    yield return ("mars.cin.M15.ellipse", 5f, Move(new Vector3(0f, 2.5f, -7f), new Vector3(0f, 9f, -13f), new Vector3(0f, 0.8f, 0f), new Vector3(0f, 0.8f, 0f), 34f, 34f), new[] { new ShotEvent(0f, "mars.cin.ellipse.april") });
                    break;
                case "M20":
                    yield return ("mars.cin.M20.reveal", 4f, Move(new Vector3(-2.8f, 1f, -2.6f), new Vector3(-0.6f, 1.1f, -3f), new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0.45f, 0f), 30f, 30f), new[] { new ShotEvent(0f, "mars.cin.meteorite.reveal") });
                    yield return ("mars.cin.M20.context", 4f, Move(new Vector3(1.8f, 1.4f, -3.4f), new Vector3(2.4f, 1.9f, -4.4f), new Vector3(0f, 0.45f, 0f), new Vector3(0f, 0.45f, 0f), 36f, 36f), new[] { new ShotEvent(0f, "mars.cin.meteorite.context") });
                    break;
                case "M23":
                    yield return ("mars.cin.M23.valley", 4.5f, Move(new Vector3(-6f, 2.4f, -7f), new Vector3(-4.5f, 2.1f, -5.5f), new Vector3(0f, -1.5f, 22f), new Vector3(0f, -1.5f, 22f), 46f, 46f), new[] { new ShotEvent(0f, "mars.cin.valley.question") });
                    yield return ("mars.cin.M23.rover", 4.5f, Move(new Vector3(2.4f, 1.6f, -4.2f), new Vector3(1.8f, 1.5f, -3.6f), new Vector3(0f, 0.9f, 0f), new Vector3(0f, 0.9f, 0f), 34f, 34f), new[] { new ShotEvent(0f, "mars.cin.valley.rover") });
                    break;
                case "M27":
                    yield return ("mars.cin.M27.dust", 6f, Move(new Vector3(0f, 1.2f, -5.2f), new Vector3(0f, 1.7f, -4.6f), new Vector3(0f, 3f, 10f), new Vector3(0f, 3f, 10f), 42f, 42f), new[] { new ShotEvent(0f, "mars.cin.dust.returns") });
                    break;
            }
        }

        static SequenceDefinition BuildAuthored(MarsChapterFlow.SubtitleTable table, string step, string sequenceId)
        {
            var shots = Authored(step).Select(s =>
            {
                var count = Mathf.Max(2, Mathf.CeilToInt(s.seconds / 0.25f));
                var keys = new List<ShotKeyframe>();
                for (var i = 0; i <= count; i++)
                {
                    var v = s.path(i / (float)count);
                    keys.Add(new ShotKeyframe(s.seconds * i / count, v.Position, Quaternion.LookRotation(v.Target - v.Position, Vector3.up).eulerAngles, v.Fov));
                }

                return new ShotDefinition(s.id, s.seconds, MarsCameraAnchors.RigIdFor(step), keys, null, ShotEasing.Linear, 0.3f, s.events);
            }).ToList();
            return SequenceDefinition.Create(sequenceId, shots, MarsChapterFlow.BuildSequenceCues(table, step));
        }

        static T Store<T>(SceneBuildContext context, string path, T template) where T : Object
        {
            var value = context.Asset(path, () => template);
            if (value != template) { EditorUtility.CopySerialized(template, value); Object.DestroyImmediate(template); }
            EditorUtility.SetDirty(value);
            return value;
        }

        static float[] CutPoints(SequenceDefinition sequence)
        {
            var points = new List<float>();
            var t = 0f;
            for (var i = 0; i < sequence.Shots.Count - 1; i++)
            {
                t += sequence.Shots[i].DurationSeconds;
                points.Add(t);
            }

            return points.ToArray();
        }

        public override void Build(SceneBuildContext context)
        {
            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            if (table == null)
                throw new InvalidOperationException("MarsSubtitles.json inválido:" + ContractValidation.Describe(errors));

            var bindings = MarsChapterFlow.CreateDefaultBindings().Where(b => b.IsCinematic).ToDictionary(b => b.StepId);
            var existing = new Dictionary<string, (string path, string region)>
            {
                ["M01"] = (MarsPath + "/Timelines/MarsOrigin_M01.asset", RegionOrigin),
                ["M02"] = (MarsPath + "/Timelines/MarsOrigin_M02.asset", RegionOrigin),
                ["M03"] = (MarsPath + "/Timelines/MarsOrigin_M03.asset", RegionOrigin),
                ["M04"] = (MarsPath + "/Timelines/MarsDualLanding_M04.asset", RegionLanding),
                ["M05"] = (MarsPath + "/Timelines/MarsDualLanding_M05.asset", RegionLanding),
                ["M06"] = (MarsPath + "/Timelines/MarsDualLanding_M06.asset", RegionLanding),
                ["M22"] = (MarsPath + "/Timelines/SpiritInterlude_M22.asset", RegionSpirit),
                ["M31"] = (MarsPath + "/Timelines/MarsMemorial_M31.asset", RegionMemorial),
                ["M32"] = (MarsPath + "/Timelines/MarsMemorial_M32.asset", RegionMemorial),
            };
            var authored = new Dictionary<string, string> { ["M14"] = RegionEagle, ["M15"] = RegionEagle, ["M20"] = RegionShield, ["M23"] = RegionValley, ["M27"] = RegionValley };

            var entries = new List<MarsChapterSequenceEntry>();
            foreach (var step in bindings.Keys.OrderBy(k => k))
            {
                SequenceDefinition sequence;
                string region;
                if (existing.TryGetValue(step, out var found))
                {
                    sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(found.path);
                    region = found.region;
                    if (sequence == null)
                        throw new InvalidOperationException($"Sequência da etapa {step} ausente: {found.path}");
                }
                else if (authored.TryGetValue(step, out region))
                {
                    sequence = Store(context, AuthoredPath(step), BuildAuthored(table, step, bindings[step].SequenceId));
                }
                else
                {
                    throw new InvalidOperationException($"Etapa cinemática {step} sem sequência nem plano autoral.");
                }

                entries.Add(new MarsChapterSequenceEntry(step, sequence, region, bindings[step].ReferenceSeconds, CutPoints(sequence)));
            }

            var catalog = context.Asset(CatalogPath, ScriptableObject.CreateInstance<MarsChapterSequenceCatalog>);
            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);

            var lighting = context.Asset(LightingPath, ScriptableObject.CreateInstance<MarsLightingBindings>);
            lighting.SetProfiles(Profiles());
            EditorUtility.SetDirty(lighting);

            var prefab = context.Prefab(PrefabPath, root => BuildPrefab(root, catalog, lighting));
            context.PrefabInstance(context.Root.transform, prefab, "MarsCameraAnchors");
            BuildPreview(context);
            AssetDatabase.SaveAssets();
        }

        static List<MarsLightingProfile> Profiles()
        {
            MarsLightingProfile P(string id, float intensity, Color sun, Vector3 euler, Color ambient, Color background) =>
                new MarsLightingProfile { regionId = id, sunIntensity = intensity, sunColor = sun, sunEulerDegrees = euler, ambientColor = ambient, backgroundColor = background };

            return new List<MarsLightingProfile>
            {
                P(RegionOrigin, 1.5f, new Color(0.96f, 0.95f, 0.92f), new Vector3(28f, 105f, 0f), new Color(0.22f, 0.22f, 0.25f), new Color(0.015f, 0.015f, 0.02f)),
                P(RegionLanding, 2.1f, new Color(1f, 0.79f, 0.57f), new Vector3(24f, 323f, 0f), new Color(0.38f, 0.34f, 0.32f), new Color(0.62f, 0.47f, 0.37f)),
                P(RegionEagle, 2.1f, new Color(1f, 0.79f, 0.57f), new Vector3(24f, 323f, 0f), new Color(0.38f, 0.34f, 0.32f), new Color(0.66f, 0.5f, 0.4f)),
                P(RegionShield, 1.5f, new Color(1f, 0.92f, 0.82f), new Vector3(40f, 160f, 0f), new Color(0.34f, 0.27f, 0.23f), new Color(0.66f, 0.5f, 0.4f)),
                P(RegionValley, 1.4f, new Color(1f, 0.92f, 0.82f), new Vector3(38f, 150f, 0f), new Color(0.34f, 0.28f, 0.24f), new Color(0.6f, 0.5f, 0.42f)),
                P(RegionSpirit, 1.3f, new Color(1f, 0.93f, 0.85f), new Vector3(36f, 150f, 0f), new Color(0.3f, 0.26f, 0.24f), new Color(0.02f, 0.02f, 0.03f)),
                P(RegionMemorial, 1.2f, new Color(1f, 0.93f, 0.85f), new Vector3(35f, 150f, 0f), new Color(0.28f, 0.24f, 0.22f), Color.black),
            };
        }

        static void BuildPrefab(GameObject root, MarsChapterSequenceCatalog catalog, MarsLightingBindings lighting)
        {
            for (var i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            foreach (var c in root.GetComponents<MonoBehaviour>())
                Object.DestroyImmediate(c);

            var anchors = root.AddComponent<MarsCameraAnchors>();
            var rigs = new List<Transform>();
            foreach (var step in new[] { "M14", "M15", "M20", "M23", "M27" })
            {
                var go = new GameObject(MarsCameraAnchors.RigIdFor(step));
                go.transform.SetParent(root.transform, false);
                rigs.Add(go.transform);
            }

            var so = new SerializedObject(anchors);
            var prop = so.FindProperty("rigs");
            prop.arraySize = rigs.Count;
            for (var i = 0; i < rigs.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = rigs[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var conductor = root.AddComponent<MarsSequenceConductor>();
            var cso = new SerializedObject(conductor);
            cso.FindProperty("catalog").objectReferenceValue = catalog;
            cso.FindProperty("lighting").objectReferenceValue = lighting;
            cso.FindProperty("anchors").objectReferenceValue = anchors;
            cso.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            foreach (var conductor in context.Root.GetComponentsInChildren<MarsSequenceConductor>(true))
                errors.AddRange(conductor.ValidateSetup());
        }

        static void BuildPreview(SceneBuildContext context)
        {
            var sunObject = context.Child(context.Root.transform, "Sun · single shadow light");
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var cameraObject = context.Child(context.Root.transform, "Preview Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            context.Component<UniversalAdditionalCameraData>(cameraObject).renderPostProcessing = false;
            context.Component<CameraDirector>(cameraObject);
            context.Component<SequenceDirector>(cameraObject).enabled = false;
        }
    }
}
