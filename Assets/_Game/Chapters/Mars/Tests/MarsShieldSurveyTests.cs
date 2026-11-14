using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-006: escudo e meteorito como descobertas distintas (M16–M19). Prova: mesmo círculo E com verbos e objetos distintos;
    /// 4 s no escudo e 6 s + 4 s no meteorito; um resultado nunca conclui o outro; imagens reais creditadas fora da conta de fotos
    /// do jogador; RAT vetada no metal; pausa, foco, reinício e o limite de 3 fotos sem travar a etapa.
    /// </summary>
    public class MarsShieldSurveyTests
    {
        const string InteractionsPath = "Assets/_Game/Chapters/Mars/Prefabs/ShieldInteractions.prefab";
        const string FieldPath = "Assets/_Game/Chapters/Mars/Prefabs/ShieldFieldEnvironment.prefab";
        const string RatPath = "Assets/_Game/Chapters/Mars/Prefabs/RatActivity.prefab";
        const string DataPath = "Assets/_Game/Chapters/Mars/Data/HeatShieldResults.asset";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Data/MarsShieldTexts.json";
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
        ShieldSurveyActivity shield;
        MeteoriteActivity meteorite;
        MarsRatActivity rat;
        ShieldFieldAnchors anchors;
        HeatShieldResultsData data;
        Transform op;
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
            data = AssetDatabase.LoadAssetAtPath<HeatShieldResultsData>(DataPath);
            Assert.That(data, Is.Not.Null, DataPath);

            var services = Track(new GameObject("services (MarsShieldSurveyTests)"));
            router = services.AddComponent<InputRouter>();
            interaction = services.AddComponent<InteractionService>();
            time = new FakeTime();
            interaction.Initialize(router, time);
            gameplay = router.Acquire(InputContext.Gameplay, "teste");

            var field = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FieldPath)));
            anchors = field.GetComponent<ShieldFieldAnchors>();
            var root = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(InteractionsPath)));
            shield = root.GetComponent<ShieldSurveyActivity>();
            meteorite = root.GetComponent<MeteoriteActivity>();
            var ratRoot = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RatPath)));
            rat = ratRoot.GetComponent<MarsRatActivity>();
            op = Track(new GameObject("operador")).transform;
            op.position = anchors.ApproachStart.position;

            director = new MockChapterDirector();
            flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            events = new List<string>();
            flow.EventAccepted += (_, e) => events.Add(e);
            store = new EvidenceStore();
            shield.Initialize(flow, interaction, time, anchors, op, store);
            meteorite.Initialize(flow, interaction, time, anchors, op, store);
            rat.Initialize(flow, interaction, time, op);
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

        static void Feed(InputRouter target, string method, params object[] args) =>
            typeof(InputRouter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        void Step(float dt)
        {
            time.ActiveDeltaSeconds = dt;
            interaction.Tick(dt);
            shield.Tick(dt);
            meteorite.Tick(dt);
            rat.Tick(dt);
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

        void AtShield() => op.position = anchors.ShieldStation.position;
        void AtRock() => op.position = anchors.MeteoriteStation.position;

        /// <summary>Em M17, pronto para fotografar (E solto).</summary>
        void ReachPhotoStep()
        {
            PlayUntil("M16");
            Step(Frame);
            AtShield();
            Run(8f); // fala C21 (7 s) com o rover parado
            Assert.That(director.CurrentStepId, Is.EqualTo("M17"));
            Step(Frame);
        }

        void Hold(float seconds)
        {
            Feed(router, "FeedInteract", true);
            Run(seconds);
            Feed(router, "FeedInteract", false);
            Step(Frame);
        }

        /// <summary>Conclui o escudo e chega a M19, pronto para medir.</summary>
        void ReachMeasureStep()
        {
            ReachPhotoStep();
            Hold(4.2f);
            Run(8f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M18"));
            AtRock();
            Run(6f); // fala C23 (5 s)
            Assert.That(director.CurrentStepId, Is.EqualTo("M19"));
            Step(Frame);
        }

        // ---------- Dados ----------

        [Test]
        public void DataAndPrefabValidateAndImagesAreCreditedRealPhotosWithinBudget()
        {
            var shieldProblems = shield.ValidateWiring();
            var rockProblems = meteorite.ValidateWiring();
            Assert.That(shieldProblems, Is.Empty, ContractValidation.Describe(shieldProblems));
            Assert.That(rockProblems, Is.Empty, ContractValidation.Describe(rockProblems));
            Assert.That(data.Validate(), Is.Empty);
            foreach (var sprite in new[] { data.shieldArchiveImage, data.meteoriteArchiveImage })
            {
                Assert.That(sprite.rect.width, Is.LessThanOrEqualTo(EvidenceRecord.MaxThumbnailWidth));
                Assert.That(sprite.rect.height, Is.LessThanOrEqualTo(EvidenceRecord.MaxThumbnailHeight));
            }

            Assert.That(shield.ValidateSetup(), Is.Empty);
            Assert.That(data.shieldPhotoHoldSeconds, Is.EqualTo(4f));
            Assert.That(data.meteoriteMeasureHoldSeconds, Is.EqualTo(6f));
            Assert.That(data.meteoriteResultSeconds, Is.EqualTo(4f));
        }

        [Test]
        public void TextsExistInBothLanguagesCreditTheRealImagesAndKeepTheRockApartFromTheShield()
        {
            var table = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath).text);
            var keys = new[] { data.shieldVerbKey, data.shieldObjectKey, data.meteoriteVerbKey, data.meteoriteObjectKey, data.shieldPhotoResultKey,
                data.shieldArchiveResultKey, data.shieldArchiveCreditKey, data.meteoritePhotoResultKey, data.meteoriteArchiveResultKey, data.meteoriteArchiveCreditKey };
            foreach (var key in keys)
            {
                var entry = table.texts.FirstOrDefault(t => t.key == key);
                Assert.That(entry, Is.Not.Null, key);
                Assert.That(string.IsNullOrWhiteSpace(entry.pt), Is.False, key + " PT");
                Assert.That(string.IsNullOrWhiteSpace(entry.en), Is.False, key + " EN");
            }

            Assert.That(table.texts.First(t => t.key == data.shieldArchiveCreditKey).pt, Does.Contain("NASA/JPL/Cornell"));
            Assert.That(table.texts.First(t => t.key == data.meteoriteArchiveCreditKey).pt, Does.Contain("NASA/JPL-Caltech"));
            Assert.That(flow.Subtitles.TryGetText(data.meteoriteCompositionResultKey, "pt", out var result), Is.True);
            Assert.That(result.ToLowerInvariant(), Does.Contain("ferro").And.Contain("níquel"));

            foreach (var key in new[] { data.meteoriteObjectKey, data.meteoriteArchiveResultKey, data.meteoritePhotoResultKey })
            {
                var text = table.texts.First(t => t.key == key).pt.ToLowerInvariant();
                Assert.That(text, Does.Not.Contain("escudo"), key + ": a rocha não é pedaço do escudo");
                Assert.That(text, Does.Not.Contain("eagle"), key);
            }

            Assert.That(flow.Steps.First(s => s.StepId == "M16").StampKey, Is.EqualTo("MAR.T05"), "carimbo 2005 · campo do escudo: não é local ao lado de Eagle");
        }

        [Test]
        public void SameCircleButDistinctVerbsAndObjectsForShieldAndRock()
        {
            Assert.That(data.shieldVerbKey, Is.Not.EqualTo(data.meteoriteVerbKey));
            Assert.That(data.shieldObjectKey, Is.Not.EqualTo(data.meteoriteObjectKey));
            Assert.That(ShieldSurveyActivity.PhotoCandidateId, Is.Not.EqualTo(MeteoriteActivity.MeasureCandidateId));
        }

        // ---------- M16 e M17 · escudo ----------

        [Test]
        public void M16RequiresArrivingAtTheShieldStationAndParksTheRoverDuringTheLine()
        {
            PlayUntil("M16");
            Step(Frame);
            Run(4f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M16"), "o tempo não conclui");
            Assert.That(interaction.TryGetActive(out _), Is.False, "M16 não tem E");

            op.position = anchors.ShieldStation.position + new Vector3(0f, 0f, -3f);
            Run(1f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M16"), "3 m da estação ainda é longe");

            AtShield();
            Run(0.3f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ShieldReached), Is.EqualTo(1));
            Assert.That(shield.IsEngaged, Is.True, "rover parado durante a fala");
            Assert.That(director.CurrentStepId, Is.EqualTo("M16"), "a etapa espera a legenda C21");
            Run(7f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M17"));
        }

        [Test]
        public void M17PhotographsOnlyWithFourSecondsOfEAndKeepsWorkWhenReleased()
        {
            ReachPhotoStep();
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(ShieldSurveyActivity.PhotoCandidateId));
            Assert.That(c.VerbKey, Is.EqualTo(data.shieldVerbKey));
            Assert.That(c.ObjectKey, Is.EqualTo(data.shieldObjectKey));

            Run(5f);
            Assert.That(shield.Photographed, Is.False, "sem E nada acontece");

            Feed(router, "FeedInteract", true);
            Run(1.5f);
            Feed(router, "FeedInteract", false);
            Run(1f);
            Assert.That(shield.WorkSeconds, Is.EqualTo(1.5f).Within(0.15f), "soltar conserva");
            Assert.That(interaction.TryGetActive(out c), Is.True);
            Assert.That(c.HoldSeconds, Is.EqualTo(4f - shield.WorkSeconds).Within(0.1f));

            Hold(2.7f);
            Assert.That(shield.Photographed, Is.True);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ShieldPhotographed), Is.EqualTo(1));
        }

        [Test]
        public void PromptForTheShieldAppearsOnlyNearTheStation()
        {
            PlayUntil("M16");
            Step(Frame);
            AtShield();
            Run(8f);
            op.position = anchors.ShieldStation.position + new Vector3(0f, 0f, -6f);
            Step(Frame);
            Assert.That(interaction.CanBeginHold(ShieldSurveyActivity.PhotoCandidateId), Is.False);
            AtShield();
            Step(Frame);
            Assert.That(interaction.CanBeginHold(ShieldSurveyActivity.PhotoCandidateId), Is.True);
        }

        [Test]
        public void ShieldResultStoresYourPhotoAndTheRealCreditedImageSeparately()
        {
            ReachPhotoStep();
            Hold(4.2f);
            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(), Is.EqualTo(new[] { data.shieldPhotoId, data.shieldArchiveId }));
            var mine = store.Records[0];
            var real = store.Records[1];
            Assert.That(mine.CanBePresentedAsPlayers, Is.True);
            Assert.That(real.CanBePresentedAsPlayers, Is.False, "imagem real nunca é apresentada como sua");
            Assert.That(real.Authorship, Is.EqualTo(EvidenceAuthorship.Archive));
            Assert.That(real.CreditKey, Is.EqualTo(data.shieldArchiveCreditKey));
            Assert.That(real.Thumbnail, Is.SameAs(data.shieldArchiveImage));
            Assert.That(store.PlayerPhotoCount, Is.EqualTo(1));
        }

        [Test]
        public void ShieldAlreadyPhotographedCannotBeInteractedWithAgain()
        {
            ReachPhotoStep();
            Hold(4.2f);
            Run(8f);
            Assert.That(interaction.TryGetActive(out var c) && c.Id == ShieldSurveyActivity.PhotoCandidateId, Is.False);
            var before = store.Records.Count;
            for (var i = 0; i < 20; i++)
            {
                Feed(router, "FeedInteract", true);
                Step(0.1f);
                Feed(router, "FeedInteract", false);
                Step(0.1f);
            }

            Assert.That(store.Records.Count, Is.EqualTo(before), "nenhum registro novo");
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ShieldPhotographed), Is.EqualTo(1));
        }

        [Test]
        public void PhotoLimitNeverBlocksTheStep()
        {
            for (var i = 0; i < EvidenceStore.MaxPlayerPhotos; i++)
                store.Add(new EvidenceRecord("mars.ev.fill." + i, EvidenceKind.Photo, "MAR.FILL", "MS01", EvidenceAuthorship.PlayerCapture));

            ReachPhotoStep();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("MAR-006.*mars.ev.shield.photo"));
            Hold(4.2f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ShieldPhotographed), Is.EqualTo(1), "a etapa conclui mesmo sem poder guardar a foto");
            Assert.That(store.Contains(data.shieldArchiveId), Is.True, "a imagem real de arquivo não conta como foto do jogador");
        }

        // ---------- M18 e M19 · meteorito ----------

        [Test]
        public void M18RequiresArrivingAtTheRockAndShowsNoPrompt()
        {
            ReachPhotoStep();
            Hold(4.2f);
            Run(8f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M18"));
            Run(3f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M18"));
            Assert.That(interaction.TryGetActive(out _), Is.False);
            AtRock();
            Run(0.3f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.MeteoriteReached), Is.EqualTo(1));
            Assert.That(meteorite.IsEngaged, Is.True);
            Run(6f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M19"));
        }

        [Test]
        public void M19MeasuresWithSixSecondsThenShowsTheResultForFourBeforePhotographed()
        {
            ReachMeasureStep();
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MeteoriteActivity.MeasureCandidateId));
            Assert.That(c.VerbKey, Is.EqualTo(data.meteoriteVerbKey));

            Feed(router, "FeedInteract", true);
            Run(3f);
            Feed(router, "FeedInteract", false);
            Run(1f);
            Assert.That(meteorite.Measured, Is.False);
            Assert.That(meteorite.WorkSeconds, Is.EqualTo(3f).Within(0.15f), "soltar conserva");
            Hold(3.3f);
            Assert.That(meteorite.Measured, Is.True);
            Assert.That(events, Does.Contain(MarsChapterFlow.Events.MeteoriteMeasured));
            Assert.That(events, Does.Not.Contain(MarsChapterFlow.Events.MeteoritePhotographed), "o registro visual vem depois do resultado");

            Run(3.5f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M19"), "resultado de 4 s ainda na tela");
            Run(1.5f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M20"));
            var order = events.Where(e => e.StartsWith("mars.meteorite.")).ToList();
            Assert.That(order, Is.EqualTo(new[] { MarsChapterFlow.Events.MeteoriteReached, MarsChapterFlow.Events.MeteoriteMeasured, MarsChapterFlow.Events.MeteoritePhotographed }));
            Assert.That(meteorite.IsEngaged, Is.False);
        }

        [Test]
        public void MeteoriteResultKeepsCompositionYourRecordAndTheRealCreditedImage()
        {
            ReachMeasureStep();
            store.Clear();
            Hold(6.3f);
            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(), Is.EqualTo(new[] { data.meteoriteCompositionId }), "primeiro só a composição");
            Run(4.3f);
            Assert.That(store.Records.Select(r => r.EvidenceId).ToList(),
                Is.EqualTo(new[] { data.meteoriteCompositionId, data.meteoritePhotoId, data.meteoriteArchiveId }));
            var composition = store.Records[0];
            Assert.That(composition.Kind, Is.EqualTo(EvidenceKind.Composition));
            Assert.That(composition.ScienceRef, Is.EqualTo("MS14"));
            Assert.That(composition.ResultKey, Is.EqualTo("MAR.RESULT04"));
            var real = store.Records[2];
            Assert.That(real.Authorship, Is.EqualTo(EvidenceAuthorship.Archive));
            Assert.That(real.CreditKey, Is.EqualTo(data.meteoriteArchiveCreditKey));
            Assert.That(real.Thumbnail, Is.SameAs(data.meteoriteArchiveImage));
            Assert.That(real.CanBePresentedAsPlayers, Is.False);
            foreach (var record in store.Records)
                Assert.That(record.Validate(), Is.Empty, record.EvidenceId);
        }

        // ---------- Distinção entre os dois ----------

        [Test]
        public void OneResultNeverConcludesTheOther()
        {
            ReachPhotoStep();
            Assert.That(meteorite.Phase, Is.EqualTo(MeteoritePhase.Idle), "meteorito intocado durante o escudo");
            Hold(4.2f);
            Assert.That(events.Where(e => e.StartsWith("mars.meteorite.")), Is.Empty);
            Run(8f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M18"));
            Assert.That(shield.Phase, Is.EqualTo(ShieldSurveyPhase.Done));
            AtRock();
            Run(6f);
            Step(Frame);
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(MeteoriteActivity.MeasureCandidateId), "em M19 só o meteorito oferece E");
            Hold(6.3f);
            Run(4.5f);
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.ShieldPhotographed), Is.EqualTo(1), "o escudo não foi refeito");
        }

        [Test]
        public void RatIsBlockedOnTheMetalAndNeverOffersAToolTargetThere()
        {
            ReachMeasureStep();
            Assert.That(meteorite.RatBlocked, Is.True);
            Assert.That(meteorite.IsRatBlockedAt(anchors.MeteoriteMeasurePoint.position), Is.True);
            Assert.That(meteorite.IsRatBlockedAt(anchors.MeteoriteMeasurePoint.position + new Vector3(0.5f, 0f, 0.3f)), Is.True);
            Assert.That(meteorite.IsRatBlockedAt(anchors.ShieldStation.position), Is.False, "longe do metal a restrição não vale");
            Assert.That(rat.Phase, Is.EqualTo(MarsRatPhase.Idle), "a RAT não está ativa em M19");
            Assert.That(interaction.TryGetActive(out var c) && c.Id.StartsWith("mars.rat."), Is.False, "nenhum candidato de RAT no meteorito");
            Hold(6.3f);
            Run(5f);
            Assert.That(meteorite.RatBlocked, Is.False, "depois do meteorito a restrição acaba");
        }

        [Test]
        public void ViewsFollowTheSceneAnchors()
        {
            var views = new List<Transform>();
            shield.ViewChanged += views.Add;
            meteorite.ViewChanged += views.Add;
            ReachMeasureStep();
            Hold(6.3f);
            Run(5f);
            Assert.That(views.Count, Is.EqualTo(4));
            Assert.That(views[0], Is.SameAs(anchors.ShieldCameraAnchor));
            Assert.That(views[1], Is.Null);
            Assert.That(views[2], Is.SameAs(anchors.MeteoriteCameraAnchor));
            Assert.That(views[3], Is.Null);
        }

        // ---------- Pausa, foco e reinício ----------

        [Test]
        public void PauseFreezesAndFocusLossKeepsTheWork()
        {
            ReachPhotoStep();
            Feed(router, "FeedInteract", true);
            Run(1.5f);
            var worked = shield.WorkSeconds;
            for (var i = 0; i < 60; i++)
                Step(0f);

            Assert.That(shield.WorkSeconds, Is.EqualTo(worked), "pausa");
            router.NotifyFocusLost();
            Run(3f);
            Assert.That(shield.IsHolding, Is.False);
            Assert.That(shield.WorkSeconds, Is.EqualTo(worked).Within(1e-4f));
            Assert.That(shield.Photographed, Is.False);
        }

        [Test]
        public void RestartingClearsBothActivitiesAndReleasesTheRover()
        {
            ReachMeasureStep();
            Hold(2f);
            Assert.That(meteorite.IsEngaged, Is.True);

            director.RestartChapter();
            Assert.That(shield.Phase, Is.EqualTo(ShieldSurveyPhase.Idle));
            Assert.That(meteorite.Phase, Is.EqualTo(MeteoritePhase.Idle));
            Assert.That(meteorite.IsEngaged, Is.False);
            Assert.That(shield.IsEngaged, Is.False);
            Assert.That(meteorite.WorkSeconds, Is.EqualTo(0f));
            Assert.That(interaction.TryGetActive(out _), Is.False);

            events.Clear();
            store.Clear();
            ReachPhotoStep();
            Assert.That(shield.WorkSeconds, Is.EqualTo(0f));
        }
    }
}
