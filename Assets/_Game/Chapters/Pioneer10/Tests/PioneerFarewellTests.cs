using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// PIO-012: despedida de 38 s e conclusão. Teclado simulado e relógio ativo manual: planos com rigs diferentes, guia de
    /// Aldebaran, preto do memorial, conclusão uma vez em 36 s, ações finais por setas + E/Enter, E mantido de antes,
    /// pausa, movimento reduzido, reinício na troca das antenas e o memorial sem relógio nem capítulo automático.
    /// </summary>
    public class PioneerFarewellTests : InputTestFixture
    {
        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        const float Frame = 1f / 30f;
        Keyboard keyboard;
        GameObject host;
        InputRouter router;
        InteractionService interaction;
        SequenceDirector director;
        PioneerPresentation presentation;
        PioneerFarewellActivity farewell;
        int completed, returns, replays;
        List<string> cues;
        List<string> shots;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            farewell = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerFarewellActivity>(true)).Single();

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
            farewell.Initialize(director, interaction, router, time, key => "«" + key + "»");

            completed = returns = replays = 0;
            cues = new List<string>();
            shots = new List<string>();
            farewell.Completed += () => completed++;
            farewell.ReturnToArchiveRequested += () => returns++;
            farewell.ReplayRequested += () => replays++;
            director.CueEntered += cue => cues.Add(cue.CueId);
            director.ShotStarted += id => shots.Add(id);
        }

        public override void TearDown()
        {
            farewell.SetReducedMotion(false);
            if (director.IsPlaying)
                director.Cancel();
            farewell.Cancel();
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
            farewell.Tick(dt);
        }

        void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame)
                Step(Frame);
        }

        void RunUntilTime(float sequenceSeconds)
        {
            for (var guard = 0; guard < 6000 && director.TimeSeconds < sequenceSeconds; guard++)
                Step(Frame);
            Assert.That(director.TimeSeconds, Is.GreaterThanOrEqualTo(sequenceSeconds - 1e-3f));
        }

        void RunUntil(System.Func<bool> condition, float limitSeconds = 120f)
        {
            for (var t = 0f; t < limitSeconds && !condition(); t += Frame)
                Step(Frame);
            Assert.That(condition(), Is.True, "condição não alcançada");
        }

        void Tap()
        {
            Press(keyboard.eKey);
            Run(0.1f);
            Release(keyboard.eKey);
            Run(Frame);
        }

        void Arrow(Key key)
        {
            var control = (UnityEngine.InputSystem.Controls.KeyControl)keyboard[key];
            Press(control);
            Step(Frame);
            Release(control);
            Step(Frame);
        }

        // ---------- Sequência, montagem e dados ----------

        [Test]
        public void TheSequenceHasSixShotsWithDifferentRigsTheFiveMarksAndTheFourCues()
        {
            var sequence = farewell.Sequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(director.Validate(sequence), Is.Empty);
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(38f).Within(1e-4f));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 4f, 4f, 6f, 6f, 10f, 8f }));

            var rigs = sequence.Shots.Select(s => s.RigId).ToArray();
            for (var i = 1; i < rigs.Length; i++)
            {
                if (rigs[i] == rigs[i - 1])
                    Assert.That(sequence.Shots[i].ContinueFromPreviousShot, Is.True, $"plano {i}: o mesmo rig só com continuidade (o preto final)");
            }

            Assert.That(rigs.Distinct().Count(), Is.GreaterThanOrEqualTo(4), "quatro enquadramentos diferentes: antena, placa, plano aberto e a Pioneer diminuindo");
            Assert.That(rigs.Take(5), Is.EqualTo(new[] { "PIO.Listening", "PIO.Launch.Reveal", "PIO.Plaque", "PIO.Farewell", "PIO.Outbound" }));
            Assert.That(sequence.Cues.Select(c => (c.CueId, c.StartSeconds, c.EndSeconds)), Is.EqualTo(new[]
            {
                ("PIO.08.01", 0f, 8f), ("PIO.08.02", 9f, 17f), ("PIO.08.03", 18f, 26f), ("PIO.08.04", 27f, 35f),
            }));
        }

        [Test]
        public void TheRecedingAndMemorialShotsMoveTheCameraAwayFromThePioneerNeverTowardIt()
        {
            // O eixo Z local do rig aponta para a sonda (o anchor olha para ela): z negativo é recuar. Um z positivo faria a
            // Pioneer CRESCER no quadro, o contrário do roteiro ("a Pioneer diminui"); só o enquadramento 3D mostrou o erro.
            var shots = farewell.Sequence.Shots;
            var receding = shots.Single(s => s.ShotId == "PIO08.Receding");
            var memorial = shots.Single(s => s.ShotId == "PIO08.Memorial");
            Assert.That(receding.Keyframes[receding.Keyframes.Count - 1].positionMeters.z, Is.LessThan(-10f), "recuo claro durante os 10 s");
            Assert.That(memorial.Keyframes[memorial.Keyframes.Count - 1].positionMeters.z,
                Is.LessThanOrEqualTo(receding.Keyframes[receding.Keyframes.Count - 1].positionMeters.z), "o preto continua o recuo, sem voltar");
        }

        [Test]
        public void ValidateSetupIsEmptyOnTheBuiltSceneAndComplainsAboutMissingReferences()
        {
            Assert.That(farewell.ValidateSetup(), Is.Empty);

            var bare = new GameObject("bare").AddComponent<PioneerFarewellActivity>();
            var errors = bare.ValidateSetup();
            Object.DestroyImmediate(bare.gameObject);
            Assert.That(errors.Any(e => e.Contains("sequence")), Is.True);
            Assert.That(errors.Any(e => e.Contains("presentation")), Is.True);
            Assert.That(errors.Any(e => e.Contains("panel")), Is.True);
        }

        [Test]
        public void ThePanelPrefabHasTheResponsiveClassAndEveryElementTheViewUses()
        {
            var panel = Object.FindAnyObjectByType<FarewellPanelView>(FindObjectsInactive.Include);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.GetComponent<Abandoned.SpaceApps.UI.ResponsiveLayoutClass>(), Is.Not.Null, "UI_SPEC: todo UIDocument de tela cheia");
            Assert.That(panel.ValidateSetup(), Is.Empty);
        }

        [Test]
        public void EveryFarewellTextHasPortugueseAndEnglishAndTheMemorialSplitsInTwoLines()
        {
            var data = AssetDatabase.LoadAssetAtPath<PioneerData>(PioneerDataBuilder.DataAssetPath);
            foreach (var key in new[] { "PIO.08.01", "PIO.08.02", "PIO.08.03", "PIO.08.04", FarewellPanelView.GuideKey, FarewellPanelView.EndingKey, FarewellPanelView.ReturnKey, FarewellPanelView.ReplayKey })
            {
                Assert.That(data.GetText(key, PioneerData.Portuguese), Is.Not.Empty, key + " PT");
                Assert.That(data.GetText(key, PioneerData.English), Is.Not.Empty, key + " EN");
            }

            var pt = FarewellPanelView.SplitEnding(data.GetText(FarewellPanelView.EndingKey, PioneerData.Portuguese));
            Assert.That(pt.title, Is.EqualTo("PIONEER 10 · 1972–2003"));
            Assert.That(pt.subtitle, Is.EqualTo("Em viagem"));
            var en = FarewellPanelView.SplitEnding(data.GetText(FarewellPanelView.EndingKey, PioneerData.English));
            Assert.That(en.title, Is.EqualTo("PIONEER 10 · 1972–2003"));
            Assert.That(en.subtitle, Is.EqualTo("Still traveling"));
            Assert.That(FarewellPanelView.SplitEnding("Só o título"), Is.EqualTo(("Só o título", string.Empty)), "nunca inventa a segunda linha");
            Assert.That(FarewellPanelView.SplitEnding(null), Is.EqualTo((string.Empty, string.Empty)));
            Assert.That(data.GetText(FarewellPanelView.GuideKey, PioneerData.Portuguese), Does.Contain("direção").IgnoreCase.And.Contain("Aldebaran"), "direção geral, não chegada");
            Assert.That(data.GetText(FarewellPanelView.GuideKey, PioneerData.English), Does.Contain("irection").And.Contain("Aldebaran"));
        }

        // ---------- A cena e o memorial ----------

        [Test]
        public void TheSceneRunsTheGuideThenBlackThenCompletesOnceAtThirtySixAndKeepsTheMemorialOpen()
        {
            farewell.Begin();
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Playing));
            Assert.That(farewell.ViewState().GuideVisible, Is.False);

            RunUntilTime(10f);
            Assert.That(farewell.ViewState().GuideVisible, Is.False, "a direção de Aldebaran só aparece no plano aberto");
            RunUntilTime(15f);
            Assert.That(farewell.ViewState().GuideVisible, Is.True);
            RunUntilTime(21f);
            Assert.That(farewell.ViewState().GuideVisible, Is.False, "o guia some quando a Pioneer começa a diminuir");

            RunUntilTime(29f);
            Assert.That(farewell.ViewState().BlackAlpha, Is.Zero);
            RunUntilTime(30.5f);
            var rising = farewell.ViewState();
            Assert.That(rising.BlackAlpha, Is.InRange(0.1f, 0.9f), "o preto sobe com o tempo ativo");
            Assert.That(rising.EndingVisible, Is.False, "o texto só aparece com o preto cheio");
            RunUntilTime(32.5f);
            var black = farewell.ViewState();
            Assert.That(black.BlackAlpha, Is.EqualTo(1f));
            Assert.That(black.EndingVisible, Is.True);
            Assert.That(black.ChoicesVisible, Is.False, "as ações só nos últimos 2 s");
            Assert.That(completed, Is.Zero);

            RunUntilTime(36.2f);
            Assert.That(farewell.ViewState().ChoicesVisible, Is.True);
            Assert.That(completed, Is.EqualTo(1), "conclui quando o memorial abre");
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Memorial));
            Assert.That(router.Context, Is.EqualTo(InputContext.Panel));

            RunUntil(() => !director.IsPlaying);
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Memorial), "a sequência acaba, o memorial fica");
            Assert.That(cues, Is.EqualTo(new[] { "PIO.08.01", "PIO.08.02", "PIO.08.03", "PIO.08.04" }), "uma legenda de cada vez, sem caixa vazia entre elas");
            Assert.That(shots.Count, Is.EqualTo(6));
        }

        [Test]
        public void TheMemorialHasNoClockAndNeverStartsAnotherChapterOnItsOwn()
        {
            farewell.Begin();
            RunUntil(() => farewell.Phase == FarewellPhase.Memorial);
            RunUntil(() => !director.IsPlaying);
            var before = farewell.ViewState();
            Run(600f); // dez minutos parado na tela de conclusão
            var after = farewell.ViewState();
            Assert.That(after.Phase, Is.EqualTo(FarewellPhase.Memorial));
            Assert.That(after.ChoicesVisible, Is.True);
            Assert.That(after.Focus, Is.EqualTo(before.Focus));
            Assert.That((completed, returns, replays), Is.EqualTo((1, 0, 0)), "nada acontece sozinho: nem outro capítulo, nem fechar o memorial");
            Assert.That(farewell.Choice, Is.EqualTo(FarewellChoice.None));
        }

        [Test]
        public void ArrowsMoveTheFocusAndETapOrEnterActivatesOnlyTheFirstChoice()
        {
            farewell.Begin();
            RunUntilTime(36.3f);
            Assert.That(farewell.Focus, Is.EqualTo(0), "o foco começa em Voltar ao arquivo");
            Assert.That(interaction.TryGetActive(out var candidate), Is.True);
            Assert.That(candidate.VerbKey, Is.EqualTo(PioneerFarewellActivity.ReturnKey), "o prompt fala do botão em foco");

            Arrow(Key.RightArrow);
            Assert.That(farewell.Focus, Is.EqualTo(1));
            Assert.That(interaction.TryGetActive(out candidate), Is.True);
            Assert.That(candidate.VerbKey, Is.EqualTo(PioneerFarewellActivity.ReplayKey));
            Arrow(Key.LeftArrow);
            Assert.That(farewell.Focus, Is.EqualTo(0));
            Arrow(Key.RightArrow);
            Arrow(Key.RightArrow);
            Assert.That(farewell.Focus, Is.EqualTo(0), "o foco dá a volta entre as duas ações");

            Arrow(Key.RightArrow); // foco em Repetir
            Tap();
            Assert.That(farewell.Choice, Is.EqualTo(FarewellChoice.Replay));
            Assert.That((replays, returns), Is.EqualTo((1, 0)));
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Done));
            Assert.That(farewell.Choose(FarewellChoice.ReturnToArchive), Is.False, "só a primeira escolha vale");
            Assert.That(farewell.ActivateFocused(), Is.False);
            Assert.That((replays, returns), Is.EqualTo((1, 0)));
            Assert.That(router.DescribeLeases(), Does.Not.Contain("PIO.Farewell"), "a escolha solta a entrada da despedida");
            Assert.That(interaction.TryGetActive(out _), Is.False);
            RunUntil(() => !director.IsPlaying); // o fim da sequência (38 s) solta o último lease
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That((replays, returns), Is.EqualTo((1, 0)));
        }

        [Test]
        public void EnterAndAHoldOfHalfASecondAlsoActivateAndNeverTwice()
        {
            farewell.Begin();
            RunUntilTime(36.3f);
            Press(keyboard.enterKey);
            Step(Frame);
            Release(keyboard.enterKey);
            Step(Frame);
            Assert.That(farewell.Choice, Is.EqualTo(FarewellChoice.ReturnToArchive), "Enter ativa o botão em foco");
            Assert.That((returns, replays), Is.EqualTo((1, 0)));

            farewell.Cancel();
            farewell.Begin();
            RunUntilTime(36.3f);
            Press(keyboard.eKey);
            Run(PioneerFarewellActivity.ActivateHoldSeconds + 0.3f);
            Release(keyboard.eKey);
            Step(Frame);
            Assert.That(farewell.Choice, Is.EqualTo(FarewellChoice.ReturnToArchive), "segurar E também ativa");
            Assert.That(returns, Is.EqualTo(2), "uma por execução: o hold concluído não repete no soltar");
        }

        [Test]
        public void AnEHeldFromBeforeTheChoicesOpenActivatesNothingUntilReleasedAndPressedAgain()
        {
            farewell.Begin();
            RunUntilTime(31f);
            Press(keyboard.eKey); // E mantido durante o preto
            RunUntilTime(37.5f);
            Run(1f);
            Assert.That(farewell.Choice, Is.EqualTo(FarewellChoice.None), "o E antigo não escolhe por si");
            Assert.That((returns, replays), Is.EqualTo((0, 0)));
            Release(keyboard.eKey);
            Step(Frame);
            Tap();
            Assert.That(farewell.Choice, Is.EqualTo(FarewellChoice.ReturnToArchive), "depois de soltar e pressionar de novo, funciona");
        }

        [Test]
        public void PausingFreezesTheBlackFadeAndReducedMotionShowsTheBlackAtOnce()
        {
            farewell.Begin();
            RunUntilTime(30.4f);
            var before = farewell.ViewState().BlackAlpha;
            Assert.That(before, Is.InRange(0.05f, 0.95f));
            for (var i = 0; i < 120; i++)
                Step(0f); // pausa: tempo ativo zero
            Assert.That(farewell.ViewState().BlackAlpha, Is.EqualTo(before), "o preto congela na pausa");

            farewell.Cancel();
            farewell.SetReducedMotion(true);
            farewell.Begin();
            RunUntilTime(30.2f);
            Assert.That(farewell.ViewState().BlackAlpha, Is.EqualTo(1f), "movimento reduzido: sem fade");
        }

        [Test]
        public void CancelingDuringTheAntennaCutAndRestartingStartsCleanWithNoLeaks()
        {
            farewell.Begin();
            RunUntilTime(4.2f); // logo depois do match cut
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Playing));
            farewell.Cancel();
            Assert.That(farewell.IsRunning, Is.False);
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Idle));
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(completed, Is.Zero);

            cues.Clear();
            farewell.Begin();
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Playing));
            Assert.That(farewell.ViewState().BlackAlpha, Is.Zero);
            Assert.That(farewell.ValidateSetup(), Is.Empty);
            RunUntilTime(1f);
            Assert.That(cues, Is.EqualTo(new[] { "PIO.08.01" }), "sem legenda da execução anterior");
        }

        [Test]
        public void CancelingWithTheMemorialOpenClosesItWithoutRaisingAChoice()
        {
            farewell.Begin();
            RunUntilTime(36.3f);
            farewell.Cancel();
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Idle));
            Assert.That(farewell.ViewState().ChoicesVisible, Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False);
            Assert.That((returns, replays), Is.EqualTo((0, 0)));
            Assert.That(farewell.ActivateFocused(), Is.False, "nada a ativar depois de fechar");
        }

        [Test]
        public void SkippingOpensTheMemorialAndCompletesOnce()
        {
            farewell.Begin(skippable: true);
            Run(5f);
            Assert.That(director.Skip(), Is.True);
            Run(1f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Memorial));
            Assert.That(farewell.ViewState().ChoicesVisible, Is.True);
            Assert.That(farewell.ViewState().EndingVisible, Is.True);
            Assert.That(farewell.ActivateFocused(), Is.True);
            Assert.That(returns, Is.EqualTo(1));
        }

        [Test]
        public void TheActivityNeverMovesHidesOrDestroysTheProbeOrThePlaque()
        {
            // O rig é só de câmera: a atividade não toca o prefab da sonda nem a placa 3D (continua presa à sonda).
            var source = System.IO.File.ReadAllText("Assets/_Game/Chapters/Pioneer10/Runtime/PioneerFarewellActivity.cs");
            Assert.That(source, Does.Not.Contain("SetActive").And.Not.Contain("Object.Destroy").And.Not.Contain("DestroyImmediate").And.Not.Contain("SetParent"));
        }
    }
}
