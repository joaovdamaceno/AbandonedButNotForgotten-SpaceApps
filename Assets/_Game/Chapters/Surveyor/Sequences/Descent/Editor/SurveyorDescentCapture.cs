using System;
using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public static class SurveyorDescentCapture
    {
        sealed class Memory:SettingsStore.IStorage
        {public string data;public string Read(string key)=>data;public void Write(string key,string value)=>data=value;public void Delete(string key)=>data=null;}
        static Queue<(string state,string lang,Vector2Int size)> cases;
        static (string state,string lang,Vector2Int size) current;
        static SurveyorDescentPreviewHost host;static PanelSettings panel;static RenderTexture target;static int frames;
        static readonly List<string> audit=new List<string>();
        public static void CaptureFromCommandLine()
        {
            cases=new Queue<(string,string,Vector2Int)>();audit.Clear();
            foreach(string state in new[]{"approach","anomaly","armed","first-rebound","second-rebound","team-receipt","settled","complete"})cases.Enqueue((state,"pt",new Vector2Int(1280,720)));
            cases.Enqueue(("reduced-motion","pt",new Vector2Int(960,600)));cases.Enqueue(("pause","pt",new Vector2Int(960,600)));cases.Enqueue(("anomaly","en",new Vector2Int(1024,768)));
            Directory.CreateDirectory("QA/SUR-006/Captures");EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try
            {
                if(host==null)
                {
                    if(cases.Count==0){File.WriteAllLines("QA/SUR-006/layout-audit.txt",audit);EditorApplication.update-=Tick;EditorApplication.Exit(0);return;}
                    StartNext();
                }
                frames++;EditorApplication.QueuePlayerLoopUpdate();
                if(frames==6){var r=host.View.Root;r.EnableInClassList(ResponsiveLayoutClass.NarrowClass,ResponsiveLayoutClass.IsNarrow(r.layout.width,1100));}
                if(frames<18)return;Save();host.Gate.Dispose();host.Flow.Dispose();Object.DestroyImmediate(host.transform.root.gameObject);host=null;
                Object.DestroyImmediate(panel);target.Release();Object.DestroyImmediate(target);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
        }
        static void StartNext()
        {
            current=cases.Dequeue();frames=0;var scene=EditorSceneManager.OpenScene(SurveyorDescentBuilder.PreviewScene);
            foreach(var root in scene.GetRootGameObjects()){host=root.GetComponentInChildren<SurveyorDescentPreviewHost>();if(host!=null)break;}
            var doc=host.GetComponent<UIDocument>();panel=Object.Instantiate(doc.panelSettings);
            target=new RenderTexture(current.size.x,current.size.y,0,RenderTextureFormat.ARGB32);target.Create();panel.targetTexture=target;panel.clearColor=true;panel.colorClearValue=new Color(.03f,.047f,.075f);doc.panelSettings=panel;
            host.Initialize(new SettingsStore(new Memory{data=JsonUtility.ToJson(new SettingsData{languageCode=current.lang,masterVolume=0,reducedMotion=current.state=="reduced-motion"})}));host.Begin();
            if(current.state=="approach"){Advance(4);return;}Advance(12.1f);
            if(current.state=="anomaly")return;
            if(current.state=="armed"){host.Action.BeginConfirmation();Advance(3.05f);host.Action.ReleaseConfirmation();Advance(1.35f);return;}
            float time=current.state=="first-rebound"?22:current.state=="second-rebound"?29:current.state=="team-receipt"?28.5f:
                current.state=="settled"?32.2f:current.state=="complete"?40.2f:18.1f;
            Advance(time-host.Flow.TimeSeconds);if(current.state=="pause")host.Pause();
        }
        static void Advance(float seconds)
        {while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);host.Clock.Tick(d);host.Service.Tick(host.Clock.ActiveDeltaSeconds);host.Queue.Tick();host.Flow.Tick(host.Clock.ActiveDeltaSeconds);seconds-=d;}}
        static void Save()
        {
            string id=$"{current.state}-{current.lang}-{target.width}x{target.height}";var old=RenderTexture.active;RenderTexture.active=target;
            var png=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,target.width,target.height),0,0);png.Apply();RenderTexture.active=old;
            File.WriteAllBytes("QA/SUR-006/Captures/"+id+".png",png.EncodeToPNG());Object.DestroyImmediate(png);
            var root=host.View.Root;var bounds=root.worldBound;
            foreach(string name in new[]{"instrument","diagram","contacts","subtitle-plate"})
            {
                if(root.Q(name).resolvedStyle.display==DisplayStyle.None){audit.Add($"{id} {name} hidden");continue;}
                var b=root.Q(name).worldBound;bool inside=b.xMin>=bounds.xMin-1&&b.yMin>=bounds.yMin-1&&b.xMax<=bounds.xMax+1&&b.yMax<=bounds.yMax+1;
                audit.Add($"{id} {name} {b} inside={inside}");if(!inside)throw new InvalidOperationException(id+" "+name+" fora da tela.");
            }
            if(current.state=="pause"){float error=Vector2.Distance(bounds.center,root.Q("pause-card").worldBound.center);audit.Add($"{id} pause-center-error={error:F3}");if(error>1)throw new InvalidOperationException("Pausa fora do centro.");}
            audit.Add($"{id} contacts={host.Flow.ContactCount} settled={host.Flow.Settled} engineOff={host.Flow.EnginesCutOff} camera={host.Gate.Active} offset={host.View.DiagramOffset}");Debug.Log("SUR-006 capture "+id);
        }
    }
}
