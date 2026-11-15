using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EclipseTests
{
    public sealed class EclipseIntegrationTests
    {
        [UnityTest] public IEnumerator RealSceneRendersBothQualitiesRestartsAndReleasesBuffers()
        {
            EditorSceneManager.OpenScene(SurveyorEclipseBuilder.PreviewScene); yield return new EnterPlayMode();
            var host=Object.FindFirstObjectByType<SurveyorEclipsePreview>(); host.Initialize();
            // Keep consumers bound. OnDisable intentionally cancels a running module.
            host.Clock.enabled=false;
            int originalQuality=QualitySettings.GetQualityLevel();
            var physicalPosition=host.Flow.TelevisionCamera.transform.position;
            var bodyRotation=host.Site.MirrorAzimuth.parent.rotation;
            var audits=new List<string>();
            for(int cycle=0;cycle<3;cycle++)
            {
                if(cycle==0) host.Begin(); else host.Restart();
                Assert.That(host.Flow.BeginConfirmation(),Is.True,$"step={host.Owner.CurrentStepId}, phase={host.Flow.Phase}, queue={host.Queue.Status}, release={host.Action.NeedsRelease}"); Advance(host,2.2f);
                Assert.That(host.Flow.Phase,Is.EqualTo(EclipsePhase.Viewing));
                Advance(host,4); yield return Capture(host,"raise-"+cycle,1280,720,audits);
                Advance(host,5);
                if(cycle==0)
                {
                    yield return Capture(host,"color-pt",960,600,audits);
                    host.ApplySettings(new SettingsData { languageCode="en" });
                    yield return Capture(host,"color-en",960,600,audits);
                }
                Advance(host,7);
                if(cycle==0)
                {
                    foreach(int quality in new[]{0,Mathf.Min(1,QualitySettings.names.Length-1)})
                    {
                        QualitySettings.SetQualityLevel(quality,true);
                        yield return Capture(host,"totality-quality-"+quality,1280,720,audits);
                        Assert.That(host.Subtitles.CurrentText,Is.Empty);
                        Assert.That(host.Document.rootVisualElement.Q<Label>("eclipse-status").resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
                    }
                    host.Pause(); float time=host.Flow.SequenceTime;
                    Advance(host,4); Assert.That(host.Flow.SequenceTime,Is.EqualTo(time));
                    yield return Capture(host,"pause",1024,768,audits); host.Resume();
                }
                Assert.That(Vector3.Distance(physicalPosition,host.Flow.TelevisionCamera.transform.position),Is.LessThan(.0001));
                Assert.That(Quaternion.Angle(bodyRotation,host.Site.MirrorAzimuth.parent.rotation),Is.LessThan(.0001));
                Advance(host,10.2f); Assert.That(host.Owner.CurrentStepId,Is.EqualTo("LunarNight"));
                Assert.That(host.Document.rootVisualElement.Q("eclipse-end").style.display.value,Is.EqualTo(DisplayStyle.Flex));
                Assert.That(host.Site.CameraDirector.IsControlled,Is.False);
                Assert.That(host.Output.Allocations,Is.EqualTo(1));
                if(cycle==0)
                {
                    host.ApplySettings(new SettingsData { languageCode="en",reducedMotion=true });
                    yield return Capture(host,"complete-en",1280,720,audits);
                }
            }
            QualitySettings.SetQualityLevel(originalQuality,true);
            host.Return(); host.Dispose(); host.Viewport.enabled=false;
            yield return null;
            Assert.That(host.Output.Texture,Is.Null); Assert.That(host.Viewport.Texture,Is.Null);
            Directory.CreateDirectory("QA/SUR-011"); File.WriteAllLines("QA/SUR-011/layout-audit.txt",audits);
            File.WriteAllText("QA/SUR-011/runtime-metrics.json",JsonUtility.ToJson(new Metrics { utc=DateTime.UtcNow.ToString("O"),cycles=3,restarts=2,
                fixedOpticalPoint=true,bodyUnchanged=true,outputAllocations=host.Output.Allocations,outputReleases=host.Output.Releases,sourceReleased=true },true));
            yield return new ExitPlayMode();
        }
        [Serializable] sealed class Metrics { public string utc; public int cycles,restarts,outputAllocations,outputReleases; public bool fixedOpticalPoint,bodyUnchanged,sourceReleased; }
        public static void Advance(SurveyorEclipsePreview host,float seconds)
        { while(seconds>.00001f) { float dt=Mathf.Min(.05f,seconds); host.Clock.Tick(dt); host.Service.Tick(host.Clock.ActiveDeltaSeconds); host.Queue.Tick(); host.Flow.Tick(host.Clock.ActiveDeltaSeconds); seconds-=dt; } host.Clock.Tick(0); }
        static IEnumerator Capture(SurveyorEclipsePreview host,string name,int width,int height,List<string> audits)
        {
            var doc=host.Document; var original=doc.panelSettings; var panel=Object.Instantiate(original);
            var target=new RenderTexture(width,height,0); target.Create(); panel.targetTexture=target; panel.clearColor=true; panel.colorClearValue=new Color(.02f,.03f,.04f); doc.panelSettings=panel;
            for(int i=0;i<20;i++) yield return null;
            host.Refresh(); yield return null;
            var previous=RenderTexture.active; RenderTexture.active=target; var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); RenderTexture.active=previous;
            if(name.StartsWith("totality"))
            {
                int red=0; foreach(var pixel in image.GetPixels32()) if(pixel.r>35 && pixel.r>pixel.g*1.5f && pixel.r>pixel.b*1.5f) red++;
                Assert.That(red,Is.GreaterThan(20),"The actual GPU output must contain the red atmospheric rim.");
            }
            Directory.CreateDirectory("QA/SUR-011/Captures"); File.WriteAllBytes($"QA/SUR-011/Captures/{name}-{width}x{height}.png",image.EncodeToPNG()); Object.Destroy(image);
            var bounds=doc.rootVisualElement.worldBound; var frame=doc.rootVisualElement.Q("eclipse-frame").worldBound;
            Assert.That(frame.width/frame.height,Is.EqualTo(4f/3).Within(.01));
            Assert.That(frame.xMin>=bounds.xMin && frame.yMin>=bounds.yMin && frame.xMax<=bounds.xMax+.1f && frame.yMax<=bounds.yMax+.1f,Is.True,"TV image outside canvas");
            if(name=="pause")
            { var card=doc.rootVisualElement.Q("eclipse-pause").Children().GetEnumerator(); card.MoveNext(); Assert.That(Vector2.Distance(bounds.center,card.Current.worldBound.center),Is.LessThan(1)); }
            audits.Add($"{name} {width}x{height}: frame={frame}, canvas={bounds}, 4:3=true");
            doc.panelSettings=original; target.Release(); Object.Destroy(target); Object.Destroy(panel); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if(Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
