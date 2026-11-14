using System;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Regras da pausa, sem interface: quem pode pausar, de onde, e como se retoma. P, Esc, o botão e a perda de foco
    /// chegam aqui; o <see cref="ChapterDirector"/> congela o relógio e troca o contexto de entrada para Paused.
    /// <para>
    /// Retomada deliberada: depois de perder o foco a pausa só termina por <see cref="Continue"/> (clique em Continuar).
    /// Enquanto isso P/Esc não retomam, porque o navegador pode ter entregue a tecla ao voltar à aba. Ao retomar, o
    /// <see cref="InputRouter"/> já zerou movimento e E (rearme): nada é reaplicado.
    /// </para>
    /// </summary>
    public sealed class PauseController : IDisposable
    {
        public const string MenuSource = "menu";
        public const string FocusSource = "focus";

        readonly ChapterDirector director;
        readonly InputRouter router;
        bool awaitingClick;
        bool disposed;

        public PauseController(ChapterDirector director, InputRouter router)
        {
            this.director = director != null ? director : throw new ArgumentNullException(nameof(director));
            this.router = router;
            director.PausedChanged += OnPausedChanged;
        }

        /// <summary>A pausa veio de perda de foco e ainda espera o clique em Continuar.</summary>
        public bool AwaitingClick => awaitingClick;

        public bool IsPaused => director.IsPaused;

        /// <summary>Só há o que pausar durante um capítulo em andamento.</summary>
        public bool CanPause => !disposed && director.State == AppState.Playing;

        /// <summary>Botão de pausa ou primeira pressão de P/Esc. Falso se não há capítulo em andamento.</summary>
        public bool RequestPause()
        {
            if (!CanPause)
                return false;

            director.Pause(MenuSource);
            return director.IsPaused;
        }

        /// <summary>P/Esc: pausa; já pausado pelo menu, retoma; pausado por foco, ignora (exige o clique).</summary>
        public bool KeyToggle()
        {
            if (!CanPause)
                return false;

            if (!director.IsPaused)
                return RequestPause();

            if (awaitingClick)
                return false;

            Continue();
            return true;
        }

        /// <summary>Aba/janela sem foco: solta teclas, ferramentas e E, e pausa se houver capítulo em andamento.</summary>
        public void FocusLost()
        {
            if (disposed)
                return;

            router?.NotifyFocusLost();
            if (!CanPause)
                return;

            awaitingClick = true;
            director.Pause(FocusSource);
        }

        /// <summary>Retomada deliberada (clique em Continuar). Remove as fontes de pausa deste controlador.</summary>
        public void Continue()
        {
            awaitingClick = false;
            director.Resume(FocusSource);
            director.Resume(MenuSource);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            director.PausedChanged -= OnPausedChanged;
        }

        void OnPausedChanged(bool paused)
        {
            if (!paused)
                awaitingClick = false;
        }
    }
}
