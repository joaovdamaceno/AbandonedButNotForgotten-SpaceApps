using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Roteiro v2, cena 7 (os anos): o ano corre de 1974 a 1998 parando nos marcos reais, o medidor de energia só tem duas marcas reais
    /// (155 W e ~140 W) e cai numa curva estilizada, e os instrumentos apagam em turnos.
    /// </summary>
    public class PioneerYearsHudTests
    {
        static string Echo(string key) => key;

        [Test]
        public void TheYearRunsFrom1974To1998WithoutGoingBackAndStopsOnTheRealMilestones()
        {
            Assert.That(PioneerYearsHud.YearAt(0f), Is.EqualTo(1974f));
            Assert.That(PioneerYearsHud.YearAt(39f), Is.EqualTo(PioneerYearsHud.VoyagerYear).Within(1e-4f));
            var previous = 0f;
            for (var t = 0f; t <= 39f; t += 0.25f)
            {
                var year = PioneerYearsHud.YearAt(t);
                Assert.That(year, Is.GreaterThanOrEqualTo(previous), $"o ano nunca volta ({t} s)");
                previous = year;
            }

            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(0.5f)), Is.EqualTo(PioneerYearsHud.YearsEvent.None), "sem marco no começo");
            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(3.5f)), Is.EqualTo(PioneerYearsHud.YearsEvent.Saturn));
            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(7f)), Is.EqualTo(PioneerYearsHud.YearsEvent.Uranus));
            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(13f)), Is.EqualTo(PioneerYearsHud.YearsEvent.Neptune));
            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(20f)), Is.EqualTo(PioneerYearsHud.YearsEvent.None), "no meio da queda de energia o ano só passa");
            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(25f)), Is.EqualTo(PioneerYearsHud.YearsEvent.TrackingEnd));
            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(30f)), Is.EqualTo(PioneerYearsHud.YearsEvent.Voyager));
            Assert.That(PioneerYearsHud.NeptuneYear, Is.EqualTo(1983.449f).Within(0.001f), "13 de junho é o dia 164 do ano");
        }

        [Test]
        public void EachMilestoneStaysInsideTheCaptionWindowThatTalksAboutIt()
        {
            // PIO.06.01 (0–8 s): Saturno e Urano; PIO.06.02 (8–16 s): Netuno; PIO.06.04 (25–33 s): a Voyager 1.
            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(3.5f)) == PioneerYearsHud.YearsEvent.Saturn && PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(7f)) == PioneerYearsHud.YearsEvent.Uranus, Is.True);
            for (var t = 11.5f; t <= 15.5f; t += 0.5f)
                Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(t)), Is.EqualTo(PioneerYearsHud.YearsEvent.Neptune), $"Netuno parado de 11 a 16 s ({t} s)");
            for (var t = 28f; t <= 33f; t += 1f)
                Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(t)), Is.EqualTo(PioneerYearsHud.YearsEvent.Voyager), $"a Voyager 1 enquanto a legenda fala dela ({t} s)");
        }

        [Test]
        public void TheEnergyMeterHasOnlyTheTwoRealMarksAndThenAStylisedDecline()
        {
            Assert.That(PioneerYearsHud.LaunchWatts, Is.EqualTo(155f));
            Assert.That(PioneerYearsHud.JupiterWatts, Is.EqualTo(140f));
            Assert.That(PioneerYearsHud.EnergyFillAt(1972f), Is.EqualTo(1f), "155 W no lançamento é o topo do medidor");
            Assert.That(PioneerYearsHud.EnergyFillAt(PioneerYearsHud.JupiterYear), Is.EqualTo(140f / 155f).Within(0.002f), "~140 W em Júpiter");
            var previous = 2f;
            for (var year = 1972f; year <= 1998.2f; year += 0.25f)
            {
                var fill = PioneerYearsHud.EnergyFillAt(year);
                Assert.That(fill, Is.LessThanOrEqualTo(previous), $"a energia só cai ({year})");
                Assert.That(fill, Is.GreaterThanOrEqualTo(0.2f));
                previous = fill;
            }

            Assert.That(PioneerYearsHud.EnergyFillAt(PioneerYearsHud.VoyagerYear), Is.InRange(0.2f, 0.45f), "no fim o medidor está baixo, sem prometer um valor em watts");
        }

        [Test]
        public void TheInstrumentsGoOutInShiftsAndTheMagnetometerIsTheLastOneOn()
        {
            Assert.That(Enumerable.Range(0, PioneerYearsHud.InstrumentCount).All(i => PioneerYearsHud.InstrumentOn(1f, i)), Is.True, "no começo todos funcionam");
            var previousOn = PioneerYearsHud.InstrumentCount;
            for (var year = 1974f; year <= 1998.2f; year += 0.25f)
            {
                var on = Enumerable.Range(0, PioneerYearsHud.InstrumentCount).Count(i => PioneerYearsHud.InstrumentOn(PioneerYearsHud.EnergyFillAt(year), i));
                Assert.That(on, Is.LessThanOrEqualTo(previousOn), $"nenhum instrumento volta a ligar ({year})");
                previousOn = on;
            }

            var finalFill = PioneerYearsHud.EnergyFillAt(PioneerYearsHud.VoyagerYear);
            Assert.That(PioneerYearsHud.InstrumentOn(finalFill, 4), Is.True, "o magnetômetro segue ligado até o fim");
            Assert.That(Enumerable.Range(0, 4).Any(i => !PioneerYearsHud.InstrumentOn(finalFill, i)), Is.True, "os outros foram sendo desligados");
        }

        [Test]
        public void TheStateCarriesFiveInstrumentNamesTheKeysAndTheTimelinePosition()
        {
            var state = PioneerYearsHud.StateAt(13f, Echo);
            Assert.That(state.Big, Is.EqualTo(CinemaHudView.Card1983Key), "em Netuno o destaque é a data completa");
            Assert.That(state.Event, Is.EqualTo(CinemaHudView.PastNeptuneKey));
            Assert.That(state.Note, Is.EqualTo(CinemaHudView.NotEdgeKey), "o aviso de que passar as órbitas não é sair do Sistema Solar");
            Assert.That(state.InstrumentNames, Has.Length.EqualTo(5));
            Assert.That(state.InstrumentOn, Has.Length.EqualTo(5));
            Assert.That(state.EnergyTitle, Is.EqualTo(CinemaHudView.EnergyKey));
            Assert.That(state.YearFraction, Is.InRange(0f, 1f));
            Assert.That(PioneerYearsHud.StateAt(0.5f, Echo).Big, Is.EqualTo("1974"));
            Assert.That(PioneerYearsHud.StateAt(0.5f, Echo).Event, Is.Empty);
            Assert.That(PioneerYearsHud.EventYears.All(y => y >= PioneerYearsHud.YearMin && y <= PioneerYearsHud.YearMax), Is.True);
            Assert.That(PioneerYearsHud.YearFraction(1973f), Is.Zero);
            Assert.That(PioneerYearsHud.YearFraction(1998f), Is.EqualTo(1f));
        }

        [Test]
        public void TheChapterHoldsTheYearsHudAndTheAtlasIsHiddenInYearsMode()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerChapterController>(true)).Single();
                Assert.That(controller.ValidateSetup(), Is.Empty);
                var atlas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OutboundAtlas>(true)).Single();
                Assert.That(atlas.YearsMode, Is.False, "o padrão é o atlas; só o controlador liga o modo dos anos");
                Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerYearsHud>(true)).Count(), Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
