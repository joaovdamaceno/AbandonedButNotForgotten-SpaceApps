using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Entrada local executável do ato. R1 liga o prefab aos mesmos serviços no capítulo integrado.</summary>
    public sealed class SurveyorLaunchPreviewHost : MonoBehaviour,ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDirector chapter;
        [SerializeField] SessionClock clock;
        [SerializeField] InputRouter router;
        [SerializeField] InteractionService interactions;
        [SerializeField] CameraDirector cameraDirector;
        [SerializeField] SequenceDirector sequence;
        [SerializeField] SurveyorLaunchController flow;
        [SerializeField] LaunchTelemetryView view;
        [SerializeField] UIDocument document;
        [SerializeField] TextAsset textData;
        [SerializeField] ChapterDefinition definition;
        [SerializeField] SequenceDefinition launch;
        [SerializeField] AudioSource audioBed;
        SettingsStore settings;
        SettingsData currentSettings;
        bool initialized;
        float previousMaster=1;
        public ChapterDirector Chapter=>chapter;
        public SessionClock Clock=>clock;
        public InputRouter Router=>router;
        public InteractionService Interactions=>interactions;
        public SurveyorLaunchController Flow=>flow;
        public LaunchTelemetryView View=>view;
        public SettingsStore Settings=>settings;

        public void Configure(ChapterDirector owner,SessionClock session,InputRouter input,InteractionService interaction,
            CameraDirector camera,SequenceDirector timeline,SurveyorLaunchController controller,LaunchTelemetryView presentation,
            UIDocument ui,TextAsset texts,ChapterDefinition chapterData,SequenceDefinition sequenceData,AudioSource audio)
        {
            chapter=owner;clock=session;router=input;interactions=interaction;cameraDirector=camera;sequence=timeline;flow=controller;
            view=presentation;document=ui;textData=texts;definition=chapterData;launch=sequenceData;audioBed=audio;
        }

        void Start()=>Initialize();
        public void Initialize(SettingsStore store=null)
        {
            if(initialized)return;
            if(chapter==null || clock==null || router==null || interactions==null || cameraDirector==null || sequence==null || flow==null || view==null || document==null)
                throw new InvalidOperationException("Preview SUR-003 sem referências; reconstrua pelo builder.");
            settings=store??new SettingsStore();settings.Load();
            currentSettings=settings.Current;
            chapter.Initialize(clock,router,this,new EvidenceStore());router.Enable();
            interactions.Initialize(router,clock);interactions.Mode=settings.Current.holdMode;
            sequence.Initialize(cameraDirector,null,clock);
            flow.Configure(chapter,router,interactions,sequence,launch,clock);
            view.Configure(document,textData,flow,settings.Current);
            view.StartRequested+=Begin;view.RestartRequested+=Restart;view.ReturnRequested+=Return;
            view.PauseRequested+=Pause;view.ResumeRequested+=Resume;view.LanguageRequested+=ToggleLanguage;view.MuteRequested+=ToggleMute;
            settings.Changed+=ApplySettings;flow.Changed+=UpdateAudio;
            chapter.StateChanged+=OnState;chapter.PausedChanged+=OnPaused;router.PauseDown+=TogglePause;router.SubmitDown+=OnSubmit;
            initialized=true;chapter.OpenDossier();view.Refresh();
        }

        public void Begin()=>chapter.Begin(definition);
        public void Restart()=>chapter.RestartChapter();
        public void Return()=>chapter.ReturnToDossier();
        public void Pause()=>chapter.Pause("launch.menu");
        public void Resume()
        {
            chapter.Resume("launch.menu");chapter.Resume("launch.focus");chapter.Resume("launch.application");
        }
        void TogglePause(){if(chapter.IsPaused)Resume();else Pause();}
        void OnSubmit(){if(chapter.State==AppState.Dossier)Begin();}
        void OnState(AppState state){view.Refresh();UpdateAudio();}
        void OnPaused(bool paused)
        {
            if(audioBed!=null){if(paused)audioBed.Pause();else audioBed.UnPause();}
            view.Refresh();UpdateAudio();
        }
        void OnApplicationFocus(bool focused){if(initialized && !focused)chapter.Pause("launch.focus");}
        void OnApplicationPause(bool paused){if(initialized && paused)chapter.Pause("launch.application");}
        public void ToggleLanguage()
        {
            settings.Apply(data=>data.languageCode=data.languageCode==SettingsData.Portuguese?SettingsData.English:SettingsData.Portuguese);
        }
        public void ToggleMute()
        {
            settings.Apply(data=>
            {
                if(data.masterVolume>0){previousMaster=data.masterVolume;data.masterVolume=0;}
                else data.masterVolume=previousMaster;
            });
        }
        void ApplySettings(SettingsData data){currentSettings=data;interactions.Mode=data.holdMode;view.ApplySettings(data);UpdateAudio();}
        void UpdateAudio()
        {
            if(audioBed==null || settings==null)return;
            if(!flow.Running){audioBed.Stop();return;}
            if(!audioBed.isPlaying && !chapter.IsPaused)audioBed.Play();
            var data=currentSettings;
            float envelope=flow.TimeSeconds<flow.AscentAt?Mathf.SmoothStep(0,1,flow.TimeSeconds/flow.AscentAt):
                1-Mathf.SmoothStep(0,1,(flow.TimeSeconds-flow.SendAt)/(flow.ExecuteAt-flow.SendAt));
            audioBed.volume=.13f*data.masterVolume*data.sfxVolume*Mathf.Clamp01(envelope);
        }

        // O preview já está aberto, de verdade. Não simula a existência de Boot/Dossier/UI-002 nem registra uma build.
        void ChapterDirector.ISceneLoader.Load(string name,Action loaded,Action<string> failed)
        {
            var scene=gameObject.scene;
            if(scene.IsValid() && scene.isLoaded && scene.name==name)loaded();
            else failed("Abra a cena local SurveyorLaunchPreview para esta entrada de teste.");
        }
        void ChapterDirector.ISceneLoader.Unload(string name,Action unloaded)=>unloaded();

        void OnDisable()
        {
            if(!initialized)return;initialized=false;
            view.StartRequested-=Begin;view.RestartRequested-=Restart;view.ReturnRequested-=Return;
            view.PauseRequested-=Pause;view.ResumeRequested-=Resume;view.LanguageRequested-=ToggleLanguage;view.MuteRequested-=ToggleMute;
            settings.Changed-=ApplySettings;flow.Changed-=UpdateAudio;
            chapter.StateChanged-=OnState;chapter.PausedChanged-=OnPaused;router.PauseDown-=TogglePause;router.SubmitDown-=OnSubmit;
            flow.Stop();if(audioBed!=null)audioBed.Stop();clock.StopActive();
        }
    }
}
