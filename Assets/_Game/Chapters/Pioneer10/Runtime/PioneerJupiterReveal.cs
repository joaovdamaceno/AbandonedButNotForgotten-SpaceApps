using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 3 · "O rei dos planetas" (só cinema). Dois efeitos presos ao relógio da sequência <c>PIO_JupiterApproach</c>:
    /// (1) nos primeiros 9 s Júpiter cresce de um pontinho de luz até o tamanho real do palco (a câmera continua perto da sonda, que
    /// fica em silhueta no primeiro plano; o crescimento é o do próprio planeta, em ritmo exponencial para o olho ver uma taxa
    /// constante); (2) a partir de 9,5 s uma Terra pequena (1/11,2 do diâmetro de Júpiter, a razão real dos diâmetros equatoriais)
    /// entra pela esquerda e para ao lado, para a comparação de tamanho. Fora da sequência tudo volta ao normal.
    /// </summary>
    public sealed class PioneerJupiterReveal : MonoBehaviour
    {
        /// <summary>Duração do crescimento de Júpiter (o plano PIO03.Grow).</summary>
        public const float GrowSeconds = 9f;
        /// <summary>Tamanho inicial de Júpiter em fração do tamanho final (um ponto de ~4 px a 70°).</summary>
        public const float MinScale = 0.015f;
        public const float EarthEnterSeconds = 9.5f;
        public const float EarthArriveSeconds = 14f;
        /// <summary>Fim do plano aberto (PIO03.Open); no corte seguinte a Terra sai.</summary>
        public const float EarthLeaveSeconds = 22f;
        /// <summary>Razão real dos diâmetros equatoriais de Júpiter e da Terra (142.984 km / 12.756 km).</summary>
        public const float JupiterToEarthDiameter = 11.2f;

        [SerializeField] PioneerPresentation presentation;
        [SerializeField, Tooltip("Sequência que este efeito acompanha (PIO_JupiterApproach).")] SequenceDefinition sequence;
        [SerializeField, Tooltip("Terra de comparação (filha do espaço profundo); fica desligada fora do plano aberto.")] Transform earth;
        [SerializeField, Tooltip("Onde a Terra entra, fora do quadro (posição local do espaço profundo).")] Vector3 earthFrom;
        [SerializeField, Tooltip("Onde a Terra para, ao lado de Júpiter (posição local do espaço profundo).")] Vector3 earthTo;

        SequenceDirector director;
        float baseScale;
        bool captured;

        public Transform Earth => earth;

        public void Initialize(SequenceDirector sequenceDirector)
        {
            director = sequenceDirector;
            Capture();
            Hide();
        }

        /// <summary>Tamanho de Júpiter em fração do final no instante <paramref name="seconds"/>: ponto, crescimento exponencial, tamanho cheio.</summary>
        public static float JupiterScaleAt(float seconds)
        {
            if (seconds >= GrowSeconds)
                return 1f;
            var u = Mathf.Clamp01(seconds / GrowSeconds);
            return Mathf.Pow(MinScale, 1f - u);
        }

        /// <summary>Quanto a Terra já andou do ponto de entrada até o repouso (0 a 1, suave).</summary>
        public static float EarthBlendAt(float seconds)
        {
            var u = Mathf.Clamp01((seconds - EarthEnterSeconds) / (EarthArriveSeconds - EarthEnterSeconds));
            return u * u * (3f - 2f * u);
        }

        public static bool EarthVisibleAt(float seconds) => seconds >= EarthEnterSeconds && seconds < EarthLeaveSeconds;

        void Update() => Tick();

        void OnDisable() => Hide();

        /// <summary>Aplica o instante da sequência ao palco. Testes chamam direto.</summary>
        public void Tick()
        {
            if (presentation == null || director == null)
                return;

            if (!director.IsPlaying || director.Current != sequence)
            {
                Hide();
                return;
            }

            Capture();
            var t = director.TimeSeconds;
            Apply(t);
        }

        /// <summary>Põe Júpiter e a Terra como no instante <paramref name="seconds"/> (sem ler a sequência).</summary>
        public void Apply(float seconds)
        {
            Capture();
            if (presentation.Jupiter != null)
                presentation.Jupiter.localScale = Vector3.one * (baseScale * JupiterScaleAt(seconds));
            if (earth != null)
            {
                var visible = EarthVisibleAt(seconds);
                if (earth.gameObject.activeSelf != visible)
                    earth.gameObject.SetActive(visible);
                if (visible)
                    earth.localPosition = Vector3.Lerp(earthFrom, earthTo, EarthBlendAt(seconds));
            }
        }

        public void Hide()
        {
            if (presentation == null)
                return;
            if (captured && presentation.Jupiter != null && presentation.Jupiter.localScale != Vector3.one * baseScale)
                presentation.Jupiter.localScale = Vector3.one * baseScale;
            if (earth != null && earth.gameObject.activeSelf)
                earth.gameObject.SetActive(false);
        }

        void Capture()
        {
            if (captured || presentation == null || presentation.Jupiter == null)
                return;
            baseScale = presentation.Jupiter.localScale.x;
            captured = true;
        }
    }
}
