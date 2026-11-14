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
    /// MAR-013 (R4, revisada): abertura M01–M03 como a do jogo anterior (2003 · Marte no espaço, laboratório, hematita em Meridiani Planum),
    /// com o mesmo tratamento para a Spirit e Gusev e o globo dos pousos com datas. Prefab com três regiões e três SequenceDefinitions.
    /// Planeta e globo usam o modelo NASA "Planet Mars" reamostrado (ver ArtSource/Mars/R-MAR-05). Não toca em Boot nem BuildSettings.
    /// </summary>
    public sealed class MarsOriginBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string ArtPath = MarsPath + "/Art/Origin";
        public const string GeneratedPath = ArtPath + "/Generated";
        public const string TexturePath = ArtPath + "/Textures";
        public const string PrefabPath = MarsPath + "/Prefabs/MarsPreparationStage.prefab";
        public const string PreviewScenePath = ArtPath + "/Preview/MarsOriginPreview.unity";
        const string SubtitlesPath = MarsPath + "/Data/MarsSubtitles.json";

        public static readonly Vector3 SpaceOffset = new Vector3(300f, 0f, 0f);
        public static readonly Vector3 GlobeOffset = new Vector3(600f, 0f, 0f);
        public static string SequencePath(string stepId) => MarsPath + "/Timelines/MarsOrigin_" + stepId + ".asset";

        public override string Id => "mars.origin";
        public override string Owner => "R4 · MAR-013";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-013] Origin";
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-013 Reconstruir abertura (espaço, laboratório e globo)")]
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
            public Material floor, steel, plate, cable, planet, glow, backdrop, stars;
        }

        // ---------- Câmera ----------

        struct View
        {
            public Vector3 Position, Target;
            public float Fov;
        }

        static View Move(float u, Vector3 p0, Vector3 p1, Vector3 t0, Vector3 t1, float fov)
        {
            var e = MerLandingChoreography.Smooth(u);
            return new View { Position = Vector3.Lerp(p0, p1, e), Target = Vector3.Lerp(t0, t1, e), Fov = fov };
        }

        static IEnumerable<(string id, float seconds, Func<float, View> path, ShotEvent[] events)> Shots(string sequence)
        {
            if (sequence == MarsOriginDirector.SequenceM01)
            {
                // Marte visto do espaço: aproxima devagar do planeta.
                yield return ("mars.origin.space", SpaceSeconds, u => Move(u, SpaceOffset + new Vector3(-1.2f, 0.5f, -17f), SpaceOffset + new Vector3(-0.6f, 0.3f, -12.5f), SpaceOffset, SpaceOffset, 32f),
                    new[] { new ShotEvent(0f, "mars.origin.space") });
            }
            else if (sequence == MarsOriginDirector.SequenceM02)
            {
                // Laboratório: abertura larga, detalhe dos rovers, e um plano para cada rover (a fala da Spirit fecha no rover dela).
                yield return ("mars.origin.lab.wide", 4.9f, u => Move(u, new Vector3(-2f, 1.6f, -6.5f), new Vector3(2.4f, 1.7f, -6.5f), new Vector3(0f, 1.4f, 0f), new Vector3(0f, 1.4f, 0f), 42f),
                    new[] { new ShotEvent(MeridianiMapStart - LabStart, "mars.origin.map.meridiani") });
                yield return ("mars.origin.lab.detail", 8.2f, u => Move(u, new Vector3(-3.6f, 1.2f, -1.6f), new Vector3(2.8f, 1.3f, -1.8f), new Vector3(-2.9f, 1.05f, 0f), new Vector3(2.9f, 1.05f, 0f), 32f),
                    new[] { new ShotEvent(0f, "mars.origin.lab.detail") });
                yield return ("mars.origin.lab.opportunity", 7.7f, u => Move(u, new Vector3(3.6f, 1.4f, -3.2f), new Vector3(2.5f, 1.6f, -2.7f), new Vector3(2.2f, 1.2f, 0f), new Vector3(2.2f, 1.3f, 0f), 34f),
                    new[] { new ShotEvent(0f, "mars.origin.lab.meridiani") });
                yield return ("mars.origin.lab.spirit", 7.7f, u => Move(u, new Vector3(-3.6f, 1.4f, -3.2f), new Vector3(-2.5f, 1.6f, -2.7f), new Vector3(-2.2f, 1.2f, 0f), new Vector3(-2.2f, 1.3f, 0f), 34f),
                    new[] { new ShotEvent(0f, "mars.origin.lab.gusev"), new ShotEvent(GusevMapStart - (LabStart + 20.8f), "mars.origin.map.gusev") });
            }
            else
            {
                yield return ("mars.origin.globe.spirit", 3f, u => Move(u, GlobeOffset + new Vector3(-2.2f, 0.8f, -7.4f), GlobeOffset + new Vector3(-1.8f, 0.6f, -6.9f), GlobeOffset, GlobeOffset, 30f),
                    new[] { new ShotEvent(GusevLitAt - GlobeStart, "mars.origin.globe.spirit") });
                yield return ("mars.origin.globe.opportunity", 3f, u => Move(u, GlobeOffset + new Vector3(2f, -0.5f, -6.8f), GlobeOffset + new Vector3(1.4f, -0.3f, -6.3f), GlobeOffset, GlobeOffset, 30f),
                    new[] { new ShotEvent(MeridianiLitAt - GlobeStart - 3f, "mars.origin.globe.opportunity") });
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
            new MarsLandingLabel("MAR.ORI.01", LabStart, LabStart + 4.9f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.CR1", MeridianiMapStart, MeridianiMapEnd, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.02", LabStart + 13.1f, LabStart + 20.8f, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.03", LabStart + 20.8f, GlobeStart, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.ORI.CR2", GusevMapStart, GusevMapEnd, MarsLandingLabelAnchor.Full),
            new MarsLandingLabel("MAR.L01", GusevLitAt, TotalSeconds, MarsLandingLabelAnchor.UpperHalf),
            new MarsLandingLabel("MAR.L02", MeridianiLitAt, TotalSeconds, MarsLandingLabelAnchor.LowerHalf),
        };

        static SequenceDefinition BuildSequence(MarsChapterFlow.SubtitleTable table, string id, string step)
        {
            var shots = Shots(id).Select(s => new ShotDefinition(s.id, s.seconds, MarsOriginDirector.RigId, Bake(s.seconds, s.path), null, ShotEasing.Linear, 0f, s.events)).ToList();
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

            Store(context, SequencePath("M01"), BuildSequence(table, MarsOriginDirector.SequenceM01, "M01"));
            Store(context, SequencePath("M02"), BuildSequence(table, MarsOriginDirector.SequenceM02, "M02"));
            Store(context, SequencePath("M03"), BuildSequence(table, MarsOriginDirector.SequenceM03, "M03"));

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

        static Color StarPixel(float u, float v)
        {
            var h = Mathf.Sin(u * 12.9898f * 977f + v * 78.233f * 631f) * 43758.5453f;
            var r = h - Mathf.Floor(h);
            var star = r > 0.9965f ? Mathf.Lerp(0.5f, 1f, (r - 0.9965f) / 0.0035f) : 0f;
            return new Color(star, star, star * 0.95f, 1f);
        }

        static Texture2D ImportedTexture(string file, bool srgb, int maxSize)
        {
            var path = TexturePath + "/" + file;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                throw new InvalidOperationException("Textura ausente: " + path);

            if (importer.maxTextureSize != maxSize || importer.sRGBTexture != srgb || !importer.mipmapEnabled)
            {
                importer.maxTextureSize = maxSize;
                importer.sRGBTexture = srgb;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Mats BuildMaterials(SceneBuildContext context)
        {
            var planetTex = ImportedTexture("Mars_Equirect.jpg", true, 2048);
            var starsTex = Texture("OriginStars", 1024, 512, true, StarPixel);
            var m = new Mats
            {
                floor = context.Material(GeneratedPath + "/LabFloor.mat", "Universal Render Pipeline/Lit"),
                steel = context.Material(GeneratedPath + "/LabSteel.mat", "Universal Render Pipeline/Lit"),
                plate = context.Material(GeneratedPath + "/LabPlate.mat", "Universal Render Pipeline/Lit"),
                cable = context.Material(GeneratedPath + "/LabCable.mat", "Universal Render Pipeline/Lit"),
                planet = context.Material(GeneratedPath + "/MarsPlanet.mat", "Universal Render Pipeline/Lit"),
                glow = context.Material(GeneratedPath + "/MarkerGlow.mat", "Universal Render Pipeline/Unlit"),
                backdrop = context.Material(GeneratedPath + "/BlackBackdrop.mat", "Universal Render Pipeline/Unlit"),
                stars = context.Material(GeneratedPath + "/Starfield.mat", "Universal Render Pipeline/Unlit"),
            };
            Solid(m.floor, new Color(0.16f, 0.17f, 0.17f), 0.1f, 0f);
            Solid(m.steel, new Color(0.45f, 0.47f, 0.48f), 0.45f, 0.6f);
            Solid(m.plate, new Color(0.82f, 0.8f, 0.74f), 0.2f, 0f);
            Solid(m.cable, new Color(0.08f, 0.08f, 0.09f), 0.3f, 0f);
            m.planet.SetTexture("_BaseMap", planetTex);
            m.planet.SetColor("_BaseColor", Color.white);
            m.planet.SetFloat("_Smoothness", 0.05f);
            m.planet.SetFloat("_Metallic", 0f);
            m.backdrop.SetColor("_BaseColor", new Color(0.004f, 0.004f, 0.007f, 1f));
            m.stars.SetTexture("_BaseMap", starsTex);
            m.stars.SetColor("_BaseColor", Color.white);
            Transparent(m.glow, null, new Color(1f, 0.82f, 0.4f, 1f));
            foreach (var material in new[] { m.planet, m.glow, m.backdrop, m.stars })
                EditorUtility.SetDirty(material);
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
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var cylinderMesh = SaveMesh("OriginCylinder", MerLandingGeometry.Cylinder(0.5f, 2f, 12));
            var sphere = SaveMesh("OriginSphere", MerLandingGeometry.Sphere(1f, 96, 48));
            var markerMesh = SaveMesh("OriginMarker", MerLandingGeometry.Sphere(1f, 16, 10));

            // Região 1 · espaço: Marte (modelo NASA) sobre um céu de estrelas.
            var space = Child(root.transform, "Region_Space");
            space.localPosition = SpaceOffset;
            Part(space, "Backdrop_Stars", quad, m.stars, new Vector3(0f, 0f, 60f), new Vector3(220f, 110f, 1f), false).localRotation = Quaternion.Euler(0f, 0f, 0f);
            var planet = Part(space, "Planet_Mars", sphere, m.planet, Vector3.zero, Vector3.one * (PlanetRadiusMeters * 2f), false);

            // Região 2 · laboratório: cada peça tem função (piso, bancada, apoio, rack de teste, cabo, placa).
            var lab = Child(root.transform, "Region_Lab");
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

            // Região 3 · globo: o mesmo modelo de Marte, com os dois pousos marcados.
            var map = Child(root.transform, "Region_Globe");
            map.localPosition = GlobeOffset;
            Part(map, "Backdrop_Black", quad, m.backdrop, new Vector3(0f, 0f, 60f), new Vector3(220f, 110f, 1f), false);
            var globe = Part(map, "Globe", sphere, m.planet, Vector3.zero, Vector3.one * (GlobeRadiusMeters * 2f), false);
            Renderer Marker(string name, Vector2 latLon) =>
                Part(globe, name, markerMesh, m.glow, Direction(latLon) * 0.505f, Vector3.one * 0.07f, false).GetComponent<MeshRenderer>();
            var gusev = Marker("Marker_Gusev", Gusev);
            var meridiani = Marker("Marker_Meridiani", Meridiani);
            gusev.enabled = meridiani.enabled = false;

            var director = root.AddComponent<MarsOriginDirector>();
            var so = new SerializedObject(director);
            var regions = so.FindProperty("regions");
            regions.arraySize = 3;
            regions.GetArrayElementAtIndex(0).objectReferenceValue = space;
            regions.GetArrayElementAtIndex(1).objectReferenceValue = lab;
            regions.GetArrayElementAtIndex(2).objectReferenceValue = map;
            so.FindProperty("planet").objectReferenceValue = planet;
            so.FindProperty("globe").objectReferenceValue = globe;
            so.FindProperty("gusevMarker").objectReferenceValue = gusev;
            so.FindProperty("meridianiMarker").objectReferenceValue = meridiani;
            so.FindProperty("meridianiMap").objectReferenceValue = ImportedTexture("OrbitalMap_Meridiani.jpg", true, 1024);
            so.FindProperty("gusevMap").objectReferenceValue = ImportedTexture("OrbitalMap_Gusev.jpg", true, 1024);
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
            foreach (var region in new[] { space, lab, map })
                region.gameObject.SetActive(region == space);
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
            RenderSettings.ambientLight = new Color(0.14f, 0.14f, 0.17f);
        }
    }
}
