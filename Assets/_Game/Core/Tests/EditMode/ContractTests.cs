using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>CORE-002: validação clara de dados inválidos e semântica dos contratos (via mocks documentados).</summary>
    public class ContractTests
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);
            created.Clear();
        }

        ChapterDefinition Chapter(params ChapterStep[] steps)
        {
            var chapter = ChapterDefinition.Create("test", "T.TITLE", "T.DESC", "T.Q", "TestScene", 360f, "test.intro", steps);
            created.Add(chapter);
            return chapter;
        }

        static ShotKeyframe Frame(float time, float fov = 40f) => new ShotKeyframe(time, Vector3.zero, Vector3.zero, fov);

        static InteractionCandidate Candidate(string id, int priority = 0, float distance = 1f, bool available = true, float hold = 1f) =>
            new InteractionCandidate { Id = id, VerbKey = "V.verb", ObjectKey = "O.obj", Available = available, DistanceMeters = distance, HoldSeconds = hold, Priority = priority };

        // ---------- ChapterDefinition ----------

        [Test]
        public void ExampleChapterIsValid()
        {
            var chapter = ContractExampleFlow.CreateExampleChapter();
            created.Add(chapter);
            Assert.That(chapter.Validate(), Is.Empty);
        }

        [Test]
        public void DefaultChapterReportsErrorsInsteadOfThrowing()
        {
            var chapter = ScriptableObject.CreateInstance<ChapterDefinition>();
            created.Add(chapter);

            List<string> errors = null;
            Assert.DoesNotThrow(() => errors = chapter.Validate());
            Assert.That(errors, Is.Not.Empty);
            Assert.That(string.Join("\n", errors), Does.Contain("chapterId").And.Contain("etapa"));
        }

        [Test]
        public void ChapterRejectsBrokenStepTables()
        {
            var cases = new Dictionary<string, ChapterStep[]>
            {
                ["duplicada"] = new[] { new ChapterStep("A", false, "B"), new ChapterStep("A", false, "B"), new ChapterStep("B", true) },
                ["não existe"] = new[] { new ChapterStep("A", false, "X"), new ChapterStep("B", true) },
                ["nenhuma etapa terminal"] = new[] { new ChapterStep("A", false, "B"), new ChapterStep("B", false, "A") },
                ["inalcançável"] = new[] { new ChapterStep("A", false, "B"), new ChapterStep("B", true), new ChapterStep("C", true) },
                ["não pode ter sucessoras"] = new[] { new ChapterStep("A", true, "B"), new ChapterStep("B", true) },
                ["travaria"] = new[] { new ChapterStep("A", false), new ChapterStep("B", true) },
                ["aponta para si"] = new[] { new ChapterStep("A", false, "A", "B"), new ChapterStep("B", true) },
                ["inválido"] = new[] { new ChapterStep("1 ruim", false, "B"), new ChapterStep("B", true) },
            };

            foreach (var pair in cases)
            {
                var errors = Chapter(pair.Value).Validate();
                Assert.That(string.Join("\n", errors), Does.Contain(pair.Key), $"caso '{pair.Key}' não foi acusado");
            }
        }

        [Test]
        public void ChapterRejectsInvalidScalarFields()
        {
            var chapter = ChapterDefinition.Create("Bad Id", "", "D", "Q", "", -1f, "", new[] { new ChapterStep("A", true) });
            created.Add(chapter);
            var text = string.Join("\n", chapter.Validate());
            Assert.That(text, Does.Contain("chapterId").And.Contain("titleKey").And.Contain("sceneName")
                .And.Contain("targetDurationSeconds").And.Contain("initialSequenceId"));
        }

        [Test]
        public void TransitionsComeOnlyFromTheTable()
        {
            var chapter = Chapter(new ChapterStep("A", false, "B"), new ChapterStep("B", true));
            Assert.That(chapter.CanTransition("A", "B"), Is.True);
            Assert.That(chapter.CanTransition("B", "A"), Is.False);
            Assert.That(chapter.CanTransition("A", "Z"), Is.False);
            Assert.That(chapter.IsTerminalStep("B"), Is.True);
        }

        // ---------- ShotDefinition / SubtitleCue / SequenceDefinition ----------

        [Test]
        public void ShotValidation()
        {
            Assert.That(new ShotDefinition("s1", 5f, "rig.main", new[] { Frame(0f), Frame(5f) }).Validate(), Is.Empty);

            var broken = new ShotDefinition("s2", 5f, "rig.main", new[] { Frame(1f, 5f), Frame(0.5f, 200f), Frame(4f) },
                events: new[] { new ShotEvent(9f, "late") });
            var text = string.Join("\n", broken.Validate());
            Assert.That(text, Does.Contain("primeiro keyframe").And.Contain("FOV").And.Contain("não é crescente")
                .And.Contain("coincidir com a duração").And.Contain("fora do plano"));

            Assert.That(string.Join("\n", new ShotDefinition("s3", 5f, "rig.main", new[] { Frame(0f) }).Validate()), Does.Contain("2 keyframes"));
            Assert.That(string.Join("\n", new ShotDefinition().Validate()), Does.Contain("shotId").And.Contain("rigId"));
        }

        [Test]
        public void SubtitleCueRequiresTimeWindowOrTriggerNotBoth()
        {
            Assert.That(new SubtitleCue("c1", "MAR.C01", 0f, 8f).Validate(), Is.Empty);
            Assert.That(SubtitleCue.Triggered("c2", "MAR.C12", "result.shown", 4f).Validate(), Is.Empty);
            Assert.That(string.Join("\n", new SubtitleCue("c3", "MAR.C01", 5f, 5f).Validate()), Does.Contain("fim"));
        }

        [Test]
        public void SequenceUsesOneClockAndChecksCuesAndMarks()
        {
            var shot = new ShotDefinition("s1", 5f, "rig.main", new[] { Frame(0f), Frame(5f) });
            var good = SequenceDefinition.Create("seq", new[] { shot }, new[] { new SubtitleCue("c1", "K.one", 0f, 5f) },
                new[] { new ActorMark("rover", 2f, Vector3.zero, Vector3.zero) });
            created.Add(good);
            Assert.That(good.Validate(), Is.Empty);
            Assert.That(good.TotalDurationSeconds, Is.EqualTo(5f));

            var bad = SequenceDefinition.Create("seq2", new[] { shot, shot }, new[] { new SubtitleCue("c1", "K.one", 0f, 11f) },
                new[] { new ActorMark("rover", 12f, Vector3.zero, Vector3.zero) });
            created.Add(bad);
            Assert.That(string.Join("\n", bad.Validate()), Does.Contain("duplicado").And.Contain("depois do fim").And.Contain("fora da sequência"));
        }

        // ---------- EvidenceRecord / EvidenceStore ----------

        [Test]
        public void EvidenceAuthorshipRules()
        {
            Assert.That(new EvidenceRecord("e1", EvidenceKind.Photo, "R.one", "MS10", EvidenceAuthorship.PlayerCapture).Validate(), Is.Empty);
            Assert.That(new EvidenceRecord("e1", EvidenceKind.Photo, "R.one", "MS10", EvidenceAuthorship.PlayerCapture).CanBePresentedAsPlayers, Is.True);

            var notPhoto = new EvidenceRecord("e2", EvidenceKind.Observation, "R.one", "MS10", EvidenceAuthorship.PlayerCapture);
            Assert.That(string.Join("\n", notPhoto.Validate()), Does.Contain("PlayerCapture"));

            var archiveNoCredit = new EvidenceRecord("e3", EvidenceKind.Photo, "R.one", "P-S09", EvidenceAuthorship.Archive);
            Assert.That(string.Join("\n", archiveNoCredit.Validate()), Does.Contain("creditKey"));
            Assert.That(new EvidenceRecord("e3", EvidenceKind.Photo, "R.one", "P-S09", EvidenceAuthorship.Archive, creditKey: "CREDIT.nasa").Validate(), Is.Empty);
            Assert.That(archiveNoCredit.CanBePresentedAsPlayers, Is.False);
        }

        [Test]
        public void EvidenceStoreIsIdempotentAndRejectsInvalidRecords()
        {
            var store = new MockEvidenceStore();
            var added = 0;
            store.Added += _ => added++;
            var record = new EvidenceRecord("e1", EvidenceKind.Observation, "R.one", "MS10", EvidenceAuthorship.Reconstruction);

            Assert.That(store.Add(record), Is.True);
            Assert.That(store.Add(new EvidenceRecord("e1", EvidenceKind.Composition, "R.two", "MS11", EvidenceAuthorship.Reconstruction)), Is.False);
            Assert.That(added, Is.EqualTo(1));
            Assert.That(store.Records.Count, Is.EqualTo(1));
            Assert.That(store.TryGet("e1", out var kept) && kept.ResultKey == "R.one", Is.True, "o primeiro registro é mantido");

            Assert.Throws<ArgumentNullException>(() => store.Add(null));
            var ex = Assert.Throws<ArgumentException>(() => store.Add(new EvidenceRecord()));
            Assert.That(ex.Message, Does.Contain("evidenceId"));

            store.Clear();
            Assert.That(store.Contains("e1"), Is.False);
        }

        // ---------- InteractionCandidate / InteractionService ----------

        [Test]
        public void CandidateValidation()
        {
            Assert.That(Candidate("c1").Validate(), Is.Empty);
            var bad = new InteractionCandidate { Id = "", HoldSeconds = -1f, DistanceMeters = -2f };
            Assert.That(string.Join("\n", bad.Validate()), Does.Contain("Id").And.Contain("VerbKey").And.Contain("HoldSeconds").And.Contain("DistanceMeters"));
        }

        [Test]
        public void InteractionRejectsInvalidCandidateWithClearMessage()
        {
            var service = new MockInteractionService();
            var ex = Assert.Throws<ArgumentException>(() => service.SetCandidate(new InteractionCandidate()));
            Assert.That(ex.Message, Does.Contain("Id"));
        }

        [Test]
        public void OnlyTheActiveAvailableCandidateCanBeginAndOptionalOnesDoNotStealTheMainAction()
        {
            var service = new MockInteractionService();
            service.SetCandidate(Candidate("main", priority: 10, distance: 3f));
            service.SetCandidate(Candidate("optional", priority: 1, distance: 0.5f));
            service.SetCandidate(Candidate("far", priority: 10, distance: 8f));
            service.SetCandidate(Candidate("blocked", priority: 99, available: false));

            Assert.That(service.TryGetActive(out var active) && active.Id == "main", Is.True);
            Assert.That(service.CanBeginHold("optional"), Is.False);
            Assert.That(service.CanBeginHold("blocked"), Is.False);
            Assert.That(service.BeginHold("optional"), Is.False);
            Assert.That(service.BeginHold("main"), Is.True);
        }

        [Test]
        public void HoldProgressesCompletesAndRequiresRearm()
        {
            var service = new MockInteractionService();
            var log = new List<string>();
            service.HoldStarted += id => log.Add("start:" + id);
            service.HoldProgress += (id, p) => log.Add("progress:" + p.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            service.HoldCompleted += id => log.Add("done:" + id);
            service.SetCandidate(Candidate("c", hold: 2f));

            Assert.That(service.BeginHold("c"), Is.True);
            service.Tick(1f);
            service.Tick(1f);

            Assert.That(log, Is.EqualTo(new[] { "start:c", "progress:0.5", "progress:1.0", "done:c" }));
            Assert.That(service.AwaitingRelease, Is.True);
            Assert.That(service.CanBeginHold("c"), Is.False, "E mantido depois de concluir não inicia outro hold");
            Assert.That(service.BeginHold("c"), Is.False);

            service.NotifyInteractReleased();
            Assert.That(service.BeginHold("c"), Is.True);
        }

        [Test]
        public void TheEThatOpensAnActivityDoesNotStartTheNextHold()
        {
            var service = new MockInteractionService();
            service.SetCandidate(Candidate("c"));
            service.RequireRelease();

            Assert.That(service.BeginHold("c"), Is.False);
            service.NotifyInteractReleased();
            Assert.That(service.BeginHold("c"), Is.True);
        }

        [Test]
        public void ReleasingCancelsTheHoldWithoutCompletingIt()
        {
            var service = new MockInteractionService();
            var canceled = new List<string>();
            var completed = 0;
            service.HoldCanceled += canceled.Add;
            service.HoldCompleted += _ => completed++;
            service.SetCandidate(Candidate("c", hold: 4f));

            service.BeginHold("c");
            service.Tick(1f);
            service.NotifyInteractReleased();
            service.Tick(10f);

            Assert.That(canceled, Is.EqualTo(new[] { "c" }));
            Assert.That(completed, Is.EqualTo(0));
            Assert.That(service.HoldingId, Is.Null);
            Assert.That(service.BeginHold("c"), Is.True, "soltar não exige rearme extra");
        }

        [Test]
        public void ClearingOrLosingTheActiveCandidateCancelsTheHold()
        {
            var service = new MockInteractionService();
            var canceled = new List<string>();
            service.HoldCanceled += canceled.Add;
            service.SetCandidate(Candidate("c"));
            service.BeginHold("c");
            service.SetCandidate(Candidate("c", available: false));
            Assert.That(canceled, Is.EqualTo(new[] { "c" }));

            service.SetCandidate(Candidate("c"));
            service.BeginHold("c");
            service.ClearCandidate("c");
            Assert.That(canceled.Count, Is.EqualTo(2));
            Assert.That(service.TryGetActive(out _), Is.False);
        }

        // ---------- ChapterDirector ----------

        [Test]
        public void DirectorRejectsNullAndInvalidChapters()
        {
            var director = new MockChapterDirector();
            Assert.Throws<ArgumentNullException>(() => director.Begin(null));
            var invalid = ScriptableObject.CreateInstance<ChapterDefinition>();
            created.Add(invalid);
            var ex = Assert.Throws<ArgumentException>(() => director.Begin(invalid));
            Assert.That(ex.Message, Does.Contain("chapterId"));
        }

        [Test]
        public void CompleteStepIsIdempotentAndFollowsTheTable()
        {
            var director = new MockChapterDirector();
            var entered = new List<string>();
            director.StepEntered += entered.Add;
            director.Begin(Chapter(new ChapterStep("A", false, "B"), new ChapterStep("B", false, "C"), new ChapterStep("C", true)));

            Assert.That(director.CompleteStep("B"), Is.False, "etapa futura não pode ser concluída");
            Assert.That(director.CompleteStep("A"), Is.True);
            Assert.That(director.CompleteStep("A"), Is.False, "repetir o mesmo ID não faz nada");
            Assert.That(director.CurrentStepId, Is.EqualTo("B"));
            Assert.That(entered, Is.EqualTo(new[] { "A", "B" }));
        }

        [Test]
        public void BranchingNeedsAnExplicitNextStep()
        {
            var director = new MockChapterDirector();
            director.Begin(Chapter(new ChapterStep("A", false, "B", "C"), new ChapterStep("B", true), new ChapterStep("C", true)));

            Assert.That(director.CompleteStep("A"), Is.False, "ambígua sem sucessora explícita");
            Assert.That(director.CompleteStep("A", "Z"), Is.False, "fora da tabela");
            Assert.That(director.CompleteStep("A", "C"), Is.True);
            Assert.That(director.CurrentStepId, Is.EqualTo("C"));
        }

        [Test]
        public void FinishingIsOnceOnlyAndOnlyFromATerminalStep()
        {
            var director = new MockChapterDirector();
            var finished = new List<string>();
            director.ChapterFinished += finished.Add;
            director.Begin(Chapter(new ChapterStep("A", false, "B"), new ChapterStep("B", true)));

            director.FinishChapter();
            Assert.That(director.IsFinished, Is.False, "FinishChapter fora da etapa terminal é ignorado");

            director.CompleteStep("A");
            Assert.That(director.CompleteStep("B"), Is.True);
            director.FinishChapter();
            Assert.That(director.CompleteStep("B"), Is.False);

            Assert.That(finished, Is.EqualTo(new[] { "test" }));
            Assert.That(director.IsFinished, Is.True);
            Assert.That(director.CurrentStepId, Is.Null);
        }

        [Test]
        public void RestartDiscardsStateAndReturnsToTheInitialStep()
        {
            var director = new MockChapterDirector();
            director.Begin(Chapter(new ChapterStep("A", false, "B"), new ChapterStep("B", true)));
            director.CompleteStep("A");
            director.CompleteStep("B");

            director.RestartChapter();

            Assert.That(director.IsFinished, Is.False);
            Assert.That(director.CurrentStepId, Is.EqualTo("A"));
            Assert.That(director.CompleteStep("A"), Is.True, "a etapa concluída antes do reinício pode ser concluída de novo");
        }

        // ---------- Exemplo ponta a ponta ----------

        [Test]
        public void ExampleFlowRequestsInteractionPresentsEvidenceAndCompletesTheChapter()
        {
            var director = new MockChapterDirector();
            var interaction = new MockInteractionService();
            var evidence = new MockEvidenceStore();
            var flow = new ContractExampleFlow(director, interaction, evidence);
            var chapter = ContractExampleFlow.CreateExampleChapter();
            created.Add(chapter);

            director.Begin(chapter);
            Assert.That(director.CurrentStepId, Is.EqualTo(ContractExampleFlow.IntroStep));
            Assert.That(interaction.TryGetActive(out _), Is.False, "nenhuma interação antes da etapa Observe");

            Assert.That(flow.AcknowledgeIntro(), Is.True);
            Assert.That(flow.AcknowledgeIntro(), Is.False, "repetir é inofensivo");
            Assert.That(interaction.TryGetActive(out var active) && active.Id == ContractExampleFlow.RockCandidateId, Is.True);

            Assert.That(interaction.BeginHold(ContractExampleFlow.RockCandidateId), Is.True);
            interaction.Tick(1f);
            Assert.That(evidence.Records, Is.Empty, "nunca concluir a descoberta só porque o tempo passou");
            interaction.Tick(1f);

            Assert.That(evidence.Contains(ContractExampleFlow.RockEvidenceId), Is.True);
            Assert.That(director.IsFinished, Is.True);
            Assert.That(interaction.TryGetActive(out _), Is.False);

            flow.Dispose();
        }
    }
}
