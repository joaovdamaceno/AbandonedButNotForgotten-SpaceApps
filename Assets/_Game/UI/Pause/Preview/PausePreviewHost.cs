using UnityEngine;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// SÓ PARA TESTE MANUAL da UI-006 (cena PausePreview; fora do Build Settings, que é do R1). Liga o ChapterDirector real
    /// a um carregador falso, entra num capítulo de mentira e deixa um cubo andar com WASD enquanto o jogo não está
    /// pausado. Mostra na tela o estado de pausa e de entrada. Não é a integração (INT-001).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PausePreviewHost : MonoBehaviour
    {
        const float Speed = 4f;

        [SerializeField] PauseMenu pauseMenu;
        [SerializeField] Transform mover;
        [SerializeField] Renderer interactIndicator;

        SessionClock clock;
        InputRouter router;
        ChapterDirector director;
        SettingsStore settings;
        ChapterDefinition chapter;
        FocusPause focus;
        MaterialPropertyBlock block;
        bool started;
        float holdSeconds;

        sealed class InstantLoader : ChapterDirector.ISceneLoader
        {
            public void Load(string sceneName, System.Action onLoaded, System.Action<string> onFailed) => onLoaded();

            public void Unload(string sceneName, System.Action onUnloaded) => onUnloaded();
        }

        void Awake()
        {
            // Só vale quando a prévia é a cena ativa: carregada junto de outra cena, fica inerte.
            if (gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene())
            {
                if (pauseMenu != null) pauseMenu.gameObject.SetActive(false);
                gameObject.SetActive(false);
                return;
            }

            clock = gameObject.AddComponent<SessionClock>();
            router = gameObject.AddComponent<InputRouter>();
            director = gameObject.AddComponent<ChapterDirector>();
            director.Initialize(clock, router, new InstantLoader(), new EvidenceStore());

            settings = new SettingsStore();
            settings.Load();
            if (pauseMenu != null)
                pauseMenu.Initialize(director, router, settings);

            block = new MaterialPropertyBlock();
            chapter = ChapterDefinition.Create("preview", "preview.TITLE", "preview.DESC", "preview.Q", "PausePreview", 360f, "preview.intro", new[]
            {
                new ChapterStep("Intro", false, "End"),
                new ChapterStep("End", true),
            });
        }

        void Start()
        {
            director.OpenDossier();
            director.Begin(chapter);
            started = director.State == AppState.Playing;
        }

        void OnDestroy()
        {
            if (chapter != null)
                Destroy(chapter);
        }

        void Update()
        {
            // O FocusPause precisa do controlador, que o PauseMenu só cria quando liga a interface.
            if (focus == null && pauseMenu != null && pauseMenu.Controller != null)
            {
                focus = gameObject.AddComponent<FocusPause>();
                focus.Initialize(pauseMenu.Controller, director);
            }

            var dt = clock.ActiveDeltaSeconds; // zero na pausa
            if (mover != null)
                mover.position += new Vector3(router.Move.x, 0f, router.Move.y) * (Speed * dt);

            // E mantido "liga a ferramenta": o cubo fica vermelho. Depois de pausar ele não pode ligar sozinho.
            if (router.InteractHeld)
                holdSeconds += dt;
            else
                holdSeconds = 0f;

            if (interactIndicator != null)
            {
                block.SetColor("_BaseColor", router.InteractHeld ? Color.red : Color.gray);
                interactIndicator.SetPropertyBlock(block);
            }
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            GUI.Label(new Rect(12, 12, 640, 200),
                $"UI-006 · prévia da pausa\n" +
                $"Estado: {director.State}   Pausado: {director.IsPaused}   Aguardando clique (foco): {(pauseMenu != null && pauseMenu.Controller != null && pauseMenu.Controller.AwaitingClick)}\n" +
                $"Contexto de entrada: {router.Context}   Move: {router.Move}   E mantido: {router.InteractHeld} ({holdSeconds:0.0} s)\n" +
                $"Tempo ativo: {clock.ActiveSeconds:0.0} s{(started ? string.Empty : "   (capítulo não iniciou)")}\n" +
                "WASD move o cubo · segure E (cubo vermelho) · P/botão pausa", style);
        }
    }
}
