using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-003 · Builder do palco único da Pioneer 10 (CORE-006). Gera <c>Art/SpaceStage.prefab</c> (câmera, luzes, céu,
    /// regiões, sonda provisória com sockets, Júpiter, anchors) e a cena <c>Scenes/Pioneer10.unity</c> com uma instância.
    /// Reconstruir mantém GUIDs e, sem mudanças, deixa os arquivos idênticos.
    /// Menu: Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas (ou -builders pioneer10.stage).
    /// </summary>
    public sealed class PioneerStageBuilder : SceneBuilderModule
    {
        public const string BuilderId = "pioneer10.stage";
        public const string SceneAssetPath = "Assets/_Game/Chapters/Pioneer10/Scenes/Pioneer10.unity";
        public const string ChapterFolder = "Assets/_Game/Chapters/Pioneer10";
        public const string ArtFolder = ChapterFolder + "/Art";
        public const string StageFolder = ArtFolder + "/Stage";
        public const string PrefabPath = ArtFolder + "/SpaceStage.prefab";
        public const string StageRootName = "[R5] Pioneer Stage";
        public const string InstanceName = "SpaceStage";

        // Layout do palco, em metros. Nada além de ~1 km da origem: precisão de float folgada, sem jitter.
        public static readonly Vector3 LaunchSiteCenter = new Vector3(500f, 0f, -500f);
        /// <summary>Onde a PIO-004 pendura o diagrama Terra–Lua (gráfico, longe da sonda, na região do espaço).</summary>
        public static readonly Vector3 LaunchDiagramCenter = new Vector3(0f, 0f, -420f);
        public static readonly Vector3 GroundStationCenter = new Vector3(-500f, 0f, -500f);
        public static readonly Vector3 JupiterPosition = new Vector3(-235f, 35f, 235f);
        public const float JupiterRadiusMeters = 80f;
        public const float SkyRadiusMeters = 1200f;
        public const float CameraFarMeters = 1500f;
        public const float CameraNearMeters = 0.08f;

        // Sonda provisória (eixo +Z = eixo da antena = eixo de rotação, P-S05). Só o refletor de 2,74 m tem fonte.
        public const float ReflectorDiameterMeters = 2.74f;
        const float ReflectorDepthMeters = 0.32f;
        const float CompartmentRadiusMeters = 0.71f;
        const float CompartmentFrontZ = -0.08f;
        const float CompartmentBackZ = -0.44f;
        const float BoomZ = -0.26f;
        const float PlaqueAngleDegrees = 90f;
        const float PhotopolarimeterAngleDegrees = 330f;

        public override string Id => BuilderId;
        public override string Owner => "R5 · PIO-003";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => StageRootName;
        public override string AssetFolder => ArtFolder;

        public override void Build(SceneBuildContext context)
        {
            var assets = BuildAssets(context);
            var prefab = context.Prefab(PrefabPath, root => ConfigureStage(root, assets));
            context.PrefabInstance(context.Root.transform, prefab, InstanceName);
            ConfigureSceneLighting();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var presentation = context.Root.GetComponentInChildren<PioneerPresentation>(true);
            if (presentation == null)
            {
                errors.Add($"{BuilderId}: PioneerPresentation ausente em {StageRootName}.");
                return;
            }

            errors.AddRange(presentation.ValidateSetup().Select(e => $"{BuilderId}: {e}"));
            errors.AddRange(PioneerStageChecks.Run(presentation).Errors.Select(e => $"{BuilderId}: {e}"));
        }

        // ---------- Assets ----------

        sealed class StageAssets
        {
            public Mesh Dish, Compartment, Rod, Rtg, FeedHorn, PlaqueBox, InstrumentBox, Sphere, SkySphere, LowSphere, StarMesh;
            public Material Ivory, DullGold, DarkMetal, Plaque, Jupiter, Sky, Stars, Ground, Aldebaran;
        }

        static StageAssets BuildAssets(SceneBuildContext context)
        {
            var a = new StageAssets
            {
                Dish = MeshAsset(context, "PIO_Reflector", m => PioneerStageArt.Dish(m, ReflectorDiameterMeters * 0.5f, ReflectorDepthMeters, 0.025f, 40, 8)),
                Compartment = MeshAsset(context, "PIO_Compartment", m => PioneerStageArt.Prism(m, "PIO_Compartment", 6, CompartmentRadiusMeters, CompartmentFrontZ - CompartmentBackZ)),
                Rod = MeshAsset(context, "PIO_Rod", m => PioneerStageArt.Prism(m, "PIO_Rod", 6, 0.022f, 1f)),
                Rtg = MeshAsset(context, "PIO_Rtg", m => PioneerStageArt.Prism(m, "PIO_Rtg", 8, 0.19f, 0.56f)),
                FeedHorn = MeshAsset(context, "PIO_Feed", m => PioneerStageArt.Prism(m, "PIO_Feed", 10, 0.1f, 0.34f)),
                PlaqueBox = MeshAsset(context, "PIO_PlaqueBox", m => PioneerStageArt.Box(m, "PIO_PlaqueBox", new Vector3(0.152f, 0.229f, 0.008f))),
                InstrumentBox = MeshAsset(context, "PIO_InstrumentBox", m => PioneerStageArt.Box(m, "PIO_InstrumentBox", new Vector3(0.14f, 0.14f, 0.22f))),
                Sphere = MeshAsset(context, "PIO_Sphere", m => PioneerStageArt.Sphere(m, "PIO_Sphere", 64, 32, false)),
                SkySphere = MeshAsset(context, "PIO_SkySphere", m => PioneerStageArt.Sphere(m, "PIO_SkySphere", 48, 24, true)),
                LowSphere = MeshAsset(context, "PIO_LowSphere", m => PioneerStageArt.Sphere(m, "PIO_LowSphere", 12, 6, false)),
                StarMesh = MeshAsset(context, "PIO_BrightStars", m => PioneerStageArt.BrightStars(m, 520, 2003, 0.09f, 0.2f)),
            };

            var sky = TextureAsset(context, "PIO_Starfield.png", () => PioneerStageArt.Starfield(2048, 1024, 3400, 1972), 2048, false, TextureWrapMode.Repeat, TextureWrapMode.Clamp);
            var palette = TextureAsset(context, "PIO_StarPalette.png", PioneerStageArt.StarPalette, 32, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp, FilterMode.Point);
            var jupiter = TextureAsset(context, "PIO_JupiterProvisional.png", () => PioneerStageArt.JupiterBands(1024, 512, 1973), 1024, true, TextureWrapMode.Repeat, TextureWrapMode.Clamp);

            a.Ivory = Lit(context, "PIO_Ivory", new Color(0.86f, 0.84f, 0.78f), 0.1f, 0.42f);
            // Metálico moderado: sem reflexo de ambiente (perfil leve), metal puro ficaria quase preto.
            a.DullGold = Lit(context, "PIO_DullGold", new Color(0.66f, 0.52f, 0.29f), 0.3f, 0.4f);
            a.DarkMetal = Lit(context, "PIO_DarkMetal", new Color(0.26f, 0.26f, 0.27f), 0.3f, 0.35f);
            a.Plaque = Lit(context, "PIO_PlaqueAnodized", new Color(0.86f, 0.69f, 0.40f), 0.3f, 0.55f);
            a.Jupiter = Lit(context, "PIO_Jupiter", Color.white, 0f, 0.05f, jupiter);
            a.Ground = Lit(context, "PIO_GroundNight", new Color(0.032f, 0.033f, 0.038f), 0f, 0.08f);
            a.Sky = Unlit(context, "PIO_Sky", Color.white, sky);
            a.Stars = Unlit(context, "PIO_BrightStars", Color.white, palette);
            a.Aldebaran = Unlit(context, "PIO_AldebaranMarker", new Color(1f, 0.6f, 0.42f), null);
            return a;
        }

        internal static Mesh MeshAsset(SceneBuildContext context, string name, Action<Mesh> fill, string folder = StageFolder)
        {
            var mesh = context.Asset(folder + "/Meshes/" + name + ".asset", () => new Mesh { name = name });
            fill(mesh);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        internal static Texture2D TextureAsset(SceneBuildContext context, string file, Func<Texture2D> generate, int maxSize, bool mipmaps,
            TextureWrapMode wrapU, TextureWrapMode wrapV, FilterMode filter = FilterMode.Bilinear, string folder = StageFolder, bool alpha = false)
        {
            var path = folder + "/Textures/" + file;
            if (!path.StartsWith(ChapterFolder + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("textura fora da pasta do dono: " + path);

            SceneBuildContext.EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var generated = generate();
            var bytes = generated.EncodeToPNG();
            Object.DestroyImmediate(generated);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var changed = importer.textureType != TextureImporterType.Default || importer.maxTextureSize != maxSize || importer.mipmapEnabled != mipmaps
                          || importer.wrapModeU != wrapU || importer.wrapModeV != wrapV || !importer.sRGBTexture || importer.alphaSource != (alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None)
                          || importer.alphaIsTransparency != alpha
                          || importer.filterMode != filter
                          || importer.textureCompression != (filter == FilterMode.Point ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed);
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = maxSize;
                importer.mipmapEnabled = mipmaps;
                importer.wrapModeU = wrapU;
                importer.wrapModeV = wrapV;
                importer.sRGBTexture = true;
                importer.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = alpha;
                importer.filterMode = filter;
                importer.textureCompression = filter == FilterMode.Point ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed; // paleta: cor exata
                importer.SaveAndReimport();
            }

            // Passa pela verificação de dono do contexto (o arquivo já existe, nada é criado).
            return context.Asset<Texture2D>(path, () => throw new InvalidOperationException("textura não importada: " + path));
        }

        internal static Material Lit(SceneBuildContext context, string name, Color color, float metallic, float smoothness, Texture2D map = null, string folder = StageFolder)
        {
            var material = context.Material(folder + "/Materials/" + name + ".mat", "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", map);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static Material Unlit(SceneBuildContext context, string name, Color color, Texture2D map, string folder = StageFolder)
        {
            var material = context.Material(folder + "/Materials/" + name + ".mat", "Universal Render Pipeline/Unlit");
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", map);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Prefab ----------

        static void ConfigureStage(GameObject root, StageAssets a)
        {
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            // Câmera única do capítulo, perfil leve: sem pós-processamento, sem sombras, sem AA caro.
            var cameraObject = Ensure(root.transform, "MainCamera");
            cameraObject.tag = "MainCamera";
            var camera = Get<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = CameraNearMeters;
            camera.farClipPlane = CameraFarMeters;
            camera.fieldOfView = 40f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            Get<AudioListener>(cameraObject);
            var cameraData = Get<UniversalAdditionalCameraData>(cameraObject);
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.renderShadows = false;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.requiresColorOption = CameraOverrideOption.Off;
            var director = Get<CameraDirector>(cameraObject);
            Set(director, "targetCamera", camera);

            // Luzes: uma key e um fill, ambas sem sombra (espaço, perfil leve).
            var lights = Ensure(root.transform, "Lights");
            var key = DirectionalLight(Ensure(lights.transform, "KeyLight"));
            var fill = DirectionalLight(Ensure(lights.transform, "FillLight"));

            // Céu compartilhado: esfera interna centrada na câmera.
            var sky = Visual(Ensure(root.transform, "Sky"), a.SkySphere, a.Sky);
            sky.transform.localScale = Vector3.one * SkyRadiusMeters;
            var stars = Visual(Ensure(sky.transform, "BrightStars"), a.StarMesh, a.Stars);
            stars.transform.localScale = Vector3.one * 0.96f; // logo à frente da esfera do céu

            // Regiões: só uma ativa por vez.
            var regions = Ensure(root.transform, "Regions");
            var deepSpace = Ensure(regions.transform, "DeepSpace");
            var launchSite = Ensure(regions.transform, "LaunchSite");
            launchSite.transform.localPosition = LaunchSiteCenter;
            var groundStation = Ensure(regions.transform, "GroundStation");
            groundStation.transform.localPosition = GroundStationCenter;
            Ground(launchSite, a.Ground);
            Ground(groundStation, a.Ground);
            Ensure(launchSite.transform, "AtlasSocket").transform.localPosition = Vector3.zero;          // PIO-004 · LaunchSilhouette
            Ensure(groundStation.transform, "DishSocket").transform.localPosition = new Vector3(0f, 0f, 0f); // PIO-011 · DsnSilhouette

            Ensure(deepSpace.transform, "LaunchDiagramSocket").transform.localPosition = LaunchDiagramCenter; // PIO-004 · elipse lunar
            var probe = Ensure(deepSpace.transform, "Probe");
            var spin = Ensure(probe.transform, "ProbeSpin");
            var sockets = BuildProbe(spin.transform, a);

            var jupiter = Visual(Ensure(deepSpace.transform, "Jupiter"), a.Sphere, a.Jupiter);
            jupiter.transform.localPosition = JupiterPosition;
            jupiter.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
            jupiter.transform.localScale = Vector3.one * JupiterRadiusMeters;

            var aldebaran = Visual(Ensure(root.transform, "AldebaranMarker"), a.LowSphere, a.Aldebaran);
            aldebaran.transform.localScale = Vector3.one * 5f;

            var groups = LightGroups();
            var anchors = BuildAnchors(Ensure(root.transform, "Anchors").transform, sockets);

            var presentation = Get<PioneerPresentation>(root);
            var so = new SerializedObject(presentation);
            so.FindProperty("cameraDirector").objectReferenceValue = director;
            so.FindProperty("probeSpin").objectReferenceValue = spin.transform;
            SetArray(so.FindProperty("probeSockets"), sockets, (p, t) => p.objectReferenceValue = t);
            so.FindProperty("spinDegreesPerSecond").floatValue = 24f;
            so.FindProperty("reducedMotionSpinScale").floatValue = 0.35f;
            SetArray(so.FindProperty("regionRoots"), new[] { deepSpace, launchSite, groundStation }, (p, g) => p.objectReferenceValue = g);
            so.FindProperty("jupiter").objectReferenceValue = jupiter.transform;
            so.FindProperty("aldebaranMarker").objectReferenceValue = aldebaran.transform;
            so.FindProperty("aldebaranDirection").vector3Value = AldebaranDirection;
            so.FindProperty("aldebaranDistanceMeters").floatValue = 1100f;
            so.FindProperty("sky").objectReferenceValue = sky.transform;
            so.FindProperty("keyLight").objectReferenceValue = key;
            so.FindProperty("fillLight").objectReferenceValue = fill;
            SetArray(so.FindProperty("lightGroups"), groups, WriteGroup);
            SetArray(so.FindProperty("anchors"), anchors, WriteAnchor);
            so.FindProperty("startAnchorId").stringValue = "PIO.Launch";
            so.ApplyModifiedPropertiesWithoutUndo();

            // Estado salvo = composição do primeiro anchor de estado, para a cena abrir já enquadrada.
            spin.transform.localRotation = Quaternion.identity;
            presentation.ShowAnchor("PIO.Launch");
        }

        public static readonly Vector3 AldebaranDirection = new Vector3(0.2f, 0.08f, -1f);

        /// <summary>Silhueta provisória + sockets nomeados (contrato com o modelo definitivo da PIO-002).</summary>
        static List<Transform> BuildProbe(Transform spin, StageAssets a)
        {
            var silhouette = Ensure(spin, "ProvisionalSilhouette").transform;
            var socketRoot = Ensure(spin, "Sockets").transform;
            var sockets = new List<Transform>();

            Visual(Ensure(silhouette, "Reflector"), a.Dish, a.Ivory);

            var compartment = Visual(Ensure(silhouette, "EquipmentCompartment"), a.Compartment, a.DullGold);
            compartment.transform.localPosition = new Vector3(0f, 0f, CompartmentBackZ);

            // Suportes da antena: do compartimento ao dorso do refletor.
            var supports = Ensure(silhouette, "AntennaSupports").transform;
            for (var i = 0; i < 6; i++)
            {
                var angle = (i * 60f + 30f) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Rod(Ensure(supports, "Support" + i), a, radial * 0.58f + Vector3.forward * CompartmentFrontZ, radial * 0.98f + Vector3.forward * 0.12f, a.DarkMetal);
            }

            // Alimentador sobre o eixo, em tripé.
            var feed = Ensure(silhouette, "Feed").transform;
            var horn = Visual(Ensure(feed, "Horn"), a.FeedHorn, a.DarkMetal);
            horn.transform.localPosition = new Vector3(0f, 0f, 0.95f);
            for (var i = 0; i < 3; i++)
            {
                var angle = (i * 120f + 90f) * Mathf.Deg2Rad;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Rod(Ensure(feed, "Strut" + i), a, radial * 1.05f + Vector3.forward * (ReflectorDepthMeters * 0.59f), radial * 0.06f + Vector3.forward * 1.0f, a.DarkMetal);
            }

            // Hastes dos RTGs (duas, com duas unidades cada) e do magnetômetro; ângulos aproximados.
            Boom(silhouette, "RtgBoomA", a, 30f, 2.5f, true);
            Boom(silhouette, "RtgBoomB", a, 150f, 2.5f, true);
            Boom(silhouette, "MagnetometerBoom", a, 270f, 6.0f, false);

            // Placa (alumínio anodizado dourado, P-S12) nos suportes da antena, voltada para trás/fora (P-S11).
            var plaqueRadial = Radial(PlaqueAngleDegrees);
            var plaqueStrut = (plaqueRadial * 0.40f + Vector3.forward * 0.20f).normalized;   // direção do suporte de 90°
            var plaqueNormal = (plaqueRadial * 0.20f - Vector3.forward * 0.40f).normalized;  // ⟂ ao suporte, para trás/fora
            var plaquePosition = plaqueRadial * 0.78f + Vector3.forward * 0.02f + plaqueNormal * (0.022f + 0.004f);
            var plaque = Visual(Ensure(silhouette, "Plaque"), a.PlaqueBox, a.Plaque);
            plaque.transform.localPosition = plaquePosition;
            plaque.transform.localRotation = Quaternion.LookRotation(plaqueNormal, plaqueStrut);

            var ippRadial = Radial(PhotopolarimeterAngleDegrees);
            var ipp = Visual(Ensure(silhouette, "Photopolarimeter"), a.InstrumentBox, a.Ivory);
            ipp.transform.localPosition = ippRadial * (CompartmentRadiusMeters + 0.06f) + Vector3.forward * -0.30f;
            ipp.transform.localRotation = Quaternion.LookRotation(ippRadial, Vector3.forward);

            sockets.Add(Socket(socketRoot, "Reflector", new Vector3(0f, 0f, ReflectorDepthMeters * 0.5f), Quaternion.identity));
            sockets.Add(Socket(socketRoot, "Feed", new Vector3(0f, 0f, 1.1f), Quaternion.identity));
            sockets.Add(Socket(socketRoot, "EquipmentCompartment", new Vector3(0f, 0f, (CompartmentFrontZ + CompartmentBackZ) * 0.5f), Quaternion.identity));
            sockets.Add(Socket(socketRoot, "RtgBoomA", Radial(30f) * 2.6f + Vector3.forward * BoomZ, Quaternion.LookRotation(Radial(30f), Vector3.forward)));
            sockets.Add(Socket(socketRoot, "RtgBoomB", Radial(150f) * 2.6f + Vector3.forward * BoomZ, Quaternion.LookRotation(Radial(150f), Vector3.forward)));
            sockets.Add(Socket(socketRoot, "MagnetometerBoom", Radial(270f) * 6.1f + Vector3.forward * BoomZ, Quaternion.LookRotation(Radial(270f), Vector3.forward)));
            sockets.Add(Socket(socketRoot, "Plaque", plaquePosition, Quaternion.LookRotation(plaqueNormal, plaqueStrut)));
            sockets.Add(Socket(socketRoot, "Photopolarimeter", ipp.transform.localPosition, ipp.transform.localRotation));
            return sockets;
        }

        static void Boom(Transform parent, string name, StageAssets a, float angleDegrees, float lengthMeters, bool rtg)
        {
            var radial = Radial(angleDegrees);
            var boom = Ensure(parent, name).transform;
            var start = radial * (CompartmentRadiusMeters - 0.05f) + Vector3.forward * BoomZ;
            var end = radial * (CompartmentRadiusMeters + lengthMeters) + Vector3.forward * BoomZ;
            Rod(Ensure(boom, "Truss"), a, start, end, a.DarkMetal);
            if (rtg)
            {
                for (var i = 0; i < 2; i++)
                {
                    var unit = Visual(Ensure(boom, "Rtg" + i), a.Rtg, a.DarkMetal);
                    unit.transform.localPosition = radial * (CompartmentRadiusMeters + lengthMeters - 1.25f + i * 0.66f) + Vector3.forward * BoomZ;
                    unit.transform.localRotation = Quaternion.LookRotation(radial, Vector3.forward);
                }
            }
            else
            {
                var sensor = Visual(Ensure(boom, "Sensor"), a.InstrumentBox, a.DarkMetal);
                sensor.transform.localPosition = end;
                sensor.transform.localRotation = Quaternion.LookRotation(radial, Vector3.forward);
            }
        }

        static void Rod(GameObject go, StageAssets a, Vector3 from, Vector3 to, Material material)
        {
            Visual(go, a.Rod, material);
            go.transform.localPosition = from;
            go.transform.localRotation = Quaternion.LookRotation(to - from, Mathf.Abs(Vector3.Dot((to - from).normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up);
            go.transform.localScale = new Vector3(1f, 1f, (to - from).magnitude);
        }

        static Transform Socket(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var socket = Ensure(parent, name).transform;
            socket.localPosition = position;
            socket.localRotation = rotation;
            return socket;
        }

        static Vector3 Radial(float angleDegrees) =>
            new Vector3(Mathf.Cos(angleDegrees * Mathf.Deg2Rad), Mathf.Sin(angleDegrees * Mathf.Deg2Rad), 0f);

        static void Ground(GameObject region, Material material)
        {
            var ground = Ensure(region.transform, "Ground");
            var filter = Get<MeshFilter>(ground);
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Plane.fbx");
            var renderer = Get<MeshRenderer>(ground);
            renderer.sharedMaterial = material;
            Quiet(renderer);
            Get<MeshCollider>(ground).sharedMesh = filter.sharedMesh; // chão real para o CameraDirector (proteção de altura)
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
        }

        // ---------- Anchors e luz ----------

        static readonly Vector3 JupiterDirection = JupiterPosition.normalized;

        /// <summary>Sol atrás da câmera e à esquerda nos planos de Júpiter: o mesmo eixo de luz na aproximação e na imagem.</summary>
        static readonly Vector3 JupiterKeyDirection =
            (JupiterDirection + Vector3.Cross(Vector3.up, JupiterDirection).normalized * 0.55f + Vector3.down * 0.12f).normalized;

        /// <summary>Ponto de mira que desloca o alvo <paramref name="degreesLeft"/> graus para a esquerda do quadro (e acima, se positivo).</summary>
        static Vector3 OffsetLook(Vector3 camera, Vector3 target, float degreesLeft, float degreesUp)
        {
            // Mesmos eixos de Quaternion.LookRotation(forward, up): direita = up × forward; cima = forward × direita.
            var forward = (target - camera).normalized;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var up = Vector3.Cross(forward, right);
            var yaw = degreesLeft * Mathf.Deg2Rad;
            var pitch = degreesUp * Mathf.Deg2Rad;
            var look = forward * (Mathf.Cos(yaw) * Mathf.Cos(pitch)) + right * Mathf.Sin(yaw) - up * Mathf.Sin(pitch);
            return camera + look.normalized * (target - camera).magnitude;
        }

        static List<PioneerLightGroup> LightGroups() => new List<PioneerLightGroup>
        {
            Group("PIO.Light.ColdRim", new Vector3(-0.95f, -0.1f, 0.25f), new Color(0.72f, 0.83f, 1f), 1.7f, new Vector3(-0.3f, -0.2f, -1f), new Color(0.3f, 0.38f, 0.55f), 0.04f),
            Group("PIO.Light.Cruise", new Vector3(-0.55f, -0.25f, -0.8f), new Color(1f, 0.96f, 0.9f), 1.25f, new Vector3(0.6f, 0.3f, 0.75f), new Color(0.45f, 0.55f, 0.75f), 0.12f),
            Group("PIO.Light.JupiterAxis", JupiterKeyDirection, new Color(1f, 0.95f, 0.88f), 1.3f, new Vector3(0.7f, 0.25f, 0.65f), new Color(0.4f, 0.48f, 0.65f), 0.08f),
            Group("PIO.Light.Plaque", new Vector3(0.36f, -0.86f, 0.38f), new Color(1f, 0.88f, 0.72f), 1.1f, new Vector3(0.3f, -0.6f, 0.7f), new Color(0.45f, 0.5f, 0.65f), 0.15f),
            Group("PIO.Light.FarSun", new Vector3(-0.1f, -0.15f, -1f), new Color(0.95f, 0.95f, 1f), 0.75f, new Vector3(0.4f, 0.3f, 1f), new Color(0.35f, 0.4f, 0.55f), 0.05f),
            Group("PIO.Light.Night", new Vector3(0.3f, -0.6f, 0.7f), new Color(0.55f, 0.65f, 0.9f), 0.35f, new Vector3(-0.4f, -0.3f, -0.8f), new Color(0.25f, 0.3f, 0.45f), 0.08f),
            Group("PIO.Light.FarewellRim", new Vector3(-0.75f, -0.2f, -0.6f), new Color(0.85f, 0.9f, 1f), 0.55f, new Vector3(0.5f, 0.4f, 0.7f), new Color(0.3f, 0.35f, 0.5f), 0.03f),
        };

        static PioneerLightGroup Group(string id, Vector3 keyDirection, Color keyColor, float keyIntensity, Vector3 fillDirection, Color fillColor, float fillIntensity) =>
            new PioneerLightGroup
            {
                groupId = id,
                keyEulerDegrees = Quaternion.LookRotation(keyDirection.normalized).eulerAngles,
                keyColor = keyColor,
                keyIntensity = keyIntensity,
                fillEulerDegrees = Quaternion.LookRotation(fillDirection.normalized).eulerAngles,
                fillColor = fillColor,
                fillIntensity = fillIntensity,
            };

        sealed class AnchorSpec
        {
            public string Id, State, Function, Light, AlignSocket, AlignTowards;
            public Vector3 Position, LookAt;
            public float Fov = 40f, SpinScale = 1f;
            public PioneerStageRegion Region;
            public bool Jupiter, Aldebaran;
            public Rect UiFrame;
        }

        /// <summary>Anchors na ordem do roteiro. Estados = um anchor cada; planos extras do lançamento antes do anchor do estado.</summary>
        static List<AnchorSpec> AnchorSpecs()
        {
            var plaqueRadial = new Vector3(0.8f, 0.6f, 0f);
            var plaqueNormal = (plaqueRadial * 0.20f - Vector3.forward * 0.40f).normalized;
            var plaqueAligned = plaqueRadial * 0.78f + Vector3.forward * 0.02f + plaqueNormal * 0.026f;
            var plaqueCamera = plaqueAligned + plaqueNormal * 0.95f + Vector3.Cross(plaqueRadial, Vector3.forward) * 0.18f;
            var jupiterRight = Vector3.Cross(Vector3.up, JupiterDirection).normalized;
            var imagingCamera = new Vector3(1.1f, 1.9f, -2.5f) + jupiterRight * 1.3f;
            var jupiterFlat = new Vector3(JupiterDirection.x, JupiterDirection.y, 0f).normalized;
            var instrument = jupiterFlat * (CompartmentRadiusMeters + 0.06f) + Vector3.forward * -0.30f;
            var instrumentCamera = instrument + jupiterFlat * 1.1f + Vector3.forward * -0.9f + Vector3.Cross(Vector3.forward, jupiterFlat) * 0.35f;
            var approachCamera = -JupiterDirection * 11f + Vector3.up * 3.2f + jupiterRight * 3.5f;
            var aldebaran = AldebaranDirection.normalized;
            var farewellCamera = new Vector3(-5f, 3f, 8f);
            var farewellLook = farewellCamera + (aldebaran * 0.55f + (-farewellCamera).normalized * 0.45f).normalized * 60f;

            return new List<AnchorSpec>
            {
                new AnchorSpec { Id = "PIO.Launch.Reveal", State = "PIO.Launch", Function = "PIO01.A · contorno do refletor em contraluz fria",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.ColdRim", Fov = 34f,
                    Position = new Vector3(3.4f, 0.9f, 5.6f), LookAt = new Vector3(0f, 0.05f, 0.2f) },
                new AnchorSpec { Id = "PIO.Launch.Pad", State = "PIO.Launch", Function = "PIO01.B · plano baixo lateral do Atlas-Centaur à noite (silhueta da PIO-004 no AtlasSocket)",
                    Region = PioneerStageRegion.LaunchSite, Light = "PIO.Light.Night", Fov = 50f,
                    Position = LaunchSiteCenter + new Vector3(27f, 1.4f, -25f), LookAt = LaunchSiteCenter + new Vector3(0f, 12f, 0f) },
                new AnchorSpec { Id = "PIO.Launch.Ellipse", State = "PIO.Launch", Function = "PIO01.C · diagrama Terra–Lua: a trajetória cruza a órbita lunar longe da Lua (PIO-004 no LaunchDiagramSocket)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Cruise", Fov = 42f,
                    Position = LaunchDiagramCenter + new Vector3(4f, 7f, -82f), LookAt = LaunchDiagramCenter + new Vector3(4f, 2f, 0f) },
                new AnchorSpec { Id = "PIO.Launch", State = "PIO.Launch", Function = "PIO01.D · sonda livre: eixo de rotação, antena e RTGs (primeiro hold)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Cruise", Fov = 40f,
                    Position = new Vector3(6.2f, 2.7f, -4.9f), LookAt = new Vector3(0.4f, 0.2f, -0.2f) },
                new AnchorSpec { Id = "PIO.Belt", State = "PIO.Belt", Function = "Plano longo quase vazio: a sonda pequena no cinturão real",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Cruise", Fov = 30f,
                    Position = new Vector3(-56f, 16f, 54f), LookAt = new Vector3(0f, 0f, 0f) },
                new AnchorSpec { Id = "PIO.Belt.Sensor", State = "PIO.Belt", Function = "Registro de poeira: dorso do refletor (sensores) à esquerda, instrumento de UI à direita (PIO-005)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Cruise", Fov = 40f,
                    Position = new Vector3(-3.4f, 1.7f, -4.8f), LookAt = OffsetLook(new Vector3(-3.4f, 1.7f, -4.8f), Vector3.zero, 17f, 0f),
                    UiFrame = new Rect(0.56f, 0.18f, 0.41f, 0.46f) },
                new AnchorSpec { Id = "PIO.JupiterApproach", State = "PIO.JupiterApproach", Function = "PIO03.B · Júpiter em dois terços do quadro, sonda no terço inferior",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.JupiterAxis", Fov = 40f, Jupiter = true,
                    Position = approachCamera, LookAt = OffsetLook(approachCamera, JupiterPosition, -7f, 4f) },
                new AnchorSpec { Id = "PIO.JupiterApproach.Instrument", State = "PIO.JupiterApproach", Function = "PIO03.C · close do fotopolarímetro, voltado a Júpiter (atrás da câmera)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.JupiterAxis", Fov = 38f, Jupiter = true, SpinScale = 0f,
                    AlignSocket = "Photopolarimeter", AlignTowards = PioneerPresentation.JupiterTargetId,
                    Position = instrumentCamera, LookAt = instrument + Vector3.forward * 0.1f },
                new AnchorSpec { Id = "PIO.Imaging", State = "PIO.Imaging", Function = "PIO04 · Júpiter e a sonda à esquerda (fotopolarímetro voltado ao planeta); quadro de UI à direita",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.JupiterAxis", Fov = 46f, Jupiter = true,
                    AlignSocket = "Photopolarimeter", AlignTowards = PioneerPresentation.JupiterTargetId,
                    Position = imagingCamera, LookAt = OffsetLook(imagingCamera, JupiterPosition, 19f, -3f),
                    UiFrame = new Rect(0.53f, 0.12f, 0.43f, 0.76f) },
                new AnchorSpec { Id = "PIO.Plaque", State = "PIO.Plaque", Function = "Placa presa aos suportes da antena; rotação visual suspensa só neste close",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Plaque", Fov = 36f, Jupiter = true, SpinScale = 0f,
                    AlignSocket = "Plaque", Position = plaqueCamera, LookAt = plaqueAligned },
                new AnchorSpec { Id = "PIO.Outbound", State = "PIO.Outbound", Function = "Sonda se afasta com a antena voltada à Terra; o diagrama orbital 2D (PIO-010) assume o quadro",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.FarSun", Fov = 40f,
                    Position = new Vector3(-4.5f, -1.8f, 17f), LookAt = new Vector3(0f, 0f, 0f) },
                new AnchorSpec { Id = "PIO.Listening", State = "PIO.Listening", Function = "Antena terrestre sob céu noturno (silhueta DSN da PIO-011 no DishSocket); painel de onda embaixo à direita",
                    Region = PioneerStageRegion.GroundStation, Light = "PIO.Light.Night", Fov = 55f,
                    Position = GroundStationCenter + new Vector3(0f, 1.7f, -36f), LookAt = GroundStationCenter + new Vector3(0f, 14f, 0f),
                    UiFrame = new Rect(0.56f, 0.07f, 0.40f, 0.33f) },
                new AnchorSpec { Id = "PIO.Farewell", State = "PIO.Farewell", Function = "Pioneer diminui no campo de estrelas, na direção geral de Aldebaran (sem iluminá-la)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.FarewellRim", Fov = 45f, Aldebaran = true,
                    Position = farewellCamera, LookAt = farewellLook },
            };
        }

        static List<PioneerStageAnchor> BuildAnchors(Transform parent, List<Transform> sockets)
        {
            var result = new List<PioneerStageAnchor>();
            foreach (var spec in AnchorSpecs())
            {
                var pose = Ensure(parent, spec.Id + " · " + Short(spec.Function)).transform;
                pose.localPosition = spec.Position;
                pose.localRotation = Quaternion.LookRotation(spec.LookAt - spec.Position, Vector3.up);
                result.Add(new PioneerStageAnchor
                {
                    anchorId = spec.Id,
                    stateId = spec.State,
                    function = spec.Function,
                    pose = pose,
                    fieldOfViewDegrees = spec.Fov,
                    region = spec.Region,
                    lightGroupId = spec.Light,
                    showJupiter = spec.Jupiter,
                    showAldebaranMarker = spec.Aldebaran,
                    spinAlignSocket = spec.AlignSocket,
                    spinAlignTowards = spec.AlignTowards,
                    spinScale = spec.SpinScale,
                    uiFrameViewport = spec.UiFrame,
                });
            }

            // Anchors removidos (ou duplicados) não ficam órfãos no prefab: só sobram as poses referenciadas.
            var used = new HashSet<Transform>(result.Select(r => r.pose));
            foreach (Transform child in parent.Cast<Transform>().ToList())
            {
                if (!used.Contains(child))
                    Object.DestroyImmediate(child.gameObject);
            }

            return result;
        }

        static string Short(string function)
        {
            var text = function.Split(';')[0].Replace('/', '+').Trim(); // '/' seria lido como caminho por Transform.Find
            if (text.Length <= 48)
                return text;

            var cut = text.LastIndexOf(' ', 48);
            return text.Substring(0, cut > 20 ? cut : 48).TrimEnd(' ', ',', ':');
        }

        static void WriteGroup(SerializedProperty p, PioneerLightGroup g)
        {
            p.FindPropertyRelative("groupId").stringValue = g.groupId;
            p.FindPropertyRelative("keyEulerDegrees").vector3Value = g.keyEulerDegrees;
            p.FindPropertyRelative("keyColor").colorValue = g.keyColor;
            p.FindPropertyRelative("keyIntensity").floatValue = g.keyIntensity;
            p.FindPropertyRelative("fillEulerDegrees").vector3Value = g.fillEulerDegrees;
            p.FindPropertyRelative("fillColor").colorValue = g.fillColor;
            p.FindPropertyRelative("fillIntensity").floatValue = g.fillIntensity;
        }

        static void WriteAnchor(SerializedProperty p, PioneerStageAnchor a)
        {
            p.FindPropertyRelative("anchorId").stringValue = a.anchorId;
            p.FindPropertyRelative("stateId").stringValue = a.stateId;
            p.FindPropertyRelative("function").stringValue = a.function;
            p.FindPropertyRelative("pose").objectReferenceValue = a.pose;
            p.FindPropertyRelative("fieldOfViewDegrees").floatValue = a.fieldOfViewDegrees;
            p.FindPropertyRelative("region").enumValueIndex = (int)a.region;
            p.FindPropertyRelative("lightGroupId").stringValue = a.lightGroupId;
            p.FindPropertyRelative("showJupiter").boolValue = a.showJupiter;
            p.FindPropertyRelative("showAldebaranMarker").boolValue = a.showAldebaranMarker;
            p.FindPropertyRelative("spinAlignSocket").stringValue = a.spinAlignSocket ?? "";
            p.FindPropertyRelative("spinAlignTowards").stringValue = a.spinAlignTowards ?? "";
            p.FindPropertyRelative("spinScale").floatValue = a.spinScale;
            p.FindPropertyRelative("uiFrameViewport").rectValue = a.uiFrameViewport;
        }

        internal static void SetArray<T>(SerializedProperty array, IList<T> values, Action<SerializedProperty, T> write)
        {
            array.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
                write(array.GetArrayElementAtIndex(i), values[i]);
        }

        // ---------- Utilidades de hierarquia (dentro do conteúdo do prefab) ----------

        internal static GameObject Ensure(Transform parent, string name)
        {
            foreach (Transform child in parent) // comparação exata: Transform.Find leria '/' como caminho
            {
                if (child.name == name)
                    return child.gameObject;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static T Get<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>(); // sem '??': o nulo falso da Unity não é nulo em C#
            return existing != null ? existing : go.AddComponent<T>();
        }

        internal static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static GameObject Visual(GameObject go, Mesh mesh, Material material)
        {
            Get<MeshFilter>(go).sharedMesh = mesh;
            var renderer = Get<MeshRenderer>(go);
            renderer.sharedMaterial = material;
            Quiet(renderer);
            return go;
        }

        internal static void Quiet(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        static Light DirectionalLight(GameObject go)
        {
            var light = Get<Light>(go);
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.cullingMask = ~0;
            return light;
        }

        /// <summary>Iluminação da cena: ambiente plano e escuro, sem skybox, sem neblina, sem GI baked/realtime.</summary>
        static void ConfigureSceneLighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.018f, 0.021f, 0.03f);
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.sun = null;
        }
    }
}
