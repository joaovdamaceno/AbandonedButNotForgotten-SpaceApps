using System.Reflection;
using Abandoned.SpaceApps.Opportunity;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>MAR-022: a abertura de Marte (cena Mars) tem faixa própria que entra suave, respeita o volume do aplicativo, pausa e sai com fade.</summary>
    public class MarsOpeningTrackTests
    {
        GameObject root;
        MarsOpeningMusic music;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("OpeningMusicTest");
            music = root.AddComponent<MarsOpeningMusic>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static void Tick(MarsOpeningMusic target) => typeof(MarsOpeningMusic).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        [Test]
        public void TheOpeningTrackIsLoadableFromResources()
        {
            Assert.That(Resources.Load<AudioClip>(MarsOpeningMusic.ClipResource), Is.Not.Null);
        }

        [Test]
        public void VolumeFollowsFadeMasterAndMusicSettings()
        {
            Assert.That(MarsOpeningMusic.Volume(1f, 1f, 1f), Is.EqualTo(MarsOpeningMusic.BaseGain).Within(1e-5f));
            Assert.That(MarsOpeningMusic.Volume(1f, 1f, 0f), Is.EqualTo(0f));
            Assert.That(MarsOpeningMusic.Volume(0f, 1f, 1f), Is.EqualTo(0f));
            Assert.That(MarsOpeningMusic.Volume(1f, .5f, .8f), Is.EqualTo(.4f * MarsOpeningMusic.BaseGain).Within(1e-5f));
        }

        [Test]
        public void PausedChapterKeepsTheMusicSilentAndLevelFrozen()
        {
            var clip = AudioClip.Create("openingTest", 44100, 1, 44100, false);
            music.Begin(clip, 1f, 1f, () => true);
            Tick(music);
            Assert.That(music.Level, Is.EqualTo(0f));
        }

        [Test]
        public void BeginIgnoresAMissingClipAndFadeOutRemovesTheSource()
        {
            music.Begin(null, 1f, 1f, null);
            Assert.That(root.GetComponent<AudioSource>(), Is.Null);
            var clip = AudioClip.Create("openingTest", 44100, 1, 44100, false);
            music.Begin(clip, 1f, 1f, null);
            Assert.That(root.GetComponent<AudioSource>(), Is.Not.Null);
            music.FadeOutAndStop(.1f);
            Tick(music);
            Assert.That(music.IsPlaying, Is.False);
        }
    }
}
