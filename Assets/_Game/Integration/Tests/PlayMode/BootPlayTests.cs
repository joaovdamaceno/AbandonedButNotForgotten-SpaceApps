using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.Integration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// INT-001: o Boot de verdade, com as cenas reais, em Play Mode. Abre cada capítulo (Pioneer 10, Marte, Surveyor 3) a partir do dossiê,
    /// avança algumas etapas, reinicia, volta ao dossiê e confere em cada estado a contagem de cenas carregadas, o único AudioListener ativo e
    /// que nenhum erro foi registrado no console (o Unity reprova o teste se um <c>Debug.LogError</c> aparecer) nem aviso de AudioListener.
    /// Escreve a medição em <c>QA/INT-001/playmode-*.json</c> quando <c>INT001_REPORT=1</c>. Não substitui o smoke no navegador.
    /// </summary>
    public class BootPlayTests
    {
        const float LoadTimeoutSeconds = 90f;

        [Serializable]
        sealed class Sample
        {
            public string label;
            public string state;
            public int sceneCount;
            public string scenes;
            public int audioListeners;
            public int evidence;
            public long managedBytes;
            public long totalAllocatedBytes;
        }

        [Serializable]
        sealed class Report
        {
            public string unity;
            public string note = "Medição do Editor em Play Mode (não é o navegador): contagem de cenas e ouvintes são válidas; memória é só referência.";
            public List<Sample> samples = new List<Sample>();
            public List<string> chapterIds = new List<string>();
        }

        GameHost host;
        ChapterDirector director;
        Report report;
        string originalLanguage;
        SettingsStore settings;
        readonly List<string> listenerWarnings = new List<string>();

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (message.Contains("audio listener"))
                listenerWarnings.Add(message);
        }

        IEnumerator OpenBoot()
        {
            report = new Report { unity = Application.unityVersion };
            listenerWarnings.Clear();
            Application.logMessageReceived += OnLog;
            var load = SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null, "Boot está nas Build Settings");
            yield return load;
            yield return null;
            yield return null;
            host = UnityEngine.Object.FindFirstObjectByType<GameHost>();
            Assert.That(host, Is.Not.Null);
            director = host.Director;
            Assert.That(director, Is.Not.Null, "GameHost iniciou");
            settings = host.Settings;
            originalLanguage = settings.Current.languageCode;
        }

        static int EnabledListeners() =>
            UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(l => l.enabled);

        static string LoadedScenes() =>
            string.Join(",", Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name));

        void Record(string label)
        {
            report.samples.Add(new Sample
            {
                label = label,
                state = director.State.ToString(),
                sceneCount = SceneManager.sceneCount,
                scenes = LoadedScenes(),
                audioListeners = EnabledListeners(),
                evidence = director.Evidence.Records.Count,
                managedBytes = GC.GetTotalMemory(false),
                totalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
            });
        }

        /// <summary>Boot + as cenas DAQUELE capítulo (Marte traz também a Opportunity, pré-carregada para a passagem); nenhuma cena repetida nem de outro capítulo.</summary>
        static void AssertChapterScenes(string id, string chapterScene, string moment)
        {
            var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).name).ToList();
            var allowed = new List<string> { "Boot", chapterScene };
            if (id == "mars")
                allowed.Add("Opportunity");
            Assert.That(loaded, Has.Member("Boot"), id + " " + moment);
            Assert.That(loaded, Has.Member(chapterScene), id + " " + moment + ": a cena do capítulo está carregada (" + string.Join(",", loaded) + ")");
            Assert.That(loaded.Except(allowed), Is.Empty, id + " " + moment + ": só Boot e as cenas do capítulo (" + string.Join(",", loaded) + ")");
            Assert.That(loaded.Count, Is.EqualTo(loaded.Distinct().Count()), id + " " + moment + ": nenhuma cena duplicada (" + string.Join(",", loaded) + ")");
        }

        IEnumerator WaitForState(AppState state, string what)
        {
            var until = Time.realtimeSinceStartup + LoadTimeoutSeconds;
            while (director.State != state && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.That(director.State, Is.EqualTo(state), what);
        }

        static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
                yield return null;
        }

        static IEnumerator Seconds(float seconds)
        {
            var until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        [TearDown]
        public void RestoreAndWriteReport()
        {
            Application.logMessageReceived -= OnLog;
            if (settings != null && originalLanguage != null && settings.Current.languageCode != originalLanguage)
                settings.Apply(data => data.languageCode = originalLanguage);

            if (report != null && report.samples.Count > 0 && Environment.GetEnvironmentVariable("INT001_REPORT") == "1")
            {
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "QA", "INT-001", "playmode-" + TestContext.CurrentContext.Test.Name + ".json"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }

            Assert.That(listenerWarnings, Is.Empty, "o Unity não pode avisar de AudioListener ausente ou duplicado");
        }

        [UnityTest]
        public IEnumerator DossierStartsWithOnlyBootAndOneListener()
        {
            yield return OpenBoot();
            yield return WaitForState(AppState.Dossier, "dossiê");
            Record("dossier-start");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1), "dossiê = só Boot: " + LoadedScenes());
            Assert.That(EnabledListeners(), Is.EqualTo(1), "um só AudioListener no dossiê");
            Assert.That(host.Catalog.Entries.Count(e => e.IsAvailable), Is.EqualTo(3), "três pastas disponíveis");
        }

        [UnityTest]
        public IEnumerator EachChapterOpensRestartsAndReturnsWithoutLeavingAWorldBehind()
        {
            yield return OpenBoot();
            yield return WaitForState(AppState.Dossier, "dossiê");
            Record("dossier-start");

            foreach (var id in new[] { "pioneer10", "mars", "surveyor3" })
            {
                var chapter = host.Catalog.Find(id).Chapter;
                report.chapterIds.Add(id);

                // Cliques rápidos: o mesmo pedido várias vezes no mesmo quadro abre a cena uma única vez.
                var started = 0;
                Action<string> count = chapterId => started++;
                director.ChapterStarted += count;
                director.Begin(chapter);
                director.Begin(chapter);
                director.Begin(chapter);
                yield return WaitForState(AppState.Playing, id + " abriu");
                yield return Frames(5);
                director.ChapterStarted -= count;
                Assert.That(started, Is.EqualTo(1), id + ": um só ChapterStarted");
                Record(id + "-playing");

                AssertChapterScenes(id, chapter.SceneName, "abriu");
                Assert.That(EnabledListeners(), Is.EqualTo(1), id + ": um só AudioListener ativo");

                yield return Seconds(2f);
                Assert.That(director.State, Is.EqualTo(AppState.Playing), id + " segue jogando");
                Assert.That(director.ActiveSeconds, Is.GreaterThan(0.5f), "o tempo ativo corre");

                // Reinício no meio: duas vezes, sem acumular cenas nem ouvintes.
                for (var restart = 1; restart <= 2; restart++)
                {
                    var steps = new List<string>();
                    Action<string> onStep = step => steps.Add(step);
                    director.StepEntered += onStep;
                    director.RestartChapter();
                    yield return WaitForState(AppState.Playing, id + " reiniciou");
                    yield return Frames(5);
                    director.StepEntered -= onStep;
                    Assert.That(steps.Count, Is.EqualTo(1), id + ": uma só etapa inicial depois do reinício " + restart + " (" + string.Join(",", steps) + ")");
                    Assert.That(steps[0], Is.EqualTo(chapter.InitialStepId));
                    AssertChapterScenes(id, chapter.SceneName, "reinício " + restart);
                    Assert.That(EnabledListeners(), Is.EqualTo(1), id + ": reinício " + restart);
                    Record(id + "-restart-" + restart);
                }

                // Volta ao dossiê no meio do capítulo.
                Assert.That(director.ReturnToDossier(), Is.True);
                yield return WaitForState(AppState.Dossier, id + " voltou ao dossiê");
                yield return Frames(5);
                Assert.That(SceneManager.sceneCount, Is.EqualTo(1), id + ": no dossiê só o Boot fica (" + LoadedScenes() + ")");
                Assert.That(EnabledListeners(), Is.EqualTo(1), id + ": um só AudioListener no dossiê");
                Assert.That(director.Evidence.Records, Is.Empty, "evidências limpas");
                Record(id + "-dossier");
            }
        }

        [UnityTest]
        public IEnumerator PioneerAdvancesThroughStatesAndSwitchesLanguageInTheMiddle()
        {
            yield return OpenBoot();
            yield return WaitForState(AppState.Dossier, "dossiê");
            director.Begin(host.Catalog.Find("pioneer10").Chapter);
            yield return WaitForState(AppState.Playing, "Pioneer abriu");
            yield return Seconds(1f);

            var visited = new List<string>();
            Action<string> onStep = step => visited.Add(step);
            director.StepEntered += onStep;
            for (var i = 0; i < 4 && director.State == AppState.Playing; i++)
            {
                director.CompleteStep(director.CurrentStepId);
                yield return Seconds(0.6f);
            }

            director.StepEntered -= onStep;
            Assert.That(visited.Count, Is.GreaterThanOrEqualTo(3), "avançou pelos estados: " + string.Join(",", visited));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(2));

            var step = director.CurrentStepId;
            var other = originalLanguage == SettingsData.English ? SettingsData.Portuguese : SettingsData.English;
            settings.Apply(data => data.languageCode = other);
            yield return Seconds(0.6f);
            Assert.That(director.State, Is.EqualTo(AppState.Playing), "trocar o idioma no meio não derruba o capítulo");
            Assert.That(director.CurrentStepId, Is.EqualTo(step), "o estado atual continua o mesmo");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(2));

            Assert.That(director.ReturnToDossier(), Is.True);
            yield return WaitForState(AppState.Dossier, "voltou");
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SurveyorDoesNotReplaceTheBootAndBringsItsOwnListener()
        {
            yield return OpenBoot();
            yield return WaitForState(AppState.Dossier, "dossiê");
            director.Begin(host.Catalog.Find("surveyor3").Chapter);
            yield return WaitForState(AppState.Playing, "Surveyor abriu");
            yield return Seconds(1.5f);

            Assert.That(SceneManager.GetSceneByName("Boot").isLoaded, Is.True, "o Boot continua carregado (o Surveyor não troca a cena inteira)");
            Assert.That(SceneManager.GetSceneByName("SurveyorArchive").IsValid(), Is.False, "o arquivo isolado do Surveyor não carrega dentro do Boot");
            Assert.That(UnityEngine.Object.FindObjectsByType<UI.PauseMenu>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length, Is.EqualTo(1),
                "um só menu de pausa ativo: o do Surveyor (o do Boot fica suspenso)");
            Assert.That(EnabledListeners(), Is.EqualTo(1));

            var steps = new List<string>();
            Action<string> onStep = s => steps.Add(s);
            director.StepEntered += onStep;
            // Pula só Prologue e Arrival (sequências de cinema). Pular a Activation por fora deixaria a câmera presa à sequência dela quando a
            // etapa Science pedisse o controle: é artefato do atalho de teste, no jogo a sequência termina e conclui a etapa.
            for (var i = 0; i < 2 && director.State == AppState.Playing; i++)
            {
                director.CompleteStep(director.CurrentStepId);
                yield return Seconds(0.8f);
            }

            director.StepEntered -= onStep;
            Assert.That(steps.Count, Is.EqualTo(2), "avançou: " + string.Join(",", steps));
            Assert.That(director.ReturnToDossier(), Is.True);
            yield return WaitForState(AppState.Dossier, "voltou");
            yield return Frames(5);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(EnabledListeners(), Is.EqualTo(1));
        }
    }
}
