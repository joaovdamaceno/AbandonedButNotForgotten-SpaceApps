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
    /// PIO-012 · Builder da despedida (CORE-006, raiz <c>[R5] Pioneer Farewell</c> na cena da Pioneer):
    /// <c>UI/FarewellPanel.prefab</c> (UIDocument + <see cref="FarewellPanelView"/> + <see cref="ResponsiveLayoutClass"/>),
    /// <see cref="PioneerFarewellActivity"/> ligada ao palco e a sequência <c>Sequences/PIO_Farewell.asset</c> (38 s).
    /// </summary>
    public sealed class PioneerFarewellBuilder : SceneBuilderModule
    {
        public const string FarewellBuilderId = "pioneer10.stage.farewell"; // depois de pioneer10.stage, de que depende
        public const string FarewellRootName = "[R5] Pioneer Farewell";
        public const string PanelPrefabPath = ChapterFolder + "/UI/FarewellPanel.prefab";
        public const string PanelLayoutPath = ChapterFolder + "/UI/FarewellPanel.uxml";
        public const string SequencePath = ChapterFolder + "/Sequences/PIO_Farewell.asset";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"; // UI-001 (consumido, não editado)

        public override string Id => FarewellBuilderId;
        public override string Owner => "R5 · PIO-012";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => FarewellRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).FirstOrDefault();
            if (stage == null)
                throw new System.InvalidOperationException("o palco da PIO-003 (pioneer10.stage) precisa existir antes da despedida.");

            // Reimporta estilo e layout (o USS usa fontes por url(); evita um import antigo guardado antes dos assets).
            AssetDatabase.ImportAsset(ChapterFolder + "/UI/FarewellPanel.uss", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PanelLayoutPath, ImportAssetOptions.ForceUpdate);
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || panelSettings == null)
                throw new System.InvalidOperationException("FarewellPanel.uxml ou SpaceAppsPanelSettings ausente.");

            var prefab = context.Prefab(PanelPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 10;
                var view = Get<FarewellPanelView>(root);
                Set(view, "document", document);
                Get<ResponsiveLayoutClass>(root); // UI_SPEC: todo UIDocument de tela cheia aplica sa-layout--narrow em 4:3
            });

            var root = context.Root.transform;
            var panel = context.PrefabInstance(root, prefab, "FarewellPanel");
            var sequence = BuildSequence(context);
            var activity = context.Component<PioneerFarewellActivity>(context.Child(root, "PioneerFarewellActivity"));
            var so = new SerializedObject(activity);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("presentation").objectReferenceValue = stage;
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<FarewellPanelView>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath);
            if (sequence == null)
            {
                errors.Add($"{FarewellBuilderId}: {SequencePath} ausente.");
                return;
            }

            errors.AddRange(sequence.Validate().Select(e => $"{FarewellBuilderId}: {e}"));
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var shot in sequence.Shots)
            {
                if (!stage.TryGetAnchor(shot.RigId, out _))
                    errors.Add($"{FarewellBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
            }

            var activity = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerFarewellActivity>(true)).FirstOrDefault();
            if (activity == null)
                errors.Add($"{FarewellBuilderId}: PioneerFarewellActivity ausente da cena.");
            else
                errors.AddRange(activity.ValidateSetup().Select(e => $"{FarewellBuilderId}: {e}"));
        }

        static SequenceDefinition BuildSequence(SceneBuildContext context)
        {
            var fresh = CreateSequence();
            fresh.name = "PIO_Farewell";
            var asset = context.Asset(SequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Farewell";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>
        /// 0–4 antena terrestre e 4–8 contorno do refletor (o match cut), 8–14 placa em close, 14–20 plano aberto com a direção
        /// de Aldebaran (a luz da placa já apagou, pois o rig muda), 20–30 a Pioneer diminuindo, 30–38 preto do memorial
        /// (marco em 36 s: as duas ações finais e a conclusão). Cada plano usa um rig diferente do anterior (nunca o mesmo
        /// zoom); só o preto continua o plano anterior, sem salto.
        /// </summary>
        public static SequenceDefinition CreateSequence()
        {
            var shots = new[]
            {
                new ShotDefinition("PIO08.Antenna", 4f, "PIO.Listening",
                    new[] { Key(0f, new Vector3(0f, 0f, -1.5f), 55f), Key(4f, Vector3.zero, 55f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerFarewellActivity.EventMatch) }),
                new ShotDefinition("PIO08.Contour", 4f, "PIO.Launch.Reveal",
                    new[] { Key(0f, Vector3.zero, 34f), Key(4f, new Vector3(-0.3f, 0f, 0.4f), 34f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO08.Plaque", 6f, "PIO.Plaque",
                    new[] { Key(0f, Vector3.zero, 36f), Key(6f, new Vector3(0f, 0f, 0.15f), 36f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO08.Open", 6f, PioneerFarewellActivity.StateId,
                    new[] { Key(0f, Vector3.zero, 45f), Key(6f, new Vector3(0.4f, 0f, 0.8f), 45f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0.05f, PioneerFarewellActivity.EventGuide) }),
                new ShotDefinition("PIO08.Receding", 10f, "PIO.Outbound",
                    new[] { Key(0f, Vector3.zero, 40f), Key(10f, new Vector3(-2.5f, -0.6f, 13f), 38f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0.05f, PioneerFarewellActivity.EventGuideEnd) }),
                new ShotDefinition("PIO08.Memorial", 8f, "PIO.Outbound",
                    new[] { Key(0f, Vector3.zero, 38f), Key(8f, new Vector3(-0.4f, -0.1f, 2f), 38f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f, continueFromPreviousShot: true,
                    events: new[] { new ShotEvent(0.05f, PioneerFarewellActivity.EventMemorial), new ShotEvent(6f, PioneerFarewellActivity.EventChoices) }),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.08.01", "PIO.08.01", 0f, 8f),
                new SubtitleCue("PIO.08.02", "PIO.08.02", 9f, 17f),
                new SubtitleCue("PIO.08.03", "PIO.08.03", 18f, 26f),
                new SubtitleCue("PIO.08.04", "PIO.08.04", 27f, 35f),
            };

            return SequenceDefinition.Create("PIO_Farewell", shots, cues);
        }
    }
}
