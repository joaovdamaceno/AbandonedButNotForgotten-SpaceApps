using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorMidcoursePreviewHost:MonoBehaviour,ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDirector owner;
        [SerializeField] SessionClock clock;
        [SerializeField] InputRouter router;
        [SerializeField] InteractionService service;
        [SerializeField] CameraDirector cameraDirector;
        [SerializeField] SequenceDirector sequence;
        [SerializeField] SurveyorCommandDelay queue;
        [SerializeField] SurveyorCommandInteraction action;
        [SerializeField] SurveyorMidcourse flow;
        [SerializeField] SurveyorMidcourseView view;
        [SerializeField] UIDocument document;
        [SerializeField] TextAsset texts;
        [SerializeField] ChapterDefinition chapter;
        [SerializeField] SequenceDefinition cruise;
        SettingsStore settings;
        bool initialized;
        public ChapterDirector Owner=>owner;
        public SessionClock Clock=>clock;
        public InputRouter Router=>router;
        public InteractionService Service=>service;
        public SurveyorCommandDelay Queue=>queue;
        public SurveyorCommandInteraction Action=>action;
        public SurveyorMidcourse Flow=>flow;
        public SurveyorMidcourseView View=>view;
        public SettingsStore Settings=>settings;
        public void Configure(ChapterDirector director,SessionClock session,InputRouter input,InteractionService interaction,
            CameraDirector camera,SequenceDirector timeline,SurveyorCommandDelay delay,SurveyorCommandInteraction command,
            SurveyorMidcourse controller,SurveyorMidcourseView panel,UIDocument ui,TextAsset data,ChapterDefinition definition,SequenceDefinition track)
        {
            owner=director;clock=session;router=input;service=interaction;cameraDirector=camera;sequence=timeline;queue=delay;
            action=command;flow=controller;view=panel;document=ui;texts=data;chapter=definition;cruise=track;
        }
        void Start()=>Initialize();
        public void Initialize(SettingsStore storage=null)
        {
            if(initialized)return;settings=storage??new SettingsStore();settings.Load();
            owner.Initialize(clock,router,this,new EvidenceStore());router.Enable();service.Initialize(router,clock);service.Mode=settings.Current.holdMode;
            sequence.Initialize(cameraDirector,null,clock);flow.Configure(owner,router,clock,service,queue,action,sequence,cruise);
            view.Configure(document,texts,flow,queue,action,settings.Current);
            view.StartRequested+=Begin;view.PauseRequested+=Pause;view.ResumeRequested+=Resume;view.RestartRequested+=Restart;
            view.ReturnRequested+=Return;view.LanguageRequested+=ToggleLanguage;settings.Changed+=ApplySettings;
            owner.StateChanged+=OnState;router.PauseDown+=TogglePause;initialized=true;owner.OpenDossier();view.Refresh();
        }
        public void Begin()=>owner.Begin(chapter);
        public void Restart()=>owner.RestartChapter();
        public void Return()=>owner.ReturnToDossier();
        public void Pause()=>owner.Pause("midcourse.menu");
        public void Resume(){owner.Resume("midcourse.menu");owner.Resume("midcourse.focus");owner.Resume("midcourse.application");}
        void TogglePause(){if(owner.IsPaused)Resume();else Pause();}
        public void ToggleLanguage()=>settings.Apply(data=>data.languageCode=data.languageCode=="pt"?"en":"pt");
        void ApplySettings(SettingsData data){service.Mode=data.holdMode;view.ApplySettings(data);}
        void OnState(AppState state)=>view.Refresh();
        void OnApplicationFocus(bool focused){if(initialized&&!focused)owner.Pause("midcourse.focus");}
        void OnApplicationPause(bool paused){if(initialized&&paused)owner.Pause("midcourse.application");}
        void ChapterDirector.ISceneLoader.Load(string name,Action loaded,Action<string> failed)
        {var scene=gameObject.scene;if(scene.IsValid()&&scene.isLoaded&&scene.name==name)loaded();else failed("Abra a cena SurveyorMidcoursePreview.");}
        void ChapterDirector.ISceneLoader.Unload(string name,Action unloaded)=>unloaded();
        void OnDisable()
        {
            if(!initialized)return;initialized=false;view.StartRequested-=Begin;view.PauseRequested-=Pause;view.ResumeRequested-=Resume;
            view.RestartRequested-=Restart;view.ReturnRequested-=Return;view.LanguageRequested-=ToggleLanguage;
            settings.Changed-=ApplySettings;owner.StateChanged-=OnState;router.PauseDown-=TogglePause;flow.Stop();clock.StopActive();
        }
    }
}
