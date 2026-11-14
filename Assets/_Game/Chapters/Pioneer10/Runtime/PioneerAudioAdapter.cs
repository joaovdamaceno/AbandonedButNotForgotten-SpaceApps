namespace Abandoned.SpaceApps
{
    /// <summary>
    /// AUD-002 → PIO-013: liga o <see cref="AudioDirector"/> real à porta <see cref="IPioneerAudioPort"/> que a
    /// <see cref="PioneerNarration"/> usa. Só repassa pedidos de ID e tempo; clipes e licenças ficam no catálogo.
    /// </summary>
    public sealed class PioneerAudioAdapter : IPioneerAudioPort
    {
        readonly AudioDirector director;

        public PioneerAudioAdapter(AudioDirector audioDirector) => director = audioDirector;

        public bool Available => director != null && director.Available;

        public void SetMusic(string cueId, float fadeSeconds) => director.SetCue(cueId, fadeSeconds);

        public void SetMusicLevel(float level, float fadeSeconds) => director.SetMusicLevel(level, fadeSeconds);

        public void PlayEffect(string cueId) => director.PlayEffect(cueId);

        public void Silence(float seconds) => director.Silence(seconds);

        public void SetPaused(bool paused) => director.SetPaused("pioneer", paused);

        public void StopAll(float fadeSeconds) => director.StopAll(fadeSeconds);
    }
}
