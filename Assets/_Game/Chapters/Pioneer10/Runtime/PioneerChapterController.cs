using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Relatório de uma execução do capítulo: segundos de tempo ATIVO por estado (sem pausa, arquivo nem carregamento).
    /// É a medição do OPERADOR (o jogo, um teste automatizado ou quem dirige a sessão), nunca um resultado humano:
    /// <see cref="CountsAsHumanResult"/> é sempre falso. Resultado humano só vem das sessões de QA-004, registradas à parte.
    /// </summary>
    [Serializable]
    public sealed class PioneerRunReport
    {
        public const string Source = "operador (não é sessão humana)";

        public List<string> states = new List<string>();
        public List<float> activeSeconds = new List<float>();
        public float totalActiveSeconds;
        public bool finished;

        /// <summary>Sempre falso: a duração medida aqui nunca é relatada como resultado humano.</summary>
        public bool CountsAsHumanResult => false;

        public override string ToString() =>
            $"Pioneer 10 · {Source} · {totalActiveSeconds:0.0} s ativos em {states.Count} estado(s){(finished ? ", concluído" : string.Empty)}";
    }

    /// <summary>
    /// PIO-014 · Controlador do capítulo Pioneer 10: liga os oito estados ao contrato global (<see cref="IChapterDirector"/>) e
    /// faz a rota dossiê → Launch → Belt → JupiterApproach → Imaging → Closest → Plaque → Outbound → Listening → Farewell → resultado →
    /// arquivo, sem comandos manuais do Editor. A ordem vem da tabela de etapas de <c>Data/PioneerChapter.asset</c>
    /// (nunca de um enum) e cada etapa só conclui quando a ATIVIDADE do estado conclui; nenhum cronômetro conclui nada.
    /// <list type="bullet">
    /// <item><b>Uma fonte</b> de duração e eventos por estado: a sequência dele no <see cref="SequenceDirector"/> (um relógio só).</item>
    /// <item><b>Entrada:</b> cada atividade pede o contexto Panel/Gameplay e o devolve ao sair; o E é consumido uma vez (rearme).</item>
    /// <item><b>Reset:</b> ao mudar de estado, reiniciar o capítulo ou destruir a cena, cancela a atividade do estado,
    /// a narração (legenda, música, efeitos), a recuperação de radiação e as fotos; solta todos os ouvintes. Repetir recarrega a cena
    /// (<see cref="IChapterDirector.RestartChapter"/>), e o novo controlador começa do zero.</item>
    /// <item><b>Tempo:</b> mede o tempo ativo por estado (<see cref="LastReport"/>), sem pausa, arquivo nem carregamento.</item>
    /// </list>
    /// Uso (R1/INT-001, depois de carregar a cena e ANTES da primeira etapa; chamar de novo é seguro):
    /// <c>controller.Initialize(chapterDirector, sessionClock, inputRouter, interactionService, audioPort, "pt")</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PioneerChapterController : MonoBehaviour
    {
        public const string ChapterId = "pioneer10";

        /// <summary>Os nove estados na ordem do roteiro (também os IDs das etapas de PioneerChapter.asset).</summary>
        public static readonly string[] StateIds =
        {
            "PIO.Launch", "PIO.Belt", "PIO.JupiterApproach", "PIO.Imaging", "PIO.Closest", "PIO.Plaque", "PIO.Outbound", "PIO.Listening", "PIO.Farewell",
        };

        [SerializeField] ChapterDefinition chapter;
        [SerializeField] PioneerPresentation presentation;
        [SerializeField] SequenceDirector sequenceDirector;
        [SerializeField] PioneerNarration narration;
        [Header("Atividades")]
        [SerializeField] PioneerLaunchActivity launch;
        [SerializeField] BeltInvestigation belt;
        [SerializeField, Tooltip("PIO.JupiterApproach é só cinema (sem interação): conclui quando a sequência termina.")] SequenceDefinition jupiterSequence;
        [SerializeField] PioneerImaging imaging;
        [SerializeField, Tooltip("PIO.Closest (a máxima aproximação de Júpiter) é só cinema: conclui quando a sequência termina.")] SequenceDefinition closestSequence;
        [SerializeField, Tooltip("PIO.Belt (roteiro v2, cena 2) é só cinema: a sequência de 54 s e o contador de dias.")] SequenceDefinition beltCinemaSequence;
        [SerializeField] PioneerBeltCounter beltCounter;
        [SerializeField, Tooltip("Cena 3: Júpiter nasce de um ponto e a Terra pequena entra para a comparação.")] PioneerJupiterReveal jupiterReveal;
        [SerializeField, Tooltip("Cena 5: medidor de radiação, estática, velocidade e comparação de distâncias.")] PioneerClosestHud closestHud;
        [SerializeField, Tooltip("Cena 7: ano, linha do tempo, instrumentos e medidor de energia dos RTG.")] PioneerYearsHud yearsHud;
        [SerializeField, Tooltip("Cena 9: as seis estrelas das primeiras vezes.")] PioneerFirstsHud firstsHud;
        [SerializeField, Tooltip("Fase 5: barras de cinema nos estados que são só cinema.")] PioneerLetterbox letterbox;
        [SerializeField] ImagingRecovery recovery;
        [SerializeField] HistoricalImageReveal reveal;
        [SerializeField] PioneerPlaqueActivity plaque;
        [SerializeField] OutboundAtlas outbound;
        [SerializeField] PioneerListening listening;
        [SerializeField] PioneerFarewellActivity farewell;

        IChapterDirector director;
        ChapterDirector concreteDirector;
        IActiveTimeSource time;
        InputRouter router;
        IInteractionService interactions;
        Func<bool> returnToArchive;
        bool initialized, cinemaBound;
        string cinemaState;
        string currentState;
        float stateSeconds;
        readonly PioneerRunReport report = new PioneerRunReport();
        readonly List<Action> unsubscribers = new List<Action>();

        /// <summary>Entrou num estado (ID de PioneerChapter.asset).</summary>
        public event Action<string> StateEntered;
        /// <summary>O estado concluiu (a atividade avisou) e a etapa foi entregue ao ChapterDirector; segundos ATIVOS dele.</summary>
        public event Action<string, float> StateCompleted;
        /// <summary>O jogador escolheu "Voltar ao arquivo" no memorial. Se o diretor é o ChapterDirector real, o arquivo já foi pedido.</summary>
        public event Action ReturnToArchiveRequested;
        /// <summary>O jogador escolheu "Repetir capítulo": o controlador pediu <see cref="IChapterDirector.RestartChapter"/>.</summary>
        public event Action ReplayRequested;

        public ChapterDefinition Chapter => chapter;
        public SequenceDirector Sequences => sequenceDirector;
        public PioneerPresentation Presentation => presentation;
        public string CurrentState => currentState;
        public bool IsInitialized => initialized;
        public SequenceDefinition JupiterSequence => jupiterSequence;
        public SequenceDefinition ClosestSequence => closestSequence;
        public SequenceDefinition BeltCinemaSequence => beltCinemaSequence;

        /// <summary>Relatório do tempo ativo por estado desta execução (operador assistido, nunca resultado humano).</summary>
        public PioneerRunReport LastReport => report;

        /// <summary>Segundos ativos do estado atual (zero na pausa).</summary>
        public float CurrentStateSeconds => stateSeconds;

        public void Initialize(IChapterDirector chapterDirector, IActiveTimeSource activeTime, InputRouter inputRouter,
            IInteractionService interactionService, IPioneerAudioPort audio = null, string language = PioneerData.Portuguese,
            Func<bool> returnToArchiveAction = null)
        {
            var errors = ValidateSetup();
            if (errors.Count > 0)
                throw new InvalidOperationException("Montagem do capítulo Pioneer 10 inválida:\n- " + string.Join("\n- ", errors));
            if (chapterDirector == null || activeTime == null || inputRouter == null || interactionService == null)
                throw new ArgumentNullException(chapterDirector == null ? nameof(chapterDirector) : activeTime == null ? nameof(activeTime)
                    : inputRouter == null ? nameof(inputRouter) : nameof(interactionService));

            Teardown();
            director = chapterDirector;
            concreteDirector = chapterDirector as ChapterDirector;
            time = activeTime;
            router = inputRouter;
            interactions = interactionService;
            returnToArchive = returnToArchiveAction;

            // Cinema: uma fonte de duração e eventos. Sem terreno no espaço, a verificação de chão fica desligada.
            presentation.CameraDirector.GroundProbe = _ => null;
            sequenceDirector.Initialize(presentation.CameraDirector, router, time);
            presentation.RegisterWithCameraDirector();
            narration.Initialize(sequenceDirector, audio, language);

            var data = narration.Data;
            Func<string, string> text = key =>
            {
                var value = data != null ? data.GetText(key, PioneerData.Normalize(language)) : null;
                return string.IsNullOrEmpty(value) ? key : value;
            };

            // Roteiro v2: só duas interações no capítulo (a foto e a sintonia de rádio). Lançamento, placa e anos são cinema.
            launch.CinemaMode = true;
            plaque.CinemaMode = true;
            outbound.CinemaMode = true;
            outbound.YearsMode = true;      // cena 7: o HUD dos anos substitui o painel do atlas
            imaging.ThreeStepMode = true;   // cena 4: a foto em três passos (sem radiação nem recuperação)
            listening.SingleRound = true;   // cena 8: uma única rodada de sintonia
            launch.Initialize(sequenceDirector, interactions, router, time);
            belt.Initialize(sequenceDirector, interactions, router, time, text);
            beltCounter.Initialize(sequenceDirector, text);
            jupiterReveal.Initialize(sequenceDirector);
            closestHud.Initialize(sequenceDirector, text);
            yearsHud.Initialize(sequenceDirector, text);
            firstsHud.Initialize(sequenceDirector, text);
            letterbox.Initialize(sequenceDirector, () => currentState, time);
            imaging.Initialize(sequenceDirector, interactions, router, time, text);
            recovery.Initialize(interactions, time, text);
            reveal.Initialize(time, text);
            plaque.Initialize(sequenceDirector, interactions, router, time, text);
            outbound.Initialize(sequenceDirector, interactions, router, time, text);
            listening.Initialize(sequenceDirector, interactions, router, time, text);
            farewell.Initialize(sequenceDirector, interactions, router, time, text);

            Bind(launch, nameof(launch), h => launch.Completed += h, h => launch.Completed -= h, "PIO.Launch");
            Bind(imaging, nameof(imaging), h => imaging.Completed += h, h => imaging.Completed -= h, "PIO.Imaging");
            Bind(plaque, nameof(plaque), h => plaque.Completed += h, h => plaque.Completed -= h, "PIO.Plaque");
            Bind(outbound, nameof(outbound), h => outbound.Completed += h, h => outbound.Completed -= h, "PIO.Outbound");
            Bind(listening, nameof(listening), h => listening.Completed += h, h => listening.Completed -= h, "PIO.Listening");
            Bind(farewell, nameof(farewell), h => farewell.Completed += h, h => farewell.Completed -= h, "PIO.Farewell");

            Action onReturn = OnReturnRequested;
            Action onReplay = OnReplayRequested;
            farewell.ReturnToArchiveRequested += onReturn;
            farewell.ReplayRequested += onReplay;
            unsubscribers.Add(() =>
            {
                farewell.ReturnToArchiveRequested -= onReturn;
                farewell.ReplayRequested -= onReplay;
            });

            director.StepEntered += OnStepEntered;
            unsubscribers.Add(() => director.StepEntered -= OnStepEntered);
            if (concreteDirector != null)
            {
                concreteDirector.PausedChanged += OnPausedChanged;
                unsubscribers.Add(() => concreteDirector.PausedChanged -= OnPausedChanged);
            }

            initialized = true;
            report.states.Clear();
            report.activeSeconds.Clear();
            report.totalActiveSeconds = 0f;
            report.finished = false;

            // Se a primeira etapa já foi anunciada antes desta chamada, entra nela agora (idempotente).
            if (!string.IsNullOrEmpty(director.CurrentStepId))
                OnStepEntered(director.CurrentStepId);
        }

        void Bind(UnityEngine.Object activity, string label, Action<Action> subscribe, Action<Action> unsubscribe, string stateId)
        {
            if (activity == null)
                throw new InvalidOperationException("atividade ausente: " + label);

            Action handler = () => CompleteState(stateId);
            subscribe(handler);
            unsubscribers.Add(() => unsubscribe(handler));
        }

        // ---------- Estados ----------

        void OnStepEntered(string stepId)
        {
            if (!initialized || Array.IndexOf(StateIds, stepId) < 0)
                return;

            CancelActive();
            currentState = stepId;
            stateSeconds = 0f;
            narration.EnterState(stepId);
            StateEntered?.Invoke(stepId);
            switch (stepId)
            {
                case "PIO.Launch": launch.Begin(); break;
                case "PIO.Belt": BeginCinema(stepId, "PIO.Belt", beltCinemaSequence); break; // cinema: a arena de poeira interativa não é usada
                case "PIO.JupiterApproach": BeginCinema(stepId, "PIO.JupiterApproach", jupiterSequence); break;
                case "PIO.Imaging": imaging.Begin(); break;
                case "PIO.Closest": BeginCinema(stepId, "PIO.Closest", closestSequence); break;
                case "PIO.Plaque": plaque.Begin(); break;
                case "PIO.Outbound": outbound.Begin(); break;
                case "PIO.Listening": listening.Begin(); break;
                case "PIO.Farewell": farewell.Begin(); break;
            }
        }

        /// <summary>Estados só de cinema (PIO.JupiterApproach e PIO.Closest): concluem pelo fim da sequência, nunca por cronômetro.</summary>
        void BeginCinema(string stateId, string anchorId, SequenceDefinition sequence)
        {
            if (!cinemaBound)
            {
                cinemaBound = true;
                sequenceDirector.Finished += OnCinemaFinished;
            }

            cinemaState = stateId;
            presentation.BindSequenceDirector(sequenceDirector);
            presentation.ShowAnchor(anchorId);
            sequenceDirector.Play(sequence, false);
        }

        void OnCinemaFinished(SequenceResult result)
        {
            if (cinemaState == null)
                return;

            var state = cinemaState;
            cinemaState = null;
            presentation.UnbindSequenceDirector();
            if (result.Reason != SequenceExitReason.Canceled)
                CompleteState(state);
        }

        void CompleteState(string stateId)
        {
            if (!initialized || stateId != currentState)
                return; // conclusão de outro estado (ou repetida) não vale

            var seconds = stateSeconds;
            report.states.Add(stateId);
            report.activeSeconds.Add(seconds);
            currentState = null;
            StateCompleted?.Invoke(stateId, seconds);
            var last = stateId == StateIds[StateIds.Length - 1];
            report.finished |= last;
            director.CompleteStep(stateId); // a última etapa é terminal: FinishChapter, uma única vez
        }

        void OnReplayRequested()
        {
            ReplayRequested?.Invoke();
            director.RestartChapter(); // recarrega a cena: o novo controlador começa do zero
        }

        void OnReturnRequested()
        {
            ReturnToArchiveRequested?.Invoke();
            if (returnToArchive != null)
                returnToArchive();
            else if (concreteDirector != null)
                concreteDirector.ReturnToDossier();
        }

        void OnPausedChanged(bool paused)
        {
            if (narration != null)
                narration.SetPaused(paused);
        }

        /// <summary>Pausa global (shell): congela a legenda e o áudio da narração. Retomar exige o clique do shell.</summary>
        public void SetPaused(bool paused)
        {
            if (initialized)
                narration.SetPaused(paused);
        }

        /// <summary>Depois do primeiro clique que libera o áudio do navegador, refaz os pedidos de música.</summary>
        public void Resync()
        {
            if (initialized)
                narration.Resync();
        }

        // ---------- Tempo ativo ----------

        void Update()
        {
            if (timeSourceIsDestroyed())
                return;

            Tick(time != null ? time.ActiveDeltaSeconds : 0f);
        }

        bool timeSourceIsDestroyed() => time is UnityEngine.Object unityObject && unityObject == null;

        /// <summary>Soma tempo ATIVO (zero na pausa) ao estado atual. Testes chamam direto.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (!initialized || currentState == null || activeDeltaSeconds <= 0f)
                return;

            stateSeconds += activeDeltaSeconds;
            report.totalActiveSeconds += activeDeltaSeconds;
        }

        // ---------- Saída ----------

        /// <summary>Cancela a atividade do estado atual e tudo que ela deixou: legenda, música, efeitos, recuperação e fotos.</summary>
        void CancelActive()
        {
            if (beltCounter != null)
                beltCounter.Hide();
            if (jupiterReveal != null)
                jupiterReveal.Hide();
            if (closestHud != null)
                closestHud.Hide();
            if (yearsHud != null)
                yearsHud.Hide();
            if (firstsHud != null)
                firstsHud.Hide();
            if (cinemaState != null)
            {
                cinemaState = null;
                presentation.UnbindSequenceDirector();
                if (sequenceDirector.IsPlaying)
                    sequenceDirector.Cancel();
            }

            launch.Cancel();
            belt.Cancel();
            imaging.Cancel();
            recovery.Reset();
            plaque.Cancel();
            outbound.Cancel();
            listening.Cancel();
            farewell.Cancel();
        }

        /// <summary>Solta ouvintes e entradas e zera o capítulo. Seguro chamar mais de uma vez.</summary>
        public void Teardown()
        {
            if (initialized)
            {
                CancelActive();
                narration.Cancel();
            }

            foreach (var unsubscribe in unsubscribers)
                unsubscribe();
            unsubscribers.Clear();
            if (cinemaBound && sequenceDirector != null)
                sequenceDirector.Finished -= OnCinemaFinished;
            cinemaBound = false;
            cinemaState = null;
            if (initialized && presentation != null)
                presentation.UnregisterFromCameraDirector();
            initialized = false;
            currentState = null;
            stateSeconds = 0f;
        }

        void OnDestroy() => Teardown();

        /// <summary>
        /// Erros de montagem (lista vazia = conforme): referências, a tabela de etapas igual aos nove estados e em ordem,
        /// uma sequência para cada estado com anchor no palco e as sequências de cinema (Júpiter e máxima aproximação).
        /// </summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = "PioneerChapterController";
            void Require(UnityEngine.Object value, string name)
            {
                if (value == null)
                    errors.Add($"{owner}: {name} não atribuído.");
            }

            Require(presentation, nameof(presentation));
            Require(sequenceDirector, nameof(sequenceDirector));
            Require(narration, nameof(narration));
            Require(launch, nameof(launch));
            Require(belt, nameof(belt));
            Require(jupiterSequence, nameof(jupiterSequence));
            Require(closestSequence, nameof(closestSequence));
            Require(beltCinemaSequence, nameof(beltCinemaSequence));
            Require(beltCounter, nameof(beltCounter));
            Require(jupiterReveal, nameof(jupiterReveal));
            Require(closestHud, nameof(closestHud));
            Require(yearsHud, nameof(yearsHud));
            Require(firstsHud, nameof(firstsHud));
            Require(letterbox, nameof(letterbox));
            Require(imaging, nameof(imaging));
            Require(recovery, nameof(recovery));
            Require(reveal, nameof(reveal));
            Require(plaque, nameof(plaque));
            Require(outbound, nameof(outbound));
            Require(listening, nameof(listening));
            Require(farewell, nameof(farewell));
            if (chapter == null)
            {
                errors.Add($"{owner}: chapter (PioneerChapter.asset) não atribuído.");
                return errors;
            }

            errors.AddRange(chapter.Validate().ConvertAll(e => $"{owner}: {e}"));
            if (chapter.ChapterId != ChapterId)
                errors.Add($"{owner}: o capítulo deve ter o ID '{ChapterId}' (igual ao slot do dossiê), e tem '{chapter.ChapterId}'.");
            var stepIds = new List<string>();
            var cursor = chapter.InitialStepId;
            for (var i = 0; i < 64 && cursor != null && chapter.TryGetStep(cursor, out var step); i++)
            {
                stepIds.Add(cursor);
                cursor = step.NextStepIds.Count == 1 ? step.NextStepIds[0] : null;
            }

            if (stepIds.Count != StateIds.Length)
                errors.Add($"{owner}: a tabela de etapas tem {stepIds.Count} etapa(s) em fila; o capítulo tem {StateIds.Length} estados.");
            else
            {
                for (var i = 0; i < StateIds.Length; i++)
                {
                    if (stepIds[i] != StateIds[i])
                        errors.Add($"{owner}: etapa {i + 1} é '{stepIds[i]}'; o roteiro pede '{StateIds[i]}'.");
                }

                if (!chapter.IsTerminalStep(StateIds[StateIds.Length - 1]))
                    errors.Add($"{owner}: a última etapa ({StateIds[StateIds.Length - 1]}) deve ser terminal.");
            }

            if (presentation != null)
            {
                foreach (var id in StateIds)
                {
                    if (!presentation.TryGetAnchor(id, out _))
                        errors.Add($"{owner}: o palco não tem o anchor do estado {id}.");
                }
            }

            if (jupiterSequence != null)
                errors.AddRange(jupiterSequence.Validate().ConvertAll(e => $"{owner}: {e}"));
            if (closestSequence != null)
                errors.AddRange(closestSequence.Validate().ConvertAll(e => $"{owner}: {e}"));
            if (beltCinemaSequence != null)
                errors.AddRange(beltCinemaSequence.Validate().ConvertAll(e => $"{owner}: {e}"));
            return errors;
        }
    }
}
