using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Dossiê funcional (UI-002): três pastas vindas do <see cref="ChapterCatalog"/>, detalhes opcionais,
    /// configurações, créditos, PT/EN, duração por capítulo e indicação de carregamento antes de o relógio da
    /// missão começar. É só a tela: as regras estão em <see cref="DossierFlow"/> e o início passa por
    /// <see cref="DossierDirectorLink"/> → <see cref="ChapterDirector.Begin"/>.
    /// <para>
    /// Instalação (R1, INT-001): instancie o prefab <c>DossierScreen</c> na cena Dossier e chame
    /// <see cref="Initialize"/> com o diretor, as preferências e o roteador de entrada do aplicativo. Mantenha o
    /// GameObject ativo: a tela se esconde sozinha durante o capítulo e precisa estar viva para mostrar o carregamento.
    /// Interface UI Toolkit não usa câmera; câmera e AudioListener do menu, se existirem, vão em <c>menuScene</c>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DossierScreen : MonoBehaviour
    {
        const string RootName = "dossier-root";
        const string OpenClass = "sa-modal--open";
        const string FocusClass = "sa-is-focused";
        const string LanguageActiveClass = "sa-lang--active";
        const string ErrorVisibleClass = "sa-dossier__error--visible";
        const string StatusVisibleClass = "sa-folder__status--visible";
        const string LoadingVisibleClass = "sa-loading--visible";
        const string HintVisibleClass = "sa-loading__hint--visible";
        const string StaticFillClass = "sa-loading__fill--static";
        const float LoadingFillPercent = 35f;
        const long LoadingHintDelayMilliseconds = 8000;
        const long LoadingTickMilliseconds = 33;
        const float VolumeStep = 0.1f;

        sealed class FolderView
        {
            public ChapterCatalog.Entry Entry;
            public VisualElement Root;
            public Label Label;
            public Button Details;
            public VisualElement Photo;
            public Label PhotoCaption;
            public Label Title;
            public Label Meta;
            public Label Question;
            public Label Status;
            public Label Duration;
            public Button Start;
        }

        readonly struct FocusTarget
        {
            public readonly Button Button;
            public readonly Action Activate;

            public FocusTarget(Button button, Action activate)
            {
                Button = button;
                Activate = activate;
            }
        }

        [SerializeField] UIDocument document;
        [SerializeField, Tooltip("Catálogo das pastas. Padrão do projeto: Assets/_Game/Core/ChapterCatalog.asset.")]
        ChapterCatalog catalog;
        [SerializeField, Tooltip("DossierTexts.json: textos PT/EN da interface do arquivo.")]
        TextAsset uiTexts;
        [SerializeField, Tooltip("DossierFolder.uxml: uma pasta por entrada do catálogo.")]
        VisualTreeAsset folderTemplate;
        [SerializeField, Tooltip("Opcional no Inspector; o aplicativo pode passar o diretor por Initialize.")]
        ChapterDirector director;
        [SerializeField, Tooltip("Opcional: habilita setas, Enter e Esc/P no dossiê. Sem ele, só o mouse.")]
        InputRouter inputRouter;
        [SerializeField, Tooltip("Câmera, AudioListener e áudio que só existem no menu: desligados durante o capítulo.")]
        MenuSceneGate menuScene = new MenuSceneGate();

        readonly List<FolderView> folderViews = new List<FolderView>();
        readonly List<FocusTarget> focusTargets = new List<FocusTarget>();
        readonly List<FocusTarget> modalFocus = new List<FocusTarget>();
        readonly List<KeyValuePair<Button, Action>> wiredButtons = new List<KeyValuePair<Button, Action>>();

        SettingsStore settings;
        DossierFlow flow;
        DossierDirectorLink link;
        DossierTextCatalog texts;
        InputRouter boundRouter;
        bool bound;
        bool keyboardMode;
        int focusIndex = -1;
        Button focusedButton;
        DossierPanel lastPanel;
        string lastPanelSlot;
        string accentOnModal;
        string accentOnLoading;

        VisualElement root;
        Label headerStamp;
        Label gameTitle;
        Label lede;
        Label errorBanner;
        VisualElement folders;
        Label languageLabel;
        Button languagePt;
        Button languageEn;
        Button settingsButton;
        Button creditsButton;
        Label disclaimer;
        VisualElement modal;
        VisualElement modalCard;
        Label modalStamp;
        Label modalTitle;
        ScrollView modalBody;
        VisualElement modalActions;
        VisualElement loadingOverlay;
        Label loadingStamp;
        Label loadingTitle;
        Label loadingChapter;
        VisualElement loadingFill;
        Label loadingNote;
        Label loadingHint;
        IVisualElementScheduledItem loadingTick;
        IVisualElementScheduledItem loadingHintTimer;
        bool loadingAnimating;

        /// <summary>
        /// Disparado quando o <c>ChapterDirector</c> aceitou o início, ainda no gesto da pessoa. Quem controla o
        /// áudio (AudioDirector) pode usá-lo para começar/destravar a música; o dossiê já libera o <c>AudioListener</c>.
        /// </summary>
        public event Action<string> StartGestureReceived;

        public bool IsBound => bound;

        /// <summary>
        /// Método de instalação. Pode ser chamado antes ou depois de o componente ativar; chamar de novo religa a tela
        /// com as novas dependências. <paramref name="settingsStore"/> nulo cria um com as preferências gravadas.
        /// </summary>
        public void Initialize(ChapterDirector chapterDirector, SettingsStore settingsStore, InputRouter router = null,
            ChapterCatalog chapterCatalog = null)
        {
            if (chapterDirector == null)
                throw new ArgumentNullException(nameof(chapterDirector));

            director = chapterDirector;
            settings = settingsStore;
            if (router != null)
                inputRouter = router;
            if (chapterCatalog != null)
                catalog = chapterCatalog;

            if (!isActiveAndEnabled)
                return;

            Unbind();
            TryBind();
        }

        void Reset() => document = GetComponent<UIDocument>();

        void OnEnable() => TryBind();

        void Start()
        {
            if (bound)
                return;

            TryBind();
            if (!bound)
                Debug.LogWarning("[DossierScreen] Não ligou: faltam diretor (Initialize), catálogo, textos, template de pasta ou o UXML do dossiê.", this);
        }

        void OnDisable() => Unbind();

        // ---------- Ligação ----------

        bool TryBind()
        {
            if (bound)
                return true;

            if (document == null)
                document = GetComponent<UIDocument>();
            if (document == null || director == null || catalog == null || uiTexts == null || folderTemplate == null)
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
            Refresh();
            return true;
        }

        void BindInterface()
        {
            headerStamp = Require<Label>("header-stamp");
            gameTitle = Require<Label>("game-title");
            lede = Require<Label>("lede");
            errorBanner = Require<Label>("error-banner");
            folders = Require<VisualElement>("folders");
            languageLabel = Require<Label>("language-label");
            languagePt = Require<Button>("language-pt");
            languageEn = Require<Button>("language-en");
            // O jogo só tem inglês: o seletor de idioma sai do dossiê.
            languageLabel.style.display = DisplayStyle.None;
            languagePt.style.display = DisplayStyle.None;
            languageEn.style.display = DisplayStyle.None;
            settingsButton = Require<Button>("settings-button");
            creditsButton = Require<Button>("credits-button");
            disclaimer = Require<Label>("disclaimer");
            modal = Require<VisualElement>("panel-overlay");
            modalCard = Require<VisualElement>("panel-card");
            modalStamp = Require<Label>("panel-stamp");
            modalTitle = Require<Label>("panel-title");
            modalBody = Require<ScrollView>("panel-body");
            modalActions = Require<VisualElement>("panel-actions");
            loadingOverlay = Require<VisualElement>("loading-overlay");
            loadingStamp = Require<Label>("loading-stamp");
            loadingTitle = Require<Label>("loading-title");
            loadingChapter = Require<Label>("loading-chapter");
            loadingFill = Require<VisualElement>("loading-fill");
            loadingNote = Require<Label>("loading-note");
            loadingHint = Require<Label>("loading-hint");

            if (settings == null)
            {
                settings = new SettingsStore();
                settings.Load();
            }

            texts = new DossierTextCatalog();
            DossierTextTables.Load(uiTexts, texts);
            foreach (var entry in catalog.Entries)
            {
                if (entry != null && entry.ChapterTexts != null)
                    DossierTextTables.Load(entry.ChapterTexts, texts);
            }

            link = new DossierDirectorLink(director, catalog, settings.Current.languageCode);
            flow = link.Flow;
            link.StartAccepted += OnStartAccepted;
            flow.Changed += Refresh;
            settings.Changed += OnSettingsChanged;
            director.StateChanged += OnDirectorStateChanged; // depois do link: o fluxo já foi atualizado

            BuildFolders();
            Wire(languagePt, () => OnLanguageClicked(DossierFlow.Portuguese));
            Wire(languageEn, () => OnLanguageClicked(DossierFlow.English));
            Wire(settingsButton, () => OnOpenPanelClicked(DossierPanel.Settings, null));
            Wire(creditsButton, () => OnOpenPanelClicked(DossierPanel.Credits, null));

            root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            if (inputRouter != null)
            {
                boundRouter = inputRouter;
                boundRouter.NavigateDown += OnNavigate;
                boundRouter.SubmitDown += OnSubmit;
                boundRouter.PauseDown += OnPause;
            }

            var inMenu = director.State == AppState.Boot || director.State == AppState.Dossier;
            menuScene.Apply(inMenu);
            if (inMenu)
                ReleaseCursor();
        }

        void Unbind()
        {
            StopLoadingAnimation();

            if (boundRouter != null)
            {
                boundRouter.NavigateDown -= OnNavigate;
                boundRouter.SubmitDown -= OnSubmit;
                boundRouter.PauseDown -= OnPause;
                boundRouter = null;
            }

            if (director != null)
                director.StateChanged -= OnDirectorStateChanged;
            if (settings != null)
                settings.Changed -= OnSettingsChanged;
            if (flow != null)
                flow.Changed -= Refresh;
            if (link != null)
            {
                link.StartAccepted -= OnStartAccepted;
                link.Dispose();
            }

            foreach (var wired in wiredButtons)
            {
                if (wired.Key != null)
                    wired.Key.clicked -= wired.Value;
            }

            root?.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            wiredButtons.Clear();
            folderViews.Clear();
            focusTargets.Clear();
            modalFocus.Clear();
            focusedButton = null;
            focusIndex = -1;
            lastPanel = DossierPanel.None;
            lastPanelSlot = null;
            link = null;
            flow = null;
            root = null;
            accentOnModal = null;
            accentOnLoading = null;
            bound = false;
        }

        T Require<T>(string elementName) where T : VisualElement
        {
            var element = root.Q<T>(elementName);
            if (element == null)
                throw new InvalidOperationException($"DossierScreen: o UXML não tem o elemento '{elementName}' ({typeof(T).Name}).");

            return element;
        }

        void Wire(Button button, Action action)
        {
            button.clicked += action;
            wiredButtons.Add(new KeyValuePair<Button, Action>(button, action));
        }

        void BuildFolders()
        {
            folders.Clear();
            folderViews.Clear();
            foreach (var entry in catalog.Entries)
            {
                var container = folderTemplate.Instantiate();
                var folder = container.Q<VisualElement>("folder");
                if (folder == null)
                    throw new InvalidOperationException("DossierScreen: DossierFolder.uxml precisa de um elemento 'folder'.");

                folder.RemoveFromHierarchy();
                folder.AddToClassList(entry.AccentClass);
                folders.Add(folder);

                var view = new FolderView
                {
                    Entry = entry,
                    Root = folder,
                    Label = folder.Q<Label>("folder-label"),
                    Details = folder.Q<Button>("folder-details"),
                    Photo = folder.Q<VisualElement>("folder-photo"),
                    PhotoCaption = folder.Q<Label>("folder-photo-caption"),
                    Title = folder.Q<Label>("folder-title"),
                    Meta = folder.Q<Label>("folder-meta"),
                    Question = folder.Q<Label>("folder-question"),
                    Status = folder.Q<Label>("folder-status"),
                    Duration = folder.Q<Label>("folder-duration"),
                    Start = folder.Q<Button>("folder-start"),
                };

                if (view.Label == null || view.Details == null || view.Photo == null || view.PhotoCaption == null || view.Title == null
                    || view.Meta == null || view.Question == null || view.Status == null || view.Duration == null || view.Start == null)
                    throw new InvalidOperationException("DossierScreen: DossierFolder.uxml está incompleto (faltam elementos nomeados).");

                var slotId = entry.SlotId;
                Wire(view.Start, () => OnStartClicked(slotId));
                Wire(view.Details, () => OnOpenPanelClicked(DossierPanel.Details, slotId));
                folderViews.Add(view);
            }
        }

        // ---------- Ações ----------

        void OnStartClicked(string slotId)
        {
            if (link != null)
                link.Start(slotId);
        }

        void OnOpenPanelClicked(DossierPanel panel, string slotId) => flow?.TryOpenPanel(panel, slotId);

        void OnBackClicked() => flow?.Back();

        void OnLanguageClicked(string code)
        {
            if (flow == null || !flow.TrySetLanguage(code))
                return;

            settings.Apply(data => data.languageCode = code);
        }

        void OnSettingsChanged(SettingsData data)
        {
            if (flow == null)
                return;

            flow.ApplyExternalLanguage(data.languageCode);
            Refresh();
        }

        /// <summary>O clique em Iniciar é o gesto da pessoa: libera o áudio do navegador e, se o capítulo pede, captura o cursor.</summary>
        void OnStartAccepted(ChapterCatalog.Entry entry)
        {
            AudioListener.pause = false;
            if (entry.CaptureCursor)
            {
                // UnityEngine.UIElements também define 'Cursor' (estilo de cursor): qualificar evita a ambiguidade.
                UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                UnityEngine.Cursor.visible = false;
            }

            StartGestureReceived?.Invoke(entry.SlotId);
        }

        void OnDirectorStateChanged(AppState state)
        {
            var inMenu = state == AppState.Boot || state == AppState.Dossier;
            menuScene.Apply(inMenu);
            if (state == AppState.Dossier)
                ReleaseCursor();

            AuditAudioListeners(state);
        }

        static void ReleaseCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        /// <summary>Menu e capítulo não podem ter dois AudioListener ativos. Só avisa quando DEBUG está definido (Editor e builds de desenvolvimento).</summary>
        [System.Diagnostics.Conditional("DEBUG")]
        void AuditAudioListeners(AppState state)
        {
            if (state != AppState.Playing && state != AppState.Dossier)
                return;

            var count = MenuSceneGate.CountEnabledAudioListeners();
            if (count > 1)
                Debug.LogWarning($"[DossierScreen] {count} AudioListener ativos no estado {state}. Liste o AudioListener e a câmera do menu em 'menuScene' para o dossiê desligá-los durante o capítulo.", this);
        }

        // ---------- Teclado (somente pelo InputRouter) ----------

        bool IsInteractive => bound && flow != null && flow.Phase == DossierPhase.Browsing && !flow.IsBusy;

        void OnPointerDown(PointerDownEvent evt)
        {
            keyboardMode = false;
            if (focusIndex < 0)
                return;

            focusIndex = -1;
            ApplyFocusVisual();
        }

        void OnNavigate(Vector2Int direction)
        {
            if (!IsInteractive)
                return;

            // Direita/baixo avançam; esquerda/cima voltam (cima é +1 no eixo Y da entrada).
            var delta = Math.Sign(direction.x - direction.y);
            if (delta == 0)
                return;

            keyboardMode = true;
            focusIndex = FocusRing.Step(focusIndex, focusTargets.Count, delta);
            ApplyFocusVisual();
        }

        void OnSubmit()
        {
            if (!IsInteractive || focusIndex < 0 || focusIndex >= focusTargets.Count)
                return;

            keyboardMode = true;
            var target = focusTargets[focusIndex];
            if (target.Button != null && target.Button.enabledSelf)
                target.Activate();
        }

        /// <summary>P/Esc: retorno ao arquivo (fecha o painel aberto).</summary>
        void OnPause()
        {
            if (IsInteractive)
                flow.Back();
        }

        void RebuildFocus()
        {
            focusTargets.Clear();
            if (flow.Phase == DossierPhase.Browsing && !flow.IsBusy)
            {
                if (flow.Panel == DossierPanel.None)
                {
                    foreach (var view in folderViews)
                    {
                        var slotId = view.Entry.SlotId;
                        if (view.Start.enabledSelf)
                            focusTargets.Add(new FocusTarget(view.Start, () => OnStartClicked(slotId)));
                        if (view.Details.enabledSelf)
                            focusTargets.Add(new FocusTarget(view.Details, () => OnOpenPanelClicked(DossierPanel.Details, slotId)));
                    }

                    if (settingsButton.enabledSelf)
                        focusTargets.Add(new FocusTarget(settingsButton, () => OnOpenPanelClicked(DossierPanel.Settings, null)));
                    if (creditsButton.enabledSelf)
                        focusTargets.Add(new FocusTarget(creditsButton, () => OnOpenPanelClicked(DossierPanel.Credits, null)));
                }
                else
                {
                    focusTargets.AddRange(modalFocus);
                }
            }

            if (flow.Panel != lastPanel || flow.PanelSlotId != lastPanelSlot)
            {
                lastPanel = flow.Panel;
                lastPanelSlot = flow.PanelSlotId;
                focusIndex = keyboardMode && focusTargets.Count > 0 ? 0 : -1;
            }
            else if (focusIndex >= focusTargets.Count)
            {
                focusIndex = -1;
            }

            ApplyFocusVisual();
        }

        void ApplyFocusVisual()
        {
            if (focusedButton != null)
                focusedButton.RemoveFromClassList(FocusClass);

            focusedButton = focusIndex >= 0 && focusIndex < focusTargets.Count ? focusTargets[focusIndex].Button : null;
            if (focusedButton != null)
                focusedButton.AddToClassList(FocusClass);
        }

        // ---------- Desenho ----------

        string T(string key) => texts.Get(key, flow.LanguageCode, key);

        string ChapterText(ChapterCatalog.Entry entry, string chapterKey, string slotField)
        {
            if (!string.IsNullOrEmpty(chapterKey) && texts.TryGet(chapterKey, flow.LanguageCode, out var resolved))
                return resolved;

            return T(DossierKeys.Slot(entry.SlotId, slotField));
        }

        static string TitleKey(ChapterCatalog.Entry entry) => entry.Chapter != null ? entry.Chapter.TitleKey : null;
        static string DescriptionKey(ChapterCatalog.Entry entry) => entry.Chapter != null ? entry.Chapter.DescriptionKey : null;
        static string QuestionKey(ChapterCatalog.Entry entry) => entry.Chapter != null ? entry.Chapter.QuestionKey : null;

        void Refresh()
        {
            if (!bound || flow == null)
                return;

            root.style.display = flow.Phase == DossierPhase.Hidden ? DisplayStyle.None : DisplayStyle.Flex;

            headerStamp.text = T(DossierKeys.Stamp);
            gameTitle.text = T(DossierKeys.Title);
            lede.text = T(DossierKeys.Lede);
            disclaimer.text = T(DossierKeys.Disclaimer);
            languageLabel.text = T(DossierKeys.LanguageLabel);
            languagePt.text = "PT";
            languagePt.tooltip = T(DossierKeys.LanguagePtName);
            languageEn.text = "EN";
            languageEn.tooltip = T(DossierKeys.LanguageEnName);
            settingsButton.text = T(DossierKeys.Settings);
            creditsButton.text = T(DossierKeys.Credits);

            var interactive = flow.Phase == DossierPhase.Browsing && !flow.IsBusy;
            languagePt.EnableInClassList(LanguageActiveClass, flow.LanguageCode == DossierFlow.Portuguese);
            languageEn.EnableInClassList(LanguageActiveClass, flow.LanguageCode == DossierFlow.English);
            languagePt.SetEnabled(!flow.IsBusy);
            languageEn.SetEnabled(!flow.IsBusy);
            settingsButton.SetEnabled(interactive);
            creditsButton.SetEnabled(interactive);

            var hasError = flow.ErrorMessage != null;
            errorBanner.text = hasError ? T(DossierKeys.ErrorLoad) : string.Empty;
            errorBanner.EnableInClassList(ErrorVisibleClass, hasError);

            for (var i = 0; i < folderViews.Count; i++)
                RefreshFolder(folderViews[i], i, interactive);

            RefreshModal();
            RefreshLoading();
            RebuildFocus();
        }

        void RefreshFolder(FolderView view, int index, bool interactive)
        {
            var entry = view.Entry;
            var chapter = entry.Chapter;
            view.Label.text = T(DossierKeys.ChapterWord) + " " + DossierFormat.Roman(index + 1);
            view.Title.text = ChapterText(entry, TitleKey(entry), DossierKeys.SlotTitle);
            view.Meta.text = ChapterText(entry, DescriptionKey(entry), DossierKeys.SlotMeta);
            view.Question.text = ChapterText(entry, QuestionKey(entry), DossierKeys.SlotQuestion);
            view.Duration.text = DossierFormat.Duration(chapter != null ? chapter.TargetDurationSeconds : 0f);
            // The archive cards show neither the period ("MARS · 2004–2018") nor the running time; the labels stay bound but hidden.
            view.Meta.style.display = DisplayStyle.None;
            view.Duration.style.display = DisplayStyle.None;

            var cover = chapter != null ? chapter.Cover : null;
            if (cover != null)
            {
                view.Photo.style.backgroundImage = new StyleBackground(cover);
                view.Photo.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                view.Photo.RemoveFromClassList("sa-photo--pending");
                view.PhotoCaption.text = entry.CoverCredit;
            }
            else
            {
                view.Photo.style.backgroundImage = StyleKeyword.Null;
                view.Photo.style.backgroundSize = StyleKeyword.Null;
                view.Photo.AddToClassList("sa-photo--pending");
                view.PhotoCaption.text = T(DossierKeys.PhotoPending);
            }

            var available = entry.IsAvailable;
            view.Status.text = T(DossierKeys.Unavailable);
            view.Status.EnableInClassList(StatusVisibleClass, !available);

            view.Start.text = T(DossierKeys.Start);
            view.Start.SetEnabled(flow.CanStart(entry.SlotId) == StartResult.Accepted);
            view.Details.text = T(DossierKeys.Details);
            view.Details.SetEnabled(interactive && flow.Panel == DossierPanel.None);
        }

        // ---------- Painel sobreposto (detalhes, configurações, créditos) ----------

        void RefreshModal()
        {
            var open = flow.Phase == DossierPhase.Browsing && flow.Panel != DossierPanel.None;
            modal.EnableInClassList(OpenClass, open);
            modalFocus.Clear();
            modalBody.contentContainer.Clear();
            modalActions.Clear();
            SetAccent(modalCard, ref accentOnModal, null);
            if (!open)
                return;

            switch (flow.Panel)
            {
                case DossierPanel.Details:
                    BuildDetails();
                    break;
                case DossierPanel.Settings:
                    BuildSettings();
                    break;
                case DossierPanel.Credits:
                    BuildCredits();
                    break;
            }
        }

        static void SetAccent(VisualElement element, ref string current, string accentClass)
        {
            if (current == accentClass)
                return;

            if (current != null)
                element.RemoveFromClassList(current);
            if (accentClass != null)
                element.AddToClassList(accentClass);
            current = accentClass;
        }

        void BuildDetails()
        {
            var entry = catalog.Find(flow.PanelSlotId);
            if (entry == null)
                return;

            SetAccent(modalCard, ref accentOnModal, entry.AccentClass);
            modalStamp.text = T(DossierKeys.DetailsStamp);
            modalTitle.text = ChapterText(entry, TitleKey(entry), DossierKeys.SlotTitle);

            AddBodyLabel(ChapterText(entry, QuestionKey(entry), DossierKeys.SlotQuestion), "sa-modal__question");
            AddBodyLabel(T(DossierKeys.DetailsTodo), "sa-stamp", "sa-modal__section");
            AddBodyLabel(T(DossierKeys.Slot(entry.SlotId, DossierKeys.SlotDetail)), "sa-modal__text");

            var slotId = entry.SlotId;
            if (flow.CanStart(slotId) == StartResult.Accepted)
                AddActionButton(T(DossierKeys.Start), () => OnStartClicked(slotId), "sa-button--primary");
            else if (!entry.IsAvailable)
                AddBodyLabel(T(DossierKeys.Unavailable), "sa-stamp", "sa-modal__section");

            AddActionButton(T(DossierKeys.Back), OnBackClicked, "sa-button--secondary");
        }

        void BuildSettings()
        {
            modalStamp.text = T(DossierKeys.Stamp);
            modalTitle.text = T(DossierKeys.SettingsTitle);

            var data = settings.Current;
            AddVolumeRow(DossierKeys.SettingsMaster, data.masterVolume, (s, v) => s.masterVolume = v, s => s.masterVolume);
            AddVolumeRow(DossierKeys.SettingsMusic, data.musicVolume, (s, v) => s.musicVolume = v, s => s.musicVolume);
            AddVolumeRow(DossierKeys.SettingsSfx, data.sfxVolume, (s, v) => s.sfxVolume = v, s => s.sfxVolume);
            AddToggleRow(DossierKeys.SettingsMotion, data.reducedMotion,
                s => s.reducedMotion = !s.reducedMotion);
            AddChoiceRow(DossierKeys.SettingsHold,
                data.holdMode == HoldMode.Toggle ? T(DossierKeys.SettingsHoldToggle) : T(DossierKeys.SettingsHoldHold),
                s => s.holdMode = s.holdMode == HoldMode.Toggle ? HoldMode.Hold : HoldMode.Toggle);
            AddToggleRow(DossierKeys.SettingsAssist, data.extraAssistance,
                s => s.extraAssistance = !s.extraAssistance);

            AddActionButton(T(DossierKeys.Back), OnBackClicked, "sa-button--primary");
        }

        void BuildCredits()
        {
            modalStamp.text = T(DossierKeys.Stamp);
            modalTitle.text = T(DossierKeys.CreditsTitle);
            foreach (var key in texts.KeysWithPrefix(DossierKeys.CreditsLinePrefix))
                AddBodyLabel(T(key), "sa-modal__text");

            AddActionButton(T(DossierKeys.Back), OnBackClicked, "sa-button--primary");
        }

        void AddBodyLabel(string text, params string[] classes)
        {
            var label = new Label(text);
            foreach (var className in classes)
                label.AddToClassList(className);

            modalBody.contentContainer.Add(label);
        }

        Button AddActionButton(string text, Action action, string styleClass)
        {
            var button = NewButton(text, action, styleClass);
            modalActions.Add(button);
            modalFocus.Add(new FocusTarget(button, action));
            return button;
        }

        static Button NewButton(string text, Action action, string styleClass)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("sa-button");
            button.AddToClassList(styleClass);
            return button;
        }

        VisualElement NewRow(string labelKey, out VisualElement controls)
        {
            var row = new VisualElement();
            row.AddToClassList("sa-row");
            var label = new Label(T(labelKey));
            label.AddToClassList("sa-row__label");
            controls = new VisualElement();
            controls.AddToClassList("sa-row__controls");
            row.Add(label);
            row.Add(controls);
            modalBody.contentContainer.Add(row);
            return row;
        }

        void AddFocusableToBody(VisualElement controls, Button button, Action action)
        {
            controls.Add(button);
            modalFocus.Add(new FocusTarget(button, action));
        }

        void AddVolumeRow(string labelKey, float value, Action<SettingsData, float> write, Func<SettingsData, float> read)
        {
            NewRow(labelKey, out var controls);

            Action decrease = () => settings.Apply(s => write(s, StepVolume(read(s), -VolumeStep)));
            Action increase = () => settings.Apply(s => write(s, StepVolume(read(s), VolumeStep)));

            var minus = NewButton("–", decrease, "sa-button--secondary");
            minus.AddToClassList("sa-step");
            minus.tooltip = T(DossierKeys.SettingsDecrease);
            var percent = new Label(Mathf.RoundToInt(value * 100f) + "%");
            percent.AddToClassList("sa-row__value");
            var plus = NewButton("+", increase, "sa-button--secondary");
            plus.AddToClassList("sa-step");
            plus.tooltip = T(DossierKeys.SettingsIncrease);

            controls.Add(minus);
            modalFocus.Add(new FocusTarget(minus, decrease));
            controls.Add(percent);
            controls.Add(plus);
            modalFocus.Add(new FocusTarget(plus, increase));
        }

        void AddToggleRow(string labelKey, bool isOn, Action<SettingsData> flip)
        {
            NewRow(labelKey, out var controls);
            Action action = () => settings.Apply(flip);
            var button = NewButton(isOn ? T(DossierKeys.SettingsOn) : T(DossierKeys.SettingsOff), action, "sa-button--secondary");
            button.EnableInClassList("sa-toggle--on", isOn);
            AddFocusableToBody(controls, button, action);
        }

        void AddChoiceRow(string labelKey, string currentLabel, Action<SettingsData> next)
        {
            NewRow(labelKey, out var controls);
            Action action = () => settings.Apply(next);
            var button = NewButton(currentLabel, action, "sa-button--secondary");
            AddFocusableToBody(controls, button, action);
        }

        static float StepVolume(float current, float delta) => Mathf.Clamp01(Mathf.Round((current + delta) * 10f) / 10f);

        // ---------- Carregamento ----------

        void RefreshLoading()
        {
            var loading = flow.Phase == DossierPhase.Loading;
            loadingOverlay.EnableInClassList(LoadingVisibleClass, loading);
            if (!loading)
            {
                StopLoadingAnimation();
                SetAccent(loadingOverlay, ref accentOnLoading, null);
                return;
            }

            var returning = director.State == AppState.Unloading;
            var chapter = !returning ? director.Current : null;
            var entry = chapter != null ? catalog.FindByChapterId(chapter.ChapterId) : null;

            SetAccent(loadingOverlay, ref accentOnLoading, entry != null ? entry.AccentClass : null);
            loadingStamp.text = T(DossierKeys.LoadingStamp);
            loadingTitle.text = returning ? T(DossierKeys.LoadingReturning) : T(DossierKeys.LoadingTitle);
            loadingChapter.text = entry != null ? ChapterText(entry, TitleKey(entry), DossierKeys.SlotTitle) : string.Empty;
            loadingNote.text = returning ? string.Empty : T(DossierKeys.LoadingNote);
            loadingHint.text = T(DossierKeys.LoadingHint);
            StartLoadingAnimation();
        }

        void StartLoadingAnimation()
        {
            if (loadingAnimating)
                return;

            loadingAnimating = true;
            var reduced = settings != null && settings.Current.reducedMotion;
            loadingFill.EnableInClassList(StaticFillClass, reduced);
            loadingFill.style.width = new Length(LoadingFillPercent, LengthUnit.Percent);
            loadingFill.style.left = new Length(0f, LengthUnit.Percent);
            loadingHint.RemoveFromClassList(HintVisibleClass);

            // Movimento reduzido: barra parada e só o texto indica o carregamento.
            if (!reduced)
                loadingTick = loadingFill.schedule.Execute(TickLoading).Every(LoadingTickMilliseconds);

            loadingHintTimer = loadingHint.schedule.Execute(ShowLoadingHint).StartingIn(LoadingHintDelayMilliseconds);
        }

        void StopLoadingAnimation()
        {
            loadingTick?.Pause();
            loadingHintTimer?.Pause();
            loadingTick = null;
            loadingHintTimer = null;
            loadingAnimating = false;
            loadingHint?.RemoveFromClassList(HintVisibleClass);
        }

        void TickLoading()
        {
            // Tempo real: o dossiê não depende de Time.timeScale nem do relógio da missão.
            var phase = Mathf.PingPong(Time.unscaledTime * 0.9f, 1f);
            loadingFill.style.left = new Length(phase * (100f - LoadingFillPercent), LengthUnit.Percent);
        }

        void ShowLoadingHint() => loadingHint.AddToClassList(HintVisibleClass);
    }
}
