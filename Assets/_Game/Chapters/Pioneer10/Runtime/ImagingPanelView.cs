using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// PIO-006 · Painel UI Toolkit da varredura IPP (<c>UI/ImagingPanel.uxml</c>): oito faixas lado a lado (quatro do canal
    /// vermelho, quatro do azul), cursor de fase com zona larga contornada e o prompt de E. Só desenha o
    /// <see cref="ImagingViewState"/>; textos vêm de chaves resolvidas pelo capítulo (sem frase literal no código).
    /// O quadro ocupa o <c>UiFrame</c> do anchor PIO.Imaging (lado direito); a esquerda fica para a sonda 3D.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ImagingPanelView : MonoBehaviour
    {
        public const string RedKey = "PIO.UI.red";
        public const string BlueKey = "PIO.UI.blue";
        public const string ScanKey = "PIO.UI.scan";

        [SerializeField] UIDocument document;

        VisualElement root, zone, cursor, holdPlate, progressFill, greenChip, interruptDim, recoverySlot;
        VisualElement[] strips;
        Label redLabel, blueLabel, scanLabel;
        string lastRed, lastBlue, lastScan;
        int lastCaptured = -1;
        float lastZoneStart = -1f, lastZoneEnd = -1f;
        ImagingPhase lastPhase = (ImagingPhase)(-1);
        ImagingHoldState lastHold = (ImagingHoldState)(-1);

        /// <summary>Uma classe por estado do prompt de E (UI_SPEC). <c>available</c> é o estilo-base: só ganha a classe.</summary>
        static readonly string[] HoldClasses =
        {
            "sa-hold--available", "sa-hold--rearm", "sa-hold--holding", "sa-hold--paused", "sa-hold--done",
        };

        public bool IsBound => root != null;
        public VisualElement Root => root;

        /// <summary>Contêiner vazio para a PIO-008 (botão de recuperação e aviso) sem editar este painel.</summary>
        public VisualElement RecoverySlot => recoverySlot;

        void OnEnable() => Bind();

        void OnDisable() => root = null;

        /// <summary>Liga os elementos do UXML. Idempotente; chamado no OnEnable e pela prévia de Editor.</summary>
        public void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("imaging-root") == null)
                return;
            if (root == tree.Q("imaging-root"))
                return;

            root = tree.Q("imaging-root");
            zone = root.Q("zone");
            cursor = root.Q("cursor");
            holdPlate = root.Q("hold");
            progressFill = root.Q("progress-fill");
            greenChip = root.Q("green-chip");
            interruptDim = root.Q("interrupt-dim");
            recoverySlot = root.Q("recovery-slot");
            strips = new VisualElement[ImagingStrips];
            for (var i = 0; i < strips.Length; i++)
                strips[i] = root.Q("strip-" + i);
            redLabel = root.Q<Label>("channel-red");
            blueLabel = root.Q<Label>("channel-blue");
            scanLabel = root.Q<Label>("scan-label");
            lastRed = lastBlue = lastScan = null;
            lastCaptured = -1;
            lastZoneStart = lastZoneEnd = -1f;
            lastPhase = (ImagingPhase)(-1);
            lastHold = (ImagingHoldState)(-1);
        }

        const int ImagingStrips = PioneerImaging.StripCount;

        public void Render(ImagingViewState state, Func<string, string> text)
        {
            if (root == null)
                Bind();
            if (root == null)
                return;

            if (state.Phase != lastPhase)
            {
                lastPhase = state.Phase;
                var visible = state.Phase != ImagingPhase.Idle && state.Phase != ImagingPhase.Done;
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                root.EnableInClassList("pio-img--learn", state.Phase == ImagingPhase.Learn);
                root.EnableInClassList("pio-img--assembled", state.Phase == ImagingPhase.Assembled);
                Show(interruptDim, state.Interrupted);
                Show(greenChip, state.Phase == ImagingPhase.Assembled);
                Show(holdPlate, state.Phase == ImagingPhase.Learn || state.Phase == ImagingPhase.Sweep || state.Phase == ImagingPhase.Interrupted);
            }

            if (state.Captured != lastCaptured)
            {
                lastCaptured = state.Captured;
                for (var i = 0; i < strips.Length; i++)
                {
                    strips[i].EnableInClassList("pio-img__strip--done", i < state.Captured);
                    strips[i].EnableInClassList("pio-img__strip--target", i == state.Target && state.Captured < ImagingStrips);
                }

                progressFill.style.width = Length.Percent(state.Captured * 100f / ImagingStrips);
                SetText(scanLabel, ScanKey, text, ref lastScan, state.Captured);
            }

            // O cursor pode ficar parado fora da janela: só a zona reposiciona quando muda (ajuda ampliou).
            cursor.style.left = Length.Percent(state.PassProgress * 100f);
            if (!Mathf.Approximately(state.WindowStart, lastZoneStart) || !Mathf.Approximately(state.WindowEnd, lastZoneEnd))
            {
                lastZoneStart = state.WindowStart;
                lastZoneEnd = state.WindowEnd;
                zone.style.left = Length.Percent(state.WindowStart * 100f);
                zone.style.width = Length.Percent((state.WindowEnd - state.WindowStart) * 100f);
            }

            zone.EnableInClassList("pio-img__zone--active", state.InWindow);
            zone.EnableInClassList("pio-img__zone--wide", state.Assisted);
            zone.EnableInClassList("pio-img__zone--captured", state.PassCaptured);
            cursor.EnableInClassList("pio-img__cursor--in", state.InWindow);
            SetHoldState(state.Hold);
            holdPlate.EnableInClassList("pio-img__hold--pulse", state.HelpLevel >= 1 && !state.Holding);
            holdPlate.EnableInClassList("pio-img__hold--missed", state.PassMissed);
            SetText(redLabel, RedKey, text, ref lastRed);
            SetText(blueLabel, BlueKey, text, ref lastBlue);
        }

        /// <summary>Troca o estado do prompt de E: desliga as outras quatro classes e liga só a atual.</summary>
        void SetHoldState(ImagingHoldState hold)
        {
            if (hold == lastHold)
                return;

            lastHold = hold;
            for (var i = 0; i < HoldClasses.Length; i++)
                holdPlate.EnableInClassList(HoldClasses[i], i == (int)hold);
        }

        /// <summary>
        /// Erros de montagem (lista vazia = conforme): UIDocument com UXML, <see cref="ResponsiveLayoutClass"/> e todos os
        /// elementos que o painel usa. O UXML é clonado, então funciona fora do Play Mode.
        /// </summary>
        public System.Collections.Generic.List<string> ValidateSetup()
        {
            var errors = new System.Collections.Generic.List<string>();
            const string owner = "ImagingPanelView";
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
            var required = new System.Collections.Generic.List<string>
            {
                "imaging-root", "frame", "image", "disc", "interrupt-dim", "track", "zone", "cursor", "hold", "progress-fill",
                "scan-label", "channel-red", "channel-blue", "green-chip", "recovery-slot",
            };
            for (var i = 0; i < ImagingStrips; i++)
                required.Add("strip-" + i);
            foreach (var name in required)
            {
                if (tree.Q(name) == null)
                    errors.Add($"{owner}: o UXML não tem o elemento '{name}'.");
            }

            foreach (var key in new[] { RedKey, BlueKey, ScanKey })
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith("PIO.UI."))
                    errors.Add($"{owner}: chave de texto inválida '{key}'.");
            }

            return errors;
        }

        static void Show(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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
