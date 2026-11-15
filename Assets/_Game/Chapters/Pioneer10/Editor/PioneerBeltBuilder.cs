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
        /// <summary>Roteiro v2, cena 2: a sequência de cinema do capítulo (50 s). PIO_Belt segue sendo a da atividade interativa.</summary>
        public const string CinemaSequencePath = ChapterFolder + "/Sequences/PIO_BeltCinema.asset";
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
            var cinema = BuildCinemaSequence(context);
            var counter = context.Component<PioneerBeltCounter>(context.Child(root, "BeltCounter"));
            var counterSo = new SerializedObject(counter);
            counterSo.FindProperty("sequence").objectReferenceValue = cinema;
            counterSo.FindProperty("nikeViewport").vector2Value = NikeViewport;
            counterSo.ApplyModifiedPropertiesWithoutUndo();
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

            var cinema = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(CinemaSequencePath);
            if (cinema == null || Mathf.Abs(cinema.TotalDurationSeconds - 50f) > 0.01f)
                errors.Add($"{BeltBuilderId}: o cinema do cinturão deve durar 50 s ({CinemaSequencePath}).");
            if (cinema != null)
            {
                errors.AddRange(cinema.Validate().Select(e => $"{BeltBuilderId}: {e}"));
                foreach (var shot in cinema.Shots)
                {
                    if (!stage.TryGetAnchor(shot.RigId, out _))
                        errors.Add($"{BeltBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
                }
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

        static SequenceDefinition BuildCinemaSequence(SceneBuildContext context)
        {
            var fresh = CreateCinemaSequence();
            fresh.name = "PIO_BeltCinema";
            var asset = context.Asset(CinemaSequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_BeltCinema";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }

        /// <summary>
        /// Roteiro v2, cena 2 (só cinema, 50 s, o tempo do roteiro): 0–8 a sonda cruza a órbita de Marte (data 15 JUL 1972); 8–19 visão larga
        /// do cinturão: pedras pequenas e muito afastadas; 8–19 o mapa do Sistema Solar com o cinturão e o caminho da sonda; 19–30 o ponto de vista da sonda dentro do cinturão enquanto o contador de
        /// dias acelera até 2 DEZ 1972; 30–40 corte para o espaço vazio e a pedra
        /// solitária (307 Nike); 40–50 a sonda sai para o escuro limpo (o contador chega a 15 FEV 1973). O contador e o rótulo vêm de
        /// <see cref="PioneerBeltCounter"/>, pelo relógio desta sequência. O evento PIO.Belt.Reality troca a música tensa pela calma (MUS_PIO_MARAVILHA) na revelação.
        /// </summary>
        /// <summary>Metros que a câmera avança no plano do contador de dias (as pedras mais próximas ficam a mais de 60 m).</summary>
        public const float DaysTravelMeters = 55f;

        /// <summary>
        /// Posições do ponto de vista: o avanço acumulado é proporcional aos dias contados (mesma exponencial do contador), de modo que a
        /// velocidade da sonda cresce junto com o ritmo dos dias; o campo de visão abre no fim para reforçar a velocidade.
        /// </summary>
        public static ShotKeyframe[] DaysPovKeys()
        {
            var keys = new ShotKeyframe[12];
            for (var i = 0; i < keys.Length; i++)
            {
                var u = i / 11f;
                var share = (Mathf.Exp(PioneerBeltCounter.Acceleration * u) - 1f) / (Mathf.Exp(PioneerBeltCounter.Acceleration) - 1f);
                var sway = Mathf.Sin(u * Mathf.PI * 3f);
                keys[i] = new ShotKeyframe(11f * u, new Vector3(1.2f * sway, -0.6f * sway, DaysTravelMeters * share),
                    new Vector3(-0.7f * sway, 1.0f * sway, -1.2f * sway), PioneerStageBuilder.BeltPovFov + 8f * u * u);
            }

            return keys;
        }

        public static SequenceDefinition CreateCinemaSequence()
        {
            var shots = new[]
            {
                new ShotDefinition("PIO02.Mars", 8f, "PIO.Belt.Mars",
                    new[] { Key(0f, new Vector3(0f, 0f, -10f), 30f), Key(8f, new Vector3(0f, 0f, 10f), 30f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
                // 8–19 s: a visão larga do cinturão, de longe: pedras pequenas e muito afastadas; a câmera se aproxima devagar.
                // 8–19 s: o Sistema Solar em 3D (PioneerSystemTour): a sonda sai de Marte e entra no cinturão; a câmera se aproxima devagar.
                new ShotDefinition("PIO02.Fear", 11f, PioneerStageBuilder.SystemBeltAnchorId,
                    new[] { Key(0f, new Vector3(0f, 0f, -30f), 40f), Key(11f, new Vector3(0f, 0f, 25f), 38f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f),
                // 19–30 s: zoom na sonda, a 70 m no fim: o espaço vazio em volta mostra como é difícil acertar uma pedra.
                // 19–30 s: ponto de vista da sonda (modelo escondido) dentro do cinturão. A velocidade segue a MESMA curva do contador de dias
                // (PioneerBeltCounter.DayAt: exponencial), então as pedras passam cada vez mais depressa na mesma proporção em que os dias aceleram.
                new ShotDefinition("PIO02.Days", 11f, PioneerStageBuilder.BeltPovAnchorId, DaysPovKeys(),
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO02.Nike", 10f, "PIO.Belt.Nike",
                    new[] { Key(0f, Vector3.zero, 30f), Key(10f, new Vector3(0f, 0f, 3f), 30f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, BeltInvestigation.EventReality) }),
                new ShotDefinition("PIO02.Exit", 10f, "PIO.Belt",
                    new[] { Key(0f, Vector3.zero, 30f), Key(10f, new Vector3(0f, 0f, -30f), 30f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.02.01", "PIO.02.01", 0f, 6f),
                new SubtitleCue("PIO.02.02", "PIO.02.02", 6.5f, 12.5f),
                new SubtitleCue("PIO.02.03", "PIO.02.03", 13f, 19f),
                new SubtitleCue("PIO.02.07", "PIO.02.07", 20.5f, 29.5f),
                new SubtitleCue("PIO.02.04", "PIO.02.04", 30.5f, 39.4f),
                new SubtitleCue("PIO.02.05", "PIO.02.05", 39.6f, 42.3f),
                new SubtitleCue("PIO.02.06", "PIO.02.06", 42.5f, 49.5f),
            };

            return SequenceDefinition.Create("PIO_BeltCinema", shots, cues);
        }

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
