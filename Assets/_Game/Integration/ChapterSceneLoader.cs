using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Integration
{
    /// <summary>
    /// INT-001: carregador de cenas de capítulo do Boot. Faz o mesmo que o carregador padrão do <see cref="ChapterDirector"/>
    /// (cena aditiva, cena ativa, <c>Resources.UnloadUnusedAssets</c> ao descarregar) e acrescenta um gancho
    /// <see cref="BeforeStart"/> que roda <b>depois do Awake/OnEnable e antes do primeiro Start</b> da cena.
    /// <para>
    /// O gancho existe porque o Surveyor, se não receber os serviços do aplicativo antes do primeiro <c>Start</c>, trata a si mesmo como
    /// execução isolada e troca a cena inteira pelo arquivo próprio. Se o gancho lançar uma exceção, a cena é descarregada e o
    /// <see cref="ChapterDirector"/> recebe a falha (volta ao dossiê). Só uma cena de capítulo existe por vez: o diretor
    /// descarrega a anterior antes de carregar a próxima.
    /// </para>
    /// </summary>
    public sealed class ChapterSceneLoader : ChapterDirector.ISceneLoader
    {
        /// <summary>Recebe o nome da cena e a cena recém-carregada, antes de qualquer Start dela. Pode lançar para recusar o capítulo.</summary>
        public Action<string, Scene> BeforeStart;

        public void Load(string sceneName, Action onLoaded, Action<string> onFailed)
        {
            var prepared = false;
            string failure = null;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler = null;

            void Prepare(Scene scene)
            {
                if (prepared)
                    return;

                prepared = true;
                if (scene.IsValid())
                    SceneManager.SetActiveScene(scene);
                try
                {
                    BeforeStart?.Invoke(sceneName, scene);
                }
                catch (Exception exception)
                {
                    failure = exception.Message;
                    Debug.LogException(exception);
                }
            }

            handler = (scene, mode) =>
            {
                if (scene.name != sceneName || mode != LoadSceneMode.Additive)
                    return;

                SceneManager.sceneLoaded -= handler;
                Prepare(scene);
            };

            SceneManager.sceneLoaded += handler;
            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
            {
                SceneManager.sceneLoaded -= handler;
                onFailed?.Invoke("cena ausente das Build Settings");
                return;
            }

            operation.completed += _ =>
            {
                SceneManager.sceneLoaded -= handler;
                var scene = SceneManager.GetSceneByName(sceneName);
                Prepare(scene); // caminho de reserva: se o evento não veio antes, prepara agora
                if (failure != null)
                {
                    Unload(sceneName, () => onFailed?.Invoke(failure));
                    return;
                }

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
