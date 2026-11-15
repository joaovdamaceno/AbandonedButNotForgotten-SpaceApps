using System;
using Abandoned.SpaceApps.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Minimal isolated composition. Uses the existing TV, subtitles, preferences and pause components.</summary>
    public sealed class SurveyorNightPreview : MonoBehaviour, ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDefinition chapter;
        [SerializeField] SequenceDefinition night, passage;
        [SerializeField] CameraDirector cameras;
        [SerializeField] Transform optics;
        [SerializeField] Light sun;
        [SerializeField] SurveyorSiteViewport viewport;
        [SerializeField] Material scan;
        [SerializeField] UIDocument document;
        [SerializeField] TextAsset texts;
        [SerializeField] PauseMenu pause;
        public ChapterDirector Owner { get; private set; }
        public SessionClock Clock { get; private set; }
        public SurveyorNight Flow { get; private set; }
        public SequenceDirector Sequence { get; private set; }
        public SubtitlePresenter Subtitles { get; private set; }
        public LocalizationService Localization { get; }=new LocalizationService();
        public UIDocument Document=>document;
        public SurveyorSiteViewport Viewport=>viewport;
        public PauseMenu Pause=>pause;
        public SurveyorTelevisionOutput Output { get; private set; }
        SettingsStore settings; InputRouter input; bool initialized;
        Image picture; VisualElement cover, hud, calendar, slot, frame;
        Label stamp, months, temperature, contact, count, title, description;
        Button begin, replay; FocusPause focus;
        public void Configure(ChapterDefinition data,SequenceDefinition first,SequenceDefinition second,CameraDirector camera,
            Transform rig,Light keyLight,SurveyorSiteViewport source,Material televisionScan,UIDocument ui,TextAsset catalog,PauseMenu pauseMenu)
        { chapter=data;night=first;passage=second;cameras=camera;optics=rig;sun=keyLight;viewport=source;scan=televisionScan;document=ui;texts=catalog;pause=pauseMenu; }
        void Start()=>Initialize();
        public void Initialize()
        {
            if(initialized) return;
            Owner=GetComponent<ChapterDirector>();Clock=GetComponent<SessionClock>();input=GetComponent<InputRouter>();
            Flow=GetComponent<SurveyorNight>();Sequence=GetComponent<SequenceDirector>();Subtitles=GetComponent<SubtitlePresenter>();Output=GetComponent<SurveyorTelevisionOutput>();
            settings=new SettingsStore();settings.Load();Localization.AddTable(texts.text,texts.name);Localization.ApplySettings(settings.Current);
            Owner.Initialize(Clock,input,this,new EvidenceStore());input.Enable();cameras.GroundProbe=p=>p.y-1;
            Flow.Configure(Owner,Clock,input,Sequence,cameras,night,passage,sun,optics);
            Output.Configure(viewport,scan);if(!Output.Begin())throw new InvalidOperationException("TV source missing.");Output.SetProgress(1);
            var root=document.rootVisualElement;picture=root.Q<Image>("picture");cover=root.Q("cover");hud=root.Q("hud");calendar=root.Q("calendar");
            slot=root.Q("slot");frame=root.Q("frame");stamp=root.Q<Label>("stamp");months=root.Q<Label>("months");temperature=root.Q<Label>("temperature");
            contact=root.Q<Label>("contact");count=root.Q<Label>("count");title=root.Q<Label>("title");description=root.Q<Label>("description");
            begin=root.Q<Button>("begin");replay=root.Q<Button>("replay");begin.clicked+=Begin;replay.clicked+=Restart;
            slot.RegisterCallback<GeometryChangedEvent>(e=>Fit());
            Subtitles.Initialize(Localization,Sequence,root.Q("subtitles"));
            pause.Initialize(Owner,input,settings);focus=GetComponent<FocusPause>();
            Flow.Changed+=Refresh;Owner.StateChanged+=State;settings.Changed+=Settings;
            initialized=true;Settings(settings.Current);Owner.OpenDossier();Refresh();
        }
        void Update() { if(initialized && !focus.IsInitialized && pause.Controller!=null) focus.Initialize(pause.Controller,Owner); }
        public void Begin()=>Owner.Begin(chapter);
        public void Restart()=>Owner.RestartChapter();
        void State(AppState state)=>Refresh();
        void Settings(SettingsData options) { Localization.ApplySettings(options);Flow.ReducedMotion=options.reducedMotion;Refresh(); }
        string Text(string key)=>Localization.Get(key);
        static void Show(VisualElement element,bool show)=>element.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
        void Fit()
        {
            float width=Mathf.Min(slot.resolvedStyle.width,slot.resolvedStyle.height*4/3);
            if(width>0 && float.IsFinite(width)) { frame.style.width=width;frame.style.height=width*.75f; }
        }
        public void Refresh()
        {
            if(picture==null) return;
            bool playing=Owner.State==AppState.Playing;
            picture.image=Output.Texture;Show(cover,!playing);Show(replay,playing && Owner.CurrentStepId==SurveyorNight.NextStep);
            Show(hud,Flow.HudOpacity>0);hud.style.opacity=Flow.HudOpacity;
            bool lastContact=Flow.TrackSeconds>=5;
            temperature.text=Text(lastContact?"sur.night.cooling":"sur.night.daylight");
            contact.text=Text("sur.night.contact");count.text=Text("sur.night.total");Show(contact,lastContact);Show(count,lastContact);
            Show(calendar,Flow.IsPassage);stamp.text=Flow.CalendarKey==null?string.Empty:Text(Flow.CalendarKey);
            months.text=Text(Flow.TrackSeconds>=8?"sur.night.months":"sur.night.reconstruction");
            title.text=Text("sur.night.title");description.text=Text("sur.night.description");begin.text=Text("sur.night.begin");replay.text=Text("sur.night.replay");Fit();
        }
        void ChapterDirector.ISceneLoader.Load(string name,Action done,Action<string> failed)
        { if(gameObject.scene.name==name && gameObject.scene.isLoaded) done();else failed("Open SurveyorNightPreview."); }
        void ChapterDirector.ISceneLoader.Unload(string name,Action done)=>done();
        void OnDisable()
        {
            if(!initialized)return;initialized=false;Flow.Changed-=Refresh;Owner.StateChanged-=State;settings.Changed-=Settings;
            begin.clicked-=Begin;replay.clicked-=Restart;Flow.Dispose();Output.Dispose();Clock.StopActive();input.Disable();
        }
    }
}
