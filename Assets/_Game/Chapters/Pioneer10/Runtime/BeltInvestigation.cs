using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum BeltPhase
    {
        Idle = 0,
        /// <summary>0–12 s: mapa Sol–Marte–Júpiter e a pergunta sobre partículas.</summary>
        Map = 1,
        /// <summary>12–27 s: fantasia identificada (selo IMAGINAÇÃO em todos os quadros), 15 s, sem dano.</summary>
        Fantasy = 2,
        /// <summary>27–40 s: moldura recolhida, espaço real quase vazio.</summary>
        Reality = 3,
        /// <summary>40–58 s: cinco registros de poeira (síntese de medições), hold de E 2 s por registro.</summary>
        Record = 4,
        Done = 5,
    }

    /// <summary>O que o painel desenha neste quadro (sem tipos de UI: a lógica é testável sem painel).</summary>
    public struct BeltViewState
    {
        public BeltPhase Phase;
        public bool SealVisible;
        /// <summary>Posição do ícone da sonda na arena (0–1, y para baixo).</summary>
        public Vector2 Probe;
        public Vector2 RockA, RockB, RockC;
        public bool ProbeDeformed;
        /// <summary>Direção ensinada agora: 0 cima, 1 direita, 2 baixo, 3 esquerda.</summary>
        public int ArrowDirection;
        public int Recorded;
        /// <summary>Registro em foco (0–4); 5 quando todos concluídos.</summary>
        public int Focus;
        /// <summary>Progresso do registro em foco (0–1).</summary>
        public float FocusProgress;
        public bool Acquiring;
        /// <summary>0 normal, 1 após 6 s sem ação compreendida (reforça o controle), 2 após 12 s (oferece ajuda).</summary>
        public int HelpLevel;
        public bool Assisted;
    }

    /// <summary>
    /// PIO-005 · Estado PIO.Belt (58 s): mapa, fantasia de 15 s com selo, contraste com o espaço real e aquisição
    /// guiada de cinco registros. Não existe vida, dano, Game Over nem repetição: a fantasia termina pelo relógio
    /// narrativo mesmo com a sonda parada, e os registros só avançam com ação do jogador (E mantido, clique ou ajuda).
    /// O número cinco é amostragem de gameplay, não contagem histórica de impactos.
    /// </summary>
    public sealed class BeltInvestigation : MonoBehaviour
    {
        public const string StateId = "PIO.Belt";
        public const string EventMap = "PIO.Belt.Map";
        public const string EventFantasy = "PIO.Belt.Fantasy";
        public const string EventReality = "PIO.Belt.Reality";
        public const string EventRecord = "PIO.Belt.Record";
        /// <summary>Antes de PIO.02.06 ("hold 5/5"): a sequência espera os cinco registros.</summary>
        public const string EventGate = "PIO.Belt.Gate";
        public const string CandidateId = "PIO.Belt.Record";
        public const string PromptKey = "PIO.UI.dust";
        public const int RecordCount = 5;
        public const float SecondsPerRecord = 2f;
        public const float TotalRecordSeconds = RecordCount * SecondsPerRecord;
        public const float HelpPulseSeconds = 6f;
        public const float HelpOfferSeconds = 12f;

        static readonly Vector2[] RockStart = { new Vector2(1.10f, 0.28f), new Vector2(1.30f, 0.62f), new Vector2(1.55f, 0.45f) };
        static readonly Vector2[] RockVelocity = { new Vector2(-0.075f, 0.004f), new Vector2(-0.085f, -0.006f), new Vector2(-0.09f, 0.003f) };

        [SerializeField] SequenceDefinition sequence;
        [SerializeField] PioneerPresentation presentation;
        [SerializeField] BeltPanelView panel;
        [SerializeField, Tooltip("Brilho pequeno no sensor a cada registro (vai para o socket Reflector).")] Transform sensorGlow;
        [SerializeField, Min(0.05f), Tooltip("Fração da arena por segundo.")] float probeSpeed = 0.4f;
        [SerializeField, Min(0.005f)] float probeRadius = 0.035f;
        [SerializeField, Min(0.01f)] float rockRadius = 0.085f;
        [SerializeField, Tooltip("Largura/altura da arena, para medir contato em distância real.")] float arenaAspect = 1.6f;

        SequenceDirector director;
        IInteractionService interaction;
        InputRouter router;
        IActiveTimeSource timeSource;
        Func<string, string> resolveText;
        IDisposable inputLease;

        BeltPhase phase;
        bool running, pendingFantasy, pendingRecord, gateWaiting, completedRaised, recordOpen;
        float fantasyClock, deformTimer, glowTimer, idleSeconds;
        float acquiredSeconds, holdBase, holdSpan, clickTarget;
        bool assisted, holding;
        int helpLevel, contacts;
        readonly bool[] touching = new bool[3];
        Vector2 probe;
        readonly Vector2[] rocks = new Vector2[3];

        public event Action Completed;
        /// <summary>Contato gráfico na fantasia ("toc"): sem dano, só retorno.</summary>
        public event Action Contact;
        /// <summary>Registro concluído (1–5).</summary>
        public event Action<int> Recorded;

        public SequenceDefinition Sequence => sequence;
        public BeltPhase Phase => phase;
        public bool IsRunning => running;
        public bool IsGateWaiting => gateWaiting;
        public int RecordedCount => Mathf.Min(RecordCount, Mathf.FloorToInt(acquiredSeconds / SecondsPerRecord + 1e-4f));
        public float AcquiredSeconds => acquiredSeconds;
        public int Contacts => contacts;
        public int HelpLevel => helpLevel;
        public Vector2 ProbePosition => probe;

        public void Initialize(SequenceDirector sequenceDirector, IInteractionService interactionService, InputRouter inputRouter,
            IActiveTimeSource activeTime, Func<string, string> textResolver)
        {
            CleanUp();
            director = sequenceDirector;
            interaction = interactionService;
            router = inputRouter;
            timeSource = activeTime;
            resolveText = textResolver;
            if (panel != null)
            {
                panel.RecordClicked -= ClickRecord;
                panel.HelpClicked -= RequestHelp;
                panel.RecordClicked += ClickRecord;
                panel.HelpClicked += RequestHelp;
            }

            if (sensorGlow != null && presentation != null && presentation.TryGetSocket("Reflector", out var reflector))
            {
                sensorGlow.SetParent(reflector, false);
                sensorGlow.localPosition = new Vector3(0.9f, 0f, -0.05f);
                sensorGlow.gameObject.SetActive(false);
            }
        }

        public void Begin(bool skippable = false)
        {
            if (director == null)
                throw new InvalidOperationException("BeltInvestigation sem SequenceDirector: chame Initialize.");

            CleanUp();
            completedRaised = false;
            running = true;
            phase = BeltPhase.Map;
            director.EventRaised += OnEventRaised;
            director.Finished += OnFinished;
            if (presentation != null)
            {
                presentation.BindSequenceDirector(director);
                presentation.ShowAnchor(StateId);
            }

            director.Play(sequence, skippable);
            PushView();
        }

        public void Cancel()
        {
            if (running && director != null && director.IsPlaying)
                director.Cancel();
            else
                CleanUp();
        }

        void OnEventRaised(string eventId)
        {
            switch (eventId)
            {
                case EventMap:
                    phase = BeltPhase.Map;
                    break;
                case EventFantasy:
                    pendingFantasy = true; // adiado: ao pular, os eventos chegam em rajada e a sequência termina antes
                    break;
                case EventReality:
                    pendingFantasy = false;
                    EndFantasy();
                    phase = BeltPhase.Reality;
                    break;
                case EventRecord:
                    pendingRecord = true;
                    break;
                case EventGate:
                    if (RecordedCount < RecordCount && director.IsPlaying)
                    {
                        gateWaiting = true;
                        director.Pause();
                    }

                    break;
            }

            PushView();
        }

        void OnFinished(SequenceResult result)
        {
            var completed = result.Reason != SequenceExitReason.Canceled;
            CleanUp();
            phase = completed ? BeltPhase.Done : BeltPhase.Idle;
            if (completed && !completedRaised)
            {
                completedRaised = true;
                Completed?.Invoke();
            }
        }

        void Update()
        {
            if (timeSource is UnityEngine.Object unityObject && unityObject == null)
                timeSource = null;

            Tick(timeSource != null ? timeSource.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO (zero na pausa). Testes chamam direto.</summary>
        public void Tick(float dt)
        {
            if (!running)
                return;

            if (pendingFantasy && director != null && director.IsPlaying)
            {
                pendingFantasy = false;
                StartFantasy();
            }

            if (pendingRecord && director != null && director.IsPlaying)
            {
                pendingRecord = false;
                StartRecord();
            }

            if (dt > 0f)
            {
                if (phase == BeltPhase.Fantasy)
                    TickFantasy(dt);
                else if (recordOpen)
                    TickRecord(dt);

                deformTimer = Mathf.Max(0f, deformTimer - dt);
                glowTimer = Mathf.Max(0f, glowTimer - dt);
            }

            if (sensorGlow != null)
                sensorGlow.gameObject.SetActive(glowTimer > 0f);
            PushView();
        }

        // ---------- Fantasia ----------

        void StartFantasy()
        {
            phase = BeltPhase.Fantasy;
            fantasyClock = 0f;
            probe = new Vector2(0.22f, 0.5f);
            for (var i = 0; i < rocks.Length; i++)
            {
                rocks[i] = RockStart[i];
                touching[i] = false;
            }

            ReleaseLease();
            inputLease = router != null ? router.Acquire(InputContext.Gameplay, "PIO.Belt.Fantasy") : null;
        }

        void TickFantasy(float dt)
        {
            fantasyClock += dt;
            var move = router != null ? router.Move : Vector2.zero;
            if (move.sqrMagnitude > 1f)
                move.Normalize();
            probe += new Vector2(move.x, -move.y) * (probeSpeed * dt); // y da arena cresce para baixo
            probe = new Vector2(Mathf.Clamp(probe.x, 0.05f, 0.95f), Mathf.Clamp(probe.y, 0.08f, 0.92f));

            for (var i = 0; i < rocks.Length; i++)
            {
                rocks[i] = RockStart[i] + RockVelocity[i] * fantasyClock;
                var delta = rocks[i] - probe;
                delta.x *= arenaAspect;
                var inside = delta.magnitude < (rockRadius + probeRadius) * arenaAspect * 0.62f;
                if (inside && !touching[i])
                {
                    contacts++;
                    deformTimer = 0.35f;
                    Contact?.Invoke();
                }

                touching[i] = inside;
            }
        }

        void EndFantasy()
        {
            if (phase == BeltPhase.Fantasy)
                ReleaseLease();
        }

        // ---------- Registros ----------

        void StartRecord()
        {
            phase = BeltPhase.Record;
            recordOpen = true;
            idleSeconds = 0f;
            helpLevel = 0;
            ReleaseLease();
            inputLease = router != null ? router.Acquire(InputContext.Panel, "PIO.Belt.Record") : null;
            if (interaction != null)
            {
                interaction.HoldStarted += OnHoldStarted;
                interaction.HoldProgress += OnHoldProgress;
                interaction.HoldCanceled += OnHoldCanceled;
                interaction.HoldCompleted += OnHoldCompleted;
                OfferCandidate();
            }
        }

        void OfferCandidate()
        {
            holdBase = acquiredSeconds;
            holdSpan = Mathf.Max(0.05f, TotalRecordSeconds - acquiredSeconds);
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = CandidateId,
                VerbKey = PromptKey,
                ObjectKey = PromptKey,
                Available = true,
                HoldSeconds = holdSpan,
                Priority = 10,
            });
        }

        void OnHoldStarted(string id)
        {
            if (id != CandidateId)
                return;

            holding = true;
            clickTarget = 0f; // E assume; o clique em andamento cede
            holdBase = acquiredSeconds;
        }

        void OnHoldProgress(string id, float progress)
        {
            if (id == CandidateId && holding)
                Acquire(holdBase + progress * holdSpan);
        }

        void OnHoldCanceled(string id)
        {
            if (id != CandidateId || !recordOpen)
                return;

            holding = false;
            OfferCandidate(); // soltar pausa sem apagar: o próximo hold cobre só o restante
        }

        void OnHoldCompleted(string id)
        {
            if (id != CandidateId)
                return;

            holding = false;
            Acquire(TotalRecordSeconds);
        }

        /// <summary>Clique no instrumento (alternativa ao E): adquire o registro em foco em 2 s, sem manter.</summary>
        public void ClickRecord()
        {
            if (!recordOpen || holding)
                return;

            // Alternativa ao E: um clique adquire o registro em foco (2 s, sem manter).
            clickTarget = Mathf.Min(TotalRecordSeconds, (RecordedCount + 1) * SecondsPerRecord);
            idleSeconds = 0f;
        }

        /// <summary>“Ajuda de alinhamento” (só depois de 12 s sem progresso): a aquisição segue sem manter E.</summary>
        public void RequestHelp()
        {
            if (!recordOpen || helpLevel < 2)
                return;

            assisted = true; // a ajuda aproxima, mas o jogador confirmou; sem avanço por inatividade
            idleSeconds = 0f;
        }

        void TickRecord(float dt)
        {
            var before = acquiredSeconds;
            if (!holding)
            {
                if (assisted)
                    Acquire(acquiredSeconds + dt);
                else if (clickTarget > acquiredSeconds)
                    Acquire(Mathf.Min(clickTarget, acquiredSeconds + dt));
            }

            if (acquiredSeconds > before + 1e-6f || holding)
                idleSeconds = 0f;
            else
                idleSeconds += dt;

            helpLevel = Mathf.Max(helpLevel, idleSeconds >= HelpOfferSeconds ? 2 : idleSeconds >= HelpPulseSeconds ? 1 : 0);
        }

        void Acquire(float seconds)
        {
            var previous = RecordedCount;
            acquiredSeconds = Mathf.Clamp(seconds, acquiredSeconds, TotalRecordSeconds);
            var now = RecordedCount;
            for (var n = previous + 1; n <= now; n++)
            {
                glowTimer = 0.6f;
                Recorded?.Invoke(n);
            }

            if (now >= RecordCount && recordOpen)
                FinishRecord();
        }

        void FinishRecord()
        {
            CloseRecord();
            if (gateWaiting && director != null && director.IsPlaying)
            {
                gateWaiting = false;
                director.Resume();
            }
        }

        void CloseRecord()
        {
            if (!recordOpen)
                return;

            recordOpen = false;
            holding = false;
            clickTarget = 0f;
            if (interaction != null)
            {
                interaction.HoldStarted -= OnHoldStarted;
                interaction.HoldProgress -= OnHoldProgress;
                interaction.HoldCanceled -= OnHoldCanceled;
                interaction.HoldCompleted -= OnHoldCompleted;
                interaction.ClearCandidate(CandidateId);
            }

            ReleaseLease();
        }

        // ---------- Saída ----------

        void ReleaseLease()
        {
            inputLease?.Dispose();
            inputLease = null;
        }

        void CleanUp()
        {
            CloseRecord();
            ReleaseLease();
            if (director != null)
            {
                director.EventRaised -= OnEventRaised;
                director.Finished -= OnFinished;
            }

            if (presentation != null)
                presentation.UnbindSequenceDirector();
            running = pendingFantasy = pendingRecord = gateWaiting = assisted = holding = false;
            phase = BeltPhase.Idle;
            acquiredSeconds = holdBase = clickTarget = idleSeconds = fantasyClock = deformTimer = glowTimer = 0f;
            helpLevel = 0;
            contacts = 0;
            if (sensorGlow != null)
                sensorGlow.gameObject.SetActive(false);
            PushView();
        }

        public BeltViewState ViewState()
        {
            var recorded = RecordedCount;
            return new BeltViewState
            {
                Phase = phase,
                SealVisible = phase == BeltPhase.Fantasy,
                Probe = probe,
                RockA = rocks[0],
                RockB = rocks[1],
                RockC = rocks[2],
                ProbeDeformed = deformTimer > 0f,
                ArrowDirection = Mathf.FloorToInt(fantasyClock / 5f) % 4,
                Recorded = recorded,
                Focus = recorded,
                FocusProgress = recorded >= RecordCount ? 1f : Mathf.Clamp01(acquiredSeconds / SecondsPerRecord - recorded),
                Acquiring = holding || assisted || clickTarget > acquiredSeconds,
                HelpLevel = helpLevel,
                Assisted = assisted,
            };
        }

        void PushView()
        {
            if (panel != null)
                panel.Render(ViewState(), resolveText);
        }

        /// <summary>Prévia de Editor: mostra uma fase com valores fixos, sem tocar a sequência.</summary>
        public void PreviewPhase(BeltPhase previewPhase, float fantasySeconds, Vector2 probePosition, float acquired, int help)
        {
            phase = previewPhase;
            fantasyClock = fantasySeconds;
            probe = probePosition;
            for (var i = 0; i < rocks.Length; i++)
                rocks[i] = RockStart[i] + RockVelocity[i] * fantasySeconds;
            acquiredSeconds = acquired;
            helpLevel = help;
            glowTimer = previewPhase == BeltPhase.Record && acquired > 0f ? 0.5f : 0f;
            if (sensorGlow != null)
                sensorGlow.gameObject.SetActive(glowTimer > 0f);
            PushView();
        }

        void OnDestroy()
        {
            CleanUp();
            if (panel != null)
            {
                panel.RecordClicked -= ClickRecord;
                panel.HelpClicked -= RequestHelp;
            }
        }
    }
}
