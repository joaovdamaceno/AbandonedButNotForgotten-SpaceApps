using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Apresentação exclusiva da telemetria. Não lê teclado nem controla etapas.</summary>
    public sealed class LaunchTelemetryView : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] TextAsset textData;
        SurveyorLaunchTexts texts;
        SurveyorLaunchController flow;
        VisualElement root,entry,flight,plate,overlay,ringElement;
        Button confirm,restart,back,languageButton,muteButton,pauseButton;
        Button startButton,resumeButton,pauseRestart,pauseReturn;
        readonly Dictionary<string,Label> labels=new Dictionary<string,Label>();
        LaunchDiagram map,entryMap,ring;
        string language=SettingsData.Portuguese;
        HoldMode mode;
        bool muted,reducedMotion;
        public Func<string,string> TextResolver { get; set; }
        public bool DrawLocalSubtitles { get; set; }=true;
        public event Action StartRequested,PauseRequested,ResumeRequested,RestartRequested,ReturnRequested,LanguageRequested,MuteRequested;
        public VisualElement Root=>root;

        static readonly string[] Bindings={"archive-mark|launch.archive","entry-kicker|launch.kicker","entry-title|launch.title",
            "entry-description|launch.description","entry-date|launch.date","entry-controls|launch.controls","mission-name|launch.mission",
            "mission-date|launch.date","earth-label|launch.earth","map-label|launch.diagram","moon-label|launch.moon",
            "command-kicker|launch.order","command-title|launch.centaur","pause-title|launch.paused","pause-description|launch.pause.description"};

        public void Configure(UIDocument ui,TextAsset data,SurveyorLaunchController controller,SettingsData settings)
        {
            Unbind();document=ui;textData=data;flow=controller;texts=SurveyorLaunchTexts.Parse(data);
            root=document.rootVisualElement;labels.Clear();root.Query<Label>().ForEach(l=>{if(!string.IsNullOrEmpty(l.name))labels[l.name]=l;});
            entry=root.Q("entry");flight=root.Q("flight");plate=root.Q("subtitle-plate");overlay=root.Q("pause-overlay");
            ringElement=root.Q("hold-ring");
            confirm=root.Q<Button>("confirm-button");restart=root.Q<Button>("restart-button");back=root.Q<Button>("return-button");
            languageButton=root.Q<Button>("language-button");muteButton=root.Q<Button>("mute-button");pauseButton=root.Q<Button>("pause-button");
            startButton=root.Q<Button>("start-button");resumeButton=root.Q<Button>("resume-button");
            pauseRestart=root.Q<Button>("pause-restart-button");pauseReturn=root.Q<Button>("pause-return-button");
            startButton.clicked+=Start;resumeButton.clicked+=Resume;pauseRestart.clicked+=Restart;pauseReturn.clicked+=Return;
            pauseButton.clicked+=Pause;restart.clicked+=Restart;back.clicked+=Return;
            languageButton.clicked+=Language;muteButton.clicked+=Mute;
            confirm.RegisterCallback<PointerDownEvent>(OnPointerDown);
            confirm.RegisterCallback<PointerUpEvent>(OnPointerUp);
            confirm.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            map=Attach(root.Q("trajectory"),false);entryMap=Attach(root.Q("entry-graphic"),false);ring=Attach(ringElement,true);
            flow.Changed+=Refresh;ApplySettings(settings);Refresh();
        }

        static LaunchDiagram Attach(VisualElement parent,bool progress)
        {
            var old=parent.Q<LaunchDiagram>();if(old!=null)old.RemoveFromHierarchy();
            var g=new LaunchDiagram(progress){pickingMode=PickingMode.Ignore};
            g.style.position=Position.Absolute;g.style.left=g.style.right=g.style.top=g.style.bottom=0;
            parent.Insert(0,g);return g;
        }

        public void ApplySettings(SettingsData settings)
        {
            language=settings.languageCode;mode=settings.holdMode;muted=settings.masterVolume<=0;reducedMotion=settings.reducedMotion;
            if(root!=null)root.style.fontSize=20f*settings.textScale;
            if(root!=null)Relocalize();
            Refresh();
        }

        void Relocalize()
        {
            foreach(var binding in Bindings){var pieces=binding.Split('|');Label(pieces[0],pieces[1]);}
            startButton.text=Text("launch.start");resumeButton.text=Text("launch.resume");
            pauseRestart.text=Text("launch.restart");pauseReturn.text=Text("launch.return");
            restart.text=Text("launch.restart");back.text=Text("launch.return");pauseButton.text=Text("launch.pause");
            languageButton.text=language==SettingsData.Portuguese?"EN":"PT";
            muteButton.text=Text(muted?"launch.unmute":"launch.mute");
            confirm.text=Text(mode==HoldMode.Toggle?"launch.toggle":"launch.hold");
            labels["command-title"].tooltip=Text("launch.explanation");
            Label("command-help",mode==HoldMode.Toggle?"launch.hint.toggle":"launch.hint");
        }

        string Text(string key)=>TextResolver!=null?TextResolver(key):texts.Get(key,language);
        void Label(string name,string key){if(labels.TryGetValue(name,out var label))label.text=Text(key);}
        static void Display(VisualElement element,bool visible){if(element!=null)element.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;}

        public void Refresh()
        {
            if(root==null || flow==null || texts==null)return;
            bool active=flow.Running || flow.ActComplete;
            Display(entry,!active);Display(flight,active);Display(overlay,flow.IsPaused);
            Display(pauseButton,active);Display(restart,flow.ActComplete);Display(back,flow.ActComplete);
            Label("phase",flow.Executed || flow.ActComplete?"launch.cruise.phase":flow.TimeSeconds<flow.AscentAt?"launch.purpose.phase":flow.TimeSeconds<flow.ReadyAt?"launch.ascent.phase":"launch.orbit.phase");
            string status=flow.IsPaused?"launch.paused":flow.ActComplete||flow.Executed?"launch.received":flow.Sent?"launch.sent":flow.Prepared?"launch.prepared":
                flow.IsHolding?"launch.holding":flow.NeedsRelease?"launch.release":flow.CommandAvailable?"launch.ready":"launch.wait";
            Label("command-status",status);
            confirm.SetEnabled(flow.CommandAvailable);
            Display(confirm,!flow.Executed);Display(ringElement,!flow.Executed);Display(labels["command-help"],!flow.Executed);
            string subtitle=DrawLocalSubtitles?Text(flow.SubtitleKey):string.Empty;
            labels["subtitle"].text=subtitle;Display(plate,!string.IsNullOrWhiteSpace(subtitle));
            map.Set(flow,flow.TimeSeconds,flow.Sent,flow.Progress,false,reducedMotion);
            entryMap.Set(flow,flow.Definition.TotalDurationSeconds,true,0,false,true);
            ring.Set(flow,0,false,flow.Progress,flow.Prepared,reducedMotion);
        }

        void OnPointerDown(PointerDownEvent e){if(e.button!=0)return;if(flow.BeginConfirmation())confirm.CapturePointer(e.pointerId);e.StopPropagation();}
        void OnPointerUp(PointerUpEvent e){flow.ReleaseConfirmation();if(confirm.HasPointerCapture(e.pointerId))confirm.ReleasePointer(e.pointerId);e.StopPropagation();}
        void OnCaptureOut(PointerCaptureOutEvent e)=>flow.ReleaseConfirmation();
        void Start()=>StartRequested?.Invoke();void Pause()=>PauseRequested?.Invoke();void Resume()=>ResumeRequested?.Invoke();
        void Restart()=>RestartRequested?.Invoke();void Return()=>ReturnRequested?.Invoke();void Language()=>LanguageRequested?.Invoke();void Mute()=>MuteRequested?.Invoke();
        void OnDisable()=>Unbind();
        void Unbind()
        {
            if(flow!=null)flow.Changed-=Refresh;
            if(root==null)return;
            root.Q<Button>("start-button").clicked-=Start;root.Q<Button>("resume-button").clicked-=Resume;
            root.Q<Button>("pause-restart-button").clicked-=Restart;root.Q<Button>("pause-return-button").clicked-=Return;
            if(pauseButton!=null)pauseButton.clicked-=Pause;if(restart!=null)restart.clicked-=Restart;if(back!=null)back.clicked-=Return;
            if(languageButton!=null)languageButton.clicked-=Language;if(muteButton!=null)muteButton.clicked-=Mute;
            if(confirm!=null){confirm.UnregisterCallback<PointerDownEvent>(OnPointerDown);confirm.UnregisterCallback<PointerUpEvent>(OnPointerUp);confirm.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);}
            root=null;
        }

        sealed class LaunchDiagram : VisualElement
        {
            readonly bool progressRing;
            float time,progress,ascentAt,readyAt,sendAt,executeAt,duration;bool sent,done,reduced;
            static readonly Color Accent=new Color(.93f,.75f,.44f),Dim=new Color(.35f,.42f,.5f,.5f);
            public LaunchDiagram(bool ring){progressRing=ring;generateVisualContent+=Paint;}
            public void Set(SurveyorLaunchController flow,float t,bool signal,float p,bool complete,bool reduce)
            {time=t;sent=signal;progress=p;done=complete;reduced=reduce;ascentAt=flow.AscentAt;readyAt=flow.ReadyAt;
                sendAt=flow.SendAt;executeAt=flow.ExecuteAt;duration=flow.Definition.TotalDurationSeconds;MarkDirtyRepaint();}
            void Line(Painter2D painter,Vector2 a,Vector2 b){painter.BeginPath();painter.MoveTo(a);painter.LineTo(b);painter.Stroke();}
            void Circle(Painter2D painter,Vector2 center,float radius,float fraction=1)
            {
                painter.BeginPath();
                for(var i=0;i<=64;i++)
                {float a=(-90f+360f*fraction*i/64)*Mathf.Deg2Rad;var p=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;if(i==0)painter.MoveTo(p);else painter.LineTo(p);}
                painter.Stroke();
            }
            void Paint(MeshGenerationContext context)
            {
                var p=context.painter2D;float w=contentRect.width,h=contentRect.height;if(w<1 || h<1)return;
                if(progressRing)
                {
                    var center=new Vector2(w*.5f,h*.5f);float radius=Mathf.Min(w,h)*.43f;
                    p.lineWidth=3;p.strokeColor=Dim;Circle(p,center,radius);
                    p.lineWidth=5;p.strokeColor=done?new Color(.56f,.85f,.60f):Accent;Circle(p,center,radius,progress);return;
                }
                p.lineWidth=1;p.strokeColor=new Color(.18f,.24f,.32f,.4f);
                for(var i=1;i<5;i++)Line(p,new Vector2(w*.05f,h*i/5),new Vector2(w*.94f,h*i/5));
                var earth=new Vector2(w*.20f,h*.70f);var moon=new Vector2(w*.82f,h*.22f);
                float transition=Mathf.Clamp01((time-sendAt)/(duration-sendAt));
                p.strokeColor=Dim;Circle(p,earth,Mathf.Lerp(32,21,transition));Circle(p,earth,49);Circle(p,moon,16);
                var control=new Vector2(w*.42f,h*.09f);
                Vector2 Path(float u)=>(1-u)*(1-u)*earth+2*(1-u)*u*control+u*u*moon;
                p.strokeColor=Dim;
                for(var i=0;i<24;i+=2)Line(p,Path(i/24f),Path((i+1)/24f));
                p.lineWidth=2;p.strokeColor=Accent;
                if(time<readyAt)
                {
                    float ascent=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-ascentAt)/(readyAt-ascentAt)));
                    var point=earth+new Vector2(0,Mathf.Lerp(62,-49,ascent));
                    Circle(p,point,4);Line(p,point+new Vector2(-10,0),point+new Vector2(10,0));
                }
                else
                {
                    float reach=sent?Mathf.Clamp01((time-executeAt)/(duration-executeAt)):0;
                    p.BeginPath();p.MoveTo(earth);
                    for(var i=1;i<=32;i++)p.LineTo(Path(reach*i/32));p.Stroke();Circle(p,Path(reach),5);
                    if(!reduced && sent && time<executeAt){p.strokeColor=new Color(.93f,.75f,.44f,.5f);Circle(p,earth,52+(time-sendAt)*28);}
                }
            }
        }
    }
}
