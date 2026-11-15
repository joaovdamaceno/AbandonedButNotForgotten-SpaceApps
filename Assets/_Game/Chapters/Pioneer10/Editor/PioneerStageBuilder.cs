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
        /// <summary>Textura de Júpiter da NASA (JPL/Caltech, de imagens da Voyager), original preservado em ArtSource.</summary>
        public const string JupiterSourcePath = "ArtSource/Pioneer10/PIO-003/original/Jupiter.jpg";
        public const string JupiterTexturePath = StageFolder + "/Textures/PIO_Jupiter_NASA.jpg";
        public const string ModelInstanceName = "PioneerActors";

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

        // Sonda (eixo +Z = eixo da antena = eixo de rotação, P-S05): modelo da PIO-002, refletor de 2,74 m.
        const float CompartmentRadiusMeters = 0.71f;

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
            public Mesh Sphere, SkySphere, LowSphere, StarMesh, BeltRocks, BeltNike, Trajectory, GlowQuad, PlaqueBox;
            public Material Jupiter, Sky, Stars, Ground, Aldebaran, AldebaranHalo, BeltRock, Mars, Earth, Ganymede, Callisto, Europa, TrajectoryLine, PlaqueGold, PlaqueFace;
        }

        static StageAssets BuildAssets(SceneBuildContext context)
        {
            var a = new StageAssets
            {
                Sphere = MeshAsset(context, "PIO_Sphere", m => PioneerStageArt.Sphere(m, "PIO_Sphere", 64, 32, false)),
                SkySphere = MeshAsset(context, "PIO_SkySphere", m => PioneerStageArt.Sphere(m, "PIO_SkySphere", 48, 24, true)),
                LowSphere = MeshAsset(context, "PIO_LowSphere", m => PioneerStageArt.Sphere(m, "PIO_LowSphere", 12, 6, false)),
                StarMesh = MeshAsset(context, "PIO_BrightStars", m => PioneerStageArt.BrightStars(m, 3000, 2003, 0.05f, 0.2f)),
                BeltRocks = MeshAsset(context, "PIO_BeltRocks", m => PioneerStageArt.SparseRockField(m, BeltRockCenters(), BeltFieldSeed, BeltRockMinMeters, BeltRockMaxMeters)),
                BeltNike = MeshAsset(context, "PIO_BeltNike", FillNike),
                GlowQuad = MeshAsset(context, "PIO_GlowQuad", PioneerStageArt.GlowQuad),
                PlaqueBox = MeshAsset(context, "PIO_PlaqueHeroBox", m => PioneerStageArt.Box(m, "PIO_PlaqueHeroBox", new Vector3(PlaqueHeroWidthMeters, PlaqueHeroHeightMeters, PlaqueHeroThicknessMeters))),
                Trajectory = MeshAsset(context, "PIO_JupiterTrajectory", m => PioneerStageArt.Ribbon(m, TrajectoryPoints(), TrajectoryWidthMeters)),
            };

            var sky = TextureAsset(context, "PIO_Starfield.png", () => PioneerStageArt.Starfield(2048, 1024, 800, 1972), 2048, false, TextureWrapMode.Repeat, TextureWrapMode.Clamp);
            var palette = TextureAsset(context, "PIO_StarPalette.png", PioneerStageArt.StarPalette, 32, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp, FilterMode.Point);
            var jupiter = JupiterTexture(context);

            a.Jupiter = Lit(context, "PIO_Jupiter", Color.white, 0f, 0.05f, jupiter);
            a.Ground = Lit(context, "PIO_GroundNight", new Color(0.032f, 0.033f, 0.038f), 0f, 0.08f);
            a.Sky = Unlit(context, "PIO_Sky", Color.white, sky);
            a.Stars = Unlit(context, "PIO_BrightStars", Color.white, palette);
            a.Aldebaran = Unlit(context, "PIO_AldebaranMarker", new Color(1f, 0.74f, 0.52f), null);
            var glow = TextureAsset(context, "PIO_StarGlow.png", () => PioneerStageArt.GlowTexture(128), 128, false, TextureWrapMode.Clamp, TextureWrapMode.Clamp, FilterMode.Bilinear, StageFolder, true);
            a.AldebaranHalo = AdditiveUnlit(context, "PIO_AldebaranHalo", new Color(1f, 0.55f, 0.32f, 0.85f), glow);
            a.BeltRock = Lit(context, "PIO_BeltRock", new Color(0.30f, 0.29f, 0.28f), 0f, 0.08f);
            var marsTexture = TextureAsset(context, "PIO_MarsSurface.png", () => PioneerStageArt.MarsSurface(1024, 512, 1976), 1024, true, TextureWrapMode.Repeat, TextureWrapMode.Clamp);
            a.Mars = Lit(context, "PIO_Mars", Color.white, 0f, 0.04f, marsTexture);
            a.Earth = Lit(context, "PIO_EarthCompare", Color.white, 0f, 0.12f, EarthTexture(context));
            a.PlaqueGold = Lit(context, "PIO_PlaqueHeroGold", new Color(0.82f, 0.64f, 0.34f), 0.55f, 0.5f);
            a.PlaqueFace = Lit(context, "PIO_PlaqueHeroFace", Color.white, 0.35f, 0.42f,
                TextureAsset(context, "PIO_PlaqueBoard.png", () => PioneerStageArt.PlaqueBoard(768, 1152), 2048, true, TextureWrapMode.Clamp, TextureWrapMode.Clamp, FilterMode.Trilinear));
            a.Ganymede = Lit(context, "PIO_Ganymede", Color.white, 0f, 0.05f, TextureAsset(context, "PIO_Ganymede.png", () => PioneerStageArt.GalileanMoonSurface(512, 256, 1610, 0), 512, true, TextureWrapMode.Repeat, TextureWrapMode.Clamp));
            a.Callisto = Lit(context, "PIO_Callisto", Color.white, 0f, 0.05f, TextureAsset(context, "PIO_Callisto.png", () => PioneerStageArt.GalileanMoonSurface(512, 256, 1610, 1), 512, true, TextureWrapMode.Repeat, TextureWrapMode.Clamp));
            a.Europa = Lit(context, "PIO_Europa", Color.white, 0f, 0.1f, TextureAsset(context, "PIO_Europa.png", () => PioneerStageArt.GalileanMoonSurface(512, 256, 1610, 2), 512, true, TextureWrapMode.Repeat, TextureWrapMode.Clamp));
            a.TrajectoryLine = Unlit(context, "PIO_JupiterTrajectory", new Color(1f, 0.84f, 0.46f), null);
            return a;
        }

        /// <summary>
        /// Copia o mapa de Júpiter da NASA (720×360, equiretangular) para o projeto, byte a byte, e o importa: sRGB, mipmaps,
        /// repetição em U (volta ao redor do planeta) e fixo em V (polos). Substitui a textura gerada por código (G-PIO-02).
        /// </summary>
        public const string EarthSourcePath = "ArtSource/Pioneer10/PIO-031/bluemarble-2048.png";
        public const string EarthTexturePath = StageFolder + "/Textures/PIO_Earth_NASA.png";

        /// <summary>
        /// Mapa da Terra da NASA (Blue Marble, 2048×1024, equiretangular; NASA SVS 2915) copiado byte a byte para o projeto e importado: sRGB,
        /// mipmaps, repetição em U e fixo em V. Usado na Terra do gráfico do lançamento e na Terra de comparação com Júpiter.
        /// </summary>
        internal static Texture2D EarthTexture(SceneBuildContext context)
        {
            if (!File.Exists(EarthSourcePath))
                throw new InvalidOperationException("Mapa da Terra da NASA ausente: " + EarthSourcePath);

            SceneBuildContext.EnsureFolder(Path.GetDirectoryName(EarthTexturePath)?.Replace('\\', '/'));
            var source = File.ReadAllBytes(EarthSourcePath);
            if (!File.Exists(EarthTexturePath) || !File.ReadAllBytes(EarthTexturePath).SequenceEqual(source))
            {
                File.WriteAllBytes(EarthTexturePath, source);
                AssetDatabase.ImportAsset(EarthTexturePath, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(EarthTexturePath);
            if (importer.textureType != TextureImporterType.Default || !importer.sRGBTexture || !importer.mipmapEnabled
                || importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Clamp
                || importer.maxTextureSize != 2048 || importer.filterMode != FilterMode.Trilinear)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            return context.Asset<Texture2D>(EarthTexturePath, () => throw new InvalidOperationException("textura não importada: " + EarthTexturePath));
        }

        static Texture2D JupiterTexture(SceneBuildContext context)
        {
            if (!File.Exists(JupiterSourcePath))
                throw new InvalidOperationException("Textura de Júpiter ausente: " + JupiterSourcePath);

            SceneBuildContext.EnsureFolder(Path.GetDirectoryName(JupiterTexturePath)?.Replace('\\', '/'));
            var source = File.ReadAllBytes(JupiterSourcePath);
            if (!File.Exists(JupiterTexturePath) || !File.ReadAllBytes(JupiterTexturePath).SequenceEqual(source))
            {
                File.WriteAllBytes(JupiterTexturePath, source);
                AssetDatabase.ImportAsset(JupiterTexturePath, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(JupiterTexturePath);
            if (importer.textureType != TextureImporterType.Default || !importer.sRGBTexture || !importer.mipmapEnabled
                || importer.wrapModeU != TextureWrapMode.Repeat || importer.wrapModeV != TextureWrapMode.Clamp
                || importer.maxTextureSize != 1024 || importer.filterMode != FilterMode.Trilinear)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            return context.Asset<Texture2D>(JupiterTexturePath, () => throw new InvalidOperationException("textura não importada: " + JupiterTexturePath));
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

        internal static Material AdditiveUnlit(SceneBuildContext context, string name, Color color, Texture2D map, string folder = StageFolder)
        {
            var material = Unlit(context, name, color, map, folder);
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
            var sockets = BuildProbe(spin.transform);

            var jupiter = Visual(Ensure(deepSpace.transform, "Jupiter"), a.Sphere, a.Jupiter);
            jupiter.transform.localPosition = JupiterPosition;
            jupiter.transform.localRotation = Quaternion.Euler(3f, 0f, 0f);
            jupiter.transform.localScale = Vector3.one * JupiterRadiusMeters;

            // Cena 2 do roteiro v2: campo de pedras e a pedra solitária; só aparecem nos anchors que os pedem.
            var beltField = Visual(Ensure(deepSpace.transform, "BeltField"), a.BeltRocks, a.BeltRock);
            beltField.SetActive(false);
            var beltNike = Visual(Ensure(deepSpace.transform, "BeltNike"), a.BeltNike, a.BeltRock);
            beltNike.transform.localPosition = NikePosition;
            beltNike.SetActive(false);

            // Cena 2: Marte, só no anchor PIO.Belt.Mars (fica atrás da sonda, à direita, iluminado pela mesma luz de cruzeiro).
            var mars = Visual(Ensure(deepSpace.transform, "Mars"), a.Sphere, a.Mars);
            mars.transform.localPosition = MarsPosition;
            mars.transform.localRotation = Quaternion.Euler(12f, 200f, 0f);
            mars.transform.localScale = Vector3.one * MarsRadiusMeters;
            mars.SetActive(false);

            // Cena 3: a Terra de comparação (11,2 vezes menor que Júpiter); PioneerJupiterReveal a liga no plano aberto.
            var earth = Visual(Ensure(deepSpace.transform, "EarthCompare"), a.Sphere, a.Earth);
            earth.transform.localPosition = EarthCompareTo;
            earth.transform.localRotation = Quaternion.Euler(20f, 90f, 0f);
            earth.transform.localScale = Vector3.one * EarthCompareRadiusMeters;
            earth.SetActive(false);

            // Cena 5: as luas galileanas (raios na razão real entre elas) e a trajetória; só aparecem nos anchors que as pedem.
            var moons = Ensure(deepSpace.transform, "GalileanMoons");
            foreach (var moon in MoonSpecs)
            {
                var body = Visual(Ensure(moons.transform, moon.Name), a.Sphere, moon.Name == "Ganymede" ? a.Ganymede : moon.Name == "Callisto" ? a.Callisto : a.Europa);
                body.transform.localPosition = moon.Position;
                body.transform.localRotation = Quaternion.Euler(10f, moon.Name == "Europa" ? 60f : 200f, 0f);
                body.transform.localScale = Vector3.one * moon.RadiusMeters;
            }

            moons.SetActive(false);
            var trajectory = Visual(Ensure(deepSpace.transform, "JupiterTrajectory"), a.Trajectory, a.TrajectoryLine);
            trajectory.SetActive(false);

            // Aldebaran: um miolo laranja-claro e um halo largo e suave (aditivo) voltado para a câmera; bem mais visível que as outras estrelas.
            // Cena 6: a placa dourada em destaque (uma chapa com a face gravada), só nos anchors PIO.Plaque.*.
            var plaqueHero = Visual(Ensure(deepSpace.transform, "PlaqueHero"), a.PlaqueBox, a.PlaqueGold);
            plaqueHero.transform.localPosition = PlaqueHeroCenter;
            var plaqueFace = Visual(Ensure(plaqueHero.transform, "Face"), a.GlowQuad, a.PlaqueFace);
            plaqueFace.transform.localPosition = new Vector3(0f, 0f, -(PlaqueHeroThicknessMeters * 0.5f + 0.002f));
            plaqueFace.transform.localScale = new Vector3(PlaqueHeroWidthMeters, PlaqueHeroHeightMeters, 1f);
            plaqueHero.SetActive(false);

            var aldebaran = Ensure(root.transform, "AldebaranMarker");
            var aldebaranCore = Visual(Ensure(aldebaran.transform, "Core"), a.Sphere, a.Aldebaran);
            aldebaranCore.transform.localScale = Vector3.one * AldebaranCoreMeters;
            var aldebaranHalo = Visual(Ensure(aldebaran.transform, "Halo"), a.GlowQuad, a.AldebaranHalo);
            aldebaranHalo.transform.localScale = new Vector3(AldebaranHaloMeters, AldebaranHaloMeters, 1f);

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
            so.FindProperty("beltField").objectReferenceValue = beltField.transform;
            so.FindProperty("beltNike").objectReferenceValue = beltNike.transform;
            so.FindProperty("mars").objectReferenceValue = mars.transform;
            so.FindProperty("earth").objectReferenceValue = earth.transform;
            so.FindProperty("moons").objectReferenceValue = moons.transform;
            so.FindProperty("trajectory").objectReferenceValue = trajectory.transform;
            so.FindProperty("plaqueHero").objectReferenceValue = plaqueHero.transform;
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
        /// <summary>A 1.100 m da câmera: miolo de 14 m (~0,7°) e halo de 110 m (~5,7°): bem mais visível que o resto do céu.</summary>
        public const float AldebaranCoreMeters = 14f;
        public const float AldebaranHaloMeters = 110f;
        public const float FarewellAwayDistanceMeters = 16f;
        /// <summary>Onde Aldebaran cai no quadro do plano em que a Pioneer se afasta (0–1, origem em cima à esquerda): no alto, à esquerda.</summary>
        public static readonly Vector2 AldebaranAwayViewport = new Vector2(0.14f, 0.18f);
        public const float FarewellAwayFov = 40f;

        /// <summary>Direção de olhar (a câmera aponta para a Pioneer, na origem) que põe Aldebaran em <see cref="AldebaranAwayViewport"/>.</summary>
        public static Vector3 FarewellAwayForward()
        {
            var d = AldebaranDirection.normalized;
            var halfV = Mathf.Tan(FarewellAwayFov * 0.5f * Mathf.Deg2Rad);
            var halfH = halfV * (16f / 9f);
            var vx = (AldebaranAwayViewport.x - 0.5f) * 2f * halfH;
            var vy = (0.5f - AldebaranAwayViewport.y) * 2f * halfV;
            var n = Mathf.Sqrt(1f + vx * vx + vy * vy);
            var f = d;
            for (var i = 0; i < 8; i++)
            {
                var right = Vector3.Cross(Vector3.up, f).normalized;
                var up = Vector3.Cross(f, right);
                f = (d * n - right * vx - up * vy).normalized;
            }

            return f;
        }

        // ---------- Cinturão (roteiro v2, cena 2) ----------

        /// <summary>Cena 2, abertura: Marte atrás da sonda, à direita do quadro (a câmera fica no lado iluminado de PIO.Light.Cruise).</summary>
        public static readonly Vector3 BeltMarsCamera = new Vector3(30f, 8f, 80f);
        /// <summary>A 880 m da câmera, 9° à direita e 3° abaixo do centro do quadro (a sonda), no anchor PIO.Belt.Mars.</summary>
        public static readonly Vector3 MarsPosition = BeltMarsCamera + (OffsetLook(BeltMarsCamera, Vector3.zero, 9f, 3f) - BeltMarsCamera).normalized * 880f;
        public const float MarsRadiusMeters = 85f;
        public const float TrajectoryWidthMeters = 2.4f;
        // Cena 6: a placa dourada em destaque (proporção 2:3 do quadro de 300 × 450 da PioneerPlaqueArt; 1 unidade do quadro = 1 cm).
        public static readonly Vector3 PlaqueHeroCenter = new Vector3(900f, -200f, 600f);
        public const float PlaqueHeroWidthMeters = 3f;
        public const float PlaqueHeroHeightMeters = 4.5f;
        public const float PlaqueHeroThicknessMeters = 0.05f;
        public const float PlaqueHeroFov = 30f;
        const float PlaqueUnitMeters = 0.01f;

        /// <summary>Centro da peça <paramref name="piece"/> (0–4) na placa, em metros, a partir do centro da placa (x à direita, y para cima).</summary>
        public static Vector2 PlaquePieceCenter(int piece)
        {
            var rect = PioneerPlaqueArt.SlotRects[piece];
            return new Vector2((rect.center.x - PioneerPlaqueArt.BoardWidth * 0.5f) * PlaqueUnitMeters, (PioneerPlaqueArt.BoardHeight * 0.5f - rect.center.y) * PlaqueUnitMeters);
        }

        /// <summary>Distância da câmera à placa que enquadra a peça com folga (a gravura ocupa ~60% da altura do quadro).</summary>
        public static float PlaquePieceDistance(int piece)
        {
            var rect = PioneerPlaqueArt.SlotRects[piece];
            // a gravura da peça ocupa o quadrado de lado min(largura, altura) do encaixe
            return Mathf.Min(rect.width, rect.height) * PlaqueUnitMeters * 1.6f / (2f * Mathf.Tan(PlaqueHeroFov * 0.5f * Mathf.Deg2Rad));
        }

        /// <summary>Distância que enquadra a placa inteira.</summary>
        public static float PlaqueOverviewDistance => PlaqueHeroHeightMeters * 1.18f / (2f * Mathf.Tan(PlaqueHeroFov * 0.5f * Mathf.Deg2Rad));

        /// <summary>Deslocamento, no rig do plano geral, que leva a câmera ao enquadramento da peça (x, y no plano da placa; z = aproximação).</summary>
        public static Vector3 PlaquePieceOffset(int piece)
        {
            var center = PlaquePieceCenter(piece);
            return new Vector3(center.x, center.y, PlaqueOverviewDistance - PlaquePieceDistance(piece));
        }
        /// <summary>Luas galileanas (cena 5): raios na razão real entre elas (2.634, 2.410 e 1.561 km), maiores que a razão com Júpiter para aparecerem; cada lua tem a sua câmera a 24 m.</summary>
        public struct MoonSpec { public string Name; public Vector3 Position; public float RadiusMeters; }
        public static readonly MoonSpec[] MoonSpecs =
        {
            new MoonSpec { Name = "Ganymede", Position = new Vector3(520f, 20f, 0f), RadiusMeters = 4.4f },
            new MoonSpec { Name = "Callisto", Position = new Vector3(560f, 20f, 70f), RadiusMeters = 4.0f },
            new MoonSpec { Name = "Europa", Position = new Vector3(600f, 20f, 140f), RadiusMeters = 2.6f },
        };
        public const float MoonCameraDistanceMeters = 24f;
        public static Vector3 MoonCamera(int index) => MoonSpecs[index].Position + new Vector3(0f, 1.6f, -MoonCameraDistanceMeters);
        /// <summary>Cena 3: a Terra tem 1/11,2 do diâmetro de Júpiter (razão real dos diâmetros equatoriais).</summary>
        public const float EarthCompareRadiusMeters = JupiterRadiusMeters / 11.2f;
        /// <summary>Onde a Terra para no quadro do plano aberto (0–1, origem em cima à esquerda) e de onde ela entra (fora do quadro, à esquerda).</summary>
        public static readonly Vector2 EarthCompareViewport = new Vector2(0.2f, 0.7f);
        public static readonly Vector2 EarthCompareEnterViewport = new Vector2(-0.12f, 0.7f);
        public const float ApproachOpenFov = 36f;
        public const int BeltRockCount = 70;
        public const int BeltFieldSeed = 1972;
        /// <summary>Anchor da teleobjetiva/dolly zoom: a câmera chega a ~82 m da sonda; o campo de pedras fica atrás dela.</summary>
        public static readonly Vector3 BeltFieldCamera = new Vector3(-20f, 20f, 77f).normalized * BeltFieldCameraMeters;
        // O cinturão de verdade: pedras pequenas e bem separadas, com muito espaço vazio (a chance de acertar uma é mínima).
        public const float BeltClearRadiusMeters = 90f;
        public const float BeltOuterRadiusMeters = 520f;
        public const float BeltRockMinMeters = 2.5f;
        public const float BeltRockMaxMeters = 8f;
        public const float BeltRockSeparationMeters = 60f;
        public const float BeltCameraClearanceMeters = 80f;
        public const float BeltFieldCameraMeters = 650f;
        public static List<Vector3> BeltRockCenters() =>
            PioneerStageArt.SparseRockCenters(BeltRockCount, BeltFieldSeed, BeltClearRadiusMeters, BeltOuterRadiusMeters, BeltRockSeparationMeters,
                BeltFieldCamera.normalized, BeltFieldCameraMeters, BeltCameraClearanceMeters);
        /// <summary>Anchor da revelação: o céu vazio, a sonda à esquerda embaixo e uma pedra distante (307 Nike) à direita em cima.</summary>
        public static readonly Vector3 BeltNikeCamera = new Vector3(46f, 6f, -60f);
        public const float NikeDistanceMeters = 380f;
        public const float NikeRadiusMeters = 7f;
        public const float NikeDegreesRight = 10f;
        public const float NikeDegreesUp = 4f;
        static readonly Vector3 BeltNikeLookAt = OffsetLook(BeltNikeCamera, Vector3.zero, 10f, -6f);
        /// <summary>Posição de mundo da pedra solitária: dez graus à direita e quatro acima do centro do quadro do anchor.</summary>
        public static readonly Vector3 NikePosition = BeltNikeCamera + (OffsetLook(BeltNikeCamera, BeltNikeLookAt, NikeDegreesRight, -NikeDegreesUp) - BeltNikeCamera).normalized * NikeDistanceMeters;

        /// <summary>
        /// Onde a pedra solitária cai no quadro do anchor PIO.Belt.Nike (0–1, origem em cima à esquerda), para 16:9 e FOV vertical de
        /// 30°: a pedra está <see cref="NikeDegreesRight"/>° à direita e <see cref="NikeDegreesUp"/>° acima do centro.
        /// </summary>
        public static Vector2 NikeViewport
        {
            get
            {
                var halfV = Mathf.Tan(15f * Mathf.Deg2Rad);
                var halfH = halfV * (16f / 9f);
                return new Vector2(0.5f + 0.5f * Mathf.Tan(NikeDegreesRight * Mathf.Deg2Rad) / halfH, 0.5f - 0.5f * Mathf.Tan(NikeDegreesUp * Mathf.Deg2Rad) / halfV);
            }
        }

        static void FillNike(Mesh mesh)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            PioneerStageArt.AppendRock(vertices, normals, triangles, Vector3.zero, NikeRadiusMeters, 307);
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Instancia o modelo definitivo da PIO-002 (<c>Art/PioneerActors.prefab</c>) dentro de <c>ProbeSpin</c> e devolve os
        /// sockets nomeados do próprio modelo (os das hastes acompanham a articulação). A rotação continua sendo do
        /// <c>ProbeSpin</c> (<see cref="PioneerPresentation"/>); o <c>Spin_Root</c> do modelo fica parado. A instância é reaproveitada
        /// quando já existe, para o prefab do palco sair idêntico a cada reconstrução.
        /// </summary>
        static List<Transform> BuildProbe(Transform spin)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PioneerProbeBuilder.PrefabAssetPath);
            if (model == null)
                throw new System.InvalidOperationException($"{PioneerProbeBuilder.PrefabAssetPath} ausente: rode o builder {PioneerProbeBuilder.ProbeBuilderId} antes do palco.");

            GameObject instance = null;
            foreach (var child in spin.Cast<Transform>().ToList())
            {
                if (instance == null && child.name == ModelInstanceName && PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == model)
                    instance = child.gameObject;
                else
                    Object.DestroyImmediate(child.gameObject); // silhueta provisória e sockets antigos
            }

            if (instance == null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(model, spin);
                instance.name = ModelInstanceName;
            }

            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            var rig = instance.GetComponent<PioneerProbeRig>();
            var sockets = new List<Transform>();
            foreach (var name in PioneerProbeRig.RequiredSockets)
            {
                if (rig == null || !rig.TryGetSocket(name, out var socket))
                    throw new System.InvalidOperationException($"{ModelInstanceName}: socket '{name}' ausente no modelo da sonda.");
                sockets.Add(socket);
            }

            return sockets;
        }

        /// <summary>Centro e normal da placa no modelo (espaço do <c>Spin_Root</c>), para enquadrar o close sem constantes duplicadas.</summary>
        static void ModelPlaque(out Vector3 center, out Vector3 normal)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PioneerProbeBuilder.PrefabAssetPath);
            var rig = model != null ? model.GetComponent<PioneerProbeRig>() : null;
            if (rig == null || rig.Plaque == null)
                throw new System.InvalidOperationException($"{PioneerProbeBuilder.PrefabAssetPath} sem rig/placa: rode o builder {PioneerProbeBuilder.ProbeBuilderId} antes do palco.");

            center = model.transform.InverseTransformPoint(rig.Plaque.position);
            normal = model.transform.InverseTransformDirection(rig.Plaque.forward);
        }

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

        /// <summary>A câmera da aproximação de Júpiter (cena 3): 11 m atrás da sonda, um pouco acima e ao lado.</summary>
        /// <summary>
        /// A trajetória da sonda em volta de Júpiter (cena 5): chega em linha reta a 130 m do centro do planeta, curva uns 38° na direção dele
        /// (a gravidade) e segue em linha reta; deitada no plano horizontal do centro de Júpiter. É uma linha de composição, não de cálculo orbital.
        /// </summary>
        public static Vector3 TrajectoryIncoming => new Vector3(JupiterDirection.x, 0f, JupiterDirection.z).normalized;
        public static Vector3 TrajectorySide => Vector3.Cross(Vector3.up, TrajectoryIncoming).normalized;
        /// <summary>Plano alto da curva (cena 5d): 620 m acima do plano da trajetória, um pouco antes de Júpiter, olhando quase de cima.</summary>
        public static Vector3 BendCamera => JupiterPosition + Vector3.up * 620f - TrajectoryIncoming * 230f + TrajectorySide * 40f;
        public static Vector3 BendLookAt => JupiterPosition + TrajectoryIncoming * 40f + TrajectorySide * 70f;

        public static Vector3[] TrajectoryPoints()
        {
            var incoming = TrajectoryIncoming;
            var side = TrajectorySide;
            var points = new List<Vector3>();
            var position = JupiterPosition + side * 130f - incoming * 900f;
            position.y = JupiterPosition.y;
            const float step = 12f;
            for (var traveled = 0f; traveled < 1800f; traveled += step)
            {
                points.Add(position);
                var u = Mathf.Clamp01((traveled - 700f) / 400f); // a curva acontece perto do ponto mais próximo
                var turn = 38f * Mathf.Deg2Rad * (u * u * (3f - 2f * u));
                var direction = incoming * Mathf.Cos(turn) - side * Mathf.Sin(turn); // dobra na direção do planeta
                position += direction * step;
            }

            return points.ToArray();
        }

        public static readonly Vector3 ApproachCamera = -JupiterDirection * 11f + Vector3.up * 3.2f + Vector3.Cross(Vector3.up, JupiterDirection).normalized * 3.5f;
        public static readonly Vector3 ApproachLookAt = OffsetLook(ApproachCamera, JupiterPosition, -7f, 4f);

        /// <summary>Ponto do mundo que cai no ponto <paramref name="viewport"/> do quadro (16:9, origem em cima à esquerda) de uma câmera, à distância dada.</summary>
        public static Vector3 ViewportPoint(Vector3 camera, Vector3 lookAt, float fovDegrees, Vector2 viewport, float distance)
        {
            var forward = (lookAt - camera).normalized;
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var up = Vector3.Cross(forward, right);
            var halfV = Mathf.Tan(fovDegrees * 0.5f * Mathf.Deg2Rad);
            var halfH = halfV * (16f / 9f);
            var direction = forward + right * ((viewport.x - 0.5f) * 2f * halfH) + up * ((0.5f - viewport.y) * 2f * halfV);
            return camera + direction.normalized * distance;
        }

        public static Vector3 EarthCompareTo => ViewportPoint(ApproachCamera, ApproachLookAt, ApproachOpenFov, EarthCompareViewport, (JupiterPosition - ApproachCamera).magnitude);
        public static Vector3 EarthCompareFrom => ViewportPoint(ApproachCamera, ApproachLookAt, ApproachOpenFov, EarthCompareEnterViewport, (JupiterPosition - ApproachCamera).magnitude);

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
            Group("PIO.Light.Cruise", new Vector3(-0.55f, -0.25f, -0.8f), new Color(0.84f, 0.9f, 1f), 1.25f, new Vector3(0.6f, 0.3f, 0.75f), new Color(0.45f, 0.55f, 0.75f), 0.12f),
            Group("PIO.Light.JupiterAxis", JupiterKeyDirection, new Color(1f, 0.95f, 0.88f), 1.3f, new Vector3(0.7f, 0.25f, 0.65f), new Color(0.4f, 0.48f, 0.65f), 0.08f),
            // Cena 6: luz de frente, um pouco de cima e da direita, para a placa dourada brilhar sem estourar.
            Group("PIO.Light.PlaqueHero", new Vector3(0.25f, -0.3f, 0.92f), new Color(1f, 0.9f, 0.76f), 1.5f, new Vector3(-0.4f, 0.2f, 0.9f), new Color(0.45f, 0.5f, 0.65f), 0.2f),
            Group("PIO.Light.Plaque", new Vector3(0.36f, -0.86f, 0.38f), new Color(1f, 0.88f, 0.72f), 1.1f, new Vector3(0.3f, -0.6f, 0.7f), new Color(0.45f, 0.5f, 0.65f), 0.15f),
            Group("PIO.Light.FarSun", new Vector3(-0.1f, -0.15f, -1f), new Color(0.86f, 0.92f, 1f), 0.75f, new Vector3(0.4f, 0.3f, 1f), new Color(0.35f, 0.4f, 0.55f), 0.05f),
            // Fase 5: na Terra (plataforma de lançamento e antena da Deep Space Network) a luz é quente; no espaço, fria.
            Group("PIO.Light.EarthWarm", new Vector3(0.3f, -0.6f, 0.7f), new Color(1f, 0.74f, 0.46f), 0.9f, new Vector3(-0.4f, -0.3f, -0.8f), new Color(0.42f, 0.3f, 0.26f), 0.1f),
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
            public bool Jupiter, Aldebaran, BeltField, BeltNike, Mars, Moons, Trajectory, PlaqueHero;
            public Rect UiFrame;
        }

        /// <summary>Anchors na ordem do roteiro. Estados = um anchor cada; planos extras do lançamento antes do anchor do estado.</summary>
        static List<AnchorSpec> AnchorSpecs()
        {
            // A placa vem do modelo (PIO-002). A sonda gira até o azimute da placa coincidir com o da CÂMERA (AlignSocket); por
            // isso a câmera fica no plano (radial, eixo) da placa, ao longo da componente da normal nesse plano (sem
            // deslocamento lateral, que desalinharia). A placa cai no centro do quadro depois do alinhamento.
            ModelPlaque(out var plaqueCenter, out var modelPlaqueNormal);
            var plaqueRadial = new Vector3(0.8f, 0.6f, 0f);
            var plaqueTurn = Quaternion.AngleAxis(Mathf.Atan2(plaqueRadial.y, plaqueRadial.x) * Mathf.Rad2Deg - Mathf.Atan2(plaqueCenter.y, plaqueCenter.x) * Mathf.Rad2Deg, Vector3.forward);
            var plaqueNormal = plaqueTurn * modelPlaqueNormal;
            var plaqueAligned = plaqueTurn * plaqueCenter + plaqueNormal * 0.01f;
            var plaqueView = (plaqueRadial * Vector3.Dot(plaqueNormal, plaqueRadial) + Vector3.forward * plaqueNormal.z).normalized;
            var plaqueCamera = plaqueAligned + plaqueView * 1.3f;
            var jupiterRight = Vector3.Cross(Vector3.up, JupiterDirection).normalized;
            var imagingCamera = new Vector3(1.1f, 1.9f, -2.5f) + jupiterRight * 1.3f;
            var jupiterFlat = new Vector3(JupiterDirection.x, JupiterDirection.y, 0f).normalized;
            var instrument = jupiterFlat * (CompartmentRadiusMeters + 0.06f) + Vector3.forward * -0.30f;
            var instrumentCamera = instrument + jupiterFlat * 1.1f + Vector3.forward * -0.9f + Vector3.Cross(Vector3.forward, jupiterFlat) * 0.35f;
            var approachCamera = ApproachCamera;
            // Máxima aproximação (cena 5): ~130 m do centro de Júpiter (raio 80 m), um pouco acima e ao lado do eixo de chegada.
            var closestCamera = JupiterPosition - JupiterDirection * 130f + Vector3.up * 22f + jupiterRight * 24f;
            var aldebaran = AldebaranDirection.normalized;
            var farewellCamera = new Vector3(-8f, 4f, 14f);
            var awayForward = FarewellAwayForward();
            var awayCamera = -awayForward * FarewellAwayDistanceMeters;
            var farewellLook = farewellCamera + (aldebaran * 0.55f + (-farewellCamera).normalized * 0.45f).normalized * 60f;

            return new List<AnchorSpec>
            {
                new AnchorSpec { Id = "PIO.Launch.Reveal", State = "PIO.Launch", Function = "PIO01.A · contorno do refletor em contraluz fria",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.ColdRim", Fov = 34f,
                    Position = new Vector3(3.4f, 0.9f, 5.6f), LookAt = new Vector3(0f, 0.05f, 0.2f) },
                new AnchorSpec { Id = "PIO.Launch.Pad", State = "PIO.Launch", Function = "PIO01.B · plano baixo lateral do Atlas-Centaur à noite (silhueta da PIO-004 no AtlasSocket)",
                    Region = PioneerStageRegion.LaunchSite, Light = "PIO.Light.EarthWarm", Fov = 50f,
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
                new AnchorSpec { Id = "PIO.Belt.Mars", State = "PIO.Belt", Function = "Cena 2 · abertura: a sonda cruza a órbita de Marte; o planeta ao fundo, à direita (Marte ligado)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Cruise", Fov = 30f, Mars = true,
                    Position = BeltMarsCamera, LookAt = Vector3.zero },
                new AnchorSpec { Id = "PIO.Belt.Field", State = "PIO.Belt", Function = "Cena 2 · teleobjetiva e dolly zoom: a sonda pequena na frente de uma parede de pedras que se abre (campo de pedras ligado)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Cruise", Fov = 30f, BeltField = true,
                    Position = BeltFieldCamera, LookAt = Vector3.zero },
                new AnchorSpec { Id = "PIO.Belt.Nike", State = "PIO.Belt", Function = "Cena 2 · a revelação: espaço vazio e uma única pedra distante (307 Nike), a milhões de km",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Cruise", Fov = 30f, BeltNike = true,
                    Position = BeltNikeCamera, LookAt = BeltNikeLookAt },
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
                new AnchorSpec { Id = "PIO.Closest", State = "PIO.Closest", Function = "PIO05 (roteiro v2, cena 5) · a máxima aproximação: Júpiter enche o quadro, a poucas dezenas de metros do topo das nuvens (escala de composição)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.JupiterAxis", Fov = 55f, Jupiter = true,
                    Position = closestCamera, LookAt = JupiterPosition + Vector3.down * 12f },
                new AnchorSpec { Id = "PIO.Closest.Ganymede", State = "PIO.Closest", Function = "Cena 5a · Ganimedes cruza o quadro (as luas ligadas)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.JupiterAxis", Fov = 30f, Moons = true,
                    Position = MoonCamera(0), LookAt = MoonSpecs[0].Position },
                new AnchorSpec { Id = "PIO.Closest.Callisto", State = "PIO.Closest", Function = "Cena 5a · Calisto cruza o quadro (as luas ligadas)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.JupiterAxis", Fov = 30f, Moons = true,
                    Position = MoonCamera(1), LookAt = MoonSpecs[1].Position },
                new AnchorSpec { Id = "PIO.Closest.Europa", State = "PIO.Closest", Function = "Cena 5a · Europa cruza o quadro (as luas ligadas)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.JupiterAxis", Fov = 30f, Moons = true,
                    Position = MoonCamera(2), LookAt = MoonSpecs[2].Position },
                new AnchorSpec { Id = "PIO.Closest.Bend", State = "PIO.Closest", Function = "Cena 5d · plano alto: a trajetória da sonda curva em volta de Júpiter (trajetória ligada)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.JupiterAxis", Fov = 50f, Jupiter = true, Trajectory = true,
                    Position = BendCamera, LookAt = BendLookAt },
                new AnchorSpec { Id = "PIO.Plaque", State = "PIO.Plaque", Function = "Placa presa aos suportes da antena; rotação visual suspensa só neste close",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.Plaque", Fov = 36f, Jupiter = true, SpinScale = 0f,
                    AlignSocket = "Plaque", Position = plaqueCamera, LookAt = plaqueAligned },
                new AnchorSpec { Id = "PIO.Plaque.Board", State = "PIO.Plaque", Function = "Cena 6 · a placa dourada inteira, de frente (plano geral); os zooms das peças partem daqui",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.PlaqueHero", Fov = PlaqueHeroFov, PlaqueHero = true, SpinScale = 0f,
                    Position = PlaqueHeroCenter + new Vector3(0f, 0f, -PlaqueOverviewDistance), LookAt = PlaqueHeroCenter },
                new AnchorSpec { Id = "PIO.Outbound", State = "PIO.Outbound", Function = "Sonda se afasta com a antena voltada à Terra; o diagrama orbital 2D (PIO-010) assume o quadro",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.FarSun", Fov = 40f,
                    Position = new Vector3(-4.5f, -1.8f, 17f), LookAt = new Vector3(0f, 0f, 0f) },
                new AnchorSpec { Id = "PIO.Listening", State = "PIO.Listening", Function = "Antena terrestre sob céu noturno (silhueta DSN da PIO-011 no DishSocket); painel de onda embaixo à direita",
                    Region = PioneerStageRegion.GroundStation, Light = "PIO.Light.EarthWarm", Fov = 55f,
                    Position = GroundStationCenter + new Vector3(0f, 1.7f, -36f), LookAt = GroundStationCenter + new Vector3(0f, 14f, 0f),
                    UiFrame = new Rect(0.56f, 0.07f, 0.40f, 0.33f) },
                new AnchorSpec { Id = "PIO.Farewell", State = "PIO.Farewell", Function = "Pioneer diminui no campo de estrelas, na direção geral de Aldebaran (sem iluminá-la)",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.FarewellRim", Fov = 45f, Aldebaran = true,
                    Position = farewellCamera, LookAt = farewellLook },
                new AnchorSpec { Id = "PIO.Farewell.Away", State = "PIO.Farewell", Function = "A Pioneer se afasta, a 16 m (sem colar a câmera), com Aldebaran grande e brilhante no alto à esquerda",
                    Region = PioneerStageRegion.DeepSpace, Light = "PIO.Light.FarewellRim", Fov = FarewellAwayFov, Aldebaran = true,
                    Position = awayCamera, LookAt = Vector3.zero },
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
                    showBeltField = spec.BeltField,
                    showBeltNike = spec.BeltNike,
                    showMars = spec.Mars,
                    showMoons = spec.Moons,
                    showPlaqueHero = spec.PlaqueHero,
                    showTrajectory = spec.Trajectory,
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
            p.FindPropertyRelative("showBeltField").boolValue = a.showBeltField;
            p.FindPropertyRelative("showBeltNike").boolValue = a.showBeltNike;
            p.FindPropertyRelative("showMars").boolValue = a.showMars;
            p.FindPropertyRelative("showMoons").boolValue = a.showMoons;
            p.FindPropertyRelative("showPlaqueHero").boolValue = a.showPlaqueHero;
            p.FindPropertyRelative("showTrajectory").boolValue = a.showTrajectory;
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
