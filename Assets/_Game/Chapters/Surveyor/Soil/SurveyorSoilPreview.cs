using System;
using Abandoned.SpaceApps.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorSoilPreview:MonoBehaviour,ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDefinition chapter;
        [SerializeField] GameObject trench,arm;
        [SerializeField] Renderer ground;[SerializeField] Renderer[] native;
        [SerializeField] CameraDirector cameras;[SerializeField] SurveyorSiteViewport viewport;
        [SerializeField] Material scan;[SerializeField] SurveyorSoilResults results;
        [SerializeField] Transform pressMark,impactMark,montageMarks;
        [SerializeField] UIDocument document;[SerializeField] TextAsset texts;
        [SerializeField] SurveyorSubtitles subtitlesCatalog;[SerializeField] PauseMenu pause;
        bool initialized;SettingsStore settings;FocusPause focus;InputRouter input;InteractionService service;SubtitlePresenter subtitles;SurveyorTelevisionOutput output;
        public ChapterDirector Owner{get;private set;}public SessionClock Clock{get;private set;}public SurveyorScoop Scoop{get;private set;}public SurveyorSoilTests Flow{get;private set;}
        public UIDocument Document=>document;public PauseMenu Pause=>pause;public SurveyorSiteViewport Viewport=>viewport;public InteractionService Service=>service;public InputRouter Input=>input;
        public LocalizationService Localization{get;}=new LocalizationService();
        public void Configure(ChapterDefinition data,GameObject patch,GameObject rig,Renderer oldGround,Renderer[] oldParts,CameraDirector camera,
            SurveyorSiteViewport source,Material material,SurveyorSoilResults result,Transform press,Transform impact,Transform montage,UIDocument ui,TextAsset catalog,SurveyorSubtitles subtitleData,PauseMenu menu)
        {chapter=data;trench=patch;arm=rig;ground=oldGround;native=oldParts;cameras=camera;viewport=source;scan=material;results=result;pressMark=press;impactMark=impact;montageMarks=montage;document=ui;texts=catalog;subtitlesCatalog=subtitleData;pause=menu;}
        void Start()=>Initialize();
        public void Initialize()
        {
            if(initialized)return;Owner=GetComponent<ChapterDirector>();Clock=GetComponent<SessionClock>();input=GetComponent<InputRouter>();service=GetComponent<InteractionService>();Scoop=GetComponent<SurveyorScoop>();Flow=GetComponent<SurveyorSoilTests>();output=GetComponent<SurveyorTelevisionOutput>();subtitles=GetComponent<SubtitlePresenter>();focus=GetComponent<FocusPause>();
            settings=new SettingsStore();settings.Load();Localization.AddTable(texts.text,"soil");var errors=new System.Collections.Generic.List<string>();Localization.AddTable(subtitlesCatalog.BuildTableJson(errors),"surveyor");if(errors.Count>0)throw new ArgumentException(string.Join("\n",errors));
            Owner.Initialize(Clock,input,this,new EvidenceStore());input.Enable();service.Initialize(input,Clock);output.Configure(viewport,scan);output.Begin();output.SetProgress(1);
            Scoop.Configure(Owner,Clock,input,service,trench,arm,new[]{ground},native);Flow.Configure(Owner,Clock,input,service,Scoop,output,cameras.TargetCamera,results,pressMark,impactMark,montageMarks);
            var root=document.rootVisualElement;root.Q<Button>("begin").clicked+=Begin;root.Q<Button>("restart").clicked+=Restart;
            subtitles.Initialize(Localization,null,root.Q("subtitles"));Scoop.CueEntered+=subtitles.ShowCue;Scoop.CueExited+=subtitles.HideCue;Flow.CueEntered+=subtitles.ShowCue;Flow.CueExited+=subtitles.HideCue;
            GetComponent<SurveyorSoilPanel>().Initialize(document,Flow,output,Localization);pause.Initialize(Owner,input,settings);
            Owner.StateChanged+=State;Owner.StepEntered+=Step;settings.Changed+=Settings;initialized=true;Settings(settings.Current);Owner.OpenDossier();Refresh();
        }
        public void Begin()=>Owner.Begin(chapter);public void Restart()=>Owner.RestartChapter();
        void State(AppState s)=>Refresh();void Step(string id)=>Refresh();
        void Refresh(){var root=document.rootVisualElement;root.Q("cover").style.display=Owner.State==AppState.Dossier?DisplayStyle.Flex:DisplayStyle.None;root.Q<Button>("begin").text=Localization.Get("soil.begin");root.Q<Button>("restart").text=Localization.Get("soil.restart");GetComponent<SurveyorSoilPanel>().Refresh();}
        void Settings(SettingsData value){Localization.ApplySettings(value);service.Mode=value.holdMode;Scoop.MotorVolume=.06f*value.masterVolume*value.sfxVolume;Refresh();}
        void Update(){if(initialized&&!focus.IsInitialized&&pause.Controller!=null)focus.Initialize(pause.Controller,Owner);}
        void ChapterDirector.ISceneLoader.Load(string n,Action done,Action<string> failed){if(n==gameObject.scene.name)done();else failed("Open soil preview.");}
        void ChapterDirector.ISceneLoader.Unload(string n,Action done)=>done();
        void OnDisable(){if(!initialized)return;initialized=false;Owner.StateChanged-=State;Owner.StepEntered-=Step;settings.Changed-=Settings;Scoop.CueEntered-=subtitles.ShowCue;Scoop.CueExited-=subtitles.HideCue;Flow.CueEntered-=subtitles.ShowCue;Flow.CueExited-=subtitles.HideCue;Flow.Dispose();Scoop.Dispose();output.Dispose();input.Disable();}
    }
}
