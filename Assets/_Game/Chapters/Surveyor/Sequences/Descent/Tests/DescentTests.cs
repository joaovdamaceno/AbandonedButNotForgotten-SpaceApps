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

namespace Abandoned.SpaceApps.Chapters.Surveyor.DescentTests
{
    public sealed class DescentTests:InputTestFixture
    {
        sealed class Loaded:ChapterDirector.ISceneLoader
        {public void Load(string name,Action loaded,Action<string> failed)=>loaded();public void Unload(string name,Action unloaded)=>unloaded();}
        GameObject root;ChapterDirector owner;SessionClock clock;InputRouter input;InteractionService service;
        SurveyorCommandDelay queue;SurveyorCommandInteraction action;SurveyorDescent flow;SequenceDirector sequence;CameraDirector camera;
        SequenceDefinition data;ChapterDefinition chapter;Keyboard keyboard;
        public override void Setup()
        {
            base.Setup();keyboard=InputSystem.AddDevice<Keyboard>();root=new GameObject("SUR-006 test");
            owner=root.AddComponent<ChapterDirector>();clock=root.AddComponent<SessionClock>();input=root.AddComponent<InputRouter>();service=root.AddComponent<InteractionService>();
            camera=root.AddComponent<CameraDirector>();camera.Initialize(root.AddComponent<Camera>());sequence=root.AddComponent<SequenceDirector>();sequence.Initialize(camera,null,clock);
            queue=root.AddComponent<SurveyorCommandDelay>();action=root.AddComponent<SurveyorCommandInteraction>();flow=root.AddComponent<SurveyorDescent>();
            owner.Initialize(clock,input,new Loaded(),new EvidenceStore());input.Enable();service.Initialize(input,clock);
            data=SurveyorDescentBuilder.CreateReference();var indicator=new GameObject("Altitude").transform;indicator.SetParent(root.transform);
            flow.Configure(owner,clock,input,service,queue,action,sequence,data,indicator);
            chapter=ChapterDefinition.Create("surveyor.descent.test","a","b","c","TestScene",40,data.SequenceId,
                new[]{new ChapterStep("Descent",false,"FirstImage"),new ChapterStep("FirstImage",false,"Observe"),new ChapterStep("Observe",false,"End"),new ChapterStep("End",true)});
            owner.OpenDossier();owner.Begin(chapter);
        }
        public override void TearDown(){flow.Dispose();input.Disable();Object.DestroyImmediate(root);Object.DestroyImmediate(data);Object.DestroyImmediate(chapter);base.TearDown();}
        void Advance(float seconds)
        {while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);clock.Tick(d);service.Tick(clock.ActiveDeltaSeconds);queue.Tick();flow.Tick(clock.ActiveDeltaSeconds);seconds-=d;}}
        void To(float seconds)=>Advance(Mathf.Max(0,seconds-flow.TimeSeconds));
        void Confirm(){Press(keyboard.eKey);Advance(3.05f);Release(keyboard.eKey);}
        [TestCase(12.1f)][TestCase(25f)][TestCase(-1f)]
        public void EarlyLateAndAbsentInputAllReachTheHistoricalThirdContact(float beginAt)
        {
            var contacts=new List<int>();flow.GroundContact+=contacts.Add;
            if(beginAt>0){To(beginAt);Confirm();}Advance(45);
            Assert.That(contacts,Is.EqualTo(new[]{1,2,3}));Assert.That(flow.Settled,Is.True);Assert.That(flow.EnginesCutOff,Is.True);
            Assert.That(owner.CurrentStepId,Is.EqualTo("FirstImage"));Assert.That(owner.IsFinished,Is.False);
            Assert.That(flow.Origin,Is.EqualTo(beginAt>0?CutoffOrigin.Player:CutoffOrigin.GroundTeam));
        }
        [Test]public void EarlyAuthorizationDoesNotTurnOffEnginesBeforeTheTwoRebounds()
        {
            To(12.1f);Confirm();Advance(1.35f);Assert.That(flow.CutoffReceived,Is.True);Assert.That(flow.EnginesCutOff,Is.False);
            To(22);Assert.That(flow.ContactCount,Is.EqualTo(1));Assert.That(flow.HeightMeters,Is.EqualTo(10).Within(.03));Assert.That(flow.EnginesCutOff,Is.False);
            To(29);Assert.That(flow.ContactCount,Is.EqualTo(2));Assert.That(flow.HeightMeters,Is.EqualTo(3).Within(.03));Assert.That(flow.EnginesCutOff,Is.False);
            To(31.1f);Assert.That(flow.EnginesCutOff,Is.True);Assert.That(flow.Settled,Is.False);To(32.1f);Assert.That(flow.Settled,Is.True);
        }
        [Test]public void HeldKeyBeforeOfferRequiresReleaseAndDoesNotAutoSend()
        {
            Press(keyboard.eKey);To(16);Assert.That(action.PreparationProgress,Is.Zero);Assert.That(queue.HasPending,Is.False);
            Release(keyboard.eKey);Confirm();Assert.That(queue.HasPending,Is.True);
        }
        [Test]public void ReleaseAndPausePreservePartialPreparation()
        {
            To(12.1f);Press(keyboard.eKey);Advance(1.2f);Release(keyboard.eKey);float progress=action.PreparationProgress;
            owner.Pause("test");Advance(100);Assert.That(action.PreparationProgress,Is.EqualTo(progress));Assert.That(flow.TimeSeconds,Is.EqualTo(13.3f).Within(.02));
            owner.Resume("test");Press(keyboard.eKey);Advance(1.85f);Release(keyboard.eKey);Assert.That(queue.HasPending,Is.True);
        }
        [Test]public void ToggleModeUsesTheSameCommandQueue()
        {To(12.1f);service.Mode=HoldMode.Toggle;Press(keyboard.eKey);Release(keyboard.eKey);Advance(4.4f);Assert.That(flow.CutoffReceived,Is.True);Assert.That(flow.Origin,Is.EqualTo(CutoffOrigin.Player));}
        [Test]public void TransitTakesOnePointThreeActiveSecondsAndFreezesInPause()
        {
            To(12.1f);Confirm();Advance(.6f);float transit=queue.ElapsedSeconds,time=flow.TimeSeconds;
            owner.Pause("test");Advance(30);Assert.That(queue.ElapsedSeconds,Is.EqualTo(transit));Assert.That(flow.TimeSeconds,Is.EqualTo(time));
            owner.Resume("test");Advance(.65f);Assert.That(flow.CutoffReceived,Is.False);Advance(.1f);Assert.That(flow.CutoffReceived,Is.True);
        }
        [Test]public void StructuralImpulseAndAltitudeFreezeInPause()
        {
            To(18.1f);float pulse=flow.ImpactPulse,height=flow.HeightMeters,time=flow.TimeSeconds;Assert.That(pulse,Is.GreaterThan(0));
            owner.Pause("test");Advance(30);Assert.That(flow.ImpactPulse,Is.EqualTo(pulse));Assert.That(flow.HeightMeters,Is.EqualTo(height));Assert.That(flow.TimeSeconds,Is.EqualTo(time));
            owner.Resume("test");Advance(1);Assert.That(flow.HeightMeters,Is.GreaterThan(height));Assert.That(flow.ImpactPulse,Is.Zero);
        }
        [Test]public void ReleasingDuringGraceHandsOverToTeamOnce()
        {
            int sends=0;queue.Sent+=r=>sends++;To(26);Press(keyboard.eKey);Advance(1.2f);Assert.That(flow.CutoffSent,Is.False);
            Release(keyboard.eKey);Advance(.1f);Assert.That(flow.Origin,Is.EqualTo(CutoffOrigin.GroundTeam));Advance(20);Assert.That(sends,Is.EqualTo(1));Assert.That(flow.ActComplete,Is.True);
        }
        [Test]public void NoInputContingencyCueIsNeverPublishedForPlayerCommand()
        {
            var log=new List<string>();flow.CueEntered+=c=>log.Add(c.CueId);To(12.1f);Confirm();Advance(30);
            Assert.That(log,Does.Contain("SUR-S08-PLAYER"));Assert.That(log,Does.Not.Contain("SUR-S08"));Assert.That(log,Does.Contain("SUR-S09"));
            log.Clear();owner.RestartChapter();Advance(45);Assert.That(log,Does.Contain("SUR-S08"));Assert.That(log,Does.Not.Contain("SUR-S08-PLAYER"));
        }
        [Test]public void AnomalyExplanationKeepsPriorityOverAnEarlyReceipt()
        {
            float start=-1,end=-1;
            flow.CueEntered+=c=>{if(c.CueId=="SUR-S08-PLAYER")start=flow.TimeSeconds;};
            flow.CueExited+=c=>{if(c.CueId=="SUR-S08-PLAYER")end=flow.TimeSeconds;};
            To(12);Confirm();Advance(1.35f);Assert.That(flow.CutoffReceived,Is.True);Assert.That(flow.SubtitleKey,Is.EqualTo("SUR-S07"));
            Assert.That(start,Is.EqualTo(-1),"A leitura do recibo só começa depois da explicação da anomalia.");
            To(17.1f);Assert.That(flow.SubtitleKey,Is.EqualTo("descent.player.receipt"));To(21.2f);
            Assert.That(start,Is.GreaterThanOrEqualTo(16.99f));Assert.That(end-start,Is.GreaterThanOrEqualTo(3.99f));
        }
        [Test]public void GraceCannotExtendIndefinitelyAndFallbackKeepsItsReadingWindow()
        {
            var starts=new Dictionary<string,float>();var ends=new Dictionary<string,float>();
            flow.CueEntered+=c=>starts[c.CueId]=flow.TimeSeconds;flow.CueExited+=c=>ends[c.CueId]=flow.TimeSeconds;
            To(26.9f);Press(keyboard.eKey);Advance(.3f);
            // Exercise the hard deadline even if a consumer stops ticking hold progress while it remains held.
            for(int i=0;i<62;i++){clock.Tick(.05f);queue.Tick();flow.Tick(clock.ActiveDeltaSeconds);}
            Assert.That(flow.CutoffSent,Is.True);Release(keyboard.eKey);Advance(20);
            Assert.That(flow.Origin,Is.EqualTo(CutoffOrigin.GroundTeam));Assert.That(ends["SUR-S08"]-starts["SUR-S08"],Is.GreaterThanOrEqualTo(5.99f));
            Assert.That(starts["SUR-S09"],Is.GreaterThanOrEqualTo(ends["SUR-S08"]-.01f));Assert.That(flow.ActComplete,Is.True);
            Assert.That(flow.TimeSeconds,Is.InRange(42.2f,42.5f));
        }
        [Test]public void AResumedHugeFrameCannotSkipTheContacts()
        {
            To(17.9f);clock.Tick(1000);service.Tick(clock.ActiveDeltaSeconds);queue.Tick();flow.Tick(1000);
            Assert.That(flow.TimeSeconds,Is.InRange(17.99f,18.01f));Assert.That(flow.ContactCount,Is.EqualTo(1));Advance(25);Assert.That(flow.ContactCount,Is.EqualTo(3));Assert.That(flow.ActComplete,Is.True);
        }
        [Test]public void LateGraceLeavesTimeToReadBothReceiptAndNextObjective()
        {
            var starts=new Dictionary<string,float>();var ends=new Dictionary<string,float>();
            flow.CueEntered+=c=>starts[c.CueId]=flow.TimeSeconds;flow.CueExited+=c=>ends[c.CueId]=flow.TimeSeconds;
            To(26.8f);Press(keyboard.eKey);Advance(3.05f);Release(keyboard.eKey);Advance(20);
            Assert.That(flow.Origin,Is.EqualTo(CutoffOrigin.Player));Assert.That(ends["SUR-S08-PLAYER"]-starts["SUR-S08-PLAYER"],Is.GreaterThanOrEqualTo(3.99f));
            Assert.That(ends["SUR-S09"]-starts["SUR-S09"],Is.GreaterThanOrEqualTo(4.99f));Assert.That(flow.ActComplete,Is.True);
        }
        [Test]public void RestartAndReturnInvalidateOldCallbacksAndReleaseInput()
        {
            To(12.1f);Confirm();Advance(.5f);owner.RestartChapter();Advance(8);
            Assert.That(flow.CutoffReceived,Is.False);Assert.That(flow.CutoffSent,Is.False);Assert.That(flow.ContactCount,Is.Zero);Assert.That(action.PreparationProgress,Is.Zero);
            owner.ReturnToDossier();Assert.That(flow.Running,Is.False);Assert.That(service.TryGetActive(out _),Is.False);Assert.That(queue.HasPending,Is.False);Assert.That(camera.IsControlled,Is.False);
        }
        [Test]public void CameraGateBlocksUntilHandoffAndReleasesTargetOnRestart()
        {
            var tvGo=new GameObject("TV");tvGo.SetActive(false);tvGo.transform.SetParent(root.transform);var tv=tvGo.AddComponent<Camera>();tv.enabled=false;
            var viewport=tvGo.AddComponent<SurveyorSiteViewport>();viewport.enabled=false;viewport.Configure(tv);tvGo.SetActive(true);
            var gate=root.AddComponent<SurveyorDescentCameraGate>();gate.Configure(flow,owner,tv,viewport);
            Assert.That(gate.TryActivate(),Is.False);Assert.That(tv.targetTexture,Is.Null);To(29);Assert.That(gate.TryActivate(),Is.False);
            To(32.2f);Assert.That(flow.Settled,Is.True);Assert.That(gate.TryActivate(),Is.False);Advance(10);
            Assert.That(gate.TryActivate(),Is.True);owner.CompleteStep("FirstImage","Observe");Assert.That(gate.Active,Is.True);
            owner.Pause("test");Assert.That(gate.Active,Is.True);owner.Resume("test");owner.RestartChapter();Assert.That(gate.Active,Is.False);Assert.That(tv.targetTexture,Is.Null);gate.Dispose();
        }
        SurveyorDescentView View()
        {
            var doc=root.AddComponent<UIDocument>();doc.panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>(SurveyorDescentBuilder.Folder+"/DescentPanelSettings.asset");
            doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SurveyorDescentBuilder.UiFolder+"/DescentTelemetry.uxml");var view=root.AddComponent<SurveyorDescentView>();
            view.Configure(doc,AssetDatabase.LoadAssetAtPath<TextAsset>(SurveyorDescentBuilder.UiFolder+"/DescentTexts.json"),flow,queue,action,new SettingsData());return view;
        }
        [Test]public void ReducedMotionRemovesDisplacementAndKeepsStructuralPulse()
        {
            var view=View();To(18.1f);view.ApplySettings(new SettingsData{reducedMotion=true});Assert.That(view.DiagramOffset,Is.EqualTo(Vector2.zero));Assert.That(flow.ImpactPulse,Is.GreaterThan(0));
            view.ApplySettings(new SettingsData{reducedMotion=false});Assert.That(view.DiagramOffset.sqrMagnitude,Is.GreaterThan(0));
        }
        [Test]public void EmptyCaptionIsHiddenAndLanguageAndCommandStateStayConsistent()
        {
            var view=View();Advance(3);Assert.That(view.Root.Q("subtitle-plate").style.display.value,Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Root.Q("confirm").style.display.value,Is.EqualTo(DisplayStyle.None));To(12.1f);Confirm();Advance(1.35f);
            view.ApplySettings(new SettingsData{languageCode="en"});Assert.That(view.Root.Q<Label>("status").text,Is.EqualTo("Command received · cutoff armed"));
            Assert.That(view.Root.Q("confirm").style.display.value,Is.EqualTo(DisplayStyle.None));Assert.That(view.Root.Q("help").style.display.value,Is.EqualTo(DisplayStyle.None));
            view.DrawLocalSubtitles=false;view.Refresh();Assert.That(view.Root.Q("subtitle-plate").style.display.value,Is.EqualTo(DisplayStyle.None));
        }
        [Test]public void BudgetAndWindowComeFromTheSingleSequenceAsset()
        {
            Assert.That(data.Validate(),Is.Empty);Assert.That(data.TotalDurationSeconds,Is.EqualTo(40));Assert.That(flow.DeadlineAt-flow.OfferAt,Is.GreaterThanOrEqualTo(15));
            Assert.That(flow.CutoffEligibleAt,Is.EqualTo(31));Assert.That(flow.SettledAt,Is.EqualTo(32));Advance(40.1f);Assert.That(flow.ActComplete,Is.True);
        }
        [TestCase(true)][TestCase(false)]public void MidcourseToDescentHandoffPreservesTheNextSequenceRegardlessOfOrder(bool cruiseFirst)
        {
            owner.ReturnToDossier();var cruise=root.AddComponent<SurveyorMidcourse>();var cruiseData=SurveyorMidcourseBuilder.CreateReference();
            if(cruiseFirst)cruise.Configure(owner,input,clock,service,queue,action,sequence,cruiseData);
            flow.Configure(owner,clock,input,service,queue,action,sequence,data,root.transform.Find("Altitude"));
            if(!cruiseFirst)cruise.Configure(owner,input,clock,service,queue,action,sequence,cruiseData);
            var flight=ChapterDefinition.Create("surveyor.handoff.test","a","b","c","TestScene",77,cruiseData.SequenceId,
                new[]{new ChapterStep("Cruise",false,"Descent"),new ChapterStep("Descent",false,"FirstImage"),new ChapterStep("FirstImage",false,"End"),new ChapterStep("End",true)});
            owner.Begin(flight);
            void Both(float seconds){while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);clock.Tick(d);service.Tick(clock.ActiveDeltaSeconds);queue.Tick();cruise.Tick(clock.ActiveDeltaSeconds);flow.Tick(clock.ActiveDeltaSeconds);seconds-=d;}}
            Both(12.1f);cruise.Assist();Press(keyboard.eKey);Both(3.05f);Release(keyboard.eKey);Both(15);
            Assert.That(owner.CurrentStepId,Is.EqualTo("Descent"));Assert.That(sequence.Current,Is.EqualTo(data));Assert.That(flow.Running,Is.True);Assert.That(camera.IsControlled,Is.True);
            cruise.Dispose();Object.DestroyImmediate(cruise);Object.DestroyImmediate(cruiseData);Object.DestroyImmediate(flight);
        }
        [Test]public void NextConsumerCanAcquireCameraAndQueueBeforeOldListenerRuns()
        {
            owner.ReturnToDossier();var next=SequenceDefinition.Create("test.firstImage",new[]{new ShotDefinition("test.surface",5,CameraDirector.WorldRigId,
                new[]{new ShotKeyframe(0,Vector3.up*3,Vector3.zero,45),new ShotKeyframe(5,Vector3.up*3,Vector3.zero,45)})});
            owner.StepEntered+=id=>{if(id!="FirstImage")return;queue.BeginState(id);sequence.Play(next,false);
                action.Offer(new InteractionCandidate{Id="next",VerbKey="a",ObjectKey="b",Available=true,HoldSeconds=2},()=>()=>{});};
            flow.Configure(owner,clock,input,service,queue,action,sequence,data,root.transform.Find("Altitude"));owner.Begin(chapter);Advance(41);
            Assert.That(sequence.Current,Is.EqualTo(next));Assert.That(queue.OwnsState,Is.True);Assert.That(action.CanConfirm,Is.True);sequence.Cancel();Object.DestroyImmediate(next);
        }
    }
}
