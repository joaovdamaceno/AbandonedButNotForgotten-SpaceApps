using System.Collections;
using System.IO;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Abandoned.SpaceApps.Chapters.Surveyor.LaunchTests
{
    public sealed class SurveyorLaunchIntegrationTests
    {
        sealed class MemorySettings : SettingsStore.IStorage
        {
            string value;
            public string Read(string key)=>value;
            public void Write(string key,string data)=>value=data;
            public void Delete(string key)=>value=null;
        }

        [Test]
        public void RebuildPreservesTheSceneAndAssetGuidsWithoutDuplicatingServices()
        {
            var first=SpaceAppsSceneBuilder.Run(new SurveyorLaunchBuilder());Assert.That(first.Succeeded,Is.True,first.ToString());
            var guid=AssetDatabase.AssetPathToGUID(SurveyorLaunchBuilder.SequencePath);
            string original=File.ReadAllText(SurveyorLaunchBuilder.Scene);
            var second=SpaceAppsSceneBuilder.Run(new SurveyorLaunchBuilder());Assert.That(second.Succeeded,Is.True,second.ToString());
            Assert.That(AssetDatabase.AssetPathToGUID(SurveyorLaunchBuilder.SequencePath),Is.EqualTo(guid));
            Assert.That(File.ReadAllText(SurveyorLaunchBuilder.Scene),Is.EqualTo(original));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorLaunchBuilder.PrefabPath);
            Assert.That(prefab.GetComponentsInChildren<ChapterDirector>(true).Length,Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<AudioListener>(true).Length,Is.EqualTo(1));
            Assert.That(SpaceAppsSceneBuilder.ValidateHierarchy(prefab),Is.Empty);
        }

        [UnityTest]
        public IEnumerator RuntimeUpdateOperatesTheRealPrefabAndFocusPausePreservesTheCommand()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            var scene=SceneManager.CreateScene("SurveyorLaunchPreview");
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorLaunchBuilder.PrefabPath));
            SceneManager.MoveGameObjectToScene(root,scene);
            var host=root.GetComponent<SurveyorLaunchPreviewHost>();host.Initialize(new SettingsStore(new MemorySettings()));host.Begin();
            // Só a espera inicial é adiantada: a operação e a pausa abaixo passam por Updates reais.
            host.Flow.Tick(host.Flow.ReadyAt+.05f);
            Assert.That(host.Flow.BeginConfirmation(),Is.True);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(host.Flow.Progress,Is.GreaterThan(.05f));
            host.SendMessage("OnApplicationFocus",false);
            float frozen=host.Flow.Progress,time=host.Flow.TimeSeconds;
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(host.Chapter.IsPaused,Is.True);Assert.That(host.Flow.Progress,Is.EqualTo(frozen));
            Assert.That(host.Flow.TimeSeconds,Is.EqualTo(time));
            host.Resume();Assert.That(host.Flow.BeginConfirmation(),Is.True);
            float deadline=Time.realtimeSinceStartup+12;
            while(!host.Flow.ActComplete && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(host.Flow.ActComplete,Is.True,"O fluxo real não chegou a Cruise.");
            Assert.That(host.Chapter.CurrentStepId,Is.EqualTo("Cruise"));
            host.Restart();Assert.That(host.Flow.Progress,Is.Zero);Assert.That(host.Flow.Running,Is.True);
            host.Return();Assert.That(host.Chapter.State,Is.EqualTo(AppState.Dossier));
            Assert.That(host.Interactions.TryGetActive(out _),Is.False);
            Object.Destroy(root);
            yield return new ExitPlayMode();
        }
    }
}
