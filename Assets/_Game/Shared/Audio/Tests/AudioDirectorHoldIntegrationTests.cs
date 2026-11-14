using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// AUD-002 com o <see cref="InteractionService"/> e o <see cref="InputRouter"/> REAIS (teclado simulado): o som do instrumento
    /// acompanha o estado real do comando de E mantido: liga ao pressionar, sobe com o progresso, desacelera ao soltar e termina
    /// ao concluir, e na pausa do shell nada continua soando.
    /// </summary>
    public class AudioDirectorHoldIntegrationTests : InputTestFixture
    {
        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        sealed class Voice : IAudioVoice
        {
            public AudioClip Clip;
            public bool Playing, Paused;
            public float Volume, Pitch = 1f;

            public void Play(AudioClip clip, bool loop, float volume, float pitch) { Clip = clip; Playing = true; Paused = false; Volume = volume; Pitch = pitch; }
            public void SetVolume(float volume) => Volume = volume;
            public void SetPitch(float pitch) => Pitch = pitch;
            public void Pause() => Paused = true;
            public void Resume() => Paused = false;
            public void Stop() => Playing = false;
        }

        Keyboard keyboard;
        GameObject host;
        InputRouter router;
        InteractionService interaction;
        AudioDirector director;
        AudioCatalog catalog;
        readonly List<Voice> voices = new List<Voice>();
        readonly List<Object> created = new List<Object>();

        AudioClip Clip(string name, float seconds)
        {
            var clip = AudioClip.Create(name, (int)(seconds * 8000), 1, 8000, false);
            created.Add(clip);
            return clip;
        }

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            host = new GameObject("services");
            var time = new FakeTime();
            router = host.AddComponent<InputRouter>();
            interaction = host.AddComponent<InteractionService>();
            router.Enable();
            interaction.Initialize(router, time);

            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            catalog.Set(new[]
            {
                new AudioCueDefinition("TOOL_START", AudioBus.Ui, clips: new[] { Clip("start", 0.5f) }),
                new AudioCueDefinition("TOOL_LOOP", AudioBus.Sfx, loop: true, clips: new[] { Clip("loop", 3f) }),
                new AudioCueDefinition("TOOL_DONE", AudioBus.Ui, clips: new[] { Clip("done", 0.5f) }),
                new AudioCueDefinition("TOOL_RELEASE", AudioBus.Ui, clips: new[] { Clip("release", 0.5f) }),
            });
            var go = new GameObject("audio");
            director = go.AddComponent<AudioDirector>();
            director.Initialize(catalog, time, new SettingsData { masterVolume = 1f, sfxVolume = 1f, uiVolume = 1f }, _ => { var v = new Voice(); voices.Add(v); return v; }, true);
            director.RegisterHold(new HoldSoundSet { CandidateId = "TOOL", StartCue = "TOOL_START", LoopCue = "TOOL_LOOP", DoneCue = "TOOL_DONE", ReleaseCue = "TOOL_RELEASE" });
            director.BindInteraction(interaction);
            created.Add(go);
        }

        public override void TearDown()
        {
            director.UnbindInteraction();
            router.Disable();
            Object.DestroyImmediate(host);
            foreach (var item in created)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }

            created.Clear();
            Object.DestroyImmediate(catalog);
            base.TearDown();
        }

        void Step(float dt)
        {
            interaction.Tick(dt);
            director.Tick(dt, dt);
        }

        void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += 1f / 60f)
                Step(1f / 60f);
        }

        Voice Playing(string clip) => voices.FirstOrDefault(v => v.Playing && v.Clip != null && v.Clip.name == clip);

        void Offer(float holdSeconds) => interaction.SetCandidate(new InteractionCandidate
        {
            Id = "TOOL", VerbKey = "V", ObjectKey = "O", Available = true, HoldSeconds = holdSeconds, Priority = 1,
        });

        [Test]
        public void TheToolSoundFollowsARealHoldFromPressToRelease()
        {
            using (router.Acquire(InputContext.Gameplay, "test"))
            {
                Offer(3f);
                Run(0.5f);
                Assert.That(voices.Any(v => v.Playing), Is.False, "sem hold, nada toca");

                Press(keyboard.eKey);
                Run(0.5f);
                var loop = Playing("loop");
                Assert.That(loop, Is.Not.Null, "o loop liga quando o hold começa de verdade");
                var early = loop.Pitch;
                Run(1.5f);
                Assert.That(loop.Pitch, Is.GreaterThan(early), "a altura acompanha o progresso real do hold");

                Release(keyboard.eKey);
                Run(0.1f);
                Assert.That(Playing("release"), Is.Not.Null, "som de soltar");
                Run(0.7f);
                Assert.That(Playing("loop"), Is.Null, "o loop termina depois da desaceleração");
            }
        }

        [Test]
        public void CompletingTheHoldEndsTheLoopWithAShortConfirmation()
        {
            using (router.Acquire(InputContext.Gameplay, "test"))
            {
                Offer(1f);
                Press(keyboard.eKey);
                Run(1.3f);
                Release(keyboard.eKey);
                Run(0.3f);
                Assert.That(Playing("loop"), Is.Null);
                Assert.That(voices.Any(v => v.Clip != null && v.Clip.name == "done"), Is.True, "confirmação curta ao concluir");
            }
        }

        [Test]
        public void ShellPauseSilencesTheToolImmediatelyAndNothingKeepsSoundingInTheMenu()
        {
            using (router.Acquire(InputContext.Gameplay, "test"))
            {
                Offer(3f);
                Press(keyboard.eKey);
                Run(0.4f);
                Assert.That(Playing("loop"), Is.Not.Null);

                director.SetPaused("shell", true);
                Assert.That(Playing("loop").Paused, Is.True);
                using (router.Acquire(InputContext.Paused, "shell"))
                    Run(1f);
                director.SetPaused("shell", false);
                Release(keyboard.eKey);
                Run(1f);
                Assert.That(voices.Any(v => v.Playing), Is.False, "depois de soltar nada fica tocando");
            }
        }
    }
}
