using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    public enum FarewellPhase
    {
        Idle = 0,
        /// <summary>0–36 s: a cena corre pelo relógio da sequência (antena, contorno, placa, direção de Aldebaran, a Pioneer se afastando).</summary>
        Playing = 1,
        /// <summary>36 s em diante: o memorial e as duas ações finais. Permanece até o jogador escolher; não depende do relógio.</summary>
        Memorial = 2,
        /// <summary>O jogador escolheu; o controlador do capítulo assume.</summary>
        Done = 3,
    }

    /// <summary>As duas ações finais do memorial. Nenhuma inicia outro capítulo.</summary>
    public enum FarewellChoice
    {
        None = -1,
        ReturnToArchive = 0,
        Replay = 1,
    }

    /// <summary>O que o painel desenha neste quadro (sem tipos de UI: a lógica é testável sem painel).</summary>
    public struct FarewellViewState
    {
        public FarewellPhase Phase;
        /// <summary>Opacidade do preto do memorial (0–1); sobe com o tempo ATIVO e fica em 1 com movimento reduzido.</summary>
        public float BlackAlpha;
        /// <summary>O texto "PIONEER 10 · 1972–2003 / Em viagem" está visível.</summary>
        public bool EndingVisible;
        /// <summary>Guia discreto "direção de Aldebaran" (não finge que a sonda já chegou).</summary>
        public bool GuideVisible;
        /// <summary>"Voltar ao arquivo" e "Repetir capítulo" visíveis (os 2 últimos segundos).</summary>
        public bool ChoicesVisible;
        /// <summary>Botão com o foco do teclado (0 = Voltar ao arquivo, 1 = Repetir capítulo).</summary>
        public int Focus;
    }

    /// <summary>
    /// PIO-012 · Estado PIO.Farewell (38 s): a comunicação acabou, mas a trajetória continua. Do match cut da antena
    /// terrestre ao contorno do refletor, a placa em close, a direção geral de Aldebaran (um ponto avermelhado distante que
    /// NÃO ilumina a nave), a Pioneer diminuindo e o memorial em preto com "PIONEER 10 · 1972–2003 / Em viagem".
    /// <para>
    /// Conclusão: <see cref="Completed"/> dispara UMA vez quando o memorial abre (36 s). Dali em diante o capítulo está
    /// concluído e a tela fica até o jogador escolher "Voltar ao arquivo" ou "Repetir capítulo" (setas + E/Enter, clique):
    /// nada inicia outro capítulo sozinho e nenhum relógio corre nessa tela (o preto sobe com o tempo ATIVO e só).
    /// </para>
    /// </summary>
    public sealed class PioneerFarewellActivity : MonoBehaviour
    {
        public const string StateId = "PIO.Farewell";
        public const string EventMatch = "PIO.Farewell.Match";
        /// <summary>Em 14 s: o plano aberto revela a direção de Aldebaran (a luz da placa já apagou).</summary>
        public const string EventGuide = "PIO.Farewell.Guide";
        public const string EventGuideEnd = "PIO.Farewell.GuideEnd";
        /// <summary>Em 30 s: preto com o texto do memorial.</summary>
        public const string EventMemorial = "PIO.Farewell.Memorial";
        /// <summary>Em 36 s: os dois últimos segundos oferecem as ações finais.</summary>
        public const string EventChoices = "PIO.Farewell.Choices";
        public const string CandidateId = "PIO.Farewell.Choose";
        public const string ReturnKey = "PIO.UI.return";
        public const string ReplayKey = "PIO.UI.replay";
        public const string EndingKey = "PIO.UI.ending";
        public const string GuideKey = "PIO.UI.aldebaran";
        /// <summary>O preto leva este tempo ativo para chegar a 1 (instantâneo com movimento reduzido).</summary>
        public const float BlackFadeSeconds = 1.5f;
        /// <summary>Segurar E por este tempo também ativa o botão em foco (alternativa ao toque).</summary>
        public const float ActivateHoldSeconds = 0.5f;
        /// <summary>Pressionar e soltar o E dentro deste tempo é um toque que ativa o botão em foco.</summary>
        public const float TapMaxSeconds = 0.35f;

        [SerializeField] SequenceDefinition sequence;
        [SerializeField] PioneerPresentation presentation;
        [SerializeField] FarewellPanelView panel;
        [SerializeField, Tooltip("Movimento reduzido: o preto aparece de uma vez, sem fade.")] bool reducedMotion;

        SequenceDirector director;
        IInteractionService interaction;
        InputRouter router;
        IActiveTimeSource timeSource;
        Func<string, string> resolveText;
        IDisposable inputLease;

        FarewellPhase phase;
        bool running, candidateOpen, holding, navigateBound, submitBound, guideVisible, memorialVisible, choicesVisible, completedRaised, choiceMade;
        int focus;
        float holdSeconds, blackAlpha;

        /// <summary>O capítulo concluiu (memorial aberto). UMA vez por execução; a tela de conclusão não conta como tempo ativo.</summary>
        public event Action Completed;
        /// <summary>O jogador escolheu "Voltar ao arquivo". O controlador (PIO-014/global) volta ao dossiê.</summary>
        public event Action ReturnToArchiveRequested;
        /// <summary>O jogador escolheu "Repetir capítulo". O controlador reinicia o capítulo.</summary>
        public event Action ReplayRequested;

        public SequenceDefinition Sequence => sequence;
        public FarewellPhase Phase => phase;
        public bool IsRunning => running;
        public bool IsMemorialOpen => phase == FarewellPhase.Memorial;
        public int Focus => focus;
        public FarewellChoice Choice { get; private set; } = FarewellChoice.None;
        public bool ReducedMotion => reducedMotion;
        public void SetReducedMotion(bool value) => reducedMotion = value;

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
                UnbindPanel();
                panel.ReturnClicked += OnReturnClicked;
                panel.ReplayClicked += OnReplayClicked;
            }
        }

        void OnReturnClicked() => Choose(FarewellChoice.ReturnToArchive);

        void OnReplayClicked() => Choose(FarewellChoice.Replay);

        void UnbindPanel()
        {
            if (panel == null)
                return;

            panel.ReturnClicked -= OnReturnClicked;
            panel.ReplayClicked -= OnReplayClicked;
        }

        public void Begin(bool skippable = false)
        {
            if (director == null)
                throw new InvalidOperationException("PioneerFarewellActivity sem SequenceDirector: chame Initialize.");

            CleanUp();
            completedRaised = false;
            running = true;
            phase = FarewellPhase.Playing;
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

        /// <summary>Cancela a sequência (reiniciar, trocar de estado) ou fecha o memorial. Zera tudo e solta as entradas.</summary>
        public void Cancel()
        {
            if (running && director != null && director.IsPlaying)
                director.Cancel();
            else
                CleanUp();
        }

        // ---------- Eventos da sequência ----------

        void OnEventRaised(string eventId)
        {
            switch (eventId)
            {
                case EventGuide:
                    guideVisible = true;
                    break;
                case EventGuideEnd:
                    guideVisible = false;
                    break;
                case EventMemorial:
                    guideVisible = false;
                    memorialVisible = true;
                    if (reducedMotion)
                        blackAlpha = 1f;
                    break;
                case EventChoices:
                    OpenChoices();
                    break;
            }

            PushView();
        }

        void OnFinished(SequenceResult result)
        {
            if (result.Reason == SequenceExitReason.Canceled)
            {
                CleanUp();
                return;
            }

            // Terminou (ou foi pulada): o memorial fica até a escolha. Garante que as ações existam mesmo se um marco foi pulado.
            if (director != null)
            {
                director.EventRaised -= OnEventRaised;
                director.Finished -= OnFinished;
            }

            if (presentation != null)
                presentation.UnbindSequenceDirector();
            memorialVisible = true;
            blackAlpha = 1f;
            OpenChoices();
            PushView();
        }

        /// <summary>Abre as duas ações finais e conclui o capítulo (UMA vez). Idempotente.</summary>
        void OpenChoices()
        {
            if (choicesVisible || choiceMade)
                return;

            choicesVisible = true;
            memorialVisible = true;
            phase = FarewellPhase.Memorial;
            focus = 0;
            OpenInput();
            if (!completedRaised)
            {
                completedRaised = true;
                Completed?.Invoke();
            }
        }

        // ---------- Tempo ativo ----------

        void Update()
        {
            if (timeSource is UnityEngine.Object unityObject && unityObject == null)
                timeSource = null;

            Tick(timeSource != null ? timeSource.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO (zero na pausa). Só o fade do preto e o toque do E usam isto. Testes chamam direto.</summary>
        public void Tick(float dt)
        {
            if (!running && phase != FarewellPhase.Memorial)
                return;

            if (dt > 0f)
            {
                if (holding)
                    holdSeconds += dt;
                if (memorialVisible && blackAlpha < 1f)
                    blackAlpha = reducedMotion ? 1f : Mathf.Min(1f, blackAlpha + dt / BlackFadeSeconds);
            }

            PushView();
        }

        // ---------- Ações finais ----------

        /// <summary>Move o foco entre "Voltar ao arquivo" (0) e "Repetir capítulo" (1). Só com as ações visíveis.</summary>
        public void MoveFocus(int step)
        {
            if (!choicesVisible || choiceMade || step == 0)
                return;

            focus = ((focus + (step > 0 ? 1 : -1)) % 2 + 2) % 2;
            UpdateCandidate();
            PushView();
        }

        /// <summary>Ativa o botão em foco (E, Enter ou clique). Idempotente: só a primeira escolha vale.</summary>
        public bool ActivateFocused() => Choose(focus == 0 ? FarewellChoice.ReturnToArchive : FarewellChoice.Replay);

        public bool Choose(FarewellChoice choice)
        {
            if (!choicesVisible || choiceMade || choice == FarewellChoice.None)
                return false;

            choiceMade = true;
            Choice = choice;
            phase = FarewellPhase.Done;
            CloseInput();
            PushView();
            if (choice == FarewellChoice.ReturnToArchive)
                ReturnToArchiveRequested?.Invoke();
            else
                ReplayRequested?.Invoke();
            return true;
        }

        void OnNavigate(Vector2Int direction)
        {
            var step = direction.x > 0 || direction.y < 0 ? 1 : direction.x < 0 || direction.y > 0 ? -1 : 0;
            MoveFocus(step);
        }

        void OnSubmit() => ActivateFocused();

        // ---------- Entrada ----------

        void OpenInput()
        {
            if (inputLease == null && router != null)
                inputLease = router.Acquire(InputContext.Panel, "PIO.Farewell");
            if (router != null && !navigateBound)
            {
                navigateBound = true;
                router.NavigateDown += OnNavigate;
            }

            if (router != null && !submitBound)
            {
                submitBound = true;
                router.SubmitDown += OnSubmit;
            }

            if (interaction == null || candidateOpen)
                return;

            candidateOpen = true;
            interaction.HoldStarted += OnHoldStarted;
            interaction.HoldCanceled += OnHoldCanceled;
            interaction.HoldCompleted += OnHoldCompleted;
            UpdateCandidate();
        }

        /// <summary>O candidato (e o prompt do HUD) sempre fala do botão em foco: nunca mostra uma ação e executa outra.</summary>
        void UpdateCandidate()
        {
            if (!candidateOpen || interaction == null)
                return;

            var key = focus == 0 ? ReturnKey : ReplayKey;
            interaction.SetCandidate(new InteractionCandidate
            {
                Id = CandidateId,
                VerbKey = key,
                ObjectKey = key,
                Available = true,
                HoldSeconds = ActivateHoldSeconds,
                Priority = 10,
            });
        }

        void OnHoldStarted(string id)
        {
            if (id != CandidateId)
                return;

            holding = true;
            holdSeconds = 0f;
        }

        void OnHoldCanceled(string id)
        {
            if (id != CandidateId)
                return;

            var wasTap = holding && holdSeconds <= TapMaxSeconds;
            holding = false;
            if (wasTap)
                ActivateFocused();
        }

        void OnHoldCompleted(string id)
        {
            if (id != CandidateId)
                return;

            holding = false;
            ActivateFocused();
        }

        void CloseInput()
        {
            holding = false;
            if (navigateBound)
            {
                navigateBound = false;
                if (router != null)
                    router.NavigateDown -= OnNavigate;
            }

            if (submitBound)
            {
                submitBound = false;
                if (router != null)
                    router.SubmitDown -= OnSubmit;
            }

            if (candidateOpen)
            {
                candidateOpen = false;
                if (interaction != null)
                {
                    interaction.HoldStarted -= OnHoldStarted;
                    interaction.HoldCanceled -= OnHoldCanceled;
                    interaction.HoldCompleted -= OnHoldCompleted;
                    interaction.ClearCandidate(CandidateId);
                }
            }

            inputLease?.Dispose();
            inputLease = null;
        }

        // ---------- Saída ----------

        void CleanUp()
        {
            CloseInput();
            if (director != null)
            {
                director.EventRaised -= OnEventRaised;
                director.Finished -= OnFinished;
            }

            if (presentation != null)
                presentation.UnbindSequenceDirector();
            running = guideVisible = memorialVisible = choicesVisible = choiceMade = false;
            phase = FarewellPhase.Idle;
            Choice = FarewellChoice.None;
            focus = 0;
            holdSeconds = blackAlpha = 0f;
            PushView();
        }

        public FarewellViewState ViewState()
        {
            return new FarewellViewState
            {
                Phase = phase,
                BlackAlpha = memorialVisible ? blackAlpha : 0f,
                EndingVisible = memorialVisible && blackAlpha >= 1f,
                GuideVisible = guideVisible,
                ChoicesVisible = choicesVisible && !choiceMade,
                Focus = focus,
            };
        }

        /// <summary>
        /// Erros de montagem (lista vazia = conforme): referências, sequência de 38 s com os cinco marcos, as quatro legendas por
        /// janela, planos com rigs do palco que nunca se repetem em sequência, e o painel ligado ao UXML.
        /// </summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = "PioneerFarewellActivity";
            if (sequence == null)
                errors.Add($"{owner}: sequence não atribuída.");
            else
            {
                errors.AddRange(sequence.Validate().ConvertAll(e => $"{owner}: {e}"));
                if (Mathf.Abs(sequence.TotalDurationSeconds - 38f) > 0.01f)
                    errors.Add($"{owner}: a sequência dura {sequence.TotalDurationSeconds} s; o roteiro pede 38 s.");

                var events = new HashSet<string>();
                string previousRig = null;
                foreach (var shot in sequence.Shots)
                {
                    if (shot.Events != null)
                    {
                        foreach (var shotEvent in shot.Events)
                            events.Add(shotEvent.eventId);
                    }

                    if (shot.RigId == previousRig && !shot.ContinueFromPreviousShot)
                        errors.Add($"{owner}: o plano {shot.ShotId} repete o rig '{shot.RigId}' sem continuidade (não repetir o mesmo zoom).");
                    previousRig = shot.RigId;
                }

                foreach (var required in new[] { EventMatch, EventGuide, EventGuideEnd, EventMemorial, EventChoices })
                {
                    if (!events.Contains(required))
                        errors.Add($"{owner}: a sequência não dispara o evento '{required}'.");
                }

                var keys = new HashSet<string>();
                foreach (var cue in sequence.Cues)
                    keys.Add(cue.TextKey);
                for (var i = 1; i <= 4; i++)
                {
                    if (!keys.Contains("PIO.08.0" + i))
                        errors.Add($"{owner}: falta a legenda PIO.08.0{i}.");
                }
            }

            if (presentation == null)
                errors.Add($"{owner}: presentation (palco da PIO-003) não atribuída.");
            else if (!presentation.TryGetAnchor(StateId, out _))
                errors.Add($"{owner}: o palco não tem o anchor {StateId}.");

            if (panel == null)
                errors.Add($"{owner}: panel (FarewellPanelView) não atribuído.");
            else
                errors.AddRange(panel.ValidateSetup());

            return errors;
        }

        void PushView()
        {
            if (panel != null)
                panel.Render(ViewState(), resolveText);
        }

        /// <summary>Prévia de Editor: mostra uma situação com valores fixos, sem tocar a sequência.</summary>
        public void PreviewState(FarewellViewState state)
        {
            if (panel != null)
                panel.Render(state, resolveText);
        }

        void OnDestroy()
        {
            CleanUp();
            UnbindPanel();
        }
    }
}
