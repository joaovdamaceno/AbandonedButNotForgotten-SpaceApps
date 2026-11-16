using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// PIO-009 · Painel UI Toolkit da placa (<c>UI/PlaquePanel.uxml</c>): o quadro 2:3 com cinco contornos e a bandeja com as
    /// cinco peças. Só desenha o <see cref="PlaqueViewState"/> e devolve as ações do mouse: clique numa peça, clique num
    /// contorno e arrastar-e-soltar (o contorno mais próximo a até <see cref="SnapRadiusPixels"/> px lógicos, medidos a 720p
    /// porque a UI escala pela altura). Textos vêm de chaves resolvidas pelo capítulo; a arte são cinco PNGs gerados.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlaquePanelView : MonoBehaviour
    {
        public const string PlaqueKey = "PIO.UI.plaque";
        public const string SnapKey = "PIO.UI.snap";
        public const string RetryKey = "PIO.UI.plaque_retry";
        public const float SnapRadiusPixels = PioneerPlaqueActivity.SnapRadiusPixels;
        const int Pieces = PioneerPlaqueActivity.PieceCount;
        /// <summary>Movimento mínimo do ponteiro (px) para um toque virar arrastar.</summary>
        const float DragStartPixels = 6f;

        /// <summary>Chaves dos nomes, na ordem das peças (a UI mostra símbolo E nome: nada de decifrar código).</summary>
        public static readonly string[] NameKeys = { "PIO.UI.hydrogen", "PIO.UI.pulsars", "PIO.UI.solar", "PIO.UI.scale", "PIO.UI.people" };

        static readonly string[] HoldClasses =
        {
            "sa-hold--available", "sa-hold--rearm", "sa-hold--holding", "sa-hold--paused", "sa-hold--done",
        };

        [SerializeField] UIDocument document;

        VisualElement root, hold, progressFill, assistTrack, assistFill, retryBox;
        VisualElement[] slots, cards;
        Label plaqueLabel, snapLabel, retryLabel;
        readonly Label[] slotNames = new Label[Pieces];
        readonly Label[] cardNames = new Label[Pieces];
        string lastPlaque, lastSnap, lastRetry;
        readonly string[] lastSlotNames = new string[Pieces];
        readonly string[] lastCardNames = new string[Pieces];
        int lastPlaced = -1;
        PlaqueHoldState lastHold = (PlaqueHoldState)(-1);
        PlaquePhase lastPhase = (PlaquePhase)(-1);
        int dragPiece = -1;
        bool dragging;
        Vector2 dragStart;
        int dragPointer;
        PlaqueViewState lastState;

        public event Action<int> PieceClicked;
        public event Action<int> OutlineClicked;
        /// <summary>Soltou um arrastar: peça e contorno a ≤ 96 px (ou -1 se solto longe de todos).</summary>
        public event Action<int, int> PieceDropped;

        public bool IsBound => root != null;
        public VisualElement Root => root;

        void OnEnable() => Bind();

        void OnDisable()
        {
            root = null;
        }

        /// <summary>Liga os elementos do UXML. Idempotente; chamado no OnEnable e pela prévia de Editor.</summary>
        public void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("plaque-root") == null)
                return;
            if (root == tree.Q("plaque-root"))
                return;

            root = tree.Q("plaque-root");
            hold = root.Q("hold");
            progressFill = root.Q("progress-fill");
            assistTrack = root.Q("assist");
            assistFill = root.Q("assist-fill");
            retryBox = root.Q("retry");
            plaqueLabel = root.Q<Label>("plaque-label");
            snapLabel = root.Q<Label>("snap-label");
            retryLabel = root.Q<Label>("retry-label");
            slots = new VisualElement[Pieces];
            cards = new VisualElement[Pieces];
            for (var i = 0; i < Pieces; i++)
            {
                slots[i] = root.Q("slot-" + i);
                cards[i] = root.Q("card-" + i);
                slotNames[i] = root.Q<Label>("slot-name-" + i);
                cardNames[i] = root.Q<Label>("card-name-" + i);
                var piece = i;
                if (slots[i] != null)
                    slots[i].RegisterCallback<PointerUpEvent>(_ => { if (!dragging) OutlineClicked?.Invoke(piece); });
                if (cards[i] != null)
                {
                    cards[i].RegisterCallback<PointerDownEvent>(e => OnCardDown(e, piece));
                    cards[i].RegisterCallback<PointerMoveEvent>(e => OnCardMove(e, piece));
                    cards[i].RegisterCallback<PointerUpEvent>(e => OnCardUp(e, piece));
                    cards[i].RegisterCallback<PointerCaptureOutEvent>(_ => CancelDrag(piece));
                }
            }

            lastPlaque = lastSnap = lastRetry = null;
            for (var i = 0; i < Pieces; i++)
                lastSlotNames[i] = lastCardNames[i] = null;
            lastPlaced = -1;
            lastHold = (PlaqueHoldState)(-1);
            lastPhase = (PlaquePhase)(-1);
        }

        // ---------- Arrastar e soltar ----------

        /// <summary>
        /// O contorno livre mais próximo de <paramref name="point"/> a até <paramref name="snapRadius"/> px, ou -1.
        /// Função pura (testável sem painel): os centros e o ponto estão no mesmo espaço lógico de 720p.
        /// </summary>
        public static int NearestOutline(Vector2 point, IReadOnlyList<Vector2> centers, int placedBits, float snapRadius)
        {
            var best = -1;
            var bestDistance = snapRadius;
            for (var i = 0; i < centers.Count; i++)
            {
                if ((placedBits & (1 << i)) != 0)
                    continue;

                var distance = Vector2.Distance(point, centers[i]);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        bool Interactive => lastState.Phase == PlaquePhase.Fit;

        void OnCardDown(PointerDownEvent e, int piece)
        {
            if (!Interactive || (lastState.PlacedBits & (1 << piece)) != 0)
                return;

            dragPiece = piece;
            dragging = false;
            dragStart = e.position;
            dragPointer = e.pointerId;
            cards[piece].CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnCardMove(PointerMoveEvent e, int piece)
        {
            if (dragPiece != piece)
                return;

            var delta = (Vector2)e.position - dragStart;
            if (!dragging && delta.magnitude > DragStartPixels)
            {
                dragging = true;
                cards[piece].AddToClassList("pio-plaque__card--dragging");
                PieceClicked?.Invoke(piece); // arrastar também seleciona: o contorno certo já pulsa
            }

            if (dragging)
                cards[piece].style.translate = new Translate(delta.x, delta.y);
        }

        void OnCardUp(PointerUpEvent e, int piece)
        {
            if (dragPiece != piece)
                return;

            var wasDragging = dragging;
            var drop = wasDragging ? cards[piece].worldBound.center : Vector2.zero;
            EndDrag(piece);
            if (!wasDragging)
            {
                PieceClicked?.Invoke(piece); // clique simples: seleciona (ou solta a seleção)
                return;
            }

            var centers = new Vector2[Pieces];
            for (var i = 0; i < Pieces; i++)
                centers[i] = slots[i].worldBound.center;
            PieceDropped?.Invoke(piece, NearestOutline(drop, centers, lastState.PlacedBits, SnapRadiusPixels));
        }

        void CancelDrag(int piece)
        {
            if (dragPiece == piece)
                EndDrag(piece);
        }

        void EndDrag(int piece)
        {
            if (cards[piece].HasPointerCapture(dragPointer))
                cards[piece].ReleasePointer(dragPointer);
            cards[piece].RemoveFromClassList("pio-plaque__card--dragging");
            cards[piece].style.translate = new Translate(0f, 0f); // a peça volta à bandeja (ou some, se encaixou)
            dragPiece = -1;
            dragging = false;
        }

        // ---------- Desenho ----------

        public void Render(PlaqueViewState state, Func<string, string> text)
        {
            if (root == null)
                Bind();
            if (root == null)
                return;

            lastState = state;
            if (state.Phase != lastPhase)
            {
                lastPhase = state.Phase;
                var visible = state.Phase == PlaquePhase.Fit || state.Phase == PlaquePhase.Complete;
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                root.EnableInClassList("pio-plaque--complete", state.Phase == PlaquePhase.Complete);
            }

            for (var i = 0; i < Pieces; i++)
            {
                var placed = (state.PlacedBits & (1 << i)) != 0;
                var slot = slots[i];
                slot.EnableInClassList("pio-plaque__slot--placed", placed);
                slot.EnableInClassList("pio-plaque__slot--hint", !placed && state.HintOutline == i);
                slot.EnableInClassList("pio-plaque__slot--pulse", !placed && state.HintOutline == i && state.HintPulseOn);
                slot.EnableInClassList("pio-plaque__slot--focus", !placed && state.FocusOutline == i);
                slot.EnableInClassList("pio-plaque__slot--retry", !placed && state.RetryVisible && state.RetryOutline == i);
                var card = cards[i];
                card.style.visibility = placed ? Visibility.Hidden : Visibility.Visible; // o lugar na bandeja não se mexe
                card.EnableInClassList("pio-plaque__card--selected", state.Selected == i);
                card.EnableInClassList("pio-plaque__card--focus", !placed && state.FocusPiece == i && state.Selected < 0);
                SetText(slotNames[i], NameKeys[i], text, ref lastSlotNames[i]);
                SetText(cardNames[i], NameKeys[i], text, ref lastCardNames[i]);
            }

            if (state.Placed != lastPlaced)
            {
                lastPlaced = state.Placed;
                progressFill.style.width = Length.Percent(state.Placed * 100f / Pieces);
                SetText(plaqueLabel, PlaqueKey, text, ref lastPlaque, state.Placed);
            }

            plaqueLabel.style.display = state.Cinema ? DisplayStyle.None : DisplayStyle.Flex;
            snapLabel.style.display = state.Cinema ? DisplayStyle.None : DisplayStyle.Flex;
            SetText(snapLabel, SnapKey, text, ref lastSnap);
            retryBox.style.display = state.RetryVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (state.RetryVisible)
                SetText(retryLabel, RetryKey, text, ref lastRetry);
            assistTrack.style.display = state.AssistProgress > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            assistFill.style.width = Length.Percent(state.AssistProgress * 100f);
            SetHoldState(state.Hold);
        }

        void SetHoldState(PlaqueHoldState state)
        {
            if (state == lastHold)
                return;

            lastHold = state;
            for (var i = 0; i < HoldClasses.Length; i++)
                hold.EnableInClassList(HoldClasses[i], i == (int)state);
        }

        /// <summary>
        /// Erros de montagem (lista vazia = conforme): UIDocument com UXML, <see cref="ResponsiveLayoutClass"/> e todos os
        /// elementos que o painel usa. O UXML é clonado, então funciona fora do Play Mode.
        /// </summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = "PlaquePanelView";
            var doc = document != null ? document : GetComponent<UIDocument>();
            if (doc == null)
            {
                errors.Add($"{owner}: sem UIDocument.");
                return errors;
            }

            if (doc.panelSettings == null)
                errors.Add($"{owner}: UIDocument sem PanelSettings.");
            if (GetComponent<Abandoned.SpaceApps.UI.ResponsiveLayoutClass>() == null)
                errors.Add($"{owner}: falta ResponsiveLayoutClass (UI_SPEC: todo UIDocument de tela cheia).");
            if (doc.visualTreeAsset == null)
            {
                errors.Add($"{owner}: UIDocument sem UXML.");
                return errors;
            }

            var tree = doc.visualTreeAsset.CloneTree();
            var required = new List<string>
            {
                "plaque-root", "board", "tray", "hold", "progress-fill", "plaque-label", "snap-label", "assist", "assist-fill", "retry", "retry-label",
            };
            for (var i = 0; i < Pieces; i++)
            {
                required.Add("slot-" + i);
                required.Add("slot-icon-" + i);
                required.Add("slot-name-" + i);
                required.Add("card-" + i);
                required.Add("card-icon-" + i);
                required.Add("card-name-" + i);
            }

            foreach (var name in required)
            {
                if (tree.Q(name) == null)
                    errors.Add($"{owner}: o UXML não tem o elemento '{name}'.");
            }

            foreach (var key in NameKeys)
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith("PIO.UI.", StringComparison.Ordinal))
                    errors.Add($"{owner}: chave de texto inválida '{key}'.");
            }

            return errors;
        }

        /// <summary>Texto por chave; "{count}" recebe o número. Sem resolvedor, mostra a chave (nunca uma frase inventada).</summary>
        static void SetText(TextElement element, string key, Func<string, string> text, ref string last, int count = -1)
        {
            if (element == null)
                return;

            var value = text != null ? text(key) : null;
            if (string.IsNullOrEmpty(value))
                value = key;
            if (count >= 0)
                value = value.Replace("{count}", count.ToString());
            if (value == last)
                return;

            last = value;
            element.text = value;
        }
    }
}
