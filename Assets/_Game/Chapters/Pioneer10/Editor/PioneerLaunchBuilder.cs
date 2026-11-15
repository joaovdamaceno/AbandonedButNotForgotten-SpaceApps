using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-004 · Builder do lançamento (CORE-006, raiz própria <c>[R5] Pioneer Launch</c> na cena da Pioneer):
    /// <c>Art/LaunchSilhouette.prefab</c> (Atlas-Centaur em silhueta, plataforma em quatro peças, chama em quads,
    /// fumaça ≤ 80 partículas, luz da chama sem sombra), o gráfico Terra–Lua, o brilho calor→eletricidade dos RTGs,
    /// a <see cref="PioneerLaunchActivity"/> ligada ao palco e a sequência <c>Sequences/PIO_Launch.asset</c> (40 s).
    /// Dimensões do Atlas-Centaur são APROXIMADAS (silhueta provisória): conferir com o desenho oficial (P-S02).
    /// </summary>
    public sealed class PioneerLaunchBuilder : SceneBuilderModule
    {
        public const string LaunchBuilderId = "pioneer10.stage.launch"; // ordena depois de pioneer10.stage, de que depende
        public const string LaunchRootName = "[R5] Pioneer Launch";
        public const string LaunchFolder = ArtFolder + "/Launch";
        public const string SilhouettePath = ArtFolder + "/LaunchSilhouette.prefab";
        public const string SequencePath = ChapterFolder + "/Sequences/PIO_Launch.asset";

        public const float StandTopMeters = 4.2f;
        public const float BoosterRadiusMeters = 1.525f; // 10 pés: Atlas e Centaur com o mesmo diâmetro (aproximação a conferir)
        public const float IgnitionSeconds = 9f;        // tempo absoluto na sequência (PIO01.B começa em 7 s)

        public override string Id => LaunchBuilderId;
        public override string Owner => "R5 · PIO-004";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => LaunchRootName;
        public override string AssetFolder => ChapterFolder;

        sealed class LaunchAssets
        {
            public Mesh Booster, Skirt, Centaur, Fairing, Nose, Box, Sphere;
            public Material Atlas, Concrete, Steel, Flame, Smoke, Earth, Moon, Orbit, Trajectory, Trace, Heat, Electric;
        }

        public override void Build(SceneBuildContext context)
        {
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).FirstOrDefault();
            if (stage == null)
                throw new System.InvalidOperationException("o palco da PIO-003 (pioneer10.stage) precisa existir antes do lançamento.");

            var a = BuildAssets(context);
            var prefab = context.Prefab(SilhouettePath, root => ConfigureSilhouette(root, a));
            var root = context.Root.transform;

            var silhouette = context.PrefabInstance(root, prefab, "LaunchSilhouette");
            silhouette.transform.position = LaunchSiteCenter;

            var diagram = BuildDiagram(context, root, a);
            var energy = BuildEnergy(context, root, a);

            var sequence = BuildSequence(context);
            var activity = context.Component<PioneerLaunchActivity>(context.Child(root, "LaunchActivity"));
            var so = new SerializedObject(activity);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("presentation").objectReferenceValue = stage;
            so.FindProperty("launchRoot").objectReferenceValue = silhouette.transform;
            so.FindProperty("atlas").objectReferenceValue = silhouette.transform.Find("Atlas");
            so.FindProperty("atlasLookPoint").objectReferenceValue = silhouette.transform.Find("Atlas/LookPoint");
            so.FindProperty("flame").objectReferenceValue = silhouette.transform.Find("Atlas/Flame").gameObject;
            so.FindProperty("flameLight").objectReferenceValue = silhouette.transform.Find("Atlas/FlameLight").GetComponent<Light>();
            so.FindProperty("smoke").objectReferenceValue = silhouette.transform.Find("Pad/Smoke").GetComponent<ParticleSystem>();
            so.FindProperty("lunarDiagram").objectReferenceValue = diagram;
            so.FindProperty("traceHead").objectReferenceValue = diagram.Find("TraceHead");
            so.FindProperty("rtgGlowA").objectReferenceValue = energy.Find("RtgGlowA");
            so.FindProperty("rtgGlowB").objectReferenceValue = energy.Find("RtgGlowB");
            so.FindProperty("pulseA").objectReferenceValue = energy.Find("PulseA");
            so.FindProperty("pulseB").objectReferenceValue = energy.Find("PulseB");
            so.FindProperty("compartmentGlow").objectReferenceValue = energy.Find("CompartmentGlow");
            so.FindProperty("flameLightIntensity").floatValue = 40f;
            so.FindProperty("rtgGlowScaleMeters").floatValue = 0.26f;
            so.FindProperty("compartmentGlowScaleMeters").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath);
            if (sequence == null)
            {
                errors.Add($"{LaunchBuilderId}: {SequencePath} ausente.");
                return;
            }

            errors.AddRange(sequence.Validate().Select(e => $"{LaunchBuilderId}: {e}"));
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var shot in sequence.Shots)
            {
                if (!stage.TryGetAnchor(shot.RigId, out _))
                    errors.Add($"{LaunchBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
            }

            if (Mathf.Abs(sequence.TotalDurationSeconds - 40f) > 0.01f)
                errors.Add($"{LaunchBuilderId}: PIO.Launch dura {sequence.TotalDurationSeconds} s; o roteiro pede 40 s.");

            var smoke = context.Root.GetComponentInChildren<ParticleSystem>(true);
            if (smoke == null || smoke.main.maxParticles > 80)
                errors.Add($"{LaunchBuilderId}: fumaça ausente ou acima de 80 partículas.");
            if (context.Root.GetComponentsInChildren<Light>(true).Any(l => l.shadows != LightShadows.None))
                errors.Add($"{LaunchBuilderId}: luz da chama não pode ter sombra (perfil leve).");
        }

        // ---------- Assets ----------

        static LaunchAssets BuildAssets(SceneBuildContext context)
        {
            var flameTexture = TextureAsset(context, "PIO_FlameGradient.png", FlameTexture, 128, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp, folder: LaunchFolder, alpha: true);
            var smokeTexture = TextureAsset(context, "PIO_SmokePuff.png", SmokeTexture, 64, true, TextureWrapMode.Clamp, TextureWrapMode.Clamp, folder: LaunchFolder, alpha: true);
            return new LaunchAssets
            {
                Booster = MeshAsset(context, "PIO_AtlasBooster", m => PioneerStageArt.Prism(m, "PIO_AtlasBooster", 20, BoosterRadiusMeters, 20f), LaunchFolder),
                Skirt = MeshAsset(context, "PIO_AtlasSkirt", m => Cone(m, "PIO_AtlasSkirt", 20, 2.35f, BoosterRadiusMeters, 3.2f), LaunchFolder),
                Centaur = MeshAsset(context, "PIO_Centaur", m => PioneerStageArt.Prism(m, "PIO_Centaur", 20, BoosterRadiusMeters, 9.6f), LaunchFolder),
                Fairing = MeshAsset(context, "PIO_Fairing", m => PioneerStageArt.Prism(m, "PIO_Fairing", 20, 1.6f, 4.8f), LaunchFolder),
                Nose = MeshAsset(context, "PIO_FairingNose", m => Cone(m, "PIO_FairingNose", 20, 1.6f, 0.15f, 4.2f), LaunchFolder),
                Box = MeshAsset(context, "PIO_UnitBox", m => PioneerStageArt.Box(m, "PIO_UnitBox", Vector3.one), LaunchFolder),
                Sphere = MeshAsset(context, "PIO_DiagramSphere", m => PioneerStageArt.Sphere(m, "PIO_DiagramSphere", 24, 12, false), LaunchFolder),
                Atlas = Lit(context, "PIO_AtlasNight", new Color(0.58f, 0.59f, 0.61f), 0.2f, 0.4f, folder: LaunchFolder),
                Concrete = Lit(context, "PIO_PadConcrete", new Color(0.17f, 0.17f, 0.18f), 0f, 0.1f, folder: LaunchFolder),
                Steel = Lit(context, "PIO_TowerSteel", new Color(0.22f, 0.2f, 0.2f), 0.3f, 0.3f, folder: LaunchFolder),
                Flame = Additive(context, "PIO_Flame", new Color(1f, 0.58f, 0.24f), flameTexture),
                Smoke = SmokeMaterial(context, smokeTexture),
                Earth = Unlit(context, "PIO_EarthDiagram", Color.white, EarthTexture(context), LaunchFolder),
                Moon = Unlit(context, "PIO_MoonDiagram", new Color(1.12f, 1.12f, 1.1f),
                    PioneerStageBuilder.TextureAsset(context, "PIO_MoonSurface.png", () => PioneerStageArt.MoonSurface(1024, 512, 1969), 1024, true, TextureWrapMode.Repeat, TextureWrapMode.Clamp, folder: LaunchFolder),
                    LaunchFolder),
                Orbit = Unlit(context, "PIO_OrbitLine", new Color(0.30f, 0.36f, 0.48f), null, LaunchFolder),
                Trajectory = Unlit(context, "PIO_TrajectoryLine", new Color(0.62f, 0.55f, 0.40f), null, LaunchFolder),
                Trace = Unlit(context, "PIO_TraceHead", new Color(1f, 0.95f, 0.82f), null, LaunchFolder),
                Heat = Additive(context, "PIO_RtgHeat", new Color(0.62f, 0.24f, 0.06f), null),
                Electric = Additive(context, "PIO_Electric", new Color(0.20f, 0.42f, 0.55f), null),
            };
        }

        /// <summary>Tronco de cone ao longo de +Z (raio na base → raio no topo), faces laterais planas.</summary>
        static void Cone(Mesh mesh, string name, int sides, float baseRadius, float topRadius, float length)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var slope = (baseRadius - topRadius) / length;
            for (var s = 0; s < sides; s++)
            {
                var a0 = s / (float)sides * Mathf.PI * 2f;
                var a1 = (s + 1) / (float)sides * Mathf.PI * 2f;
                var d0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                var d1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                var mid = (d0 + d1).normalized;
                var normal = (mid + Vector3.forward * slope).normalized;
                var i = vertices.Count;
                vertices.AddRange(new[] { d0 * baseRadius, d1 * baseRadius, d0 * topRadius + Vector3.forward * length, d1 * topRadius + Vector3.forward * length });
                normals.AddRange(new[] { normal, normal, normal, normal });
                uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
                triangles.AddRange(new[] { i, i + 1, i + 2, i + 1, i + 3, i + 2 });
            }

            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        static Texture2D FlameTexture()
        {
            const int w = 64, h = 128;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            var pixels = new Color[w * h];
            for (var y = 0; y < h; y++)
            {
                var v = y / (float)(h - 1); // 1 = bocal (topo), 0 = ponta da chama
                var width = Mathf.Lerp(0.08f, 0.42f, Mathf.Pow(v, 0.6f));
                for (var x = 0; x < w; x++)
                {
                    var u = Mathf.Abs(x / (float)(w - 1) - 0.5f);
                    var edge = Mathf.Clamp01(1f - u / width);
                    var a = Mathf.Pow(edge, 1.6f) * Mathf.Pow(v, 0.7f);
                    var core = Mathf.Pow(edge, 4f) * v;
                    pixels[y * w + x] = new Color(1f, Mathf.Lerp(0.55f, 0.95f, core), Mathf.Lerp(0.25f, 0.75f, core), a);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false);
            return texture;
        }

        static Texture2D SmokeTexture()
        {
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, true, false);
            var pixels = new Color[n * n];
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var d = new Vector2(x - (n - 1) * 0.5f, y - (n - 1) * 0.5f).magnitude / (n * 0.5f);
                    var a = Mathf.Clamp01(1f - d);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        /// <summary>URP Unlit transparente aditivo (chama, calor, eletricidade): brilho sem bloom e sem escrever profundidade.</summary>
        static Material Additive(SceneBuildContext context, string name, Color color, Texture2D map)
        {
            var material = Unlit(context, name, color, map, LaunchFolder);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material SmokeMaterial(SceneBuildContext context, Texture2D map)
        {
            var material = context.Material(LaunchFolder + "/Materials/PIO_Smoke.mat", "Universal Render Pipeline/Particles/Unlit");
            material.SetTexture("_BaseMap", map);
            material.SetColor("_BaseColor", new Color(0.46f, 0.46f, 0.48f, 1f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- LaunchSilhouette.prefab ----------

        static void ConfigureSilhouette(GameObject root, LaunchAssets a)
        {
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var up = Quaternion.Euler(-90f, 0f, 0f); // +Z das malhas → +Y

            // Atlas-Centaur: ator da sequência (sobe a partir de 11,5 s). Medidas aproximadas, ~38 m.
            var atlas = Ensure(root.transform, "Atlas");
            atlas.transform.localPosition = Vector3.zero;
            Part(atlas.transform, "BoosterSkirt", a.Skirt, a.Atlas, new Vector3(0f, StandTopMeters, 0f), up);
            Part(atlas.transform, "Booster", a.Booster, a.Atlas, new Vector3(0f, StandTopMeters, 0f), up);
            Part(atlas.transform, "Centaur", a.Centaur, a.Atlas, new Vector3(0f, StandTopMeters + 20f, 0f), up);
            Part(atlas.transform, "Fairing", a.Fairing, a.Atlas, new Vector3(0f, StandTopMeters + 29.6f, 0f), up);
            Part(atlas.transform, "Nose", a.Nose, a.Atlas, new Vector3(0f, StandTopMeters + 34.4f, 0f), up);
            Ensure(atlas.transform, "LookPoint").transform.localPosition = new Vector3(0f, StandTopMeters + 11f, 0f);

            // Chama: três quads cruzados, aditivos, saindo do bocal para baixo. Desligada até a ignição.
            var flame = Ensure(atlas.transform, "Flame");
            flame.transform.localPosition = new Vector3(0f, StandTopMeters + 0.2f, 0f);
            for (var i = 0; i < 3; i++)
            {
                var quad = Visual(Ensure(flame.transform, "Quad" + i), Resources.GetBuiltinResource<Mesh>("Quad.fbx"), a.Flame);
                quad.transform.localPosition = new Vector3(0f, -5.5f, 0f);
                quad.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
                quad.transform.localScale = new Vector3(3.4f, 11f, 1f);
            }

            flame.SetActive(false);

            var lightObject = Ensure(atlas.transform, "FlameLight");
            lightObject.transform.localPosition = new Vector3(0f, StandTopMeters - 2f, 0f);
            var light = Get<Light>(lightObject);
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.62f, 0.32f);
            light.range = 60f;
            light.intensity = 0f;
            light.enabled = false;

            // Plataforma sugerida por quatro peças.
            var pad = Ensure(root.transform, "Pad");
            Block(pad.transform, "Slab", a, a.Concrete, new Vector3(0f, 0.6f, 0f), new Vector3(26f, 1.2f, 26f));
            Block(pad.transform, "Stand", a, a.Concrete, new Vector3(0f, 2.7f, 0f), new Vector3(6.5f, 3f, 6.5f));
            Block(pad.transform, "Tower", a, a.Steel, new Vector3(-10f, 26f, 0f), new Vector3(3f, 52f, 3f));
            Block(pad.transform, "Arm", a, a.Steel, new Vector3(-5.4f, 33f, 0f), new Vector3(8f, 1f, 1.5f));

            // Fumaça cinza: no máximo 80 partículas, simulada pelo tempo ativo (a atividade chama Simulate).
            var smokeObject = Ensure(pad.transform, "Smoke");
            smokeObject.transform.localPosition = new Vector3(3.5f, 2f, 0f);
            smokeObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            ConfigureSmoke(Get<ParticleSystem>(smokeObject), a.Smoke);
        }

        static void ConfigureSmoke(ParticleSystem smoke, Material material)
        {
            var main = smoke.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 10f;
            main.startLifetime = 6f;
            main.startSpeed = 4.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startColor = new Color(0.5f, 0.5f, 0.52f, 0.34f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.04f;
            main.useUnscaledTime = false;

            var emission = smoke.emission;
            emission.enabled = false;
            emission.rateOverTime = 12f;

            var shape = smoke.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 2.5f;

            var size = smoke.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.9f));

            var color = smoke.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = smoke.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 0.5f;
        }

        static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation)
        {
            var go = Visual(Ensure(parent, name), mesh, material);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
        }

        static void Block(Transform parent, string name, LaunchAssets a, Material material, Vector3 center, Vector3 size)
        {
            var go = Visual(Ensure(parent, name), a.Box, material);
            go.transform.localPosition = center;
            go.transform.localScale = size;
        }

        // ---------- Gráfico Terra–Lua (PIO01.C) ----------

        static readonly Vector3 EarthLocal = new Vector3(-26f, -6f, 0f);
        const float MoonOrbitRadius = 34f;
        const float MoonAngleDegrees = 125f;

        /// <summary>Trajetória gráfica: sai da Terra e cruza a órbita lunar a ~25°, longe da Lua (125°): sem encontro próximo.</summary>
        public static Vector3 TrajectoryPoint(float s)
        {
            var angle = Mathf.Lerp(-35f, 22f, s) * Mathf.Deg2Rad;
            var radius = Mathf.Lerp(3.4f, 64f, Mathf.Pow(s, 0.85f));
            return EarthLocal + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        }

        /// <summary>
        /// A mesma trajetória, prolongada em linha reta além do último ponto (s &gt; 1) até sair do quadro: no teste de jogo a linha amarela
        /// "parava no meio da tela". Em <paramref name="s"/> de 0 a 1 é igual a <see cref="TrajectoryPoint"/>.
        /// </summary>
        public const float TrajectoryExtent = 1.9f;
        public static Vector3 ExtendedTrajectoryPoint(float s)
        {
            if (s <= 1f)
                return TrajectoryPoint(s);

            var end = TrajectoryPoint(1f) - EarthLocal;
            return EarthLocal + end.normalized * (end.magnitude + (s - 1f) * 130f);
        }

        static Transform BuildDiagram(SceneBuildContext context, Transform root, LaunchAssets a)
        {
            var diagram = context.Child(root, "LunarDiagram").transform;
            diagram.position = LaunchDiagramCenter;
            diagram.gameObject.SetActive(false); // só existe no plano PIO01.C (a atividade liga)

            var earth = context.Child(diagram, "Earth");
            DiagramVisual(context, earth, a.Sphere, a.Earth, EarthLocal, 3.2f);
            earth.transform.localRotation = Quaternion.Euler(0f, 150f, 0f); // mostra as Américas e o Atlântico, não a Ásia
            var moonPosition = EarthLocal + new Vector3(Mathf.Cos(MoonAngleDegrees * Mathf.Deg2Rad), Mathf.Sin(MoonAngleDegrees * Mathf.Deg2Rad), 0f) * MoonOrbitRadius;
            DiagramVisual(context, context.Child(diagram, "Moon"), a.Sphere, a.Moon, moonPosition, 1.1f);

            var orbit = new Vector3[96];
            for (var i = 0; i < orbit.Length; i++)
            {
                var t = i / (float)orbit.Length * Mathf.PI * 2f;
                orbit[i] = EarthLocal + new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f) * MoonOrbitRadius;
            }

            Line(context, context.Child(diagram, "MoonOrbit"), a.Orbit, orbit, true, 0.28f);
            var path = Enumerable.Range(0, 65).Select(i => ExtendedTrajectoryPoint(i / 64f * TrajectoryExtent)).ToArray(); // até sair do quadro
            Line(context, context.Child(diagram, "Trajectory"), a.Trajectory, path, false, 0.36f);
            DiagramVisual(context, context.Child(diagram, "TraceHead"), a.Sphere, a.Trace, TrajectoryPoint(0f), 0.9f);
            return diagram;
        }

        static void DiagramVisual(SceneBuildContext context, GameObject go, Mesh mesh, Material material, Vector3 localPosition, float radius)
        {
            context.Component<MeshFilter>(go).sharedMesh = mesh;
            var renderer = context.Component<MeshRenderer>(go);
            renderer.sharedMaterial = material;
            Quiet(renderer);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * radius;
        }

        static void Line(SceneBuildContext context, GameObject go, Material material, Vector3[] points, bool loop, float width)
        {
            var line = context.Component<LineRenderer>(go);
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.widthMultiplier = width;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 0;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ---------- Calor → eletricidade ----------

        static Transform BuildEnergy(SceneBuildContext context, Transform root, LaunchAssets a)
        {
            var energy = context.Child(root, "RtgEnergy").transform;
            Glow(context, energy, "RtgGlowA", a.Sphere, a.Heat, 0.26f);
            Glow(context, energy, "RtgGlowB", a.Sphere, a.Heat, 0.26f);
            Glow(context, energy, "PulseA", a.Sphere, a.Electric, 0.08f);
            Glow(context, energy, "PulseB", a.Sphere, a.Electric, 0.08f);
            Glow(context, energy, "CompartmentGlow", a.Sphere, a.Electric, 0.5f);
            return energy;
        }

        static void Glow(SceneBuildContext context, Transform parent, string name, Mesh mesh, Material material, float size)
        {
            var go = context.Child(parent, name);
            context.Component<MeshFilter>(go).sharedMesh = mesh;
            var renderer = context.Component<MeshRenderer>(go);
            renderer.sharedMaterial = material;
            Quiet(renderer);
            go.transform.localScale = Vector3.one * size;
            go.SetActive(false); // a atividade liga no hold
        }

        // ---------- Sequência PIO_Launch (40 s) ----------

        static SequenceDefinition BuildSequence(SceneBuildContext context)
        {
            var fresh = CreateSequence();
            fresh.name = "PIO_Launch";
            var asset = context.Asset(SequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Launch";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>Planos PIO01.A–D do roteiro (0–7, 7–17, 17–26, 26–40 s), legendas PIO.01.01–05 e marcas dos atores.</summary>
        public static SequenceDefinition CreateSequence()
        {
            var shots = new[]
            {
                new ShotDefinition("PIO01.A", 7f, "PIO.Launch.Reveal",
                    new[] { Key(0f, new Vector3(0f, 0f, -1.6f), 32f), Key(7f, Vector3.zero, 32f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, "PIO.Launch.Title") }),
                new ShotDefinition("PIO01.B", 10f, "PIO.Launch.Pad",
                    new[] { Key(0f, Vector3.zero, 50f), Key(10f, new Vector3(0f, 0.4f, 0f), 46f) },
                    lookAtTargetId: PioneerLaunchActivity.AtlasLookTargetId, easing: ShotEasing.Linear, minGroundClearanceMeters: 1f,
                    events: new[] { new ShotEvent(0f, "PIO.Launch.DateCard"), new ShotEvent(IgnitionSeconds - 7f, PioneerLaunchActivity.EventIgnition), new ShotEvent(4.5f, PioneerLaunchActivity.EventLiftoff) }),
                new ShotDefinition("PIO01.C", 9f, "PIO.Launch.Ellipse",
                    new[] { Key(0f, Vector3.zero, 42f), Key(9f, new Vector3(0f, 0f, 3f), 42f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, "PIO.Launch.Ellipse") }),
                new ShotDefinition("PIO01.D", 14f, "PIO.Launch",
                    new[] { Key(0f, new Vector3(-0.8f, 0.3f, -1f), 34f), Key(14f, new Vector3(0.6f, -0.2f, 1.2f), 34f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, "PIO.Launch.Callouts"), new ShotEvent(6.5f, PioneerLaunchActivity.EventRtgHold) }),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.01.01", "PIO.01.01", 0f, 7f),
                new SubtitleCue("PIO.01.02", "PIO.01.02", 8f, 16f),
                new SubtitleCue("PIO.01.03", "PIO.01.03", 18f, 25f),
                new SubtitleCue("PIO.01.04", "PIO.01.04", 26f, 32f),
                new SubtitleCue("PIO.01.05", "PIO.01.05", 33f, 40f),
            };

            var marks = new List<ActorMark>
            {
                new ActorMark(PioneerLaunchActivity.AtlasActorId, 0f, Vector3.zero, Vector3.zero),
                new ActorMark(PioneerLaunchActivity.AtlasActorId, 11.5f, Vector3.zero, Vector3.zero),
                new ActorMark(PioneerLaunchActivity.AtlasActorId, 13f, new Vector3(0f, 2.5f, 0f), Vector3.zero),
                new ActorMark(PioneerLaunchActivity.AtlasActorId, 15f, new Vector3(0f, 14f, 0f), Vector3.zero),
                new ActorMark(PioneerLaunchActivity.AtlasActorId, 17f, new Vector3(0f, 42f, 0f), Vector3.zero),
            };
            for (var i = 0; i <= 8; i++)
            {
                var s = i / 8f;
                marks.Add(new ActorMark(PioneerLaunchActivity.TraceActorId, 17f + 0.5f + s * 8f, ExtendedTrajectoryPoint(s * TrajectoryExtent), Vector3.zero)); // a sonda segue a linha até a borda da tela
            }

            return SequenceDefinition.Create("PIO_Launch", shots, cues, marks);
        }
    }
}
