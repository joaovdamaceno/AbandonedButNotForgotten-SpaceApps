using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Roteiro v2, cena 8 (versão final): a sintonia do rádio sem tecla E. A primeira rodada abre com a cena, só as setas sintonizam, a escuta
    /// é automática dentro da zona, cada contato tem a zona mais estreita e o sinal mais fraco, e a próxima rodada abre sozinha.
    /// </summary>
    public class PioneerAutoListenTests : InputTestFixture
    {
        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        Keyboard keyboard;
        GameObject host;
        InputRouter router;
        InteractionService interaction;
        SequenceDirector director;
        PioneerPresentation presentation;
        PioneerListening listening;
        int completed, noSignal;
        readonly List<int> contacts = new List<int>();

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
            listening.AutoListenMode = true;

            completed = noSignal = 0;
            contacts.Clear();
            listening.Completed += () => completed++;
            listening.NoSignalHeard += () => noSignal++;
            listening.ContactMade += n => contacts.Add(n);
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

        void RunUntil(System.Func<bool> condition, float limitSeconds = 120f, float frame = 1f / 30f)
        {
            for (var t = 0f; t < limitSeconds && !condition(); t += frame)
                Step(frame);
            Assert.That(condition(), Is.True, "condição não alcançada");
        }

        /// <summary>Move o botão com as setas até o centro da zona e solta.</summary>
        void TuneIntoZone()
        {
            for (var guard = 0; guard < 600; guard++)
            {
                var view = listening.ViewState();
                var target = (view.ZoneStart + view.ZoneEnd) * 0.5f;
                if (Mathf.Abs(listening.Knob - target) < 0.02f)
                    break;
                if (listening.Knob < target)
                {
                    Release(keyboard.leftArrowKey);
                    Press(keyboard.rightArrowKey);
                }
                else
                {
                    Release(keyboard.rightArrowKey);
                    Press(keyboard.leftArrowKey);
                }

                Step(1f / 30f);
            }

            Release(keyboard.leftArrowKey);
            Release(keyboard.rightArrowKey);
            Step(1f / 30f);
        }

        [Test]
        public void TheFirstRoundIsOpenAssoonAsTheSceneStartsAndNeedsNoE()
        {
            listening.Begin();
            Assert.That(listening.Phase, Is.EqualTo(ListeningPhase.Contact1), "nada de ensino parado: já dá para sintonizar");
            var view = listening.ViewState();
            Assert.That(view.AutoListen, Is.True);
            Assert.That(view.ZoneVisible, Is.True);

            TuneIntoZone();
            Assert.That(listening.ViewState().InZone, Is.True);
            RunUntil(() => contacts.Count == 1, 6f);
            Assert.That(listening.Phase, Is.EqualTo(ListeningPhase.Contact1));
            Assert.That(listening.ViewState().Result, Is.EqualTo(ListeningResult.Signal), "sem apertar E: a escuta é automática dentro da zona");
        }

        [Test]
        public void TheZonesGetNarrowerAndTheSignalWeakerRoundAfterRound()
        {
            for (var round = 1; round < PioneerListening.ContactCount; round++)
            {
                Assert.That(PioneerListening.AutoZoneWidths[round], Is.LessThan(PioneerListening.AutoZoneWidths[round - 1]), "a barra amarela encolhe");
                Assert.That(PioneerListening.AutoSignals[round], Is.LessThan(PioneerListening.AutoSignals[round - 1]), "o sinal enfraquece");
            }

            for (var round = 0; round < PioneerListening.ContactCount; round++)
            {
                var start = PioneerListening.AutoZoneStarts[round];
                var end = start + PioneerListening.AutoZoneWidths[round];
                var knob = PioneerListening.AutoStartKnobs[round];
                var gap = knob < start ? start - knob : knob > end ? knob - end : 0f;
                Assert.That(gap, Is.LessThanOrEqualTo(0.15f), $"rodada {round + 1}: o botão começa perto da zona");
                Assert.That(end, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void TheNextRoundOpensItselfAfterTheResultAndTheZoneIsNarrower()
        {
            listening.Begin();
            TuneIntoZone();
            RunUntil(() => contacts.Count == 1, 6f);
            var first = listening.ViewState();
            var firstWidth = first.ZoneEnd - first.ZoneStart;
            Assert.That(listening.Phase, Is.EqualTo(ListeningPhase.Contact1), "o resultado fica um instante na tela");
            RunUntil(() => listening.Phase == ListeningPhase.Contact2, PioneerListening.NextRoundDelaySeconds + 20f);
            var second = listening.ViewState();
            Assert.That(second.ZoneEnd - second.ZoneStart, Is.LessThan(firstWidth - 0.05f));
            Assert.That(second.Signal, Is.LessThan(first.Signal));
            Assert.That(second.Result, Is.EqualTo(ListeningResult.None));
        }

        [Test]
        public void TheFourRoundsFinishWithOnlyTheArrowKeys()
        {
            listening.Begin();
            for (var contact = 1; contact <= 3; contact++)
            {
                RunUntil(() => listening.Phase == ListeningPhase.Contact1 + (contact - 1) && listening.ViewState().Result == ListeningResult.None, 60f);
                TuneIntoZone();
                RunUntil(() => contacts.Count == contact, 8f);
            }

            RunUntil(() => listening.Phase == ListeningPhase.Attempt4, 60f);
            RunUntil(() => noSignal == 1, 8f);
            Assert.That(listening.ViewState().Result, Is.EqualTo(ListeningResult.NoSignal), "a quarta tentativa também é automática, e não há sinal");
            RunUntil(() => completed == 1, 120f);
            Assert.That(contacts, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void StandingStillOutsideTheZoneEventuallyBringsTheAntennaCloserWithoutPenalty()
        {
            listening.Begin();
            var start = listening.Knob;
            var view = listening.ViewState();
            Assert.That(listening.Knob, Is.LessThan(view.ZoneStart), "o botão começa fora da zona");
            for (var t = 0f; t < PioneerListening.AutoAssistSeconds - 1f; t += 1f / 30f)
                Step(1f / 30f);
            Assert.That(listening.Knob, Is.EqualTo(start).Within(1e-4f), "antes de 15 s nada se mexe sozinho");
            RunUntil(() => listening.ViewState().Assisting, 5f);
            RunUntil(() => listening.ViewState().InZone, 20f);
            Assert.That(contacts, Is.Empty, "a ajuda só aproxima; a escuta ainda precisa dos 2 s dentro da zona");
        }

        [Test]
        public void ThePromptOnlyMentionsTheArrowsAndTheWordsSayStrengthNotNearOrFar()
        {
            var state = new ListeningViewState { Phase = ListeningPhase.Contact1, AutoListen = true, ZoneVisible = true };
            Assert.That(ListeningPanelView.HoldLabelKey(state), Is.EqualTo(ListeningPanelView.TuneAutoKey));
            state.InZone = true;
            Assert.That(ListeningPanelView.HoldLabelKey(state), Is.EqualTo(ListeningPanelView.ReceivingKey));
            state.AutoListen = false;
            Assert.That(ListeningPanelView.HoldLabelKey(state), Is.EqualTo(PioneerListening.TunePromptKey), "o modo antigo continua com E");

            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<PioneerData>(PioneerDataBuilder.DataAssetPath);
            foreach (var language in new[] { PioneerData.Portuguese, PioneerData.English })
            {
                foreach (var key in new[] { ListeningPanelView.NearerKey, ListeningPanelView.FartherKey })
                    Assert.That(data.GetText(key, language).ToLowerInvariant(), Does.Contain(language == PioneerData.Portuguese ? "intensidade" : "strength"), key + " " + language);
                Assert.That(data.GetText(ListeningPanelView.TuneAutoKey, language), Does.Not.Contain("E ·"), "sem E no prompt");
            }
        }

        [Test]
        public void TheWaveShowsAStrongToneThenDataThenAThinCarrierThenOnlyNoise()
        {
            float Amplitude(ListeningResult result, int contact)
            {
                var state = new ListeningViewState { Phase = ListeningPhase.Contact1 + contact, Result = result, Signal = PioneerListening.AutoSignals[Mathf.Min(contact, 2)], AutoListen = true };
                ListeningPanelView.WaveParams(state, out var amplitude, out _, out _);
                return amplitude;
            }

            var signal = Amplitude(ListeningResult.Signal, 0);
            var data = Amplitude(ListeningResult.Data, 1);
            var carrier = Amplitude(ListeningResult.Carrier, 2);
            var none = Amplitude(ListeningResult.NoSignal, 2);
            Assert.That(signal, Is.GreaterThan(data));
            Assert.That(data, Is.GreaterThan(carrier));
            Assert.That(carrier, Is.GreaterThan(0.1f), "a portadora é fraca mas se vê");
            Assert.That(none, Is.Zero);

            ListeningPanelView.WaveParams(new ListeningViewState { Result = ListeningResult.Data, Signal = 0.5f }, out _, out _, out var blocks);
            Assert.That(blocks, Is.True, "os dados vêm em blocos");

            // O traço nunca sai de -1..1 e a sintonia melhora o sinal e abaixa o ruído antes do resultado.
            for (var i = 0; i <= 100; i++)
            {
                var x = i / 100f;
                Assert.That(Mathf.Abs(ListeningPanelView.WaveNoise(x, 1.3f)), Is.LessThanOrEqualTo(1f));
                Assert.That(Mathf.Abs(ListeningPanelView.WaveSignal(1f, false, x, 1.3f)), Is.LessThanOrEqualTo(1f));
            }

            var far = new ListeningViewState { Phase = ListeningPhase.Contact1, Proximity = 0.1f, Strength = 0.08f };
            var near = new ListeningViewState { Phase = ListeningPhase.Contact1, Proximity = 1f, Strength = 0.8f };
            ListeningPanelView.WaveParams(far, out var farAmplitude, out var farNoise, out _);
            ListeningPanelView.WaveParams(near, out var nearAmplitude, out var nearNoise, out _);
            Assert.That(nearAmplitude, Is.GreaterThan(farAmplitude));
            Assert.That(nearNoise, Is.LessThan(farNoise));
        }

        [Test]
        public void TheListeningCameraLooksUpSoTheBlackGroundStaysOutOfTheFrame()
        {
            presentation.RegisterWithCameraDirector();
            Assert.That(presentation.TryGetAnchor(PioneerListening.StateId, out var anchor), Is.True);
            var forward = anchor.pose.rotation * Vector3.forward;
            var pitch = Mathf.Asin(forward.y) * Mathf.Rad2Deg;
            var bottomElevation = pitch - anchor.fieldOfViewDegrees * 0.5f;
            Assert.That(anchor.pose.position.y, Is.LessThan(3f), "câmera à altura de uma pessoa");
            Assert.That(bottomElevation, Is.GreaterThanOrEqualTo(0f), "a borda de baixo do quadro está acima do horizonte: nenhum chão aparece");
        }

        [Test]
        public void ValidateSetupIsEmptyOnTheBuiltScene()
        {
            Assert.That(listening.ValidateSetup(), Is.Empty);
        }
    }
}
