using System.IO;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Aplica de forma idempotente a configuração fixada em ProjectSetupSpec (CORE-001).
    /// Reexecutar não cria novos GUIDs: assets existentes são reutilizados e apenas campos divergentes mudam.
    /// Linha de comando: Unity -batchmode -projectPath . -executeMethod
    /// Abandoned.SpaceApps.EditorTools.ProjectSetup.ApplyFromCommandLine -logFile -
    /// </summary>
    public static class ProjectSetup
    {
        const string MenuRoot = "Abandoned Space Apps/";

        [MenuItem(MenuRoot + "Aplicar configuração do projeto", priority = 0)]
        public static void ApplyFromMenu()
        {
            Apply();
            var issues = ProjectSetupValidator.Collect();
            ProjectSetupValidator.Report(issues);
        }

        public static void ApplyFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                Apply();
                var issues = ProjectSetupValidator.Collect();
                ProjectSetupValidator.Report(issues);
                exitCode = issues.Count == 0 ? 0 : 1;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        public static void Apply()
        {
            ApplyEditorSettings();
            ApplyPlayerSettings();
            EnsureFolders();
            var pipeline = EnsurePipelineAsset();
            AssignPipeline(pipeline);
            RelocateGlobalSettings();
            RelocateDefaultVolumeProfile();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void ApplyEditorSettings()
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
                EditorSettings.serializationMode = SerializationMode.ForceText;

            if (VersionControlSettings.mode != ProjectSetupSpec.VisibleMetaFilesMode)
                VersionControlSettings.mode = ProjectSetupSpec.VisibleMetaFilesMode;
        }

        static void ApplyPlayerSettings()
        {
            if (PlayerSettings.companyName != ProjectSetupSpec.CompanyName)
                PlayerSettings.companyName = ProjectSetupSpec.CompanyName;
            if (PlayerSettings.productName != ProjectSetupSpec.ProductName)
                PlayerSettings.productName = ProjectSetupSpec.ProductName;
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
                PlayerSettings.colorSpace = ColorSpace.Linear;

            // WebGL 2 somente: o perfil Web do MVP não depende de WebGPU nem de fallback WebGL 1.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // Um único leitor de teclado: o InputRouter (CORE-003) usa o Input System; o Input Manager antigo fica desligado.
            var playerSettings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var handler = playerSettings.FindProperty("activeInputHandler");
            if (handler == null)
                throw new System.InvalidOperationException("PlayerSettings.activeInputHandler não encontrado nesta versão da Unity.");

            if (handler.intValue != ProjectSetupSpec.InputSystemOnlyHandler)
            {
                handler.intValue = ProjectSetupSpec.InputSystemOnlyHandler;
                playerSettings.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[ProjectSetup] Input System ativado. Reinicie o Editor para o backend novo entrar em vigor.");
            }
        }

        static void EnsureFolders()
        {
            foreach (var folder in ProjectSetupSpec.RequiredFolders)
                EnsureFolder(folder);
        }

        static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }

        static UniversalRenderPipelineAsset EnsurePipelineAsset()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ProjectSetupSpec.RendererDataPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, ProjectSetupSpec.RendererDataPath);
            }

            var rendererObject = new SerializedObject(renderer);
            SetInt(rendererObject, "m_RenderingMode", ProjectSetupSpec.ForwardRenderingMode);
            rendererObject.ApplyModifiedPropertiesWithoutUndo();

            if (renderer.postProcessData == null)
            {
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(ProjectSetupSpec.DefaultPostProcessDataPath);
                EditorUtility.SetDirty(renderer);
            }

            ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectSetupSpec.PipelineAssetPath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, ProjectSetupSpec.PipelineAssetPath);
            }

            var pipelineObject = new SerializedObject(pipeline);
            foreach (var setting in ProjectSetupSpec.PipelineSettings)
            {
                var property = pipelineObject.FindProperty(setting.PropertyPath)
                    ?? throw new System.InvalidOperationException($"Campo URP '{setting.PropertyPath}' não existe em {ProjectSetupSpec.UrpPackageVersion}.");

                switch (setting.Kind)
                {
                    case ProjectSetupSpec.SettingKind.Bool:
                        property.boolValue = setting.BoolValue;
                        break;
                    case ProjectSetupSpec.SettingKind.Int:
                        property.intValue = setting.IntValue;
                        break;
                    default:
                        property.floatValue = setting.FloatValue;
                        break;
                }
            }

            pipelineObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            return pipeline;
        }

        static void AssignPipeline(UniversalRenderPipelineAsset pipeline)
        {
            if (GraphicsSettings.defaultRenderPipeline != pipeline)
                GraphicsSettings.defaultRenderPipeline = pipeline;

            // Todos os níveis herdam o pipeline padrão; QA-001 pode atribuir o perfil Leve explicitamente depois.
            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            for (var i = 0; i < levels.arraySize; i++)
            {
                var custom = levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");
                if (custom != null && custom.objectReferenceValue != null && custom.objectReferenceValue != pipeline)
                    custom.objectReferenceValue = null;
            }

            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RelocateGlobalSettings()
        {
            var global = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            if (global == null)
                return;

            var currentPath = AssetDatabase.GetAssetPath(global);
            if (string.IsNullOrEmpty(currentPath) || currentPath == ProjectSetupSpec.GlobalSettingsPath)
                return;

            Move(currentPath, ProjectSetupSpec.GlobalSettingsPath);
        }

        static void RelocateDefaultVolumeProfile()
        {
            var profile = GraphicsSettings.GetRenderPipelineSettings<URPDefaultVolumeProfileSettings>()?.volumeProfile;
            if (profile == null)
                return;

            var currentPath = AssetDatabase.GetAssetPath(profile);
            if (!string.IsNullOrEmpty(currentPath) && currentPath != ProjectSetupSpec.DefaultVolumeProfilePath)
                Move(currentPath, ProjectSetupSpec.DefaultVolumeProfilePath);
        }

        static void Move(string from, string to)
        {
            // MoveAsset preserva o GUID, logo as referências serializadas continuam válidas.
            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning($"[ProjectSetup] Não foi possível mover {from} para {to}: {error}");
        }

        static void SetInt(SerializedObject target, string path, int value)
        {
            var property = target.FindProperty(path)
                ?? throw new System.InvalidOperationException($"Campo '{path}' não existe em {target.targetObject.GetType().Name}.");
            property.intValue = value;
        }
    }
}
