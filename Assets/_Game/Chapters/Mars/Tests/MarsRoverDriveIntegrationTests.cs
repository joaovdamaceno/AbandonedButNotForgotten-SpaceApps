using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>Regressão: a montagem desligava a raiz do rover; ele ficava invisível e parado em toda a partida (só o filho era ligado).</summary>
    public class MarsRoverDriveIntegrationTests
    {
        sealed class ImmediateLoader : ChapterDirector.ISceneLoader
        {
            public void Load(string sceneName, Action onLoaded, Action<string> onFailed) => onLoaded();
            public void Unload(string sceneName, Action onUnloaded) => onUnloaded();
        }

        [Test]
        public void TheRoverIsActiveInTheHierarchyAndDrivesOnTheEagleTerrainAtM07()
        {
            GameObject host = null;
            MarsChapterBindings bindings = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.OpenScene("Assets/_Game/Chapters/Mars/Scenes/Mars.unity", OpenSceneMode.Additive);
                bindings = UnityEngine.Object.FindFirstObjectByType<MarsChapterBindings>(FindObjectsInactive.Include);
                host = new GameObject("svc");
                var clock = host.AddComponent<SessionClock>();
                var router = host.AddComponent<InputRouter>();
                var interactions = host.AddComponent<InteractionService>();
                interactions.Initialize(router, clock);
                var evidence = new EvidenceStore();
                var director = host.AddComponent<ChapterDirector>();
                director.Initialize(clock, router, new ImmediateLoader(), evidence);
                bindings.Initialize(director, clock, router, interactions, evidence);
                director.OpenDossier();
                director.Begin(bindings.Bindings.Chapter);
                var guard = 0;
                while (bindings.Flow.CurrentBinding != null && bindings.Flow.CurrentBinding.StepId != "M07" && guard++ < 400000)
                {
                    var b = bindings.Flow.CurrentBinding;
                    if (!b.IsCinematic) foreach (var e in b.CompletionEvents) bindings.Flow.Raise(e);
                    clock.Tick(0.05f);
                    var dt = clock.ActiveDeltaSeconds;
                    bindings.SequenceDirector.Tick(dt);
                    foreach (var d in new MonoBehaviour[] { bindings.Landing, bindings.Spirit, bindings.Memorial })
                        if (d != null && d.gameObject.activeInHierarchy) d.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, null);
                    foreach (var d in UnityEngine.Object.FindObjectsByType<MarsOriginDirector>(FindObjectsInactive.Exclude))
                        d.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, null);
                    bindings.Flow.Tick(dt);
                }

                Assert.That(bindings.Flow.CurrentBinding.StepId, Is.EqualTo("M07"));
                Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay));
                var rover = bindings.Rover;
                var cc = rover.GetComponent<CharacterController>();
                Assert.That(cc, Is.Not.Null);
                Assert.That(rover.gameObject.activeInHierarchy, Is.True, "o rover precisa estar ativo na hierarquia (a raiz do prefab não pode ficar desligada)");
                var startPosition = rover.transform.position;
                Physics.SyncTransforms();
                var feed = typeof(InputRouter).GetMethod("FeedMove", BindingFlags.Instance | BindingFlags.NonPublic);
                for (var i = 0; i < 40; i++)
                {
                    feed.Invoke(router, new object[] { Vector2.up });
                    clock.Tick(0.05f);
                    rover.Tick(clock.ActiveDeltaSeconds);
                }

                Assert.That(rover.DrivenMeters, Is.GreaterThan(1.5f), "W anda o rover em M07");
                Assert.That(Vector3.Distance(startPosition, rover.transform.position), Is.GreaterThan(1.5f));

            }
            finally
            {
                if (bindings != null)
                    bindings.Teardown();
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
