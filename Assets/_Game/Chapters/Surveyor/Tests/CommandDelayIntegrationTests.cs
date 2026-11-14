using System;
using System.Collections;
using System.IO;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.CommandTests
{
    public sealed class CommandDelayIntegrationTests
    {
        sealed class MemorySettings : SettingsStore.IStorage
        {
            string data;
            public string Read(string key) => data;
            public void Write(string key,string value) => data=value;
            public void Delete(string key) => data=null;
        }
        [Serializable] sealed class Metrics
        {
            public string utc, unity;
            public int uiBindingFrames;
            public float deliveryActiveSeconds, pausedActiveDelta;
        }
        // Criado DEPOIS de EnterPlayMode. Closures do iterador seriam alocadas antes do reload e perderiam referência.
        sealed class DeliveryObservation
        {
            readonly SurveyorCommandPreviewHost host;
            public float sentAt, deliveredAt;
            public int delivered;
            public DeliveryObservation(SurveyorCommandPreviewHost preview) { host=preview; }
            public void OnSent(SurveyorCommandReceipt receipt) => sentAt=host.Clock.ActiveSeconds;
            public void OnReceived(SurveyorCommandReceipt receipt) { deliveredAt=host.Clock.ActiveSeconds; delivered++; }
        }
        [Test] public void BuilderPreservesSceneAndGuidsAcrossRebuilds()
        {
            var first=SpaceAppsSceneBuilder.Run(new SurveyorCommandBuilder()); Assert.That(first.Succeeded,Is.True,first.ToString());
            string before=File.ReadAllText(SurveyorCommandBuilder.PreviewScene);
            string guid=AssetDatabase.AssetPathToGUID(SurveyorCommandBuilder.PrefabPath);
            var second=SpaceAppsSceneBuilder.Run(new SurveyorCommandBuilder()); Assert.That(second.Succeeded,Is.True,second.ToString());
            Assert.That(File.ReadAllText(SurveyorCommandBuilder.PreviewScene),Is.EqualTo(before));
            Assert.That(AssetDatabase.AssetPathToGUID(SurveyorCommandBuilder.PrefabPath),Is.EqualTo(guid));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorCommandBuilder.PrefabPath);
            Assert.That(SpaceAppsSceneBuilder.ValidateHierarchy(prefab),Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<SurveyorCommandDelay>(true).Length,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator RuntimePrefabSeparatesImmediateHudFromDelayedMirrorAndSurvivesRestart()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            var scene=SceneManager.CreateScene("CommandDelayPreview");
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorCommandBuilder.PrefabPath));
            Assert.That(root,Is.Not.Null,"A instanciação deve retornar a raiz do prefab.");
            SceneManager.MoveGameObjectToScene(root,scene);
            Assert.That(root,Is.Not.Null,"Mover a cena não deve destruir a raiz.");
            var host=root.GetComponent<SurveyorCommandPreviewHost>();
            Assert.That(host,Is.Not.Null,"O prefab deve conter o host de preview no objeto raiz.");
            host.Initialize(new SettingsStore(new MemorySettings()));
            var observed=new DeliveryObservation(host);
            host.Queue.Sent+=observed.OnSent;
            host.Queue.Received+=observed.OnReceived;
            host.BeginOrRestart(); Assert.That(host.Action.BeginConfirmation(),Is.True);
            float deadline=Time.realtimeSinceStartup+20;
            while(!host.Queue.HasPending && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Queue.HasPending,Is.True);
            Assert.That(host.View.LastFeedbackFrames,Is.Zero);
            host.SetDesired(-40); Assert.That(host.DesiredDegrees,Is.EqualTo(-40)); Assert.That(host.ActualDegrees,Is.Zero);
            while(host.Queue.ElapsedSeconds<.6f && Time.realtimeSinceStartup<deadline)yield return null;
            host.SendMessage("OnApplicationFocus",false);
            float active=host.Clock.ActiveSeconds,elapsed=host.Queue.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(host.Owner.IsPaused,Is.True); Assert.That(host.Queue.ElapsedSeconds,Is.EqualTo(elapsed));
            float pausedDelta=host.Clock.ActiveSeconds-active;
            Assert.That(pausedDelta,Is.Zero); host.Resume();
            while(host.Queue.HasPending && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(observed.delivered,Is.EqualTo(1)); Assert.That(host.ActualDegrees,Is.EqualTo(30).Within(.001));
            Assert.That(host.Owner.CurrentStepId,Is.EqualTo(SurveyorCommandPreviewHost.PreviewStep));
            Assert.That(observed.deliveredAt-observed.sentAt,Is.InRange(1.2999f,1.4f));
            var metrics=new Metrics {utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,
                uiBindingFrames=host.View.LastFeedbackFrames,deliveryActiveSeconds=observed.deliveredAt-observed.sentAt,pausedActiveDelta=pausedDelta};
            Directory.CreateDirectory("QA/SUR-004"); File.WriteAllText("QA/SUR-004/runtime-metrics.json",JsonUtility.ToJson(metrics,true));

            Assert.That(host.Action.BeginConfirmation(),Is.True);
            deadline=Time.realtimeSinceStartup+15;
            while(!host.Queue.HasPending && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Queue.HasPending,Is.True);
            host.Queue.enabled=false; host.Queue.enabled=true;
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(host.Queue.HasPending,Is.False); Assert.That(observed.delivered,Is.EqualTo(1));
            host.Restart(); Assert.That(host.Action.BeginConfirmation(),Is.True);
            deadline=Time.realtimeSinceStartup+15;
            while(!host.Queue.HasPending && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Queue.HasPending,Is.True); host.Restart();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(observed.delivered,Is.EqualTo(1)); Assert.That(host.ActualDegrees,Is.Zero);
            host.Return(); Assert.That(host.Clock.IsRunning,Is.False); Assert.That(host.Owner.State,Is.EqualTo(AppState.Dossier));
            Assert.That(host.Interactions.TryGetActive(out _),Is.False);
            Object.Destroy(root); yield return new ExitPlayMode();
        }
        [UnityTearDown] public IEnumerator RestoreEditorAfterRuntimeFailure()
        { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
