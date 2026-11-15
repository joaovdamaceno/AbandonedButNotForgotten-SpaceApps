using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 5d · o estilingue gravitacional em tempo acelerado. No plano alto <c>PIO09.Bend</c> a sonda (ampliada para dar para
    /// vê-la a centenas de metros) percorre de verdade a trajetória desenhada, perto de Júpiter e para fora, ganhando velocidade ao se
    /// aproximar do planeta e saindo mais rápida do que chegou. A câmera acompanha a sonda (alvo <c>PIO.Probe</c> no plano). Fora deste plano
    /// a sonda volta à origem e ao tamanho normal. É composição, não cálculo orbital: a curva é a mesma linha do <c>PioneerStageBuilder</c>.
    /// </summary>
    public sealed class PioneerSlingshot : MonoBehaviour
    {
        public const string ShotId = "PIO09.Bend";
        /// <summary>A sonda real tem 2,74 m de refletor; a 700 m de altura, 22 vezes (cerca de 60 m) ainda aparece com uns 3 graus.</summary>
        public const float ProbeScaleFactor = 22f;
        /// <summary>O trecho mostrado, em fração do comprimento da linha: começa antes da curva e termina bem depois dela.</summary>
        public const float StartFraction = 0.30f;
        public const float EndFraction = 0.93f;
        /// <summary>Expoente da aceleração: maior que 1 faz a sonda andar devagar na chegada e rápido na saída.</summary>
        public const float AccelerationExponent = 1.8f;

        [SerializeField] PioneerPresentation presentation;
        [SerializeField, Tooltip("Sequência que este efeito acompanha (PIO_Closest).")] SequenceDefinition sequence;
        [SerializeField, Tooltip("Pai do giro da sonda (Probe): é ele que anda, para não brigar com a rotação da antena.")] Transform probe;
        [SerializeField, Tooltip("Pontos da trajetória no espaço profundo (a mesma linha dourada do plano).")] Vector3[] path = new Vector3[0];

        SequenceDirector director;
        Vector3 basePosition, baseScale;
        bool captured, moved;
        float[] lengths;

        public bool IsMoved => moved;

        public void Initialize(SequenceDirector sequenceDirector)
        {
            director = sequenceDirector;
            Capture();
            BuildLengths();
        }

        /// <summary>0–1 do tempo do plano para 0–1 do percurso: lento no começo, rápido no fim.</summary>
        public static float Progress(float u) => Mathf.Pow(Mathf.Clamp01(u), AccelerationExponent);

        /// <summary>Velocidade relativa no instante <paramref name="u"/> (derivada do avanço), para testes e legendas.</summary>
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

        /// <summary>Posição da sonda no instante <paramref name="u"/> (0–1) do plano.</summary>
        public Vector3 PositionAt(float u) => PositionAtFraction(Mathf.Lerp(StartFraction, EndFraction, Progress(u)));

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
            if (presentation == null || director == null || probe == null || sequence == null)
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
        }

        /// <summary>Põe a sonda como no instante <paramref name="u"/> (0–1) do plano.</summary>
        public void Apply(float u)
        {
            Capture();
            probe.localPosition = PositionAt(u);
            probe.localScale = baseScale * ProbeScaleFactor;
            moved = true;
        }

        public void Hide()
        {
            if (!moved || probe == null)
                return;

            probe.localPosition = basePosition;
            probe.localScale = baseScale;
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
            if (captured || probe == null)
                return;

            basePosition = probe.localPosition;
            baseScale = probe.localScale;
            captured = true;
        }
    }
}
