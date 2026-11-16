using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.EditorTools;
using Abandoned.SpaceApps.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.TelevisionTests
{
    public sealed class TelevisionIntegrationTests
    {
        sealed class Memory:SettingsStore.IStorage
        {string data=JsonUtility.ToJson(new SettingsData{masterVolume=0,reducedMotion=true});public string Read(string key)=>data;public void Write(string key,string value)=>data=value;public void Delete(string key)=>data=null;}
        sealed class Delivery
        {readonly SurveyorTelevisionPreviewHost host;public float sent,received;public Delivery(SurveyorTelevisionPreviewHost h){host=h;}public void Sent(SurveyorCommandReceipt r){if(r.CommandId=="tv.firstImage")sent=host.Clock.ActiveSeconds;}public void Received(SurveyorCommandReceipt r){if(r.CommandId=="tv.firstImage")received=host.Clock.ActiveSeconds;}}
        [Serializable]sealed class Metrics{public string utc,finalStep;public float transitActiveSeconds;public bool pausePreserved,observerPositionPreserved;public int allocations,releases,restarts;}
        [Test]public void RebuildPreservesSceneGuidsReferencesAndCameraCount()
        {
            var a=SpaceAppsSceneBuilder.Run(new SurveyorTelevisionBuilder());Assert.That(a.Succeeded,Is.True,a.ToString());
            string before=File.ReadAllText(SurveyorTelevisionBuilder.PreviewScene),guid=AssetDatabase.AssetPathToGUID(SurveyorTelevisionBuilder.SequencePath);
            var b=SpaceAppsSceneBuilder.Run(new SurveyorTelevisionBuilder());Assert.That(b.Succeeded,Is.True,b.ToString());Assert.That(File.ReadAllText(SurveyorTelevisionBuilder.PreviewScene),Is.EqualTo(before));
            Assert.That(AssetDatabase.AssetPathToGUID(SurveyorTelevisionBuilder.SequencePath),Is.EqualTo(guid));var p=AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorTelevisionBuilder.PrefabPath);
            Assert.That(SpaceAppsSceneBuilder.ValidateHierarchy(p),Is.Empty);Assert.That(p.GetComponentsInChildren<AudioListener>(true).Length,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator RealPrefabReceivesScansPausesAndReusesThePictureAcrossHandoff()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var scene=SceneManager.CreateScene("SurveyorFirstImagePreview");var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorTelevisionBuilder.PrefabPath));SceneManager.MoveGameObjectToScene(root,scene);
            var host=root.GetComponent<SurveyorTelevisionPreviewHost>();host.Initialize(new SettingsStore(new Memory()));yield return null;
            Assert.That(host.Owner.State,Is.EqualTo(AppState.Dossier),"The preview waits for an explicit click.");
            var observation=new Delivery(host);host.Queue.Sent+=observation.Sent;host.Queue.Received+=observation.Received;
            host.Begin();Advance(host,30);Assert.That(host.Output.Source,Is.Null);Assert.That(host.Output.Texture,Is.Null);Advance(host,10.2f);
            host.Clock.enabled=false;host.Clock.Tick(0);Assert.That(host.Flow.Phase,Is.EqualTo(TelevisionPhase.AwaitingOrder));
            var audit=new List<string>();yield return Capture(host,"before",1280,720,audit);
            Assert.That(host.Action.BeginConfirmation(),Is.True);Advance(host,2.05f);host.Action.ReleaseConfirmation();Advance(host,.6f);
            Assert.That(host.Output.Source,Is.Null);host.Pause();float transit=host.Queue.ElapsedSeconds;yield return new WaitForSecondsRealtime(.2f);Assert.That(host.Queue.ElapsedSeconds,Is.EqualTo(transit));host.Resume();
            for(int i=0;i<20&&host.Flow.Phase!=TelevisionPhase.Scanning;i++)Advance(host,.05f);
            host.Clock.Tick(0);Assert.That(host.Flow.Phase,Is.EqualTo(TelevisionPhase.Scanning));Assert.That(host.Output.Progress,Is.Zero);
            Assert.That(observation.received-observation.sent,Is.InRange(1.2999f,1.36f));var source=host.Output.Source;var display=host.Output.Texture;var position=host.Gate.TelevisionCamera.transform.position;
            Assert.That(source.width,Is.EqualTo(640));Assert.That(display.height,Is.EqualTo(480));yield return Capture(host,"scan-start",1280,720,audit);
            Advance(host,2);host.Clock.Tick(0);Assert.That(host.Output.Progress,Is.EqualTo(.5f).Within(.02));yield return Capture(host,"half-scan",1280,720,audit);
            host.Pause();float scan=host.Output.Progress;yield return new WaitForSecondsRealtime(.25f);Assert.That(host.Output.Progress,Is.EqualTo(scan));yield return Capture(host,"pause",960,600,audit);host.Resume();
            Advance(host,2);host.Clock.Tick(0);Assert.That(host.Flow.Phase,Is.EqualTo(TelevisionPhase.Contemplation));Assert.That(host.Flow.SubtitleKey,Is.EqualTo("SUR-S10"));
            yield return Capture(host,"received",1280,720,audit);host.View.ApplySettings(new SettingsData{languageCode="en"});yield return Capture(host,"received-en",1024,768,audit);
            Advance(host,6.1f);host.Clock.Tick(0);Assert.That(host.Owner.CurrentStepId,Is.EqualTo("Observe"));Assert.That(host.Output.Texture,Is.SameAs(display));Assert.That(host.Output.Source,Is.SameAs(source));
            Assert.That(host.Gate.TelevisionCamera.transform.position,Is.EqualTo(position));Assert.That(host.Output.Allocations,Is.EqualTo(1));yield return Capture(host,"handoff",1280,720,audit);
            var metrics=new Metrics{utc=DateTime.UtcNow.ToString("O"),finalStep=host.Owner.CurrentStepId,transitActiveSeconds=observation.received-observation.sent,pausePreserved=true,observerPositionPreserved=true,restarts=3};
            Assert.That(host.Owner.CompleteStep("Observe","End"),Is.True);Assert.That(host.Output.Texture,Is.SameAs(display));Assert.That(host.Output.Source,Is.SameAs(source),"Later surface steps keep the same source.");
            for(int i=0;i<3;i++)
            {
                host.Restart();host.Clock.Tick(0);Assert.That(host.Output.Texture,Is.Null);Assert.That(host.Output.Source,Is.Null);yield return null;
                Advance(host,40.2f);Assert.That(host.Action.BeginConfirmation(),Is.True);Advance(host,3.4f);Advance(host,4.1f);host.Clock.Tick(0);yield return null;
                Assert.That(host.Output.Texture,Is.Not.Null);Assert.That(host.Output.Allocations,Is.EqualTo(i+2));
            }
            host.Return();yield return null;metrics.allocations=host.Output.Allocations;metrics.releases=host.Output.Releases;
            Assert.That(metrics.allocations,Is.EqualTo(4));Assert.That(metrics.releases,Is.EqualTo(4));Assert.That(host.Output.Source,Is.Null);
            Directory.CreateDirectory("QA/SUR-007");File.WriteAllText("QA/SUR-007/runtime-metrics.json",JsonUtility.ToJson(metrics,true));File.WriteAllLines("QA/SUR-007/layout-audit.txt",audit);
            host.Dispose();Object.Destroy(root);yield return null;yield return new ExitPlayMode();
        }
        static void Advance(SurveyorTelevisionPreviewHost h,float seconds){while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);h.Clock.Tick(d);h.Service.Tick(h.Clock.ActiveDeltaSeconds);h.Queue.Tick();h.Descent.Tick(h.Clock.ActiveDeltaSeconds);h.Flow.Tick(h.Clock.ActiveDeltaSeconds);seconds-=d;}h.Clock.Tick(0);}
        static IEnumerator Capture(SurveyorTelevisionPreviewHost host,string name,int width,int height,List<string> audit)
        {
            var doc=host.Document;var old=doc.panelSettings;var panel=Object.Instantiate(old);var target=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32);target.Create();panel.targetTexture=target;panel.clearColor=true;panel.colorClearValue=new Color(.03f,.047f,.075f);doc.panelSettings=panel;
            for(int i=0;i<20;i++){if(i==6){var r=host.View.Root;r.EnableInClassList(ResponsiveLayoutClass.NarrowClass,ResponsiveLayoutClass.IsNarrow(r.layout.width,1100));}yield return null;}
            var active=RenderTexture.active;RenderTexture.active=target;var png=new Texture2D(width,height,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,width,height),0,0);png.Apply();RenderTexture.active=active;
            Directory.CreateDirectory("QA/SUR-007/Captures");File.WriteAllBytes($"QA/SUR-007/Captures/{name}-{width}x{height}.png",png.EncodeToPNG());Object.Destroy(png);
            var root=host.View.Root;var bounds=root.worldBound;
            foreach(string element in new[]{"screen","instrument","subtitle-plate"})
            {
                var e=root.Q(element);if(e.resolvedStyle.display==DisplayStyle.None)continue;var b=e.worldBound;bool inside=b.xMin>=bounds.xMin-1&&b.yMin>=bounds.yMin-1&&b.xMax<=bounds.xMax+1&&b.yMax<=bounds.yMax+1;
                audit.Add($"{name} {width}x{height} {element} {b} inside={inside}");Assert.That(inside,Is.True,element+" outside viewport.");
            }
            var screen=root.Q("screen").worldBound;Assert.That(screen.width/screen.height,Is.EqualTo(4f/3).Within(.01));
            if(name=="pause"){float error=Vector2.Distance(bounds.center,root.Q("pause-card").worldBound.center);audit.Add($"pause center-error={error}");Assert.That(error,Is.LessThanOrEqualTo(1));}
            doc.panelSettings=old;target.Release();Object.Destroy(target);Object.Destroy(panel);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
