using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// PIO-012 · Painel UI Toolkit da despedida (<c>UI/FarewellPanel.uxml</c>): guia discreto "direção de Aldebaran", o preto
    /// intencional do memorial com "PIONEER 10 · 1972–2003 / Em viagem" e os dois botões finais. Só desenha o
    /// <see cref="FarewellViewState"/> e devolve os cliques; textos vêm de chaves resolvidas pelo capítulo (sem frase literal
    /// no código). Nada aqui inicia outro capítulo.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class FarewellPanelView : MonoBehaviour
    {
        public const string GuideKey = PioneerFarewellActivity.GuideKey;
        public const string EndingKey = PioneerFarewellActivity.EndingKey;
        public const string ReturnKey = PioneerFarewellActivity.ReturnKey;
        public const string ReplayKey = PioneerFarewellActivity.ReplayKey;
        public const string CreditsKey = "PIO.UI.credits", CreditsTitleKey = "PIO.UI.credits.title", CreditsBodyKey = "PIO.UI.credits.body", CreditsCloseKey = "PIO.UI.credits.close";
        /// <summary>A chave do memorial junta o título e a linha de situação com " / "; o painel as desenha em duas linhas.</summary>
        public const string EndingSeparator = " / ";

        [SerializeField] UIDocument document;

        /// <summary>O memorial não é mais preto total: no máximo este tanto de escuro, para a sonda seguindo devagar para Aldebaran aparecer por trás do texto.</summary>
        public const float MemorialDimMax = 0.55f;

        VisualElement root, guide, black, memorial, choices, returnButton, replayButton, creditsPanel;
        Label guideLabel, titleLabel, subtitleLabel, returnLabel, replayLabel, creditsTitle, creditsBody;
        Button creditsButton, creditsClose;
        string lastGuide, lastTitle, lastSubtitle, lastReturn, lastReplay, lastCredits, lastCreditsTitle, lastCreditsBody, lastCreditsClose;
        FarewellPhase lastPhase = (FarewellPhase)(-1);

        public event Action ReturnClicked;
        public event Action ReplayClicked;

        /// <summary>O painel de fontes e creditos esta aberto: E/Enter nao podem escolher por tras dele.</summary>
        public bool CreditsOpen => creditsPanel != null && creditsPanel.style.display == DisplayStyle.Flex;
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
            if (tree == null || tree.Q("farewell-root") == null)
                return;
            if (root == tree.Q("farewell-root"))
                return;

            root = tree.Q("farewell-root");
            guide = root.Q("guide");
            black = root.Q("black");
            memorial = root.Q("memorial");
            choices = root.Q("choices");
            returnButton = root.Q("return-button");
            replayButton = root.Q("replay-button");
            guideLabel = root.Q<Label>("guide-label");
            titleLabel = root.Q<Label>("ending-title");
            subtitleLabel = root.Q<Label>("ending-subtitle");
            returnLabel = root.Q<Label>("return-label");
            replayLabel = root.Q<Label>("replay-label");
            if (returnButton != null)
                returnButton.RegisterCallback<ClickEvent>(_ => ReturnClicked?.Invoke());
            if (replayButton != null)
                replayButton.RegisterCallback<ClickEvent>(_ => ReplayClicked?.Invoke());
            creditsButton = root.Q<Button>("credits-button");
            creditsPanel = root.Q("credits-panel");
            creditsTitle = root.Q<Label>("credits-title");
            creditsBody = root.Q<Label>("credits-body");
            creditsClose = root.Q<Button>("credits-close");
            if (creditsButton != null && creditsPanel != null)
                creditsButton.clicked += () => creditsPanel.style.display = DisplayStyle.Flex;
            if (creditsClose != null && creditsPanel != null)
                creditsClose.clicked += () => creditsPanel.style.display = DisplayStyle.None;
            lastGuide = lastTitle = lastSubtitle = lastReturn = lastReplay = lastCredits = lastCreditsTitle = lastCreditsBody = lastCreditsClose = null;
            lastPhase = (FarewellPhase)(-1);
        }

        /// <summary>
        /// Separa o texto do memorial em título e linha de situação. Sem " / ", tudo vira título (nunca inventa a segunda linha).
        /// Função pura (testável sem painel).
        /// </summary>
        public static (string title, string subtitle) SplitEnding(string text)
        {
            if (string.IsNullOrEmpty(text))
                return (string.Empty, string.Empty);

            var index = text.IndexOf(EndingSeparator, StringComparison.Ordinal);
            return index < 0 ? (text, string.Empty) : (text.Substring(0, index), text.Substring(index + EndingSeparator.Length));
        }

        public void Render(FarewellViewState state, Func<string, string> text)
        {
            if (root == null)
                Bind();
            if (root == null)
                return;

            if (state.Phase != lastPhase)
            {
                lastPhase = state.Phase;
                root.style.display = state.Phase == FarewellPhase.Playing || state.Phase == FarewellPhase.Memorial ? DisplayStyle.Flex : DisplayStyle.None;
            }

            guide.style.display = state.GuideVisible ? DisplayStyle.Flex : DisplayStyle.None;
            black.style.opacity = state.BlackAlpha * MemorialDimMax; // escurece, mas deixa ver a sonda indo para a estrela (teste de jogo)
            black.style.display = state.BlackAlpha > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            memorial.style.display = state.EndingVisible ? DisplayStyle.Flex : DisplayStyle.None;
            choices.style.display = state.ChoicesVisible ? DisplayStyle.Flex : DisplayStyle.None;
            returnButton.EnableInClassList("pio-farewell__button--focus", state.Focus == 0);
            replayButton.EnableInClassList("pio-farewell__button--focus", state.Focus == 1);

            if (state.GuideVisible)
                SetText(guideLabel, GuideKey, text, ref lastGuide);
            if (state.EndingVisible)
            {
                var (title, subtitle) = SplitEnding(Resolve(EndingKey, text));
                if (title != lastTitle)
                {
                    lastTitle = title;
                    titleLabel.text = title;
                }

                if (subtitle != lastSubtitle)
                {
                    lastSubtitle = subtitle;
                    subtitleLabel.text = subtitle;
                }

                subtitleLabel.style.display = subtitle.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (state.ChoicesVisible)
            {
                SetText(returnLabel, ReturnKey, text, ref lastReturn);
                SetText(replayLabel, ReplayKey, text, ref lastReplay);
                SetText(creditsButton, CreditsKey, text, ref lastCredits);
                SetText(creditsTitle, CreditsTitleKey, text, ref lastCreditsTitle);
                SetText(creditsBody, CreditsBodyKey, text, ref lastCreditsBody);
                SetText(creditsClose, CreditsCloseKey, text, ref lastCreditsClose);
            }
            else if (creditsPanel != null)
                creditsPanel.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Erros de montagem (lista vazia = conforme): UIDocument com UXML, <see cref="ResponsiveLayoutClass"/> e todos os
        /// elementos que o painel usa. O UXML é clonado, então funciona fora do Play Mode.
        /// </summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = "FarewellPanelView";
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
            foreach (var name in new[]
            {
                "farewell-root", "guide", "guide-label", "black", "memorial", "ending-title", "ending-subtitle", "choices",
                "return-button", "return-label", "replay-button", "replay-label",
                "credits-button", "credits-panel", "credits-title", "credits-body", "credits-close",
            })
            {
                if (tree.Q(name) == null)
                    errors.Add($"{owner}: o UXML não tem o elemento '{name}'.");
            }

            foreach (var key in new[] { GuideKey, EndingKey, ReturnKey, ReplayKey })
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith("PIO.UI.", StringComparison.Ordinal))
                    errors.Add($"{owner}: chave de texto inválida '{key}'.");
            }

            return errors;
        }

        static string Resolve(string key, Func<string, string> text)
        {
            var value = text != null ? text(key) : null;
            return string.IsNullOrEmpty(value) ? key : value;
        }

        /// <summary>Texto por chave. Sem resolvedor, mostra a chave (nunca uma frase inventada).</summary>
        static void SetText(TextElement element, string key, Func<string, string> text, ref string last)
        {
            if (element == null)
                return;

            var value = Resolve(key, text);
            if (value == last)
                return;

            last = value;
            element.text = value;
        }
    }
}
