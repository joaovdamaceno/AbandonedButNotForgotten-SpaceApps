using System.Collections;
using System.IO;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.VisitTests
{
    public sealed class VisitSceneTests
    {
        [UnityTest]public IEnumerator RealSceneShowsReconstructionThenCreditedPhotoAndReleasesItsTarget()
        {
            EditorSceneManager.OpenScene(SurveyorVisitBuilder.PreviewScene);yield return new EnterPlayMode();
            var host=Object.FindAnyObjectByType<SurveyorVisitPreview>();host.Initialize();host.Clock.enabled=false;host.Begin();
            var position=host.Sequence.CameraDirector.TargetCamera.transform.position;
            Advance(host,2);Assert.That(host.Flow.ShowWorld,Is.False);Assert.That(host.Subtitles.CurrentText,Does.Contain("never got to transmit"));
            Advance(host,6);yield return Capture(host,"intrepid-19-nov");
            Assert.That(host.Document.rootVisualElement.Q<Label>("date").text,Does.StartWith("19 NOV 1969"));
            Advance(host,4);yield return Capture(host,"intrepid-landed");
            Advance(host,12);yield return Capture(host,"visitor-20-nov");
            Assert.That(host.Document.rootVisualElement.Q<Label>("date").text,Does.StartWith("20 NOV 1969"));
            Advance(host,5.5f);yield return Capture(host,"glove-before-black");
            var camera=host.Sequence.CameraDirector.TargetCamera;
            for(int i=0;i<31;i++)
            {
                host.Actors.Evaluate(26+i*.15f,false);
                var mesh=host.Actors.Glove.GetComponent<MeshFilter>();
                foreach(var v in mesh.sharedMesh.vertices)
                    Assert.That(Vector3.Dot(camera.transform.forward,mesh.transform.TransformPoint(v)-position),Is.GreaterThan(camera.nearClipPlane+.01f),"Glove crosses the optics.");
            }
            Advance(host,4.5f);host.Localization.SetLanguage("en");host.Refresh();
            Assert.That(host.Flow.ShowArchive,Is.True);Assert.That(camera.enabled,Is.False);
            Assert.That(host.Document.rootVisualElement.Q<Label>("credit").text,Does.Contain("AS12-48-7134"));
            Assert.That(host.Flow.Ending.HistoricalPhoto.width,Is.LessThanOrEqualTo(1024));
            yield return Capture(host,"historical-photo-en");
            host.Pause.Controller.RequestPause();float time=host.Flow.TimeSeconds;Advance(host,4);Assert.That(host.Flow.TimeSeconds,Is.EqualTo(time));host.Pause.Controller.Continue();
            host.Localization.SetLanguage("pt");host.Refresh();Advance(host,11);yield return Capture(host,"memorial-pt");
            Assert.That(host.Flow.ShowMemorial,Is.True);Assert.That(host.Subtitles.CurrentText,Is.EqualTo("Ela ficou. O que aprendemos com ela veio conosco."));
            Advance(host,5.2f);Assert.That(host.Owner.IsFinished,Is.True);Assert.That(host.Flow.AwaitingReturn,Is.True);
            Assert.That(camera.transform.position,Is.EqualTo(position));
            host.Restart();Advance(host,50.2f);Assert.That(host.Owner.IsFinished,Is.True);Assert.That(host.Flow.ReturnFromButton(),Is.True);
            host.Viewport.enabled=false;yield return null;Assert.That(host.Viewport.Texture,Is.Null);
            Assert.That(host.Flow.Ending.HistoricalPhoto,Is.Not.Null);
            yield return new ExitPlayMode();
        }
        static void Advance(SurveyorVisitPreview host,float seconds)
        {while(seconds>.00001f){float dt=Mathf.Min(.05f,seconds);host.Clock.Tick(dt);host.Flow.Tick(host.Clock.ActiveDeltaSeconds);seconds-=dt;}host.Clock.Tick(0);}
        static IEnumerator Capture(SurveyorVisitPreview host,string name)
        {
            var doc=host.Document;var original=doc.panelSettings;var panel=Object.Instantiate(original);
            var target=new RenderTexture(1280,720,0);target.Create();panel.targetTexture=target;panel.clearColor=true;panel.colorClearValue=Color.black;doc.panelSettings=panel;
            for(int i=0;i<12;i++)yield return null;host.Refresh();yield return null;
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();RenderTexture.active=previous;
            Directory.CreateDirectory("QA/SUR-013");File.WriteAllBytes($"QA/SUR-013/{name}.png",image.EncodeToPNG());Object.Destroy(image);
            doc.panelSettings=original;target.Release();Object.Destroy(target);Object.Destroy(panel);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
