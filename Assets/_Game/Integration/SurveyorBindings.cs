using System;
using Abandoned.SpaceApps.Chapters.Surveyor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abandoned.SpaceApps.Integration
{
    /// <summary>
    /// INT-001: liga o Surveyor 3 ao Boot sem editar a cena dele. A cena <c>SurveyorChapter</c> contém o <see cref="SurveyorDocumentaryHost"/>,
    /// que sem serviços injetados antes do primeiro Start se trata como execução isolada e troca a cena inteira pelo arquivo do capítulo
    /// (<c>SurveyorArchive</c>), derrubando o Boot. Por isso <see cref="Prepare"/> roda no gancho do <see cref="ChapterSceneLoader"/>
    /// (antes do Start) e entrega diretor, relógio, entrada, interação e preferências do aplicativo.
    /// <para>
    /// O capítulo traz a própria câmera, <c>AudioListener</c>, <c>AudioDirector</c> (catálogo <c>DocumentaryAudio</c>), legendas e menu de pausa.
    /// Para ninguém ouvir o P duas vezes (as duas pausas se anulariam), a pausa do Boot fica suspensa enquanto o Surveyor roda.
    /// </para>
    /// </summary>
    public sealed class SurveyorBindings : IChapterBindings
    {
        public const string Id = "surveyor3";

        readonly ShellServices shell;
        SurveyorDocumentaryHost host;
        bool suspended;

        public SurveyorBindings(ShellServices services) => shell = services ?? throw new ArgumentNullException(nameof(services));

        public string ChapterId => Id;
        public SurveyorDocumentaryHost Host => host;

        public void Prepare(Scene scene)
        {
            host = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                host = root.GetComponentInChildren<SurveyorDocumentaryHost>(true);
                if (host != null)
                    break;
            }

            if (host == null)
                throw new InvalidOperationException("A cena " + scene.name + " não tem SurveyorDocumentaryHost.");

            host.Initialize(shell.Director, shell.Clock, shell.Router, shell.Interactions, shell.Settings);
        }

        public bool Bind()
        {
            if (host == null)
            {
                Debug.LogError("[GameHost] O Surveyor não foi preparado antes do Start.");
                return false;
            }

            if (!suspended)
            {
                suspended = true;
                shell.SetPauseSuspended(true);
            }

            return true;
        }

        public void OnSettingsChanged(SettingsData data)
        {
            // O host do Surveyor escuta o SettingsStore compartilhado e se atualiza sozinho.
        }

        public void Unbind()
        {
            if (suspended)
            {
                suspended = false;
                shell.SetPauseSuspended(false);
            }

            host = null;
        }
    }
}
