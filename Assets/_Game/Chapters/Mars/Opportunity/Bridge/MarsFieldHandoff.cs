using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Abandoned.SpaceApps.Opportunity;

namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>
    /// Depois da abertura (M01–M03: Marte, as duas rovers no laboratório e o globo), o capítulo de Marte continua na cena <c>Opportunity</c>:
    /// o pouso das duas sondas, a ativação, a exploração, a ciência, o escudo térmico, 2018, a tempestade e o fim. Este componente esconde o palco
    /// e a interface da abertura, carrega a cena em modo aditivo (ela começa no pouso) e, quando a pessoa sai pela pausa ou pela tela final,
    /// devolve o controle ao arquivo.
    /// </summary>
    public sealed class MarsFieldHandoff : MonoBehaviour
    {
        public const string SceneName = "Opportunity";
        const string PreloadStep = "M01";
        const string LabStep = "M02";
        const string GlobeStep = "M03";
        const string HandoffStep = "M04";
        // A troca acontece um instante antes do fim do globo: a abertura congela no último quadro enquanto a cena ativa.
        const float HandoffLeadSeconds = 0.15f;

        AsyncOperation preload;
        ThreadPriority savedPriority = ThreadPriority.Normal;

        MarsChapterBindings bindings;
        Abandoned.SpaceApps.ChapterDirector appDirector;
        ChapterDirector field;
        Abandoned.SpaceApps.UI.PauseMenu shellPause;
        int pauseChangedFrame = -1;
        Scene openingScene;
        readonly List<Behaviour> disabled = new List<Behaviour>();
        readonly List<VisualElement> hiddenUi = new List<VisualElement>();
        bool started, ended, reachedEnd, frozen;
        MarsOpeningMusic openingMusic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("Mars · Opportunity");
            DontDestroyOnLoad(go);
            go.AddComponent<MarsFieldHandoff>();
        }

        void Update()
        {
            if (started)
            {
                Watch();
                return;
            }

            if (bindings == null)
            {
                bindings = FindAnyObjectByType<MarsChapterBindings>(FindObjectsInactive.Include);
                return;
            }

            var flow = bindings.Flow;
            var step = flow != null && flow.CurrentBinding != null ? flow.CurrentBinding.StepId : null;
            if (step == null)
                return;

            StartOpeningMusic();

            // A cena seguinte carrega em segundo plano durante o laboratório, para o pouso começar sem tela preta.
            if (preload == null && (step == PreloadStep || step == LabStep || step == GlobeStep))
            {
                // Prioridade baixa: o carregamento usa pouco tempo por quadro e não trava o laboratório nem o giro do globo.
                savedPriority = Application.backgroundLoadingPriority;
                Application.backgroundLoadingPriority = ThreadPriority.Low;
                preload = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
                if (preload != null)
                    preload.allowSceneActivation = false;
                Watch(FindAnyObjectByType<Abandoned.SpaceApps.ChapterDirector>());
            }

            var sequence = bindings.SequenceDirector;
            var globeEnding = step == GlobeStep && sequence != null && sequence.IsPlaying && sequence.Current != null
                && sequence.TimeSeconds >= sequence.Current.TotalDurationSeconds - HandoffLeadSeconds;
            if (!globeEnding && step != HandoffStep)
                return;

            if (globeEnding)
                sequence.Pause();
            frozen = globeEnding;
            started = true;
            if (openingMusic != null)
                openingMusic.FadeOutAndStop(2f);
            StartCoroutine(Begin());
        }

        void StartOpeningMusic()
        {
            if (openingMusic != null)
                return;
            var clip = Resources.Load<AudioClip>(MarsOpeningMusic.ClipResource);
            if (clip == null)
            {
                Debug.LogWarning("[Marte] Faixa da abertura ausente: " + MarsOpeningMusic.ClipResource);
                return;
            }

            var shellSettings = new Abandoned.SpaceApps.SettingsStore();
            shellSettings.Load();
            openingMusic = gameObject.AddComponent<MarsOpeningMusic>();
            var app = FindAnyObjectByType<Abandoned.SpaceApps.ChapterDirector>();
            var openingVoice = bindings.GetComponent<NarrationPlayer>();
            openingMusic.Begin(clip, shellSettings.Current.masterVolume, shellSettings.Current.musicVolume,
                () => app != null && app.IsPaused, () => openingVoice != null && openingVoice.IsBusy);
        }

        IEnumerator Begin()
        {
            // INT-001: Marte segue o idioma escolhido no dossiê, como os outros capítulos (inglês na primeira abertura).
            ApplyShellSettings();

            // Anota o que é da abertura antes de a cena seguinte criar a interface dela.
            var openingLayers = new List<VisualElement>();
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var root = document.rootVisualElement;
                if (root == null) continue;
                // Camadas soltas no painel (letreiros da abertura) também saem, não só a raiz do documento.
                var layers = root.panel != null ? root.panel.visualTree.Children() : new[] { root };
                foreach (var layer in layers)
                    // O menu de pausa do jogo continua: ele também serve à cena da Opportunity.
                    if (!openingLayers.Contains(layer) && layer.resolvedStyle.display != DisplayStyle.None && layer.Q("pause-root") == null)
                        openingLayers.Add(layer);
            }

            var openingLights = new List<Light>();
            openingScene = bindings.gameObject.scene;
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (light.gameObject.scene == openingScene) openingLights.Add(light);

            var load = preload ?? SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError($"[Marte] A cena '{SceneName}' não está nas Build Settings.");
                yield break;
            }

            load.allowSceneActivation = true;
            yield return load;
            Application.backgroundLoadingPriority = savedPriority;

            // A cena seguinte já está ativa: só agora a abertura sai, no mesmo quadro.
            Watch(FindAnyObjectByType<Abandoned.SpaceApps.ChapterDirector>());
            // O globo fica congelado (cancelar faria o fluxo da abertura seguir para o pouso dela).
            if (!frozen)
                bindings.SequenceDirector?.Cancel();
            foreach (var region in bindings.Regions)
                foreach (var root in region.roots)
                    if (root != null) root.SetActive(false);
            bindings.Rover.gameObject.SetActive(false);
            Disable(bindings.SceneCamera);
            if (bindings.SceneCamera != null) Disable(bindings.SceneCamera.GetComponent<AudioListener>());
            foreach (var light in openingLights)
                Disable(light);

            // A interface e a entrada da abertura saem de cena; a cena seguinte tem as suas.
            foreach (var layer in openingLayers)
            {
                layer.style.display = DisplayStyle.None;
                hiddenUi.Add(layer);
            }

            var router = FindAnyObjectByType<InputRouter>();
            if (router != null) router.Disable();

            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid()) SceneManager.SetActiveScene(scene);
            yield return null;
            yield return null;
            field = FindAnyObjectByType<ChapterDirector>();
            if (field == null)
            {
                Debug.LogError("[Marte] A cena Opportunity não tem diretor.");
                yield break;
            }

            field.ExitRequested += Finish;
            field.RestartRequested += Restart;

            // P, Esc e o botão de pausa da cena abrem o menu de pausa do jogo (o mesmo da abertura).
            shellPause = FindAnyObjectByType<Abandoned.SpaceApps.UI.PauseMenu>(FindObjectsInactive.Include);
            if (shellPause != null && shellPause.Controller != null && appDirector != null)
            {
                field.PauseMenuHandler = OnFieldPauseKey;
                appDirector.PausedChanged += OnAppPausedChanged;
                if (appDirector.IsPaused) OnAppPausedChanged(true);
            }
            else
                Debug.LogWarning("[Marte] Menu de pausa do jogo não encontrado; a cena da Opportunity usa o próprio menu.");
        }

        /// <summary>Pausa pela cena: só pede a pausa; com o menu aberto, P/Esc chegam ao menu pelo InputRouter.</summary>
        void OnFieldPauseKey()
        {
            if (shellPause == null || appDirector == null || appDirector.IsPaused || Time.frameCount == pauseChangedFrame) return;
            shellPause.Controller.RequestPause();
        }

        /// <summary>O menu do jogo pausa ou retoma: a cena acompanha e a entrada do menu liga só enquanto ele está aberto.</summary>
        void OnAppPausedChanged(bool paused)
        {
            pauseChangedFrame = Time.frameCount;
            if (field == null) return;
            var router = FindAnyObjectByType<InputRouter>();
            if (paused)
            {
                field.Pause(true);
                if (router != null) router.Enable();
            }
            else
            {
                if (router != null) router.Disable();
                ApplyShellSettings();
                field.Pause(false);
            }
        }

        /// <summary>Idioma, volumes, sensibilidade e movimento reduzido escolhidos no menu do jogo valem também na cena da Opportunity.</summary>
        static void ApplyShellSettings()
        {
            var store = new Abandoned.SpaceApps.SettingsStore();
            store.Load();
            var data = store.Current;
            GameSettings.English = true; // o jogo só tem inglês
            // Geral e música seguem direto os do aplicativo (1 e 0,8): os ganhos dos cues de música da cena foram calibrados para esse
            // padrão, de modo que Marte fique no mesmo nível da Pioneer e do Surveyor. Efeitos e interface mantêm o nível que Marte já tinha
            // com o geral de 0,8 da cena (0,8 x 0,65 e 0,8 x 0,6 nos padrões), agora com o geral em 1.
            GameSettings.Master = data.masterVolume;
            GameSettings.Music = data.musicVolume;
            GameSettings.Effects = .8f * .65f / .9f * data.sfxVolume;
            GameSettings.Interface = .8f * .6f / .9f * data.uiVolume;
            GameSettings.Sensitivity = data.lookSensitivity;
            GameSettings.ReducedMotion = data.reducedMotion;
        }

        void ReleasePauseMenu()
        {
            if (field != null) field.PauseMenuHandler = null;
            if (appDirector != null) appDirector.PausedChanged -= OnAppPausedChanged;
            shellPause = null;
        }

        void Watch(Abandoned.SpaceApps.ChapterDirector director)
        {
            if (director == null || appDirector == director) return;
            appDirector = director;
            appDirector.ChapterUnloading += OnAppUnloading;
        }

        void Disable(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled) return;
            behaviour.enabled = false;
            disabled.Add(behaviour);
        }

        void Watch()
        {
            if (field == null || ended) return;
            if (field.Stage == ChapterStage.ChapterEnd) reachedEnd = true;
        }

        /// <summary>Recomeçar volta ao início do capítulo (abertura): o capítulo de Marte é recarregado e esta cena sai junto.</summary>
        void Restart()
        {
            if (ended || appDirector == null) return;
            field.ExitRequested -= Finish;
            field.RestartRequested -= Restart;
            ReleasePauseMenu();
            appDirector.RestartChapter();
        }

        void Finish()
        {
            if (ended) return;
            if (field != null) { field.ExitRequested -= Finish; field.RestartRequested -= Restart; }
            ended = true;
            ReleasePauseMenu();
            StartCoroutine(Close(returnToArchive: true));
        }

        void OnAppUnloading(string chapterId)
        {
            if (!ended)
            {
                ended = true;
                ReleasePauseMenu();
                StartCoroutine(Close(returnToArchive: false));
            }
        }

        IEnumerator Close(bool returnToArchive)
        {
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            Application.backgroundLoadingPriority = savedPriority;
            // Saída durante a abertura: a cena pré-carregada termina de ativar para poder ser descarregada.
            if (preload != null && !preload.isDone)
            {
                preload.allowSceneActivation = true;
                yield return preload;
            }

            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);

            foreach (var behaviour in disabled)
                if (behaviour != null) behaviour.enabled = true;
            foreach (var element in hiddenUi)
                element.style.display = DisplayStyle.Flex;

            var router = FindAnyObjectByType<InputRouter>();
            if (router != null) router.Enable();
            if (appDirector != null)
            {
                appDirector.ChapterUnloading -= OnAppUnloading;
                if (returnToArchive)
                {
                    if (reachedEnd) appDirector.FinishChapter();
                    appDirector.ReturnToDossier();
                }
            }

            Destroy(gameObject);
            Boot();
        }
    }
}
