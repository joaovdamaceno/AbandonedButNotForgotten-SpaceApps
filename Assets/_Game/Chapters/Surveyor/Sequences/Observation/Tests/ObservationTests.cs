using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.ObservationTests
{
    public sealed class ObservationTests : InputTestFixture
    {
        sealed class Loader:ChapterDirector.ISceneLoader
        {public void Load(string name,Action done,Action<string> fail)=>done();public void Unload(string name,Action done)=>done();}
        GameObject root;ChapterDirector owner;SessionClock clock;InputRouter input;InteractionService service;SurveyorCommandDelay queue;
        SurveyorCommandInteraction action;SurveyorSiteBindings site;SurveyorObservation flow;SurveyorObservationTargets data;
        SurveyorTelevisionOutput output;Camera camera;ChapterDefinition chapter;Keyboard keyboard;
        public override void Setup()
        {
            base.Setup();keyboard=InputSystem.AddDevice<Keyboard>();root=new GameObject("SUR-008 test");
            owner=root.AddComponent<ChapterDirector>();clock=root.AddComponent<SessionClock>();input=root.AddComponent<InputRouter>();service=root.AddComponent<InteractionService>();
            queue=root.AddComponent<SurveyorCommandDelay>();action=root.AddComponent<SurveyorCommandInteraction>();flow=root.AddComponent<SurveyorObservation>();
            output=root.AddComponent<SurveyorTelevisionOutput>();camera=Child("TV").gameObject.AddComponent<Camera>();camera.aspect=4f/3;camera.transform.position=new Vector3(0,2,0);camera.transform.rotation=Quaternion.identity;
            site=root.AddComponent<SurveyorSiteBindings>();Transform az=Child("Azimuth"),el=Child("Elevation"),mount=Child("Mount");
            Transform marks=Child("Marks"),slope=Child("Slope"),foot=Child("Foot");marks.position=new Vector3(-1,0,4);slope.position=new Vector3(-12,2,4);foot.position=new Vector3(.8f,0,1.6f);
            site.Configure(null,mount,az,el,null,null,new[]{new SurveyorSiteBindings.Target{id="SUR.LandingMarks",anchor=marks},new SurveyorSiteBindings.Target{id="SUR.CraterSlope",anchor=slope},new SurveyorSiteBindings.Target{id="SUR.Footpad",anchor=foot}});
            owner.Initialize(clock,input,new Loader(),new EvidenceStore());input.Enable();service.Initialize(input,clock);data=SurveyorObservationTargets.CreateReference();
            flow.Configure(owner,clock,input,service,queue,action,site,camera,output,data);
            flow.CaptureOverride=()=>{var t=new Texture2D(16,12);t.SetPixel(0,0,Color.white);t.Apply();return Sprite.Create(t,new Rect(0,0,16,12),Vector2.zero);};
            chapter=ChapterDefinition.Create("surveyor.observe.test","a","b","c","Test",60,"surveyor.observe.fixture",new[]{new ChapterStep("Observe",false,"Sample"),new ChapterStep("Sample",true)});
            owner.OpenDossier();owner.Begin(chapter);
        }
        Transform Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform);return go.transform;}
        public override void TearDown(){flow.Dispose();owner.Evidence.Clear();input.Disable();Object.DestroyImmediate(root);Object.DestroyImmediate(data);Object.DestroyImmediate(chapter);base.TearDown();}
        void Advance(float seconds){while(seconds>.0001f){float dt=Mathf.Min(.05f,seconds);clock.Tick(dt);service.Tick(clock.ActiveDeltaSeconds);queue.Tick();flow.Tick(clock.ActiveDeltaSeconds);seconds-=dt;}clock.Tick(0);}
        void Frame(){Press(keyboard.hKey);Release(keyboard.hKey);Advance(6);Assert.That(flow.Phase,Is.EqualTo(ObservationPhase.Ready));}
        void Photograph(){Press(keyboard.eKey);Advance(1.55f);Release(keyboard.eKey);Advance(1.35f);Assert.That(flow.Phase,Is.EqualTo(ObservationPhase.Result));}
        void Continue(){Press(keyboard.eKey);Advance(.05f);Release(keyboard.eKey);}

        [Test]public void DesiredPointingNeverMovesMirrorBeforeTheCommandArrives()
        {
            Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
            flow.SetDesired(new Vector2(-30,-15));Advance(.5f);Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
            Press(keyboard.eKey);Advance(.05f);Release(keyboard.eKey);Advance(1.2f);Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
            Advance(.2f);Assert.That(camera.transform.rotation,Is.Not.EqualTo(rotation));Assert.That(camera.transform.position,Is.EqualTo(position));
        }
        [Test]public void HUsesTheSameDelayedQueueAndCannotDuplicateCommands()
        {
            int count=0;queue.Sent+=r=>count++;var rotation=camera.transform.rotation;
            Press(keyboard.hKey);Release(keyboard.hKey);Assert.That(queue.HasPending,Is.True);Assert.That(flow.AssistFrame(),Is.False);
            Advance(1.2f);Assert.That(camera.transform.rotation,Is.EqualTo(rotation));Advance(6);Assert.That(count,Is.EqualTo(1));Assert.That(flow.Aligned,Is.True);
        }
        [Test]public void MousePreparesPointingOnlyWhileOverThePictureAndNeverMovesTheMirror()
        {
            var mouse=InputSystem.AddDevice<Mouse>();var before=flow.DesiredAngles;var rotation=camera.transform.rotation;
            Set(mouse.delta,new Vector2(10,3));Advance(.05f);Assert.That(flow.DesiredAngles,Is.EqualTo(before));
            flow.PointerOverImage=true;Set(mouse.delta,new Vector2(12,4));Advance(.05f);
            Assert.That(flow.DesiredAngles,Is.Not.EqualTo(before));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
        }
        [Test]public void DesiredAlignmentDoesNotPermitPhotographWhileMirrorIsElsewhere()
        {flow.AssistFrame();Assert.That(flow.CanPrimary,Is.False);Assert.That(owner.Evidence.Records,Is.Empty);Advance(1.25f);Assert.That(flow.Phase,Is.EqualTo(ObservationPhase.InTransit));Assert.That(owner.Evidence.Records,Is.Empty);}
        [TestCase(.22f,.22f,1,true)][TestCase(.78f,.78f,1,true)][TestCase(.219f,.5f,1,false)][TestCase(.781f,.5f,1,false)][TestCase(.5f,.5f,-1,false)]
        public void FrameEdgesAndBehindCameraAreExplicit(float x,float y,float z,bool inside)
        {Assert.That(SurveyorObservation.InsideFrame(new Vector3(x,y,z),new Vector2(.28f,.28f)),Is.EqualTo(inside));}
        [Test]public void ThreeTargetsFinishWithKeyboardAndHAndProduceThreeDistinctRecords()
        {
            var observer=camera.transform.position;
            for(int n=0;n<3;n++){Frame();Photograph();Assert.That(flow.RecordCount,Is.EqualTo(n+1));Assert.That(owner.Evidence.PlayerPhotoCount,Is.EqualTo(n+1));Continue();}
            Assert.That(owner.CurrentStepId,Is.EqualTo("Sample"));Assert.That(flow.Running,Is.False);Assert.That(camera.transform.position,Is.EqualTo(observer));
            Assert.That(owner.Evidence.Records[2].CanBePresentedAsPlayers,Is.True);
        }
        [Test]public void ReleasePreservesPreparationAndPauseFreezesHoldAndTransit()
        {
            Frame();Press(keyboard.eKey);Advance(.65f);Release(keyboard.eKey);float p=action.PreparationProgress;
            owner.Pause("test");Advance(8);Assert.That(action.PreparationProgress,Is.EqualTo(p));owner.Resume("test");
            Press(keyboard.eKey);Advance(.9f);Release(keyboard.eKey);Advance(.5f);float elapsed=queue.ElapsedSeconds;
            owner.Pause("test");Advance(9);Assert.That(queue.ElapsedSeconds,Is.EqualTo(elapsed));owner.Resume("test");Advance(1);Assert.That(flow.RecordCount,Is.EqualTo(1));
        }
        [Test]public void HoldingEAcrossReceptionCannotCloseResultOrStartNextTarget()
        {
            Frame();Press(keyboard.eKey);Advance(5);Assert.That(flow.Phase,Is.EqualTo(ObservationPhase.Result));Assert.That(flow.TargetIndex,Is.Zero);
            Advance(5);Assert.That(flow.TargetIndex,Is.Zero);Release(keyboard.eKey);Continue();Assert.That(flow.TargetIndex,Is.EqualTo(1));Assert.That(flow.Phase,Is.EqualTo(ObservationPhase.Framing));
        }
        [Test]public void ReviewingOnePictureDoesNotAdvanceOrCreateAnotherRecord()
        {
            Frame();Photograph();Continue();Assert.That(flow.Review(0),Is.True);Assert.That(flow.Reviewing,Is.True);Continue();
            Assert.That(flow.TargetIndex,Is.EqualTo(1));Assert.That(flow.RecordCount,Is.EqualTo(1));Assert.That(owner.Evidence.Records.Count,Is.EqualTo(1));
        }
        [Test]public void ClosingWhileCommandIsPendingInvalidatesItAndReleasesInput()
        {
            flow.AssistFrame();var rotation=camera.transform.rotation;flow.Stop();Advance(10);
            Assert.That(queue.HasPending,Is.False);Assert.That(camera.transform.rotation,Is.EqualTo(rotation));Assert.That(service.TryGetActive(out _),Is.False);Assert.That(flow.AssistFrame(),Is.False);
        }
        [Test]public void LensChangeWaitsForTransitAndDoesNotMoveObserver()
        {
            Frame();var origin=camera.transform.position;Assert.That(flow.ChangeLens(),Is.True);Advance(1.2f);Assert.That(camera.fieldOfView,Is.EqualTo(25.3f));
            Advance(2);Assert.That(camera.fieldOfView,Is.EqualTo(6.43f).Within(.02f));Assert.That(flow.DetailLens,Is.True);Assert.That(flow.ChangeLens(),Is.True);Advance(3);
            Assert.That(camera.fieldOfView,Is.EqualTo(25.3f).Within(.02f));Assert.That(camera.transform.position,Is.EqualTo(origin));
        }
        [Test]public void ToggleModeCanStartAndStopPreparationWithoutHoldingE()
        {
            Frame();service.Mode=HoldMode.Toggle;Press(keyboard.eKey);Release(keyboard.eKey);Advance(.5f);float progress=action.PreparationProgress;
            Press(keyboard.eKey);Release(keyboard.eKey);Advance(.4f);Assert.That(action.PreparationProgress,Is.EqualTo(progress));
            Press(keyboard.eKey);Release(keyboard.eKey);Advance(2.5f);Assert.That(flow.RecordCount,Is.EqualTo(1));
        }
        [Test]public void ReconfiguringInTheSameStepRestoresExistingPictures()
        {
            Frame();Photograph();Continue();flow.Configure(owner,clock,input,service,queue,action,site,camera,output,data);
            Assert.That(flow.RecordCount,Is.EqualTo(1));Assert.That(flow.TargetIndex,Is.EqualTo(1));Assert.That(owner.Evidence.Records.Count,Is.EqualTo(1));
        }
        [Test]public void RestartClearsPhotosAndCameraCommands()
        {
            Frame();Photograph();var photo=flow.Result.Thumbnail;Continue();flow.AssistFrame();owner.RestartChapter();
            Assert.That(owner.Evidence.Records,Is.Empty);Assert.That(photo==null,Is.True);Assert.That(queue.HasPending,Is.False);Assert.That(flow.RecordCount,Is.Zero);
        }
        [Test]public void ReturningFromAnEvidenceCallbackCannotReopenTheResultOrLeakInput()
        {
            Frame();owner.Evidence.Added+=record=>owner.ReturnToDossier();
            Press(keyboard.eKey);Advance(1.55f);Release(keyboard.eKey);Advance(1.35f);
            Assert.That(owner.State,Is.EqualTo(AppState.Dossier));Assert.That(flow.Running,Is.False);
            Assert.That(flow.RecordCount,Is.Zero);Assert.That(service.TryGetActive(out _),Is.False);
            Assert.That(input.DescribeLeases(),Is.Empty);
        }
        [Test]public void DisplayedAvailabilityMatchesSharedServiceAndLocalAction()
        {
            Assert.That(flow.CanPrimary,Is.EqualTo(action.CanConfirm));Frame();Assert.That(flow.CanPrimary,Is.EqualTo(action.CanConfirm));
            owner.Pause("test");Assert.That(flow.CanPrimary,Is.False);owner.Resume("test");Assert.That(flow.CanPrimary,Is.EqualTo(action.CanConfirm));
        }
        [Test]public void BuiltAssetsHaveReferencesAndAllThreeTargetsAndBilingualCues()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorObservationBuilder.PrefabPath);Assert.That(prefab,Is.Not.Null);
            Assert.That(prefab.GetComponent<SurveyorObservationPreviewHost>(),Is.Not.Null);Assert.That(prefab.GetComponentsInChildren<AudioListener>(true).Length,Is.EqualTo(1));
            var targetData=AssetDatabase.LoadAssetAtPath<SurveyorObservationTargets>(SurveyorObservationBuilder.TargetsPath);Assert.That(targetData.Validate(),Is.Empty);
            var catalog=SurveyorLaunchTexts.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(SurveyorObservationBuilder.Folder+"/ObservationTexts.json"));
            for(int n=0;n<3;n++){Assert.That(catalog.Get(targetData.Targets[n].subtitleKey,"pt"),Is.Not.Empty);Assert.That(catalog.Get(targetData.Targets[n].subtitleKey,"en"),Is.Not.Empty);}
        }
    }
}
