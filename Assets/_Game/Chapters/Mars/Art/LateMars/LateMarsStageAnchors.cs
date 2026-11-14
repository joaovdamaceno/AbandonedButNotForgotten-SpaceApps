using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-016: pontos de ligação dos palcos de Marte tardio (Victoria 2007 e Perseverance Valley 2018) para a montagem e para a
    /// MAR-008. O mirante, as poses guiadas do Sol e do vale e a referência da Terra ficam a poucos metros do rover, dentro do palco.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LateMarsStageAnchors : MonoBehaviour
    {
        public enum StageKind
        {
            Victoria = 0,
            Valley = 1,
        }

        [SerializeField] StageKind kind;
        [SerializeField] Transform roverStart;
        [Header("Só no vale (M24–M30)")]
        [SerializeField, Tooltip("Mirante a poucos metros do rover: chegada de M24.")] Transform overlook;
        [SerializeField, Tooltip("Pose da câmera guiada que já enquadra o Sol.")] Transform sunView;
        [SerializeField, Tooltip("Pose da câmera guiada que já enquadra o vale.")] Transform valleyView;
        [SerializeField, Tooltip("Direção da Terra para a antena (M29).")] Transform earthTarget;

        public StageKind Kind => kind;
        public Transform RoverStart => roverStart;
        public Transform Overlook => overlook;
        public Transform SunView => sunView;
        public Transform ValleyView => valleyView;
        public Transform EarthTarget => earthTarget;

        /// <summary>Distância horizontal do rover ao mirante (metros). Infinita se faltar um dos dois.</summary>
        public float RoverToOverlookMeters
        {
            get
            {
                if (roverStart == null || overlook == null)
                    return float.PositiveInfinity;

                var delta = overlook.position - roverStart.position;
                delta.y = 0f;
                return delta.magnitude;
            }
        }

        /// <summary>As âncoras do palco do vale no formato que a <see cref="MarsFinalSurvey"/> pede.</summary>
        public MarsFinalSurvey.Anchors ToSurveyAnchors() => new MarsFinalSurvey.Anchors { overlook = overlook, sunView = sunView, valleyView = valleyView };

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(LateMarsStageAnchors);
            if (roverStart == null)
                ContractValidation.Report(errors, owner, "falta o ponto de partida do rover.");

            if (kind == StageKind.Valley)
            {
                if (overlook == null) ContractValidation.Report(errors, owner, "falta o mirante.");
                if (sunView == null) ContractValidation.Report(errors, owner, "falta a pose guiada do Sol.");
                if (valleyView == null) ContractValidation.Report(errors, owner, "falta a pose guiada do vale.");
                if (earthTarget == null) ContractValidation.Report(errors, owner, "falta a referência da Terra.");
                if (errors.Count == 0 && RoverToOverlookMeters > 12f)
                    ContractValidation.Report(errors, owner, $"o mirante está a {RoverToOverlookMeters:0.0} m do rover; deve ficar a poucos metros (≤ 12 m).");
            }

            return errors;
        }
    }
}
