using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorSoilPanel : MonoBehaviour
    {
        UIDocument document;SurveyorSoilTests flow;SurveyorTelevisionOutput output;LocalizationService localization;
        VisualElement frame,comparison,curve;Image live,before,after,archive;Label objective,prompt,caption,beforeLabel,afterLabel;ProgressBar progress;
        public UIDocument Document=>document;
        public void Initialize(UIDocument ui,SurveyorSoilTests controller,SurveyorTelevisionOutput source,LocalizationService texts)
        {
            document=ui;flow=controller;output=source;localization=texts;var root=ui.rootVisualElement;
            frame=root.Q("frame");comparison=root.Q("comparison");curve=root.Q("curve");live=root.Q<Image>("live");before=root.Q<Image>("before");after=root.Q<Image>("after");archive=root.Q<Image>("archive");
            objective=root.Q<Label>("objective");prompt=root.Q<Label>("prompt");caption=root.Q<Label>("caption");beforeLabel=root.Q<Label>("before-label");afterLabel=root.Q<Label>("after-label");progress=root.Q<ProgressBar>("progress");
            root.Q("slot").RegisterCallback<GeometryChangedEvent>(e=>Fit());curve.generateVisualContent+=DrawCurve;flow.Changed+=Refresh;Refresh();
        }
        void Fit(){var slot=document.rootVisualElement.Q("slot");float width=Mathf.Min(slot.resolvedStyle.width,slot.resolvedStyle.height*4/3);if(width>0&&float.IsFinite(width)){frame.style.width=width;frame.style.height=width*.75f;}}
        static void Show(VisualElement element,bool show)=>element.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
        public void Refresh()
        {
            if(document==null)return;bool comparing=flow.Phase==SoilPhase.Comparison;bool historic=flow.Phase==SoilPhase.Archive;
            live.image=output.Texture;before.image=flow.Before;after.image=flow.After;archive.image=flow.Results.HistoricalPhoto;
            live.scaleMode=before.scaleMode=after.scaleMode=archive.scaleMode=ScaleMode.ScaleToFit;
            Show(live,!comparing&&!historic);Show(comparison,comparing);Show(archive,historic);
            string key=flow.Phase==SoilPhase.Digging?"soil.dig":flow.Phase==SoilPhase.Press||flow.Phase==SoilPhase.PressRetreat?"soil.press":flow.Phase==SoilPhase.Impact||flow.Phase==SoilPhase.Falling||flow.Phase==SoilPhase.ImpactRetreat?"soil.impact":comparing?"soil.compare":historic?"soil.archive":flow.Phase==SoilPhase.Montage?"soil.missionTotal":"soil.done";
            objective.text=localization.Get(key);prompt.text=flow.NeedsRelease?localization.Get("soil.release"):flow.CanAct?localization.Get("soil.hold"):string.Empty;
            beforeLabel.text=localization.Get("soil.before");afterLabel.text=localization.Get("soil.after");caption.text=localization.Get(historic?SurveyorSoilResults.HistoricalCreditKey:flow.Phase==SoilPhase.Montage?"soil.total":"soil.qualitative");
            progress.value=flow.Preparation*100;Show(progress,flow.Phase==SoilPhase.Press||flow.Phase==SoilPhase.Impact);Show(curve,flow.Phase==SoilPhase.Press);curve.MarkDirtyRepaint();Fit();
        }
        void DrawCurve(MeshGenerationContext context)
        {
            var p=context.painter2D;var r=curve.contentRect;p.lineWidth=2;p.strokeColor=new Color(.48f,.77f,.74f);p.BeginPath();
            for(int i=0;i<=30;i++){float x=flow.Preparation*i/30;var point=new Vector2(12+x*(r.width-24),r.height-10-Mathf.Pow(x,.65f)*(r.height-20));if(i==0)p.MoveTo(point);else p.LineTo(point);}p.Stroke();
        }
        void OnDisable(){if(flow!=null)flow.Changed-=Refresh;}
    }
}
