using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Integration
{
    /// <summary>
    /// INT-001: liga a Pioneer 10 ao Boot sem editar a cena dela. Chama <see cref="PioneerChapterController.Initialize"/> com os serviços do
    /// aplicativo (diretor, relógio, entrada, interação, áudio global e idioma), liga a legenda global à <see cref="SequenceDirector"/> do
    /// capítulo e a câmera dele ao HUD. Ao descarregar, o <c>Teardown()</c> do controlador solta todas as atividades, a narração e os ouvintes.
    /// </summary>
    public sealed class PioneerBindings : IChapterBindings
    {
        readonly ShellServices shell;
        readonly IPioneerAudioPort audioPort;
        PioneerChapterController controller;
        string boundLanguage;

        public PioneerBindings(ShellServices services)
        {
            shell = services ?? throw new ArgumentNullException(nameof(services));
            audioPort = shell.Audio != null ? new PioneerAudioAdapter(shell.Audio) : null;
        }

        public string ChapterId => PioneerChapterController.ChapterId;
        public PioneerChapterController Controller => controller;

        public void Prepare(Scene scene)
        {
        }

        public bool Bind()
        {
            Unbind();
            controller = UnityEngine.Object.FindFirstObjectByType<PioneerChapterController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("[GameHost] A cena Pioneer10 não tem PioneerChapterController.");
                return false;
            }

            Initialize();
            // A legenda se liga à sequência ANTES da primeira etapa: o controlador já foi inicializado, mas a primeira etapa só vem depois.
            shell.Subtitles.Bind(controller.Sequences);
            shell.Hud.Initialize(shell.Interactions, shell.Router, shell.Director, shell.Settings, controller.Presentation.CameraDirector);
            return true;
        }

        void Initialize()
        {
            boundLanguage = PioneerData.Normalize(shell.Preferences.languageCode);
            controller.Initialize(shell.Director, shell.Clock, shell.Router, shell.Interactions, audioPort, boundLanguage);
        }

        public void OnSettingsChanged(SettingsData data)
        {
            // Os painéis da Pioneer recebem o texto na inicialização: trocar o idioma no meio refaz a ligação (o estado atual recomeça no novo idioma).
            if (controller == null || !controller.IsInitialized || PioneerData.Normalize(data.languageCode) == boundLanguage)
                return;

            Initialize();
        }

        public void Unbind()
        {
            if (controller == null)
                return;

            controller.Teardown();
            shell.Subtitles.Bind(null);
            shell.Hud.ClearObjective();
            shell.Hud.Initialize(shell.Interactions, shell.Router, shell.Director, shell.Settings, null);
            controller = null;
        }
    }
}
