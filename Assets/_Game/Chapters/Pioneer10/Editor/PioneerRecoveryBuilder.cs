using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-008 · Builder da recuperação de radiação e da revelação histórica (CORE-006, raiz
    /// <c>[R5] Pioneer Imaging Recovery</c> na cena da Pioneer). Cria <c>UI/HistoricalImageReveal.prefab</c> (UIDocument +
    /// <see cref="HistoricalImageReveal"/> com as duas fotos da PIO-007) e liga <see cref="ImagingRecovery"/> ao painel e à
    /// varredura da PIO-006 sem editar nada delas. Menu: Abandoned Space Apps → CORE-006 → Reconstruir todas as cenas.
    /// </summary>
    public sealed class PioneerRecoveryBuilder : SceneBuilderModule
    {
        public const string RecoveryBuilderId = "pioneer10.stage.imaging.recovery"; // depois de pioneer10.stage.imaging
        public const string RecoveryRootName = "[R5] Pioneer Imaging Recovery";
        public const string RevealPrefabPath = ChapterFolder + "/UI/HistoricalImageReveal.prefab";
        public const string RevealLayoutPath = ChapterFolder + "/UI/HistoricalImageReveal.uxml";
        public const string RecoveryStylePath = ChapterFolder + "/UI/ImagingRecovery.uss";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"; // UI-001 (consumido, não editado)

        public override string Id => RecoveryBuilderId;
        public override string Owner => "R5 · PIO-008";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => RecoveryRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var imaging = FindImaging(context);
            if (imaging == null)
                throw new System.InvalidOperationException("a varredura da PIO-006 (pioneer10.stage.imaging) precisa existir antes da recuperação.");

            var panelView = imaging.GetComponentInChildren<ImagingPanelView>(true);
            if (panelView == null)
                panelView = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ImagingPanelView>(true)).FirstOrDefault();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RevealLayoutPath);
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(RecoveryStylePath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var jupiter = AssetDatabase.LoadAssetAtPath<Texture2D>(PioneerHistoricalImagesImporter.Files[1]);
            var ganymede = AssetDatabase.LoadAssetAtPath<Texture2D>(PioneerHistoricalImagesImporter.Files[0]);
            if (panelView == null || layout == null || style == null || panelSettings == null || jupiter == null || ganymede == null)
                throw new System.InvalidOperationException("faltam ImagingPanelView, UXML/USS da PIO-008, PanelSettings ou as fotos da PIO-007.");

            var prefab = context.Prefab(RevealPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 11; // acima do painel da varredura (10): cobre a composição
                Get<ResponsiveLayoutClass>(root);
                var view = Get<HistoricalImageReveal>(root);
                Set(view, "document", document);
                Set(view, "jupiterTexture", jupiter);
                Set(view, "ganymedeTexture", ganymede);
            });

            var parent = context.Root.transform;
            var instance = context.PrefabInstance(parent, prefab, "HistoricalImageReveal");
            var reveal = instance.GetComponent<HistoricalImageReveal>();
            Set(reveal, "imaging", imaging);

            var recovery = context.Component<ImagingRecovery>(context.Child(parent, "ImagingRecovery"));
            Set(recovery, "imaging", imaging);
            Set(recovery, "panel", panelView);
            Set(recovery, "style", style);
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var imaging = FindImaging(context);
            if (imaging == null)
            {
                errors.Add($"{RecoveryBuilderId}: PioneerImaging ausente da cena.");
                return;
            }

            var recovery = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ImagingRecovery>(true)).FirstOrDefault();
            if (recovery == null)
                errors.Add($"{RecoveryBuilderId}: ImagingRecovery ausente da cena.");
            else
                errors.AddRange(recovery.ValidateSetup().Select(e => $"{RecoveryBuilderId}: {e}"));

            var reveal = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HistoricalImageReveal>(true)).FirstOrDefault();
            if (reveal == null)
                errors.Add($"{RecoveryBuilderId}: HistoricalImageReveal ausente da cena.");
            else
                errors.AddRange(reveal.ValidateSetup().Select(e => $"{RecoveryBuilderId}: {e}"));
        }

        static PioneerImaging FindImaging(SceneBuildContext context) =>
            context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerImaging>(true)).FirstOrDefault();
    }
}
