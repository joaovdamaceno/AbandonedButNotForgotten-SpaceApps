using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorDescentView:MonoBehaviour
    {
        VisualElement root,entry,flight,overlay,plate,diagramHost,preparation,radio,landing;
        Button start,pause,language,confirm,resume,restart,back,restartPaused,backPaused;
        readonly Dictionary<string,Label> labels=new Dictionary<string,Label>();
        SurveyorDescent flow;SurveyorCommandDelay queue;SurveyorCommandInteraction action;SurveyorLaunchTexts texts;
        Diagram diagram;string locale="en";HoldMode mode;bool reducedMotion;
        public Func<string,string> TextResolver{get;set;}
        public bool DrawLocalSubtitles{get;set;}=true;
        public event Action StartRequested,PauseRequested,ResumeRequested,RestartRequested,ReturnRequested,LanguageRequested;
        public VisualElement Root=>root;
        public Vector2 DiagramOffset{get;private set;}
        public void Configure(UIDocument document,TextAsset data,SurveyorDescent controller,SurveyorCommandDelay delay,SurveyorCommandInteraction interaction,SettingsData settings)
        {
            Unbind();root=document.rootVisualElement;flow=controller;queue=delay;action=interaction;texts=SurveyorLaunchTexts.Parse(data);
            labels.Clear();root.Query<Label>().ForEach(l=>{if(!string.IsNullOrEmpty(l.name))labels[l.name]=l;});
            entry=root.Q("entry");flight=root.Q("flight");overlay=root.Q("pause-overlay");plate=root.Q("subtitle-plate");landing=root.Q("landing-stamp");
            preparation=root.Q("preparation");radio=root.Q("radio");diagramHost=root.Q("diagram");
            start=root.Q<Button>("start");pause=root.Q<Button>("pause");language=root.Q<Button>("language");if(language!=null)language.style.display=DisplayStyle.None;confirm=root.Q<Button>("confirm");
            resume=root.Q<Button>("resume");restart=root.Q<Button>("restart");back=root.Q<Button>("return");restartPaused=root.Q<Button>("restart-paused");backPaused=root.Q<Button>("return-paused");
            start.clicked+=Start;pause.clicked+=Pause;language.clicked+=Language;resume.clicked+=Resume;restart.clicked+=Restart;restartPaused.clicked+=Restart;back.clicked+=Return;backPaused.clicked+=Return;
            confirm.RegisterCallback<PointerDownEvent>(OnDown);confirm.RegisterCallback<PointerUpEvent>(OnUp);confirm.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            diagramHost.Clear();diagram=new Diagram{pickingMode=PickingMode.Ignore};diagram.style.flexGrow=1;diagramHost.Add(diagram);
            flow.Changed+=Refresh;queue.Changed+=Refresh;action.Changed+=Refresh;ApplySettings(settings);
        }
        public void ApplySettings(SettingsData settings)
        {
            locale=settings.languageCode;mode=settings.holdMode;reducedMotion=settings.reducedMotion;if(root==null)return;
            string[] names={"archive","title","description","engines","diagram-label","date","location","pause-title","pause-description","help"};
            string[] keys={"descent.archive","descent.title","descent.description","descent.engines","descent.diagram","descent.date","descent.location","descent.paused","descent.pause.description","descent.help"};
            for(int i=0;i<names.Length;i++)Label(names[i],keys[i]);
            start.text=Text("descent.start");pause.text=Text("descent.pause");resume.text=Text("descent.resume");
            restart.text=restartPaused.text=Text("descent.restart");back.text=backPaused.text=Text("descent.return");
            confirm.text=Text(mode==HoldMode.Toggle?"descent.toggle":"descent.send");language.text=locale=="pt"?"EN":"PT";
            diagramHost.tooltip=Text("descent.context");Refresh();
        }
        string Text(string key)=>TextResolver!=null?TextResolver(key):texts.Get(key,locale);
        void Label(string name,string key)=>labels[name].text=Text(key);
        static void Show(VisualElement element,bool visible)=>element.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
        public void Refresh()
        {
            if(root==null)return;bool active=flow.Running||flow.ActComplete;Show(entry,!active);Show(flight,active);Show(pause,active);Show(overlay,flow.IsPaused);
            string phase=flow.Settled?"descent.phase.settled":flow.ContactCount>0?"descent.phase.contacts":flow.TimeSeconds>=flow.OfferAt-1?"descent.phase.anomaly":"descent.phase.approach";
            Label("phase",phase);
            string status=flow.IsPaused?"descent.paused":flow.Settled?"descent.status.settled":flow.EnginesCutOff?"descent.status.off":flow.CutoffReceived?"descent.status.armed":
                flow.CutoffSent?(flow.Origin==CutoffOrigin.GroundTeam?"descent.status.team.sent":"descent.status.player.sent"):
                action.IsHolding?"descent.status.holding":action.NeedsRelease?"descent.status.release":flow.TimeSeconds>=flow.OfferAt?"descent.status.ready":"descent.status.approach";
            Label("status",status);Label("contacts",flow.ContactCount==0?"descent.contacts.0":flow.ContactCount==1?"descent.contacts.1":flow.ContactCount==2?"descent.contacts.2":"descent.contacts.3");
            bool command=flow.Running && flow.TimeSeconds>=flow.OfferAt && !flow.CutoffSent;
            Show(confirm,command);Show(labels["help"],command);confirm.SetEnabled(!flow.IsPaused && (action.IsHolding||action.CanConfirm));
            preparation.style.width=Length.Percent(action.PreparationProgress*100);radio.style.width=Length.Percent(queue.Progress*100);
            Show(labels["transit"],queue.HasPending);Label("transit","descent.status.transit");Show(landing,flow.Settled);
            Label("camera",flow.CanActivateCamera?"descent.camera.ready":"descent.camera.locked");
            Show(restart,flow.ActComplete);Show(back,flow.ActComplete);
            string subtitle=DrawLocalSubtitles?Text(flow.SubtitleKey):string.Empty;labels["subtitle"].text=subtitle;Show(plate,!string.IsNullOrWhiteSpace(subtitle));
            float pulse=flow.ImpactPulse;
            DiagramOffset=reducedMotion?Vector2.zero:new Vector2(Mathf.Sin(flow.TimeSeconds*72),Mathf.Cos(flow.TimeSeconds*57))*pulse*3;
            diagramHost.style.translate=new Translate(DiagramOffset.x,DiagramOffset.y);diagram.Set(flow);
        }
        void OnDown(PointerDownEvent e){if(e.button==0 && action.BeginConfirmation())confirm.CapturePointer(e.pointerId);e.StopPropagation();}
        void OnUp(PointerUpEvent e){action.ReleaseConfirmation();if(confirm.HasPointerCapture(e.pointerId))confirm.ReleasePointer(e.pointerId);e.StopPropagation();}
        void OnCaptureOut(PointerCaptureOutEvent e)=>action.ReleaseConfirmation();
        void Start()=>StartRequested?.Invoke();void Pause()=>PauseRequested?.Invoke();void Resume()=>ResumeRequested?.Invoke();void Restart()=>RestartRequested?.Invoke();void Return()=>ReturnRequested?.Invoke();void Language()=>LanguageRequested?.Invoke();
        void OnDisable()=>Unbind();
        void Unbind()
        {
            if(flow!=null)flow.Changed-=Refresh;if(queue!=null)queue.Changed-=Refresh;if(action!=null)action.Changed-=Refresh;if(root==null)return;
            start.clicked-=Start;pause.clicked-=Pause;language.clicked-=Language;resume.clicked-=Resume;restart.clicked-=Restart;restartPaused.clicked-=Restart;back.clicked-=Return;backPaused.clicked-=Return;
            confirm.UnregisterCallback<PointerDownEvent>(OnDown);confirm.UnregisterCallback<PointerUpEvent>(OnUp);confirm.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);root=null;
        }
        sealed class Diagram:VisualElement
        {
            SurveyorDescent flow;
            static readonly Color Gold=new Color(.93f,.75f,.44f),Cool=new Color(.6f,.78f,.83f),Dim=new Color(.23f,.31f,.40f);
            public Diagram(){generateVisualContent+=Paint;}
            public void Set(SurveyorDescent value){flow=value;MarkDirtyRepaint();}
            static void Line(Painter2D p,Vector2 a,Vector2 b){p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();}
            void Paint(MeshGenerationContext context)
            {
                if(flow==null)return;float w=contentRect.width,h=contentRect.height;if(w<1 || h<1)return;var p=context.painter2D;
                float ground=h*.84f;p.strokeColor=Dim;p.lineWidth=1;
                for(int i=1;i<6;i++)Line(p,new Vector2(w*.06f,h*i/7),new Vector2(w*.96f,h*i/7));
                p.strokeColor=new Color(.4f,.47f,.53f);p.lineWidth=2;Line(p,new Vector2(w*.08f,ground),new Vector2(w*.92f,ground));
                float range=Mathf.Lerp(180,16,Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,12,flow.TimeSeconds)));
                float altitude=Mathf.Clamp01(flow.HeightMeters/range);var center=new Vector2(w*.42f,ground-38-altitude*h*.60f);
                p.strokeColor=flow.TimeSeconds>=flow.OfferAt&&!flow.EnginesCutOff?Gold:Cool;p.lineWidth=2;
                Vector2 At(float x,float y)=>center+new Vector2(x,y);
                Line(p,At(-25,-12),At(25,-12));Line(p,At(-25,-12),At(20,12));Line(p,At(25,-12),At(-20,12));Line(p,At(-20,12),At(20,12));
                Line(p,At(-20,8),At(-54,36));Line(p,At(20,8),At(54,36));Line(p,At(0,12),At(0,38));
                Line(p,At(-64,37),At(-44,37));Line(p,At(44,37),At(64,37));Line(p,At(-10,39),At(10,39));
                Line(p,At(-6,-12),At(-6,-66));Line(p,At(-13,-66),At(8,-66));Line(p,At(-6,-39),At(26,-54));Line(p,At(26,-54),At(46,-42));Line(p,At(46,-42),At(13,-26));Line(p,At(13,-26),At(-6,-39));
                if(!flow.EnginesCutOff && flow.Running)
                {p.strokeColor=new Color(.93f,.75f,.44f,.55f);for(int i=-1;i<=1;i++)Line(p,At(i*16,16),At(i*18,27+Mathf.Sin(flow.TimeSeconds*19+i)*3));}
                float x=w*.86f;p.strokeColor=Cool;p.lineWidth=2;Line(p,new Vector2(x,h*.08f),new Vector2(x,ground));
                for(int i=0;i<=8;i++)Line(p,new Vector2(x-14,h*.08f+(ground-h*.08f)*i/8),new Vector2(x,h*.08f+(ground-h*.08f)*i/8));
                float y=ground-altitude*h*.70f;p.strokeColor=Gold;Line(p,new Vector2(x-30,y),new Vector2(x+8,y));
                if(flow.ImpactPulse>0)
                {
                    p.strokeColor=new Color(.93f,.75f,.44f,flow.ImpactPulse);p.lineWidth=2+flow.ImpactPulse*2;
                    Line(p,new Vector2(w*.08f,ground+4),new Vector2(w*.92f,ground+4));
                }
            }
        }
    }
}
