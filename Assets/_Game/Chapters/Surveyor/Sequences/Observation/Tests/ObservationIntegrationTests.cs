using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Abandoned.SpaceApps.EditorTools;
using Abandoned.SpaceApps.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.ObservationTests
{
    public sealed class ObservationIntegrationTests
    {
        [UnityTest]public IEnumerator LandingFirstImageAndThreeRealPicturesSurviveTwoRestarts()
        {
            EditorSceneManager.OpenScene(SurveyorObservationBuilder.PreviewScene,OpenSceneMode.Single);
            yield return new EnterPlayMode();
            var host=Object.FindFirstObjectByType<SurveyorObservationPreviewHost>();var root=host.gameObject;
            host.Initialize();var preceding=host.Preceding;preceding.Clock.enabled=false;
            var hashes=new HashSet<string>();var layout=new List<string>();var images=new List<Sprite>();
            var origin=preceding.Gate.TelevisionCamera.transform.position;
            for(int cycle=0;cycle<3;cycle++)
            {
                if(cycle==0)preceding.Begin();else preceding.Restart();
                Advance(host,40.2f);Assert.That(preceding.Action.BeginConfirmation(),Is.True,$"step={preceding.Owner.CurrentStepId} tv={preceding.Flow.Phase} queue={preceding.Queue.Status} canSend={preceding.Queue.CanSend} release={preceding.Action.NeedsRelease} candidate={preceding.Service.TryGetActive(out var active)}:{active.Id}");Advance(host,3.4f);Advance(host,10.1f);
                Assert.That(preceding.Owner.CurrentStepId,Is.EqualTo("Observe"));
                Assert.That(preceding.Output.Texture,Is.Not.Null);yield return null;yield return null;
                for(int n=0;n<3;n++)
                {
                    Assert.That(host.Flow.AssistFrame(),Is.True);Advance(host,7);Assert.That(host.Flow.Aligned,Is.True);
                    Assert.That(preceding.Gate.TelevisionCamera.transform.position,Is.EqualTo(origin));
                    yield return null;yield return null;
                    if(cycle==0)
                    {
                        yield return Capture(host,"target-"+n,1280,720,layout);
                        if(n==1)
                        {
                            Assert.That(host.Flow.ChangeLens(),Is.True);Advance(host,3);yield return null;yield return null;
                            yield return Capture(host,"detail-100mm",960,600,layout);
                            Assert.That(host.Flow.ChangeLens(),Is.True);Advance(host,3);yield return null;yield return null;
                        }
                    }
                    Assert.That(host.Flow.BeginPrimary(),Is.True);Advance(host,1.55f);host.Flow.ReleasePrimary();Advance(host,1.35f);
                    Assert.That(host.Flow.Phase,Is.EqualTo(ObservationPhase.Result));Assert.That(host.Flow.RecordCount,Is.EqualTo(n+1));
                    var photo=host.Flow.Result.Thumbnail;images.Add(photo);
                    Assert.That(photo.rect.width,Is.EqualTo(640));Assert.That(photo.rect.height,Is.EqualTo(480));
                    if(cycle==0)
                    {
                        byte[] pixels=photo.texture.GetRawTextureData();string hash=BitConverter.ToString(SHA256.Create().ComputeHash(pixels));
                        Assert.That(hashes.Add(hash),Is.True,"Each view must produce its own picture, not a reused thumbnail.");
                        Directory.CreateDirectory("QA/SUR-008/Captures");File.WriteAllBytes($"QA/SUR-008/Captures/photo-{n}.png",photo.texture.EncodeToPNG());
                        yield return Capture(host,"result-"+n,1280,720,layout);
                    }
                    Assert.That(host.Flow.BeginPrimary(),Is.True);Advance(host,.05f);
                    if(cycle==0&&n==0)
                    {
                        Assert.That(host.Flow.Review(0),Is.True);yield return Capture(host,"review",960,600,layout);
                        Assert.That(host.Flow.BeginPrimary(),Is.True);Advance(host,.05f);Assert.That(host.Flow.RecordCount,Is.EqualTo(1));
                    }
                }
                Assert.That(preceding.Owner.CurrentStepId,Is.EqualTo("Sample"));Assert.That(preceding.Owner.Evidence.PlayerPhotoCount,Is.EqualTo(3));
                if(cycle==0)
                {
                    host.View.ApplySettings(new SettingsData{languageCode="en"});yield return Capture(host,"complete-en",1024,768,layout);
                    preceding.Pause();yield return Capture(host,"pause",960,600,layout);preceding.Resume();
                }
            }
            preceding.Return();yield return null;yield return null;
            foreach(var image in images)Assert.That(image==null,Is.True,"A restart or return must destroy its runtime photograph.");
            Assert.That(preceding.Output.Texture,Is.Null);Assert.That(preceding.Output.Source,Is.Null);
            Assert.That(host.Flow.RecordCount,Is.Zero);Assert.That(host.Flow.CaptureAllocations,Is.EqualTo(9));
            Directory.CreateDirectory("QA/SUR-008");File.WriteAllLines("QA/SUR-008/layout-audit.txt",layout);
            File.WriteAllText("QA/SUR-008/runtime-metrics.json",JsonUtility.ToJson(new Metrics{utc=DateTime.UtcNow.ToString("O"),cycles=3,restarts=2,captures=host.Flow.CaptureAllocations,uniqueFirstCycleImages=hashes.Count,observerFixed=true,photosReleased=true,sourceReleased=true},true));
            host.Dispose();preceding.Dispose();Object.Destroy(root);yield return null;yield return new ExitPlayMode();
        }
        [Serializable]sealed class Metrics{public string utc;public int cycles,restarts,captures,uniqueFirstCycleImages;public bool observerFixed,photosReleased,sourceReleased;}
        public static void Advance(SurveyorObservationPreviewHost host,float seconds)
        {
            var h=host.Preceding;
            while(seconds>.0001f){float dt=Mathf.Min(.05f,seconds);h.Clock.Tick(dt);h.Service.Tick(h.Clock.ActiveDeltaSeconds);h.Queue.Tick();h.Descent.Tick(h.Clock.ActiveDeltaSeconds);h.Flow.Tick(h.Clock.ActiveDeltaSeconds);host.Flow.Tick(h.Clock.ActiveDeltaSeconds);seconds-=dt;}
            h.Clock.Tick(0);
        }
        static IEnumerator Capture(SurveyorObservationPreviewHost host,string name,int width,int height,List<string> audit)
        {
            var doc=host.Document;var original=doc.panelSettings;var panel=Object.Instantiate(original);
            var target=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32);target.Create();panel.targetTexture=target;panel.clearColor=true;panel.colorClearValue=new Color(.03f,.047f,.075f);doc.panelSettings=panel;
            for(int i=0;i<20;i++){if(i==6){var r=host.View.Root;r.EnableInClassList(ResponsiveLayoutClass.NarrowClass,ResponsiveLayoutClass.IsNarrow(r.layout.width,1100));}yield return null;}
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();RenderTexture.active=previous;
            Directory.CreateDirectory("QA/SUR-008/Captures");File.WriteAllBytes($"QA/SUR-008/Captures/{name}-{width}x{height}.png",image.EncodeToPNG());Object.Destroy(image);
            var bounds=host.View.Root.worldBound;
            foreach(var id in new[]{"obs-screen","obs-primary","obs-assist","obs-lens","obs-result-card","obs-pause-card"})
            {
                var e=host.View.Root.Q(id);if(!e.visible||e.resolvedStyle.display==DisplayStyle.None)continue;
                // A parent hidden by display:none can retain its old child's geometry.
                bool hidden=false;for(var parent=e.parent;parent!=null;parent=parent.parent)if(parent.resolvedStyle.display==DisplayStyle.None)hidden=true;if(hidden)continue;
                var b=e.worldBound;bool inside=b.xMin>=bounds.xMin-1&&b.yMin>=bounds.yMin-1&&b.xMax<=bounds.xMax+1&&b.yMax<=bounds.yMax+1;
                audit.Add($"{name} {width}x{height} {id} {b} inside={inside}");Assert.That(inside,Is.True,id+" outside panel.");
            }
            if(name=="pause")Assert.That(Vector2.Distance(bounds.center,host.View.Root.Q("obs-pause-card").worldBound.center),Is.LessThan(1));
            doc.panelSettings=original;target.Release();Object.Destroy(target);Object.Destroy(panel);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
