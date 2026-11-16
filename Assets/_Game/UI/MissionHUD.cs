using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// HUD compartilhado dos três capítulos (UI-003): objetivo principal, âncora e seta de borda, prompt E com
    /// progresso, comando em trânsito e energia. Apresenta o que o <see cref="IInteractionService"/> informa; não
    /// recalcula alcance. Esconde a orientação na pausa e tudo na cinemática (<see cref="HudVisibility"/>).
    /// <para>
    /// Instalação (R1, INT-001): instancie o prefab <c>MissionHUD</c> na cena do capítulo (ou no aplicativo) e chame
    /// <see cref="Initialize"/> com o <c>InteractionService</c>, o <c>InputRouter</c>, o <c>ChapterDirector</c>, o
    /// <c>SettingsStore</c> e o <c>CameraDirector</c>. Os capítulos alimentam o HUD pela interface
    /// <see cref="IMissionHud"/>. Legenda (UI-004) e pausa (UI-006) entram pelas colunas <see cref="BottomColumn"/> e
    /// <see cref="RightColumn"/>, sem criar outro documento que se sobreponha.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MissionHUD : MonoBehaviour, IMissionHud
    {
        const string RootName = "hud-root";
        const string EnergyLowClass = "sa-energy--low";

        [SerializeField] UIDocument document;
        [SerializeField, Tooltip("HudTexts.json: textos PT/EN do HUD compartilhado.")] TextAsset uiTexts;

        readonly List<TextAsset> chapterTables = new List<TextAsset>();

        IInteractionService interactions;
        InputRouter router;
        ChapterDirector director;
        SettingsStore settings;
        CameraDirector cameraDirector;
        Camera cameraOverride;
        Transform reference;

        DossierTextCatalog texts;
        InteractionPrompt prompt;
        ObjectiveIndicator indicator;
        HudLayers layers = HudLayers.None;
        string language = SettingsData.English;
        HoldMode holdMode = HoldMode.Hold;
        bool bound;
        float? energy;

        VisualElement root;
        VisualElement energyChip;
        Label energyLabel;
        VisualElement energyFill;
        VisualElement rightColumn;
        VisualElement bottomColumn;
        bool energyShown;
        bool energyKnown;
        bool energyLowShown;
        string renderedEnergyLabel;
        int renderedEnergyPercent = -1;
        int energySignature = -2;
        string energyLanguage;
        int energyTextCount = -1;
        EnergyView energyView = EnergyView.Hidden;

        public bool IsBound => bound;

        /// <summary>Camada do HUD que está visível agora (para testes e diagnóstico).</summary>
        public HudLayers CurrentLayers => layers;

        public InteractionPrompt Prompt => prompt;
        public ObjectiveIndicator Indicator => indicator;

        /// <summary>Coluna do topo à direita (UI-006 coloca a pausa aqui, acima do medidor de energia).</summary>
        public VisualElement RightColumn => rightColumn;

        /// <summary>Coluna da base: o prompt E fica no topo dela e a legenda (UI-004) entra logo abaixo, sem sobreposição.</summary>
        public VisualElement BottomColumn => bottomColumn;

        /// <summary>
        /// Método de instalação. Só <paramref name="interactionService"/> é obrigatório. Sem <paramref name="inputRouter"/> o
        /// HUD assume gameplay; sem <paramref name="chapterDirector"/> assume capítulo em andamento; sem
        /// <paramref name="settingsStore"/> usa as preferências gravadas. Chamar de novo religa com as novas dependências.
        /// </summary>
        public void Initialize(IInteractionService interactionService, InputRouter inputRouter, ChapterDirector chapterDirector,
            SettingsStore settingsStore, CameraDirector cameras)
        {
            interactions = interactionService ?? throw new ArgumentNullException(nameof(interactionService));
            router = inputRouter;
            director = chapterDirector;
            settings = settingsStore;
            cameraDirector = cameras;

            if (!isActiveAndEnabled)
                return;

            Unbind();
            TryBind();
        }

        /// <summary>Câmera usada para projetar as âncoras quando não há <see cref="CameraDirector"/> (testes e ferramentas).</summary>
        public void SetCamera(Camera camera) => cameraOverride = camera;

        void Reset() => document = GetComponent<UIDocument>();

        void OnEnable() => TryBind();

        void Start()
        {
            if (bound)
                return;

            TryBind();
            if (!bound)
                Debug.LogWarning("[MissionHUD] Não ligou: faltam o IInteractionService (Initialize), os textos ou o UXML do HUD.", this);
        }

        void OnDisable() => Unbind();

        void LateUpdate()
        {
            if (!bound)
                return;

            Tick(Time.unscaledDeltaTime);
        }

        // ---------- IMissionHud ----------

        public void SetObjective(in HudObjective objective)
        {
            if (!bound)
                throw new InvalidOperationException("MissionHUD ainda não foi ligado (Initialize).");

            indicator.SetObjective(objective);
        }

        public void ClearObjective() => indicator?.Clear();

        public void SetEnergy(float fraction01) => energy = fraction01;

        public void ClearEnergy() => energy = null;

        public void SetCommand(HudCommandState command) => prompt?.Model.SetCommand(command);

        public void ClearCommand() => prompt?.Model.ClearCommand();

        public void ReportProgress(string candidateId, float fraction01) => prompt?.Model.ReportProgress(candidateId, fraction01);

        public void ClearProgress(string candidateId) => prompt?.Model.ClearProgress(candidateId);

        public void SetReference(Transform value) => reference = value;

        public void AddTextTable(TextAsset table)
        {
            if (table == null)
                return;

            chapterTables.Add(table);
            if (texts != null)
                DossierTextTables.Load(table, texts);
        }

        // ---------- Ligação ----------

        bool TryBind()
        {
            if (bound)
                return true;

            if (document == null)
                document = GetComponent<UIDocument>();
            if (document == null || interactions == null || uiTexts == null)
                return false;

            var documentRoot = document.rootVisualElement;
            root = documentRoot != null ? documentRoot.Q<VisualElement>(RootName) : null;
            if (root == null)
                return false;

            try
            {
                BindInterface();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Unbind();
                return false;
            }

            bound = true;
            ApplyLayers();
            return true;
        }

        void BindInterface()
        {
            energyChip = Require<VisualElement>("energy");
            energyLabel = Require<Label>("energy-label");
            energyFill = Require<VisualElement>("energy-fill");
            rightColumn = Require<VisualElement>("hud-right");
            bottomColumn = Require<VisualElement>("hud-bottom");

            if (settings == null)
            {
                settings = new SettingsStore();
                settings.Load();
            }

            texts = new DossierTextCatalog();
            DossierTextTables.Load(uiTexts, texts);
            foreach (var table in chapterTables)
                DossierTextTables.Load(table, texts);

            prompt = new InteractionPrompt(root, interactions);
            indicator = new ObjectiveIndicator(root);
            SetEnergyShown(false);

            // SettingsStore.Current clona via JSON: ler uma vez e acompanhar por evento (nada de alocação por quadro).
            ReadSettings(settings.Current);
            settings.Changed += OnSettingsChanged;

            if (router != null)
                router.ContextChanged += OnContextChanged;
            if (director != null)
            {
                director.StateChanged += OnStateChanged;
                director.ChapterUnloading += OnChapterUnloading;
            }
        }

        void Unbind()
        {
            if (settings != null)
                settings.Changed -= OnSettingsChanged;
            if (router != null)
                router.ContextChanged -= OnContextChanged;
            if (director != null)
            {
                director.StateChanged -= OnStateChanged;
                director.ChapterUnloading -= OnChapterUnloading;
            }

            prompt?.Dispose();
            prompt = null;
            indicator = null;
            energyShown = false;
            energyKnown = false;
            renderedEnergyLabel = null;
            renderedEnergyPercent = -1;
            energySignature = -2;
            layers = HudLayers.None;
            root = null;
            bound = false;
        }

        T Require<T>(string elementName) where T : VisualElement
        {
            var element = root.Q<T>(elementName);
            if (element == null)
                throw new InvalidOperationException($"MissionHUD: o UXML não tem o elemento '{elementName}' ({typeof(T).Name}).");

            return element;
        }

        void ReadSettings(SettingsData data)
        {
            language = data.languageCode;
            holdMode = data.holdMode;
        }

        void OnSettingsChanged(SettingsData data) => ReadSettings(data);

        void OnContextChanged(InputContext context) => ApplyLayers();

        void OnStateChanged(AppState state) => ApplyLayers();

        /// <summary>Nada de um capítulo atravessa para o seguinte: objetivo, energia, comando, progresso e feedback zeram.</summary>
        void OnChapterUnloading(string chapterId)
        {
            indicator?.Clear();
            energy = null;
            prompt?.Model.Reset();
            reference = null;
        }

        void ApplyLayers()
        {
            if (!bound)
                return;

            var playing = director == null || director.State == AppState.Playing;
            var context = router != null ? (HudInputContext)(int)router.Context : HudInputContext.Gameplay;
            layers = HudVisibility.For(playing, context);
            root.style.display = layers.Any ? DisplayStyle.Flex : DisplayStyle.None;
            prompt.SetLayerVisible(layers.Prompt);
        }

        // ---------- Quadro ----------

        void Tick(float unscaledDeltaSeconds)
        {
            // O modelo do prompt corre sempre (relógio do feedback de conclusão), mesmo com a camada oculta.
            prompt.Tick(unscaledDeltaSeconds, texts, language, holdMode);

            var candidateActive = interactions.TryGetActive(out var candidate);
            if (layers.Any)
                indicator.Tick(ActiveCamera(), reference, candidateActive, in candidate, layers, texts, language);

            RenderEnergy();
        }

        Camera ActiveCamera()
        {
            if (cameraDirector != null && cameraDirector.TargetCamera != null)
                return cameraDirector.TargetCamera;

            return cameraOverride;
        }

        void RenderEnergy()
        {
            // Só reconstrói o texto quando o percentual, o idioma ou a tabela mudam (sem alocação por quadro).
            var signature = layers.Energy && energy.HasValue ? HudFormat.Percent(energy.Value) : -1;
            if (signature != energySignature || language != energyLanguage || texts.Count != energyTextCount)
            {
                energyView = signature >= 0 ? EnergyViewBuilder.Build(energy, texts, language) : EnergyView.Hidden;
                energySignature = signature;
                energyLanguage = language;
                energyTextCount = texts.Count;
            }

            var view = energyView;
            SetEnergyShown(view.Visible);
            if (!view.Visible)
                return;

            if (view.Label != renderedEnergyLabel)
            {
                energyLabel.text = view.Label;
                renderedEnergyLabel = view.Label;
            }

            var percent = HudFormat.Percent(view.Fraction);
            if (percent != renderedEnergyPercent)
            {
                energyFill.style.width = new Length(percent, LengthUnit.Percent);
                renderedEnergyPercent = percent;
            }

            if (view.IsLow != energyLowShown)
            {
                energyChip.EnableInClassList(EnergyLowClass, view.IsLow);
                energyLowShown = view.IsLow;
            }
        }

        void SetEnergyShown(bool visible)
        {
            if (energyKnown && energyShown == visible)
                return;

            energyKnown = true;
            energyShown = visible;
            energyChip.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
