using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// PIO-013 · Liga legendas e áudio do capítulo aos estados. Escuta a <see cref="SequenceDirector"/> (cues e eventos no
    /// mesmo relógio da sequência), resolve as chaves em <see cref="PioneerData"/> e pede música e efeitos pela porta
    /// <see cref="IPioneerAudioPort"/> conforme o <see cref="PioneerCueMap"/>.
    /// <list type="bullet">
    /// <item>Legenda: <see cref="TextChanged"/> entrega o texto atual; <b>vazio = a caixa some</b> (o apresentador global,
    /// UI-004, esconde placa e fundo). Duas legendas ao mesmo tempo nunca aparecem juntas: vale a mais recente.</item>
    /// <item>Música: pedida só quando o cue muda (CUR segue de PIO.Belt a PIO.Imaging sem reiniciar); o nível volta a 1 ao
    /// trocar de estado. Se o áudio ainda não foi liberado, as legendas funcionam normalmente e <see cref="Resync"/>
    /// refaz os pedidos depois do clique inicial.</item>
    /// <item><see cref="Cancel"/> encerra tudo (legenda, música, efeitos) e ignora eventos até o próximo
    /// <see cref="EnterState"/>: nada vaza para o arquivo.</item>
    /// </list>
    /// </summary>
    public sealed class PioneerNarration : MonoBehaviour
    {
        [SerializeField] PioneerData data;
        [SerializeField] PioneerCueMap cueMap;

        SequenceDirector director;
        IPioneerAudioPort audio;
        string languageCode = PioneerData.English;
        string stateId;
        string requestedMusic;
        float requestedMusicLevel = 1f;
        bool active;
        bool paused;
        readonly List<SubtitleCue> open = new List<SubtitleCue>();

        public string StateId => stateId;
        public bool IsActive => active;
        public string LanguageCode => languageCode;
        public string CurrentMusicCue => requestedMusic;
        public SubtitleCue CurrentCue => open.Count > 0 ? open[open.Count - 1] : null;

        /// <summary>Texto da legenda atual no idioma; vazio quando não há legenda (a caixa some).</summary>
        public string CurrentText => CurrentCue != null && data != null ? data.GetText(CurrentCue.TextKey, languageCode) : string.Empty;

        /// <summary>Texto mudou (inclusive para vazio). Quem desenha a caixa só mostra fundo quando o texto não é vazio.</summary>
        public event Action<string> TextChanged;

        public PioneerData Data => data;
        public PioneerCueMap CueMap => cueMap;

        public void Configure(PioneerData pioneerData, PioneerCueMap map)
        {
            data = pioneerData;
            cueMap = map;
        }

        /// <summary>Serviços globais. <paramref name="audioPort"/> pode ser nulo (sem áudio): as legendas continuam.</summary>
        public void Initialize(SequenceDirector sequenceDirector, IPioneerAudioPort audioPort, string language)
        {
            Unbind();
            director = sequenceDirector;
            audio = audioPort;
            languageCode = PioneerData.English; // o jogo só tem inglês
            if (director != null)
            {
                director.CueEntered += OnCueEntered;
                director.CueExited += OnCueExited;
                director.EventRaised += OnEvent;
            }
        }

        /// <summary>Troca de idioma, também no meio de uma legenda: o texto atual é resolvido de novo, sem reiniciar nada.</summary>
        public void SetLanguage(string language)
        {
            var normalized = PioneerData.Normalize(language);
            if (string.IsNullOrEmpty(normalized) || normalized == languageCode)
                return;

            languageCode = normalized;
            TextChanged?.Invoke(CurrentText);
        }

        /// <summary>Entra no estado: pede a música do mapa (crossfade) só se o cue mudou e devolve o nível da música a 1.</summary>
        public bool EnterState(string state)
        {
            if (cueMap == null || !cueMap.TryGetState(state, out var audioState))
                return false;

            ClearText();
            stateId = state;
            active = true;
            if (requestedMusicLevel != 1f)
            {
                requestedMusicLevel = 1f;
                Request(a => a.SetMusicLevel(1f, audioState.fadeSeconds));
            }

            if (requestedMusic != audioState.musicCueId)
            {
                requestedMusic = audioState.musicCueId;
                var cue = requestedMusic;
                var fade = audioState.fadeSeconds;
                Request(a => a.SetMusic(cue, fade));
            }

            return true;
        }

        /// <summary>Refaz os pedidos do estado atual (depois do clique inicial que libera o áudio no Web).</summary>
        public void Resync()
        {
            if (!active || audio == null || !audio.Available || cueMap == null || !cueMap.TryGetState(stateId, out var audioState))
                return;

            audio.SetMusic(string.IsNullOrEmpty(requestedMusic) ? audioState.musicCueId : requestedMusic, audioState.fadeSeconds);
            if (requestedMusicLevel != 1f)
                audio.SetMusicLevel(requestedMusicLevel, audioState.fadeSeconds);
            audio.SetPaused(paused);
        }

        /// <summary>Gatilho do jogo (ex.: peça encaixada): abre as legendas por gatilho da sequência atual.</summary>
        public int RaiseTrigger(string triggerId) => active && director != null ? director.Trigger(triggerId) : 0;

        /// <summary>Evento de estado que não vem da sequência (ex.: PIO.Listening.Attempt4): aplica as ações do mapa.</summary>
        public void RaiseEvent(string eventId) => OnEvent(eventId);

        public void SetPaused(bool value)
        {
            if (paused == value)
                return;

            paused = value;
            Request(a => a.SetPaused(value));
        }

        /// <summary>Cancelar ou reiniciar o capítulo: legenda some, música e efeitos encerram, eventos antigos são ignorados.</summary>
        public void Cancel()
        {
            var wasActive = active;
            active = false;
            paused = false;
            requestedMusic = null;
            requestedMusicLevel = 1f;
            stateId = null;
            ClearText();
            if (wasActive && cueMap != null)
            {
                var fade = cueMap.CancelFadeSeconds;
                Request(a => a.StopAll(fade));
            }
        }

        void OnCueEntered(SubtitleCue cue)
        {
            if (!active)
                return;

            open.Remove(cue);
            open.Add(cue);
            TextChanged?.Invoke(CurrentText);
        }

        void OnCueExited(SubtitleCue cue)
        {
            if (!open.Remove(cue))
                return;

            TextChanged?.Invoke(CurrentText);
        }

        void OnEvent(string eventId)
        {
            if (!active || cueMap == null)
                return;

            foreach (var audioEvent in cueMap.EventsFor(eventId))
            {
                switch (audioEvent.action)
                {
                    case PioneerAudioAction.Effect:
                        var effect = audioEvent.cueId;
                        Request(a => a.PlayEffect(effect));
                        break;
                    case PioneerAudioAction.MusicLevel:
                        requestedMusicLevel = audioEvent.level;
                        var level = audioEvent.level;
                        var fade = audioEvent.seconds;
                        Request(a => a.SetMusicLevel(level, fade));
                        break;
                    case PioneerAudioAction.Silence:
                        var seconds = audioEvent.seconds;
                        Request(a => a.Silence(seconds));
                        break;
                    case PioneerAudioAction.Music:
                        var musicCue = audioEvent.cueId;
                        var musicFade = audioEvent.seconds;
                        requestedMusic = musicCue; // o estado seguinte com o mesmo cue não reinicia
                        Request(a => a.SetMusic(musicCue, musicFade));
                        break;
                }
            }
        }

        void Request(Action<IPioneerAudioPort> action)
        {
            if (audio != null && audio.Available)
                action(audio);
        }

        void ClearText()
        {
            if (open.Count == 0)
                return;

            open.Clear();
            TextChanged?.Invoke(string.Empty);
        }

        void Unbind()
        {
            if (director == null)
                return;

            director.CueEntered -= OnCueEntered;
            director.CueExited -= OnCueExited;
            director.EventRaised -= OnEvent;
            director = null;
        }

        void OnDestroy() => Unbind();
    }
}
