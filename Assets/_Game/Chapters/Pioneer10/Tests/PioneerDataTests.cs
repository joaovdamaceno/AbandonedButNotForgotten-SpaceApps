using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// PIO-001: dados do capítulo (Data/Chapters/Pioneer10.json e Data/Localization/Pioneer10.*.json) contra o código e as
    /// sequências já integrados. A validação completa (fontes, fatos, datas) está em Tools/validate_pioneer_data.py.
    /// </summary>
    public class PioneerDataTests
    {
        const string ChapterPath = "Data/Chapters/Pioneer10.json";
        const string PortuguesePath = "Data/Localization/Pioneer10.pt-BR.json";
        const string EnglishPath = "Data/Localization/Pioneer10.en.json";

        [System.Serializable] sealed class Chapter { public State[] states; public Cue[] cues; }

        [System.Serializable] sealed class State { public string id; public int order; public int nominalSeconds; public int globalStartSeconds; public int globalEndSeconds; }

        [System.Serializable] sealed class Cue { public string id; public string state; public string textKey; public Timing timing; }

        [System.Serializable] sealed class Timing { public string kind; public float startSeconds; public float endSeconds; public string triggerId; public float minimumReadSeconds; }

        [System.Serializable] sealed class Locale { public string language; public Entry[] entries; }

        [System.Serializable] sealed class Entry { public string key; public string group; public string status; public string text; }

        static Chapter chapter;
        static Locale portuguese, english;

        [SetUp]
        public void SetUp()
        {
            chapter = JsonUtility.FromJson<Chapter>(File.ReadAllText(ChapterPath));
            portuguese = JsonUtility.FromJson<Locale>(File.ReadAllText(PortuguesePath));
            english = JsonUtility.FromJson<Locale>(File.ReadAllText(EnglishPath));
            Assert.That(chapter?.states, Is.Not.Null.And.Not.Empty, "Pioneer10.json ilegível");
        }

        [Test]
        public void TheNineStatesKeepTheStableIdsTheOrderAndTheNominalBudget()
        {
            Assert.That(chapter.states.Select(s => s.id), Is.EqualTo(PioneerPresentation.StateIds), "mesmos IDs e ordem do palco da PIO-003");
            Assert.That(chapter.states.Select(s => s.order), Is.EqualTo(Enumerable.Range(1, 9)));
            Assert.That(chapter.states.Select(s => s.nominalSeconds), Is.EqualTo(new[] { 40, 50, 27, 64, 40, 33, 39, 40, 38 }));
            Assert.That(chapter.states.Sum(s => s.nominalSeconds), Is.EqualTo(371));

            var cursor = 0;
            foreach (var state in chapter.states)
            {
                Assert.That(state.globalStartSeconds, Is.EqualTo(cursor), state.id);
                cursor = state.globalEndSeconds;
                Assert.That(cursor - state.globalStartSeconds, Is.EqualTo(state.nominalSeconds), state.id);
            }

            Assert.That(cursor, Is.EqualTo(371));
        }

        [Test]
        public void PortugueseAndEnglishHaveTheSameKeysAndEveryCueHasItsText()
        {
            var pt = portuguese.entries.Select(e => e.key).ToList();
            var en = english.entries.Select(e => e.key).ToList();
            Assert.That(pt, Is.Unique);
            Assert.That(en, Is.Unique);
            Assert.That(pt.OrderBy(k => k), Is.EqualTo(en.OrderBy(k => k)), "as chaves PT/EN coincidem");
            Assert.That(portuguese.entries.Concat(english.entries).Select(e => e.text), Has.None.Null.And.None.Empty);
            Assert.That(chapter.cues.Select(c => c.id), Is.Unique);
            foreach (var cue in chapter.cues)
                Assert.That(pt, Does.Contain(cue.textKey), cue.id);
        }

        [Test]
        public void SubtitleWindowsStayInsideTheirStateWithoutOverlapAndTriggersHaveAReadingTime()
        {
            foreach (var state in chapter.states)
            {
                var windows = chapter.cues.Where(c => c.state == state.id && c.timing.kind == "window").OrderBy(c => c.timing.startSeconds).ToList();
                Assert.That(chapter.cues.Count(c => c.state == state.id), Is.GreaterThan(0), state.id + " sem legenda");
                var previousEnd = 0f;
                foreach (var cue in windows)
                {
                    Assert.That(cue.timing.startSeconds, Is.LessThan(cue.timing.endSeconds), cue.id);
                    Assert.That(cue.timing.startSeconds, Is.GreaterThanOrEqualTo(previousEnd), cue.id + " sobrepõe a anterior");
                    Assert.That(cue.timing.endSeconds, Is.LessThanOrEqualTo(state.nominalSeconds), cue.id + " passa do fim do estado");
                    previousEnd = cue.timing.endSeconds;
                }
            }

            foreach (var cue in chapter.cues.Where(c => c.timing.kind == "trigger"))
            {
                Assert.That(cue.timing.triggerId, Is.Not.Null.And.Not.Empty, cue.id);
                Assert.That(cue.timing.minimumReadSeconds, Is.GreaterThan(0f), cue.id);
            }
        }

        [Test]
        public void TheKeysTheIntegratedCodeAsksForExistInBothLanguages()
        {
            var keys = new[]
            {
                PioneerLaunchActivity.PromptKey, BeltInvestigation.PromptKey, BeltPanelView.SealKey, BeltPanelView.MoveKey,
                BeltPanelView.DustKey, BeltPanelView.HelpKey, ImagingPanelView.RedKey, ImagingPanelView.BlueKey, ImagingPanelView.ScanKey,
            };
            foreach (var key in keys)
            {
                Assert.That(portuguese.entries.Any(e => e.key == key), Is.True, "PT sem " + key);
                Assert.That(english.entries.Any(e => e.key == key), Is.True, "EN sem " + key);
            }
        }

        [Test]
        public void TheIntegratedSequencesUseTheSameCuesAndWindowsAsTheData()
        {
            var sequences = new[] { "PIO_Launch", "PIO_BeltCinema", "PIO_Imaging" };
            foreach (var name in sequences)
            {
                var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>($"Assets/_Game/Chapters/Pioneer10/Sequences/{name}.asset");
                Assert.That(sequence, Is.Not.Null, name);
                foreach (var cue in sequence.Cues)
                {
                    var data = chapter.cues.FirstOrDefault(c => c.id == cue.CueId);
                    Assert.That(data, Is.Not.Null, $"{name}: cue {cue.CueId} não existe nos dados");
                    Assert.That(cue.TextKey, Is.EqualTo(data.textKey), cue.CueId);
                    if (data.timing.kind != "window")
                        continue; // gatilhos do roteiro (ex.: PIO.04.03–05) que a sequência aproxima por janela fixa

                    Assert.That(cue.StartSeconds, Is.EqualTo(data.timing.startSeconds).Within(1e-4f), cue.CueId + " início");
                    Assert.That(cue.EndSeconds, Is.EqualTo(data.timing.endSeconds).Within(1e-4f), cue.CueId + " fim");
                }
            }
        }

        [Test]
        public void NarrativeTextIsNeverEmbeddedInTheChapterScripts()
        {
            var scripts = Directory.GetFiles("Assets/_Game/Chapters/Pioneer10/Runtime", "*.cs", SearchOption.AllDirectories);
            var narrative = portuguese.entries.Where(e => e.group == "narrative").Select(e => e.text).Concat(english.entries.Where(e => e.group == "narrative").Select(e => e.text)).ToList();
            foreach (var path in scripts)
            {
                var code = File.ReadAllText(path);
                foreach (var text in narrative)
                    Assert.That(code, Does.Not.Contain(text), $"{Path.GetFileName(path)} embute texto narrativo");
            }
        }
    }
}
