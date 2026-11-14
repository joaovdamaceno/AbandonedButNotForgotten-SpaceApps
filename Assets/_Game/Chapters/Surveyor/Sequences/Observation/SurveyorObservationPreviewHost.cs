using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Isolated SUR-008 composition. Production injects its existing Core services instead.</summary>
    public sealed class SurveyorObservationPreviewHost : MonoBehaviour
    {
        [SerializeField] SurveyorTelevisionPreviewHost preceding;
        [SerializeField] SurveyorObservation flow;
        [SerializeField] SurveyorObservationTargets targets;
        [SerializeField] SurveyorObservationView view;
        [SerializeField] UIDocument document;
        [SerializeField] TextAsset texts;
        [SerializeField] AudioSource confirmation;
        bool initialized;
        public SurveyorTelevisionPreviewHost Preceding => preceding;
        public SurveyorObservation Flow => flow;
        public SurveyorObservationView View => view;
        public UIDocument Document => document;
        public void Configure(SurveyorTelevisionPreviewHost before,SurveyorObservation controller,SurveyorObservationTargets data,
            SurveyorObservationView panel,UIDocument ui,TextAsset catalog,AudioSource sound)
        {preceding=before;flow=controller;targets=data;view=panel;document=ui;texts=catalog;confirmation=sound;}
        void Start()=>Initialize();
        public void Initialize()
        {
            if(initialized)return;preceding.Initialize();
            var bindings=preceding.GetComponentInChildren<SurveyorSiteBindings>();
            flow.Configure(preceding.Owner,preceding.Clock,preceding.Input,preceding.Service,preceding.Queue,preceding.Action,
                bindings,preceding.Gate.TelevisionCamera,preceding.Output,targets);
            view.Configure(document,texts,flow,preceding.Queue,preceding.Action,preceding.Output,preceding.Settings.Current);
            view.PauseRequested+=preceding.Pause;view.ResumeRequested+=preceding.Resume;view.RestartRequested+=preceding.Restart;view.ReturnRequested+=preceding.Return;view.LanguageRequested+=Language;
            preceding.Owner.StepEntered+=Step;preceding.Owner.StateChanged+=State;preceding.Owner.PausedChanged+=Paused;preceding.Settings.Changed+=Settings;flow.PictureRecorded+=Recorded;
            initialized=true;Settings(preceding.Settings.Current);Refresh();
        }
        void Language()=>preceding.Settings.Apply(s=>s.languageCode=s.languageCode=="pt"?"en":"pt");
        void Settings(SettingsData value){view.ApplySettings(value);confirmation.volume=value.masterVolume*value.uiVolume*.35f;}
        void Recorded(){if(confirmation!=null&&confirmation.clip!=null)confirmation.Play();}
        void Step(string id)=>Refresh();void State(AppState value)=>Refresh();void Paused(bool value)=>view.Refresh();
        void Refresh()
        {
            bool active=preceding.Owner.State==AppState.Playing && (preceding.Owner.CurrentStepId==SurveyorObservation.StepId || preceding.Owner.CurrentStepId==SurveyorObservation.NextStepId);
            document.rootVisualElement.style.display=active?DisplayStyle.Flex:DisplayStyle.None;
            if(active)preceding.Document.rootVisualElement.style.display=DisplayStyle.None;
            view.Refresh();
        }
        public void Dispose()
        {
            if(!initialized)return;initialized=false;
            view.PauseRequested-=preceding.Pause;view.ResumeRequested-=preceding.Resume;view.RestartRequested-=preceding.Restart;view.ReturnRequested-=preceding.Return;view.LanguageRequested-=Language;
            preceding.Owner.StepEntered-=Step;preceding.Owner.StateChanged-=State;preceding.Owner.PausedChanged-=Paused;preceding.Settings.Changed-=Settings;flow.PictureRecorded-=Recorded;flow.Dispose();
        }
        void OnDisable()=>Dispose();
    }
}
