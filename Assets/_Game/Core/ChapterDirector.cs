using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps
{
    /// <summary>Estado do aplicativo. Pausa é uma camada sobre Playing (ver <see cref="ChapterDirector.IsPaused"/>).</summary>
    public enum AppState
    {
        Boot = 0,
        Dossier = 1,
        Loading = 2,
        Playing = 3,
        Completion = 4,
        Unloading = 5,
    }

    /// <summary>
    /// Fluxo Dossiê → Carregamento → Capítulo → Conclusão → Dossiê e implementação de <see cref="IChapterDirector"/>.
    /// <list type="bullet">
    /// <item>Qualquer capítulo começa sem outro concluído; nenhum estado de evidência cruza capítulos.</item>
    /// <item>O relógio ativo só corre do momento em que o capítulo efetivamente começa até a conclusão; dossiê,
    /// carregamento, conclusão e pausa não contam.</item>
    /// <item>Antes de qualquer troca (outro capítulo, reinício, volta ao dossiê) avisa <see cref="ChapterUnloading"/>
    /// para liberar RenderTextures/áudio, limpa evidências (destruindo fotos), zera o relógio e descarrega a cena.</item>
    /// <item>Etapas avançam só pela tabela explícita de <see cref="ChapterDefinition"/>; repetir um ID é idempotente.</item>
    /// <item>Pedidos durante Loading/Unloading são ignorados (cliques rápidos não abrem a cena duas vezes).</item>
    /// </list>
    /// </summary>
    public sealed class ChapterDirector : MonoBehaviour, IChapterDirector
    {
        /// <summary>Carrega/descarrega a cena do capítulo. A implementação padrão usa SceneManager (aditivo).</summary>
        public interface ISceneLoader
        {
            void Load(string sceneName, Action onLoaded, Action<string> onFailed);
            void Unload(string sceneName, Action onUnloaded);
        }

        [SerializeField] SessionClock sessionClock;
        [SerializeField, Tooltip("Opcional: Gameplay durante o capítulo, Paused durante a pausa.")] InputRouter inputRouter;

        readonly HashSet<string> completedSteps = new HashSet<string>();
        ISceneLoader sceneLoader;
        EvidenceStore evidence;
        AppState state = AppState.Boot;
        string loadedScene;
        int operationToken;
        IDisposable gameplayLease;
        IDisposable pauseLease;

        public AppState State => state;
        public ChapterDefinition Current { get; private set; }
        public string CurrentStepId { get; private set; }
        public bool IsFinished { get; private set; }
        public bool IsPaused => state == AppState.Playing && sessionClock != null && sessionClock.IsPaused;
        public bool CanBegin => state == AppState.Boot || state == AppState.Dossier || state == AppState.Completion;
        public SessionClock Clock => sessionClock;
        public EvidenceStore Evidence => evidence ??= new EvidenceStore();
        public float ActiveSeconds => sessionClock != null ? sessionClock.ActiveSeconds : 0f;

        /// <summary>Tempo ativo da última execução concluída ou interrompida (para QA-003).</summary>
        public float LastRunActiveSeconds { get; private set; }

        public event Action<AppState> StateChanged;
        public event Action<string> ChapterLoading;
        public event Action<string> ChapterStarted;

        /// <summary>Antes de descarregar: libere RenderTextures, fotos e fontes de áudio do capítulo. Argumento: chapterId.</summary>
        public event Action<string> ChapterUnloading;

        public event Action<string> LoadFailed;
        public event Action<bool> PausedChanged;
        public event Action<string> StepEntered;
        public event Action<string> ChapterFinished;

        public void Initialize(SessionClock clock, InputRouter router, ISceneLoader loader, EvidenceStore store)
        {
            sessionClock = clock;
            inputRouter = router;
            sceneLoader = loader;
            evidence = store;
        }

        void Awake()
        {
            sceneLoader ??= new UnitySceneLoader();
            evidence ??= new EvidenceStore();
        }

        // ---- Fluxo do aplicativo ----

        /// <summary>Boot concluído: mostra o arquivo.</summary>
        public void OpenDossier()
        {
            if (state == AppState.Boot)
                SetState(AppState.Dossier);
        }

        public void Begin(ChapterDefinition chapter)
        {
            if (chapter == null)
                throw new ArgumentNullException(nameof(chapter));

            var errors = chapter.Validate();
            if (errors.Count > 0)
                throw new ArgumentException("Capítulo inválido:" + ContractValidation.Describe(errors), nameof(chapter));

            if (!CanBegin)
            {
                Debug.LogWarning($"[ChapterDirector] Begin('{chapter.ChapterId}') ignorado no estado {state}.", this);
                return;
            }

            EnsureDependencies();
            UnloadCurrent(() => Load(chapter));
        }

        public void RestartChapter()
        {
            if (Current == null || state == AppState.Loading || state == AppState.Unloading)
                return;

            var chapter = Current;
            UnloadCurrent(() => Load(chapter));
        }

        /// <summary>Volta ao arquivo de qualquer ponto do capítulo (inclusive pausado). Ignorado durante carregamento.</summary>
        public bool ReturnToDossier()
        {
            if (state == AppState.Loading || state == AppState.Unloading || state == AppState.Dossier)
                return false;

            EnsureDependencies();
            UnloadCurrent(() =>
            {
                Current = null;
                SetState(AppState.Dossier);
            });
            return true;
        }

        public void Pause(string source)
        {
            if (state != AppState.Playing || sessionClock == null)
                return;

            var wasPaused = sessionClock.IsPaused;
            sessionClock.Pause(source);
            if (pauseLease == null && inputRouter != null)
                pauseLease = inputRouter.Acquire(InputContext.Paused, "ChapterDirector.Pause");
            if (!wasPaused)
                PausedChanged?.Invoke(true);
        }

        public void Resume(string source)
        {
            if (sessionClock == null || !sessionClock.IsPaused)
                return;

            sessionClock.Resume(source);
            if (sessionClock.IsPaused)
                return;

            pauseLease?.Dispose();
            pauseLease = null;
            PausedChanged?.Invoke(false);
        }

        // ---- IChapterDirector: etapas ----

        public bool CompleteStep(string stepId)
        {
            if (!CanComplete(stepId, out var step))
                return false;

            if (step.IsTerminal)
            {
                FinishChapter();
                return true;
            }

            if (step.NextStepIds.Count != 1)
                return false;

            Advance(stepId, step.NextStepIds[0]);
            return true;
        }

        public bool CompleteStep(string stepId, string nextStepId)
        {
            if (!CanComplete(stepId, out var step) || step.IsTerminal || !step.AllowsNext(nextStepId))
                return false;

            Advance(stepId, nextStepId);
            return true;
        }

        public void FinishChapter()
        {
            if (state != AppState.Playing || IsFinished || Current == null || !Current.IsTerminalStep(CurrentStepId))
                return;

            completedSteps.Add(CurrentStepId);
            IsFinished = true;
            CurrentStepId = null;
            var wasPaused = sessionClock.IsPaused;
            sessionClock.StopActive();
            LastRunActiveSeconds = sessionClock.ActiveSeconds;
            ReleaseInputLeases();
            sessionClock.ClearPauses();
            if (wasPaused)
                PausedChanged?.Invoke(false);
            SetState(AppState.Completion);
            ChapterFinished?.Invoke(Current.ChapterId);
        }

        bool CanComplete(string stepId, out ChapterStep step)
        {
            step = null;
            return state == AppState.Playing && Current != null && !IsFinished && stepId == CurrentStepId
                && !completedSteps.Contains(stepId) && Current.TryGetStep(stepId, out step);
        }

        void Advance(string fromStepId, string toStepId)
        {
            completedSteps.Add(fromStepId);
            EnterStep(toStepId);
        }

        void EnterStep(string stepId)
        {
            CurrentStepId = stepId;
            StepEntered?.Invoke(stepId);
        }

        // ---- Carregar / descarregar ----

        void EnsureDependencies()
        {
            if (sessionClock == null)
                throw new InvalidOperationException("ChapterDirector precisa de um SessionClock (Initialize ou Inspector).");

            sceneLoader ??= new UnitySceneLoader();
            evidence ??= new EvidenceStore();
        }

        void UnloadCurrent(Action then)
        {
            var token = ++operationToken;
            var chapterId = Current != null ? Current.ChapterId : null;
            if (Current != null && !IsFinished)
                LastRunActiveSeconds = sessionClock.ActiveSeconds;

            ResetSession();
            if (loadedScene == null)
            {
                then();
                return;
            }

            SetState(AppState.Unloading);
            ChapterUnloading?.Invoke(chapterId);
            var scene = loadedScene;
            loadedScene = null;
            sceneLoader.Unload(scene, () =>
            {
                if (token == operationToken)
                    then();
            });
        }

        void Load(ChapterDefinition chapter)
        {
            var token = ++operationToken;
            Current = chapter;
            SetState(AppState.Loading);
            ChapterLoading?.Invoke(chapter.ChapterId);
            sceneLoader.Load(chapter.SceneName,
                () =>
                {
                    if (token != operationToken)
                        return;

                    loadedScene = chapter.SceneName;
                    StartChapter(chapter);
                },
                error =>
                {
                    if (token != operationToken)
                        return;

                    Current = null;
                    SetState(AppState.Dossier);
                    LoadFailed?.Invoke($"Não foi possível carregar '{chapter.SceneName}': {error}");
                });
        }

        void StartChapter(ChapterDefinition chapter)
        {
            ResetSession();
            if (inputRouter != null)
                gameplayLease = inputRouter.Acquire(InputContext.Gameplay, "Chapter:" + chapter.ChapterId);

            SetState(AppState.Playing);
            sessionClock.StartActive(); // entrada efetiva: só agora o tempo da missão começa
            ChapterStarted?.Invoke(chapter.ChapterId);
            if (state == AppState.Playing && Current == chapter)
                EnterStep(chapter.InitialStepId);
        }

        /// <summary>Zera tudo que pertence a uma execução: relógio, pausas, evidências, etapas e contextos de entrada.</summary>
        void ResetSession()
        {
            var wasPaused = sessionClock != null && sessionClock.IsPaused;
            ReleaseInputLeases();
            sessionClock?.ResetActive();
            Evidence.Clear();
            completedSteps.Clear();
            CurrentStepId = null;
            IsFinished = false;
            if (wasPaused)
                PausedChanged?.Invoke(false);
        }

        void ReleaseInputLeases()
        {
            var pause = pauseLease;
            var gameplay = gameplayLease;
            pauseLease = null;
            gameplayLease = null;
            pause?.Dispose();
            gameplay?.Dispose();
        }

        void SetState(AppState next)
        {
            if (state == next)
                return;

            state = next;
            StateChanged?.Invoke(next);
        }

        void OnDestroy() => ReleaseInputLeases();

        /// <summary>Cena do capítulo em modo aditivo; ao descarregar, libera assets não usados (texturas, áudio).</summary>
        sealed class UnitySceneLoader : ISceneLoader
        {
            public void Load(string sceneName, Action onLoaded, Action<string> onFailed)
            {
                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    onFailed?.Invoke("cena ausente das Build Settings");
                    return;
                }

                operation.completed += _ =>
                {
                    var scene = SceneManager.GetSceneByName(sceneName);
                    if (scene.IsValid())
                        SceneManager.SetActiveScene(scene);
                    onLoaded?.Invoke();
                };
            }

            public void Unload(string sceneName, Action onUnloaded)
            {
                var scene = SceneManager.GetSceneByName(sceneName);
                var operation = scene.IsValid() && scene.isLoaded ? SceneManager.UnloadSceneAsync(scene) : null;
                if (operation == null)
                {
                    Resources.UnloadUnusedAssets();
                    onUnloaded?.Invoke();
                    return;
                }

                operation.completed += _ => Resources.UnloadUnusedAssets().completed += __ => onUnloaded?.Invoke();
            }
        }
    }
}
