using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Isolated executable composition. Production injects its Core services into SurveyorEclipse.Configure.</summary>
    public sealed class SurveyorEclipsePreview : MonoBehaviour, ChapterDirector.ISceneLoader
    {
        [SerializeField] ChapterDefinition chapter;
        [SerializeField] ChapterDirector owner;
        [SerializeField] SessionClock clock;
        [SerializeField] InputRouter input;
        [SerializeField] InteractionService service;
        [SerializeField] SurveyorCommandDelay queue;
        [SerializeField] SurveyorCommandInteraction action;
        [SerializeField] SequenceDirector sequence;
        [SerializeField] SurveyorEclipse flow;
        [SerializeField] SequenceDefinition track;
        [SerializeField] SurveyorSiteBindings site;
        [SerializeField] SurveyorEclipseImage skyImage;
        [SerializeField] Transform skyRig, groundRig;
        [SerializeField] SurveyorSiteViewport viewport;
        [SerializeField] SurveyorTelevisionOutput output;
        [SerializeField] Material scanMaterial;
        [SerializeField] UIDocument document;
        [SerializeField] TextAsset texts;
        [SerializeField] SubtitlePresenter subtitles;
        [SerializeField] AudioSource confirmation;
        readonly LocalizationService localization = new LocalizationService();
        SettingsStore settings; bool initialized;
        Image picture; Label title, objective, status, prompt, date, startTitle, startBody, endTitle, endBody, pauseTitle;
        VisualElement root, slot, frame, overlay, pause, ending, hud, subtitleHost;
        Button startButton, sendButton, languageButton, pauseButton, resumeButton, restartButton, returnButton, endRestart;
        public SurveyorEclipse Flow => flow;
        public ChapterDirector Owner => owner;
        public SessionClock Clock => clock;
        public InputRouter Input => input;
        public InteractionService Service => service;
        public SurveyorCommandDelay Queue => queue;
        public SurveyorCommandInteraction Action => action;
        public SequenceDirector Sequence => sequence;
        public SurveyorSiteBindings Site => site;
        public UIDocument Document => document;
        public SubtitlePresenter Subtitles => subtitles;
        public SettingsStore Settings => settings;
        public SurveyorSiteViewport Viewport => viewport;
        public SurveyorTelevisionOutput Output => output;
        public LocalizationService Localization => localization;

        public void Configure(ChapterDefinition definition, SequenceDefinition timeline, SurveyorSiteBindings bindings,
            SurveyorEclipseImage image, Transform sky, Transform ground, SurveyorSiteViewport source,
            Material scan, UIDocument ui, TextAsset catalog, AudioSource sound)
        {
            chapter=definition; track=timeline; site=bindings; skyImage=image; skyRig=sky; groundRig=ground;
            viewport=source; scanMaterial=scan; document=ui; texts=catalog; confirmation=sound;
            owner=GetComponent<ChapterDirector>(); clock=GetComponent<SessionClock>(); input=GetComponent<InputRouter>();
            service=GetComponent<InteractionService>(); queue=GetComponent<SurveyorCommandDelay>(); action=GetComponent<SurveyorCommandInteraction>();
            sequence=GetComponent<SequenceDirector>(); flow=GetComponent<SurveyorEclipse>(); output=GetComponent<SurveyorTelevisionOutput>(); subtitles=GetComponent<SubtitlePresenter>();
        }
        void Start() => Initialize();
        public void Initialize(SettingsStore storage=null)
        {
            if(initialized) return;
            settings=storage??new SettingsStore(); settings.Load();
            if(localization.Count==0 && localization.AddTable(texts.text,texts.name).Count>0) throw new ArgumentException("Eclipse localization invalid.");
            localization.ApplySettings(settings.Current);
            owner.Initialize(clock,input,this,new EvidenceStore()); input.Enable(); service.Initialize(input,clock);
            // All positions are above the known surface. The source ray must not hit the camera's own hardware collider.
            site.CameraDirector.GroundProbe = p => p.y-1;
            sequence.Initialize(site.CameraDirector,input,clock);
            flow.Configure(owner,clock,input,service,queue,action,sequence,track,site,skyImage,skyRig,groundRig);
            output.Configure(viewport,scanMaterial); if(!output.Begin()) throw new InvalidOperationException("Missing TV source."); output.SetProgress(1);
            BindUi(); subtitles.Initialize(localization,sequence,subtitleHost);
            flow.Changed+=Refresh; owner.StateChanged+=OnState; owner.PausedChanged+=OnPause; input.PauseDown+=TogglePause;
            settings.Changed+=ApplySettings; queue.Sent+=Sent; queue.Received+=Received;
            initialized=true; ApplySettings(settings.Current); owner.OpenDossier(); Refresh();
        }
        void BindUi()
        {
            root=document.rootVisualElement; picture=root.Q<Image>("eclipse-picture"); slot=root.Q("eclipse-slot"); frame=root.Q("eclipse-frame");
            hud=root.Q("eclipse-hud"); subtitleHost=root.Q("eclipse-subtitles"); overlay=root.Q("eclipse-start"); pause=root.Q("eclipse-pause"); ending=root.Q("eclipse-end");
            title=root.Q<Label>("eclipse-title"); date=root.Q<Label>("eclipse-date"); objective=root.Q<Label>("eclipse-objective");
            status=root.Q<Label>("eclipse-status"); prompt=root.Q<Label>("eclipse-prompt");
            startTitle=root.Q<Label>("eclipse-start-title"); startBody=root.Q<Label>("eclipse-start-body");
            pauseTitle=root.Q<Label>("eclipse-pause-title"); endTitle=root.Q<Label>("eclipse-end-title"); endBody=root.Q<Label>("eclipse-end-body");
            startButton=root.Q<Button>("eclipse-begin"); sendButton=root.Q<Button>("eclipse-send"); pauseButton=root.Q<Button>("eclipse-pause-button");
            languageButton=root.Q<Button>("eclipse-language"); resumeButton=root.Q<Button>("eclipse-resume"); restartButton=root.Q<Button>("eclipse-restart");
            returnButton=root.Q<Button>("eclipse-return"); endRestart=root.Q<Button>("eclipse-end-restart");
            startButton.clicked+=Begin; pauseButton.clicked+=Pause; resumeButton.clicked+=Resume; restartButton.clicked+=Restart; returnButton.clicked+=Return; endRestart.clicked+=Restart; languageButton.clicked+=Language;
            sendButton.RegisterCallback<PointerDownEvent>(e=> { if(e.button==0) { flow.BeginConfirmation(); sendButton.CapturePointer(e.pointerId); } });
            sendButton.RegisterCallback<PointerUpEvent>(e=> { flow.ReleaseConfirmation(); sendButton.ReleasePointer(e.pointerId); });
            sendButton.RegisterCallback<PointerCaptureOutEvent>(e=>flow.ReleaseConfirmation());
            slot.RegisterCallback<GeometryChangedEvent>(e=>LayoutImage());
        }
        void LayoutImage()
        {
            if(slot==null) return;
            float width=Mathf.Min(slot.resolvedStyle.width,slot.resolvedStyle.height*4/3);
            if(float.IsFinite(width) && width>0) { frame.style.width=width; frame.style.height=width*.75f; }
        }
        public void Begin() => owner.Begin(chapter);
        public void Restart() => owner.RestartChapter();
        public void Return() => owner.ReturnToDossier();
        public void Pause() => owner.Pause("eclipse.menu");
        public void Resume() { owner.Resume("eclipse.menu"); owner.Resume("eclipse.focus"); owner.Resume("eclipse.application"); }
        void TogglePause() { if(owner.IsPaused) Resume(); else Pause(); }
        void Language() => settings.Apply(s=>s.languageCode=s.languageCode=="pt"?"en":"pt");
        public void ApplySettings(SettingsData options)
        {
            localization.ApplySettings(options); service.Mode=options.holdMode; flow.ReducedMotion=options.reducedMotion;
            root.style.fontSize=19*options.textScale;
            confirmation.volume=options.masterVolume*options.uiVolume*.28f; Refresh();
        }
        void Sent(SurveyorCommandReceipt receipt) { confirmation.pitch=1.25f; confirmation.Play(); }
        void Received(SurveyorCommandReceipt receipt) { confirmation.pitch=.85f; confirmation.Play(); }
        void OnState(AppState state) => Refresh();
        void OnPause(bool paused) => Refresh();
        static void Show(VisualElement element,bool show) => element.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
        string Text(string key) => localization.Get(key);
        public void Refresh()
        {
            if(root==null) return;
            bool playing=owner.State==AppState.Playing;
            bool complete=owner.CurrentStepId==SurveyorEclipse.NextStepId && playing;
            Show(overlay,!playing); Show(pause,owner.IsPaused); Show(ending,complete && !owner.IsPaused);
            bool quiet=flow.Phase==EclipsePhase.Viewing && flow.SequenceTime>=14 && flow.SequenceTime<22;
            hud.style.opacity=quiet?0:1;
            title.text=Text("sur.eclipse.title"); date.text=Text("sur.eclipse.date");
            Show(date,playing && flow.Running && (flow.Phase!=EclipsePhase.Viewing || flow.SequenceTime<2));
            objective.text=Text("sur.eclipse.objective");
            string statusKey=flow.Phase==EclipsePhase.InTransit?"sur.eclipse.transit":flow.Phase==EclipsePhase.Viewing?"sur.eclipse.color":"sur.eclipse.ready";
            status.text=Text(statusKey);
            Show(status,flow.Running && !quiet);
            prompt.text=Text(flow.NeedsRelease?"sur.eclipse.release":"sur.eclipse.hold");
            Show(sendButton,playing && flow.Phase==EclipsePhase.AwaitingOrder);
            sendButton.SetEnabled(flow.CanConfirm || action.IsHolding); sendButton.text=Text("sur.eclipse.send");
            Show(prompt,playing && flow.Phase==EclipsePhase.AwaitingOrder);
            Show(objective,playing && (flow.Phase==EclipsePhase.AwaitingOrder || flow.Phase==EclipsePhase.InTransit));
            languageButton.text=localization.Language=="pt"?"EN":"PT"; pauseButton.text=Text("sur.eclipse.pause");
            startTitle.text=Text("sur.eclipse.title"); startBody.text=Text("sur.eclipse.description"); startButton.text=Text("sur.eclipse.begin");
            pauseTitle.text=Text("sur.eclipse.paused"); resumeButton.text=Text("sur.eclipse.resume"); restartButton.text=Text("sur.eclipse.restart"); returnButton.text=Text("sur.eclipse.return");
            endTitle.text=Text("sur.eclipse.complete"); endBody.text=Text("sur.eclipse.handoff"); endRestart.text=Text("sur.eclipse.restart");
            bool color=flow.Phase==EclipsePhase.Viewing && flow.SequenceTime>=4 && flow.SequenceTime<22;
            picture.image=color ? viewport.Texture : output.Texture;
            float crop=flow.Enlargement, size=Mathf.Lerp(1,.38f,crop);
            picture.uv=new Rect((1-size)*.5f,Mathf.Lerp(0,.59f,crop),size,size);
            LayoutImage();
        }
        void OnApplicationFocus(bool focused) { if(initialized && !focused) owner.Pause("eclipse.focus"); }
        void OnApplicationPause(bool paused) { if(initialized && paused) owner.Pause("eclipse.application"); }
        void ChapterDirector.ISceneLoader.Load(string name,Action loaded,Action<string> failed)
        { if(gameObject.scene.name==name && gameObject.scene.isLoaded) loaded(); else failed("Open SurveyorEclipsePreview."); }
        void ChapterDirector.ISceneLoader.Unload(string name,Action unloaded) => unloaded();
        public void Dispose()
        {
            if(!initialized) return; initialized=false;
            flow.Changed-=Refresh; owner.StateChanged-=OnState; owner.PausedChanged-=OnPause; input.PauseDown-=TogglePause; settings.Changed-=ApplySettings;
            queue.Sent-=Sent; queue.Received-=Received;
            startButton.clicked-=Begin; pauseButton.clicked-=Pause; resumeButton.clicked-=Resume; restartButton.clicked-=Restart;
            returnButton.clicked-=Return; endRestart.clicked-=Restart; languageButton.clicked-=Language;
            flow.Dispose(); subtitles.Clear(); output.Dispose(); input.Disable(); clock.StopActive();
        }
        void OnDisable() => Dispose();
    }
}
