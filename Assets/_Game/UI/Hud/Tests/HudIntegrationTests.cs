using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// UI-003 com o <see cref="InteractionService"/> e o <see cref="InputRouter"/> reais: o prompt E só aparece quando a
    /// ação pode executar, os três feedbacks (início, soltura, conclusão) são distintos, a orientação projeta o alvo em
    /// âncora ou seta de borda e nada de um capítulo vaza para o seguinte. Usa o UXML e os textos reais do HUD.
    /// </summary>
    public class HudIntegrationTests
    {
        const string UxmlPath = "Assets/_Game/UI/Hud/MissionHud.uxml";
        const string TextsPath = "Assets/_Game/UI/Hud/HudTexts.json";

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        GameObject host;
        InputRouter router;
        InteractionService service;
        IDisposable gameplay;
        VisualElement tree;
        UI.InteractionPrompt prompt;
        UI.ObjectiveIndicator indicator;
        UI.DossierTextCatalog texts;
        Camera camera;
        Transform reference;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("hud-tests");
            router = host.AddComponent<InputRouter>();
            service = host.AddComponent<InteractionService>();
            service.Initialize(router, null);
            gameplay = router.Acquire(InputContext.Gameplay, "hud-tests");

            tree = Load<VisualTreeAsset>(UxmlPath).Instantiate();
            prompt = new UI.InteractionPrompt(tree, service);
            indicator = new UI.ObjectiveIndicator(tree);

            texts = new UI.DossierTextCatalog();
            UI.DossierTextTables.Load(Load<TextAsset>(TextsPath), texts);
            texts.Add("TST.VERB.grind", "esmerilhar", "grind");
            texts.Add("TST.OBJ.rock", "a rocha", "the rock");
            texts.Add("TST.OBJECTIVE", "Prepare e analise a rocha", "Prepare and analyze the rock");
            texts.Add("TST.TARGET", "McKittrick", "McKittrick");
            texts.Add("TST.STAMP", "OPPORTUNITY · MERIDIANI · 2004", "OPPORTUNITY · MERIDIANI · 2004");

            var cameraObject = new GameObject("hud-camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.identity;
            reference = new GameObject("hud-reference").transform;
            created.Add(cameraObject);
            created.Add(reference.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            prompt.Dispose();
            gameplay?.Dispose();
            UnityEngine.Object.DestroyImmediate(host);
            foreach (var obj in created)
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }

            created.Clear();
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset != null, Is.True, $"asset ausente: {path}");
            return asset;
        }

        static InteractionCandidate Candidate(string id, float hold = 2f, int priority = 0, float distance = 1f, bool available = true, Transform anchor = null) =>
            new InteractionCandidate
            {
                Id = id, VerbKey = "TST.VERB.grind", ObjectKey = "TST.OBJ.rock", Available = available, DistanceMeters = distance,
                HoldSeconds = hold, Priority = priority, Anchor = anchor,
            };

        void Frame(float dt = 0.016f, string language = "pt", HoldMode mode = HoldMode.Hold)
        {
            service.Tick(dt);
            prompt.Tick(dt, texts, language, mode);
        }

        Transform Target(float x, float y, float z)
        {
            var go = new GameObject("hud-target");
            go.transform.position = new Vector3(x, y, z);
            created.Add(go);
            return go.transform;
        }

        string PromptText(string name) => tree.Q<Label>(name).text;

        // ---------- "E aparece disponível apenas quando a ação pode executar" ----------

        [Test]
        public void EIsHiddenForAnUnavailableCandidateAndShownWhenItBecomesAvailable()
        {
            service.SetCandidate(Candidate("a", available: false));
            Frame();
            Assert.That(prompt.IsShown, Is.False, "candidato fora de alcance: nada de E");

            service.SetCandidate(Candidate("a", available: true));
            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Available));
            Assert.That(prompt.IsShown, Is.True);
            Assert.That(PromptText("prompt-label"), Is.EqualTo("Segure E · esmerilhar a rocha"));
            Assert.That(tree.Q<VisualElement>("prompt").ClassListContains("sa-hold--available"), Is.True);
        }

        [Test]
        public void EDisappearsWhenTheInputContextCannotRunTheAction()
        {
            service.SetCandidate(Candidate("a"));
            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Available));

            using (router.Acquire(InputContext.Cinematic, "cutscene"))
            {
                Frame();
                Assert.That(service.CanBeginHold("a"), Is.False);
                Assert.That(prompt.IsShown, Is.False, "numa cinemática a ação não executa e o E não aparece");
            }

            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Available));
        }

        [Test]
        public void PromptNeverShowsEAvailableWhenTheServiceWouldRefuseTheAction()
        {
            var random = new System.Random(2004);
            var ids = new[] { "a", "b", "c" };
            var held = false;
            for (var step = 0; step < 1500; step++)
            {
                switch (random.Next(8))
                {
                    case 0:
                        service.SetCandidate(Candidate(ids[random.Next(3)], hold: random.Next(2) == 0 ? 0f : 1f + random.Next(3),
                            priority: random.Next(3), distance: random.Next(1, 20), available: random.Next(3) != 0));
                        break;
                    case 1:
                        service.ClearCandidate(ids[random.Next(3)]);
                        break;
                    case 2:
                        router.FeedInteract(true);
                        held = true;
                        break;
                    case 3:
                        router.FeedInteract(false);
                        held = false;
                        break;
                    case 4:
                        service.RequireRelease();
                        break;
                    case 5:
                        service.CancelHold();
                        break;
                    default:
                        break;
                }

                Frame((float)random.NextDouble() * 0.6f);

                if (prompt.State == UI.PromptState.Available || prompt.State == UI.PromptState.Paused)
                {
                    Assert.That(service.CanBeginHold(prompt.Model.CandidateId), Is.True,
                        $"passo {step}: o prompt mostrou E disponível para '{prompt.Model.CandidateId}' mas o serviço recusaria (held={held})");
                    Assert.That(service.AwaitingRelease, Is.False, $"passo {step}: E disponível durante o rearme");
                }

                if (prompt.State == UI.PromptState.Holding)
                    Assert.That(service.HoldingId, Is.EqualTo(prompt.Model.CandidateId), $"passo {step}: Holding de outro candidato");

                if (service.AwaitingRelease && service.TryGetActive(out _) && prompt.State != UI.PromptState.Done && prompt.State != UI.PromptState.Command)
                    Assert.That(prompt.State == UI.PromptState.Rearm || prompt.State == UI.PromptState.Holding, Is.True, $"passo {step}: rearme não apresentado como rearme");
            }
        }

        // ---------- Início, soltura e conclusão ----------

        [Test]
        public void StartReleaseAndCompletionThroughTheRealServiceAreThreeDistinctFeedbacks()
        {
            service.SetCandidate(Candidate("a", hold: 2f));
            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Available));

            router.FeedInteract(true);
            Frame(1f);
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Holding), "início");
            Assert.That(PromptText("prompt-status"), Is.EqualTo("OPERANDO · 50%"));
            Assert.That(tree.Q<VisualElement>("prompt").ClassListContains("sa-hold--holding"), Is.True);

            router.FeedInteract(false);
            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Paused), "soltura");
            Assert.That(PromptText("prompt-status"), Is.EqualTo("PAUSADO · 50% PRESERVADO"));
            Assert.That(PromptText("prompt-label"), Is.EqualTo("Segure E para continuar"));
            Assert.That(tree.Q<VisualElement>("prompt").ClassListContains("sa-hold--paused"), Is.True);
            Assert.That(tree.Q<VisualElement>("prompt").ClassListContains("sa-hold--holding"), Is.False, "uma classe de estado por vez");

            router.FeedInteract(true);
            Frame(0.016f);
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Holding));
            Frame(5f);
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Done), "conclusão");
            Assert.That(PromptText("prompt-status"), Is.EqualTo("CONCLUÍDO ✓"));
            Assert.That(tree.Q<VisualElement>("prompt").ClassListContains("sa-hold--done"), Is.True);
        }

        [Test]
        public void KeepingEPressedAfterCompletionNeverOffersTheNextActivityAsAvailable()
        {
            service.SetCandidate(Candidate("a", hold: 1f));
            service.HoldCompleted += id =>
            {
                if (id != "a")
                    return;

                service.ClearCandidate("a");
                service.SetCandidate(Candidate("b", hold: 1f));
            };

            router.FeedInteract(true);
            Frame(1f);
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Done));

            Frame(2f);
            Assert.That(service.AwaitingRelease, Is.True);
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Rearm), "E segue pressionado: a próxima atividade espera o rearme");
            Assert.That(PromptText("prompt-label"), Is.EqualTo("Solte E para começar"));

            router.FeedInteract(false);
            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Available));
        }

        [Test]
        public void ToggleModeChangesTheWordingNotTheRules()
        {
            service.SetCandidate(Candidate("a", hold: 2f));
            Frame(0.016f, "en", HoldMode.Toggle);
            Assert.That(PromptText("prompt-label"), Is.EqualTo("Press E · grind the rock"));
            Frame(0.016f, "pt", HoldMode.Hold);
            Assert.That(PromptText("prompt-label"), Is.EqualTo("Segure E · esmerilhar a rocha"));
        }

        [Test]
        public void LanguageChangeRelabelsWithoutRestartingTheFeedback()
        {
            service.SetCandidate(Candidate("a", hold: 2f));
            router.FeedInteract(true);
            Frame(1f, "pt");
            Assert.That(PromptText("prompt-status"), Is.EqualTo("OPERANDO · 50%"));
            Frame(0f, "en");
            Assert.That(PromptText("prompt-status"), Is.EqualTo("WORKING · 50%"));
            Assert.That(PromptText("prompt-label"), Is.EqualTo("Hold E · grind the rock"));
        }

        [Test]
        public void InstrumentProgressIsShownWhenTheHoldResumesWithTheRemainingTime()
        {
            // Como a Surveyor: o hold é retomado só com o tempo restante, mas o progresso físico já é 60%.
            service.SetCandidate(Candidate("a", hold: 0.8f));
            prompt.Model.ReportProgress("a", 0.6f);
            router.FeedInteract(true);
            Frame(0.4f);
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Holding));
            Assert.That(PromptText("prompt-status"), Is.EqualTo("OPERANDO · 60%"), "vale o progresso físico, não o do hold atual");
        }

        // ---------- Comando em trânsito (Surveyor) ----------

        [Test]
        public void CommandInTransitReplacesTheEPromptAndTheEReturnsAfterwards()
        {
            service.SetCandidate(Candidate("a"));
            Frame();
            prompt.Model.SetCommand(new UI.HudCommandState(UI.HudCommandStage.InTransit, 0.5f, 0.7f));
            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Command));
            Assert.That(PromptText("prompt-label"), Is.EqualTo("Comando em trânsito · 0,7 s"));
            Assert.That(PromptText("prompt-status"), Is.EqualTo("EM TRÂNSITO · 50%"));
            Assert.That(tree.Q<VisualElement>("prompt").ClassListContains("sa-hold--command"), Is.True);

            prompt.Model.ClearCommand();
            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Available));
            Assert.That(tree.Q<VisualElement>("prompt").ClassListContains("sa-hold--command"), Is.False);
        }

        // ---------- Orientação: âncora, seta de borda, distância ----------

        UI.HudObjective Objective(Transform anchor, string candidateId = null, int step = 2, int stepCount = 3) =>
            new UI.HudObjective
            {
                Id = "tst.objective", StampKey = "TST.STAMP", LabelKey = "TST.OBJECTIVE", TargetNameKey = "TST.TARGET",
                Anchor = anchor, CandidateId = candidateId, Step = step, StepCount = stepCount, AccentClass = "sa-chapter--mars",
            };

        void Orient(string language = "pt", UI.HudLayers? layers = null, bool candidateActive = false, InteractionCandidate candidate = default) =>
            indicator.Tick(camera, reference, candidateActive, in candidate, layers ?? UI.HudVisibility.For(true, UI.HudInputContext.Gameplay), texts, language);

        [Test]
        public void TargetInFrontOfTheCameraGetsAWorldAnchorAndAFarOneGetsAnEdgeArrow()
        {
            indicator.SetObjective(Objective(Target(0f, 0f, 10f)));
            Orient();
            Assert.That(indicator.IsMarkerShown, Is.True, "alvo no quadro: âncora no mundo");
            Assert.That(indicator.IsEdgeShown, Is.False);

            indicator.SetObjective(Objective(Target(80f, 0f, 10f)));
            Orient();
            Assert.That(indicator.IsMarkerShown, Is.False);
            Assert.That(indicator.IsEdgeShown, Is.True, "alvo fora do quadro: seta de borda");
            Assert.That(indicator.LastCue.Side, Is.EqualTo(UI.EdgeSide.Right));
            Assert.That(tree.Q<Label>("edge-label").text, Is.EqualTo("McKittrick · 81 m"), "nome do alvo e distância (80,6 m) acompanham a seta");
        }

        [Test]
        public void TargetBehindTheCameraNeverGetsAMarker()
        {
            indicator.SetObjective(Objective(Target(0f, 0f, -12f)));
            Orient();
            Assert.That(indicator.IsMarkerShown, Is.False, "atrás da câmera a projeção espelhada não pode virar âncora");
            Assert.That(indicator.IsEdgeShown, Is.True);
        }

        [Test]
        public void ChangingTheFramingWhileTheVehicleMovesSwitchesBetweenAnchorAndArrowWithoutStaleState()
        {
            var target = Target(0f, 0f, 10f);
            indicator.SetObjective(Objective(target));
            for (var step = 0; step < 40; step++)
            {
                camera.transform.rotation = Quaternion.Euler(0f, step * 9f + 1f, 0f); // gira em volta do eixo vertical; +1° evita o plano da câmera (profundidade zero)
                reference.position = new Vector3(0f, 0f, step * 0.1f);
                Orient();
                Assert.That(indicator.LastCue.Valid, Is.True, $"giro {step}");
                Assert.That(indicator.IsMarkerShown != indicator.IsEdgeShown, Is.True, $"giro {step}: exatamente uma orientação (âncora XOR seta)");
                Assert.That(indicator.IsMarkerShown, Is.EqualTo(indicator.LastCue.OnScreen), $"giro {step}");
            }
        }

        [Test]
        public void OrientationHidesInPauseAndCinematicButThePlateStaysInPause()
        {
            indicator.SetObjective(Objective(Target(80f, 0f, 10f)));
            Orient();
            Assert.That(indicator.IsEdgeShown, Is.True);
            Assert.That(indicator.IsPlateShown, Is.True);

            Orient(layers: UI.HudVisibility.For(true, UI.HudInputContext.Paused));
            Assert.That(indicator.IsEdgeShown, Is.False, "pausa esconde a orientação");
            Assert.That(indicator.IsMarkerShown, Is.False);
            Assert.That(indicator.IsPlateShown, Is.True, "mas o objetivo continua legível");

            Orient(layers: UI.HudVisibility.For(true, UI.HudInputContext.Cinematic));
            Assert.That(indicator.IsEdgeShown || indicator.IsMarkerShown || indicator.IsPlateShown, Is.False, "cinemática esconde tudo");
        }

        [Test]
        public void ObjectiveDistanceComesFromTheCandidateWhenTheCandidateIsTheObjectiveTarget()
        {
            var anchor = Target(0f, 0f, 30f);
            reference.position = Vector3.zero; // medida geométrica seria 30 m
            indicator.SetObjective(Objective(anchor, candidateId: "c1"));
            var candidate = Candidate("c1", distance: 3f, available: true);
            Orient(candidateActive: true, candidate: candidate);
            Assert.That(tree.Q<Label>("objective-meta").text, Is.EqualTo("2 / 3 · McKittrick · 3 m"), "a mesma distância que decide o E");

            Orient(candidateActive: false);
            Assert.That(tree.Q<Label>("objective-meta").text, Is.EqualTo("2 / 3 · McKittrick · 30 m"), "sem o candidato, medida da referência");
        }

        [Test]
        public void TheAvailableCandidateAnchorWinsOverTheObjectiveAnchor()
        {
            var far = Target(80f, 0f, 10f);
            var near = Target(0f, 0f, 4f);
            indicator.SetObjective(Objective(far));
            Orient();
            Assert.That(indicator.IsEdgeShown, Is.True, "só o objetivo: seta para o alvo distante");

            Orient(candidateActive: true, candidate: Candidate("c", distance: 4f, anchor: near));
            Assert.That(indicator.IsMarkerShown, Is.True, "o E vai agir no candidato próximo: a âncora fica nele");
            Assert.That(indicator.IsEdgeShown, Is.False, "um alvo só, sem duas orientações disputando");

            Orient(candidateActive: true, candidate: Candidate("c", distance: 4f, available: false, anchor: near));
            Assert.That(indicator.IsEdgeShown, Is.True, "candidato indisponível volta ao objetivo");
        }

        [Test]
        public void ObjectiveTextIsPortugueseOrEnglishAndInvalidObjectivesAreRejected()
        {
            indicator.SetObjective(Objective(null, step: 0, stepCount: 0));
            Orient("pt");
            Assert.That(tree.Q<Label>("objective-label").text, Is.EqualTo("Prepare e analise a rocha"));
            Orient("en");
            Assert.That(tree.Q<Label>("objective-label").text, Is.EqualTo("Prepare and analyze the rock"));
            Assert.That(tree.Q<Label>("objective-meta").text, Is.EqualTo("McKittrick"), "sem âncora nem referência: só o nome");

            var invalid = Objective(null);
            invalid.Id = "";
            Assert.Throws<ArgumentException>(() => indicator.SetObjective(invalid));
            invalid = Objective(null);
            invalid.AccentClass = "mars";
            Assert.Throws<ArgumentException>(() => indicator.SetObjective(invalid));
            invalid = Objective(null, step: 5, stepCount: 3);
            Assert.Throws<ArgumentException>(() => indicator.SetObjective(invalid));
        }

        [Test]
        public void PresentersBindWhenGivenTheHudRootElementItselfAsTheMissionHudDoes()
        {
            // Q() só enxerga descendentes: o MissionHUD entrega o hud-root, não o contêiner pai.
            var hudRoot = tree.Q<VisualElement>("hud-root");
            Assert.That(hudRoot != null, Is.True);
            using (var rootPrompt = new UI.InteractionPrompt(hudRoot, service))
            {
                var rootIndicator = new UI.ObjectiveIndicator(hudRoot);
                Assert.That(rootPrompt.State, Is.EqualTo(UI.PromptState.Hidden));
                Assert.That(rootIndicator.HasObjective, Is.False);
            }
        }

        // ---------- Nada vaza entre capítulos ----------

        [Test]
        public void ResetAndClearLeaveNothingForTheNextChapter()
        {
            service.SetCandidate(Candidate("a"));
            router.FeedInteract(true);
            Frame(1f);
            router.FeedInteract(false);
            Frame();
            prompt.Model.SetCommand(new UI.HudCommandState(UI.HudCommandStage.Sent));
            indicator.SetObjective(Objective(Target(0f, 0f, 10f)));
            Orient();

            service.ClearCandidate("a");
            prompt.Model.Reset();
            indicator.Clear();
            Frame();
            Orient();
            Assert.That(prompt.IsShown, Is.False);
            Assert.That(indicator.HasObjective, Is.False);
            Assert.That(indicator.IsPlateShown, Is.False);
            Assert.That(indicator.IsMarkerShown || indicator.IsEdgeShown, Is.False);

            service.SetCandidate(Candidate("a"));
            Frame();
            Assert.That(prompt.State, Is.EqualTo(UI.PromptState.Available), "sem 'pausado' nem comando herdados");
        }

        [Test]
        public void DisposedPromptStopsListeningToTheService()
        {
            service.SetCandidate(Candidate("a"));
            prompt.Dispose();
            router.FeedInteract(true);
            service.Tick(1f);
            Assert.That(prompt.Model.State, Is.Not.EqualTo(UI.PromptState.Holding));
            prompt = new UI.InteractionPrompt(tree, service); // o TearDown descarta o novo
        }
    }
}
