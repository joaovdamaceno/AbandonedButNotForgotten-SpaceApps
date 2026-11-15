using System.Collections;
using System.IO;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.NightTests
{
    public sealed class NightSceneTests
    {
        [UnityTest]public IEnumerator RealSceneShowsContactCalendarAndPauseWithoutMovingTheCamera()
        {
            EditorSceneManager.OpenScene(SurveyorNightBuilder.PreviewScene);yield return new EnterPlayMode();
            var host=Object.FindAnyObjectByType<SurveyorNightPreview>();host.Initialize();host.Clock.enabled=false;host.Begin();
            var position=host.Sequence.CameraDirector.TargetCamera.transform.position;
            Advance(host,6);yield return Capture(host,"last-contact-pt");
            Assert.That(host.Document.rootVisualElement.Q<Label>("contact").text,Is.EqualTo("4 MAI 1967 · ÚLTIMO CONTATO"));
            Advance(host,10);Assert.That(host.Flow.HudOpacity,Is.Zero);
            host.Pause.Controller.RequestPause();float t=host.Flow.TrackSeconds;Advance(host,3);Assert.That(host.Flow.TrackSeconds,Is.EqualTo(t));
            host.Pause.Controller.Continue();Assert.That(host.Owner.IsPaused,Is.False);
            Advance(host,5);Assert.That(host.Flow.CalendarKey,Is.EqualTo("sur.night.year1968"));yield return Capture(host,"year-1968");
            host.Localization.SetLanguage("en");host.Refresh();Advance(host,5);Assert.That(host.Flow.CalendarKey,Is.EqualTo("sur.night.nov1969"));
            yield return Capture(host,"november-1969-en");
            Assert.That(host.Document.rootVisualElement.Q<Label>("months").text,Is.EqualTo("31 MONTHS LATER"));
            Assert.That(host.Subtitles.CurrentText,Is.Empty);
            Advance(host,4.2f);Assert.That(host.Owner.CurrentStepId,Is.EqualTo(SurveyorNight.NextStep));
            Assert.That(host.Sequence.CameraDirector.TargetCamera.transform.position,Is.EqualTo(position));
            host.Restart();Advance(host,30.2f);Assert.That(host.Owner.CurrentStepId,Is.EqualTo(SurveyorNight.NextStep));
            host.Owner.ReturnToDossier();host.Output.Dispose();host.Viewport.enabled=false;yield return null;
            Assert.That(host.Output.Texture,Is.Null);Assert.That(host.Viewport.Texture,Is.Null);
            yield return new ExitPlayMode();
        }
        static void Advance(SurveyorNightPreview host,float seconds)
        {while(seconds>.00001f){float dt=Mathf.Min(.05f,seconds);host.Clock.Tick(dt);host.Flow.Tick(host.Clock.ActiveDeltaSeconds);seconds-=dt;}host.Clock.Tick(0);}
        static IEnumerator Capture(SurveyorNightPreview host,string name)
        {
            var doc=host.Document;var original=doc.panelSettings;var panel=Object.Instantiate(original);
            var target=new RenderTexture(1280,720,0);target.Create();panel.targetTexture=target;panel.clearColor=true;panel.colorClearValue=Color.black;doc.panelSettings=panel;
            for(int i=0;i<12;i++)yield return null;host.Refresh();yield return null;
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=previous;
            Directory.CreateDirectory("QA/SUR-012");File.WriteAllBytes($"QA/SUR-012/{name}.png",image.EncodeToPNG());Object.Destroy(image);
            doc.panelSettings=original;target.Release();Object.Destroy(target);Object.Destroy(panel);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
