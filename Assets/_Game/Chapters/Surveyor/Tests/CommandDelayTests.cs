using System;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.CommandTests
{
    public sealed class CommandDelayTests : InputTestFixture
    {
        sealed class LoadedScene : ChapterDirector.ISceneLoader
        {
            public void Load(string name,Action loaded,Action<string> failed) => loaded();
            public void Unload(string name,Action unloaded) => unloaded();
        }
        GameObject root;
        ChapterDirector owner;
        SessionClock clock;
        InputRouter router;
        InteractionService service;
        SurveyorCommandDelay queue;
        SurveyorCommandInteraction action;
        ChapterDefinition chapter;
        IDisposable panel;
        Keyboard keyboard;
        int executions;
        float desired, actual;

        public override void Setup()
        {
            base.Setup(); keyboard = InputSystem.AddDevice<Keyboard>(); root = new GameObject("SUR-004 test");
            owner = root.AddComponent<ChapterDirector>(); clock = root.AddComponent<SessionClock>();
            router = root.AddComponent<InputRouter>(); service = root.AddComponent<InteractionService>();
            queue = root.AddComponent<SurveyorCommandDelay>(); action = root.AddComponent<SurveyorCommandInteraction>();
            owner.Initialize(clock,router,new LoadedScene(),new EvidenceStore()); router.Enable(); service.Initialize(router,clock);
            queue.Configure(owner,clock); action.Configure(queue,service,router);
            chapter = ChapterDefinition.Create("surveyor.command.test","a","b","c","CommandDelayTest",30,"test.sequence",
                new[] { new ChapterStep("Observe",false,"Sample"),new ChapterStep("Sample",false,"End"),new ChapterStep("End",true) });
            owner.OpenDossier(); owner.Begin(chapter); queue.BeginState("Observe"); panel = router.Acquire(InputContext.Panel,"test");
            executions = 0; desired = 30; actual = 0;
        }
        public override void TearDown()
        {
            action.ClearAction(); queue.EndState(); panel.Dispose(); router.Disable();
            Object.DestroyImmediate(root); Object.DestroyImmediate(chapter); base.TearDown();
        }
        bool Offer() => action.Offer(new InteractionCandidate { Id="mirror.point",VerbKey="cmd.mirror",ObjectKey="cmd.kicker",
            Available=true,HoldSeconds=1.5f,Priority=100 }, () =>
            {
                float captured = desired;
                return () => { executions++; actual = captured; };
            });
        void Advance(float seconds)
        {
            while (seconds > .0001f)
            {
                float delta = Mathf.Min(.05f,seconds); clock.Tick(delta); service.Tick(clock.ActiveDeltaSeconds); queue.Tick(); seconds -= delta;
            }
        }
        void Confirm() { Assert.That(Offer(),Is.True); Press(keyboard.eKey); Advance(1.55f); Release(keyboard.eKey); }

        [Test] public void OnlyOnePendingOrderAndOneExecutionDespiteRepeatedTicks()
        {
            Assert.That(queue.TrySend("one",()=>executions++),Is.True);
            Assert.That(queue.TrySend("two",()=>executions++),Is.False);
            Advance(1.25f); Assert.That(executions,Is.Zero);
            Advance(.05f); Assert.That(executions,Is.EqualTo(1));
            Advance(5); queue.Tick(); Assert.That(executions,Is.EqualTo(1));
        }
        [Test] public void SendingDoesNotConsumeTheCurrentFrameDelta()
        {
            clock.Tick(.1f); queue.TrySend("one",()=>executions++); queue.Tick();
            Assert.That(queue.ElapsedSeconds,Is.Zero); Assert.That(executions,Is.Zero);
        }
        [Test] public void PauseAtPointSixPreservesRemainingTransit()
        {
            queue.TrySend("one",()=>executions++); Advance(.6f);
            Assert.That(queue.RemainingSeconds,Is.EqualTo(.7f).Within(.001));
            owner.Pause("test"); Advance(15); Assert.That(queue.RemainingSeconds,Is.EqualTo(.7f).Within(.001));
            Assert.That(executions,Is.Zero); owner.Resume("test");
            Advance(.65f); Assert.That(executions,Is.Zero); Advance(.05f); Assert.That(executions,Is.EqualTo(1));
        }
        [Test] public void RestartInvalidatesOldGenerationAndDoesNotAutoReenter()
        {
            queue.TrySend("one",()=>executions++); Advance(.6f); uint generation = queue.Generation;
            owner.RestartChapter(); Advance(3);
            Assert.That(queue.Generation,Is.GreaterThan(generation)); Assert.That(queue.OwnsState,Is.False); Assert.That(executions,Is.Zero);
            queue.BeginState("Observe"); queue.TrySend("new",()=>executions++); Advance(1.3f); Assert.That(executions,Is.EqualTo(1));
        }
        [Test] public void ChangingStepInvalidatesThePendingPhysicalOperation()
        {
            queue.TrySend("one",()=>executions++); Advance(.6f); owner.CompleteStep("Observe","Sample"); Advance(3);
            Assert.That(executions,Is.Zero); Assert.That(queue.Status,Is.EqualTo(CommandTransitStatus.Inactive));
        }
        [Test] public void ReturnToDossierCancelsAndStopsTime()
        {
            queue.TrySend("one",()=>executions++); owner.ReturnToDossier(); Advance(5);
            Assert.That(executions,Is.Zero); Assert.That(queue.HasPending,Is.False); Assert.That(clock.IsRunning,Is.False);
        }
        [Test] public void ExplicitExitAndReentryNeverReviveTheOldCallback()
        {
            queue.TrySend("one",()=>executions++); queue.EndState(); queue.BeginState("Observe"); Advance(3);
            Assert.That(executions,Is.Zero); Assert.That(queue.Status,Is.EqualTo(CommandTransitStatus.Ready));
        }
        [Test] public void ReconfigureRemovesOldBindingsAndOperations()
        {
            queue.TrySend("old",()=>executions++); queue.Configure(owner,clock); queue.Configure(owner,clock);
            queue.BeginState("Observe"); queue.TrySend("new",()=>executions++); Advance(3); Assert.That(executions,Is.EqualTo(1));
        }
        [Test] public void ReleasingKeepsPreparationWithoutSending()
        {
            Offer(); Press(keyboard.eKey); Advance(.6f); Release(keyboard.eKey);
            Assert.That(action.PreparationProgress,Is.EqualTo(.4f).Within(.001)); Advance(3);
            Assert.That(queue.HasPending,Is.False); Assert.That(action.PreparationProgress,Is.EqualTo(.4f).Within(.001));
            Press(keyboard.eKey); Advance(.95f); Release(keyboard.eKey);
            Assert.That(queue.HasPending,Is.True); Advance(1.3f); Assert.That(executions,Is.EqualTo(1));
        }
        [Test] public void PauseDuringHoldPreservesPreparationAndRequiresKeyboardRearm()
        {
            Offer(); Press(keyboard.eKey); Advance(.6f); owner.Pause("test"); Advance(2); owner.Resume("test"); Advance(2);
            Assert.That(action.PreparationProgress,Is.EqualTo(.4f).Within(.001)); Assert.That(queue.HasPending,Is.False);
            Release(keyboard.eKey); Press(keyboard.eKey); Advance(.95f); Release(keyboard.eKey); Assert.That(queue.HasPending,Is.True);
        }
        [Test] public void ToggleModePausesAndResumesPreparation()
        {
            service.Mode=HoldMode.Toggle; Offer(); Press(keyboard.eKey); Release(keyboard.eKey); Advance(.6f);
            Press(keyboard.eKey); Release(keyboard.eKey); Advance(3); Assert.That(action.PreparationProgress,Is.EqualTo(.4f).Within(.001));
            Press(keyboard.eKey); Release(keyboard.eKey); Advance(.95f); Assert.That(queue.HasPending,Is.True);
        }
        [Test] public void HeldKeyCannotConfirmTheNextOfferedAction()
        {
            queue.Received += receipt => Offer(); Offer(); Press(keyboard.eKey); Advance(5);
            Assert.That(executions,Is.EqualTo(1)); Assert.That(action.NeedsRelease,Is.True);
            Advance(3); Assert.That(executions,Is.EqualTo(1)); Release(keyboard.eKey); Press(keyboard.eKey); Advance(3);
            Release(keyboard.eKey); Assert.That(executions,Is.EqualTo(2));
        }
        [Test] public void HeldKeyAcrossStateChangeDoesNotPrepareTheNextInstrument()
        {
            Offer(); Press(keyboard.eKey); Advance(.6f); owner.CompleteStep("Observe","Sample");
            queue.BeginState("Sample"); Offer(); Advance(4);
            Assert.That(action.PreparationProgress,Is.Zero); Assert.That(executions,Is.Zero);
            Release(keyboard.eKey); Press(keyboard.eKey); Advance(3); Release(keyboard.eKey); Assert.That(executions,Is.EqualTo(1));
        }
        [Test] public void SentTargetIsASnapshotAndCursorContinuesToRespond()
        {
            Confirm(); desired=-40; Assert.That(actual,Is.Zero); Advance(1.3f);
            Assert.That(actual,Is.EqualTo(30)); Assert.That(desired,Is.EqualTo(-40));
        }
        [Test] public void SentEventIsImmediateAndCancellationInsideItIsSafe()
        {
            int sent=0; queue.Sent += receipt => { sent++; Assert.That(queue.ElapsedSeconds,Is.Zero); queue.EndState(); };
            Assert.That(queue.TrySend("one",()=>executions++),Is.True); Assert.That(sent,Is.EqualTo(1));
            Advance(3); Assert.That(executions,Is.Zero);
        }
        [Test] public void ReentrantOperationCannotExecuteOrEnqueueItselfAgain()
        {
            bool duplicate=true;
            queue.TrySend("one",()=>{ executions++; queue.Tick(); duplicate=queue.TrySend("bad",()=>executions++); });
            Advance(3); Assert.That(executions,Is.EqualTo(1)); Assert.That(duplicate,Is.False);
        }
        [Test] public void PhysicalCallbackCanChangeStateWithoutPublishingOldReceipt()
        {
            int received=0; queue.Received += receipt=>received++;
            queue.TrySend("one",()=>
            {
                executions++; owner.CompleteStep("Observe","Sample"); queue.BeginState("Sample"); Assert.That(Offer(),Is.True);
            });
            Advance(1.3f); Assert.That(executions,Is.EqualTo(1)); Assert.That(received,Is.Zero); Assert.That(action.CanConfirm,Is.True);
        }
        [Test] public void ThrowingOperationIsConsumedOnceAndErrorRemainsVisible()
        {
            queue.TrySend("bad",()=>{ executions++; throw new InvalidOperationException("instrument failure"); });
            clock.Tick(.1f);
            for(int i=0;i<11;i++){clock.Tick(.1f);queue.Tick();}
            clock.Tick(.1f); Assert.Throws<InvalidOperationException>(()=>queue.Tick());
            Assert.That(queue.Status,Is.EqualTo(CommandTransitStatus.Faulted)); Advance(3); Assert.That(executions,Is.EqualTo(1));
        }
        [TestCase(30)] [TestCase(60)]
        public void TransitUsesActiveTimeAtBothFrameRates(int fps)
        {
            queue.TrySend("one",()=>executions++);
            for(int i=0;i<Mathf.RoundToInt(1.3f*fps)-1;i++){clock.Tick(1f/fps);queue.Tick();}
            Assert.That(executions,Is.Zero);clock.Tick(1f/fps);queue.Tick();Assert.That(executions,Is.EqualTo(1));
        }
        [Test] public void InvalidOrInactiveRequestsDoNotMutateTheQueue()
        {
            Assert.That(queue.TrySend("bad id",()=>{}),Is.False); Assert.That(queue.TrySend("valid",null),Is.False);
            owner.Pause("test"); Assert.That(queue.TrySend("paused",()=>{}),Is.False); owner.Resume("test");
            Assert.That(queue.BeginState("NotCurrent"),Is.False); owner.ReturnToDossier();
            Assert.That(queue.TrySend("dossier",()=>{}),Is.False); Assert.That(queue.BeginState("Observe"),Is.False);
        }
        [Test] public void HudBindsSentInTheSameFrameWhileThePhysicalEffectIsStillPending()
        {
            var tree=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SurveyorCommandBuilder.UiFolder+"/CommandTransit.uxml").CloneTree();
            var view=root.AddComponent<CommandTransitView>(); Offer();
            view.Configure(tree,AssetDatabase.LoadAssetAtPath<TextAsset>(SurveyorCommandBuilder.UiFolder+"/CommandTransitTexts.json"),queue,action,new SettingsData{languageCode="pt"}); // o padrão do jogo é inglês; o teste confere a troca PT → EN
            ConfirmWithoutOffer();
            Assert.That(tree.Q<Label>("command-status").text,Does.StartWith("Enviada"));
            Assert.That(view.LastFeedbackFrames,Is.Zero); Assert.That(executions,Is.Zero);
            view.ApplySettings(new SettingsData{languageCode="en"});
            Assert.That(tree.Q<Label>("command-status").text,Does.StartWith("Sent"));
            Assert.That(tree.Q<Label>("transit-description").text,Does.Contain("1.3"));
        }
        void ConfirmWithoutOffer() { Press(keyboard.eKey); Advance(1.55f); Release(keyboard.eKey); }
    }
}
