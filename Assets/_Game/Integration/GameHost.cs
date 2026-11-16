using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.UI;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Integration
{
    /// <summary>
    /// INT-001: instala os serviços do aplicativo e liga a interface global ao capítulo carregado. Segue as instruções de instalação dos
    /// READMEs de UI/Core: dossiê, HUD, guia, pausa, legendas e áudio. O <see cref="ChapterDirector"/> carrega a cena do capítulo em modo
    /// aditivo (<see cref="ChapterSceneLoader"/>, uma só cena de capítulo por vez) e este componente liga o capítulo que abriu: os
    /// <c>bindings</c> de Marte, a <see cref="PioneerBindings"/> e a <see cref="SurveyorBindings"/>. Não cria regras de jogo.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class GameHost : MonoBehaviour
    {
        [SerializeField] ChapterCatalog catalog;
        [SerializeField] GameObject dossierPrefab;
        [SerializeField] GameObject missionHudPrefab;
        [SerializeField, Tooltip("Camada de legenda própria (fora do HUD, que some nas cinemáticas).")] GameObject subtitleLayerPrefab;
        [SerializeField] GameObject guidancePrefab;
        [SerializeField] GameObject pauseMenuPrefab;
        [SerializeField] GameObject audioDirectorPrefab;
        [SerializeField] AudioCatalog audioCatalog;
        [SerializeField, Tooltip("Tabelas {texts:[{key,pt,en}]} de todos os capítulos (verbos, objetos, objetivos, legendas).")] TextAsset[] textTables;
        [SerializeField, Tooltip("Câmera de fundo do dossiê. O AudioListener dela só fica ligado quando nenhum capítulo traz o seu (um único ouvinte por estado).")] Camera bootCamera;

        readonly List<string> problems = new List<string>();
        SessionClock clock;
        InputRouter router;
        InteractionService interactions;
        EvidenceStore evidence;
        ChapterDirector director;
        SettingsStore settings;
        LocalizationService localization;
        MissionHUD hud;
        GuidanceController guidance;
        PauseMenu pause;
        DossierScreen dossier;
        AudioDirector audioDirector;
        SubtitlePresenter subtitles;
        MarsChapterBindings mars;
        MarsLabelOverlay labels;
        ShellServices shell;
        ChapterSceneLoader loader;
        AudioListener bootListener;
        FocusPause focusPause;
        IChapterBindings bindings;
        readonly Dictionary<string, IChapterBindings> chapterBindings = new Dictionary<string, IChapterBindings>();
        float listenerCheckAt;

        public ChapterDirector Director => director;
        public ChapterCatalog Catalog => catalog;
        public SettingsStore Settings => settings;
        public MissionHUD Hud => hud;
        public IReadOnlyList<string> Problems => problems;

        void Start()
        {
            clock = gameObject.AddComponent<SessionClock>();
            router = gameObject.AddComponent<InputRouter>();
            interactions = gameObject.AddComponent<InteractionService>();
            interactions.Initialize(router, clock);
            evidence = new EvidenceStore();
            director = gameObject.AddComponent<ChapterDirector>();
            loader = new ChapterSceneLoader { BeforeStart = PrepareChapterScene };
            director.Initialize(clock, router, loader, evidence);
            settings = new SettingsStore();
            settings.Load();

            localization = new LocalizationService();
            if (textTables != null)
                foreach (var table in textTables)
                    if (table != null)
                    {
                        problems.AddRange(localization.AddTable(table.text, table.name));
                        // Falas do roteiro (lista "cues") trazem o texto junto; registra para a legenda achar pela chave.
                        var cueTexts = CueTexts(table.text);
                        if (cueTexts != null)
                            problems.AddRange(localization.AddTable(cueTexts, table.name + " (falas)"));
                    }
            localization.ApplySettings(settings.Current);
            settings.Changed += OnSettingsChanged;

            if (audioDirectorPrefab != null)
            {
                audioDirector = Instantiate(audioDirectorPrefab, transform).GetComponentInChildren<AudioDirector>(true);
                audioDirector.Initialize(audioCatalog, clock, settings.Current);
                audioDirector.BindInteraction(interactions);
            }

            hud = Instantiate(missionHudPrefab, transform).GetComponentInChildren<MissionHUD>(true);
            if (textTables != null)
                foreach (var table in textTables)
                    if (table != null)
                        hud.AddTextTable(table);
            hud.Initialize(interactions, router, director, settings, null);

            // O roteiro aparece também nas cinemáticas: a legenda tem camada própria; sem o prefab, cai na coluna do HUD.
            if (subtitleLayerPrefab != null)
            {
                subtitles = Instantiate(subtitleLayerPrefab, transform).GetComponentInChildren<SubtitlePresenter>(true);
                // Acima do HUD (5), abaixo da pausa (8) e do dossiê (10): a legenda nunca cobre um menu.
                var subtitleDocument = subtitles.GetComponentInParent<UIDocument>();
                if (subtitleDocument != null)
                    subtitleDocument.sortingOrder = 6f;
                // Legenda de todos os capítulos que usam esta camada (Marte, Pioneer 10) no formato do Surveyor 3: faixa larga, fundo a 50%, texto creme.
                var host = subtitleDocument != null ? subtitleDocument.rootVisualElement?.Q("subtitle-host") : null;
                var style = Resources.Load<StyleSheet>("GameSubtitles");
                if (host != null && style != null)
                {
                    host.styleSheets.Add(style);
                    host.AddToClassList("game-subtitles");
                }
                subtitles.Initialize(localization, null);
            }
            else
            {
                subtitles = new GameObject("Subtitles").AddComponent<SubtitlePresenter>();
                subtitles.transform.SetParent(transform, false);
                subtitles.Initialize(localization, null, hud.BottomColumn);
            }

            if (guidancePrefab != null)
            {
                var root = Instantiate(guidancePrefab, transform);
                guidance = root.GetComponentInChildren<GuidanceController>(true);
                guidance.Initialize(router, interactions, clock, director, settings);
                root.GetComponentInChildren<TutorialPresenter>(true)?.Initialize(guidance, hud, router, director, settings);
            }

            pause = Instantiate(pauseMenuPrefab, transform).GetComponentInChildren<PauseMenu>(true);
            pause.Initialize(director, router, settings, hud);
            focusPause = gameObject.AddComponent<FocusPause>();
            focusPause.Initialize(pause.Controller, director, audioDirector);

            dossier = Instantiate(dossierPrefab, transform).GetComponentInChildren<DossierScreen>(true);
            dossier.Initialize(director, settings, router, catalog);

            shell = new ShellServices
            {
                Director = director,
                Clock = clock,
                Router = router,
                Interactions = interactions,
                Evidence = evidence,
                Settings = settings,
                Localization = localization,
                Hud = hud,
                Subtitles = subtitles,
                Audio = audioDirector,
                SetPauseSuspended = SetPauseSuspended,
            };
            Register(new PioneerBindings(shell));
            Register(new SurveyorBindings(shell));

            if (bootCamera != null)
                bootListener = bootCamera.GetComponent<AudioListener>();
            // Logo depois de qualquer cena abrir (inclusive a Opportunity, que Marte carrega sozinho) e antes do próximo quadro de áudio.
            SceneManager.sceneLoaded += OnSceneLoaded;
            director.StateChanged += OnStateChanged;
            director.ChapterStarted += OnChapterStarted;
            director.ChapterUnloading += OnChapterUnloading;
            director.LoadFailed += message => Debug.LogError("[GameHost] " + message);
            director.OpenDossier();
            ArbitrateAudioListener();
        }

        void Register(IChapterBindings entry) => chapterBindings[entry.ChapterId] = entry;

        /// <summary>Gancho do carregador: roda antes do primeiro Start da cena que abriu (o Surveyor precisa dos serviços antes dele).</summary>
        void PrepareChapterScene(string sceneName, Scene scene)
        {
            var chapter = director.Current;
            if (chapter != null && chapterBindings.TryGetValue(chapter.ChapterId, out var entry))
                entry.Prepare(scene);
        }

        void OnSettingsChanged(SettingsData data)
        {
            ApplyLanguage();
            bindings?.OnSettingsChanged(data);
        }

        /// <summary>O Boot suspende a própria pausa enquanto um capítulo traz outra (o P seria tratado duas vezes e se anularia).</summary>
        void SetPauseSuspended(bool value)
        {
            if (pause != null)
                pause.gameObject.SetActive(!value);
            if (focusPause != null)
                focusPause.enabled = !value;
        }

        void OnStateChanged(AppState state)
        {
            // A cena de Marte (já pré-carregada durante a abertura) ainda escreve o "geral" dela em AudioListener.volume até ser descarregada;
            // ao chegar no menu principal o volume global volta a 1 de vez.
            if (state == AppState.Dossier)
                AudioListener.volume = 1f;
            ArbitrateAudioListener();
        }

        /// <summary>
        /// Saída de um capítulo para o menu principal: nada continua tocando. O AudioDirector do shell (música, loops, efeitos, pausa de áudio e
        /// ducking) volta ao estado inicial na hora, e o volume global do AudioListener (que Marte altera para o "geral" dele) volta a 1.
        /// O áudio da cena do capítulo some junto com a cena descarregada.
        /// </summary>
        void StopChapterAudio()
        {
            audioDirector?.ResetState();
            AudioListener.volume = 1f;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ArbitrateAudioListener();

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        /// <summary>
        /// Antes de descarregar o capítulo: o ouvinte do Boot volta e os do capítulo (que vão ser destruídos) desligam no mesmo instante,
        /// para não haver um quadro sem ouvinte nem dois.
        /// </summary>
        void HandBackAudioListener()
        {
            if (bootListener == null)
                return;

            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (listener != bootListener)
                    listener.enabled = false;
            }

            bootListener.enabled = true;
        }

        void Update()
        {
            // Marte troca de câmera no meio do capítulo (abertura para Opportunity): reavalia o único AudioListener algumas vezes por segundo.
            if (Time.unscaledTime < listenerCheckAt)
                return;

            listenerCheckAt = Time.unscaledTime + 0.25f;
            ArbitrateAudioListener();
        }

        /// <summary>
        /// Um único AudioListener ativo por estado: o do Boot fica ligado no dossiê e nos capítulos que não trazem ouvinte (a abertura de
        /// Marte) e desliga assim que um capítulo ativa o seu.
        /// </summary>
        void ArbitrateAudioListener()
        {
            if (bootListener == null)
                return;

            var chapterHasListener = false;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (listener != bootListener && listener.enabled)
                {
                    chapterHasListener = true;
                    break;
                }
            }

            if (bootListener.enabled == chapterHasListener)
                bootListener.enabled = !chapterHasListener;
        }

        string runningChapter;

        /// <summary>Os três capítulos seguem o idioma escolhido nas configurações (inglês na primeira abertura).</summary>
        void ApplyLanguage() => localization.ApplySettings(settings.Current);

        void OnChapterStarted(string chapterId)
        {
            runningChapter = chapterId;
            ApplyLanguage();
            if (chapterBindings.TryGetValue(chapterId, out var entry))
            {
                bindings = entry;
                if (!entry.Bind())
                    bindings = null;
                ArbitrateAudioListener();
                return;
            }

            if (chapterId != "mars")
                return;

            mars = FindFirstObjectByType<MarsChapterBindings>(FindObjectsInactive.Include);
            if (mars == null)
            {
                Debug.LogError("[GameHost] A cena de Marte não tem MarsChapterBindings.");
                return;
            }

            // A legenda se liga ao diretor de sequência ANTES do fluxo: ao fim de uma sequência ela limpa primeiro, e só depois o fluxo
            // começa a próxima (senão a primeira fala da sequência seguinte seria apagada).
            subtitles.Bind(mars.SequenceDirector);
            mars.Initialize(director, clock, router, interactions, evidence, settings);
            hud.Initialize(interactions, router, director, settings, mars.CameraDirector);
            hud.SetReference(mars.Rover.transform);
            mars.Flow.CueEntered += subtitles.ShowCue;
            mars.Flow.CueExited += subtitles.HideCue;
            mars.Flow.StepStarted += OnMarsStep;
            mars.Flow.StepEnded += OnMarsStepEnded;
            guidance?.SetMovementProbe(() => mars != null ? mars.Rover.DrivenMeters : 0f);
            // Letreiros da abertura no sistema visual do jogo: título como carimbo da legenda e nomes do globo em placa (como as outras sondas).
            var subtitleHost = subtitles.GetComponentInParent<UIDocument>()?.rootVisualElement?.Q("subtitle-host");
            labels = new MarsLabelOverlay(hud.BottomColumn, localization.Resolver, mars, subtitleHost, key => { titleStamp = key; ApplyStamp(); });
            ArbitrateAudioListener();
        }

        string stepStamp, titleStamp;

        /// <summary>Carimbo da legenda: o letreiro de tela visível (título da cena) ou, sem ele, o carimbo da etapa.</summary>
        void ApplyStamp()
        {
            var key = titleStamp ?? stepStamp;
            if (string.IsNullOrEmpty(key))
                subtitles.ClearStamp();
            else
                subtitles.SetStamp(key);
        }

        void OnMarsStep(MarsChapterFlow.StepBinding binding)
        {
            // M07: o tutorial por ações (UI-005) mostra mover, olhar e E até cada um ser feito de verdade.
            if (binding.StepId == "M07" && guidance != null)
                guidance.BeginTutorial("mars.tutorial", new GuidanceOptions { InteractCandidateId = "mars.tutorial.inspect", HelpAvailable = true, CorridorAvailable = false }, force: true);

            stepStamp = binding.StampKey;
            ApplyStamp();

            if (binding.IsCinematic || string.IsNullOrEmpty(binding.ObjectiveKey))
            {
                hud.ClearObjective();
                return;
            }

            var anchor = mars.ObjectiveAnchor(binding.StepId);
            foreach (var goal in mars.Rover.ReachGoals)
            {
                if (anchor == null && goal != null && goal.stepId == binding.StepId && goal.target != null)
                {
                    anchor = goal.target;
                    break;
                }
            }

            hud.SetObjective(new HudObjective
            {
                Id = "mars.objective." + binding.StepId,
                StampKey = binding.StampKey,
                LabelKey = binding.ObjectiveKey,
                Anchor = anchor,
                AccentClass = "sa-chapter--mars",
            });
        }

        [Serializable] sealed class CueFile { public CueRow[] cues; }
        [Serializable] sealed class CueRow { public string textKey, pt, en; }
        [Serializable] sealed class TextFile { public TextRow[] texts; }
        [Serializable] sealed class TextRow { public string key, pt, en; }

        /// <summary>Tabela {texts} com o texto das falas de uma tabela {cues}; nulo se não houver falas.</summary>
        static string CueTexts(string json)
        {
            CueFile file;
            try { file = JsonUtility.FromJson<CueFile>(json); }
            catch (ArgumentException) { return null; }
            if (file?.cues == null || file.cues.Length == 0)
                return null;

            var rows = new List<TextRow>();
            foreach (var cue in file.cues)
                if (cue != null && !string.IsNullOrEmpty(cue.textKey) && !string.IsNullOrWhiteSpace(cue.pt) && !string.IsNullOrWhiteSpace(cue.en))
                    rows.Add(new TextRow { key = cue.textKey, pt = cue.pt, en = cue.en });
            return rows.Count == 0 ? null : JsonUtility.ToJson(new TextFile { texts = rows.ToArray() });
        }

        void OnMarsStepEnded(string stepId)
        {
            if (stepId == "M07")
                guidance?.End();
        }

        void OnChapterUnloading(string chapterId)
        {
            runningChapter = null;
            StopChapterAudio();
            ApplyLanguage();
            stepStamp = titleStamp = null;
            HandBackAudioListener();
            if (bindings != null)
            {
                bindings.Unbind();
                bindings = null;
            }

            if (mars == null)
                return;

            mars.Flow.CueEntered -= subtitles.ShowCue;
            mars.Flow.CueExited -= subtitles.HideCue;
            mars.Flow.StepStarted -= OnMarsStep;
            mars.Flow.StepEnded -= OnMarsStepEnded;
            guidance?.End();
            subtitles.Bind(null);
            labels?.Dispose();
            labels = null;
            hud.ClearObjective();
            mars.Teardown();
            mars = null;
        }
    }
}
