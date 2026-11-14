using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum ShotEasing
    {
        Linear = 0,
        EaseInOut = 1,
    }

    /// <summary>Pose da câmera num instante do plano. Posição em metros; ângulos em graus.</summary>
    [Serializable]
    public struct ShotKeyframe
    {
        public float timeSeconds;
        public Vector3 positionMeters;
        public Vector3 eulerAnglesDegrees;
        public float fieldOfViewDegrees;

        public ShotKeyframe(float timeSeconds, Vector3 positionMeters, Vector3 eulerAnglesDegrees, float fieldOfViewDegrees)
        {
            this.timeSeconds = timeSeconds;
            this.positionMeters = positionMeters;
            this.eulerAnglesDegrees = eulerAnglesDegrees;
            this.fieldOfViewDegrees = fieldOfViewDegrees;
        }
    }

    /// <summary>Evento disparado num instante do plano, no mesmo relógio da câmera, dos atores e das legendas.</summary>
    [Serializable]
    public struct ShotEvent
    {
        public float timeSeconds;
        public string eventId;

        public ShotEvent(float timeSeconds, string eventId)
        {
            this.timeSeconds = timeSeconds;
            this.eventId = eventId;
        }
    }

    /// <summary>
    /// Plano de câmera. Ponto inicial/final = dois keyframes; movimentos mais ricos usam mais keyframes.
    /// A duração é a única fonte de tempo do plano: câmera, atores, legendas e término a consomem.
    /// </summary>
    [Serializable]
    public sealed class ShotDefinition
    {
        public const float MinFieldOfViewDegrees = 10f;
        public const float MaxFieldOfViewDegrees = 120f;

        [SerializeField] string shotId;
        [SerializeField] float durationSeconds = 5f;
        [SerializeField, Tooltip("ID do rig de câmera (CameraDirector). Câmeras especiais são rigs, não AudioListeners.")] string rigId;
        [SerializeField, Tooltip("ID do alvo de LookAt. Vazio = usa a rotação dos keyframes.")] string lookAtTargetId;
        [SerializeField] ShotEasing easing = ShotEasing.EaseInOut;
        [SerializeField, Tooltip("Altura mínima da câmera sobre o chão real, em metros (impede câmera abaixo da malha).")] float minGroundClearanceMeters = 0.3f;
        [SerializeField, Tooltip("Começa exatamente da pose e do FOV atuais da câmera (fim do plano anterior ou câmera de gameplay), ignorando o keyframe 0. Use quando a continuidade é prevista.")] bool continueFromPreviousShot;
        [SerializeField] List<ShotKeyframe> keyframes = new List<ShotKeyframe>();
        [SerializeField] List<ShotEvent> events = new List<ShotEvent>();

        public ShotDefinition()
        {
        }

        public ShotDefinition(string shotId, float durationSeconds, string rigId, IEnumerable<ShotKeyframe> keyframes,
            string lookAtTargetId = null, ShotEasing easing = ShotEasing.EaseInOut, float minGroundClearanceMeters = 0.3f,
            IEnumerable<ShotEvent> events = null, bool continueFromPreviousShot = false)
        {
            this.continueFromPreviousShot = continueFromPreviousShot;
            this.shotId = shotId;
            this.durationSeconds = durationSeconds;
            this.rigId = rigId;
            this.keyframes = new List<ShotKeyframe>(keyframes ?? Array.Empty<ShotKeyframe>());
            this.lookAtTargetId = lookAtTargetId;
            this.easing = easing;
            this.minGroundClearanceMeters = minGroundClearanceMeters;
            this.events = new List<ShotEvent>(events ?? Array.Empty<ShotEvent>());
        }

        public string ShotId => shotId;
        public float DurationSeconds => durationSeconds;
        public string RigId => rigId;
        public string LookAtTargetId => lookAtTargetId;
        public ShotEasing Easing => easing;
        public float MinGroundClearanceMeters => minGroundClearanceMeters;
        public IReadOnlyList<ShotKeyframe> Keyframes => keyframes;

        /// <summary>O plano parte da pose/FOV atuais da câmera; o keyframe 0 só define o instante inicial.</summary>
        public bool ContinueFromPreviousShot => continueFromPreviousShot;
        public IReadOnlyList<ShotEvent> Events => events;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"ShotDefinition '{shotId ?? "<sem id>"}'";
            ContractValidation.RequireId(errors, owner, nameof(shotId), shotId);
            ContractValidation.RequireId(errors, owner, nameof(rigId), rigId);
            ContractValidation.RequirePositive(errors, owner, nameof(durationSeconds), durationSeconds);
            ContractValidation.RequireNonNegative(errors, owner, nameof(minGroundClearanceMeters), minGroundClearanceMeters);
            if (!string.IsNullOrEmpty(lookAtTargetId))
                ContractValidation.RequireId(errors, owner, nameof(lookAtTargetId), lookAtTargetId);

            ValidateKeyframes(errors, owner);
            ValidateEvents(errors, owner);
            return errors;
        }

        void ValidateKeyframes(List<string> errors, string owner)
        {
            if (keyframes == null || keyframes.Count < 2)
            {
                ContractValidation.Report(errors, owner, "precisa de ao menos 2 keyframes (ponto inicial e final).");
                return;
            }

            for (var i = 0; i < keyframes.Count; i++)
            {
                var frame = keyframes[i];
                if (frame.fieldOfViewDegrees < MinFieldOfViewDegrees || frame.fieldOfViewDegrees > MaxFieldOfViewDegrees)
                {
                    ContractValidation.Report(errors, owner,
                        $"keyframe {i}: FOV {frame.fieldOfViewDegrees}° fora de {MinFieldOfViewDegrees}–{MaxFieldOfViewDegrees}°.");
                }

                if (i > 0 && frame.timeSeconds <= keyframes[i - 1].timeSeconds)
                    ContractValidation.Report(errors, owner, $"keyframe {i}: tempo {frame.timeSeconds} s não é crescente.");
            }

            if (Math.Abs(keyframes[0].timeSeconds) > ContractValidation.TimeToleranceSeconds)
                ContractValidation.Report(errors, owner, "o primeiro keyframe deve estar em 0 s.");

            if (durationSeconds > 0f && Math.Abs(keyframes[keyframes.Count - 1].timeSeconds - durationSeconds) > ContractValidation.TimeToleranceSeconds)
                ContractValidation.Report(errors, owner, $"o último keyframe ({keyframes[keyframes.Count - 1].timeSeconds} s) deve coincidir com a duração ({durationSeconds} s).");
        }

        void ValidateEvents(List<string> errors, string owner)
        {
            if (events == null)
                return;

            foreach (var shotEvent in events)
            {
                ContractValidation.RequireId(errors, owner, "events[].eventId", shotEvent.eventId);
                if (shotEvent.timeSeconds < 0f || shotEvent.timeSeconds > durationSeconds + ContractValidation.TimeToleranceSeconds)
                    ContractValidation.Report(errors, owner, $"evento '{shotEvent.eventId}' em {shotEvent.timeSeconds} s está fora do plano (0–{durationSeconds} s).");
            }
        }
    }
}
