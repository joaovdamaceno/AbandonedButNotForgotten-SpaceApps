using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.UI;
using UnityEngine;

namespace Abandoned.SpaceApps.Integration
{
    /// <summary>
    /// INT-001 (teste local): instala os serviços do aplicativo e liga a interface global a um capítulo carregado. Segue as instruções de
    /// instalação dos READMEs de UI/Core: dossiê, HUD, guia, pausa, legendas e áudio; o <see cref="ChapterDirector"/> carrega a cena do capítulo em
    /// modo aditivo e este componente liga os <c>bindings</c> de Marte quando ela abre. Não cria regras de jogo.
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

        public ChapterDirector Director => director;
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
            director.Initialize(clock, router, null, evidence);
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
            settings.Changed += data => localization.ApplySettings(data);

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
            gameObject.AddComponent<FocusPause>().Initialize(pause.Controller, director, audioDirector);

            dossier = Instantiate(dossierPrefab, transform).GetComponentInChildren<DossierScreen>(true);
            dossier.Initialize(director, settings, router, catalog);

            director.ChapterStarted += OnChapterStarted;
            director.ChapterUnloading += OnChapterUnloading;
            director.LoadFailed += message => Debug.LogError("[GameHost] " + message);
            director.OpenDossier();
        }

        void OnChapterStarted(string chapterId)
        {
            if (chapterId != "mars")
                return;

            mars = FindFirstObjectByType<MarsChapterBindings>(FindObjectsInactive.Include);
            if (mars == null)
            {
                Debug.LogError("[GameHost] A cena de Marte não tem MarsChapterBindings.");
                return;
            }

            mars.Initialize(director, clock, router, interactions, evidence, settings);
            subtitles.Bind(mars.SequenceDirector);
            hud.Initialize(interactions, router, director, settings, mars.CameraDirector);
            hud.SetReference(mars.Rover.transform);
            mars.Flow.CueEntered += subtitles.ShowCue;
            mars.Flow.CueExited += subtitles.HideCue;
            mars.Flow.StepStarted += OnMarsStep;
            mars.Flow.StepEnded += OnMarsStepEnded;
            guidance?.SetMovementProbe(() => mars != null ? mars.Rover.DrivenMeters : 0f);
            labels = new MarsLabelOverlay(hud.BottomColumn, localization.Resolver, mars);
        }

        void OnMarsStep(MarsChapterFlow.StepBinding binding)
        {
            // M07: o tutorial por ações (UI-005) mostra mover, olhar e E até cada um ser feito de verdade.
            if (binding.StepId == "M07" && guidance != null)
                guidance.BeginTutorial("mars.tutorial", new GuidanceOptions { InteractCandidateId = "mars.tutorial.inspect", HelpAvailable = true, CorridorAvailable = false }, force: true);

            if (string.IsNullOrEmpty(binding.StampKey))
                subtitles.ClearStamp();
            else
                subtitles.SetStamp(binding.StampKey);

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
