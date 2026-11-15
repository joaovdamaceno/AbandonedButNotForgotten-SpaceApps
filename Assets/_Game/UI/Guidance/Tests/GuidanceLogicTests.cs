using System;
using NUnit.Framework;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>UI-005: regras da orientação sem Unity — relógio de 12/30 s, medidor de ação, checklist e textos PT/EN.</summary>
    public class GuidanceLogicTests
    {
        const string Pt = "pt";
        const string En = "en";

        UI.DossierTextCatalog texts;
        UI.GuidanceChecklist checklist;

        [SetUp]
        public void SetUp()
        {
            texts = new UI.DossierTextCatalog();
            texts.Add(UI.GuidanceKeys.Title, "COMO JOGAR", "HOW TO PLAY");
            texts.Add(UI.GuidanceKeys.Done, "feito ✓", "done ✓");
            texts.Add(UI.GuidanceKeys.GlyphMove, "WASD", "WASD");
            texts.Add(UI.GuidanceKeys.GlyphInteract, "E", "E");
            texts.Add(UI.GuidanceKeys.StepMove, "Mova-se", "Move");
            texts.Add(UI.GuidanceKeys.StepMoveReinforce, "Ainda parado?", "Still standing?");
            texts.Add(UI.GuidanceKeys.StepInteractHold, "Segure E", "Hold E");
            texts.Add(UI.GuidanceKeys.StepInteractPress, "Pressione E", "Press E");
            texts.Add(UI.GuidanceKeys.StepInteractReinforceHold, "Chegue mais perto e segure E", "Closer, then hold E");
            texts.Add(UI.GuidanceKeys.StepInteractReinforcePress, "Chegue mais perto e pressione E", "Closer, then press E");
            texts.Add(UI.GuidanceKeys.OfferHelp, "Pressione H", "Press H");
            texts.Add(UI.GuidanceKeys.HelpHint, "Dica: H", "Tip: H");
            texts.Add(UI.GuidanceKeys.OfferCorridor, "Volte ao corredor", "Back to the corridor");

            checklist = Tutorial();
        }

        static UI.GuidanceChecklist Tutorial() => new UI.GuidanceChecklist(new[]
        {
            new UI.GuidanceStep(UI.GuidanceController.MoveStepId, UI.GuidanceKeys.GlyphMove, UI.GuidanceKeys.StepMove, UI.GuidanceKeys.StepMoveReinforce),
            new UI.GuidanceStep(UI.GuidanceController.InteractStepId, UI.GuidanceKeys.GlyphInteract, UI.GuidanceKeys.StepInteractHold,
                UI.GuidanceKeys.StepInteractReinforceHold, UI.GuidanceKeys.StepInteractPress, UI.GuidanceKeys.StepInteractReinforcePress),
        });

        static UI.GuidanceSnapshot Snap(UI.GuidanceChecklist list, UI.GuidanceLevel level = UI.GuidanceLevel.None, bool help = false,
            bool corridor = false, bool title = true) => new UI.GuidanceSnapshot(list, level, help, corridor, title);

        // ---------- Relógio ----------

        [Test]
        public void TimelineReinforcesAtTwelveSecondsAndOffersAtThirty()
        {
            var timeline = new UI.GuidanceTimeline();
            Assert.That(timeline.Advance(11.9f), Is.EqualTo(UI.GuidanceLevel.None));
            Assert.That(timeline.Advance(0.1f), Is.EqualTo(UI.GuidanceLevel.Reinforce));
            Assert.That(timeline.Advance(17.8f), Is.EqualTo(UI.GuidanceLevel.Reinforce));
            Assert.That(timeline.Advance(0.2f), Is.EqualTo(UI.GuidanceLevel.Offer));
        }

        [Test]
        public void TimelineStaysAtOfferWithoutLoopingBackToReinforce()
        {
            var timeline = new UI.GuidanceTimeline();
            var changes = 0;
            var last = timeline.Level;
            for (var i = 0; i < 4000; i++)
            {
                var level = timeline.Advance(0.5f);
                if (level != last)
                    changes++;

                last = level;
            }

            Assert.That(changes, Is.EqualTo(2), "None → Reinforce → Offer, e nada mais em 2000 s sem progresso");
            Assert.That(timeline.Level, Is.EqualTo(UI.GuidanceLevel.Offer));
        }

        [Test]
        public void ProgressResetsTheTimelineAndItCanEscalateAgainOnlyAfterTheFullIdleTime()
        {
            var timeline = new UI.GuidanceTimeline();
            timeline.Advance(31f);
            timeline.NoteProgress();
            Assert.That(timeline.Level, Is.EqualTo(UI.GuidanceLevel.None));
            Assert.That(timeline.Advance(11f), Is.EqualTo(UI.GuidanceLevel.None));
            Assert.That(timeline.Advance(1f), Is.EqualTo(UI.GuidanceLevel.Reinforce));
        }

        [Test]
        public void TimelineIgnoresNegativeNaNAndInfiniteTime()
        {
            var timeline = new UI.GuidanceTimeline();
            timeline.Advance(-5f);
            timeline.Advance(float.NaN);
            timeline.Advance(float.PositiveInfinity);
            Assert.That(timeline.IdleSeconds, Is.EqualTo(0f));
        }

        // ---------- Medidor ----------

        [Test]
        public void MeterReachesTheGoalOnceAndIgnoresInvalidAmounts()
        {
            var meter = new UI.ActionMeter(1f);
            Assert.That(meter.Add(-1f), Is.False);
            Assert.That(meter.Add(float.NaN), Is.False);
            Assert.That(meter.Add(0.6f), Is.False);
            Assert.That(meter.Add(0.6f), Is.True, "atingiu agora");
            Assert.That(meter.Add(5f), Is.False, "já atingida: não repete");
            Assert.That(meter.Reached, Is.True);
            meter.Reset();
            Assert.That(meter.Reached, Is.False);
        }

        [Test]
        public void MeterRejectsAnInvalidGoal()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UI.ActionMeter(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UI.ActionMeter(float.PositiveInfinity));
        }

        // ---------- Checklist ----------

        [Test]
        public void ChecklistCompletesEachStepOnceAndInAnyOrder()
        {
            Assert.That(checklist.PendingCount, Is.EqualTo(2));
            Assert.That(checklist.Complete(UI.GuidanceController.InteractStepId), Is.True);
            Assert.That(checklist.Complete(UI.GuidanceController.InteractStepId), Is.False, "idempotente");
            Assert.That(checklist.Complete("inexistente"), Is.False);
            Assert.That(checklist.IsComplete, Is.False);
            Assert.That(checklist.FirstPending(), Is.EqualTo(0));
            Assert.That(checklist.Complete(UI.GuidanceController.MoveStepId), Is.True);
            Assert.That(checklist.IsComplete, Is.True);
            Assert.That(checklist.FirstPending(), Is.EqualTo(-1));
        }

        [Test]
        public void ChecklistRejectsEmptyDuplicatedAndInvalidSteps()
        {
            Assert.Throws<ArgumentException>(() => new UI.GuidanceChecklist(new UI.GuidanceStep[0]));
            var step = new UI.GuidanceStep("a.b", UI.GuidanceKeys.GlyphMove, UI.GuidanceKeys.StepMove, UI.GuidanceKeys.StepMoveReinforce);
            Assert.Throws<ArgumentException>(() => new UI.GuidanceChecklist(new[] { step, step }));
            var invalid = new UI.GuidanceStep("com espaço", UI.GuidanceKeys.GlyphMove, UI.GuidanceKeys.StepMove, UI.GuidanceKeys.StepMoveReinforce);
            Assert.Throws<ArgumentException>(() => new UI.GuidanceChecklist(new[] { invalid }));
        }

        // ---------- Textos ----------

        [Test]
        public void ViewShowsBothPendingInstructionsInPortugueseAndEnglish()
        {
            var pt = UI.GuidanceViewBuilder.Build(Snap(checklist), texts, Pt, HoldMode.Hold, false, false);
            Assert.That(pt.Visible, Is.True);
            Assert.That(pt.Title, Is.EqualTo("COMO JOGAR"));
            Assert.That(pt.Lines.Length, Is.EqualTo(2));
            Assert.That(pt.Lines[0].Glyph, Is.EqualTo("WASD"));
            Assert.That(pt.Lines[0].Text, Is.EqualTo("Mova-se"));
            Assert.That(pt.Lines[1].Text, Is.EqualTo("Segure E"));

            var en = UI.GuidanceViewBuilder.Build(Snap(checklist), texts, En, HoldMode.Hold, false, false);
            Assert.That(en.Title, Is.EqualTo("HOW TO PLAY"));
            Assert.That(en.Lines[0].Text, Is.EqualTo("Move"));
        }

        [Test]
        public void ViewUsesThePressWordingInToggleMode()
        {
            var view = UI.GuidanceViewBuilder.Build(Snap(checklist), texts, Pt, HoldMode.Toggle, false, false);
            Assert.That(view.Lines[1].Text, Is.EqualTo("Pressione E"));

            checklist.Complete(UI.GuidanceController.MoveStepId);
            var reinforced = UI.GuidanceViewBuilder.Build(Snap(checklist, UI.GuidanceLevel.Reinforce), texts, Pt, HoldMode.Toggle, false, false);
            Assert.That(reinforced.Lines[0].Text, Is.EqualTo("Chegue mais perto e pressione E"));
        }

        [Test]
        public void ReinforceEmphasizesOnlyTheFirstPendingInstruction()
        {
            var view = UI.GuidanceViewBuilder.Build(Snap(checklist, UI.GuidanceLevel.Reinforce), texts, Pt, HoldMode.Hold, false, false);
            Assert.That(view.Lines[0].Emphasized, Is.True);
            Assert.That(view.Lines[0].Text, Is.EqualTo("Ainda parado?"));
            Assert.That(view.Lines[1].Emphasized, Is.False);
            Assert.That(view.Lines[1].Text, Is.EqualTo("Segure E"));
            Assert.That(view.HelpOffer, Is.Empty, "a oferta de H só aos 30 s");
        }

        [Test]
        public void OfferShowsHelpAndCorridorOnlyWhenTheChapterDeclaresThem()
        {
            var none = UI.GuidanceViewBuilder.Build(Snap(checklist, UI.GuidanceLevel.Offer), texts, Pt, HoldMode.Hold, false, false);
            Assert.That(none.HelpOffer, Is.Empty);
            Assert.That(none.CorridorOffer, Is.Empty);

            var both = UI.GuidanceViewBuilder.Build(Snap(checklist, UI.GuidanceLevel.Offer, help: true, corridor: true), texts, Pt, HoldMode.Hold, false, false);
            Assert.That(both.HelpOffer, Is.EqualTo("Pressione H"));
            Assert.That(both.CorridorOffer, Is.EqualTo("Volte ao corredor"));
        }

        [Test]
        public void ExtraAssistanceShowsTheHelpHintWithoutWaiting()
        {
            var view = UI.GuidanceViewBuilder.Build(Snap(checklist, UI.GuidanceLevel.None, help: true), texts, Pt, HoldMode.Hold, false, true);
            Assert.That(view.HelpOffer, Is.EqualTo("Dica: H"));

            var withoutHelp = UI.GuidanceViewBuilder.Build(Snap(checklist), texts, Pt, HoldMode.Hold, false, true);
            Assert.That(withoutHelp.HelpOffer, Is.Empty, "sem H no capítulo não há o que oferecer");
        }

        [Test]
        public void CompletedInstructionsAreHiddenUnlessLingering()
        {
            checklist.Complete(UI.GuidanceController.MoveStepId);
            var hidden = UI.GuidanceViewBuilder.Build(Snap(checklist), texts, Pt, HoldMode.Hold, false, false);
            Assert.That(hidden.Lines.Length, Is.EqualTo(1));
            Assert.That(hidden.Lines[0].StepId, Is.EqualTo(UI.GuidanceController.InteractStepId));

            var lingering = UI.GuidanceViewBuilder.Build(Snap(checklist), texts, Pt, HoldMode.Hold, true, false);
            Assert.That(lingering.Lines.Length, Is.EqualTo(2));
            Assert.That(lingering.Lines[0].Done, Is.True);
            Assert.That(lingering.Lines[0].Text, Is.EqualTo("Mova-se · feito ✓"));
        }

        [Test]
        public void FullyCompletedTutorialDisappearsAfterTheLinger()
        {
            checklist.Complete(UI.GuidanceController.MoveStepId);
            checklist.Complete(UI.GuidanceController.InteractStepId);
            Assert.That(UI.GuidanceViewBuilder.Build(Snap(checklist), texts, Pt, HoldMode.Hold, false, false).Visible, Is.False);
            Assert.That(UI.GuidanceViewBuilder.Build(Snap(checklist), texts, Pt, HoldMode.Hold, true, false).Visible, Is.True);
        }

        [Test]
        public void MissingKeyShowsTheKeyItselfSoQaSeesIt()
        {
            var empty = new UI.DossierTextCatalog();
            var view = UI.GuidanceViewBuilder.Build(Snap(checklist), empty, Pt, HoldMode.Hold, false, false);
            Assert.That(view.Lines[0].Text, Is.EqualTo(UI.GuidanceKeys.StepMove));
        }

        [Test]
        public void NoChecklistMeansNothingIsShown()
        {
            Assert.That(UI.GuidanceViewBuilder.Build(Snap(null), texts, Pt, HoldMode.Hold, false, false).Visible, Is.False);
            Assert.That(UI.GuidanceView.Hidden.Signature(), Is.Empty);
        }

        [Test]
        public void SignatureChangesWithLevelLanguageAndCompletionButNotWithRepeatedBuilds()
        {
            string Sig(UI.GuidanceLevel level, string language) =>
                UI.GuidanceViewBuilder.Build(Snap(checklist, level), texts, language, HoldMode.Hold, false, false).Signature();

            Assert.That(Sig(UI.GuidanceLevel.None, Pt), Is.EqualTo(Sig(UI.GuidanceLevel.None, Pt)));
            Assert.That(Sig(UI.GuidanceLevel.None, Pt), Is.Not.EqualTo(Sig(UI.GuidanceLevel.Reinforce, Pt)));
            Assert.That(Sig(UI.GuidanceLevel.None, Pt), Is.Not.EqualTo(Sig(UI.GuidanceLevel.None, En)));
        }

        [Test]
        public void ActivityTasksCarryAKeyboardAlternativeAndValidKeys()
        {
            foreach (var task in new[] { UI.GuidanceTask.Plaque(), UI.GuidanceTask.Tuning(), UI.GuidanceTask.Sweep() })
                Assert.That(task.Step.Validate(), Is.Empty, task.Step.Id);

            Assert.That(UI.GuidanceTask.Plaque().CountsInPanel, Is.True);
            Assert.That(UI.GuidanceTask.Sweep().CountsInPanel, Is.True, "a varredura roda em painel");
            Assert.That(UI.GuidanceTask.Tuning().CountsInPanel, Is.False);
        }
    }
}
