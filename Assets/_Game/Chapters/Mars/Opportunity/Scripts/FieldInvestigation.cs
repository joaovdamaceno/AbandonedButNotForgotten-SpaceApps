using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Abandoned.SpaceApps.Opportunity
{
    public enum FieldStudy { None, Berries, Shield, Meteorite, SunClear, SunDust }
    public sealed class FieldInvestigation : MonoBehaviour
    {
        public ChapterDirector chapter;
        public Transform berriesSite,berryClose,rockClose,shieldSite,shieldClose,meteoriteSite,meteoriteClose,heatSpawn;
        public Transform clearSunSite,dustSunSite,entryPayload,entryBridle;
        public Collider berrySpecimen;
        public GameObject shieldRegion,entryCapsule,entryShield,entryParachute;
        public ParticleSystem entryBrake;
        public Texture2D orbitalMap,berryArchive,shieldArchive,solarArchive,meteoriteArchive;
        readonly Texture2D[] frames=new Texture2D[6];
        public Texture2D BerryFirstFrame { get; private set; }
        public Texture2D ClearSunFrame=>frames[(int)FieldStudy.SunClear];
        public Texture2D DustSunFrame=>frames[(int)FieldStudy.SunDust];
        public Texture2D FrameFor(FieldStudy kind)=>frames[(int)kind];
        public Texture2D ResultImage=>Active?frames[(int)Study]:null;
        public bool Capturing { get; private set; }
        string previousCue;bool acquired;int transitionFrame=-1;
        public bool InputBlockedThisFrame=>transitionFrame==Time.frameCount;
        public FieldStudy Study { get; private set; }
        public bool Active=>Study!=FieldStudy.None;
        public bool BerriesCompared { get; private set; }
        public bool ShieldExamined { get; private set; }
        public bool MeteoriteIdentified { get; private set; }
        public bool ClearSunMeasured { get; private set; }
        public bool DustSunMeasured { get; private set; }
        public bool Result { get; private set; }
        bool resultReleased;float resultHold;
        public int Sample { get; private set; }
        public float Dwell { get; private set; }
        public float AimX { get; private set; }
        public float AimY { get; private set; }
        public bool Solar=>Study==FieldStudy.SunClear||Study==FieldStudy.SunDust;
        public Vector3 FocusPoint=>Solar?chapter.rover.transform.position+Vector3.up*1.65f-chapter.environment.sun.transform.forward*500:
            (Study==FieldStudy.Berries?(Sample==0?berryClose:rockClose):Study==FieldStudy.Shield?shieldClose:meteoriteClose).position;
        public Vector3 TargetViewport=>Active?chapter.cameras.view.WorldToViewportPoint(FocusPoint):new Vector3(.5f,.5f,1);
        public float FramingError=>Vector2.Distance(new Vector2(TargetViewport.x,TargetViewport.y),new Vector2(.5f,.5f));
        // Observations are hold-only: the camera is already framed on the subject, nothing has to be aimed.
        public bool Stable=>Active;
        public float FramingQuality=>Mathf.Clamp01(1-FramingError/.24f);
        public string DirectionKey { get {var v=TargetViewport-new Vector3(.5f,.5f,0);return Mathf.Abs(v.x)>Mathf.Abs(v.y)?(v.x>0?"v11.guide.right":"v11.guide.left"):(v.y>0?"v11.guide.up":"v11.guide.down");} }
        public bool NeedBerries=>chapter.Stage==ChapterStage.Exploration&&chapter.journey.TraverseStep>=1&&!BerriesCompared;
        public bool NeedSun=>chapter.journey.FinalAct&&(!ClearSunMeasured||chapter.journey.PanoramaCount>0&&!DustSunMeasured);
        public Transform CurrentTarget=>NeedBerries?berriesSite:chapter.Stage==ChapterStage.HeatShieldExploration?(ShieldExamined?meteoriteSite:shieldSite):!ClearSunMeasured?clearSunSite:dustSunSite;
        public string ObjectiveKey=>NeedBerries?"v10.goal.berries":chapter.Stage==ChapterStage.HeatShieldExploration?(ShieldExamined?"v10.goal.meteorite":"v10.goal.shield"):ClearSunMeasured?"v10.goal.sun.dust":"v10.goal.sun.clear";
        public bool Near=>CurrentTarget&&Vector3.Distance(chapter.rover.transform.position,CurrentTarget.position)<8;
        public bool OverridesTarget=>NeedBerries||chapter.Stage==ChapterStage.HeatShieldExploration||NeedSun;
        public string TitleKey=>"v10.study."+Study.ToString().ToLowerInvariant();
        public string BodyKey=>Result?"v10.result."+Study.ToString().ToLowerInvariant():Solar?"v10.study.solar.help":Study==FieldStudy.Berries?(Sample==0?"v10.berries.first":"v10.berries.second"):"v10.study.help";
        public Texture2D Archive=>Study==FieldStudy.Berries?berryArchive:Study==FieldStudy.Shield?shieldArchive:Study==FieldStudy.Meteorite?meteoriteArchive:solarArchive;
        public void ResetEvidence()
        {
            Close();foreach(var f in frames)if(f)Destroy(f);System.Array.Clear(frames,0,frames.Length);
            if(BerryFirstFrame)Destroy(BerryFirstFrame);BerryFirstFrame=null;
            BerriesCompared=ShieldExamined=MeteoriteIdentified=ClearSunMeasured=DustSunMeasured=false;
        }
        public void Begin(FieldStudy study)
        {
            if(Active||chapter.Paused||study==FieldStudy.None)return;
            transitionFrame=Time.frameCount;
            previousCue=chapter.sound.CurrentCue;Study=study;Sample=0;Result=false;Capturing=false;Dwell=0;AimX=0;AimY=0;acquired=false;
            chapter.rover.Controllable=false;chapter.rover.Halt();chapter.journey.photography.Close();chapter.sound.Cue("science");chapter.SetCursor();
        }
        public void Close()
        {
            transitionFrame=Time.frameCount;
            string[] followUp=null;
            if(Active&&Result&&chapter&&chapter.film)
            {
                // Depois de cada observação, a narração diz o que ela significa e para onde ir em seguida.
                if(Study==FieldStudy.Berries)followUp=new[]{"v14.berries.water","v14.berries.doubt","route.outcrop"};
                else if(Study==FieldStudy.Shield)followUp=new[]{"v14.shield.odd","v10.goal.meteorite"};
                else if(Study==FieldStudy.SunClear)followUp=new[]{"objective.panorama"};
            }
            StopAllCoroutines();if(Capturing&&chapter&&chapter.hud)chapter.hud.HideForCapture(false);Capturing=false;
            if(Active&&!string.IsNullOrEmpty(previousCue))chapter.sound.Cue(previousCue);
            Study=FieldStudy.None;Dwell=0;Result=false;
            if(followUp!=null)chapter.film.SpeakSequence(followUp);
            if(chapter&&chapter.rover){chapter.rover.Controllable=chapter.Stage==ChapterStage.Exploration||chapter.Stage==ChapterStage.HeatShieldExploration||chapter.journey.FinalAct;chapter.SetCursor();}
        }
        public void Observe(float dt,Vector2 adjustment,bool hold,bool confirm)
        {
            if(!Active||chapter.Paused||Capturing||InputBlockedThisFrame)return;
            // O resultado fecha segurando E de novo (depois de soltar).
            if(Result){if(!hold)resultReleased=true;resultHold=resultReleased&&hold?resultHold+dt:0;if(resultHold>=.6f)Close();return;}
            if(Stable&&!acquired){acquired=true;chapter.sound.Confirm();}
            Dwell=Mathf.MoveTowards(Dwell,hold?1:0,dt/(hold?2.2f:3.5f));
            if(Dwell>=1)StartCoroutine(CaptureSample());
        }
        IEnumerator CaptureSample()
        {
            Capturing=true;chapter.sound.Photograph();chapter.feedback.Observe();
            chapter.hud.HideForCapture(true);yield return null;yield return new WaitForEndOfFrame();
            var frame=FrameCaptureUtility.Photograph();chapter.hud.HideForCapture(false);
            // A rocha das esferas é uma única observação.
            int index=(int)Study;if(frames[index])Destroy(frames[index]);frames[index]=frame;
            // Depois das esferas, o próximo destino é a própria McKittrick (sem ponto de rota intermediário).
            if(Study==FieldStudy.Berries){BerriesCompared=true;chapter.journey.CompleteTraverseForPreview();}
            if(Study==FieldStudy.Shield)ShieldExamined=true;
            if(Study==FieldStudy.Meteorite)MeteoriteIdentified=true;
            if(Study==FieldStudy.SunClear)ClearSunMeasured=true;
            if(Study==FieldStudy.SunDust)DustSunMeasured=true;
            Result=true;resultReleased=false;resultHold=0;Capturing=false;chapter.sound.Confirm();chapter.hud.ObjectiveChanged();
        }
        public void CameraPose(out Vector3 position,out Quaternion rotation,out float fov)
        {
            if(Solar){position=chapter.rover.transform.position+Vector3.up*1.65f;rotation=Quaternion.LookRotation(-chapter.environment.sun.transform.forward)*Quaternion.Euler(AimY*12,AimX*12,0);fov=12;return;}
            Vector3 focus=FocusPoint,offset=Study==FieldStudy.Berries?new Vector3(.10f,.32f,-.12f):Study==FieldStudy.Shield?new Vector3(-2.3f,1.9f,-2):new Vector3(-.65f,.55f,-.65f);
            position=focus+offset;
            if(Study==FieldStudy.Berries&&berrySpecimen&&berrySpecimen.Raycast(new Ray(position+Vector3.up,Vector3.down),out var clear,2))position.y=Mathf.Max(position.y,clear.point.y+.12f);
            rotation=Quaternion.LookRotation(focus-position)*Quaternion.Euler(AimY*7,AimX*7,0);fov=Study==FieldStudy.Berries?28:40;
        }
        void Update()
        {
            if(chapter.Paused)return;
            if(Active){Observe(Time.deltaTime,Vector2.zero,GameInput.ActionHeld,GameInput.ActionPressed);return;}
        }
    }
}
