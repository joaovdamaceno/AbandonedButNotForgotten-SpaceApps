using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Abandoned.SpaceApps.UI;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-006 · Builder da varredura IPP (CORE-006, raiz <c>[R5] Pioneer Imaging</c> na cena da Pioneer):
    /// <c>UI/ImagingPanel.prefab</c> (UIDocument + <see cref="ImagingPanelView"/>), <see cref="PioneerImaging"/> ligada ao
    /// palco e a sequência <c>Sequences/PIO_Imaging.asset</c> (64 s).
    /// </summary>
    public sealed class PioneerImagingBuilder : SceneBuilderModule
    {
        public const string ImagingBuilderId = "pioneer10.stage.imaging"; // depois de pioneer10.stage, de que depende
        public const string ImagingRootName = "[R5] Pioneer Imaging";
        public const string PanelPrefabPath = ChapterFolder + "/UI/ImagingPanel.prefab";
        public const string PanelLayoutPath = ChapterFolder + "/UI/ImagingPanel.uxml";
        public const string SequencePath = ChapterFolder + "/Sequences/PIO_Imaging.asset";
        public const string SpinPanelPrefabPath = ChapterFolder + "/UI/SpinShotPanel.prefab";
        public const string SpinPanelLayoutPath = ChapterFolder + "/UI/SpinShotPanel.uxml";
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

            var spinLayout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SpinPanelLayoutPath);
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || spinLayout == null || panelSettings == null)
                throw new System.InvalidOperationException("ImagingPanel.uxml, SpinShotPanel.uxml ou SpaceAppsPanelSettings ausente.");

            var prefab = context.Prefab(PanelPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 10;
                var view = Get<ImagingPanelView>(root);
                Set(view, "document", document);
                Get<ResponsiveLayoutClass>(root); // UI_SPEC: todo UIDocument de tela cheia aplica sa-layout--narrow em 4:3
            });

            var spinPrefab = context.Prefab(SpinPanelPrefabPath, spinRoot =>
            {
                var document = Get<UIDocument>(spinRoot);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = spinLayout;
                document.sortingOrder = 10;
                var view = Get<SpinShotPanelView>(spinRoot);
                Set(view, "document", document);
                Get<ResponsiveLayoutClass>(spinRoot);
            });

            var root = context.Root.transform;
            var panel = context.PrefabInstance(root, prefab, "ImagingPanel");
            var spinPanel = context.PrefabInstance(root, spinPrefab, "SpinShotPanel");
            var sequence = BuildSequence(context);
            var imaging = context.Component<PioneerImaging>(context.Child(root, "PioneerImaging"));
            var so = new SerializedObject(imaging);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("presentation").objectReferenceValue = stage;
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<ImagingPanelView>();
            so.FindProperty("spinPanel").objectReferenceValue = spinPanel.GetComponent<SpinShotPanelView>();
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
            if (Mathf.Abs(sequence.TotalDurationSeconds - 64f) > 0.01f)
                errors.Add($"{ImagingBuilderId}: PIO.Imaging dura {sequence.TotalDurationSeconds} s; o roteiro v2 retemporizado pede 64 s.");

            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var shot in sequence.Shots)
            {
                if (!stage.TryGetAnchor(shot.RigId, out _))
                    errors.Add($"{ImagingBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
            }

            if (!stage.TryGetUiFrame(PioneerImaging.StateId, out _))
                errors.Add($"{ImagingBuilderId}: o anchor {PioneerImaging.StateId} não reserva quadro de UI.");

            var imaging = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerImaging>(true)).FirstOrDefault();
            if (imaging == null)
                errors.Add($"{ImagingBuilderId}: PioneerImaging ausente da cena.");
            else
                errors.AddRange(imaging.ValidateSetup().Select(e => $"{ImagingBuilderId}: {e}"));
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
        /// Deriva suave da câmera de ponto de vista (teste de jogo: a sonda parecia parada durante as fotos e a revelação). A cada ~6 s a
        /// câmera inclina e rola poucos graus para o outro lado e avança alguns metros; o giro do jogo (anchor) soma por cima.
        /// </summary>
        static ShotKeyframe[] Drift(float durationSeconds, float travelMeters, float swayDegrees)
        {
            var count = Mathf.Max(2, Mathf.CeilToInt(durationSeconds / 6f));
            var keys = new ShotKeyframe[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var sign = i % 2 == 0 ? 1f : -1f;
                var position = new Vector3(sign * travelMeters * 0.35f, -sign * travelMeters * 0.2f, travelMeters * i / count);
                var euler = new Vector3(sign * swayDegrees * 0.5f, sign * swayDegrees * 0.6f, -sign * swayDegrees);
                keys[i] = new ShotKeyframe(durationSeconds * i / count, position, euler, ImagingPovFov * (1f + 0.012f * sign));
            }

            return keys;
        }

        /// <summary>
        /// 0–10 aprender, 10–38 as passagens (os marcos de 21, 27, 31,5 e 37 s), 38–64 revelação. Os marcos seguram a
        /// sequência até a faixa 4, a interrupção (faixa 6), a recuperação e 8/8; no ritmo nominal (uma faixa por
        /// passagem) nenhuma espera acontece e as legendas PIO.04.03–05 caem exatamente nas lacunas do roteiro.
        /// </summary>
        public static SequenceDefinition CreateSequence()
        {
            var shots = new[]
            {
                new ShotDefinition("PIO04.Learn", 10f, PioneerImaging.PovAnchorId,
                    Drift(10f, 3f, 1.6f),
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerImaging.EventLearn) }),
                new ShotDefinition("PIO04.Sweep", 28f, PioneerImaging.PovAnchorId,
                    Drift(28f, 8f, 1.6f),
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[]
                    {
                        new ShotEvent(0f, PioneerImaging.EventSweep),
                        new ShotEvent(11f, PioneerImaging.EventGateChannels),
                        new ShotEvent(17f, PioneerImaging.EventGateInterruption),
                        new ShotEvent(21.5f, PioneerImaging.EventGateRecovery),
                        new ShotEvent(27f, PioneerImaging.EventGateComplete),
                    }),
                new ShotDefinition("PIO04.Reveal", 26f, PioneerImaging.PovAnchorId,
                    Drift(26f, 8f, 2.4f),
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerImaging.EventReveal) }),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.04.01", "PIO.04.01", 0f, 3.2f),
                new SubtitleCue("PIO.04.02", "PIO.04.02", 3.4f, 9.8f),
                new SubtitleCue("PIO.04.03", "PIO.04.03", 21f, 27f),
                new SubtitleCue("PIO.04.04", "PIO.04.04", 27f, 31.5f),
                new SubtitleCue("PIO.04.05", "PIO.04.05", 31.5f, 37f),
                new SubtitleCue("PIO.04.06", "PIO.04.06", 38f, 45f),
                new SubtitleCue("PIO.04.07", "PIO.04.07", 45f, 53f),
                new SubtitleCue("PIO.04.08", "PIO.04.08", 53f, 59f),
                new SubtitleCue("PIO.04.09", "PIO.04.09", 59f, 64f),
            };

            return SequenceDefinition.Create("PIO_Imaging", shots, cues);
        }
    }
}
