using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorDescentPreviewHost:MonoBehaviour,ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDirector owner;[SerializeField] SessionClock clock;[SerializeField] InputRouter input;[SerializeField] InteractionService service;
        [SerializeField] CameraDirector cameraDirector;[SerializeField] SequenceDirector sequence;[SerializeField] SurveyorCommandDelay queue;[SerializeField] SurveyorCommandInteraction action;
        [SerializeField] SurveyorDescent flow;[SerializeField] SurveyorDescentView view;[SerializeField] SurveyorDescentCameraGate gate;[SerializeField] SurveyorDescentAudio audioView;
        [SerializeField] UIDocument document;[SerializeField] TextAsset texts;[SerializeField] ChapterDefinition chapter;[SerializeField] SequenceDefinition track;[SerializeField] Transform indicator;
        [SerializeField] Camera television;[SerializeField] SurveyorSiteViewport viewport;[SerializeField] AudioSource engine;[SerializeField] AudioSource impacts;
        [SerializeField] AudioClip first,second,third;
        SettingsStore settings;bool initialized;
        public ChapterDirector Owner=>owner;public SessionClock Clock=>clock;public InputRouter Input=>input;public InteractionService Service=>service;
        public SurveyorDescent Flow=>flow;public SurveyorCommandDelay Queue=>queue;public SurveyorCommandInteraction Action=>action;
        public SurveyorDescentView View=>view;public SurveyorDescentCameraGate Gate=>gate;public SettingsStore Settings=>settings;
        public void Configure(ChapterDirector director,SessionClock session,InputRouter router,InteractionService interaction,CameraDirector camera,SequenceDirector timeline,
            SurveyorCommandDelay delay,SurveyorCommandInteraction command,SurveyorDescent controller,SurveyorDescentView panel,SurveyorDescentCameraGate cameraGate,
            SurveyorDescentAudio sound,UIDocument ui,TextAsset data,ChapterDefinition definition,SequenceDefinition sequenceData,Transform altitude,Camera tv,SurveyorSiteViewport tvViewport,
            AudioSource motor,AudioSource contacts,AudioClip a,AudioClip b,AudioClip c)
        {
            owner=director;clock=session;input=router;service=interaction;cameraDirector=camera;sequence=timeline;queue=delay;action=command;flow=controller;view=panel;
            gate=cameraGate;audioView=sound;document=ui;texts=data;chapter=definition;track=sequenceData;indicator=altitude;television=tv;viewport=tvViewport;engine=motor;impacts=contacts;first=a;second=b;third=c;
        }
        void Start()=>Initialize();
        public void Initialize(SettingsStore storage=null)
        {
            if(initialized)return;settings=storage??new SettingsStore();settings.Load();var options=settings.Current;
            owner.Initialize(clock,input,this,new EvidenceStore());input.Enable();service.Initialize(input,clock);service.Mode=options.holdMode;
            sequence.Initialize(cameraDirector,null,clock);flow.Configure(owner,clock,input,service,queue,action,sequence,track,indicator);
            gate.Configure(flow,owner,television,viewport);view.Configure(document,texts,flow,queue,action,options);audioView.Configure(flow,clock,engine,impacts,first,second,third,options);
            view.StartRequested+=Begin;view.PauseRequested+=Pause;view.ResumeRequested+=Resume;view.RestartRequested+=Restart;view.ReturnRequested+=Return;view.LanguageRequested+=Language;
            settings.Changed+=ApplySettings;owner.StateChanged+=OnState;input.PauseDown+=TogglePause;initialized=true;owner.OpenDossier();view.Refresh();
        }
        public void Begin()=>owner.Begin(chapter);public void Restart()=>owner.RestartChapter();public void Return()=>owner.ReturnToDossier();
        public void Pause()=>owner.Pause("descent.menu");public void Resume(){owner.Resume("descent.menu");owner.Resume("descent.focus");owner.Resume("descent.application");}
        void TogglePause(){if(owner.IsPaused)Resume();else Pause();}
        void Language()=>settings.Apply(s=>s.languageCode=s.languageCode=="pt"?"en":"pt");
        void ApplySettings(SettingsData data){service.Mode=data.holdMode;view.ApplySettings(data);audioView.ApplySettings(data);}
        void OnState(AppState state)=>view.Refresh();
        void OnApplicationFocus(bool focused){if(initialized&&!focused)owner.Pause("descent.focus");}
        void OnApplicationPause(bool paused){if(initialized&&paused)owner.Pause("descent.application");}
        void ChapterDirector.ISceneLoader.Load(string name,Action loaded,Action<string> failed)
        {var scene=gameObject.scene;if(scene.IsValid()&&scene.isLoaded&&scene.name==name)loaded();else failed("Abra a cena SurveyorDescentPreview.");}
        void ChapterDirector.ISceneLoader.Unload(string name,Action unloaded)=>unloaded();
        void OnDisable()
        {
            if(!initialized)return;initialized=false;view.StartRequested-=Begin;view.PauseRequested-=Pause;view.ResumeRequested-=Resume;view.RestartRequested-=Restart;view.ReturnRequested-=Return;view.LanguageRequested-=Language;
            settings.Changed-=ApplySettings;owner.StateChanged-=OnState;input.PauseDown-=TogglePause;flow.Stop();gate.Dispose();audioView.Dispose();clock.StopActive();
        }
    }
}
