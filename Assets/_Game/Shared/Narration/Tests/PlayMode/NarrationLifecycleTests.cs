using System;
using System.Collections;
using System.Linq;
using Abandoned.SpaceApps.Integration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Tests
{
    public sealed class NarrationLifecycleTests
    {
        sealed class Voice : IAudioVoice
        {
            public AudioClip Clip;
            public void Play(AudioClip clip,bool loop,float volume,float pitch) { Clip=clip; }
            public void Stop() { Clip=null; }
            public void Pause() { }
            public void Resume() { }
            public void SetVolume(float value) { }
            public void SetPitch(float value) { }
        }

        [UnityTest] public IEnumerator DisablingTheChapterReleasesItsSpeechSource()
        {
            var go=new GameObject("Narration lifecycle");var clip=AudioClip.Create("recording",128000,1,32000,false);
            var catalog=ScriptableObject.CreateInstance<NarrationCatalog>();catalog.Configure("test",new[]{new NarrationLine("N.LINE",clip)});
            var player=go.AddComponent<NarrationPlayer>();player.Configure(catalog);var voice=new Voice();player.Initialize(null,voiceOverride:voice);
            player.TryPlay("N.LINE");Assert.That(player.IsBusy,Is.True);
            go.SetActive(false);yield return null;
            Assert.That(player.IsBusy,Is.False);Assert.That(voice.Clip,Is.Null);
            Object.Destroy(go);Object.Destroy(catalog);Object.Destroy(clip);yield return null;
        }

        static IEnumerator Until(Func<bool> condition,string label)
        {
            float deadline=Time.realtimeSinceStartup+75;
            while(!condition()&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(condition(),Is.True,label);
        }

        [UnityTest] public IEnumerator AllThreeChaptersStartEnglishSpeechPauseAndReleaseItOnReturnToTheDossier()
        {
            yield return SceneManager.LoadSceneAsync("Boot",LoadSceneMode.Single);yield return null;yield return null;
            var host=Object.FindFirstObjectByType<GameHost>();Assert.That(host,Is.Not.Null);
            var owner=host.Director;yield return Until(()=>owner.State==AppState.Dossier,"dossier");
            string original=host.Settings.Current.languageCode;
            try
            {
                // Portuguese captions still use the same English recordings.
                host.Settings.Apply(data=>data.languageCode="pt");
                foreach(var id in new[]{"pioneer10","mars","surveyor3"})
                {
                    owner.Begin(host.Catalog.Find(id).Chapter);
                    yield return Until(()=>owner.State==AppState.Playing,id+" opened");
                    var players=Object.FindObjectsByType<NarrationPlayer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
                    var player=players.Single(p=>p.Catalog!=null&&p.Catalog.ChapterId==id);
                    player.SetFocus(true);
                    yield return Until(()=>player.IsBusy,id+" English narration");
                    Assert.That(player.CurrentKey,Is.Not.Empty);
                    owner.Pause("narration-test");float remaining=player.RemainingSeconds;
                    yield return null;yield return null;
                    Assert.That(player.RemainingSeconds,Is.EqualTo(remaining).Within(.001f),id+" pause");
                    owner.Resume("narration-test");
                    Assert.That(owner.ReturnToDossier(),Is.True);
                    Assert.That(player==null||!player.IsBusy,Is.True,id+" immediate speech cancellation");
                    yield return Until(()=>owner.State==AppState.Dossier,id+" return");
                    Assert.That(Object.FindObjectsByType<NarrationPlayer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None),Is.Empty,id+" no leftover voice owner");
                }
            }
            finally { host.Settings.Apply(data=>data.languageCode=original); }
        }
    }
}
