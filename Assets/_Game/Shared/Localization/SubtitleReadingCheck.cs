using System.Collections.Generic;

namespace Abandoned.SpaceApps
{
    /// <summary>Uma legenda de uma sequência medida em um idioma.</summary>
    public struct SubtitleReadingRow
    {
        public string SequenceId;
        public string CueId;
        public string TextKey;
        public string Language;
        public int Characters;
        /// <summary>Tempo que o diretor garante na tela: a janela do cue ou a leitura mínima do gatilho.</summary>
        public float SecondsOnScreen;
        public float CharactersPerSecond;
        public bool TooFast;
        public bool Missing;
    }

    /// <summary>
    /// UI-004 · Verificação de leitura das legendas de uma sequência: caracteres por segundo da janela (ou da leitura mínima do
    /// gatilho) em cada idioma. Não corrige nada: aponta os cues rápidos demais para quem edita o roteiro. O limite padrão (22 cps) é
    /// o do validador da PIO-001; a velocidade de leitura confortável usada pelo capítulo é uma suposição, não um dado medido.
    /// </summary>
    public static class SubtitleReadingCheck
    {
        public const float DefaultMaxCharactersPerSecond = 22f;

        public static List<SubtitleReadingRow> Analyze(SequenceDefinition sequence, LocalizationService localization, string language,
            float maxCharactersPerSecond = DefaultMaxCharactersPerSecond)
        {
            var rows = new List<SubtitleReadingRow>();
            if (sequence == null || localization == null)
                return rows;

            foreach (var cue in sequence.Cues)
            {
                if (cue == null)
                    continue;

                var seconds = cue.IsTriggered ? cue.MinimumReadSeconds : cue.EndSeconds - cue.StartSeconds;
                var found = localization.TryGet(cue.TextKey, language, out var text);
                var characters = found ? text.Length : 0;
                var cps = seconds > 0f ? characters / seconds : float.PositiveInfinity;
                rows.Add(new SubtitleReadingRow
                {
                    SequenceId = sequence.name,
                    CueId = cue.CueId,
                    TextKey = cue.TextKey,
                    Language = language,
                    Characters = characters,
                    SecondsOnScreen = seconds,
                    CharactersPerSecond = cps,
                    TooFast = found && cps > maxCharactersPerSecond,
                    Missing = !found,
                });
            }

            return rows;
        }
    }
}
