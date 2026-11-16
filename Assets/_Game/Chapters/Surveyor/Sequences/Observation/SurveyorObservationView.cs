using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorObservationView : MonoBehaviour
    {
        VisualElement root, screen, slot, frame, marker, desiredMarker, subtitle, result, pause;
        Image live, photograph; Label objective, status, progress, caption, resultName, resultText, count, direction;
        Button primary, assist, lens, proceed, resume, restart, back, endReturn, language, pauseButton;
        readonly Button[] gallery=new Button[3];
        SurveyorObservation flow; SurveyorCommandDelay queue; SurveyorCommandInteraction action;
        SurveyorTelevisionOutput output; SurveyorLaunchTexts texts; string locale="en"; bool toggle;
        int shownCount=-1;string shownPrimary;
        public VisualElement Root => root;
        public Func<string,string> TextResolver { get; set; }
        public bool DrawLocalSubtitles { get; set; } = true;
        public event Action PauseRequested, ResumeRequested, RestartRequested, ReturnRequested, LanguageRequested;

        public void Configure(UIDocument doc, TextAsset catalog, SurveyorObservation controller, SurveyorCommandDelay delay,
            SurveyorCommandInteraction command, SurveyorTelevisionOutput image, SettingsData settings)
        {
            Unbind();root=doc.rootVisualElement;flow=controller;queue=delay;action=command;output=image;texts=SurveyorLaunchTexts.Parse(catalog);
            slot=root.Q("obs-slot");screen=root.Q("obs-screen");frame=root.Q("obs-frame");marker=root.Q("obs-marker");desiredMarker=root.Q("obs-aim");subtitle=root.Q("obs-subtitle-plate");result=root.Q("obs-result");pause=root.Q("obs-pause");
            live=root.Q<Image>("obs-live");photograph=root.Q<Image>("obs-photo");objective=root.Q<Label>("obs-objective");status=root.Q<Label>("obs-status");
            progress=root.Q<Label>("obs-progress");caption=root.Q<Label>("obs-subtitle");resultName=root.Q<Label>("obs-result-name");resultText=root.Q<Label>("obs-result-text");
            count=root.Q<Label>("obs-count");direction=root.Q<Label>("obs-direction");
            primary=root.Q<Button>("obs-primary");assist=root.Q<Button>("obs-assist");lens=root.Q<Button>("obs-lens");proceed=root.Q<Button>("obs-continue");
            resume=root.Q<Button>("obs-resume");restart=root.Q<Button>("obs-restart");back=root.Q<Button>("obs-return");endReturn=root.Q<Button>("obs-end-return");language=root.Q<Button>("obs-language");if(language!=null)language.style.display=DisplayStyle.None;pauseButton=root.Q<Button>("obs-pause-button");
            assist.clicked+=Assist;lens.clicked+=Lens;resume.clicked+=Resume;restart.clicked+=Restart;back.clicked+=Return;language.clicked+=Language;pauseButton.clicked+=Pause;
            endReturn.clicked+=Return;screen.RegisterCallback<PointerEnterEvent>(PointerEnter);screen.RegisterCallback<PointerLeaveEvent>(PointerLeave);
            primary.RegisterCallback<PointerDownEvent>(Down);primary.RegisterCallback<PointerUpEvent>(Up);primary.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
            proceed.RegisterCallback<PointerDownEvent>(Down);proceed.RegisterCallback<PointerUpEvent>(Up);proceed.RegisterCallback<PointerCaptureOutEvent>(CaptureOut);
            for(int i=0;i<3;i++){gallery[i]=root.Q<Button>("obs-gallery-"+i);}
            gallery[0].clicked+=Review0;gallery[1].clicked+=Review1;gallery[2].clicked+=Review2;
            slot.RegisterCallback<GeometryChangedEvent>(Resize);flow.Changed+=Refresh;ApplySettings(settings);
        }
        string T(string key)=>string.IsNullOrEmpty(key)?string.Empty:TextResolver!=null?TextResolver(key):texts.Get(key,locale);
        static void Show(VisualElement element,bool value)=>element.style.display=value?DisplayStyle.Flex:DisplayStyle.None;
        public void ApplySettings(SettingsData settings)
        {
            locale=settings.languageCode;toggle=settings.holdMode==HoldMode.Toggle;flow.LookSensitivity=settings.lookSensitivity;
            if(root==null)return;
            shownCount=-1;shownPrimary=null;
            root.Q<Label>("obs-title").text=T("obs.title");root.Q<Label>("obs-controls").text=T("obs.controls");root.Q<Label>("obs-provenance").text=T("obs.provenance");
            root.Q<Label>("obs-pause-title").text=T("obs.paused");resume.text=T("obs.resume");restart.text=T("obs.restart");back.text=T("obs.return");
            root.Q<Label>("obs-history").text=T("obs.history");
            assist.text=T("obs.assist");pauseButton.text=T("obs.pause");language.text=locale=="pt"?"EN":"PT";Refresh();
            endReturn.text=T("obs.return");for(int i=0;i<3;i++)gallery[i].text=string.Format(T("obs.gallery"),i+1);
        }
        public void Refresh()
        {
            if(root==null)return;
            bool completed=flow.ActComplete, showing=flow.Phase==ObservationPhase.Result;
            live.image=output.Texture;objective.text=T(completed?"obs.complete":flow.Target.nameKey);
            if(shownCount!=flow.RecordCount){shownCount=flow.RecordCount;count.text=string.Format(T("obs.count"),shownCount);}
            if(shownPrimary!=flow.PrimaryKey){shownPrimary=flow.PrimaryKey;primary.text=T(shownPrimary)+(shownPrimary=="obs.capture"?T(toggle?"obs.toggle.suffix":"obs.hold.suffix"):"");}
            primary.SetEnabled(!flow.IsPaused&&(flow.CanPrimary||action.IsHolding));Show(primary,!showing&&!completed);
            assist.SetEnabled(flow.CanAim);lens.SetEnabled(flow.CanChangeLens);lens.text=T(flow.DetailLens?"obs.wide":"obs.detail");
            Show(assist,!showing&&!completed);Show(lens,!showing&&!completed);Show(frame,!showing&&!completed);Show(marker,!showing&&!completed);Show(desiredMarker,!showing&&!completed);Show(endReturn,completed);
            string key=completed?"obs.status.complete":flow.Phase==ObservationPhase.InTransit?"obs.status.transit":flow.Phase==ObservationPhase.Slewing?"obs.status.moving":
                action.IsHolding?"obs.status.recording":flow.Phase==ObservationPhase.Ready?"obs.status.ready":"obs.status.aim";
            status.text=T(key);progress.text=flow.Phase==ObservationPhase.InTransit?string.Format(T("obs.transit"),queue.RemainingSeconds.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)):
                action.IsHolding?Mathf.RoundToInt(action.PreparationProgress*100)+"%":T(flow.IdleSeconds>=8?"obs.hint":"obs.controls.short");
            frame.EnableInClassList("obs-aligned",flow.Aligned);
            if(!showing&&!completed)
            {
                var p=flow.TargetViewport;float x=p.z>0?p.x:.5f+Mathf.Sign(p.x-.5f),y=p.z>0?p.y:.5f+Mathf.Sign(p.y-.5f);
                marker.style.left=Length.Percent(Mathf.Clamp(x,.06f,.94f)*100);marker.style.top=Length.Percent((1-Mathf.Clamp(y,.08f,.92f))*100);
                var aim=flow.DesiredViewport;desiredMarker.style.left=Length.Percent(Mathf.Clamp(aim.x,.04f,.96f)*100);desiredMarker.style.top=Length.Percent((1-Mathf.Clamp(aim.y,.06f,.94f))*100);
                string arrow=x<.22f?"←":x>.78f?"→":y>.78f?"↑":y<.22f?"↓":"·";
                direction.text=arrow;direction.tooltip=T(flow.Target.nameKey);
            }
            Show(subtitle,DrawLocalSubtitles&&!string.IsNullOrEmpty(flow.SubtitleKey));caption.text=DrawLocalSubtitles?T(flow.SubtitleKey):string.Empty;
            Show(result,showing);
            if(showing&&flow.Result!=null)
            {
                photograph.sprite=flow.Result.Thumbnail;resultName.text=T(flow.Definition.Targets[flow.Reviewing?FindResultIndex():flow.TargetIndex].nameKey);
                resultText.text=T(flow.Result.ResultKey);proceed.text=T(flow.PrimaryKey);proceed.SetEnabled(flow.CanPrimary);
            }
            Show(pause,flow.IsPaused);back.text=T(completed?"obs.return":"obs.return");
            for(int i=0;i<3;i++)gallery[i].SetEnabled(i<flow.RecordCount&&flow.CanAim&&!showing);
        }
        int FindResultIndex(){for(int i=0;i<3;i++)if(flow.Definition.Targets[i].evidenceId==flow.Result.EvidenceId)return i;return 0;}
        void Resize(GeometryChangedEvent e){float w=Mathf.Min(slot.contentRect.width,slot.contentRect.height*4/3);screen.style.width=Mathf.Max(0,w);screen.style.height=Mathf.Max(0,w*.75f);}
        void Down(PointerDownEvent e){if(e.button==0&&flow.BeginPrimary())((VisualElement)e.currentTarget).CapturePointer(e.pointerId);e.StopPropagation();}
        void Up(PointerUpEvent e){flow.ReleasePrimary();var target=(VisualElement)e.currentTarget;if(target.HasPointerCapture(e.pointerId))target.ReleasePointer(e.pointerId);e.StopPropagation();}
        void CaptureOut(PointerCaptureOutEvent e)=>flow.ReleasePrimary();
        void PointerEnter(PointerEnterEvent e)=>flow.PointerOverImage=true;
        void PointerLeave(PointerLeaveEvent e)=>flow.PointerOverImage=false;
        void Assist()=>flow.AssistFrame();void Lens()=>flow.ChangeLens();void Review0()=>flow.Review(0);void Review1()=>flow.Review(1);void Review2()=>flow.Review(2);
        void Pause()=>PauseRequested?.Invoke();void Resume()=>ResumeRequested?.Invoke();void Restart()=>RestartRequested?.Invoke();void Return()=>ReturnRequested?.Invoke();void Language()=>LanguageRequested?.Invoke();
        void Unbind()
        {
            if(root==null)return;flow.Changed-=Refresh;slot.UnregisterCallback<GeometryChangedEvent>(Resize);
            assist.clicked-=Assist;lens.clicked-=Lens;resume.clicked-=Resume;restart.clicked-=Restart;back.clicked-=Return;language.clicked-=Language;pauseButton.clicked-=Pause;
            endReturn.clicked-=Return;screen.UnregisterCallback<PointerEnterEvent>(PointerEnter);screen.UnregisterCallback<PointerLeaveEvent>(PointerLeave);flow.PointerOverImage=false;
            gallery[0].clicked-=Review0;gallery[1].clicked-=Review1;gallery[2].clicked-=Review2;
            foreach(var button in new[]{primary,proceed}){button.UnregisterCallback<PointerDownEvent>(Down);button.UnregisterCallback<PointerUpEvent>(Up);button.UnregisterCallback<PointerCaptureOutEvent>(CaptureOut);}
            root=null;
        }
        void OnDisable()=>Unbind();
    }
}
