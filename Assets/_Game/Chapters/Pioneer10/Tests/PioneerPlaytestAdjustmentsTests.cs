using System.Linq;
using NUnit.Framework;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Ajustes do teste de jogo (15/11/2026): estilingue em tempo acelerado, mapa esquemático (cinturão e Netuno), efeitos sonoros e a
    /// janela mais fácil do minigame da foto. Só comportamento verificável sem Play Mode.
    /// </summary>
    public sealed class PioneerPlaytestAdjustmentsTests
    {
        static string Resolve(string key) => key;

        [Test]
        public void TheSlingshotAcceleratesTowardJupiterAndLeavesFasterThanItArrived()
        {
            Assert.That(PioneerSlingshot.Progress(0f), Is.EqualTo(0f));
            Assert.That(PioneerSlingshot.Progress(1f), Is.EqualTo(1f));
            Assert.That(PioneerSlingshot.RelativeSpeed(0.85f), Is.GreaterThan(PioneerSlingshot.RelativeSpeed(0.15f) * 3f), "sai bem mais rápida do que chegou");
            var previous = -1f;
            for (var u = 0f; u <= 1f; u += 0.05f)
            {
                Assert.That(PioneerSlingshot.Progress(u), Is.GreaterThan(previous), "o avanço só cresce");
                previous = PioneerSlingshot.Progress(u);
            }
        }

        [Test]
        public void TheSlingshotCameraRidesTheTrajectoryLookingAheadWithJupiterInView()
        {
            var points = new[] { new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(200f, 0f, 100f), new Vector3(300f, 0f, 400f) };
            var go = new GameObject("Slingshot");
            try
            {
                var slingshot = go.AddComponent<PioneerSlingshot>();
                var so = new SerializedObject(slingshot);
                var path = so.FindProperty("path");
                path.arraySize = points.Length;
                for (var i = 0; i < points.Length; i++)
                    path.GetArrayElementAtIndex(i).vector3Value = points[i];
                var jupiter = new Vector3(150f, 0f, 120f);
                so.FindProperty("jupiterCenter").vector3Value = jupiter;
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(slingshot.PositionAtFraction(0f), Is.EqualTo(points[0]));
                Assert.That(slingshot.PositionAtFraction(1f), Is.EqualTo(points[3]));
                var start = slingshot.PositionAt(0f);
                var end = slingshot.PositionAt(1f);
                Assert.That(Vector3.Distance(points[0], start), Is.GreaterThan(1f), "começa depois do início da linha");
                Assert.That(Vector3.Distance(points[3], end), Is.GreaterThan(0.1f), "termina antes do fim da linha");

                var previous = -1f;
                var previousStep = 0f;
                var travelled = 0f;
                var last = start;
                for (var u = 0.1f; u <= 1.0001f; u += 0.1f)
                {
                    var position = slingshot.PositionAt(u);
                    var step = Vector3.Distance(position, last);
                    Assert.That(step, Is.GreaterThan(previousStep - 1e-3f), "cada passo é maior que o anterior: o tempo acelera");
                    previousStep = step;
                    travelled += step;
                    last = position;
                    var forward = slingshot.ForwardAt(u);
                    Assert.That(forward.magnitude, Is.EqualTo(1f).Within(1e-3f));
                    Assert.That(Vector3.Dot(forward, (jupiter - position).normalized), Is.GreaterThan(0f), "Júpiter fica no hemisfério do olhar (u=" + u + ")");
                    Assert.That(PioneerSlingshot.FractionAt(u), Is.GreaterThan(previous), "o avanço só cresce");
                    previous = PioneerSlingshot.FractionAt(u);
                }

                Assert.That(travelled, Is.GreaterThan(100f), "percorre uma boa parte da linha");
                Object.DestroyImmediate(go);
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheProbeCrossesTheOrbitsOfSaturnUranusAndNeptuneInTheRealYears()
        {
            Assert.That(PioneerYearsHud.ProbeAuAt(1976f), Is.EqualTo(SystemMapElement.OuterAu[1]).Within(1.5f), "Saturno, 1976");
            Assert.That(PioneerYearsHud.ProbeAuAt(1979f), Is.EqualTo(SystemMapElement.OuterAu[2]).Within(1.5f), "Urano, 1979");
            Assert.That(PioneerYearsHud.ProbeAuAt(PioneerYearsHud.NeptuneYear), Is.EqualTo(SystemMapElement.OuterAu[3]).Within(1.5f), "Netuno, 13 JUN 1983");
            Assert.That(PioneerYearsHud.ProbeAuAt(1973f), Is.EqualTo(PioneerYearsHud.JupiterAu), "nunca abaixo da órbita de Júpiter");
        }

        [Test]
        public void TheYearsMapPulsesTheOrbitJustCrossedAndTheBeltMapHighlightsTheBelt()
        {
            var neptune = PioneerYearsHud.MapStateAt(20f, Resolve); // 13 JUN 1983 (parada de 16 a 24 s)
            Assert.That(PioneerYearsHud.EventAt(PioneerYearsHud.YearAt(20f)), Is.EqualTo(PioneerYearsHud.YearsEvent.Neptune));
            Assert.That(neptune.PulseRing, Is.EqualTo(3), "a órbita de Netuno pulsa");
            Assert.That(neptune.ProbeAu, Is.GreaterThan(SystemMapElement.OuterAu[3] - 1.5f));
            Assert.That(neptune.Mode, Is.EqualTo(SystemMapElement.MapMode.Outer));
            Assert.That(neptune.ShowPath, Is.True);
            Assert.That(neptune.Labels, Has.Length.EqualTo(4));

            Assert.That(PioneerBeltCounter.MapVisibleAt(PioneerBeltCounter.MapStartSeconds - 0.1f), Is.False);
            Assert.That(PioneerBeltCounter.MapVisibleAt(PioneerBeltCounter.MapStartSeconds), Is.True);
            Assert.That(PioneerBeltCounter.MapVisibleAt(PioneerBeltCounter.MapEndSeconds - 0.1f), Is.True);
            Assert.That(PioneerBeltCounter.MapVisibleAt(PioneerBeltCounter.MapEndSeconds), Is.False, "o ponto de vista da sonda assume");
            var inBelt = PioneerBeltCounter.MapStateAt(PioneerBeltCounter.MapEndSeconds - 0.01f, Resolve);
            Assert.That(inBelt.PulseRing, Is.EqualTo(SystemMapElement.BeltRing), "o cinturão pulsa");
            Assert.That(inBelt.ProbeAu, Is.InRange(SystemMapElement.BeltInnerAu - 0.2f, SystemMapElement.BeltInnerAu), "para na borda do campo, sem atravessá-lo (a travessia é mostrada depois)");
            Assert.That(inBelt.PathEndAu, Is.LessThanOrEqualTo(SystemMapElement.BeltInnerAu + 0.1f), "a linha à frente também acaba na borda");
            Assert.That(inBelt.Labels, Has.Length.EqualTo(6), "Mercúrio, Vênus, Terra, Marte, cinturão e Júpiter");
            Assert.That(neptune.Title, Is.Empty, "menos informação na cena dos anos");
            Assert.That(neptune.Footnote, Is.Empty);
            Assert.That(PioneerBeltCounter.MapProbeAuAt(PioneerBeltCounter.MapStartSeconds), Is.EqualTo(PioneerBeltCounter.MapStartAu).Within(1e-3f));
        }

        [Test]
        public void TheMapRadiusGrowsWithTheSquareRootSoTheInnerSystemStaysVisible()
        {
            Assert.That(SystemMapElement.RadiusFor(1.5f, 6f, 100f), Is.EqualTo(100f * Mathf.Sqrt(0.25f)).Within(1e-3f));
            Assert.That(SystemMapElement.RadiusFor(6f, 6f, 100f), Is.EqualTo(100f).Within(1e-3f));
            Assert.That(SystemMapElement.RadiusFor(1f, 72f, 100f), Is.GreaterThan(10f), "a órbita da Terra não vira um ponto no mapa externo");
        }

        [Test]
        public void TheBeltRocksHaveVariedSizesWithMostOfThemSmall()
        {
            var sizes = Enumerable.Range(0, 400).Select(i => PioneerStageArt.RockSize(PioneerStageBuilder.BeltRockMinMeters, PioneerStageBuilder.BeltRockMaxMeters, i / 399f)).ToArray();
            Assert.That(sizes.Min(), Is.EqualTo(PioneerStageBuilder.BeltRockMinMeters).Within(1e-3f));
            Assert.That(sizes.Max(), Is.EqualTo(PioneerStageBuilder.BeltRockMaxMeters).Within(1e-3f));
            Assert.That(sizes.Max() / sizes.Min(), Is.GreaterThan(10f), "tamanhos bem variados");
            var median = sizes.OrderBy(s => s).ElementAt(sizes.Length / 2);
            Assert.That(median, Is.LessThan((PioneerStageBuilder.BeltRockMinMeters + PioneerStageBuilder.BeltRockMaxMeters) * 0.5f), "a maioria é menor que o meio da faixa");
        }

        [Test]
        public void TheSpinGameIsFasterAndEasierThanBefore()
        {
            Assert.That(PioneerImaging.SpinPeriodSeconds, Is.LessThan(9f), "volta mais rápida que os 9 s de antes");
            Assert.That(PioneerImaging.SpinWindowHalfDegrees, Is.GreaterThan(15f), "janela mais larga que os 15 graus de antes");
            Assert.That(PioneerImaging.SpinAssistSeconds, Is.LessThan(PioneerImaging.HelpAssistSeconds), "a ajuda chega antes");
            Assert.That(PioneerImaging.CombineSeconds, Is.LessThan(9f));
            Assert.That(PioneerImaging.SpinWindowHalfDegrees * 2f / 360f * PioneerImaging.SpinPeriodSeconds, Is.GreaterThan(0.6f), "ainda dá tempo de reagir");
        }

        [Test]
        public void TheSpaceBackgroundIsNearBlackAndNeutral()
        {
            var texture = PioneerStageArt.Starfield(128, 64, 0, 1972);
            try
            {
                var darkest = texture.GetPixels().Min(c => c.maxColorComponent);
                Assert.That(darkest, Is.LessThan(0.04f), "há céu quase preto (valor de textura sRGB: até ~10/255)");
                var sample = texture.GetPixel(2, 2);
                Assert.That(sample.b - sample.r, Is.LessThan(0.01f), "sem tom azul no fundo");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TheLaunchAndPowerDownEffectsAreRegisteredWithFilesAndCredits()
        {
            var map = AssetDatabase.LoadAssetAtPath<PioneerCueMap>("Assets/_Game/Chapters/Pioneer10/Audio/PioneerCueMap.asset");
            Assert.That(map, Is.Not.Null);
            foreach (var id in new[] { "SFX_PIO_LAUNCH", "SFX_PIO_POWERDOWN" })
            {
                Assert.That(map.TryGetCue(id, out var cue), Is.True, id);
                Assert.That(cue.licenseStatus, Is.EqualTo("acquired"), id);
                Assert.That(cue.creditId, Is.Not.Empty, id);
            }

            Assert.That(map.EventsFor("PIO.Outbound.PowerDown").Any(e => e.cueId == "SFX_PIO_POWERDOWN"), Is.True, "o desligar do equipamento tem evento");
            Assert.That(map.EventsFor("PIO.Launch.Ignition").Any(e => e.cueId == "SFX_PIO_LAUNCH"), Is.True);
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioDirectorSetup.CatalogPath);
            foreach (var id in new[] { "SFX_PIO_LAUNCH", "SFX_PIO_POWERDOWN" })
                Assert.That(catalog.Cues.Any(c => c.Id == id && !c.IsPending), Is.True, id + ": cue com clipe no catálogo");
            Assert.That(System.IO.File.ReadAllText("ASSET_CREDITS.md"), Does.Contain("S-PIO-SFX-01").And.Contain("S-PIO-SFX-02"));
        }

        [Test]
        public void TheFarewellDriftNeverStopsAndStartsSlowly()
        {
            Assert.That(PioneerFarewellDrift.DistanceAt(PioneerFarewellDrift.StartSeconds), Is.EqualTo(0f), "parada até o plano em que a sonda se afasta");
            var previous = 0f;
            for (var t = PioneerFarewellDrift.StartSeconds; t < 2000f; t += 7f)
            {
                var distance = PioneerFarewellDrift.DistanceAt(t);
                Assert.That(distance, Is.GreaterThanOrEqualTo(previous), "só avança");
                previous = distance;
            }

            Assert.That(PioneerFarewellDrift.DistanceAt(2000f), Is.GreaterThan(PioneerFarewellDrift.DistanceAt(1000f) + 500f), "sem fim: continua andando depois de muito tempo");
            var speed = PioneerFarewellDrift.DistanceAt(300f) - PioneerFarewellDrift.DistanceAt(299f);
            Assert.That(speed, Is.EqualTo(PioneerFarewellDrift.MetersPerSecond).Within(1e-3f), "velocidade constante e lenta");
            Assert.That(PioneerFarewellDrift.MetersPerSecond, Is.LessThan(2f), "lenta");
        }

        [Test]
        public void TheInstrumentsSwitchOffOneByOneSoTheShutdownSoundHasSomethingToPlayOn()
        {
            var on = new bool[PioneerYearsHud.InstrumentCount];
            for (var i = 0; i < on.Length; i++)
                on[i] = true;
            var switchOffs = 0;
            for (var seconds = 0f; seconds <= 55f; seconds += 0.25f)
            {
                var now = PioneerYearsHud.StateAt(seconds, Resolve).InstrumentOn;
                var any = false;
                for (var i = 0; i < now.Length; i++)
                {
                    if (on[i] && !now[i])
                        any = true;
                    on[i] = now[i];
                }

                if (any)
                    switchOffs++;
            }

            Assert.That(switchOffs, Is.GreaterThanOrEqualTo(3), "pelo menos três momentos de desligamento ao longo da cena dos anos");
        }

        [Test]
        public void ThePhotoGameKeepsTheCameraStillAndOnlyTheBarMoves()
        {
            Assert.That(PioneerImaging.CameraStaysStill, Is.True);
            Assert.That(PioneerImaging.SpinPeriodSeconds, Is.LessThan(4f), "a barra cruza rápido");
            Assert.That(PioneerImaging.SpinWindowHalfDegrees * 2f / 360f * PioneerImaging.SpinPeriodSeconds, Is.GreaterThan(0.6f), "janela de pelo menos 0,6 s");
            Assert.That(PioneerImaging.SpinWindowHalfDegrees * 2f / 360f, Is.InRange(0.18f, 0.4f), "a zona dourada ocupa uma boa parte da barra");
        }
    }
}
