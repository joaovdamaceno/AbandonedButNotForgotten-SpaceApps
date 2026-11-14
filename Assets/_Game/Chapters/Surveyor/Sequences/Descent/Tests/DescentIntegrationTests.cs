using System;
using System.Collections;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.DescentTests
{
    public sealed class DescentIntegrationTests
    {
        sealed class Memory:SettingsStore.IStorage
        {string data=JsonUtility.ToJson(new SettingsData{masterVolume=0,reducedMotion=true});public string Read(string key)=>data;public void Write(string key,string value)=>data=value;public void Delete(string key)=>data=null;}
        sealed class Delivery
        {
            readonly SurveyorDescentPreviewHost host;public float sent,received;public int deliveries,contacts;
            public Delivery(SurveyorDescentPreviewHost owner){host=owner;}
            public void OnSent(SurveyorCommandReceipt receipt)=>sent=host.Clock.ActiveSeconds;
            public void OnReceived(SurveyorCommandReceipt receipt){received=host.Clock.ActiveSeconds;deliveries++;}
            public void OnContact(int number)=>contacts++;
        }
        [Serializable]sealed class Metrics
        {public string utc;public float transitActiveSeconds;public bool pausedImpulsePreserved,textureBeforeSettlement,textureAfterHandoff,restartReleasedTexture;public int contacts,deliveries;public string origin,finalStep;}
        [Test]public void RebuildPreservesGuidsSceneAndReferences()
        {
            var first=SpaceAppsSceneBuilder.Run(new SurveyorDescentBuilder());Assert.That(first.Succeeded,Is.True,first.ToString());
            string before=File.ReadAllText(SurveyorDescentBuilder.PreviewScene),guid=AssetDatabase.AssetPathToGUID(SurveyorDescentBuilder.SequencePath);
            var second=SpaceAppsSceneBuilder.Run(new SurveyorDescentBuilder());Assert.That(second.Succeeded,Is.True,second.ToString());
            Assert.That(File.ReadAllText(SurveyorDescentBuilder.PreviewScene),Is.EqualTo(before));
            Assert.That(AssetDatabase.AssetPathToGUID(SurveyorDescentBuilder.SequencePath),Is.EqualTo(guid));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDescentBuilder.PrefabPath);
            Assert.That(SpaceAppsSceneBuilder.ValidateHierarchy(prefab),Is.Empty);Assert.That(prefab.GetComponentsInChildren<AudioListener>(true).Length,Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<SurveyorSiteViewport>(true).Single().enabled,Is.False);
            var clips=AssetDatabase.FindAssets("t:AudioClip",new[]{SurveyorDescentBuilder.Folder+"/Audio"});Assert.That(clips.Length,Is.EqualTo(4));
        }
        [UnityTest]public IEnumerator RuntimePrefabSettlesWithGroundAssistanceAndGatesTheActualCamera()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var scene=SceneManager.CreateScene("SurveyorDescentPreview");
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDescentBuilder.PrefabPath));SceneManager.MoveGameObjectToScene(root,scene);
            var host=root.GetComponent<SurveyorDescentPreviewHost>();host.Initialize(new SettingsStore(new Memory()));
            var observation=new Delivery(host);host.Queue.Sent+=observation.OnSent;host.Queue.Received+=observation.OnReceived;host.Flow.GroundContact+=observation.OnContact;host.Begin();
            Advance(host,18.1f);Assert.That(host.Flow.ContactCount,Is.EqualTo(1));Assert.That(host.Flow.ImpactPulse,Is.GreaterThan(0));
            host.Pause();float height=host.Flow.HeightMeters,pulse=host.Flow.ImpactPulse,time=host.Flow.TimeSeconds;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(host.Flow.HeightMeters,Is.EqualTo(height));Assert.That(host.Flow.ImpactPulse,Is.EqualTo(pulse));Assert.That(host.Flow.TimeSeconds,Is.EqualTo(time));
            host.Resume();Assert.That(host.Gate.TryActivate(),Is.False);bool before=host.Gate.TelevisionCamera.targetTexture!=null;Assert.That(before,Is.False);
            Advance(host,26.9f-host.Flow.TimeSeconds);float deadline=Time.realtimeSinceStartup+25;
            while(!host.Flow.CutoffReceived && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Flow.CutoffReceived,Is.True);Assert.That(host.Flow.Origin,Is.EqualTo(CutoffOrigin.GroundTeam));
            Assert.That(host.Flow.SubtitleKey,Is.EqualTo("SUR-S08"));Assert.That(host.Flow.EnginesCutOff,Is.False);
            Assert.That(observation.received-observation.sent,Is.InRange(1.2999f,1.4f));
            Advance(host,35-host.Flow.TimeSeconds);
            while(!host.Flow.ActComplete && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Owner.CurrentStepId,Is.EqualTo("FirstImage"));Assert.That(observation.contacts,Is.EqualTo(3));Assert.That(observation.deliveries,Is.EqualTo(1));
            Assert.That(host.Gate.TryActivate(),Is.True);var texture=host.Gate.TelevisionCamera.targetTexture;
            Assert.That(texture,Is.Not.Null);Assert.That(texture.width,Is.EqualTo(640));Assert.That(texture.height,Is.EqualTo(480));
            yield return null;yield return null;
            Directory.CreateDirectory("QA/SUR-006/Captures");Save(texture,"QA/SUR-006/Captures/surface-after-settlement.png");
            var metrics=new Metrics{utc=DateTime.UtcNow.ToString("O"),transitActiveSeconds=observation.received-observation.sent,pausedImpulsePreserved=true,
                textureBeforeSettlement=before,textureAfterHandoff=true,contacts=observation.contacts,deliveries=observation.deliveries,
                origin=host.Flow.Origin.ToString(),finalStep=host.Owner.CurrentStepId};
            host.Restart();Assert.That(host.Gate.Active,Is.False);Assert.That(host.Gate.TelevisionCamera.targetTexture,Is.Null);metrics.restartReleasedTexture=true;
            host.Return();Assert.That(host.Clock.IsRunning,Is.False);Assert.That(host.Flow.Running,Is.False);Assert.That(host.Service.TryGetActive(out _),Is.False);
            File.WriteAllText("QA/SUR-006/runtime-metrics.json",JsonUtility.ToJson(metrics,true));
            Object.Destroy(root);yield return null;yield return new ExitPlayMode();
        }
        static void Advance(SurveyorDescentPreviewHost host,float seconds)
        {while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);host.Clock.Tick(d);host.Service.Tick(host.Clock.ActiveDeltaSeconds);host.Queue.Tick();host.Flow.Tick(host.Clock.ActiveDeltaSeconds);seconds-=d;}}
        static void Save(RenderTexture target,string path)
        {
            var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=old;File.WriteAllBytes(path,image.EncodeToPNG());Object.Destroy(image);
        }
        [UnityTearDown]public IEnumerator CleanupRuntime(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
