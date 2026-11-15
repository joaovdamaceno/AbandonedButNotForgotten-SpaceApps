using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-009 · Builder da placa (CORE-006, raiz <c>[R5] Pioneer Plaque</c> na cena da Pioneer): gera o SVG mestre
    /// <c>Art/PlaqueEducational.svg</c> e os cinco PNGs do painel a partir da MESMA lista de formas
    /// (<see cref="PioneerPlaqueArt"/>), <c>UI/PlaquePanel.prefab</c> (UIDocument + <see cref="PlaquePanelView"/> +
    /// <see cref="ResponsiveLayoutClass"/>), <see cref="PioneerPlaqueActivity"/> ligada ao palco e a sequência
    /// <c>Sequences/PIO_Plaque.asset</c> (33 s).
    /// </summary>
    public sealed class PioneerPlaqueBuilder : SceneBuilderModule
    {
        public const string PlaqueBuilderId = "pioneer10.stage.plaque"; // depois de pioneer10.stage, de que depende
        public const string PlaqueRootName = "[R5] Pioneer Plaque";
        public const string PanelPrefabPath = ChapterFolder + "/UI/PlaquePanel.prefab";
        public const string PanelLayoutPath = ChapterFolder + "/UI/PlaquePanel.uxml";
        public const string SequencePath = ChapterFolder + "/Sequences/PIO_Plaque.asset";
        public const string SvgPath = ArtFolder + "/PlaqueEducational.svg";
        public const string PlaqueArtFolder = ArtFolder + "/Plaque";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"; // UI-001 (consumido, não editado)

        public static string IconPath(int piece) => PlaqueArtFolder + "/Textures/PIO_Plaque_" + PioneerPlaqueArt.PieceIds[piece] + ".png";

        public override string Id => PlaqueBuilderId;
        public override string Owner => "R5 · PIO-009";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => PlaqueRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).FirstOrDefault();
            if (stage == null)
                throw new System.InvalidOperationException("o palco da PIO-003 (pioneer10.stage) precisa existir antes da placa.");

            WriteSvg();
            for (var piece = 0; piece < PioneerPlaqueActivity.PieceCount; piece++)
            {
                var index = piece;
                TextureAsset(context, "PIO_Plaque_" + PioneerPlaqueArt.PieceIds[piece] + ".png", () => PioneerPlaqueArt.Render(index), 256, false,
                    TextureWrapMode.Clamp, TextureWrapMode.Clamp, FilterMode.Bilinear, PlaqueArtFolder, alpha: true);
            }

            // O USS aponta para os PNGs por url(): se ele foi importado ANTES de os PNGs existirem (primeira geração),
            // a Unity guardou o ícone "aviso amarelo". Reimporta estilo e layout depois das texturas.
            AssetDatabase.ImportAsset(ChapterFolder + "/UI/PlaquePanel.uss", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(PanelLayoutPath, ImportAssetOptions.ForceUpdate);
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || panelSettings == null)
                throw new System.InvalidOperationException("PlaquePanel.uxml ou SpaceAppsPanelSettings ausente.");

            var prefab = context.Prefab(PanelPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 10;
                var view = Get<PlaquePanelView>(root);
                Set(view, "document", document);
                Get<ResponsiveLayoutClass>(root); // UI_SPEC: todo UIDocument de tela cheia aplica sa-layout--narrow em 4:3
            });

            var root = context.Root.transform;
            var panel = context.PrefabInstance(root, prefab, "PlaquePanel");
            var sequence = BuildSequence(context);
            var activity = context.Component<PioneerPlaqueActivity>(context.Child(root, "PioneerPlaqueActivity"));
            var so = new SerializedObject(activity);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("presentation").objectReferenceValue = stage;
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<PlaquePanelView>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath);
            if (sequence == null)
            {
                errors.Add($"{PlaqueBuilderId}: {SequencePath} ausente.");
                return;
            }

            errors.AddRange(sequence.Validate().Select(e => $"{PlaqueBuilderId}: {e}"));
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var shot in sequence.Shots)
            {
                if (!stage.TryGetAnchor(shot.RigId, out _))
                    errors.Add($"{PlaqueBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
            }

            if (!File.Exists(SvgPath))
                errors.Add($"{PlaqueBuilderId}: {SvgPath} ausente.");
            for (var piece = 0; piece < PioneerPlaqueActivity.PieceCount; piece++)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath(piece)) == null)
                    errors.Add($"{PlaqueBuilderId}: textura {IconPath(piece)} ausente.");
            }

            var activity = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPlaqueActivity>(true)).FirstOrDefault();
            if (activity == null)
                errors.Add($"{PlaqueBuilderId}: PioneerPlaqueActivity ausente da cena.");
            else
                errors.AddRange(activity.ValidateSetup().Select(e => $"{PlaqueBuilderId}: {e}"));
        }

        /// <summary>Grava o SVG mestre só se mudou (reconstruir sem mudanças deixa o arquivo idêntico).</summary>
        static void WriteSvg()
        {
            SceneBuildContext.EnsureFolder(Path.GetDirectoryName(SvgPath)?.Replace('\\', '/'));
            var svg = PioneerPlaqueArt.BoardSvg();
            if (!File.Exists(SvgPath) || File.ReadAllText(SvgPath, Encoding.UTF8) != svg)
            {
                File.WriteAllText(SvgPath, svg, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(SvgPath, ImportAssetOptions.ForceUpdate);
            }
        }

        static SequenceDefinition BuildSequence(SceneBuildContext context)
        {
            var fresh = CreateSequence();
            fresh.name = "PIO_Plaque";
            var asset = context.Asset(SequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Plaque";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>Deslocamento do zoom da peça no rig do plano geral, com uma deriva lenta para a frente (<paramref name="drift"/> m).</summary>
        static Vector3 PieceView(int piece, float drift = 0f) => PioneerStageBuilder.PlaquePieceOffset(piece) + new Vector3(0f, 0f, drift);

        /// <summary>
        /// Cutscene pura (sem encaixe, sem barra de E): a câmera mostra a placa dourada inteira e dá um zoom moderado em cada peça,
        /// seguindo as legendas. 0–7,5 chegada (placa inteira, depois a primeira peça); 7,5–27,5 as cinco peças (cada uma é
        /// enquadrada até 0,2 s depois do gatilho da sua legenda e a câmera segue para a próxima nos 0,8 s finais da legenda;
        /// marco em 27,4 s); 27,5–33 recuo até a placa inteira. As legendas das peças (PIO.05.02–06) abrem por gatilho e ficam o
        /// tempo de ler a 16 caracteres por segundo no pior idioma (de 3,6 a 4 s). 33 s é o mínimo que a regra de leitura permite.
        /// </summary>
        public static SequenceDefinition CreateSequence()
        {
            const string rig = "PIO.Plaque.Board";
            const float fov = PioneerStageBuilder.PlaqueHeroFov;
            var fit = new List<ShotKeyframe> { Key(0f, PieceView(0), fov) };
            for (var piece = 0; piece < 5; piece++)
            {
                var leave = piece * PioneerPlaqueActivity.CinemaPieceIntervalSeconds + 3.4f;
                if (piece < 4)
                {
                    fit.Add(Key(leave, PieceView(piece, 0.15f), fov));
                    fit.Add(Key(leave + 0.8f, PieceView(piece + 1), fov));
                }
                else
                {
                    fit.Add(Key(20f, PieceView(piece, 0.15f), fov));
                }
            }

            var shots = new[]
            {
                new ShotDefinition("PIO05.Arrive", 7.5f, rig,
                    new[]
                    {
                        Key(0f, new Vector3(-0.6f, 0.5f, -1.8f), fov),
                        Key(3.8f, Vector3.zero, fov),
                        Key(7.5f, PieceView(0), fov),
                    },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerPlaqueActivity.EventArrive) }),
                new ShotDefinition("PIO05.Fit", 20f, rig, fit,
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerPlaqueActivity.EventActivity), new ShotEvent(19.9f, PioneerPlaqueActivity.EventGate) }),
                new ShotDefinition("PIO05.Reveal", 5.5f, rig,
                    new[] { Key(0f, PieceView(4, 0.15f), fov), Key(5.5f, new Vector3(0f, 0f, -0.8f), fov) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerPlaqueActivity.EventReveal) }),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.05.01", "PIO.05.01", 0f, 7.3f),
                SubtitleCue.Triggered("PIO.05.02", "PIO.05.02", PioneerPlaqueActivity.TriggerIds[0], 4.0f),
                SubtitleCue.Triggered("PIO.05.03", "PIO.05.03", PioneerPlaqueActivity.TriggerIds[1], 3.7f),
                SubtitleCue.Triggered("PIO.05.04", "PIO.05.04", PioneerPlaqueActivity.TriggerIds[2], 3.9f),
                SubtitleCue.Triggered("PIO.05.05", "PIO.05.05", PioneerPlaqueActivity.TriggerIds[3], 3.6f),
                SubtitleCue.Triggered("PIO.05.06", "PIO.05.06", PioneerPlaqueActivity.TriggerIds[4], 3.6f),
                new SubtitleCue("PIO.05.07", "PIO.05.07", 27.7f, 33f),
            };

            return SequenceDefinition.Create("PIO_Plaque", shots, cues);
        }
    }
}
