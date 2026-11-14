using System.Collections.Generic;
using NUnit.Framework;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// UI-002: regras do dossiê sem Unity — seleção sem pré-requisito, um único início por vez, idioma travado
    /// durante o carregamento, painéis opcionais, foco por teclado e textos PT/EN.
    /// </summary>
    public class DossierFlowTests
    {
        const string Mars = "mars";
        const string Pioneer = "pioneer10";
        const string Surveyor = "surveyor3";

        UI.DossierFlow flow;
        int changes;

        [SetUp]
        public void SetUp()
        {
            flow = NewFlow(true, true, true);
            flow.OnAppState(UI.DossierAppState.Dossier);
        }

        UI.DossierFlow NewFlow(bool mars, bool pioneer, bool surveyor, string language = "pt")
        {
            var created = new UI.DossierFlow(new[]
            {
                new UI.DossierFolderInfo(Mars, mars),
                new UI.DossierFolderInfo(Pioneer, pioneer),
                new UI.DossierFolderInfo(Surveyor, surveyor),
            }, language);
            changes = 0;
            created.Changed += () => changes++;
            return created;
        }

        /// <summary>Simula o ChapterDirector aceitando o pedido: Loading, depois Playing.</summary>
        void DirectorLoadsAndPlays()
        {
            flow.OnAppState(UI.DossierAppState.Loading);
            flow.OnAppState(UI.DossierAppState.Playing);
        }

        /// <summary>Simula concluir e voltar ao arquivo.</summary>
        void DirectorFinishesAndReturns()
        {
            flow.OnAppState(UI.DossierAppState.Completion);
            flow.OnAppState(UI.DossierAppState.Unloading);
            flow.OnAppState(UI.DossierAppState.Dossier);
        }

        // ---------- Estado inicial e visibilidade ----------

        [Test]
        public void StartsHiddenAndShowsOnlyInDossierState()
        {
            var fresh = NewFlow(true, true, true);
            Assert.That(fresh.Phase, Is.EqualTo(UI.DossierPhase.Hidden));
            Assert.That(fresh.TryStart(Mars), Is.EqualTo(UI.StartResult.NotVisible));
            Assert.That(fresh.StartPending, Is.False);

            foreach (var hidden in new[] { UI.DossierAppState.Boot, UI.DossierAppState.Playing, UI.DossierAppState.Completion })
            {
                fresh.OnAppState(hidden);
                Assert.That(fresh.Phase, Is.EqualTo(UI.DossierPhase.Hidden), hidden.ToString());
            }

            fresh.OnAppState(UI.DossierAppState.Dossier);
            Assert.That(fresh.Phase, Is.EqualTo(UI.DossierPhase.Browsing));
        }

        [Test]
        public void LoadingAndUnloadingBothShowTheLoadingPhase()
        {
            flow.OnAppState(UI.DossierAppState.Loading);
            Assert.That(flow.Phase, Is.EqualTo(UI.DossierPhase.Loading));
            flow.OnAppState(UI.DossierAppState.Unloading);
            Assert.That(flow.Phase, Is.EqualTo(UI.DossierPhase.Loading));
            Assert.That(flow.IsBusy, Is.True);
        }

        // ---------- Todos os capítulos iniciam, sem pré-requisito ----------

        [Test]
        public void AnyChapterStartsInAnyOrderTwiceWithoutPrerequisites()
        {
            foreach (var round in new[] { 1, 2 })
            {
                foreach (var slot in new[] { Surveyor, Mars, Pioneer })
                {
                    Assert.That(flow.CanStart(slot), Is.EqualTo(UI.StartResult.Accepted), $"rodada {round}: {slot} liberado");
                    Assert.That(flow.TryStart(slot), Is.EqualTo(UI.StartResult.Accepted), $"rodada {round}: {slot} aceito");
                    DirectorLoadsAndPlays();
                    Assert.That(flow.Phase, Is.EqualTo(UI.DossierPhase.Hidden));
                    DirectorFinishesAndReturns();
                    Assert.That(flow.Phase, Is.EqualTo(UI.DossierPhase.Browsing));
                    Assert.That(flow.IsBusy, Is.False, "depois de voltar o arquivo responde de novo");
                }
            }
        }

        // ---------- Cliques rápidos ----------

        [Test]
        public void DoubleClickInTheSameFrameIsAcceptedOnlyOnce()
        {
            var accepted = 0;
            for (var click = 0; click < 5; click++)
            {
                if (flow.TryStart(Mars) == UI.StartResult.Accepted)
                    accepted++;
            }

            Assert.That(accepted, Is.EqualTo(1), "cinco cliques antes de o diretor mudar de estado");
            Assert.That(flow.TryStart(Mars), Is.EqualTo(UI.StartResult.Busy));
        }

        [Test]
        public void ClickingAnotherFolderWhilePendingOrLoadingIsRejected()
        {
            Assert.That(flow.TryStart(Mars), Is.EqualTo(UI.StartResult.Accepted));
            Assert.That(flow.TryStart(Pioneer), Is.EqualTo(UI.StartResult.Busy), "pendente");

            flow.OnAppState(UI.DossierAppState.Loading);
            Assert.That(flow.TryStart(Surveyor), Is.EqualTo(UI.StartResult.Busy), "carregando");
            Assert.That(flow.TryStart(Mars), Is.EqualTo(UI.StartResult.Busy), "carregando, mesma pasta");
        }

        [Test]
        public void PendingStartSurvivesLoadingAndClearsWhenTheChapterRuns()
        {
            flow.TryStart(Mars);
            flow.OnAppState(UI.DossierAppState.Loading);
            Assert.That(flow.StartPending, Is.True);
            flow.OnAppState(UI.DossierAppState.Playing);
            Assert.That(flow.StartPending, Is.False);
        }

        // ---------- Idioma durante o carregamento ----------

        [Test]
        public void LanguageCannotChangeWhilePendingOrLoadingAndStaysUnchanged()
        {
            Assert.That(flow.LanguageCode, Is.EqualTo("pt"));
            flow.TryStart(Mars);
            Assert.That(flow.TrySetLanguage("en"), Is.False, "início pendente");
            flow.OnAppState(UI.DossierAppState.Loading);
            Assert.That(flow.TrySetLanguage("en"), Is.False, "carregando");
            flow.OnAppState(UI.DossierAppState.Unloading);
            Assert.That(flow.TrySetLanguage("en"), Is.False, "descarregando");
            Assert.That(flow.LanguageCode, Is.EqualTo("pt"));
        }

        [Test]
        public void LanguageChangesInTheArchiveAndRejectsInvalidOrRepeatedCodes()
        {
            Assert.That(flow.TrySetLanguage("en"), Is.True);
            Assert.That(flow.LanguageCode, Is.EqualTo("en"));
            Assert.That(flow.TrySetLanguage("en"), Is.False, "repetido");
            Assert.That(flow.TrySetLanguage("fr"), Is.False, "não suportado");
            Assert.That(flow.TrySetLanguage(null), Is.False);
            Assert.That(flow.LanguageCode, Is.EqualTo("en"));
        }

        [Test]
        public void ExternalLanguageChangeIsAppliedEvenWhileBusy()
        {
            flow.TryStart(Mars);
            flow.ApplyExternalLanguage("en");
            Assert.That(flow.LanguageCode, Is.EqualTo("en"));
            flow.ApplyExternalLanguage("xx");
            Assert.That(flow.LanguageCode, Is.EqualTo("en"));
        }

        [Test]
        public void UnsupportedInitialLanguageFallsBackToPortuguese()
        {
            Assert.That(NewFlow(true, true, true, "de").LanguageCode, Is.EqualTo("pt"));
            Assert.That(NewFlow(true, true, true, null).LanguageCode, Is.EqualTo("pt"));
            Assert.That(NewFlow(true, true, true, "en").LanguageCode, Is.EqualTo("en"));
        }

        // ---------- Falhas ----------

        [Test]
        public void LoadFailureShowsTheErrorFreesTheButtonsAndRetryClearsIt()
        {
            flow.TryStart(Mars);
            flow.OnAppState(UI.DossierAppState.Loading);

            // O ChapterDirector volta ao Dossier e só depois dispara LoadFailed.
            flow.OnAppState(UI.DossierAppState.Dossier);
            flow.OnLoadFailed("cena ausente");

            Assert.That(flow.ErrorMessage, Is.EqualTo("cena ausente"));
            Assert.That(flow.IsBusy, Is.False);
            Assert.That(flow.Phase, Is.EqualTo(UI.DossierPhase.Browsing));
            Assert.That(flow.TryStart(Mars), Is.EqualTo(UI.StartResult.Accepted), "tentar de novo");
            Assert.That(flow.ErrorMessage, Is.Null, "a nova tentativa limpa o erro");
        }

        [Test]
        public void ReleaseStartFreesTheLatchWhenBeginWasIgnoredOrThrew()
        {
            Assert.That(flow.TryStart(Mars), Is.EqualTo(UI.StartResult.Accepted));
            flow.ReleaseStart("falhou");
            Assert.That(flow.StartPending, Is.False);
            Assert.That(flow.ErrorMessage, Is.EqualTo("falhou"));
            Assert.That(flow.TryStart(Pioneer), Is.EqualTo(UI.StartResult.Accepted));
        }

        [Test]
        public void ReleaseStartWithoutPendingStartDoesNothing()
        {
            var before = changes;
            flow.ReleaseStart();
            Assert.That(changes, Is.EqualTo(before));
        }

        [Test]
        public void ClearErrorRemovesTheMessageOnce()
        {
            flow.OnLoadFailed("x");
            flow.ClearError();
            Assert.That(flow.ErrorMessage, Is.Null);
            var before = changes;
            flow.ClearError();
            Assert.That(changes, Is.EqualTo(before), "sem erro não notifica");
        }

        // ---------- Capítulos indisponíveis e pastas desconhecidas ----------

        [Test]
        public void UnavailableChapterCannotStartAndDoesNotLatch()
        {
            var partial = NewFlow(true, false, false);
            partial.OnAppState(UI.DossierAppState.Dossier);

            Assert.That(partial.TryStart(Pioneer), Is.EqualTo(UI.StartResult.NotAvailable));
            Assert.That(partial.StartPending, Is.False);
            Assert.That(partial.TryStart(Surveyor), Is.EqualTo(UI.StartResult.NotAvailable));
            Assert.That(partial.TryStart(Mars), Is.EqualTo(UI.StartResult.Accepted), "o disponível continua iniciando");
        }

        [Test]
        public void UnknownFolderIsRejected()
        {
            Assert.That(flow.TryStart("jupiter"), Is.EqualTo(UI.StartResult.UnknownFolder));
            Assert.That(flow.TryStart(null), Is.EqualTo(UI.StartResult.UnknownFolder));
            Assert.That(flow.StartPending, Is.False);
        }

        [Test]
        public void DuplicateFolderOrNullListIsRejectedAtConstruction()
        {
            Assert.Throws<System.ArgumentException>(() => new UI.DossierFlow(new[]
            {
                new UI.DossierFolderInfo(Mars, true),
                new UI.DossierFolderInfo(Mars, true),
            }, "pt"));
            Assert.Throws<System.ArgumentNullException>(() => new UI.DossierFlow(null, "pt"));
            Assert.Throws<System.ArgumentException>(() => new UI.DossierFolderInfo("", true));
        }

        // ---------- Painéis opcionais ----------

        [Test]
        public void DetailsAreOptionalAndDoNotBlockStartingTheSameFolder()
        {
            Assert.That(flow.CanStart(Mars), Is.EqualTo(UI.StartResult.Accepted), "sem abrir detalhes");
            Assert.That(flow.TryOpenPanel(UI.DossierPanel.Details, Mars), Is.True);
            Assert.That(flow.Panel, Is.EqualTo(UI.DossierPanel.Details));
            Assert.That(flow.PanelSlotId, Is.EqualTo(Mars));
            Assert.That(flow.CanStart(Mars), Is.EqualTo(UI.StartResult.Accepted), "Iniciar dentro dos detalhes");
            Assert.That(flow.CanStart(Pioneer), Is.EqualTo(UI.StartResult.PanelOpen));
        }

        [Test]
        public void SettingsAndCreditsCoverTheFoldersAndBackReturns()
        {
            foreach (var panel in new[] { UI.DossierPanel.Settings, UI.DossierPanel.Credits })
            {
                Assert.That(flow.TryOpenPanel(panel, Mars), Is.True);
                Assert.That(flow.PanelSlotId, Is.Null, "só Detalhes guarda a pasta");
                Assert.That(flow.CanStart(Mars), Is.EqualTo(UI.StartResult.PanelOpen));
                Assert.That(flow.Back(), Is.True);
                Assert.That(flow.Panel, Is.EqualTo(UI.DossierPanel.None));
                Assert.That(flow.CanStart(Mars), Is.EqualTo(UI.StartResult.Accepted));
            }

            Assert.That(flow.Back(), Is.False, "sem painel não há o que fechar");
        }

        [Test]
        public void PanelsRefuseUnknownFolderNonePanelHiddenAndBusyStates()
        {
            Assert.That(flow.TryOpenPanel(UI.DossierPanel.Details, "jupiter"), Is.False);
            Assert.That(flow.TryOpenPanel(UI.DossierPanel.Details, null), Is.False);
            Assert.That(flow.TryOpenPanel(UI.DossierPanel.None), Is.False);

            flow.TryStart(Mars);
            Assert.That(flow.TryOpenPanel(UI.DossierPanel.Settings), Is.False, "início pendente");
            flow.OnAppState(UI.DossierAppState.Loading);
            Assert.That(flow.TryOpenPanel(UI.DossierPanel.Credits), Is.False, "carregando");

            flow.OnAppState(UI.DossierAppState.Playing);
            Assert.That(flow.TryOpenPanel(UI.DossierPanel.Credits), Is.False, "dossiê oculto");
        }

        [Test]
        public void EnteringLoadingOrReturningToTheArchiveClosesOpenPanels()
        {
            flow.TryOpenPanel(UI.DossierPanel.Details, Pioneer);
            flow.OnAppState(UI.DossierAppState.Loading);
            Assert.That(flow.Panel, Is.EqualTo(UI.DossierPanel.None));
            Assert.That(flow.PanelSlotId, Is.Null);

            flow.OnAppState(UI.DossierAppState.Dossier);
            flow.TryOpenPanel(UI.DossierPanel.Settings);
            flow.OnAppState(UI.DossierAppState.Dossier);
            Assert.That(flow.Panel, Is.EqualTo(UI.DossierPanel.None));
        }

        [Test]
        public void OpeningTheSamePanelTwiceDoesNotNotifyAgain()
        {
            flow.TryOpenPanel(UI.DossierPanel.Settings);
            var before = changes;
            Assert.That(flow.TryOpenPanel(UI.DossierPanel.Settings), Is.True);
            Assert.That(changes, Is.EqualTo(before));
        }

        // ---------- Foco por teclado ----------

        [Test]
        public void FocusRingWrapsAndStartsFromNothingFocused()
        {
            Assert.That(UI.FocusRing.Step(-1, 4, 1), Is.EqualTo(0));
            Assert.That(UI.FocusRing.Step(-1, 4, -1), Is.EqualTo(3));
            Assert.That(UI.FocusRing.Step(0, 4, -1), Is.EqualTo(3));
            Assert.That(UI.FocusRing.Step(3, 4, 1), Is.EqualTo(0));
            Assert.That(UI.FocusRing.Step(1, 4, 1), Is.EqualTo(2));
            Assert.That(UI.FocusRing.Step(1, 4, -1), Is.EqualTo(0));
            Assert.That(UI.FocusRing.Step(0, 0, 1), Is.EqualTo(-1), "lista vazia");
            Assert.That(UI.FocusRing.Step(9, 3, 1), Is.EqualTo(0), "índice antigo fora da lista nova");
        }

        // ---------- Rótulos ----------

        [Test]
        public void DurationShowsTheEditorialRangeInsideItAndTheRealTargetOutside()
        {
            Assert.That(UI.DossierFormat.Duration(360f), Is.EqualTo("5–7 MIN"));
            Assert.That(UI.DossierFormat.Duration(300f), Is.EqualTo("5–7 MIN"), "limite inferior");
            Assert.That(UI.DossierFormat.Duration(420f), Is.EqualTo("5–7 MIN"), "limite superior");
            Assert.That(UI.DossierFormat.Duration(600f), Is.EqualTo("≈ 10 MIN"));
            Assert.That(UI.DossierFormat.Duration(150f), Is.EqualTo("≈ 3 MIN"));
            Assert.That(UI.DossierFormat.Duration(20f), Is.EqualTo("≈ 1 MIN"), "nunca zero minutos");
            Assert.That(UI.DossierFormat.Duration(0f), Is.EqualTo("5–7 MIN"), "sem definição");
            Assert.That(UI.DossierFormat.Duration(float.NaN), Is.EqualTo("5–7 MIN"));
        }

        [Test]
        public void RomanNumeralsLabelTheChapters()
        {
            var expected = new Dictionary<int, string>
            {
                { 1, "I" }, { 2, "II" }, { 3, "III" }, { 4, "IV" }, { 5, "V" }, { 6, "VI" },
                { 9, "IX" }, { 10, "X" }, { 14, "XIV" }, { 19, "XIX" }, { 38, "XXXVIII" },
            };

            foreach (var pair in expected)
                Assert.That(UI.DossierFormat.Roman(pair.Key), Is.EqualTo(pair.Value), pair.Key.ToString());

            Assert.That(UI.DossierFormat.Roman(0), Is.EqualTo("0"));
            Assert.That(UI.DossierFormat.Roman(40), Is.EqualTo("40"));
        }

        // ---------- Textos PT/EN ----------

        [Test]
        public void TextCatalogResolvesPerLanguageAndFallsBackOnlyWhenMissing()
        {
            var texts = new UI.DossierTextCatalog();
            Assert.That(texts.Add("a.title", "Título", "Title"), Is.True);
            Assert.That(texts.Add("a.onlypt", "Só PT", ""), Is.True);
            Assert.That(texts.Add("", "x", "y"), Is.False, "chave vazia");

            Assert.That(texts.Get("a.title", "pt"), Is.EqualTo("Título"));
            Assert.That(texts.Get("a.title", "en"), Is.EqualTo("Title"));
            Assert.That(texts.Get("a.onlypt", "en", "?"), Is.EqualTo("?"), "EN ausente não herda o PT em silêncio");
            Assert.That(texts.Get("nope", "pt", "alt"), Is.EqualTo("alt"));
            Assert.That(texts.Get("nope", "pt"), Is.EqualTo(""));
            Assert.That(texts.TryGet("a.title", "en", out var english), Is.True);
            Assert.That(english, Is.EqualTo("Title"));
            Assert.That(texts.KeysMissingLanguage().Count, Is.EqualTo(1));
            Assert.That(texts.KeysMissingLanguage()[0], Is.EqualTo("a.onlypt"));
        }

        [Test]
        public void TextCatalogKeepsInsertionOrderForPrefixesAndOverwritesWithoutReordering()
        {
            var texts = new UI.DossierTextCatalog();
            texts.Add("credits.line.b", "B", "B");
            texts.Add("other", "O", "O");
            texts.Add("credits.line.a", "A", "A");
            texts.Add("credits.line.b", "B2", "B2");

            var keys = texts.KeysWithPrefix("credits.line.");
            Assert.That(keys.Count, Is.EqualTo(2));
            Assert.That(keys[0], Is.EqualTo("credits.line.b"));
            Assert.That(keys[1], Is.EqualTo("credits.line.a"));
            Assert.That(texts.Get("credits.line.b", "pt"), Is.EqualTo("B2"));
            Assert.That(texts.Count, Is.EqualTo(3));
            Assert.That(texts.KeysWithPrefix("").Count, Is.EqualTo(0));
        }
    }
}
