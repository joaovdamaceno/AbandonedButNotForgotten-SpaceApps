using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorMidcourseView : MonoBehaviour
    {
        SurveyorMidcourse flow;
        SurveyorCommandDelay queue;
        SurveyorCommandInteraction action;
        SurveyorLaunchTexts texts;
        VisualElement root,entry,flight,overlay,plate,mapHost,preparation,radio;
        FlightMap map;
        Button confirm,start,pause,language,resume,restart,back,restartPaused,backPaused,assist;
        readonly Dictionary<string,Label> labels=new Dictionary<string,Label>();
        string locale="en";
        HoldMode mode;
        public Func<string,string> TextResolver { get; set; }
        public bool DrawLocalSubtitles { get; set; }=true;
        public event Action StartRequested,PauseRequested,ResumeRequested,RestartRequested,ReturnRequested,LanguageRequested;
        public VisualElement Root=>root;

        public void Configure(UIDocument document,TextAsset data,SurveyorMidcourse controller,SurveyorCommandDelay delay,
            SurveyorCommandInteraction interaction,SettingsData settings)
        {
            Unbind(); flow=controller;queue=delay;action=interaction;texts=SurveyorLaunchTexts.Parse(data);root=document.rootVisualElement;
            labels.Clear();root.Query<Label>().ForEach(label=>{if(!string.IsNullOrEmpty(label.name))labels[label.name]=label;});
            entry=root.Q("entry");flight=root.Q("flight");overlay=root.Q("pause-overlay");plate=root.Q("subtitle-plate");
            mapHost=root.Q("flight-map");preparation=root.Q("preparation");radio=root.Q("radio");
            confirm=root.Q<Button>("confirm");start=root.Q<Button>("start");pause=root.Q<Button>("pause");language=root.Q<Button>("language");if(language!=null)language.style.display=DisplayStyle.None;
            resume=root.Q<Button>("resume");restart=root.Q<Button>("restart");back=root.Q<Button>("return");assist=root.Q<Button>("assist");
            restartPaused=root.Q<Button>("restart-paused");backPaused=root.Q<Button>("return-paused");
            start.clicked+=Start;pause.clicked+=Pause;language.clicked+=Language;resume.clicked+=Resume;
            restart.clicked+=Restart;restartPaused.clicked+=Restart;back.clicked+=Return;backPaused.clicked+=Return;assist.clicked+=flow.Assist;
            confirm.RegisterCallback<PointerDownEvent>(ConfirmDown);confirm.RegisterCallback<PointerUpEvent>(ConfirmUp);
            confirm.RegisterCallback<PointerCaptureOutEvent>(ConfirmOut);
            mapHost.RegisterCallback<GeometryChangedEvent>(ResizeMap);mapHost.RegisterCallback<PointerDownEvent>(AimDown);
            mapHost.RegisterCallback<PointerMoveEvent>(AimMove);mapHost.RegisterCallback<PointerUpEvent>(AimUp);
            mapHost.Clear();map=new FlightMap{pickingMode=PickingMode.Ignore};map.style.width=Length.Percent(100);map.style.height=Length.Percent(100);mapHost.Add(map);
            flow.Changed+=Refresh;queue.Changed+=Refresh;action.Changed+=Refresh;ApplySettings(settings);
        }
        public void ApplySettings(SettingsData settings)
        {
            locale=settings.languageCode;mode=settings.holdMode;if(root==null)return;
            string[] names={"archive","title","description","journey","destination-kicker","destination","latin","earth","moon","diagram","keys","pause-title","pause-description"};
            string[] keys={"mid.archive","mid.title","mid.description","mid.flight","mid.destination.kicker","mid.destination","mid.destination.latin","mid.earth","mid.moon","mid.diagram","mid.help.keys","mid.paused","mid.pause.description"};
            for(int i=0;i<names.Length;i++)Label(names[i],keys[i]);
            start.text=Text("mid.start");pause.text=Text("mid.pause");resume.text=Text("mid.resume");
            restart.text=restartPaused.text=Text("mid.restart");back.text=backPaused.text=Text("mid.return");assist.text=Text("mid.help.assist");
            confirm.text=Text(mode==HoldMode.Toggle?"mid.toggle":"mid.send");language.text=locale=="pt"?"EN":"PT";
            mapHost.tooltip=Text("mid.qualitative");Refresh();
        }
        string Text(string key)=>TextResolver!=null?TextResolver(key):texts.Get(key,locale);
        void Label(string name,string key)=>labels[name].text=Text(key);
        static void Show(VisualElement element,bool visible)=>element.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
        public void Refresh()
        {
            if(root==null)return;
            bool active=flow.Running||flow.ActComplete;Show(entry,!active);Show(flight,active);Show(pause,active);Show(overlay,flow.IsPaused);
            string phase=flow.ActComplete?"mid.phase.complete":flow.Phase==MidcoursePhase.Travel?"mid.phase.travel":
                flow.Phase==MidcoursePhase.Guidance?"mid.phase.guidance":flow.Phase==MidcoursePhase.Burn?"mid.phase.burn":"mid.phase.handoff";
            Label("phase",phase);Label("date",flow.Phase==MidcoursePhase.Travel?
                (flow.TravelProgress<.35f?"mid.day17":flow.TravelProgress<.7f?"mid.day18":"mid.day19"):"mid.range");
            Label("journey",flow.Phase==MidcoursePhase.Travel?"mid.flight":"mid.diagram");
            string status=flow.IsPaused?"mid.paused":flow.ActComplete||flow.Phase==MidcoursePhase.Handoff?"mid.status.handoff":
                flow.Phase==MidcoursePhase.Travel?"mid.status.travel":flow.Phase==MidcoursePhase.Burn?"mid.status.burn":
                queue.HasPending?"mid.status.sent":action.IsHolding?"mid.status.holding":action.NeedsRelease?"mid.status.release":
                !flow.Aligned?"mid.status.aim":action.CanConfirm?"mid.status.ready":"mid.status.read";
            Label("status",status);Label("help",flow.HelpLevel>0?"mid.help.emphasis":"mid.help.release");
            labels["help"].EnableInClassList("mid-help-emphasis",flow.HelpLevel>0);
            bool guidance=flow.Phase==MidcoursePhase.Guidance;
            Show(labels["keys"],guidance);Show(labels["help"],guidance);
            Show(confirm,guidance);confirm.SetEnabled(!flow.IsPaused && (action.IsHolding||action.CanConfirm));
            Show(assist,guidance&&flow.HelpLevel>=2);assist.SetEnabled(flow.CanAim);
            Show(restart,flow.ActComplete);Show(back,flow.ActComplete);
            preparation.style.width=Length.Percent(100*(flow.Phase==MidcoursePhase.Burn?flow.BurnProgress:action.PreparationProgress));
            radio.style.width=Length.Percent(queue.Progress*100);
            string subtitle=DrawLocalSubtitles?Text(flow.SubtitleKey):string.Empty;labels["subtitle"].text=subtitle;Show(plate,!string.IsNullOrWhiteSpace(subtitle));
            map.Set(flow);
        }
        void ResizeMap(GeometryChangedEvent e){if(e.newRect.width>0)mapHost.style.height=e.newRect.width*SurveyorMidcourse.MapAspect;}
        void AimAt(Vector2 local)
        {float width=mapHost.resolvedStyle.width;if(width>0)flow.SetDesired(local/width);}
        void AimDown(PointerDownEvent e){if(e.button!=0 || !flow.CanAim)return;AimAt(e.localPosition);mapHost.CapturePointer(e.pointerId);e.StopPropagation();}
        void AimMove(PointerMoveEvent e){if(mapHost.HasPointerCapture(e.pointerId))AimAt(e.localPosition);}
        void AimUp(PointerUpEvent e){if(mapHost.HasPointerCapture(e.pointerId))mapHost.ReleasePointer(e.pointerId);}
        void ConfirmDown(PointerDownEvent e){if(e.button==0 && action.BeginConfirmation())confirm.CapturePointer(e.pointerId);e.StopPropagation();}
        void ConfirmUp(PointerUpEvent e){action.ReleaseConfirmation();if(confirm.HasPointerCapture(e.pointerId))confirm.ReleasePointer(e.pointerId);e.StopPropagation();}
        void ConfirmOut(PointerCaptureOutEvent e)=>action.ReleaseConfirmation();
        void Start()=>StartRequested?.Invoke();void Pause()=>PauseRequested?.Invoke();void Resume()=>ResumeRequested?.Invoke();
        void Restart()=>RestartRequested?.Invoke();void Return()=>ReturnRequested?.Invoke();void Language()=>LanguageRequested?.Invoke();
        void OnDisable()=>Unbind();
        void Unbind()
        {
            if(flow!=null)flow.Changed-=Refresh;if(queue!=null)queue.Changed-=Refresh;if(action!=null)action.Changed-=Refresh;
            if(root==null)return;
            start.clicked-=Start;pause.clicked-=Pause;language.clicked-=Language;resume.clicked-=Resume;
            restart.clicked-=Restart;restartPaused.clicked-=Restart;back.clicked-=Return;backPaused.clicked-=Return;assist.clicked-=flow.Assist;
            confirm.UnregisterCallback<PointerDownEvent>(ConfirmDown);confirm.UnregisterCallback<PointerUpEvent>(ConfirmUp);confirm.UnregisterCallback<PointerCaptureOutEvent>(ConfirmOut);
            mapHost.UnregisterCallback<GeometryChangedEvent>(ResizeMap);mapHost.UnregisterCallback<PointerDownEvent>(AimDown);
            mapHost.UnregisterCallback<PointerMoveEvent>(AimMove);mapHost.UnregisterCallback<PointerUpEvent>(AimUp);root=null;
        }
        sealed class FlightMap:VisualElement
        {
            SurveyorMidcourse flow;
            static readonly Color Gold=new Color(.93f,.75f,.44f),Blue=new Color(.6f,.78f,.83f),Dim=new Color(.24f,.32f,.43f);
            public FlightMap(){generateVisualContent+=Paint;}
            public void Set(SurveyorMidcourse value){flow=value;MarkDirtyRepaint();}
            static void Line(Painter2D p,Vector2 a,Vector2 b){p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();}
            static void Circle(Painter2D p,Vector2 center,float radius,bool fill=false)
            {
                p.BeginPath();for(int i=0;i<=64;i++){float a=i*Mathf.PI*2/64;var point=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;if(i==0)p.MoveTo(point);else p.LineTo(point);}
                p.ClosePath();if(fill)p.Fill();else p.Stroke();
            }
            void Paint(MeshGenerationContext context)
            {
                if(flow==null)return;float w=contentRect.width;if(w<=1)return;var p=context.painter2D;
                var earth=new Vector2(.12f,.34f)*w;var moon=SurveyorMidcourse.TargetCenter*w;
                p.lineWidth=1;p.strokeColor=new Color(.15f,.2f,.28f,.6f);
                for(int i=1;i<5;i++)Line(p,new Vector2(.03f*w,.09f*i*w),new Vector2(.97f*w,.09f*i*w));
                p.strokeColor=Blue;Circle(p,earth,w*.042f);p.fillColor=new Color(.08f,.14f,.20f);Circle(p,earth,w*.04f,true);
                float zoom=flow.HandoffProgress;var center=Vector2.Lerp(moon,new Vector2(w*.53f,w*.23f),zoom);
                float radius=w*Mathf.Lerp(SurveyorMidcourse.TargetDiameter*.5f,.25f,zoom);
                p.fillColor=new Color(.10f,.13f,.16f);Circle(p,center,radius,true);p.strokeColor=new Color(.40f,.44f,.48f);Circle(p,center,radius);
                p.strokeColor=new Color(.22f,.26f,.30f);
                Circle(p,center+new Vector2(-.05f,-.06f)*w,radius*.17f);Circle(p,center+new Vector2(.075f,.035f)*w,radius*.22f);Circle(p,center+new Vector2(-.035f,.09f)*w,radius*.12f);
                Vector2 Path(float t)=>(1-t)*(1-t)*earth+2*(1-t)*t*new Vector2(w*.35f,w*.015f)+t*t*moon;
                p.strokeColor=Dim;for(int i=0;i<30;i+=2)Line(p,Path(i/30f),Path((i+1)/30f));
                if(flow.Phase==MidcoursePhase.Travel)
                {p.strokeColor=Gold;p.lineWidth=2;Circle(p,Path(.12f+flow.TravelProgress*.7f),w*.006f);}
                else if(zoom<.8f)
                {
                    float scale=radius/(w*SurveyorMidcourse.TargetDiameter*.5f);
                    Vector2 Place(Vector2 aim)=>center+(aim*w-moon)*scale;
                    p.strokeColor=new Color(.7f,.77f,.64f,.7f);p.lineWidth=2;Circle(p,center,w*flow.Tolerance*scale);
                    var desired=Place(flow.DesiredAim);p.strokeColor=flow.Aligned?Gold:Blue;p.lineWidth=3;
                    Line(p,desired-new Vector2(15,0),desired+new Vector2(15,0));Line(p,desired-new Vector2(0,15),desired+new Vector2(0,15));Circle(p,desired,8);
                    var actual=Place(flow.ActualAim);p.fillColor=Gold;Circle(p,actual,4,true);
                    p.strokeColor=Gold;p.lineWidth=2;if(flow.BurnProgress>0)Line(p,Place(SurveyorMidcourse.InitialAim),actual);
                }
                if(zoom>0)
                {
                    p.strokeColor=new Color(.93f,.75f,.44f,zoom);p.lineWidth=2;float x=w*.92f;
                    Line(p,new Vector2(x,w*.04f),new Vector2(x,w*.4f));
                    for(int i=0;i<=8;i++)Line(p,new Vector2(x-15,w*(.04f+.045f*i)),new Vector2(x,w*(.04f+.045f*i)));
                    Line(p,new Vector2(x-25,w*.12f),new Vector2(x+8,w*.12f));
                }
            }
        }
    }
}
