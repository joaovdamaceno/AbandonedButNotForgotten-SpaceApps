using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.TelevisionTests
{
    public sealed class TelevisionShaderTests
    {
        [Test]public void ScanRevealsFromTopToBottomAndEndsMonochrome()
        {
            var material=new Material(Shader.Find("Abandoned/Surveyor/Scan"));Assert.That(material.shader.isSupported,Is.True);
            var source=new Texture2D(64,48,TextureFormat.RGBA32,false,true);var pixels=new Color[64*48];
            for(int y=0;y<48;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color(.3f+y/100f,.16f+x/170f,.08f,1);
            source.SetPixels(pixels);source.Apply();var target=RenderTexture.GetTemporary(64,48,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            Color Read(int x,int y){var old=RenderTexture.active;RenderTexture.active=target;var t=new Texture2D(64,48,TextureFormat.RGBA32,false,true);t.ReadPixels(new Rect(0,0,64,48),0,0);t.Apply();var c=t.GetPixel(x,y);RenderTexture.active=old;Object.DestroyImmediate(t);return c;}
            material.SetFloat("_Progress",0);Graphics.Blit(source,target,material);float baseline=Read(20,10).r;Assert.That(baseline,Is.LessThan(.03f));
            material.SetFloat("_Progress",.5f);Graphics.Blit(source,target,material);Assert.That(Read(20,38).r,Is.GreaterThan(baseline+.1f));Assert.That(Read(20,10).r,Is.EqualTo(baseline).Within(.01));
            material.SetFloat("_Progress",1);Graphics.Blit(source,target,material);var full=Read(20,10);Assert.That(full.r,Is.GreaterThan(baseline+.1f));Assert.That(full.r,Is.EqualTo(full.g).Within(.005));Assert.That(full.r,Is.EqualTo(full.b).Within(.005));
            RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(source);Object.DestroyImmediate(material);
        }
        [Test]public void ScanMaterialDoesNotDependOnUiOrExternalTextures()
        {
            string[] dependencies=AssetDatabase.GetDependencies(Abandoned.SpaceApps.EditorTools.SurveyorTelevisionBuilder.MaterialPath);
            Assert.That(dependencies.Length,Is.EqualTo(2));Assert.That(dependencies[0]+dependencies[1],Does.Contain("SurveyorScan.shader"));
        }
    }
}
