using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Tests
{
    public sealed class NarrationTests
    {
        sealed class Clock : IActiveTimeSource { public float ActiveDeltaSeconds => .02f; }
        sealed class Voice : IAudioVoice
        {
            public int Plays, Pauses, Resumes, Stops;
            public AudioClip Clip; public float Pitch;
            public void Play(AudioClip clip, bool loop, float volume, float pitch) { Plays++; Clip=clip; Pitch=pitch; Assert.That(loop, Is.False); }
            public void SetVolume(float value) { }
            public void SetPitch(float value) { Pitch=value; }
            public void Pause() { Pauses++; }
            public void Resume() { Resumes++; }
            public void Stop() { Stops++; Clip=null; }
        }
        GameObject host; NarrationPlayer player; NarrationCatalog catalog; AudioClip first, second; Voice voice;
        [SetUp] public void SetUp()
        {
            first=AudioClip.Create("human-English-one",128000,1,32000,false);
            second=AudioClip.Create("human-English-two",64000,1,32000,false);
            catalog=ScriptableObject.CreateInstance<NarrationCatalog>();
            catalog.Configure("test",new[]{new NarrationLine("N.ONE",first),new NarrationLine("N.TWO",second)});
            host=new GameObject("Narration test");player=host.AddComponent<NarrationPlayer>();player.Configure(catalog);
            voice=new Voice();player.Initialize(new Clock(),voiceOverride:voice);
        }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(host);Object.DestroyImmediate(catalog);Object.DestroyImmediate(first);Object.DestroyImmediate(second); }

        [Test] public void PauseFreezesRemainingSpeechAndResumeDoesNotReplayOrChangePitch()
        {
            player.TryPlay("N.ONE");player.Tick(1f);float remaining=player.RemainingSeconds;
            player.SetPaused(true);player.Tick(30f);Assert.That(player.RemainingSeconds,Is.EqualTo(remaining));
            player.SetPaused(false);player.Tick(.5f);
            Assert.That(voice.Plays,Is.EqualTo(1));Assert.That(voice.Pauses,Is.EqualTo(1));Assert.That(voice.Resumes,Is.EqualTo(1));
            Assert.That(voice.Pitch,Is.EqualTo(1f));Assert.That(player.RemainingSeconds,Is.EqualTo(remaining-.5f).Within(.001f));
        }
        [Test] public void FocusAndMenuPauseHaveIndependentReasons()
        {
            player.TryPlay("N.ONE");player.SetFocus(false);player.SetPaused(true);
            player.SetFocus(true);Assert.That(player.IsPaused,Is.True);Assert.That(voice.Resumes,Is.Zero);
            player.SetPaused(false);Assert.That(player.IsPaused,Is.False);Assert.That(voice.Resumes,Is.EqualTo(1));
        }
        [Test] public void EnglishAudioIsIndependentOfSubtitleLanguageAndRedrawDoesNotReplay()
        {
            var text=new LocalizationService();text.AddTable("{\"texts\":[{\"key\":\"N.ONE\",\"group\":\"subtitle\",\"en\":\"One\",\"pt\":\"Um\"}]}","test");
            var subtitles=host.AddComponent<SubtitlePresenter>();subtitles.Initialize(text,null,new VisualElement());player.Bind(subtitles);
            subtitles.ShowCue(new SubtitleCue("cue","N.ONE",0f,6f));Assert.That(voice.Clip,Is.SameAs(first));
            text.SetLanguage("pt");text.SetLanguage("en");Assert.That(voice.Plays,Is.EqualTo(1));
            Assert.That(player.CurrentKey,Is.EqualTo("N.ONE"));
        }
        [Test] public void ClearAndReentryStopOldVoiceAndPlayTheNewTakeExactlyOnce()
        {
            player.TryPlay("N.ONE");player.TryPlay("N.ONE");Assert.That(voice.Plays,Is.EqualTo(1));
            player.Stop();player.Tick(20f);Assert.That(player.IsBusy,Is.False);Assert.That(voice.Clip,Is.Null);
            player.TryPlay("N.ONE");Assert.That(voice.Plays,Is.EqualTo(2));
            player.TryPlay("N.TWO");Assert.That(voice.Clip,Is.SameAs(second));Assert.That(player.CurrentKey,Is.EqualTo("N.TWO"));
        }
        [Test] public void CompletedLineDoesNotReplayUntilThePresentationChanges()
        {
            player.TryPlay("N.ONE");player.Tick(4.1f);Assert.That(player.IsBusy,Is.False);
            player.TryPlay("N.ONE");Assert.That(voice.Plays,Is.EqualTo(1));
            player.Present(null);player.TryPlay("N.ONE");Assert.That(voice.Plays,Is.EqualTo(2));
        }
        [Test] public void UnvoicedInstructionDoesNotInterruptButReplacingTheNarrativeCueDoes()
        {
            player.TryPlay("N.ONE");Assert.That(player.TryPlay("UI.PRESS_E"),Is.False);Assert.That(player.IsBusy,Is.True);
            player.Present("N.UNRECORDED");Assert.That(player.IsBusy,Is.False);Assert.That(voice.Clip,Is.Null);
        }
        [Test] public void MutingStopsImmediatelyAndUnmutingAllowsANewLine()
        {
            player.TryPlay("N.ONE");player.SetMuted(true);Assert.That(player.IsBusy,Is.False);
            player.SetMuted(false);player.TryPlay("N.TWO");
            Assert.That(player.IsBusy,Is.True);Assert.That(voice.Clip,Is.SameAs(second));
        }
        [Test] public void TimeMapKeepsCameraAndActorsContinuousAcrossLongerSpeechWindows()
        {
            var map=new NarrationTimeMap();map.AddWindow(8f,12.5f,18.8f);map.AddWindow(11f,6f,7.12f);
            Assert.That(map.ToAuthored(19.15f),Is.EqualTo(8f).Within(.001f));
            Assert.That(map.ToAuthored(26.62f),Is.EqualTo(11f).Within(.001f));
            float previous=-1;
            for(float time=0;time<30;time+=.01f){float mapped=map.ToAuthored(time);Assert.That(mapped,Is.GreaterThanOrEqualTo(previous));previous=mapped;}
        }
        [Test] public void MissingRecordingKeepsTheAuthoredReadingDuration()
        {
            var map=new NarrationTimeMap();map.AddWindow(8f,12.5f,0f);map.AddWindow(11f,6f,0f);
            Assert.That(map.DurationSeconds,Is.EqualTo(18.5f));Assert.That(map.ToAuthored(18.5f),Is.EqualTo(11f));
        }
        [TestCase("surveyor",25)] [TestCase("pioneer",61)] [TestCase("mars",30)]
        public void InstalledCatalogsHaveRealMonoClipsAndWebImportSettings(string chapter,int count)
        {
            var asset=AssetDatabase.LoadAssetAtPath<NarrationCatalog>(NarrationSetup.CatalogPath(chapter));
            Assert.That(asset,Is.Not.Null);Assert.That(asset.Validate(),Is.Empty);Assert.That(asset.Lines.Count,Is.EqualTo(count));
            foreach(var line in asset.Lines)
            {
                Assert.That(line.Clip.channels,Is.EqualTo(1),line.TextKey);Assert.That(line.Clip.frequency,Is.GreaterThan(0),line.TextKey);
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(line.Clip));
                var web=importer.GetOverrideSampleSettings("WebGL");
                Assert.That(importer.defaultSampleSettings.sampleRateOverride,Is.EqualTo(32000));
                // Web's AAC decoder may use 44.1/48 kHz; durations, not sample
                // indices, govern narration. The source/default request is 32k.
                Assert.That(web.preloadAudioData,Is.False,line.TextKey);Assert.That(web.loadType,Is.EqualTo(AudioClipLoadType.CompressedInMemory));
                Assert.That(line.Clip.length,Is.GreaterThan(.25f));
            }
        }
        [Test] public void VoiceWindowFittingIsIdempotentAndPreventsTheNextCueFromCuttingSpeech()
        {
            var sequence=SequenceDefinition.Create("narration-test",new[]{new ShotDefinition("shot",12f,"world",new[]{new ShotKeyframe(0,Vector3.zero,Vector3.zero,40),new ShotKeyframe(12,Vector3.one,Vector3.zero,40)})},
                new[]{new SubtitleCue("a","N.ONE",0,2),new SubtitleCue("b","N.TWO",2,3)});
            try
            {
                Assert.That(NarrationSetup.FitSequence(sequence,catalog),Is.True);
                Assert.That(sequence.Cues[0].EndSeconds,Is.GreaterThanOrEqualTo(4.19f));
                Assert.That(sequence.Cues[1].StartSeconds,Is.GreaterThanOrEqualTo(sequence.Cues[0].EndSeconds));
                Assert.That(NarrationSetup.FitSequence(sequence,catalog),Is.False);
            }
            finally {Object.DestroyImmediate(sequence);}
        }
        [Test] public void ConfiguredScenesCanBeReopenedWithOneEnglishVoiceOwnerEach()
        { NarrationSetup.ValidateInstalled(); }
    }
}
