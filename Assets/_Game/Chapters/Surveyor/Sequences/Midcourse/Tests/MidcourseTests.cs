using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.MidcourseTests
{
    public sealed class MidcourseTests:InputTestFixture
    {
        sealed class Loaded:ChapterDirector.ISceneLoader
        {public void Load(string name,Action loaded,Action<string> failed)=>loaded();public void Unload(string name,Action unloaded)=>unloaded();}
        GameObject root;ChapterDirector owner;SessionClock clock;InputRouter input;InteractionService service;
        SurveyorCommandDelay queue;SurveyorCommandInteraction action;SurveyorMidcourse flow;SequenceDirector sequence;CameraDirector camera;
        SequenceDefinition data;ChapterDefinition chapter;Keyboard keyboard;
        public override void Setup()
        {
            base.Setup();keyboard=InputSystem.AddDevice<Keyboard>();root=new GameObject("SUR-005 test");
            owner=root.AddComponent<ChapterDirector>();clock=root.AddComponent<SessionClock>();input=root.AddComponent<InputRouter>();service=root.AddComponent<InteractionService>();
            camera=root.AddComponent<CameraDirector>();camera.Initialize(root.AddComponent<Camera>());sequence=root.AddComponent<SequenceDirector>();sequence.Initialize(camera,null,clock);
            queue=root.AddComponent<SurveyorCommandDelay>();action=root.AddComponent<SurveyorCommandInteraction>();flow=root.AddComponent<SurveyorMidcourse>();
            owner.Initialize(clock,input,new Loaded(),new EvidenceStore());input.Enable();service.Initialize(input,clock);
            data=SurveyorMidcourseBuilder.CreateReference();flow.Configure(owner,input,clock,service,queue,action,sequence,data);
            chapter=ChapterDefinition.Create("surveyor.midcourse.test","a","b","c","TestScene",37,data.SequenceId,
                new[]{new ChapterStep("Cruise",false,"Descent"),new ChapterStep("Descent",false,"End"),new ChapterStep("End",true)});
            owner.OpenDossier();owner.Begin(chapter);
        }
        public override void TearDown(){flow.Dispose();input.Disable();Object.DestroyImmediate(root);Object.DestroyImmediate(data);Object.DestroyImmediate(chapter);base.TearDown();}
        void Advance(float seconds)
        {while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);clock.Tick(d);service.Tick(clock.ActiveDeltaSeconds);queue.Tick();flow.Tick(clock.ActiveDeltaSeconds);seconds-=d;}}
        void Guide(){Advance(12);Assert.That(flow.Phase,Is.EqualTo(MidcoursePhase.Guidance));}
        void Confirm(){flow.Assist();Press(keyboard.eKey);Advance(3.05f);Release(keyboard.eKey);}
        [Test]public void KeyboardAloneReachesTheZoneAndFinishes()
        {
            Guide();Press(keyboard.rightArrowKey);Advance(.4f);Release(keyboard.rightArrowKey);Assert.That(flow.Aligned,Is.True);
            Press(keyboard.eKey);Advance(3.05f);Release(keyboard.eKey);Advance(15);
            Assert.That(owner.CurrentStepId,Is.EqualTo("Descent"));Assert.That(owner.IsFinished,Is.False);Assert.That(camera.IsControlled,Is.False);
        }
        [Test]public void HelpAlignsButNeverSendsTheCorrection()
        {Guide();Press(keyboard.hKey);Release(keyboard.hKey);Assert.That(flow.DesiredAim,Is.EqualTo(SurveyorMidcourse.TargetCenter));Advance(60);Assert.That(queue.HasPending,Is.False);Assert.That(owner.CurrentStepId,Is.EqualTo("Cruise"));}
        [Test]public void NoInputNeverCompletesEvenAfterTheReferenceBudget()
        {Advance(100);Assert.That(flow.Phase,Is.EqualTo(MidcoursePhase.Guidance));Assert.That(owner.CurrentStepId,Is.EqualTo("Cruise"));Assert.That(flow.HelpLevel,Is.EqualTo(2));}
        [Test]public void AssistanceUsesActiveIdleTimeAndStillNeedsAnOrder()
        {
            Advance(10.1f);Advance(8.1f);Assert.That(flow.HelpLevel,Is.EqualTo(1));owner.Pause("test");Advance(30);owner.Resume("test");
            Advance(7.1f);Assert.That(flow.HelpLevel,Is.EqualTo(2));Assert.That(flow.Tolerance,Is.EqualTo(.12f));Assert.That(action.PreparationProgress,Is.Zero);
        }
        [Test]public void PartialPreparationSurvivesReleaseMisalignmentAndPause()
        {
            Guide();flow.Assist();Press(keyboard.eKey);Advance(1.2f);Release(keyboard.eKey);Assert.That(action.PreparationProgress,Is.EqualTo(.4f).Within(.01));
            flow.SetDesired(new Vector2(.1f,.1f));Advance(2);Assert.That(action.CanConfirm,Is.False);
            owner.Pause("test");Advance(10);owner.Resume("test");flow.Assist();
            Assert.That(action.PreparationProgress,Is.EqualTo(.4f).Within(.01));Press(keyboard.eKey);Advance(1.85f);Release(keyboard.eKey);Assert.That(queue.HasPending,Is.True);
        }
        [Test]public void LeavingAlignmentWhileHoldingPausesAndRequiresRelease()
        {Guide();flow.Assist();Press(keyboard.eKey);Advance(1);flow.SetDesired(new Vector2(.1f,.1f));float p=action.PreparationProgress;flow.Assist();Advance(3);Assert.That(action.PreparationProgress,Is.EqualTo(p));Assert.That(queue.HasPending,Is.False);Release(keyboard.eKey);}
        [Test]public void EffectStartsOnlyAfterTransitAndBurnFreezesInPause()
        {
            Guide();Confirm();Assert.That(queue.HasPending,Is.True);Advance(1.15f);Assert.That(flow.Phase,Is.EqualTo(MidcoursePhase.Guidance));
            Advance(.2f);Assert.That(flow.Phase,Is.EqualTo(MidcoursePhase.Burn));Advance(2);float p=flow.BurnProgress;var actual=flow.ActualAim;
            owner.Pause("test");Advance(20);Assert.That(flow.BurnProgress,Is.EqualTo(p));Assert.That(flow.ActualAim,Is.EqualTo(actual));owner.Resume("test");Advance(12);Assert.That(flow.ActComplete,Is.True);
        }
        [Test]public void RestartDuringTransitInvalidatesTheOldCorrection()
        {Guide();Confirm();Advance(.5f);owner.RestartChapter();Advance(8);Assert.That(flow.Phase,Is.EqualTo(MidcoursePhase.Travel));Assert.That(queue.HasPending,Is.False);Assert.That(action.PreparationProgress,Is.Zero);}
        [Test]public void ReentryDoesNotGrowTransientSequences()
        {
            for(int i=0;i<12;i++)owner.RestartChapter();
            int count=Resources.FindObjectsOfTypeAll<SequenceDefinition>().Count(s=>s.SequenceId!=null&&s.SequenceId.StartsWith("surveyor.cruise.runtime."));
            Assert.That(count,Is.EqualTo(3));owner.ReturnToDossier();Assert.That(service.TryGetActive(out _),Is.False);Assert.That(camera.IsControlled,Is.False);
        }
        [Test]public void ToggleModeCanSendAndHoldCannotLeakPastTheAct()
        {Guide();service.Mode=HoldMode.Toggle;flow.Assist();Press(keyboard.eKey);Release(keyboard.eKey);Advance(18);Assert.That(flow.ActComplete,Is.True);Assert.That(service.TryGetActive(out _),Is.False);}
        [Test]public void CuesAndReferenceBudgetComeFromTheAuthoredSequence()
        {
            Assert.That(data.TotalDurationSeconds,Is.EqualTo(37));Assert.That(data.Validate(),Is.Empty);
            Assert.That(data.Cues.Select(c=>c.CueId),Is.EqualTo(new[]{"SUR-S04","SUR-S05","SUR-S06"}));
            var log=new List<string>();flow.CueEntered+=cue=>log.Add(cue.CueId);owner.RestartChapter();Guide();Confirm();Advance(15);
            Assert.That(log,Is.EqualTo(new[]{"SUR-S04","SUR-S05","SUR-S06"}));
        }
        [Test]public void ReferencePlaythroughIsAboutThirtySevenActiveSeconds()
        {Advance(19.8f);Confirm();Advance(14.4f);Assert.That(flow.ActComplete,Is.True);Assert.That(clock.ActiveSeconds,Is.InRange(37f,38f));}
        [TestCase(true)][TestCase(false)]public void LaunchHandoffWorksRegardlessOfSubscriptionOrder(bool launchFirst)
        {
            owner.ReturnToDossier();var launch=root.AddComponent<SurveyorLaunchController>();var launchData=SurveyorLaunchBuilder.CreateSequence();
            if(launchFirst)launch.Configure(owner,input,service,sequence,launchData,clock);
            flow.Configure(owner,input,clock,service,queue,action,sequence,data);
            if(!launchFirst)launch.Configure(owner,input,service,sequence,launchData,clock);
            var flight=ChapterDefinition.Create("surveyor.flight.test","a","b","c","TestScene",65,launchData.SequenceId,
                new[]{new ChapterStep("Launch",false,"Cruise"),new ChapterStep("Cruise",false,"Descent"),new ChapterStep("Descent",false,"End"),new ChapterStep("End",true)});
            owner.Begin(flight);
            void Both(float seconds){while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);clock.Tick(d);service.Tick(clock.ActiveDeltaSeconds);queue.Tick();launch.Tick(clock.ActiveDeltaSeconds);flow.Tick(clock.ActiveDeltaSeconds);seconds-=d;}}
            Both(17.1f);Press(keyboard.eKey);Both(2.55f);Release(keyboard.eKey);Both(9);
            Assert.That(owner.CurrentStepId,Is.EqualTo("Cruise"));Assert.That(flow.Running,Is.True);Assert.That(sequence.IsPlaying,Is.True);Assert.That(camera.IsControlled,Is.True);
            Object.DestroyImmediate(launch);Object.DestroyImmediate(launchData);Object.DestroyImmediate(flight);
        }
        [Test]public void PointerTargetUnitsAndTheVisibleCircleUseTheSameScale()
        {Guide();flow.SetDesired(SurveyorMidcourse.TargetCenter+new Vector2(0,.07f));Assert.That(flow.Aligned,Is.True);flow.SetDesired(SurveyorMidcourse.TargetCenter+new Vector2(0,.08f));Assert.That(flow.Aligned,Is.False);}
        [Test]public void NextConsumerKeepsItsCameraAndCommandScopeWhenEnteredFirst()
        {
            owner.ReturnToDossier();
            var next=SequenceDefinition.Create("test.descent",new[]{new ShotDefinition("test.next",5,CameraDirector.WorldRigId,
                new[]{new ShotKeyframe(0,Vector3.up*3,Vector3.zero,45),new ShotKeyframe(5,Vector3.up*3,Vector3.zero,45)})});
            owner.StepEntered+=id=>
            {
                if(id!="Descent")return;queue.BeginState(id);action.Offer(new InteractionCandidate{Id="next",VerbKey="a",ObjectKey="b",Available=true,HoldSeconds=1},()=>()=>{});
                sequence.Play(next,false);
            };
            flow.Configure(owner,input,clock,service,queue,action,sequence,data);owner.Begin(chapter);Guide();Confirm();Advance(15);
            Assert.That(sequence.Current,Is.EqualTo(next));Assert.That(queue.OwnsState,Is.True);Assert.That(action.CanConfirm,Is.True);
            sequence.Cancel();Object.DestroyImmediate(next);
        }
        [Test]public void EmptySubtitlePlateDisappearsAndEnglishKeepsTheObjective()
        {
            var doc=root.AddComponent<UIDocument>();doc.panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>(SurveyorMidcourseBuilder.Folder+"/MidcoursePanelSettings.asset");
            doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SurveyorMidcourseBuilder.UiFolder+"/MidcoursePanel.uxml");var view=root.AddComponent<SurveyorMidcourseView>();
            view.Configure(doc,AssetDatabase.LoadAssetAtPath<TextAsset>(SurveyorMidcourseBuilder.UiFolder+"/MidcourseTexts.json"),flow,queue,action,new SettingsData());
            Advance(7);Assert.That(view.Root.Q("subtitle-plate").style.display.value,Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Root.Q("help").style.display.value,Is.EqualTo(DisplayStyle.None),"Instruções de hold não devem aparecer durante uma sequência automática.");
            view.ApplySettings(new SettingsData{languageCode="en"});Assert.That(view.Root.Q<Label>("destination").text,Is.EqualTo("OCEAN OF STORMS"));
        }
    }
}
