using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>
    /// Música da abertura de Marte (cena <c>Mars</c>, antes do pouso). A cena de abertura não tem fonte de áudio própria; este componente é
    /// criado por <see cref="MarsFieldHandoff"/>, toca a faixa em loop com entrada suave, pausa junto com o capítulo e sai com fade quando a cena
    /// <c>Opportunity</c> assume (onde o <c>MissionAudio</c> continua com a música do pouso).
    /// </summary>
    public sealed class MarsOpeningMusic : MonoBehaviour
    {
        public const string ClipResource = "Audio/a-kind-of-hope";
        public const float FadeInSeconds = 4f;
        // Mesmo nível da música do pouso no campo (MissionAudio, cue "orbit", ganho 0,64) e dos outros capítulos: cerca de -27 LUFS com os
        // volumes padrão (música 0,8). A faixa a-kind-of-hope mede -21,2 LUFS.
        public const float BaseGain = 0.64f;

        AudioSource source;
        Func<bool> isPaused;
        Func<bool> isNarrating;
        float level, musicVolume, fadeOutSeconds;
        float narrationGain = 1f;
        bool fadingOut;

        /// <summary>Volume linear final para um nível de fade e o ajuste de música/geral do aplicativo.</summary>
        public static float Volume(float fade, float master, float music) => Mathf.Clamp01(fade) * Mathf.Clamp01(master) * Mathf.Clamp01(music) * BaseGain;

        public bool IsPlaying => source != null && source.isPlaying;
        public float Level => level;

        public void Begin(AudioClip clip, float master, float music, Func<bool> pausedQuery, Func<bool> narrationQuery = null)
        {
            if (clip == null || source != null)
                return;
            isPaused = pausedQuery;
            isNarrating = narrationQuery;
            musicVolume = Mathf.Clamp01(master) * Mathf.Clamp01(music);
            source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
        }

        public void FadeOutAndStop(float seconds)
        {
            fadingOut = true;
            fadeOutSeconds = Mathf.Max(0.1f, seconds);
        }

        void Update()
        {
            if (source == null)
                return;
            var paused = isPaused != null && isPaused();
            if (paused && source.isPlaying)
                source.Pause();
            else if (!paused && !source.isPlaying && !fadingOut)
                source.UnPause();
            if (paused)
                return;

            level = fadingOut
                ? Mathf.MoveTowards(level, 0f, Time.unscaledDeltaTime / fadeOutSeconds)
                : Mathf.MoveTowards(level, 1f, Time.unscaledDeltaTime / FadeInSeconds);
            narrationGain = Mathf.MoveTowards(narrationGain, isNarrating != null && isNarrating() ? .5f : 1f, Time.unscaledDeltaTime * 3f);
            source.volume = Volume(level, 1f, musicVolume) * narrationGain;
            if (fadingOut && level <= 0f)
            {
                source.Stop();
                if (Application.isPlaying)
                    Destroy(source);
                else
                    DestroyImmediate(source);
                source = null;
            }
        }
    }
}
