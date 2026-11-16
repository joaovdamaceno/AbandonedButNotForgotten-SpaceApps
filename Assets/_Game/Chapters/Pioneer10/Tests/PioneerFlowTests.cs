using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// PIO-014: a rota completa do capítulo com os SERVIÇOS REAIS do Core (ChapterDirector, SessionClock, InputRouter,
    /// InteractionService) e a cena Pioneer10 montada pelos builders. Um "operador" aperta e solta a tecla E de verdade pelo
    /// Input System; nenhum teste chama CompleteStep nem Completed por conta própria. A duração medida é do operador, não
    /// um resultado humano.
    /// </summary>
    public class PioneerFlowTests : InputTestFixture
    {
        sealed class InstantLoader : ChapterDirector.ISceneLoader
        {
            public int Loads, Unloads;
            /// <summary>Descartar a cena destrói o controlador: o gancho faz o que o OnDestroy faria.</summary>
            public Action BeforeUnload;

            public void Load(string sceneName, Action onLoaded, Action<string> onFailed)
            {
                Loads++;
                onLoaded();
            }

            public void Unload(string sceneName, Action onUnloaded)
            {
                Unloads++;
                BeforeUnload?.Invoke();
                onUnloaded();
            }
        }

        const float Frame = 0.05f;
        /// <summary>Ciclo do operador: E mantido 3,4 s, solto, toque de 0,1 s, solto. Serve para todos os estados (todos aceitam E mantido ou toque).</summary>
        const float OperatorCycleSeconds = 4f;

        Keyboard keyboard;
        GameObject host;
        SessionClock clock;
        InputRouter router;
        ChapterDirector director;
        InteractionService interaction;
        InstantLoader loader;
        PioneerChapterController controller;
        PioneerNarration narration;
        List<string> entered, completed;
        int finished, returns, replays;
        float operatorClock;
        bool eDown;

        PioneerLaunchActivity launch;
        BeltInvestigation belt;
        PioneerImaging imaging;
        ImagingRecovery recovery;
        HistoricalImageReveal reveal;
        PioneerPlaqueActivity plaque;
        OutboundAtlas outbound;
        PioneerListening listening;
        PioneerFarewellActivity farewell;

        static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            controller = Find<PioneerChapterController>();
            narration = Find<PioneerNarration>();
            launch = Find<PioneerLaunchActivity>();
            belt = Find<BeltInvestigation>();
            imaging = Find<PioneerImaging>();
            recovery = Find<ImagingRecovery>();
            reveal = Find<HistoricalImageReveal>();
            plaque = Find<PioneerPlaqueActivity>();
            outbound = Find<OutboundAtlas>();
            listening = Find<PioneerListening>();
            farewell = Find<PioneerFarewellActivity>();

            host = new GameObject("app");
            clock = host.AddComponent<SessionClock>();
            router = host.AddComponent<InputRouter>();
            interaction = host.AddComponent<InteractionService>();
            director = host.AddComponent<ChapterDirector>();
            loader = new InstantLoader();
            director.Initialize(clock, router, loader, new EvidenceStore());
            router.Enable();
            interaction.Initialize(router, clock);
            director.OpenDossier();

            entered = new List<string>();
            completed = new List<string>();
            finished = returns = replays = 0;
            operatorClock = 0f;
            eDown = false;
            controller.StateEntered += id => entered.Add(id);
            controller.StateCompleted += (id, seconds) => completed.Add(id);
            controller.ReturnToArchiveRequested += () => returns++;
            controller.ReplayRequested += () => replays++;
            loader.BeforeUnload = () => controller.Teardown();
            director.ChapterFinished += _ => finished++;
            // O que a R1 faz: inicializa o controlador quando a cena carrega, ANTES da primeira etapa.
            director.ChapterStarted += _ => controller.Initialize(director, clock, router, interaction);
        }

        public override void TearDown()
        {
            SetE(false);
            if (controller != null)
                controller.Teardown();
            Object.DestroyImmediate(host);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            base.TearDown();
        }

        void SetE(bool want)
        {
            if (want == eDown)
                return;

            eDown = want;
            if (want)
                Press(keyboard.eKey);
            else
                Release(keyboard.eKey);
        }

        /// <summary>Um quadro com o relógio ativo real: o SessionClock decide se o tempo corre (pausa = zero).</summary>
        void Step(float dt)
        {
            clock.Tick(dt);
            var active = clock.ActiveDeltaSeconds;
            controller.Sequences.Tick(active);
            interaction.Tick(active);
            launch.Tick(active);
            belt.Tick(active);
            imaging.Tick(active);
            recovery.Tick(active);
            reveal.Tick(active);
            plaque.Tick(active);
            outbound.Tick(active);
            listening.Tick(active);
            farewell.Tick(active);
            controller.Tick(active);
        }

        /// <summary>O operador mexe só no teclado: E mantido, solto, toque, solto. Não chama nenhuma API do capítulo.</summary>
        void OperatorStep()
        {
            var t = operatorClock % OperatorCycleSeconds;
            SetE(t < 3.4f || (t >= 3.6f && t < 3.7f));
            Step(Frame);
            operatorClock += Frame;
        }

        void OperateUntil(Func<bool> condition, float limitActiveSeconds = 900f)
        {
            var spent = 0f;
            while (!condition() && spent < limitActiveSeconds)
            {
                OperatorStep();
                spent += Frame;
            }

            SetE(false);
            Assert.That(condition(), Is.True, $"condição não alcançada em {limitActiveSeconds} s; estado {controller.CurrentState}, etapa {director.CurrentStepId}");
        }

        void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame)
                Step(Frame);
        }

        void BeginChapter()
        {
            director.Begin(controller.Chapter);
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
        }

        static readonly string[] StateIds = PioneerChapterController.StateIds;

        static readonly Dictionary<string, string> SequenceAssets = new Dictionary<string, string>
        {
            { "PIO.Launch", "PIO_Launch" }, { "PIO.Belt", "PIO_BeltCinema" }, { "PIO.JupiterApproach", "PIO_JupiterApproach" }, { "PIO.Imaging", "PIO_Imaging" },
            { "PIO.Closest", "PIO_Closest" }, { "PIO.Plaque", "PIO_Plaque" }, { "PIO.Outbound", "PIO_Outbound" }, { "PIO.Listening", "PIO_Listening" }, { "PIO.Farewell", "PIO_Farewell" },
        };

        // ---------- Montagem ----------

        [Test]
        public void TheChapterAssetListsTheEightStatesInTheScriptOrderWithTheDossierKeys()
        {
            var chapter = controller.Chapter;
            Assert.That(chapter.Validate(), Is.Empty);
            Assert.That(chapter.ChapterId, Is.EqualTo("pioneer10"), "igual ao slot do dossiê");
            Assert.That(chapter.SceneName, Is.EqualTo("Pioneer10"));
            Assert.That(chapter.TargetDurationSeconds, Is.EqualTo(427f));

            var order = new List<string>();
            var cursor = chapter.InitialStepId;
            while (cursor != null)
            {
                order.Add(cursor);
                Assert.That(chapter.TryGetStep(cursor, out var step), Is.True);
                cursor = step.IsTerminal ? null : step.NextStepIds.Single();
            }

            Assert.That(order, Is.EqualTo(StateIds), "a ordem vem da tabela de etapas, não de um enum");
            Assert.That(chapter.IsTerminalStep("PIO.Farewell"), Is.True);

            var dossier = File.ReadAllText("Assets/_Game/UI/Dossier/DossierTexts.json");
            foreach (var key in new[] { chapter.TitleKey, chapter.DescriptionKey, chapter.QuestionKey })
                Assert.That(dossier, Does.Contain("\"" + key + "\""), key + " existe nos textos do dossiê");

            var fresh = PioneerChapterBuilder.CreateChapter();
            Assert.That(fresh.InitialStepId, Is.EqualTo(chapter.InitialStepId));
            Object.DestroyImmediate(fresh);
        }

        [Test]
        public void EveryStateHasASequenceWithTheNominalDurationAndTheSceneIsFullyWired()
        {
            Assert.That(controller.ValidateSetup(), Is.Empty);
            var data = AssetDatabase.LoadAssetAtPath<PioneerData>(PioneerDataBuilder.DataAssetPath);
            var total = 0f;
            foreach (var state in StateIds)
            {
                var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>($"Assets/_Game/Chapters/Pioneer10/Sequences/{SequenceAssets[state]}.asset");
                Assert.That(sequence, Is.Not.Null, state);
                Assert.That(data.TryGetState(state, out var nominal), Is.True, state);
                Assert.That(sequence.TotalDurationSeconds, Is.EqualTo(nominal.nominalSeconds).Within(0.01f), state + ": duração do roteiro");
                total += sequence.TotalDurationSeconds;
            }

            Assert.That(total, Is.EqualTo(427f).Within(0.01f), "soma nominal do capítulo");
            Assert.That(controller.JupiterSequence.Cues.Select(c => c.CueId), Is.EqualTo(new[] { "PIO.03.01", "PIO.03.02", "PIO.03.03", "PIO.03.04" }));
            Assert.That(controller.ClosestSequence.Cues.Select(c => c.CueId), Is.EqualTo(new[] { "PIO.09.01", "PIO.09.08", "PIO.09.02", "PIO.09.03", "PIO.09.04", "PIO.09.05", "PIO.09.06", "PIO.09.09", "PIO.09.10", "PIO.09.07" }));
        }

        [Test]
        public void ThePreviewScenesAreNotInThePackageOrReferencedByTheChapterScene()
        {
            var scene = File.ReadAllText(PioneerStageBuilder.SceneAssetPath);
            Assert.That(scene, Does.Not.Contain("Probe Preview").And.Not.Contain("ProbePreview"), "a prévia da sonda é ferramenta de desenvolvimento");
            Assert.That(EditorBuildSettings.scenes.Select(s => s.path), Has.None.Contains("Preview"), "nenhuma cena de prévia nas Build Settings");
            Assert.That(controller.ValidateSetup(), Is.Empty);
        }

        // ---------- Rota ----------

        [Test]
        public void TheNineStatesPassInOrderEachOnceThenTheChapterFinishesOnce()
        {
            BeginChapter();
            Assert.That(controller.IsInitialized, Is.True);
            Assert.That(controller.CurrentState, Is.EqualTo("PIO.Launch"));

            OperateUntil(() => director.State != AppState.Playing || finished > 0);
            Assert.That(entered, Is.EqualTo(StateIds), "os nove estados, na ordem");
            Assert.That(completed, Is.EqualTo(StateIds), "cada estado conclui uma vez, pela própria atividade");
            Assert.That(finished, Is.EqualTo(1), "uma única conclusão do capítulo");
            Assert.That(director.State, Is.EqualTo(AppState.Completion), "o memorial é o estado Completion: não conta como tempo ativo");
            Assert.That(director.IsFinished, Is.True);
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Memorial), "o memorial fica até a escolha");
        }

        [Test]
        public void TheOperatorDurationIsMeasuredAsActiveTimePerStateAndNeverReportedAsAHumanResult()
        {
            BeginChapter();
            OperateUntil(() => finished > 0);

            var report = controller.LastReport;
            Assert.That(report.states, Is.EqualTo(StateIds));
            Assert.That(report.finished, Is.True);
            Assert.That(report.CountsAsHumanResult, Is.False, "duração do operador assistido não é resultado humano");
            Assert.That(report.ToString(), Does.Contain("operador").And.Contain("não é sessão humana"));
            Assert.That(report.activeSeconds.Sum(), Is.EqualTo(report.totalActiveSeconds).Within(0.05f));
            Assert.That(report.totalActiveSeconds, Is.EqualTo(clock.ActiveSeconds).Within(0.5f), "o relógio do controlador é o relógio ativo do SessionClock");
            for (var i = 0; i < StateIds.Length; i++)
            {
                var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>($"Assets/_Game/Chapters/Pioneer10/Sequences/{SequenceAssets[StateIds[i]]}.asset");
                // PIO.Farewell conclui aos 36 s, quando o memorial abre (os 2 s finais são do arquivo); os demais, no fim da sequência.
                var slack = StateIds[i] == "PIO.Farewell" ? 2.1f : 1f;
                Assert.That(report.activeSeconds[i], Is.GreaterThanOrEqualTo(sequence.TotalDurationSeconds - slack), StateIds[i] + ": a atividade usa a duração da sequência (uma fonte)");
            }

            Assert.That(director.LastRunActiveSeconds, Is.EqualTo(clock.ActiveSeconds).Within(0.5f));
        }

        [Test]
        public void NothingConcludesByTimeAloneAndPausingFreezesTheStateClock()
        {
            BeginChapter();
            Run(300f); // cinco minutos sem tocar em nada
            Assert.That(director.CurrentStepId, Is.EqualTo("PIO.Imaging"), "só cinema corre sozinho; a foto (interação 1) espera o jogador: o tempo não conclui nada");
            Assert.That(completed, Is.EqualTo(new[] { "PIO.Launch", "PIO.Belt", "PIO.JupiterApproach" }));
            Assert.That(entered, Is.EqualTo(new[] { "PIO.Launch", "PIO.Belt", "PIO.JupiterApproach", "PIO.Imaging" }));

            var before = controller.CurrentStateSeconds;
            director.Pause("test");
            Run(30f);
            Assert.That(controller.CurrentStateSeconds, Is.EqualTo(before).Within(1e-4f), "pausa: o tempo ativo do estado congela");
            director.Resume("test");
            Run(1f);
            Assert.That(controller.CurrentStateSeconds, Is.GreaterThan(before));
        }

        [Test]
        public void ReturnToTheArchiveFromTheMemorialLandsOnTheDossierWithEverythingReleased()
        {
            BeginChapter();
            OperateUntil(() => director.State == AppState.Dossier, 1200f);
            Assert.That((returns, replays), Is.EqualTo((1, 0)), "uma escolha: Voltar ao arquivo");
            Assert.That(director.State, Is.EqualTo(AppState.Dossier));
            Assert.That(finished, Is.EqualTo(1));
            Assert.That(loader.Unloads, Is.EqualTo(1), "o arquivo descarrega a cena do capítulo");
            Assert.That(router.DescribeLeases(), Is.Empty);
        }

        [Test]
        public void ReplayRestartsTheChapterThroughTheGlobalContractFromTheMemorial()
        {
            BeginChapter();
            OperateUntil(() => farewell.Phase == FarewellPhase.Memorial);
            SetE(false);
            Run(0.3f);
            Press(keyboard.rightArrowKey); // foco em "Repetir capítulo"
            Step(Frame);
            Release(keyboard.rightArrowKey);
            Step(Frame);
            Assert.That(farewell.Focus, Is.EqualTo(1));

            entered.Clear();
            Press(keyboard.eKey);
            Run(0.1f);
            Release(keyboard.eKey);
            Run(0.2f);

            Assert.That((replays, returns), Is.EqualTo((1, 0)), "uma escolha: Repetir capítulo");
            Assert.That(loader.Loads, Is.EqualTo(2), "Repetir recarrega a cena");
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(director.CurrentStepId, Is.EqualTo("PIO.Launch"));
            Assert.That(controller.CurrentState, Is.EqualTo("PIO.Launch"));
            Assert.That(entered, Is.EqualTo(new[] { "PIO.Launch" }), "recomeça só do primeiro estado");
            Assert.That(controller.LastReport.states, Is.Empty, "relatório novo");
        }

        // ---------- Reset por estado ----------

        [Test]
        public void RestartingInEachActivityCleansInteractionsWithoutDuplicatingListenersOrObjects([NUnit.Framework.Range(0, 8)] int stateIndex)
        {
            BeginChapter();
            var target = StateIds[stateIndex];
            OperateUntil(() => controller.CurrentState == target);
            Run(1.5f); // um pouco dentro da atividade
            var objects = Resources.FindObjectsOfTypeAll<GameObject>().Length;
            var entriesBefore = entered.Count;

            SetE(false);
            director.RestartChapter();
            Assert.That(director.State, Is.EqualTo(AppState.Playing));
            Assert.That(controller.CurrentState, Is.EqualTo("PIO.Launch"), $"reiniciar em {target} volta ao começo");
            Assert.That(controller.LastReport.states, Is.Empty);
            Assert.That(farewell.Phase, Is.EqualTo(FarewellPhase.Idle));
            Assert.That(imaging.Phase == ImagingPhase.Idle && !belt.IsRunning && !plaque.IsRunning && !outbound.IsRunning && !listening.IsRunning, Is.True,
                "nenhuma atividade antiga continua rodando");
            Assert.That(router.DescribeLeases(), Does.Not.Contain("PIO.Plaque").And.Not.Contain("PIO.Imaging").And.Not.Contain("PIO.Farewell"));
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Length, Is.EqualTo(objects), "nenhum objeto acumulado entre os reinícios");
            Assert.That(entered.Count, Is.EqualTo(entriesBefore + 1), "um único StepEntered depois do reinício (nenhum ouvinte duplicado)");
            Assert.That(entered.Last(), Is.EqualTo("PIO.Launch"));

            // E segue funcionando uma vez: o segundo percurso avança normalmente até o Belt.
            completed.Clear();
            OperateUntil(() => controller.CurrentState == "PIO.Belt");
            Assert.That(completed, Is.EqualTo(new[] { "PIO.Launch" }), "uma conclusão por estado, mesmo depois de reiniciar");
        }

        [Test]
        public void RestartingTwiceNeverDoublesTheCompletionOfAState()
        {
            BeginChapter();
            OperateUntil(() => controller.CurrentState == "PIO.Belt");
            SetE(false);
            director.RestartChapter();
            director.RestartChapter();
            completed.Clear();
            OperateUntil(() => controller.CurrentState == "PIO.JupiterApproach");
            Assert.That(completed, Is.EqualTo(new[] { "PIO.Launch", "PIO.Belt" }), "cada estado concluiu exatamente uma vez no último percurso");
            Assert.That(director.CurrentStepId, Is.EqualTo("PIO.JupiterApproach"));
        }

        [Test]
        public void DestroyingTheSceneReleasesTheListenersAndLeavesNoActivityRunning()
        {
            BeginChapter();
            OperateUntil(() => controller.CurrentState == "PIO.Belt");
            SetE(false);
            Object.DestroyImmediate(controller.gameObject); // o que acontece ao descarregar a cena
            Assert.That(router.DescribeLeases(), Does.Not.Contain("PIO."));
            Assert.That(() => director.CompleteStep("PIO.Belt"), Throws.Nothing, "o diretor não chama um controlador morto");
            Assert.That(belt == null || !belt.IsRunning, Is.True);
        }

        [Test]
        public void TheJupiterApproachIsCinemaOnlyAndConcludesWhenItsSequenceEnds()
        {
            BeginChapter();
            OperateUntil(() => controller.CurrentState == "PIO.JupiterApproach");
            SetE(false);
            var start = controller.CurrentStateSeconds;
            Run(10f);
            Assert.That(controller.CurrentState, Is.EqualTo("PIO.JupiterApproach"));
            Assert.That(completed, Does.Not.Contain("PIO.JupiterApproach"));
            Run(21f); // 31 s no total: a sequência tem 30 s
            Assert.That(completed, Does.Contain("PIO.JupiterApproach"));
            Assert.That(controller.CurrentState, Is.EqualTo("PIO.Imaging"));
            Assert.That(controller.LastReport.activeSeconds[2] - 0f, Is.EqualTo(30f).Within(1.5f), "30 s de cinema, a duração nominal");
            Assert.That(start, Is.LessThan(1f));
        }

        [Test]
        public void LaunchPlaqueAndOutboundRunWithoutAnyInputAndAskForNoKeyPress()
        {
            BeginChapter();
            // Cena 1: a demonstração dos geradores corre sozinha (evento aos 32,5 s, 3 s de rampa) e o estado conclui no fim da sequência.
            Run(34f);
            Assert.That(launch.HoldDone, Is.False);
            Assert.That(launch.HoldProgress, Is.GreaterThan(0f).And.LessThan(1f), "a rampa começou sozinha");
            Assert.That(router.DescribeLeases(), Does.Not.Contain("PIO.Launch.Rtg"), "nenhum lease de painel: não há E a segurar");
            Run(2.5f);
            Assert.That(launch.HoldDone, Is.True, "a demonstração terminou sem entrada");
            Run(4f);
            Assert.That(completed, Does.Contain("PIO.Launch"));

            // Cena 6: as cinco peças encaixam sozinhas e nenhuma instrução de E é pedida.
            OperateUntil(() => controller.CurrentState == "PIO.Plaque");
            SetE(false);
            Run(1f);
            Assert.That(router.DescribeLeases(), Does.Not.Contain("PIO.Plaque"));
            Run(18f); // a apresentação da placa dura 17,5 s; a primeira peça entra 0,4 s depois do início do encaixe e a segunda só aos 21,9 s
            Assert.That(plaque.IsPlaced(0), Is.True, "a primeira peça já encaixou sozinha");
            Assert.That(plaque.IsPlaced(4), Is.False);
            OperateUntil(() => controller.CurrentState == "PIO.Outbound");
            Assert.That(completed, Does.Contain("PIO.Plaque"), "as cinco peças encaixaram e o estado concluiu sem entrada");

            // Cena 7: a priorização da comunicação corre sozinha.
            SetE(false);
            Run(36f); // a priorização abre em 28 s e leva 3 s
            Assert.That(router.DescribeLeases(), Does.Not.Contain("PIO.Outbound.Power"));
            Assert.That(outbound.HoldDone || completed.Contains("PIO.Outbound") || controller.CurrentState == "PIO.Listening", Is.True);
        }

        [Test]
        public void TheListeningRoundsOpenAtOnceAndAdvanceWithoutTheEKey()
        {
            var contacts = new List<int>();
            var silences = 0;
            BeginChapter();
            OperateUntil(() => controller.CurrentState == "PIO.Listening");
            SetE(false);
            listening.ContactMade += n => contacts.Add(n);
            listening.NoSignalHeard += () => silences++;
            Assert.That(listening.Phase, Is.EqualTo(ListeningPhase.Contact1), "a primeira rodada já está aberta quando a cena aparece");
            Assert.That(listening.ViewState().AutoListen, Is.True, "sem E: só as setas sintonizam");

            // Nenhuma tecla: depois de 15 s parado a ajuda aproxima a antena da zona, a escuta é automática e as rodadas avançam sozinhas.
            Run(300f);
            Assert.That(contacts, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(silences, Is.EqualTo(1), "a quarta tentativa sem sinal chega uma vez");
            Assert.That(completed, Does.Contain("PIO.Listening"), "o estado conclui sem outra entrada");
        }

        [Test]
        public void TheClosestApproachIsCinemaOnlyAndConcludesWhenItsSequenceEnds()
        {
            BeginChapter();
            OperateUntil(() => controller.CurrentState == "PIO.Closest");
            SetE(false);
            Run(30f);
            Assert.That(controller.CurrentState, Is.EqualTo("PIO.Closest"), "30 s depois ainda é cinema: nada conclui por clique nem por tempo antes do fim da sequência");
            Assert.That(completed, Does.Not.Contain("PIO.Closest"));
            Run(36f); // 66 s no total: a sequência tem 65 s
            Assert.That(completed, Does.Contain("PIO.Closest"));
            Assert.That(controller.CurrentState, Is.EqualTo("PIO.Plaque"));
            Assert.That(controller.LastReport.activeSeconds[4], Is.EqualTo(65f).Within(1.5f), "65 s de cinema, a duração nominal");
        }
    }
}
