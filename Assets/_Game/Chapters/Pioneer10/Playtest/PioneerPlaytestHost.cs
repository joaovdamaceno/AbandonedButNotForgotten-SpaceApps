using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Anfitrião de TESTE JOGÁVEL da Pioneer 10 (só para o Editor, não é a integração): faz o papel do shell que ainda não existe
    /// (INT-001). Cria os serviços REAIS do Core (<see cref="SessionClock"/>, <see cref="InputRouter"/>,
    /// <see cref="InteractionService"/>, <see cref="ChapterDirector"/>), carrega a cena <c>Pioneer10</c> em modo aditivo, chama
    /// <see cref="PioneerChapterController.Initialize"/> antes da primeira etapa e liga as legendas (UI-004). Sem áudio (nenhum clipe
    /// existe), sem dossiê, sem pausa/configurações finais: só o suficiente para jogar o capítulo de ponta a ponta.
    /// <para>
    /// Teclas: setas/WASD e mouse (sintonia e movimento), <b>E</b> (todas as ações), <b>P</b> ou <b>Esc</b> (pausa), <b>L</b> (idioma; reinicia
    /// o capítulo para os painéis também trocarem), <b>R</b> (reiniciar), <b>N</b> (pular o estado atual: ferramenta de teste, não é jogo).
    /// </para>
    /// </summary>
    public sealed class PioneerPlaytestHost : MonoBehaviour
    {
        public const string ScenePath = "Assets/_Game/Chapters/Pioneer10/Scenes/Pioneer10.unity";

        [SerializeField] ChapterDefinition chapter;
        [SerializeField, Tooltip("Tabelas JSON de texto PT/EN (Pioneer10Texts.json).")] TextAsset[] textTables;
        [SerializeField, Tooltip("Prefab SubtitleLayer (UI-004).")] GameObject subtitleLayer;
        [SerializeField, Tooltip("Prefab AudioDirector (AUD-002) com o catálogo de cues da música da Pioneer.")] GameObject audioDirectorPrefab;
        [SerializeField] string language = SettingsData.Portuguese;
        [SerializeField, Tooltip("Mostra o painel de teste (estado, tempo, teclas).")] bool showOverlay = true;

        SessionClock clock;
        InputRouter router;
        InteractionService interaction;
        ChapterDirector director;
        LocalizationService localization;
        SubtitlePresenter subtitles;
        AudioDirector audioDirector;
        IPioneerAudioPort audioPort;
        PioneerChapterController controller;
        string note = "Carregando o capítulo…";
        bool restartingForLanguage;
        bool archive;

        public PioneerChapterController Controller => controller;
        public ChapterDirector Director => director;
        public string Language => language;
        public bool IsInArchive => archive;
        public string Note => note;

        void Awake()
        {
            var services = new GameObject("PlaytestServices");
            services.transform.SetParent(transform, false);
            clock = services.AddComponent<SessionClock>();
            router = services.AddComponent<InputRouter>();
            interaction = services.AddComponent<InteractionService>();
            director = services.AddComponent<ChapterDirector>();
            interaction.Initialize(router, clock);
            director.Initialize(clock, router, CreateLoader(), new EvidenceStore());

            localization = new LocalizationService();
            foreach (var table in textTables ?? Array.Empty<TextAsset>())
            {
                if (table != null)
                    localization.AddTable(table.text, table.name);
            }

            localization.SetLanguage(language);
            if (audioDirectorPrefab != null)
            {
                audioDirector = Instantiate(audioDirectorPrefab, transform).GetComponent<AudioDirector>();
                audioDirector.Initialize(null, clock, new SettingsData(), null, true); // no Editor o áudio já está liberado
                audioDirector.BindInteraction(interaction);
                audioPort = new PioneerAudioAdapter(audioDirector);
            }

            director.ChapterStarted += OnChapterStarted;
            director.ChapterUnloading += OnChapterUnloading;
            director.LoadFailed += reason => note = "Falha ao carregar o capítulo: " + reason;
            router.PauseDown += TogglePause;
        }

        void Start()
        {
            if (chapter == null)
            {
                note = "PioneerChapter.asset não está ligado ao anfitrião.";
                return;
            }

            director.OpenDossier();
            director.Begin(chapter);
        }

        void OnChapterStarted(string id)
        {
            controller = FindFirstObjectByType<PioneerChapterController>();
            if (controller == null)
            {
                note = "A cena carregada não tem o PioneerChapterController.";
                return;
            }

            archive = false;
            controller.Initialize(director, clock, router, interaction, audioPort, language, ReturnToArchive);
            controller.ReturnToArchiveRequested += OnReturnRequested;
            if (subtitleLayer != null)
            {
                if (subtitles == null)
                    subtitles = Instantiate(subtitleLayer, transform).GetComponent<SubtitlePresenter>();
                subtitles.Initialize(localization, controller.Sequences);
            }

            note = restartingForLanguage ? "Idioma trocado." : string.Empty;
            restartingForLanguage = false;
        }

        void OnChapterUnloading(string id)
        {
            if (controller != null)
                controller.ReturnToArchiveRequested -= OnReturnRequested;
            subtitles?.Clear();
            controller = null;
        }

        bool ReturnToArchive()
        {
            archive = true;
            note = "Fim do capítulo. R recomeça; o arquivo (dossiê) é da integração.";
            return true;
        }

        void OnReturnRequested() => archive = true;

        void TogglePause()
        {
            if (director.IsPaused)
            {
                director.Resume("playtest");
                audioDirector?.SetPaused("playtest", false);
            }
            else
            {
                director.Pause("playtest");
                audioDirector?.SetPaused("playtest", true);
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.lKey.wasPressedThisFrame)
                ToggleLanguage();
            if (keyboard.rKey.wasPressedThisFrame)
                Restart();
            if (keyboard.nKey.wasPressedThisFrame)
                SkipState();
        }

        public void ToggleLanguage()
        {
            language = language == SettingsData.Portuguese ? SettingsData.English : SettingsData.Portuguese;
            localization.SetLanguage(language);
            restartingForLanguage = true;
            Restart(); // os painéis recebem o resolvedor na inicialização: reiniciar troca tudo junto
        }

        public void Restart()
        {
            if (director.State == AppState.Playing || director.State == AppState.Completion)
                director.RestartChapter();
            else if (director.CanBegin)
                director.Begin(chapter);
        }

        /// <summary>Pula o estado atual (ferramenta de teste): conclui a etapa e o capítulo segue para a próxima.</summary>
        public void SkipState()
        {
            if (controller != null && !string.IsNullOrEmpty(director.CurrentStepId))
                director.CompleteStep(director.CurrentStepId);
        }

        /// <summary>Salta para um estado da rota concluindo as etapas anteriores (ferramenta de teste).</summary>
        public void JumpTo(string stateId)
        {
            var from = Array.IndexOf(PioneerChapterController.StateIds, director.CurrentStepId);
            var to = Array.IndexOf(PioneerChapterController.StateIds, stateId);
            if (to < from)
            {
                note = "Só dá para avançar: aperte R para recomeçar e depois escolha o estado.";
                return;
            }

            var guard = 0;
            while (controller != null && director.CurrentStepId != stateId && !string.IsNullOrEmpty(director.CurrentStepId) && guard++ < 12)
                director.CompleteStep(director.CurrentStepId);
        }

        void OnGUI()
        {
            if (!showOverlay)
                return;

            var scale = Mathf.Max(1f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var box = new Rect(8, 8, 310, 236);
            GUI.Box(box, GUIContent.none);
            var state = controller != null ? controller.CurrentState : "—";
            GUI.Label(new Rect(16, 12, 296, 22), $"TESTE · Pioneer 10 · {(language == SettingsData.Portuguese ? "PT" : "EN")}");
            GUI.Label(new Rect(16, 32, 296, 22), $"Estado: {state}   ({(controller != null ? controller.CurrentStateSeconds : 0f):0.0} s)");
            GUI.Label(new Rect(16, 52, 296, 22), $"Tempo ativo: {director.ActiveSeconds:0} s   {(director.IsPaused ? "· PAUSADO" : string.Empty)}");
            GUI.Label(new Rect(16, 72, 296, 40), "Setas/WASD/mouse · E (todas as ações)\nP/Esc pausa · L idioma · R reiniciar · N pular");
            if (!string.IsNullOrEmpty(note))
                GUI.Label(new Rect(16, 130, 296, 40), note);

            var x = 16f;
            var y = 160f;
            foreach (var id in PioneerChapterController.StateIds)
            {
                var label = id.Substring(4, Mathf.Min(6, id.Length - 4));
                if (GUI.Button(new Rect(x, y, 70, 22), label))
                    JumpTo(id);
                x += 72f;
                if (x > 250f)
                {
                    x = 16f;
                    y += 24f;
                }
            }
        }

        void OnDestroy()
        {
            if (director != null)
            {
                director.ChapterStarted -= OnChapterStarted;
                director.ChapterUnloading -= OnChapterUnloading;
            }

            if (router != null)
                router.PauseDown -= TogglePause;
        }

        static ChapterDirector.ISceneLoader CreateLoader()
        {
#if UNITY_EDITOR
            return new EditorSceneLoader();
#else
            return null;
#endif
        }

#if UNITY_EDITOR
        /// <summary>Carrega a cena pelo CAMINHO, em modo aditivo, sem precisar das Build Settings (só no Editor).</summary>
        sealed class EditorSceneLoader : ChapterDirector.ISceneLoader
        {
            public void Load(string sceneName, Action onLoaded, Action<string> onFailed)
            {
                var loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                if (!loading.IsValid())
                {
                    onFailed?.Invoke("não foi possível abrir " + ScenePath);
                    return;
                }

                // Nesta versão da Unity a chamada devolve a cena (o carregamento termina nos próximos quadros): espera ficar carregada.
                void Poll()
                {
                    var scene = SceneManager.GetSceneByPath(ScenePath);
                    if (!scene.IsValid() || !scene.isLoaded)
                        return;

                    UnityEditor.EditorApplication.update -= Poll;
                    SceneManager.SetActiveScene(scene);
                    onLoaded?.Invoke();
                }

                UnityEditor.EditorApplication.update += Poll;
            }

            public void Unload(string sceneName, Action onUnloaded)
            {
                var scene = SceneManager.GetSceneByPath(ScenePath);
                var operation = scene.IsValid() && scene.isLoaded ? SceneManager.UnloadSceneAsync(scene) : null;
                if (operation == null)
                {
                    onUnloaded?.Invoke();
                    return;
                }

                operation.completed += _ => onUnloaded?.Invoke();
            }
        }
#endif
    }
}
