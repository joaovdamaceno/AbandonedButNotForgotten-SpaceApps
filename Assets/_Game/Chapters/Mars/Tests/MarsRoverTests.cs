using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-002: controle da Opportunity e tutorial por ações reais, sobre o prefab MarsRoverGameplay.
    /// Prova: percurso só com teclado, parada em pausa/instrumento/filme, sem giro instantâneo, colisão, tutorial que
    /// conclui por ação (nunca por tempo) e pode ser repetido, E com rearme, ajuda de 12 s/25 s e metas de chegada.
    /// </summary>
    public class MarsRoverTests
    {
        const string PrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/MarsRoverGameplay.prefab";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TutorialTextsPath = "Assets/_Game/Chapters/Mars/Data/MarsTutorialTexts.json";
        const float Frame = 0.05f;

        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        sealed class MemoryStorage : SettingsStore.IStorage
        {
            readonly Dictionary<string, string> values = new Dictionary<string, string>();
            public string Read(string key) => values.TryGetValue(key, out var v) ? v : null;
            public void Write(string key, string value) => values[key] = value;
            public void Delete(string key) => values.Remove(key);
        }

        [Serializable]
        sealed class TextTable
        {
            [Serializable]
            public sealed class Entry
            {
                public string key;
                public string pt;
                public string en;
            }

            public List<Entry> texts = new List<Entry>();
        }

        readonly List<GameObject> hosts = new List<GameObject>();
        ChapterDefinition chapter;
        string subtitles;
        GameObject root;
        GameObject services;
        InputRouter router;
        InteractionService interaction;
        FakeTime time;
        MarsRoverController rover;
        MarsTutorial tutorial;
        MarsChapterFlow flow;
        MockChapterDirector director;
        IDisposable gameplay;

        [SetUp]
        public void SetUp()
        {
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);

            services = Track(new GameObject("services (MarsRoverTests)"));
            router = services.AddComponent<InputRouter>();
            interaction = services.AddComponent<InteractionService>();
            time = new FakeTime();
            interaction.Initialize(router, time);
            gameplay = router.Acquire(InputContext.Gameplay, "teste");

            root = Track((GameObject)PrefabUtility.InstantiatePrefab(prefab));
            rover = root.GetComponentInChildren<MarsRoverController>();
            tutorial = root.GetComponentInChildren<MarsTutorial>();

            director = new MockChapterDirector();
            flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            rover.Initialize(router, time, null, flow);
            tutorial.Initialize(rover, flow, interaction, time);
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.name = "chão (teste)";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(400f, 1f, 400f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            gameplay?.Dispose();
            foreach (var host in hosts)
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            }

            hosts.Clear();
        }

        GameObject Track(GameObject go)
        {
            hosts.Add(go);
            return go;
        }

        // O InputRouter só expõe os pontos de entrada para o assembly de testes do Core; aqui a ponte é reflexão, só em teste.
        static void Feed(InputRouter target, string method, params object[] args) =>
            typeof(InputRouter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        void Drive(Vector2 move, float seconds)
        {
            Feed(router, "FeedMove", move);
            for (var t = 0f; t < seconds - 1e-4f; t += Frame)
                Step(Frame);

            Feed(router, "FeedMove", Vector2.zero);
        }

        void Step(float dt)
        {
            time.ActiveDeltaSeconds = dt;
            interaction.Tick(dt);
            rover.Tick(dt);
            tutorial.Tick(dt);
            flow.Tick(dt);
            Feed(router, "EndFrame"); // LateUpdate do router: o delta do mouse vale um quadro
        }

        void BeginTutorial()
        {
            director.Begin(chapter);
            for (var i = 1; i <= 6; i++)
            {
                var binding = flow.CurrentBinding;
                Assert.That(flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f)), Is.True, binding.StepId);
            }

            Assert.That(director.CurrentStepId, Is.EqualTo("M07"));
            Assert.That(tutorial.IsRunning, Is.True, "M07 inicia o tutorial");
        }

        void PlayUntil(string stepId)
        {
            var guard = 0;
            while (director.CurrentStepId != stepId && guard++ < 64)
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                {
                    flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
                }
                else
                {
                    foreach (var e in binding.CompletionEvents)
                        flow.Raise(e);

                    for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                        flow.Tick(Frame);
                }
            }

            Assert.That(director.CurrentStepId, Is.EqualTo(stepId));
        }

        // ---------- Prefab e dados ----------

        [Test]
        public void PrefabIsWiredAndValidates()
        {
            Assert.That(rover.ValidateSetup(), Is.Empty, ContractValidation.Describe(rover.ValidateSetup()));
            Assert.That(tutorial.ValidateSetup(), Is.Empty, ContractValidation.Describe(tutorial.ValidateSetup()));
            Assert.That(rover.GetComponent<CharacterController>(), Is.Not.Null, "colisão");
            Assert.That(tutorial.InspectionTarget.IsChildOf(rover.transform), Is.False, "o alvo fica parado quando o rover anda");
        }

        [Test]
        public void EveryTutorialKeyHasPortugueseAndEnglish()
        {
            var table = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(TutorialTextsPath).text);
            var keys = new[] { MarsTutorial.MoveKey, MarsTutorial.LookKey, MarsTutorial.InteractKey, MarsTutorial.ReinforceKey,
                MarsTutorial.HelpKey, MarsTutorial.InspectVerbKey, MarsTutorial.InspectObjectKey };
            foreach (var key in keys)
            {
                var entry = table.texts.FirstOrDefault(t => t.key == key);
                Assert.That(entry, Is.Not.Null, key);
                Assert.That(string.IsNullOrWhiteSpace(entry.pt), Is.False, key + " PT");
                Assert.That(string.IsNullOrWhiteSpace(entry.en), Is.False, key + " EN");
            }
        }

        // ---------- Movimento ----------

        [Test]
        public void MotionRampsInsteadOfJumping()
        {
            float speed = 0f, turn = 0f;
            MarsRoverController.StepMotion(ref speed, ref turn, 1f, 1f, Frame, 1.6f, 0.8f, 2.4f, 4f, 55f, 140f);
            Assert.That(speed, Is.EqualTo(0.12f).Within(1e-4f), "um quadro não chega à velocidade máxima");
            Assert.That(turn, Is.EqualTo(7f).Within(1e-4f), "nem ao giro máximo");

            for (var i = 0; i < 100; i++)
                MarsRoverController.StepMotion(ref speed, ref turn, 1f, 1f, Frame, 1.6f, 0.8f, 2.4f, 4f, 55f, 140f);
            Assert.That(speed, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(turn, Is.EqualTo(55f).Within(1e-4f));

            MarsRoverController.StepMotion(ref speed, ref turn, -1f, 0f, Frame, 1.6f, 0.8f, 2.4f, 4f, 55f, 140f);
            Assert.That(speed, Is.EqualTo(1.4f).Within(1e-4f), "inverter passa pela frenagem, sem salto para a ré");
        }

        [Test]
        public void WasdOrArrowsCompleteADriveWithoutMouseOrShift()
        {
            var start = rover.transform.position;
            Drive(new Vector2(0f, 1f), 3f);
            var travelled = Vector3.Distance(start, rover.transform.position);
            Assert.That(rover.DrivenMeters, Is.GreaterThan(3f), "3 s de W percorrem mais de 3 m");
            Assert.That(travelled, Is.EqualTo(rover.DrivenMeters).Within(0.05f));
            Assert.That(rover.LookedDegrees, Is.EqualTo(0f), "nenhum olhar manual foi necessário");

            var heading = rover.transform.eulerAngles.y;
            Drive(new Vector2(1f, 0.5f), 1f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(heading, rover.transform.eulerAngles.y)), Is.InRange(5f, 56f), "curva suave");
        }

        [Test]
        public void ReleasingKeysBrakesSmoothlyAndStops()
        {
            Feed(router, "FeedMove", new Vector2(0f, 1f));
            for (var i = 0; i < 30; i++)
                Step(Frame);
            Feed(router, "FeedMove", Vector2.zero);
            Step(Frame);
            Assert.That(rover.SpeedMetersPerSecond, Is.GreaterThan(0f).And.LessThan(rover.MaxForwardSpeedMetersPerSecond), "frenagem gradual");
            for (var i = 0; i < 20; i++)
                Step(Frame);
            Assert.That(rover.SpeedMetersPerSecond, Is.EqualTo(0f));
        }

        [Test]
        public void RoverStopsDuringPauseInstrumentAndFilm()
        {
            Feed(router, "FeedMove", new Vector2(0f, 1f));
            for (var i = 0; i < 10; i++)
                Step(Frame);
            var moving = rover.transform.position;
            Assert.That(rover.SpeedMetersPerSecond, Is.GreaterThan(0f));

            // Pausa: o relógio ativo entrega delta zero; nada anda, mesmo com a tecla mantida.
            for (var i = 0; i < 10; i++)
                Step(0f);
            Assert.That(rover.transform.position, Is.EqualTo(moving), "pausa");

            // Instrumento: trava na hora e não escorrega para dentro dele.
            rover.SetLocked("instrumento", true);
            Assert.That(rover.SpeedMetersPerSecond, Is.EqualTo(0f));
            for (var i = 0; i < 10; i++)
                Step(Frame);
            Assert.That(rover.transform.position, Is.EqualTo(moving), "instrumento");
            rover.SetLocked("instrumento", false);

            // Filme: o contexto de entrada deixa de ser Gameplay.
            using (router.Acquire(InputContext.Cinematic, "filme"))
            {
                Feed(router, "FeedMove", new Vector2(0f, 1f));
                for (var i = 0; i < 40; i++)
                    Step(Frame);
            }

            Assert.That(rover.SpeedMetersPerSecond, Is.EqualTo(0f).Within(1e-4f), "freou durante o filme");
            var parked = rover.transform.position;
            for (var i = 0; i < 20; i++)
                Step(Frame);
            Assert.That(rover.transform.position, Is.EqualTo(parked), "filme");
        }

        [Test]
        public void SeveralLocksNeedAllToBeReleased()
        {
            rover.SetLocked("a", true);
            rover.SetLocked("b", true);
            rover.SetLocked("a", false);
            Assert.That(rover.IsLocked, Is.True);
            rover.SetLocked("b", false);
            Assert.That(rover.IsLocked, Is.False);
            Assert.Throws<ArgumentException>(() => rover.SetLocked("", true));
        }

        [Test]
        public void ImposedPoseTurnsGraduallyNeverInstantly()
        {
            var target = Vector3.right;
            var aligned = 0;
            rover.Aligned += () => aligned++;
            rover.AlignTo(target);

            Step(0.1f);
            var firstFrame = Vector3.Angle(rover.transform.forward, target);
            Assert.That(firstFrame, Is.GreaterThan(80f), "um quadro gira poucos graus (máx. 6° a 60°/s)");
            Assert.That(aligned, Is.EqualTo(0));

            for (var i = 0; i < 40 && rover.IsAligning; i++)
                Step(0.1f);
            Assert.That(Vector3.Angle(rover.transform.forward, target), Is.LessThan(0.5f));
            Assert.That(aligned, Is.EqualTo(1), "avisa uma vez");
            Assert.That(rover.IsAligning, Is.False);
        }

        [Test]
        public void ObstacleBlocksTheRoverAndKillsTheSpeed()
        {
            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 1f, 4f);
            wall.transform.localScale = new Vector3(10f, 2f, 0.5f);
            Physics.SyncTransforms();

            Drive(new Vector2(0f, 1f), 6f);
            Assert.That(rover.transform.position.z, Is.LessThan(3.75f - rover.GetComponent<CharacterController>().radius + 0.1f), "não atravessa a parede");
            Assert.That(rover.SpeedMetersPerSecond, Is.LessThan(0.1f), "a velocidade acompanha o bloqueio");
            Assert.That(rover.DrivenMeters, Is.LessThan(4f), "empurrar a parede não conta como percurso");

            Drive(new Vector2(0f, -1f), 2f);
            Assert.That(rover.transform.position.z, Is.LessThan(2.5f), "recupera marchando à ré");
        }

        [Test]
        public void PlaceAtDropsVelocityAndLocks()
        {
            Feed(router, "FeedMove", new Vector2(0f, 1f));
            for (var i = 0; i < 10; i++)
                Step(Frame);

            rover.PlaceAt(new Vector3(10f, 0f, 10f), Quaternion.Euler(0f, 90f, 0f));
            Assert.That(rover.SpeedMetersPerSecond, Is.EqualTo(0f));
            Assert.That(rover.transform.position.x, Is.EqualTo(10f).Within(0.01f));
        }

        // ---------- Câmera e ajuda ----------

        [Test]
        public void HelpFramesTheTargetWithoutTurningTheRoverOrSolvingScience()
        {
            BeginTutorial();
            rover.PlaceAt(Vector3.zero, Quaternion.Euler(0f, 90f, 0f)); // alvo de inspeção a 90° do rumo
            var heading = rover.transform.rotation;

            Feed(router, "FeedHelp");
            Step(0.1f);
            Assert.That(Mathf.Abs(rover.OrbitYawDegrees), Is.LessThan(12f), "câmera gira gradualmente, a 110°/s");
            for (var i = 0; i < 30; i++)
                Step(0.1f);

            Assert.That(Mathf.Abs(rover.OrbitYawDegrees), Is.GreaterThan(80f), "câmera chegou ao alvo");
            Assert.That(rover.transform.rotation, Is.EqualTo(heading), "o rover não girou");
            Assert.That(new Vector2(rover.transform.position.x, rover.transform.position.z).magnitude, Is.LessThan(0.01f), "o rover não se moveu");
            Assert.That(tutorial.IsDone(MarsTutorialItem.Interact), Is.False, "H nunca executa a ciência");
        }

        [Test]
        public void ManualLookReturnsToTheRoverHeadingAfterAMoment()
        {
            Feed(router, "FeedLook", new Vector2(300f, -40f));
            Step(Frame);
            Assert.That(rover.OrbitYawDegrees, Is.EqualTo(30f).Within(0.01f));
            Assert.That(rover.LookedDegrees, Is.GreaterThan(30f));

            for (var i = 0; i < 200; i++)
                Step(0.1f);
            Assert.That(rover.OrbitYawDegrees, Is.EqualTo(0f).Within(0.01f), "recuperação de orientação sem ação");
        }

        [Test]
        public void CameraFollowsBehindWithoutClippingAndNeverWritesWhileASequenceOwnsIt()
        {
            var cameraHost = Track(new GameObject("camera"));
            var camera = cameraHost.AddComponent<Camera>();
            var director3d = cameraHost.AddComponent<CameraDirector>();
            director3d.Initialize(camera);
            director3d.GroundProbe = _ => null;
            rover.Initialize(router, time, director3d, flow);

            camera.transform.position = new Vector3(0f, 30f, -30f);
            camera.transform.rotation = Quaternion.identity;
            var before = camera.transform.position;

            using (director3d.AcquireControl("filme"))
            {
                rover.TickCamera(Frame);
                Assert.That(camera.transform.position, Is.EqualTo(before), "sequência dona da câmera");
            }

            rover.TickCamera(Frame);
            var firstFrame = Vector3.Distance(camera.transform.position, before);
            var total = Vector3.Distance(rover.ComputeIdealCameraPose(camera.fieldOfView).Position, before);
            Assert.That(firstFrame, Is.GreaterThan(0f).And.LessThan(total * 0.3f), "retomada parte da pose atual, sem salto");

            for (var i = 0; i < 200; i++)
                rover.TickCamera(Frame);
            var ideal = rover.ComputeIdealCameraPose(camera.fieldOfView);
            Assert.That(Vector3.Distance(camera.transform.position, ideal.Position), Is.LessThan(0.2f));
            Assert.That(camera.transform.position.z, Is.LessThan(rover.transform.position.z), "atrás do rover");

            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0f, 2f, -3f);
            wall.transform.localScale = new Vector3(20f, 8f, 0.5f);
            Physics.SyncTransforms();
            rover.TickCamera(Frame);
            Assert.That(camera.transform.position.z, Is.GreaterThan(-2.7f), "a colisão aproxima a câmera, sem atravessar a parede");
        }

        // ---------- Tutorial ----------

        [Test]
        public void TutorialCompletesOnlyByRealActionsAndAdvancesTheStep()
        {
            BeginTutorial();
            var accepted = new List<string>();
            flow.EventAccepted += (_, e) => accepted.Add(e);
            var items = new List<MarsTutorialItem>();
            tutorial.ItemCompleted += items.Add;

            for (var i = 0; i < 400; i++)
                Step(Frame);
            Assert.That(tutorial.IsComplete, Is.False, "o tempo sozinho nunca conclui");
            Assert.That(tutorial.PendingItems().Count(), Is.EqualTo(3));

            Drive(new Vector2(0f, 1f), 1f);
            Assert.That(rover.DrivenMeters, Is.GreaterThan(1f).And.LessThan(2f));
            Assert.That(tutorial.IsDone(MarsTutorialItem.Move), Is.False, "ainda faltam metros");
            Drive(new Vector2(0f, 1f), 0.6f);
            Assert.That(tutorial.IsDone(MarsTutorialItem.Move), Is.True, "2 m dirigidos");

            Feed(router, "FeedLook", new Vector2(100f, 0f)); // 10° a 1,0 de sensibilidade
            Step(Frame);
            Assert.That(tutorial.IsDone(MarsTutorialItem.Look), Is.True);
            Assert.That(tutorial.PendingItems().ToList(), Is.EqualTo(new List<MarsTutorialItem> { MarsTutorialItem.Interact }));

            // O alvo agora está ao alcance: E segura, conclui e avança M07 → M08.
            Assert.That(interaction.TryGetActive(out var active), Is.True);
            Assert.That(active.Id, Is.EqualTo(MarsTutorial.InspectCandidateId));
            Feed(router, "FeedInteract", true);
            Assert.That(interaction.HoldingId, Is.EqualTo(MarsTutorial.InspectCandidateId));
            Step(0.3f);
            Assert.That(tutorial.IsComplete, Is.False, "soltar antes de concluir não vale; ainda segurando");
            Step(0.4f);

            Assert.That(tutorial.IsComplete, Is.True);
            Assert.That(items, Is.EquivalentTo(new[] { MarsTutorialItem.Move, MarsTutorialItem.Look, MarsTutorialItem.Interact }));
            Assert.That(accepted.Count(e => e == MarsChapterFlow.Events.TutorialDone), Is.EqualTo(1));
            Assert.That(director.CurrentStepId, Is.EqualTo("M08"));
            Assert.That(interaction.TryGetActive(out _), Is.False, "o candidato do tutorial saiu: E não aparece mais");
        }

        [Test]
        public void ELiftedBeforeCompletionPausesTheToolAndSurvivesFocusLoss()
        {
            BeginTutorial();
            Drive(new Vector2(0f, 1f), 1.5f); // aproxima do alvo
            Assert.That(interaction.CanBeginHold(MarsTutorial.InspectCandidateId), Is.True);

            Feed(router, "FeedInteract", true);
            Step(0.3f);
            Feed(router, "FeedInteract", false);
            Step(0.5f);
            Assert.That(tutorial.IsDone(MarsTutorialItem.Interact), Is.False, "soltar E pausa a operação");
            Assert.That(interaction.HoldingId, Is.Null);

            Feed(router, "FeedInteract", true);
            router.NotifyFocusLost();
            Step(1f);
            Assert.That(tutorial.IsDone(MarsTutorialItem.Interact), Is.False, "perder foco cancela");

            Feed(router, "FeedInteract", true);
            Step(0.7f);
            Assert.That(tutorial.IsDone(MarsTutorialItem.Interact), Is.True);
        }

        [Test]
        public void PromptAppearsOnlyWhereTheActionWouldSucceed()
        {
            BeginTutorial();
            rover.PlaceAt(new Vector3(0f, 0f, -20f), Quaternion.identity);
            Step(Frame);
            Assert.That(interaction.TryGetActive(out _), Is.False, "longe do alvo, nenhum E");
            Assert.That(interaction.CanBeginHold(MarsTutorial.InspectCandidateId), Is.False);

            rover.PlaceAt(Vector3.zero, Quaternion.identity);
            Step(Frame);
            Assert.That(interaction.CanBeginHold(MarsTutorial.InspectCandidateId), Is.False, "5 m do alvo, fora do alcance de 3,5 m");
            Drive(new Vector2(0f, 1f), 1.5f);
            Assert.That(interaction.CanBeginHold(MarsTutorial.InspectCandidateId), Is.True);
        }

        [Test]
        public void HelpKeyAlsoCompletesLookForPlayersWithoutAMouse()
        {
            BeginTutorial();
            Feed(router, "FeedHelp");
            Assert.That(tutorial.IsDone(MarsTutorialItem.Look), Is.True);
            Assert.That(tutorial.IsDone(MarsTutorialItem.Move), Is.False);
        }

        [Test]
        public void IdleTutorialReinforcesAt12sOffersHelpAt25sAndProgressResets()
        {
            BeginTutorial();
            var levels = new List<MarsAssistLevel>();
            tutorial.AssistChanged += levels.Add;

            for (var i = 0; i < 11; i++)
                Step(1f);
            Assert.That(tutorial.AssistLevel, Is.EqualTo(MarsAssistLevel.None));
            Step(1f);
            Assert.That(tutorial.AssistLevel, Is.EqualTo(MarsAssistLevel.Reinforce), "12 s");
            for (var i = 0; i < 12; i++)
                Step(1f);
            Assert.That(tutorial.AssistLevel, Is.EqualTo(MarsAssistLevel.Reinforce), "24 s");
            Step(1f);
            Assert.That(tutorial.AssistLevel, Is.EqualTo(MarsAssistLevel.Offer), "25 s");

            Feed(router, "FeedHelp");
            Assert.That(tutorial.IsDone(MarsTutorialItem.Look), Is.True, "progresso real");
            Assert.That(tutorial.AssistLevel, Is.EqualTo(MarsAssistLevel.None), "progresso zera a escada");
            Assert.That(levels, Is.EqualTo(new[] { MarsAssistLevel.Reinforce, MarsAssistLevel.Offer, MarsAssistLevel.None }));
            Assert.That(tutorial.IsComplete, Is.False, "a ajuda não resolve o tutorial");
        }

        [Test]
        public void PausedTimeDoesNotAdvanceTheAssistLadder()
        {
            BeginTutorial();
            for (var i = 0; i < 60; i++)
                Step(0f);
            Assert.That(tutorial.AssistLevel, Is.EqualTo(MarsAssistLevel.None));
        }

        [Test]
        public void TutorialCanBeRepeatedEvenAfterBeingMarkedAsSeen()
        {
            var settings = new SettingsStore(new MemoryStorage());
            tutorial.Initialize(rover, flow, interaction, time, settings);
            BeginTutorial();
            Drive(new Vector2(0f, 1f), 2.5f);
            Feed(router, "FeedHelp");
            Feed(router, "FeedInteract", true);
            Step(0.7f);
            Assert.That(tutorial.IsComplete, Is.True);
            Assert.That(settings.HasSeenTutorial(MarsTutorial.SeenKey), Is.True);
            Feed(router, "FeedInteract", false);

            tutorial.Repeat();
            Assert.That(tutorial.IsRunning, Is.True, "visto não impede repetir");
            Assert.That(tutorial.IsComplete, Is.False);
            Assert.That(tutorial.PendingItems().Count(), Is.EqualTo(3));

            rover.PlaceAt(Vector3.zero, Quaternion.identity);
            Drive(new Vector2(0f, 1f), 1.5f);
            Assert.That(tutorial.IsDone(MarsTutorialItem.Move), Is.False, "conta a partir da repetição, não do percurso anterior");
        }

        [Test]
        public void RestartingTheStepResetsTheTutorialAndReleasesTheCandidate()
        {
            BeginTutorial();
            Drive(new Vector2(0f, 1f), 3f);
            Assert.That(tutorial.IsDone(MarsTutorialItem.Move), Is.True);

            director.RestartChapter();
            Assert.That(tutorial.IsRunning, Is.False, "M01 reiniciado: tutorial encerrado");
            Assert.That(interaction.TryGetActive(out _), Is.False);

            rover.PlaceAt(Vector3.zero, Quaternion.identity);
            PlayUntil("M07");
            Assert.That(tutorial.IsRunning, Is.True);
            Assert.That(tutorial.PendingItems().Count(), Is.EqualTo(3), "nova tentativa limpa");
        }

        // ---------- Metas de chegada (M10) ----------

        [Test]
        public void OutcropGoalReportsArrivalOnceAndOffersHelpWhenIdle()
        {
            var goalTarget = Track(new GameObject("McKittrick (teste)"));
            goalTarget.transform.position = new Vector3(0f, 0f, 14f);
            var serialized = new SerializedObject(rover);
            var goals = serialized.FindProperty("reachGoals");
            goals.arraySize = 1;
            var goal = goals.GetArrayElementAtIndex(0);
            goal.FindPropertyRelative("stepId").stringValue = "M10";
            goal.FindPropertyRelative("eventId").stringValue = MarsChapterFlow.Events.OutcropReached;
            goal.FindPropertyRelative("target").objectReferenceValue = goalTarget.transform;
            goal.FindPropertyRelative("radiusMeters").floatValue = 2.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(rover.ValidateSetup(), Is.Empty);

            director.Begin(chapter);
            PlayUntil("M10");
            var assistLevels = new List<MarsAssistLevel>();
            rover.GoalAssistChanged += assistLevels.Add;
            var reached = new List<string>();
            rover.GoalReached += reached.Add;
            Assert.That(rover.ActiveGoal, Is.Not.Null);
            Assert.That(rover.FrameTarget, Is.EqualTo(goalTarget.transform), "H enquadra o afloramento");

            for (var i = 0; i < 25; i++)
                Step(1f);
            Assert.That(assistLevels, Is.EqualTo(new[] { MarsAssistLevel.Reinforce, MarsAssistLevel.Offer }), "12 s e 25 s");
            Assert.That(director.CurrentStepId, Is.EqualTo("M10"), "o tempo não conclui a etapa");

            Drive(new Vector2(0f, 1f), 12f);
            Assert.That(reached, Is.EqualTo(new[] { MarsChapterFlow.Events.OutcropReached }), "uma vez");
            Assert.That(director.CurrentStepId, Is.EqualTo("M11"));
            Assert.That(rover.ActiveGoal, Is.Null);
            Assert.That(rover.FrameTarget, Is.Null);
            Assert.That(assistLevels.Last(), Is.EqualTo(MarsAssistLevel.None), "ajuda encerrada ao chegar");
        }
    }
}
