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
using static Abandoned.SpaceApps.MarsOriginChoreography;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-013 (R4): laboratório com os dois rovers, globo com os dois destinos e dois lançamentos em sequência (M01–M03, 25 s), por
    /// prefab e três SequenceDefinitions. Tudo procedural; não toca em Boot, BuildSettings nem scripts de jogo.
    /// </summary>
    public sealed class MarsOriginBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string ArtPath = MarsPath + "/Art/Origin";
        public const string GeneratedPath = ArtPath + "/Generated";
        public const string PrefabPath = MarsPath + "/Prefabs/MarsPreparationStage.prefab";
        public const string PreviewScenePath = ArtPath + "/Preview/MarsOriginPreview.unity";
        const string SubtitlesPath = MarsPath + "/Data/MarsSubtitles.json";

        public static readonly Vector3 MapOffset = new Vector3(500f, 0f, 0f);
        public static readonly Vector3 LaunchOffset = new Vector3(1000f, 0f, 0f);
        public static string SequencePath(string stepId) => MarsPath + "/Timelines/MarsOrigin_" + stepId + ".asset";

        public override string Id => "mars.origin";
        public override string Owner => "R4 · MAR-013";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-013] Origin";
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-013 Reconstruir laboratório, mapa e lançamentos")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new MarsOriginBuilder());
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

        sealed class Mats
        {
            public Material floor, steel, plate, cable, globe, glow, hematite, rocket, flame;
        }

        // ---------- Câmera ----------

        struct View
        {
            public Vector3 Position, Target;
            public float Fov;
        }

        static IEnumerable<(string id, float seconds, Func<float, View> path)> Shots(string sequence)
        {
            Vector3 L(Vector3 a, Vector3 b, float u) => Vector3.Lerp(a, b, MerLandingChoreography.Smooth(u));
            var globe = MapOffset;
            var launch = LaunchOffset;
            if (sequence == MarsOriginDirector.SequenceM01)
            {
                yield return ("mars.origin.lab.detail", 4f, u => new View { Position = L(new Vector3(-3.6f, 1.2f, -1.6f), new Vector3(-0.6f, 1.3f, -1.8f), u), Target = L(new Vector3(-2.9f, 1.05f, 0f), new Vector3(-1.2f, 1.25f, 0f), u), Fov = Mathf.Lerp(32f, 30f, u) });
                yield return ("mars.origin.lab.wide", 4f, u => new View { Position = L(new Vector3(-2f, 1.6f, -6.5f), new Vector3(2.4f, 1.7f, -6.5f), u), Target = new Vector3(0f, 1.4f, 0f), Fov = 42f });
            }
            else if (sequence == MarsOriginDirector.SequenceM02)
            {
                yield return ("mars.origin.map.gusev", 4f, u => new View { Position = globe + L(new Vector3(-2.5f, 1.2f, -8f), new Vector3(-1.8f, 1f, -7.6f), u), Target = globe, Fov = 34f });
                yield return ("mars.origin.map.meridiani", 4f, u => new View { Position = globe + L(new Vector3(2.2f, -0.8f, -6.2f), new Vector3(1.4f, -0.4f, -5.8f), u), Target = globe + new Vector3(0f, 0f, -0.8f), Fov = 28f });
                yield return ("mars.origin.map.hematite", 4f, u => new View { Position = globe + L(new Vector3(0.4f, 0.3f, -4.1f), new Vector3(0.2f, 0.1f, -3.6f), u), Target = globe + new Vector3(0f, 0f, -2f), Fov = 30f });
            }
            else
            {
                Vector3 Follow(float startTime, Vector3 from, Vector3 to, float x, float u, bool second)
                {
                    var t = LaunchStart + startTime + u * 2.5f;
                    var pose = Sample(t);
                    var h = second ? pose.RocketBHeight : pose.RocketAHeight;
                    return launch + L(from, to, u) + new Vector3(0f, h * 0.5f, 0f);
                }

                yield return ("mars.origin.launch.first", 2.5f, u =>
                {
                    var h = Sample(LaunchStart + u * 2.5f).RocketAHeight;
                    return new View { Position = Follow(0f, new Vector3(-58f, 10f, -40f), new Vector3(-52f, 22f, -46f), -25f, u, false), Target = launch + new Vector3(-RocketSpacingMeters * 0.5f, h + 8f, 0f), Fov = 38f };
                });
                yield return ("mars.origin.launch.second", 2.5f, u =>
                {
                    var h = Sample(LaunchStart + 2.5f + u * 2.5f).RocketBHeight;
                    return new View { Position = Follow(2.5f, new Vector3(58f, 10f, -40f), new Vector3(52f, 22f, -46f), 25f, u, true), Target = launch + new Vector3(RocketSpacingMeters * 0.5f, h + 8f, 0f), Fov = 38f };
                });
            }
        }

        static List<ShotKeyframe> Bake(float seconds, Func<float, View> path)
        {
            var count = Mathf.Max(2, Mathf.CeilToInt(seconds / 0.25f));
            var keys = new List<ShotKeyframe>();
            for (var i = 0; i <= count; i++)
            {
                var u = i / (float)count;
                var v = path(u);
                var forward = v.Target - v.Position;
                keys.Add(new ShotKeyframe(seconds * u, v.Position, Quaternion.LookRotation(forward, Vector3.up).eulerAngles, v.Fov));
            }

            return keys;
        }

        static List<MarsLandingLabel> DefaultLabels() => new List<MarsLandingLabel>
        {
            new MarsLandingLabel("MAR.ORI.01", 4.4f, 8f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.02", 4.8f, 8f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.03", 8.4f, 12f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.04", 12.4f, 16f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.05", 16.4f, 20f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.06", 20.2f, 22.5f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.07", 22.7f, 25f, MarsLandingLabelAnchor.Full),
        };

        static SequenceDefinition BuildSequence(MarsChapterFlow.SubtitleTable table, string id, string step, Dictionary<string, string> events)
        {
            var shots = new List<ShotDefinition>();
            foreach (var (shotId, seconds, path) in Shots(id))
            {
                var evs = events.Where(p => p.Key.StartsWith(shotId + "|")).Select(p => new ShotEvent(float.Parse(p.Key.Split('|')[1], System.Globalization.CultureInfo.InvariantCulture), p.Value));
                shots.Add(new ShotDefinition(shotId, seconds, MarsOriginDirector.RigId, Bake(seconds, path), null, ShotEasing.Linear, 0f, evs));
            }

            return SequenceDefinition.Create(id, shots, MarsChapterFlow.BuildSequenceCues(table, step));
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

        public override void Build(SceneBuildContext context)
        {
            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            if (table == null)
                throw new InvalidOperationException("MarsSubtitles.json inválido:" + ContractValidation.Describe(errors));

            Store(context, SequencePath("M01"), BuildSequence(table, MarsOriginDirector.SequenceM01, "M01", new Dictionary<string, string>()));
            Store(context, SequencePath("M02"), BuildSequence(table, MarsOriginDirector.SequenceM02, "M02", new Dictionary<string, string>
            {
                ["mars.origin.map.gusev|0"] = "mars.origin.gusev.glow",
                ["mars.origin.map.meridiani|0"] = "mars.origin.meridiani.glow",
                ["mars.origin.map.hematite|0"] = "mars.origin.hematite.glow",
            }));
            Store(context, SequencePath("M03"), BuildSequence(table, MarsOriginDirector.SequenceM03, "M03", new Dictionary<string, string>
            {
                ["mars.origin.launch.first|0.3"] = "mars.origin.launch.first",
                ["mars.origin.launch.second|0.3"] = "mars.origin.launch.second",
            }));

            var m = BuildMaterials(context);
            var prefab = context.Prefab(PrefabPath, root => BuildPrefab(root, m));
            context.PrefabInstance(context.Root.transform, prefab, "MarsPreparationStage").transform.localPosition = Vector3.zero;
            BuildPreview(context);
            AssetDatabase.SaveAssets();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            foreach (var director in context.Root.GetComponentsInChildren<MarsOriginDirector>(true))
                errors.AddRange(director.ValidateSetup());
        }

        // ---------- Materiais ----------

        static float T(float u, float v, float s) => G.TileNoise(u, v, s);
        static float Frac(float x) => x - Mathf.Floor(x);

        static Color GlobePixel(float u, float v)
        {
            // u = longitude/360, v = (latitude+90)/180. Estilizado: não é o mapa de hematita do TES.
            var lat = v * 180f - 90f;
            var c = Color.Lerp(new Color(0.55f, 0.32f, 0.2f), new Color(0.72f, 0.46f, 0.3f), T(u, v, 6f));
            c = Color.Lerp(c, new Color(0.32f, 0.2f, 0.15f), Mathf.Clamp01(T(u, v, 3f) * 1.6f - 0.7f) * 0.8f);
            c *= 0.92f + 0.14f * T(u, v, 24f);
            c = Color.Lerp(c, new Color(0.9f, 0.88f, 0.84f), Mathf.Clamp01((Mathf.Abs(lat) - 76f) / 6f));
            var lon = u * 360f;
            var line = Mathf.Min(Mathf.Abs(Frac(lon / 30f + 0.5f) - 0.5f) * 30f * Mathf.Cos(lat * Mathf.Deg2Rad) / 360f * 100f, Mathf.Abs(Frac((lat + 90f) / 30f + 0.5f) - 0.5f) * 30f / 180f * 100f);
            return line < 0.35f ? Color.Lerp(c, new Color(0.95f, 0.85f, 0.7f), 0.35f) : c;
        }

        static Color FlamePixel(float u, float v)
        {
            var alpha = Mathf.Pow(1f - v, 1.2f) * (0.8f + 0.2f * T(u, v, 5f));
            return new Color(1f, Mathf.Lerp(0.92f, 0.5f, v), Mathf.Lerp(0.6f, 0.2f, v), alpha);
        }

        static Mats BuildMaterials(SceneBuildContext context)
        {
            var globeTex = Texture("OriginGlobe", 512, 256, true, GlobePixel);
            var flameTex = Texture("OriginFlame", 64, 64, false, FlamePixel);
            var m = new Mats
            {
                floor = context.Material(GeneratedPath + "/LabFloor.mat", "Universal Render Pipeline/Lit"),
                steel = context.Material(GeneratedPath + "/LabSteel.mat", "Universal Render Pipeline/Lit"),
                plate = context.Material(GeneratedPath + "/LabPlate.mat", "Universal Render Pipeline/Lit"),
                cable = context.Material(GeneratedPath + "/LabCable.mat", "Universal Render Pipeline/Lit"),
                globe = context.Material(GeneratedPath + "/Globe.mat", "Universal Render Pipeline/Lit"),
                glow = context.Material(GeneratedPath + "/MarkerGlow.mat", "Universal Render Pipeline/Unlit"),
                hematite = context.Material(GeneratedPath + "/HematitePatch.mat", "Universal Render Pipeline/Unlit"),
                rocket = context.Material(GeneratedPath + "/RocketSilhouette.mat", "Universal Render Pipeline/Lit"),
                flame = context.Material(GeneratedPath + "/LaunchFlame.mat", "Universal Render Pipeline/Unlit"),
            };
            Solid(m.floor, new Color(0.16f, 0.17f, 0.17f), 0.1f, 0f);
            Solid(m.steel, new Color(0.45f, 0.47f, 0.48f), 0.45f, 0.6f);
            Solid(m.plate, new Color(0.82f, 0.8f, 0.74f), 0.2f, 0f);
            Solid(m.cable, new Color(0.08f, 0.08f, 0.09f), 0.3f, 0f);
            Solid(m.rocket, new Color(0.1f, 0.1f, 0.12f), 0.3f, 0.2f);
            m.globe.SetTexture("_BaseMap", globeTex);
            m.globe.SetColor("_BaseColor", Color.white);
            m.globe.SetFloat("_Smoothness", 0.08f);
            Transparent(m.glow, null, new Color(1f, 0.82f, 0.4f, 1f));
            Transparent(m.hematite, null, new Color(1f, 0.7f, 0.3f, 0.55f));
            Transparent(m.flame, flameTex, new Color(1f, 0.7f, 0.35f, 0.9f));
            return m;
        }

        static void Solid(Material m, Color color, float smoothness, float metallic)
        {
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
        }

        static void Transparent(Material m, Texture2D map, Color color)
        {
            if (map != null)
                m.SetTexture("_BaseMap", map);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", 0f);
            BaseShaderGUI.SetupMaterialBlendMode(m);
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
        }

        static Texture2D Texture(string name, int width, int height, bool srgb, Func<float, float, Color> pixel)
        {
            var path = GeneratedPath + "/" + name + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, !srgb);
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = pixel((x + 0.5f) / width, (y + 0.5f) / height);

            texture.SetPixels(pixels);
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.maxTextureSize != Mathf.Max(width, height) || !importer.mipmapEnabled))
            {
                importer.maxTextureSize = Mathf.Max(width, height);
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = !srgb || name.Contains("Flame");
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

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

        static G.Draft Cap(Vector3 direction, float radius, float degrees)
        {
            var d = new G.Draft();
            var up = Mathf.Abs(direction.y) > 0.9f ? Vector3.right : Vector3.up;
            var tangent = Vector3.Cross(direction, up).normalized;
            var bitangent = Vector3.Cross(direction, tangent);
            const int rings = 4;
            const int segments = 28;
            d.Add(direction * radius, new Vector2(0.5f, 0.5f));
            for (var r = 1; r <= rings; r++)
            {
                var a = degrees * Mathf.Deg2Rad * r / rings;
                for (var s = 0; s < segments; s++)
                {
                    var phi = s * Mathf.PI * 2f / segments;
                    var dir = direction * Mathf.Cos(a) + (tangent * Mathf.Cos(phi) + bitangent * Mathf.Sin(phi)) * Mathf.Sin(a);
                    d.Add(dir * radius, new Vector2(0.5f + 0.5f * r / rings * Mathf.Cos(phi), 0.5f + 0.5f * r / rings * Mathf.Sin(phi)));
                }
            }

            void Tri(int a, int b, int c)
            {
                var n = Vector3.Cross(d.Vertices[b] - d.Vertices[a], d.Vertices[c] - d.Vertices[a]);
                if (Vector3.Dot(n, d.Vertices[a]) < 0f) { var t = b; b = c; c = t; }
                d.Tri(a, b, c);
            }

            for (var s = 0; s < segments; s++)
                Tri(0, 1 + s, 1 + (s + 1) % segments);
            for (var r = 1; r < rings; r++)
                for (var s = 0; s < segments; s++)
                {
                    var n = (s + 1) % segments;
                    var a = 1 + (r - 1) * segments;
                    var b = 1 + r * segments;
                    Tri(a + s, b + s, a + n);
                    Tri(a + n, b + s, b + n);
                }

            return d;
        }

        // ---------- Prefab ----------

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, bool shadows = true)
        {
            var t = Child(parent, name);
            t.localPosition = position;
            t.localScale = scale;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return t;
        }

        static void Link(Transform parent, string name, Mesh cylinder, Material material, Vector3 a, Vector3 b, float radius)
        {
            var t = Part(parent, name, cylinder, material, (a + b) * 0.5f, new Vector3(radius * 2f, (b - a).magnitude * 0.5f, radius * 2f), false);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        }

        static void BuildPrefab(GameObject root, Mats m)
        {
            for (var i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            foreach (var component in root.GetComponents<MonoBehaviour>())
                Object.DestroyImmediate(component);

            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cylinder = MerLandingGeometry.Cylinder(0.5f, 2f, 12);
            var cylinderMesh = SaveMesh("OriginCylinder", cylinder);

            // Laboratório: cada peça tem função (piso, bancada, apoio, rack de teste, cabo, placa).
            var lab = Child(root.transform, "Lab");
            Part(lab, "Floor", cube, m.floor, new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 9f), false);
            foreach (var (name, x, rover) in new[] { ("Spirit", -2.2f, "SpiritVisual"), ("Opportunity", 2.2f, "OpportunityVisual") })
            {
                Part(lab, "Bench_" + name, cube, m.steel, new Vector3(x, 0.4f, 0f), new Vector3(2.6f, 0.8f, 2.2f));
                foreach (var corner in new[] { new Vector2(-0.8f, -0.55f), new Vector2(0.8f, -0.55f), new Vector2(-0.8f, 0.55f), new Vector2(0.8f, 0.55f) })
                    Part(lab, "Support_" + name, cylinderMesh, m.steel, new Vector3(x + corner.x, 0.86f, corner.y), new Vector3(0.12f, 0.06f, 0.12f));
                var plate = Part(lab, "Plate_" + name, cube, m.plate, new Vector3(x, 0.55f, -1.2f), new Vector3(1.5f, 0.28f, 0.04f));
                plate.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarsPath + "/Prefabs/" + rover + ".prefab");
                if (prefab == null)
                    throw new InvalidOperationException("Prefab do rover ausente: " + rover);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Rover_" + name;
                instance.transform.SetParent(lab, false);
                instance.transform.localPosition = new Vector3(x, 0.92f, 0f);
            }

            Part(lab, "Rack_Test", cube, m.steel, new Vector3(0f, 0.7f, 1.5f), new Vector3(0.7f, 1.4f, 0.5f));
            Link(lab, "Cable_Spirit", cylinderMesh, m.cable, new Vector3(-0.3f, 1.15f, 1.3f), new Vector3(-1.9f, 1.25f, -0.35f), 0.02f);
            Link(lab, "Cable_Opportunity", cylinderMesh, m.cable, new Vector3(0.3f, 1.15f, 1.3f), new Vector3(1.9f, 1.25f, -0.35f), 0.02f);

            // Mapa: globo com os dois destinos; marcadores e mancha de hematita giram junto.
            var map = Child(root.transform, "Map");
            map.localPosition = MapOffset;
            var globe = Part(map, "Globe", SaveMesh("OriginGlobe", MerLandingGeometry.Sphere(1f, 48, 24)), m.globe, Vector3.zero, Vector3.one * (GlobeRadiusMeters * 2f), false);
            var markerMesh = SaveMesh("OriginMarker", MerLandingGeometry.Sphere(1f, 16, 10));
            Renderer Marker(string name, Vector2 latLon)
            {
                var t = Part(globe, name, markerMesh, m.glow, MarsOriginChoreography.Direction(latLon) * 0.505f, Vector3.one * 0.07f, false);
                return t.GetComponent<MeshRenderer>();
            }

            var gusev = Marker("Marker_Gusev", Gusev);
            var meridiani = Marker("Marker_Meridiani", Meridiani);
            var patch = Part(globe, "Patch_Hematite", SaveMesh("OriginHematite", Cap(MarsOriginChoreography.Direction(Meridiani), 0.504f, HematiteRadiusDegrees)), m.hematite, Vector3.zero, Vector3.one, false).GetComponent<MeshRenderer>();
            gusev.enabled = meridiani.enabled = patch.enabled = false;

            // Lançamentos: dois foguetes em silhueta e uma chama única reaproveitada.
            var launch = Child(root.transform, "Launch");
            launch.localPosition = LaunchOffset;
            var profile = new List<Vector2> { new Vector2(0.0001f, 0f), new Vector2(1.3f, 0f), new Vector2(1.3f, 22f), new Vector2(0.9f, 27f), new Vector2(0.0001f, 31f) };
            var body = SaveMesh("OriginRocketBody", MerLandingGeometry.Lathe(profile, 20));
            Transform Rocket(string name, float x)
            {
                var r = Part(launch, name, body, m.rocket, new Vector3(x, 0f, 0f), Vector3.one);
                for (var i = 0; i < 4; i++)
                {
                    var angle = i * 90f * Mathf.Deg2Rad + 0.78f;
                    Part(r, "Booster_" + i, cylinderMesh, m.rocket, new Vector3(Mathf.Cos(angle) * 1.8f, 6f, Mathf.Sin(angle) * 1.8f), new Vector3(1.1f, 6f, 1.1f));
                }

                return r;
            }

            var rocketA = Rocket("Rocket_A", -RocketSpacingMeters * 0.5f);
            var rocketB = Rocket("Rocket_B", RocketSpacingMeters * 0.5f);
            var flame = Part(launch, "Flame_Shared", SaveMesh("OriginFlame", MerLandingGeometry.Flame()), m.flame, Vector3.zero, Vector3.one, false);
            flame.gameObject.SetActive(false);

            var director = root.AddComponent<MarsOriginDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("globe").objectReferenceValue = globe;
            so.FindProperty("gusevMarker").objectReferenceValue = gusev;
            so.FindProperty("meridianiMarker").objectReferenceValue = meridiani;
            so.FindProperty("hematitePatch").objectReferenceValue = patch;
            so.FindProperty("rocketA").objectReferenceValue = rocketA;
            so.FindProperty("rocketB").objectReferenceValue = rocketB;
            so.FindProperty("flame").objectReferenceValue = flame;
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
            sunObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.7f, -0.45f, 0.55f), Vector3.up);
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.95f, 0.88f);

            var cameraObject = context.Child(context.Root.transform, "Preview Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.015f, 0.02f);
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 600f;
            context.Component<UniversalAdditionalCameraData>(cameraObject).renderPostProcessing = false;
            context.Component<CameraDirector>(cameraObject);
            context.Component<SequenceDirector>(cameraObject).enabled = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.22f, 0.25f);
        }
    }
}
