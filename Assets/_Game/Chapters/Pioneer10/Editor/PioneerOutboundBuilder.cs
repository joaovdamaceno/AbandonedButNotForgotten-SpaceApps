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
    /// PIO-010 · Builder do caminho de saída (CORE-006, raiz <c>[R5] Pioneer Outbound</c> na cena da Pioneer):
    /// <c>UI/OutboundAtlas.prefab</c> (UIDocument + <see cref="OutboundAtlasView"/>), <see cref="OutboundAtlas"/> ligada ao palco e
    /// a sequência <c>Sequences/PIO_Outbound.asset</c> (39 s).
    /// </summary>
    public sealed class PioneerOutboundBuilder : SceneBuilderModule
    {
        public const string OutboundBuilderId = "pioneer10.stage.outbound"; // depois de pioneer10.stage, de que depende
        public const string OutboundRootName = "[R5] Pioneer Outbound";
        public const string PanelPrefabPath = ChapterFolder + "/UI/OutboundAtlas.prefab";
        public const string PanelLayoutPath = ChapterFolder + "/UI/OutboundAtlas.uxml";
        public const string SequencePath = ChapterFolder + "/Sequences/PIO_Outbound.asset";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"; // UI-001 (consumido, não editado)

        public override string Id => OutboundBuilderId;
        public override string Owner => "R5 · PIO-010";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => OutboundRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).FirstOrDefault();
            if (stage == null)
                throw new System.InvalidOperationException("o palco da PIO-003 (pioneer10.stage) precisa existir antes do caminho de saída.");

            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || panelSettings == null)
                throw new System.InvalidOperationException("OutboundAtlas.uxml ou SpaceAppsPanelSettings ausente.");

            var prefab = context.Prefab(PanelPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 10;
                var view = Get<OutboundAtlasView>(root);
                Set(view, "document", document);
                Get<ResponsiveLayoutClass>(root); // UI_SPEC: todo UIDocument de tela cheia aplica sa-layout--narrow em 4:3
            });

            var parent = context.Root.transform;
            var panel = context.PrefabInstance(parent, prefab, "OutboundAtlas");
            var sequence = BuildSequence(context);
            var atlas = context.Component<OutboundAtlas>(context.Child(parent, "OutboundAtlas"));
            var so = new SerializedObject(atlas);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("presentation").objectReferenceValue = stage;
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<OutboundAtlasView>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath);
            if (sequence == null)
            {
                errors.Add($"{OutboundBuilderId}: {SequencePath} ausente.");
                return;
            }

            errors.AddRange(sequence.Validate().Select(e => $"{OutboundBuilderId}: {e}"));
            if (Mathf.Abs(sequence.TotalDurationSeconds - 39f) > 0.01f)
                errors.Add($"{OutboundBuilderId}: PIO.Outbound dura {sequence.TotalDurationSeconds} s; o roteiro pede 39 s.");

            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var shot in sequence.Shots)
            {
                if (!stage.TryGetAnchor(shot.RigId, out _))
                    errors.Add($"{OutboundBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
            }

            var atlas = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OutboundAtlas>(true)).FirstOrDefault();
            if (atlas == null)
                errors.Add($"{OutboundBuilderId}: OutboundAtlas ausente da cena.");
            else
                errors.AddRange(atlas.ValidateSetup().Select(e => $"{OutboundBuilderId}: {e}"));
        }

        static SequenceDefinition BuildSequence(SceneBuildContext context)
        {
            var fresh = CreateSequence();
            fresh.name = "PIO_Outbound";
            var asset = context.Asset(SequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Outbound";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>
        /// 0–12 cartão 1976, 12–24 cartão 1983, 24–39 energia e gráfico de 1998. O marco de 24 s (início do terceiro plano)
        /// segura a sequência até o hold de 3 s; o gráfico (25 s) só entra depois dele, com as legendas PIO.06.04 e PIO.06.05.
        /// A câmera 3D fica atrás do diagrama 2D: varia o suficiente para não repetir plano, sem simular um sobrevoo.
        /// </summary>
        public static SequenceDefinition CreateSequence()
        {
            const string rig = OutboundAtlas.StateId;
            var shots = new[]
            {
                new ShotDefinition("PIO06.Saturn", 12f, rig,
                    new[] { Key(0f, Vector3.zero, 40f), Key(12f, new Vector3(0f, 0f, -0.6f), 38f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, OutboundAtlas.EventCard1976) }),
                new ShotDefinition("PIO06.Neptune", 12f, rig,
                    new[] { Key(0f, new Vector3(0.8f, 0.3f, -0.6f), 44f), Key(12f, new Vector3(1.4f, 0.5f, -1.2f), 46f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, OutboundAtlas.EventCard1983) }),
                new ShotDefinition("PIO06.Power", 15f, rig,
                    new[] { Key(0f, new Vector3(-1.2f, -0.4f, 0.6f), 50f), Key(15f, new Vector3(-1.6f, -0.6f, 1.2f), 52f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, OutboundAtlas.EventPower), new ShotEvent(1f, OutboundAtlas.EventChart) }),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.06.01", "PIO.06.01", 0f, 8f),
                new SubtitleCue("PIO.06.02", "PIO.06.02", 8f, 16f),
                new SubtitleCue("PIO.06.03", "PIO.06.03", 17f, 24f),
                new SubtitleCue("PIO.06.04", "PIO.06.04", 25f, 33f),
                new SubtitleCue("PIO.06.05", "PIO.06.05", 33f, 39f),
            };

            return SequenceDefinition.Create("PIO_Outbound", shots, cues);
        }
    }
}
