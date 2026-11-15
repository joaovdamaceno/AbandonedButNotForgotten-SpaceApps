using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.VisitTests
{
    public sealed class VisitTests:InputTestFixture
    {
        sealed class Loader:ChapterDirector.ISceneLoader{public void Load(string n,Action done,Action<string> fail)=>done();public void Unload(string n,Action done)=>done();}
        GameObject root;SurveyorVisit flow;SurveyorVisitActors actors;ChapterDirector owner;SessionClock clock;InputRouter input;
        SequenceDirector sequence;Camera camera;CameraDirector cameras;SequenceDefinition track;ChapterDefinition chapter;SurveyorEnding ending;Texture2D photo;Keyboard keyboard;
        Transform Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform);return go.transform;}
        public override void Setup()
        {
            base.Setup();keyboard=InputSystem.AddDevice<Keyboard>();root=new GameObject("SUR-013 fixture");owner=root.AddComponent<ChapterDirector>();clock=root.AddComponent<SessionClock>();input=root.AddComponent<InputRouter>();sequence=root.AddComponent<SequenceDirector>();flow=root.AddComponent<SurveyorVisit>();
            camera=Child("Camera").gameObject.AddComponent<Camera>();camera.transform.position=new Vector3(0,2,0);cameras=root.AddComponent<CameraDirector>();cameras.Initialize(camera);cameras.GroundProbe=p=>-10;
            actors=root.AddComponent<SurveyorVisitActors>();var landing=Child("LandingRig");var human=Child("HumanRig");human.rotation=Quaternion.Euler(20,0,0);
            actors.Configure(Child("Lander"),Child("Walkers"),Child("Left"),Child("Right"),Child("Hand"),Array.Empty<Transform>(),new Vector3(-170,9,60),new Vector3(0,0,4),new Vector3(0,0,2),new Vector3(.75f,.4f,1),new Vector3(.06f,.04f,.38f));actors.SetHandFrame(human);
            track=SurveyorVisitBuilder.CreateReference();ending=ScriptableObject.CreateInstance<SurveyorEnding>();photo=new Texture2D(16,12);ending.Configure(photo);
            owner.Initialize(clock,input,new Loader(),new EvidenceStore());input.Enable();flow.Configure(owner,clock,input,sequence,track,ending,actors,cameras,landing,human);
            chapter=ChapterDefinition.Create("surveyor.visit.test","a","b","c","Test",50,track.SequenceId,new[]{new ChapterStep(SurveyorVisit.StepId,false,SurveyorVisit.ReceiptStep),new ChapterStep(SurveyorVisit.ReceiptStep,false,SurveyorVisit.EndStep),new ChapterStep(SurveyorVisit.EndStep,true)});
            owner.OpenDossier();owner.Begin(chapter);
        }
        public override void TearDown(){flow.Dispose();input.Disable();Object.DestroyImmediate(root);Object.DestroyImmediate(track);Object.DestroyImmediate(ending);Object.DestroyImmediate(photo);Object.DestroyImmediate(chapter);base.TearDown();}
        void Advance(float seconds){while(seconds>.00001f){float dt=Mathf.Min(.05f,seconds);clock.Tick(dt);flow.Tick(clock.ActiveDeltaSeconds);seconds-=dt;}clock.Tick(0);}
        [Test]public void NoticePrecedesImageAndHistoryHasItsOwnStage()
        {Advance(4.9f);Assert.That(flow.ShowWorld,Is.False);Advance(.2f);Assert.That(flow.ShowWorld,Is.True);Advance(26);Assert.That(flow.ShowWorld,Is.False);Advance(1);Assert.That(flow.ShowArchive,Is.True);Assert.That(owner.CurrentStepId,Is.EqualTo(SurveyorVisit.ReceiptStep));Advance(12);Assert.That(flow.ShowMemorial,Is.True);}
        [Test]public void FinalCuesPreserveTheirExactWindowsAndNeverOverlap()
        {Assert.That(track.Validate(),Is.Empty);Assert.That(track.TotalDurationSeconds,Is.EqualTo(50));var cues=track.Cues;Assert.That(cues.Count,Is.EqualTo(6));Assert.That(cues[3].StartSeconds,Is.EqualTo(32));Assert.That(cues[3].EndSeconds,Is.EqualTo(38));Assert.That(cues[4].StartSeconds,Is.EqualTo(38));Assert.That(cues[4].EndSeconds,Is.EqualTo(43));Assert.That(cues[5].StartSeconds,Is.EqualTo(44));Assert.That(cues[5].EndSeconds,Is.EqualTo(50));for(int i=1;i<cues.Count;i++)Assert.That(cues[i-1].EndSeconds,Is.LessThanOrEqualTo(cues[i].StartSeconds));}
        [Test]public void HeldEAtTheEndCannotReturnUntilReleasedAndPressedAgain()
        {Press(keyboard.eKey);Advance(50.2f);Assert.That(owner.State,Is.EqualTo(AppState.Completion));Assert.That(flow.AwaitingReturn,Is.True);Assert.That(flow.CanClickReturn,Is.False);Assert.That(flow.ReturnFromButton(),Is.False);Advance(10);Assert.That(owner.State,Is.EqualTo(AppState.Completion));Release(keyboard.eKey);Press(keyboard.eKey);Release(keyboard.eKey);Assert.That(owner.State,Is.EqualTo(AppState.Dossier));Assert.That(camera.enabled,Is.True);}
        [Test]public void NoReturnBeforeAllFiftySecondsAndASeparateClickWorks()
        {Advance(49.9f);Assert.That(flow.ReturnFromButton(),Is.False);Advance(.3f);Assert.That(flow.ReturnFromButton(),Is.True);Assert.That(owner.State,Is.EqualTo(AppState.Dossier));}
        [Test]public void PauseInThePhotographPreservesTheCueAndReadingTime()
        {Advance(35);owner.Pause("test");float time=flow.TimeSeconds;Advance(8);Assert.That(flow.TimeSeconds,Is.EqualTo(time));Assert.That(flow.ShowArchive,Is.True);owner.Resume("test");Advance(15.2f);Assert.That(owner.IsFinished,Is.True);}
        [Test]public void RestartDuringTheGloveClearsArchiveAndRestartsTheNotice()
        {Advance(28);Assert.That(actors.Glove.gameObject.activeSelf,Is.True);owner.RestartChapter();Assert.That(flow.TimeSeconds,Is.Zero);Assert.That(flow.ShowWorld,Is.False);Assert.That(owner.Evidence.Records.Count,Is.Zero);Advance(50.2f);Assert.That(owner.IsFinished,Is.True);}
        [Test]public void CancelRestoresTheCameraAndDoesNotFinishTheChapter()
        {Advance(33);owner.ReturnToDossier();Advance(30);Assert.That(sequence.IsPlaying,Is.False);Assert.That(cameras.IsControlled,Is.False);Assert.That(camera.enabled,Is.True);Assert.That(owner.IsFinished,Is.False);Assert.That(flow.AwaitingReturn,Is.False);}
        [Test]public void CameraNeverLeavesItsAnchorAndTheEvidenceIsArchival()
        {var position=camera.transform.position;for(int i=0;i<501;i++){Advance(.1f);Assert.That(camera.transform.position,Is.EqualTo(position));}Assert.That(owner.Evidence.TryGet("surveyor.apollo12.archive",out var record),Is.True);Assert.That(record.Authorship,Is.EqualTo(EvidenceAuthorship.Archive));Assert.That(record.CanBePresentedAsPlayers,Is.False);Assert.That(cameras.IsControlled,Is.False);}
    }
}
