namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Pedidos de áudio do capítulo. NÃO é uma implementação de áudio: é a porta que o <c>AudioDirector</c> (AUD-002, ainda
    /// não integrado; contrato em Docs/03_ARCHITECTURE.md: <c>SetCue(cueId, fadeSeconds)</c>) vai adaptar. Os pedidos são
    /// só IDs de cue e tempos; nenhum clipe, licença ou crédito passa por aqui.
    /// </summary>
    public interface IPioneerAudioPort
    {
        /// <summary>Falso enquanto o áudio não foi liberado (no Web, até o clique inicial). Pedidos podem ser repetidos depois.</summary>
        bool Available { get; }

        void SetMusic(string cueId, float fadeSeconds);

        /// <summary>Nível da música, 0–1 (ex.: quase silêncio na virada do cinturão).</summary>
        void SetMusicLevel(float level, float fadeSeconds);

        void PlayEffect(string cueId);

        /// <summary>Silêncio real, sem ruído de fundo, pela duração pedida.</summary>
        void Silence(float seconds);

        void SetPaused(bool paused);

        /// <summary>Encerra música, efeitos e ambiente com um fade curto.</summary>
        void StopAll(float fadeSeconds);
    }
}
