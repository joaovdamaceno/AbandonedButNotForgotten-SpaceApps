using System;
using UnityEngine;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Liga e desliga o que pertence só ao menu (câmera e AudioListener da cena Dossier, ambiente, qualquer
    /// AudioSource) para que menu e capítulo nunca tenham duas câmeras principais nem dois AudioListener ativos.
    /// A <c>DossierScreen</c> aplica <c>Apply(false)</c> assim que um capítulo começa a carregar e
    /// <c>Apply(true)</c> quando o arquivo volta. A interface UI Toolkit do dossiê não precisa de câmera.
    /// </summary>
    [Serializable]
    public sealed class MenuSceneGate
    {
        [SerializeField, Tooltip("Camera, AudioListener e AudioSource que só existem para o menu.")]
        Behaviour[] behaviours = new Behaviour[0];

        [SerializeField, Tooltip("Objetos inteiros que só existem para o menu.")]
        GameObject[] objects = new GameObject[0];

        public bool IsActive { get; private set; } = true;

        public int Count => (behaviours?.Length ?? 0) + (objects?.Length ?? 0);

        public static MenuSceneGate Create(Behaviour[] behaviours, GameObject[] objects) =>
            new MenuSceneGate { behaviours = behaviours ?? new Behaviour[0], objects = objects ?? new GameObject[0] };

        public void Apply(bool active)
        {
            IsActive = active;
            if (behaviours != null)
            {
                foreach (var behaviour in behaviours)
                {
                    if (behaviour != null)
                        behaviour.enabled = active;
                }
            }

            if (objects != null)
            {
                foreach (var gameObject in objects)
                {
                    if (gameObject != null)
                        gameObject.SetActive(active);
                }
            }
        }

        /// <summary>AudioListener realmente ativos. Mais de um é o sinal de menu e capítulo duplicando áudio.</summary>
        public static int CountEnabledAudioListeners()
        {
            var count = 0;
            foreach (var listener in UnityEngine.Object.FindObjectsByType<AudioListener>())
            {
                if (listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
                    count++;
            }

            return count;
        }
    }
}
