using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>UI-003: regras do HUD sem Unity — visibilidade, formatação, seta de borda, prompt E e visões PT/EN.</summary>
    public class HudLogicTests
    {
        const string Pt = "pt";
        const string En = "en";

        UI.DossierTextCatalog texts;
        UI.PromptModel model;

        [SetUp]
        public void SetUp()
        {
            texts = new UI.DossierTextCatalog();
            texts.Add(UI.HudKeys.PromptPress, "E · {action}", "E · {action}");
            texts.Add(UI.HudKeys.PromptHold, "Segure E · {action}", "Hold E · {action}");
            texts.Add(UI.HudKeys.PromptToggle, "Pressione E · {action}", "Press E · {action}");
            texts.Add(UI.HudKeys.PromptToggleStop, "Pressione E para parar · {action}", "Press E to stop · {action}");
            texts.Add(UI.HudKeys.PromptRearm, "Solte E para começar", "Release E to begin");
            texts.Add(UI.HudKeys.PromptResumeHold, "Segure E para continuar", "Hold E to continue");
            texts.Add(UI.HudKeys.PromptResumeToggle, "Pressione E para continuar", "Press E to continue");
            texts.Add(UI.HudKeys.StatusAvailablePress, "DISPONÍVEL · PRESSIONE E", "AVAILABLE · PRESS E");
            texts.Add(UI.HudKeys.StatusAvailableHold, "DISPONÍVEL · SEGURE E", "AVAILABLE · HOLD E");
            texts.Add(UI.HudKeys.StatusRearm, "AGUARDANDO SOLTAR E", "WAITING FOR E RELEASE");
            texts.Add(UI.HudKeys.StatusHolding, "OPERANDO · {percent}%", "WORKING · {percent}%");
            texts.Add(UI.HudKeys.StatusPaused, "PAUSADO · {percent}% PRESERVADO", "PAUSED · {percent}% KEPT");
            texts.Add(UI.HudKeys.StatusDone, "CONCLUÍDO ✓", "DONE ✓");
            texts.Add(UI.HudKeys.CommandSent, "Enviado", "Sent");
            texts.Add(UI.HudKeys.CommandTransit, "Comando em trânsito · {seconds} s", "Command in transit · {seconds} s");
            texts.Add(UI.HudKeys.CommandConfirmed, "Comando confirmado", "Command confirmed");
            texts.Add(UI.HudKeys.CommandStatusSent, "ENVIADO", "SENT");
            texts.Add(UI.HudKeys.CommandStatusTransit, "EM TRÂNSITO · {percent}%", "IN TRANSIT · {percent}%");
            texts.Add(UI.HudKeys.CommandStatusConfirmed, "CONFIRMADO ✓", "CONFIRMED ✓");
            texts.Add(UI.HudKeys.EnergyLabel, "ENERGIA", "ENERGY");
            texts.Add(UI.HudKeys.EnergyLow, "ENERGIA BAIXA", "LOW ENERGY");
            texts.Add("MAR.VERB.grind", "esmerilhar", "grind");
            texts.Add("MAR.OBJ.rock", "a rocha", "the rock");
            texts.Add("OBJ.label", "Prepare e analise a rocha", "Prepare and analyze the rock");
            texts.Add("OBJ.stamp", "OPPORTUNITY · MERIDIANI · 2004", "OPPORTUNITY · MERIDIANI · 2004");
            texts.Add("OBJ.target", "McKittrick", "McKittrick");

            model = new UI.PromptModel();
        }

        static UI.PromptSnapshot Snap(string id = "a", bool canBegin = true, bool awaiting = false, string holding = null, float holdSeconds = 2f, bool has = true) =>
            new UI.PromptSnapshot
            {
                HasCandidate = has, CandidateId = has ? id : null, VerbKey = "MAR.VERB.grind", ObjectKey = "MAR.OBJ.rock", HoldSeconds = holdSeconds,
                CanBegin = canBegin, AwaitingRelease = awaiting, HoldingId = holding,
            };

        void Refresh(UI.PromptSnapshot snapshot, float dt = 0.016f) => model.Refresh(snapshot, dt);

        // ---------- Visibilidade por contexto ----------

        [Test]
        public void GameplayShowsEveryLayer()
        {
            var layers = UI.HudVisibility.For(true, UI.HudInputContext.Gameplay);
            Assert.That(layers.Objective && layers.Prompt && layers.Orientation && layers.Energy, Is.True);
        }

        [Test]
        public void PauseKeepsOnlyTheObjectiveAndHidesOrientationAndPrompt()
        {
            var layers = UI.HudVisibility.For(true, UI.HudInputContext.Paused);
            Assert.That(layers.Objective, Is.True);
            Assert.That(layers.Orientation, Is.False, "orientação some na pausa");
            Assert.That(layers.Prompt, Is.False);
            Assert.That(layers.Energy, Is.False);
        }

        [Test]
        public void CinematicPanelAndNotPlayingShowNothing()
        {
            Assert.That(UI.HudVisibility.For(true, UI.HudInputContext.Cinematic).Any, Is.False, "cinemática");
            Assert.That(UI.HudVisibility.For(true, UI.HudInputContext.Panel).Any, Is.False, "painel");
            foreach (var context in new[] { UI.HudInputContext.Gameplay, UI.HudInputContext.Paused, UI.HudInputContext.Cinematic, UI.HudInputContext.Panel })
                Assert.That(UI.HudVisibility.For(false, context).Any, Is.False, "capítulo não está em andamento: " + context);
        }

        // ---------- Formatação ----------

        [Test]
        public void DistanceUsesOneDecimalBelowTenMetersAndLocalizesTheSeparator()
        {
            Assert.That(UI.HudFormat.Distance(4f, Pt), Is.EqualTo("4 m"));
            Assert.That(UI.HudFormat.Distance(4.5f, Pt), Is.EqualTo("4,5 m"));
            Assert.That(UI.HudFormat.Distance(4.5f, En), Is.EqualTo("4.5 m"));
            Assert.That(UI.HudFormat.Distance(0f, Pt), Is.EqualTo("0 m"));
            Assert.That(UI.HudFormat.Distance(0.04f, Pt), Is.EqualTo("0 m"));
            Assert.That(UI.HudFormat.Distance(9.96f, Pt), Is.EqualTo("10 m"), "arredonda para 10 sem ',0'");
            Assert.That(UI.HudFormat.Distance(10f, Pt), Is.EqualTo("10 m"));
            Assert.That(UI.HudFormat.Distance(42.4f, Pt), Is.EqualTo("42 m"));
        }

        [Test]
        public void DistanceSwitchesToKilometersWithoutShowingThousandMeters()
        {
            Assert.That(UI.HudFormat.Distance(999.4f, Pt), Is.EqualTo("999 m"));
            Assert.That(UI.HudFormat.Distance(999.6f, Pt), Is.EqualTo("1 km"));
            Assert.That(UI.HudFormat.Distance(1234f, Pt), Is.EqualTo("1,2 km"));
            Assert.That(UI.HudFormat.Distance(1234f, En), Is.EqualTo("1.2 km"));
            Assert.That(UI.HudFormat.Distance(12500f, Pt), Is.EqualTo("13 km"));
        }

        [Test]
        public void InvalidDistanceShowsNothing()
        {
            Assert.That(UI.HudFormat.Distance(-1f, Pt), Is.EqualTo(""));
            Assert.That(UI.HudFormat.Distance(float.NaN, Pt), Is.EqualTo(""));
            Assert.That(UI.HudFormat.Distance(float.PositiveInfinity, Pt), Is.EqualTo(""));
        }

        [Test]
        public void PercentAndSecondsAreClampedAndLocalized()
        {
            Assert.That(UI.HudFormat.Percent(0.604f), Is.EqualTo(60));
            Assert.That(UI.HudFormat.Percent(1.2f), Is.EqualTo(100));
            Assert.That(UI.HudFormat.Percent(-0.5f), Is.EqualTo(0));
            Assert.That(UI.HudFormat.Percent(float.NaN), Is.EqualTo(0));
            Assert.That(UI.HudFormat.Seconds(1.3f, Pt), Is.EqualTo("1,3"));
            Assert.That(UI.HudFormat.Seconds(1.3f, En), Is.EqualTo("1.3"));
            Assert.That(UI.HudFormat.Seconds(-4f, Pt), Is.EqualTo("0,0"));
        }

        [Test]
        public void FillAndComposeSkipMissingPieces()
        {
            Assert.That(UI.HudFormat.Fill("Segure E · {action}", "esmerilhar"), Is.EqualTo("Segure E · esmerilhar"));
            Assert.That(UI.HudFormat.Fill("OPERANDO · {percent}%", percent: 60), Is.EqualTo("OPERANDO · 60%"));
            Assert.That(UI.HudFormat.Fill("{seconds} s", seconds: "1,3"), Is.EqualTo("1,3 s"));
            Assert.That(UI.HudFormat.Fill("sem marcador", "x", 1, "2"), Is.EqualTo("sem marcador"));
            Assert.That(UI.HudFormat.Fill(null), Is.EqualTo(""));
            Assert.That(UI.HudFormat.Compose("2 / 3", "", "Pata", null, "4 m"), Is.EqualTo("2 / 3 · Pata · 4 m"));
            Assert.That(UI.HudFormat.Compose("", null), Is.EqualTo(""));
        }

        [Test]
        public void DistanceKeyChangesExactlyWhenTheDisplayedTextChanges()
        {
            Assert.That(UI.HudFormat.DistanceKey(-1f), Is.EqualTo(-1));
            Assert.That(UI.HudFormat.DistanceKey(float.NaN), Is.EqualTo(-1));
            Assert.That(UI.HudFormat.DistanceKey(4.51f), Is.EqualTo(UI.HudFormat.DistanceKey(4.54f)), "mesmo texto, mesma chave");
            Assert.That(UI.HudFormat.DistanceKey(4.4f) != UI.HudFormat.DistanceKey(4.6f), Is.True);
            Assert.That(UI.HudFormat.DistanceKey(42.1f), Is.EqualTo(UI.HudFormat.DistanceKey(41.6f)), "a partir de 10 m o texto é inteiro");
            Assert.That(UI.HudFormat.DistanceKey(42.1f) != UI.HudFormat.DistanceKey(43.1f), Is.True);

            var previousText = string.Empty;
            var previousKey = int.MinValue;
            for (var meters = 0f; meters < 1500f; meters += 0.05f)
            {
                var text = UI.HudFormat.Distance(meters, Pt);
                var key = UI.HudFormat.DistanceKey(meters);
                if (key == previousKey)
                    Assert.That(text, Is.EqualTo(previousText), $"mesma chave com textos diferentes em {meters}");
                previousKey = key;
                previousText = text;
            }
        }

        // ---------- Seta de borda e âncora ----------

        [Test]
        public void TargetInsideTheInnerRectangleUsesTheWorldAnchor()
        {
            var cue = UI.EdgeCueMath.Compute(0.5f, 0.5f, 10f);
            Assert.That(cue.Valid && cue.OnScreen, Is.True);
            Assert.That(cue.Side, Is.EqualTo(UI.EdgeSide.None));
            Assert.That(Near(cue.X, 0.5f) && Near(cue.Y, 0.5f), Is.True);

            var upperLeft = UI.EdgeCueMath.Compute(0.1f, 0.7f, 3f); // y de viewport sobe: sy = 0,3
            Assert.That(upperLeft.OnScreen, Is.True);
            Assert.That(Near(upperLeft.X, 0.1f) && Near(upperLeft.Y, 0.3f), Is.True, "âncora fica exatamente sobre o alvo");
        }

        [Test]
        public void TargetOffToEachSideRestsOnThatSideOfTheInnerRectangle()
        {
            var insets = UI.EdgeInsets.Default;
            var right = UI.EdgeCueMath.Compute(1.6f, 0.5f, 5f);
            Assert.That(right.OnScreen, Is.False);
            Assert.That(right.Side, Is.EqualTo(UI.EdgeSide.Right));
            Assert.That(Near(right.X, 1f - insets.Right) && Near(right.Y, 0.5f) && Near(right.AngleDegrees, 0f), Is.True);

            var left = UI.EdgeCueMath.Compute(-0.6f, 0.5f, 5f);
            Assert.That(left.Side, Is.EqualTo(UI.EdgeSide.Left));
            Assert.That(Near(left.X, insets.Left) && Near(left.AngleDegrees, 180f), Is.True);

            var above = UI.EdgeCueMath.Compute(0.5f, 1.8f, 5f);
            Assert.That(above.Side, Is.EqualTo(UI.EdgeSide.Top));
            Assert.That(Near(above.Y, insets.Top) && Near(above.X, 0.5f) && Near(above.AngleDegrees, 270f), Is.True);

            var below = UI.EdgeCueMath.Compute(0.5f, -0.8f, 5f);
            Assert.That(below.Side, Is.EqualTo(UI.EdgeSide.Bottom));
            Assert.That(Near(below.Y, 1f - insets.Bottom) && Near(below.AngleDegrees, 90f), Is.True);
        }

        [Test]
        public void DiagonalTargetHitsTheNearestSideAlongTheRay()
        {
            var cue = UI.EdgeCueMath.Compute(2f, 2f, 5f); // acima e à direita
            Assert.That(cue.OnScreen, Is.False);
            Assert.That(cue.Side, Is.EqualTo(UI.EdgeSide.Top));
            Assert.That(Near(cue.AngleDegrees, 315f), Is.True);
            Assert.That(cue.X > 0.5f && cue.X < 0.96f, Is.True, "ainda inclinada para a direita, sem encostar na lateral");
        }

        [Test]
        public void TargetBehindTheCameraNeverCountsAsOnScreenAndPointsTheOppositeWay()
        {
            var behindRight = UI.EdgeCueMath.Compute(0.8f, 0.5f, -3f); // projeção espelhada: o alvo está à esquerda
            Assert.That(behindRight.OnScreen, Is.False);
            Assert.That(behindRight.Side, Is.EqualTo(UI.EdgeSide.Left));
            Assert.That(Near(behindRight.AngleDegrees, 180f), Is.True);

            var exactlyBehind = UI.EdgeCueMath.Compute(0.5f, 0.5f, -2f);
            Assert.That(exactlyBehind.OnScreen, Is.False);
            Assert.That(exactlyBehind.Side, Is.EqualTo(UI.EdgeSide.Bottom), "no eixo, atrás: aponta para baixo");

            Assert.That(UI.EdgeCueMath.Compute(0.5f, 0.5f, 0f).OnScreen, Is.False, "profundidade zero também é atrás");
        }

        [Test]
        public void TargetUnderTheBottomPromptZoneGetsAnArrowInsteadOfAMarkerBehindThePlate()
        {
            var insets = UI.EdgeInsets.Default;
            var cue = UI.EdgeCueMath.Compute(0.5f, 0.1f, 5f); // sy = 0,9: abaixo do limite inferior do retângulo interno
            Assert.That(cue.OnScreen, Is.False);
            Assert.That(cue.Side, Is.EqualTo(UI.EdgeSide.Bottom));
            Assert.That(Near(cue.Y, 1f - insets.Bottom), Is.True);
            Assert.That(cue.Y < 0.65f, Is.True, "a seta fica acima da pilha prompt + legenda (começa em ~65% da altura)");
        }

        [Test]
        public void InvalidProjectionIsNotDrawn()
        {
            Assert.That(UI.EdgeCueMath.Compute(float.NaN, 0.5f, 1f).Valid, Is.False);
            Assert.That(UI.EdgeCueMath.Compute(0.5f, float.PositiveInfinity, 1f).Valid, Is.False);
            Assert.That(UI.EdgeCueMath.Compute(0.5f, 0.5f, float.NaN).Valid, Is.False);
        }

        [Test]
        public void EverySampledTargetEndsInsideTheInnerRectangleWithAValidAngle()
        {
            var insets = UI.EdgeInsets.Default;
            var random = new Random(1972);
            for (var i = 0; i < 2000; i++)
            {
                var x = (float)(random.NextDouble() * 6d - 3d);
                var y = (float)(random.NextDouble() * 6d - 3d);
                var depth = (float)(random.NextDouble() * 20d - 10d);
                var cue = UI.EdgeCueMath.Compute(x, y, depth, insets);
                Assert.That(cue.Valid, Is.True, $"amostra {i}");
                Assert.That(cue.X >= insets.Left - 1e-4f && cue.X <= 1f - insets.Right + 1e-4f, Is.True, $"X fora ({x},{y},{depth}) → {cue.X}");
                Assert.That(cue.Y >= insets.Top - 1e-4f && cue.Y <= 1f - insets.Bottom + 1e-4f, Is.True, $"Y fora ({x},{y},{depth}) → {cue.Y}");
                Assert.That(cue.AngleDegrees >= 0f && cue.AngleDegrees < 360f, Is.True, $"ângulo {cue.AngleDegrees}");
                Assert.That(cue.OnScreen == (cue.Side == UI.EdgeSide.None), Is.True, "na tela = sem lado");
            }
        }

        [Test]
        public void InsetsAreClampedSoTheScreenCenterStaysInside()
        {
            var insets = new UI.EdgeInsets(0.9f, -1f, float.NaN, 0.7f);
            Assert.That(insets.Left <= 0.45f && insets.Bottom <= 0.45f, Is.True);
            Assert.That(insets.Top, Is.EqualTo(0f));
            Assert.That(insets.Right, Is.EqualTo(0f));
        }

        // ---------- Prompt E: estados ----------

        [Test]
        public void PromptIsHiddenWithoutACandidate()
        {
            Refresh(Snap(has: false));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Hidden));
        }

        [Test]
        public void EAppearsAvailableOnlyWhenTheServiceSaysTheActionCanRun()
        {
            Refresh(Snap(canBegin: true));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Available));

            Refresh(Snap(canBegin: false));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Hidden), "candidato existe, mas CanBeginHold é falso: nada de E");
        }

        [Test]
        public void RearmIsShownWhileTheServiceWaitsForRelease()
        {
            Refresh(Snap(canBegin: false, awaiting: true));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Rearm));
            var view = UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold);
            Assert.That(view.Label, Is.EqualTo("Solte E para começar"));
            Assert.That(view.Status, Is.EqualTo("AGUARDANDO SOLTAR E"));
            Assert.That(view.StateClass, Is.EqualTo(UI.PromptViewBuilder.RearmClass));
        }

        [Test]
        public void StartReleaseAndCompletionGiveThreeDistinctFeedbacks()
        {
            model.OnHoldStarted("a");
            model.OnHoldProgress("a", 0.6f);
            Refresh(Snap(holding: "a"));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Holding));
            var holding = UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold);

            model.OnHoldCanceled("a");
            Refresh(Snap());
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Paused));
            var paused = UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold);

            model.OnHoldStarted("a");
            model.OnHoldProgress("a", 1f);
            model.OnHoldCompleted("a");
            Refresh(Snap(canBegin: false, awaiting: true));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Done));
            var done = UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold);

            Assert.That(holding.StateClass != paused.StateClass && paused.StateClass != done.StateClass && holding.StateClass != done.StateClass, Is.True);
            Assert.That(holding.Status, Is.EqualTo("OPERANDO · 60%"));
            Assert.That(paused.Status, Is.EqualTo("PAUSADO · 60% PRESERVADO"));
            Assert.That(done.Status, Is.EqualTo("CONCLUÍDO ✓"));
            Assert.That(holding.Label != paused.Label, Is.True, "o texto também muda, não só a cor");
        }

        [Test]
        public void ReleasingAtZeroProgressDoesNotClaimPreservedWork()
        {
            model.OnHoldStarted("a");
            model.OnHoldCanceled("a");
            Refresh(Snap());
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Available));
        }

        [Test]
        public void PausedWorkBelongsToItsCandidateAndAnotherCandidateInvalidatesIt()
        {
            model.OnHoldStarted("a");
            model.OnHoldProgress("a", 0.4f);
            model.OnHoldCanceled("a");
            Refresh(Snap("a"));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Paused));

            Refresh(Snap("b"));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Available), "trocar o candidato com o veículo em movimento");
            Refresh(Snap("a"));
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Available), "voltar ao antigo não ressuscita o estado pausado");
        }

        [Test]
        public void CompletionShowsDoneForALimitedTimeThenFollowsTheService()
        {
            Refresh(Snap());
            model.OnHoldStarted("a");
            model.OnHoldCompleted("a");

            Refresh(Snap(has: false), 0.5f);
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Done), "feedback de conclusão sobrevive ao candidato removido");
            var view = UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold);
            Assert.That(view.Label, Is.EqualTo("esmerilhar a rocha"));

            Refresh(Snap(has: false), UI.PromptModel.DoneSeconds);
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Hidden));
        }

        [Test]
        public void ALongHitchNeverSwallowsTheDoneFeedback()
        {
            Refresh(Snap());
            model.OnHoldStarted("a");
            model.OnHoldCompleted("a");
            Refresh(Snap(canBegin: false, awaiting: true), 30f); // uma travada de 30 s no mesmo quadro da conclusão
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Done), "aparece em pelo menos um quadro");
            Refresh(Snap(canBegin: false, awaiting: true), 0.016f);
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Done), "a partir daí dura o tempo normal (a travada não conta)");
            Refresh(Snap(canBegin: false, awaiting: true), UI.PromptModel.DoneSeconds);
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Rearm), "e depois segue o serviço");
        }

        [Test]
        public void KeepingEPressedAfterCompletionEndsInRearmNeverAvailable()
        {
            Refresh(Snap());
            model.OnHoldStarted("a");
            model.OnHoldCompleted("a");
            Refresh(Snap("b", canBegin: false, awaiting: true), 0.1f);
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Done));

            Refresh(Snap("b", canBegin: false, awaiting: true), 2f);
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Rearm), "a próxima atividade nunca aparece como E disponível");
        }

        [Test]
        public void ProgressEventsFromAnotherHoldAreIgnoredAndDoneTimerIgnoresNegativeTime()
        {
            model.OnHoldStarted("a");
            model.OnHoldProgress("x", 0.9f);
            model.OnHoldProgress("a", 0.25f);
            Refresh(Snap(holding: "a"));
            Assert.That(HudPercent(), Is.EqualTo(25));

            model.OnHoldCompleted("a");
            Refresh(Snap(), -5f);
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Done), "tempo negativo não adianta o relógio");
        }

        [Test]
        public void InstrumentProgressOverridesTheHoldProgressAndZeroClearsPausedState()
        {
            model.OnHoldStarted("a");
            model.OnHoldProgress("a", 0.2f);
            model.ReportProgress("a", 0.7f);
            Refresh(Snap(holding: "a"));
            Assert.That(HudPercent(), Is.EqualTo(70), "hold retomado com o tempo restante: vale o progresso físico");

            model.OnHoldCanceled("a");
            Refresh(Snap());
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Paused));
            Assert.That(HudPercent(), Is.EqualTo(70));

            model.ReportProgress("a", 0f);
            Refresh(Snap());
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Available), "o instrumento disse que nada foi preservado");

            model.ReportProgress("a", 0.5f);
            model.ClearProgress("a");
            Refresh(Snap());
            Assert.That(HudPercent(), Is.EqualTo(0));
        }

        [Test]
        public void CommandInTransitReplacesTheEPromptAndClearingReturnsIt()
        {
            Refresh(Snap());
            model.SetCommand(new UI.HudCommandState(UI.HudCommandStage.InTransit, 0.46f, 0.7f));
            Refresh(Snap());
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Command));
            var pt = UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold);
            Assert.That(pt.Label, Is.EqualTo("Comando em trânsito · 0,7 s"));
            Assert.That(pt.Status, Is.EqualTo("EM TRÂNSITO · 46%"));
            Assert.That(pt.StateClass, Is.EqualTo(UI.PromptViewBuilder.CommandClass));
            var en = UI.PromptViewBuilder.Build(model, texts, En, HoldMode.Hold);
            Assert.That(en.Label, Is.EqualTo("Command in transit · 0.7 s"));
            Assert.That(en.Status, Is.EqualTo("IN TRANSIT · 46%"));

            model.SetCommand(new UI.HudCommandState(UI.HudCommandStage.Confirmed, 1f));
            Refresh(Snap());
            Assert.That(UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold).Label, Is.EqualTo("Comando confirmado"));

            model.ClearCommand();
            Refresh(Snap());
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Available));
        }

        [Test]
        public void ResetLeavesNothingFromThePreviousChapter()
        {
            model.OnHoldStarted("a");
            model.OnHoldProgress("a", 0.5f);
            model.OnHoldCanceled("a");
            model.ReportProgress("a", 0.5f);
            model.SetCommand(new UI.HudCommandState(UI.HudCommandStage.Sent));
            Refresh(Snap());

            model.Reset();
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Hidden));
            Refresh(Snap());
            Assert.That(model.State, Is.EqualTo(UI.PromptState.Available), "sem pausado, sem comando, sem progresso herdado");
            Assert.That(HudPercent(), Is.EqualTo(0));
        }

        // ---------- Prompt E: textos PT/EN ----------

        [Test]
        public void AvailableLabelDependsOnHoldRequirementAndMode()
        {
            Refresh(Snap(holdSeconds: 2f));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold).Label, Is.EqualTo("Segure E · esmerilhar a rocha"));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, En, HoldMode.Hold).Label, Is.EqualTo("Hold E · grind the rock"));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Toggle).Label, Is.EqualTo("Pressione E · esmerilhar a rocha"));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold).Status, Is.EqualTo("DISPONÍVEL · SEGURE E"));

            Refresh(Snap(holdSeconds: 0f));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold).Label, Is.EqualTo("E · esmerilhar a rocha"), "um aperto basta");
            Assert.That(UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Toggle).Label, Is.EqualTo("E · esmerilhar a rocha"));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, En, HoldMode.Hold).Status, Is.EqualTo("AVAILABLE · PRESS E"));
        }

        [Test]
        public void HoldingAndPausedLabelsFollowTheToggleMode()
        {
            model.OnHoldStarted("a");
            model.OnHoldProgress("a", 0.3f);
            Refresh(Snap(holding: "a"));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold).Label, Is.EqualTo("Segure E · esmerilhar a rocha"));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, En, HoldMode.Toggle).Label, Is.EqualTo("Press E to stop · grind the rock"));

            model.OnHoldCanceled("a");
            Refresh(Snap());
            Assert.That(UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold).Label, Is.EqualTo("Segure E para continuar"));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, En, HoldMode.Toggle).Label, Is.EqualTo("Press E to continue"));
            Assert.That(UI.PromptViewBuilder.Build(model, texts, En, HoldMode.Hold).Status, Is.EqualTo("PAUSED · 30% KEPT"));
        }

        [Test]
        public void MissingKeysShowTheKeyItselfSoQaSeesThem()
        {
            var bare = new UI.DossierTextCatalog();
            Refresh(Snap());
            var view = UI.PromptViewBuilder.Build(model, bare, Pt, HoldMode.Hold);
            Assert.That(view.Visible, Is.True);
            Assert.That(view.Label, Is.EqualTo(UI.HudKeys.PromptHold));

            Assert.That(UI.PromptViewBuilder.Build(null, texts, Pt, HoldMode.Hold).Visible, Is.False);
            Assert.That(UI.PromptViewBuilder.Build(model, null, Pt, HoldMode.Hold).Visible, Is.False);
        }

        [Test]
        public void HiddenModelBuildsAnEmptyView()
        {
            Refresh(Snap(has: false));
            var view = UI.PromptViewBuilder.Build(model, texts, Pt, HoldMode.Hold);
            Assert.That(view.Visible, Is.False);
            Assert.That(view.Label, Is.EqualTo(""));
        }

        // ---------- Objetivo e energia ----------

        [Test]
        public void ObjectiveMetaJoinsStepTargetAndDistanceWithoutEmptyParts()
        {
            var full = new UI.ObjectiveData("o", "OBJ.stamp", "OBJ.label", "OBJ.target", 2, 3);
            var view = UI.ObjectiveViewBuilder.Build(full, UI.HudFormat.Distance(4f, Pt), texts, Pt);
            Assert.That(view.Visible, Is.True);
            Assert.That(view.Stamp, Is.EqualTo("OPPORTUNITY · MERIDIANI · 2004"));
            Assert.That(view.Label, Is.EqualTo("Prepare e analise a rocha"));
            Assert.That(view.Meta, Is.EqualTo("2 / 3 · McKittrick · 4 m"));
            Assert.That(view.TargetName, Is.EqualTo("McKittrick"));
            Assert.That(UI.ObjectiveViewBuilder.Build(full, "", texts, En).Label, Is.EqualTo("Prepare and analyze the rock"));

            var bare = new UI.ObjectiveData("o", null, "OBJ.label", null, 0, 0);
            var small = UI.ObjectiveViewBuilder.Build(bare, "", texts, Pt);
            Assert.That(small.Meta, Is.EqualTo(""));
            Assert.That(small.Stamp, Is.EqualTo(""));
            Assert.That(UI.ObjectiveViewBuilder.Build(new UI.ObjectiveData("o", null, null, null, 0, 0), "", texts, Pt).Visible, Is.False);
            Assert.That(UI.ObjectiveViewBuilder.Build(full, "", null, Pt).Visible, Is.False);
        }

        [Test]
        public void EnergyShowsOnlyWhenProvidedAndSwitchesFormBelowTheThreshold()
        {
            Assert.That(UI.EnergyViewBuilder.Build(null, texts, Pt).Visible, Is.False, "sem energia informada, nada aparece");
            Assert.That(UI.EnergyViewBuilder.Build(float.NaN, texts, Pt).Visible, Is.False);

            var healthy = UI.EnergyViewBuilder.Build(0.82f, texts, Pt);
            Assert.That(healthy.Visible && !healthy.IsLow, Is.True);
            Assert.That(healthy.Label, Is.EqualTo("ENERGIA · 82%"));

            var edge = UI.EnergyViewBuilder.Build(0.2f, texts, Pt);
            Assert.That(edge.IsLow, Is.False, "no limite ainda não é baixa");

            var low = UI.EnergyViewBuilder.Build(0.18f, texts, En);
            Assert.That(low.IsLow, Is.True);
            Assert.That(low.Label, Is.EqualTo("LOW ENERGY · 18%"), "o texto muda, não só a cor");

            Assert.That(UI.EnergyViewBuilder.Build(2f, texts, Pt).Fraction, Is.EqualTo(1f));
            Assert.That(UI.EnergyViewBuilder.Build(-1f, texts, Pt).Fraction, Is.EqualTo(0f));
        }

        // ---------- Conteúdo ----------

        [Test]
        public void ContentCheckReportsMissingKeysAndLostPlaceholdersInBothLanguages()
        {
            var empty = new UI.DossierTextCatalog();
            var allMissing = UI.HudContentCheck.Validate(empty);
            Assert.That(allMissing.Count, Is.EqualTo(UI.HudContentCheck.RequiredKeys().Count * 2), "toda chave falta em PT e EN");
            Assert.That(UI.HudContentCheck.Validate(null).Count, Is.EqualTo(1));

            var complete = new UI.DossierTextCatalog();
            foreach (var key in UI.HudContentCheck.RequiredKeys())
                complete.Add(key, "{action} {percent}% {seconds}", "{action} {percent}% {seconds}");
            Assert.That(string.Join("\n", UI.HudContentCheck.Validate(complete)), Is.EqualTo(""));

            complete.Add(UI.HudKeys.PromptHold, "Segure E", "Hold E · {action}");
            var lost = UI.HudContentCheck.Validate(complete);
            Assert.That(lost.Count, Is.EqualTo(1));
            Assert.That(lost[0].Contains("PromptHold") || lost[0].Contains("prompt.hold"), Is.True, "aponta a chave que perdeu {action}");

            complete.Add(UI.HudKeys.EnergyLabel, "ENERGIA", "");
            Assert.That(UI.HudContentCheck.Validate(complete).Count, Is.GreaterThan(1), "EN vazio é divergência");
        }

        [Test]
        public void RequiredKeysAreAllDistinctAndNotEmpty()
        {
            var keys = UI.HudContentCheck.RequiredKeys();
            Assert.That(keys.Count, Is.GreaterThan(20));
            var seen = new HashSet<string>();
            foreach (var key in keys)
            {
                Assert.That(string.IsNullOrEmpty(key), Is.False);
                Assert.That(seen.Add(key), Is.True, "chave duplicada: " + key);
            }
        }

        // ---------- auxiliares ----------

        int HudPercent() => UI.HudFormat.Percent(model.Progress);

        static bool Near(float a, float b) => Math.Abs(a - b) < 1e-3f;
    }
}
