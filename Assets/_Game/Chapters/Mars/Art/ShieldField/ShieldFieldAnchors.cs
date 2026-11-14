using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-015: pontos de ligação do campo do escudo para a MAR-006 (e a montagem MAR-009), sem lógica de interação.
    /// O escudo (hardware descartado, M17) e o meteorito (ciência, M18–M19) são objetos distintos com estações e
    /// enquadramentos próprios; o rover só precisa chegar às estações. Metros; âncoras já assentadas no terreno do prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShieldFieldAnchors : MonoBehaviour
    {
        /// <summary>Distância máxima, em metros, entre a estação do escudo e a rocha metálica (M18: "no máximo cinco metros").</summary>
        public const float MaxShieldToMeteoriteMeters = 5f;

        [Header("M16 · movimento final de 5 m")]
        [SerializeField] Transform approachStart;

        [Header("M17 · escudo (hardware descartado)")]
        [SerializeField, Tooltip("Onde o rover para; olha para o destroço principal e o fragmento lateral.")] Transform shieldStation;
        [SerializeField, Tooltip("Ponto que enquadra o destroço principal e o fragmento juntos.")] Transform shieldPhotoTarget;
        [SerializeField, Tooltip("Pose sugerida da câmera de foto (olha para o alvo).")] Transform shieldCameraAnchor;

        [Header("M18–M19 · meteorito (ciência)")]
        [SerializeField, Tooltip("Onde o rover para para examinar a rocha metálica, ao alcance do E.")] Transform meteoriteStation;
        [SerializeField] Transform meteoriteMeasurePoint;
        [SerializeField] Transform meteoritePhotoTarget;
        [SerializeField] Transform meteoriteCameraAnchor;
        [SerializeField, Tooltip("Corredor livre de pedras e destroços entre as estações.")] float corridorWidthMeters = 2.4f;

        public Transform ApproachStart => approachStart;
        public Transform ShieldStation => shieldStation;
        public Transform ShieldPhotoTarget => shieldPhotoTarget;
        public Transform ShieldCameraAnchor => shieldCameraAnchor;
        public Transform MeteoriteStation => meteoriteStation;
        public Transform MeteoriteMeasurePoint => meteoriteMeasurePoint;
        public Transform MeteoritePhotoTarget => meteoritePhotoTarget;
        public Transform MeteoriteCameraAnchor => meteoriteCameraAnchor;
        public float CorridorWidthMeters => corridorWidthMeters;

        /// <summary>Distância horizontal entre a estação do escudo e o meteorito.</summary>
        public float ShieldStationToMeteoriteMeters
        {
            get
            {
                if (shieldStation == null || meteoriteMeasurePoint == null)
                    return float.PositiveInfinity;

                var delta = meteoriteMeasurePoint.position - shieldStation.position;
                delta.y = 0f;
                return delta.magnitude;
            }
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(ShieldFieldAnchors);
            Require(errors, owner, approachStart, nameof(approachStart));
            Require(errors, owner, shieldStation, nameof(shieldStation));
            Require(errors, owner, shieldPhotoTarget, nameof(shieldPhotoTarget));
            Require(errors, owner, shieldCameraAnchor, nameof(shieldCameraAnchor));
            Require(errors, owner, meteoriteStation, nameof(meteoriteStation));
            Require(errors, owner, meteoriteMeasurePoint, nameof(meteoriteMeasurePoint));
            Require(errors, owner, meteoritePhotoTarget, nameof(meteoritePhotoTarget));
            Require(errors, owner, meteoriteCameraAnchor, nameof(meteoriteCameraAnchor));
            if (errors.Count == 0 && ShieldStationToMeteoriteMeters > MaxShieldToMeteoriteMeters)
                ContractValidation.Report(errors, owner, $"a rocha metálica está a {ShieldStationToMeteoriteMeters:0.0} m da estação do escudo; o roteiro pede no máximo {MaxShieldToMeteoriteMeters} m.");

            ContractValidation.RequirePositive(errors, owner, nameof(corridorWidthMeters), corridorWidthMeters);
            return errors;
        }

        static void Require(List<string> errors, string owner, Transform value, string name)
        {
            if (value == null)
                ContractValidation.Report(errors, owner, $"'{name}' não está ligada.");
        }
    }
}
