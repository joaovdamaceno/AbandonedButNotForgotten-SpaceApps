using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum PioneerAudioKind
    {
        Music = 0,
        Effect = 1,
        Ambience = 2,
    }

    public enum PioneerAudioAction
    {
        Effect = 0,
        MusicLevel = 1,
        Silence = 2,
    }

    /// <summary>Cue cadastrado. O ID segue a folha de cues (Docs/Art/AUDIO_CUE_SHEET.md); o clipe só entra com licença e crédito.</summary>
    [Serializable]
    public sealed class PioneerAudioCue
    {
        public string cueId;
        public PioneerAudioKind kind;
        [Tooltip("Família da folha de cues: VIA, CUR, LEG ou vazio para efeitos.")] public string family;
        [Tooltip("Ruído de rádio e sons de interface não são sons espaciais (no vácuo, bipes são interface).")] public bool interfaceSound = true;
        public bool loop;
        [Min(0f), Tooltip("Duração máxima; 0 = ilimitada (só música). Limita loops cansativos.")] public float maxSeconds;
        [Tooltip("ID da folha de cues já confirmado? Falso = proposto por esta tarefa; AUD-002/R7 confirma.")] public bool idConfirmedInCueSheet;
        [Tooltip("candidate = faixa candidata, escuta e licença pendentes; acquired = arquivo importado com crédito.")] public string licenseStatus = "pending";
        [Tooltip("Crédito em ASSET_CREDITS.md. Obrigatório se licenseStatus = acquired.")] public string creditId;
        [Tooltip("Vazio até existir arquivo licenciado: este mapa NÃO traz áudio.")] public AudioClip clip;
    }

    [Serializable]
    public sealed class PioneerStateAudio
    {
        public string stateId;
        public string musicCueId;
        [Tooltip("Crossfade ao entrar no estado, em segundos (Pioneer: 1,5 a 2).")] public float fadeSeconds = 1.75f;
        public string note;
    }

    [Serializable]
    public sealed class PioneerAudioEvent
    {
        [Tooltip("Evento da sequência ou do estado que dispara a ação.")] public string eventId;
        public PioneerAudioAction action;
        public string cueId;
        [Range(0f, 1f)] public float level = 1f;
        [Min(0f), Tooltip("Fade (MusicLevel) ou duração (Silence), em segundos.")] public float seconds;
        [Tooltip("Evento ainda não existe no código integrado: PIO-009/011 (ou o dono do estado) confirma o ID.")] public bool proposedEvent;
        public string note;
    }

    /// <summary>
    /// PIO-013 · Mapa de áudio do capítulo, derivado da folha de cues (AUD-001): qual música cobre cada estado, os
    /// crossfades de 1,5 a 2 s, os efeitos de interface e o silêncio final. É só dados: sem clipes, sem licenças
    /// declaradas além do que a folha diz (candidatas, não adquiridas), sem locução de IA.
    /// </summary>
    public sealed class PioneerCueMap : ScriptableObject
    {
        public const float MinFadeSeconds = 1.5f;
        public const float MaxFadeSeconds = 2.0f;
        public const float ListeningSilenceSeconds = 2.5f;

        [SerializeField, Min(0f), Tooltip("Fade ao cancelar ou reiniciar o capítulo: nada pode vazar para o arquivo.")] float cancelFadeSeconds = 0.5f;
        [SerializeField] List<PioneerStateAudio> states = new List<PioneerStateAudio>();
        [SerializeField] List<PioneerAudioEvent> events = new List<PioneerAudioEvent>();
        [SerializeField] List<PioneerAudioCue> cues = new List<PioneerAudioCue>();

        public float CancelFadeSeconds => cancelFadeSeconds;
        public IReadOnlyList<PioneerStateAudio> States => states;
        public IReadOnlyList<PioneerAudioEvent> Events => events;
        public IReadOnlyList<PioneerAudioCue> Cues => cues;

        public static PioneerCueMap Create(float cancelFadeSeconds, IEnumerable<PioneerStateAudio> states, IEnumerable<PioneerAudioEvent> events, IEnumerable<PioneerAudioCue> cues)
        {
            var map = CreateInstance<PioneerCueMap>();
            map.cancelFadeSeconds = cancelFadeSeconds;
            map.states = new List<PioneerStateAudio>(states);
            map.events = new List<PioneerAudioEvent>(events);
            map.cues = new List<PioneerAudioCue>(cues);
            return map;
        }

        public bool TryGetState(string stateId, out PioneerStateAudio state)
        {
            foreach (var candidate in states)
            {
                if (candidate != null && candidate.stateId == stateId)
                {
                    state = candidate;
                    return true;
                }
            }

            state = null;
            return false;
        }

        public bool TryGetCue(string cueId, out PioneerAudioCue cue)
        {
            foreach (var candidate in cues)
            {
                if (candidate != null && candidate.cueId == cueId)
                {
                    cue = candidate;
                    return true;
                }
            }

            cue = null;
            return false;
        }

        public IEnumerable<PioneerAudioEvent> EventsFor(string eventId)
        {
            foreach (var candidate in events)
            {
                if (candidate != null && candidate.eventId == eventId)
                    yield return candidate;
            }
        }

        /// <summary>Erros do mapa. Lista vazia = conforme.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            const string owner = "PioneerCueMap";
            var known = new HashSet<string>();
            foreach (var cue in cues)
            {
                if (cue == null || !known.Add(cue.cueId ?? string.Empty))
                {
                    ContractValidation.Report(errors, owner, $"cue nulo ou duplicado '{cue?.cueId}'.");
                    continue;
                }

                ContractValidation.RequireId(errors, owner, "cueId", cue.cueId);
                // Nenhuma faixa entra sem licença e crédito registrados.
                if (cue.clip != null && (cue.licenseStatus != "acquired" || string.IsNullOrEmpty(cue.creditId)))
                    ContractValidation.Report(errors, owner, $"cue '{cue.cueId}' tem clipe sem licença adquirida e crédito registrado.");
                if (cue.licenseStatus == "acquired" && string.IsNullOrEmpty(cue.creditId))
                    ContractValidation.Report(errors, owner, $"cue '{cue.cueId}' adquirido sem creditId.");
                // Loops cansativos: todo loop de efeito/ambiente tem duração máxima.
                if (cue.kind != PioneerAudioKind.Music && cue.loop && cue.maxSeconds <= 0f)
                    ContractValidation.Report(errors, owner, $"cue '{cue.cueId}' em loop sem duração máxima.");
                if (cue.kind != PioneerAudioKind.Music && !cue.interfaceSound)
                    ContractValidation.Report(errors, owner, $"cue '{cue.cueId}': no vácuo, efeitos são som de interface, não som espacial.");
            }

            if (states.Count != PioneerPresentation.StateIds.Length)
                ContractValidation.Report(errors, owner, $"{states.Count} estados com áudio; esperado {PioneerPresentation.StateIds.Length}.");
            foreach (var state in states)
            {
                if (state == null || !Array.Exists(PioneerPresentation.StateIds, id => id == state.stateId))
                {
                    ContractValidation.Report(errors, owner, $"estado desconhecido '{state?.stateId}'.");
                    continue;
                }

                if (!known.Contains(state.musicCueId ?? string.Empty))
                    ContractValidation.Report(errors, owner, $"estado '{state.stateId}': música '{state.musicCueId}' não cadastrada.");
                else if (TryGetCue(state.musicCueId, out var music) && music.kind != PioneerAudioKind.Music)
                    ContractValidation.Report(errors, owner, $"estado '{state.stateId}': '{state.musicCueId}' não é música.");
                if (state.fadeSeconds < MinFadeSeconds || state.fadeSeconds > MaxFadeSeconds)
                    ContractValidation.Report(errors, owner, $"estado '{state.stateId}': crossfade de {state.fadeSeconds} s fora de {MinFadeSeconds}–{MaxFadeSeconds} s.");
            }

            foreach (var id in PioneerPresentation.StateIds)
            {
                if (!TryGetState(id, out _))
                    ContractValidation.Report(errors, owner, $"estado '{id}' sem música.");
            }

            foreach (var audioEvent in events)
            {
                if (audioEvent == null)
                {
                    ContractValidation.Report(errors, owner, "evento nulo.");
                    continue;
                }

                ContractValidation.RequireId(errors, owner, "eventId", audioEvent.eventId);
                if (audioEvent.action == PioneerAudioAction.Effect && !known.Contains(audioEvent.cueId ?? string.Empty))
                    ContractValidation.Report(errors, owner, $"evento '{audioEvent.eventId}': efeito '{audioEvent.cueId}' não cadastrado.");
                if (audioEvent.action == PioneerAudioAction.Silence && audioEvent.seconds <= 0f)
                    ContractValidation.Report(errors, owner, $"evento '{audioEvent.eventId}': silêncio sem duração.");
            }

            if (!HasSilence("PIO.Listening.Attempt4", ListeningSilenceSeconds))
                ContractValidation.Report(errors, owner, $"falta o silêncio real de {ListeningSilenceSeconds} s na tentativa 4 da escuta.");
            if (cancelFadeSeconds < 0f || cancelFadeSeconds > 1f)
                ContractValidation.Report(errors, owner, "fade de cancelamento deve ficar entre 0 e 1 s.");
            return errors;
        }

        bool HasSilence(string eventId, float seconds)
        {
            foreach (var audioEvent in EventsFor(eventId))
            {
                if (audioEvent.action == PioneerAudioAction.Silence && Mathf.Abs(audioEvent.seconds - seconds) < 1e-4f)
                    return true;
            }

            return false;
        }
    }
}
