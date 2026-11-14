using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-014 · Builder da rota completa (CORE-006, raiz <c>[R5] Pioneer Chapter</c> na cena da Pioneer): gera
    /// <c>Data/PioneerChapter.asset</c> (o <see cref="ChapterDefinition"/> dos oito estados), a sequência
    /// <c>Sequences/PIO_JupiterApproach.asset</c> (27 s, que nenhuma outra tarefa montava) e liga o
    /// <see cref="PioneerChapterController"/> a todas as atividades já presentes na cena.
    /// <para>
    /// O Id começa com <c>pioneer10.z</c> de propósito: os módulos rodam em ordem alfabética e este precisa vir
    /// DEPOIS do palco e de todas as atividades (<c>pioneer10.stage.*</c>) de que depende.
    /// </para>
    /// </summary>
    public sealed class PioneerChapterBuilder : SceneBuilderModule
    {
        public const string ChapterBuilderId = "pioneer10.zchapter";
        public const string ChapterRootName = "[R5] Pioneer Chapter";
        public const string ChapterAssetPath = ChapterFolder + "/Data/PioneerChapter.asset";
        public const string JupiterSequencePath = ChapterFolder + "/Sequences/PIO_JupiterApproach.asset";
        public const string DataAssetPath = ChapterFolder + "/Data/PioneerData.asset";
        public const string CueMapAssetPath = ChapterFolder + "/Audio/PioneerCueMap.asset";
        public const float TargetDurationSeconds = 375f;

        public override string Id => ChapterBuilderId;
        public override string Owner => "R5 · PIO-014";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => ChapterRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var scene = context.Scene.GetRootGameObjects();
            T Only<T>() where T : Component
            {
                var found = scene.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
                if (found.Length != 1)
                    throw new System.InvalidOperationException($"{typeof(T).Name}: esperado 1 na cena, encontrado {found.Length} (rode os builders das atividades antes: pioneer10.stage.*).");
                return found[0];
            }

            var presentation = Only<PioneerPresentation>();
            var chapter = BuildChapter(context);
            var jupiter = BuildJupiterSequence(context);

            var root = context.Root.transform;
            var narration = context.Component<PioneerNarration>(context.Child(root, "PioneerNarration"));
            var narrationSo = new SerializedObject(narration);
            narrationSo.FindProperty("data").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PioneerData>(DataAssetPath);
            narrationSo.FindProperty("cueMap").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PioneerCueMap>(CueMapAssetPath);
            narrationSo.ApplyModifiedPropertiesWithoutUndo();

            var sequences = context.Component<SequenceDirector>(context.Child(root, "SequenceDirector"));
            var controller = context.Component<PioneerChapterController>(context.Child(root, "PioneerChapterController"));
            var so = new SerializedObject(controller);
            so.FindProperty("chapter").objectReferenceValue = chapter;
            so.FindProperty("presentation").objectReferenceValue = presentation;
            so.FindProperty("sequenceDirector").objectReferenceValue = sequences;
            so.FindProperty("narration").objectReferenceValue = narration;
            so.FindProperty("launch").objectReferenceValue = Only<PioneerLaunchActivity>();
            so.FindProperty("belt").objectReferenceValue = Only<BeltInvestigation>();
            so.FindProperty("jupiterSequence").objectReferenceValue = jupiter;
            so.FindProperty("imaging").objectReferenceValue = Only<PioneerImaging>();
            so.FindProperty("recovery").objectReferenceValue = Only<ImagingRecovery>();
            so.FindProperty("reveal").objectReferenceValue = Only<HistoricalImageReveal>();
            so.FindProperty("plaque").objectReferenceValue = Only<PioneerPlaqueActivity>();
            so.FindProperty("outbound").objectReferenceValue = Only<OutboundAtlas>();
            so.FindProperty("listening").objectReferenceValue = Only<PioneerListening>();
            so.FindProperty("farewell").objectReferenceValue = Only<PioneerFarewellActivity>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var controller = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerChapterController>(true)).FirstOrDefault();
            if (controller == null)
            {
                errors.Add($"{ChapterBuilderId}: PioneerChapterController ausente da cena.");
                return;
            }

            errors.AddRange(controller.ValidateSetup().Select(e => $"{ChapterBuilderId}: {e}"));
            var jupiter = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(JupiterSequencePath);
            if (jupiter == null || Mathf.Abs(jupiter.TotalDurationSeconds - 27f) > 0.01f)
                errors.Add($"{ChapterBuilderId}: PIO.JupiterApproach deve durar 27 s ({JupiterSequencePath}).");
            if (jupiter != null)
            {
                var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
                foreach (var shot in jupiter.Shots)
                {
                    if (!stage.TryGetAnchor(shot.RigId, out _))
                        errors.Add($"{ChapterBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
                }
            }
        }

        // ---------- ChapterDefinition ----------

        /// <summary>
        /// Os oito estados em fila, na ordem do roteiro. Título, descrição e pergunta usam as chaves do dossiê (R2), já
        /// presentes em <c>UI/Dossier/DossierTexts.json</c>; a cena é <c>Pioneer10</c>.
        /// </summary>
        public static ChapterDefinition CreateChapter()
        {
            var ids = PioneerChapterController.StateIds;
            var steps = ids.Select((id, i) => i == ids.Length - 1 ? new ChapterStep(id, true) : new ChapterStep(id, false, ids[i + 1])).ToArray();
            return ChapterDefinition.Create(PioneerChapterController.ChapterId, "slot.pioneer10.title", "slot.pioneer10.meta", "slot.pioneer10.question",
                "Pioneer10", TargetDurationSeconds, "PIO_Launch", steps);
        }

        static ChapterDefinition BuildChapter(SceneBuildContext context)
        {
            var fresh = CreateChapter();
            fresh.name = "PioneerChapter";
            var asset = context.Asset(ChapterAssetPath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PioneerChapter";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }

        // ---------- PIO.JupiterApproach (27 s) ----------

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>
        /// 0–9 Júpiter cresce de ponto a disco (a ABERTURA do campo de visão, não um zoom por distâncias literais), 9–19 plano
        /// aberto com o planeta em ~2/3 do quadro e a sonda embaixo, 19–27 corte ao fotopolarímetro (a câmera pára antes da
        /// primeira instrução da PIO.Imaging). Legendas PIO.03.01–03 por janela. Estado SÓ de cinema: conclui quando a sequência
        /// termina. O enquadramento usa os anchors da PIO-003 e NÃO foi conferido no 3D.
        /// </summary>
        public static SequenceDefinition CreateJupiterSequence()
        {
            var shots = new[]
            {
                new ShotDefinition("PIO03.Grow", 9f, "PIO.JupiterApproach",
                    new[] { Key(0f, Vector3.zero, 70f), Key(9f, Vector3.zero, 44f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO03.Open", 10f, "PIO.JupiterApproach",
                    new[] { Key(0f, Vector3.zero, 44f), Key(10f, Vector3.zero, 44f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f, continueFromPreviousShot: true),
                new ShotDefinition("PIO03.Instrument", 8f, "PIO.JupiterApproach.Instrument",
                    new[] { Key(0f, Vector3.zero, 38f), Key(8f, new Vector3(0f, 0f, 0.05f), 38f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.03.01", "PIO.03.01", 0f, 8f),
                new SubtitleCue("PIO.03.02", "PIO.03.02", 9f, 17f),
                new SubtitleCue("PIO.03.03", "PIO.03.03", 18f, 26f),
            };

            return SequenceDefinition.Create("PIO_JupiterApproach", shots, cues);
        }

        static SequenceDefinition BuildJupiterSequence(SceneBuildContext context)
        {
            var fresh = CreateJupiterSequence();
            fresh.name = "PIO_JupiterApproach";
            var asset = context.Asset(JupiterSequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_JupiterApproach";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
        }
    }
}
