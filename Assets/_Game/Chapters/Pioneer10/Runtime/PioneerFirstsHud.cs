using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 9 · Rumo a Aldebaran (as seis primeiras vezes). Lê o relógio da sequência <c>PIO_Farewell</c> (38 s) e, no plano
    /// em que a sonda se afasta (20–30 s), acende ao redor dela seis pequenas estrelas, uma a cada 1,6 s, cada uma com seu rótulo curto.
    /// Os seis "primeiros" são os que a NASA credita à missão (página dos 50 anos do lançamento, lida em 15/11/2026) e os do registro de
    /// fontes do projeto; "energia totalmente nuclear" do roteiro NÃO entra, porque a página só diz que a sonda usava RTGs.
    /// </summary>
    public sealed class PioneerFirstsHud : MonoBehaviour
    {
        public const int StarCount = 6;
        public const float StartSeconds = 20f;
        public const float StepSeconds = 1.6f;
        /// <summary>Quanto dura o acender de cada estrela (o brilho cresce do ponto ao tamanho cheio).</summary>
        public const float LightSeconds = 0.5f;
        /// <summary>As estrelas somem quando entra o memorial (plano PIO08.Memorial, 30 s).</summary>
        public const float EndSeconds = 30f;
        /// <summary>Centro e raios (fração do quadro) da elipse onde as estrelas ficam, ao redor da sonda.</summary>
        public static readonly Vector2 EllipseCenter = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 EllipseRadius = new Vector2(0.3f, 0.24f);

        public static readonly string[] LabelKeys =
        {
            "PIO.UI.first_1", "PIO.UI.first_2", "PIO.UI.first_3", "PIO.UI.first_4", "PIO.UI.first_5", "PIO.UI.first_6",
        };

        [SerializeField] CinemaHudView hud;
        [SerializeField, Tooltip("Sequência que este HUD acompanha (PIO_Farewell).")] SequenceDefinition sequence;

        SequenceDirector director;
        Func<string, string> text;
        readonly float[] progress = new float[StarCount];
        readonly string[] labels = new string[StarCount];

        public void Initialize(SequenceDirector sequenceDirector, Func<string, string> textResolver)
        {
            director = sequenceDirector;
            text = textResolver;
            Hide();
        }

        public static bool VisibleAt(float seconds) => seconds >= StartSeconds && seconds < EndSeconds;

        /// <summary>Instante em que a estrela <paramref name="index"/> (0–5) começa a acender.</summary>
        public static float LightStartAt(int index) => StartSeconds + index * StepSeconds;

        /// <summary>Quanto a estrela já acendeu (0 apagada, 1 cheia), em ordem cronológica.</summary>
        public static float ProgressAt(int index, float seconds)
        {
            var u = Mathf.Clamp01((seconds - LightStartAt(index)) / LightSeconds);
            return u * u * (3f - 2f * u);
        }

        /// <summary>Posição da estrela no quadro (0–1, origem em cima à esquerda): a primeira no alto à esquerda e as demais em sentido horário.</summary>
        public static Vector2 ViewportOf(int index)
        {
            var angle = (-150f + 60f * index) * Mathf.Deg2Rad;
            return new Vector2(EllipseCenter.x + EllipseRadius.x * Mathf.Cos(angle), EllipseCenter.y + EllipseRadius.y * Mathf.Sin(angle));
        }

        void Update() => Tick();

        void OnDisable() => Hide();

        /// <summary>Atualiza o HUD pelo relógio da sequência. Testes chamam direto.</summary>
        public void Tick()
        {
            if (hud == null || director == null)
                return;

            if (!director.IsPlaying || director.Current != sequence)
            {
                Hide();
                return;
            }

            Apply(director.TimeSeconds);
        }

        /// <summary>Desenha as estrelas como no instante <paramref name="seconds"/> (sem ler a sequência).</summary>
        public void Apply(float seconds)
        {
            if (hud == null)
                return;

            var visible = VisibleAt(seconds);
            for (var i = 0; i < StarCount; i++)
            {
                progress[i] = visible ? ProgressAt(i, seconds) : 0f;
                labels[i] = visible && progress[i] > 0f ? Resolve(LabelKeys[i]) : string.Empty;
            }

            hud.SetFirsts(visible, labels, progress);
        }

        public void Hide()
        {
            if (hud != null)
                hud.SetFirsts(false, null, null);
        }

        string Resolve(string key)
        {
            var value = text != null ? text(key) : null;
            return string.IsNullOrEmpty(value) ? key : value;
        }
    }
}
