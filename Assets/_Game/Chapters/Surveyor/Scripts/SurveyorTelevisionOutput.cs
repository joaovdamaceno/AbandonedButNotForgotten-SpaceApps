using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>One reusable image buffer, separate from the viewport source and from the UI.</summary>
    [DisallowMultipleComponent]
    public sealed class SurveyorTelevisionOutput:MonoBehaviour,IDisposable
    {
        public const int Width=640,Height=480,ScanLines=200;
        static readonly int ProgressId=Shader.PropertyToID("_Progress");
        SurveyorSiteViewport viewport;Material material;RenderTexture texture;
        public RenderTexture Texture=>texture;
        public RenderTexture Source=>viewport!=null?viewport.Texture:null;
        public float Progress{get;private set;}
        public int Allocations{get;private set;}
        public int Releases{get;private set;}
        public event Action Changed;
        public void Configure(SurveyorSiteViewport source,Material template)
        {
            Dispose();viewport=source;
            if(source==null || template==null || template.shader==null)throw new ArgumentException("TV output references missing.");
            material=new Material(template){name="Surveyor TV scan instance"};
        }
        public bool Begin()
        {
            if(material==null || Source==null)return false;
            if(texture==null){texture=new RenderTexture(Width,Height,0,RenderTextureFormat.ARGB32){name="Surveyor TV scanned 640x480",useMipMap=false,antiAliasing=1};texture.Create();Allocations++;}
            SetProgress(0);RenderFrame();Changed?.Invoke();return true;
        }
        public void SetProgress(float value){Progress=Mathf.Clamp01(value);}
        public void RenderFrame(){if(texture!=null && Source!=null && material!=null){material.SetFloat(ProgressId,Progress);Graphics.Blit(Source,texture,material);}}
        void LateUpdate()=>RenderFrame();
        public void Release()
        {
            Progress=0;if(texture==null)return;texture.Release();DestroyOwned(texture);texture=null;Releases++;Changed?.Invoke();
        }
        public void Dispose(){Release();if(material!=null){DestroyOwned(material);material=null;}}
        static void DestroyOwned(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void OnDisable()=>Dispose();
    }
}
