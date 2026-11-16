using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>The same snapshot drives the world prompt and the action it advertises.</summary>
    public readonly struct InteractionPresentation
    {
        public readonly string Id, LabelKey;
        public readonly Transform Anchor;
        public readonly float Range, Distance;
        public readonly bool Available;
        public bool Valid => Anchor != null;

        public InteractionPresentation(string id, string labelKey, Transform anchor, float range, float distance, bool available)
        { Id=id; LabelKey=labelKey; Anchor=anchor; Range=range; Distance=distance; Available=available; }
    }

    public sealed class InteractionDirector : MonoBehaviour
    {
        public ChapterDirector chapter;
        int executedFrame=-1;

        InteractionPresentation Present(string id, string label, Transform anchor, float range)
        {
            if(!anchor)return default;
            float distance=Vector3.Distance(chapter.rover.transform.position,anchor.position);
            return new InteractionPresentation(id,label,anchor,range,distance,distance<range);
        }

        public InteractionPresentation Current
        {
            get
            {
                if(!chapter||!chapter.rover||chapter.Paused||chapter.Cinematic)return default;
                var field=chapter.investigation;var journey=chapter.journey;
                if(!field||!journey||field.Active||field.InputBlockedThisFrame||journey.photography.Active)return default;
                if(field.NeedBerries)return Present("berries","v13.interact.berries",field.berriesSite,3.5f);
                if(chapter.Stage==ChapterStage.Exploration&&journey.ReadyForScience)return Present("rat","v13.interact.rat",chapter.science.surface.workFrame,3.4f);
                if(chapter.Stage==ChapterStage.HeatShieldExploration)
                {
                    if(!field.ShieldExamined)return Present("shield","v13.interact.shield",field.shieldSite,8);
                    if(!field.MeteoriteIdentified)return Present("meteorite","v13.interact.meteorite",field.meteoriteSite,8);
                    return default;
                }
                if(!journey.FinalAct)return default;
                if(!field.ClearSunMeasured)return Present("sun-clear","v13.interact.sun",field.clearSunSite,8);
                if(journey.PanoramaCount>0&&!field.DustSunMeasured)return Present("sun-dust","v13.interact.sun",field.dustSunSite,8);
                // Com a vista e a comparação solar registradas, a etapa termina sozinha (sem envio de imagem);
                // fotos opcionais seguem disponíveis com F.
                if(journey.PanoramaCount>0)return default;
                int station=journey.NearbyStation;
                if(station>=0)return Present("photo","v13.interact.photo",journey.panoramaStations[station],8);
                return Present("photo","v13.interact.photo",journey.CurrentTarget,8);
            }
        }

        public bool TryExecute()
        {
            if(executedFrame==Time.frameCount)return false;
            var action=Current;if(!action.Valid||!action.Available)return false;
            executedFrame=Time.frameCount;
            switch(action.Id)
            {
                case "berries":chapter.investigation.Begin(FieldStudy.Berries);break;
                case "rat":chapter.BeginRockAnalysis();break;
                case "shield":chapter.investigation.Begin(FieldStudy.Shield);break;
                case "meteorite":chapter.investigation.Begin(FieldStudy.Meteorite);break;
                case "sun-clear":chapter.investigation.Begin(FieldStudy.SunClear);break;
                case "sun-dust":chapter.investigation.Begin(FieldStudy.SunDust);break;
                case "photo":chapter.journey.photography.Toggle();break;
                default:return false;
            }
            return true;
        }

        /// <summary>Reframe the objective without turning or teleporting the rover.</summary>
        public bool FrameTarget()
        {
            var action=Current;Transform target=action.Valid?action.Anchor:chapter.ActiveTarget;
            if(!target||chapter.Paused||chapter.Cinematic||chapter.investigation.Active)return false;
            chapter.cameras.FramePhoto(target);return true;
        }
    }
}
