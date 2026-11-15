using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 5d · o estilingue gravitacional em primeira pessoa e em tempo acelerado. No plano <c>PIO09.Bend</c> a câmera é a da
    /// sonda: sai de antes da curva, passa rente a Júpiter e segue para fora, devagar na chegada e cada vez mais depressa (o tempo é
    /// acelerado), com o campo de visão abrindo com a velocidade. Move a pose do anchor <c>PIO.Closest.Bend</c> ao longo da trajetória
    /// desenhada (a mesma linha dourada do plano) e a faz olhar para a frente com uma inclinação para Júpiter, que fica sempre à vista na
    /// curva. É composição, não cálculo orbital. Fora do plano a pose volta ao lugar de origem.
    /// </summary>
    public sealed class PioneerSlingshot : MonoBehaviour
    {
        public const string ShotId = "PIO09.Bend";
        public const string AnchorId = "PIO.Closest.Bend";
        /// <summary>O trecho mostrado, em fração do comprimento da linha: começa antes da curva e termina bem depois dela.</summary>
        public const float StartFraction = 0.28f;
        public const float EndFraction = 0.93f;
        /// <summary>Expoente da aceleração: maior que 1 faz a sonda andar devagar na chegada e rápido na saída.</summary>
        public const float AccelerationExponent = 1.8f;
        /// <summary>Quanto o olhar se inclina para Júpiter (0 = só para a frente, 1 = só para o planeta): começa em 0,3 e, depois da passagem, vai se voltando para ele, que fica encolhendo atrás.</summary>
        public const float JupiterLookBlend = 0.3f;
        public const float JupiterLookBlendEnd = 0.9f;
        /// <summary>Distância (m) à frente na linha para calcular a direção do movimento.</summary>
        public const float LookAheadMeters = 40f;

        [SerializeField] PioneerPresentation presentation;
        [SerializeField, Tooltip("Sequência que este efeito acompanha (PIO_Closest).")] SequenceDefinition sequence;
        [SerializeField, Tooltip("Pontos da trajetória no espaço profundo (a mesma linha dourada do plano).")] Vector3[] path = new Vector3[0];
        [SerializeField, Tooltip("Centro de Júpiter no espaço profundo, para o olhar acompanhar o planeta na curva.")] Vector3 jupiterCenter;

        SequenceDirector director;
        Transform pose;
        Vector3 basePosition;
        Quaternion baseRotation;
        bool captured, moved;
        float[] lengths;

        public bool IsMoved => moved;

        public void Initialize(SequenceDirector sequenceDirector)
        {
            director = sequenceDirector;
            BuildLengths();
            Capture();
        }

        /// <summary>0–1 do tempo do plano para 0–1 do percurso: lento no começo, rápido no fim.</summary>
        public static float Progress(float u) => Mathf.Pow(Mathf.Clamp01(u), AccelerationExponent);

        /// <summary>Velocidade relativa no instante <paramref name="u"/> (derivada do avanço).</summary>
        public static float RelativeSpeed(float u) => AccelerationExponent * Mathf.Pow(Mathf.Clamp01(u), AccelerationExponent - 1f);

        /// <summary>Posição ao longo da linha na fração <paramref name="fraction"/> (0–1) do comprimento.</summary>
        public Vector3 PositionAtFraction(float fraction)
        {
            if (path == null || path.Length == 0)
                return basePosition;
            if (path.Length == 1)
                return path[0];

            BuildLengths();
            var target = Mathf.Clamp01(fraction) * lengths[lengths.Length - 1];
            var index = 1;
            while (index < lengths.Length - 1 && lengths[index] < target)
                index++;
            var span = Mathf.Max(1e-4f, lengths[index] - lengths[index - 1]);
            return Vector3.Lerp(path[index - 1], path[index], Mathf.Clamp01((target - lengths[index - 1]) / span));
        }

        float TotalLength
        {
            get
            {
                BuildLengths();
                return lengths != null && lengths.Length > 0 ? lengths[lengths.Length - 1] : 0f;
            }
        }

        /// <summary>Fração do comprimento da linha no instante <paramref name="u"/> (0–1) do plano.</summary>
        public static float FractionAt(float u) => Mathf.Lerp(StartFraction, EndFraction, Progress(u));

        /// <summary>Posição da câmera no instante <paramref name="u"/> (0–1) do plano.</summary>
        public Vector3 PositionAt(float u) => PositionAtFraction(FractionAt(u));

        /// <summary>Direção do olhar: para a frente na linha, inclinada para Júpiter.</summary>
        public Vector3 ForwardAt(float u)
        {
            var fraction = FractionAt(u);
            var position = PositionAtFraction(fraction);
            var total = Mathf.Max(1f, TotalLength);
            var ahead = PositionAtFraction(Mathf.Min(1f, fraction + LookAheadMeters / total));
            var tangent = (ahead - position).normalized;
            if (tangent.sqrMagnitude < 1e-6f)
                tangent = Vector3.forward;
            var toJupiter = (jupiterCenter - position).normalized;
            var turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, Mathf.Clamp01(u)));
            var look = Vector3.Slerp(tangent, toJupiter, Mathf.Lerp(JupiterLookBlend, JupiterLookBlendEnd, turn));
            return look.sqrMagnitude < 1e-6f ? tangent : look.normalized;
        }

        void BuildLengths()
        {
            if (path == null || path.Length == 0 || (lengths != null && lengths.Length == path.Length))
                return;

            lengths = new float[path.Length];
            for (var i = 1; i < path.Length; i++)
                lengths[i] = lengths[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        }

        void Update() => Tick();

        void OnDisable() => Hide();

        /// <summary>Aplica o instante da sequência. Testes chamam direto.</summary>
        public void Tick()
        {
            if (presentation == null || director == null || sequence == null)
                return;

            if (!director.IsPlaying || director.Current != sequence)
            {
                Hide();
                return;
            }

            var start = ShotStart();
            var duration = ShotDuration();
            if (start < 0f || duration <= 0f)
            {
                Hide();
                return;
            }

            var local = director.TimeSeconds - start;
            if (local < 0f || local > duration)
            {
                Hide();
                return;
            }

            Apply(local / duration);
            director.RefreshCamera();
        }

        /// <summary>Põe a câmera da sonda como no instante <paramref name="u"/> (0–1) do plano.</summary>
        public void Apply(float u)
        {
            Capture();
            if (pose == null)
                return;

            pose.position = PositionAt(u);
            pose.rotation = Quaternion.LookRotation(ForwardAt(u), Vector3.up);
            moved = true;
        }

        public void Hide()
        {
            if (!moved || pose == null)
                return;

            pose.position = basePosition;
            pose.rotation = baseRotation;
            moved = false;
        }

        float ShotStart()
        {
            var time = 0f;
            foreach (var shot in sequence.Shots)
            {
                if (shot.ShotId == ShotId)
                    return time;
                time += shot.DurationSeconds;
            }

            return -1f;
        }

        float ShotDuration()
        {
            foreach (var shot in sequence.Shots)
            {
                if (shot.ShotId == ShotId)
                    return shot.DurationSeconds;
            }

            return 0f;
        }

        void Capture()
        {
            if (captured || presentation == null)
                return;

            if (!presentation.TryGetAnchor(AnchorId, out var anchor) || anchor.pose == null)
                return;

            pose = anchor.pose;
            basePosition = pose.position;
            baseRotation = pose.rotation;
            captured = true;
        }
    }
}
