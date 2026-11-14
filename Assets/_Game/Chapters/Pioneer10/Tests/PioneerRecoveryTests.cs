using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// PIO-008: interrupção em 6/8 sem falha, recuperação por um único E (toque), elipse temporal em tempo ativo, retomada
    /// no mesmo progresso, cancelamento e reentrada, e a revelação das duas imagens históricas sem piscar.
    /// </summary>
    public class PioneerRecoveryTests : InputTestFixture
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
        PioneerImaging imaging;
        ImagingRecovery recovery;
        HistoricalImageReveal reveal;
        int packets, recovered, revealed, interruptions;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            presentation = roots.SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            imaging = roots.SelectMany(r => r.GetComponentsInChildren<PioneerImaging>(true)).Single();
            recovery = roots.SelectMany(r => r.GetComponentsInChildren<ImagingRecovery>(true)).Single();
            reveal = roots.SelectMany(r => r.GetComponentsInChildren<HistoricalImageReveal>(true)).Single();

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
            System.Func<string, string> text = key => "«" + key + "»";
            imaging.Initialize(director, interaction, router, time, text);
            recovery.Initialize(interaction, time, text);
            reveal.Initialize(time, text);

            packets = recovered = revealed = interruptions = 0;
            recovery.PacketSent += () => packets++;
            recovery.Recovered += () => recovered++;
            imaging.RevealStarted += () => revealed++;
            imaging.RadiationInterruption += () => interruptions++;
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
            imaging.Tick(dt);
            recovery.Tick(dt);
            reveal.Tick(dt);
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

        /// <summary>Segura E desde o aprendizado até a interrupção em 6/8 e deixa E mantido (como um jogador real faria).</summary>
        void ReachTheInterruption(bool releaseE)
        {
            imaging.Begin();
            RunUntil(() => imaging.Phase == ImagingPhase.Learn);
            Press(keyboard.eKey);
            RunUntil(() => imaging.IsInterrupted);
            if (releaseE)
            {
                Release(keyboard.eKey);
                Step(1f / 30f);
            }
        }

        void Tap()
        {
            Press(keyboard.eKey);
            Step(1f / 30f);
            Release(keyboard.eKey);
            Step(1f / 30f);
        }

        // ---------- Recuperação ----------

        [Test]
        public void TheBuiltSceneIsWiredAndValidates()
        {
            Assert.That(recovery.ValidateSetup(), Is.Empty);
            Assert.That(reveal.ValidateSetup(), Is.Empty);
            var bare = new GameObject("bare").AddComponent<ImagingRecovery>();
            Assert.That(bare.ValidateSetup(), Is.Not.Empty, "sem referências a montagem reclama");
            Object.DestroyImmediate(bare.gameObject);
        }

        [Test]
        public void TheInterruptionAtSixOfEightShowsNoiseNoticeAndOneButtonWithoutAnyDamage()
        {
            ReachTheInterruption(releaseE: true);
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.InterruptionStrip), "as seis faixas continuam");
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Awaiting));
            Assert.That(recovery.VisibleNoiseBars, Is.EqualTo(ImagingRecovery.NoiseBars), "ruído só no quadro do instrumento");
            Assert.That(recovery.RecoveryButton, Is.Not.Null.And.Property("text").EqualTo("«PIO.UI.recover»"));
            Assert.That(interaction.TryGetActive(out var active), Is.True);
            Assert.That(active.Id, Is.EqualTo(ImagingRecovery.CandidateId), "um único botão de E");
            Assert.That(imaging.Phase, Is.EqualTo(ImagingPhase.Interrupted));
            Assert.That(interruptions, Is.EqualTo(1));
        }

        [Test]
        public void ReleasingTheHeldEDoesNotConfirmTheRecoveryByItself()
        {
            ReachTheInterruption(releaseE: false);
            Run(2f); // E ainda mantido, o mesmo que registrava as faixas
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Awaiting), "só um toque deliberado confirma");
            Assert.That(packets, Is.Zero);
            Release(keyboard.eKey);
            Run(1f);
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Awaiting));
        }

        [Test]
        public void SeveralPressesOfESendOnePacketAndRecoverOnce()
        {
            ReachTheInterruption(releaseE: true);
            for (var i = 0; i < 6; i++)
                Tap();
            Assert.That(packets, Is.EqualTo(1), "uma única recuperação");
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Sending));
            Assert.That(imaging.IsInterrupted, Is.True, "o pacote ainda viaja: a varredura não retoma na hora");

            Run(ImagingRecovery.SendSeconds + 0.5f);
            Assert.That(recovered, Is.EqualTo(1));
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Done));
            Assert.That(imaging.Phase, Is.EqualTo(ImagingPhase.Sweep), "retoma no mesmo progresso");
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.InterruptionStrip), "nenhuma faixa perdida nem reiniciada");
            Assert.That(imaging.RecoveryDone, Is.True);

            for (var i = 0; i < 4; i++)
                Tap();
            Assert.That((packets, recovered), Is.EqualTo((1, 1)), "depois de recuperar, E não envia de novo");
        }

        [Test]
        public void TheClickOnTheButtonConfirmsTheSameSingleRecovery()
        {
            ReachTheInterruption(releaseE: true);
            Assert.That(recovery.Send(), Is.True);
            Assert.That(recovery.Send(), Is.False, "a segunda confirmação é ignorada");
            Tap();
            Assert.That(packets, Is.EqualTo(1));
        }

        [Test]
        public void ThePacketTravelsOnActiveTimeSoPausingFreezesIt()
        {
            ReachTheInterruption(releaseE: true);
            recovery.Send();
            Run(1.5f);
            var progress = recovery.SendProgress;
            Assert.That(progress, Is.InRange(0.2f, 0.6f));

            for (var i = 0; i < 300; i++)
            {
                recovery.Tick(0f); // pausa: o tempo ativo é zero
                imaging.Tick(0f);
            }

            Assert.That(recovery.SendProgress, Is.EqualTo(progress), "pausar congela o pacote");
            Assert.That(imaging.IsInterrupted, Is.True);
            Run(ImagingRecovery.SendSeconds);
            Assert.That(recovered, Is.EqualTo(1), "ao retomar, termina sem perder nada");
        }

        [TestCase(15)]
        [TestCase(30)]
        [TestCase(60)]
        public void TheRecoveryTakesTheSameActiveTimeAtAnyFrameRate(int fps)
        {
            var frame = 1f / fps;
            ReachTheInterruption(releaseE: true);
            recovery.Send();
            var elapsed = 0f;
            for (; elapsed < ImagingRecovery.SendSeconds + 1f && recovered == 0; elapsed += frame)
                Step(frame);
            Assert.That(elapsed, Is.EqualTo(ImagingRecovery.SendSeconds).Within(frame + 0.01f));
        }

        [Test]
        public void CancelingWhileAwaitingOrSendingResetsAndTheNextRunInterruptsAgainExactlyOnce()
        {
            ReachTheInterruption(releaseE: true);
            imaging.Cancel();
            Step(1f / 30f);
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Idle));
            Assert.That(recovery.VisibleNoiseBars, Is.Zero, "sem ruído depois de cancelar");
            Assert.That(interaction.TryGetActive(out var none) && none.Id == ImagingRecovery.CandidateId, Is.False, "o botão some");

            ReachTheInterruption(releaseE: true);
            recovery.Send();
            Run(1f);
            imaging.Cancel();
            Step(1f / 30f);
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Idle));

            ReachTheInterruption(releaseE: true);
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Awaiting));
            Tap();
            Run(ImagingRecovery.SendSeconds + 0.5f);
            Assert.That(imaging.Phase, Is.EqualTo(ImagingPhase.Sweep));
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.InterruptionStrip));
        }

        [Test]
        public void TheWholeRouteWithTheRecoveryReachesEightOfEightAndTheRevealWithoutSoftlock()
        {
            ReachTheInterruption(releaseE: true);
            Run(5f); // jogador lendo o aviso: a sequência fica retida, sem avanço por inatividade
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Awaiting));
            Tap();
            Press(keyboard.eKey); // volta a segurar E para as duas últimas faixas
            RunUntil(() => imaging.Phase == ImagingPhase.Assembled);
            Release(keyboard.eKey);
            Assert.That(imaging.CapturedCount, Is.EqualTo(PioneerImaging.StripCount));
            RunUntil(() => revealed == 1);
            RunUntil(() => !imaging.IsRunning);
            Assert.That(recovery.Phase, Is.EqualTo(RecoveryPhase.Idle), "terminou: nada pendurado");
            Assert.That(recovered, Is.EqualTo(1));
        }

        // ---------- Revelação ----------

        [Test]
        public void TheRevealFollowsTheSubtitlesOfTheSequence()
        {
            var cues = imaging.Sequence.Cues.ToDictionary(c => c.TextKey);
            Assert.That(cues["PIO.04.06"].StartSeconds, Is.EqualTo(48f), "a revelação começa em 48 s");
            Assert.That(48f + HistoricalImageReveal.JupiterAtSeconds, Is.EqualTo(cues["PIO.04.07"].StartSeconds), "Júpiter com a legenda que fala de Io");
            Assert.That(48f + HistoricalImageReveal.GanymedeAtSeconds, Is.EqualTo(cues["PIO.04.08"].StartSeconds), "Ganimedes com a legenda de Ganimedes");
            Assert.That(48f + HistoricalImageReveal.GanymedeAtSeconds + HistoricalImageReveal.FadeSeconds, Is.LessThan(cues["PIO.04.09"].EndSeconds));
        }

        [Test]
        public void TheCrossfadesAreOneSecondEachMonotonicAndNeverFlash()
        {
            Assert.That(HistoricalImageReveal.Evaluate(0f).Visible, Is.False);
            Assert.That(HistoricalImageReveal.Evaluate(HistoricalImageReveal.JupiterAtSeconds - 0.01f).Visible, Is.False);
            var mid = HistoricalImageReveal.Evaluate(HistoricalImageReveal.JupiterAtSeconds + 0.5f);
            Assert.That(mid.Jupiter, Is.EqualTo(0.5f).Within(0.01f), "crossfade de 1 s");
            Assert.That(mid.IoLost, Is.True);

            var done = HistoricalImageReveal.Evaluate(HistoricalImageReveal.GanymedeAtSeconds + HistoricalImageReveal.FadeSeconds);
            Assert.That((done.Jupiter, done.Ganymede, done.Frame), Is.EqualTo((1f, 1f, 1f)));
            Assert.That(done.IoLost, Is.False, "Io é lembrada só enquanto Júpiter está à vista");

            var previous = HistoricalImageReveal.Evaluate(0f);
            var direction = 0;
            for (var t = 0f; t <= 26f; t += 1f / 60f)
            {
                var state = HistoricalImageReveal.Evaluate(t);
                Assert.That(state.Frame, Is.GreaterThanOrEqualTo(previous.Frame - 1e-6f), "o quadro só aparece: sem piscar");
                Assert.That(state.Jupiter, Is.GreaterThanOrEqualTo(previous.Jupiter - 1e-6f));
                Assert.That(state.Ganymede, Is.GreaterThanOrEqualTo(previous.Ganymede - 1e-6f));
                Assert.That(state.Frame - previous.Frame, Is.LessThan(0.1f), "nenhum salto de luminosidade entre quadros");
                direction += state.Frame > previous.Frame + 1e-6f ? 1 : 0;
                previous = state;
            }

            Assert.That(direction, Is.GreaterThan(0));
        }

        [Test]
        public void TheRevealStartsWithTheSequenceFreezesOnPauseAndStopsWhenCanceled()
        {
            Assert.That(reveal.IsRevealing, Is.False);
            ReachTheInterruption(releaseE: true);
            recovery.Send();
            Release(keyboard.eKey);
            Run(ImagingRecovery.SendSeconds + 0.5f);
            Press(keyboard.eKey);
            RunUntil(() => revealed == 1);
            Release(keyboard.eKey);
            Assert.That(reveal.IsRevealing, Is.True);
            Run(HistoricalImageReveal.JupiterAtSeconds + 2f);
            Assert.That(reveal.ViewState().Jupiter, Is.EqualTo(1f).Within(1e-4f));

            var clock = reveal.Clock;
            for (var i = 0; i < 200; i++)
                reveal.Tick(0f); // pausa
            Assert.That(reveal.Clock, Is.EqualTo(clock), "pausar congela a revelação");

            imaging.Cancel();
            reveal.Tick(1f / 30f);
            Assert.That(reveal.IsRevealing, Is.False);
            Assert.That(reveal.ViewState().Visible, Is.False, "cancelar esconde as fotos");
        }

        [Test]
        public void TheRevealCarriesTheRealImagesTheirCreditsAndDatesInBothLanguages()
        {
            var jupiter = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(PioneerHistoricalImagesImporter.Files[1]);
            var ganymede = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(PioneerHistoricalImagesImporter.Files[0]);
            Assert.That(jupiter, Is.Not.Null);
            Assert.That(ganymede, Is.Not.Null);
            Assert.That(ganymede.width, Is.LessThanOrEqualTo(1024), "resolução modesta preservada, sem ampliar");

            foreach (var path in new[] { "Data/Localization/Pioneer10.pt-BR.json", "Data/Localization/Pioneer10.en.json" })
            {
                var entries = JsonUtility.FromJson<Locale>(File.ReadAllText(path)).entries.ToDictionary(e => e.key, e => e.text);
                foreach (var key in new[] { HistoricalImageReveal.RealKey, HistoricalImageReveal.JupiterKey, HistoricalImageReveal.GanymedeKey, HistoricalImageReveal.IoLostKey,
                                            ImagingRecovery.RadiationKey, ImagingRecovery.RecoverKey, ImagingRecovery.SentKey })
                    Assert.That(entries.ContainsKey(key) && !string.IsNullOrWhiteSpace(entries[key]), Is.True, path + ": " + key);

                Assert.That(entries[HistoricalImageReveal.RealKey], Does.Contain("NASA").And.Contain("1973").And.Contain("Pioneer 10"), path);
                Assert.That(entries[HistoricalImageReveal.GanymedeKey], Does.Contain("1973").And.Contain("3"), path + ": data de Ganimedes");
                Assert.That(entries[HistoricalImageReveal.JupiterKey], Does.Contain("1973"), path);
                Assert.That(entries[HistoricalImageReveal.JupiterKey].ToLowerInvariant(), Does.Contain("montage").Or.Contain("montagem"), path + ": é uma montagem, não uma foto única");
                var ioLost = entries[HistoricalImageReveal.IoLostKey].ToLowerInvariant();
                Assert.That(ioLost, Does.Contain("io"), path);
                Assert.That(ioLost, Does.Contain("perdida").Or.Contain("lost"), path);
                Assert.That(entries[ImagingRecovery.RadiationKey].ToLowerInvariant(), Does.Contain("radia"), path);
                Assert.That(entries[ImagingRecovery.SentKey], Does.Not.Contain("instant"), path + ": sem sugerir comunicação instantânea");
            }
        }

        [Test]
        public void TheRevealDocumentUsesTheSameFrameAsTheSweepAndNoLiteralSentences()
        {
            var uxml = File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/HistoricalImageReveal.uxml");
            Assert.That(uxml, Does.Not.Contain("text=\""), "textos por chave: nenhuma frase literal no UXML");
            var css = File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/HistoricalImageReveal.uss");
            var panelCss = File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/ImagingPanel.uss");
            foreach (var geometry in new[] { "left: 53%;", "top: 12%;", "width: 43%;", "height: 76%;" })
            {
                Assert.That(css, Does.Contain(geometry), geometry);
                Assert.That(panelCss, Does.Contain(geometry), geometry);
            }

            Assert.That(css, Does.Not.Contain("animation"), "sem animação por USS");
            Assert.That(File.ReadAllText("Assets/_Game/Chapters/Pioneer10/UI/ImagingRecovery.uss"), Does.Not.Contain("animation"));
        }
    }
}
