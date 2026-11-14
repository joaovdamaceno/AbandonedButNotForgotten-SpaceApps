using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Quem recebe a entrada agora. O contexto efetivo é o do último "lease" ativo; sem lease vale o padrão
    /// (Panel), de modo que a entrada de gameplay só existe quando alguém a pede explicitamente.
    /// </summary>
    public enum InputContext
    {
        /// <summary>Dossiê, painéis e menus: navegação, confirmação e E. Sem movimento.</summary>
        Panel = 0,

        /// <summary>Jogo: movimento, olhar, E e ajuda (H).</summary>
        Gameplay = 1,

        /// <summary>Sequência cinematográfica: só pausa e confirmação (pular, quando o roteiro permite).</summary>
        Cinematic = 2,

        /// <summary>Pausa: só pausa (retomar), navegação e confirmação.</summary>
        Paused = 3,
    }

    /// <summary>
    /// ÚNICO leitor de teclado/mouse do jogo (Input System): WASD/setas, mouse, E, H, Enter e P/Esc. Instrumentos,
    /// UI e capítulos consomem estes eventos e nunca leem a tecla direto.
    /// <para>
    /// Regras: (1) o contexto decide o que é entregue; (2) ao trocar de contexto, perder foco ou desabilitar, o
    /// estado é limpo e um E ainda pressionado só volta a valer depois de soltar (sem vazamento de comandos entre
    /// painel, cinematic, pausa e retomada); (3) os eventos são entregues uma vez por mudança de estado.
    /// </para>
    /// Movimento: depois de uma troca de contexto o vetor fica zerado até a próxima mudança de tecla (uma tecla de
    /// direção mantida durante a pausa não move o jogador ao retomar).
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        [SerializeField, Tooltip("Contexto quando ninguém pediu outro. Panel = só interface, o mais seguro.")]
        InputContext defaultContext = InputContext.Panel;

        readonly List<ContextLease> leases = new List<ContextLease>();

        InputAction moveAction;
        InputAction lookAction;
        InputAction interactAction;
        InputAction pauseAction;
        InputAction helpAction;
        InputAction submitAction;
        InputAction navigateAction;

        bool actionsEnabled;
        Vector2 moveValue;
        Vector2 lookValue;
        bool physicalInteract;
        bool logicalInteract;
        bool interactSuppressed;
        InputContext lastContext;

        /// <summary>Eixos de movimento (WASD/setas), normalizados. Zero fora do contexto Gameplay.</summary>
        public Vector2 Move => moveValue;

        /// <summary>Delta do mouse deste quadro. Opcional: nenhum objetivo exige câmera manual.</summary>
        public Vector2 Look => lookValue;

        /// <summary>E logicamente mantido: pressionado, entregue ao contexto atual e não suprimido pelo rearme.</summary>
        public bool InteractHeld => logicalInteract;

        /// <summary>A tecla E está fisicamente pressionada, independentemente de contexto ou supressão.</summary>
        public bool IsInteractPhysicallyHeld => physicalInteract;

        public InputContext Context => leases.Count > 0 ? leases[leases.Count - 1].Context : defaultContext;
        public bool AllowsInteract => Allows(Context, Capability.Interact);

        /// <summary>E foi pressionado agora e o contexto o aceita. Nunca repete enquanto a tecla é mantida.</summary>
        public event Action InteractDown;

        /// <summary>E deixou de valer: soltou, trocou de contexto, perdeu foco ou o router foi desabilitado.</summary>
        public event Action InteractUp;

        public event Action PauseDown;
        public event Action HelpDown;
        public event Action SubmitDown;

        /// <summary>Navegação discreta em painéis e menus (-1/0/1 por eixo).</summary>
        public event Action<Vector2Int> NavigateDown;

        /// <summary>O contexto efetivo mudou; o estado de entrada já foi limpo quando o evento dispara.</summary>
        public event Action<InputContext> ContextChanged;

        /// <summary>Foco perdido ou router desabilitado: tudo foi solto. Quem mantém hold ou ferramenta deve parar.</summary>
        public event Action InputCleared;

        enum Capability
        {
            Move,
            Look,
            Interact,
            Help,
            Navigate,
            Submit,
        }

        static bool Allows(InputContext context, Capability capability)
        {
            switch (capability)
            {
                case Capability.Move:
                case Capability.Look:
                case Capability.Help:
                    return context == InputContext.Gameplay;
                case Capability.Interact:
                    return context == InputContext.Gameplay || context == InputContext.Panel;
                case Capability.Navigate:
                    return context == InputContext.Panel || context == InputContext.Paused;
                case Capability.Submit:
                    return context != InputContext.Gameplay;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Pede um contexto até o descarte do objeto devolvido. Vale o último pedido ativo; descartar fora de
        /// ordem ou duas vezes é seguro. O dono usa <c>using</c>/<c>Dispose</c> na saída VÁLIDA do estado.
        /// </summary>
        public IDisposable Acquire(InputContext context, string ownerLabel)
        {
            if (string.IsNullOrWhiteSpace(ownerLabel))
                throw new ArgumentException("Informe quem pede o contexto (ownerLabel) para diagnóstico.", nameof(ownerLabel));

            var lease = new ContextLease(this, context, ownerLabel);
            leases.Add(lease);
            RefreshContext();
            return lease;
        }

        /// <summary>Quem segura o contexto agora (do mais antigo ao mais recente), para diagnóstico.</summary>
        public IReadOnlyList<string> DescribeLeases()
        {
            var result = new List<string>(leases.Count);
            foreach (var lease in leases)
                result.Add($"{lease.Context}:{lease.Owner}");
            return result;
        }

        void Release(ContextLease lease)
        {
            if (leases.Remove(lease))
                RefreshContext();
        }

        void OnEnable() => Enable();

        void OnDisable() => Disable();

        void LateUpdate() => EndFrame();

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                NotifyFocusLost();
        }

        /// <summary>Cria e habilita as ações. Idempotente.</summary>
        public void Enable()
        {
            if (actionsEnabled)
                return;

            BuildActions();
            actionsEnabled = true;
            lastContext = Context;
        }

        /// <summary>Desabilita as ações e solta tudo. Idempotente.</summary>
        public void Disable()
        {
            if (!actionsEnabled)
                return;

            actionsEnabled = false;
            DisposeActions();
            ClearState();
        }

        /// <summary>Foco da aba/janela perdido: solta tudo e não deixa tecla ou ferramenta ligada na volta.</summary>
        public void NotifyFocusLost() => ClearState();

        void ClearState()
        {
            moveValue = Vector2.zero;
            lookValue = Vector2.zero;
            physicalInteract = false;
            interactSuppressed = false;
            ReleaseLogicalInteract();
            InputCleared?.Invoke();
        }

        void RefreshContext()
        {
            var context = Context;
            if (context == lastContext)
                return;

            lastContext = context;
            moveValue = Vector2.zero;
            lookValue = Vector2.zero;
            ReleaseLogicalInteract();

            // Uma tecla E já pressionada não pode iniciar nada no novo contexto: exige soltar e pressionar de novo.
            interactSuppressed = physicalInteract;
            ContextChanged?.Invoke(context);
        }

        void ReleaseLogicalInteract()
        {
            if (!logicalInteract)
                return;

            logicalInteract = false;
            InteractUp?.Invoke();
        }

        internal void EndFrame() => lookValue = Vector2.zero;

        // ---- Pontos de entrada (ações do Input System e testes) ----

        internal void FeedMove(Vector2 value) =>
            moveValue = Allows(Context, Capability.Move) ? value : Vector2.zero;

        internal void FeedLook(Vector2 delta)
        {
            if (Allows(Context, Capability.Look))
                lookValue += delta;
        }

        internal void FeedInteract(bool pressed)
        {
            if (pressed)
            {
                if (physicalInteract)
                    return;

                physicalInteract = true;
                if (interactSuppressed || !Allows(Context, Capability.Interact))
                    return;

                logicalInteract = true;
                InteractDown?.Invoke();
                return;
            }

            if (!physicalInteract)
                return;

            physicalInteract = false;
            interactSuppressed = false;
            ReleaseLogicalInteract();
        }

        internal void FeedPause() => PauseDown?.Invoke();

        internal void FeedHelp()
        {
            if (Allows(Context, Capability.Help))
                HelpDown?.Invoke();
        }

        internal void FeedSubmit()
        {
            if (Allows(Context, Capability.Submit))
                SubmitDown?.Invoke();
        }

        internal void FeedNavigate(Vector2 value)
        {
            var direction = new Vector2Int(Axis(value.x), Axis(value.y));
            if (direction == Vector2Int.zero || !Allows(Context, Capability.Navigate))
                return;

            NavigateDown?.Invoke(direction);
        }

        static int Axis(float value) => value > 0.5f ? 1 : value < -0.5f ? -1 : 0;

        // ---- Ações do Input System ----

        void BuildActions()
        {
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            AddDirectionBindings(moveAction);
            moveAction.performed += ctx => FeedMove(ctx.ReadValue<Vector2>());
            moveAction.canceled += _ => FeedMove(Vector2.zero);

            lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            lookAction.performed += ctx => FeedLook(ctx.ReadValue<Vector2>());

            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.performed += _ => FeedInteract(true);
            interactAction.canceled += _ => FeedInteract(false);

            pauseAction = new InputAction("Pause", InputActionType.Button);
            pauseAction.AddBinding("<Keyboard>/p");
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.performed += _ => FeedPause();

            helpAction = new InputAction("Help", InputActionType.Button, "<Keyboard>/h");
            helpAction.performed += _ => FeedHelp();

            submitAction = new InputAction("Submit", InputActionType.Button);
            submitAction.AddBinding("<Keyboard>/enter");
            submitAction.AddBinding("<Keyboard>/numpadEnter");
            submitAction.performed += _ => FeedSubmit();

            navigateAction = new InputAction("Navigate", InputActionType.Value, expectedControlType: "Vector2");
            AddDirectionBindings(navigateAction);
            navigateAction.performed += ctx => FeedNavigate(ctx.ReadValue<Vector2>());

            foreach (var action in AllActions())
                action.Enable();
        }

        static void AddDirectionBindings(InputAction action)
        {
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
        }

        IEnumerable<InputAction> AllActions()
        {
            yield return moveAction;
            yield return lookAction;
            yield return interactAction;
            yield return pauseAction;
            yield return helpAction;
            yield return submitAction;
            yield return navigateAction;
        }

        void DisposeActions()
        {
            foreach (var action in AllActions())
            {
                if (action == null)
                    continue;

                action.Disable();
                action.Dispose();
            }

            moveAction = lookAction = interactAction = pauseAction = helpAction = submitAction = navigateAction = null;
        }

        sealed class ContextLease : IDisposable
        {
            readonly InputRouter router;

            public ContextLease(InputRouter router, InputContext context, string owner)
            {
                this.router = router;
                Context = context;
                Owner = owner;
            }

            public InputContext Context { get; }
            public string Owner { get; }

            public void Dispose() => router.Release(this);
        }
    }
}
