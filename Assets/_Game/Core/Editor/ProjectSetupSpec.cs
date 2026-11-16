using System.Collections.Generic;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Fonte única da configuração fixada em CORE-001. ProjectSetup aplica e ProjectSetupValidator confere
    /// exatamente estes valores; mudar um valor aqui exige PR revisado por R1 e atualização do README.
    /// </summary>
    public static class ProjectSetupSpec
    {
        public const string EditorVersion = "6000.6.7f1";
        public const string UrpPackageName = "com.unity.render-pipelines.universal";
        public const string UrpPackageVersion = "17.6.0";
        public const string InputSystemPackageName = "com.unity.inputsystem";
        public const string InputSystemPackageVersion = "1.20.1";
        public const string McpForUnityPackageName = "com.coplaydev.unity-mcp";
        public const string McpForUnityPackageSource = "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#5914ce5bd9d11d66a7896ce72e395b0a2f049c2a";

        public const string CompanyName = "Abandoned But Not Forgotten";
        public const string ProductName = "Abandoned, But Not Forgotten";

        public const string GameRoot = "Assets/_Game";
        public const string UrpFolder = GameRoot + "/Settings/URP";
        public const string PipelineAssetPath = UrpFolder + "/SpaceApps_URP.asset";
        public const string RendererDataPath = UrpFolder + "/SpaceApps_URP_Renderer.asset";
        public const string GlobalSettingsPath = UrpFolder + "/SpaceApps_URP_GlobalSettings.asset";
        public const string DefaultVolumeProfilePath = UrpFolder + "/SpaceApps_URP_DefaultVolumeProfile.asset";
        public const string DefaultPostProcessDataPath = "Packages/" + UrpPackageName + "/Runtime/Data/PostProcessData.asset";

        public const string EditorAssemblyName = "Abandoned.SpaceApps.Editor";
        public const string VisibleMetaFilesMode = "Visible Meta Files";

        /// <summary>Valor serializado de PlayerSettings.activeInputHandler: 0 antigo, 1 Input System, 2 ambos.</summary>
        public const int InputSystemOnlyHandler = 1;

        /// <summary>Renderer Forward (0). Forward+ e Deferred ficam fora do perfil WebGL 2 do MVP.</summary>
        public const int ForwardRenderingMode = 0;

        public const string ManifestPath = "Packages/manifest.json";

        /// <summary>
        /// Dependências diretas aprovadas. A Unity pode reinserir seus pacotes padrão ao criar um projeto;
        /// o validador rejeita qualquer dependência direta fora desta lista. Novo pacote = PR com uso concreto e revisão de R1.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> ManifestDependencies = new Dictionary<string, string>
        {
            // MCP for Unity (Coplay): ponte do Editor com assistentes de IA (cena, testes, build, console) para a equipe.
            // Fixado num commit; atualizar só com PR. Uso no Editor; o impacto no build Web está medido no PR da liberação.
            { McpForUnityPackageName, McpForUnityPackageSource },
            { "com.unity.ide.rider", "3.1.0" },
            { "com.unity.ide.visualstudio", "2.0.26" },
            { InputSystemPackageName, InputSystemPackageVersion },
            { UrpPackageName, UrpPackageVersion },
            { "com.unity.test-framework", "1.8.0" },
            { "com.unity.modules.animation", "1.0.0" },
            { "com.unity.modules.audio", "1.0.0" },
            { "com.unity.modules.imageconversion", "1.0.0" },
            { "com.unity.modules.imgui", "1.0.0" },
            { "com.unity.modules.jsonserialize", "1.0.0" },
            { "com.unity.modules.particlesystem", "1.0.0" },
            { "com.unity.modules.physics", "1.0.0" },
            { "com.unity.modules.ui", "1.0.0" },
            { "com.unity.modules.uielements", "1.0.0" },
            { "com.unity.modules.umbra", "1.0.0" },
        };

        public static readonly string[] RequiredFolders =
        {
            GameRoot + "/Core",
            GameRoot + "/Core/Contracts",
            GameRoot + "/Presentation",
            GameRoot + "/UI",
            GameRoot + "/UI/Design",
            GameRoot + "/UI/Dossier",
            GameRoot + "/Chapters/Mars/Runtime",
            GameRoot + "/Chapters/Mars/Data",
            GameRoot + "/Chapters/Mars/Prefabs",
            GameRoot + "/Chapters/Mars/Art",
            GameRoot + "/Chapters/Mars/Scenes",
            GameRoot + "/Chapters/Mars/Timelines",
            GameRoot + "/Chapters/Pioneer10/Scripts",
            GameRoot + "/Chapters/Pioneer10/Data",
            GameRoot + "/Chapters/Pioneer10/Prefabs",
            GameRoot + "/Chapters/Pioneer10/Art",
            GameRoot + "/Chapters/Pioneer10/Scenes",
            GameRoot + "/Chapters/Surveyor/Scripts",
            GameRoot + "/Chapters/Surveyor/Data",
            GameRoot + "/Chapters/Surveyor/Prefabs",
            GameRoot + "/Chapters/Surveyor/Art",
            GameRoot + "/Chapters/Surveyor/Scenes",
            GameRoot + "/Shared/Materials",
            GameRoot + "/Shared/Fonts",
            GameRoot + "/Shared/Audio",
            GameRoot + "/Shared/VFX",
            GameRoot + "/Shared/Localization",
            GameRoot + "/Editor",
            GameRoot + "/Tests/EditMode",
            GameRoot + "/Tests/PlayMode",
            GameRoot + "/Scenes",
            UrpFolder,
            GameRoot + "/Settings/Web",
        };

        /// <summary>
        /// Linha de base do URP alinhada a Docs/04_WEB_AND_QA.md: uma luz principal com sombra de 1024 px e
        /// duas cascatas, fills sem sombra, sem MSAA, sem texturas de profundidade/opaca (sem SSAO/DOF).
        /// O perfil Leve medido no navegador pertence a QA-001 e pode derivar desta base.
        /// </summary>
        public static readonly IReadOnlyList<PipelineSetting> PipelineSettings = new[]
        {
            PipelineSetting.Bool("m_SupportsHDR", true),
            PipelineSetting.Int("m_MSAA", 1),
            PipelineSetting.Float("m_RenderScale", 1f),
            PipelineSetting.Int("m_MainLightRenderingMode", 1),
            PipelineSetting.Bool("m_MainLightShadowsSupported", true),
            PipelineSetting.Int("m_MainLightShadowmapResolution", 1024),
            PipelineSetting.Int("m_ShadowCascadeCount", 2),
            PipelineSetting.Float("m_ShadowDistance", 40f),
            PipelineSetting.Bool("m_SoftShadowsSupported", false),
            PipelineSetting.Int("m_AdditionalLightsRenderingMode", 1),
            PipelineSetting.Int("m_AdditionalLightsPerObjectLimit", 2),
            PipelineSetting.Bool("m_AdditionalLightShadowsSupported", false),
            PipelineSetting.Bool("m_RequireDepthTexture", false),
            PipelineSetting.Bool("m_RequireOpaqueTexture", false),
            PipelineSetting.Bool("m_UseSRPBatcher", true),
            PipelineSetting.Bool("m_EnableLODCrossFade", false),
        };

        public enum SettingKind
        {
            Bool,
            Int,
            Float,
        }

        /// <summary>Um campo serializado do UniversalRenderPipelineAsset e seu valor exigido.</summary>
        public readonly struct PipelineSetting
        {
            public readonly string PropertyPath;
            public readonly SettingKind Kind;
            public readonly bool BoolValue;
            public readonly int IntValue;
            public readonly float FloatValue;

            PipelineSetting(string propertyPath, SettingKind kind, bool boolValue, int intValue, float floatValue)
            {
                PropertyPath = propertyPath;
                Kind = kind;
                BoolValue = boolValue;
                IntValue = intValue;
                FloatValue = floatValue;
            }

            public static PipelineSetting Bool(string path, bool value) => new PipelineSetting(path, SettingKind.Bool, value, 0, 0f);
            public static PipelineSetting Int(string path, int value) => new PipelineSetting(path, SettingKind.Int, false, value, 0f);
            public static PipelineSetting Float(string path, float value) => new PipelineSetting(path, SettingKind.Float, false, 0, value);

            public string ExpectedText => Kind switch
            {
                SettingKind.Bool => BoolValue.ToString(),
                SettingKind.Int => IntValue.ToString(),
                _ => FloatValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
        }
    }
}
