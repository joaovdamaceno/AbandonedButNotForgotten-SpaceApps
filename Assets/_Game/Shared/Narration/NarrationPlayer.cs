using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>One speech owner per chapter. Pause/resume never restarts a sentence or changes its pitch.</summary>
    public sealed class NarrationPlayer : MonoBehaviour
    {
        [SerializeField] NarrationCatalog catalog;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        IActiveTimeSource clock;
        IAudioVoice voice;
        AudioSource unitySource;
        AudioDirector music;
        SubtitlePresenter presenter;
        ChapterDirector owner;
        AudioClip loadedClip;
        string presentedKey;
        float remainingSeconds;
        bool initialized, ownerPaused, focusPaused, systemPaused, muted, pending, playing, ownsLoadedData, observedPlayback;
        float startWaitSeconds;
        public NarrationCatalog Catalog => catalog;
        public string CurrentKey => loadedClip != null ? presentedKey : null;
        public bool IsBusy => loadedClip != null;
        public bool IsPaused => ownerPaused || focusPaused || systemPaused;
        public bool Muted => muted;
        public float RemainingSeconds => remainingSeconds;
        public event Action<string> LineStarted;
        public event Action<string> LineFinished;

        public void Configure(NarrationCatalog recordings) => catalog = recordings;
        public void Initialize(IActiveTimeSource activeTime, AudioDirector audio = null,
            ChapterDirector chapter = null, IAudioVoice voiceOverride = null)
        {
            Stop(); Unbind(); clock = activeTime; music = audio; owner = chapter;
            ownerPaused = chapter != null && chapter.IsPaused;
            if (voiceOverride != null) voice = voiceOverride;
            else if (voice == null)
            {
                unitySource = gameObject.AddComponent<AudioSource>();
                unitySource.playOnAwake = false; unitySource.spatialBlend = 0f; unitySource.priority = 16;
                voice = new UnityAudioVoice(unitySource);
            }
            if (owner != null) { owner.PausedChanged += SetPaused; owner.ChapterUnloading += Unloading; }
            initialized = true;
        }
        public void Bind(SubtitlePresenter subtitles)
        {
            if (presenter != null) presenter.BodyKeyChanged -= Present;
            presenter = subtitles;
            if (presenter != null) { presenter.BodyKeyChanged += Present; Present(presenter.CurrentBodyKey); }
        }
        public void Present(string key)
        {
            if (string.IsNullOrEmpty(key) || catalog == null || !catalog.TryGet(key, out _)) { Stop(); return; }
            TryPlay(key);
        }
        public bool TryPlay(string key)
        {
            if (catalog == null || !catalog.TryGet(key, out var clip)) return false;
            if (!initialized) Initialize(null);
            if (key == presentedKey) return true;
            Stop(); presentedKey = key;
            if (muted) return true;
            loadedClip = clip; remainingSeconds = clip.length; pending = true; startWaitSeconds = 0f; observedPlayback = false;
            ownsLoadedData = Application.platform != RuntimePlatform.WebGLPlayer && clip.loadState == AudioDataLoadState.Unloaded;
            if (ownsLoadedData) clip.LoadAudioData();
            StartReadyClip(); return true;
        }
        void StartReadyClip()
        {
            if (!pending || IsPaused || muted || loadedClip == null) return;
            if (loadedClip.loadState == AudioDataLoadState.Failed) { Stop(); return; }
            // Web's compressed AAC backend loads on Play. Its resampled audio
            // playhead, rather than a presumed sample frequency, is authoritative.
            if (Application.platform != RuntimePlatform.WebGLPlayer && loadedClip.loadState != AudioDataLoadState.Loaded) return;
            pending = false; playing = true;
            voice.Play(loadedClip, false, Gain, 1f);
            music?.Duck("human-narration", true);
            LineStarted?.Invoke(presentedKey);
        }
        float Gain => Mathf.Clamp01(volume) * (music != null ? music.EffectiveGain(AudioBus.Master) : 1f);
        void Update() { if (clock != null) Tick(clock.ActiveDeltaSeconds); }
        public void Tick(float activeDeltaSeconds)
        {
            if (!initialized) return;
            voice?.SetVolume(Gain);
            if (IsPaused) return;
            StartReadyClip();
            if (!playing) return;
            if (unitySource != null)
            {
                if (unitySource.isPlaying)
                {
                    if (!observedPlayback && (Application.isEditor || Debug.isDebugBuild))
                        Debug.Log("NAR_AUDIO_PLAYING " + presentedKey + " seconds=" + loadedClip.length.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                    observedPlayback = true; remainingSeconds = Mathf.Max(0f, loadedClip.length - unitySource.time);
                    return;
                }
                if (!observedPlayback)
                {
                    startWaitSeconds += Mathf.Max(0f, activeDeltaSeconds);
                    if (startWaitSeconds < 5f) return;
                    Debug.LogWarning("[Narration] Playback could not start: " + presentedKey);
                }
                remainingSeconds = 0f;
            }
            else remainingSeconds -= Mathf.Max(0f, activeDeltaSeconds);
            if (remainingSeconds <= 0f)
            {
                var finished = presentedKey; ReleaseClip(); LineFinished?.Invoke(finished);
                // Keep presentedKey: a language redraw cannot replay the finished line.
            }
        }
        public void SetPaused(bool value)
        { bool before = IsPaused; ownerPaused = value; ApplyPause(before); }
        public void SetFocus(bool focused)
        { bool before = IsPaused; focusPaused = !focused; ApplyPause(before); }
        void OnApplicationFocus(bool focused) => SetFocus(focused);
        void OnApplicationPause(bool value)
        { bool before = IsPaused; systemPaused = value; ApplyPause(before); }
        void ApplyPause(bool before)
        {
            if (before == IsPaused) return;
            if (IsPaused) voice?.Pause(); else if (playing) voice?.Resume();
        }
        public void SetMuted(bool value)
        { if (muted == value) return; muted = value; if (value) Stop(); }
        public void Stop() { ReleaseClip(); presentedKey = null; }
        void ReleaseClip()
        {
            voice?.Stop(); music?.Duck("human-narration", false);
            if (loadedClip != null && ownsLoadedData) loadedClip.UnloadAudioData();
            ownsLoadedData = false;
            loadedClip = null; pending = playing = false; remainingSeconds = 0f;
        }
        void Unloading(string id) => Stop();
        void Unbind()
        {
            if (presenter != null) presenter.BodyKeyChanged -= Present;
            presenter = null;
            if (owner != null) { owner.PausedChanged -= SetPaused; owner.ChapterUnloading -= Unloading; }
            owner = null;
        }
        void OnDisable() { Stop(); Unbind(); }
        void OnDestroy() { Stop(); Unbind(); }
    }
}
