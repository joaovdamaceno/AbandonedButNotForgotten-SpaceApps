using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.ChapterTests
{
    public sealed class SurveyorChapterTests
    {
        static List<string> stageOrder;
        static void RecordStage(string id)=>stageOrder.Add(id);
        [UnityTest]public IEnumerator DossierToMemorialUsesEveryRealOperationAndReturnsWithoutLeaking()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene);yield return new EnterPlayMode();
            var archive=Object.FindAnyObjectByType<SurveyorArchiveHost>();archive.Initialize();yield return null;
            yield return Capture("archive");
            var starts=archive.Screen.GetComponent<UIDocument>().rootVisualElement.Query<Button>("folder-start").ToList();
            Assert.That(starts.Count,Is.EqualTo(3));var start=starts.Single(b=>b.enabledSelf);start.Focus();using(var submit=NavigationSubmitEvent.GetPooled())start.SendEvent(submit);
            float until=Time.realtimeSinceStartup+30;while(archive.Owner.State==AppState.Loading&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(archive.Owner.State,Is.EqualTo(AppState.Playing));
            var h=Object.FindAnyObjectByType<SurveyorChapterHost>();Assert.That(h,Is.Not.Null,"Chapter instance missing");
            Assert.That(h.Owner,Is.Not.Null,"Chapter has not been initialized");Assert.That(h.Owner,Is.SameAs(archive.Owner));Assert.That(h.Clock,Is.SameAs(archive.Clock));
            Debug.Log("INJECTION_OK "+h.name+" owner="+h.Owner.name+" clock="+h.Clock.name);
            h.Clock.enabled=false;
            stageOrder=new List<string>{h.Owner.CurrentStepId};h.Owner.StepEntered+=RecordStage;
            h.Service.Mode=HoldMode.Hold;Advance(h,4);yield return Capture("launch");Advance(h,13.3f);Hold(h,2.7f);Advance(h,9);
            Assert.That(h.Owner.CurrentStepId,Is.EqualTo("Cruise"));Advance(h,10.2f);h.Cruise.Assist();Advance(h,6.2f);Hold(h,3.2f);Advance(h,15.2f);
            Assert.That(h.Owner.CurrentStepId,Is.EqualTo("Descent"));Advance(h,14);Hold(h,3.2f);Advance(h,24);
            Assert.That(h.Owner.CurrentStepId,Is.EqualTo("FirstImage"));Hold(h,2.2f);Advance(h,6.5f);yield return Capture("first-picture");Advance(h,5.2f);
            Assert.That(h.Owner.CurrentStepId,Is.EqualTo("Observe"));var optical=h.Viewport.GetComponent<Camera>().transform.position;
            for(int n=0;n<3;n++)
            {
                h.Service.NotifyInteractReleased();Assert.That(h.Observation.AssistFrame(),Is.True);Advance(h,7);yield return null;yield return null;
                Assert.That(h.Observation.Aligned,Is.True);h.Service.NotifyInteractReleased();Assert.That(h.Observation.BeginPrimary(),Is.True);Advance(h,1.6f);h.Observation.ReleasePrimary();Advance(h,1.5f);
                Assert.That(h.Observation.Phase,Is.EqualTo(ObservationPhase.Result));Assert.That(h.Observation.RecordCount,Is.EqualTo(n+1));
                h.Service.NotifyInteractReleased();Assert.That(h.Observation.BeginPrimary(),Is.True);Advance(h,.2f);
            }
            Assert.That(h.Owner.CurrentStepId,Is.EqualTo("Sample"));Advance(h,2.5f);Hold(h,12.2f);Advance(h,4.3f);Assert.That(h.Soil.Phase,Is.EqualTo(SoilPhase.Press));
            Hold(h,4.2f);Advance(h,3.4f);Assert.That(h.Soil.Phase,Is.EqualTo(SoilPhase.Impact));Hold(h,2.2f);Advance(h,4.2f);Assert.That(h.Soil.HasBothCaptures,Is.True);yield return Capture("soil-comparison");Advance(h,20.2f);
            Assert.That(h.Owner.CurrentStepId,Is.EqualTo("Eclipse"));h.Service.NotifyInteractReleased();Assert.That(h.Eclipse.BeginConfirmation(),Is.True);Advance(h,10);yield return Capture("eclipse");Advance(h,18.3f);
            Assert.That(h.Owner.CurrentStepId,Is.EqualTo("LunarNight"));Advance(h,30.3f);Assert.That(h.Owner.CurrentStepId,Is.EqualTo("ImaginedVisit"));Advance(h,34);yield return Capture("archive-photograph");h.Pause.Controller.FocusLost();float visitTime=h.Visit.TimeSeconds;Advance(h,3);Assert.That(h.Visit.TimeSeconds,Is.EqualTo(visitTime));h.Resume();Advance(h,11);yield return Capture("memorial");Advance(h,5.3f);
            Assert.That(h.Localization.Problems,Is.Empty);Assert.That(h.Localization.MissingRequests,Is.Empty);
            Assert.That(h.Owner.State,Is.EqualTo(AppState.Completion));Assert.That(h.Visit.AwaitingReturn,Is.True);Assert.That(h.Viewport.GetComponent<Camera>().transform.position,Is.EqualTo(optical));
            Assert.That(stageOrder,Is.EqualTo(new[]{"Launch","Cruise","Descent","FirstImage","Observe","Sample","Eclipse","LunarNight","TimePassage","ImaginedVisit","HistoricalReceipt","End"}));
            float duration=h.Clock.ActiveSeconds;Directory.CreateDirectory("QA/SUR-015");File.WriteAllText("QA/SUR-015/technical-journey.txt","AUTOMATED, not a human median.\nActive seconds "+duration+"\nSteps "+string.Join(" -> ",stageOrder)+"\n");Assert.That(duration,Is.InRange(300,420));h.Owner.StepEntered-=RecordStage;
            Assert.That(h.Visit.ReturnFromButton(),Is.True);until=Time.realtimeSinceStartup+15;while(archive.Owner.State!=AppState.Dossier&&Time.realtimeSinceStartup<until)yield return null;
            Assert.That(archive.Owner.State,Is.EqualTo(AppState.Dossier));Assert.That(Object.FindAnyObjectByType<SurveyorChapterHost>(),Is.Null);Assert.That(archive.Owner.Evidence.Records.Count,Is.Zero);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator RestartAndFocusCancelPendingScanAndSoilWithoutLateCallbacks()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene);yield return new EnterPlayMode();
            var archive=Object.FindAnyObjectByType<SurveyorArchiveHost>();archive.Initialize();yield return null;
            for(int attempt=0;attempt<3;attempt++)
            {
                if(attempt==0)archive.Owner.Begin(UnityEditor.AssetDatabase.LoadAssetAtPath<ChapterDefinition>(SurveyorChapterBuilder.ChapterPath));
                float until=Time.realtimeSinceStartup+30;while(archive.Owner.State!=AppState.Playing&&Time.realtimeSinceStartup<until)yield return null;
                var h=Object.FindAnyObjectByType<SurveyorChapterHost>();Assert.That(h,Is.Not.Null);h.Clock.enabled=false;h.Service.Mode=HoldMode.Hold;
                Advance(h,17.3f);Hold(h,2.7f);Advance(h,9.1f);Advance(h,10.2f);h.Cruise.Assist();Advance(h,6.2f);
                if(attempt==0){Hold(h,3.05f);Assert.That(h.GetComponent<SurveyorCommandDelay>().HasPending,Is.True);}
                else
                {
                    Hold(h,3.2f);Advance(h,15.2f);Advance(h,14);Hold(h,3.2f);Advance(h,24);Hold(h,2.2f);Advance(h,1.5f);
                    Assert.That(h.Television.Phase,Is.EqualTo(TelevisionPhase.Scanning));
                    if(attempt==2)
                    {
                        Advance(h,10.2f);
                        for(int n=0;n<3;n++){h.Service.NotifyInteractReleased();Assert.That(h.Observation.AssistFrame(),Is.True);Advance(h,7);yield return null;yield return null;h.Service.NotifyInteractReleased();Assert.That(h.Observation.BeginPrimary(),Is.True);Advance(h,1.6f);h.Observation.ReleasePrimary();Advance(h,1.5f);h.Service.NotifyInteractReleased();Assert.That(h.Observation.BeginPrimary(),Is.True);Advance(h,.2f);}
                        Advance(h,2.5f);Hold(h,6);Assert.That(h.Scoop.Passes,Is.GreaterThan(0));
                    }
                }
                yield return null;h.Pause.Controller.FocusLost();float frozen=h.Clock.ActiveSeconds;Advance(h,4);Assert.That(h.Clock.ActiveSeconds,Is.EqualTo(frozen));Assert.That(h.Pause.Controller.KeyToggle(),Is.False);Assert.That(h.Owner.IsPaused,Is.True);
                h.Pause.Controller.Continue();Assert.That(h.Owner.IsPaused,Is.False);
                if(attempt==1)
                {
                    var old=h.Settings.Current;h.PauseGame();h.Settings.Apply(v=>{v.languageCode="en";v.textScale=1.25f;});Assert.That(h.Localization.Get("launch.mission"),Is.EqualTo("SURVEYOR 3"));Assert.That(h.Localization.Language,Is.EqualTo("en"));
                    h.Settings.Apply(v=>{v.languageCode=old.languageCode;v.textScale=old.textScale;});h.Resume();
                }
                if(attempt==2)h.Owner.ReturnToDossier();else h.Restart();
                until=Time.realtimeSinceStartup+30;while((archive.Owner.State==AppState.Loading||archive.Owner.State==AppState.Unloading)&&Time.realtimeSinceStartup<until)yield return null;
                yield return null;Assert.That(h==null,Is.True,"Old chapter retained after unload");Assert.That(archive.Owner.Evidence.Records.Count,Is.Zero);
                if(attempt<2)Assert.That(archive.Owner.CurrentStepId,Is.EqualTo("Launch"));
            }
            Assert.That(archive.Owner.State,Is.EqualTo(AppState.Dossier));yield return new ExitPlayMode();
        }
        static void Hold(SurveyorChapterHost h,float seconds)
        {
            h.Service.NotifyInteractReleased();Assert.That(h.Service.TryGetActive(out var candidate),Is.True,"No action in "+h.Owner.CurrentStepId);
            Assert.That(h.Service.BeginHold(candidate.Id),Is.True,"Unavailable "+candidate.Id+" in "+h.Owner.CurrentStepId);Advance(h,seconds);h.Service.CancelHold();h.Service.NotifyInteractReleased();
        }
        public static void Advance(SurveyorChapterHost h,float seconds)
        {
            var queue=h.GetComponent<SurveyorCommandDelay>();var pages=h.GetComponent<SurveyorChapterSubtitles>();
            while(seconds>.00001f)
            {
                float dt=Mathf.Min(.05f,seconds);h.Clock.Tick(dt);float active=h.Clock.ActiveDeltaSeconds;h.Service.Tick(active);queue.Tick();h.Launch.Tick(active);h.Cruise.Tick(active);h.Descent.Tick(active);h.Television.Tick(active);h.Observation.Tick(active);h.Scoop.Tick(active);h.Soil.Tick(active);h.Eclipse.Tick(active);h.Night.Tick(active);h.Visit.Tick(active);pages.Tick(active);seconds-=dt;
            }h.Clock.Tick(0);
        }
        public static IEnumerator Capture(string name)
        {
            var documents=Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include);var originals=new Dictionary<UIDocument,PanelSettings>();var clones=new Dictionary<PanelSettings,PanelSettings>();var target=new RenderTexture(1280,720,0);target.Create();
            foreach(var doc in documents)
            {
                if(doc.panelSettings==null)continue;var old=doc.panelSettings;originals[doc]=old;if(!clones.TryGetValue(old,out var copy)){copy=Object.Instantiate(old);copy.targetTexture=target;copy.clearColor=false;clones.Add(old,copy);}doc.panelSettings=copy;
            }
            var saved=RenderTexture.active;RenderTexture.active=target;GL.Clear(true,true,Color.black);RenderTexture.active=saved;
            for(int i=0;i<12;i++)yield return null;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);saved=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=saved;Directory.CreateDirectory("QA/SUR-015");File.WriteAllBytes("QA/SUR-015/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
            foreach(var pair in originals)if(pair.Key!=null)pair.Key.panelSettings=pair.Value;target.Release();Object.Destroy(target);foreach(var p in clones.Values)Object.Destroy(p);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
