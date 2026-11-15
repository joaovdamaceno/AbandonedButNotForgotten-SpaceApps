using System;
using Abandoned.SpaceApps.UI;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Comportamento de foco do navegador (UI-006). Aba trocada, janela desfocada ou iframe sem foco: solta E/WASD e
    /// qualquer ferramenta ligada, pausa o capítulo em andamento e libera o cursor, se algo o tivesse capturado. Voltar ao
    /// foco NÃO retoma: só o clique em Continuar (<see cref="PauseController.Continue"/>), para que nenhuma tecla que o
    /// navegador entregue na volta seja reaplicada.
    /// <para>
    /// Também congela o áudio enquanto o jogo está pausado, por uma fonte própria no <see cref="AudioDirector"/>. O
    /// <c>AudioDirector</c> já pausa por foco, mas retoma ao voltar a ele; esta fonte mantém o som parado até a retomada
    /// deliberada. A interface continua sonora (o diretor só congela música, efeitos e ambiente).
    /// </para>
    /// Instalação (R1, INT-001): <c>Initialize(pauseMenu.Controller, director, audioDirector)</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FocusPause : MonoBehaviour
    {
        public const string AudioPauseSource = "pause";

        PauseController controller;
        ChapterDirector director;
        AudioDirector audioDirector;

        public bool IsInitialized => controller != null;

        public void Initialize(PauseController pauseController, ChapterDirector chapterDirector, AudioDirector audio = null)
        {
            Detach();
            controller = pauseController ?? throw new ArgumentNullException(nameof(pauseController));
            director = chapterDirector ?? throw new ArgumentNullException(nameof(chapterDirector));
            audioDirector = audio;
            director.PausedChanged += OnPausedChanged;
            OnPausedChanged(director.IsPaused);
        }

        void OnDestroy() => Detach();

        void Detach()
        {
            if (director != null)
                director.PausedChanged -= OnPausedChanged;

            audioDirector?.SetPaused(AudioPauseSource, false);
            controller = null;
            director = null;
            audioDirector = null;
        }

        // O Unity chama as duas ao trocar de aba ou desfocar o iframe; só a perda interessa. A volta nunca retoma.
        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                HandleFocusLost();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                HandleFocusLost();
        }

        /// <summary>Ponto único de perda de foco (também usado por testes e por hospedeiros de pré-visualização).</summary>
        public void HandleFocusLost()
        {
            if (controller == null)
                return;

            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None; // captura do cursor é opcional e nunca pode prender o menu

            controller.FocusLost();
        }

        void OnPausedChanged(bool paused) => audioDirector?.SetPaused(AudioPauseSource, paused);
    }
}
