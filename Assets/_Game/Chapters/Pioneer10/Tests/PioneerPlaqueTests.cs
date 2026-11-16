using System.Collections.Generic;
using System.IO;
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
    /// PIO-009: placa de cinco peças. Teclado simulado e relógio ativo manual: setas + E, cliques, arrastar com snap de 96 px,
    /// ajuda (segurar E por 2 s), contorno errado sem perda, rearme, pausa, marco 5/5, reinício em 3/5 sem vazar e arte
    /// (SVG e PNGs da mesma fonte).
    /// </summary>
    public class PioneerPlaqueTests : InputTestFixture
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
        PioneerPlaqueActivity plaque;
        int completed, all, reveals, wrong;
        List<(int piece, int count)> fitted;
        List<string> cues;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            plaque = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPlaqueActivity>(true)).Single();

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
            plaque.Initialize(director, interaction, router, time, key => "«" + key + "»");

            completed = all = reveals = wrong = 0;
            fitted = new List<(int, int)>();
            cues = new List<string>();
            plaque.Completed += () => completed++;
            plaque.AllPiecesFitted += () => all++;
            plaque.RevealStarted += () => reveals++;
            plaque.WrongOutline += (p, o) => wrong++;
            plaque.PieceFitted += (piece, count) => fitted.Add((piece, count));
            director.CueEntered += cue => cues.Add(cue.CueId);
        }

        public override void TearDown()
        {
            plaque.SetReducedMotion(false);
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
            plaque.Tick(dt);
        }

        void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame)
                Step(Frame);
        }

        void RunUntil(System.Func<bool> condition, float limitSeconds = 120f)
        {
            for (var t = 0f; t < limitSeconds && !condition(); t += Frame)
                Step(Frame);
            Assert.That(condition(), Is.True, "condição não alcançada");
        }

        void EnterFit()
        {
            plaque.Begin();
            RunUntil(() => plaque.Phase == PlaquePhase.Fit);
        }

        /// <summary>Toque no E: pressiona e solta antes do limite de toque.</summary>
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

        /// <summary>Percorre com a seta direita até o foco chegar (no máximo uma volta e meia).</summary>
        void FocusPieceWithArrows(int piece)
        {
            for (var i = 0; i < 8 && plaque.FocusPiece != piece; i++)
                Arrow(Key.RightArrow);
            Assert.That(plaque.FocusPiece, Is.EqualTo(piece));
        }

        void FocusOutlineWithArrows(int outline)
        {
            for (var i = 0; i < 8 && plaque.FocusOutline != outline; i++)
                Arrow(Key.RightArrow);
            Assert.That(plaque.FocusOutline, Is.EqualTo(outline));
        }

        void KeyboardPlace(int piece)
        {
            FocusPieceWithArrows(piece);
            Tap(); // seleciona
            Assert.That(plaque.SelectedPiece, Is.EqualTo(piece));
            FocusOutlineWithArrows(piece);
            Tap(); // encaixa
        }

        // ---------- Sequência, montagem e dados ----------

        [Test]
        public void TheSequenceHasThreeShotsTheMarksAndNineCuesFiveOfThemByTrigger()
        {
            var sequence = plaque.Sequence;
            Assert.That(sequence.Validate(), Is.Empty);
            Assert.That(director.Validate(sequence), Is.Empty);
            Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(43f).Within(1e-4f));
            Assert.That(sequence.Shots.Select(s => s.DurationSeconds), Is.EqualTo(new[] { 17.5f, 20f, 5.5f }));
            Assert.That(sequence.Cues.Select(c => c.CueId), Is.EqualTo(new[] { "PIO.05.01", "PIO.05.01b", "PIO.05.01c", "PIO.05.02", "PIO.05.03", "PIO.05.04", "PIO.05.05", "PIO.05.06", "PIO.05.07" }));
            Assert.That(sequence.Cues.Where(c => c.IsTriggered).Select(c => c.TriggerId), Is.EqualTo(PioneerPlaqueActivity.TriggerIds));
            Assert.That(sequence.Cues.Where(c => c.IsTriggered).All(c => c.MinimumReadSeconds >= 3.5f && c.MinimumReadSeconds <= 4.7f), Is.True, "frase de 3,5–4,7 s: o tempo de ler a 16 caracteres por segundo (o texto em inglês da lista nova é mais longo)");
            var timed = sequence.Cues.Where(c => !c.IsTriggered).Select(c => (c.CueId, c.StartSeconds, c.EndSeconds)).ToArray();
            Assert.That(timed, Is.EqualTo(new[] { ("PIO.05.01", 0f, 5.9f), ("PIO.05.01b", 6f, 11.7f), ("PIO.05.01c", 11.8f, 17.2f), ("PIO.05.07", 37.7f, 43f) }));
        }

        [Test]
        public void ValidateSetupIsEmptyOnTheBuiltSceneAndComplainsAboutMissingReferences()
        {
            Assert.That(plaque.ValidateSetup(), Is.Empty);

            var bare = new GameObject("bare").AddComponent<PioneerPlaqueActivity>();
            var errors = bare.ValidateSetup();
            Object.DestroyImmediate(bare.gameObject);
            Assert.That(errors.Any(e => e.Contains("sequence")), Is.True);
            Assert.That(errors.Any(e => e.Contains("presentation")), Is.True);
            Assert.That(errors.Any(e => e.Contains("panel")), Is.True);
        }

        [Test]
        public void ThePanelPrefabHasTheResponsiveClassAndEveryElementTheViewUses()
        {
            var panel = Object.FindAnyObjectByType<PlaquePanelView>(FindObjectsInactive.Include);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.GetComponent<Abandoned.SpaceApps.UI.ResponsiveLayoutClass>(), Is.Not.Null, "UI_SPEC: todo UIDocument de tela cheia");
            Assert.That(panel.ValidateSetup(), Is.Empty);
        }

        [Test]
        public void EveryPlaqueTextKeyHasPortugueseAndEnglishAndTheTriggersMatchTheChapterData()
        {
            var data = AssetDatabase.LoadAssetAtPath<PioneerData>(PioneerDataBuilder.DataAssetPath);
            var keys = PlaquePanelView.NameKeys.Concat(new[]
            {
                PlaquePanelView.PlaqueKey, PlaquePanelView.SnapKey, PlaquePanelView.RetryKey,
                "PIO.05.01", "PIO.05.01b", "PIO.05.01c", "PIO.05.02", "PIO.05.03", "PIO.05.04", "PIO.05.05", "PIO.05.06", "PIO.05.07",
            });
            foreach (var key in keys)
            {
                Assert.That(data.GetText(key, PioneerData.Portuguese), Is.Not.Empty, key + " PT");
                Assert.That(data.GetText(key, PioneerData.English), Is.Not.Empty, key + " EN");
            }

            Assert.That(data.GetText(PlaquePanelView.RetryKey, PioneerData.Portuguese), Does.Not.Contain("erro").And.Not.Contain("errou").And.Not.Contain("falha"), "mensagem neutra");
            var chapter = File.ReadAllText("Data/Chapters/Pioneer10.json");
            foreach (var trigger in PioneerPlaqueActivity.TriggerIds)
                Assert.That(chapter, Does.Contain(trigger), trigger + " nos dados do capítulo (PIO-001)");
            Assert.That(data.CuesOf(PioneerPlaqueActivity.StateId).Count(c => c.timing == PioneerCueTiming.Trigger), Is.EqualTo(5));
        }

        // ---------- Caminhos de entrada ----------

        [Test]
        public void KeyboardAloneCompletesFiveOfFiveWithoutMouseAudioOrPrecision()
        {
            EnterFit();
            Assert.That(router.Context, Is.EqualTo(InputContext.Panel));
            foreach (var piece in new[] { 2, 0, 4, 1, 3 })
                KeyboardPlace(piece);

            Assert.That(fitted.Select(f => f.count), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }), "progresso n/5, sem pontuação");
            Assert.That(fitted.Select(f => f.piece).OrderBy(p => p), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(plaque.Phase, Is.EqualTo(PlaquePhase.Complete));
            Assert.That(all, Is.EqualTo(1));
            Assert.That(cues, Is.SupersetOf(new[] { "PIO.05.02", "PIO.05.03", "PIO.05.04", "PIO.05.05", "PIO.05.06" }), "uma frase por encaixe");
            RunUntil(() => completed > 0);
            Assert.That((completed, all, reveals), Is.EqualTo((1, 1, 1)));
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False);
        }

        [Test]
        public void ClickingPieceThenOutlineWorksInAnyOrderAndClickingTwiceDeselects()
        {
            EnterFit();
            plaque.ClickPiece(3);
            Assert.That(plaque.SelectedPiece, Is.EqualTo(3));
            plaque.ClickPiece(3);
            Assert.That(plaque.SelectedPiece, Is.EqualTo(-1), "clicar de novo solta a seleção");
            Assert.That(plaque.ClickOutline(3), Is.EqualTo(PlaqueResult.NothingSelected), "contorno sem peça selecionada não faz nada");

            foreach (var piece in new[] { 4, 2, 0, 3, 1 })
            {
                plaque.ClickPiece(piece);
                Assert.That(plaque.ClickOutline(piece), Is.EqualTo(PlaqueResult.Placed));
            }

            Assert.That(plaque.PlacedCount, Is.EqualTo(5));
            Assert.That(plaque.ClickPiece_AlreadyPlacedIsIgnored(), Is.True);
        }

        [Test]
        public void DroppingWithinNinetySixPixelsSnapsAndDroppingFarJustReturnsThePiece()
        {
            var centers = new[] { new Vector2(300f, 200f), new Vector2(300f, 400f), new Vector2(500f, 300f), new Vector2(700f, 300f), new Vector2(700f, 500f) };
            Assert.That(PlaquePanelView.NearestOutline(new Vector2(300f, 200f + 95f), centers, 0, PlaquePanelView.SnapRadiusPixels), Is.EqualTo(0), "a 95 px: encaixa");
            Assert.That(PlaquePanelView.NearestOutline(new Vector2(300f, 200f + 97f), centers, 0, PlaquePanelView.SnapRadiusPixels), Is.EqualTo(-1), "a 97 px: volta");
            Assert.That(PlaquePanelView.NearestOutline(new Vector2(410f, 300f), centers, 0, PlaquePanelView.SnapRadiusPixels), Is.EqualTo(2), "o mais próximo vence");
            Assert.That(PlaquePanelView.NearestOutline(new Vector2(300f, 200f), centers, 1, PlaquePanelView.SnapRadiusPixels), Is.EqualTo(-1), "contorno já ocupado não recebe (o próximo livre está a 200 px)");
            Assert.That(PlaquePanelView.NearestOutline(new Vector2(300f, 200f), centers, 1, 40f), Is.EqualTo(-1));
            Assert.That(PioneerPlaqueActivity.SnapRadiusPixels, Is.EqualTo(96f));

            EnterFit();
            Assert.That(plaque.DropPiece(2, -1), Is.EqualTo(PlaqueResult.NothingSelected), "solto longe: só volta, sem mensagem");
            Assert.That(plaque.ViewState().RetryVisible, Is.False);
            Assert.That(plaque.PlacedCount, Is.Zero);
            Assert.That(plaque.DropPiece(2, 2), Is.EqualTo(PlaqueResult.Placed));
            Assert.That(plaque.DropPiece(2, 2), Is.EqualTo(PlaqueResult.AlreadyPlaced), "peça já encaixada");
        }

        [Test]
        public void AWrongOutlineReturnsThePieceWithANeutralMessageAndNeverRemovesAFit()
        {
            EnterFit();
            plaque.ClickPiece(0);
            plaque.ClickOutline(0);
            plaque.ClickPiece(1);
            plaque.ClickOutline(1);
            Assert.That(plaque.PlacedCount, Is.EqualTo(2));

            plaque.ClickPiece(3);
            Assert.That(plaque.ClickOutline(4), Is.EqualTo(PlaqueResult.WrongOutline));
            Assert.That(wrong, Is.EqualTo(1));
            Assert.That(plaque.PlacedCount, Is.EqualTo(2), "o erro não remove acertos");
            Assert.That(plaque.IsPlaced(0) && plaque.IsPlaced(1), Is.True);
            Assert.That(plaque.SelectedPiece, Is.EqualTo(-1), "a peça voltou");
            var view = plaque.ViewState();
            Assert.That(view.RetryVisible, Is.True);
            Assert.That(view.RetryOutline, Is.EqualTo(4));
            Assert.That(view.Mode, Is.EqualTo(PlaqueMode.ChoosePiece));

            Run(PioneerPlaqueActivity.RetrySeconds + 0.2f);
            Assert.That(plaque.ViewState().RetryVisible, Is.False, "a mensagem some sozinha");
            plaque.ClickPiece(3);
            Assert.That(plaque.ClickOutline(3), Is.EqualTo(PlaqueResult.Placed), "tentar de novo funciona");
        }

        [Test]
        public void TheCorrectOutlinePulsesAfterSelectionAndOnlyThatOne()
        {
            EnterFit();
            Assert.That(plaque.ViewState().HintOutline, Is.EqualTo(-1), "sem seleção, nenhum contorno pulsa");
            plaque.ClickPiece(2);
            var view = plaque.ViewState();
            Assert.That(view.HintOutline, Is.EqualTo(2));
            Assert.That(view.Selected, Is.EqualTo(2));
            Assert.That(view.Mode, Is.EqualTo(PlaqueMode.ChooseOutline));
        }

        // ---------- Ajuda (segurar E) ----------

        [Test]
        public void HoldingEPlacesTheFocusedPieceAfterTwoSecondsAndFiveTakeAtMostThirtySeconds()
        {
            EnterFit();
            var start = director.TimeSeconds;
            var activeSeconds = 0f;
            for (var i = 0; i < PioneerPlaqueActivity.PieceCount; i++)
            {
                var before = plaque.PlacedCount;
                Press(keyboard.eKey);
                for (var t = 0f; t < PioneerPlaqueActivity.AssistHoldSeconds + 0.5f && plaque.PlacedCount == before; t += Frame)
                {
                    Step(Frame);
                    activeSeconds += Frame;
                }

                Release(keyboard.eKey);
                Step(Frame);
                activeSeconds += Frame;
                Assert.That(plaque.PlacedCount, Is.EqualTo(before + 1), $"peça {i + 1}: a ajuda encaixa uma por vez");
            }

            Assert.That(activeSeconds, Is.LessThanOrEqualTo(PioneerPlaqueActivity.MaxAssistedRunSeconds), "cinco peças assistidas em até 30 s");
            Assert.That(plaque.Phase, Is.EqualTo(PlaquePhase.Complete));
            Assert.That(fitted.Select(f => f.piece).Distinct().Count(), Is.EqualTo(5));
            Assert.That(director.TimeSeconds - start, Is.LessThanOrEqualTo(MaxSequenceSecondsForAssistedRun), "dentro da janela de 7,5–27,5 s");
        }

        const float MaxSequenceSecondsForAssistedRun = 30f;

        [Test]
        public void AShortPressIsATapAndAHoldReleasedEarlyDoesNothingAndKeepsEveryFit()
        {
            EnterFit();
            plaque.ClickPiece(0);
            plaque.ClickOutline(0);

            Tap(); // E curto: seleciona a peça em foco
            Assert.That(plaque.SelectedPiece, Is.Not.EqualTo(-1));
            var selected = plaque.SelectedPiece;

            Press(keyboard.eKey);
            Run(1f); // mais que um toque, menos que a ajuda
            Assert.That(plaque.ViewState().Hold, Is.EqualTo(PlaqueHoldState.Holding));
            Assert.That(plaque.ViewState().AssistProgress, Is.InRange(0.3f, 0.7f));
            Release(keyboard.eKey);
            Step(Frame);

            Assert.That(plaque.PlacedCount, Is.EqualTo(1), "soltar cedo não encaixa nem apaga");
            Assert.That(plaque.SelectedPiece, Is.EqualTo(selected));
            Assert.That(plaque.ViewState().Hold, Is.EqualTo(PlaqueHoldState.Paused), "soltou no meio da ajuda: pausado, com os encaixes preservados");
            Assert.That(plaque.ViewState().AssistProgress, Is.Zero);
        }

        [Test]
        public void AnEHeldFromBeforeTheActivityOpensIsRearmedAndStartsNothingUntilReleased()
        {
            Press(keyboard.eKey);
            plaque.Begin();
            RunUntil(() => plaque.Phase == PlaquePhase.Fit);
            Run(3f);
            Assert.That(plaque.ViewState().Hold, Is.EqualTo(PlaqueHoldState.Rearm));
            Assert.That(plaque.SelectedPiece, Is.EqualTo(-1));
            Assert.That(plaque.PlacedCount, Is.Zero, "o E antigo não encaixa nada");

            Release(keyboard.eKey);
            Step(Frame);
            Assert.That(plaque.ViewState().Hold, Is.EqualTo(PlaqueHoldState.Available));
            Tap();
            Assert.That(plaque.SelectedPiece, Is.Not.EqualTo(-1), "depois de soltar e pressionar de novo, funciona");
        }

        // ---------- Pausa, marco, reinício ----------

        [Test]
        public void PausingFreezesTheAssistTimerTheRetryMessageAndThePulse()
        {
            EnterFit();
            plaque.ClickPiece(1);
            plaque.ClickOutline(2); // errado: mensagem neutra
            plaque.ClickPiece(1);
            var pulse = plaque.ViewState();
            Run(0.5f);
            var before = plaque.ViewState();
            for (var i = 0; i < 90; i++)
                Step(0f); // pausa: tempo ativo zero
            var after = plaque.ViewState();
            Assert.That(after.HintPulseOn, Is.EqualTo(before.HintPulseOn), "o pulso congela");
            Assert.That(after.RetryVisible, Is.EqualTo(before.RetryVisible));
            Assert.That(pulse.Selected, Is.EqualTo(1));
            Assert.That(plaque.PlacedCount, Is.Zero);
        }

        [Test]
        public void TheGateWaitsForFiveOfFiveThenTheRevealStartsOnceAndTheChapterCompletesOnce()
        {
            EnterFit();
            plaque.ClickPiece(0);
            plaque.ClickOutline(0);
            RunUntil(() => plaque.IsGateWaiting);
            var waitingAt = director.TimeSeconds;
            Run(20f);
            Assert.That(director.TimeSeconds, Is.EqualTo(waitingAt).Within(1e-4f), "a revelação espera as cinco peças; nada passa por inatividade");
            Assert.That(waitingAt, Is.EqualTo(37.4f).Within(0.1f));
            Assert.That(reveals, Is.Zero);

            foreach (var piece in new[] { 1, 2, 3, 4 })
            {
                plaque.ClickPiece(piece);
                plaque.ClickOutline(piece);
            }

            Assert.That(plaque.IsGateWaiting, Is.False);
            RunUntil(() => completed > 0);
            Assert.That((completed, all, reveals), Is.EqualTo((1, 1, 1)));
        }

        [Test]
        public void CancelingAtThreeOfFiveAndRestartingStartsFromZeroWithNoLeaksOrOldCallbacks()
        {
            EnterFit();
            foreach (var piece in new[] { 0, 1, 2 })
            {
                plaque.ClickPiece(piece);
                plaque.ClickOutline(piece);
            }

            plaque.ClickPiece(3);
            Assert.That(plaque.PlacedCount, Is.EqualTo(3));
            plaque.Cancel();
            Assert.That(plaque.IsRunning, Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False);
            Assert.That(completed, Is.Zero);

            fitted.Clear();
            EnterFit();
            Assert.That(plaque.PlacedCount, Is.Zero, "nova execução começa do zero");
            Assert.That(plaque.SelectedPiece, Is.EqualTo(-1));
            KeyboardPlace(1);
            Assert.That(fitted, Is.EqualTo(new[] { (1, 1) }), "sem eventos da execução anterior");
            Assert.That(plaque.ValidateSetup(), Is.Empty);
        }

        [Test]
        public void ReducedMotionKeepsTheOutlineSteadyAndTheNormalPulseIsSlowerThanTwoChangesPerSecond()
        {
            EnterFit();
            plaque.ClickPiece(4);
            var changes = 0;
            var last = plaque.ViewState().HintPulseOn;
            for (var t = 0f; t < 7f; t += Frame)
            {
                Step(Frame);
                var now = plaque.ViewState().HintPulseOn;
                if (now != last)
                    changes++;
                last = now;
            }

            Assert.That(changes, Is.InRange(5, 14), "pulso lento: período de 1,4 s");
            Assert.That(changes / 7f, Is.LessThan(3f), "bem abaixo do limiar de flashes");

            plaque.SetReducedMotion(true);
            for (var t = 0f; t < 3f; t += Frame)
            {
                Step(Frame);
                Assert.That(plaque.ViewState().HintPulseOn, Is.True, "movimento reduzido: o contorno fica destacado e parado");
            }
        }

        [Test]
        public void SkippingCompletesOnceWithoutOpeningInputs()
        {
            plaque.Begin(skippable: true);
            Run(5f);
            Assert.That(director.Skip(), Is.True);
            Run(1f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(router.DescribeLeases(), Is.Empty);
            Assert.That(interaction.TryGetActive(out _), Is.False);
        }

        // ---------- Arte: uma fonte para o SVG e para os PNGs ----------

        [Test]
        public void TheSvgAndTheFiveTexturesComeFromTheSameShapesAndMatchTheSlotTable()
        {
            Assert.That(File.Exists(PioneerPlaqueBuilder.SvgPath), "Art/PlaqueEducational.svg");
            var svg = File.ReadAllText(PioneerPlaqueBuilder.SvgPath);
            Assert.That(svg.Replace("\r\n", "\n"), Is.EqualTo(PioneerPlaqueArt.BoardSvg()), "o SVG é a saída determinística do gerador");
            Assert.That(svg, Does.Not.Contain("<image").And.Not.Contain("<text").And.Not.Contain("href"), "vetor puro, sem bitmap nem texto embutido");
            Assert.That(svg, Does.Contain("adaptacao educativa").And.Contain("nao e reproducao fiel"));

            for (var i = 0; i < PioneerPlaqueArt.PieceIds.Length; i++)
            {
                var id = PioneerPlaqueArt.PieceIds[i];
                var slot = PioneerPlaqueArt.SlotRects[i];
                Assert.That(svg, Does.Contain($"<g id=\"{id}\""), id);
                var scale = Mathf.Min(slot.width, slot.height) / 100f;
                var tx = slot.x + (slot.width - 100f * scale) * 0.5f;
                var ty = slot.y + (slot.height - 100f * scale) * 0.5f;
                var expected = string.Format(System.Globalization.CultureInfo.InvariantCulture, "translate({0:0.##} {1:0.##}) scale({2:0.##})", tx, ty, scale);
                Assert.That(svg, Does.Contain(expected), id + ": a peça está no contorno da tabela");
                Assert.That(slot.xMax, Is.LessThanOrEqualTo(PioneerPlaqueArt.BoardWidth).And.GreaterThanOrEqualTo(0f));
                Assert.That(slot.yMax, Is.LessThanOrEqualTo(PioneerPlaqueArt.BoardHeight));

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PioneerPlaqueBuilder.IconPath(i));
                Assert.That(texture, Is.Not.Null, id);
                Assert.That(texture.width, Is.EqualTo(PioneerPlaqueArt.IconPixels));
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false); // o asset importado não é legível: lê o PNG
                Assert.That(decoded.LoadImage(File.ReadAllBytes(PioneerPlaqueBuilder.IconPath(i))), Is.True);
                var pixels = decoded.GetPixels32();
                Object.DestroyImmediate(decoded);
                Assert.That(pixels.Count(p => p.a == 0), Is.GreaterThan(pixels.Length / 4), id + ": fundo transparente");
                Assert.That(pixels.Count(p => p.a > 200), Is.GreaterThan(500), id + ": traço dourado visível");
            }

            // contornos não se sobrepõem (cada um é um alvo próprio)
            for (var i = 0; i < PioneerPlaqueArt.SlotRects.Length; i++)
            {
                for (var j = i + 1; j < PioneerPlaqueArt.SlotRects.Length; j++)
                    Assert.That(PioneerPlaqueArt.SlotRects[i].Overlaps(PioneerPlaqueArt.SlotRects[j]), Is.False, $"{i} × {j}");
            }
        }

        [Test]
        public void ThePeopleAreSmoothSilhouettesAndTheArtCreditsTheEducationalAdaptation()
        {
            var credits = File.ReadAllText("ASSET_CREDITS.md");
            Assert.That(credits, Does.Contain("PlaqueEducational.svg"), "ASSET_CREDITS registra a arte da placa");
            Assert.That(credits, Does.Contain("adaptação educativa"));
            var svg = File.ReadAllText(PioneerPlaqueBuilder.SvgPath);
            var people = svg.Substring(svg.IndexOf("<g id=\"people\"", System.StringComparison.Ordinal));
            Assert.That(people, Does.Not.Contain("<rect"), "silhuetas lisas: cabeça, tronco e membros em cápsulas, sem detalhe anatômico");
        }
    }

    static class PlaqueTestExtensions
    {
        /// <summary>Clicar numa peça já encaixada não muda nada (a peça nem aparece mais na bandeja).</summary>
        public static bool ClickPiece_AlreadyPlacedIsIgnored(this PioneerPlaqueActivity plaque)
        {
            var before = plaque.PlacedCount;
            plaque.ClickPiece(0);
            return plaque.PlacedCount == before && plaque.SelectedPiece == -1;
        }
    }
}
