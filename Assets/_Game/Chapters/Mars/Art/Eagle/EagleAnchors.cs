using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-012: pontos de ligação da cratera Eagle (M05–M14) para a MAR-003/004/005/009 e a montagem, sem lógica de jogo.
    /// A rota é uma curva curta já assentada no terreno: início (ponto de parada do pouso), estação das esferas e estação da
    /// McKittrick (≈ 10 m adiante, depois de uma curva que a revela). Metros; origem no ponto de parada, y = 0 na planície.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EagleAnchors : MonoBehaviour
    {
        /// <summary>Raio do CharacterController do rover (Prefabs/MarsRoverGameplay), em metros.</summary>
        public const float RoverRadiusMeters = 0.8f;

        /// <summary>Largura mínima livre da rota: o diâmetro do rover mais 0,4 m de folga de cada lado.</summary>
        public const float MinCorridorWidthMeters = RoverRadiusMeters * 2f + 0.8f;

        /// <summary>M10: "cerca de 10 m" entre as duas estações; a tolerância cobre a curva real do terreno.</summary>
        public const float MinStationToStationMeters = 8.5f;
        public const float MaxStationToStationMeters = 11.5f;

        [Header("Rota (amostras a cada ~0,5 m, já no chão)")]
        [SerializeField, Tooltip("Ponto de parada do pouso (M05/M06): onde a câmera de condução começa.")] Transform routeStart;
        [SerializeField, Tooltip("Amostras da linha central da rota, em ordem, do início até a estação da McKittrick.")] Transform[] routeSamples = new Transform[0];
        [SerializeField, Min(0), Tooltip("Índice em routeSamples da estação das esferas.")] int blueberriesStationIndex;
        [SerializeField, Min(0), Tooltip("Índice em routeSamples da estação da McKittrick (última amostra).")] int mcKittrickStationIndex;
        [SerializeField, Tooltip("Largura livre de colisores ao longo da rota.")] float corridorWidthMeters = 3.2f;

        [Header("M08–M09 · esferas (instância de BlueberriesVisual)")]
        [SerializeField] EagleTargetAnchors blueberries;

        [Header("M10–M14 · McKittrick (instância de McKittrickVisual)")]
        [SerializeField] EagleTargetAnchors mcKittrick;

        [Header("M05 · plano elevado que revela a pequena bacia e o afloramento")]
        [SerializeField] Transform overviewCamera;
        [SerializeField, Tooltip("Ponto que o plano elevado enquadra (centro do afloramento das esferas).")] Transform overviewTarget;

        public Transform RouteStart => routeStart;
        public IReadOnlyList<Transform> RouteSamples => routeSamples;
        public int BlueberriesStationIndex => blueberriesStationIndex;
        public int McKittrickStationIndex => mcKittrickStationIndex;
        public float CorridorWidthMeters => corridorWidthMeters;
        public EagleTargetAnchors Blueberries => blueberries;
        public EagleTargetAnchors McKittrick => mcKittrick;
        public Transform OverviewCamera => overviewCamera;
        public Transform OverviewTarget => overviewTarget;

        /// <summary>Comprimento horizontal da rota entre duas amostras (inclusive), em metros.</summary>
        public float RouteLengthMeters(int fromIndex, int toIndex)
        {
            var length = 0f;
            for (var i = Mathf.Max(0, fromIndex); i < Mathf.Min(toIndex, routeSamples.Length - 1); i++)
            {
                if (routeSamples[i] == null || routeSamples[i + 1] == null)
                    return float.PositiveInfinity;

                var delta = routeSamples[i + 1].position - routeSamples[i].position;
                delta.y = 0f;
                length += delta.magnitude;
            }

            return length;
        }

        /// <summary>Do início até a estação das esferas.</summary>
        public float StartToBlueberriesMeters => RouteLengthMeters(0, blueberriesStationIndex);

        /// <summary>Da estação das esferas até a da McKittrick, pela rota (a curva incluída).</summary>
        public float BlueberriesToMcKittrickMeters => RouteLengthMeters(blueberriesStationIndex, mcKittrickStationIndex);

        /// <summary>Distância horizontal entre os dois alvos científicos.</summary>
        public float TargetSeparationMeters
        {
            get
            {
                if (blueberries == null || mcKittrick == null || blueberries.PromptAnchor == null || mcKittrick.PromptAnchor == null)
                    return 0f;

                var delta = mcKittrick.PromptAnchor.position - blueberries.PromptAnchor.position;
                delta.y = 0f;
                return delta.magnitude;
            }
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(EagleAnchors);
            if (routeStart == null)
                ContractValidation.Report(errors, owner, $"'{nameof(routeStart)}' não está ligada.");
            if (overviewCamera == null)
                ContractValidation.Report(errors, owner, $"'{nameof(overviewCamera)}' não está ligada.");
            if (overviewTarget == null)
                ContractValidation.Report(errors, owner, $"'{nameof(overviewTarget)}' não está ligada.");

            if (routeSamples == null || routeSamples.Length < 8)
                ContractValidation.Report(errors, owner, "a rota precisa de pelo menos 8 amostras.");
            else
            {
                for (var i = 0; i < routeSamples.Length; i++)
                {
                    if (routeSamples[i] == null)
                        ContractValidation.Report(errors, owner, $"amostra {i} da rota não está ligada.");
                }

                if (blueberriesStationIndex <= 0 || blueberriesStationIndex >= mcKittrickStationIndex || mcKittrickStationIndex >= routeSamples.Length)
                    ContractValidation.Report(errors, owner, "índices de estação fora de ordem ou fora da rota (esferas < McKittrick < amostras).");
            }

            ValidateTarget(errors, owner, nameof(blueberries), blueberries, false);
            ValidateTarget(errors, owner, nameof(mcKittrick), mcKittrick, true);
            ContractValidation.RequirePositive(errors, owner, nameof(corridorWidthMeters), corridorWidthMeters);
            if (corridorWidthMeters < MinCorridorWidthMeters)
                ContractValidation.Report(errors, owner, $"corredor de {corridorWidthMeters:0.0} m é estreito para o rover (mínimo {MinCorridorWidthMeters:0.0} m).");

            if (errors.Count == 0)
            {
                var between = BlueberriesToMcKittrickMeters;
                if (between < MinStationToStationMeters || between > MaxStationToStationMeters)
                    ContractValidation.Report(errors, owner, $"entre as estações há {between:0.0} m de rota; o roteiro pede cerca de 10 m (M10).");
                if (TargetSeparationMeters < 5f)
                    ContractValidation.Report(errors, owner, "os dois alvos estão perto demais para serem distintos.");
            }

            return errors;
        }

        static void ValidateTarget(List<string> errors, string owner, string field, EagleTargetAnchors target, bool needsRatMount)
        {
            if (target == null)
            {
                ContractValidation.Report(errors, owner, $"'{field}' não está ligada.");
                return;
            }

            errors.AddRange(target.ValidateSetup(needsRatMount));
        }
    }
}
