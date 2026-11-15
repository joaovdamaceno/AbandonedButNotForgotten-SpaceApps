using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum SequenceExitReason
    {
        Completed = 0,
        Skipped = 1,
        Canceled = 2,
    }

    public readonly struct SequenceResult
    {
        public readonly string SequenceId;
        public readonly SequenceExitReason Reason;
        public readonly float TimeSeconds;

        public SequenceResult(string sequenceId, SequenceExitReason reason, float timeSeconds)
        {
            SequenceId = sequenceId;
            Reason = reason;
            TimeSeconds = timeSeconds;
        }
    }

    /// <summary>
    /// Executa uma <see cref="SequenceDefinition"/>: câmera, atores, legendas e eventos nomeados num ÚNICO relógio
    /// (<see cref="SequenceClock"/>). Eventos disparam quando o relógio CRUZA o instante agendado, então a ordem e os
    /// instantes são os mesmos a 30 ou 60 FPS. Pausa (sessão ou local) congela tudo junto.
    /// <para>
    /// Controle: durante a execução a sequência possui a câmera (<see cref="CameraDirector.AcquireControl"/>) e o
    /// contexto de entrada <see cref="InputContext.Cinematic"/>. Término, salto permitido e cancelamento passam pelo
    /// MESMO caminho de saída: o controle é devolvido e <see cref="Finished"/> dispara uma única vez por execução,
    /// mesmo que callbacks chamem Cancel/Skip/Play no meio.
    /// </para>
    /// </summary>
    public sealed class SequenceDirector : MonoBehaviour
    {
        const float TimeEpsilon = 1e-4f;

        [SerializeField] CameraDirector cameraDirector;
        [SerializeField, Tooltip("Opcional. Quando presente, a sequência segura o contexto Cinematic.")] InputRouter inputRouter;

        readonly SequenceClock clock = new SequenceClock();
        readonly Dictionary<string, Transform> actors = new Dictionary<string, Transform>();
        readonly List<TimelineEntry> timeline = new List<TimelineEntry>();
        readonly Dictionary<string, List<ActorMark>> marksByActor = new Dictionary<string, List<ActorMark>>();
        readonly List<SubtitleCue> activeTimedCues = new List<SubtitleCue>();
        readonly List<TriggeredCue> activeTriggeredCues = new List<TriggeredCue>();
        readonly List<float> shotStartTimes = new List<float>();

        IActiveTimeSource timeSource;
        SequenceDefinition current;
        bool playing;
        bool allowSkip;
        int playToken;
        int nextEntry;
        int shotIndex = -1;
        CameraPose? continuityStart;
        IDisposable inputLease;
        IDisposable cameraLease;

        public bool IsPlaying => playing;
        public bool IsPaused => clock.IsPaused;
        public float TimeSeconds => clock.TimeSeconds;
        public SequenceDefinition Current => playing ? current : null;
        public string CurrentShotId => playing && shotIndex >= 0 ? current.Shots[shotIndex].ShotId : null;
        public CameraDirector CameraDirector => cameraDirector;

        public event Action<string> Started;
        public event Action<string> ShotStarted;

        /// <summary>Evento nomeado de um plano (ex.: "airbags.deflate"), no instante agendado.</summary>
        public event Action<string> EventRaised;

        /// <summary>Legenda que passa a valer (UI-004 mostra o texto pela chave). Uma sai antes da próxima entrar no mesmo instante.</summary>
        public event Action<SubtitleCue> CueEntered;

        public event Action<SubtitleCue> CueExited;

        /// <summary>Uma única vez por execução, depois que câmera e entrada foram devolvidas.</summary>
        public event Action<SequenceResult> Finished;

        public void Initialize(CameraDirector camera, InputRouter router, IActiveTimeSource activeTime)
        {
            cameraDirector = camera;
            inputRouter = router;
            timeSource = activeTime;
        }

        public void RegisterActor(string actorId, Transform actor)
        {
            if (!ContractValidation.IsValidId(actorId) || actor == null)
                throw new ArgumentException($"Ator inválido: '{actorId}'.");

            actors[actorId] = actor;
        }

        public void UnregisterActor(string actorId) => actors.Remove(actorId);

        void Update()
        {
            if (timeSource != null)
                Tick(timeSource.ActiveDeltaSeconds);
        }

        void OnDisable() => Cancel();

        /// <summary>Dados da sequência + referências desta cena (câmera, rigs, alvos, atores). Vazio = pode tocar.</summary>
        public List<string> Validate(SequenceDefinition sequence)
        {
            if (sequence == null)
                return new List<string> { "SequenceDirector: sequência nula." };

            var errors = sequence.Validate();
            var owner = $"SequenceDirector '{sequence.SequenceId}'";
            if (cameraDirector == null || cameraDirector.TargetCamera == null)
                ContractValidation.Report(errors, owner, "CameraDirector ou câmera ausente: as tomadas precisam mover a câmera de fato.");

            foreach (var shot in sequence.Shots)
            {
                if (shot == null)
                    continue;

                if (cameraDirector != null && !cameraDirector.HasRig(shot.RigId))
                    ContractValidation.Report(errors, owner, $"rig '{shot.RigId}' do plano '{shot.ShotId}' não registrado.");
                if (cameraDirector != null && !string.IsNullOrEmpty(shot.LookAtTargetId) && !cameraDirector.HasLookTarget(shot.LookAtTargetId))
                    ContractValidation.Report(errors, owner, $"alvo '{shot.LookAtTargetId}' do plano '{shot.ShotId}' não registrado.");
            }

            var reported = new HashSet<string>();
            foreach (var mark in sequence.ActorMarks)
            {
                if (mark.actorId != null && !actors.ContainsKey(mark.actorId) && reported.Add(mark.actorId))
                    ContractValidation.Report(errors, owner, $"ator '{mark.actorId}' não registrado.");
            }

            return errors;
        }

        /// <summary>
        /// Toca a sequência desde 0 s. Dados ou referências inválidos lançam <see cref="ArgumentException"/> antes de
        /// qualquer efeito. Se outra sequência estiver tocando, ela é cancelada pelo caminho normal de saída.
        /// </summary>
        public void Play(SequenceDefinition sequence, bool skippable = false)
        {
            var errors = Validate(sequence);
            if (errors.Count > 0)
                throw new ArgumentException("Sequência inválida:" + ContractValidation.Describe(errors), nameof(sequence));

            if (playing)
                Exit(SequenceExitReason.Canceled);

            current = sequence;
            allowSkip = skippable;
            BuildTimeline(sequence);
            activeTimedCues.Clear();
            activeTriggeredCues.Clear();
            nextEntry = 0;
            shotIndex = -1;
            continuityStart = null;

            playing = true;
            var token = ++playToken;
            clock.Start(sequence.TotalDurationSeconds);

            cameraLease = cameraDirector.AcquireControl("Sequence:" + sequence.SequenceId);
            cameraDirector.EnforceSingleAudioListener();
            inputLease = inputRouter != null ? inputRouter.Acquire(InputContext.Cinematic, "Sequence:" + sequence.SequenceId) : null;

            Started?.Invoke(sequence.SequenceId);
            if (token != playToken)
                return;

            if (!Process(0f, includeCueEnter: true))
                return;

            ApplyAt(0f);
            CompleteIfDone();
        }

        /// <summary>Avança com tempo ativo. Chamado por Update com o IActiveTimeSource; testes chamam direto.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (!playing)
                return;

            var from = clock.TimeSeconds;
            clock.Advance(activeDeltaSeconds);
            var to = clock.TimeSeconds;
            if (!(to > from))
                return; // pausado ou congelado: câmera, atores e texto ficam exatamente como estão

            if (!Process(to, includeCueEnter: true))
                return;

            ApplyAt(to);
            CompleteIfDone();
        }

        /// <summary>
        /// Reaplica a câmera e os atores no instante atual sem avançar o relógio. Serve a quem mexe num rig enquanto a sequência está
        /// pausada num marco (o giro do jogo da foto da Pioneer): sem isto a câmera só segue o rig quando o tempo anda.
        /// </summary>
        public void RefreshCamera()
        {
            if (playing)
                ApplyAt(clock.TimeSeconds);
        }

        public void Pause() => clock.Pause();

        public void Resume() => clock.Resume();

        /// <summary>
        /// Salto permitido: fecha as legendas, dispara em ordem os eventos nomeados restantes (o estado do mundo fica
        /// igual ao de assistir até o fim, sem piscar legendas), aplica a pose final e sai como Skipped.
        /// </summary>
        public bool Skip()
        {
            if (!playing || !allowSkip)
                return false;

            var token = playToken;
            CloseAllCues();
            if (token != playToken)
                return true;

            activeTriggeredCues.Clear();
            clock.JumpForwardTo(clock.DurationSeconds);
            if (!Process(clock.DurationSeconds, includeCueEnter: false))
                return true;

            ApplyAt(clock.DurationSeconds);
            Exit(SequenceExitReason.Skipped);
            return true;
        }

        /// <summary>Interrompe sem disparar os eventos restantes (reinício, saída do capítulo). Idempotente.</summary>
        public void Cancel() => Exit(SequenceExitReason.Canceled);

        /// <summary>
        /// Abre as legendas com este gatilho (ex.: resultado de uma ação). Ficam pelo tempo mínimo de leitura no relógio
        /// da sequência; o término espera essa leitura, para nenhuma legenda ser cortada pelo fim do plano.
        /// </summary>
        public int Trigger(string triggerId)
        {
            if (!playing || string.IsNullOrEmpty(triggerId))
                return 0;

            var opened = 0;
            var token = playToken;
            foreach (var cue in current.Cues)
            {
                if (cue == null || !cue.IsTriggered || cue.TriggerId != triggerId || IsTriggeredActive(cue))
                    continue;

                activeTriggeredCues.Add(new TriggeredCue(cue, clock.TimeSeconds + cue.MinimumReadSeconds));
                opened++;
                CueEntered?.Invoke(cue);
                if (token != playToken)
                    break;
            }

            return opened;
        }

        /// <summary>
        /// Mostra o estado da sequência num instante, sem eventos nem posse de controle (Editor/testes: estados
        /// anteriores e seguintes). Planos com continuidade usam o keyframe 0, pois não há câmera "anterior".
        /// </summary>
        public void Preview(SequenceDefinition sequence, float timeSeconds)
        {
            if (playing)
                throw new InvalidOperationException("Preview não é permitido durante a execução.");

            var errors = Validate(sequence);
            if (errors.Count > 0)
                throw new ArgumentException("Sequência inválida:" + ContractValidation.Describe(errors), nameof(sequence));

            BuildTimeline(sequence);
            var time = Mathf.Clamp(timeSeconds, 0f, sequence.TotalDurationSeconds);
            var index = ShotIndexAt(time);
            var shot = sequence.Shots[index];
            cameraDirector.Apply(cameraDirector.Evaluate(shot, Mathf.Clamp(time - shotStartTimes[index], 0f, shot.DurationSeconds), null));
            ApplyActors(time);
        }

        // ---- Execução ----

        void BuildTimeline(SequenceDefinition sequence)
        {
            timeline.Clear();
            shotStartTimes.Clear();
            marksByActor.Clear();

            var start = 0f;
            var order = 0;
            for (var i = 0; i < sequence.Shots.Count; i++)
            {
                var shot = sequence.Shots[i];
                shotStartTimes.Add(start);
                timeline.Add(TimelineEntry.ForShot(start, i, null, order++));
                foreach (var shotEvent in shot.Events)
                    timeline.Add(TimelineEntry.ForShot(start + shotEvent.timeSeconds, i, shotEvent.eventId, order++));
                start += shot.DurationSeconds;
            }

            foreach (var cue in sequence.Cues)
            {
                if (cue == null || cue.IsTriggered)
                    continue;

                timeline.Add(TimelineEntry.ForCue(cue.StartSeconds, cue, entering: true, order++));
                timeline.Add(TimelineEntry.ForCue(cue.EndSeconds, cue, entering: false, order++));
            }

            timeline.Sort(TimelineEntry.Compare);

            foreach (var mark in sequence.ActorMarks)
            {
                if (!marksByActor.TryGetValue(mark.actorId, out var list))
                    marksByActor[mark.actorId] = list = new List<ActorMark>();
                list.Add(mark);
            }

            foreach (var list in marksByActor.Values)
                list.Sort((a, b) => a.timeSeconds.CompareTo(b.timeSeconds));
        }

        /// <summary>Processa, em ordem de tempo, tudo até <paramref name="upTo"/>. False se a execução terminou/trocou no meio.</summary>
        bool Process(float upTo, bool includeCueEnter)
        {
            var token = playToken;
            while (playing && token == playToken)
            {
                var nextTimeline = nextEntry < timeline.Count ? timeline[nextEntry].Time : float.PositiveInfinity;
                var expiryIndex = EarliestTriggeredExpiry(out var nextExpiry);
                var next = Math.Min(nextTimeline, nextExpiry);
                if (next > upTo + TimeEpsilon)
                    break;

                if (nextExpiry <= nextTimeline)
                {
                    var cue = activeTriggeredCues[expiryIndex].Cue;
                    activeTriggeredCues.RemoveAt(expiryIndex);
                    CueExited?.Invoke(cue);
                }
                else
                {
                    Handle(timeline[nextEntry++], includeCueEnter);
                }
            }

            return playing && token == playToken;
        }

        void Handle(TimelineEntry entry, bool includeCueEnter)
        {
            switch (entry.Kind)
            {
                case EntryKind.ShotStart:
                    BeginShot(entry.ShotIndex);
                    break;
                case EntryKind.ShotEvent:
                    EventRaised?.Invoke(entry.EventId);
                    break;
                case EntryKind.CueEnter:
                    if (includeCueEnter)
                    {
                        activeTimedCues.Add(entry.Cue);
                        CueEntered?.Invoke(entry.Cue);
                    }

                    break;
                case EntryKind.CueExit:
                    if (activeTimedCues.Remove(entry.Cue))
                        CueExited?.Invoke(entry.Cue);
                    break;
            }
        }

        void BeginShot(int index)
        {
            // Fecha o plano anterior exatamente na pose final antes de capturar a continuidade.
            if (shotIndex >= 0)
            {
                var previous = current.Shots[shotIndex];
                cameraDirector.Apply(cameraDirector.Evaluate(previous, previous.DurationSeconds, continuityStart));
            }

            shotIndex = index;
            var shot = current.Shots[index];
            continuityStart = shot.ContinueFromPreviousShot ? cameraDirector.CurrentPose : (CameraPose?)null;
            ShotStarted?.Invoke(shot.ShotId);
        }

        void ApplyAt(float time)
        {
            if (shotIndex < 0)
                return;

            var clamped = Mathf.Clamp(time, 0f, clock.DurationSeconds);
            var shot = current.Shots[shotIndex];
            var local = Mathf.Clamp(clamped - shotStartTimes[shotIndex], 0f, shot.DurationSeconds);
            cameraDirector.Apply(cameraDirector.Evaluate(shot, local, continuityStart));
            ApplyActors(clamped);
        }

        void ApplyActors(float time)
        {
            foreach (var pair in marksByActor)
            {
                if (!actors.TryGetValue(pair.Key, out var actor) || actor == null)
                    continue;

                SampleMarks(pair.Value, time, out var position, out var rotation);
                actor.localPosition = position;
                actor.localRotation = rotation;
            }
        }

        static void SampleMarks(List<ActorMark> marks, float time, out Vector3 position, out Quaternion rotation)
        {
            var first = marks[0];
            var last = marks[marks.Count - 1];
            if (time <= first.timeSeconds || marks.Count == 1)
            {
                position = first.positionMeters;
                rotation = Quaternion.Euler(first.eulerAnglesDegrees);
                return;
            }

            if (time >= last.timeSeconds)
            {
                position = last.positionMeters;
                rotation = Quaternion.Euler(last.eulerAnglesDegrees);
                return;
            }

            for (var i = 0; i < marks.Count - 1; i++)
            {
                var a = marks[i];
                var b = marks[i + 1];
                if (time > b.timeSeconds)
                    continue;

                var u = b.timeSeconds > a.timeSeconds ? (time - a.timeSeconds) / (b.timeSeconds - a.timeSeconds) : 1f;
                position = Vector3.Lerp(a.positionMeters, b.positionMeters, u);
                rotation = Quaternion.Slerp(Quaternion.Euler(a.eulerAnglesDegrees), Quaternion.Euler(b.eulerAnglesDegrees), u);
                return;
            }

            position = last.positionMeters;
            rotation = Quaternion.Euler(last.eulerAnglesDegrees);
        }

        int ShotIndexAt(float time)
        {
            for (var i = shotStartTimes.Count - 1; i >= 0; i--)
            {
                if (time >= shotStartTimes[i])
                    return i;
            }

            return 0;
        }

        void CompleteIfDone()
        {
            if (playing && clock.TimeSeconds >= clock.DurationSeconds - TimeEpsilon
                && nextEntry >= timeline.Count && activeTriggeredCues.Count == 0)
            {
                Exit(SequenceExitReason.Completed);
            }
        }

        /// <summary>O ÚNICO caminho de saída. Devolve câmera e entrada antes de avisar; idempotente.</summary>
        void Exit(SequenceExitReason reason)
        {
            if (!playing)
                return;

            playing = false;
            playToken++;
            var result = new SequenceResult(current.SequenceId, reason, clock.TimeSeconds);
            clock.Stop();

            var input = inputLease;
            var camera = cameraLease;
            inputLease = null;
            cameraLease = null;
            input?.Dispose();
            camera?.Dispose();

            CloseAllCues();
            Finished?.Invoke(result);
        }

        void CloseAllCues()
        {
            var closing = new List<SubtitleCue>(activeTimedCues);
            foreach (var triggered in activeTriggeredCues)
                closing.Add(triggered.Cue);

            activeTimedCues.Clear();
            activeTriggeredCues.Clear();
            foreach (var cue in closing)
                CueExited?.Invoke(cue);
        }

        int EarliestTriggeredExpiry(out float time)
        {
            time = float.PositiveInfinity;
            var index = -1;
            for (var i = 0; i < activeTriggeredCues.Count; i++)
            {
                if (activeTriggeredCues[i].EndTime < time)
                {
                    time = activeTriggeredCues[i].EndTime;
                    index = i;
                }
            }

            return index;
        }

        bool IsTriggeredActive(SubtitleCue cue)
        {
            foreach (var triggered in activeTriggeredCues)
            {
                if (triggered.Cue == cue)
                    return true;
            }

            return false;
        }

        enum EntryKind
        {
            CueExit = 0,
            ShotStart = 1,
            ShotEvent = 2,
            CueEnter = 3,
        }

        readonly struct TriggeredCue
        {
            public readonly SubtitleCue Cue;
            public readonly float EndTime;

            public TriggeredCue(SubtitleCue cue, float endTime)
            {
                Cue = cue;
                EndTime = endTime;
            }
        }

        readonly struct TimelineEntry
        {
            public readonly float Time;
            public readonly EntryKind Kind;
            public readonly int ShotIndex;
            public readonly string EventId;
            public readonly SubtitleCue Cue;
            readonly int order;

            TimelineEntry(float time, EntryKind kind, int shotIndex, string eventId, SubtitleCue cue, int order)
            {
                Time = time;
                Kind = kind;
                ShotIndex = shotIndex;
                EventId = eventId;
                Cue = cue;
                this.order = order;
            }

            public static TimelineEntry ForShot(float time, int shotIndex, string eventId, int order) =>
                new TimelineEntry(time, eventId == null ? EntryKind.ShotStart : EntryKind.ShotEvent, shotIndex, eventId, null, order);

            public static TimelineEntry ForCue(float time, SubtitleCue cue, bool entering, int order) =>
                new TimelineEntry(time, entering ? EntryKind.CueEnter : EntryKind.CueExit, -1, null, cue, order);

            /// <summary>
            /// Mesmo instante: legendas que terminam saem primeiro; depois entradas de plano por ordem de plano (o evento
            /// final de um plano vem antes do início do seguinte); por último legendas que começam.
            /// </summary>
            public static int Compare(TimelineEntry a, TimelineEntry b)
            {
                var byTime = a.Time.CompareTo(b.Time);
                if (byTime != 0)
                    return byTime;

                var byPhase = Phase(a).CompareTo(Phase(b));
                if (byPhase != 0)
                    return byPhase;

                if (a.Kind is EntryKind.ShotStart or EntryKind.ShotEvent)
                {
                    var byShot = a.ShotIndex.CompareTo(b.ShotIndex);
                    if (byShot != 0)
                        return byShot;

                    var byKind = a.Kind.CompareTo(b.Kind);
                    if (byKind != 0)
                        return byKind;
                }

                return a.order.CompareTo(b.order);
            }

            static int Phase(TimelineEntry entry) =>
                entry.Kind == EntryKind.CueExit ? 0 : entry.Kind == EntryKind.CueEnter ? 2 : 1;
        }
    }
}
