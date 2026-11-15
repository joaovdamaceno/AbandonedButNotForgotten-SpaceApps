using System.Collections;
using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.ChapterTests
{
    public sealed class SurveyorChapterTests : InputTestFixture
    {
        [Test]
        public void AstronautFacesTravelAndItsStanceFootStaysPlanted()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast = instance.GetComponentInChildren<SurveyorApolloCast>(true);
                cast.Evaluate(2f,true); var before = cast.Astronauts[0].transform.position; var planted = cast.FootPosition(0,1);
                cast.Evaluate(2.04f,true); var motion = cast.Astronauts[0].transform.position-before;
                Assert.That(Vector3.Dot(cast.FacingDirection(0).normalized,motion.normalized),Is.GreaterThan(.9f));
                Assert.That(Vector3.Distance(planted,cast.FootPosition(0,1)),Is.LessThan(.003f),"The stance ankle must not slide forward with the body.");
                Assert.That(cast.FootPosition(0,1).y-SurveyorLunarSurface.Height(planted.x,planted.z),Is.EqualTo(.13f).Within(.008f));
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test]
        public void PickupStartsAtItsMountAndSeekingBackRestoresTheScoop()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast = instance.GetComponentInChildren<SurveyorApolloCast>(true);
                cast.Evaluate(SurveyorApolloCast.CameraPickupSeconds,true);
                var camera = instance.transform.Find("Recovered camera assembly");
                var before = camera.position;
                cast.Evaluate(SurveyorApolloCast.CameraPickupSeconds+.02f,true);
                Assert.That(Vector3.Distance(camera.position,before),Is.LessThan(.015f),"The camera must leave its mount continuously.");
                cast.Evaluate(40f,true);cast.Evaluate(1f,true);
                var arm = instance.transform.Find("ScoopRig");
                Assert.That(arm.Find(SurveyorScoop.ScoopToolPath).gameObject.activeSelf,Is.True);
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test]
        public void PressureFindsRealSupportWhenThePlayerExcavatedItsPreferredPoint()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath);
            var instance = Object.Instantiate(prefab);
            var scoop = instance.GetComponentInChildren<SurveyorDirectScoop>(true);
            try
            {
                scoop.Initialize(); scoop.Deploy(1f);
                // Reproduce a mouse stroke through the preferred pressure point.
                for (int pass = 0; pass < 6; pass++)
                    for (int segment = 0; segment < 100; segment++)
                    {
                        var from = new Vector3(-.275f + segment * .0055f, 0f, -.11f);
                        scoop.Soil.Cut(from, from + Vector3.right * .0055f, .075f);
                    }
                scoop.SetAim(new Vector3(.16f, 0f, -.11f));
                for (int frame = 0; frame < 144 * 3; frame++) scoop.Tick(1f / 144f, false, true, 0f, false);
                float contactSeconds = 0f;
                for (int frame = 0; frame < 144 * 10; frame++)
                {
                    scoop.Tick(1f / 144f, true, true, 0f, false);
                    if (scoop.InGroundContact) contactSeconds += 1f / 144f;
                }
                Assert.That(contactSeconds, Is.GreaterThan(3.5f), "A real supporting edge permits the pressure trial.");
                Assert.That(scoop.ContactPointLocal.y, Is.EqualTo(scoop.Soil.Height(scoop.ContactPointLocal.x, scoop.ContactPointLocal.z)).Within(.02f));
            }
            finally
            {
                if (scoop.Soil.RuntimeMesh != null) Object.DestroyImmediate(scoop.Soil.RuntimeMesh);
                Object.DestroyImmediate(instance);
            }
        }
        static SurveyorDocumentaryHost Current => Object.FindAnyObjectByType<SurveyorDocumentaryHost>();
        [UnityTest] public IEnumerator DocumentaryJourneyDeformsPlayerGroundAndShowsCompleteActors()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene); yield return new EnterPlayMode();
            var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            var archive = Object.FindAnyObjectByType<SurveyorArchiveHost>(); archive.Initialize(); yield return null;
            archive.Input.Disable(); archive.Input.Enable();
            archive.Owner.Begin(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(SurveyorChapterBuilder.ChapterPath));
            for (int i = 0; i < 500 && Current == null; i++) yield return null;
            var h = Current; Assert.That(h, Is.Not.Null); h.Clock.enabled = false;
            var stages = new List<string>(); h.Owner.StepEntered += stages.Add;
            Advance(h, 4); yield return Capture("01-machine");
            Advance(h, 23); yield return Capture("02-descent");
            Advance(h, 26); yield return Capture("03-contact");
            Advance(h, SurveyorDocumentaryTiming.BeforeScience + SurveyorDocumentaryTiming.Deploy + .3f - h.Clock.ActiveSeconds);
            Assert.That(h.Owner.CurrentStepId, Is.EqualTo("Science")); yield return Capture("04-instrument");
            Set(mouse.position, new Vector2(-2000, -2000));
            float distantGround = h.Scoop.Soil.Height(0f, .40f), scienceStart = h.Clock.ActiveSeconds;
            Keys(keyboard, Key.E); Advance(h, 12f); Keys(keyboard);
            Assert.That(h.CanFinishCut, Is.True, "A novice must be able to complete a short cut with E alone. Travel="+h.Scoop.Soil.TravelMeters+" depth="+h.Scoop.Soil.DeepestMeters);
            Assert.That(h.CutProgress, Is.EqualTo(1f));
            Assert.That(h.View.GetComponentInChildren<UIDocument>().rootVisualElement.Q("effect"), Is.Null);
            Assert.That(h.Scoop.Soil.Height(0f,.40f), Is.EqualTo(distantGround).Within(.001f));
            yield return Capture("05-excavated");
            float depth = h.Scoop.Soil.DeepestMeters; h.PauseGame(); Advance(h, 5); Assert.That(h.Scoop.Soil.DeepestMeters, Is.EqualTo(depth)); h.Resume();
            Keys(keyboard, Key.E); Advance(h, 8f); Keys(keyboard);
            Assert.That(h.SciencePhase, Is.EqualTo(3), "One fresh hold must move from ready cut into the pressure test.");
            Keys(keyboard, Key.E); Advance(h,.05f); Keys(keyboard); Advance(h,3.1f); yield return null; yield return null;
            Assert.That(h.Owner.Evidence.PlayerPhotoCount, Is.EqualTo(1));
            Advance(h,SurveyorDocumentaryTiming.ResultRead+.1f);
            Assert.That(h.Owner.CurrentStepId, Is.EqualTo("Findings"));
            File.WriteAllText("QA/SUR-019/science-seconds.txt", (h.Clock.ActiveSeconds-scienceStart).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Advance(h,10f); yield return Capture("06-findings");
            Advance(h,SurveyorDocumentaryTiming.Findings-10f+8f); yield return Capture("07-night");
            Advance(h,SurveyorDocumentaryTiming.Night-8f+5f); Assert.That(h.Owner.CurrentStepId, Is.EqualTo("Reunion")); yield return Capture("08-astronauts-walking");
            var cast = Object.FindAnyObjectByType<SurveyorApolloCast>(); Assert.That(cast.Astronauts.Length, Is.EqualTo(2));
            foreach (var actor in cast.Astronauts) Assert.That(actor.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length, Is.GreaterThanOrEqualTo(16));
            Advance(h,17f); yield return Capture("09-inspection"); Advance(h,17f); yield return Capture("10-recovered");
            Advance(h,8f); yield return Capture("11-archive"); Advance(h,10.1f);
            Assert.That(h.Owner.State, Is.EqualTo(AppState.Completion));
            Assert.That(h.Localization.Problems, Is.Empty); Assert.That(h.Localization.MissingRequests, Is.Empty);
            Assert.That(h.Owner.LastRunActiveSeconds, Is.InRange(SurveyorDocumentaryTiming.CinematicSeconds+15f,SurveyorDocumentaryTiming.CinematicSeconds+60f));
            yield return Capture("12-memorial");
            File.WriteAllText("QA/SUR-019/technical-journey.txt", "Automated assisted controls, not novice timing.\nActive seconds "+h.Owner.LastRunActiveSeconds+"\n"+string.Join(" -> ",stages));
            Keys(keyboard,Key.E); Keys(keyboard);
            for (int i=0;i<300&&archive.Owner.State!=AppState.Dossier;i++)yield return null;
            Assert.That(archive.Owner.State,Is.EqualTo(AppState.Dossier)); Assert.That(archive.Owner.Evidence.Records,Is.Empty);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator RestartRecreatesSoilAndFocusStopsTheTool()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene); yield return new EnterPlayMode();
            var archive = Object.FindAnyObjectByType<SurveyorArchiveHost>(); archive.Initialize(); yield return null;
            for (int run = 0; run < 2; run++)
            {
                archive.Owner.Begin(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(SurveyorChapterBuilder.ChapterPath));
                for (int i = 0; i < 500 && Current == null; i++) yield return null;
                var h = Current; h.Clock.enabled = false; Advance(h, SurveyorDocumentaryTiming.BeforeScience + 3f);
                Assert.That(h.Scoop.Soil.RemovedCubicMeters, Is.Zero);
                h.GetComponent<FocusPause>().HandleFocusLost(); float time = h.Clock.ActiveSeconds; Advance(h, 3); Assert.That(h.Clock.ActiveSeconds, Is.EqualTo(time));
                h.Resume(); archive.Owner.ReturnToDossier(); for (int i = 0; i < 300 && archive.Owner.State != AppState.Dossier; i++) yield return null;
                Assert.That(Current, Is.Null); Assert.That(archive.Owner.Evidence.Records, Is.Empty);
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator PilotSamplesCompleteBodyMotionWithoutClaimingPlayerProgress()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene); yield return new EnterPlayMode(); yield return null;
            var archive = Object.FindAnyObjectByType<SurveyorArchiveHost>(); archive.Initialize();archive.Owner.Begin(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(SurveyorChapterBuilder.ChapterPath));
            for(int i=0;i<500&&Current==null;i++)yield return null;
            var h = Current; h.Clock.enabled = false; h.Sequence.Cancel(); h.enabled = false;
            var film = h.GetComponent<SurveyorDocumentaryFilm>(); var cameras = h.GetComponent<CameraDirector>(); var cast = Object.FindAnyObjectByType<SurveyorApolloCast>();
            var times = new[] { 0f, .5f, 1f, 1.5f, 2f, 5f, 12f, 18f, 30f, 34f, 38f, 44f };
            foreach (float t in times)
            {
                film.Evaluate("Reunion", t);
                var actor = cast.Astronauts[0]; var at = actor.transform.position + Vector3.up;
                cameras.Apply(new CameraPose(at + new Vector3(-2.4f, .3f, -3.2f), Quaternion.LookRotation(at - (at + new Vector3(-2.4f,.3f,-3.2f))), 38f));
                yield return null; yield return null; h.View.Render(t, 1f); yield return null;
                var rt = h.View.Raw; var previous = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();RenderTexture.active=previous;
                Directory.CreateDirectory("QA/SUR-019/motion");File.WriteAllBytes("QA/SUR-019/motion/"+t.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png",image.EncodeToPNG());Object.Destroy(image);
            }
            yield return new ExitPlayMode();
        }
        static void Button(SurveyorDocumentaryHost host, string name)
        {
            var button = host.View.GetComponentInChildren<UIDocument>().rootVisualElement.Q<Button>(name);
            using var evt = NavigationSubmitEvent.GetPooled(); button.SendEvent(evt);
        }
        static void Keys(Keyboard keyboard,params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update(); }
        public static void Advance(SurveyorDocumentaryHost host, float seconds)
        {
            // UnityTest queues fixture events until the next player update; flush before advancing the deterministic clock.
            InputSystem.Update();
            while (seconds > .00001f)
            { float dt = Mathf.Min(.05f, seconds); host.Clock.Tick(dt); host.Tick(host.Clock.ActiveDeltaSeconds); seconds -= dt; }
            host.Clock.Tick(0);
        }
        public static IEnumerator Capture(string name, string folder = "QA/SUR-019")
        {
            var documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include); var originals = new Dictionary<PanelSettings, RenderTexture>(); var clear = new Dictionary<PanelSettings,bool>(); var target = new RenderTexture(1280, 720, 0); target.Create();
            foreach (var doc in documents)
            {
                if (doc.panelSettings == null || originals.ContainsKey(doc.panelSettings)) continue; var settings = doc.panelSettings; originals[settings] = settings.targetTexture;clear[settings]=settings.clearColor;settings.targetTexture=target;settings.clearColor=false;
            }
            var saved = RenderTexture.active; RenderTexture.active = target; GL.Clear(true, true, Color.black); RenderTexture.active = saved;
            for (int i = 0; i < 12; i++) yield return null;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); saved = RenderTexture.active; RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); RenderTexture.active = saved;
            Directory.CreateDirectory(folder); File.WriteAllBytes(folder + "/" + name + ".png", image.EncodeToPNG()); Object.Destroy(image);
            foreach (var pair in originals) if (pair.Key != null) { pair.Key.targetTexture = pair.Value;pair.Key.clearColor=clear[pair.Key]; }
            target.Release(); Object.Destroy(target); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
