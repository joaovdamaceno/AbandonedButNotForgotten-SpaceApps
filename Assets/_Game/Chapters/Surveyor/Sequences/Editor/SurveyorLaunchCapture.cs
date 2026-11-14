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
    /// <summary>Captura a UI real com o fluxo comum, sem alterar PlayerPrefs nem BuildSettings.</summary>
    public static class SurveyorLaunchCapture
    {
        sealed class MemorySettings : SettingsStore.IStorage
        {
            string value;
            public string Read(string key)=>value;
            public void Write(string key,string data)=>value=data;
            public void Delete(string key)=>value=null;
        }
        static Queue<(string state,string language,Vector2Int size)> cases;
        static (string state,string language,Vector2Int size) current;
        static SurveyorLaunchPreviewHost host;
        static PanelSettings panel;
        static RenderTexture target;
        static int frames;
        static readonly List<string> audit = new List<string>();

        public static void CaptureFromCommandLine()
        {
            cases=new Queue<(string,string,Vector2Int)>();audit.Clear();
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(960,600),new Vector2Int(1024,768)})
            foreach(var state in new[]{"entry","ready","pause","cruise"}) cases.Enqueue((state,"pt",size));
            cases.Enqueue(("ready","en",new Vector2Int(1280,720)));
            cases.Enqueue(("transit","en",new Vector2Int(1280,720)));
            Directory.CreateDirectory("QA/SUR-003/Captures");EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            try
            {
                if(host==null)
                {
                    if(cases.Count==0)
                    {
                        File.WriteAllLines("QA/SUR-003/layout-audit.txt",audit);
                        EditorApplication.update-=Tick;EditorApplication.Exit(0);return;
                    }
                    StartNext();
                }
                EditorApplication.QueuePlayerLoopUpdate();frames++;
                if(frames==6)
                {
                    var root=host.View.Root;
                    root.EnableInClassList(ResponsiveLayoutClass.NarrowClass,ResponsiveLayoutClass.IsNarrow(root.layout.width,1100));
                }
                if(frames<16)return;
                Save();Object.DestroyImmediate(host.gameObject.transform.root.gameObject);host=null;
                Object.DestroyImmediate(panel);target.Release();Object.DestroyImmediate(target);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.update-=Tick;EditorApplication.Exit(1);}
        }
        static void StartNext()
        {
            current=cases.Dequeue();frames=0;
            var scene=EditorSceneManager.OpenScene(SurveyorLaunchBuilder.Scene);
            foreach(var root in scene.GetRootGameObjects())
            {host=root.GetComponentInChildren<SurveyorLaunchPreviewHost>();if(host!=null)break;}
            if(host==null)throw new InvalidOperationException("Reconstrua SUR-003 antes de capturar.");
            var doc=host.GetComponent<UIDocument>();panel=Object.Instantiate(doc.panelSettings);
            target=new RenderTexture(current.size.x,current.size.y,0,RenderTextureFormat.ARGB32);target.Create();
            panel.targetTexture=target;panel.clearColor=true;panel.colorClearValue=new Color(.03f,.047f,.075f);doc.panelSettings=panel;
            var storage=new MemorySettings();storage.Write(SettingsStore.StorageKey,JsonUtility.ToJson(new SettingsData{languageCode=current.language}));
            host.Initialize(new SettingsStore(storage));
            if(current.state=="entry")return;
            host.Begin();Advance(17.1f);
            if(current.state=="pause") {host.Flow.BeginConfirmation();Advance(.8f);host.Flow.ReleaseConfirmation();host.Pause();}
            if(current.state=="transit" || current.state=="cruise")
            {host.Flow.BeginConfirmation();Advance(2.55f);host.Flow.ReleaseConfirmation();Advance(current.state=="transit"?2.95f:10);}
            host.View.Refresh();
        }
        static void Advance(float seconds)
        {
            while(seconds>.0001f)
            {float delta=Mathf.Min(.05f,seconds);host.Clock.Tick(delta);host.Interactions.Tick(host.Clock.ActiveDeltaSeconds);host.Flow.Tick(host.Clock.ActiveDeltaSeconds);seconds-=delta;}
        }
        static void Save()
        {
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=previous;
            string id=$"{current.state}-{current.language}-{target.width}x{target.height}";
            File.WriteAllBytes("QA/SUR-003/Captures/"+id+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
            var root=host.View.Root;var bounds=root.worldBound;
            string[] names=current.state=="entry"?new[]{"entry-title","start-button"}:new[]{"command-panel"};
            foreach(string name in names)
            {
                var b=root.Q(name).worldBound;
                bool inside=b.xMin>=bounds.xMin-1 && b.yMin>=bounds.yMin-1 && b.xMax<=bounds.xMax+1 && b.yMax<=bounds.yMax+1;
                audit.Add($"{id} {name} bounds={b} inside={inside}");
                if(!inside)throw new InvalidOperationException(id+": "+name+" fora da tela.");
            }
            if(current.state=="pause")
            {
                var center=root.Q(className:"launch-pause-card").worldBound.center;
                float error=Vector2.Distance(bounds.center,center);audit.Add($"{id} pause-center-error={error:F3}");
                if(error>1)throw new InvalidOperationException("Pausa não centralizada.");
            }
            Debug.Log("SUR-003 capture "+id);
        }
    }
}
