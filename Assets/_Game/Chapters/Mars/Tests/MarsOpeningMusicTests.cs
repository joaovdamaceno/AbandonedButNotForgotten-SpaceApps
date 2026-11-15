using Abandoned.SpaceApps.Opportunity;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>MAR-022: a abertura da Opportunity (Intro, Landing, Activation) tem música; trocar de etapa não reinicia a mesma faixa.</summary>
    public class MarsOpeningMusicTests
    {
        GameObject root;
        MissionAudio audio;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MissionAudioTest");
            audio = root.AddComponent<MissionAudio>();
            audio.musicA = root.AddComponent<AudioSource>();
            audio.musicB = root.AddComponent<AudioSource>();
            var clipA = AudioClip.Create("orbitTest", 4410, 1, 44100, false);
            var clipB = AudioClip.Create("descentTest", 4410, 1, 44100, false);
            audio.cues = new[]
            {
                new MusicCue { id = "orbit", clip = clipA, gain = .62f, fade = 4 },
                new MusicCue { id = "descent", clip = clipB, gain = .55f, fade = 4 },
                new MusicCue { id = "arrival", clip = clipB, gain = .48f, fade = 4 },
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void OpeningStagesRequestTheirMusic()
        {
            audio.Enter(ChapterStage.Intro);
            Assert.That(audio.CurrentCue, Is.EqualTo("orbit"));
            audio.Enter(ChapterStage.Landing);
            Assert.That(audio.CurrentCue, Is.EqualTo("descent"));
            audio.Enter(ChapterStage.Activation);
            Assert.That(audio.CurrentCue, Is.EqualTo("arrival"));
        }

        [Test]
        public void ReenteringTheSameStageDoesNotRestartTheTrack()
        {
            audio.Enter(ChapterStage.Intro);
            var playing = audio.musicA.clip != null ? audio.musicA : audio.musicB;
            var clip = playing.clip;
            audio.Enter(ChapterStage.Intro);
            Assert.That(playing.clip, Is.SameAs(clip));
            Assert.That(audio.CurrentCue, Is.EqualTo("orbit"));
        }
    }
}
