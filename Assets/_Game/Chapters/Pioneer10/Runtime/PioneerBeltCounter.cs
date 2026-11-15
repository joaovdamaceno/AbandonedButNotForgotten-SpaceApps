using System;
using System.Globalization;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 2 · O contador de dias do cinturão (só cinema). Lê o relógio da sequência <c>PIO_BeltCinema</c> (congelado na
    /// pausa) e desenha no <see cref="BeltPanelView"/>: a data "15 JUL 1972" no começo; depois o contador "Dia n / 215" que acelera
    /// até o Dia 140 (2 DEZ 1972), para por um instante com o rótulo da pedra solitária (307 Nike) e segue até o Dia 215
    /// (15 FEV 1973). Os dias são datas reais (a travessia começa em 15/7/1972 e termina em 15/2/1973); a curva entre elas é
    /// estilizada, e o texto de cada dia sai de chaves (sem frase literal).
    /// </summary>
    public sealed class PioneerBeltCounter : MonoBehaviour
    {
        /// <summary>O mapa do Sistema Solar com o cinturão e o caminho da sonda: a visão larga (8–19 s), antes do ponto de vista da sonda.</summary>
        public const float MapStartSeconds = 8f;
        public const float MapEndSeconds = 19f;
        public static readonly Rect MapFrame = new Rect(0.27f, 0.12f, 0.46f, 0.64f);
        /// <summary>A sonda sai da órbita de Marte (1,52 UA) e chega ao meio do cinturão (3 UA) no fim do mapa.</summary>
        public const float MapStartAu = 1.52f;
        public const float MapEndAu = 3f;

        public static bool MapVisibleAt(float seconds) => seconds >= MapStartSeconds && seconds < MapEndSeconds;

        /// <summary>Distância da sonda ao Sol (UA) no mapa: parte de Marte e avança suavemente pelo cinturão.</summary>
        public static float MapProbeAuAt(float seconds)
        {
            var u = Mathf.Clamp01((seconds - MapStartSeconds) / (MapEndSeconds - MapStartSeconds));
            return Mathf.Lerp(MapStartAu, MapEndAu, u * u * (3f - 2f * u));
        }

        /// <summary>Estado do mapa da cena 2 no instante dado.</summary>
        public static SystemMapElement.State MapStateAt(float seconds, Func<string, string> resolve) => new SystemMapElement.State
        {
            Mode = SystemMapElement.MapMode.Inner,
            ProbeAu = MapProbeAuAt(seconds),
            PathStartAu = MapStartAu,
            PathEndAu = 4.4f,
            ShowPath = true,
            PulseRing = 2,
            Pulse01 = Mathf.Repeat(seconds, 1.6f) / 1.6f,
            Labels = new[] { resolve("PIO.UI.map_earth"), resolve("PIO.UI.map_mars"), resolve("PIO.UI.map_belt"), resolve("PIO.UI.orbit_jupiter") },
            Title = resolve("PIO.UI.map_title_belt"),
            Footnote = resolve("PIO.UI.map_note"),
            ProbeLabel = resolve("PIO.UI.map_probe"),
        };

        /// <summary>Só a data, no começo (mostra "15 JUL 1972").</summary>
        public const float DateOnlyEndSeconds = 6f;
        /// <summary>O contador começa a acelerar (início do plano do dolly zoom).</summary>
        public const float DaysStartSeconds = 19f;
        /// <summary>Chega ao Dia 140 (2 DEZ 1972).</summary>
        public const float DaysRampEndSeconds = 30f;
        /// <summary>Fim da pausa em 140 e da revelação de 307 Nike; o contador segue até o Dia 215.</summary>
        public const float HoldEndSeconds = 40f;
        public const float ExitEndSeconds = 47f;
        public const int StopDay = 140;
        public const int FinalDay = 215;
        /// <summary>Quanto o contador acelera: a taxa final é <c>e^k</c> vezes a inicial (k = 5: de ~0,4 a ~63 dias por segundo).</summary>
        public const float Acceleration = 5f;

        /// <summary>Primeiro dia da travessia: 15 de julho de 1972.</summary>
        public static readonly DateTime StartDate = new DateTime(1972, 7, 15);

        [SerializeField] CinemaHudView hud;
        [SerializeField, Tooltip("Sequência que este contador acompanha (PIO_BeltCinema).")] SequenceDefinition sequence;
        [SerializeField, Tooltip("Onde a pedra solitária aparece no quadro do anchor PIO.Belt.Nike (0–1, origem em cima à esquerda).")]
        Vector2 nikeViewport = new Vector2(0.685f, 0.37f);

        SequenceDirector director;
        Func<string, string> text;

        public Vector2 NikeViewport => nikeViewport;

        public void Initialize(SequenceDirector sequenceDirector, Func<string, string> textResolver)
        {
            director = sequenceDirector;
            text = textResolver;
            Hide();
        }

        /// <summary>O dia do contador no instante <paramref name="seconds"/> da sequência (0 antes de acelerar).</summary>
        public static int DayAt(float seconds)
        {
            if (seconds <= DaysStartSeconds)
                return 0;
            if (seconds < DaysRampEndSeconds)
            {
                var u = (seconds - DaysStartSeconds) / (DaysRampEndSeconds - DaysStartSeconds);
                return Mathf.FloorToInt(StopDay * (Mathf.Exp(Acceleration * u) - 1f) / (Mathf.Exp(Acceleration) - 1f));
            }

            if (seconds < HoldEndSeconds)
                return StopDay;
            if (seconds < ExitEndSeconds)
            {
                var u = (seconds - HoldEndSeconds) / (ExitEndSeconds - HoldEndSeconds);
                return StopDay + Mathf.FloorToInt((FinalDay - StopDay) * (u * u * (3f - 2f * u)));
            }

            return FinalDay;
        }

        /// <summary>A data do <paramref name="day"/>-ésimo dia ("2 DEZ 1972"); <paramref name="months"/> são os doze meses abreviados separados por espaço.</summary>
        public static string DateText(int day, string months)
        {
            var date = StartDate.AddDays(day);
            var names = (months ?? string.Empty).Split(' ');
            var month = names.Length >= 12 ? names[date.Month - 1] : date.Month.ToString(CultureInfo.InvariantCulture);
            return date.Day.ToString(CultureInfo.InvariantCulture) + " " + month + " " + date.Year.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A nota "a pedra mais próxima ficava a milhões de quilômetros", nos últimos segundos do zoom na sonda.</summary>
        public const string NoteKey = "PIO.UI.belt_far";
        public const float NoteStartSeconds = 25f;

        public static bool NoteVisibleAt(float seconds) => seconds >= NoteStartSeconds && seconds < DaysRampEndSeconds;

        public static bool DayCounterVisibleAt(float seconds) => seconds >= DaysStartSeconds;

        public static bool DateOnlyVisibleAt(float seconds) => seconds < DateOnlyEndSeconds;

        /// <summary>A pedra solitária (e seu rótulo) aparecem na pausa em 2 DEZ 1972.</summary>
        public static bool NikeVisibleAt(float seconds) => seconds >= DaysRampEndSeconds && seconds < HoldEndSeconds;

        void Update() => Tick();

        void OnDisable() => Hide();

        /// <summary>Atualiza o painel pelo relógio da sequência. Testes chamam direto.</summary>
        public void Tick()
        {
            if (hud == null || director == null)
                return;

            if (!director.IsPlaying || director.Current != sequence)
            {
                Hide();
                return;
            }

            var t = director.TimeSeconds;
            var counter = DayCounterVisibleAt(t);
            var dateOnly = DateOnlyVisibleAt(t);
            var day = DayAt(t);
            var months = Resolve(BeltPanelView.MonthsKey);
            var dayText = counter ? Resolve(BeltPanelView.DayKey).Replace("{count}", day.ToString(CultureInfo.InvariantCulture)) : string.Empty;
            var nike = NikeVisibleAt(t);
            var note = NoteVisibleAt(t);
            hud.SetBeltCounter(counter || dateOnly, dayText, DateText(day, months), nike, nike ? Resolve(BeltPanelView.NearestKey) : string.Empty, nikeViewport,
                note ? Resolve(NoteKey) : string.Empty);
        }

        public void Hide()
        {
            if (hud != null)
                hud.SetBeltCounter(false, string.Empty, string.Empty, false, string.Empty, nikeViewport, string.Empty);
        }

        string Resolve(string key)
        {
            var value = text != null ? text(key) : null;
            return string.IsNullOrEmpty(value) ? key : value;
        }
    }
}
