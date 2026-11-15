using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Composition of the delivered acts, with one injected owner, clock, router, interaction service and sequence.</summary>
    public sealed class SurveyorChapterHost:MonoBehaviour,ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDefinition chapter;
        [SerializeField] SequenceDefinition[] tracks;
        [SerializeField] TextAsset[] viewTexts;
        [SerializeField] SurveyorSubtitles subtitleData;
        [SerializeField] TextAsset chapterTexts;
        [SerializeField] CameraDirector cameras;
        [SerializeField] Camera telemetryCamera,televisionCamera;
        [SerializeField] SurveyorSiteBindings site;
        [SerializeField] SurveyorSiteViewport viewport;
        [SerializeField] Material scan;
        [SerializeField] GameObject trench,arm;
        [SerializeField] Renderer replacedGround;
        [SerializeField] Renderer[] nativeParts;
        [SerializeField] Transform pressMark,impactMark,montageMarks,nightRig,skyRig,groundRig,landingRig,humanRig;
        [SerializeField] Light sun;
        [SerializeField] SurveyorEclipseImage eclipseImage;
        [SerializeField] SurveyorVisitActors visitor;
        [SerializeField] SurveyorEnding ending;
        [SerializeField] SurveyorSoilResults soilResults;
        [SerializeField] SurveyorObservationTargets targets;
        [SerializeField] UIDocument[] panels;
        [SerializeField] UIDocument story,overlay;
        [SerializeField] PauseMenu pause;
        [SerializeField] AudioSource launchSound,engineSound,contactSound,confirmation;
        [SerializeField] AudioClip[] contacts;
        SettingsStore settings;InputRouter input;InteractionService service;SessionClock clock;ChapterDirector owner;
        SequenceDirector sequence;SubtitlePresenter presenter;SurveyorChapterSubtitles subtitles;
        SurveyorCommandDelay queue;SurveyorCommandInteraction action;SurveyorDescentCameraGate gate;SurveyorTelevisionOutput output;FocusPause focus;
        LaunchTelemetryView launchView;SurveyorMidcourseView cruiseView;SurveyorDescentView descentView;SurveyorTelevisionView televisionView;SurveyorObservationView observationView;SurveyorSoilPanel soilPanel;
        SurveyorDescentAudio descentAudio;AudioDirector music;
        bool initialized,standalone;string activeStep;
        static readonly string[] PanelSteps={"Launch","Cruise","Descent","FirstImage","Observe","Sample"};
        public ChapterDirector Owner=>owner;public SessionClock Clock=>clock;public InputRouter Input=>input;public InteractionService Service=>service;
        public SurveyorLaunchController Launch{get;private set;}public SurveyorMidcourse Cruise{get;private set;}public SurveyorDescent Descent{get;private set;}
        public SurveyorTelevision Television{get;private set;}public SurveyorObservation Observation{get;private set;}public SurveyorScoop Scoop{get;private set;}public SurveyorSoilTests Soil{get;private set;}
        public SurveyorEclipse Eclipse{get;private set;}public SurveyorNight Night{get;private set;}public SurveyorVisit Visit{get;private set;}
        public SurveyorTelevisionOutput Output=>output;public SurveyorSiteViewport Viewport=>viewport;public SequenceDirector Sequence=>sequence;public PauseMenu Pause=>pause;
        public LocalizationService Localization{get;}=new LocalizationService();public UIDocument Story=>story;public SettingsStore Settings=>settings;
        void Start(){if(!initialized)Initialize();}
        public void Initialize(ChapterDirector sharedOwner=null,SessionClock sharedClock=null,InputRouter sharedInput=null,InteractionService sharedInteractions=null,SettingsStore sharedSettings=null)
        {
            if(initialized)return;standalone=sharedOwner==null;owner=sharedOwner!=null?sharedOwner:GetComponent<ChapterDirector>();clock=sharedClock!=null?sharedClock:GetComponent<SessionClock>();input=sharedInput!=null?sharedInput:GetComponent<InputRouter>();service=sharedInteractions!=null?sharedInteractions:GetComponent<InteractionService>();
            settings=sharedSettings??new SettingsStore();if(sharedSettings==null)settings.Load();
            if(standalone){owner.enabled=clock.enabled=input.enabled=service.enabled=true;owner.Initialize(clock,input,this,new EvidenceStore());input.Enable();service.Initialize(input,clock);}
            var errors=new List<string>();foreach(var data in viewTexts)errors.AddRange(Localization.AddTable(NormalizeTable(data.text),data.name));errors.AddRange(Localization.AddTable(chapterTexts.text,"chapter"));errors.AddRange(Localization.AddTable(subtitleData.BuildTableJson(errors),"surveyor"));if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));Localization.ApplySettings(settings.Current);
            sequence=GetComponent<SequenceDirector>();queue=GetComponent<SurveyorCommandDelay>();action=GetComponent<SurveyorCommandInteraction>();gate=GetComponent<SurveyorDescentCameraGate>();output=GetComponent<SurveyorTelevisionOutput>();presenter=GetComponent<SubtitlePresenter>();focus=GetComponent<FocusPause>();
            Launch=GetComponent<SurveyorLaunchController>();Cruise=GetComponent<SurveyorMidcourse>();Descent=GetComponent<SurveyorDescent>();Television=GetComponent<SurveyorTelevision>();Observation=GetComponent<SurveyorObservation>();Scoop=GetComponent<SurveyorScoop>();Soil=GetComponent<SurveyorSoilTests>();Eclipse=GetComponent<SurveyorEclipse>();Night=GetComponent<SurveyorNight>();Visit=GetComponent<SurveyorVisit>();
            cameras.GroundProbe=p=>p.y-1;cameras.Initialize(telemetryCamera);sequence.Initialize(cameras,null,clock);sequence.enabled=false;
            queue.Configure(owner,clock);action.Configure(queue,service,input);output.Configure(viewport,scan);
            Launch.Configure(owner,input,service,sequence,tracks[0],clock);
            Cruise.Configure(owner,input,clock,service,queue,action,sequence,tracks[1]);
            Descent.Configure(owner,clock,input,service,queue,action,sequence,tracks[2],transform.Find("AltitudeIndicator"));gate.Configure(Descent,owner,televisionCamera,viewport);
            Television.Configure(owner,clock,input,service,queue,action,sequence,tracks[3],Descent,gate,output);
            Observation.Configure(owner,clock,input,service,queue,action,site,televisionCamera,output,targets);
            Scoop.Configure(owner,clock,input,service,trench,arm,new[]{replacedGround},nativeParts);
            Soil.Configure(owner,clock,input,service,Scoop,output,televisionCamera,soilResults,pressMark,impactMark,montageMarks);
            Soil.SetOpticalOccluders(new[]{arm.transform.Find("Shoulder/UpperLink").GetComponent<Renderer>()});
            cameras.Initialize(televisionCamera);
            Eclipse.Configure(owner,clock,input,service,queue,action,sequence,tracks[4],site,eclipseImage,skyRig,groundRig);
            Night.Configure(owner,clock,input,sequence,cameras,tracks[5],tracks[6],sun,nightRig,new[]{engineSound,launchSound});
            Visit.Configure(owner,clock,input,sequence,tracks[7],ending,visitor,cameras,landingRig,humanRig);
            cameras.Initialize(telemetryCamera);sequence.Initialize(cameras,null,clock);arm.SetActive(false);foreach(var r in nativeParts)r.enabled=true;
            ConfigureViews();presenter.Initialize(Localization,null,overlay.rootVisualElement.Q("chapter-subtitles"));subtitles=GetComponent<SurveyorChapterSubtitles>();subtitles.Initialize(presenter,subtitleData,clock,settings);
            sequence.CueEntered+=subtitles.Show;sequence.CueExited+=subtitles.Hide;Observation.CueEntered+=subtitles.Show;Observation.CueExited+=subtitles.Hide;Scoop.CueEntered+=subtitles.Show;Scoop.CueExited+=subtitles.Hide;Soil.CueEntered+=subtitles.Show;Soil.CueExited+=subtitles.Hide;
            pause.Initialize(owner,input,settings);descentAudio=GetComponent<SurveyorDescentAudio>();descentAudio.Configure(Descent,clock,engineSound,contactSound,contacts[0],contacts[1],contacts[2],settings.Current);
            music=GetComponent<AudioDirector>();music.Initialize(GetComponent<SurveyorChapterAudio>().Catalog,clock,settings.Current);music.BindInteraction(service);
            Launch.AudioCueRequested+=LaunchAudio;Observation.PictureRecorded+=PhotoSound;
            owner.StepEntered+=Step;owner.StateChanged+=State;owner.ChapterUnloading+=Unloading;owner.PausedChanged+=Paused;settings.Changed+=ApplySettings;initialized=true;ApplySettings(settings.Current);
            if(standalone)owner.OpenDossier();Refresh();
        }
        void ConfigureViews()
        {
            launchView=panels[0].GetComponent<LaunchTelemetryView>();launchView.Configure(panels[0],viewTexts[0],Launch,settings.Current);launchView.DrawLocalSubtitles=false;launchView.TextResolver=Localization.Get;
            cruiseView=panels[1].GetComponent<SurveyorMidcourseView>();cruiseView.Configure(panels[1],viewTexts[1],Cruise,queue,action,settings.Current);cruiseView.DrawLocalSubtitles=false;cruiseView.TextResolver=Localization.Get;
            descentView=panels[2].GetComponent<SurveyorDescentView>();descentView.Configure(panels[2],viewTexts[2],Descent,queue,action,settings.Current);descentView.DrawLocalSubtitles=false;descentView.TextResolver=Localization.Get;
            televisionView=panels[3].GetComponent<SurveyorTelevisionView>();televisionView.Configure(panels[3],viewTexts[3],Television,queue,action,settings.Current);televisionView.DrawLocalSubtitles=false;televisionView.TextResolver=Localization.Get;
            observationView=panels[4].GetComponent<SurveyorObservationView>();observationView.Configure(panels[4],viewTexts[4],Observation,queue,action,output,settings.Current);observationView.DrawLocalSubtitles=false;observationView.TextResolver=Localization.Get;
            soilPanel=GetComponent<SurveyorSoilPanel>();soilPanel.Initialize(panels[5],Soil,output,Localization);panels[5].rootVisualElement.Q("cover").style.display=DisplayStyle.None;panels[5].rootVisualElement.Q("restart").style.display=DisplayStyle.None;
            launchView.StartRequested+=Begin;launchView.PauseRequested+=PauseGame;launchView.ResumeRequested+=Resume;launchView.RestartRequested+=Restart;launchView.ReturnRequested+=Return;launchView.LanguageRequested+=Language;
            cruiseView.StartRequested+=Begin;cruiseView.PauseRequested+=PauseGame;cruiseView.ResumeRequested+=Resume;cruiseView.RestartRequested+=Restart;cruiseView.ReturnRequested+=Return;cruiseView.LanguageRequested+=Language;
            descentView.StartRequested+=Begin;descentView.PauseRequested+=PauseGame;descentView.ResumeRequested+=Resume;descentView.RestartRequested+=Restart;descentView.ReturnRequested+=Return;descentView.LanguageRequested+=Language;
            televisionView.StartRequested+=Begin;televisionView.PauseRequested+=PauseGame;televisionView.ResumeRequested+=Resume;televisionView.RestartRequested+=Restart;televisionView.ReturnRequested+=Return;televisionView.LanguageRequested+=Language;
            observationView.PauseRequested+=PauseGame;observationView.ResumeRequested+=Resume;observationView.RestartRequested+=Restart;observationView.ReturnRequested+=Return;observationView.LanguageRequested+=Language;
            foreach(var panel in panels)foreach(var name in new[]{"pause-button","mute-button","language-button","pause","language","mute","pause-overlay"}){var control=panel.rootVisualElement.Q(name);if(control!=null)control.style.visibility=Visibility.Hidden;}
            var send=story.rootVisualElement.Q<Button>("story-send");send.RegisterCallback<PointerDownEvent>(e=>{if(e.button!=0)return;if(Eclipse.BeginConfirmation())send.CapturePointer(e.pointerId);e.StopPropagation();});send.RegisterCallback<PointerUpEvent>(e=>{Eclipse.ReleaseConfirmation();if(send.HasPointerCapture(e.pointerId))send.ReleasePointer(e.pointerId);e.StopPropagation();});send.RegisterCallback<PointerCaptureOutEvent>(e=>Eclipse.ReleaseConfirmation());story.rootVisualElement.Q<Button>("story-return").clicked+=FinalReturn;
        }
        public void Begin(){music.Unlock();owner.Begin(chapter);}public void Restart()=>owner.RestartChapter();public void Return()=>owner.ReturnToDossier();
        public void PauseGame()=>pause.Controller?.RequestPause();public void Resume()=>pause.Controller?.Continue();void Language()=>settings.Apply(v=>v.languageCode=v.languageCode=="pt"?"en":"pt");
        void FinalReturn()=>Visit.ReturnFromButton();
        void LaunchAudio(string id){if(id=="surveyor.launch.bed"&&!launchSound.isPlaying)launchSound.Play();}void PhotoSound(){confirmation.Play();}
        void Paused(bool value){music.SetPaused(value);if(value){launchSound.Pause();confirmation.Pause();}else{launchSound.UnPause();confirmation.UnPause();}Refresh();}
        void Step(string id)
        {
            activeStep=id;
            Debug.Log("SURVEYOR_STEP "+id+" active="+clock.ActiveSeconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
            if(id=="Launch"||id=="FirstImage"||id=="Observe"){arm.SetActive(false);foreach(var r in nativeParts)r.enabled=true;}
            if(id=="Sample"){arm.SetActive(true);foreach(var r in nativeParts)r.enabled=false;}
            if(id=="FirstImage"){cameras.Initialize(televisionCamera);telemetryCamera.enabled=false;}
            if(id=="ImaginedVisit"){sun.intensity=1.05f;sun.transform.rotation=Quaternion.Euler(8,55,0);RenderSettings.ambientLight=new Color(.065f,.072f,.082f);}
            string cue=id=="Launch"||id=="Cruise"?"MUS_SUR_AVESSO":id=="ImaginedVisit"||id=="HistoricalReceipt"||id=="End"?"MUS_SUR_HUMANO":"MUS_SUR_MAQUINA";music.SetCue(cue,1.5f);
            if(id!="Launch")launchSound.Stop();Refresh();
        }
        void State(AppState state){if(state==AppState.Playing)music.Unlock();if(state==AppState.Completion){music.StopAll(0);Debug.Log("SURVEYOR_COMPLETE active="+clock.ActiveSeconds.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));}Refresh();}
        void Unloading(string id){music.StopAll(0);launchSound.Stop();engineSound.Stop();contactSound.Stop();confirmation.Stop();}
        void ApplySettings(SettingsData options)
        {
            Localization.ApplySettings(options);service.Mode=options.holdMode;launchView.ApplySettings(options);cruiseView.ApplySettings(options);descentView.ApplySettings(options);televisionView.ApplySettings(options);observationView.ApplySettings(options);soilPanel.Refresh();
            if(presenter.Layer!=null)presenter.Layer.Query<Label>(className:"sa-subtitle__text").ForEach(label=>label.style.fontSize=28*options.textScale);
            Scoop.MotorVolume=.06f*options.masterVolume*options.sfxVolume;Eclipse.ReducedMotion=Night.ReducedMotion=Visit.ReducedMotion=options.reducedMotion;launchSound.volume=.09f*options.masterVolume*options.sfxVolume;confirmation.volume=.2f*options.masterVolume*options.uiVolume;descentAudio.ApplySettings(options);music.ApplySettings(options);Refresh();
        }
        static void Show(VisualElement e,bool show)=>e.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
        void Update(){if(!initialized)return;if(!focus.IsInitialized&&pause.Controller!=null)focus.Initialize(pause.Controller,owner);Refresh();}
        public void Refresh()
        {
            if(!initialized)return;string step=owner.CurrentStepId;bool dossier=standalone&&owner.State==AppState.Dossier;
            for(int i=0;i<panels.Length;i++)Show(panels[i].rootVisualElement,(owner.State==AppState.Playing&&step==PanelSteps[i])||(i==0&&dossier));
            bool storyActive=step=="Eclipse"||step=="LunarNight"||step=="TimePassage"||step=="ImaginedVisit"||step=="HistoricalReceipt"||step=="End"||Visit.AwaitingReturn;var root=story.rootVisualElement;Show(root,storyActive);overlay.rootVisualElement.style.display=owner.State==AppState.Playing&&!owner.IsPaused||Visit.AwaitingReturn?DisplayStyle.Flex:DisplayStyle.None;
            var notice=overlay.rootVisualElement.Q("chapter-subtitles");bool centered=Visit.Running&&Visit.TimeSeconds<5;notice.style.top=centered?new StyleLength(0f):new StyleLength(StyleKeyword.Auto);notice.style.justifyContent=centered?Justify.Center:Justify.FlexEnd;if(!storyActive)return;
            var image=root.Q<Image>("story-picture");bool eclipseColor=step=="Eclipse"&&Eclipse.Phase==EclipsePhase.Viewing&&Eclipse.SequenceTime>=4&&Eclipse.SequenceTime<22;
            image.image=Visit.ShowArchive?ending.HistoricalPhoto:Visit.ShowWorld||eclipseColor?viewport.Texture:output.Texture;image.scaleMode=ScaleMode.ScaleToFit;
            float crop=step=="Eclipse"?Eclipse.Enlargement:0,size=Mathf.Lerp(1,.38f,crop);image.uv=new Rect((1-size)*.5f,Mathf.Lerp(0,.59f,crop),size,size);
            Show(image,step=="Eclipse"||step=="LunarNight"||step=="TimePassage"||Visit.ShowArchive||Visit.ShowWorld);
            var date=root.Q<Label>("story-date");date.text=Localization.Get(step=="Eclipse"?"sur.eclipse.date":step=="LunarNight"?"sur.night.contact":step=="TimePassage"?Night.CalendarKey:Visit.TimeSeconds<19?"sur.visit.landingDate":"sur.visit.visitDate");Show(date,!Visit.ShowArchive&&!Visit.ShowMemorial&&(!Visit.Running||Visit.ShowWorld));
            var credit=root.Q<Label>("story-credit");Show(credit,Visit.ShowArchive);credit.text=Localization.Get(ending.CreditKey);
            var title=root.Q<Label>("story-title");Show(title,Visit.ShowMemorial);title.text=Localization.Get("sur.visit.title");var location=root.Q<Label>("story-location");Show(location,Visit.ShowMemorial);location.text=Localization.Get("sur.visit.location");
            var total=root.Q<Label>("story-total");Show(total,step=="LunarNight"&&Night.TransmissionEnded||step=="TimePassage");total.text=Localization.Get(step=="TimePassage"?"sur.night.months":"sur.night.total");
            var send=root.Q<Button>("story-send");Show(send,step=="Eclipse"&&Eclipse.Phase==EclipsePhase.AwaitingOrder);send.text=Localization.Get("sur.eclipse.send");send.SetEnabled(Eclipse.CanConfirm);
            var back=root.Q<Button>("story-return");Show(back,Visit.AwaitingReturn);back.text=Localization.Get("sur.visit.return");back.SetEnabled(Visit.CanClickReturn);
            overlay.rootVisualElement.style.display=owner.State==AppState.Playing&&!owner.IsPaused||Visit.AwaitingReturn?DisplayStyle.Flex:DisplayStyle.None;
            float width=Mathf.Min(root.resolvedStyle.width-40,(root.resolvedStyle.height-170)*4/3);var frame=root.Q("story-frame");if(float.IsFinite(width)&&width>0){frame.style.width=width;frame.style.height=width*.75f;}
        }
        void ChapterDirector.ISceneLoader.Load(string name,Action done,Action<string> failed){if(name==gameObject.scene.name)done();else failed("Open SurveyorChapter or SurveyorArchive.");}
        void ChapterDirector.ISceneLoader.Unload(string name,Action done)=>done();
        [Serializable] sealed class LegacyRow{public string id,pt,en;}
        [Serializable] sealed class LegacyTable{public LegacyRow[] entries;}
        [Serializable] sealed class TextRow{public string key,pt,en;}
        [Serializable] sealed class TextTable{public TextRow[] texts;}
        // Earlier panels use {entries,id}; adapt once without changing their source data.
        static string NormalizeTable(string json)
        {
            var legacy=JsonUtility.FromJson<LegacyTable>(json);if(legacy.entries==null)return json;
            var rows=new TextRow[legacy.entries.Length];for(int i=0;i<rows.Length;i++){var r=legacy.entries[i];rows[i]=new TextRow{key=r.id,pt=r.pt,en=r.en};}return JsonUtility.ToJson(new TextTable{texts=rows});
        }
        void OnDisable()
        {
            if(!initialized)return;initialized=false;owner.StepEntered-=Step;owner.StateChanged-=State;owner.ChapterUnloading-=Unloading;owner.PausedChanged-=Paused;settings.Changed-=ApplySettings;
            sequence.CueEntered-=subtitles.Show;sequence.CueExited-=subtitles.Hide;Observation.CueEntered-=subtitles.Show;Observation.CueExited-=subtitles.Hide;Scoop.CueEntered-=subtitles.Show;Scoop.CueExited-=subtitles.Hide;Soil.CueEntered-=subtitles.Show;Soil.CueExited-=subtitles.Hide;
            Launch.AudioCueRequested-=LaunchAudio;Observation.PictureRecorded-=PhotoSound;music.StopAll(0);music.UnbindInteraction();output.Dispose();
        }
    }
}
