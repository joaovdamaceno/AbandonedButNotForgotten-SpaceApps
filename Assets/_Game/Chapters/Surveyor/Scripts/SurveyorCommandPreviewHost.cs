using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Banco de verificação local de SUR-004; não implementa a câmera de SUR-007 ou o dossiê global.</summary>
    public sealed class SurveyorCommandPreviewHost : MonoBehaviour, ChapterDirector.ISceneLoader
    {
        public const string PreviewStep = "PointMirror";
        [SerializeField] ChapterDirector owner;
        [SerializeField] SessionClock clock;
        [SerializeField] InputRouter router;
        [SerializeField] InteractionService interactions;
        [SerializeField] SurveyorCommandDelay queue;
        [SerializeField] SurveyorCommandInteraction action;
        [SerializeField] CommandTransitView view;
        [SerializeField] UIDocument document;
        [SerializeField] TextAsset textData;
        [SerializeField] ChapterDefinition definition;
        [SerializeField] Transform mirror;
        SurveyorLaunchTexts texts;
        SettingsStore settings;
        SettingsData current;
        VisualElement root, overlay;
        Button start, pause, back, left, right, language, resume, restartPaused, returnPaused;
        IDisposable inputLease;
        bool initialized;
        float desiredDegrees = 30;
        public ChapterDirector Owner => owner;
        public SessionClock Clock => clock;
        public InputRouter Router => router;
        public InteractionService Interactions => interactions;
        public SurveyorCommandDelay Queue => queue;
        public SurveyorCommandInteraction Action => action;
        public CommandTransitView View => view;
        public SettingsStore Settings => settings;
        public float DesiredDegrees => desiredDegrees;
        public float ActualDegrees => Mathf.DeltaAngle(0, mirror.localEulerAngles.y);

        public void Configure(ChapterDirector chapter, SessionClock session, InputRouter input, InteractionService service,
            SurveyorCommandDelay delay, SurveyorCommandInteraction interaction, CommandTransitView widget, UIDocument ui,
            TextAsset data, ChapterDefinition chapterData, Transform physicalMirror)
        {
            owner = chapter; clock = session; router = input; interactions = service; queue = delay; action = interaction;
            view = widget; document = ui; textData = data; definition = chapterData; mirror = physicalMirror;
        }
        void Start() => Initialize();
        public void Initialize(SettingsStore storage = null)
        {
            if (initialized) return;
            if (owner == null || clock == null || router == null || interactions == null || queue == null ||
                action == null || view == null || document == null || textData == null || definition == null || mirror == null)
                throw new InvalidOperationException("Referências do preview SUR-004 incompletas; reconstrua pelo builder.");
            settings = storage ?? new SettingsStore(); settings.Load(); current = settings.Current;
            texts = SurveyorLaunchTexts.Parse(textData);
            owner.Initialize(clock, router, this, new EvidenceStore()); router.Enable();
            interactions.Initialize(router, clock); interactions.Mode = current.holdMode;
            queue.Configure(owner, clock); action.Configure(queue, interactions, router);
            root = document.rootVisualElement; overlay = root.Q("pause-overlay");
            view.Configure(root.Q("command-widget"), textData, queue, action, current);
            start = root.Q<Button>("start"); pause = root.Q<Button>("pause"); back = root.Q<Button>("return");
            left = root.Q<Button>("left"); right = root.Q<Button>("right"); language = root.Q<Button>("language");
            resume = root.Q<Button>("resume"); restartPaused = root.Q<Button>("restart-paused"); returnPaused = root.Q<Button>("return-paused");
            start.clicked += BeginOrRestart; pause.clicked += Pause; back.clicked += Return;
            left.clicked += Left; right.clicked += Right; language.clicked += ToggleLanguage;
            resume.clicked += Resume; restartPaused.clicked += Restart; returnPaused.clicked += Return;
            owner.StepEntered += OnStep; owner.StateChanged += OnState; owner.PausedChanged += OnPaused;
            queue.Received += OnReceived; router.NavigateDown += Navigate; router.PauseDown += TogglePause;
            settings.Changed += ApplySettings; initialized = true;
            owner.OpenDossier(); Localize(); Refresh();
        }
        public void BeginOrRestart() { if (owner.CanBegin) owner.Begin(definition); else owner.RestartChapter(); }
        public void Restart() => owner.RestartChapter();
        public void Return() => owner.ReturnToDossier();
        public void Pause() => owner.Pause("command.menu");
        public void Resume() { owner.Resume("command.menu"); owner.Resume("command.focus"); owner.Resume("command.application"); }
        void TogglePause() { if (owner.IsPaused) Resume(); else Pause(); }
        void Left() => SetDesired(desiredDegrees - 10);
        void Right() => SetDesired(desiredDegrees + 10);
        void Navigate(Vector2Int value) { if (value.x != 0) SetDesired(desiredDegrees + value.x * 10); }
        public void SetDesired(float degrees)
        {
            if (!queue.OwnsState || queue.IsPaused) return;
            desiredDegrees = Mathf.Clamp(degrees, -60, 60);
            view.SetAim(desiredDegrees, ActualDegrees);
        }
        void OnStep(string id)
        {
            inputLease?.Dispose(); inputLease = null;
            if (id != PreviewStep) return;
            desiredDegrees = 30; mirror.localRotation = Quaternion.identity;
            queue.BeginState(id); inputLease = router.Acquire(InputContext.Panel, "SUR-004.Preview");
            Offer(); view.SetAim(desiredDegrees, ActualDegrees); Refresh();
        }
        void Offer()
        {
            action.Offer(new InteractionCandidate { Id = "mirror.point", VerbKey = "cmd.mirror", ObjectKey = "cmd.kicker",
                Available = true, HoldSeconds = 1.5f, Priority = 100, Anchor = mirror }, () =>
            {
                float captured = desiredDegrees;
                return () => { mirror.localRotation = Quaternion.Euler(0, captured, 0); view.SetAim(desiredDegrees, ActualDegrees); };
            });
        }
        void OnReceived(SurveyorCommandReceipt receipt) => Offer();
        void OnState(AppState state)
        {
            if (state != AppState.Playing) { inputLease?.Dispose(); inputLease = null; }
            Refresh();
        }
        void OnPaused(bool value) => Refresh();
        void OnApplicationFocus(bool focused) { if (initialized && !focused) owner.Pause("command.focus"); }
        void OnApplicationPause(bool paused) { if (initialized && paused) owner.Pause("command.application"); }
        public void ToggleLanguage() => settings.Apply(data => data.languageCode = data.languageCode == "pt" ? "en" : "pt");
        void ApplySettings(SettingsData data)
        {
            current = data; interactions.Mode = data.holdMode; view.ApplySettings(data); Localize(); Refresh();
        }
        void Localize()
        {
            string[] names = { "archive", "preview", "title", "description", "keys", "pause-title", "pause-description" };
            string[] keys = { "cmd.archive", "cmd.preview", "cmd.title", "cmd.description", "cmd.keys", "cmd.paused", "cmd.pause.description" };
            for (int i = 0; i < names.Length; i++) root.Q<Label>(names[i]).text = texts.Get(keys[i], current.languageCode);
            left.text = texts.Get("cmd.left", current.languageCode); right.text = texts.Get("cmd.right", current.languageCode);
            pause.text = texts.Get("cmd.pause", current.languageCode); back.text = texts.Get("cmd.return", current.languageCode);
            resume.text = texts.Get("cmd.resume", current.languageCode); restartPaused.text = texts.Get("cmd.restart", current.languageCode);
            returnPaused.text = texts.Get("cmd.return", current.languageCode); language.text = current.languageCode == "pt" ? "EN" : "PT";
        }
        void Refresh()
        {
            if (root == null) return;
            bool active = owner.State == AppState.Playing;
            start.text = texts.Get(active ? "cmd.restart" : "cmd.start", current.languageCode);
            left.SetEnabled(active && !owner.IsPaused); right.SetEnabled(active && !owner.IsPaused);
            pause.SetEnabled(active); back.SetEnabled(active);
            overlay.style.display = owner.IsPaused ? DisplayStyle.Flex : DisplayStyle.None;
        }
        void ChapterDirector.ISceneLoader.Load(string name, Action loaded, Action<string> failed)
        {
            var scene = gameObject.scene;
            if (scene.IsValid() && scene.isLoaded && scene.name == name) loaded();
            else failed("Abra a cena CommandDelayPreview para executar esta verificação local.");
        }
        void ChapterDirector.ISceneLoader.Unload(string name, Action unloaded) => unloaded();
        void OnDisable()
        {
            if (!initialized) return; initialized = false;
            start.clicked -= BeginOrRestart; pause.clicked -= Pause; back.clicked -= Return;
            left.clicked -= Left; right.clicked -= Right; language.clicked -= ToggleLanguage;
            resume.clicked -= Resume; restartPaused.clicked -= Restart; returnPaused.clicked -= Return;
            owner.StepEntered -= OnStep; owner.StateChanged -= OnState; owner.PausedChanged -= OnPaused;
            queue.Received -= OnReceived; router.NavigateDown -= Navigate; router.PauseDown -= TogglePause;
            settings.Changed -= ApplySettings;
            action.ClearAction(); queue.EndState(); inputLease?.Dispose(); inputLease = null; clock.StopActive();
        }
    }
}
