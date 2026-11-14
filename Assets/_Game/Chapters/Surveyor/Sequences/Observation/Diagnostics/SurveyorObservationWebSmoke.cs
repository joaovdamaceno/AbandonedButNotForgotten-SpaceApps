using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Opt-in diagnostic in the isolated preview; never installed in the published chapter.</summary>
    public sealed class SurveyorObservationWebSmoke : MonoBehaviour
    {
        [SerializeField] SurveyorObservationPreviewHost host;
        [Serializable]sealed class Report
        {
            public string status="waiting-start",utc,graphicsDevice,unityVersion;
            public int cycles,restarts,photos,uniqueViews,allocations,releases;
            public bool fixedObserver,delayedPointing,pausePreserved,photosReleased,sourceReleased;
            public List<string> errors=new List<string>();
        }
        Report report;bool running,awaitingReturn;readonly List<Sprite> photographs=new List<Sprite>();
        public void Configure(SurveyorObservationPreviewHost value)=>host=value;
        IEnumerator Start()
        {
            if(!Application.absoluteURL.Contains("sur008qa=1"))yield break;
            while(host.Preceding.Settings==null)yield return null;
            QualitySettings.SetQualityLevel(0,true);
            report=new Report{utc=DateTime.UtcNow.ToString("O"),graphicsDevice=SystemInfo.graphicsDeviceName,unityVersion=Application.unityVersion};
            host.Preceding.Owner.StepEntered+=Step;host.Preceding.Owner.StateChanged+=State;Publish();
        }
        void Step(string id){if(!running&&id==SurveyorDescent.StepId){running=true;StartCoroutine(Run());}}
        void State(AppState value)
        {
            if(!awaitingReturn||value!=AppState.Dossier)return;
            StartCoroutine(Finish());
        }
        IEnumerator Finish()
        {
            yield return null;yield return null;
            report.photosReleased=photographs.TrueForAll(p=>p==null);report.sourceReleased=host.Preceding.Output.Texture==null&&host.Preceding.Output.Source==null;
            report.allocations=host.Preceding.Output.Allocations;report.releases=host.Preceding.Output.Releases;
            Check(report.photosReleased&&report.sourceReleased&&report.allocations==report.releases,"Return retained runtime resources.");
            report.status=report.errors.Count==0?"passed":"failed";Publish();
        }
        IEnumerator Run()
        {
            yield return null;var h=host.Preceding;h.Clock.enabled=false;report.status="running";Publish();
            var position=h.Gate.TelevisionCamera.transform.position;var signatures=new HashSet<uint>();
            for(int cycle=0;cycle<3;cycle++)
            {
                if(cycle>0){h.Restart();report.restarts++;}
                Advance(40.2f);Check(h.Action.BeginConfirmation(),"First-image order unavailable.");Advance(3.4f);Advance(10.1f);yield return null;yield return null;
                Check(h.Owner.CurrentStepId=="Observe","Observe was not entered.");if(Failed())yield break;
                for(int n=0;n<3;n++)
                {
                    var rotation=h.Gate.TelevisionCamera.transform.rotation;
                    Check(host.Flow.AssistFrame(),"H-equivalent command unavailable.");Advance(.5f);
                    if(cycle==0&&n==1)
                    {
                        h.Pause();float elapsed=h.Queue.ElapsedSeconds;yield return new WaitForSecondsRealtime(.2f);
                        report.pausePreserved=elapsed==h.Queue.ElapsedSeconds;Check(report.pausePreserved,"Transit advanced during pause.");h.Resume();
                    }
                    Advance(.7f);report.delayedPointing=h.Gate.TelevisionCamera.transform.rotation==rotation;Check(report.delayedPointing,"Mirror moved before the command arrived.");Advance(6);
                    report.fixedObserver=h.Gate.TelevisionCamera.transform.position==position;Check(report.fixedObserver&&host.Flow.Aligned,"Observer moved or target could not be framed.");
                    yield return null;yield return null;
                    Check(host.Flow.BeginPrimary(),"Photo hold unavailable.");Advance(1.55f);host.Flow.ReleasePrimary();Advance(1.35f);
                    Check(host.Flow.Phase==ObservationPhase.Result,"Photo result did not appear.");if(Failed())yield break;
                    var picture=host.Flow.Result.Thumbnail;photographs.Add(picture);report.photos++;
                    Check(picture.rect.width<=640&&picture.rect.height<=480,"Photo exceeded its memory budget.");
                    if(cycle==0)
                    {
                        var pixels=picture.texture.GetPixels32();uint signature=2166136261;byte low=255,high=0;
                        for(int i=0;i<pixels.Length;i+=37){signature=(signature^pixels[i].r)*16777619;low=Math.Min(low,pixels[i].r);high=Math.Max(high,pixels[i].r);}
                        Check(high-low>8&&signatures.Add(signature),"Blank or repeated photograph.");report.uniqueViews=signatures.Count;
                    }
                    Check(host.Flow.BeginPrimary(),"Result could not be closed.");Advance(.05f);yield return null;
                    if(Failed())yield break;
                }
                Check(h.Owner.CurrentStepId=="Sample"&&h.Owner.Evidence.PlayerPhotoCount==3,"Three-picture handoff failed.");report.cycles=cycle+1;Publish();
                if(Failed())yield break;
            }
            report.status="awaiting-return";awaitingReturn=true;Publish();
        }
        void Advance(float seconds)
        {
            var h=host.Preceding;
            while(seconds>.0001f){float dt=Mathf.Min(.05f,seconds);h.Clock.Tick(dt);h.Service.Tick(h.Clock.ActiveDeltaSeconds);h.Queue.Tick();h.Descent.Tick(h.Clock.ActiveDeltaSeconds);h.Flow.Tick(h.Clock.ActiveDeltaSeconds);host.Flow.Tick(h.Clock.ActiveDeltaSeconds);seconds-=dt;}h.Clock.Tick(0);
        }
        void Check(bool condition,string message){if(!condition)report.errors.Add(message);}
        bool Failed(){if(report.errors.Count==0)return false;report.status="failed";Publish();return true;}
        void Publish()
        {
            string json=JsonUtility.ToJson(report,true);
            #if UNITY_WEBGL && !UNITY_EDITOR
            SUR008_Report(json);
            #else
            Debug.Log("SUR-008 diagnostic: "+json);
            #endif
        }
        #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void SUR008_Report(string json);
        #endif
    }
}
