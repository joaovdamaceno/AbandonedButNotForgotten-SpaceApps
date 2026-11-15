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
    /// <c>Sequences/PIO_JupiterApproach.asset</c> (30 s, que nenhuma outra tarefa montava) e liga o
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
        public const string ClosestSequencePath = ChapterFolder + "/Sequences/PIO_Closest.asset";
        public const string DataAssetPath = ChapterFolder + "/Data/PioneerData.asset";
        public const string CueMapAssetPath = ChapterFolder + "/Audio/PioneerCueMap.asset";
        public const float TargetDurationSeconds = 417f;

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
            var closest = BuildClosestSequence(context);

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
            so.FindProperty("closestSequence").objectReferenceValue = closest;
            so.FindProperty("beltCinemaSequence").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(PioneerBeltBuilder.CinemaSequencePath);
            var beltCounterComponent = Only<PioneerBeltCounter>();
            so.FindProperty("beltCounter").objectReferenceValue = beltCounterComponent;
            var beltCounterSo = new SerializedObject(beltCounterComponent);
            beltCounterSo.FindProperty("hud").objectReferenceValue = Only<CinemaHudView>();
            beltCounterSo.ApplyModifiedPropertiesWithoutUndo();
            var reveal3 = context.Component<PioneerJupiterReveal>(context.Child(root, "JupiterReveal"));
            var reveal3So = new SerializedObject(reveal3);
            reveal3So.FindProperty("presentation").objectReferenceValue = presentation;
            reveal3So.FindProperty("sequence").objectReferenceValue = jupiter;
            reveal3So.FindProperty("earth").objectReferenceValue = presentation.Earth;
            reveal3So.FindProperty("earthFrom").vector3Value = PioneerStageBuilder.EarthCompareFrom;
            reveal3So.FindProperty("earthTo").vector3Value = PioneerStageBuilder.EarthCompareTo;
            reveal3So.ApplyModifiedPropertiesWithoutUndo();
            so.FindProperty("jupiterReveal").objectReferenceValue = reveal3;
            var closestHud = context.Component<PioneerClosestHud>(context.Child(root, "ClosestHud"));
            var closestHudSo = new SerializedObject(closestHud);
            closestHudSo.FindProperty("hud").objectReferenceValue = Only<CinemaHudView>();
            closestHudSo.FindProperty("sequence").objectReferenceValue = closest;
            closestHudSo.ApplyModifiedPropertiesWithoutUndo();
            so.FindProperty("closestHud").objectReferenceValue = closestHud;
            var yearsHud = context.Component<PioneerYearsHud>(context.Child(root, "YearsHud"));
            var yearsHudSo = new SerializedObject(yearsHud);
            yearsHudSo.FindProperty("hud").objectReferenceValue = Only<CinemaHudView>();
            yearsHudSo.FindProperty("sequence").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(PioneerOutboundBuilder.SequencePath);
            yearsHudSo.ApplyModifiedPropertiesWithoutUndo();
            so.FindProperty("yearsHud").objectReferenceValue = yearsHud;
            var firstsHud = context.Component<PioneerFirstsHud>(context.Child(root, "FirstsHud"));
            var firstsHudSo = new SerializedObject(firstsHud);
            firstsHudSo.FindProperty("hud").objectReferenceValue = Only<CinemaHudView>();
            firstsHudSo.FindProperty("sequence").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(PioneerFarewellBuilder.SequencePath);
            firstsHudSo.ApplyModifiedPropertiesWithoutUndo();
            so.FindProperty("firstsHud").objectReferenceValue = firstsHud;
            var letterbox = context.Component<PioneerLetterbox>(context.Child(root, "Letterbox"));
            var letterboxSo = new SerializedObject(letterbox);
            letterboxSo.FindProperty("hud").objectReferenceValue = Only<CinemaHudView>();
            letterboxSo.ApplyModifiedPropertiesWithoutUndo();
            so.FindProperty("letterbox").objectReferenceValue = letterbox;
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
            if (jupiter == null || Mathf.Abs(jupiter.TotalDurationSeconds - 30f) > 0.01f)
                errors.Add($"{ChapterBuilderId}: PIO.JupiterApproach deve durar 30 s ({JupiterSequencePath}).");
            var closest = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(ClosestSequencePath);
            if (closest == null || Mathf.Abs(closest.TotalDurationSeconds - 65f) > 0.01f)
                errors.Add($"{ChapterBuilderId}: PIO.Closest deve durar 65 s ({ClosestSequencePath}).");
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var sequence in new[] { jupiter, closest })
            {
                if (sequence == null)
                    continue;

                foreach (var shot in sequence.Shots)
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

        // ---------- PIO.JupiterApproach (30 s) ----------

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>
        /// 0–9 Júpiter cresce de ponto a disco (a ABERTURA do campo de visão, não um zoom por distâncias literais), 9–22 plano
        /// aberto com o planeta em ~2/3 do quadro e a sonda embaixo (a Terra entra em 9,5 s e a legenda da comparação cai nela), 22–30 corte ao fotopolarímetro (a câmera pára antes da
        /// primeira instrução da PIO.Imaging). Legendas PIO.03.01–03 por janela. Estado SÓ de cinema: conclui quando a sequência
        /// termina. O enquadramento usa os anchors da PIO-003 e NÃO foi conferido no 3D.
        /// </summary>
        public static SequenceDefinition CreateJupiterSequence()
        {
            var shots = new[]
            {
                new ShotDefinition("PIO03.Grow", 9f, "PIO.JupiterApproach",
                    new[] { Key(0f, Vector3.zero, 70f), Key(9f, Vector3.zero, PioneerStageBuilder.ApproachOpenFov) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO03.Open", 13f, "PIO.JupiterApproach",
                    new[] { Key(0f, Vector3.zero, PioneerStageBuilder.ApproachOpenFov), Key(13f, Vector3.zero, PioneerStageBuilder.ApproachOpenFov) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f, continueFromPreviousShot: true),
                new ShotDefinition("PIO03.Instrument", 8f, "PIO.JupiterApproach.Instrument",
                    new[] { Key(0f, Vector3.zero, 38f), Key(8f, new Vector3(0f, 0f, 0.05f), 38f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.03.01", "PIO.03.01", 0f, 6f),
                new SubtitleCue("PIO.03.02", "PIO.03.02", 9f, 15f),
                new SubtitleCue("PIO.03.03", "PIO.03.03", 17f, 23f),
                new SubtitleCue("PIO.03.04", "PIO.03.04", 24f, 29f),
            };

            return SequenceDefinition.Create("PIO_JupiterApproach", shots, cues);
        }

        // ---------- PIO.Closest (65 s) ----------

        static ShotKeyframe KeyAt(float t, Vector3 p, Vector3 euler, float fov) => new ShotKeyframe(t, p, euler, fov);

        /// <summary>
        /// Roteiro v2, cena 5 · "O mais perto possível" (só cinema, 65 s, o tempo do roteiro). 0–12 as três luas, 12–27 a radiação sobre o
        /// fotopolarímetro, 27–41 a passagem rente às nuvens (data e distância), 41–55 o plano alto da curva, 55–65 a sonda saindo.
        /// Usa só os anchors do palco. Legendas PIO.09.01–07, cada uma com a janela de leitura que o texto pede.
        /// </summary>
        public static SequenceDefinition CreateClosestSequence()
        {
            var shots = new[]
            {
                // 5a (0–12 s): as três luas galileanas cruzam o quadro, uma a cada 4 s (a câmera desliza ao lado de cada uma).
                new ShotDefinition("PIO09.Ganymede", 4f, "PIO.Closest.Ganymede",
                    new[] { Key(0f, new Vector3(-6f, 0f, 0f), 30f), Key(4f, new Vector3(6f, 0f, 0f), 30f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO09.Callisto", 4f, "PIO.Closest.Callisto",
                    new[] { Key(0f, new Vector3(6f, 0f, 0f), 30f), Key(4f, new Vector3(-6f, 0f, 0f), 30f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO09.Europa", 4f, "PIO.Closest.Europa",
                    new[] { Key(0f, new Vector3(-5f, 0f, 0f), 30f), Key(4f, new Vector3(5f, 0f, 0f), 30f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO09.Radiation", 15f, "PIO.JupiterApproach.Instrument",
                    new[]
                    {
                        Key(0f, Vector3.zero, 38f), Key(4f, new Vector3(0.012f, -0.008f, 0.01f), 38f), Key(8f, new Vector3(-0.01f, 0.01f, 0.02f), 37f),
                        Key(11f, new Vector3(0.014f, 0.006f, 0.03f), 36f), Key(15f, new Vector3(0f, 0f, 0.05f), 35f),
                    },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO09.Pass", 14f, "PIO.Closest",
                    new[] { Key(0f, new Vector3(-70f, 6f, -30f), 60f), Key(14f, new Vector3(70f, -4f, 40f), 72f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO09.Bend", 14f, "PIO.Closest.Bend",
                    new[] { Key(0f, Vector3.zero, 50f), Key(14f, new Vector3(0f, 0f, -70f), 50f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f),
                new ShotDefinition("PIO09.Leaving", 10f, "PIO.Outbound",
                    new[] { Key(0f, Vector3.zero, 40f), Key(10f, new Vector3(0f, 0f, -8f), 38f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.09.01", "PIO.09.01", 1f, 8f),
                new SubtitleCue("PIO.09.02", "PIO.09.02", 12.5f, 19.5f),
                new SubtitleCue("PIO.09.03", "PIO.09.03", 20f, 27f),
                new SubtitleCue("PIO.09.04", "PIO.09.04", 28f, 35f),
                new SubtitleCue("PIO.09.05", "PIO.09.05", 35.5f, 42f),
                new SubtitleCue("PIO.09.06", "PIO.09.06", 43f, 51f),
                new SubtitleCue("PIO.09.07", "PIO.09.07", 56f, 64f),
            };

            return SequenceDefinition.Create("PIO_Closest", shots, cues);
        }

        static SequenceDefinition BuildClosestSequence(SceneBuildContext context)
        {
            var fresh = CreateClosestSequence();
            fresh.name = "PIO_Closest";
            var asset = context.Asset(ClosestSequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Closest";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
            return asset;
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
