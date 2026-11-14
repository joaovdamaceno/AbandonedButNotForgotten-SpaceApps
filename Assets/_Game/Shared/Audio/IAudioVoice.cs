using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Uma "voz" de áudio: o mínimo que o <see cref="AudioDirector"/> pede a um <c>AudioSource</c>. Existe para que o diretor
    /// seja testável sem placa de som (os testes usam uma voz falsa) e para que ele seja o ÚNICO a escrever volume e altura.
    /// </summary>
    public interface IAudioVoice
    {
        void Play(AudioClip clip, bool loop, float volume, float pitch);
        void SetVolume(float volume);
        void SetPitch(float pitch);
        void Pause();
        void Resume();
        void Stop();
    }
}
