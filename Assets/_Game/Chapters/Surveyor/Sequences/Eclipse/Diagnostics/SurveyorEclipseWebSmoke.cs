using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Opt-in, isolated preview only. Not referenced by the production chapter.</summary>
    public sealed class SurveyorEclipseWebSmoke : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public string status="waiting-click",utc,graphicsDevice,unityVersion;
            public int cycles,restarts,redPixels,overexposedPixels,bufferAllocations,bufferReleases;
            public long allocatedBytes;
            public bool fixedObserver=true,bodyUnchanged=true,pausePreserved,sourceReleased;
            public List<string> errors=new List<string>();
        }
        Report report; SurveyorEclipsePreview host; bool running;
        IEnumerator Start()
        {
            if(!Application.absoluteURL.Contains("sur011qa=1")) yield break;
            host=GetComponent<SurveyorEclipsePreview>();
            while(host.Settings==null) yield return null;
            report=new Report { utc=DateTime.UtcNow.ToString("O"),graphicsDevice=SystemInfo.graphicsDeviceName,unityVersion=Application.unityVersion };
            host.Owner.StepEntered+=Step; Publish();
        }
        void Step(string id)
        { if(!running && id==SurveyorEclipse.StepId) { running=true; StartCoroutine(Run()); } }
        IEnumerator Run()
        {
            yield return null;
            Vector3 position=host.Flow.TelevisionCamera.transform.position;
            Quaternion body=host.Site.MirrorAzimuth.parent.rotation;
            report.status="running";
            for(int cycle=0;cycle<3;cycle++)
            {
                if(cycle>0) { host.Restart(); report.restarts++; }
                QualitySettings.SetQualityLevel(Mathf.Min(cycle%2,QualitySettings.names.Length-1),true);
                host.ApplySettings(new SettingsData { languageCode=cycle==1?"en":"pt",reducedMotion=cycle==2 });
                Check(host.Flow.BeginConfirmation(),"Order unavailable."); Publish();
                float deadline=Time.realtimeSinceStartup+60;
                // SequenceDirector retains its final time until the next Play. Wait for reception first.
                while(host.Owner.CurrentStepId==SurveyorEclipse.StepId &&
                    (host.Flow.Phase!=EclipsePhase.Viewing || host.Flow.SequenceTime<16) && Time.realtimeSinceStartup<deadline) yield return null;
                Check(host.Flow.Phase==EclipsePhase.Viewing,"Eclipse did not begin.");
                if(cycle==0)
                {
                    host.Pause(); float t=host.Flow.SequenceTime; yield return new WaitForSecondsRealtime(.3f);
                    report.pausePreserved=host.Flow.SequenceTime==t; Check(report.pausePreserved,"Pause advanced the film."); host.Resume();
                }
                report.fixedObserver &= Vector3.Distance(position,host.Flow.TelevisionCamera.transform.position)<.0001f;
                report.bodyUnchanged &= Quaternion.Angle(body,host.Site.MirrorAzimuth.parent.rotation)<.0001f;
                Check(report.fixedObserver && report.bodyUnchanged,"The physical source moved.");
                MeasureImage(); Check(host.Subtitles.CurrentText==string.Empty,"Quiet window has text."); Publish();
                while(host.Owner.CurrentStepId==SurveyorEclipse.StepId && Time.realtimeSinceStartup<deadline) yield return null;
                Check(host.Owner.CurrentStepId==SurveyorEclipse.NextStepId,"Night handoff failed.");
                report.cycles=cycle+1; Publish(); yield return new WaitForSecondsRealtime(1);
            }
            host.Return(); host.Dispose(); host.Viewport.enabled=false; yield return null;
            report.sourceReleased=host.Viewport.Texture==null && host.Output.Texture==null;
            report.bufferAllocations=host.Output.Allocations; report.bufferReleases=host.Output.Releases;
            report.allocatedBytes=Profiler.GetTotalAllocatedMemoryLong();
            Check(report.sourceReleased && report.bufferAllocations==report.bufferReleases,"Image memory retained.");
            report.status=report.errors.Count==0?"passed":"failed"; Publish();
        }
        void MeasureImage()
        {
            var previous=RenderTexture.active; RenderTexture.active=host.Viewport.Texture;
            var image=new Texture2D(640,480,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,640,480),0,0); image.Apply(); RenderTexture.active=previous;
            int red=0,white=0;
            foreach(var color in image.GetPixels32()) { if(color.r>35 && color.r>color.g*1.5f && color.r>color.b*1.5f) red++; if(color.r>245 && color.g>245 && color.b>245) white++; }
            Destroy(image); report.redPixels=red; report.overexposedPixels=white;
            Check(red>20,"Missing red atmosphere."); Check(white<640*480*.005,"Blown white image.");
        }
        void Check(bool value,string message) { if(!value) report.errors.Add(message); }
        void Publish()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            SUR011_Report(JsonUtility.ToJson(report,true));
            #endif
        }
        #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SUR011_Report(string json);
        #endif
        void OnDisable() { if(host!=null) host.Owner.StepEntered-=Step; }
    }
}
