using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.NightTests
{
    public sealed class NightTests : InputTestFixture
    {
        sealed class Loader:ChapterDirector.ISceneLoader
        {public void Load(string n,Action done,Action<string> fail)=>done();public void Unload(string n,Action done)=>done();}
        GameObject root;SurveyorNight flow;ChapterDirector owner;SessionClock clock;InputRouter input;
        SequenceDirector sequence;Camera camera;CameraDirector cameras;Light sun;SequenceDefinition night,passage;ChapterDefinition chapter;
        Color ambient;bool nextConsumer;int completions;
        public override void Setup()
        {
            base.Setup();root=new GameObject("SUR-012 fixture");ambient=RenderSettings.ambientLight;
            owner=root.AddComponent<ChapterDirector>();clock=root.AddComponent<SessionClock>();input=root.AddComponent<InputRouter>();
            sequence=root.AddComponent<SequenceDirector>();flow=root.AddComponent<SurveyorNight>();
            var obj=new GameObject("TV");obj.transform.SetParent(root.transform);camera=obj.AddComponent<Camera>();camera.transform.position=new Vector3(0,2,0);camera.fieldOfView=25.3f;
            cameras=root.AddComponent<CameraDirector>();cameras.Initialize(camera);cameras.GroundProbe=p=>-10;
            var lightObject=new GameObject("Sun");lightObject.transform.SetParent(root.transform);
            sun=lightObject.AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.15f;sun.transform.rotation=Quaternion.Euler(23,145,0);
            var optics=new GameObject("Optics").transform;optics.SetParent(root.transform);
            night=SurveyorNightBuilder.CreateNight();passage=SurveyorNightBuilder.CreatePassage();
            owner.Initialize(clock,input,new Loader(),new EvidenceStore());input.Enable();nextConsumer=false;completions=0;
            owner.StepEntered+=id=>{if(id==SurveyorNight.NextStep){completions++;if(nextConsumer)sun.intensity=2.5f;}};
            flow.Configure(owner,clock,input,sequence,cameras,night,passage,sun,optics);
            chapter=ChapterDefinition.Create("surveyor.night.test","a","b","c","Test",30,night.SequenceId,new[]{
                new ChapterStep(SurveyorNight.NightStep,false,SurveyorNight.PassageStep),new ChapterStep(SurveyorNight.PassageStep,false,SurveyorNight.NextStep),new ChapterStep(SurveyorNight.NextStep,true)});
            owner.OpenDossier();owner.Begin(chapter);
        }
        public override void TearDown()
        {flow.Dispose();owner.Evidence.Clear();input.Disable();Object.DestroyImmediate(root);Object.DestroyImmediate(night);Object.DestroyImmediate(passage);Object.DestroyImmediate(chapter);RenderSettings.ambientLight=ambient;base.TearDown();}
        void Advance(float seconds){while(seconds>.00001f){float dt=Mathf.Min(.05f,seconds);clock.Tick(dt);flow.Tick(clock.ActiveDeltaSeconds);seconds-=dt;}clock.Tick(0);}
        [Test]public void ThirtySecondsCompleteBothStepsOnceWithoutMovingTheOpticalPoint()
        {var position=camera.transform.position;Advance(16.9f);Assert.That(owner.CurrentStepId,Is.EqualTo(SurveyorNight.NightStep));Advance(.3f);Assert.That(owner.CurrentStepId,Is.EqualTo(SurveyorNight.PassageStep));Advance(13);Assert.That(owner.CurrentStepId,Is.EqualTo(SurveyorNight.NextStep));Advance(5);Assert.That(completions,Is.EqualTo(1));Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(cameras.IsControlled,Is.False);}
        [Test]public void PauseBeforeTheEllipsePreservesCueAndLighting()
        {Advance(16);owner.Pause("test");float t=flow.TrackSeconds;var rotation=sun.transform.rotation;Advance(10);Assert.That(flow.TrackSeconds,Is.EqualTo(t));Assert.That(sun.transform.rotation,Is.EqualTo(rotation));Assert.That(owner.CurrentStepId,Is.EqualTo(SurveyorNight.NightStep));owner.Resume("test");Advance(1.2f);Assert.That(owner.CurrentStepId,Is.EqualTo(SurveyorNight.PassageStep));}
        [Test]public void RestartInTheEllipseRestoresInitialLightingAndNight()
        {Advance(22);owner.RestartChapter();Assert.That(owner.CurrentStepId,Is.EqualTo(SurveyorNight.NightStep));Assert.That(sequence.TimeSeconds,Is.EqualTo(0));Assert.That(sun.intensity,Is.EqualTo(1.15f).Within(.001));Assert.That(flow.TransmissionEnded,Is.False);Advance(30.2f);Assert.That(completions,Is.EqualTo(1));}
        [Test]public void ReturnCancelsTheSequenceAndRestoresTheScene()
        {Advance(10);owner.ReturnToDossier();Advance(30);Assert.That(sequence.IsPlaying,Is.False);Assert.That(cameras.IsControlled,Is.False);Assert.That(sun.intensity,Is.EqualTo(1.15f).Within(.001));Assert.That(completions,Is.Zero);}
        [Test]public void CompletionAndDisposalRespectTheNextConsumersLighting()
        {nextConsumer=true;Advance(30.2f);Assert.That(sun.intensity,Is.EqualTo(2.5f));flow.Dispose();Assert.That(sun.intensity,Is.EqualTo(2.5f));}
        [Test]public void FourCuesHaveTheirCompleteReadingWindows()
        {Assert.That(night.Validate(),Is.Empty);Assert.That(passage.Validate(),Is.Empty);Assert.That(night.TotalDurationSeconds+passage.TotalDurationSeconds,Is.EqualTo(30));Assert.That(night.Cues.Count,Is.EqualTo(3));Assert.That(night.Cues[1].EndSeconds-night.Cues[1].StartSeconds,Is.EqualTo(6));Assert.That(night.Cues[2].EndSeconds-night.Cues[2].StartSeconds,Is.EqualTo(6));Assert.That(passage.Cues[0].TextKey,Is.EqualTo("SUR-S25"));}
        [TestCase(false)][TestCase(true)]public void FourLightChangesAndCalendarFinishInThirteenSeconds(bool reduced)
        {
            flow.ReducedMotion=reduced;Advance(17.1f);Assert.That(flow.CalendarKey,Is.EqualTo("sur.night.year1967"));
            int changes=0;bool above=false;
            for(int i=0;i<258;i++){Advance(.05f);bool now=sun.intensity>(reduced?.23f:.575f);if(now!=above)changes++;above=now;if(i==70)Assert.That(flow.CalendarKey,Is.EqualTo("sur.night.year1968"));if(i==180)Assert.That(flow.CalendarKey,Is.EqualTo("sur.night.nov1969"));}
            Assert.That(changes,Is.EqualTo(4));
        }
        [Test]public void RuntimeThumbnailsAreReleasedWithoutLosingEvidenceMetadataOrArchive()
        {
            var texture=new Texture2D(16,12);var photo=Sprite.Create(texture,new Rect(0,0,16,12),Vector2.zero);
            var archiveTexture=new Texture2D(16,12);var archive=Sprite.Create(archiveTexture,new Rect(0,0,16,12),Vector2.zero);
            owner.Evidence.Add(new EvidenceRecord("photo",EvidenceKind.Photo,"result","S3-03",EvidenceAuthorship.PlayerCapture,photo));
            owner.Evidence.Add(new EvidenceRecord("archive",EvidenceKind.Photo,"result","S3-03",EvidenceAuthorship.Archive,archive,"credit"));
            Advance(17.2f);Assert.That(photo==null&&texture==null,Is.True);Assert.That(owner.Evidence.TryGet("photo",out var record),Is.True);Assert.That(record.Thumbnail,Is.Null);Assert.That(record.ScienceRef,Is.EqualTo("S3-03"));
            Assert.That(owner.Evidence.TryGet("archive",out var kept)&&kept.Thumbnail==archive,Is.True);
            Object.DestroyImmediate(archive);Object.DestroyImmediate(archiveTexture);
        }
    }
}
