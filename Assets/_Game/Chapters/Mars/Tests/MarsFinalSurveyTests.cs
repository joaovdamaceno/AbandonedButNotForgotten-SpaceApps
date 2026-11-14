using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-008: mirante, Sol, vale, antena e escolha da memória (M24–M30). Prova: chegada ao mirante; 4 s de E por captura com
    /// enquadramento automático; só o vale é foto do jogador (o Sol vira registro científico com comparação lado a lado);
    /// transmissão com prioridade e sem girar o chassi; escolha com setas, foto do vale por padrão, sem tempo limite e sem o E
    /// da antena confirmar; imagens liberadas ao reiniciar; pausa, foco e a cadeia completa.
    /// </summary>
    public class MarsFinalSurveyTests
    {
        const string PrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/FinalSurveyInteractions.prefab";
        const string DataPath = "Assets/_Game/Chapters/Mars/Data/FinalSurvey.asset";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Data/MarsFinalSurveyTexts.json";
        const float Frame = 0.05f;

        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
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
        InputRouter router;
        InteractionService interaction;
        FakeTime time;
        MarsFinalSurvey survey;
        MarsTransmission transmission;
        FinalSurveyData data;
        MarsFinalSurvey.Anchors anchors;
        Transform op;
        Transform gimbal;
        Transform earth;
        Transform chassis;
        Camera camera;
        MarsChapterFlow flow;
        MockChapterDirector director;
        EvidenceStore store;
        IDisposable gameplay;
        List<string> events;

        [SetUp]
        public void SetUp()
        {
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            data = AssetDatabase.LoadAssetAtPath<FinalSurveyData>(DataPath);
            Assert.That(data, Is.Not.Null, DataPath);

            var services = Track(new GameObject("services (MarsFinalSurveyTests)"));
            router = services.AddComponent<InputRouter>();
            interaction = services.AddComponent<InteractionService>();
            time = new FakeTime();
            interaction.Initialize(router, time);
            gameplay = router.Acquire(InputContext.Gameplay, "teste");

            camera = Track(new GameObject("camera")).AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 2f, -6f);
            anchors = new MarsFinalSurvey.Anchors
            {
                overlook = Track(new GameObject("mirante")).transform,
                sunView = Track(new GameObject("vista do Sol")).transform,
                valleyView = Track(new GameObject("vista do vale")).transform,
            };
            anchors.sunView.SetPositionAndRotation(new Vector3(0f, 1.5f, -1f), Quaternion.Euler(-40f, 0f, 0f));
            anchors.valleyView.SetPositionAndRotation(new Vector3(0f, 1.6f, -2f), Quaternion.Euler(8f, 20f, 0f));
            op = Track(new GameObject("operador")).transform;
            op.position = new Vector3(0f, 0f, -12f);
            chassis = Track(new GameObject("chassi do rover")).transform;
            chassis.position = new Vector3(1f, 0f, -3f);
            chassis.rotation = Quaternion.Euler(0f, 33f, 0f);
            gimbal = Track(new GameObject("articulação da antena")).transform;
            gimbal.position = new Vector3(1f, 1.4f, -3f);
            earth = Track(new GameObject("Terra")).transform;
            earth.position = new Vector3(-30f, 25f, 40f);

            var root = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
            survey = root.GetComponent<MarsFinalSurvey>();
            transmission = root.GetComponent<MarsTransmission>();

            director = new MockChapterDirector();
            flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            events = new List<string>();
            flow.EventAccepted += (_, e) => events.Add(e);
            store = new EvidenceStore();
            survey.Initialize(flow, interaction, time, anchors, op, camera, store);
            transmission.Initialize(flow, interaction, time, router, store, gimbal, earth);
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
            store.Clear();
        }

        GameObject Track(GameObject go)
        {
            hosts.Add(go);
            return go;
        }

        static void Feed(InputRouter target, string method, params object[] args) =>
            typeof(InputRouter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        void Step(float dt)
        {
            time.ActiveDeltaSeconds = dt;
            interaction.Tick(dt);
            survey.Tick(dt);
            transmission.Tick(dt);
            flow.Tick(dt);
            Feed(router, "EndFrame");
        }

        void Run(float seconds)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += Frame)
                Step(Frame);
        }

        void PlayUntil(string stepId)
        {
            director.Begin(chapter);
            var guard = 0;
            while (director.CurrentStepId != stepId && guard++ < 64)
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                {
                    flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
                    continue;
                }

                foreach (var e in binding.CompletionEvents)
                    flow.Raise(e);
                for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                    flow.Tick(Frame);
            }

            Assert.That(director.CurrentStepId, Is.EqualTo(stepId));
        }

        void Hold(float seconds)
        {
            Feed(router, "FeedInteract", true);
            Run(seconds);
            Feed(router, "FeedInteract", false);
            Step(Frame);
        }

        void AtOverlook() => op.position = anchors.overlook.position + new Vector3(0f, 0f, -2f);

        void FinishCinematicIfAny()
        {
            var binding = flow.CurrentBinding;
            if (binding != null && binding.IsCinematic)
                flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
        }

        /// <summary>Em M25 (Sol de referência), pronto para segurar E.</summary>
        void ReachSun()
        {
            PlayUntil("M24");
            Step(Frame);
            AtOverlook();
            Run(8f); // fala C39 (7 s) com o rover parado
            Assert.That(director.CurrentStepId, Is.EqualTo("M25"));
            Step(Frame);
        }

        void ReachValley()
        {
            ReachSun();
            Hold(4.2f);
            Run(8f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M26"));
            Step(Frame);
        }

        void ReachDust()
        {
            ReachValley();
            Hold(4.2f);
            Run(8f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M27"));
            FinishCinematicIfAny();
            Run(0.2f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M28"));
            Step(Frame);
        }

        void ReachAntenna()
        {
            ReachDust();
            Hold(4.2f);
            Run(9f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M29"));
            Step(Frame);
        }

        /// <summary>Em M30 com a antena concluída e o E já solto.</summary>
        void ReachMemory()
        {
            ReachAntenna();
            Hold(4.2f);
            Run(7f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M30"));
            Step(Frame);
        }

        // ---------- Dados ----------

        [Test]
        public void DataAndPrefabValidateAndImagesRespectTheBudget()
        {
            Assert.That(survey.ValidateWiring(), Is.Empty, ContractValidation.Describe(survey.ValidateWiring()));
            Assert.That(transmission.ValidateWiring(), Is.Empty, ContractValidation.Describe(transmission.ValidateWiring()));
            Assert.That(data.Validate(), Is.Empty);
            Assert.That(data.sunHoldSeconds, Is.EqualTo(4f));
            Assert.That(data.valleyHoldSeconds, Is.EqualTo(4f));
            Assert.That(data.valleyWidth, Is.LessThanOrEqualTo(960));
            Assert.That(data.valleyHeight, Is.LessThanOrEqualTo(540));
            Assert.That(data.sunWidth * 2, Is.LessThanOrEqualTo(960), "a comparação lado a lado cabe em 960");

            var big = UnityEngine.Object.Instantiate(data);
            big.sunWidth = 480;
            big.valleyWidth = 960;
            Assert.That(big.Validate(), Is.Empty);
            UnityEngine.Object.DestroyImmediate(big);
        }

        [Test]
        public void TextsExistInBothLanguagesWithNoNumbersAndTheTransmissionIsNotALiteralMessage()
        {
            var table = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath).text);
            var keys = new[] { data.sunVerbKey, data.valleyVerbKey, data.antennaVerbKey, data.confirmVerbKey, data.sunObjectKey, data.valleyObjectKey,
                data.antennaObjectKey, data.confirmObjectKey, data.sunReferenceResultKey, data.sunDustResultKey, data.sunComparisonResultKey,
                data.valleyPhotoResultKey, data.memoryValleyKey, data.memoryScienceKey, data.transmissionNoteKey };
            foreach (var key in keys)
            {
                var entry = table.texts.FirstOrDefault(t => t.key == key);
                Assert.That(entry, Is.Not.Null, key);
                Assert.That(string.IsNullOrWhiteSpace(entry.pt), Is.False, key + " PT");
                Assert.That(string.IsNullOrWhiteSpace(entry.en), Is.False, key + " EN");
                Assert.That(Regex.IsMatch(entry.pt + entry.en, @"\d"), Is.False, key + ": sem números inventados");
            }

            var note = table.texts.First(t => t.key == data.transmissionNoteKey);
            Assert.That(note.pt.ToLowerInvariant(), Does.Contain("não é a comunicação literal"));
            Assert.That(flow.Subtitles.TryGetText("MAR.O08", "pt", out var objective), Is.True);
            Assert.That(objective, Does.Contain("transmissão"));
        }

        // ---------- M24 ----------

        [Test]
        public void M24RequiresReachingTheOverlookAndParksTheRover()
        {
            PlayUntil("M24");
            Step(Frame);
            var far = survey.DistanceToOverlookMeters;
            Run(4f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M24"), "o tempo não conclui");
            Assert.That(interaction.TryGetActive(out _), Is.False, "M24 não tem E");
            Assert.That(far, Is.GreaterThan(data.reachMeters));

            op.position = anchors.overlook.position + new Vector3(0f, 0f, -4f);
            Run(1f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M24"), "4 m ainda é longe");
            Assert.That(survey.DistanceToOverlookMeters, Is.LessThan(far), "o HUD vê a distância diminuir");

            AtOverlook();
            Run(0.3f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.OverlookReached), Is.EqualTo(1));
            Assert.That(survey.IsEngaged, Is.True, "rover parado no mirante");
            Run(7f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M25"));
        }

        // ---------- M25 e M28 · Sol ----------

        [Test]
        public void SunReferenceTakesFourSecondsOfEWithAutomaticFramingAndKeepsWorkWhenReleased()
        {
            var views = new List<Transform>();
            survey.ViewChanged += views.Add;
            ReachSun();
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MarsFinalSurvey.SunCandidateId));
            Assert.That(c.VerbKey, Is.EqualTo(data.sunVerbKey));
            Assert.That(views.Last(), Is.SameAs(anchors.sunView), "enquadramento automático: nada para clicar no disco solar");

            Run(5f);
            Assert.That(events, Does.Not.Contain(MarsChapterFlow.Events.SunReferenceCaptured), "sem E nada acontece");
            Feed(router, "FeedInteract", true);
            Run(1.5f);
            Feed(router, "FeedInteract", false);
            Run(1f);
            Assert.That(survey.WorkSeconds, Is.EqualTo(1.5f).Within(0.15f), "soltar conserva");
            Hold(2.7f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.SunReferenceCaptured), Is.EqualTo(1));
        }

        [Test]
        public void SunRecordsAreScientificObservationsWithImagesNeverPlayerPhotos()
        {
            ReachSun();
            Hold(4.2f);
            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(), Is.EqualTo(new[] { data.sunReferenceId }));
            var reference = store.Records[0];
            Assert.That(reference.Kind, Is.EqualTo(EvidenceKind.Observation));
            Assert.That(reference.CanBePresentedAsPlayers, Is.False, "o Sol não gasta uma das três fotos do jogador");
            Assert.That(reference.Thumbnail, Is.Not.Null, "o registro tem imagem");
            Assert.That(reference.Thumbnail.rect.width, Is.EqualTo(data.sunWidth));
            Assert.That(reference.Thumbnail.rect.height, Is.EqualTo(data.sunHeight));
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(0));
            Assert.That(reference.Validate(), Is.Empty);
        }

        [Test]
        public void DustSunIsComparedSideBySideWithTheFirstImage()
        {
            ReachDust();
            Hold(4.2f);
            var ids = store.Records.Select(r => r.EvidenceId).ToList();
            Assert.That(ids, Is.EqualTo(new[] { data.sunReferenceId, data.valleyPhotoId, data.sunDustId, data.sunComparisonId }));
            var comparison = store.Records.First(r => r.EvidenceId == data.sunComparisonId);
            Assert.That(comparison.Kind, Is.EqualTo(EvidenceKind.Observation));
            Assert.That(comparison.CanBePresentedAsPlayers, Is.False);
            Assert.That(comparison.Thumbnail.rect.width, Is.EqualTo(data.sunWidth * 2), "as duas imagens lado a lado");
            Assert.That(comparison.Thumbnail.rect.width, Is.LessThanOrEqualTo(EvidenceRecord.MaxThumbnailWidth));
            Assert.That(comparison.Thumbnail.rect.height, Is.LessThanOrEqualTo(EvidenceRecord.MaxThumbnailHeight));
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(1), "só o vale é foto do jogador");
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.SunDustCaptured), Is.EqualTo(1));
        }

        // ---------- M26 · vale ----------

        [Test]
        public void ValleyIsTheOnlyPlayerPhotoAndTakesFourSecondsOfE()
        {
            ReachValley();
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MarsFinalSurvey.ValleyCandidateId));
            Assert.That(c.VerbKey, Is.EqualTo(data.valleyVerbKey));
            Run(4f);
            Assert.That(survey.ValleyPhoto, Is.Null, "sem E não há foto");
            Hold(4.2f);
            var valley = store.Records.First(r => r.EvidenceId == data.valleyPhotoId);
            Assert.That(valley.Kind, Is.EqualTo(EvidenceKind.Photo));
            Assert.That(valley.CanBePresentedAsPlayers, Is.True);
            Assert.That(valley.Thumbnail.rect.width, Is.LessThanOrEqualTo(960));
            Assert.That(valley.Thumbnail.rect.height, Is.LessThanOrEqualTo(540));
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(1));
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ValleyCaptured), Is.EqualTo(1));
        }

        [Test]
        public void ValleyFitsAsTheThirdPhotoAfterTheShieldAndTheMeteorite()
        {
            store.Add(new EvidenceRecord("mars.ev.shield.photo", EvidenceKind.Photo, "MAR.SHIELD.photo", "MS15", EvidenceAuthorship.PlayerCapture));
            store.Add(new EvidenceRecord("mars.ev.meteorite.photo", EvidenceKind.Photo, "MAR.METEORITE.photo", "MS14", EvidenceAuthorship.PlayerCapture));
            ReachValley();
            Hold(4.2f);
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(EvidenceStore.MaxPlayerPhotos), "a vaga reservada ao vale existe de fato");
            Assert.That(store.Contains(data.valleyPhotoId), Is.True);
        }

        [Test]
        public void PhotoLimitNeverBlocksTheValleyStep()
        {
            for (var i = 0; i < EvidenceStore.MaxPlayerPhotos; i++)
                store.Add(new EvidenceRecord("mars.ev.fill." + i, EvidenceKind.Photo, "MAR.FILL", "MS01", EvidenceAuthorship.PlayerCapture));

            ReachValley();
            LogAssert.Expect(LogType.Warning, new Regex("MAR-008.*mars.ev.valley.photo"));
            Hold(4.2f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ValleyCaptured), Is.EqualTo(1), "a etapa conclui mesmo sem poder guardar");
        }

        [Test]
        public void OptionalFrameAdjustmentIsClampedAndNeverRequired()
        {
            ReachValley();
            survey.AdjustFrame(500f, -500f);
            Assert.That(survey.FrameAdjustYawDegrees, Is.EqualTo(data.maxFrameAdjustDegrees), "limitado");
            Assert.That(survey.FrameAdjustPitchDegrees, Is.EqualTo(-data.maxFrameAdjustDegrees));
            survey.AdjustFrame(0f, 0f);
            Hold(4.2f);
            Assert.That(events, Does.Contain(MarsChapterFlow.Events.ValleyCaptured), "sem ajuste a etapa conclui com o quadro guiado");
            Assert.That(camera.targetTexture, Is.Null, "a câmera do jogo foi restaurada");
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0f, 2f, -6f)), "e voltou à pose original");
        }

        // ---------- M29 · antena ----------

        [Test]
        public void AntennaTurnsGraduallyTowardEarthNeverTheChassisAndWorksFarFromTheOverlook()
        {
            ReachAntenna();
            Assert.That(op.position.z, Is.LessThan(anchors.overlook.position.z - 1f), "o operador continua a metros do mirante");
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MarsTransmission.AntennaCandidateId), "a transmissão é o prompt prioritário");
            Assert.That(c.VerbKey, Is.EqualTo(data.antennaVerbKey));

            var chassisRotation = chassis.rotation;
            var start = gimbal.rotation;
            var previous = gimbal.rotation;
            var maxStep = 0f;
            Feed(router, "FeedInteract", true);
            for (var i = 0; i < 90; i++)
            {
                Step(Frame);
                maxStep = Mathf.Max(maxStep, Quaternion.Angle(previous, gimbal.rotation));
                previous = gimbal.rotation;
            }

            Feed(router, "FeedInteract", false);
            Step(Frame);
            var goal = Quaternion.LookRotation((earth.position - gimbal.position).normalized, Vector3.up);
            Assert.That(maxStep, Is.LessThanOrEqualTo(data.antennaDegreesPerSecond * Frame + 0.05f), "giro gradual, nunca instantâneo");
            Assert.That(Quaternion.Angle(gimbal.rotation, goal), Is.LessThan(Quaternion.Angle(start, goal) * 0.1f), "apontou para a Terra");
            Assert.That(chassis.rotation, Is.EqualTo(chassisRotation), "o chassi nunca gira");
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.AntennaPrepared), Is.EqualTo(1));
        }

        [Test]
        public void AntennaKeepsWorkWhenReleasedAndNothingReopensOptionalCaptures()
        {
            ReachAntenna();
            Feed(router, "FeedInteract", true);
            Run(1.5f);
            Feed(router, "FeedInteract", false);
            Run(2f);
            Assert.That(transmission.WorkSeconds, Is.EqualTo(1.5f).Within(0.2f));
            Assert.That(survey.Phase, Is.Not.EqualTo(FinalSurveyPhase.Valley), "a foto opcional não reabre");
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MarsTransmission.AntennaCandidateId));
            Assert.That(c.HoldSeconds, Is.EqualTo(4f - transmission.WorkSeconds).Within(0.1f));
            Hold(3f);
            Assert.That(events, Does.Contain(MarsChapterFlow.Events.AntennaPrepared));
        }

        // ---------- M30 · memória ----------

        [Test]
        public void MemoryDefaultsToTheValleyPhotoWithNoTimeout()
        {
            ReachMemory();
            Assert.That(transmission.Phase, Is.EqualTo(TransmissionPhase.ChoosingMemory));
            Assert.That(transmission.Options.ToList(), Is.EqualTo(new[] { MemoryOption.ValleyPhoto, MemoryOption.ScienceRecord }));
            Assert.That(transmission.Selected, Is.EqualTo(MemoryOption.ValleyPhoto), "foto do vale por padrão");
            Run(120f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M30"), "nenhum tempo limite escolhe a foto");
            Assert.That(events, Does.Not.Contain(MarsChapterFlow.Events.MemoryConfirmed));
            Assert.That(transmission.Selected, Is.EqualTo(MemoryOption.ValleyPhoto));
        }

        [Test]
        public void ArrowsToggleAndTheRoverCannotMoveWhileChoosing()
        {
            ReachMemory();
            Assert.That(router.Context, Is.EqualTo(InputContext.Panel), "as setas só existem no painel");
            Feed(router, "FeedMove", new Vector2(0f, 1f));
            Assert.That(router.Move, Is.EqualTo(Vector2.zero), "o rover não anda enquanto escolhe");

            Feed(router, "FeedNavigate", Vector2.right);
            Assert.That(transmission.Selected, Is.EqualTo(MemoryOption.ScienceRecord));
            Feed(router, "FeedNavigate", Vector2.right);
            Assert.That(transmission.Selected, Is.EqualTo(MemoryOption.ValleyPhoto), "alterna");
            Feed(router, "FeedNavigate", Vector2.left);
            Assert.That(transmission.Selected, Is.EqualTo(MemoryOption.ScienceRecord));
            Feed(router, "FeedNavigate", Vector2.up);
            Assert.That(transmission.Selected, Is.EqualTo(MemoryOption.ScienceRecord), "só horizontal alterna");
        }

        [Test]
        public void TheEThatFinishedTheAntennaNeverConfirmsAndConfirmationHappensOnce()
        {
            ReachAntenna();
            Feed(router, "FeedInteract", true);
            Run(4.3f); // conclui a antena com o E ainda pressionado
            Run(7f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M30"));
            Run(2f);
            Assert.That(events, Does.Not.Contain(MarsChapterFlow.Events.MemoryConfirmed), "o E que abriu não confirma");
            Assert.That(interaction.CanBeginHold(MarsTransmission.ConfirmCandidateId), Is.False);

            Feed(router, "FeedInteract", false);
            Step(Frame);
            Assert.That(interaction.CanBeginHold(MarsTransmission.ConfirmCandidateId), Is.True);
            for (var i = 0; i < 6; i++)
            {
                Feed(router, "FeedInteract", true);
                Step(Frame);
                Feed(router, "FeedInteract", false);
                Step(Frame);
            }

            Assert.That(events.Count(e => e == MarsChapterFlow.Events.MemoryConfirmed), Is.EqualTo(1), "confirma uma vez");
            Assert.That(transmission.SelectedRecord, Is.Not.Null);
            Assert.That(transmission.SelectedRecord.EvidenceId, Is.EqualTo(data.valleyPhotoId));
            Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay), "o painel é devolvido");
        }

        [Test]
        public void ChoosingTheScientificRecordSendsItToTheMemorial()
        {
            ReachMemory();
            Feed(router, "FeedNavigate", Vector2.right);
            Feed(router, "FeedInteract", true);
            Step(Frame);
            Feed(router, "FeedInteract", false);
            Step(Frame);
            Assert.That(transmission.SelectedRecord, Is.Not.Null);
            Assert.That(transmission.SelectedRecord.EvidenceId, Is.EqualTo(data.sunComparisonId));
            Assert.That(transmission.SelectedRecord.Thumbnail, Is.Not.Null, "a imagem escolhida chega ao memorial");
            Run(7f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M31"));
        }

        [Test]
        public void SelectingTheValleyPhotoHandsItsImageToTheMemorial()
        {
            ReachMemory();
            Feed(router, "FeedInteract", true);
            Step(Frame);
            Feed(router, "FeedInteract", false);
            Step(Frame);
            Assert.That(transmission.SelectedRecord.EvidenceId, Is.EqualTo(data.valleyPhotoId));
            Assert.That(transmission.SelectedRecord.Thumbnail, Is.SameAs(survey.ValleyPhoto));
            Assert.That(transmission.SelectedRecord.CanBePresentedAsPlayers, Is.True, "é a foto do jogador");
        }

        // ---------- Memória, pausa, foco e reinício ----------

        [Test]
        public void RestartingReleasesTheCreatedImagesAndThePanel()
        {
            ReachMemory();
            var valley = survey.ValleyPhoto;
            var comparison = store.Records.First(r => r.EvidenceId == data.sunComparisonId).Thumbnail;
            var reference = survey.SunReferenceImage;
            Assert.That(valley != null && comparison != null && reference != null, Is.True);
            var valleyTexture = valley.texture;
            var comparisonTexture = comparison.texture;

            director.RestartChapter();
            Assert.That(valley == null, Is.True, "o sprite do vale foi liberado");
            Assert.That(valleyTexture == null, Is.True, "e a textura dele");
            Assert.That(comparison == null && comparisonTexture == null, Is.True, "a comparação também");
            Assert.That(reference == null, Is.True);
            Assert.That(router.Context, Is.EqualTo(InputContext.Gameplay), "painel devolvido ao reiniciar");
            Assert.That(transmission.Phase, Is.EqualTo(TransmissionPhase.Idle));
            Assert.That(survey.Phase, Is.EqualTo(FinalSurveyPhase.Idle));
            Assert.That(interaction.TryGetActive(out _), Is.False);
        }

        [Test]
        public void RestartingAfterTheMemoryWasConfirmedLeavesNoResidualState()
        {
            ReachMemory();
            Hold(0.1f);
            Assert.That(transmission.Phase, Is.EqualTo(TransmissionPhase.Done));
            director.RestartChapter();
            Assert.That(transmission.SelectedRecord, Is.Null);
            Assert.That(transmission.Phase, Is.EqualTo(TransmissionPhase.Idle));
            Assert.That(survey.IsEngaged, Is.False);
            Assert.That(survey.ValleyPhoto == null, Is.True);
        }

        [Test]
        public void PauseFreezesTheCaptureAndFocusLossKeepsTheWork()
        {
            ReachValley();
            Feed(router, "FeedInteract", true);
            Run(2f);
            var worked = survey.WorkSeconds;
            for (var i = 0; i < 60; i++)
                Step(0f);
            Assert.That(survey.WorkSeconds, Is.EqualTo(worked), "pausa");
            router.NotifyFocusLost();
            Run(3f);
            Assert.That(survey.IsHolding, Is.False);
            Assert.That(survey.WorkSeconds, Is.EqualTo(worked).Within(1e-4f), "perder foco conserva");
            Assert.That(events, Does.Not.Contain(MarsChapterFlow.Events.ValleyCaptured));
        }

        // ---------- Cadeia completa ----------

        [Test]
        public void FullChainFromTheOverlookToTheLastSignalWithOnlyTheValleyAsAPlayerPhoto()
        {
            ReachMemory();
            Feed(router, "FeedInteract", true);
            Step(Frame);
            Feed(router, "FeedInteract", false);
            Run(7f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M31"));
            var order = events.Where(e => e.StartsWith("mars.overlook.") || e.StartsWith("mars.sun.") || e.StartsWith("mars.valley.")
                || e.StartsWith("mars.antenna.") || e.StartsWith("mars.memory.")).ToList();
            Assert.That(order, Is.EqualTo(new[]
            {
                MarsChapterFlow.Events.OverlookReached, MarsChapterFlow.Events.SunReferenceCaptured, MarsChapterFlow.Events.ValleyCaptured,
                MarsChapterFlow.Events.SunDustCaptured, MarsChapterFlow.Events.AntennaPrepared, MarsChapterFlow.Events.MemoryConfirmed,
            }), "uma vez cada, na ordem do roteiro");
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(1));
            Assert.That(store.Records.Count, Is.EqualTo(4), "referência, vale, poeira e comparação");
        }
    }
}
