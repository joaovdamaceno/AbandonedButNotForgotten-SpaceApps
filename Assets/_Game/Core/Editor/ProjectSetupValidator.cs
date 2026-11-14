using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Confere, sem alterar nada, se o projeto corresponde a ProjectSetupSpec. Cada divergência vira uma linha
    /// legível; lista vazia significa configuração conforme. Linha de comando: -executeMethod
    /// Abandoned.SpaceApps.EditorTools.ProjectSetupValidator.ValidateFromCommandLine (código 0 = conforme).
    /// </summary>
    public static class ProjectSetupValidator
    {
        [MenuItem("Abandoned Space Apps/Validar configuração do projeto", priority = 1)]
        public static void ValidateFromMenu() => Report(Collect());

        public static void ValidateFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                var issues = Collect();
                Report(issues);
                exitCode = issues.Count == 0 ? 0 : 1;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        public static void Report(IReadOnlyList<string> issues)
        {
            if (issues.Count == 0)
            {
                Debug.Log($"[ProjectSetup] Configuração conforme CORE-001 (Unity {Application.unityVersion}, URP {ResolvedVersion(ProjectSetupSpec.UrpPackageName)}).");
                return;
            }

            Debug.LogError($"[ProjectSetup] {issues.Count} divergência(s):\n- " + string.Join("\n- ", issues));
        }

        public static List<string> Collect()
        {
            var issues = new List<string>();
            CheckVersions(issues);
            CheckEditorSettings(issues);
            CheckPlayerSettings(issues);
            CheckFolders(issues);
            CheckEditorAssemblies(issues);
            CheckPipeline(issues);
            return issues;
        }

        static void CheckVersions(List<string> issues)
        {
            if (Application.unityVersion != ProjectSetupSpec.EditorVersion)
                issues.Add($"Editor {Application.unityVersion}; esperado {ProjectSetupSpec.EditorVersion}.");

            Expect(issues, ProjectSetupSpec.UrpPackageName, ProjectSetupSpec.UrpPackageVersion);
            Expect(issues, ProjectSetupSpec.InputSystemPackageName, ProjectSetupSpec.InputSystemPackageVersion);
            CheckManifest(issues);
        }

        /// <summary>Lê as dependências diretas de Packages/manifest.json (nome → versão).</summary>
        public static Dictionary<string, string> ReadManifestDependencies()
        {
            var text = File.ReadAllText(ProjectSetupSpec.ManifestPath);
            var start = text.IndexOf("\"dependencies\"", System.StringComparison.Ordinal);
            var open = start < 0 ? -1 : text.IndexOf('{', start);
            var close = open < 0 ? -1 : text.IndexOf('}', open);
            var result = new Dictionary<string, string>();
            if (close < 0)
                return result;

            var body = text.Substring(open + 1, close - open - 1);
            foreach (Match match in Regex.Matches(body, @"""([^""]+)""\s*:\s*""([^""]+)"""))
                result[match.Groups[1].Value] = match.Groups[2].Value;
            return result;
        }

        static void CheckManifest(List<string> issues)
        {
            var actual = ReadManifestDependencies();
            foreach (var pair in actual)
            {
                if (!ProjectSetupSpec.ManifestDependencies.TryGetValue(pair.Key, out var expected))
                    issues.Add($"Dependência direta não aprovada no manifesto: {pair.Key} {pair.Value}.");
                else if (expected != pair.Value)
                    issues.Add($"Manifesto fixa {pair.Key} {pair.Value}; esperado {expected}.");
            }

            foreach (var pair in ProjectSetupSpec.ManifestDependencies.Where(p => !actual.ContainsKey(p.Key)))
                issues.Add($"Manifesto sem a dependência aprovada {pair.Key} {pair.Value}.");
        }

        static void Expect(List<string> issues, string packageName, string version)
        {
            var resolved = ResolvedVersion(packageName);
            if (resolved != version)
                issues.Add($"Pacote {packageName} resolvido como '{resolved ?? "ausente"}'; esperado {version}.");
        }

        static string ResolvedVersion(string packageName) => PackageInfo.FindForAssetPath("Packages/" + packageName)?.version;

        static void CheckEditorSettings(List<string> issues)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
                issues.Add($"Serialização {EditorSettings.serializationMode}; esperado ForceText.");

            if (VersionControlSettings.mode != ProjectSetupSpec.VisibleMetaFilesMode)
                issues.Add($"Controle de versão '{VersionControlSettings.mode}'; esperado '{ProjectSetupSpec.VisibleMetaFilesMode}'.");
        }

        static void CheckPlayerSettings(List<string> issues)
        {
            if (PlayerSettings.productName != ProjectSetupSpec.ProductName)
                issues.Add($"productName '{PlayerSettings.productName}'; esperado '{ProjectSetupSpec.ProductName}'.");

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                issues.Add($"Espaço de cor {PlayerSettings.colorSpace}; esperado Linear.");

            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL) || apis.Length != 1 || apis[0] != GraphicsDeviceType.OpenGLES3)
                issues.Add("WebGL deve usar somente WebGL 2 (OpenGLES3), sem APIs automáticas.");

            if (PlayerSettings.WebGL.threadsSupport)
                issues.Add("WebGL threads ativado; o MVP exige threads desabilitadas.");

            var playerSettings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var handler = playerSettings.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue != ProjectSetupSpec.InputSystemOnlyHandler)
                issues.Add($"Active Input Handling = {handler?.intValue.ToString() ?? "?"}; esperado {ProjectSetupSpec.InputSystemOnlyHandler} (somente Input System).");
        }

        static void CheckFolders(List<string> issues)
        {
            foreach (var folder in ProjectSetupSpec.RequiredFolders.Where(f => !AssetDatabase.IsValidFolder(f)))
                issues.Add($"Pasta obrigatória ausente: {folder}.");
        }

        /// <summary>Pastas chamadas Editor sob _Game sem asmdef/asmref compilariam código de Editor no runtime.</summary>
        public static List<string> FindEditorFoldersWithoutAssembly()
        {
            var missing = new List<string>();
            if (!Directory.Exists(ProjectSetupSpec.GameRoot))
                return missing;

            foreach (var directory in Directory.GetDirectories(ProjectSetupSpec.GameRoot, "Editor", SearchOption.AllDirectories))
            {
                var hasAssembly = Directory.GetFiles(directory, "*.asmdef").Length > 0
                    || Directory.GetFiles(directory, "*.asmref").Length > 0;
                if (!hasAssembly)
                    missing.Add(directory.Replace('\\', '/'));
            }

            return missing;
        }

        static void CheckEditorAssemblies(List<string> issues)
        {
            foreach (var folder in FindEditorFoldersWithoutAssembly())
                issues.Add($"{folder} não tem .asmref para {ProjectSetupSpec.EditorAssemblyName}.");
        }

        static void CheckPipeline(List<string> issues)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectSetupSpec.PipelineAssetPath);
            if (pipeline == null)
            {
                issues.Add($"Asset URP ausente em {ProjectSetupSpec.PipelineAssetPath}.");
                return;
            }

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
                issues.Add("GraphicsSettings.defaultRenderPipeline não aponta para o asset URP do projeto.");

            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (var i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var custom = level.FindPropertyRelative("customRenderPipeline")?.objectReferenceValue;
                if (custom != null && custom != pipeline && !(custom is UniversalRenderPipelineAsset))
                    issues.Add($"Nível de qualidade '{level.FindPropertyRelative("name").stringValue}' usa pipeline não URP.");
            }

            var pipelineObject = new SerializedObject(pipeline);
            foreach (var setting in ProjectSetupSpec.PipelineSettings)
            {
                var property = pipelineObject.FindProperty(setting.PropertyPath);
                if (property == null)
                {
                    issues.Add($"Campo URP '{setting.PropertyPath}' inexistente.");
                    continue;
                }

                var matches = setting.Kind switch
                {
                    ProjectSetupSpec.SettingKind.Bool => property.boolValue == setting.BoolValue,
                    ProjectSetupSpec.SettingKind.Int => property.intValue == setting.IntValue,
                    _ => Mathf.Approximately(property.floatValue, setting.FloatValue),
                };

                if (!matches)
                    issues.Add($"URP {setting.PropertyPath}: esperado {setting.ExpectedText}.");
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ProjectSetupSpec.RendererDataPath);
            if (renderer == null)
            {
                issues.Add($"Renderer URP ausente em {ProjectSetupSpec.RendererDataPath}.");
                return;
            }

            var rendererList = pipelineObject.FindProperty("m_RendererDataList");
            if (rendererList == null || rendererList.arraySize != 1 || rendererList.GetArrayElementAtIndex(0).objectReferenceValue != renderer)
                issues.Add("O asset URP deve ter exatamente um renderer: SpaceApps_URP_Renderer.");

            var mode = new SerializedObject(renderer).FindProperty("m_RenderingMode");
            if (mode == null || mode.intValue != ProjectSetupSpec.ForwardRenderingMode)
                issues.Add("Renderer URP deve usar o modo Forward.");

            if (renderer.postProcessData == null)
                issues.Add("Renderer URP sem PostProcessData (shaders de pós-processamento seriam removidos).");

            var global = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            var globalPath = global == null ? null : AssetDatabase.GetAssetPath(global);
            if (globalPath != ProjectSetupSpec.GlobalSettingsPath)
                issues.Add($"URP Global Settings em '{globalPath ?? "ausente"}'; esperado {ProjectSetupSpec.GlobalSettingsPath}.");

            var profile = GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>()?.volumeProfile;
            var profilePath = profile == null ? null : AssetDatabase.GetAssetPath(profile);
            if (profilePath != ProjectSetupSpec.DefaultVolumeProfilePath)
                issues.Add($"Perfil de volume padrão em '{profilePath ?? "ausente"}'; esperado {ProjectSetupSpec.DefaultVolumeProfilePath}.");
        }
    }
}
