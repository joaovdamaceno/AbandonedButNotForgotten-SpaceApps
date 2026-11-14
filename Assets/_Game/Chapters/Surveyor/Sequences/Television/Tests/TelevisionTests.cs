using System;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.TelevisionTests
{
    public sealed class TelevisionTests:InputTestFixture
    {
        sealed class Loaded:ChapterDirector.ISceneLoader
        {public void Load(string name,Action loaded,Action<string> failed)=>loaded();public void Unload(string name,Action unloaded)=>unloaded();}
        GameObject root;ChapterDirector owner;SessionClock clock;InputRouter input;InteractionService service;CameraDirector camera;SequenceDirector sequence;
        SurveyorCommandDelay queue;SurveyorCommandInteraction action;SurveyorDescent descent;SurveyorDescentCameraGate gate;SurveyorTelevision flow;SurveyorTelevisionOutput output;
        SequenceDefinition landing,data;ChapterDefinition chapter;Keyboard keyboard;
        public override void Setup()
        {
            base.Setup();keyboard=InputSystem.AddDevice<Keyboard>();root=new GameObject("SUR-007 test");
            owner=root.AddComponent<ChapterDirector>();clock=root.AddComponent<SessionClock>();input=root.AddComponent<InputRouter>();service=root.AddComponent<InteractionService>();
            camera=root.AddComponent<CameraDirector>();camera.Initialize(root.AddComponent<Camera>());sequence=root.AddComponent<SequenceDirector>();sequence.Initialize(camera,null,clock);
            queue=root.AddComponent<SurveyorCommandDelay>();action=root.AddComponent<SurveyorCommandInteraction>();descent=root.AddComponent<SurveyorDescent>();gate=root.AddComponent<SurveyorDescentCameraGate>();
            flow=root.AddComponent<SurveyorTelevision>();output=root.AddComponent<SurveyorTelevisionOutput>();
            owner.Initialize(clock,input,new Loaded(),new EvidenceStore());input.Enable();service.Initialize(input,clock);
            landing=SurveyorDescentBuilder.CreateReference();data=SurveyorTelevisionBuilder.CreateReference();var indicator=new GameObject("Altitude").transform;indicator.SetParent(root.transform);
            descent.Configure(owner,clock,input,service,queue,action,sequence,landing,indicator);
            var tvGo=new GameObject("TV");tvGo.SetActive(false);tvGo.transform.SetParent(root.transform);var tv=tvGo.AddComponent<Camera>();tv.enabled=false;
            var viewport=tvGo.AddComponent<SurveyorSiteViewport>();viewport.enabled=false;viewport.Configure(tv);tvGo.SetActive(true);
            gate.Configure(descent,owner,tv,viewport);output.Configure(viewport,AssetDatabase.LoadAssetAtPath<Material>(SurveyorTelevisionBuilder.MaterialPath));
            flow.Configure(owner,clock,input,service,queue,action,sequence,data,descent,gate,output);
            chapter=ChapterDefinition.Create("surveyor.tv.test","a","b","c","TestScene",53.3f,landing.SequenceId,
                new[]{new ChapterStep("Descent",false,"FirstImage"),new ChapterStep("FirstImage",false,"Observe"),new ChapterStep("Observe",false,"End"),new ChapterStep("End",true)});
            owner.OpenDossier();owner.Begin(chapter);
        }
        public override void TearDown(){flow.Dispose();descent.Dispose();gate.Dispose();output.Dispose();input.Disable();Object.DestroyImmediate(root);Object.DestroyImmediate(landing);Object.DestroyImmediate(data);Object.DestroyImmediate(chapter);base.TearDown();}
        void Advance(float seconds){while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);clock.Tick(d);service.Tick(clock.ActiveDeltaSeconds);queue.Tick();descent.Tick(clock.ActiveDeltaSeconds);flow.Tick(clock.ActiveDeltaSeconds);seconds-=d;}}
        [Test]public void BeforeLandingThereIsNoTvImageCandidateOrBuffer()
        {Advance(30);Assert.That(flow.Running,Is.False);Assert.That(output.Texture,Is.Null);Assert.That(output.Source,Is.Null);Assert.That(gate.TryActivate(),Is.False);}
        [Test]public void LandingOffersFirstImageButNoInputDoesNotReceiveIt()
        {Advance(120);Assert.That(owner.CurrentStepId,Is.EqualTo("FirstImage"));Assert.That(flow.Phase,Is.EqualTo(TelevisionPhase.AwaitingOrder));Assert.That(action.CanConfirm,Is.True);Assert.That(output.Allocations,Is.Zero);Assert.That(output.Source,Is.Null);}
        [Test]public void EAlreadyHeldAtHandoffRequiresRelease()
        {
            Advance(39);Press(keyboard.eKey);Advance(4);Assert.That(action.PreparationProgress,Is.Zero);Release(keyboard.eKey);
            Press(keyboard.eKey);Advance(2.05f);Release(keyboard.eKey);Assert.That(flow.Phase,Is.EqualTo(TelevisionPhase.InTransit));Assert.That(queue.HasPending,Is.True);Assert.That(output.Source,Is.Null);
        }
        [Test]public void PreparationAndTransitPreserveProgressAcrossPause()
        {
            Advance(40.1f);Press(keyboard.eKey);Advance(.8f);Release(keyboard.eKey);float p=action.PreparationProgress;
            owner.Pause("test");Advance(30);Assert.That(action.PreparationProgress,Is.EqualTo(p));owner.Resume("test");
            Press(keyboard.eKey);Advance(1.25f);Release(keyboard.eKey);Advance(.6f);float transit=queue.ElapsedSeconds;owner.Pause("test");Advance(20);
            Assert.That(queue.ElapsedSeconds,Is.EqualTo(transit));Assert.That(output.Texture,Is.Null);
        }
        [Test]public void TogglePreparationAlsoSendsOnlyOneCommand()
        {Advance(40.1f);service.Mode=HoldMode.Toggle;int sends=0;queue.Sent+=r=>sends++;Press(keyboard.eKey);Release(keyboard.eKey);Advance(2.05f);Assert.That(sends,Is.EqualTo(1));Assert.That(queue.HasPending,Is.True);}
        [Test]public void RestartDuringTransitInvalidatesReceptionAndReturnsToDescent()
        {Advance(40.1f);Press(keyboard.eKey);Advance(2.05f);Release(keyboard.eKey);Advance(.5f);owner.RestartChapter();Advance(5);Assert.That(flow.Running,Is.False);Assert.That(output.Allocations,Is.Zero);Assert.That(owner.CurrentStepId,Is.EqualTo("Descent"));Assert.That(queue.HasPending,Is.False);}
        [Test]public void ReturnReleasesPromptAndSequenceOwnership()
        {Advance(40.1f);owner.ReturnToDossier();Assert.That(flow.Running,Is.False);Assert.That(service.TryGetActive(out _),Is.False);Assert.That(camera.IsControlled,Is.False);Assert.That(output.Texture,Is.Null);}
        [Test]public void TheAuthoredScanAndContemplationHaveOneSourceOfDuration()
        {Assert.That(data.Validate(),Is.Empty);Assert.That(flow.ScanSeconds,Is.EqualTo(4));Assert.That(data.TotalDurationSeconds,Is.EqualTo(10));Assert.That(data.Cues[0].TextKey,Is.EqualTo("SUR-S10"));Assert.That(data.Cues[0].EndSeconds-data.Cues[0].StartSeconds,Is.EqualTo(6));}
        [Test]public void EmptyCaptionDisappearsAndEnglishKeepsTheReceivingPrompt()
        {
            var doc=root.AddComponent<UIDocument>();doc.panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>(SurveyorTelevisionBuilder.Folder+"/TelevisionPanelSettings.asset");
            doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SurveyorTelevisionBuilder.UiFolder+"/TelevisionPanel.uxml");var view=root.AddComponent<SurveyorTelevisionView>();
            view.Configure(doc,AssetDatabase.LoadAssetAtPath<TextAsset>(SurveyorTelevisionBuilder.UiFolder+"/TelevisionTexts.json"),flow,queue,action,new SettingsData{languageCode="en"});Advance(40.1f);
            Assert.That(view.Root.Q("subtitle-plate").style.display.value,Is.EqualTo(DisplayStyle.None));Assert.That(view.Root.Q<Button>("confirm").text,Is.EqualTo("Hold E · receive picture"));Assert.That(view.Root.Q<Image>("image").image,Is.Null);
        }
    }
}
