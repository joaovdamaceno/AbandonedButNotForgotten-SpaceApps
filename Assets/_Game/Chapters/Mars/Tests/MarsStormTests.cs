using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-007: tempestade de 2007 (M21). Prova: horizonte apaga em 6 s; sem E nada conclui nem pune; 8 s de E desliga rodas,
    /// braço e instrumentos e só então conserva; elipse de semanas com comunicações reduzidas; céu clareia e a máquina retoma;
    /// só marcos publicados no medidor (sem números intermediários); pausa, foco e reinício; sem derrota.
    /// </summary>
    public class MarsStormTests
    {
        const string PrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/Storm2007Controls.prefab";
        const string DataPath = "Assets/_Game/Chapters/Mars/Data/Storm2007.asset";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Data/MarsStormTexts.json";
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
        Storm2007Activity storm;
        Storm2007Data data;
        MarsChapterFlow flow;
        MockChapterDirector director;
        IDisposable gameplay;
        List<string> events;

        [SetUp]
        public void SetUp()
        {
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            data = AssetDatabase.LoadAssetAtPath<Storm2007Data>(DataPath);
            Assert.That(data, Is.Not.Null, DataPath);

            var services = Track(new GameObject("services (MarsStormTests)"));
            router = services.AddComponent<InputRouter>();
            interaction = services.AddComponent<InteractionService>();
            time = new FakeTime();
            interaction.Initialize(router, time);
            gameplay = router.Acquire(InputContext.Gameplay, "teste");

            storm = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))).GetComponent<Storm2007Activity>();
            director = new MockChapterDirector();
            flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            events = new List<string>();
            flow.EventAccepted += (_, e) => events.Add(e);
            storm.Initialize(flow, interaction, time);
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
            storm.Tick(dt);
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

        /// <summary>Em M21, depois dos 6 s de escurecimento: pronto para segurar E.</summary>
        void ReachConserving()
        {
            PlayUntil("M21");
            Step(Frame);
            Run(6.1f);
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Conserving));
            Step(Frame);
        }

        void Hold(float seconds)
        {
            Feed(router, "FeedInteract", true);
            Run(seconds);
            Feed(router, "FeedInteract", false);
            Step(Frame);
        }

        // ---------- Dados ----------

        [Test]
        public void DataValidatesAndKeepsTheRoteiroTimesAndOnlyThePublishedMilestones()
        {
            Assert.That(storm.ValidateSetup(), Is.Empty, ContractValidation.Describe(storm.ValidateSetup()));
            Assert.That(data.TotalSeconds, Is.EqualTo(25f).Within(0.01f), "M21: 25 s");
            Assert.That(data.conserveHoldSeconds, Is.EqualTo(8f));
            Assert.That(data.beforeWhPerSol, Is.EqualTo(700));
            Assert.That(data.lowWhPerSol, Is.EqualTo(128));

            var invented = UnityEngine.Object.Instantiate(data);
            invented.lowWhPerSol = 150;
            Assert.That(invented.Validate(), Is.Not.Empty, "valor inventado é recusado pela validação");
            UnityEngine.Object.DestroyImmediate(invented);
        }

        [Test]
        public void TextsExistInBothLanguagesWithTheUnitAndNoInventedNumbers()
        {
            var table = JsonUtility.FromJson<TextTable>(AssetDatabase.LoadAssetAtPath<TextAsset>(TextsPath).text);
            var keys = new[] { data.conserveVerbKey, data.conserveObjectKey, data.meterBeforeKey, data.meterLowKey, data.meterRecoveringKey, data.meterNoteKey,
                data.wheelsKey, data.armKey, data.instrumentsKey };
            foreach (var key in keys)
            {
                var entry = table.texts.FirstOrDefault(t => t.key == key);
                Assert.That(entry, Is.Not.Null, key);
                Assert.That(string.IsNullOrWhiteSpace(entry.pt), Is.False, key + " PT");
                Assert.That(string.IsNullOrWhiteSpace(entry.en), Is.False, key + " EN");
            }

            Assert.That(flow.Subtitles.TryGetText(data.meterTitleKey, "pt", out var title), Is.True);
            Assert.That(title, Does.Contain("Wh/sol"), "unidade do medidor");
            Assert.That(flow.Subtitles.TryGetText(data.commsReducedKey, "en", out _), Is.True);

            foreach (var entry in table.texts.Where(t => t.key.StartsWith("MAR.STORM.gen.")))
            {
                foreach (var text in new[] { entry.pt, entry.en })
                {
                    foreach (Match number in Regex.Matches(text, @"\d+"))
                        Assert.That(new[] { "700", "128", "2007" }, Does.Contain(number.Value), $"{entry.key}: só os marcos publicados e o ano ({text})");
                }
            }

            var recovering = table.texts.First(t => t.key == data.meterRecoveringKey);
            Assert.That(Regex.IsMatch(recovering.pt + recovering.en, @"\d"), Is.False, "a retomada não tem número");
            foreach (var entry in table.texts)
            {
                Assert.That(entry.pt.ToLowerInvariant(), Does.Not.Contain("bateria").Or.Contain("não"), entry.key + ": geração não é bateria");
            }
        }

        // ---------- Escurecimento ----------

        [Test]
        public void HorizonDarkensGraduallyInSixSecondsWithoutAPromptAndWithoutNumbersInBetween()
        {
            PlayUntil("M21");
            var dust = new List<float>();
            storm.DustLevelChanged += dust.Add;
            Step(Frame);
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Dimming));
            Assert.That(storm.MeterState, Is.EqualTo(StormMeterState.Before));
            Assert.That(storm.PublishedWhPerSol, Is.EqualTo(700), "antes: o marco publicado");
            for (var i = 0; i < 140 && storm.Phase == StormPhase.Dimming; i++)
            {
                Assert.That(interaction.TryGetActive(out _), Is.False, "sem E nos 6 s do escurecimento");
                Step(Frame);
            }

            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Conserving));
            Assert.That(storm.DustLevel, Is.EqualTo(data.dustPeak).Within(1e-4f));
            for (var i = 1; i < dust.Count; i++)
                Assert.That(dust[i], Is.GreaterThanOrEqualTo(dust[i - 1] - 1e-5f), "o horizonte só escurece");
            Assert.That(dust.Count, Is.GreaterThan(20), "gradual, não um salto");
        }

        // ---------- Conservar ----------

        [Test]
        public void WithoutEThePowerIsNeverConservedAndNothingPunishes()
        {
            ReachConserving();
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.Id, Is.EqualTo(Storm2007Activity.ConserveCandidateId));
            Assert.That(c.VerbKey, Is.EqualTo(data.conserveVerbKey));
            Assert.That(c.HoldSeconds, Is.EqualTo(8f).Within(0.01f));

            Run(90f);
            Assert.That(storm.Conserved, Is.False);
            Assert.That(events, Does.Not.Contain(MarsChapterFlow.Events.StormConserved));
            Assert.That(director.CurrentStepId, Is.EqualTo("M21"));
            Assert.That(storm.DustLevel, Is.EqualTo(data.dustPeak).Within(1e-4f), "a tempestade não piora por esperar");
            Assert.That(storm.IsGroupEnabled(StormGroup.Wheels), Is.True, "nada foi desligado sem E");
            Assert.That(storm.CommsReduced, Is.False);
        }

        [Test]
        public void EightSecondsOfEShutGroupsInOrderThenConserve()
        {
            ReachConserving();
            var changes = new List<string>();
            storm.GroupChanged += (g, on) => changes.Add($"{g}:{on}");

            Feed(router, "FeedInteract", true);
            Run(2.0f);
            Assert.That(storm.IsGroupEnabled(StormGroup.Wheels), Is.True, "ainda cedo");
            Run(1.0f);
            Assert.That(storm.IsGroupEnabled(StormGroup.Wheels), Is.False, "as rodas caem primeiro (~1/3)");
            Assert.That(storm.IsGroupEnabled(StormGroup.Arm), Is.True);
            Run(2.5f);
            Assert.That(storm.IsGroupEnabled(StormGroup.Arm), Is.False, "o braço cai em seguida (~2/3)");
            Assert.That(storm.IsGroupEnabled(StormGroup.Instruments), Is.True);
            Assert.That(storm.Conserved, Is.False);
            Run(2.8f);
            Feed(router, "FeedInteract", false);
            Assert.That(storm.Conserved, Is.True);
            Assert.That(storm.IsGroupEnabled(StormGroup.Instruments), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { "Wheels:False", "Arm:False", "Instruments:False" }));
            Assert.That(events.Count(e => e == MarsChapterFlow.Events.StormConserved), Is.EqualTo(1));
            Assert.That(storm.CommsReduced, Is.True, "comunicações limitadas, não rádio morto");
        }

        [Test]
        public void ReleasingEPausesAndKeepsWhatWasAlreadyShutDown()
        {
            ReachConserving();
            Feed(router, "FeedInteract", true);
            Run(3.2f);
            Feed(router, "FeedInteract", false);
            Run(2f);
            var worked = storm.WorkSeconds;
            Assert.That(worked, Is.EqualTo(3.2f).Within(0.2f), "soltar conserva");
            Assert.That(storm.IsGroupEnabled(StormGroup.Wheels), Is.False, "o que caiu continua desligado");
            Assert.That(storm.Conserved, Is.False);
            Assert.That(interaction.TryGetActive(out var c), Is.True);
            Assert.That(c.HoldSeconds, Is.EqualTo(8f - worked).Within(0.1f), "pede só o restante");
            Hold(5f);
            Assert.That(storm.Conserved, Is.True);
        }

        // ---------- Elipse de semanas e recuperação ----------

        [Test]
        public void WeeksEllipseThenSkyClearsMachineResumesAndTheGameAdvances()
        {
            ReachConserving();
            Hold(8.3f);
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Weeks));
            var states = new List<StormMeterState> { storm.MeterState };

            Run(2.0f);
            Assert.That(storm.MeterState, Is.EqualTo(StormMeterState.Before), "ainda antes de julho");
            Run(1.2f);
            Assert.That(storm.MeterState, Is.EqualTo(StormMeterState.Low), "julho de 2007");
            Assert.That(storm.PublishedWhPerSol, Is.EqualTo(128));
            Run(2.6f);
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Clearing));
            Assert.That(storm.IsGroupEnabled(StormGroup.Wheels), Is.False, "a máquina ainda não retomou");

            var dust = storm.DustLevel;
            Run(2.5f);
            Assert.That(storm.DustLevel, Is.LessThan(dust), "o céu clareia pela passagem do tempo");
            Assert.That(storm.MeterState, Is.EqualTo(StormMeterState.Recovering));
            Assert.That(storm.PublishedWhPerSol, Is.Null, "a retomada não tem número");
            Run(3f);

            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Done));
            Assert.That(storm.DustLevel, Is.EqualTo(data.dustAfterClearing).Within(1e-4f));
            Assert.That(storm.DustLevel, Is.LessThan(data.dustPeak * 0.25f), "recuperação inequívoca");
            foreach (var group in new[] { StormGroup.Wheels, StormGroup.Arm, StormGroup.Instruments })
                Assert.That(storm.IsGroupEnabled(group), Is.True, group + " retoma");
            Assert.That(storm.CommsReduced, Is.False);
            Assert.That(storm.MeterNeedle, Is.EqualTo(data.needleRecovered).Within(1e-4f));
            Assert.That(storm.MeterNeedle, Is.LessThan(data.needleBefore), "retomada cautelosa, não volta ao início");
            Assert.That(events.Where(e => e.StartsWith("mars.storm.")).ToList(),
                Is.EqualTo(new[] { MarsChapterFlow.Events.StormConserved, MarsChapterFlow.Events.StormSkyCleared }), "uma vez cada, na ordem");
            Run(6f);
            Assert.That(director.CurrentStepId, Is.EqualTo("M22"), "o jogo avança");
        }

        [Test]
        public void TotalActiveTimeOfTheStepIsTwentyFiveSecondsWhenEIsHeldStraightAway()
        {
            PlayUntil("M21");
            Step(Frame);
            var elapsed = Frame;
            var held = false;
            while (director.CurrentStepId == "M21" && elapsed < 80f)
            {
                if (storm.Phase == StormPhase.Conserving && !held)
                {
                    Feed(router, "FeedInteract", true);
                    held = true;
                }

                Step(Frame);
                elapsed += Frame;
            }

            Feed(router, "FeedInteract", false);
            // A etapa ainda espera a leitura mínima das legendas (C29: 5 s), então passa um pouco de 25 s.
            Assert.That(elapsed, Is.InRange(24.5f, 31f), "6 + 8 + 6 + 5 s de tempo ativo, mais a leitura mínima");
            Assert.That(director.CurrentStepId, Is.EqualTo("M22"));
        }

        // ---------- Pausa, foco e reinício ----------

        [Test]
        public void PauseFreezesTheHoldAndTheWeeksEllipse()
        {
            ReachConserving();
            Feed(router, "FeedInteract", true);
            Run(2f);
            var worked = storm.WorkSeconds;
            for (var i = 0; i < 60; i++)
                Step(0f);
            Assert.That(storm.WorkSeconds, Is.EqualTo(worked), "pausa durante o E sustentado");
            Feed(router, "FeedInteract", false);
            Hold(7f);
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Weeks));

            Run(1f);
            var needle = storm.MeterNeedle;
            var dust = storm.DustLevel;
            for (var i = 0; i < 100; i++)
                Step(0f);
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Weeks), "pausa durante a elipse");
            Assert.That(storm.MeterNeedle, Is.EqualTo(needle));
            Assert.That(storm.DustLevel, Is.EqualTo(dust));
        }

        [Test]
        public void FocusLossKeepsTheWork()
        {
            ReachConserving();
            Feed(router, "FeedInteract", true);
            Run(2.5f);
            var worked = storm.WorkSeconds;
            router.NotifyFocusLost();
            Run(3f);
            Assert.That(storm.IsHolding, Is.False);
            Assert.That(storm.WorkSeconds, Is.EqualTo(worked).Within(1e-4f));
            Assert.That(storm.Conserved, Is.False);
        }

        [Test]
        public void RestartingClearsTheStormAtAnyPoint()
        {
            ReachConserving();
            Hold(8.3f);
            Run(2f);
            Assert.That(storm.IsGroupEnabled(StormGroup.Wheels), Is.False);

            director.RestartChapter();
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Idle));
            Assert.That(storm.DustLevel, Is.EqualTo(0f), "céu limpo");
            foreach (var group in new[] { StormGroup.Wheels, StormGroup.Arm, StormGroup.Instruments })
                Assert.That(storm.IsGroupEnabled(group), Is.True);
            Assert.That(storm.CommsReduced, Is.False);
            Assert.That(storm.MeterState, Is.EqualTo(StormMeterState.Before));
            Assert.That(interaction.TryGetActive(out _), Is.False);

            events.Clear();
            ReachConserving();
            Assert.That(storm.WorkSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void RestartingAfterTheStormHasEndedLeavesNoResidualState()
        {
            ReachConserving();
            Hold(8.3f);
            Run(17f);
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Done));
            director.RestartChapter();
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Idle));
            Assert.That(storm.DustLevel, Is.EqualTo(0f));
            Assert.That(storm.Conserved, Is.False);
        }

        [Test]
        public void NoDefeatNoDestructiveStateExists()
        {
            // A tempestade nunca arranca peças nem derrota: todo o ciclo termina com tudo ligado e só há fases de espera/recuperação.
            Assert.That(Enum.GetNames(typeof(StormPhase)), Is.EquivalentTo(new[] { "Idle", "Dimming", "Conserving", "Weeks", "Clearing", "Done" }));
            ReachConserving();
            Hold(8.3f);
            Run(17f);
            foreach (var group in new[] { StormGroup.Wheels, StormGroup.Arm, StormGroup.Instruments })
                Assert.That(storm.IsGroupEnabled(group), Is.True);
        }
    }
}
