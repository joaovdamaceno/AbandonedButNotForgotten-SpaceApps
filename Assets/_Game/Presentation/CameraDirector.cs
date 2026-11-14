using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Pose de câmera em espaço de mundo. Metros e graus.</summary>
    public struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float FieldOfViewDegrees;

        public CameraPose(Vector3 position, Quaternion rotation, float fieldOfViewDegrees)
        {
            Position = position;
            Rotation = rotation;
            FieldOfViewDegrees = fieldOfViewDegrees;
        }
    }

    /// <summary>
    /// ÚNICO dono da câmera do jogo. Avalia planos (<see cref="ShotDefinition"/>) em rigs registrados, aplica alvo de
    /// enquadramento, mantém a câmera acima do terreno real e garante um só AudioListener ativo. Câmeras especiais são
    /// rigs (transforms de referência), não câmeras ou AudioListeners concorrentes.
    /// Enquanto <see cref="IsControlled"/> é verdadeiro, scripts de câmera de gameplay não escrevem na câmera.
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        /// <summary>Rig implícito: keyframes já estão em coordenadas de mundo.</summary>
        public const string WorldRigId = "world";

        [SerializeField, Tooltip("A única câmera do jogo.")] Camera targetCamera;
        [SerializeField, Tooltip("Camadas consideradas chão para a proteção de altura. Prefira só a camada do terreno.")] LayerMask groundMask = ~0;
        [SerializeField, Min(1f), Tooltip("Altura acima da câmera de onde a sonda de chão começa, em metros.")] float groundProbeHeightMeters = 1000f;

        readonly Dictionary<string, Transform> rigs = new Dictionary<string, Transform>();
        readonly Dictionary<string, Transform> lookTargets = new Dictionary<string, Transform>();
        ControlLease controlLease;

        public Camera TargetCamera => targetCamera;
        public bool IsControlled => controlLease != null;
        public string ControlOwner => controlLease?.Owner;

        /// <summary>Altura do chão sob um ponto (ou nulo se não há chão). Padrão: raycast para baixo em <c>groundMask</c>.</summary>
        public Func<Vector3, float?> GroundProbe { get; set; }

        public CameraPose CurrentPose
        {
            get
            {
                if (targetCamera == null)
                    return default;

                var t = targetCamera.transform;
                return new CameraPose(t.position, t.rotation, targetCamera.fieldOfView);
            }
        }

        public void Initialize(Camera camera)
        {
            targetCamera = camera;
            EnforceSingleAudioListener();
        }

        void OnEnable()
        {
            if (targetCamera != null)
                EnforceSingleAudioListener();
        }

        public void RegisterRig(string rigId, Transform rig)
        {
            if (!ContractValidation.IsValidId(rigId) || rigId == WorldRigId || rig == null)
                throw new ArgumentException($"Rig inválido: '{rigId}'. Use um ID válido, diferente de '{WorldRigId}', e um Transform.");

            rigs[rigId] = rig;
        }

        public void UnregisterRig(string rigId) => rigs.Remove(rigId);

        public void RegisterLookTarget(string targetId, Transform target)
        {
            if (!ContractValidation.IsValidId(targetId) || target == null)
                throw new ArgumentException($"Alvo de enquadramento inválido: '{targetId}'.");

            lookTargets[targetId] = target;
        }

        public void UnregisterLookTarget(string targetId) => lookTargets.Remove(targetId);

        public bool HasRig(string rigId) => rigId == WorldRigId || (rigId != null && rigs.TryGetValue(rigId, out var rig) && rig != null);

        public bool HasLookTarget(string targetId) => targetId != null && lookTargets.TryGetValue(targetId, out var target) && target != null;

        /// <summary>Posse exclusiva da câmera até o descarte (devolvida uma única vez). Um segundo dono simultâneo é erro.</summary>
        public IDisposable AcquireControl(string owner)
        {
            if (controlLease != null)
                throw new InvalidOperationException($"A câmera já é controlada por '{controlLease.Owner}'.");

            controlLease = new ControlLease(this, owner);
            return controlLease;
        }

        void ReleaseControl(ControlLease lease)
        {
            if (controlLease == lease)
                controlLease = null;
        }

        /// <summary>Amostra os keyframes em espaço do rig: posição, rotação e FOV interpolados com a suavização do plano.</summary>
        public static void SampleKeyframes(ShotDefinition shot, float localTimeSeconds, out Vector3 position, out Quaternion rotation, out float fieldOfViewDegrees)
        {
            var frames = shot.Keyframes;
            var index = FindSegment(frames, localTimeSeconds, out var u);
            var a = frames[index];
            var b = frames[Math.Min(index + 1, frames.Count - 1)];
            var eased = Ease(shot.Easing, u);

            position = Vector3.LerpUnclamped(a.positionMeters, b.positionMeters, eased);
            rotation = Quaternion.SlerpUnclamped(Quaternion.Euler(a.eulerAnglesDegrees), Quaternion.Euler(b.eulerAnglesDegrees), eased);
            fieldOfViewDegrees = Mathf.LerpUnclamped(a.fieldOfViewDegrees, b.fieldOfViewDegrees, eased);
        }

        /// <summary>
        /// Pose de mundo do plano no instante local: rig → mundo, continuidade opcional a partir de
        /// <paramref name="continuityStart"/> (só no primeiro segmento), proteção de chão e, por fim, alvo de enquadramento.
        /// </summary>
        public CameraPose Evaluate(ShotDefinition shot, float localTimeSeconds, CameraPose? continuityStart)
        {
            if (!TryGetRigTransform(shot.RigId, out var rig))
                throw new InvalidOperationException($"Rig '{shot.RigId}' do plano '{shot.ShotId}' não está registrado.");

            SampleKeyframes(shot, localTimeSeconds, out var localPosition, out var localRotation, out var fov);
            var position = ToWorld(rig, localPosition);
            var rotation = ToWorld(rig, localRotation);

            var frames = shot.Keyframes;
            var segment = FindSegment(frames, localTimeSeconds, out var u);
            var blend = Ease(shot.Easing, u);
            var inContinuitySegment = continuityStart.HasValue && segment == 0;
            if (inContinuitySegment)
            {
                var end = frames[1];
                var start = continuityStart.Value;
                position = Vector3.LerpUnclamped(start.Position, ToWorld(rig, end.positionMeters), blend);
                rotation = Quaternion.SlerpUnclamped(start.Rotation, ToWorld(rig, Quaternion.Euler(end.eulerAnglesDegrees)), blend);
                fov = Mathf.LerpUnclamped(start.FieldOfViewDegrees, end.fieldOfViewDegrees, blend);
            }

            position = ClampAboveGround(position, shot.MinGroundClearanceMeters);

            if (!string.IsNullOrEmpty(shot.LookAtTargetId) && lookTargets.TryGetValue(shot.LookAtTargetId, out var target) && target != null)
            {
                var direction = target.position - position;
                if (direction.sqrMagnitude > 1e-8f)
                {
                    var look = Quaternion.LookRotation(direction, Vector3.up);
                    rotation = inContinuitySegment ? Quaternion.SlerpUnclamped(continuityStart.Value.Rotation, look, blend) : look;
                }
            }

            return new CameraPose(position, rotation, fov);
        }

        public void Apply(CameraPose pose)
        {
            if (targetCamera == null)
                return;

            targetCamera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            targetCamera.fieldOfView = pose.FieldOfViewDegrees;
        }

        /// <summary>Eleva o ponto para ficar <paramref name="clearanceMeters"/> acima do chão real; sem chão, não altera.</summary>
        public Vector3 ClampAboveGround(Vector3 position, float clearanceMeters)
        {
            var ground = GroundProbe != null ? GroundProbe(position) : PhysicsGround(position);
            if (ground.HasValue && position.y < ground.Value + clearanceMeters)
                position.y = ground.Value + clearanceMeters;

            return position;
        }

        float? PhysicsGround(Vector3 position)
        {
            var origin = new Vector3(position.x, position.y + groundProbeHeightMeters, position.z);
            return Physics.Raycast(origin, Vector3.down, out var hit, groundProbeHeightMeters * 2f, groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : (float?)null;
        }

        /// <summary>
        /// Mantém exatamente um AudioListener ativo: o da câmera (criado se faltar). Os demais são desabilitados.
        /// Chamado ao habilitar e no início de cada sequência, nunca por quadro. Devolve quantos foram desabilitados.
        /// </summary>
        public int EnforceSingleAudioListener()
        {
            if (targetCamera == null)
                return 0;

            var own = targetCamera.GetComponent<AudioListener>();
            if (own == null)
                own = targetCamera.gameObject.AddComponent<AudioListener>();
            own.enabled = true;

            var disabled = 0;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
            {
                if (listener == own || !listener.enabled)
                    continue;

                listener.enabled = false;
                disabled++;
            }

            return disabled;
        }

        bool TryGetRigTransform(string rigId, out Transform rig)
        {
            rig = null;
            if (rigId == WorldRigId)
                return true;

            return rigId != null && rigs.TryGetValue(rigId, out rig) && rig != null;
        }

        static Vector3 ToWorld(Transform rig, Vector3 local) => rig != null ? rig.TransformPoint(local) : local;

        static Quaternion ToWorld(Transform rig, Quaternion local) => rig != null ? rig.rotation * local : local;

        static int FindSegment(IReadOnlyList<ShotKeyframe> frames, float time, out float u)
        {
            if (frames.Count < 2 || time <= frames[0].timeSeconds)
            {
                u = 0f;
                return 0;
            }

            for (var i = 0; i < frames.Count - 1; i++)
            {
                var t0 = frames[i].timeSeconds;
                var t1 = frames[i + 1].timeSeconds;
                if (time <= t1)
                {
                    u = t1 > t0 ? Mathf.Clamp01((time - t0) / (t1 - t0)) : 1f;
                    return i;
                }
            }

            u = 1f;
            return frames.Count - 2;
        }

        static float Ease(ShotEasing easing, float u) =>
            easing == ShotEasing.EaseInOut ? u * u * (3f - 2f * u) : u;

        sealed class ControlLease : IDisposable
        {
            readonly CameraDirector director;

            public ControlLease(CameraDirector director, string owner)
            {
                this.director = director;
                Owner = owner;
            }

            public string Owner { get; }

            public void Dispose() => director.ReleaseControl(this);
        }
    }
}
