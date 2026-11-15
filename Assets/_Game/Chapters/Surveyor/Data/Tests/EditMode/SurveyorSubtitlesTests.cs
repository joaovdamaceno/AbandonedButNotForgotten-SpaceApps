using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor.Tests
{
    /// <summary>SUR-014 · textos PT/EN e vínculo dos 31 cues. Os testes lêem o roteiro, os JSONs, o asset e os assets de sequência reais.</summary>
    public sealed class SurveyorSubtitlesTests
    {
        const string AssetPath = "Assets/_Game/Chapters/Surveyor/Data/SurveyorSubtitles.asset";
        const string ChapterRoot = "Assets/_Game/Chapters/Surveyor";
        const string ScriptPath = "Docs/Chapters/03_SURVEYOR_3.md";

        SurveyorSubtitles catalog;
        string mergedJson;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<SurveyorSubtitles>(AssetPath);
            Assert.That(catalog, Is.Not.Null, "SurveyorSubtitles.asset não carregou.");
            var errors = new List<string>();
            mergedJson = catalog.BuildTableJson(errors);
            Assert.That(errors, Is.Empty);
        }

        [Serializable] sealed class Merged { public Row[] texts; }

        [Serializable] sealed class Row { public string key, group, pt, en; }

        // Formatos das tabelas locais dos outros atos: {"texts":[{key,pt,en}]} ou {"entries":[{id,pt,en}]}.
        [Serializable] sealed class LocalFile { public Row[] texts; public LocalEntry[] entries; }

        [Serializable] sealed class LocalEntry { public string id, pt, en; }

        Dictionary<string, Row> Table()
        {
            var table = new Dictionary<string, Row>();
            foreach (var row in JsonUtility.FromJson<Merged>(mergedJson).texts)
                table[row.key] = row;
            return table;
        }

        static string Clean(string value) => value.Trim();

        [Test]
        public void CatalogAssetIsValid()
        {
            Assert.That(catalog.Validate(), Is.Empty);
            Assert.That(catalog.Cues.Count, Is.EqualTo(31));
        }

        [Test]
        public void PtAndEnHaveTheSameKeysWithoutDuplicatesOrGaps()
        {
            var pt = File.ReadAllText(AssetDatabase.GetAssetPath(catalog.PtBrTable));
            var en = File.ReadAllText(AssetDatabase.GetAssetPath(catalog.EnTable));
            var ptKeys = Regex.Matches(pt, "\"key\":\\s*\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            var enKeys = Regex.Matches(en, "\"key\":\\s*\"([^\"]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();

            Assert.That(ptKeys, Is.Unique, "chave duplicada em PT");
            Assert.That(enKeys, Is.Unique, "chave duplicada em EN");
            Assert.That(ptKeys, Is.EqualTo(enKeys), "PT e EN precisam ter as mesmas chaves, na mesma ordem");
            foreach (var row in Table().Values)
            {
                Assert.That(row.pt.Trim(), Is.Not.Empty, row.key + " sem PT");
                Assert.That(row.en.Trim(), Is.Not.Empty, row.key + " sem EN");
            }
        }

        [Test]
        public void MergeReportsMissingDuplicateAndEmptyKeys()
        {
            const string pt = "{\"entries\":[{\"key\":\"A.one\",\"group\":\"g\",\"text\":\"um\"},{\"key\":\"A.one\",\"group\":\"g\",\"text\":\"um\"},{\"key\":\"A.two\",\"group\":\"g\",\"text\":\"\"},{\"key\":\"A.only\",\"group\":\"g\",\"text\":\"so\"}]}";
            const string en = "{\"entries\":[{\"key\":\"A.one\",\"group\":\"g\",\"text\":\"one\"},{\"key\":\"A.two\",\"group\":\"g\",\"text\":\"two\"},{\"key\":\"A.extra\",\"group\":\"g\",\"text\":\"x\"}]}";
            var errors = new List<string>();
            Assert.That(SurveyorSubtitles.MergeTables(pt, en, errors), Is.Null);
            Assert.That(errors, Has.Some.Contains("duplicada 'A.one'"));
            Assert.That(errors, Has.Some.Contains("'A.two' sem texto em PT"));
            Assert.That(errors, Has.Some.Contains("'A.only' existe em PT e falta em EN"));
            Assert.That(errors, Has.Some.Contains("'A.extra' existe em EN e falta em PT"));
            Assert.That(SurveyorSubtitles.MergeTables("não é json", en, new List<string>()), Is.Null);
        }

        [Test]
        public void TextsAreTheScriptTableTranscribedWithoutChange()
        {
            var table = Table();
            var cues = 0;
            var ui = 0;
            foreach (var line in File.ReadAllLines(ScriptPath))
            {
                if (!line.StartsWith("|", StringComparison.Ordinal))
                    continue;

                var cells = line.Trim().Trim('|').Split('|').Select(Clean).ToArray();
                if (Regex.IsMatch(cells[0], @"^SUR-S\d\d$") && cells.Length == 5)
                {
                    cues++;
                    Assert.That(table[cells[0]].pt, Is.EqualTo(cells[3]), cells[0] + " PT");
                    Assert.That(table[cells[0]].en, Is.EqualTo(cells[4]), cells[0] + " EN");
                    Assert.That(catalog.TryGetCue(cells[0], out var binding), Is.True);
                    Assert.That(binding.minimumReadSeconds, Is.EqualTo(float.Parse(cells[2].Split(' ')[0])), cells[0] + " mínimo");
                }
                else if (Regex.IsMatch(cells[0], @"^[a-z]+\.[a-z0-9.]+$") && cells.Length == 3)
                {
                    ui++;
                    Assert.That(table["sur." + cells[0]].pt, Is.EqualTo(cells[1]), cells[0] + " PT");
                    Assert.That(table["sur." + cells[0]].en, Is.EqualTo(cells[2]), cells[0] + " EN");
                }
            }

            Assert.That(cues, Is.EqualTo(31));
            Assert.That(ui, Is.EqualTo(20));
        }

        [Test]
        public void ExistingActTablesAgreeWithTheCatalog()
        {
            var table = Table();
            var compared = new HashSet<string>();
            foreach (var path in Directory.GetFiles(ChapterRoot, "*Texts.json", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').Contains("/Localization/"))
                    continue;

                var file = JsonUtility.FromJson<LocalFile>(File.ReadAllText(path));
                var rows = new List<(string key, string pt, string en)>();
                foreach (var row in file.texts ?? Array.Empty<Row>())
                    rows.Add((row.key, row.pt, row.en));
                foreach (var row in file.entries ?? Array.Empty<LocalEntry>())
                    rows.Add((row.id, row.pt, row.en));

                foreach (var (key, pt, en) in rows)
                {
                    if (key == null || !Regex.IsMatch(key, @"^SUR-S\d\d$"))
                        continue;

                    compared.Add(key);
                    Assert.That(pt, Is.EqualTo(table[key].pt), $"{Path.GetFileName(path)} {key} PT diverge do catálogo");
                    Assert.That(en, Is.EqualTo(table[key].en), $"{Path.GetFileName(path)} {key} EN diverge do catálogo");
                }
            }

            Assert.That(compared.Count, Is.GreaterThanOrEqualTo(15), "o teste devia comparar as tabelas dos atos já montados");
        }

        [Test]
        public void LocalizationServiceResolvesBothLanguagesAndHidesMissingText()
        {
            var service = new LocalizationService();
            Assert.That(service.AddTable(mergedJson, "surveyor"), Is.Empty);
            Assert.That(service.Validate(), Is.Empty);

            foreach (var cue in catalog.Cues)
            {
                service.SetLanguage("pt");
                var pt = service.Get(cue.textKey);
                service.SetLanguage("en");
                var en = service.Get(cue.textKey);
                Assert.That(pt, Is.Not.Empty);
                Assert.That(en, Is.Not.Empty.And.Not.EqualTo(pt), cue.cueId);
            }

            service.SetLanguage("en");
            Assert.That(service.Get("SUR-S31"), Is.EqualTo("It stayed. What we learned from it came home with us."));
            Assert.That(service.Get("SUR-S99"), Is.Empty, "chave inexistente devolve vazio: a legenda some");
            Assert.That(service.MissingRequests, Contains.Item("SUR-S99"));
        }

        [Test]
        public void BuiltSequencesMatchTheCatalogBinding()
        {
            var files = new[]
            {
                "Sequences/SurveyorLaunch.asset", "Sequences/SurveyorCruise.asset", "Sequences/SurveyorDescent.asset",
                "Sequences/FirstImage.asset", "Sequences/SurveyorEclipse.asset", "Sequences/SurveyorNight.asset",
                "Sequences/SurveyorTimePassage.asset",
            };
            var found = new HashSet<string>();
            foreach (var file in files)
            {
                var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>($"{ChapterRoot}/{file}");
                Assert.That(sequence, Is.Not.Null, file);
                foreach (var cue in sequence.Cues)
                {
                    if (!catalog.TryGetCue(cue.CueId, out var binding))
                    {
                        Assert.That(cue.CueId, Is.EqualTo("SUR-S08-PLAYER"), $"{file}: cue {cue.CueId} fora do catálogo");
                        continue;
                    }

                    found.Add(cue.CueId);
                    Assert.That(binding.sequenceId, Is.EqualTo(sequence.SequenceId), cue.CueId);
                    Assert.That(cue.TextKey, Is.EqualTo(binding.textKey), cue.CueId);
                    Assert.That(cue.IsTriggered, Is.EqualTo(binding.IsEvent), cue.CueId);
                    Assert.That(cue.Priority, Is.EqualTo(binding.priority), cue.CueId);
                    if (binding.IsEvent)
                    {
                        Assert.That(cue.TriggerId, Is.EqualTo(binding.triggerId), cue.CueId);
                        Assert.That(cue.MinimumReadSeconds, Is.GreaterThanOrEqualTo(binding.minimumReadSeconds), cue.CueId);
                    }
                    else
                    {
                        Assert.That(cue.StartSeconds, Is.EqualTo(binding.startSeconds).Within(1e-3f), cue.CueId);
                        Assert.That(cue.EndSeconds, Is.EqualTo(binding.endSeconds).Within(1e-3f), cue.CueId);
                    }
                }
            }

            var windowOrDescent = new[] { "SUR-S01", "SUR-S02", "SUR-S03", "SUR-S04", "SUR-S05", "SUR-S06", "SUR-S07", "SUR-S08", "SUR-S09",
                "SUR-S10", "SUR-S20", "SUR-S21", "SUR-S22", "SUR-S23", "SUR-S24", "SUR-S25" };
            Assert.That(found, Is.SupersetOf(windowOrDescent));
        }

        [Test]
        public void RuntimeBoundCuesUseTheOwnerConstants()
        {
            string Trigger(string id) { catalog.TryGetCue(id, out var b); return b.triggerId; }

            Assert.That(Trigger("SUR-S08"), Is.EqualTo(SurveyorDescent.TeamTrigger));
            Assert.That(Trigger("SUR-S09"), Is.EqualTo(SurveyorDescent.ImageTrigger));
            Assert.That(Trigger("SUR-S14"), Is.EqualTo(SurveyorScoop.PreparedTrigger));
            Assert.That(Trigger("SUR-S15"), Is.EqualTo(SurveyorScoop.GrowingTrigger));
            Assert.That(SurveyorScoop.PreparedCueId, Is.EqualTo("SUR-S14"));
            Assert.That(SurveyorScoop.GrowingCueId, Is.EqualTo("SUR-S15"));
            Assert.That(SurveyorScoop.PreparedReadSeconds, Is.GreaterThanOrEqualTo(Min("SUR-S14")));
            Assert.That(SurveyorScoop.GrowingReadSeconds, Is.GreaterThanOrEqualTo(Min("SUR-S15")));

            var targets = SurveyorObservationTargets.CreateReference();
            Assert.That(targets.Targets.Select(t => t.subtitleKey), Is.EqualTo(new[] { "SUR-S11", "SUR-S12", "SUR-S13" }));
            foreach (var target in targets.Targets)
                Assert.That(targets.ReadingSeconds, Is.GreaterThanOrEqualTo(Min(target.subtitleKey)));
            UnityEngine.Object.DestroyImmediate(targets);
        }

        float Min(string id)
        {
            catalog.TryGetCue(id, out var binding);
            return binding.minimumReadSeconds;
        }

        [Test]
        public void SubtitlesDoNotOverlapAndEventCuesWaitForTheirPredecessor()
        {
            foreach (var cue in catalog.Cues.Where(c => c.IsEvent))
                Assert.That(cue.afterCueId, Is.Not.Empty, cue.cueId + " precisa esperar o cue anterior terminar a leitura");

            foreach (var group in catalog.Cues.Where(c => !c.IsEvent).GroupBy(c => c.sequenceId))
            {
                var ordered = group.OrderBy(c => c.startSeconds).ToList();
                for (var i = 1; i < ordered.Count; i++)
                    Assert.That(ordered[i].startSeconds, Is.GreaterThanOrEqualTo(ordered[i - 1].endSeconds - 1e-3f), group.Key);
            }
        }

        [Test]
        public void ObjectivesAreCornerCardsNeverSubtitles()
        {
            Assert.That(catalog.ObjectiveRegion, Is.EqualTo("corner"));
            Assert.That(catalog.Objectives.Count, Is.EqualTo(9));
            var cueKeys = new HashSet<string>(catalog.Cues.Select(c => c.textKey));
            var table = Table();
            foreach (var objective in catalog.Objectives)
            {
                Assert.That(cueKeys, Does.Not.Contain(objective.textKey));
                Assert.That(table[objective.textKey].group, Is.EqualTo("objective"));
            }

            foreach (var cue in catalog.BuildCues("surveyor.seq.Launch").Concat(catalog.BuildCues("surveyor.seq.Visit", true)))
                Assert.That(cue.Position, Is.EqualTo(SubtitlePosition.Bottom));
        }

        [Test]
        public void FinalActKeepsFiftySecondsWithFullReading()
        {
            var visit = catalog.Cues.Where(c => c.act == 6).OrderBy(c => c.startSeconds).ToList();
            Assert.That(visit.Select(c => c.cueId), Is.EqualTo(new[] { "SUR-S26", "SUR-S27", "SUR-S28", "SUR-S29", "SUR-S30", "SUR-S31" }));
            Assert.That(visit.Last().endSeconds, Is.EqualTo(50f));
            Assert.That(visit.Single(c => c.cueId == "SUR-S29").startSeconds, Is.EqualTo(32f));
            Assert.That(visit.Single(c => c.cueId == "SUR-S30").startSeconds, Is.EqualTo(38f));
            Assert.That(visit.Single(c => c.cueId == "SUR-S31").startSeconds, Is.EqualTo(44f));
            Assert.That(visit.Skip(3).Sum(c => c.minimumReadSeconds), Is.EqualTo(17f), "17 s combinados nos 18 s finais");
            foreach (var cue in visit)
                Assert.That(cue.endSeconds - cue.startSeconds, Is.GreaterThanOrEqualTo(cue.minimumReadSeconds));
        }

        [Test]
        public void EnlargedFontPagesKeepTheJointTimeAndTheText()
        {
            var table = Table();
            var paged = catalog.Cues.Where(c => c.HasPages).ToList();
            Assert.That(paged.Count, Is.GreaterThan(5));
            foreach (var cue in paged)
            {
                Assert.That(table[cue.pageKeys[0]].pt + " " + table[cue.pageKeys[1]].pt, Is.EqualTo(table[cue.textKey].pt), cue.cueId);
                Assert.That(table[cue.pageKeys[0]].en + " " + table[cue.pageKeys[1]].en, Is.EqualTo(table[cue.textKey].en), cue.cueId);
            }

            foreach (var sequenceId in catalog.Cues.Select(c => c.sequenceId).Distinct())
            {
                var normal = catalog.BuildCues(sequenceId);
                var large = catalog.BuildCues(sequenceId, true);
                foreach (var cue in large)
                    Assert.That(cue.Validate(), Is.Empty, cue.CueId);

                foreach (var window in normal.Where(c => !c.IsTriggered))
                {
                    var pages = large.Where(c => c.CueId.StartsWith(window.CueId + ".p", StringComparison.Ordinal)).ToList();
                    if (pages.Count == 0)
                        continue;
                    Assert.That(pages.Count, Is.EqualTo(2));
                    Assert.That(pages[0].StartSeconds, Is.EqualTo(window.StartSeconds).Within(1e-3f));
                    Assert.That(pages[0].EndSeconds, Is.EqualTo(pages[1].StartSeconds).Within(1e-3f), "sem lacuna nem sobreposição");
                    Assert.That(pages[1].EndSeconds, Is.EqualTo(window.EndSeconds).Within(1e-3f));
                }

                foreach (var trigger in normal.Where(c => c.IsTriggered))
                {
                    var pages = large.Where(c => c.CueId.StartsWith(trigger.CueId + ".p", StringComparison.Ordinal)).ToList();
                    if (pages.Count == 0)
                        continue;
                    Assert.That(pages[0].TriggerId, Is.EqualTo(trigger.TriggerId));
                    Assert.That(pages[1].TriggerId, Is.EqualTo(trigger.TriggerId + SurveyorSubtitles.PageTriggerSuffix));
                    Assert.That(pages.Sum(p => p.MinimumReadSeconds), Is.EqualTo(trigger.MinimumReadSeconds).Within(1e-3f));
                }
            }
        }

        [Test]
        public void NoScriptCarriesTheChapterText()
        {
            var table = Table();
            var phrases = table.Values
                .Where(r => r.group == "narrative" && r.key.IndexOf(".p", StringComparison.Ordinal) < 0)
                .SelectMany(r => new[] { r.pt, r.en })
                .Where(t => t.Length >= 25)
                .ToList();
            Assert.That(phrases.Count, Is.EqualTo(62));

            var offenders = new List<string>();
            foreach (var path in Directory.GetFiles(ChapterRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.Contains("/Tests/") || normalized.EndsWith("/SurveyorSubtitles.cs", StringComparison.Ordinal))
                    continue;

                var code = File.ReadAllText(path);
                foreach (var phrase in phrases)
                    if (code.Contains(phrase))
                        offenders.Add($"{normalized}: {phrase}");
            }

            Assert.That(offenders, Is.Empty, "texto do capítulo deve ficar nos JSONs");
        }
    }
}
