using System;
using Abandoned.SpaceApps.UI;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Integration
{
    /// <summary>
    /// INT-001: os serviços únicos do aplicativo (um por sessão), entregues pelo <see cref="GameHost"/> às ligações de cada capítulo.
    /// Só agrupa referências; não cria regra de jogo.
    /// </summary>
    public sealed class ShellServices
    {
        public ChapterDirector Director;
        public SessionClock Clock;
        public InputRouter Router;
        public InteractionService Interactions;
        public EvidenceStore Evidence;
        public SettingsStore Settings;
        public LocalizationService Localization;
        public MissionHUD Hud;
        public SubtitlePresenter Subtitles;
        public AudioDirector Audio;

        /// <summary>Liga/desliga a pausa do Boot (menu e botão). O Surveyor traz a própria; duas pausas ouvindo o P se anulariam.</summary>
        public Action<bool> SetPauseSuspended = _ => { };

        public SettingsData Preferences => Settings.Current;
    }

    /// <summary>
    /// Ligação entre o <see cref="GameHost"/> e um capítulo carregado (mesmo papel do <c>MarsChapterBindings</c> para Marte). A cena do
    /// capítulo é do dono dela: esta ligação só injeta os serviços e solta tudo ao descarregar.
    /// </summary>
    public interface IChapterBindings
    {
        string ChapterId { get; }

        /// <summary>Antes do primeiro Start da cena (só o Surveyor precisa); lançar recusa o capítulo.</summary>
        void Prepare(Scene scene);

        /// <summary>Depois que a cena abriu e antes da primeira etapa. Devolve falso e registra o motivo se a cena não tem o que o capítulo exige.</summary>
        bool Bind();

        /// <summary>Solta ouvintes, legendas e HUD do capítulo; chamado antes de descarregar e a cada reinício. Seguro chamar mais de uma vez.</summary>
        void Unbind();

        /// <summary>Preferências mudaram durante o capítulo (idioma, tamanho de texto etc.).</summary>
        void OnSettingsChanged(SettingsData data);
    }
}
