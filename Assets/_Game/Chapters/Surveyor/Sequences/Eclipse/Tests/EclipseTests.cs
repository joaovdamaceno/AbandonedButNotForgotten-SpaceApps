using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EclipseTests
{
    public sealed class EclipseTests : InputTestFixture
    {
        sealed class Loader : ChapterDirector.ISceneLoader
        { public void Load(string name,Action done,Action<string> fail)=>done(); public void Unload(string name,Action done)=>done(); }
        GameObject root; ChapterDirector owner; SessionClock clock; InputRouter input; InteractionService service;
        SurveyorCommandDelay queue; SurveyorCommandInteraction action; SequenceDirector sequence; SurveyorEclipse flow;
        SurveyorSiteBindings site; Camera camera; CameraDirector cameras; SequenceDefinition track; ChapterDefinition chapter;
        Keyboard keyboard; readonly List<string> entered=new List<string>();
        Transform Child(string name) { var obj=new GameObject(name); obj.transform.SetParent(root.transform); return obj.transform; }
        public override void Setup()
        {
            base.Setup(); keyboard=InputSystem.AddDevice<Keyboard>(); root=new GameObject("SUR-011 fixture");
            owner=root.AddComponent<ChapterDirector>(); clock=root.AddComponent<SessionClock>(); input=root.AddComponent<InputRouter>(); service=root.AddComponent<InteractionService>();
            queue=root.AddComponent<SurveyorCommandDelay>(); action=root.AddComponent<SurveyorCommandInteraction>(); sequence=root.AddComponent<SequenceDirector>(); flow=root.AddComponent<SurveyorEclipse>();
            camera=Child("TV").gameObject.AddComponent<Camera>(); camera.transform.position=new Vector3(0,2,0); camera.fieldOfView=25.3f;
            cameras=root.AddComponent<CameraDirector>(); cameras.Initialize(camera); cameras.GroundProbe=p=>-100;
            site=root.AddComponent<SurveyorSiteBindings>(); var trench=Child("Trench"); trench.position=new Vector3(1,0,3);
            site.Configure(cameras,Child("Mount"),Child("Azimuth"),Child("Elevation"),null,null,new[]{new SurveyorSiteBindings.Target{id="SUR.Trench",anchor=trench}});
            var image=Child("Earth").gameObject.AddComponent<SurveyorEclipseImage>();
            track=SurveyorEclipseBuilder.CreateReference();
            owner.Initialize(clock,input,new Loader(),new EvidenceStore()); input.Enable(); service.Initialize(input,clock);
            flow.Configure(owner,clock,input,service,queue,action,sequence,track,site,image,Child("Sky"),Child("Ground"));
            chapter=ChapterDefinition.Create("surveyor.eclipse.test","a","b","c","Test",30,track.SequenceId,new[]{new ChapterStep("Eclipse",false,"LunarNight"),new ChapterStep("LunarNight",true)});
            entered.Clear(); owner.StepEntered+=entered.Add; owner.OpenDossier(); owner.Begin(chapter);
        }
        public override void TearDown()
        { flow.Dispose(); input.Disable(); Object.DestroyImmediate(root); Object.DestroyImmediate(track); Object.DestroyImmediate(chapter); base.TearDown(); }
        void Advance(float seconds)
        { while(seconds>.00001f) { float dt=Mathf.Min(.05f,seconds); clock.Tick(dt); service.Tick(clock.ActiveDeltaSeconds); queue.Tick(); flow.Tick(clock.ActiveDeltaSeconds); seconds-=dt; } clock.Tick(0); }
        void Send()
        { Press(keyboard.eKey); Advance(.85f); Release(keyboard.eKey); Assert.That(queue.HasPending,Is.True); Advance(1.35f); Assert.That(flow.Phase,Is.EqualTo(EclipsePhase.Viewing)); }

        [Test] public void ReceivedCommandStartsTheOnlyTwentySixSecondClock()
        {
            var position=camera.transform.position; var rotation=camera.transform.rotation;
            Press(keyboard.eKey); Advance(.85f); Release(keyboard.eKey);
            Assert.That(flow.Phase,Is.EqualTo(EclipsePhase.InTransit)); Assert.That(sequence.IsPlaying,Is.False);
            Advance(1); Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
            Advance(.35f); Assert.That(sequence.IsPlaying,Is.True); Assert.That(camera.transform.position,Is.EqualTo(position));
            Advance(25.8f); Assert.That(owner.CurrentStepId,Is.EqualTo("Eclipse"));
            Advance(.4f); Assert.That(owner.CurrentStepId,Is.EqualTo("LunarNight"));
            Advance(5); Assert.That(entered.FindAll(id=>id=="LunarNight").Count,Is.EqualTo(1));
        }
        [Test] public void ReleasingTheHoldPreservesPreparationWithoutSending()
        {
            Press(keyboard.eKey); Advance(.35f); Release(keyboard.eKey); float progress=action.PreparationProgress;
            Advance(3); Assert.That(queue.HasPending,Is.False); Assert.That(progress,Is.InRange(.3f,.6f));
            Press(keyboard.eKey); Advance(.55f); Release(keyboard.eKey); Assert.That(queue.HasPending,Is.True);
        }
        [Test] public void PauseDuringTransitDoesNotMoveMirrorOrSpendDelay()
        {
            Press(keyboard.eKey); Advance(.85f); Release(keyboard.eKey); Advance(.4f);
            owner.Pause("focus"); float remaining=queue.RemainingSeconds; var mirror=site.MirrorElevation.localRotation;
            Advance(4); Assert.That(queue.RemainingSeconds,Is.EqualTo(remaining)); Assert.That(site.MirrorElevation.localRotation,Is.EqualTo(mirror));
            owner.Resume("focus"); Advance(1); Assert.That(sequence.IsPlaying,Is.True);
        }
        [Test] public void PauseFreezesCameraMirrorImageAndCueTogether()
        {
            Send(); Advance(10); owner.Pause("menu"); float time=flow.SequenceTime; var pose=camera.transform.rotation; var mirror=site.MirrorElevation.localRotation;
            Advance(5); Assert.That(flow.SequenceTime,Is.EqualTo(time)); Assert.That(camera.transform.rotation,Is.EqualTo(pose)); Assert.That(site.MirrorElevation.localRotation,Is.EqualTo(mirror));
            owner.Resume("menu"); Advance(1); Assert.That(flow.SequenceTime,Is.GreaterThan(time));
        }
        [Test] public void RestartInTransitCancelsStaleCommandAndRequiresFreshInput()
        {
            Press(keyboard.eKey); Advance(.85f); owner.RestartChapter(); Advance(3);
            Assert.That(sequence.IsPlaying,Is.False); Assert.That(flow.Phase,Is.EqualTo(EclipsePhase.AwaitingOrder));
            Release(keyboard.eKey); Send(); Assert.That(sequence.TimeSeconds,Is.LessThan(.2));
        }
        [Test] public void ReturnToArchiveCancelsCameraLeaseAndQueuedWork()
        { Send(); Advance(7); owner.ReturnToDossier(); Advance(40); Assert.That(sequence.IsPlaying,Is.False); Assert.That(cameras.IsControlled,Is.False); Assert.That(queue.HasPending,Is.False); Assert.That(flow.Running,Is.False); }
        [Test] public void OpticalPositionAndBodyRemainFixedAndMirrorReturnsToRest()
        {
            var position=camera.transform.position; var siteRotation=site.transform.rotation; Send();
            for(int i=0;i<260;i++)
            { Advance(.1f); Assert.That(Vector3.Distance(position,camera.transform.position),Is.LessThan(.00001f)); Assert.That(site.transform.rotation,Is.EqualTo(siteRotation)); Assert.That(Quaternion.Angle(Quaternion.identity,site.MirrorElevation.localRotation),Is.LessThanOrEqualTo(35.001f)); }
            Advance(.2f); Assert.That(site.MirrorElevation.localRotation,Is.EqualTo(Quaternion.identity));
            Assert.That(camera.fieldOfView,Is.EqualTo(25.3f)); Assert.That(cameras.IsControlled,Is.False);
        }
        [Test] public void EightSecondsOfTotalityHaveNoSubtitles()
        {
            var cues=track.Cues; Assert.That(track.TotalDurationSeconds,Is.EqualTo(26)); Assert.That(cues.Count,Is.EqualTo(2));
            Assert.That(cues[0].TextKey,Is.EqualTo("SUR-S20")); Assert.That(cues[1].TextKey,Is.EqualTo("SUR-S21"));
            Assert.That(cues[0].EndSeconds-cues[0].StartSeconds,Is.EqualTo(6)); Assert.That(cues[1].EndSeconds,Is.EqualTo(14));
            foreach(var cue in cues) Assert.That(cue.EndSeconds<=14 || cue.StartSeconds>=22,Is.True);
        }
        [Test] public void ReducedMotionUsesStaticCropWithoutChangingTheClock()
        { flow.ReducedMotion=true; Send(); Advance(10); Assert.That(flow.Enlargement,Is.EqualTo(1)); Advance(12); Assert.That(flow.Enlargement,Is.EqualTo(0)); Advance(4); Assert.That(owner.CurrentStepId,Is.EqualTo("LunarNight")); }
        [Test] public void DataAndBothLanguagesAreComplete()
        {
            Assert.That(track.Validate(),Is.Empty); var loc=new LocalizationService();
            Assert.That(loc.AddTable(AssetDatabase.LoadAssetAtPath<TextAsset>(SurveyorEclipseBuilder.Folder+"/EclipseTexts.json").text,"eclipse"),Is.Empty);
            foreach(var language in new[]{"pt","en"}) { loc.SetLanguage(language); foreach(var cue in track.Cues) Assert.That(loc.Get(cue.TextKey),Is.Not.Empty); }
            Assert.That(SurveyorEclipseImage.EarthDiameterDegrees/SurveyorEclipseImage.SunDiameterDegrees,Is.InRange(3.5f,3.7f));
        }
        [Test] public void ToggleModeUsesTheSharedHoldWithoutRequiringPhysicalHold()
        { service.Mode=HoldMode.Toggle; Press(keyboard.eKey); Release(keyboard.eKey); Advance(2.2f); Assert.That(flow.Phase,Is.EqualTo(EclipsePhase.Viewing)); }
    }
}
