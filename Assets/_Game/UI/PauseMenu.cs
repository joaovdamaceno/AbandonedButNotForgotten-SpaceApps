using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Pausa centralizada (UI-006): botão sempre visível durante o capítulo, menu centralizado com Continuar,
    /// Preferências, tela cheia opcional, Reiniciar e Voltar ao arquivo. P e o botão abrem a pausa; Esc também, quando o
    /// navegador não o consome. Quem decide o que pausa é o <see cref="PauseController"/>; este componente só apresenta.
    /// <para>
    /// Instalação (R1, INT-001): instancie o prefab <c>PauseMenu</c> no aplicativo e chame <see cref="Initialize"/> com o
    /// <c>ChapterDirector</c>, o <c>InputRouter</c>, o <c>SettingsStore</c> e, se houver, o <c>MissionHUD</c> (o botão vai
    /// para <c>RightColumn</c>, acima do medidor de energia). Passe <see cref="Controller"/> ao <c>FocusPause</c>.
    /// </para>
    /// <para>
    /// Tela cheia e captura do cursor são opcionais: nada depende deles e o cursor nunca é capturado aqui. A tela cheia
    /// só muda por clique (o navegador exige gesto).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PauseMenu : MonoBehaviour
    {
        const string FocusClass = "sa-pausemenu__focus";

        enum View
        {
            Main = 0,
            Settings = 1,
        }

        enum Confirmation
        {
            None = 0,
            Restart = 1,
            Dossier = 2,
        }

        sealed class Entry
        {
            public VisualElement Element;
            public Label Label;
            public Action Action;
        }

        [SerializeField] UIDocument document;
        [SerializeField, Tooltip("PauseTexts.json: textos PT/EN da pausa e das preferências.")] TextAsset uiTexts;

        readonly Dictionary<VisualElement, Entry> entries = new Dictionary<VisualElement, Entry>();
        readonly List<VisualElement> mainFocusables = new List<VisualElement>();
        readonly List<VisualElement> settingsFocusables = new List<VisualElement>();

        ChapterDirector director;
        InputRouter router;
        SettingsStore settings;
        MissionHUD hud;
        PauseController controller;
        SettingsPanel panel;
        DossierTextCatalog texts;
        string language = SettingsData.Portuguese;

        bool bound;
        bool open;
        View view;
        Confirmation confirmation;
        int focusIndex;

        VisualElement root;
        VisualElement corner;
        VisualElement pauseButton;
        VisualElement overlay;
        VisualElement mainView;
        VisualElement settingsView;
        ScrollView settingsScroll;
        Label buttonLabel;
        Label buttonKey;
        Label title;
        Label subtitle;
        Label hint;
        Label navigation;
        Label settingsTitle;
        Entry resume;
        Entry openSettings;
        Entry restart;
        Entry toDossier;
        Entry back;

        public bool IsBound => bound;

        /// <summary>O menu está na tela (o jogo está pausado e a pessoa vê as opções).</summary>
        public bool IsOpen => open;

        /// <summary>Regras da pausa; entregue ao <c>FocusPause</c> para que ambos usem o mesmo controlador.</summary>
        public PauseController Controller => controller;

        /// <summary>
        /// Método de instalação. Chamar de novo religa com as novas dependências. <paramref name="missionHud"/> é opcional:
        /// sem ele (ou antes de o HUD ligar) o botão fica no canto superior direito do próprio documento.
        /// </summary>
        public void Initialize(ChapterDirector chapterDirector, InputRouter inputRouter, SettingsStore settingsStore, MissionHUD missionHud = null)
        {
            director = chapterDirector ?? throw new ArgumentNullException(nameof(chapterDirector));
            router = inputRouter;
            settings = settingsStore;
            hud = missionHud;

            if (!isActiveAndEnabled)
                return;

            Unbind();
            TryBind();
        }

        /// <summary>Move o botão de pausa para a coluna direita do HUD (chamar depois que o HUD ligou).</summary>
        public void AttachToHud(MissionHUD missionHud)
        {
            hud = missionHud;
            if (!bound)
                return;

            var column = hud != null && hud.IsBound ? hud.RightColumn : null;
            pauseButton.RemoveFromHierarchy();
            if (column != null)
                column.Insert(0, pauseButton);
            else
                corner.Add(pauseButton);
        }

        void Reset() => document = GetComponent<UIDocument>();

        void OnEnable() => TryBind();

        void Start()
        {
            if (bound)
                return;

            TryBind();
            if (!bound)
                Debug.LogWarning("[PauseMenu] Não ligou: faltam o ChapterDirector (Initialize), os textos ou o UXML da pausa.", this);
        }

        void OnDisable() => Unbind();

        // ---------- Ligação ----------

        bool TryBind()
        {
            if (bound)
                return true;

            if (document == null)
                document = GetComponent<UIDocument>();
            if (document == null || director == null || uiTexts == null)
                return false;

            root = document.rootVisualElement?.Q<VisualElement>("pause-root");
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
            AttachToHud(hud);
            ApplyState();
            if (director.IsPaused)
                Show();

            return true;
        }

        void BindInterface()
        {
            corner = Require<VisualElement>("pause-corner");
            pauseButton = Require<VisualElement>("pause-button");
            overlay = Require<VisualElement>("pause-overlay");
            mainView = Require<VisualElement>("view-main");
            settingsView = Require<VisualElement>("view-settings");
            settingsScroll = Require<ScrollView>("settings-scroll");
            buttonLabel = Require<Label>("pause-button-label");
            buttonKey = Require<Label>("pause-button-key");
            title = Require<Label>("pause-title");
            subtitle = Require<Label>("pause-subtitle");
            hint = Require<Label>("pause-hint");
            navigation = Require<Label>("pause-navigation");
            settingsTitle = Require<Label>("settings-title");

            if (settings == null)
            {
                settings = new SettingsStore();
                settings.Load();
            }

            texts = new DossierTextCatalog();
            DossierTextTables.Load(uiTexts, texts);
            language = settings.Current.languageCode;
            SettingsPanel.ApplyEngineSettings(settings.Current);

            controller = new PauseController(director, router);
            panel = new SettingsPanel(settingsScroll.contentContainer, settings, texts);

            entries.Clear();
            resume = Wire("btn-resume", () => controller.Continue());
            openSettings = Wire("btn-settings", () => ShowView(View.Settings));
            restart = Wire("btn-restart", () => Confirmable(Confirmation.Restart, () => director.RestartChapter()));
            toDossier = Wire("btn-dossier", () => Confirmable(Confirmation.Dossier, () => director.ReturnToDossier()));
            back = Wire("btn-back", () => ShowView(View.Main));

            mainFocusables.Clear();
            mainFocusables.AddRange(new[] { resume.Element, openSettings.Element, restart.Element, toDossier.Element });

            pauseButton.AddManipulator(new Clickable(() => controller.RequestPause()));
            overlay.pickingMode = PickingMode.Position; // o fundo captura cliques: nada atrás reage com o menu aberto

            settings.Changed += OnSettingsChanged;
            director.PausedChanged += OnPausedChanged;
            director.StateChanged += OnStateChanged;
            director.ChapterUnloading += OnChapterUnloading;
            if (router != null)
            {
                router.PauseDown += OnPauseKey;
                router.NavigateDown += OnNavigate;
                router.SubmitDown += OnSubmit;
            }

            RefreshTexts();
        }

        void Unbind()
        {
            if (settings != null)
                settings.Changed -= OnSettingsChanged;
            if (director != null)
            {
                director.PausedChanged -= OnPausedChanged;
                director.StateChanged -= OnStateChanged;
                director.ChapterUnloading -= OnChapterUnloading;
            }

            if (router != null)
            {
                router.PauseDown -= OnPauseKey;
                router.NavigateDown -= OnNavigate;
                router.SubmitDown -= OnSubmit;
            }

            panel?.Dispose();
            panel = null;
            controller?.Dispose();
            controller = null;
            if (pauseButton != null && corner != null)
            {
                pauseButton.RemoveFromHierarchy();
                corner.Add(pauseButton);
            }

            open = false;
            bound = false;
            root = null;
        }

        T Require<T>(string elementName) where T : VisualElement
        {
            var element = root.Q<T>(elementName);
            if (element == null)
                throw new InvalidOperationException($"PauseMenu: o UXML não tem o elemento '{elementName}' ({typeof(T).Name}).");

            return element;
        }

        Entry Wire(string elementName, Action action)
        {
            var element = Require<VisualElement>(elementName);
            var entry = new Entry { Element = element, Label = element.Q<Label>(), Action = action };
            element.AddManipulator(new Clickable(action));
            entries[element] = entry;
            return entry;
        }

        // ---------- Eventos ----------

        void OnPauseKey()
        {
            if (!bound)
                return;

            // Esc/P dentro das preferências voltam ao menu; só no menu principal eles retomam.
            if (open && view == View.Settings)
            {
                ShowView(View.Main);
                return;
            }

            controller.KeyToggle();
        }

        void OnPausedChanged(bool paused)
        {
            if (!bound)
                return;

            if (paused)
                Show();
            else
                Hide();
        }

        void OnStateChanged(AppState state) => ApplyState();

        void OnChapterUnloading(string chapterId) => Hide();

        void OnSettingsChanged(SettingsData data)
        {
            language = data.languageCode;
            RefreshTexts();
        }

        void ApplyState()
        {
            var playing = director.State == AppState.Playing;
            pauseButton.style.display = playing ? DisplayStyle.Flex : DisplayStyle.None;
            if (!playing)
                Hide();
        }

        // ---------- Mostrar e esconder ----------

        void Show()
        {
            open = true;
            confirmation = Confirmation.None;
            view = View.Main;
            overlay.style.display = DisplayStyle.Flex;
            ApplyView();
            RefreshTexts();
            SetFocus(0);
        }

        void Hide()
        {
            if (!bound)
                return;

            open = false;
            confirmation = Confirmation.None;
            view = View.Main;
            overlay.style.display = DisplayStyle.None;
            ClearFocusMarks();
            ApplyView();
        }

        void ShowView(View next)
        {
            view = next;
            confirmation = Confirmation.None;
            if (next == View.Settings)
                panel.Refresh();

            ApplyView();
            RefreshTexts();
            SetFocus(0);
        }

        void ApplyView()
        {
            mainView.style.display = view == View.Main ? DisplayStyle.Flex : DisplayStyle.None;
            settingsView.style.display = view == View.Settings ? DisplayStyle.Flex : DisplayStyle.None;
            settingsFocusables.Clear();
            settingsFocusables.AddRange(panel.Focusables);
            settingsFocusables.Add(back.Element);
        }

        // ---------- Ações ----------

        void Confirmable(Confirmation kind, Action action)
        {
            if (confirmation != kind)
            {
                confirmation = kind;
                RefreshTexts();
                return;
            }

            confirmation = Confirmation.None;
            action();
        }

        // ---------- Teclado (via InputRouter; nenhuma tecla é lida aqui) ----------

        List<VisualElement> Current => view == View.Main ? mainFocusables : settingsFocusables;

        void OnNavigate(Vector2Int direction)
        {
            if (!open)
                return;

            var list = Current;
            if (direction.y != 0 && list.Count > 0)
                SetFocus((focusIndex - direction.y + list.Count) % list.Count);
            else if (direction.x != 0 && view == View.Settings && focusIndex < list.Count)
                panel.TryAdjust(list[focusIndex], direction.x);
        }

        void OnSubmit()
        {
            if (!open)
                return;

            var list = Current;
            if (focusIndex < 0 || focusIndex >= list.Count)
                return;

            var element = list[focusIndex];
            if (entries.TryGetValue(element, out var entry))
                entry.Action();
            else if (view == View.Settings)
                panel.TryActivate(element);
        }

        void SetFocus(int index)
        {
            ClearFocusMarks();
            var list = Current;
            if (list.Count == 0)
                return;

            focusIndex = Mathf.Clamp(index, 0, list.Count - 1);
            var element = list[focusIndex];
            element.AddToClassList(FocusClass);
            element.Focus();
            if (view == View.Settings && settingsScroll.contentContainer.Contains(element))
                settingsScroll.ScrollTo(element);
        }

        void ClearFocusMarks()
        {
            foreach (var element in mainFocusables)
                element.RemoveFromClassList(FocusClass);
            foreach (var element in settingsFocusables)
                element.RemoveFromClassList(FocusClass);
        }

        // ---------- Textos ----------

        void RefreshTexts()
        {
            if (!bound)
                return;

            buttonLabel.text = T(PauseKeys.Button);
            buttonKey.text = T(PauseKeys.KeyHint);
            title.text = T(PauseKeys.Title);
            subtitle.text = T(controller.AwaitingClick ? PauseKeys.SubtitleFocus : PauseKeys.SubtitleMenu);
            hint.text = controller.AwaitingClick ? T(PauseKeys.ResumeHint) : string.Empty;
            hint.style.display = controller.AwaitingClick ? DisplayStyle.Flex : DisplayStyle.None;
            navigation.text = T(PauseKeys.Navigation);
            settingsTitle.text = T(PauseKeys.SettingsTitle);

            resume.Label.text = T(PauseKeys.Resume);
            openSettings.Label.text = T(PauseKeys.Settings);
            restart.Label.text = T(confirmation == Confirmation.Restart ? PauseKeys.RestartConfirm : PauseKeys.Restart);
            toDossier.Label.text = T(confirmation == Confirmation.Dossier ? PauseKeys.DossierConfirm : PauseKeys.Dossier);
            back.Label.text = T(PauseKeys.Back);
        }

        string T(string key) => texts.Get(key, language, key);
    }
}
