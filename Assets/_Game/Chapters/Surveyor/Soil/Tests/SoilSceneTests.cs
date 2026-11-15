using System.Collections;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.SoilTests
{
    public sealed class SoilSceneTests
    {
        [UnityTest]public IEnumerator SameAreaHasDifferentRealBeforeAndAfterPictures()
        {
            EditorSceneManager.OpenScene(SurveyorSoilBuilder.Scene);yield return new EnterPlayMode();
            var host=Object.FindAnyObjectByType<SurveyorSoilPreview>();host.Initialize();host.Clock.enabled=false;host.Service.Mode=HoldMode.Hold;host.Begin();var position=host.Viewport.GetComponent<Camera>().transform.position;
            Advance(host,2.5f);host.Service.NotifyInteractReleased();Assert.That(host.Service.BeginHold("sur.scoop.dig"),Is.True);Advance(host,17);
            host.Service.NotifyInteractReleased();Assert.That(host.Service.BeginHold(SurveyorSoilTests.PressId),Is.True);Advance(host,2);yield return Capture(host,"pressing");Advance(host,5.4f);
            host.Service.NotifyInteractReleased();Assert.That(host.Service.BeginHold(SurveyorSoilTests.ImpactId),Is.True);Advance(host,2.5f);yield return Capture(host,"impact");Advance(host,3.6f);
            Assert.That(host.Flow.HasBothCaptures,Is.True);Assert.That(host.Flow.Before.GetRawTextureData<byte>().SequenceEqual(host.Flow.After.GetRawTextureData<byte>()),Is.False);
            yield return Capture(host,"before-after");Advance(host,8);host.Localization.SetLanguage("en");host.GetComponent<SurveyorSoilPanel>().Refresh();yield return Capture(host,"historical-photo");
            Advance(host,12.2f);Assert.That(host.Owner.CurrentStepId,Is.EqualTo("Eclipse"));Assert.That(host.Viewport.GetComponent<Camera>().transform.position,Is.EqualTo(position));
            host.Owner.ReturnToDossier();host.Viewport.enabled=false;yield return null;Assert.That(host.Flow.Before,Is.Null);Assert.That(host.Flow.After,Is.Null);Assert.That(host.Viewport.Texture,Is.Null);
            yield return new ExitPlayMode();
        }
        static void Advance(SurveyorSoilPreview h,float seconds){while(seconds>.00001f){float dt=Mathf.Min(.05f,seconds);h.Clock.Tick(dt);h.Service.Tick(h.Clock.ActiveDeltaSeconds);h.Scoop.Tick(h.Clock.ActiveDeltaSeconds);h.Flow.Tick(h.Clock.ActiveDeltaSeconds);seconds-=dt;}h.Clock.Tick(0);}
        static IEnumerator Capture(SurveyorSoilPreview h,string name)
        {
            var doc=h.Document;var old=doc.panelSettings;var panel=Object.Instantiate(old);var target=new RenderTexture(1280,720,0);target.Create();panel.targetTexture=target;panel.clearColor=true;panel.colorClearValue=Color.black;doc.panelSettings=panel;
            for(int i=0;i<10;i++)yield return null;h.GetComponent<SurveyorSoilPanel>().Refresh();yield return null;
            var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();RenderTexture.active=previous;
            Directory.CreateDirectory("QA/SUR-010");File.WriteAllBytes("QA/SUR-010/"+name+".png",texture.EncodeToPNG());Object.Destroy(texture);doc.panelSettings=old;target.Release();Object.Destroy(target);Object.Destroy(panel);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(Application.isPlaying)yield return new ExitPlayMode();}
    }
}
