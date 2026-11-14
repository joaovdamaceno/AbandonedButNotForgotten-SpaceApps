using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// UI-004: serviço de localização PT/EN, apresentação das legendas (sem caixa vazia, carimbo de data/nome, troca de idioma sem perder o
    /// cue), integração com o SequenceDirector real da Pioneer e verificação das tabelas e do tempo de leitura de todo o projeto.
    /// </summary>
    public class LocalizationTests
    {
        const string Table = "{\"texts\":[" +
            "{\"key\":\"T.ONE\",\"group\":\"narrative\",\"pt\":\"Um\",\"en\":\"One\"}," +
            "{\"key\":\"T.ONLYPT\",\"group\":\"narrative\",\"pt\":\"Só em português\",\"en\":\"\"}," +
            "{\"key\":\"T.CARD\",\"group\":\"card\",\"pt\":\"13 JUN 1983\",\"en\":\"13 JUN 1983\"}," +
            "{\"key\":\"T.LONG\",\"group\":\"narrative\",\"pt\":\"Uma frase longa de teste para a legenda.\",\"en\":\"A long test sentence for the subtitle.\"}" +
            "]}";

        LocalizationService localization;
        SubtitlePresenter presenter;
        VisualElement host;
        GameObject go;

        [SetUp]
        public void SetUp()
        {
            localization = new LocalizationService();
            Assert.That(localization.AddTable(Table, "teste").Count, Is.EqualTo(1), "só T.ONLYPT sem EN reclama");
            go = new GameObject("presenter");
            presenter = go.AddComponent<SubtitlePresenter>();
            host = new VisualElement();
            presenter.Initialize(localization, null, host);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        static SubtitleCue Cue(string id, string key, int priority = 0, SubtitlePosition position = SubtitlePosition.Bottom) =>
            new SubtitleCue(id, key, 0f, 5f, position, priority);

        // ---------- LocalizationService ----------

        [Test]
        public void LanguageCodesAreNormalizedAndOnlyARealChangeRaisesTheEvent()
        {
            var changes = new List<string>();
            localization.LanguageChanged += changes.Add;
            Assert.That(localization.Language, Is.EqualTo("pt"));
            Assert.That(localization.SetLanguage("pt-BR"), Is.False, "mesmo idioma: nada");
            Assert.That(localization.SetLanguage("EN-us"), Is.True);
            Assert.That(localization.SetLanguage("en"), Is.False);
            Assert.That(localization.SetLanguage("fr"), Is.False, "idioma desconhecido não muda nada");
            Assert.That(localization.SetLanguage(null), Is.False);
            Assert.That(localization.Language, Is.EqualTo("en"));
            Assert.That(changes, Is.EqualTo(new[] { "en" }));
            Assert.That(localization.ApplySettings(new SettingsData { languageCode = "pt" }), Is.True);
            Assert.That(localization.Language, Is.EqualTo("pt"));
        }

        [Test]
        public void GetNeverReturnsTheKeyAndAMissingOrEmptyTextComesBackEmptyAndRecorded()
        {
            Assert.That(localization.Get("T.ONE"), Is.EqualTo("Um"));
            localization.SetLanguage("en");
            Assert.That(localization.Get("T.ONE"), Is.EqualTo("One"));
            Assert.That(localization.Get("T.ONLYPT"), Is.Empty, "texto vazio na tradução: vazio, não a chave nem o outro idioma");
            Assert.That(localization.Get("T.NOPE"), Is.Empty);
            Assert.That(localization.MissingRequests, Is.EquivalentTo(new[] { "T.ONLYPT", "T.NOPE" }));
            Assert.That(localization.TryGet("T.ONE", "pt", out var pt) && pt == "Um", Is.True);
            Assert.That(localization.Resolver("T.ONE"), Is.EqualTo("One"));
            Assert.That(localization.KeysMissingLanguage(), Is.EqualTo(new[] { "T.ONLYPT" }));
            Assert.That(localization.Validate().Any(e => e.Contains("T.ONLYPT")), Is.True);
        }

        [Test]
        public void DuplicatesAreRepetitionsWhenTheTextMatchesAndConflictsWhenItDoesNot()
        {
            var service = new LocalizationService();
            Assert.That(service.AddTable("{\"texts\":[{\"key\":\"A.B\",\"pt\":\"x\",\"en\":\"y\"}]}", "a"), Is.Empty);
            Assert.That(service.AddTable("{\"texts\":[{\"key\":\"A.B\",\"pt\":\"x\",\"en\":\"y\"}]}", "b"), Is.Empty, "mesmo texto: só repetição");
            var conflict = service.AddTable("{\"texts\":[{\"key\":\"A.B\",\"pt\":\"outro\",\"en\":\"y\"}]}", "c");
            Assert.That(conflict.Single(), Does.Contain("A.B").And.Contain("a").And.Contain("diferente"));
            Assert.That(service.Get("A.B"), Is.EqualTo("x"), "vale o primeiro");
            Assert.That(service.Count, Is.EqualTo(1));
            Assert.That(service.Validate(), Is.Not.Empty);
        }

        [Test]
        public void InvalidKeysAndBrokenJsonAreReportedWithoutThrowing()
        {
            var service = new LocalizationService();
            Assert.That(service.AddTable("{\"texts\":[{\"key\":\"1 inválida\",\"pt\":\"x\",\"en\":\"y\"},{\"key\":\"OK.1\",\"pt\":\"x\",\"en\":\"y\"}]}", "t").Single(), Does.Contain("chave inválida"));
            Assert.That(service.Contains("OK.1"), Is.True);
            Assert.That(service.AddTable("isto não é json", "x"), Is.Not.Empty);
            Assert.That(service.AddTable("", "vazio"), Is.Not.Empty);
            Assert.That(service.AddTable("{\"outra\":1}", "sem lista"), Is.Not.Empty);
        }

        [Test]
        public void EveryTextTableOfTheProjectLoadsTogetherInBothLanguagesWithoutConflicts()
        {
            var service = new LocalizationService();
            var files = Directory.GetFiles("Assets/_Game", "*.json", SearchOption.AllDirectories)
                .Where(f => File.ReadAllText(f).Contains("\"texts\"") && (f.EndsWith("Texts.json") || f.EndsWith("Subtitles.json"))).ToList();
            Assert.That(files.Count, Is.GreaterThanOrEqualTo(14), "tabelas de Marte, HUD, dossiê e Pioneer");
            foreach (var file in files)
                service.AddTable(File.ReadAllText(file), Path.GetFileName(file));

            Assert.That(service.Problems, Is.Empty, "nenhum conflito de texto nem chave inválida: " + string.Join("; ", service.Problems));
            Assert.That(service.KeysMissingLanguage(), Is.Empty, "todas as chaves existem em PT e EN");
            Assert.That(service.Count, Is.GreaterThan(300));
            foreach (var key in service.Keys)
            {
                Assert.That(service.TryGet(key, "pt", out _) && service.TryGet(key, "en", out _), Is.True, key);
            }
        }

        [Test]
        public void ThePioneerExportMatchesTheSourceTablesOfThePioneerTask()
        {
            var service = new LocalizationService();
            Assert.That(service.AddTable(File.ReadAllText("Assets/_Game/Shared/Localization/Pioneer10Texts.json"), "Pioneer10Texts.json"), Is.Empty);
            var pt = JsonUtility.FromJson<Source>(File.ReadAllText("Data/Localization/Pioneer10.pt-BR.json")).entries;
            var en = JsonUtility.FromJson<Source>(File.ReadAllText("Data/Localization/Pioneer10.en.json")).entries;
            Assert.That(service.Count, Is.EqualTo(pt.Length).And.EqualTo(en.Length));
            for (var i = 0; i < pt.Length; i++)
            {
                Assert.That(service.TryGet(pt[i].key, "pt", out var p) && p == pt[i].text, Is.True, pt[i].key);
                Assert.That(service.TryGet(en[i].key, "en", out var e) && e == en[i].text, Is.True, en[i].key);
                Assert.That(service.GetGroup(pt[i].key), Is.EqualTo(pt[i].group));
            }
        }

        [System.Serializable] sealed class Source { public SourceRow[] entries; }

        [System.Serializable] sealed class SourceRow { public string key, group, text; }

        // ---------- SubtitlePresenter ----------

        [Test]
        public void NoEmptyBoxIsEverVisibleBeforeDuringGapsOrAfterTheCue()
        {
            var plate = host.Q("subtitle");
            Assert.That(plate.ClassListContains(SubtitlePresenter.HiddenClass), Is.True, "nada na tela antes do primeiro cue");
            var cue = Cue("C1", "T.ONE");
            presenter.ShowCue(cue);
            Assert.That(plate.ClassListContains(SubtitlePresenter.HiddenClass), Is.False);
            Assert.That(presenter.CurrentText, Is.EqualTo("Um"));
            Assert.That(presenter.CurrentCueId, Is.EqualTo("C1"));
            presenter.HideCue(cue);
            Assert.That(plate.ClassListContains(SubtitlePresenter.HiddenClass), Is.True, "entre cues a placa some inteira");
            Assert.That(presenter.CurrentText, Is.Empty);
            Assert.That(presenter.IsVisible(SubtitlePosition.Top), Is.False);
        }

        [Test]
        public void AnEmptyTranslationHidesTheWholePlateEvenWhileTheCueIsActiveAndItReturnsWithTheText()
        {
            presenter.ShowCue(Cue("C1", "T.ONLYPT"));
            Assert.That(presenter.IsVisible(SubtitlePosition.Bottom), Is.True);
            localization.SetLanguage("en");
            Assert.That(presenter.IsVisible(SubtitlePosition.Bottom), Is.False, "sem texto em inglês: nenhuma caixa vazia");
            Assert.That(presenter.ActiveCueCount, Is.EqualTo(1), "o cue continua ativo (o diretor é quem o encerra)");
            localization.SetLanguage("pt");
            Assert.That(presenter.IsVisible(SubtitlePosition.Bottom), Is.True);
            Assert.That(presenter.CurrentText, Is.EqualTo("Só em português"));

        }

        [Test]
        public void SwitchingLanguageRedrawsTheSameCueOnceWithoutRestartingIt()
        {
            presenter.ShowCue(Cue("C1", "T.LONG"));
            var drawn = presenter.RenderCount;
            localization.SetLanguage("en");
            Assert.That(presenter.CurrentText, Is.EqualTo("A long test sentence for the subtitle."));
            Assert.That(presenter.CurrentCueId, Is.EqualTo("C1"));
            Assert.That(presenter.ActiveCueCount, Is.EqualTo(1));
            Assert.That(presenter.RenderCount, Is.EqualTo(drawn + 1));
            localization.SetLanguage("en");
            Assert.That(presenter.RenderCount, Is.EqualTo(drawn + 1), "repetir o idioma não redesenha");
        }

        [Test]
        public void DatesAndNamesUseTheirOwnStampHierarchyDifferentFromTheSubtitleBody()
        {
            var plate = host.Q("subtitle");
            presenter.ShowCue(Cue("DATE", "T.CARD"));
            var stamp = host.Q<Label>("subtitle-stamp");
            var body = host.Q<Label>("subtitle-text");
            Assert.That(stamp.text, Is.EqualTo("13 JUN 1983"));
            Assert.That(body.text, Is.Empty);
            Assert.That(body.resolvedStyle.display == DisplayStyle.None || body.style.display == DisplayStyle.None, Is.True, "sem corpo, o corpo não ocupa a placa");
            Assert.That(plate.ClassListContains(SubtitlePresenter.StampClass), Is.True);
            Assert.That(stamp.ClassListContains("sa-stamp"), Is.True, "carimbo: Plex Mono do design system");
            Assert.That(body.ClassListContains("sa-subtitle__text"), Is.True, "corpo: Outfit da legenda");
            Assert.That(stamp.ClassListContains("sa-subtitle__text"), Is.False, "hierarquias distintas");

            presenter.HideCue(Cue("DATE", "T.CARD"));
            presenter.ShowCue(Cue("DATE2", "T.CARD"));
            presenter.SetStamp("T.CARD");
            presenter.ShowCue(Cue("BODY", "T.ONE", priority: 1));
            Assert.That(body.text, Is.EqualTo("Um"));
            Assert.That(stamp.text, Is.EqualTo("13 JUN 1983"), "um carimbo avulso fica acima do corpo");
            presenter.ClearStamp();
            Assert.That(stamp.style.display == DisplayStyle.None || string.IsNullOrEmpty(stamp.text), Is.True);
        }

        [Test]
        public void TheHighestPriorityWinsAndTheOlderCueReturnsWhenItLeaves()
        {
            var window = Cue("WINDOW", "T.ONE");
            var trigger = Cue("TRIGGER", "T.LONG", priority: 1);
            presenter.ShowCue(window);
            presenter.ShowCue(trigger);
            Assert.That(presenter.CurrentCueId, Is.EqualTo("TRIGGER"));
            var same = Cue("SAME", "T.ONE", priority: 1);
            presenter.ShowCue(same);
            Assert.That(presenter.CurrentCueId, Is.EqualTo("SAME"), "mesma prioridade: o mais recente");
            presenter.HideCue(same);
            Assert.That(presenter.CurrentCueId, Is.EqualTo("TRIGGER"));
            presenter.HideCue(trigger);
            Assert.That(presenter.CurrentCueId, Is.EqualTo("WINDOW"), "volta o cue anterior que ainda estava ativo");
            presenter.Clear();
            Assert.That(presenter.ActiveCueCount, Is.Zero);
            Assert.That(presenter.IsVisible(SubtitlePosition.Bottom), Is.False);
        }

        [Test]
        public void BottomAndTopPlatesAreIndependentAndTheLayerEntersTheHostColumn()
        {
            presenter.ShowCue(Cue("B", "T.ONE"));
            presenter.ShowCue(Cue("T", "T.LONG", position: SubtitlePosition.Top));
            Assert.That(presenter.IsVisible(SubtitlePosition.Bottom), Is.True);
            Assert.That(presenter.IsVisible(SubtitlePosition.Top), Is.True);
            Assert.That(host.Q("subtitle-top").ClassListContains("pio-subtitle--top"), Is.True);
            Assert.That(presenter.Layer.parent, Is.SameAs(host), "a camada entra na coluna do HUD, sem criar outro documento");
            Assert.That(presenter.Layer.ClassListContains("sa-subtitle-layer--hosted"), Is.True);
        }

        [Test]
        public void ReinitializingCleansTheScreenAndRemovesTheOldLayer()
        {
            presenter.ShowCue(Cue("B", "T.ONE"));
            var other = new VisualElement();
            presenter.Initialize(localization, null, other);
            Assert.That(host.childCount, Is.Zero, "a camada antiga saiu da coluna");
            Assert.That(presenter.ActiveCueCount, Is.Zero);
            Assert.That(presenter.IsVisible(SubtitlePosition.Bottom), Is.False);
        }

        [Test]
        public void ThePrefabWithItsOwnDocumentIsCreatedOnceAndIsIdempotent()
        {
            Assert.That(SubtitleLayerSetup.Create(), Is.Empty);
            Assert.That(SubtitleLayerSetup.Create(), Is.Empty);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SubtitleLayerSetup.PrefabPath);
            Assert.That(prefab.GetComponent<SubtitlePresenter>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Abandoned.SpaceApps.UI.ResponsiveLayoutClass>(), Is.Not.Null, "UI_SPEC: todo UIDocument de tela cheia");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var own = instance.GetComponent<SubtitlePresenter>();
                own.Initialize(localization, null);
                own.ShowCue(Cue("B", "T.ONE"));
                Assert.That(own.IsVisible(SubtitlePosition.Bottom), Is.True);
                Assert.That(own.Layer.ClassListContains("sa-subtitle-layer--hosted"), Is.False, "sem HUD a camada cobre a tela");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ---------- Com o SequenceDirector real da Pioneer ----------

        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        [Test]
        public void TheRealPioneerSequencesShowEachCueOnlyInItsWindowInBothLanguagesAndNeverLeaveABox()
        {
            var service = new LocalizationService();
            service.AddTable(File.ReadAllText("Assets/_Game/Shared/Localization/Pioneer10Texts.json"), "Pioneer10Texts.json");
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
            var host2 = new GameObject("services");
            try
            {
                var time = new FakeTime();
                var router = host2.AddComponent<InputRouter>();
                var director = host2.AddComponent<SequenceDirector>();
                router.Enable();
                presentation.CameraDirector.GroundProbe = _ => null;
                director.Initialize(presentation.CameraDirector, router, time);
                presentation.RegisterWithCameraDirector();

                var layerHost = new VisualElement();
                var own = host2.AddComponent<SubtitlePresenter>();
                own.Initialize(service, director, layerHost);

                var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>("Assets/_Game/Chapters/Pioneer10/Sequences/PIO_Outbound.asset");
                Assert.That(sequence, Is.Not.Null);
                director.Play(sequence, false);
                var seenText = new List<string>();
                for (var t = 0f; t < 39f; t += 0.1f)
                {
                    director.Tick(0.1f);
                    var expected = sequence.Cues.FirstOrDefault(c => !c.IsTriggered && director.TimeSeconds >= c.StartSeconds + 0.05f && director.TimeSeconds < c.EndSeconds - 0.05f);
                    if (expected != null)
                    {
                        Assert.That(own.CurrentCueId, Is.EqualTo(expected.CueId), "t=" + director.TimeSeconds);
                        Assert.That(own.CurrentText, Is.Not.Empty);
                        seenText.Add(own.CurrentText);
                    }
                    else if (!sequence.Cues.Any(c => !c.IsTriggered && director.TimeSeconds >= c.StartSeconds - 0.05f && director.TimeSeconds <= c.EndSeconds + 0.05f))
                    {
                        Assert.That(own.IsVisible(SubtitlePosition.Bottom), Is.False, "lacuna sem legenda: nenhuma caixa em t=" + director.TimeSeconds);
                    }

                    if (t > 20f && t < 20.5f)
                        service.SetLanguage("en"); // troca no meio de um cue: o cue e o tempo continuam
                }

                Assert.That(service.Language, Is.EqualTo("en"));
                Assert.That(seenText.Distinct().Count(), Is.GreaterThanOrEqualTo(5), "as cinco legendas do estado apareceram");
                director.Tick(1f);
                Assert.That(director.IsPlaying, Is.False);
                Assert.That(own.IsVisible(SubtitlePosition.Bottom), Is.False, "ao terminar, nada fica na tela");
            }
            finally
            {
                presentation.UnregisterFromCameraDirector();
                Object.DestroyImmediate(host2);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void EveryPioneerCueHasTextInBothLanguagesAndTheTooFastOnesAreOnlyTheDeclaredStretchCases()
        {
            var service = new LocalizationService();
            service.AddTable(File.ReadAllText("Assets/_Game/Shared/Localization/Pioneer10Texts.json"), "Pioneer10Texts.json");
            var sequences = Directory.GetFiles("Assets/_Game/Chapters/Pioneer10/Sequences", "*.asset")
                .Select(AssetDatabase.LoadAssetAtPath<SequenceDefinition>).Where(s => s != null).ToList();
            Assert.That(sequences.Count, Is.GreaterThanOrEqualTo(5));
            var tooFast = new List<string>();
            foreach (var sequence in sequences)
            {
                foreach (var language in new[] { "pt", "en" })
                {
                    foreach (var row in SubtitleReadingCheck.Analyze(sequence, service, language))
                    {
                        Assert.That(row.Missing, Is.False, $"{sequence.name} {row.CueId} {language}: sem texto");
                        if (row.TooFast)
                            tooFast.Add($"{sequence.name}:{row.CueId}:{language}:{row.CharactersPerSecond:0.0}");
                    }
                }
            }

            Assert.That(tooFast, Is.Empty, "cues rápidos demais (>22 caracteres/s): " + string.Join(", ", tooFast));
        }
    }
}
