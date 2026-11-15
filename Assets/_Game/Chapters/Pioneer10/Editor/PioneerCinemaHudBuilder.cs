using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Roteiro v2 · Builder do HUD dos planos de cinema (raiz <c>[R5] Pioneer Cinema HUD</c> na cena da Pioneer):
    /// <c>UI/CinemaHud.prefab</c> (UIDocument + <see cref="CinemaHudView"/>) e a textura de estática. Roda depois de
    /// <c>pioneer10.stage</c> e antes de <c>pioneer10.zchapter</c>, que liga os drivers (<see cref="PioneerClosestHud"/> e os demais).
    /// </summary>
    public sealed class PioneerCinemaHudBuilder : SceneBuilderModule
    {
        public const string HudBuilderId = "pioneer10.stage.hud";
        public const string HudRootName = "[R5] Pioneer Cinema HUD";
        public const string PanelPrefabPath = ChapterFolder + "/UI/CinemaHud.prefab";
        public const string PanelLayoutPath = ChapterFolder + "/UI/CinemaHud.uxml";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"; // UI-001 (consumido, não editado)

        public override string Id => HudBuilderId;
        public override string Owner => "R5 · PIO-024";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => HudRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || panelSettings == null)
                throw new System.InvalidOperationException("CinemaHud.uxml ou SpaceAppsPanelSettings ausente.");

            var noise = TextureAsset(context, "PIO_StaticNoise.png", () => PioneerStageArt.StaticNoise(128, 1973), 128, false,
                TextureWrapMode.Repeat, TextureWrapMode.Repeat, FilterMode.Bilinear, ChapterFolder + "/UI", true);
            var prefab = context.Prefab(PanelPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 12;
                var view = Get<CinemaHudView>(root);
                Set(view, "document", document);
                Set(view, "staticTexture", noise);
            });

            context.PrefabInstance(context.Root.transform, prefab, "CinemaHud");
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath) == null)
                errors.Add($"{HudBuilderId}: {PanelPrefabPath} ausente.");
            var view = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CinemaHudView>(true)).FirstOrDefault();
            if (view == null)
                errors.Add($"{HudBuilderId}: CinemaHudView ausente da cena.");
        }
    }
}
