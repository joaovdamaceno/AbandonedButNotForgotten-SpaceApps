using System;
using System.Globalization;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 7 · Os anos (só cinema). Lê o relógio da sequência <c>PIO_Outbound</c> (55 s) e desenha no
    /// <see cref="CinemaHudView"/>: o ano em destaque correndo de 1974 a 1998, com pausa nos marcos reais (distância orbital de Saturno
    /// em 1976, de Urano em 1979, de Netuno em 13 de junho de 1983, o fim do acompanhamento de rotina em 31 de março de 1997 e a
    /// Voyager 1 em 1998); a linha do tempo; os ícones dos instrumentos, que apagam em turnos; e o medidor de energia dos RTG, com as
    /// duas marcas reais (155 W no lançamento e ~140 W em Júpiter). A curva depois de Júpiter é ESTILIZADA e o painel diz isso.
    /// </summary>
    public sealed class PioneerYearsHud : MonoBehaviour
    {
        public enum YearsEvent { None, Saturn, Uranus, Neptune, TrackingEnd, Voyager }

        public const int YearMin = 1973;
        public const int YearMax = 1998;
        /// <summary>13 de junho de 1983 como fração do ano (dia 164 de 365).</summary>
        public const float NeptuneYear = 1983f + 164f / 365f;
        /// <summary>31 de março de 1997 (dia 90 de 365).</summary>
        public const float TrackingEndYear = 1997f + 90f / 365f;
        /// <summary>17 de fevereiro de 1998 (dia 48 de 365).</summary>
        public const float VoyagerYear = 1998f + 48f / 365f;
        public const float SaturnYear = 1976f;
        public const float UranusYear = 1979f;
        /// <summary>Marcas reais do medidor: 155 W no lançamento (início de 1972) e ~140 W em Júpiter (fim de 1973).</summary>
        public const float LaunchWatts = 155f;
        public const float JupiterWatts = 140f;
        public const float LaunchYear = 1972.17f;
        public const float JupiterYear = 1973.95f;

        /// <summary>Instantes da sequência e o ano correspondente: o relógio fica parado nos marcos e anda entre eles.</summary>
        static readonly (float seconds, float year)[] Schedule =
        {
            (0f, 1974f), (3f, SaturnYear), (7f, SaturnYear), (9f, UranusYear), (13f, UranusYear), (16f, NeptuneYear), (24f, NeptuneYear),
            (38f, TrackingEndYear), (40f, TrackingEndYear), (42f, VoyagerYear), (55f, VoyagerYear),
        };

        /// <summary>Anos com marco real, desenhados mais altos e com o número na linha do tempo.</summary>
        public static readonly int[] EventYears = { 1976, 1979, 1983, 1997, 1998 };

        /// <summary>Nível da energia (0–1, relativo ao lançamento) abaixo do qual cada instrumento apaga: câmera, radiação, plasma, meteoroides, campo magnético.</summary>
        public static readonly float[] InstrumentOffBelow = { 0.5f, 0.42f, 0.36f, 0.7f, 0f };
        public const int InstrumentCount = 5;

        [SerializeField] CinemaHudView hud;
        [SerializeField, Tooltip("Sequência que este HUD acompanha (PIO_Outbound).")] SequenceDefinition sequence;

        SequenceDirector director;
        Func<string, string> text;
        bool mapShown;

        public void Initialize(SequenceDirector sequenceDirector, Func<string, string> textResolver)
        {
            director = sequenceDirector;
            text = textResolver;
            Hide();
        }

        /// <summary>Onde o mapa fica no quadro (entre o medidor de instrumentos e o de energia, abaixo do ano).</summary>
        public static readonly Rect MapFrame = new Rect(0.30f, 0.21f, 0.40f, 0.52f);
        /// <summary>A sonda se afasta do Sol a cerca de 2,6 UA por ano depois de Júpiter (5,2 UA no fim de 1973): bate com Saturno em 1976, Urano em 1979 e Netuno em 1983.</summary>
        public const float AuPerYear = 2.6f;
        public const float JupiterAu = 5.2f;

        /// <summary>Distância da sonda ao Sol (UA) no ano dado; estilizada e coerente com os marcos reais do roteiro.</summary>
        public static float ProbeAuAt(float year) => Mathf.Max(JupiterAu, JupiterAu + AuPerYear * (year - JupiterYear));

        /// <summary>O mapa da cena 7: a sonda cruza as órbitas de Saturno (1976), Urano (1979) e Netuno (13 jun 1983), e a órbita cruzada pulsa.</summary>
        public static SystemMapElement.State MapStateAt(float seconds, Func<string, string> resolve)
        {
            var year = YearAt(seconds);
            var ev = EventAt(year);
            return new SystemMapElement.State
            {
                Mode = SystemMapElement.MapMode.Outer,
                ProbeAu = ProbeAuAt(year),
                PathStartAu = JupiterAu,
                PathEndAu = SystemMapElement.OuterMaxAu * 0.96f,
                ShowPath = true,
                PulseRing = ev switch { YearsEvent.Saturn => 1, YearsEvent.Uranus => 2, YearsEvent.Neptune => 3, _ => -1 },
                Pulse01 = Mathf.Repeat(seconds, 1.4f) / 1.4f,
                Labels = new[] { resolve("PIO.UI.orbit_jupiter"), resolve("PIO.UI.orbit_saturn"), resolve("PIO.UI.orbit_uranus"), resolve("PIO.UI.orbit_neptune") },
                Title = resolve("PIO.UI.map_title_out"),
                Footnote = resolve("PIO.UI.map_note"),
                ProbeLabel = resolve("PIO.UI.map_probe"),
            };
        }

        /// <summary>O ano (com fração) mostrado no instante <paramref name="seconds"/> da sequência.</summary>
        public static float YearAt(float seconds)
        {
            if (seconds <= Schedule[0].seconds)
                return Schedule[0].year;
            for (var i = 1; i < Schedule.Length; i++)
            {
                if (seconds > Schedule[i].seconds)
                    continue;
                var a = Schedule[i - 1];
                var b = Schedule[i];
                var u = Mathf.Clamp01((seconds - a.seconds) / (b.seconds - a.seconds));
                return Mathf.Lerp(a.year, b.year, u * u * (3f - 2f * u));
            }

            return Schedule[Schedule.Length - 1].year;
        }

        /// <summary>O marco real em que o relógio está parado (ou quase) neste ano.</summary>
        public static YearsEvent EventAt(float year)
        {
            if (Mathf.Abs(year - SaturnYear) < 0.02f)
                return YearsEvent.Saturn;
            if (Mathf.Abs(year - UranusYear) < 0.02f)
                return YearsEvent.Uranus;
            if (Mathf.Abs(year - NeptuneYear) < 0.02f)
                return YearsEvent.Neptune;
            if (Mathf.Abs(year - TrackingEndYear) < 0.02f)
                return YearsEvent.TrackingEnd;
            if (Mathf.Abs(year - VoyagerYear) < 0.02f)
                return YearsEvent.Voyager;
            return YearsEvent.None;
        }

        /// <summary>Posição do ano na linha do tempo (0 em 1973, 1 em 1998).</summary>
        public static float YearFraction(float year) => Mathf.Clamp01((year - YearMin) / (YearMax - YearMin));

        /// <summary>
        /// Energia relativa ao lançamento (0–1): 1,0 no início de 1972, 140/155 em Júpiter (marcas reais, entre elas uma reta) e, depois,
        /// uma queda exponencial ESTILIZADA (nenhum valor real além dessas duas marcas) que não passa de 0,2.
        /// </summary>
        public static float EnergyFillAt(float year)
        {
            var jupiter = JupiterWatts / LaunchWatts;
            if (year <= LaunchYear)
                return 1f;
            if (year <= JupiterYear)
                return Mathf.Lerp(1f, jupiter, (year - LaunchYear) / (JupiterYear - LaunchYear));
            return Mathf.Max(0.2f, jupiter * Mathf.Exp(-(year - JupiterYear) / 22f));
        }

        public static bool InstrumentOn(float energyFill, int index) => energyFill > InstrumentOffBelow[index];

        /// <summary>Estado do HUD no instante <paramref name="seconds"/>, com os textos resolvidos por <paramref name="resolve"/>.</summary>
        public static CinemaHudView.YearsState StateAt(float seconds, Func<string, string> resolve)
        {
            var year = YearAt(seconds);
            var ev = EventAt(year);
            var fill = EnergyFillAt(year);
            var state = new CinemaHudView.YearsState
            {
                Big = ev == YearsEvent.Neptune ? resolve(CinemaHudView.Card1983Key) : Mathf.FloorToInt(year).ToString(CultureInfo.InvariantCulture),
                Event = ev switch
                {
                    YearsEvent.Saturn => resolve(CinemaHudView.PastSaturnKey),
                    YearsEvent.Uranus => resolve(CinemaHudView.PastUranusKey),
                    YearsEvent.Neptune => resolve(CinemaHudView.PastNeptuneKey),
                    YearsEvent.TrackingEnd => resolve(CinemaHudView.EndTrackingKey),
                    YearsEvent.Voyager => resolve(CinemaHudView.Cross1998Key),
                    _ => string.Empty,
                },
                Note = ev == YearsEvent.Neptune ? resolve(CinemaHudView.NotEdgeKey) : string.Empty,
                InstrumentsTitle = resolve(CinemaHudView.InstrumentsKey),
                EnergyTitle = resolve(CinemaHudView.EnergyKey),
                LaunchMark = resolve(CinemaHudView.LaunchMarkKey),
                JupiterMark = resolve(CinemaHudView.JupiterMarkKey),
                InstrumentNames = new[]
                {
                    resolve(CinemaHudView.InstrumentCameraKey), resolve(CinemaHudView.InstrumentRadiationKey), resolve(CinemaHudView.InstrumentPlasmaKey),
                    resolve(CinemaHudView.InstrumentMeteoroidKey), resolve(CinemaHudView.InstrumentMagnetometerKey),
                },
                InstrumentOn = new bool[InstrumentCount],
                YearFraction = YearFraction(year),
                EnergyFill = fill,
            };
            for (var i = 0; i < InstrumentCount; i++)
                state.InstrumentOn[i] = InstrumentOn(fill, i);
            return state;
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

        /// <summary>Desenha o HUD como no instante <paramref name="seconds"/> (sem ler a sequência).</summary>
        public void Apply(float seconds)
        {
            if (hud == null)
                return;

            hud.SetYears(true, StateAt(seconds, Resolve));
            hud.SetSystemMap(true, MapStateAt(seconds, Resolve), MapFrame);
            mapShown = true;
        }

        public void Hide()
        {
            if (hud == null)
                return;

            hud.SetYears(false, default);
            if (mapShown)
                hud.SetSystemMap(false, default, MapFrame); // o mapa é compartilhado com o contador do cinturão
            mapShown = false;
        }

        string Resolve(string key)
        {
            var value = text != null ? text(key) : null;
            return string.IsNullOrEmpty(value) ? key : value;
        }
    }
}
