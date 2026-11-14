using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// CORE-006: build reproduzível WebGL/Windows.
    /// <list type="number">
    /// <item>O build só roda com o alvo JÁ ativo. Pelo menu, troca o alvo, espera importação e recarga do domínio e
    /// retoma sozinho; na linha de comando o alvo vem de <c>-buildTarget</c> (Tools/build_release.py) e uma divergência
    /// é erro. <c>BuildPlayerOptions.target</c> nunca substitui a troca de plataforma.</item>
    /// <item>Pré-verificação: configuração do projeto (CORE-001), dados de contrato (CORE-002), cenas e hierarquias
    /// (scripts, materiais, shaders e referências) e perfil Web da QA-001 quando existir.</item>
    /// <item>Pós-build: todo shader de que as cenas dependem precisa estar empacotado (não removido); relatório JSON com
    /// arquivos reais, tamanhos e SHA-256 para identificar a build testada.</item>
    /// </list>
    /// </summary>
    public static class SpaceAppsBuild
    {
        public enum Platform
        {
            WebGL = 0,
            Windows = 1,
        }

        public const string ReportFileName = "space-apps-build.json";
        public const string WebProfileFolder = "Assets/_Game/Settings/Web";
        const string PendingKey = "Abandoned.SpaceApps.PendingBuild";

        [Serializable]
        public sealed class FileEntry
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        public sealed class BuildReportData
        {
            public string unityVersion;
            public string platform;
            public string commit;
            public string startedUtc;
            public float durationSeconds;
            public string result;
            public string outputPath;
            public List<string> scenes = new List<string>();
            public string renderPipeline;
            public List<string> warnings = new List<string>();
            public List<string> errors = new List<string>();
            public List<string> shadersChecked = new List<string>();
            public List<string> shadersMissing = new List<string>();
            public List<FileEntry> files = new List<FileEntry>();
            public long totalBytes;
        }

        public sealed class BuildRequest
        {
            public Platform Platform;
            public string OutputPath;

            /// <summary>Cenas a incluir; nulo = cenas habilitadas nas Build Settings (INT-001).</summary>
            public string[] Scenes;

            public string Commit = "desconhecido";
            public bool Development;
        }

        // ---------- Menus ----------

        [MenuItem("Abandoned Space Apps/CORE-006/Build WebGL", priority = 50)]
        public static void BuildWebGLFromMenu() => BuildFromMenu(Platform.WebGL);

        [MenuItem("Abandoned Space Apps/CORE-006/Build Windows", priority = 51)]
        public static void BuildWindowsFromMenu() => BuildFromMenu(Platform.Windows);

        [MenuItem("Abandoned Space Apps/CORE-006/Pré-verificação do build", priority = 52)]
        public static void PreflightFromMenu()
        {
            var report = new BuildReportData();
            Preflight(new BuildRequest { Platform = ActivePlatformOrWebGL() }, report);
            LogPreflight(report);
        }

        static void BuildFromMenu(Platform platform)
        {
            if (IsActive(platform))
            {
                Build(new BuildRequest { Platform = platform, OutputPath = DefaultOutput(platform) });
                return;
            }

            // Troca a plataforma e só retoma depois da importação e da recarga do domínio (ResumePendingBuild).
            SessionState.SetString(PendingKey, platform.ToString());
            Debug.Log($"[CORE-006] Trocando a plataforma ativa para {platform}; o build continua sozinho após a importação.");
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(GroupOf(platform), TargetOf(platform));
        }

        [InitializeOnLoadMethod]
        static void ResumePendingBuild()
        {
            var pending = SessionState.GetString(PendingKey, null);
            if (string.IsNullOrEmpty(pending) || Application.isBatchMode)
                return;

            EditorApplication.delayCall += WaitAndBuild;

            void WaitAndBuild()
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += WaitAndBuild;
                    return;
                }

                SessionState.EraseString(PendingKey);
                var platform = (Platform)Enum.Parse(typeof(Platform), pending);
                if (IsActive(platform))
                    Build(new BuildRequest { Platform = platform, OutputPath = DefaultOutput(platform) });
                else
                    Debug.LogError($"[CORE-006] A troca para {platform} não terminou; nenhum build foi feito.");
            }
        }

        // ---------- Linha de comando ----------

        /// <summary>
        /// <c>-executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsBuild.BuildFromCommandLine -buildTarget WebGL
        /// -spaceAppsPlatform WebGL [-buildOutput dir] [-buildScenes a.unity;b.unity] [-buildCommit sha]</c>. Saída 0 = sucesso.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            var exitCode = 1;
            try
            {
                var report = Build(ReadRequest());
                exitCode = report.result == "Succeeded" ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        /// <summary>Só a pré-verificação (sem build). Saída 0 = nada bloqueia.</summary>
        public static void PreflightFromCommandLine()
        {
            var exitCode = 1;
            try
            {
                var report = new BuildReportData();
                Preflight(ReadRequest(), report);
                LogPreflight(report);
                exitCode = report.errors.Count == 0 ? 0 : 1;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        static BuildRequest ReadRequest()
        {
            var platformText = SpaceAppsSceneBuilder.ReadArgument("-spaceAppsPlatform");
            if (!Enum.TryParse(platformText, true, out Platform platform))
                throw new ArgumentException("Informe -spaceAppsPlatform WebGL|Windows.");

            var scenes = SpaceAppsSceneBuilder.ReadArgument("-buildScenes");
            return new BuildRequest
            {
                Platform = platform,
                OutputPath = SpaceAppsSceneBuilder.ReadArgument("-buildOutput") ?? DefaultOutput(platform),
                Scenes = string.IsNullOrEmpty(scenes) ? null : scenes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                Commit = SpaceAppsSceneBuilder.ReadArgument("-buildCommit") ?? "desconhecido",
            };
        }

        // ---------- Build ----------

        public static BuildReportData Build(BuildRequest request)
        {
            var started = DateTime.UtcNow;
            var report = new BuildReportData
            {
                unityVersion = Application.unityVersion,
                platform = request.Platform.ToString(),
                commit = request.Commit,
                startedUtc = started.ToString("o"),
                outputPath = Path.GetFullPath(request.OutputPath ?? DefaultOutput(request.Platform)).Replace('\\', '/'),
            };

            Preflight(request, report);
            if (report.errors.Count > 0)
            {
                report.result = "PreflightFailed";
                return Finish(report, started);
            }

            Directory.CreateDirectory(report.outputPath);
            var location = request.Platform == Platform.Windows
                ? Path.Combine(report.outputPath, PlayerSettings.productName.Replace(",", string.Empty) + ".exe")
                : report.outputPath;

            var options = new BuildPlayerOptions
            {
                scenes = report.scenes.ToArray(),
                locationPathName = location,
                target = TargetOf(request.Platform),
                targetGroup = GroupOf(request.Platform),
                options = request.Development ? BuildOptions.Development : BuildOptions.None,
            };

            var buildReport = BuildPipeline.BuildPlayer(options);
            report.result = buildReport.summary.result.ToString();
            foreach (var step in buildReport.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        report.errors.Add(message.content);
                }
            }

            if (buildReport.summary.result == BuildResult.Succeeded)
            {
                VerifyPackedShaders(buildReport, report);
                CollectFiles(report);
                if (report.shadersMissing.Count > 0)
                    report.result = "ShadersStripped";
            }

            return Finish(report, started);
        }

        /// <summary>Tudo que impede um build confiável, sem compilar nada.</summary>
        public static void Preflight(BuildRequest request, BuildReportData report)
        {
            if (!IsActive(request.Platform))
            {
                report.errors.Add($"Plataforma ativa é {EditorUserBuildSettings.activeBuildTarget}; troque para {request.Platform} ANTES do build " +
                    "(menu do CORE-006 ou -buildTarget no build_release.py). BuildPlayerOptions.target não substitui a troca.");
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                report.errors.Add("O Editor ainda está compilando/importando; aguarde antes do build.");

            foreach (var issue in ProjectSetupValidator.Collect())
                report.errors.Add("Projeto: " + issue);
            foreach (var issue in ContractAssetValidator.Collect())
                report.errors.Add("Contratos: " + issue);

            report.scenes = (request.Scenes ?? EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray()).ToList();
            if (report.scenes.Count == 0)
                report.errors.Add("Nenhuma cena para o build (Build Settings vazias; INT-001 registra Boot/Dossiê e capítulos).");

            foreach (var scenePath in report.scenes)
            {
                if (!File.Exists(scenePath))
                {
                    report.errors.Add($"Cena inexistente: {scenePath}.");
                    continue;
                }

                foreach (var issue in ValidateSceneFile(scenePath))
                    report.errors.Add(issue);
            }

            report.renderPipeline = DescribePipelineFor(request.Platform, report);
        }

        /// <summary>Abre a cena aditivamente, valida todas as raízes e fecha sem salvar.</summary>
        static List<string> ValidateSceneFile(string scenePath)
        {
            var errors = new List<string>();
            var alreadyOpen = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(scenePath);
            var scene = alreadyOpen.isLoaded
                ? alreadyOpen
                : UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var issue in SpaceAppsSceneBuilder.ValidateHierarchy(root))
                        errors.Add($"{scenePath}: {issue}");
                }
            }
            finally
            {
                if (!alreadyOpen.isLoaded)
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }

            return errors;
        }

        /// <summary>
        /// Pipeline que o player usará. No WebGL, se a QA-001 já criou um perfil em Settings/Web, o nível de qualidade
        /// padrão do WebGL precisa usá-lo; sem perfil, o build segue com a base do CORE-001 e registra o aviso.
        /// </summary>
        static string DescribePipelineFor(Platform platform, BuildReportData report)
        {
            var pipeline = PipelineForPlatform(platform == Platform.WebGL ? "WebGL" : "Standalone");
            var name = pipeline != null ? AssetDatabase.GetAssetPath(pipeline) : "nenhum (Built-in)";
            if (pipeline == null)
                report.errors.Add("Nenhum URP ativo para a plataforma; o mundo seria renderizado sem os shaders URP.");

            if (platform != Platform.WebGL)
                return name;

            var webProfiles = AssetDatabase.IsValidFolder(WebProfileFolder)
                ? AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { WebProfileFolder }).Select(AssetDatabase.GUIDToAssetPath).ToList()
                : new List<string>();

            if (webProfiles.Count == 0)
                report.warnings.Add("Perfil Web (QA-001) ainda não existe; usando a base do CORE-001: " + name);
            else if (!webProfiles.Contains(name))
                report.errors.Add($"O WebGL usa {name}, mas o perfil Web da QA-001 é {string.Join(", ", webProfiles)}. Ajuste o nível de qualidade padrão do WebGL.");

            return name;
        }

        /// <summary>Lê o nível de qualidade padrão da plataforma e devolve o pipeline efetivo dele.</summary>
        static RenderPipelineAsset PipelineForPlatform(string platformName)
        {
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            var index = quality.FindProperty("m_CurrentQuality")?.intValue ?? 0;
            var perPlatform = quality.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null)
            {
                for (var i = 0; i < perPlatform.arraySize; i++)
                {
                    var entry = perPlatform.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("first")?.stringValue == platformName)
                        index = entry.FindPropertyRelative("second").intValue;
                }
            }

            if (levels != null && index >= 0 && index < levels.arraySize)
            {
                var custom = levels.GetArrayElementAtIndex(index).FindPropertyRelative("customRenderPipeline")?.objectReferenceValue as RenderPipelineAsset;
                if (custom != null)
                    return custom;
            }

            return GraphicsSettings.defaultRenderPipeline;
        }

        /// <summary>Todo shader de que as cenas dependem precisa aparecer entre os assets empacotados do player.</summary>
        static void VerifyPackedShaders(BuildReport buildReport, BuildReportData report)
        {
            var packed = new HashSet<string>(buildReport.packedAssets.SelectMany(p => p.contents).Select(c => c.sourceAssetPath));
            var required = AssetDatabase.GetDependencies(report.scenes.ToArray(), true)
                .Where(p => p.EndsWith(".shader", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal);

            foreach (var shaderPath in required)
            {
                report.shadersChecked.Add(shaderPath);
                if (!packed.Contains(shaderPath))
                    report.shadersMissing.Add(shaderPath);
            }
        }

        static void CollectFiles(BuildReportData report)
        {
            if (!Directory.Exists(report.outputPath))
                return;

            using (var sha = SHA256.Create())
            {
                foreach (var file in Directory.GetFiles(report.outputPath, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                {
                    if (Path.GetFileName(file) == ReportFileName)
                        continue;

                    var info = new FileInfo(file);
                    using (var stream = File.OpenRead(file))
                    {
                        report.files.Add(new FileEntry
                        {
                            path = file.Substring(report.outputPath.Length).TrimStart('/', '\\').Replace('\\', '/'),
                            bytes = info.Length,
                            sha256 = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant(),
                        });
                    }

                    report.totalBytes += info.Length;
                }
            }
        }

        static BuildReportData Finish(BuildReportData report, DateTime started)
        {
            report.durationSeconds = (float)(DateTime.UtcNow - started).TotalSeconds;
            if (Directory.Exists(report.outputPath))
                File.WriteAllText(Path.Combine(report.outputPath, ReportFileName), JsonUtility.ToJson(report, true));

            var summary = $"[CORE-006] Build {report.platform}: {report.result} em {report.durationSeconds:0.0} s, " +
                $"{report.files.Count} arquivo(s), {report.totalBytes / 1048576f:0.00} MiB, shaders {report.shadersChecked.Count - report.shadersMissing.Count}/{report.shadersChecked.Count}, " +
                $"pipeline {report.renderPipeline}. Relatório: {report.outputPath}/{ReportFileName}";
            if (report.result == "Succeeded")
                Debug.Log(summary);
            else
                Debug.LogError(summary + ContractValidation.Describe(report.errors.Concat(report.shadersMissing.Select(s => "shader removido: " + s)).ToList()));

            foreach (var warning in report.warnings)
                Debug.LogWarning("[CORE-006] " + warning);
            return report;
        }

        static void LogPreflight(BuildReportData report)
        {
            foreach (var warning in report.warnings)
                Debug.LogWarning("[CORE-006] " + warning);
            if (report.errors.Count == 0)
                Debug.Log($"[CORE-006] Pré-verificação sem bloqueios ({report.scenes.Count} cena(s), pipeline {report.renderPipeline}).");
            else
                Debug.LogError($"[CORE-006] Pré-verificação bloqueada:{ContractValidation.Describe(report.errors)}");
        }

        // ---------- Plataforma ----------

        public static BuildTarget TargetOf(Platform platform) => platform == Platform.WebGL ? BuildTarget.WebGL : BuildTarget.StandaloneWindows64;

        public static BuildTargetGroup GroupOf(Platform platform) => platform == Platform.WebGL ? BuildTargetGroup.WebGL : BuildTargetGroup.Standalone;

        public static bool IsActive(Platform platform) => EditorUserBuildSettings.activeBuildTarget == TargetOf(platform);

        static Platform ActivePlatformOrWebGL() => IsActive(Platform.Windows) ? Platform.Windows : Platform.WebGL;

        public static string DefaultOutput(Platform platform) =>
            Path.Combine("Builds", platform.ToString(), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")).Replace('\\', '/');
    }
}
