using System;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.LaunchTests
{
    public sealed class SurveyorLaunchTests : InputTestFixture
    {
        sealed class LoadedScene : ChapterDirector.ISceneLoader
        {
            public void Load(string name,Action loaded,Action<string> failed)=>loaded();
            public void Unload(string name,Action unloaded)=>unloaded();
        }
        GameObject root;
        ChapterDirector owner;
        SessionClock clock;
        InputRouter router;
        InteractionService interactions;
        SequenceDirector sequence;
        CameraDirector camera;
        SurveyorLaunchController flow;
        SequenceDefinition data;
        ChapterDefinition chapter;
        Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();keyboard=InputSystem.AddDevice<Keyboard>();root=new GameObject("SUR-003 test");
            clock=root.AddComponent<SessionClock>();router=root.AddComponent<InputRouter>();
            owner=root.AddComponent<ChapterDirector>();interactions=root.AddComponent<InteractionService>();
            camera=root.AddComponent<CameraDirector>();camera.Initialize(root.AddComponent<Camera>());
            sequence=root.AddComponent<SequenceDirector>();sequence.Initialize(camera,null,clock);
            data=SurveyorLaunchBuilder.CreateSequence();
            chapter=ChapterDefinition.Create("surveyor.launch.test","a","b","c","SurveyorLaunchTest",28,data.SequenceId,
                new[] {new ChapterStep("Launch",false,"Cruise"),new ChapterStep("Cruise",false,"End"),new ChapterStep("End",true)});
            owner.Initialize(clock,router,new LoadedScene(),new EvidenceStore());router.Enable();interactions.Initialize(router,clock);
            flow=root.AddComponent<SurveyorLaunchController>();flow.Configure(owner,router,interactions,sequence,data,clock);
            owner.OpenDossier();owner.Begin(chapter);
        }
        public override void TearDown()
        {
            flow.Stop();router.Disable();Object.DestroyImmediate(root);Object.DestroyImmediate(data);Object.DestroyImmediate(chapter);base.TearDown();
        }
        void Advance(float seconds)
        {
            while(seconds>.0001f)
            {
                float delta=Mathf.Min(.05f,seconds);clock.Tick(delta);
                interactions.Tick(clock.ActiveDeltaSeconds);flow.Tick(clock.ActiveDeltaSeconds);seconds-=delta;
            }
        }
        void Ready()=>Advance(17.05f);
        void Confirm(){Press(keyboard.eKey);Advance(2.55f);Release(keyboard.eKey);}

        [Test] public void GateNeverExpiresOrAdvancesWithoutConfirmation()
        {
            flow.Tick(200);flow.Tick(200);
            Assert.That(flow.TimeSeconds,Is.EqualTo(flow.SendAt).Within(.001));
            Assert.That(flow.CommandAvailable,Is.True);Assert.That(owner.CurrentStepId,Is.EqualTo("Launch"));
            Assert.That(flow.Executed,Is.False);
        }
        [Test] public void HeldBeforeTheCandidateAppearsRequiresRelease()
        {
            Press(keyboard.eKey);Ready();Advance(4);
            Assert.That(flow.Progress,Is.Zero);Assert.That(flow.NeedsRelease,Is.True);
            Release(keyboard.eKey);Confirm();Assert.That(flow.Prepared,Is.True);
        }
        [Test] public void ReleasingPreservesProgressAndOnlyTheRemainderIsNeeded()
        {
            Ready();Press(keyboard.eKey);Advance(1);Release(keyboard.eKey);
            Assert.That(flow.Progress,Is.EqualTo(.4f).Within(.01));
            Advance(10);Assert.That(flow.Progress,Is.EqualTo(.4f).Within(.01));
            Press(keyboard.eKey);Advance(1.55f);Release(keyboard.eKey);
            Assert.That(flow.Prepared,Is.True);Assert.That(flow.Sent,Is.True);
        }
        [Test] public void PauseFreezesBothClocksAndHeldKeyCannotResumeAnOperation()
        {
            Ready();Press(keyboard.eKey);Advance(.6f);float time=flow.TimeSeconds,progress=flow.Progress;
            owner.Pause("test");Advance(10);
            Assert.That(flow.TimeSeconds,Is.EqualTo(time));Assert.That(flow.Progress,Is.EqualTo(progress));
            owner.Resume("test");Advance(1);Assert.That(flow.Progress,Is.EqualTo(progress));
            Release(keyboard.eKey);Press(keyboard.eKey);Advance(1.95f);Release(keyboard.eKey);
            Assert.That(flow.Prepared,Is.True);
        }
        [Test] public void DelayedCommandExecutesOnceThenOnlyChapterOwnerAdvances()
        {
            int executed=0,cruise=0;flow.CentaurExecuted+=()=>executed++;owner.StepEntered+=s=>{if(s=="Cruise")cruise++;};
            Ready();Confirm();Assert.That(flow.Prepared,Is.True);Assert.That(flow.Sent,Is.False);
            Advance(2.4f);Assert.That(flow.Sent,Is.True);Assert.That(flow.Executed,Is.False);
            Advance(1.1f);Assert.That(executed,Is.Zero);
            Advance(.3f);Assert.That(executed,Is.EqualTo(1));Assert.That(owner.CurrentStepId,Is.EqualTo("Launch"));
            Advance(10);flow.Tick(100);
            Assert.That(owner.CurrentStepId,Is.EqualTo("Cruise"));Assert.That(cruise,Is.EqualTo(1));Assert.That(executed,Is.EqualTo(1));
            Assert.That(camera.IsControlled,Is.False);Assert.That(owner.IsFinished,Is.False);
        }
        [Test] public void LateConfirmationRetainsSubtitleReadTime()
        {
            Advance(80);Assert.That(flow.TimeSeconds,Is.EqualTo(22).Within(.001));
            Confirm();float sentAt=flow.TimeSeconds;Advance(4);
            Assert.That(owner.CurrentStepId,Is.EqualTo("Launch"));Assert.That(flow.SubtitleKey,Is.EqualTo("SUR-S03"));
            Advance(3);Assert.That(flow.ActComplete,Is.True);Assert.That(sentAt,Is.GreaterThanOrEqualTo(flow.SendAt));
        }
        [Test] public void RestartInvalidatesInFlightCommandAndDoesNotDuplicateCallbacks()
        {
            int count=0;flow.CentaurExecuted+=()=>count++;Ready();Confirm();Advance(2.5f);
            owner.RestartChapter();Assert.That(flow.Progress,Is.Zero);Assert.That(flow.TimeSeconds,Is.Zero);
            Advance(10);Assert.That(count,Is.Zero);
            Advance(7.1f);Confirm();Advance(10);Assert.That(count,Is.EqualTo(1));Assert.That(flow.ActComplete,Is.True);
        }
        [Test] public void ReturnClearsCandidateAndCameraAndReentryStartsFromZero()
        {
            Ready();Press(keyboard.eKey);Advance(.8f);owner.ReturnToDossier();
            Assert.That(flow.Running,Is.False);Assert.That(interactions.TryGetActive(out _),Is.False);Assert.That(camera.IsControlled,Is.False);
            Release(keyboard.eKey);owner.Begin(chapter);Assert.That(flow.Progress,Is.Zero);Assert.That(flow.TimeSeconds,Is.Zero);
        }
        [Test] public void ToggleModeKeepsRunningAfterReleaseAndCanBePaused()
        {
            Ready();interactions.Mode=HoldMode.Toggle;Press(keyboard.eKey);Release(keyboard.eKey);Advance(1);
            Press(keyboard.eKey);Release(keyboard.eKey);float progress=flow.Progress;Advance(2);
            Assert.That(flow.Progress,Is.EqualTo(progress));Press(keyboard.eKey);Release(keyboard.eKey);Advance(1.6f);
            Assert.That(flow.Prepared,Is.True);
        }
        [Test] public void ReconfigureDoesNotAccumulateHandlersOrCameraLeases()
        {
            flow.Configure(owner,router,interactions,sequence,data,clock);flow.Configure(owner,router,interactions,sequence,data,clock);
            int count=0;flow.CentaurExecuted+=()=>count++;Ready();Confirm();Advance(10);
            Assert.That(count,Is.EqualTo(1));Assert.That(camera.IsControlled,Is.False);
        }
        [Test] public void DataUsesTheCommonThreeCuesAndBilingualIds()
        {
            Assert.That(data.Validate(),Is.Empty);Assert.That(data.TotalDurationSeconds,Is.EqualTo(28));
            Assert.That(data.Cues.Select(c=>c.CueId),Is.EqualTo(new[]{"SUR-S01","SUR-S02","SUR-S03"}));
            var texts=SurveyorLaunchTexts.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(SurveyorLaunchBuilder.UiFolder+"/LaunchTexts.json"));
            foreach(var cue in data.Cues){Assert.That(texts.Get(cue.TextKey,"pt"),Is.Not.Empty);Assert.That(texts.Get(cue.TextKey,"en"),Is.Not.Empty);}
        }
        [Test] public void ViewHidesEmptySubtitleAndRelocalizesWithoutChangingState()
        {
            var document=root.AddComponent<UIDocument>();
            document.panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>(SurveyorLaunchBuilder.Folder+"/LaunchPanelSettings.asset");
            document.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SurveyorLaunchBuilder.UiFolder+"/LaunchTelemetry.uxml");
            var view=root.AddComponent<LaunchTelemetryView>();
            var settings=new SettingsData();
            view.Configure(document,AssetDatabase.LoadAssetAtPath<TextAsset>(SurveyorLaunchBuilder.UiFolder+"/LaunchTexts.json"),flow,settings);
            Assert.That(view.Root.Q<Label>("subtitle").text,Does.Contain("Antes de enviar"));
            Advance(9);Assert.That(view.Root.Q("subtitle-plate").style.display.value,Is.EqualTo(DisplayStyle.None));
            settings.languageCode="en";view.ApplySettings(settings);
            Assert.That(view.Root.Q<Label>("mission-date").text,Does.Contain("APR"));
            Assert.That(owner.CurrentStepId,Is.EqualTo("Launch"));
        }
    }
}
