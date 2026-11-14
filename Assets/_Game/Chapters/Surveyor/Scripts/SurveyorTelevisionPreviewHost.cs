using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorTelevisionPreviewHost:MonoBehaviour,ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDirector owner;[SerializeField] SessionClock clock;[SerializeField] InputRouter input;[SerializeField] InteractionService service;
        [SerializeField] SequenceDirector sequence;[SerializeField] CameraDirector cameraDirector;[SerializeField] SurveyorCommandDelay queue;[SerializeField] SurveyorCommandInteraction action;
        [SerializeField] SurveyorDescent descent;[SerializeField] SurveyorDescentCameraGate gate;[SerializeField] SurveyorDescentView descentView;
        [SerializeField] SurveyorTelevision flow;[SerializeField] SurveyorTelevisionOutput output;[SerializeField] SurveyorTelevisionView view;
        [SerializeField] UIDocument descentDocument;[SerializeField] UIDocument televisionDocument;[SerializeField] TextAsset descentTexts;[SerializeField] TextAsset texts;
        [SerializeField] ChapterDefinition chapter;[SerializeField] SequenceDefinition descentData;[SerializeField] SequenceDefinition data;[SerializeField] Transform indicator;
        [SerializeField] Camera television;[SerializeField] SurveyorSiteViewport viewport;[SerializeField] Material scanMaterial;
        SettingsStore settings;bool initialized;
        public ChapterDirector Owner=>owner;public SessionClock Clock=>clock;public InputRouter Input=>input;public InteractionService Service=>service;
        public SurveyorCommandDelay Queue=>queue;public SurveyorCommandInteraction Action=>action;public SurveyorDescent Descent=>descent;
        public SurveyorTelevision Flow=>flow;public SurveyorTelevisionOutput Output=>output;public SurveyorTelevisionView View=>view;
        public SurveyorDescentCameraGate Gate=>gate;public SettingsStore Settings=>settings;public UIDocument Document=>televisionDocument;
        public void Configure(ChapterDirector director,SessionClock session,InputRouter router,InteractionService interaction,SequenceDirector timeline,CameraDirector camera,
            SurveyorCommandDelay delay,SurveyorCommandInteraction command,SurveyorDescent landing,SurveyorDescentCameraGate cameraGate,SurveyorDescentView landingView,
            SurveyorTelevision controller,SurveyorTelevisionOutput image,SurveyorTelevisionView panel,UIDocument landingUi,UIDocument ui,TextAsset landingText,TextAsset text,
            ChapterDefinition definition,SequenceDefinition landingTrack,SequenceDefinition track,Transform altitude,Camera tv,SurveyorSiteViewport source,Material material)
        {
            owner=director;clock=session;input=router;service=interaction;sequence=timeline;cameraDirector=camera;queue=delay;action=command;descent=landing;gate=cameraGate;descentView=landingView;
            flow=controller;output=image;view=panel;descentDocument=landingUi;televisionDocument=ui;descentTexts=landingText;texts=text;chapter=definition;descentData=landingTrack;data=track;indicator=altitude;television=tv;viewport=source;scanMaterial=material;
        }
        void Start()=>Initialize();
        public void Initialize(SettingsStore storage=null)
        {
            if(initialized)return;settings=storage??new SettingsStore();settings.Load();var options=settings.Current;
            owner.Initialize(clock,input,this,new EvidenceStore());input.Enable();service.Initialize(input,clock);service.Mode=options.holdMode;sequence.Initialize(cameraDirector,null,clock);
            descent.Configure(owner,clock,input,service,queue,action,sequence,descentData,indicator);gate.Configure(descent,owner,television,viewport);output.Configure(viewport,scanMaterial);
            flow.Configure(owner,clock,input,service,queue,action,sequence,data,descent,gate,output);
            descentView.Configure(descentDocument,descentTexts,descent,queue,action,options);view.Configure(televisionDocument,texts,flow,queue,action,options);
            view.StartRequested+=Begin;view.PauseRequested+=Pause;view.ResumeRequested+=Resume;view.RestartRequested+=Restart;view.ReturnRequested+=Return;view.LanguageRequested+=Language;
            descentView.PauseRequested+=Pause;descentView.ResumeRequested+=Resume;descentView.RestartRequested+=Restart;descentView.ReturnRequested+=Return;descentView.LanguageRequested+=Language;
            owner.StateChanged+=OnState;owner.StepEntered+=OnStep;settings.Changed+=ApplySettings;input.PauseDown+=TogglePause;
            initialized=true;owner.OpenDossier();RefreshVisibility();
        }
        public void Begin()=>owner.Begin(chapter);public void Restart()=>owner.RestartChapter();public void Return()=>owner.ReturnToDossier();
        public void Pause()=>owner.Pause("television.menu");public void Resume(){owner.Resume("television.menu");owner.Resume("television.focus");owner.Resume("television.application");}
        void Language()=>settings.Apply(s=>s.languageCode=s.languageCode=="pt"?"en":"pt");
        void ApplySettings(SettingsData s){service.Mode=s.holdMode;view.ApplySettings(s);descentView.ApplySettings(s);}
        void TogglePause(){if(owner.IsPaused)Resume();else Pause();}
        void OnState(AppState state)=>RefreshVisibility();void OnStep(string id)=>RefreshVisibility();
        void RefreshVisibility()
        {
            bool landing=owner.State==AppState.Playing&&owner.CurrentStepId==SurveyorDescent.StepId;
            descentDocument.rootVisualElement.style.display=landing?DisplayStyle.Flex:DisplayStyle.None;
            televisionDocument.rootVisualElement.style.display=landing?DisplayStyle.None:DisplayStyle.Flex;view.Refresh();
        }
        void OnApplicationFocus(bool focused){if(initialized&&!focused)owner.Pause("television.focus");}
        void OnApplicationPause(bool paused){if(initialized&&paused)owner.Pause("television.application");}
        void ChapterDirector.ISceneLoader.Load(string name,Action loaded,Action<string> failed)
        {if(gameObject.scene.IsValid()&&gameObject.scene.isLoaded&&gameObject.scene.name==name)loaded();else failed("Open SurveyorFirstImagePreview.");}
        void ChapterDirector.ISceneLoader.Unload(string name,Action unloaded)=>unloaded();
        public void Dispose()
        {
            if(!initialized)return;initialized=false;view.StartRequested-=Begin;view.PauseRequested-=Pause;view.ResumeRequested-=Resume;view.RestartRequested-=Restart;view.ReturnRequested-=Return;view.LanguageRequested-=Language;
            descentView.PauseRequested-=Pause;descentView.ResumeRequested-=Resume;descentView.RestartRequested-=Restart;descentView.ReturnRequested-=Return;descentView.LanguageRequested-=Language;
            owner.StateChanged-=OnState;owner.StepEntered-=OnStep;settings.Changed-=ApplySettings;input.PauseDown-=TogglePause;flow.Dispose();descent.Dispose();gate.Dispose();output.Dispose();clock.StopActive();
        }
        void OnDisable()=>Dispose();
    }
}
