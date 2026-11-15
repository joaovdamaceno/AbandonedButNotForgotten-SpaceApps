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
    /// PIO-011: escuta DSN em 52 s, três contatos de zona generosa, resultados históricos distintos, quarta tentativa sem
    /// resposta e silêncio real. Teclado simulado e relógio ativo manual: contatos concluíveis só com teclado, E mantido não
    /// confirma a rodada seguinte, pausa, cancelamento/reentrada, pular, e os limites científicos.
    /// </summary>
    public class PioneerListeningTests : InputTestFixture
    {
        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        [System.Serializable] sealed class Locale { public Entry[] entries; }

        [System.Serializable] sealed class Entry { public string key, text; }

        [System.Serializable] sealed class Chapter { public ChapterCue[] cues; }

        [System.Serializable] sealed class ChapterCue { public string id, state, textKey; public Timing timing; }

        [System.Serializable] sealed class Timing { public string kind, triggerId; public float startSeconds, endSeconds, minimumReadSeconds; }

        Keyboard keyboard;
        GameObject host;
        InputRouter router;
        InteractionService interaction;
        SequenceDirector director;
        PioneerPresentation presentation;
        PioneerListening listening;
        int completed, noSignal;
        readonly List<int> contacts = new List<int>();
        readonly List<string> stateEvents = new List<string>();
        readonly List<string> cuesShown = new List<string>();

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            listening = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerListening>(true)).Single();

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
            listening.Initialize(director, interaction, router, time, key => "«" + key + "»");

            completed = noSignal = 0;
            contacts.Clear();
            stateEvents.Clear();
            cuesShown.Clear();
            listening.Completed += () => completed++;
            listening.NoSignalHeard += () => noSignal++;
            listening.ContactMade += n => contacts.Add(n);
            listening.StateEvent += id => stateEvents.Add(id);
            director.CueEntered += cue => cuesShown.Add(cue.CueId);
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
            listening.Tick(dt);
        }

        void Run(float seconds, float frame = 1f / 30f)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += frame)
                Step(frame);
        }

        void RunUntil(System.Func<bool> condition, float limitSeconds = 120f, float frame = 1f / 30f)
        {
            for (var t = 0f; t < limitSeconds && !condition(); t += frame)
                Step(frame);
            Assert.That(condition(), Is.True, "condição não alcançada");
        }

        /// <summary>Faz uma rodada só com E: a varredura assistida leva o botão à zona e depois ele escuta por 2 s.</summary>
        void CompleteContactWithE(int expectedContacts)
        {
            Release(keyboard.eKey);
            Step(1f / 30f);
            Press(keyboard.eKey);
            RunUntil(() => contacts.Count == expectedContacts, 30f);
            Release(keyboard.eKey);
            Step(1f / 30f);
        }

        // ---------- Sequência e montagem ----------

        [Test]
        public void TheBuiltSceneValidatesAndTheSequenceFollowsTheScript()
        {
            Assert.That(listening.ValidateSetup(), Is.Empty);
            var sequence = listening.Sequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(director.Validate(sequence), Is.Empty);
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(52f).Within(1e-4f));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 7f, 9f, 9f, 12f, 8f, 7f }), "ensinar 0–7, contatos 7–16/16–25/25–37, tentativa 37–45, respirar 45–52");
            var bare = new GameObject("bare").AddComponent<PioneerListening>();
            Assert.That(bare.ValidateSetup(), Is.Not.Empty, "sem referências a montagem reclama");
            Object.DestroyImmediate(bare.gameObject);
        }

        [Test]
        public void TheSequenceCuesAreTheOnesOfTheChapterData()
        {
            var chapter = JsonUtility.FromJson<Chapter>(File.ReadAllText("Data/Chapters/Pioneer10.json"));
            var data = chapter.cues.Where(c => c.state == PioneerListening.StateId).ToDictionary(c => c.id);
            Assert.That(listening.Sequence.Cues.Select(c => c.CueId), Is.EquivalentTo(data.Keys));
            foreach (var cue in listening.Sequence.Cues)
            {
                var expected = data[cue.CueId];
                Assert.That(cue.TextKey, Is.EqualTo(expected.textKey));
                if (expected.timing.kind == "trigger")
                {
                    Assert.That(cue.IsTriggered, Is.True, cue.CueId);
                    Assert.That(cue.TriggerId, Is.EqualTo(expected.timing.triggerId), cue.CueId + ": id de gatilho confirmado");
                    Assert.That(cue.MinimumReadSeconds, Is.EqualTo(expected.timing.minimumReadSeconds), cue.CueId);
                }
                else
                {
                    Assert.That((cue.StartSeconds, cue.EndSeconds), Is.EqualTo((expected.timing.startSeconds, expected.timing.endSeconds)), cue.CueId);
                }
            }
        }

        [Test]
        public void TheDishSilhouetteSitsOnTheStageSocketAndTheZonesAreGenerousAndLegible()
        {
            listening.AttachToStage();
            var dish = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "DsnSilhouette").gameObject;
                        Assert.That(dish.transform.parent.name, Is.EqualTo("DishSocket"));

            for (var i = 0; i < PioneerListening.ContactCount; i++)
            {
                var start = PioneerListening.ZoneStarts[i];
                var end = start + PioneerListening.ZoneWidth;
                Assert.That(PioneerListening.ZoneWidth, Is.EqualTo(0.30f), "zona de 30% da escala");
                Assert.That(start, Is.GreaterThanOrEqualTo(0f));
                Assert.That(end, Is.LessThanOrEqualTo(1f));
                var knob = PioneerListening.StartKnobs[i];
                var gap = knob < start ? start - knob : knob - end;
                Assert.That(gap, Is.InRange(0.0001f, 0.15f), "o botão começa a no máximo 15% da borda da zona");
            }

            Assert.That(PioneerListening.ZoneStarts.Distinct().Count(), Is.EqualTo(3), "zonas ligeiramente deslocadas");
            Assert.That(PioneerListening.Signals[2], Is.LessThan(PioneerListening.Signals[1]).And.LessThan(PioneerListening.Signals[0]), "o terceiro contato é mais fraco");
            Assert.That(PioneerListening.Signals[2], Is.GreaterThanOrEqualTo(0.4f), "mas a zona continua legível");
        }

        // ---------- Entrada e rodadas ----------

        [Test]
        public void TeachingMovesTheKnobWithTheArrowsAndShowsProximityBeforeAnyListening()
        {
            listening.Begin();
            Run(1f);
            var before = listening.ViewState();
            Assert.That(before.Phase, Is.EqualTo(ListeningPhase.Teach));
            Assert.That(before.Proximity, Is.GreaterThan(0.5f), "o indicador de proximidade já aparece antes do primeiro ajuste");
            Assert.That(before.Direction, Is.EqualTo(1), "a seta aponta para a zona");

            Press(keyboard.rightArrowKey);
            Run(0.5f);
            Release(keyboard.rightArrowKey);
            Assert.That(listening.ViewState().Knob, Is.GreaterThan(before.Knob), "setas movem o botão");
            Assert.That(listening.ViewState().Proximity, Is.GreaterThan(before.Proximity));

            Press(keyboard.eKey);
            Run(3f);
            Release(keyboard.eKey);
            Assert.That(contacts, Is.Empty, "no ensino E ainda não escuta: não há rodada concluída antes de 7 s");
            Assert.That(listening.ListenProgress, Is.Zero);
        }

        [Test]
        public void TheArrowKeysAloneCompleteTheFirstContactWithoutTimingAndWithoutE()
        {
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            var guard = 0;
            Press(keyboard.rightArrowKey);
            while (!listening.ViewState().InZone && guard++ < 400)
                Step(1f / 30f);
            Release(keyboard.rightArrowKey);
            Assert.That(listening.ViewState().InZone, Is.True, "as setas levam o botão à zona de 30%");

            Press(keyboard.eKey);
            Run(PioneerListening.ListenSeconds + 0.2f);
            Release(keyboard.eKey);
            Assert.That(contacts, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void HoldingEOutsideTheZoneRunsASlowAssistedSweepThatStopsAtTheFirstValidZone()
        {
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            var start = listening.Knob;
            Assert.That(listening.ViewState().InZone, Is.False);
            Press(keyboard.eKey);
            Run(0.3f);
            Assert.That(listening.ViewState().Assisting, Is.True);
            Assert.That(listening.Knob, Is.GreaterThan(start));
            RunUntil(() => listening.ViewState().InZone, 10f);
            Assert.That(listening.Knob, Is.LessThan(PioneerListening.ZoneStarts[0] + 0.1f), "pára na primeira zona válida, na borda");
            Assert.That(listening.ViewState().Assisting, Is.False);
            Release(keyboard.eKey);
        }

        [Test]
        public void TheThreeContactsGiveDifferentHistoricalResultsAndTheFourthHearsNothing()
        {
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            CompleteContactWithE(1);
            Assert.That(listening.ViewState().Result, Is.EqualTo(ListeningResult.Signal));
            Assert.That(ListeningPanelView.CardKey(listening.ViewState()), Is.EqualTo("PIO.Card.contact1"));
            RunUntil(() => listening.Phase == ListeningPhase.Contact2);
            CompleteContactWithE(2);
            Assert.That(listening.ViewState().Result, Is.EqualTo(ListeningResult.Data), "27 ABR 2002: últimos dados de telemetria");
            RunUntil(() => listening.Phase == ListeningPhase.Contact3);
            CompleteContactWithE(3);
            Assert.That(listening.ViewState().Result, Is.EqualTo(ListeningResult.Carrier), "23 JAN 2003: portadora fraca, sem dados novos");
            RunUntil(() => listening.Phase == ListeningPhase.Attempt4);
            Assert.That(listening.ViewState().ZoneVisible, Is.False, "não há zona escondida nem frequência correta");
            Assert.That(listening.ViewState().Proximity, Is.Zero);
            CompleteAttempt4();
            Assert.That(noSignal, Is.EqualTo(1));
            Assert.That(listening.ViewState().Result, Is.EqualTo(ListeningResult.NoSignal));

            var views = new[] { ListeningResult.Signal, ListeningResult.Data, ListeningResult.Carrier, ListeningResult.NoSignal }
                .Select(r => new ListeningViewState { Result = r, Phase = ListeningPhase.Contact1 }).ToList();
            Assert.That(views.Select(ListeningPanelView.StatusKey).Distinct().Count(), Is.EqualTo(4), "sinal, dados, portadora e nada: quatro rótulos diferentes");
            Assert.That(views.Select(ListeningPanelView.CardKey).Distinct().Count(), Is.EqualTo(4), "quatro datas diferentes");
            var waves = views.Select(v => string.Join(",", Enumerable.Range(0, ListeningPanelView.WaveBars).Select(i => ListeningPanelView.WaveHeight(v, i).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)))).ToList();
            Assert.That(waves.Distinct().Count(), Is.EqualTo(4), "quatro formas de onda diferentes (a diferença não depende de som nem de cor)");
        }

        void CompleteAttempt4()
        {
            Release(keyboard.eKey);
            Step(1f / 30f);
            Press(keyboard.eKey);
            RunUntil(() => noSignal == 1, 20f);
            Release(keyboard.eKey);
            Step(1f / 30f);
        }

        [Test]
        public void AnEHeldAfterEachContactDoesNotConfirmTheNextOneUntilReleased()
        {
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            Press(keyboard.eKey);
            RunUntil(() => contacts.Count == 1, 30f);
            RunUntil(() => listening.Phase == ListeningPhase.Contact2);
            Run(8f); // E continua mantido durante toda a rodada 2
            Assert.That(contacts, Is.EqualTo(new[] { 1 }), "o E mantido não conclui a rodada seguinte");
            Assert.That(listening.ListenProgress, Is.Zero);
            Assert.That(listening.ViewState().Hold, Is.EqualTo(ListeningHoldState.Rearm));
            Assert.That(listening.IsWaitingForPrevious, Is.False);

            Release(keyboard.eKey);
            Step(1f / 30f);
            Press(keyboard.eKey);
            RunUntil(() => contacts.Count == 2, 30f);
            Release(keyboard.eKey);
        }

        [Test]
        public void ReleasingEOrLeavingTheZoneKeepsTheListeningProgress()
        {
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            Press(keyboard.eKey);
            RunUntil(() => listening.ListenProgress >= 0.5f, 20f);
            Release(keyboard.eKey);
            Step(1f / 30f);
            var kept = listening.ListenProgress;
            Run(5f);
            Assert.That(listening.ListenProgress, Is.EqualTo(kept).Within(1e-4f), "soltar pausa, não apaga");
            Assert.That(listening.ViewState().Hold, Is.EqualTo(ListeningHoldState.Paused));
            Press(keyboard.eKey);
            Run(1.2f);
            Release(keyboard.eKey);
            Assert.That(contacts, Is.EqualTo(new[] { 1 }), "o segundo hold cobre só o restante");
        }

        // ---------- Sequência, silêncio e saída ----------

        [Test]
        public void EachMarkWaitsForThePreviousRoundAndNothingAdvancesByInactivity()
        {
            listening.Begin();
            Run(20f);
            Assert.That(listening.IsWaitingForPrevious, Is.True, "a rodada 1 não foi feita: o marco de 16 s espera");
            var waitingAt = director.TimeSeconds;
            Assert.That(waitingAt, Is.EqualTo(16f).Within(0.2f));
            Run(15f);
            Assert.That(director.TimeSeconds, Is.EqualTo(waitingAt).Within(1e-3f));
            Assert.That(listening.Phase, Is.EqualTo(ListeningPhase.Contact1));
            Assert.That(completed, Is.Zero);

            CompleteContactWithE(1);
            Run(0.2f);
            Assert.That(listening.Phase, Is.EqualTo(ListeningPhase.Contact2), "ao concluir, a rodada 2 abre e a sequência segue");
            Assert.That(listening.IsWaitingForPrevious, Is.False);
        }

        [Test]
        public void ContactCuesOpenByTriggerAndNeverOverlapTheWindowCues()
        {
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            CompleteContactWithE(1);
            Assert.That(cuesShown, Does.Contain("PIO.07.02"), "o resultado do contato 1 abre a legenda por gatilho");
            RunUntil(() => listening.Phase == ListeningPhase.Contact2);
            CompleteContactWithE(2);
            RunUntil(() => listening.Phase == ListeningPhase.Contact3);
            CompleteContactWithE(3);
            Assert.That(cuesShown, Is.SupersetOf(new[] { "PIO.07.01", "PIO.07.02", "PIO.07.03", "PIO.07.04" }));
            Assert.That(stateEvents.Where(e => e.StartsWith("PIO.Listening.Contact")), Is.EqualTo(new[] { "PIO.Listening.Contact1", "PIO.Listening.Contact2", "PIO.Listening.Contact3" }));
        }

        [Test]
        public void TheBreathingPhaseCollapsesTheControlsAndHasTwoAndAHalfSecondsOfRealSilence()
        {
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            CompleteContactWithE(1);
            RunUntil(() => listening.Phase == ListeningPhase.Contact2);
            CompleteContactWithE(2);
            RunUntil(() => listening.Phase == ListeningPhase.Contact3);
            CompleteContactWithE(3);
            RunUntil(() => listening.Phase == ListeningPhase.Attempt4);
            CompleteAttempt4();
            RunUntil(() => listening.Phase == ListeningPhase.Breathe);

            var view = listening.ViewState();
            Assert.That(view.ControlsCollapsed, Is.True, "os controles recolhem");
            Assert.That(view.Silent, Is.True);
            Assert.That(listening.SilenceRemaining, Is.EqualTo(PioneerListening.SilenceSeconds).Within(0.1f));
            Assert.That(PioneerListening.SilenceSeconds, Is.EqualTo(2.5f));
            Assert.That(stateEvents.Count(e => e == PioneerListening.StateEventAttempt4), Is.EqualTo(1), "o silêncio é pedido ao áudio uma vez");
            Assert.That(Enumerable.Range(0, ListeningPanelView.WaveBars).All(i => ListeningPanelView.WaveHeight(view, i) <= 0.061f), Is.True, "onda parada e mínima: sem chiado");

            Run(1f);
            Assert.That(listening.SilenceRemaining, Is.EqualTo(1.5f).Within(0.1f));
            for (var i = 0; i < 120; i++)
                Step(0f); // pausa: tempo ativo zero
            Assert.That(listening.SilenceRemaining, Is.EqualTo(1.5f).Within(0.1f), "pausar congela o silêncio");
            Run(3f);
            Assert.That(listening.IsSilent, Is.False);

            RunUntil(() => completed == 1, 30f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(router.DescribeLeases(), Is.Empty, "nenhum contexto de entrada esquecido");
            Assert.That(interaction.TryGetActive(out _), Is.False);
        }

        [Test]
        public void CancelingAnywhereCleansEverythingAndReplayingStartsFromTheBeginning()
        {
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            Press(keyboard.eKey);
            Run(1f);
            listening.Cancel();
            Release(keyboard.eKey);
            Step(1f / 30f);
            Assert.That(listening.Phase, Is.EqualTo(ListeningPhase.Idle));
            Assert.That(listening.ListenProgress, Is.Zero);
            Assert.That(listening.ContactsMade, Is.Zero);
            Assert.That(completed, Is.Zero, "cancelar não conclui");
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False, "sem candidato");

            contacts.Clear();
            listening.Begin();
            RunUntil(() => listening.Phase == ListeningPhase.Contact1);
            Assert.That(listening.ListenProgress, Is.Zero, "nada do ensaio anterior volta");
            CompleteContactWithE(1);
            Assert.That(contacts, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void SkippingCompletesOnceWithoutLeavingInputOrAWaitingMark()
        {
            listening.Begin(skippable: true);
            Run(3f);
            Assert.That(director.Skip(), Is.True);
            Step(1f / 30f);
            Step(1f / 30f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(listening.IsWaitingForPrevious, Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);
        }

        // ---------- Limites científicos ----------

        [Test]
        public void TextsAreInBothLanguagesAndKeepTheHistoricalDistinctionsWithoutBlamingTheListener()
        {
            var keys = new[]
            {
                "PIO.UI.tune", "PIO.UI.listen", "PIO.UI.nearer", "PIO.UI.farther", "PIO.UI.received", "PIO.UI.kind_data", "PIO.UI.kind_carrier", "PIO.UI.no_signal",
                "PIO.UI.no_fault", "PIO.UI.help", "PIO.Card.contact1", "PIO.Card.contact2", "PIO.Card.contact3", "PIO.Card.attempt4",
            };
            foreach (var path in new[] { "Data/Localization/Pioneer10.pt-BR.json", "Data/Localization/Pioneer10.en.json" })
            {
                var t = JsonUtility.FromJson<Locale>(File.ReadAllText(path)).entries.ToDictionary(e => e.key, e => e.text);
                foreach (var key in keys)
                    Assert.That(t.ContainsKey(key) && !string.IsNullOrWhiteSpace(t[key]), Is.True, path + ": " + key);

                Assert.That(t["PIO.Card.contact1"], Does.Contain("3 MAR 2002").Or.Contain("3 MAR 2002"), path);
                Assert.That(t["PIO.Card.contact2"], Does.Contain("27 ABR 2002").Or.Contain("27 APR 2002"), path);
                Assert.That(t["PIO.Card.contact3"], Does.Contain("23 JAN 2003"), path);
                Assert.That(t["PIO.Card.attempt4"], Does.Contain("7 FEV 2003").Or.Contain("7 FEB 2003"), path);
                Assert.That(new[] { t["PIO.UI.received"], t["PIO.UI.kind_data"], t["PIO.UI.kind_carrier"], t["PIO.UI.no_signal"] }.Distinct().Count(), Is.EqualTo(4), path);
                Assert.That(t["PIO.UI.kind_carrier"].ToLowerInvariant(), Does.Contain("sem dados").Or.Contain("no new data"), path + ": a portadora não traz dados novos");
                Assert.That(t["PIO.UI.no_fault"].ToLowerInvariant(), Does.Contain("funcionou").Or.Contain("correctly"), path + ": não culpa quem escuta");
                foreach (var key in keys)
                {
                    foreach (var forbidden in new[] { "game over", "erro", "falha", "error", "failed", "tente de novo", "try again" })
                        Assert.That(t[key].ToLowerInvariant(), Does.Not.Contain(forbidden), path + ": " + key);
                }
            }
        }

        [Test]
        public void TheDocumentHasNoLiteralSentencesAndTheAntennaIsTheDocumentedNasaModel()
        {
            var uxml = File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/ListeningPanel.uxml");
            var literals = Regex.Matches(uxml, "text=\"([^\"]*)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.That(literals, Is.EqualTo(new[] { "E" }), "textos por chave; só a tecla E é literal");
            var css = File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/ListeningPanel.uss");
            Assert.That(css, Does.Not.Contain("animation"), "sem animação por USS: nada pisca");
            Assert.That(css, Does.Contain("bottom: 16%;").And.Contain("height: 33%;"), "quadro embaixo à direita, acima da legenda de base");
            // A antena é o modelo NASA convertido (PIO-011): só o atlas derivado entra em Assets; o GLB original fica em ArtSource,
            // fora do pacote, e o manifesto da conversão prova a origem pelo SHA-256 do arquivo guardado.
            var imported = Directory.GetFiles("Assets/_Game/Chapters/Pioneer10/Art/Dsn", "*.*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")).Select(Path.GetFileName).ToList();
            Assert.That(imported.Any(f => f.EndsWith(".glb") || f.EndsWith(".jpg")), Is.False, "o original não entra em Assets");
            Assert.That(imported.Where(f => f.EndsWith(".png")), Is.EqualTo(new[] { "DsnAtlas.png" }), "só o atlas derivado");
            var manifest = File.ReadAllText("Data/Assets/Dsn70.json");
            const string original = "ArtSource/Pioneer10/PIO-011/original/Deep Space Network 70-meter.glb";
            Assert.That(File.Exists(original), Is.True, "o original da NASA está preservado");
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = string.Concat(sha.ComputeHash(File.ReadAllBytes(original)).Select(b => b.ToString("x2")));
                Assert.That(manifest, Does.Contain(hash), "o manifesto registra o SHA-256 do original");
            }

            Assert.That(manifest, Does.Contain("Kumanchik").And.Contain("science.nasa.gov/3d-resources/deep-space-network-70-meter"), "crédito e página de origem");
        }
    }
}
