using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Explicit ?sur007qa=1 diagnostic for the isolated preview only; absent from chapter prefabs.</summary>
    public sealed class SurveyorTelevisionWebSmoke:MonoBehaviour
    {
        [SerializeField] SurveyorTelevisionPreviewHost host;
        [Serializable]sealed class Report
        {
            public string status="waiting-start",utc,graphicsDevice,unityVersion,qualityName;public int cycles,restarts,allocations,releases;
            public bool sourceGated,scanTopToBottom,monochrome,scanPausePreserved,observerPositionPreserved,pictureRetained,returnReleased;
            public float beforeMean,halfTopMean,halfBottomMean,fullRange;public List<string> errors=new List<string>();
        }
        Report report;bool running,awaitingReturn;
        public void Configure(SurveyorTelevisionPreviewHost preview)=>host=preview;
        IEnumerator Start()
        {
            if(!Application.absoluteURL.Contains("sur007qa=1"))yield break;
            while(host.Settings==null)yield return null;
            QualitySettings.SetQualityLevel(0,true);
            report=new Report{utc=DateTime.UtcNow.ToString("O"),graphicsDevice=SystemInfo.graphicsDeviceName,unityVersion=Application.unityVersion,qualityName=QualitySettings.names[QualitySettings.GetQualityLevel()]};
            host.Owner.StepEntered+=OnStep;host.Owner.StateChanged+=OnState;Publish();
        }
        void OnStep(string step){if(!running&&step==SurveyorDescent.StepId){running=true;StartCoroutine(Run());}}
        void OnState(AppState state)
        {
            if(!awaitingReturn||state!=AppState.Dossier)return;
            report.returnReleased=host.Output.Texture==null&&host.Output.Source==null;report.allocations=host.Output.Allocations;report.releases=host.Output.Releases;
            Check(report.returnReleased&&report.allocations==report.releases,"Return did not release every buffer.");report.status=report.errors.Count==0?"passed":"failed";Publish();
        }
        IEnumerator Run()
        {
            yield return null;report.status="running";Publish();
            for(int round=0;round<4;round++)
            {
                host.Clock.enabled=false;Advance(40.2f);host.Clock.Tick(0);
                report.sourceGated=host.Output.Texture==null&&host.Output.Source==null;Check(report.sourceGated,"An image existed before the receive order.");
                Check(host.Flow.Phase==TelevisionPhase.AwaitingOrder,"FirstImage was not offered.");
                if(report.errors.Count>0){Fail();yield break;}
                Check(host.Action.BeginConfirmation(),"The receive hold could not start.");host.Clock.enabled=true;
                float deadline=Time.realtimeSinceStartup+15;
                while(host.Flow.Phase!=TelevisionPhase.Scanning&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(host.Flow.Phase==TelevisionPhase.Scanning,"Receive order timed out.");host.Clock.enabled=false;host.Clock.Tick(0);
                if(report.errors.Count>0){Fail();yield break;}
                var source=host.Output.Source;var image=host.Output.Texture;var observer=host.Gate.TelevisionCamera.transform.position;
                Check(source!=null&&image!=null&&source.width<=640&&source.height<=480&&image.width<=640&&image.height<=480,"Texture dimensions invalid.");
                yield return null;yield return null;var before=Sample(image);report.beforeMean=before.mean;
                Advance(Mathf.Max(0,2-host.Flow.SequenceTime));host.Clock.Tick(0);yield return null;yield return null;
                var half=Sample(image);report.halfTopMean=half.top;report.halfBottomMean=half.bottom;
                report.scanTopToBottom=half.top>before.mean+.02f&&Mathf.Abs(half.bottom-before.mean)<.03f;Check(report.scanTopToBottom,"Scan direction or unrevealed region invalid.");
                host.Pause();float progress=host.Output.Progress;yield return new WaitForSecondsRealtime(.2f);report.scanPausePreserved=host.Output.Progress==progress;host.Resume();Check(report.scanPausePreserved,"Scan advanced during pause.");
                Advance(Mathf.Max(0,4-host.Flow.SequenceTime)+.01f);host.Clock.Tick(0);yield return null;yield return null;
                var full=Sample(image);report.fullRange=full.range;report.monochrome=full.colorError<.01f;Check(full.range>.03f&&report.monochrome,"Final image is blank or not monochrome.");
                Advance(6.1f);host.Clock.Tick(0);yield return null;
                report.observerPositionPreserved=host.Gate.TelevisionCamera.transform.position==observer;report.pictureRetained=host.Output.Texture==image&&host.Output.Source==source&&host.Owner.CurrentStepId==SurveyorTelevision.NextStepId;
                Check(report.observerPositionPreserved&&report.pictureRetained,"Handoff moved the observer or replaced the picture.");report.cycles=round+1;Publish();
                if(round<3)
                {
                    host.Restart();host.Clock.Tick(0);yield return null;
                    Check(host.Output.Texture==null&&host.Output.Source==null,"Restart retained a texture.");report.restarts++;
                }
                if(report.errors.Count>0){Fail();yield break;}
            }
            report.allocations=host.Output.Allocations;report.releases=host.Output.Releases;report.status="awaiting-return";awaitingReturn=true;Publish();
        }
        void Advance(float seconds){while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);host.Clock.Tick(d);host.Service.Tick(host.Clock.ActiveDeltaSeconds);host.Queue.Tick();host.Descent.Tick(host.Clock.ActiveDeltaSeconds);host.Flow.Tick(host.Clock.ActiveDeltaSeconds);seconds-=d;}host.Clock.Tick(0);}
        struct Pixels{public float mean,top,bottom,range,colorError;}
        static Pixels Sample(RenderTexture image)
        {
            var old=RenderTexture.active;RenderTexture.active=image;var read=new Texture2D(image.width,image.height,TextureFormat.RGBA32,false);
            read.ReadPixels(new Rect(0,0,image.width,image.height),0,0);read.Apply();RenderTexture.active=old;var pixels=read.GetPixels32();
            float sum=0,top=0,bottom=0,min=1,max=0,error=0;int topCount=0,bottomCount=0;
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++){var c=pixels[y*image.width+x];float v=c.r/255f;sum+=v;min=Mathf.Min(min,v);max=Mathf.Max(max,v);error=Mathf.Max(error,Mathf.Abs(c.r-c.g)/255f,Mathf.Abs(c.r-c.b)/255f);if(y>image.height*.75f){top+=v;topCount++;}if(y<image.height*.25f){bottom+=v;bottomCount++;}}
            Destroy(read);return new Pixels{mean=sum/pixels.Length,top=top/topCount,bottom=bottom/bottomCount,range=max-min,colorError=error};
        }
        void Check(bool valid,string message){if(!valid)report.errors.Add(message);}
        void Fail(){report.status="failed";Publish();}
        void Publish()
        {
            string json=JsonUtility.ToJson(report,true);
            #if UNITY_WEBGL&&!UNITY_EDITOR
            SUR007_Report(json);
            #else
            Debug.Log("SUR-007 Web diagnostic: "+json);
            #endif
        }
        #if UNITY_WEBGL&&!UNITY_EDITOR
        [DllImport("__Internal")]static extern void SUR007_Report(string json);
        #endif
        void OnDisable(){if(host==null)return;host.Owner.StepEntered-=OnStep;host.Owner.StateChanged-=OnState;}
    }
}
