using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Voz real: um <c>AudioSource</c> 2D (sem espacialização: som de interface e música, não propagação no vácuo).</summary>
    public sealed class UnityAudioVoice : IAudioVoice
    {
        readonly AudioSource source;

        public UnityAudioVoice(AudioSource audioSource)
        {
            source = audioSource;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false;
        }

        public void Play(AudioClip clip, bool loop, float volume, float pitch)
        {
            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.pitch = pitch;
            source.Play();
        }

        public void SetVolume(float volume) => source.volume = volume;

        public void SetPitch(float pitch) => source.pitch = pitch;

        public void Pause() => source.Pause();

        public void Resume() => source.UnPause();

        public void Stop()
        {
            source.Stop();
            source.clip = null;
        }
    }
}
