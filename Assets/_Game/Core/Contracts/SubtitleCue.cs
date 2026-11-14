using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum SubtitlePosition
    {
        Bottom = 0,
        Top = 1,
    }

    /// <summary>
    /// Legenda por chave de localização (PT/EN em dados próprios; nenhuma frase em C#). Janela por tempo
    /// OU por gatilho de evento, nunca os dois. Texto vazio na tradução oculta a placa (UI-004).
    /// </summary>
    [Serializable]
    public sealed class SubtitleCue
    {
        [SerializeField] string cueId;
        [SerializeField] string textKey;
        [SerializeField, Tooltip("Início em segundos no relógio do plano/estado. Ignorado se houver gatilho.")] float startSeconds;
        [SerializeField, Tooltip("Fim em segundos. Ignorado se houver gatilho.")] float endSeconds;
        [SerializeField, Tooltip("ID do evento que abre a legenda (ex.: resultado da ação). Vazio = usa a janela de tempo.")] string triggerId;
        [SerializeField, Tooltip("Tempo mínimo de leitura quando o gatilho abre a legenda.")] float minimumReadSeconds;
        [SerializeField] SubtitlePosition position = SubtitlePosition.Bottom;
        [SerializeField] int priority;

        public SubtitleCue()
        {
        }

        public SubtitleCue(string cueId, string textKey, float startSeconds, float endSeconds,
            SubtitlePosition position = SubtitlePosition.Bottom, int priority = 0)
        {
            this.cueId = cueId;
            this.textKey = textKey;
            this.startSeconds = startSeconds;
            this.endSeconds = endSeconds;
            this.position = position;
            this.priority = priority;
        }

        public static SubtitleCue Triggered(string cueId, string textKey, string triggerId, float minimumReadSeconds,
            SubtitlePosition position = SubtitlePosition.Bottom, int priority = 0) =>
            new SubtitleCue(cueId, textKey, 0f, 0f, position, priority)
            {
                triggerId = triggerId,
                minimumReadSeconds = minimumReadSeconds,
            };

        public string CueId => cueId;
        public string TextKey => textKey;
        public float StartSeconds => startSeconds;
        public float EndSeconds => endSeconds;
        public string TriggerId => triggerId;
        public float MinimumReadSeconds => minimumReadSeconds;
        public SubtitlePosition Position => position;
        public int Priority => priority;
        public bool IsTriggered => !string.IsNullOrEmpty(triggerId);

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"SubtitleCue '{cueId ?? "<sem id>"}'";
            ContractValidation.RequireId(errors, owner, nameof(cueId), cueId);
            ContractValidation.RequireId(errors, owner, nameof(textKey), textKey);

            if (IsTriggered)
            {
                ContractValidation.RequireId(errors, owner, nameof(triggerId), triggerId);
                ContractValidation.RequirePositive(errors, owner, nameof(minimumReadSeconds), minimumReadSeconds);
                if (startSeconds != 0f || endSeconds != 0f)
                    ContractValidation.Report(errors, owner, "janela de tempo e gatilho são mutuamente exclusivos.");
            }
            else
            {
                ContractValidation.RequireNonNegative(errors, owner, nameof(startSeconds), startSeconds);
                if (!(endSeconds > startSeconds))
                    ContractValidation.Report(errors, owner, $"fim ({endSeconds} s) deve ser maior que o início ({startSeconds} s).");
            }

            return errors;
        }
    }
}
