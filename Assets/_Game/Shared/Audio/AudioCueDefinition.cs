using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Um cue de áudio por dados (ID da planilha <c>Docs/Art/AUDIO_CUE_SHEET.md</c>). Sem clipe, o cue é PENDENTE: a licença do
    /// arquivo ainda não foi adquirida e o diretor o trata como silêncio, sem erro. Nenhum clipe entra aqui sem linha
    /// "Importado" em <c>ASSET_CREDITS.md</c>.
    /// </summary>
    [Serializable]
    public sealed class AudioCueDefinition
    {
        /// <summary>Teto de um loop que não é música, em segundos de tempo ativo (nenhum loop fica agressivo para sempre).</summary>
        public const float MaxLoopSecondsLimit = 120f;
        public const float DefaultLoopSeconds = 40f;
        /// <summary>Loops de cama de música têm pelo menos 30 s, sem clique na emenda (planilha §2).</summary>
        public const float MinMusicLoopClipSeconds = 30f;
        public const float MinLoopClipSeconds = 1f;

        [SerializeField, Tooltip("ID do cue (ex.: MUS_PIO_CUR, SFX_SCAN).")] string id;
        [SerializeField] AudioBus bus = AudioBus.Sfx;
        [SerializeField, Tooltip("Variações: o diretor alterna sem repetir a mesma duas vezes seguidas. Vazio = cue pendente (sem arquivo licenciado).")] List<AudioClip> clips = new List<AudioClip>();
        [SerializeField] bool loop;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [SerializeField, Range(0f, 0.3f), Tooltip("Variação aleatória de altura (± fração) a cada disparo.")] float pitchVariation;
        [SerializeField, Tooltip("Loops que não são música: duração máxima em segundos de tempo ativo (0 = padrão).")] float maxSeconds;

        public AudioCueDefinition()
        {
        }

        public AudioCueDefinition(string id, AudioBus bus, bool loop = false, float volume = 1f, float pitchVariation = 0f, float maxSeconds = 0f, params AudioClip[] clips)
        {
            this.id = id;
            this.bus = bus;
            this.loop = loop;
            this.volume = volume;
            this.pitchVariation = pitchVariation;
            this.maxSeconds = maxSeconds;
            this.clips = new List<AudioClip>(clips ?? Array.Empty<AudioClip>());
        }

        public string Id => id;
        public AudioBus Bus => bus;
        public IReadOnlyList<AudioClip> Clips => clips;
        public bool Loop => loop;
        public float Volume => volume;
        public float PitchVariation => pitchVariation;
        public bool IsPending => clips == null || clips.Count == 0 || clips.TrueForAll(c => c == null);

        /// <summary>Duração máxima do loop em tempo ativo; música não tem teto.</summary>
        public float LoopLimitSeconds => bus == AudioBus.Music ? 0f : maxSeconds > 0f ? maxSeconds : DefaultLoopSeconds;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"AudioCueDefinition '{id ?? "<sem id>"}'";
            ContractValidation.RequireId(errors, owner, nameof(id), id);
            if (bus == AudioBus.Master)
                ContractValidation.Report(errors, owner, "o bus de um cue é Music, Sfx ou Ui (Master só multiplica).");
            if (volume < 0f || volume > 1f)
                ContractValidation.Report(errors, owner, "volume fora de 0–1.");
            if (maxSeconds < 0f || maxSeconds > MaxLoopSecondsLimit)
                ContractValidation.Report(errors, owner, $"maxSeconds fora de 0–{MaxLoopSecondsLimit} s.");
            if (!loop && maxSeconds > 0f)
                ContractValidation.Report(errors, owner, "maxSeconds só vale para loop.");

            if (clips != null)
            {
                foreach (var clip in clips)
                {
                    if (clip == null)
                    {
                        ContractValidation.Report(errors, owner, "há uma variação sem arquivo (referência quebrada).");
                        continue;
                    }

                    if (loop && clip.length < MinLoopClipSeconds)
                        ContractValidation.Report(errors, owner, $"o loop '{clip.name}' dura {clip.length:0.##} s; abaixo de {MinLoopClipSeconds} s a emenda vira clique.");
                    if (loop && bus == AudioBus.Music && clip.length < MinMusicLoopClipSeconds)
                        ContractValidation.Report(errors, owner, $"a cama de música '{clip.name}' dura {clip.length:0.##} s; o mínimo é {MinMusicLoopClipSeconds} s.");
                }
            }

            return errors;
        }
    }
}
