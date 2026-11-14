using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// PIO-010: percurso orbital (cartões de 1976 e 13 JUN 1983), síntese de energia com hold de 3 s e gráfico de distâncias de
    /// 1998. Teclado simulado e relógio ativo manual: rearme do E, progresso preservado, pausa, cancelamento, pular, conclusão
    /// única e os limites científicos (nenhum encontro, rótulos separados, nenhum valor inventado).
    /// </summary>
    public class PioneerOutboundTests : InputTestFixture
    {
        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        [System.Serializable] sealed class Locale { public Entry[] entries; }

        [System.Serializable] sealed class Entry { public string key, text; }

        Keyboard keyboard;
        GameObject host;
        InputRouter router;
        InteractionService interaction;
        SequenceDirector director;
        PioneerPresentation presentation;
        OutboundAtlas atlas;
        int completed, powered;
        readonly List<SequenceResult> results = new List<SequenceResult>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            atlas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<OutboundAtlas>(true)).Single();

            host = new GameObject("services");
            var time = new FakeTime();
            router = host.AddComponent<InputRouter>();
            interaction = host.AddComponent<InteractionService>();
            director = host.AddComponent<SequenceDirector>();
            router.Enable();
            interaction.Initialize(router, time);
            presentation.CameraDirector.GroundProbe = _ => null;
            director.Initialize(presentation.CameraDirector, router, time);
            presentation.RegisterWithCameraDirector();
            atlas.Initialize(director, interaction, router, time, key => "«" + key + "»");

            completed = powered = 0;
            results.Clear();
            atlas.Completed += () => completed++;
            atlas.PowerConfirmed += () => powered++;
            director.Finished += result => results.Add(result);
        }

        public override void TearDown()
        {
            if (director.IsPlaying)
                director.Cancel();
            presentation.UnregisterFromCameraDirector();
            router.Disable();
            Object.DestroyImmediate(host);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            base.TearDown();
        }

        void Step(float dt)
        {
            director.Tick(dt);
            interaction.Tick(dt);
            atlas.Tick(dt);
        }

        void Run(float seconds, float frame = 1f / 30f)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += frame)
                Step(frame);
        }

        void RunUntilHold()
        {
            for (var t = 0f; t < 60f && !atlas.IsAwaitingHold; t += 1f / 30f)
                Step(1f / 30f);
            Assert.That(atlas.IsAwaitingHold, Is.True, "o marco de 24 s abre o hold");
        }

        // ---------- Sequência e montagem ----------

        [Test]
        public void TheBuiltSceneValidatesAndTheSequenceFollowsTheScript()
        {
            Assert.That(atlas.ValidateSetup(), Is.Empty);
            var sequence = atlas.Sequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(director.Validate(sequence), Is.Empty);
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(39f).Within(1e-4f));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 12f, 12f, 15f }), "0–12, 12–24, 24–39 s");
            Assert.That(sequence.Cues.Select(c => (c.TextKey, c.StartSeconds, c.EndSeconds)), Is.EqualTo(new[]
            {
                ("PIO.06.01", 0f, 8f), ("PIO.06.02", 8f, 16f), ("PIO.06.03", 17f, 24f), ("PIO.06.04", 25f, 33f), ("PIO.06.05", 33f, 39f),
            }), "janelas do roteiro");
            var bare = new GameObject("bare").AddComponent<OutboundAtlas>();
            Assert.That(bare.ValidateSetup(), Is.Not.Empty, "sem referências a montagem reclama");
            Object.DestroyImmediate(bare.gameObject);
        }

        [Test]
        public void TheSequenceCuesAreTheOnesOfTheChapterData()
        {
            var chapter = JsonUtility.FromJson<Chapter>(File.ReadAllText("Data/Chapters/Pioneer10.json"));
            var data = chapter.cues.Where(c => c.state == OutboundAtlas.StateId).OrderBy(c => c.timing.startSeconds).ToList();
            Assert.That(data.Select(c => (c.textKey, c.timing.startSeconds, c.timing.endSeconds)),
                Is.EqualTo(atlas.Sequence.Cues.Select(c => (c.TextKey, c.StartSeconds, c.EndSeconds))));
        }

        [System.Serializable] sealed class Chapter { public ChapterCue[] cues; }

        [System.Serializable] sealed class ChapterCue { public string id, state, textKey; public Timing timing; }

        [System.Serializable] sealed class Timing { public float startSeconds, endSeconds; }

        [Test]
        public void ThePhasesFollowTheCardsAndTheSequenceWaitsForTheHoldAtTwentyFourSeconds()
        {
            atlas.Begin();
            Run(5f);
            Assert.That(atlas.Phase, Is.EqualTo(OutboundPhase.Saturn1976));
            Assert.That(atlas.ViewState().OrbitsCrossed, Is.EqualTo(2), "1976: distância orbital de Saturno (dois anéis)");
            Run(8f);
            Assert.That(atlas.Phase, Is.EqualTo(OutboundPhase.Neptune1983));
            Assert.That(atlas.ViewState().OrbitsCrossed, Is.EqualTo(OutboundAtlas.OrbitRings), "1983: distância orbital de Netuno (quatro anéis)");

            RunUntilHold();
            Assert.That(atlas.Phase, Is.EqualTo(OutboundPhase.Power));
            var waitingAt = director.TimeSeconds;
            Assert.That(waitingAt, Is.EqualTo(24f).Within(0.1f));
            Run(6f);
            Assert.That(director.TimeSeconds, Is.EqualTo(waitingAt).Within(1e-4f), "o relógio espera o hold, sem avanço por inatividade");
            Assert.That(completed, Is.Zero);
        }

        // ---------- Hold ----------

        [Test]
        public void AnEHeldBeforeTheHoldDoesNotCountAndTheHoldConfirmsOncePrioritizingCommunication()
        {
            Press(keyboard.eKey); // segurado desde antes
            atlas.Begin();
            RunUntilHold();
            Run(4f);
            Assert.That(atlas.HoldProgress, Is.Zero, "o E antigo não inicia o hold (rearme)");
            Assert.That(atlas.ViewState().Hold, Is.EqualTo(OutboundHoldState.Rearm));

            Release(keyboard.eKey);
            Press(keyboard.eKey);
            Run(OutboundAtlas.HoldSeconds + 0.1f);
            Release(keyboard.eKey);
            Assert.That(atlas.HoldDone, Is.True);
            Assert.That(powered, Is.EqualTo(1));
            Assert.That(atlas.ViewState().PowerConfirmed, Is.True);
            Assert.That(router.Context, Is.EqualTo(InputContext.Cinematic), "o contexto do hold voltou à sequência");

            Run(2f);
            Assert.That(atlas.Phase, Is.EqualTo(OutboundPhase.Chart1998), "o gráfico entra depois do hold");
            Run(20f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(powered, Is.EqualTo(1), "a economia é confirmada uma única vez");
            Assert.That(results.Select(r => r.Reason), Is.EqualTo(new[] { SequenceExitReason.Completed }));
            Assert.That(router.DescribeLeases(), Is.Empty, "nenhum contexto de entrada esquecido");
        }

        [Test]
        public void ReleasingEKeepsTheProgressAndTheSecondHoldOnlyCoversTheRest()
        {
            atlas.Begin();
            RunUntilHold();
            Press(keyboard.eKey);
            Run(1.5f);
            Release(keyboard.eKey);
            var kept = atlas.HoldProgress;
            Assert.That(kept, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(atlas.ViewState().Hold, Is.EqualTo(OutboundHoldState.Paused));
            Run(4f);
            Assert.That(atlas.HoldProgress, Is.EqualTo(kept).Within(1e-4f), "soltar pausa, não apaga");

            Press(keyboard.eKey);
            Run(1.5f + 0.1f);
            Assert.That(atlas.HoldDone, Is.True);
            Release(keyboard.eKey);
        }

        [Test]
        public void ShellPauseFreezesTheHoldAndNothingFailsWhileWaiting()
        {
            atlas.Begin();
            RunUntilHold();
            Press(keyboard.eKey);
            Run(1f);
            var before = atlas.HoldProgress;
            for (var i = 0; i < 60; i++)
                Step(0f); // pausa: tempo ativo zero
            Assert.That(atlas.HoldProgress, Is.EqualTo(before).Within(1e-4f));

            using (router.Acquire(InputContext.Paused, "shell"))
                Run(1f);
            Assert.That(atlas.HoldProgress, Is.EqualTo(before).Within(0.02f), "pausa do shell não avança nem apaga");
            Release(keyboard.eKey);
            Run(10f);
            Assert.That(atlas.HoldProgress, Is.EqualTo(before).Within(0.05f), "esperar não consome nem pune: não existe falha");
            Assert.That(atlas.IsAwaitingHold, Is.True);
            Assert.That(completed, Is.Zero);
        }

        [Test]
        public void CancelingDuringTheHoldCleansEverythingAndReplayingCompletesOnce()
        {
            atlas.Begin();
            RunUntilHold();
            Press(keyboard.eKey);
            Run(1f);
            atlas.Cancel();
            Release(keyboard.eKey);
            Step(1f / 30f);
            Assert.That(atlas.Phase, Is.EqualTo(OutboundPhase.Idle));
            Assert.That(atlas.IsAwaitingHold, Is.False);
            Assert.That(atlas.HoldProgress, Is.Zero);
            Assert.That(completed, Is.Zero, "cancelar não conclui");
            Assert.That(interaction.TryGetActive(out _), Is.False, "sem candidato");
            Assert.That(router.DescribeLeases(), Is.Empty);

            atlas.Begin(); // repetir o capítulo: começa limpo
            RunUntilHold();
            Assert.That(atlas.HoldProgress, Is.Zero, "o progresso antigo não volta");
            Press(keyboard.eKey);
            Run(OutboundAtlas.HoldSeconds + 0.1f);
            Release(keyboard.eKey);
            Run(20f);
            Assert.That((completed, powered), Is.EqualTo((1, 1)));
        }

        [Test]
        public void SkippingCompletesOnceWithoutOpeningTheHold()
        {
            atlas.Begin(skippable: true);
            Run(3f);
            Assert.That(director.Skip(), Is.True);
            Step(1f / 30f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(atlas.IsAwaitingHold, Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);
        }

        // ---------- Diagrama, gráfico e limites científicos ----------

        [Test]
        public void TheDiagramHasFourSchematicRingsAndNoPlanetNextToTheProbe()
        {
            Assert.That(OutboundAtlas.OrbitRings, Is.EqualTo(4));
            Assert.That(OutboundAtlasView.RingKeys, Is.EqualTo(new[] { "PIO.UI.orbit_jupiter", "PIO.UI.orbit_saturn", "PIO.UI.orbit_uranus", "PIO.UI.orbit_neptune" }));
            for (var ring = 1; ring < OutboundAtlas.OrbitRings; ring++)
                Assert.That(OutboundAtlasView.RingFraction(ring) - OutboundAtlasView.RingFraction(ring - 1), Is.EqualTo(OutboundAtlasView.RingFraction(0)).Within(1e-5f), "espaçamento igual: esquemático, sem escala");

            // A UI só tem rótulos de anéis e do Sol: nenhuma imagem/ícone de planeta no UXML.
            var uxml = File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/OutboundAtlas.uxml");
            Assert.That(uxml, Does.Not.Contain("background-image"));
            Assert.That(uxml, Does.Not.Contain("<ui:Image"));
        }

        [Test]
        public void TheChartCrossesTheDistancesWithoutAnEncounterOrASecondProbe()
        {
            Assert.That(OutboundAtlasView.CrossingT, Is.InRange(0.5f, 0.95f), "o cruzamento acontece dentro do gráfico");
            Assert.That(OutboundAtlasView.CardKey(OutboundPhase.Chart1998), Is.EqualTo("PIO.Card.y1998"));
            Assert.That(OutboundAtlasView.NoteKey(OutboundPhase.Chart1998), Is.EqualTo("PIO.UI.cross1998"));
            Assert.That(OutboundAtlasView.CardKey(OutboundPhase.Saturn1976), Is.EqualTo("PIO.Card.y1976"));
            Assert.That(OutboundAtlasView.CardKey(OutboundPhase.Neptune1983), Is.EqualTo("PIO.Card.y1983"));
            Assert.That(OutboundAtlasView.CommFraction(OutboundPhase.Saturn1976), Is.LessThan(OutboundAtlasView.CommFraction(OutboundPhase.Neptune1983)));
            Assert.That(OutboundAtlasView.CommFraction(OutboundPhase.Neptune1983), Is.LessThan(OutboundAtlasView.CommFraction(OutboundPhase.Chart1998)), "o tempo de comunicação cresce com a distância");
        }

        [Test]
        public void ScienceTextsAreInBothLanguagesSeparateDistanceFromCommunicationTimeAndInventNoValues()
        {
            var keys = new[]
            {
                "PIO.UI.past_saturn", "PIO.UI.past_neptune", "PIO.UI.not_edge", "PIO.UI.sun", "PIO.UI.orbit_jupiter", "PIO.UI.orbit_saturn", "PIO.UI.orbit_uranus",
                "PIO.UI.orbit_neptune", "PIO.UI.scheme", "PIO.UI.radio", "PIO.UI.instruments", "PIO.UI.power_title", "PIO.UI.power_ok", "PIO.UI.dist_sun",
                "PIO.UI.time", "PIO.UI.pioneer10", "PIO.UI.voyager1", "PIO.UI.comm_time", "PIO.UI.cross1998", "PIO.UI.power",
            };
            foreach (var path in new[] { "Data/Localization/Pioneer10.pt-BR.json", "Data/Localization/Pioneer10.en.json" })
            {
                var t = JsonUtility.FromJson<Locale>(File.ReadAllText(path)).entries.ToDictionary(e => e.key, e => e.text);
                foreach (var key in keys)
                    Assert.That(t.ContainsKey(key) && !string.IsNullOrWhiteSpace(t[key]), Is.True, path + ": " + key);

                Assert.That(t["PIO.UI.dist_sun"], Is.Not.EqualTo(t["PIO.UI.comm_time"]), path + ": distância ao Sol e tempo de comunicação são rótulos distintos");
                Assert.That(t["PIO.UI.comm_time"].ToLowerInvariant(), Does.Contain("comunica").Or.Contain("communication"), path);
                Assert.That(t["PIO.UI.dist_sun"].ToLowerInvariant(), Does.Contain("sol").Or.Contain("sun"), path);
                Assert.That(t["PIO.UI.past_saturn"].ToLowerInvariant(), Does.Contain("não visita").Or.Contain("does not visit"), path + ": cruzar a distância não é visitar");
                Assert.That(t["PIO.UI.past_neptune"].ToLowerInvariant(), Does.Contain("além").Or.Contain("beyond"), path);
                Assert.That(t["PIO.UI.not_edge"].ToLowerInvariant(), Does.Contain("sistema solar").Or.Contain("solar system"), path);
                foreach (var forbidden in new[] { "heliopaus", "fora de todo", "outside the solar", "game over" })
                {
                    foreach (var key in keys)
                        Assert.That(t[key].ToLowerInvariant(), Does.Not.Contain(forbidden), path + ": " + key);
                }

                foreach (var key in new[] { "PIO.UI.power_title", "PIO.UI.power_ok", "PIO.UI.comm_time", "PIO.UI.cross1998" })
                    Assert.That(Regex.IsMatch(t[key].Replace("Voyager 1", "").Replace("Pioneer 10", ""), "[0-9]"), Is.False, path + ": " + key + " não traz número (nenhum valor inventado)");
            }
        }

        [Test]
        public void TheDocumentHasNoLiteralSentencesAndTheFrameLeavesMargins()
        {
            var uxml = File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/OutboundAtlas.uxml");
            var literals = Regex.Matches(uxml, "text=\"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.That(literals, Is.EqualTo(new[] { "E" }), "textos por chave; só a tecla E é literal");
            var css = File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/OutboundAtlas.uss");
            Assert.That(css, Does.Not.Contain("animation"), "sem animação por USS: nada pisca");
            Assert.That(atlas.ValidateSetup(), Is.Empty);
        }
    }
}
