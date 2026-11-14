using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-005 · Builder do cinturão (CORE-006, raiz <c>[R5] Pioneer Belt</c> na cena da Pioneer): <c>UI/BeltPanel.prefab</c>
    /// (UIDocument + <see cref="BeltPanelView"/>), brilho do sensor, <see cref="BeltInvestigation"/> ligada ao palco e a
    /// sequência <c>Sequences/PIO_Belt.asset</c> (58 s).
    /// </summary>
    public sealed class PioneerBeltBuilder : SceneBuilderModule
    {
        public const string BeltBuilderId = "pioneer10.stage.belt"; // depois de pioneer10.stage, de que depende
        public const string BeltRootName = "[R5] Pioneer Belt";
        public const string PanelPrefabPath = ChapterFolder + "/UI/BeltPanel.prefab";
        public const string PanelLayoutPath = ChapterFolder + "/UI/BeltPanel.uxml";
        public const string SequencePath = ChapterFolder + "/Sequences/PIO_Belt.asset";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"; // UI-001 (consumido, não editado)
        const string BeltFolder = ArtFolder + "/Belt";

        public override string Id => BeltBuilderId;
        public override string Owner => "R5 · PIO-005";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => BeltRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).FirstOrDefault();
            if (stage == null)
                throw new System.InvalidOperationException("o palco da PIO-003 (pioneer10.stage) precisa existir antes do cinturão.");

            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || panelSettings == null)
                throw new System.InvalidOperationException("BeltPanel.uxml ou SpaceAppsPanelSettings ausente.");

            var prefab = context.Prefab(PanelPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 10;
                var view = Get<BeltPanelView>(root);
                Set(view, "document", document);
            });

            var root = context.Root.transform;
            var panel = context.PrefabInstance(root, prefab, "BeltPanel");

            var glow = context.Child(root, "SensorGlow");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(StageFolder + "/Meshes/PIO_LowSphere.asset");
            context.Component<MeshFilter>(glow).sharedMesh = mesh;
            var renderer = context.Component<MeshRenderer>(glow);
            renderer.sharedMaterial = Unlit(context, "PIO_SensorGlow", new Color(1f, 0.93f, 0.62f), null, BeltFolder);
            Quiet(renderer);
            glow.transform.localScale = Vector3.one * 0.07f;
            glow.SetActive(false);

            var sequence = BuildSequence(context);
            var investigation = context.Component<BeltInvestigation>(context.Child(root, "BeltInvestigation"));
            var so = new SerializedObject(investigation);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("presentation").objectReferenceValue = stage;
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<BeltPanelView>();
            so.FindProperty("sensorGlow").objectReferenceValue = glow.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath);
            if (sequence == null)
            {
                errors.Add($"{BeltBuilderId}: {SequencePath} ausente.");
                return;
            }

            errors.AddRange(sequence.Validate().Select(e => $"{BeltBuilderId}: {e}"));
            if (Mathf.Abs(sequence.TotalDurationSeconds - 58f) > 0.01f)
                errors.Add($"{BeltBuilderId}: PIO.Belt dura {sequence.TotalDurationSeconds} s; o roteiro pede 58 s.");

            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var shot in sequence.Shots)
            {
                if (!stage.TryGetAnchor(shot.RigId, out _))
                    errors.Add($"{BeltBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
            }
        }

        static SequenceDefinition BuildSequence(SceneBuildContext context)
        {
            var fresh = CreateSequence();
            fresh.name = "PIO_Belt";
            var asset = context.Asset(SequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Belt";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>
        /// 0–12 mapa, 12–27 fantasia (15 s), 27–40 espaço real, 40–58 registros. A trava em 50,5 s segura PIO.02.06
        /// ("hold 5/5") até os cinco registros; o restante corre no relógio, sem concluir atividade pelo tempo.
        /// </summary>
        public static SequenceDefinition CreateSequence()
        {
            var shots = new[]
            {
                new ShotDefinition("PIO02.Map", 12f, "PIO.Belt",
                    new[] { Key(0f, new Vector3(0f, 0f, -6f), 30f), Key(12f, Vector3.zero, 30f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, BeltInvestigation.EventMap), new ShotEvent(1f, "PIO.Belt.DateCard") }),
                new ShotDefinition("PIO02.Fantasy", 15f, "PIO.Belt",
                    new[] { Key(0f, Vector3.zero, 30f), Key(15f, Vector3.zero, 30f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, BeltInvestigation.EventFantasy) }),
                new ShotDefinition("PIO02.Reality", 13f, "PIO.Belt",
                    new[] { Key(0f, new Vector3(0f, 0f, -14f), 26f), Key(13f, new Vector3(0f, 0f, -20f), 26f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, BeltInvestigation.EventReality) }),
                new ShotDefinition("PIO02.Record", 18f, "PIO.Belt.Sensor",
                    new[] { Key(0f, Vector3.zero, 40f), Key(18f, new Vector3(0f, 0f, 0.3f), 40f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, BeltInvestigation.EventRecord), new ShotEvent(10.5f, BeltInvestigation.EventGate), new ShotEvent(11f, "PIO.Belt.Calendar") }),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.02.01", "PIO.02.01", 0f, 6f),
                new SubtitleCue("PIO.02.02", "PIO.02.02", 6f, 12f),
                new SubtitleCue("PIO.02.03", "PIO.02.03", 13f, 20f),
                new SubtitleCue("PIO.02.04", "PIO.02.04", 27f, 34f),
                new SubtitleCue("PIO.02.05", "PIO.02.05", 34f, 40f),
                new SubtitleCue("PIO.02.06", "PIO.02.06", 51f, 58f),
            };

            return SequenceDefinition.Create("PIO_Belt", shots, cues);
        }
    }
}
