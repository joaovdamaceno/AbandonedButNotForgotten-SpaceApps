using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-012: âncoras de um alvo científico visual (BlueberriesVisual ou McKittrickVisual). Só posições e poses, sem lógica
    /// de interação: as atividades da MAR-003 (esferas) e MAR-004/005 (RAT e espectrômetros) consomem estes transforms.
    /// Eixos do prefab: +Z entra na rocha a partir do rover; +Y é a normal da superfície de trabalho quando existir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EagleTargetAnchors : MonoBehaviour
    {
        [SerializeField, Tooltip("Ponto no chão junto ao alvo: âncora do círculo E (verbo + objeto) e da chegada.")] Transform promptAnchor;
        [SerializeField, Tooltip("Pose do rover/câmera de condução ao chegar; olha para o alvo.")] Transform stationAnchor;
        [SerializeField, Tooltip("Macrovisão principal (esferas: solo; McKittrick: disco de trabalho).")] Transform primaryView;
        [SerializeField, Tooltip("Macrovisão secundária (esferas: rocha). Opcional na McKittrick.")] Transform secondaryView;
        [SerializeField, Tooltip("McKittrick: ponto na superfície plana com +Y = normal da rocha, onde a RatCavitySurface (escala 1) é montada.")] Transform ratSurfaceMount;

        public Transform PromptAnchor => promptAnchor;
        public Transform StationAnchor => stationAnchor;
        public Transform PrimaryView => primaryView;
        public Transform SecondaryView => secondaryView;
        public Transform RatSurfaceMount => ratSurfaceMount;

        public List<string> ValidateSetup(bool needsRatMount)
        {
            var errors = new List<string>();
            var owner = $"{nameof(EagleTargetAnchors)} '{name}'";
            Require(errors, owner, promptAnchor, nameof(promptAnchor));
            Require(errors, owner, stationAnchor, nameof(stationAnchor));
            Require(errors, owner, primaryView, nameof(primaryView));
            if (needsRatMount)
            {
                Require(errors, owner, ratSurfaceMount, nameof(ratSurfaceMount));
                if (ratSurfaceMount != null && Vector3.Dot(ratSurfaceMount.up, Vector3.up) < 0.5f)
                    ContractValidation.Report(errors, owner, "a superfície da RAT está muito inclinada (normal a mais de 60° da vertical): o braço não a alcança por cima.");
            }
            else
            {
                Require(errors, owner, secondaryView, nameof(secondaryView));
            }

            return errors;
        }

        static void Require(List<string> errors, string owner, Transform value, string field)
        {
            if (value == null)
                ContractValidation.Report(errors, owner, $"'{field}' não está ligada.");
        }
    }
}
