using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-001: fluxo exclusivo das 32 batidas do capítulo Spirit &amp; Opportunity (M01–M32 de
    /// Docs/Chapters/01_MARS_TWINS.md). Consome só contratos: avança por <see cref="IChapterDirector.CompleteStep(string)"/>
    /// com o ID da etapa atual; a ordem vem da tabela de transições de <c>MarsChapter.asset</c>, nunca de um enum.
    /// <para>
    /// Três relógios separados: o <b>ativo</b> (<see cref="IActiveTimeSource"/>, SessionClock) mede a etapa de gameplay e
    /// congela na pausa; a <b>etapa</b> só termina quando seus eventos chegam na ordem da tabela; a <b>cinemática</b>
    /// (SequenceDirector) tem relógio próprio e encerra a etapa pelo seu <c>Finished</c>. Os segundos de referência
    /// (soma 360 s) são editoriais: nenhuma etapa conclui porque o tempo passou.
    /// </para>
    /// <para>
    /// Regras: evento fora de ordem, de outra etapa, repetido ou desconhecido é ignorado; a última etapa só conclui uma
    /// vez; legendas de resultado (clock <c>trigger</c>) abrem apenas quando o evento da medição é aceito, e a etapa
    /// espera a leitura mínima delas antes de concluir; reinício (novo StepEntered) fecha legendas e zera o estado.
    /// Nenhuma frase de jogo em C#: textos PT/EN em <c>MarsSubtitles.json</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Bindings para integração (MAR-009 monta; quem dispara cada evento está entre colchetes).
    /// Cinemática: ao receber <see cref="StepStarted"/> toque a sequência <c>SequenceId</c> e ligue
    /// <c>SequenceDirector.Finished += flow.NotifySequenceFinished</c>; o evento de conclusão é o próprio ID da sequência.
    /// Gameplay: ao receber StepStarted habilite a atividade; ela chama <see cref="Raise"/> com os eventos abaixo, em ordem.
    /// <code>
    /// Etapa ref.s Tipo       Entrada (selo / objetivo)            Eventos de conclusão, em ordem               [dono]
    /// M01    8  Cinemática mars.seq.M01 · MAR.T01               mars.seq.M01                                 [MAR-013/018]
    /// M02   12  Cinemática mars.seq.M02                         mars.seq.M02                                 [MAR-013/018]
    /// M03    5  Cinemática mars.seq.M03                         mars.seq.M03                                 [MAR-013/018]
    /// M04   18  Cinemática mars.seq.M04 · MAR.T02               mars.seq.M04                                 [MAR-014/018]
    /// M05    4  Cinemática mars.seq.M05                         mars.seq.M05                                 [MAR-014/018]
    /// M06    5  Cinemática mars.seq.M06                         mars.seq.M06                                 [MAR-014/018]
    /// M07   13  Gameplay   tutorial (mover, olhar, E)           mars.tutorial.done                           [MAR-002]
    /// M08    8  Gameplay   MAR.O01                              mars.spheres.reached                         [MAR-003]
    /// M09   12  Gameplay   MAR.O01                              mars.spheres.soilRecorded →
    ///                                                           mars.spheres.rockRecorded →
    ///                                                           mars.spheres.hematiteIdentified (C12) →
    ///                                                           mars.spheres.done                            [MAR-003]
    /// M10   10  Gameplay   MAR.T03 · MAR.O02                    mars.outcrop.reached                         [MAR-002]
    /// M11    5  Gameplay   MAR.O02                              mars.arm.placed                              [MAR-004]
    /// M12   27  Gameplay   MAR.O02                              mars.rat.ground → mars.rat.retracted         [MAR-004]
    /// M13   18  Gameplay   MAR.O02                              mars.apxs.measured (C16) →
    ///                                                           mars.mossbauer.measured (C17) →
    ///                                                           mars.composition.done                        [MAR-005]
    /// M14   14  Cinemática mars.seq.M14                         mars.seq.M14                                 [MAR-005/018]
    /// M15    5  Cinemática mars.seq.M15 · MAR.T04               mars.seq.M15                                 [MAR-018]
    /// M16    7  Gameplay   MAR.T05 · MAR.O03                    mars.shield.reached                          [MAR-006]
    /// M17    8  Gameplay   MAR.O03                              mars.shield.photographed                     [MAR-006]
    /// M18    5  Gameplay   MAR.O04                              mars.meteorite.reached                       [MAR-006]
    /// M19   10  Gameplay   MAR.O04                              mars.meteorite.measured (C24) →
    ///                                                           mars.meteorite.photographed                  [MAR-006]
    /// M20    8  Cinemática mars.seq.M20                         mars.seq.M20                                 [MAR-015/018]
    /// M21   25  Gameplay   MAR.T06 · MAR.O05                    mars.storm.conserved (C28) →
    ///                                                           mars.storm.skyCleared (C29)                  [MAR-007]
    /// M22   45  Cinemática mars.seq.M22 · MAR.T07               mars.seq.M22                                 [MAR-017/018]
    /// M23    9  Cinemática mars.seq.M23 · MAR.T08               mars.seq.M23                                 [MAR-018]
    /// M24    7  Gameplay   MAR.O06                              mars.overlook.reached                        [MAR-008]
    /// M25    7  Gameplay   MAR.O06                              mars.sun.referenceCaptured                   [MAR-008]
    /// M26    7  Gameplay   MAR.O07                              mars.valley.captured                         [MAR-008]
    /// M27    6  Cinemática mars.seq.M27                         mars.seq.M27                                 [MAR-016/018]
    /// M28    7  Gameplay   MAR.O06                              mars.sun.dustCaptured (C43)                  [MAR-008]
    /// M29    6  Gameplay   MAR.O08                              mars.antenna.prepared                        [MAR-008]
    /// M30    5  Gameplay   escolha da memória                   mars.memory.confirmed                        [MAR-008]
    /// M31   30  Cinemática mars.seq.M31 · MAR.T09               mars.seq.M31                                 [MAR-019/018]
    /// M32    4  Cinemática mars.seq.M32 (terminal)              mars.seq.M32 → FinishChapter                 [MAR-019/018]
    /// </code>
    /// Legendas: <c>clock=sequence</c> (28) vão nas SequenceDefinitions (<see cref="BuildSequenceCues"/>);
    /// <c>clock=step</c> (14) e <c>clock=trigger</c> (7) são emitidas aqui por <see cref="CueEntered"/>/<see cref="CueExited"/>,
    /// com a mesma assinatura do SequenceDirector, para o SubtitlePresenter (UI-004) ouvir as duas fontes.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MarsChapterFlow : MonoBehaviour
    {
        public const string MarsChapterId = "mars";
        public const int ExpectedStepCount = 32;
        public const int ExpectedCueCount = 49;
        public const float ReferenceDurationSeconds = 360f;

        public const string ClockSequence = "sequence";
        public const string ClockStep = "step";
        public const string ClockTrigger = "trigger";

        public enum StepKind
        {
            Cinematic = 0,
            Gameplay = 1,
        }

        /// <summary>IDs de evento que as atividades enviam por <see cref="Raise"/>. Use as constantes, não literais.</summary>
        public static class Events
        {
            public const string TutorialDone = "mars.tutorial.done";
            public const string SpheresReached = "mars.spheres.reached";
            public const string SpheresSoilRecorded = "mars.spheres.soilRecorded";
            public const string SpheresRockRecorded = "mars.spheres.rockRecorded";
            public const string SpheresHematiteIdentified = "mars.spheres.hematiteIdentified";
            public const string SpheresDone = "mars.spheres.done";
            public const string OutcropReached = "mars.outcrop.reached";
            public const string ArmPlaced = "mars.arm.placed";
            public const string RatGround = "mars.rat.ground";
            public const string RatRetracted = "mars.rat.retracted";
            public const string ApxsMeasured = "mars.apxs.measured";
            public const string MossbauerMeasured = "mars.mossbauer.measured";
            public const string CompositionDone = "mars.composition.done";
            public const string ShieldReached = "mars.shield.reached";
            public const string ShieldPhotographed = "mars.shield.photographed";
            public const string MeteoriteReached = "mars.meteorite.reached";
            public const string MeteoriteMeasured = "mars.meteorite.measured";
            public const string MeteoritePhotographed = "mars.meteorite.photographed";
            public const string StormConserved = "mars.storm.conserved";
            public const string StormSkyCleared = "mars.storm.skyCleared";
            public const string OverlookReached = "mars.overlook.reached";
            public const string SunReferenceCaptured = "mars.sun.referenceCaptured";
            public const string ValleyCaptured = "mars.valley.captured";
            public const string SunDustCaptured = "mars.sun.dustCaptured";
            public const string AntennaPrepared = "mars.antenna.prepared";
            public const string MemoryConfirmed = "mars.memory.confirmed";
        }

        /// <summary>Uma linha da tabela: tipo, entrada e eventos que concluem a etapa, na ordem exigida.</summary>
        [Serializable]
        public sealed class StepBinding
        {
            [SerializeField, Tooltip("ID da etapa em MarsChapter.asset (M01–M32).")] string stepId;
            [SerializeField] StepKind kind;
            [SerializeField, Tooltip("Cinemática: sequência tocada na entrada; seu Finished conclui a etapa.")] string sequenceId;
            [SerializeField, Tooltip("Duração de referência do roteiro. Editorial: nunca conclui a etapa.")] float referenceSeconds;
            [SerializeField, Tooltip("Chave de data/local exibida na entrada (opcional).")] string stampKey;
            [SerializeField, Tooltip("Chave do objetivo persistente do HUD (opcional).")] string objectiveKey;
            [SerializeField, Tooltip("Eventos aceitos em ordem; o último conclui a etapa.")] List<string> completionEvents = new List<string>();

            public StepBinding()
            {
            }

            public StepBinding(string stepId, StepKind kind, string sequenceId, float referenceSeconds, string stampKey,
                string objectiveKey, IEnumerable<string> completionEvents)
            {
                this.stepId = stepId;
                this.kind = kind;
                this.sequenceId = sequenceId;
                this.referenceSeconds = referenceSeconds;
                this.stampKey = stampKey;
                this.objectiveKey = objectiveKey;
                this.completionEvents = new List<string>(completionEvents ?? new string[0]);
            }

            /// <summary>Etapa cinemática: concluída quando a sequência termina (o evento é o próprio ID da sequência).</summary>
            public static StepBinding Cinematic(string stepId, float referenceSeconds, string sequenceId, string stampKey = null) =>
                new StepBinding(stepId, StepKind.Cinematic, sequenceId, referenceSeconds, stampKey, null, new[] { sequenceId });

            public static StepBinding Gameplay(string stepId, float referenceSeconds, string stampKey, string objectiveKey,
                params string[] completionEvents) =>
                new StepBinding(stepId, StepKind.Gameplay, null, referenceSeconds, stampKey, objectiveKey, completionEvents);

            public string StepId => stepId;
            public StepKind Kind => kind;
            public bool IsCinematic => kind == StepKind.Cinematic;
            public string SequenceId => sequenceId;
            public float ReferenceSeconds => referenceSeconds;
            public string StampKey => stampKey;
            public string ObjectiveKey => objectiveKey;
            public IReadOnlyList<string> CompletionEvents => completionEvents;
        }

        /// <summary>Linha de <c>MarsSubtitles.json</c> (campos com os mesmos nomes do JSON, lidos por JsonUtility).</summary>
        [Serializable]
        public sealed class SubtitleEntry
        {
            public string cueId;
            public string textKey;
            public string stepId;
            public string clock;
            public float startSeconds;
            public float endSeconds;
            public string triggerId;
            public float minimumReadSeconds;
            public string position;
            public int priority;
            public string pt;
            public string en;

            public bool IsTriggered => clock == ClockTrigger;

            public SubtitlePosition ParsedPosition => position == "top" ? SubtitlePosition.Top : SubtitlePosition.Bottom;

            /// <summary>Converte para o contrato global (janela de tempo OU gatilho).</summary>
            public SubtitleCue ToCue() =>
                IsTriggered
                    ? SubtitleCue.Triggered(cueId, textKey, triggerId, minimumReadSeconds, ParsedPosition, priority)
                    : new SubtitleCue(cueId, textKey, startSeconds, endSeconds, ParsedPosition, priority);
        }

        [Serializable]
        public sealed class TextEntry
        {
            public string key;
            public string pt;
            public string en;
        }

        /// <summary>Conteúdo de <c>MarsSubtitles.json</c>: 49 cues e textos de HUD/títulos, PT e EN.</summary>
        [Serializable]
        public sealed class SubtitleTable
        {
            public int schemaVersion;
            public string chapterId;
            public List<SubtitleEntry> cues = new List<SubtitleEntry>();
            public List<TextEntry> texts = new List<TextEntry>();

            /// <summary>Texto de uma chave (cue ou texto de HUD) no idioma <c>pt</c>/<c>en</c> de SettingsData.</summary>
            public bool TryGetText(string key, string languageCode, out string text)
            {
                text = null;
                if (string.IsNullOrEmpty(key))
                    return false;

                foreach (var cue in cues ?? new List<SubtitleEntry>())
                {
                    if (cue != null && cue.textKey == key)
                        text = languageCode == "en" ? cue.en : cue.pt;
                }

                foreach (var entry in texts ?? new List<TextEntry>())
                {
                    if (entry != null && entry.key == key)
                        text = languageCode == "en" ? entry.en : entry.pt;
                }

                return !string.IsNullOrEmpty(text);
            }
        }

        sealed class ActiveCue
        {
            public readonly SubtitleCue Cue;
            public readonly float EndSeconds;

            public ActiveCue(SubtitleCue cue, float endSeconds)
            {
                Cue = cue;
                EndSeconds = endSeconds;
            }
        }

        [SerializeField, Tooltip("MarsChapter.asset")] ChapterDefinition chapter;
        [SerializeField, Tooltip("MarsSubtitles.json")] TextAsset subtitlesJson;
        [SerializeField, Tooltip("Vazio = tabela padrão (CreateDefaultBindings). Menu de contexto restaura a padrão.")]
        List<StepBinding> steps = new List<StepBinding>();
        [SerializeField, Tooltip("Editor/Development: registra no Console eventos ignorados.")] bool logIgnoredEvents = true;

        readonly List<StepBinding> effectiveSteps = new List<StepBinding>();
        readonly Dictionary<string, StepBinding> bindingById = new Dictionary<string, StepBinding>();
        readonly Dictionary<string, List<SubtitleCue>> stepClockCues = new Dictionary<string, List<SubtitleCue>>();
        readonly Dictionary<string, List<SubtitleCue>> triggeredCues = new Dictionary<string, List<SubtitleCue>>();
        readonly List<ActiveCue> activeCues = new List<ActiveCue>();
        readonly HashSet<string> openedCueIds = new HashSet<string>();

        IChapterDirector director;
        IActiveTimeSource activeTime;
        SubtitleTable subtitles;
        StepBinding current;
        float stepSeconds;
        int nextEventIndex;
        bool completionPending;
        bool completionRequested;
        bool warnedRefusal;
        int entryToken;

        /// <summary>Entrou numa etapa de Marte: toque a sequência (cinemática) ou habilite a atividade (gameplay).</summary>
        public event Action<StepBinding> StepStarted;

        /// <summary>A etapa terminou (concluída, reinício, fim do capítulo ou Shutdown): desligue a atividade. Argumento: stepId.</summary>
        public event Action<string> StepEnded;

        /// <summary>Evento aceito na etapa atual. Argumentos: stepId, eventId.</summary>
        public event Action<string, string> EventAccepted;

        /// <summary>Evento recusado (fora de ordem, de outra etapa, repetido ou sem etapa ativa). Argumento: eventId.</summary>
        public event Action<string> EventIgnored;

        /// <summary>Legenda de etapa (clock step/trigger) abriu. Mesma assinatura de SequenceDirector.CueEntered.</summary>
        public event Action<SubtitleCue> CueEntered;

        public event Action<SubtitleCue> CueExited;

        public bool IsBound => director != null;
        public bool LogIgnoredEvents => logIgnoredEvents;
        public StepBinding CurrentBinding => current;
        public string CurrentStepId => current != null ? current.StepId : null;

        /// <summary>Tempo ativo desde a entrada na etapa atual (pausa não conta).</summary>
        public float StepActiveSeconds => stepSeconds;

        /// <summary>Próximo evento aceito na etapa atual; nulo se não há etapa ou se ela já está concluindo.</summary>
        public string NextExpectedEvent =>
            current != null && !completionPending && nextEventIndex < current.CompletionEvents.Count
                ? current.CompletionEvents[nextEventIndex]
                : null;

        /// <summary>Todos os eventos chegaram; a etapa espera a leitura mínima das legendas abertas para concluir.</summary>
        public bool IsAwaitingCueReading => current != null && completionPending && activeCues.Count > 0;

        public SubtitleTable Subtitles => subtitles;
        public IReadOnlyList<StepBinding> Steps => effectiveSteps.Count > 0 ? (IReadOnlyList<StepBinding>)effectiveSteps : CreateDefaultBindings();

        // ---- Ligação ----

        /// <summary>Liga com os assets serializados (MarsChapter.asset, MarsSubtitles.json e a tabela do Inspector ou a padrão).</summary>
        public void Initialize(IChapterDirector chapterDirector, IActiveTimeSource activeTimeSource)
        {
            Initialize(chapterDirector, activeTimeSource, chapter, subtitlesJson != null ? subtitlesJson.text : null,
                steps != null && steps.Count > 0 ? steps : null);
        }

        /// <summary>
        /// Liga o fluxo ao diretor. Dados inválidos lançam <see cref="ArgumentException"/> com a lista de divergências antes
        /// de qualquer efeito. Se o capítulo já está numa etapa (a cena carrega depois do Begin), alcança essa etapa.
        /// <paramref name="activeTimeSource"/> pode ser nulo em testes que chamam <see cref="Tick"/> diretamente.
        /// </summary>
        public void Initialize(IChapterDirector chapterDirector, IActiveTimeSource activeTimeSource, ChapterDefinition chapterDefinition,
            string subtitlesJsonText, IEnumerable<StepBinding> bindings = null)
        {
            if (chapterDirector == null)
                throw new ArgumentNullException(nameof(chapterDirector));

            Shutdown();

            var errors = new List<string>();
            var table = ParseSubtitles(subtitlesJsonText, errors);
            var list = bindings != null ? new List<StepBinding>(bindings) : CreateDefaultBindings();
            if (table != null)
                errors.AddRange(Validate(chapterDefinition, table, list));
            if (errors.Count > 0)
                throw new ArgumentException("MarsChapterFlow inválido:" + ContractValidation.Describe(errors));

            subtitles = table;
            effectiveSteps.Clear();
            effectiveSteps.AddRange(list);
            BuildLookups();

            director = chapterDirector;
            activeTime = activeTimeSource;
            director.StepEntered += OnStepEntered;
            director.ChapterFinished += OnChapterFinished;

            if (IsMarsRunning() && !string.IsNullOrEmpty(director.CurrentStepId))
                OnStepEntered(director.CurrentStepId);
        }

        /// <summary>Encerra a etapa atual (fecha legendas, StepEnded) e solta o diretor. Idempotente.</summary>
        public void Shutdown()
        {
            if (director != null)
            {
                director.StepEntered -= OnStepEntered;
                director.ChapterFinished -= OnChapterFinished;
            }

            EndCurrentStep();
            director = null;
            activeTime = null;
        }

        void OnDisable() => Shutdown();

        void Update()
        {
            if (activeTime != null)
                Tick(activeTime.ActiveDeltaSeconds);
        }

        // ---- Eventos das atividades e das sequências ----

        /// <summary>
        /// Uma atividade informa um evento. Aceito somente se é o próximo esperado da etapa atual; qualquer outro
        /// (inválido, de outra etapa, fora de ordem ou repetido) é ignorado e devolve false. O último evento conclui a
        /// etapa assim que as legendas de resultado abertas cumprirem a leitura mínima.
        /// </summary>
        public bool Raise(string eventId)
        {
            // O diretor é a verdade sobre a etapa: durante reinício/descarga ele já não está nela e nada é aceito.
            if (current == null || completionPending || string.IsNullOrEmpty(eventId) || director == null
                || director.CurrentStepId != current.StepId
                || nextEventIndex >= current.CompletionEvents.Count || current.CompletionEvents[nextEventIndex] != eventId)
            {
                Ignore(eventId);
                return false;
            }

            var binding = current;
            var token = entryToken;
            nextEventIndex++;
            EventAccepted?.Invoke(binding.StepId, eventId);
            if (token != entryToken)
                return true;

            OpenTriggeredCues(binding, eventId, token);
            if (token != entryToken)
                return true;

            if (nextEventIndex >= binding.CompletionEvents.Count)
            {
                completionPending = true;
                TryComplete();
            }

            return true;
        }

        /// <summary>
        /// Ligue em <c>SequenceDirector.Finished</c>. Término ou salto permitido conclui a etapa cinemática da sequência;
        /// cancelamento (reinício, saída) nunca conclui.
        /// </summary>
        public bool NotifySequenceFinished(SequenceResult result)
        {
            if (result.Reason == SequenceExitReason.Canceled)
                return false;

            return Raise(result.SequenceId);
        }

        /// <summary>Avança o relógio da etapa com tempo ATIVO (zero na pausa). Update usa o IActiveTimeSource; testes chamam direto.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (current == null || !(activeDeltaSeconds > 0f) || float.IsInfinity(activeDeltaSeconds))
                return;

            stepSeconds += activeDeltaSeconds;
            var token = entryToken;
            ProcessStepClock(token);
            if (token == entryToken)
                TryComplete();
        }

        // ---- Diretor ----

        void OnStepEntered(string stepId)
        {
            if (!IsMarsRunning())
                return;

            // Toda entrada é nova: reinício reentra em M01 e precisa zerar o que a execução anterior abriu.
            EndCurrentStep();

            StepBinding binding;
            if (!bindingById.TryGetValue(stepId, out binding))
            {
                Debug.LogError($"[MarsChapterFlow] etapa '{stepId}' sem binding; o capítulo ficaria parado.", this);
                return;
            }

            current = binding;
            stepSeconds = 0f;
            nextEventIndex = 0;
            completionPending = false;
            completionRequested = false;
            warnedRefusal = false;
            openedCueIds.Clear();
            var token = ++entryToken;

            StepStarted?.Invoke(binding);
            if (token == entryToken)
                ProcessStepClock(token);
        }

        void OnChapterFinished(string chapterId)
        {
            if (chapterId == MarsChapterId)
                EndCurrentStep();
        }

        bool IsMarsRunning() =>
            director != null && director.Current != null && director.Current.ChapterId == MarsChapterId && !director.IsFinished;

        void EndCurrentStep()
        {
            if (current == null)
                return;

            var ended = current;
            current = null;
            ++entryToken;
            completionPending = false;
            completionRequested = false;

            var closing = new List<ActiveCue>(activeCues);
            activeCues.Clear();
            foreach (var active in closing)
                CueExited?.Invoke(active.Cue);

            StepEnded?.Invoke(ended.StepId);
        }

        void TryComplete()
        {
            if (current == null || !completionPending || completionRequested || activeCues.Count > 0 || director == null)
                return;

            var stepId = current.StepId;
            var token = entryToken;
            completionRequested = true;
            if (director.CompleteStep(stepId) || token != entryToken)
                return;

            // O diretor recusou (ex.: estado global diferente de Playing). Tenta de novo no próximo quadro ativo.
            completionRequested = false;
            if (!warnedRefusal)
            {
                warnedRefusal = true;
                Debug.LogWarning($"[MarsChapterFlow] ChapterDirector recusou concluir '{stepId}' (etapa do diretor: '{director.CurrentStepId}').", this);
            }

            if (director.CurrentStepId == stepId)
                return;

            if (IsMarsRunning() && !string.IsNullOrEmpty(director.CurrentStepId))
                OnStepEntered(director.CurrentStepId);
            else
                EndCurrentStep();
        }

        // ---- Legendas da etapa ----

        void ProcessStepClock(int token)
        {
            var epsilon = ContractValidation.TimeToleranceSeconds;

            // No mesmo instante: legenda que termina sai antes da que começa (igual ao SequenceDirector).
            for (var i = 0; i < activeCues.Count;)
            {
                if (stepSeconds + epsilon < activeCues[i].EndSeconds)
                {
                    i++;
                    continue;
                }

                var cue = activeCues[i].Cue;
                activeCues.RemoveAt(i);
                CueExited?.Invoke(cue);
                if (token != entryToken)
                    return;
            }

            // Depois do último evento nenhuma fala nova começa: a etapa só espera a leitura das que já abriram.
            List<SubtitleCue> timed;
            if (completionPending || current == null || !stepClockCues.TryGetValue(current.StepId, out timed))
                return;

            foreach (var cue in timed)
            {
                if (openedCueIds.Contains(cue.CueId) || stepSeconds + epsilon < cue.StartSeconds)
                    continue;

                openedCueIds.Add(cue.CueId);
                if (stepSeconds + epsilon >= cue.EndSeconds)
                    continue;

                activeCues.Add(new ActiveCue(cue, cue.EndSeconds));
                CueEntered?.Invoke(cue);
                if (token != entryToken)
                    return;
            }
        }

        void OpenTriggeredCues(StepBinding binding, string eventId, int token)
        {
            List<SubtitleCue> list;
            if (!triggeredCues.TryGetValue(binding.StepId, out list))
                return;

            foreach (var cue in list)
            {
                if (cue.TriggerId != eventId || openedCueIds.Contains(cue.CueId))
                    continue;

                openedCueIds.Add(cue.CueId);
                activeCues.Add(new ActiveCue(cue, stepSeconds + cue.MinimumReadSeconds));
                CueEntered?.Invoke(cue);
                if (token != entryToken)
                    return;
            }
        }

        void Ignore(string eventId)
        {
            EventIgnored?.Invoke(eventId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logIgnoredEvents)
            {
                Debug.Log($"[MarsChapterFlow] evento '{eventId}' ignorado na etapa '{CurrentStepId ?? "<nenhuma>"}' " +
                    $"(esperado: '{NextExpectedEvent ?? "<nenhum>"}').", this);
            }
#endif
        }

        void BuildLookups()
        {
            bindingById.Clear();
            stepClockCues.Clear();
            triggeredCues.Clear();
            foreach (var binding in effectiveSteps)
                bindingById[binding.StepId] = binding;

            foreach (var entry in subtitles.cues)
            {
                Dictionary<string, List<SubtitleCue>> target;
                if (entry.clock == ClockStep)
                    target = stepClockCues;
                else if (entry.clock == ClockTrigger)
                    target = triggeredCues;
                else
                    continue; // clock=sequence: pertence à SequenceDefinition da etapa cinemática.

                List<SubtitleCue> list;
                if (!target.TryGetValue(entry.stepId, out list))
                    target[entry.stepId] = list = new List<SubtitleCue>();
                list.Add(entry.ToCue());
            }

            foreach (var list in stepClockCues.Values)
                list.Sort((a, b) => a.StartSeconds.CompareTo(b.StartSeconds));
        }

        // ---- Dados ----

        /// <summary>Tabela aprovada das 32 etapas (ver remarks da classe).</summary>
        public static List<StepBinding> CreateDefaultBindings() => new List<StepBinding>
        {
            StepBinding.Cinematic("M01", 8f, "mars.seq.M01", "MAR.T01"),
            StepBinding.Cinematic("M02", 12f, "mars.seq.M02"),
            StepBinding.Cinematic("M03", 5f, "mars.seq.M03"),
            StepBinding.Cinematic("M04", 18f, "mars.seq.M04", "MAR.T02"),
            StepBinding.Cinematic("M05", 4f, "mars.seq.M05"),
            StepBinding.Cinematic("M06", 5f, "mars.seq.M06"),
            StepBinding.Gameplay("M07", 13f, null, null, Events.TutorialDone),
            StepBinding.Gameplay("M08", 8f, null, "MAR.O01", Events.SpheresReached),
            StepBinding.Gameplay("M09", 12f, null, "MAR.O01", Events.SpheresSoilRecorded, Events.SpheresRockRecorded,
                Events.SpheresHematiteIdentified, Events.SpheresDone),
            StepBinding.Gameplay("M10", 10f, "MAR.T03", "MAR.O02", Events.OutcropReached),
            StepBinding.Gameplay("M11", 5f, null, "MAR.O02", Events.ArmPlaced),
            StepBinding.Gameplay("M12", 27f, null, "MAR.O02", Events.RatGround, Events.RatRetracted),
            StepBinding.Gameplay("M13", 18f, null, "MAR.O02", Events.ApxsMeasured, Events.MossbauerMeasured, Events.CompositionDone),
            StepBinding.Cinematic("M14", 14f, "mars.seq.M14"),
            StepBinding.Cinematic("M15", 5f, "mars.seq.M15", "MAR.T04"),
            StepBinding.Gameplay("M16", 7f, "MAR.T05", "MAR.O03", Events.ShieldReached),
            StepBinding.Gameplay("M17", 8f, null, "MAR.O03", Events.ShieldPhotographed),
            StepBinding.Gameplay("M18", 5f, null, "MAR.O04", Events.MeteoriteReached),
            StepBinding.Gameplay("M19", 10f, null, "MAR.O04", Events.MeteoriteMeasured, Events.MeteoritePhotographed),
            StepBinding.Cinematic("M20", 8f, "mars.seq.M20"),
            StepBinding.Gameplay("M21", 25f, "MAR.T06", "MAR.O05", Events.StormConserved, Events.StormSkyCleared),
            StepBinding.Cinematic("M22", 45f, "mars.seq.M22", "MAR.T07"),
            StepBinding.Cinematic("M23", 9f, "mars.seq.M23", "MAR.T08"),
            StepBinding.Gameplay("M24", 7f, null, "MAR.O06", Events.OverlookReached),
            StepBinding.Gameplay("M25", 7f, null, "MAR.O06", Events.SunReferenceCaptured),
            StepBinding.Gameplay("M26", 7f, null, "MAR.O07", Events.ValleyCaptured),
            StepBinding.Cinematic("M27", 6f, "mars.seq.M27"),
            StepBinding.Gameplay("M28", 7f, null, "MAR.O06", Events.SunDustCaptured),
            StepBinding.Gameplay("M29", 6f, null, "MAR.O08", Events.AntennaPrepared),
            StepBinding.Gameplay("M30", 5f, null, null, Events.MemoryConfirmed),
            StepBinding.Cinematic("M31", 30f, "mars.seq.M31", "MAR.T09"),
            StepBinding.Cinematic("M32", 4f, "mars.seq.M32"),
        };

        /// <summary>Lê o JSON. Devolve nulo e acrescenta a mensagem em <paramref name="errors"/> se vazio ou malformado.</summary>
        public static SubtitleTable ParseSubtitles(string json, List<string> errors)
        {
            const string owner = "MarsSubtitles.json";
            if (string.IsNullOrWhiteSpace(json))
            {
                ContractValidation.Report(errors, owner, "ausente ou vazio.");
                return null;
            }

            try
            {
                var table = JsonUtility.FromJson<SubtitleTable>(json);
                if (table == null)
                    ContractValidation.Report(errors, owner, "não pôde ser lido.");
                return table;
            }
            catch (ArgumentException exception)
            {
                ContractValidation.Report(errors, owner, "JSON malformado: " + exception.Message);
                return null;
            }
        }

        /// <summary>Legendas de uma etapa cinemática no relógio da sequência (para a SequenceDefinition da MAR-018).</summary>
        public static List<SubtitleCue> BuildSequenceCues(SubtitleTable table, string stepId)
        {
            var result = new List<SubtitleCue>();
            if (table == null || table.cues == null)
                return result;

            foreach (var entry in table.cues)
            {
                if (entry != null && entry.stepId == stepId && entry.clock == ClockSequence)
                    result.Add(entry.ToCue());
            }

            return result;
        }

        /// <summary>Valida os assets serializados (Editor, ValidateSetup da MAR-009). Vazia = conforme.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var table = ParseSubtitles(subtitlesJson != null ? subtitlesJson.text : null, errors);
            if (table != null)
                errors.AddRange(Validate(chapter, table, steps != null && steps.Count > 0 ? steps : CreateDefaultBindings()));
            return errors;
        }

        /// <summary>
        /// Confere capítulo, tabela de etapas e legendas entre si: 32 etapas com binding único, referência somando
        /// 360 s, eventos únicos e válidos, 49 cues sem ID/chave duplicada, PT e EN preenchidos, relógio coerente com o
        /// tipo da etapa, janela dentro da referência e gatilho que é evento da própria etapa.
        /// </summary>
        public static List<string> Validate(ChapterDefinition chapterDefinition, SubtitleTable table, IReadOnlyList<StepBinding> bindings)
        {
            var errors = new List<string>();
            const string owner = "MarsChapterFlow";

            if (chapterDefinition == null)
            {
                ContractValidation.Report(errors, owner, "MarsChapter.asset não atribuído.");
                return errors;
            }

            errors.AddRange(chapterDefinition.Validate());
            if (chapterDefinition.ChapterId != MarsChapterId)
                ContractValidation.Report(errors, owner, $"chapterId deve ser '{MarsChapterId}' (atual '{chapterDefinition.ChapterId}').");
            if (chapterDefinition.Steps.Count != ExpectedStepCount)
                ContractValidation.Report(errors, owner, $"esperadas {ExpectedStepCount} etapas, há {chapterDefinition.Steps.Count}.");

            var byStep = ValidateBindings(errors, owner, chapterDefinition, bindings);
            if (table == null)
            {
                ContractValidation.Report(errors, owner, "MarsSubtitles.json ausente.");
                return errors;
            }

            var keys = ValidateCues(errors, chapterDefinition, table, byStep);
            ValidateTexts(errors, table, keys);

            foreach (var key in new[] { chapterDefinition.TitleKey, chapterDefinition.DescriptionKey, chapterDefinition.QuestionKey })
                RequireTextKey(errors, owner, table, key);
            foreach (var binding in byStep.Values)
            {
                RequireTextKey(errors, owner, table, binding.StampKey, optional: true);
                RequireTextKey(errors, owner, table, binding.ObjectiveKey, optional: true);
            }

            return errors;
        }

        static Dictionary<string, StepBinding> ValidateBindings(List<string> errors, string owner, ChapterDefinition chapterDefinition,
            IReadOnlyList<StepBinding> bindings)
        {
            var byStep = new Dictionary<string, StepBinding>();
            var eventOwner = new Dictionary<string, string>();
            var referenceTotal = 0f;

            foreach (var binding in bindings ?? new List<StepBinding>())
            {
                if (binding == null)
                {
                    ContractValidation.Report(errors, owner, "binding nulo na tabela.");
                    continue;
                }

                var where = $"binding '{binding.StepId}'";
                ContractValidation.RequireId(errors, owner, "stepId", binding.StepId);
                if (!ContractValidation.IsValidId(binding.StepId))
                    continue;
                if (byStep.ContainsKey(binding.StepId))
                {
                    ContractValidation.Report(errors, owner, $"{where} duplicado.");
                    continue;
                }

                byStep.Add(binding.StepId, binding);
                if (!chapterDefinition.TryGetStep(binding.StepId, out _))
                    ContractValidation.Report(errors, owner, $"{where} não existe em MarsChapter.asset.");

                ContractValidation.RequirePositive(errors, owner, $"{where}.referenceSeconds", binding.ReferenceSeconds);
                referenceTotal += binding.ReferenceSeconds;

                if (binding.CompletionEvents.Count == 0)
                    ContractValidation.Report(errors, owner, $"{where} não tem evento de conclusão (a etapa travaria).");

                foreach (var eventId in binding.CompletionEvents)
                {
                    ContractValidation.RequireId(errors, owner, $"{where}.completionEvents[]", eventId);
                    if (string.IsNullOrEmpty(eventId))
                        continue;

                    string other;
                    if (eventOwner.TryGetValue(eventId, out other))
                        ContractValidation.Report(errors, owner, $"evento '{eventId}' usado em '{other}' e '{binding.StepId}'.");
                    else
                        eventOwner.Add(eventId, binding.StepId);
                }

                if (binding.IsCinematic)
                {
                    ContractValidation.RequireId(errors, owner, $"{where}.sequenceId", binding.SequenceId);
                    if (binding.CompletionEvents.Count != 1 || binding.CompletionEvents[0] != binding.SequenceId)
                        ContractValidation.Report(errors, owner, $"{where}: cinemática conclui só pelo fim da própria sequência ('{binding.SequenceId}').");
                }
                else if (!string.IsNullOrEmpty(binding.SequenceId))
                {
                    ContractValidation.Report(errors, owner, $"{where}: etapa de gameplay não tem sequência própria (use uma etapa cinemática).");
                }
            }

            foreach (var step in chapterDefinition.Steps)
            {
                if (step != null && ContractValidation.IsValidId(step.StepId) && !byStep.ContainsKey(step.StepId))
                    ContractValidation.Report(errors, owner, $"etapa '{step.StepId}' de MarsChapter.asset sem binding.");
            }

            if (Math.Abs(referenceTotal - ReferenceDurationSeconds) > 0.01f)
                ContractValidation.Report(errors, owner, $"referências somam {referenceTotal} s; o roteiro aprovado soma {ReferenceDurationSeconds} s.");
            if (Math.Abs(chapterDefinition.TargetDurationSeconds - referenceTotal) > 0.01f)
                ContractValidation.Report(errors, owner, $"targetDurationSeconds ({chapterDefinition.TargetDurationSeconds}) difere da soma das referências ({referenceTotal}).");

            StepBinding initial;
            if (chapterDefinition.InitialStepId != null && byStep.TryGetValue(chapterDefinition.InitialStepId, out initial)
                && initial.SequenceId != chapterDefinition.InitialSequenceId)
            {
                ContractValidation.Report(errors, owner,
                    $"initialSequenceId '{chapterDefinition.InitialSequenceId}' difere da sequência de '{initial.StepId}' ('{initial.SequenceId}').");
            }

            return byStep;
        }

        static HashSet<string> ValidateCues(List<string> errors, ChapterDefinition chapterDefinition, SubtitleTable table,
            Dictionary<string, StepBinding> byStep)
        {
            const string owner = "MarsSubtitles.json";
            var keys = new HashSet<string>();
            var cueIds = new HashSet<string>();
            var cues = table.cues ?? new List<SubtitleEntry>();

            if (table.chapterId != MarsChapterId)
                ContractValidation.Report(errors, owner, $"chapterId deve ser '{MarsChapterId}' (atual '{table.chapterId}').");
            if (cues.Count != ExpectedCueCount)
                ContractValidation.Report(errors, owner, $"esperadas {ExpectedCueCount} cues do roteiro, há {cues.Count}.");

            foreach (var entry in cues)
            {
                if (entry == null)
                {
                    ContractValidation.Report(errors, owner, "cue nula.");
                    continue;
                }

                var where = $"cue '{entry.cueId}'";
                if (ContractValidation.IsValidId(entry.cueId) && !cueIds.Add(entry.cueId))
                    ContractValidation.Report(errors, owner, $"{where} duplicada.");
                if (ContractValidation.IsValidId(entry.textKey) && !keys.Add(entry.textKey))
                    ContractValidation.Report(errors, owner, $"chave '{entry.textKey}' duplicada.");
                if (string.IsNullOrWhiteSpace(entry.pt))
                    ContractValidation.Report(errors, owner, $"{where} sem texto PT.");
                if (string.IsNullOrWhiteSpace(entry.en))
                    ContractValidation.Report(errors, owner, $"{where} sem texto EN.");
                if (entry.position != "bottom" && entry.position != "top")
                    ContractValidation.Report(errors, owner, $"{where}: position '{entry.position}' (use bottom ou top).");

                if (entry.clock != ClockSequence && entry.clock != ClockStep && entry.clock != ClockTrigger)
                {
                    ContractValidation.Report(errors, owner, $"{where}: clock '{entry.clock}' (use sequence, step ou trigger).");
                    continue;
                }

                errors.AddRange(entry.ToCue().Validate());

                StepBinding binding;
                if (!byStep.TryGetValue(entry.stepId ?? string.Empty, out binding))
                {
                    ContractValidation.Report(errors, owner, $"{where}: etapa '{entry.stepId}' não existe.");
                    continue;
                }

                if (entry.clock == ClockSequence && !binding.IsCinematic)
                    ContractValidation.Report(errors, owner, $"{where}: clock=sequence só vale em etapa cinemática ('{entry.stepId}' é gameplay).");
                if (entry.clock != ClockSequence && binding.IsCinematic)
                    ContractValidation.Report(errors, owner, $"{where}: etapa cinemática '{entry.stepId}' usa clock=sequence.");

                if (entry.clock == ClockTrigger)
                {
                    var isEvent = false;
                    foreach (var eventId in binding.CompletionEvents)
                        isEvent |= eventId == entry.triggerId;
                    if (!isEvent)
                        ContractValidation.Report(errors, owner, $"{where}: gatilho '{entry.triggerId}' não é evento da etapa '{entry.stepId}'.");
                }
                else if (entry.endSeconds > binding.ReferenceSeconds + ContractValidation.TimeToleranceSeconds)
                {
                    ContractValidation.Report(errors, owner,
                        $"{where}: termina em {entry.endSeconds} s, depois da referência de '{entry.stepId}' ({binding.ReferenceSeconds} s).");
                }
            }

            return keys;
        }

        static void ValidateTexts(List<string> errors, SubtitleTable table, HashSet<string> keys)
        {
            const string owner = "MarsSubtitles.json";
            foreach (var entry in table.texts ?? new List<TextEntry>())
            {
                if (entry == null)
                {
                    ContractValidation.Report(errors, owner, "texto nulo.");
                    continue;
                }

                ContractValidation.RequireId(errors, owner, "texts[].key", entry.key);
                if (ContractValidation.IsValidId(entry.key) && !keys.Add(entry.key))
                    ContractValidation.Report(errors, owner, $"chave '{entry.key}' duplicada.");
                if (string.IsNullOrWhiteSpace(entry.pt))
                    ContractValidation.Report(errors, owner, $"texto '{entry.key}' sem PT.");
                if (string.IsNullOrWhiteSpace(entry.en))
                    ContractValidation.Report(errors, owner, $"texto '{entry.key}' sem EN.");
            }
        }

        static void RequireTextKey(List<string> errors, string owner, SubtitleTable table, string key, bool optional = false)
        {
            if (optional && string.IsNullOrEmpty(key))
                return;

            string pt, en;
            if (!table.TryGetText(key, "pt", out pt) || !table.TryGetText(key, "en", out en))
                ContractValidation.Report(errors, owner, $"chave '{key}' sem texto PT/EN em MarsSubtitles.json.");
        }

#if UNITY_EDITOR
        void Reset() => steps = CreateDefaultBindings();

        [ContextMenu("MAR-001/Restaurar tabela padrão das etapas")]
        void RestoreDefaultBindings()
        {
            UnityEditor.Undo.RecordObject(this, "Restaurar etapas de Marte");
            steps = CreateDefaultBindings();
        }

        [ContextMenu("MAR-001/Validar dados")]
        void ValidateFromMenu()
        {
            var errors = Validate();
            if (errors.Count == 0)
                Debug.Log("[MarsChapterFlow] sem divergências.", this);
            else
                Debug.LogError("[MarsChapterFlow]" + ContractValidation.Describe(errors), this);
        }
#endif
    }
}
