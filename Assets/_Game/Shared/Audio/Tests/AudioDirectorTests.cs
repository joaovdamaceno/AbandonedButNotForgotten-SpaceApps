using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// AUD-002: crossfades de 0,8–2 s, um único dono do volume de cada voz, buses independentes vindos das preferências, ducking,
    /// silêncio real, pausa, loops com teto, sons de instrumento ligados aos eventos reais do HoldAction, liberação do áudio no Web
    /// e reinício. Vozes falsas (sem placa de som) e clipes sintéticos: nenhum arquivo licenciado existe ainda.
    /// </summary>
    public class AudioDirectorTests
    {
        sealed class FakeVoice : IAudioVoice
        {
            public string Name;
            public AudioClip Clip;
            public bool Playing, Paused, Loop;
            public float Volume, Pitch = 1f;
            public int PlayCount, VolumeWrites, StopCount;

            public void Play(AudioClip clip, bool loop, float volume, float pitch)
            {
                Clip = clip; Loop = loop; Volume = volume; Pitch = pitch; Playing = true; Paused = false; PlayCount++;
            }

            public void SetVolume(float volume) { Volume = volume; VolumeWrites++; }
            public void SetPitch(float pitch) => Pitch = pitch;
            public void Pause() => Paused = true;
            public void Resume() => Paused = false;
            public void Stop() { Playing = false; Paused = false; StopCount++; }
        }

        sealed class FakeInteraction : IInteractionService
        {
            public event Action<string> HoldStarted, HoldCompleted, HoldCanceled;
            public event Action<string, float> HoldProgress;
            public bool AwaitingRelease => false;
            public string HoldingId => null;
            public bool TryGetActive(out InteractionCandidate candidate) { candidate = default; return false; }
            public void SetCandidate(InteractionCandidate candidate) { }
            public void ClearCandidate(string id) { }
            public bool CanBeginHold(string id) => false;
            public bool BeginHold(string id) => false;
            public void CancelHold() { }
            public void RequireRelease() { }
            public void NotifyInteractReleased() { }
            public void Start(string id) => HoldStarted?.Invoke(id);
            public void Progress(string id, float p) => HoldProgress?.Invoke(id, p);
            public void Complete(string id) => HoldCompleted?.Invoke(id);
            public void Cancel(string id) => HoldCanceled?.Invoke(id);
        }

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        readonly List<FakeVoice> voices = new List<FakeVoice>();
        AudioDirector director;
        AudioCatalog catalog;
        SettingsData settings;

        AudioClip Clip(string name, float seconds)
        {
            var clip = AudioClip.Create(name, Mathf.Max(1, (int)(seconds * 8000)), 1, 8000, false);
            created.Add(clip);
            return clip;
        }

        [SetUp]
        public void SetUp()
        {
            voices.Clear();
            settings = new SettingsData { masterVolume = 1f, musicVolume = 1f, sfxVolume = 1f, uiVolume = 1f };
            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            created.Add(catalog);
            catalog.Set(new[]
            {
                new AudioCueDefinition("MUS_A", AudioBus.Music, loop: true, volume: 1f, clips: new[] { Clip("musA", 40f) }),
                new AudioCueDefinition("MUS_B", AudioBus.Music, loop: true, volume: 1f, clips: new[] { Clip("musB", 40f) }),
                new AudioCueDefinition("MUS_PENDING", AudioBus.Music, loop: true),
                new AudioCueDefinition("SFX_TICK", AudioBus.Sfx, volume: 1f, pitchVariation: 0.1f, clips: new[] { Clip("t1", 0.5f), Clip("t2", 0.5f), Clip("t3", 0.5f) }),
                new AudioCueDefinition("SFX_ONE", AudioBus.Sfx, volume: 1f, clips: new[] { Clip("one", 1f) }),
                new AudioCueDefinition("UI_CLICK", AudioBus.Ui, volume: 1f, clips: new[] { Clip("click", 0.3f) }),
                new AudioCueDefinition("SFX_LOOP", AudioBus.Sfx, loop: true, volume: 1f, maxSeconds: 12f, clips: new[] { Clip("loop", 3f) }),
                new AudioCueDefinition("SFX_LOOP_DEFAULT", AudioBus.Sfx, loop: true, volume: 1f, clips: new[] { Clip("loopd", 3f) }),
                new AudioCueDefinition("HOLD_START", AudioBus.Ui, clips: new[] { Clip("hs", 0.5f) }),
                new AudioCueDefinition("HOLD_LOOP", AudioBus.Sfx, loop: true, volume: 1f, clips: new[] { Clip("hl", 3f) }),
                new AudioCueDefinition("HOLD_DONE", AudioBus.Ui, clips: new[] { Clip("hd", 0.3f) }),
                new AudioCueDefinition("HOLD_RELEASE", AudioBus.Ui, clips: new[] { Clip("hr", 0.3f) }),
            });
            var go = new GameObject("audio");
            created.Add(go);
            director = go.AddComponent<AudioDirector>();
            Init();
        }

        void Init(bool? unlocked = true)
        {
            voices.Clear();
            director.Initialize(catalog, null, settings, name => { var v = new FakeVoice { Name = name }; voices.Add(v); return v; }, unlocked);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
            {
                if (item != null)
                    UnityEngine.Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        void Run(float seconds, float dt = 1f / 60f, bool paused = false)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += dt)
                director.Tick(paused ? 0f : dt, dt);
        }

        FakeVoice Playing(string clipName) => voices.FirstOrDefault(v => v.Playing && v.Clip != null && v.Clip.name == clipName);

        // ---------- Catálogo ----------

        [Test]
        public void TheCatalogValidatesIdsBusesAndLoopLengthsAndPendingCuesAreNotErrors()
        {
            Assert.That(catalog.Validate(), Is.Empty);
            Assert.That(catalog.PendingCueIds(), Is.EqualTo(new[] { "MUS_PENDING" }));

            var bad = ScriptableObject.CreateInstance<AudioCatalog>();
            created.Add(bad);
            bad.Set(new[]
            {
                new AudioCueDefinition("DUP", AudioBus.Sfx), new AudioCueDefinition("DUP", AudioBus.Sfx),
                new AudioCueDefinition("1BAD", AudioBus.Sfx),
                new AudioCueDefinition("MASTER", AudioBus.Master),
                new AudioCueDefinition("SHORT_MUSIC", AudioBus.Music, loop: true, clips: new[] { Clip("short", 10f) }),
                new AudioCueDefinition("TINY_LOOP", AudioBus.Sfx, loop: true, clips: new[] { Clip("tiny", 0.2f) }),
                new AudioCueDefinition("LIMIT", AudioBus.Sfx, loop: false, maxSeconds: 5f),
            });
            var errors = bad.Validate();
            Assert.That(errors.Any(e => e.Contains("duplicado")), Is.True);
            Assert.That(errors.Any(e => e.Contains("1BAD")), Is.True);
            Assert.That(errors.Any(e => e.Contains("Master")), Is.True);
            Assert.That(errors.Any(e => e.Contains("cama de música")), Is.True, "música em loop tem pelo menos 30 s");
            Assert.That(errors.Any(e => e.Contains("clique")), Is.True, "loop curto demais vira clique na emenda");
            Assert.That(errors.Any(e => e.Contains("maxSeconds só vale")), Is.True);
        }

        // ---------- Música ----------

        [Test]
        public void MusicCrossfadesWithEqualPowerWithinTheClampedFadeAndTheOldVoiceStops()
        {
            director.SetCue("MUS_A", 1.5f);
            Assert.That(director.CurrentMusicId, Is.EqualTo("MUS_A"));
            Run(1.6f);
            Assert.That(Playing("musA").Volume, Is.EqualTo(1f).Within(0.01f), "entra por inteiro depois do fade");

            director.SetCue("MUS_B", 1.5f);
            Run(0.75f);
            var a = Playing("musA");
            var b = Playing("musB");
            Assert.That(a, Is.Not.Null);
            Assert.That(b, Is.Not.Null);
            Assert.That(a.Volume * a.Volume + b.Volume * b.Volume, Is.EqualTo(1f).Within(0.06f), "potência constante no meio do crossfade");
            Run(1.0f);
            Assert.That(Playing("musA"), Is.Null, "a música antiga para quando o fade acaba");
            Assert.That(Playing("musB").Volume, Is.EqualTo(1f).Within(0.01f));
            Assert.That(director.ActiveMusicVoices, Is.EqualTo(1));
        }

        [Test]
        public void RequestingTheSameCueDoesNotRestartItAndFadesAreClampedToZeroPointEightToTwoSeconds()
        {
            director.SetCue("MUS_A", 1f);
            Run(0.2f);
            var voice = Playing("musA");
            var plays = voice.PlayCount;
            director.SetCue("MUS_A", 2f);
            Run(0.2f);
            Assert.That(Playing("musA").PlayCount, Is.EqualTo(plays), "o mesmo cue não reinicia (estado seguinte com o mesmo cue)");

            Assert.That(AudioDirector.ClampFade(0.1f), Is.EqualTo(0.8f));
            Assert.That(AudioDirector.ClampFade(5f), Is.EqualTo(2f));
            Assert.That(AudioDirector.ClampFade(1.5f), Is.EqualTo(1.5f));
            Assert.That(AudioDirector.ClampFade(0f), Is.Zero, "0 = corte seco, só onde o roteiro pede silêncio");

            director.SetCue("MUS_B", 0f);
            Assert.That(Playing("musA"), Is.Null, "corte seco");
            Assert.That(Playing("musB"), Is.Not.Null);
        }

        [Test]
        public void EachVoiceVolumeHasASingleWriterPerFrameEvenDuringOverlappingFades()
        {
            director.SetCue("MUS_A", 1.5f);
            Run(0.4f);
            director.SetCue("MUS_B", 1.5f);
            director.SetCue("MUS_A", 1.5f); // pedido no meio do fade: ninguém disputa o volume da mesma fonte
            foreach (var v in voices)
                v.VolumeWrites = 0;
            director.Tick(1f / 60f, 1f / 60f);
            Assert.That(voices.Where(v => v.Playing).All(v => v.VolumeWrites == 1), Is.True, "uma escrita por voz ativa por quadro");
            Run(3f);
            Assert.That(director.ActiveMusicVoices, Is.EqualTo(1));
            Assert.That(director.CurrentMusicId, Is.EqualTo("MUS_A"));
        }

        [Test]
        public void PendingCuesAreSilentButKeepTheStateAndUnknownCuesAreRecordedWithoutThrowing()
        {
            director.SetCue("MUS_PENDING", 1f);
            Assert.That(director.CurrentMusicId, Is.EqualTo("MUS_PENDING"));
            Assert.That(director.ActiveMusicVoices, Is.Zero, "sem arquivo licenciado: silêncio");
            director.PlayEffect("SFX_NAO_EXISTE");
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SFX_NAO_EXISTE"));
            Assert.That(director.MissingCueRequests, Does.Contain("SFX_NAO_EXISTE"));
            Assert.That(director.ActiveOneShotVoices, Is.Zero);
        }

        [Test]
        public void TheLevelOfTheMusicFollowsTheRequestAndDuckingIsMinusSixDecibelsWithAttackAndRelease()
        {
            director.SetCue("MUS_A", 1f);
            Run(1.2f);
            director.SetMusicLevel(0.15f, 1.5f);
            Run(1.6f);
            Assert.That(Playing("musA").Volume, Is.EqualTo(0.15f).Within(0.01f), "a música quase silencia na virada");
            director.SetMusicLevel(1f, 1.5f);
            Run(1.6f);

            director.Duck("subtitle", true);
            director.Duck("result", true);
            Run(0.35f);
            Assert.That(director.DuckMultiplier, Is.EqualTo(AudioDirector.DuckGain).Within(0.01f), "−6 dB em 0,3 s");
            Assert.That(Playing("musA").Volume, Is.EqualTo(AudioDirector.DuckGain).Within(0.02f));
            director.Duck("subtitle", false);
            Run(1f);
            Assert.That(director.DuckMultiplier, Is.EqualTo(AudioDirector.DuckGain).Within(0.01f), "um motivo ainda ativo mantém o ducking");
            director.Duck("result", false);
            Run(0.9f);
            Assert.That(director.DuckMultiplier, Is.EqualTo(1f).Within(0.01f), "retorno em 0,8 s");
        }

        // ---------- Buses e preferências ----------

        [Test]
        public void BusesAreIndependentAndMasterMultipliesAllOfThem()
        {
            settings.masterVolume = 0.5f;
            settings.musicVolume = 0f;
            settings.sfxVolume = 1f;
            settings.uiVolume = 0.6f;
            director.ApplySettings(settings, immediate: true);
            director.SetCue("MUS_A", 0f);
            director.PlayEffect("SFX_ONE");
            director.PlayEffect("UI_CLICK");
            Run(0.1f);
            Assert.That(Playing("musA").Volume, Is.Zero, "música em 0 não apaga os outros");
            Assert.That(Playing("one").Volume, Is.EqualTo(0.5f).Within(0.01f), "SFX = master × sfx");
            Assert.That(Playing("click").Volume, Is.EqualTo(0.3f).Within(0.01f), "UI = master × ui");

            settings.masterVolume = 0f;
            director.ApplySettings(settings, immediate: true);
            director.PlayEffect("SFX_ONE");
            Run(0.1f);
            Assert.That(voices.Where(v => v.Playing).All(v => v.Volume == 0f), Is.True, "master 0 silencia tudo");
        }

        [Test]
        public void ChangingAVolumeIsSmoothedSoThereIsNoStep()
        {
            director.SetCue("MUS_A", 0f);
            Run(0.05f);
            director.SetBusVolume(AudioBus.Music, 0f);
            var before = Playing("musA").Volume;
            director.Tick(1f / 60f, 1f / 60f);
            Assert.That(before - Playing("musA").Volume, Is.InRange(0.01f, 0.3f), "desce em rampa, não em degrau");
            Run(0.5f);
            Assert.That(Playing("musA").Volume, Is.Zero);
        }

        // ---------- Web: liberação do áudio ----------

        [Test]
        public void AudioIsNotAvailableUntilUnlockedAndTheMusicRequestedBeforeTheClickStartsThen()
        {
            Init(unlocked: false);
            Assert.That(director.Available, Is.False);
            director.SetCue("MUS_A", 1f);
            director.PlayEffect("SFX_ONE");
            director.StartLoop("SFX_LOOP");
            Run(0.2f);
            Assert.That(voices.Any(v => v.Playing), Is.False, "nada toca antes do clique");
            director.Unlock();
            Assert.That(director.Available, Is.True);
            Run(0.2f);
            Assert.That(Playing("musA"), Is.Not.Null, "a música pedida antes do clique entra ao liberar");
            Assert.That(director.CurrentMusicId, Is.EqualTo("MUS_A"));
            Assert.That(voices.Count(v => v.Playing), Is.EqualTo(1), "efeitos pedidos antes do clique foram descartados, não empilhados");
        }

        // ---------- Efeitos ----------

        [Test]
        public void EffectVariationsNeverRepeatTwiceInARowAndPitchStaysInsideTheVariation()
        {
            director.SetSeed(11);
            var picks = new List<string>();
            for (var i = 0; i < 30; i++)
            {
                director.PlayEffect("SFX_TICK");
                var latest = voices.Where(v => v.Playing && v.Clip != null && v.Clip.name.StartsWith("t")).OrderByDescending(v => v.PlayCount).First();
                picks.Add(latest.Clip.name);
                Assert.That(latest.Pitch, Is.InRange(0.9f, 1.1f), "altura dentro de ±10%");
                Run(0.6f);
            }

            for (var i = 1; i < picks.Count; i++)
                Assert.That(picks[i], Is.Not.EqualTo(picks[i - 1]), "variação repetida na jogada " + i);
            Assert.That(picks.Distinct().Count(), Is.EqualTo(3), "as três variações aparecem");
        }

        [Test]
        public void LoopsStopThemselvesAtTheirLimitAndMusicBedsHaveNone()
        {
            director.StartLoop("SFX_LOOP");
            director.StartLoop("SFX_LOOP"); // não duplica
            director.StartLoop("SFX_LOOP_DEFAULT");
            director.SetCue("MUS_A", 1f);
            Run(1f);
            Assert.That(director.ActiveLoopVoices, Is.EqualTo(2));
            Run(11.8f);
            Run(1f);
            Assert.That(director.ActiveLoopVoices, Is.EqualTo(1), "o loop de 12 s parou sozinho (fade de 0,5 s)");
            Run(30f);
            Assert.That(director.ActiveLoopVoices, Is.Zero, "o loop sem teto declarado usa o padrão de 40 s");
            Run(60f);
            Assert.That(Playing("musA"), Is.Not.Null, "a cama de música não tem teto");
        }

        [Test]
        public void RealSilenceMutesEverythingButTheInterfaceDropsNewEffectsAndReturnsAfterwards()
        {
            director.SetCue("MUS_A", 0f);
            director.StartLoop("SFX_LOOP_DEFAULT", 0f);
            Run(0.5f);
            director.Silence(2.5f);
            Run(0.2f);
            Assert.That(Playing("musA").Volume, Is.Zero.Within(1e-4f), "a música cai em 0,15 s");
            Assert.That(Playing("loopd").Volume, Is.Zero.Within(1e-4f), "sem chiado em loop");
            director.PlayEffect("SFX_ONE");
            Assert.That(Playing("one"), Is.Null, "nenhum efeito novo durante o silêncio");
            director.PlayEffect("UI_CLICK");
            Assert.That(Playing("click"), Is.Not.Null, "a interface responde");

            Run(2.4f, paused: true);
            Assert.That(director.SilenceRemaining, Is.EqualTo(2.3f).Within(0.05f), "o silêncio conta tempo ativo: pausar congela");
            Run(2.4f);
            Assert.That(director.IsSilent, Is.False);
            Run(1.1f);
            Assert.That(Playing("musA").Volume, Is.EqualTo(1f).Within(0.02f), "volta em 1 s");
        }

        // ---------- Pausa e reinício ----------

        [Test]
        public void PausingFreezesMusicAndEffectsButNotTheInterfaceAndEveryPauseSourceMustRelease()
        {
            director.SetCue("MUS_A", 1.5f);
            Run(0.5f);
            director.PlayEffect("SFX_ONE");
            director.PlayEffect("UI_CLICK");
            director.SetPaused("shell", true);
            director.SetPaused("focus", true);
            Assert.That(Playing("musA").Paused, Is.True);
            Assert.That(Playing("one").Paused, Is.True);
            Assert.That(Playing("click").Paused, Is.False, "a interface responde na pausa");

            var volume = Playing("musA").Volume;
            Run(2f, paused: true); // tempo ativo zero: o fade não anda
            Assert.That(Playing("musA").Volume, Is.EqualTo(volume).Within(1e-4f));
            director.SetPaused("shell", false);
            Assert.That(Playing("musA").Paused, Is.True, "o foco perdido ainda segura");
            director.SetPaused("focus", false);
            Assert.That(Playing("musA").Paused, Is.False);
            Assert.That(director.IsPaused, Is.False);
        }

        [Test]
        public void StopAllAndResetRestoreTheInitialSoundStateSoRestartingWorks()
        {
            director.SetCue("MUS_A", 1f);
            director.StartLoop("SFX_LOOP");
            director.Duck("x", true);
            director.Silence(5f);
            director.SetMusicLevel(0.2f, 0f);
            director.SetPaused("shell", true);
            Run(0.5f);

            director.StopAll(0.5f);
            Assert.That(director.CurrentMusicId, Is.Null);
            Run(0.7f);
            Assert.That(voices.Any(v => v.Playing), Is.False, "StopAll não deixa fonte nem loop esquecidos");

            director.ResetState();
            Assert.That(director.IsPaused, Is.False);
            Assert.That(director.IsSilent, Is.False);
            Assert.That(director.DuckMultiplier, Is.EqualTo(1f));
            Assert.That(director.MusicLevel, Is.EqualTo(1f));
            director.SetCue("MUS_A", 1f); // o mesmo cue volta a tocar depois do reinício
            Run(1.2f);
            Assert.That(Playing("musA").Volume, Is.EqualTo(1f).Within(0.02f));
        }

        // ---------- Instrumentos (HoldAction) ----------

        [Test]
        public void ToolSoundsFollowTheRealHoldEventsAndNeverPlayWithoutAHold()
        {
            var interaction = new FakeInteraction();
            director.RegisterHold(new HoldSoundSet { CandidateId = "TOOL", StartCue = "HOLD_START", LoopCue = "HOLD_LOOP", DoneCue = "HOLD_DONE", ReleaseCue = "HOLD_RELEASE" });
            director.BindInteraction(interaction);
            Run(1f);
            Assert.That(voices.Any(v => v.Playing), Is.False, "sem hold, nada toca");
            interaction.Start("OUTRO");
            Assert.That(voices.Any(v => v.Playing), Is.False, "candidato sem som registrado é ignorado");

            interaction.Start("TOOL");
            Run(0.2f);
            Assert.That(Playing("hs"), Is.Not.Null, "clique de início");
            var loop = Playing("hl");
            Assert.That(loop, Is.Not.Null, "loop do instrumento");

            interaction.Progress("TOOL", 0f);
            Run(0.05f);
            var low = loop.Pitch;
            var lowVolume = loop.Volume;
            interaction.Progress("TOOL", 1f);
            Run(0.05f);
            Assert.That(loop.Pitch, Is.GreaterThan(low), "a altura sobe com o progresso real");
            Assert.That(loop.Volume, Is.GreaterThan(lowVolume));

            interaction.Cancel("TOOL"); // soltou
            Run(0.25f);
            Assert.That(loop.Pitch, Is.LessThan(AudioDirector.HoldPitchMax), "desacelera ao soltar");
            Assert.That(Playing("hr"), Is.Not.Null, "som de soltar");
            Run(0.6f);
            Assert.That(Playing("hl"), Is.Null, "o loop para depois de 0,5 s");

            interaction.Start("TOOL");
            Run(0.2f);
            interaction.Complete("TOOL");
            Run(0.3f);
            Assert.That(Playing("hl"), Is.Null, "concluir encerra o loop");
            Assert.That(voices.Count(v => v.Clip != null && v.Clip.name == "hd" && v.PlayCount > 0), Is.GreaterThan(0), "confirmação curta");

            director.UnbindInteraction();
            interaction.Start("TOOL");
            Run(0.2f);
            Assert.That(Playing("hl"), Is.Null, "desligado do HoldAction, o diretor não reage");
        }

        [Test]
        public void TheToolLoopIsPausedWithTheShellAndStopsAtItsLimit()
        {
            var interaction = new FakeInteraction();
            director.RegisterHold(new HoldSoundSet { CandidateId = "TOOL", LoopCue = "HOLD_LOOP" });
            director.BindInteraction(interaction);
            interaction.Start("TOOL");
            Run(0.3f);
            director.SetPaused("shell", true);
            Assert.That(Playing("hl").Paused, Is.True, "nada soa na pausa nem no menu");
            director.SetPaused("shell", false);
            Run(41f);
            Assert.That(Playing("hl"), Is.Null, "nenhum loop agressivo depois do teto (40 s, padrão)");
        }

        // ---------- Pioneer ----------

        [Test]
        public void ThePioneerAdapterForwardsThePortCallsToTheDirector()
        {
            Init(unlocked: false);
            IPioneerAudioPort port = new PioneerAudioAdapter(director);
            Assert.That(port.Available, Is.False);
            port.SetMusic("MUS_A", 1.5f);
            director.Unlock();
            Assert.That(port.Available, Is.True);
            Run(1.6f);
            Assert.That(Playing("musA").Volume, Is.EqualTo(1f).Within(0.02f));
            port.SetMusicLevel(0.5f, 1f);
            port.PlayEffect("SFX_ONE");
            Assert.That(Playing("one"), Is.Not.Null);
            Run(1.1f);
            Assert.That(Playing("musA").Volume, Is.EqualTo(0.5f).Within(0.02f));
            port.Silence(2.5f);
            Assert.That(director.SilenceRemaining, Is.EqualTo(2.5f).Within(0.01f));
            port.SetPaused(true);
            Assert.That(director.IsPaused, Is.True);
            port.SetPaused(false);
            port.StopAll(0.5f);
            Run(0.6f);
            Assert.That(voices.Any(v => v.Playing), Is.False);
        }

        // ---------- Montagem ----------

        [Test]
        public void TheSetupCreatesAConformingCatalogWithAllPlannedCuesPendingAndAPrefabAndIsIdempotent()
        {
            Assert.That(AudioDirectorSetup.Create(), Is.Empty);
            var first = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioDirectorSetup.CatalogPath);
            var count = first.Cues.Count;
            Assert.That(AudioDirectorSetup.Create(), Is.Empty);
            Assert.That(first.Cues.Count, Is.EqualTo(count), "reexecutar não duplica");
            foreach (var planned in AudioDirectorSetup.PlannedCues())
                Assert.That(first.Contains(planned.Id), Is.True, planned.Id);
            Assert.That(first.PendingCueIds().Count, Is.GreaterThan(0), "nenhum áudio foi adquirido: os cues nascem pendentes");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AudioDirectorSetup.PrefabPath);
            Assert.That(prefab.GetComponent<AudioDirector>().ValidateSetup(), Is.Empty);
            Assert.That(new GameObject("bare").AddComponent<AudioDirector>().ValidateSetup(), Is.Not.Empty);
            foreach (var bare in UnityEngine.Object.FindObjectsByType<AudioDirector>(FindObjectsSortMode.None).Where(d => d.name == "bare"))
                UnityEngine.Object.DestroyImmediate(bare.gameObject);
        }

        [Test]
        public void TheDirectorKeepsASingleAudioListenerPolicyAndSources2D()
        {
            var real = new GameObject("real").AddComponent<AudioDirector>();
            created.Add(real.gameObject);
            real.Initialize(catalog, null, settings, null, true);
            var sources = real.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources.Length, Is.EqualTo(15), "3 de música, 4 de loop e 8 de efeito");
            Assert.That(sources.All(s => s.spatialBlend == 0f && !s.playOnAwake), Is.True, "som 2D: sem propagação no vácuo");
            Assert.That(real.GetComponentsInChildren<AudioListener>(true), Is.Empty, "o único AudioListener é o da câmera (CameraDirector)");
        }
    }
}
