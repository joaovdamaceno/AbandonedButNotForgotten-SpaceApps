using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorTelevisionView:MonoBehaviour
    {
        VisualElement root,entry,flight,overlay,plate,progress,slot,screen;Image image;
        Button start,pause,language,confirm,resume,restart,back,restartPaused,backPaused;
        readonly Dictionary<string,Label> labels=new Dictionary<string,Label>();
        SurveyorTelevision flow;SurveyorCommandDelay queue;SurveyorCommandInteraction action;SurveyorLaunchTexts texts;string locale="en";HoldMode mode;
        public Func<string,string> TextResolver{get;set;}
        public bool DrawLocalSubtitles{get;set;}=true;
        public VisualElement Root=>root;
        public event Action StartRequested,PauseRequested,ResumeRequested,RestartRequested,ReturnRequested,LanguageRequested;
        public void Configure(UIDocument document,TextAsset data,SurveyorTelevision controller,SurveyorCommandDelay delay,SurveyorCommandInteraction interaction,SettingsData settings)
        {
            Unbind();root=document.rootVisualElement;flow=controller;queue=delay;action=interaction;texts=SurveyorLaunchTexts.Parse(data);
            labels.Clear();root.Query<Label>().ForEach(l=>{if(!string.IsNullOrEmpty(l.name))labels[l.name]=l;});
            entry=root.Q("entry");flight=root.Q("flight");overlay=root.Q("pause-overlay");plate=root.Q("subtitle-plate");progress=root.Q("progress");
            slot=root.Q("screen-slot");screen=root.Q("screen");image=root.Q<Image>("image");image.scaleMode=ScaleMode.StretchToFill;
            start=root.Q<Button>("start");pause=root.Q<Button>("pause");language=root.Q<Button>("language");if(language!=null)language.style.display=DisplayStyle.None;confirm=root.Q<Button>("confirm");resume=root.Q<Button>("resume");
            restart=root.Q<Button>("restart");back=root.Q<Button>("return");restartPaused=root.Q<Button>("restart-paused");backPaused=root.Q<Button>("return-paused");
            start.clicked+=RequestStart;pause.clicked+=Pause;language.clicked+=Language;resume.clicked+=Resume;restart.clicked+=Restart;restartPaused.clicked+=Restart;back.clicked+=Return;backPaused.clicked+=Return;
            confirm.RegisterCallback<PointerDownEvent>(OnDown);confirm.RegisterCallback<PointerUpEvent>(OnUp);confirm.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            slot.RegisterCallback<GeometryChangedEvent>(OnGeometry);flow.Changed+=Refresh;queue.Changed+=Refresh;action.Changed+=Refresh;flow.Output.Changed+=Refresh;ApplySettings(settings);
        }
        public void ApplySettings(SettingsData settings)
        {
            locale=settings.languageCode;mode=settings.holdMode;if(root==null)return;
            string[] names={"archive","title","description","mode","help","provenance","location","next","pause-title","pause-description","standby"};
            string[] keys={"tv.archive","tv.title","tv.description","tv.mode","tv.help","tv.provenance","tv.location","tv.next","tv.paused","tv.pause.description","tv.standby"};
            for(int i=0;i<names.Length;i++)Label(names[i],keys[i]);
            start.text=Text("tv.start");pause.text=Text("tv.pause");resume.text=Text("tv.resume");restart.text=restartPaused.text=Text("tv.restart");back.text=backPaused.text=Text("tv.return");
            language.text=locale=="pt"?"EN":"PT";confirm.text=Text(mode==HoldMode.Toggle?"tv.toggle":"tv.receive");screen.tooltip=Text("tv.context");Refresh();
        }
        string Text(string key)=>TextResolver!=null?TextResolver(key):texts.Get(key,locale);void Label(string name,string key)=>labels[name].text=Text(key);
        static void Show(VisualElement e,bool visible)=>e.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
        public void Refresh()
        {
            if(root==null)return;bool active=flow.Running||flow.ActComplete;Show(entry,!active);Show(flight,active);Show(pause,active);Show(overlay,flow.IsPaused);
            string phase=flow.Phase==TelevisionPhase.InTransit?"tv.phase.transit":flow.Phase==TelevisionPhase.Scanning?"tv.phase.scan":flow.Phase>=TelevisionPhase.Contemplation?"tv.phase.ready":"tv.phase.wait";
            string status=flow.IsPaused?"tv.paused":flow.Phase==TelevisionPhase.Locked?"tv.status.locked":flow.Phase==TelevisionPhase.InTransit?"tv.status.transit":
                flow.Phase==TelevisionPhase.Scanning?"tv.status.scan":flow.Phase>=TelevisionPhase.Contemplation?"tv.status.ready":action.IsHolding?"tv.status.hold":action.NeedsRelease?"tv.status.release":"tv.status.wait";
            Label("phase",phase);Label("status",status);bool command=flow.Running&&flow.Phase==TelevisionPhase.AwaitingOrder;
            Show(confirm,command);Show(labels["help"],command);confirm.SetEnabled(!flow.IsPaused&&(action.IsHolding||action.CanConfirm));
            float value=flow.Phase==TelevisionPhase.InTransit?queue.Progress:flow.Phase>=TelevisionPhase.Scanning?flow.Output.Progress:action.PreparationProgress;
            progress.style.width=Length.Percent(value*100);image.image=flow.Output.Texture;
            Show(labels["standby"],flow.Output.Texture==null);Show(labels["provenance"],flow.Output.Texture!=null);Show(labels["location"],true);
            Show(labels["next"],flow.ActComplete);Show(restart,flow.ActComplete);Show(back,flow.ActComplete);
            string subtitle=DrawLocalSubtitles?Text(flow.SubtitleKey):string.Empty;labels["subtitle"].text=subtitle;Show(plate,!string.IsNullOrWhiteSpace(subtitle));
        }
        void OnGeometry(GeometryChangedEvent e){float w=Mathf.Min(slot.contentRect.width,slot.contentRect.height*4/3);screen.style.width=Mathf.Max(0,w);screen.style.height=Mathf.Max(0,w*.75f);}
        void OnDown(PointerDownEvent e){if(e.button==0&&action.BeginConfirmation())confirm.CapturePointer(e.pointerId);e.StopPropagation();}
        void OnUp(PointerUpEvent e){action.ReleaseConfirmation();if(confirm.HasPointerCapture(e.pointerId))confirm.ReleasePointer(e.pointerId);e.StopPropagation();}
        void OnCaptureOut(PointerCaptureOutEvent e)=>action.ReleaseConfirmation();
        void RequestStart()=>StartRequested?.Invoke();void Pause()=>PauseRequested?.Invoke();void Resume()=>ResumeRequested?.Invoke();void Restart()=>RestartRequested?.Invoke();void Return()=>ReturnRequested?.Invoke();void Language()=>LanguageRequested?.Invoke();
        void Unbind()
        {
            if(flow!=null){flow.Changed-=Refresh;flow.Output.Changed-=Refresh;}if(queue!=null)queue.Changed-=Refresh;if(action!=null)action.Changed-=Refresh;if(root==null)return;
            start.clicked-=RequestStart;pause.clicked-=Pause;language.clicked-=Language;resume.clicked-=Resume;restart.clicked-=Restart;restartPaused.clicked-=Restart;back.clicked-=Return;backPaused.clicked-=Return;
            slot.UnregisterCallback<GeometryChangedEvent>(OnGeometry);confirm.UnregisterCallback<PointerDownEvent>(OnDown);confirm.UnregisterCallback<PointerUpEvent>(OnUp);confirm.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);root=null;
        }
        void OnDisable()=>Unbind();
    }
}
