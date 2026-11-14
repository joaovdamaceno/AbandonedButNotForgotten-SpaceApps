using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Etapa do capítulo → região 3D que fica ativa nela.</summary>
    [Serializable]
    public struct MarsStepRegion
    {
        public string stepId;
        public string regionId;
    }

    /// <summary>
    /// Dados de montagem do capítulo de Marte (MAR-009): referências SERIALIZADAS a todos os prefabs e dados (nada é procurado por nome),
    /// a tabela etapa → região das 32 etapas e o orçamento de 375,8 s. A montagem de Editor (<c>MarsChapterAssembly</c>) instancia estes
    /// prefabs de forma determinística; o <see cref="MarsChapterBindings"/> usa a tabela em execução.
    /// </summary>
    [CreateAssetMenu(fileName = "MarsSceneBindings", menuName = "Abandoned/Mars Scene Bindings")]
    public sealed class MarsSceneBindings : ScriptableObject
    {
        public const float TargetSeconds = 375.8f;
        public const float BudgetToleranceSeconds = 1f;

        [Header("Dados")]
        [SerializeField] ChapterDefinition chapter;
        [SerializeField] TextAsset subtitlesJson;
        [SerializeField] MarsChapterSequenceCatalog sequenceCatalog;
        [SerializeField] MarsLightingBindings lighting;

        [Header("Regiões (um palco por região; só uma região ativa por vez)")]
        [SerializeField] GameObject originStage;
        [SerializeField] GameObject landingAssembly;
        [SerializeField, Tooltip("Opcional até a MAR-012 ser integrada: sem prefab, a montagem cria um piso provisório identificado.")] GameObject eagleEnvironment;
        [SerializeField] GameObject shieldField;
        [SerializeField] GameObject victoriaStage;
        [SerializeField] GameObject valleyStage;
        [SerializeField] GameObject spiritStage;
        [SerializeField] GameObject memorialStage;

        [Header("Jogo")]
        [SerializeField] GameObject roverGameplay;
        [SerializeField] GameObject roverVisual;
        [SerializeField] GameObject blueberries;
        [SerializeField] GameObject ratActivity;
        [SerializeField] GameObject compositionActivity;
        [SerializeField] GameObject shieldInteractions;
        [SerializeField] GameObject stormControls;
        [SerializeField] GameObject finalSurvey;
        [SerializeField] GameObject cameraAnchors;

        [Header("Etapas")]
        [SerializeField] List<MarsStepRegion> stepRegions = new List<MarsStepRegion>();

        public ChapterDefinition Chapter => chapter;
        public TextAsset SubtitlesJson => subtitlesJson;
        public MarsChapterSequenceCatalog SequenceCatalog => sequenceCatalog;
        public MarsLightingBindings Lighting => lighting;
        public GameObject OriginStage => originStage;
        public GameObject LandingAssembly => landingAssembly;
        public GameObject EagleEnvironment => eagleEnvironment;
        public GameObject ShieldField => shieldField;
        public GameObject VictoriaStage => victoriaStage;
        public GameObject ValleyStage => valleyStage;
        public GameObject SpiritStage => spiritStage;
        public GameObject MemorialStage => memorialStage;
        public GameObject RoverGameplay => roverGameplay;
        public GameObject RoverVisual => roverVisual;
        public GameObject Blueberries => blueberries;
        public GameObject RatActivity => ratActivity;
        public GameObject CompositionActivity => compositionActivity;
        public GameObject ShieldInteractions => shieldInteractions;
        public GameObject StormControls => stormControls;
        public GameObject FinalSurvey => finalSurvey;
        public GameObject CameraAnchors => cameraAnchors;
        public IReadOnlyList<MarsStepRegion> StepRegions => stepRegions;

        public void SetStepRegions(IEnumerable<MarsStepRegion> value) => stepRegions = new List<MarsStepRegion>(value);

        public string RegionOf(string stepId)
        {
            for (var i = 0; i < stepRegions.Count; i++)
            {
                if (stepRegions[i].stepId == stepId)
                    return stepRegions[i].regionId;
            }

            return null;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsSceneBindings);
            void Need(UnityEngine.Object value, string field)
            {
                if (value == null)
                    ContractValidation.Report(errors, owner, $"{field} não atribuído.");
            }

            Need(chapter, nameof(chapter));
            Need(subtitlesJson, nameof(subtitlesJson));
            Need(sequenceCatalog, nameof(sequenceCatalog));
            Need(lighting, nameof(lighting));
            Need(originStage, nameof(originStage));
            Need(landingAssembly, nameof(landingAssembly));
            Need(shieldField, nameof(shieldField));
            Need(victoriaStage, nameof(victoriaStage));
            Need(valleyStage, nameof(valleyStage));
            Need(spiritStage, nameof(spiritStage));
            Need(memorialStage, nameof(memorialStage));
            Need(roverGameplay, nameof(roverGameplay));
            Need(roverVisual, nameof(roverVisual));
            Need(blueberries, nameof(blueberries));
            Need(ratActivity, nameof(ratActivity));
            Need(compositionActivity, nameof(compositionActivity));
            Need(shieldInteractions, nameof(shieldInteractions));
            Need(stormControls, nameof(stormControls));
            Need(finalSurvey, nameof(finalSurvey));
            Need(cameraAnchors, nameof(cameraAnchors));

            var steps = MarsChapterFlow.CreateDefaultBindings();
            var seen = new HashSet<string>();
            foreach (var entry in stepRegions)
            {
                ContractValidation.RequireId(errors, owner, "stepRegions[].regionId", entry.regionId);
                if (!seen.Add(entry.stepId))
                    ContractValidation.Report(errors, owner, $"etapa '{entry.stepId}' duplicada na tabela de regiões.");
            }

            var total = 0f;
            foreach (var step in steps)
            {
                total += step.ReferenceSeconds;
                if (!seen.Contains(step.StepId))
                    ContractValidation.Report(errors, owner, $"etapa '{step.StepId}' sem região.");
            }

            if (stepRegions.Count != steps.Count)
                ContractValidation.Report(errors, owner, $"{stepRegions.Count} etapas mapeadas para {steps.Count}.");
            if (Mathf.Abs(total - TargetSeconds) > BudgetToleranceSeconds)
                ContractValidation.Report(errors, owner, $"orçamento de referência soma {total} s; meta {TargetSeconds} s.");
            if (chapter != null && Mathf.Abs(chapter.TargetDurationSeconds - TargetSeconds) > BudgetToleranceSeconds)
                ContractValidation.Report(errors, owner, $"ChapterDefinition com {chapter.TargetDurationSeconds} s; meta {TargetSeconds} s.");
            return errors;
        }
    }
}
