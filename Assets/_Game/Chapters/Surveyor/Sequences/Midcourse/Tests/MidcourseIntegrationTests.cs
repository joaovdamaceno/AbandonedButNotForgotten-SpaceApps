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

namespace Abandoned.SpaceApps.Chapters.Surveyor.MidcourseTests
{
    public sealed class MidcourseIntegrationTests
    {
        sealed class Memory:SettingsStore.IStorage
        {string data;public string Read(string key)=>data;public void Write(string key,string value)=>data=value;public void Delete(string key)=>data=null;}
        sealed class Delivery
        {
            readonly SurveyorMidcoursePreviewHost host;public float sent,received;public int count;
            public Delivery(SurveyorMidcoursePreviewHost owner){host=owner;}
            public void OnSent(SurveyorCommandReceipt receipt)=>sent=host.Clock.ActiveSeconds;
            public void OnReceived(SurveyorCommandReceipt receipt){received=host.Clock.ActiveSeconds;count++;}
        }
        [Serializable]sealed class Metrics{public string utc;public float transitActiveSeconds;public bool pausedBurnPreserved;public int corrections;public string finalStep;}
        [Test]public void RebuildPreservesGuidsSceneAndReferences()
        {
            var first=SpaceAppsSceneBuilder.Run(new SurveyorMidcourseBuilder());Assert.That(first.Succeeded,Is.True,first.ToString());
            string before=File.ReadAllText(SurveyorMidcourseBuilder.PreviewScene),guid=AssetDatabase.AssetPathToGUID(SurveyorMidcourseBuilder.SequencePath);
            var second=SpaceAppsSceneBuilder.Run(new SurveyorMidcourseBuilder());Assert.That(second.Succeeded,Is.True,second.ToString());
            Assert.That(File.ReadAllText(SurveyorMidcourseBuilder.PreviewScene),Is.EqualTo(before));
            Assert.That(AssetDatabase.AssetPathToGUID(SurveyorMidcourseBuilder.SequencePath),Is.EqualTo(guid));
            Assert.That(SpaceAppsSceneBuilder.ValidateHierarchy(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorMidcourseBuilder.PrefabPath)),Is.Empty);
        }
        [UnityTest]public IEnumerator RuntimePrefabConfirmsCorrectsPausesTheBurnAndHandsOffToDescent()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            var scene=SceneManager.CreateScene("SurveyorMidcoursePreview");
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorMidcourseBuilder.PrefabPath));SceneManager.MoveGameObjectToScene(root,scene);
            var host=root.GetComponent<SurveyorMidcoursePreviewHost>();host.Initialize(new SettingsStore(new Memory()));
            var observation=new Delivery(host);host.Queue.Sent+=observation.OnSent;host.Queue.Received+=observation.OnReceived;host.Begin();
            Advance(host,12);host.Flow.Assist();Assert.That(host.Action.BeginConfirmation(),Is.True);
            float deadline=Time.realtimeSinceStartup+50;
            while(host.Flow.Phase!=MidcoursePhase.Burn && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Flow.Phase,Is.EqualTo(MidcoursePhase.Burn));
            while(host.Flow.BurnProgress<.15f && Time.realtimeSinceStartup<deadline)yield return null;
            host.Pause();float progress=host.Flow.BurnProgress;var aim=host.Flow.ActualAim;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(host.Flow.BurnProgress,Is.EqualTo(progress));Assert.That(host.Flow.ActualAim,Is.EqualTo(aim));host.Resume();
            while(!host.Flow.ActComplete && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Owner.CurrentStepId,Is.EqualTo("Descent"));Assert.That(observation.count,Is.EqualTo(1));
            Assert.That(observation.received-observation.sent,Is.InRange(1.2999f,1.4f));
            var metrics=new Metrics{utc=DateTime.UtcNow.ToString("O"),transitActiveSeconds=observation.received-observation.sent,
                pausedBurnPreserved=true,corrections=observation.count,finalStep=host.Owner.CurrentStepId};
            Directory.CreateDirectory("QA/SUR-005");File.WriteAllText("QA/SUR-005/runtime-metrics.json",JsonUtility.ToJson(metrics,true));
            for(int i=0;i<12;i++)host.Restart();Assert.That(host.Flow.Phase,Is.EqualTo(MidcoursePhase.Travel));
            Assert.That(RuntimeSequenceCount(),Is.EqualTo(3));host.Return();Assert.That(host.Clock.IsRunning,Is.False);
            Object.Destroy(root);float cleanupDeadline=Time.realtimeSinceStartup+3;
            while(RuntimeSequenceCount()>0 && Time.realtimeSinceStartup<cleanupDeadline)yield return null;
            Assert.That(RuntimeSequenceCount(),Is.Zero);yield return new ExitPlayMode();
        }
        static void Advance(SurveyorMidcoursePreviewHost host,float seconds)
        {while(seconds>.0001f){float d=Mathf.Min(.05f,seconds);host.Clock.Tick(d);host.Service.Tick(host.Clock.ActiveDeltaSeconds);host.Queue.Tick();host.Flow.Tick(host.Clock.ActiveDeltaSeconds);seconds-=d;}}
        static int RuntimeSequenceCount()=>Resources.FindObjectsOfTypeAll<SequenceDefinition>().Count(s=>s.SequenceId!=null&&s.SequenceId.StartsWith("surveyor.cruise.runtime."));
        [UnityTearDown]public IEnumerator CleanupRuntime(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
