using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Bloqueia a câmera/RT reais até a transferência para FirstImage, depois do assentamento.</summary>
    public sealed class SurveyorDescentCameraGate:MonoBehaviour,IDisposable
    {
        SurveyorDescent descent;ChapterDirector owner;Camera television;SurveyorSiteViewport viewport;bool bound,granted;
        public bool Active=>television!=null && television.enabled && viewport!=null && viewport.enabled;
        public Camera TelevisionCamera=>television;
        public void Configure(SurveyorDescent flow,ChapterDirector director,Camera camera,SurveyorSiteViewport televisionViewport)
        {
            Dispose();descent=flow;owner=director;television=camera;viewport=televisionViewport;Block();
            if(descent==null || owner==null || television==null || viewport==null)throw new ArgumentException("Gate TV sem referências.");
            bound=true;descent.Changed+=Check;owner.StateChanged+=OnState;owner.StepEntered+=OnStep;
        }
        public bool TryActivate()
        {
            if(descent==null || !(descent.CanActivateCamera || granted && SafeSurfaceState() && !owner.IsPaused)){Block();return false;}
            granted=true;viewport.DrawToGameView=false;viewport.enabled=true;television.enabled=true;return true;
        }
        bool SafeSurfaceState()=>descent!=null && descent.Settled && descent.EnginesCutOff && owner!=null && owner.State==AppState.Playing && owner.CurrentStepId!=SurveyorDescent.StepId;
        void Block(){granted=false;if(television!=null)television.enabled=false;if(viewport!=null)viewport.enabled=false;}
        void Check(){if(!SafeSurfaceState() || !granted && owner.CurrentStepId!=SurveyorDescent.NextStepId)Block();}
        void OnState(AppState state)=>Check();void OnStep(string id)=>Check();void LateUpdate()=>Check();
        public void Dispose(){Block();if(!bound)return;bound=false;descent.Changed-=Check;owner.StateChanged-=OnState;owner.StepEntered-=OnStep;}
        void OnDisable()=>Dispose();
    }
}
