using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Prompt E compartilhado (UI-003): tecla + verbo + objeto + progresso. Apresenta o candidato e a disponibilidade
    /// que o <see cref="IInteractionService"/> informa e NÃO recalcula alcance: o E só aparece quando
    /// <c>CanBeginHold</c> é verdadeiro, a mesma condição que a execução usa. As regras de estado estão em
    /// <see cref="PromptModel"/>; esta classe só liga os eventos do serviço e desenha o <see cref="PromptView"/>.
    /// </summary>
    public sealed class InteractionPrompt : IDisposable
    {
        readonly IInteractionService interactions;
        readonly PromptModel model = new PromptModel();
        readonly VisualElement plate;
        readonly Label label;
        readonly Label status;
        readonly VisualElement fill;

        bool disposed;
        bool layerVisible = true;
        bool shown;
        bool shownKnown;
        string renderedClass;
        string renderedLabel;
        string renderedStatus;
        int renderedPercent = -1;
        InputSignature signature;
        bool signatureKnown;

        /// <summary>Tudo que decide o texto do prompt. Igual à do quadro anterior = nada a reconstruir (sem alocação por quadro).</summary>
        struct InputSignature
        {
            public PromptState State;
            public string CandidateId;
            public string VerbKey;
            public string ObjectKey;
            public bool NeedsHold;
            public int Percent;
            public HudCommandStage Stage;
            public int SecondsTenths;
            public string Language;
            public HoldMode Mode;
            public int TextCount;

            public bool Equals(in InputSignature other) =>
                State == other.State && CandidateId == other.CandidateId && VerbKey == other.VerbKey && ObjectKey == other.ObjectKey
                && NeedsHold == other.NeedsHold && Percent == other.Percent && Stage == other.Stage && SecondsTenths == other.SecondsTenths
                && Language == other.Language && Mode == other.Mode && TextCount == other.TextCount;
        }

        public InteractionPrompt(VisualElement root, IInteractionService interactionService)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            interactions = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
            plate = Require<VisualElement>(root, "prompt");
            label = Require<Label>(root, "prompt-label");
            status = Require<Label>(root, "prompt-status");
            fill = Require<VisualElement>(root, "prompt-fill");

            interactions.HoldStarted += model.OnHoldStarted;
            interactions.HoldProgress += model.OnHoldProgress;
            interactions.HoldCompleted += model.OnHoldCompleted;
            interactions.HoldCanceled += model.OnHoldCanceled;
            SetShown(false);
        }

        /// <summary>Modelo de estado (progresso físico, comando em trânsito, reinício entre capítulos).</summary>
        public PromptModel Model => model;

        public PromptState State => model.State;

        /// <summary>O prompt está desenhado agora (camada visível e estado diferente de oculto).</summary>
        public bool IsShown => shown;

        public PromptView View { get; private set; } = PromptView.Hidden;

        /// <summary>Esconde a camada inteira (pausa, cinemática) sem perder o estado do modelo.</summary>
        public void SetLayerVisible(bool visible) => layerVisible = visible;

        /// <summary>
        /// Atualiza a partir do serviço. Chamar a cada quadro com o tempo REAL (<c>Time.unscaledDeltaTime</c>): o feedback não congela.
        /// Só escreve na interface quando algo visível muda (sem alocação por quadro).
        /// </summary>
        public void Tick(float unscaledDeltaSeconds, DossierTextCatalog texts, string language, HoldMode mode)
        {
            if (disposed)
                return;

            var snapshot = new PromptSnapshot { HoldingId = interactions.HoldingId, AwaitingRelease = interactions.AwaitingRelease };
            if (interactions.TryGetActive(out var active))
            {
                snapshot.HasCandidate = true;
                snapshot.CandidateId = active.Id;
                snapshot.VerbKey = active.VerbKey;
                snapshot.ObjectKey = active.ObjectKey;
                snapshot.HoldSeconds = active.HoldSeconds;
                snapshot.CanBegin = interactions.CanBeginHold(active.Id);
            }

            model.Refresh(snapshot, unscaledDeltaSeconds);

            var current = new InputSignature
            {
                State = model.State, CandidateId = model.CandidateId, VerbKey = model.VerbKey, ObjectKey = model.ObjectKey, NeedsHold = model.NeedsHold,
                Percent = HudFormat.Percent(model.Progress), Stage = model.Command.Stage,
                SecondsTenths = (int)(model.Command.RemainingSeconds * 10f), Language = language, Mode = mode, TextCount = texts != null ? texts.Count : 0,
            };

            if (signatureKnown && signature.Equals(in current))
                return;

            signatureKnown = true;
            signature = current;
            Render(PromptViewBuilder.Build(model, texts, language, mode));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            interactions.HoldStarted -= model.OnHoldStarted;
            interactions.HoldProgress -= model.OnHoldProgress;
            interactions.HoldCompleted -= model.OnHoldCompleted;
            interactions.HoldCanceled -= model.OnHoldCanceled;
        }

        void Render(in PromptView view)
        {
            View = view;
            var visible = layerVisible && view.Visible;
            SetShown(visible);
            if (!view.Visible)
                return;

            if (view.StateClass != renderedClass)
            {
                foreach (var stateClass in PromptViewBuilder.AllStateClasses)
                    plate.RemoveFromClassList(stateClass);

                plate.AddToClassList(view.StateClass);
                renderedClass = view.StateClass;
            }

            if (view.Label != renderedLabel)
            {
                label.text = view.Label;
                renderedLabel = view.Label;
            }

            if (view.Status != renderedStatus)
            {
                status.text = view.Status;
                renderedStatus = view.Status;
            }

            var percent = HudFormat.Percent(view.Progress);
            if (percent != renderedPercent)
            {
                fill.style.width = new Length(percent, LengthUnit.Percent);
                renderedPercent = percent;
            }
        }

        void SetShown(bool visible)
        {
            if (shownKnown && shown == visible)
                return;

            shownKnown = true;
            shown = visible;
            plate.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            var element = root.Q<T>(name);
            if (element == null)
                throw new InvalidOperationException($"InteractionPrompt: o UXML não tem o elemento '{name}' ({typeof(T).Name}).");

            return element;
        }
    }
}
