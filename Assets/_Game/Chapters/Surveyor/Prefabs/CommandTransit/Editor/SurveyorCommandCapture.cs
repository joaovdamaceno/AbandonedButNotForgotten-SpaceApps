using System;
using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public static class SurveyorCommandCapture
    {
        sealed class MemorySettings : SettingsStore.IStorage
        {
            public string data;
            public string Read(string key)=>data;
            public void Write(string key,string value)=>data=value;
            public void Delete(string key)=>data=null;
        }
        static Queue<(string state,string lang,Vector2Int size)> cases;
        static (string state,string lang,Vector2Int size) current;
        static SurveyorCommandPreviewHost host;
        static PanelSettings panel;
        static RenderTexture target;
        static int frames;
        static readonly List<string> audit=new List<string>();
        public static void CaptureFromCommandLine()
        {
            cases=new Queue<(string,string,Vector2Int)>();audit.Clear();
            foreach(string state in new[]{"ready","sent","received"})cases.Enqueue((state,"pt",new Vector2Int(1280,720)));
            cases.Enqueue(("pause","pt",new Vector2Int(960,600)));
            cases.Enqueue(("ready","en",new Vector2Int(1024,768)));
            cases.Enqueue(("sent","en",new Vector2Int(1280,720)));
            Directory.CreateDirectory("QA/SUR-004/Captures");EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try
            {
                if(host==null)
                {
                    if(cases.Count==0)
                    {File.WriteAllLines("QA/SUR-004/layout-audit.txt",audit);EditorApplication.update-=Tick;EditorApplication.Exit(0);return;}
                    StartNext();
                }
                EditorApplication.QueuePlayerLoopUpdate();frames++;
                if(frames==6)
                {
                    var root=host.GetComponent<UIDocument>().rootVisualElement;
                    root.EnableInClassList(ResponsiveLayoutClass.NarrowClass,ResponsiveLayoutClass.IsNarrow(root.layout.width,1100));
                }
                if(frames<16)return;
                Save();Object.DestroyImmediate(host.transform.root.gameObject);host=null;
                Object.DestroyImmediate(panel);target.Release();Object.DestroyImmediate(target);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
        }
        static void StartNext()
        {
            current=cases.Dequeue();frames=0;
            var scene=EditorSceneManager.OpenScene(SurveyorCommandBuilder.PreviewScene);
            foreach(var root in scene.GetRootGameObjects())
            {host=root.GetComponentInChildren<SurveyorCommandPreviewHost>();if(host!=null)break;}
            if(host==null)throw new InvalidOperationException("Reconstrua a cena SUR-004.");
            var doc=host.GetComponent<UIDocument>();panel=Object.Instantiate(doc.panelSettings);
            target=new RenderTexture(current.size.x,current.size.y,0,RenderTextureFormat.ARGB32);target.Create();
            panel.targetTexture=target;panel.clearColor=true;panel.colorClearValue=new Color(.03f,.047f,.075f);doc.panelSettings=panel;
            var store=new MemorySettings{data=JsonUtility.ToJson(new SettingsData{languageCode=current.lang})};
            host.Initialize(new SettingsStore(store));host.BeginOrRestart();host.View.SetAim(host.DesiredDegrees,host.ActualDegrees);
            if(current.state=="ready")return;
            host.Action.BeginConfirmation();Advance(1.55f);host.Action.ReleaseConfirmation();host.SetDesired(-30);
            if(current.state=="pause") {Advance(.6f-host.Queue.ElapsedSeconds);host.Pause();}
            else Advance(current.state=="received"?1.3f:.35f);
        }
        static void Advance(float seconds)
        {
            while(seconds>.0001f)
            {float d=Mathf.Min(.05f,seconds);host.Clock.Tick(d);host.Interactions.Tick(host.Clock.ActiveDeltaSeconds);host.Queue.Tick();seconds-=d;}
        }
        static void Save()
        {
            string id=$"{current.state}-{current.lang}-{target.width}x{target.height}";
            var previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=previous;
            File.WriteAllBytes("QA/SUR-004/Captures/"+id+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            var root=host.GetComponent<UIDocument>().rootVisualElement;var bounds=root.worldBound;
            foreach(string name in new[]{"command-widget","confirm-command","hold-help","start"})
            {
                var box=root.Q(name).worldBound;
                bool inside=box.xMin>=bounds.xMin-1 && box.yMin>=bounds.yMin-1 && box.xMax<=bounds.xMax+1 && box.yMax<=bounds.yMax+1;
                audit.Add($"{id} {name} bounds={box} inside={inside}");
                if(!inside)throw new InvalidOperationException(id+": "+name+" fora da tela.");
            }
            if(current.state=="pause")
            {
                float error=Vector2.Distance(bounds.center,root.Q("pause-card").worldBound.center);
                audit.Add($"{id} pause-center-error={error:F3}");
                if(error>1)throw new InvalidOperationException("Pausa fora do centro.");
            }
            audit.Add($"{id} desired={host.DesiredDegrees} received={host.ActualDegrees} elapsed={host.Queue.ElapsedSeconds} bindingFrames={host.View.LastFeedbackFrames}");
            Debug.Log("SUR-004 capture "+id);
        }
    }
}
