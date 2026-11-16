using System;
using Abandoned.SpaceApps.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorVisitPreview : MonoBehaviour,ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDefinition chapter;
        [SerializeField] SequenceDefinition track;
        [SerializeField] SurveyorEnding ending;
        [SerializeField] SurveyorVisitActors actors;
        [SerializeField] CameraDirector cameras;
        [SerializeField] Transform landingRig,humanRig;
        [SerializeField] SurveyorSiteViewport viewport;
        [SerializeField] UIDocument document;
        [SerializeField] TextAsset texts;
        [SerializeField] PauseMenu pause;
        public ChapterDirector Owner {get;private set;}
        public SessionClock Clock {get;private set;}
        public SurveyorVisit Flow {get;private set;}
        public SequenceDirector Sequence {get;private set;}
        public SubtitlePresenter Subtitles {get;private set;}
        public LocalizationService Localization {get;}=new LocalizationService();
        public UIDocument Document=>document;
        public SurveyorVisitActors Actors=>actors;
        public SurveyorSiteViewport Viewport=>viewport;
        public PauseMenu Pause=>pause;
        SettingsStore settings;InputRouter input;FocusPause focus;bool initialized;
        Image image;VisualElement cover,slot,frame,memorial,subtitleHost;
        Label date,credit,title,location,coverTitle,description;Button begin,returnButton;
        public void Configure(ChapterDefinition data,SequenceDefinition film,SurveyorEnding result,SurveyorVisitActors cast,
            CameraDirector camera,Transform landing,Transform human,SurveyorSiteViewport source,UIDocument ui,TextAsset catalog,PauseMenu menu)
        {chapter=data;track=film;ending=result;actors=cast;cameras=camera;landingRig=landing;humanRig=human;viewport=source;document=ui;texts=catalog;pause=menu;}
        void Start()=>Initialize();
        public void Initialize()
        {
            if(initialized)return;Owner=GetComponent<ChapterDirector>();Clock=GetComponent<SessionClock>();input=GetComponent<InputRouter>();Flow=GetComponent<SurveyorVisit>();Sequence=GetComponent<SequenceDirector>();Subtitles=GetComponent<SubtitlePresenter>();focus=GetComponent<FocusPause>();
            settings=new SettingsStore();settings.Load();Localization.AddTable(texts.text,texts.name);Localization.ApplySettings(settings.Current);
            Owner.Initialize(Clock,input,this,new EvidenceStore());input.Enable();cameras.GroundProbe=p=>p.y-1;
            Flow.Configure(Owner,Clock,input,Sequence,track,ending,actors,cameras,landingRig,humanRig);
            var root=document.rootVisualElement;image=root.Q<Image>("picture");image.scaleMode=ScaleMode.ScaleToFit;
            cover=root.Q("cover");slot=root.Q("slot");frame=root.Q("frame");memorial=root.Q("memorial");subtitleHost=root.Q("subtitles");
            date=root.Q<Label>("date");credit=root.Q<Label>("credit");title=root.Q<Label>("title");location=root.Q<Label>("location");
            coverTitle=root.Q<Label>("cover-title");description=root.Q<Label>("description");begin=root.Q<Button>("begin");returnButton=root.Q<Button>("return");
            begin.clicked+=Begin;returnButton.clicked+=Return;slot.RegisterCallback<GeometryChangedEvent>(e=>Fit());
            Subtitles.Initialize(Localization,Sequence,subtitleHost);pause.Initialize(Owner,input,settings);
            Owner.StateChanged+=State;Flow.Changed+=Refresh;settings.Changed+=Settings;initialized=true;Settings(settings.Current);Owner.OpenDossier();Refresh();
        }
        void Update(){if(initialized&&!focus.IsInitialized&&pause.Controller!=null)focus.Initialize(pause.Controller,Owner);}
        public void Begin()=>Owner.Begin(chapter);
        public void Restart()=>Owner.RestartChapter();
        void Return()=>Flow.ReturnFromButton();
        void State(AppState state)=>Refresh();
        void Settings(SettingsData options){Localization.ApplySettings(options);Flow.ReducedMotion=options.reducedMotion;Refresh();}
        static void Show(VisualElement element,bool show)=>element.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
        void Fit(){float width=Mathf.Min(slot.resolvedStyle.width,slot.resolvedStyle.height*4/3);if(width>0&&float.IsFinite(width)){frame.style.width=width;frame.style.height=width*.75f;}}
        public void Refresh()
        {
            if(image==null)return;
            bool dossier=Owner.State==AppState.Dossier||Owner.State==AppState.Boot;Show(cover,dossier);
            Show(image,Flow.ShowWorld||Flow.ShowArchive);image.image=Flow.ShowArchive?ending.HistoricalPhoto:viewport.Texture;
            Show(date,Flow.ShowWorld);date.text=Localization.Get(Flow.TimeSeconds<19?"sur.visit.landingDate":"sur.visit.visitDate");
            Show(credit,Flow.ShowArchive);credit.text=Localization.Get(ending.CreditKey);
            Show(memorial,Flow.ShowMemorial);title.text=Localization.Get("sur.visit.title");location.text=Localization.Get("sur.visit.location");
            coverTitle.text=Localization.Get("sur.visit.previewTitle");description.text=Localization.Get("sur.visit.description");begin.text=Localization.Get("sur.visit.begin");
            Show(returnButton,Flow.AwaitingReturn);returnButton.text=Localization.Get("sur.visit.return");returnButton.SetEnabled(Flow.CanClickReturn);
            bool notice=Flow.Running&&Flow.TimeSeconds<5;subtitleHost.style.top=notice?new StyleLength(0f):new StyleLength(StyleKeyword.Auto);subtitleHost.style.bottom=notice?0:22;subtitleHost.style.justifyContent=notice?Justify.Center:Justify.FlexEnd;Fit();
        }
        void ChapterDirector.ISceneLoader.Load(string name,Action done,Action<string> failed)
        {if(gameObject.scene.name==name&&gameObject.scene.isLoaded)done();else failed("Open SurveyorVisitPreview.");}
        void ChapterDirector.ISceneLoader.Unload(string name,Action done)=>done();
        void OnDisable()
        {
            if(!initialized)return;initialized=false;Owner.StateChanged-=State;Flow.Changed-=Refresh;settings.Changed-=Settings;
            begin.clicked-=Begin;returnButton.clicked-=Return;Flow.Dispose();Clock.StopActive();input.Disable();
        }
    }
}
