using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Abandoned.SpaceApps.Opportunity
{
    public static class FrameCaptureUtility
    {
        public const int MaximumWidth=960, MaximumHeight=540;
        // Render at album resolution, rather than allocating a full-window screenshot
        // first. On a 4K display the latter used over 60 MB for one temporary copy.
        public static Texture2D Photograph(Camera camera=null)
        {
            if(!camera)camera=Camera.main;
            if(!camera)throw new System.InvalidOperationException("A mission camera is required to record an observation.");
            float aspect=Mathf.Max(.1f,camera.aspect);
            int width=Mathf.Max(1,Mathf.Min(MaximumWidth,Mathf.RoundToInt(MaximumHeight*aspect)));
            int height=Mathf.Max(1,Mathf.RoundToInt(width/aspect));
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;var previousTarget=camera.targetTexture;bool previousSRGB=GL.sRGBWrite;
            Texture2D result=null;
            try
            {
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                if(GraphicsSettings.currentRenderPipeline&&RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);
                else {camera.targetTexture=target;camera.Render();}
                RenderTexture.active=target;
                result=new Texture2D(width,height,TextureFormat.RGBA32,false,false){name="Recorded mission observation",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                result.ReadPixels(new Rect(0,0,width,height),0,0,false);result.Apply(false,false);
                return result;
            }
            catch{if(result)Object.Destroy(result);throw;}
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previous;GL.sRGBWrite=previousSRGB;RenderTexture.ReleaseTemporary(target);}
        }
    }
}
