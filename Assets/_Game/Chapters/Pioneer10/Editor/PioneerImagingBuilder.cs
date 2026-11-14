using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-006 · Builder da varredura IPP (CORE-006, raiz <c>[R5] Pioneer Imaging</c> na cena da Pioneer):
    /// <c>UI/ImagingPanel.prefab</c> (UIDocument + <see cref="ImagingPanelView"/>), <see cref="PioneerImaging"/> ligada ao
    /// palco e a sequência <c>Sequences/PIO_Imaging.asset</c> (74 s).
    /// </summary>
    public sealed class PioneerImagingBuilder : SceneBuilderModule
    {
        public const string ImagingBuilderId = "pioneer10.stage.imaging"; // depois de pioneer10.stage, de que depende
        public const string ImagingRootName = "[R5] Pioneer Imaging";
        public const string PanelPrefabPath = ChapterFolder + "/UI/ImagingPanel.prefab";
        public const string PanelLayoutPath = ChapterFolder + "/UI/ImagingPanel.uxml";
        public const string SequencePath = ChapterFolder + "/Sequences/PIO_Imaging.asset";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"; // UI-001 (consumido, não editado)

        public override string Id => ImagingBuilderId;
        public override string Owner => "R5 · PIO-006";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => ImagingRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).FirstOrDefault();
            if (stage == null)
                throw new System.InvalidOperationException("o palco da PIO-003 (pioneer10.stage) precisa existir antes da varredura IPP.");

            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || panelSettings == null)
                throw new System.InvalidOperationException("ImagingPanel.uxml ou SpaceAppsPanelSettings ausente.");

            var prefab = context.Prefab(PanelPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 10;
                var view = Get<ImagingPanelView>(root);
                Set(view, "document", document);
            });

            var root = context.Root.transform;
            var panel = context.PrefabInstance(root, prefab, "ImagingPanel");
            var sequence = BuildSequence(context);
            var imaging = context.Component<PioneerImaging>(context.Child(root, "PioneerImaging"));
            var so = new SerializedObject(imaging);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("presentation").objectReferenceValue = stage;
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<ImagingPanelView>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath);
            if (sequence == null)
            {
                errors.Add($"{ImagingBuilderId}: {SequencePath} ausente.");
                return;
            }

            errors.AddRange(sequence.Validate().Select(e => $"{ImagingBuilderId}: {e}"));
            if (Mathf.Abs(sequence.TotalDurationSeconds - 74f) > 0.01f)
                errors.Add($"{ImagingBuilderId}: PIO.Imaging dura {sequence.TotalDurationSeconds} s; o roteiro pede 74 s.");

            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var shot in sequence.Shots)
            {
                if (!stage.TryGetAnchor(shot.RigId, out _))
                    errors.Add($"{ImagingBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
            }

            if (!stage.TryGetUiFrame(PioneerImaging.StateId, out _))
                errors.Add($"{ImagingBuilderId}: o anchor {PioneerImaging.StateId} não reserva quadro de UI.");
        }

        static SequenceDefinition BuildSequence(SceneBuildContext context)
        {
            var fresh = CreateSequence();
            fresh.name = "PIO_Imaging";
            var asset = context.Asset(SequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Imaging";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>
        /// 0–16 aprender, 16–48 oito passagens (3 s cada), 48–74 revelação. Os marcos de 28, 34, 40 e 46 s seguram a
        /// sequência até a faixa 4, a interrupção (faixa 6), a recuperação e 8/8; no ritmo nominal (uma faixa por
        /// passagem) nenhuma espera acontece e as legendas PIO.04.03–05 caem exatamente nas lacunas do roteiro.
        /// </summary>
        public static SequenceDefinition CreateSequence()
        {
            var shots = new[]
            {
                new ShotDefinition("PIO04.Learn", 16f, PioneerImaging.StateId,
                    new[] { Key(0f, Vector3.zero, 46f), Key(16f, new Vector3(0f, 0f, 0.2f), 46f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerImaging.EventLearn) }),
                new ShotDefinition("PIO04.Sweep", 32f, PioneerImaging.StateId,
                    new[] { Key(0f, new Vector3(0f, 0f, 0.2f), 46f), Key(32f, new Vector3(0f, 0f, 0.4f), 46f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[]
                    {
                        new ShotEvent(0f, PioneerImaging.EventSweep),
                        new ShotEvent(12f, PioneerImaging.EventGateChannels),
                        new ShotEvent(18f, PioneerImaging.EventGateInterruption),
                        new ShotEvent(24f, PioneerImaging.EventGateRecovery),
                        new ShotEvent(30f, PioneerImaging.EventGateComplete),
                    }),
                new ShotDefinition("PIO04.Reveal", 26f, PioneerImaging.StateId,
                    new[] { Key(0f, new Vector3(0f, 0f, 0.4f), 46f), Key(26f, new Vector3(0f, 0f, 0.4f), 46f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerImaging.EventReveal) }),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.04.01", "PIO.04.01", 0f, 8f),
                new SubtitleCue("PIO.04.02", "PIO.04.02", 8f, 16f),
                new SubtitleCue("PIO.04.03", "PIO.04.03", 28f, 34f),
                new SubtitleCue("PIO.04.04", "PIO.04.04", 34f, 40f),
                new SubtitleCue("PIO.04.05", "PIO.04.05", 40f, 46f),
                new SubtitleCue("PIO.04.06", "PIO.04.06", 48f, 55f),
                new SubtitleCue("PIO.04.07", "PIO.04.07", 55f, 63f),
                new SubtitleCue("PIO.04.08", "PIO.04.08", 63f, 69f),
                new SubtitleCue("PIO.04.09", "PIO.04.09", 69f, 74f),
            };

            return SequenceDefinition.Create("PIO_Imaging", shots, cues);
        }
    }
}
