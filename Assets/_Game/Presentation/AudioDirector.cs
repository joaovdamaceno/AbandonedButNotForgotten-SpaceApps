using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// AUD-002 · Diretor de áudio orientado por dados: um ÚNICO dono do volume de cada voz (sem corrotinas que disputem a mesma
    /// fonte). Música com crossfade de 0,8–2 s, ducking, silêncio real, efeitos com variação, loops com teto de duração, sons de
    /// instrumento ligados aos eventos reais do HoldAction, pausa por origem, volumes independentes (Master/Music/SFX/UI) vindos
    /// das preferências e liberação do áudio só após o clique inicial no Web (AudioContext).
    /// <para>
    /// Tempo: fades, loops e silêncio avançam em tempo ATIVO (congelam na pausa, como o resto do jogo); a interface (bus Ui) e a
    /// suavização dos volumes usam o tempo real, para responder mesmo pausada. Som no espaço ou na Lua é música e sonificação da
    /// interface: nada é "propagado no vácuo". Nenhuma informação essencial depende só do som.
    /// </para>
    /// Sem arquivos licenciados (AUD-001 ainda é só uma lista de candidatos), os cues são pendentes e o diretor funciona como
    /// silêncio: guarda o estado (cue atual, loops, silêncio) sem erro, e os testes usam clipes sintéticos.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const float MinFadeSeconds = 0.8f;
        public const float MaxFadeSeconds = 2f;
        /// <summary>Ducking da música: −6 dB, ataque 0,3 s e retorno 0,8 s (planilha §2).</summary>
        public const float DuckGain = 0.501187f;
        public const float DuckAttackSeconds = 0.3f;
        public const float DuckReleaseSeconds = 0.8f;
        public const float SilenceMuteSeconds = 0.15f;
        public const float SilenceReturnSeconds = 1f;
        public const float LoopFadeOutSeconds = 0.5f;
        public const float HoldLoopFadeSeconds = 0.1f;
        public const float HoldReleaseSeconds = 0.5f;
        public const float HoldPitchMin = 0.9f;
        public const float HoldPitchMax = 1.3f;
        public const float HoldPitchReleased = 0.7f;
        public const float GainSmoothingPerSecond = 12f;
        static readonly AudioBus[] Buses = { AudioBus.Master, AudioBus.Music, AudioBus.Sfx, AudioBus.Ui };
        const int MusicVoices = 3;
        const int LoopVoices = 4;
        const int OneShotVoices = 8;

        [SerializeField] AudioCatalog catalog;

        sealed class Slot
        {
            public IAudioVoice Voice;
            public AudioBus Bus;
            public AudioCueDefinition Cue;
            public string Key;
            public bool Active, Paused, Loop;
            public float Env, EnvTarget, EnvRate, Age, Duration, Pitch = 1f, VolumeMul = 1f, PitchTarget = 1f, PitchRate;
            public long Order;
        }

        readonly List<Slot> music = new List<Slot>();
        readonly List<Slot> loops = new List<Slot>();
        readonly List<Slot> shots = new List<Slot>();
        readonly Dictionary<AudioBus, float> targetGain = new Dictionary<AudioBus, float>
        {
            { AudioBus.Master, 1f }, { AudioBus.Music, 0.8f }, { AudioBus.Sfx, 0.9f }, { AudioBus.Ui, 0.9f },
        };

        readonly Dictionary<AudioBus, float> gain = new Dictionary<AudioBus, float>
        {
            { AudioBus.Master, 1f }, { AudioBus.Music, 0.8f }, { AudioBus.Sfx, 0.9f }, { AudioBus.Ui, 0.9f },
        };

        readonly HashSet<string> duckReasons = new HashSet<string>();
        readonly HashSet<string> pauseSources = new HashSet<string>();
        readonly HashSet<string> missingCues = new HashSet<string>();
        readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
        readonly Dictionary<string, HoldSoundSet> holdSets = new Dictionary<string, HoldSoundSet>();

        IActiveTimeSource timeSource;
        IInteractionService interaction;
        System.Random random = new System.Random(1234);
        bool initialized, unlocked;
        string currentMusicId, pendingMusicId;
        float pendingMusicFade, musicLevel = 1f, musicLevelTarget = 1f, musicLevelRate, duck = 1f, silenceRemaining, silenceMul = 1f;
        long order;

        public AudioCatalog Catalog => catalog;
        /// <summary>No Web fica falso até <see cref="Unlock"/> (clique em Iniciar); fora dele, verdadeiro desde <see cref="Initialize"/>.</summary>
        public bool Available => initialized && unlocked;
        public string CurrentMusicId => currentMusicId;
        public bool IsPaused => pauseSources.Count > 0;
        public bool IsSilent => silenceRemaining > 0f;
        public float SilenceRemaining => silenceRemaining;
        public float MusicLevel => musicLevel;
        public float DuckMultiplier => duck;
        public float SilenceMultiplier => silenceMul;
        public IReadOnlyCollection<string> MissingCueRequests => missingCues;
        public int ActiveMusicVoices => CountActive(music);
        public int ActiveLoopVoices => CountActive(loops);
        public int ActiveOneShotVoices => CountActive(shots);

        public static float ClampFade(float fadeSeconds) => fadeSeconds <= 0f ? 0f : Mathf.Clamp(fadeSeconds, MinFadeSeconds, MaxFadeSeconds);

        /// <summary>
        /// Liga o diretor. <paramref name="voiceFactory"/> cria as vozes (padrão: <c>AudioSource</c> filhos). <paramref name="startUnlocked"/>
        /// vale falso no Web, onde o áudio só começa após o clique inicial.
        /// </summary>
        public void Initialize(AudioCatalog audioCatalog, IActiveTimeSource activeTime, SettingsData settings, Func<string, IAudioVoice> voiceFactory = null, bool? startUnlocked = null)
        {
            ReleaseAll();
            catalog = audioCatalog != null ? audioCatalog : catalog;
            timeSource = activeTime;
            voiceFactory = voiceFactory ?? CreateUnityVoice;
            music.Clear();
            loops.Clear();
            shots.Clear();
            for (var i = 0; i < MusicVoices; i++)
                music.Add(new Slot { Voice = voiceFactory("Music" + i), Bus = AudioBus.Music });
            for (var i = 0; i < LoopVoices; i++)
                loops.Add(new Slot { Voice = voiceFactory("Loop" + i), Bus = AudioBus.Sfx });
            for (var i = 0; i < OneShotVoices; i++)
                shots.Add(new Slot { Voice = voiceFactory("Shot" + i), Bus = AudioBus.Sfx });

            initialized = true;
            unlocked = startUnlocked ?? Application.platform != RuntimePlatform.WebGLPlayer;
            ResetState();
            if (settings != null)
                ApplySettings(settings, immediate: true);
        }

        IAudioVoice CreateUnityVoice(string name)
        {
            var child = new GameObject("AudioVoice_" + name);
            child.transform.SetParent(transform, false);
            return new UnityAudioVoice(child.AddComponent<AudioSource>());
        }

        // ---------- Preferências e liberação ----------

        /// <summary>Volumes independentes: Master multiplica Music, SFX e UI. Mudanças são suavizadas (sem estalo).</summary>
        public void ApplySettings(SettingsData settings, bool immediate = false)
        {
            if (settings == null)
                return;

            SetBusVolume(AudioBus.Master, settings.masterVolume, immediate);
            SetBusVolume(AudioBus.Music, settings.musicVolume, immediate);
            SetBusVolume(AudioBus.Sfx, settings.sfxVolume, immediate);
            SetBusVolume(AudioBus.Ui, settings.uiVolume, immediate);
        }

        public void SetBusVolume(AudioBus bus, float volume, bool immediate = false)
        {
            var value = float.IsNaN(volume) ? 0f : Mathf.Clamp01(volume);
            targetGain[bus] = value;
            if (immediate)
                gain[bus] = value;
        }

        public float GetBusVolume(AudioBus bus) => targetGain[bus];

        /// <summary>Ganho efetivo do bus (Master × bus), já suavizado.</summary>
        public float EffectiveGain(AudioBus bus) => bus == AudioBus.Master ? gain[AudioBus.Master] : gain[AudioBus.Master] * gain[bus];

        /// <summary>Chamar no clique em Iniciar (AudioContext do navegador). Aplica o cue de música pedido antes do clique.</summary>
        public void Unlock()
        {
            if (unlocked)
                return;

            unlocked = true;
            if (pendingMusicId != null)
            {
                var id = pendingMusicId;
                var fade = pendingMusicFade;
                pendingMusicId = null;
                SetCue(id, fade);
            }
        }

        // ---------- Música ----------

        /// <summary>
        /// Troca a música por crossfade igual-potência de 0,8–2 s (fade 0 = corte seco, só onde o roteiro pede silêncio). Pedir o cue que
        /// já toca não o reinicia. ID vazio faz a música sair. Antes do <see cref="Unlock"/> o pedido fica guardado.
        /// </summary>
        public void SetCue(string cueId, float fadeSeconds)
        {
            if (!initialized)
                throw new InvalidOperationException("AudioDirector sem Initialize.");

            if (!unlocked)
            {
                pendingMusicId = cueId;
                pendingMusicFade = fadeSeconds;
                return;
            }

            if (string.IsNullOrEmpty(cueId))
            {
                FadeOutMusic(ClampFade(fadeSeconds));
                currentMusicId = null;
                return;
            }

            if (cueId == currentMusicId)
                return;

            var fade = ClampFade(fadeSeconds);
            FadeOutMusic(fade);
            currentMusicId = cueId;
            if (!TryGetCue(cueId, out var cue) || cue.IsPending)
                return; // cue pendente: o estado fica registrado, sem som

            var slot = TakeSlot(music);
            Begin(slot, cue, cueId, PickClip(cue, cueId), loop: true, pitch: 1f, env: fade > 0f ? 0f : 1f);
            Fade(slot, 1f, fade);
        }

        void FadeOutMusic(float fade)
        {
            foreach (var slot in music)
            {
                if (!slot.Active || slot.EnvTarget <= 0f)
                    continue;
                if (fade <= 0f)
                    Release(slot);
                else
                    Fade(slot, 0f, fade);
            }
        }

        /// <summary>Nível da música (0–1), ex.: quase silêncio na virada do cinturão. Fade em tempo ativo.</summary>
        public void SetMusicLevel(float level, float fadeSeconds)
        {
            musicLevelTarget = Mathf.Clamp01(level);
            musicLevelRate = fadeSeconds <= 0f ? float.PositiveInfinity : Mathf.Abs(musicLevelTarget - musicLevel) / fadeSeconds;
            if (fadeSeconds <= 0f)
                musicLevel = musicLevelTarget;
        }

        /// <summary>Ducking por motivo: vários motivos não se anulam (a música só volta quando o último sai).</summary>
        public void Duck(string reason, bool active)
        {
            if (string.IsNullOrEmpty(reason))
                return;
            if (active)
                duckReasons.Add(reason);
            else
                duckReasons.Remove(reason);
        }

        // ---------- Efeitos, loops e silêncio ----------

        /// <summary>Dispara um efeito (alterna as variações e varia a altura). Cue de loop vira <see cref="StartLoop"/>. Perdido antes do Unlock.</summary>
        public void PlayEffect(string cueId)
        {
            if (!initialized || !unlocked || string.IsNullOrEmpty(cueId))
                return;
            if (!TryGetCue(cueId, out var cue))
                return;
            if (cue.Loop)
            {
                StartLoop(cueId);
                return;
            }

            if (cue.IsPending || IsSilent && cue.Bus != AudioBus.Ui)
                return; // silêncio real: nada fora da interface toca

            PlayShot(cue, cueId);
        }

        void PlayShot(AudioCueDefinition cue, string key)
        {
            var clip = PickClip(cue, key);
            if (clip == null)
                return;

            var pitch = 1f + (float)(random.NextDouble() * 2.0 - 1.0) * cue.PitchVariation;
            var slot = TakeSlot(shots);
            slot.Bus = cue.Bus;
            Begin(slot, cue, key, clip, loop: false, pitch: pitch, env: 1f);
            slot.Duration = clip.length / Mathf.Max(0.1f, pitch);
        }

        /// <summary>Liga um loop (ambiente, rádio, varredura). Não duplica; para sozinho no teto de duração e respeita o silêncio.</summary>
        public void StartLoop(string cueId, float fadeInSeconds = 0.2f)
        {
            if (!initialized || !unlocked || string.IsNullOrEmpty(cueId) || FindLoop(cueId) != null)
                return;
            if (!TryGetCue(cueId, out var cue) || cue.IsPending)
                return;

            var slot = TakeSlot(loops);
            slot.Bus = cue.Bus;
            Begin(slot, cue, cueId, PickClip(cue, cueId), loop: true, pitch: 1f, env: fadeInSeconds > 0f ? 0f : 1f);
            Fade(slot, 1f, fadeInSeconds);
        }

        public void StopLoop(string cueId, float fadeSeconds = LoopFadeOutSeconds)
        {
            var slot = FindLoop(cueId);
            if (slot == null)
                return;
            if (fadeSeconds <= 0f)
                Release(slot);
            else
                Fade(slot, 0f, fadeSeconds);
        }

        public bool IsLoopPlaying(string cueId) => FindLoop(cueId) != null;

        Slot FindLoop(string key)
        {
            foreach (var slot in loops)
            {
                if (slot.Active && slot.Key == key && slot.EnvTarget > 0f)
                    return slot;
            }

            return null;
        }

        /// <summary>
        /// Silêncio REAL pela duração pedida (tempo ativo): música, ambiente e efeitos caem em 0,15 s, nada novo fora da interface
        /// toca e não sobra chiado em loop; depois tudo volta em 1 s. Chamadas seguidas não encurtam o silêncio em curso.
        /// </summary>
        public void Silence(float seconds) => silenceRemaining = Mathf.Max(silenceRemaining, Mathf.Max(0f, seconds));

        // ---------- Pausa, parada e reinício ----------

        /// <summary>Pausa por origem (shell, foco, menu): música, efeitos e ambiente congelam; a interface continua.</summary>
        public void SetPaused(string source, bool paused)
        {
            if (string.IsNullOrEmpty(source))
                return;

            var before = IsPaused;
            if (paused)
                pauseSources.Add(source);
            else
                pauseSources.Remove(source);
            if (IsPaused != before)
                ApplyPause();
        }

        public void SetPaused(bool paused) => SetPaused("shell", paused);

        void ApplyPause()
        {
            var paused = IsPaused;
            foreach (var slot in AllSlots())
            {
                if (!slot.Active || slot.Bus == AudioBus.Ui || slot.Paused == paused)
                    continue;

                slot.Paused = paused;
                if (paused)
                    slot.Voice.Pause();
                else
                    slot.Voice.Resume();
            }
        }

        /// <summary>Encerra música, loops e efeitos com um fade curto (troca de capítulo). Não apaga o estado de preferências.</summary>
        public void StopAll(float fadeSeconds)
        {
            currentMusicId = null;
            pendingMusicId = null;
            foreach (var slot in AllSlots())
            {
                if (!slot.Active)
                    continue;
                if (fadeSeconds <= 0f)
                    Release(slot);
                else
                    Fade(slot, 0f, fadeSeconds);
            }
        }

        /// <summary>Reinício do capítulo: para tudo na hora e devolve o estado sonoro ao inicial (sem ducking, silêncio ou pausa de áudio).</summary>
        public void ResetState()
        {
            ReleaseAll();
            currentMusicId = null;
            pendingMusicId = null;
            musicLevel = musicLevelTarget = 1f;
            duck = 1f;
            silenceRemaining = 0f;
            silenceMul = 1f;
            duckReasons.Clear();
            pauseSources.Clear();
            missingCues.Clear();
        }

        // ---------- Instrumentos (HoldAction) ----------

        public void RegisterHold(HoldSoundSet set)
        {
            if (set != null && !string.IsNullOrEmpty(set.CandidateId))
                holdSets[set.CandidateId] = set;
        }

        /// <summary>Liga os eventos reais do HoldAction. O som acompanha o estado do comando: nunca toca sem hold em curso.</summary>
        public void BindInteraction(IInteractionService service)
        {
            UnbindInteraction();
            interaction = service;
            if (interaction == null)
                return;

            interaction.HoldStarted += OnHoldStarted;
            interaction.HoldProgress += OnHoldProgress;
            interaction.HoldCompleted += OnHoldCompleted;
            interaction.HoldCanceled += OnHoldCanceled;
        }

        public void UnbindInteraction()
        {
            if (interaction != null)
            {
                interaction.HoldStarted -= OnHoldStarted;
                interaction.HoldProgress -= OnHoldProgress;
                interaction.HoldCompleted -= OnHoldCompleted;
                interaction.HoldCanceled -= OnHoldCanceled;
            }

            interaction = null;
        }

        static string HoldKey(string candidateId) => "hold:" + candidateId;

        void OnHoldStarted(string id)
        {
            if (!holdSets.TryGetValue(id, out var set))
                return;

            if (!string.IsNullOrEmpty(set.StartCue))
                PlayEffect(set.StartCue);
            StartHoldLoop(set);
        }

        void StartHoldLoop(HoldSoundSet set)
        {
            var key = HoldKey(set.CandidateId);
            if (!initialized || !unlocked || string.IsNullOrEmpty(set.LoopCue) || FindLoop(key) != null)
                return;
            if (!TryGetCue(set.LoopCue, out var cue) || cue.IsPending)
                return;

            var slot = TakeSlot(loops);
            slot.Bus = cue.Bus;
            Begin(slot, cue, key, PickClip(cue, key), loop: true, pitch: HoldPitchMin, env: 0f);
            slot.VolumeMul = 0.6f;
            Fade(slot, 1f, HoldLoopFadeSeconds);
        }

        void OnHoldProgress(string id, float progress)
        {
            if (!holdSets.ContainsKey(id))
                return;

            var slot = FindLoop(HoldKey(id));
            if (slot == null)
                return;

            var p = Mathf.Clamp01(progress);
            slot.Pitch = slot.PitchTarget = Mathf.Lerp(HoldPitchMin, HoldPitchMax, p); // a altura acompanha o progresso real
            slot.VolumeMul = Mathf.Lerp(0.6f, 1f, p);
        }

        void OnHoldCompleted(string id)
        {
            if (!holdSets.TryGetValue(id, out var set))
                return;

            StopLoop(HoldKey(id), HoldLoopFadeSeconds);
            if (!string.IsNullOrEmpty(set.DoneCue))
                PlayEffect(set.DoneCue);
        }

        void OnHoldCanceled(string id)
        {
            if (!holdSets.TryGetValue(id, out var set))
                return;

            // Desacelera ao soltar: a altura cai e o volume some em 0,5 s, depois o loop para.
            var slot = FindLoop(HoldKey(id));
            if (slot != null)
            {
                slot.PitchTarget = HoldPitchReleased;
                slot.PitchRate = Mathf.Abs(slot.Pitch - HoldPitchReleased) / HoldReleaseSeconds;
                Fade(slot, 0f, HoldReleaseSeconds);
            }

            if (!string.IsNullOrEmpty(set.ReleaseCue))
                PlayEffect(set.ReleaseCue);
        }

        // ---------- Tempo ----------

        void Update()
        {
            if (!initialized)
                return;

            var real = Time.unscaledDeltaTime;
            Tick(timeSource != null ? timeSource.ActiveDeltaSeconds : real, real);
        }

        void OnApplicationFocus(bool focus)
        {
            if (initialized)
                SetPaused("focus", !focus);
        }

        /// <summary>
        /// Avança um quadro: <paramref name="activeDt"/> (zero na pausa) move fades, loops e silêncio; <paramref name="realDt"/>
        /// suaviza os volumes e move a interface. Cada voz recebe o volume UMA vez por quadro.
        /// </summary>
        public void Tick(float activeDt, float realDt)
        {
            if (!initialized)
                return;

            for (var i = 0; i < Buses.Length; i++)
                gain[Buses[i]] = Mathf.MoveTowards(gain[Buses[i]], targetGain[Buses[i]], GainSmoothingPerSecond * realDt);

            if (silenceRemaining > 0f)
                silenceRemaining = Mathf.Max(0f, silenceRemaining - activeDt);
            var silenceRate = silenceRemaining > 0f ? 1f / SilenceMuteSeconds : 1f / SilenceReturnSeconds;
            silenceMul = Mathf.MoveTowards(silenceMul, silenceRemaining > 0f ? 0f : 1f, silenceRate * activeDt);

            var duckTarget = duckReasons.Count > 0 ? DuckGain : 1f;
            var duckRate = duckTarget < duck ? (1f - DuckGain) / DuckAttackSeconds : (1f - DuckGain) / DuckReleaseSeconds;
            duck = Mathf.MoveTowards(duck, duckTarget, duckRate * activeDt);
            musicLevel = Mathf.MoveTowards(musicLevel, musicLevelTarget, (float.IsInfinity(musicLevelRate) ? 1000f : musicLevelRate) * activeDt);

            TickList(music, activeDt, realDt);
            TickList(loops, activeDt, realDt);
            TickList(shots, activeDt, realDt);
        }

        void TickList(List<Slot> pool, float activeDt, float realDt)
        {
            for (var i = 0; i < pool.Count; i++)
                TickSlot(pool[i], pool[i].Bus == AudioBus.Ui ? realDt : activeDt);
        }

        void TickSlot(Slot slot, float dt)
        {
            if (!slot.Active)
                return;

            if (slot.Env != slot.EnvTarget)
                slot.Env = Mathf.MoveTowards(slot.Env, slot.EnvTarget, slot.EnvRate * dt);
            if (slot.Pitch != slot.PitchTarget)
                slot.Pitch = Mathf.MoveTowards(slot.Pitch, slot.PitchTarget, slot.PitchRate * dt);
            slot.Age += dt;

            if (slot.Loop && slot.Cue.Bus != AudioBus.Music && slot.EnvTarget > 0f && slot.Cue.LoopLimitSeconds > 0f && slot.Age >= slot.Cue.LoopLimitSeconds)
                Fade(slot, 0f, LoopFadeOutSeconds); // nenhum loop fica ligado para sempre
            if (!slot.Loop && slot.Age >= slot.Duration)
            {
                Release(slot);
                return;
            }

            if (slot.EnvTarget <= 0f && slot.Env <= 0f)
            {
                Release(slot);
                return;
            }

            var envelope = slot.Bus == AudioBus.Music ? Mathf.Sqrt(slot.Env) : slot.Env; // igual-potência no crossfade
            var volume = slot.Cue.Volume * envelope * slot.VolumeMul * EffectiveGain(slot.Bus);
            if (slot.Bus != AudioBus.Ui)
                volume *= silenceMul;
            if (slot.Bus == AudioBus.Music)
                volume *= duck * musicLevel;
            slot.Voice.SetVolume(Mathf.Clamp01(volume));
            slot.Voice.SetPitch(slot.Pitch);
        }

        // ---------- Utilidades ----------

        bool TryGetCue(string cueId, out AudioCueDefinition cue)
        {
            cue = null;
            if (catalog != null && catalog.TryGet(cueId, out cue))
                return true;

            if (missingCues.Add(cueId))
                Debug.LogWarning($"[AudioDirector] cue '{cueId}' não está no catálogo (nada toca; registrar o ID e o arquivo licenciado).");
            return false;
        }

        AudioClip PickClip(AudioCueDefinition cue, string key)
        {
            var clips = cue.Clips;
            var valid = 0;
            AudioClip only = null;
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i] == null)
                    continue;
                valid++;
                only = clips[i];
            }

            if (valid <= 1)
                return only;

            var has = lastVariant.TryGetValue(key, out var last);
            var index = random.Next(valid - 1);
            if (has && index >= last)
                index++; // nunca repete a mesma variação duas vezes seguidas
            index = Mathf.Min(index, valid - 1);
            lastVariant[key] = index;
            var seen = 0;
            for (var i = 0; i < clips.Count; i++)
            {
                if (clips[i] == null)
                    continue;
                if (seen++ == index)
                    return clips[i];
            }

            return only;
        }

        void Begin(Slot slot, AudioCueDefinition cue, string key, AudioClip clip, bool loop, float pitch, float env)
        {
            slot.Cue = cue;
            slot.Key = key;
            slot.Loop = loop;
            slot.Active = true;
            slot.Paused = IsPaused && cue.Bus != AudioBus.Ui;
            slot.Env = env;
            slot.EnvTarget = env;
            slot.Age = 0f;
            slot.Duration = clip != null ? clip.length : 0f;
            slot.Pitch = slot.PitchTarget = pitch;
            slot.PitchRate = 0f;
            slot.VolumeMul = 1f;
            slot.Order = ++order;
            slot.Voice.Play(clip, loop, 0f, pitch);
            if (slot.Paused)
                slot.Voice.Pause();
        }

        static void Fade(Slot slot, float target, float seconds)
        {
            slot.EnvTarget = target;
            slot.EnvRate = seconds <= 0f ? float.PositiveInfinity : Mathf.Abs(target - slot.Env) / seconds;
            if (seconds <= 0f)
                slot.Env = target;
        }

        static void Release(Slot slot)
        {
            if (!slot.Active)
                return;

            slot.Active = false;
            slot.Paused = false;
            slot.Key = null;
            slot.Cue = null;
            slot.Env = slot.EnvTarget = 0f;
            slot.Voice.Stop();
        }

        /// <summary>Slot livre do pool; sem livre, rouba o mais antigo (nunca deixa duas rotinas na mesma voz).</summary>
        Slot TakeSlot(List<Slot> pool)
        {
            Slot oldest = null;
            foreach (var slot in pool)
            {
                if (!slot.Active)
                    return slot;
                if (oldest == null || slot.Order < oldest.Order)
                    oldest = slot;
            }

            Release(oldest);
            return oldest;
        }

        IEnumerable<Slot> AllSlots()
        {
            foreach (var slot in music)
                yield return slot;
            foreach (var slot in loops)
                yield return slot;
            foreach (var slot in shots)
                yield return slot;
        }

        void ReleaseAll()
        {
            foreach (var slot in AllSlots())
                Release(slot);
        }

        static int CountActive(List<Slot> pool)
        {
            var count = 0;
            foreach (var slot in pool)
            {
                if (slot.Active)
                    count++;
            }

            return count;
        }

        /// <summary>Semente do sorteio de variações e de altura (testes).</summary>
        public void SetSeed(int seed) => random = new System.Random(seed);

        /// <summary>Erros de montagem (lista vazia = conforme).</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            if (catalog == null)
                errors.Add("AudioDirector: catalog não atribuído.");
            else
                errors.AddRange(catalog.Validate());
            return errors;
        }

        void OnDestroy()
        {
            UnbindInteraction();
            ReleaseAll();
        }
    }
}
