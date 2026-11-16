using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Desenha a orientação do <see cref="GuidanceController"/> (UI-005): placa "Como jogar" com uma linha por instrução
    /// (tecla + texto PT/EN), reforço aos 12 s e oferta de H/corredor aos 30 s. Entra na coluna da base do
    /// <see cref="MissionHUD"/>, acima do prompt E, então não se sobrepõe a ele nem à legenda.
    /// <para>
    /// Só por eventos: não há <c>Update</c> e nenhuma alocação por quadro (o texto é refeito quando o estado, o idioma ou
    /// a tabela mudam e só se a assinatura mudou). Nenhum elemento captura o mouse (<c>picking-mode="Ignore"</c>) e não há
    /// botão: a instrução some pela ação, nunca por um clique. Visível só em jogo e em contexto de gameplay (ou painel, quando
    /// a atividade roda num painel); some na pausa, na cinemática e fora do capítulo.
    /// </para>
    /// Instalação (R1, INT-001): instancie o prefab <c>Guidance.prefab</c> e chame
    /// <c>Initialize(guidanceController, missionHud, inputRouter, chapterDirector, settingsStore)</c> depois de ligar o HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialPresenter : MonoBehaviour
    {
        const string ContainerName = "guidance";
        const string ReinforceClass = "sa-guide--reinforce";
        const string OfferClass = "sa-guide--offer";
        const string PulseClass = "sa-guide--pulse";
        const string EmphasisClass = "sa-guide__line--emphasis";
        const string DoneClass = "sa-guide__line--done";

        /// <summary>Tempo que a instrução cumprida fica à vista, com "feito ✓", antes de recolher.</summary>
        const long DoneLingerMilliseconds = 1500;

        [SerializeField, Tooltip("GuidanceTexts.json: textos PT/EN do tutorial e da assistência.")] TextAsset uiTexts;
        [SerializeField, Tooltip("Guidance.uss: só estende os tokens da UI-001.")] StyleSheet styleSheet;

        sealed class LineView
        {
            public VisualElement Root;
            public Label Glyph;
            public Label Text;
            public bool Shown;
            public bool Emphasized;
            public bool Done;
        }

        readonly List<TextAsset> chapterTables = new List<TextAsset>();
        readonly List<LineView> lines = new List<LineView>();

        GuidanceController controller;
        InputRouter router;
        ChapterDirector director;
        SettingsStore settings;
        VisualElement parent;
        DossierTextCatalog texts;
        VisualElement container;
        Label title;
        Label helpOffer;
        Label corridorOffer;
        IVisualElementScheduledItem lingerItem;

        string language = SettingsData.English;
        HoldMode holdMode = HoldMode.Hold;
        bool reducedMotion;
        bool extraAssistance;
        bool lingering;
        bool bound;
        bool containerShown;
        string renderedSignature = string.Empty;
        string renderedLanguage;
        int renderedTextCount = -1;

        public bool IsBound => bound;

        /// <summary>A placa está desenhada e visível agora (para testes e diagnóstico).</summary>
        public bool IsShowing => bound && containerShown;

        public int VisibleLineCount
        {
            get
            {
                var count = 0;
                foreach (var line in lines)
                {
                    if (line.Shown)
                        count++;
                }

                return count;
            }
        }

        /// <summary>Texto da linha <paramref name="index"/> visível (para testes), ou nulo.</summary>
        public string LineText(int index)
        {
            var seen = 0;
            foreach (var line in lines)
            {
                if (!line.Shown)
                    continue;

                if (seen++ == index)
                    return line.Text.text;
            }

            return null;
        }

        public bool HasHelpOffer => bound && helpOffer != null && helpOffer.style.display == DisplayStyle.Flex;
        public bool HasCorridorOffer => bound && corridorOffer != null && corridorOffer.style.display == DisplayStyle.Flex;

        /// <summary>Tabela de textos do capítulo (ex.: alvo e objeto de uma atividade). Pode ser chamada antes ou depois de ligar.</summary>
        public void AddTextTable(TextAsset table)
        {
            if (table == null)
                return;

            chapterTables.Add(table);
            if (texts != null)
            {
                DossierTextTables.Load(table, texts);
                Render();
            }
        }

        /// <summary>Instala a placa na coluna da base do HUD (acima do prompt E). O HUD precisa estar ligado.</summary>
        public void Initialize(GuidanceController guidance, MissionHUD hud, InputRouter inputRouter = null,
            ChapterDirector chapterDirector = null, SettingsStore settingsStore = null)
        {
            if (hud == null)
                throw new ArgumentNullException(nameof(hud));

            var column = hud.BottomColumn;
            if (column == null)
                throw new InvalidOperationException("TutorialPresenter: o MissionHUD ainda não está ligado (chame o Initialize do HUD antes).");

            Initialize(guidance, column, inputRouter, chapterDirector, settingsStore);
        }

        /// <summary>Instala a placa num elemento qualquer (testes, ferramentas). O elemento precisa receber os estilos da UI-001.</summary>
        public void Initialize(GuidanceController guidance, VisualElement host, InputRouter inputRouter = null,
            ChapterDirector chapterDirector = null, SettingsStore settingsStore = null)
        {
            Unbind();
            controller = guidance != null ? guidance : throw new ArgumentNullException(nameof(guidance));
            parent = host != null ? host : throw new ArgumentNullException(nameof(host));
            router = inputRouter;
            director = chapterDirector;
            settings = settingsStore;
            if (isActiveAndEnabled)
                Bind();
        }

        void OnEnable()
        {
            if (controller != null)
                Bind();
        }

        void OnDisable() => Unbind();

        bool Bind()
        {
            if (bound)
                return true;

            if (controller == null || parent == null || uiTexts == null)
            {
                Debug.LogWarning("[TutorialPresenter] Não ligou: faltam o GuidanceController, o elemento de destino ou GuidanceTexts.json.", this);
                return false;
            }

            if (settings == null)
            {
                settings = new SettingsStore();
                settings.Load();
            }

            texts = new DossierTextCatalog();
            DossierTextTables.Load(uiTexts, texts);
            foreach (var table in chapterTables)
                DossierTextTables.Load(table, texts);

            BuildContainer();
            ReadSettings(settings.Current);
            settings.Changed += OnSettingsChanged;
            controller.Changed += OnControllerChanged;
            controller.StepCompleted += OnStepCompleted;
            if (router != null)
                router.ContextChanged += OnContextChanged;

            if (director != null)
                director.StateChanged += OnStateChanged;

            bound = true;
            Render();
            return true;
        }

        void Unbind()
        {
            if (!bound)
                return;

            bound = false;
            settings.Changed -= OnSettingsChanged;
            controller.Changed -= OnControllerChanged;
            controller.StepCompleted -= OnStepCompleted;
            if (router != null)
                router.ContextChanged -= OnContextChanged;

            if (director != null)
                director.StateChanged -= OnStateChanged;

            lingerItem?.Pause();
            lingerItem = null;
            lingering = false;
            container?.RemoveFromHierarchy();
            container = null;
            lines.Clear();
            renderedSignature = string.Empty;
            containerShown = false;
        }

        void BuildContainer()
        {
            container = new VisualElement { name = ContainerName, pickingMode = PickingMode.Ignore };
            container.AddToClassList("sa-plate");
            container.AddToClassList("sa-guide");
            if (styleSheet != null)
                container.styleSheets.Add(styleSheet);

            title = MakeLabel("sa-guide__title");
            helpOffer = MakeLabel("sa-guide__offer");
            corridorOffer = MakeLabel("sa-guide__offer");
            container.Add(title);
            container.Add(helpOffer);
            container.Add(corridorOffer);
            SetDisplay(title, false);
            SetDisplay(helpOffer, false);
            SetDisplay(corridorOffer, false);
            container.style.display = DisplayStyle.None;
            parent.Insert(0, container);
        }

        static Label MakeLabel(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        static void SetDisplay(VisualElement element, bool visible) =>
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        LineView GetLine(int index)
        {
            while (lines.Count <= index)
            {
                var root = new VisualElement { pickingMode = PickingMode.Ignore };
                root.AddToClassList("sa-guide__line");
                var keycap = new VisualElement { pickingMode = PickingMode.Ignore };
                keycap.AddToClassList("sa-keycap");
                keycap.AddToClassList("sa-guide__keycap");
                var glyph = MakeLabel("sa-keycap__glyph");
                keycap.Add(glyph);
                var text = MakeLabel("sa-guide__text");
                root.Add(keycap);
                root.Add(text);
                // As ofertas (H, corredor) ficam sempre abaixo das linhas: linhas entram antes do primeiro rótulo de oferta.
                container.Insert(container.IndexOf(helpOffer), root);
                lines.Add(new LineView { Root = root, Glyph = glyph, Text = text });
            }

            return lines[index];
        }

        // ---------- Eventos ----------

        void ReadSettings(SettingsData data)
        {
            language = data.languageCode;
            holdMode = data.holdMode;
            reducedMotion = data.reducedMotion;
            extraAssistance = data.extraAssistance;
        }

        void OnSettingsChanged(SettingsData data)
        {
            ReadSettings(data);
            Render();
        }

        void OnControllerChanged()
        {
            if (!controller.IsComplete && !controller.IsRunning)
                lingering = false;

            Render();
        }

        void OnStepCompleted(string stepId)
        {
            // Cumprida por uma ação real: a linha fica um instante com "feito ✓" e recolhe sozinha.
            lingering = true;
            lingerItem?.Pause();
            lingerItem = container.schedule.Execute(EndLinger);
            lingerItem.StartingIn(DoneLingerMilliseconds);
        }

        void EndLinger()
        {
            lingering = false;
            Render();
        }

        void OnContextChanged(InputContext context) => Render();

        void OnStateChanged(AppState state) => Render();

        // ---------- Desenho ----------

        bool ContextAllowsShowing()
        {
            if (director != null && director.State != AppState.Playing)
                return false;

            if (router == null)
                return true;

            var context = router.Context;
            return context == InputContext.Gameplay || (controller.CountsInPanel && context == InputContext.Panel);
        }

        void Render()
        {
            if (!bound)
                return;

            var view = ContextAllowsShowing()
                ? GuidanceViewBuilder.Build(controller.Snapshot, texts, language, holdMode, lingering, extraAssistance)
                : GuidanceView.Hidden;

            // Só reescreve quando algo mudou: estado, idioma ou tabela (nunca por quadro).
            var signature = view.Signature();
            if (signature == renderedSignature && language == renderedLanguage && texts.Count == renderedTextCount)
                return;

            renderedSignature = signature;
            renderedLanguage = language;
            renderedTextCount = texts.Count;
            Apply(view);
        }

        void Apply(in GuidanceView view)
        {
            containerShown = view.Visible;
            container.style.display = view.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!view.Visible)
                return;

            var hasTitle = !string.IsNullOrEmpty(view.Title);
            SetDisplay(title, hasTitle);
            if (hasTitle)
                title.text = view.Title;

            container.EnableInClassList(ReinforceClass, view.Level == GuidanceLevel.Reinforce);
            container.EnableInClassList(OfferClass, view.Level == GuidanceLevel.Offer);
            container.EnableInClassList(PulseClass, !reducedMotion && view.Level >= GuidanceLevel.Reinforce);

            for (var i = 0; i < view.Lines.Length; i++)
            {
                var line = GetLine(i);
                var data = view.Lines[i];
                line.Shown = true;
                SetDisplay(line.Root, true);
                line.Glyph.text = data.Glyph;
                line.Text.text = data.Text;
                if (line.Emphasized != data.Emphasized)
                {
                    line.Root.EnableInClassList(EmphasisClass, data.Emphasized);
                    line.Emphasized = data.Emphasized;
                }

                if (line.Done != data.Done)
                {
                    line.Root.EnableInClassList(DoneClass, data.Done);
                    line.Done = data.Done;
                }
            }

            for (var i = view.Lines.Length; i < lines.Count; i++)
            {
                lines[i].Shown = false;
                SetDisplay(lines[i].Root, false);
            }

            SetOffer(helpOffer, view.HelpOffer);
            SetOffer(corridorOffer, view.CorridorOffer);
        }

        static void SetOffer(Label label, string text)
        {
            var visible = !string.IsNullOrEmpty(text);
            SetDisplay(label, visible);
            if (visible)
                label.text = text;
        }

        /// <summary>Referências e tabelas. Vazio = pronto. Chamado pelo teste do prefab.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(TutorialPresenter);
            if (uiTexts == null)
                ContractValidation.Report(errors, owner, "falta GuidanceTexts.json.");

            if (styleSheet == null)
                ContractValidation.Report(errors, owner, "falta Guidance.uss.");

            return errors;
        }
    }
}
