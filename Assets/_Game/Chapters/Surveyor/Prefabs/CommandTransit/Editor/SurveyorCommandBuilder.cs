using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorCommandBuilder : SceneBuilderModule
    {
        public const string Folder = "Assets/_Game/Chapters/Surveyor/Prefabs/CommandTransit";
        public const string UiFolder = "Assets/_Game/Chapters/Surveyor/UI";
        public const string PreviewScene = Folder + "/Preview/CommandDelayPreview.unity";
        public const string PrefabPath = Folder + "/CommandDelayPreview.prefab";
        public override string Id => "surveyor.commandTransit";
        public override string Owner => "R6 · SUR-004";
        public override string ScenePath => PreviewScene;
        public override string RootName => "[R6 SUR-004] Command transit";
        public override string AssetFolder => Folder;

        [MenuItem("Abandoned Space Apps/Surveyor/SUR-004 Reconstruir verificação de comandos")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new SurveyorCommandBuilder());
            if (!result.Succeeded) throw new InvalidOperationException(result.ToString());
            Debug.Log(result.ToString());
        }
        public static void BuildFromCommandLine()
        {
            try { BuildFromMenu(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public override void Build(SceneBuildContext context)
        {
            var chapter = context.Asset(Folder + "/Preview/CommandDelayPreview.asset", () => ChapterDefinition.Create(
                "surveyor.command.preview", "cmd.archive", "cmd.description", "cmd.title", "CommandDelayPreview", 30, "surveyor.command.preview",
                new[] { new ChapterStep(SurveyorCommandPreviewHost.PreviewStep,false,"End"),new ChapterStep("End",true) }));
            var panel = context.Asset(Folder + "/CommandPanelSettings.asset", () => Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));
            var prefab = context.Prefab(PrefabPath, root =>
            {
                var owner = Get<ChapterDirector>(root); var clock = Get<SessionClock>(root);
                var router = Get<InputRouter>(root); var service = Get<InteractionService>(root);
                var queue = Get<SurveyorCommandDelay>(root); var action = Get<SurveyorCommandInteraction>(root);
                var view = Get<CommandTransitView>(root); var host = Get<SurveyorCommandPreviewHost>(root);
                var document = Get<UIDocument>(root); document.panelSettings = panel;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiFolder + "/CommandTransitPreview.uxml");
                Get<ResponsiveLayoutClass>(root);
                var camera = Get<Camera>(Child(root,"PreviewCamera"));
                camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.03f,.047f,.075f);
                Get<AudioListener>(camera.gameObject);
                host.Configure(owner,clock,router,service,queue,action,view,document,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder + "/CommandTransitTexts.json"),chapter,Child(root,"ReceivedMirrorPivot").transform);
            });
            context.PrefabInstance(context.Root.transform,prefab,"CommandDelayPreview");
        }
        public override void Validate(SceneBuildContext context,List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(Folder + "/Preview/CommandDelayPreview.asset").Validate());
            errors.AddRange(SurveyorLaunchTexts.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder + "/CommandTransitTexts.json")).Validate());
            if (context.Root.GetComponentsInChildren<SurveyorCommandDelay>(true).Length != 1) errors.Add("Fila duplicada ou ausente.");
            if (context.Root.GetComponentsInChildren<AudioListener>(true).Length != 1) errors.Add("Listener duplicado ou ausente.");
        }
        static GameObject Child(GameObject root,string name)
        {
            var child = root.transform.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name); go.transform.SetParent(root.transform,false); return go;
        }
        static T Get<T>(GameObject go) where T : Component
        { var value = go.GetComponent<T>(); return value != null ? value : go.AddComponent<T>(); }
    }
}
