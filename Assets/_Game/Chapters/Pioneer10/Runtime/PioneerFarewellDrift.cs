using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Cena 9 · o final: a Pioneer segue devagar, sem parar, na direção de Aldebaran. Depois que o plano da despedida começa a se afastar
    /// (<see cref="StartSeconds"/>), a sonda anda em linha reta para a estrela a <see cref="MetersPerSecond"/> m/s, com tempo ativo (zero
    /// na pausa), e continua enquanto a cena estiver aberta, inclusive com as escolhas finais na tela: a câmera fica parada e a sonda vai
    /// encolhendo até um ponto, sem nunca chegar. A estrela é só uma marca distante do palco; nada é medido. Ao sair, a sonda volta à origem.
    /// </summary>
    public sealed class PioneerFarewellDrift : MonoBehaviour
    {
        /// <summary>Instante (s desde o começo da cena) em que a sonda começa a andar: o plano em que ela se afasta.</summary>
        public const float StartSeconds = 20f;
        /// <summary>Velocidade lenta e constante, em metros do palco por segundo.</summary>
        public const float MetersPerSecond = 0.9f;
        /// <summary>Segundos até chegar à velocidade cheia (a sonda arranca devagar).</summary>
        public const float RampSeconds = 6f;

        [SerializeField] PioneerPresentation presentation;
        [SerializeField, Tooltip("Pai do giro da sonda (Probe): é ele que anda.")] Transform probe;

        IActiveTimeSource time;
        Vector3 basePosition;
        float elapsed;
        bool running, captured;

        public bool IsRunning => running;
        public float ElapsedSeconds => elapsed;

        public void Initialize(IActiveTimeSource activeTime)
        {
            time = activeTime;
            Capture();
        }

        /// <summary>Distância percorrida (m) depois de <paramref name="seconds"/> de cena: parada até 20 s, depois acelera suave até a velocidade cheia.</summary>
        public static float DistanceAt(float seconds)
        {
            var t = Mathf.Max(0f, seconds - StartSeconds);
            if (t <= RampSeconds)
                return MetersPerSecond * t * t / (2f * RampSeconds);
            return MetersPerSecond * (RampSeconds * 0.5f + (t - RampSeconds));
        }

        /// <summary>Começa o final (chamado ao entrar no estado da despedida).</summary>
        public void Begin()
        {
            Capture();
            elapsed = 0f;
            running = true;
        }

        void Update()
        {
            if (!running || time == null)
                return;

            Advance(time.ActiveDeltaSeconds);
        }

        /// <summary>Avança com tempo ativo e põe a sonda no ponto correspondente. Testes chamam direto.</summary>
        public void Advance(float activeDeltaSeconds)
        {
            if (!running || probe == null || activeDeltaSeconds <= 0f)
                return;

            elapsed += activeDeltaSeconds;
            Place();
        }

        void Place()
        {
            var toStar = Vector3.forward;
            if (presentation != null && presentation.AldebaranMarker != null)
            {
                var direction = presentation.AldebaranMarker.position - basePosition;
                if (direction.sqrMagnitude > 1e-4f)
                    toStar = direction.normalized;
            }

            probe.position = basePosition + toStar * DistanceAt(elapsed);
        }

        /// <summary>Para e devolve a sonda à origem (saída, reinício ou descarte).</summary>
        public void Hide()
        {
            running = false;
            elapsed = 0f;
            if (captured && probe != null)
                probe.position = basePosition;
        }

        void OnDisable() => Hide();

        void Capture()
        {
            if (captured || probe == null)
                return;

            basePosition = probe.position;
            captured = true;
        }
    }
}
