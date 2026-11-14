using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// UI-002: os assets versionados (catálogo, prefab, UXML, textos) batem com o código. Pega divergência entre
    /// o UXML e o que a tela exige, chave de texto faltando em PT/EN e catálogo inválido antes de abrir o Play Mode.
    /// </summary>
    public class DossierAssetsTests
    {
        const string CatalogPath = "Assets/_Game/Core/ChapterCatalog.asset";
        const string PrefabPath = "Assets/_Game/UI/Dossier/DossierScreen.prefab";
        const string ScreenUxmlPath = "Assets/_Game/UI/Dossier/DossierScreen.uxml";
        const string FolderUxmlPath = "Assets/_Game/UI/Dossier/DossierFolder.uxml";
        const string TextsPath = "Assets/_Game/UI/Dossier/DossierTexts.json";

        /// <summary>Elementos que DossierScreen.BindInterface exige no UXML (mantenha igual ao código).</summary>
        static readonly string[] ScreenElements =
        {
            "dossier-root", "header-stamp", "game-title", "lede", "error-banner", "folders", "language-label", "language-pt",
            "language-en", "settings-button", "credits-button", "disclaimer", "panel-overlay", "panel-card", "panel-stamp",
            "panel-title", "panel-body", "panel-actions", "loading-overlay", "loading-stamp", "loading-title",
            "loading-chapter", "loading-fill", "loading-note", "loading-hint",
        };

        static readonly string[] FolderElements =
        {
            "folder", "folder-label", "folder-details", "folder-photo", "folder-photo-caption", "folder-title", "folder-meta",
            "folder-question", "folder-status", "folder-duration", "folder-start",
        };

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
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

        // ---------- Catálogo ----------

        [Test]
        public void CatalogAssetIsValidAndListsTheThreeFoldersInOrder()
        {
            var catalog = Load<UI.ChapterCatalog>(CatalogPath);
            Assert.That(string.Join(" | ", catalog.Validate()), Is.EqualTo(string.Empty));

            Assert.That(catalog.Entries.Count, Is.EqualTo(3));
            Assert.That(catalog.Entries[0].SlotId, Is.EqualTo("mars"));
            Assert.That(catalog.Entries[1].SlotId, Is.EqualTo("pioneer10"));
            Assert.That(catalog.Entries[2].SlotId, Is.EqualTo("surveyor3"));
            Assert.That(catalog.Entries[0].AccentClass, Is.EqualTo("sa-chapter--mars"));
            Assert.That(catalog.Entries[1].AccentClass, Is.EqualTo("sa-chapter--pioneer"));
            Assert.That(catalog.Entries[2].AccentClass, Is.EqualTo("sa-chapter--surveyor"));
        }

        [Test]
        public void MarsIsAvailableAndPointsToItsChapterAndTextTable()
        {
            var catalog = Load<UI.ChapterCatalog>(CatalogPath);
            var mars = catalog.Find("mars");
            Assert.That(mars.IsAvailable, Is.True);
            Assert.That(mars.Chapter.ChapterId, Is.EqualTo("mars"));
            Assert.That(mars.ChapterTexts != null, Is.True);
            Assert.That(catalog.FindByChapterId("mars"), Is.EqualTo(mars));
            Assert.That(catalog.FindByChapterId("jupiter"), Is.Null);
            Assert.That(catalog.Find("jupiter"), Is.Null);
        }

        [Test]
        public void CatalogCarriesNoHeavyAssetsOnlyLightReferences()
        {
            var catalog = Load<UI.ChapterCatalog>(CatalogPath);
            var serialized = new SerializedObject(catalog);
            var allowed = new HashSet<Type> { typeof(ChapterDefinition), typeof(TextAsset) };
            var entries = serialized.FindProperty("entries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var property = entries.GetArrayElementAtIndex(i).Copy();
                var end = property.GetEndProperty();
                var enterChildren = true;
                while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, end))
                {
                    enterChildren = false;
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                        continue;

                    Assert.That(allowed.Contains(property.objectReferenceValue.GetType()), Is.True,
                        $"{property.propertyPath} referencia {property.objectReferenceValue.GetType().Name}: só definição e textos entram no menu");
                }
            }
        }

        [Test]
        public void CatalogValidationCatchesBadEntries()
        {
            UI.ChapterCatalog Make(params UI.ChapterCatalog.Entry[] entries)
            {
                var catalog = UI.ChapterCatalog.Create(entries);
                created.Add(catalog);
                return catalog;
            }

            ChapterDefinition Chapter(string id)
            {
                var chapter = ChapterDefinition.Create(id, id + ".T", id + ".D", id + ".Q", "Scene", 360f, id + ".s", new[]
                {
                    new ChapterStep("A", false, "B"),
                    new ChapterStep("B", true),
                });
                created.Add(chapter);
                return chapter;
            }

            Assert.That(Make().Validate().Count, Is.GreaterThan(0), "catálogo vazio");
            Assert.That(Make(UI.ChapterCatalog.Entry.Create("a", null, "sa-chapter--a"), UI.ChapterCatalog.Entry.Create("a", null, "sa-chapter--b"))
                .Validate().Count, Is.GreaterThan(0), "slot duplicado");
            Assert.That(Make(UI.ChapterCatalog.Entry.Create("a", null, "mars")).Validate().Count, Is.GreaterThan(0), "classe sem prefixo");
            Assert.That(Make(UI.ChapterCatalog.Entry.Create("a", null, "sa-chapter--")).Validate().Count, Is.GreaterThan(0), "classe só com prefixo");
            Assert.That(Make(UI.ChapterCatalog.Entry.Create("1 inválido", null, "sa-chapter--a")).Validate().Count, Is.GreaterThan(0), "slot inválido");
            Assert.That(Make(UI.ChapterCatalog.Entry.Create("a", Chapter("b"), "sa-chapter--a")).Validate().Count, Is.GreaterThan(0), "chapterId diferente do slot");
            Assert.That(Make(UI.ChapterCatalog.Entry.Create("a", Chapter("a"), "sa-chapter--a")).Validate().Count, Is.EqualTo(0), "capítulo válido");
            Assert.That(Make(UI.ChapterCatalog.Entry.Create("a", null, "sa-chapter--a")).Validate().Count, Is.EqualTo(0), "capítulo ainda não integrado é permitido");
        }

        [Test]
        public void CoverRequiresACredit()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
            created.Add(texture);
            created.Add(sprite);

            var chapter = ChapterDefinition.Create("a", "a.T", "a.D", "a.Q", "Scene", 360f, "a.s", new[]
            {
                new ChapterStep("A", false, "B"),
                new ChapterStep("B", true),
            }, sprite);
            created.Add(chapter);

            var without = UI.ChapterCatalog.Create(new[] { UI.ChapterCatalog.Entry.Create("a", chapter, "sa-chapter--a") });
            var with = UI.ChapterCatalog.Create(new[]
            {
                UI.ChapterCatalog.Entry.Create("a", chapter, "sa-chapter--a", null, false, "NASA/JPL-Caltech"),
            });
            created.Add(without);
            created.Add(with);

            Assert.That(without.Validate().Count, Is.GreaterThan(0), "capa sem crédito");
            Assert.That(with.Validate().Count, Is.EqualTo(0));
        }

        // ---------- Textos PT/EN ----------

        [Test]
        public void EveryKeyHasPortugueseAndEnglishText()
        {
            var catalog = Load<UI.ChapterCatalog>(CatalogPath);
            var texts = new UI.DossierTextCatalog();
            UI.DossierTextTables.Load(Load<TextAsset>(TextsPath), texts);

            var slotIds = new List<string>();
            foreach (var entry in catalog.Entries)
            {
                slotIds.Add(entry.SlotId);
                UI.DossierTextTables.Load(entry.ChapterTexts, texts);
            }

            Assert.That(string.Join("\n", UI.DossierContentCheck.Validate(texts, slotIds)), Is.EqualTo(string.Empty));
        }

        [Test]
        public void MarsTitleDescriptionAndQuestionComeFromItsOwnTable()
        {
            var catalog = Load<UI.ChapterCatalog>(CatalogPath);
            var mars = catalog.Find("mars");
            var texts = new UI.DossierTextCatalog();
            UI.DossierTextTables.Load(mars.ChapterTexts, texts);

            Assert.That(texts.Get(mars.Chapter.TitleKey, "pt"), Is.EqualTo("Spirit & Opportunity"));
            Assert.That(texts.Get(mars.Chapter.QuestionKey, "pt"), Is.EqualTo("Marte já teve água líquida?"));
            Assert.That(texts.Get(mars.Chapter.QuestionKey, "en"), Is.EqualTo("Did Mars once have liquid water?"));
            Assert.That(texts.Get(mars.Chapter.DescriptionKey, "en"), Is.EqualTo("MARS · 2004–2018"));
        }

        [Test]
        public void MalformedJsonAddsNothingAndDoesNotThrow()
        {
            var texts = new UI.DossierTextCatalog();
            Assert.That(UI.DossierTextTables.Parse("{ isto não é json", texts, "teste"), Is.EqualTo(0));
            Assert.That(UI.DossierTextTables.Parse("", texts), Is.EqualTo(0));
            Assert.That(UI.DossierTextTables.Parse("{\"texts\":[{\"key\":\"a\",\"pt\":\"A\",\"en\":\"B\"}]}", texts), Is.EqualTo(1));
            Assert.That(texts.Get("a", "en"), Is.EqualTo("B"));
        }

        // ---------- UXML e prefab ----------

        [Test]
        public void ScreenUxmlHasEveryElementTheScreenRequires()
        {
            var container = Load<VisualTreeAsset>(ScreenUxmlPath).Instantiate();
            foreach (var name in ScreenElements)
                Assert.That(container.Q<VisualElement>(name) != null, Is.True, $"DossierScreen.uxml sem '{name}'");

            Assert.That(container.Q<ScrollView>("panel-body") != null, Is.True, "panel-body precisa ser ScrollView");
            Assert.That(container.Q<Button>("language-pt") != null, Is.True);
            Assert.That(container.Q<Button>("settings-button") != null, Is.True);
        }

        [Test]
        public void FolderUxmlHasEveryElementTheScreenRequires()
        {
            var container = Load<VisualTreeAsset>(FolderUxmlPath).Instantiate();
            foreach (var name in FolderElements)
                Assert.That(container.Q<VisualElement>(name) != null, Is.True, $"DossierFolder.uxml sem '{name}'");

            Assert.That(container.Q<Button>("folder-start") != null, Is.True);
            Assert.That(container.Q<Button>("folder-details") != null, Is.True);
        }

        [Test]
        public void PrefabIsWiredToTheCatalogTextsTemplateAndPanelSettings()
        {
            var prefab = Load<GameObject>(PrefabPath);
            var screen = prefab.GetComponent<UI.DossierScreen>();
            Assert.That(screen != null, Is.True, "DossierScreen no prefab");
            Assert.That(prefab.GetComponent<UIDocument>() != null, Is.True);
            Assert.That(prefab.GetComponent<UI.ResponsiveLayoutClass>() != null, Is.True, "variante compacta 4:3");

            var document = prefab.GetComponent<UIDocument>();
            Assert.That(document.visualTreeAsset, Is.EqualTo(Load<VisualTreeAsset>(ScreenUxmlPath)));
            Assert.That(document.panelSettings != null, Is.True);

            var serialized = new SerializedObject(screen);
            Assert.That(serialized.FindProperty("catalog").objectReferenceValue, Is.EqualTo(Load<UI.ChapterCatalog>(CatalogPath)));
            Assert.That(serialized.FindProperty("uiTexts").objectReferenceValue, Is.EqualTo(Load<TextAsset>(TextsPath)));
            Assert.That(serialized.FindProperty("folderTemplate").objectReferenceValue, Is.EqualTo(Load<VisualTreeAsset>(FolderUxmlPath)));
            Assert.That(serialized.FindProperty("document").objectReferenceValue, Is.EqualTo(document));
        }

        [Test]
        public void PrefabHasNoCameraAndNoAudioSoMenuAndChapterNeverDuplicateThem()
        {
            var prefab = Load<GameObject>(PrefabPath);
            Assert.That(prefab.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<AudioListener>(true).Length, Is.EqualTo(0));
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(0));
        }

        // ---------- Portão do menu e espelho de estados ----------

        [Test]
        public void MenuGateDisablesAndRestoresCameraListenerAndObjects()
        {
            var menu = new GameObject("menu-camera");
            var listener = menu.AddComponent<AudioListener>();
            var camera = menu.AddComponent<Camera>();
            var extra = new GameObject("menu-ambient");
            created.Add(menu);
            created.Add(extra);

            var gate = UI.MenuSceneGate.Create(new Behaviour[] { listener, camera }, new[] { extra });
            Assert.That(gate.Count, Is.EqualTo(3));

            gate.Apply(false);
            Assert.That(listener.enabled, Is.False);
            Assert.That(camera.enabled, Is.False);
            Assert.That(extra.activeSelf, Is.False);
            Assert.That(gate.IsActive, Is.False);

            gate.Apply(true);
            Assert.That(listener.enabled, Is.True);
            Assert.That(camera.enabled, Is.True);
            Assert.That(extra.activeSelf, Is.True);
        }

        [Test]
        public void ChapterRunningNeverLeavesTwoAudioListenersWhenMenuListenerIsGated()
        {
            var menu = new GameObject("menu-listener");
            var menuListener = menu.AddComponent<AudioListener>();
            var chapter = new GameObject("chapter-listener");
            chapter.AddComponent<AudioListener>();
            created.Add(menu);
            created.Add(chapter);

            var gate = UI.MenuSceneGate.Create(new Behaviour[] { menuListener }, new GameObject[0]);
            var before = UI.MenuSceneGate.CountEnabledAudioListeners();

            gate.Apply(false);
            Assert.That(UI.MenuSceneGate.CountEnabledAudioListeners(), Is.EqualTo(before - 1), "o do menu foi desligado");

            gate.Apply(true);
            Assert.That(UI.MenuSceneGate.CountEnabledAudioListeners(), Is.EqualTo(before));
        }

        [Test]
        public void DossierAppStateMirrorsAppStateNamesAndValues()
        {
            var app = Enum.GetNames(typeof(AppState));
            var mirror = Enum.GetNames(typeof(UI.DossierAppState));
            Assert.That(string.Join(",", mirror), Is.EqualTo(string.Join(",", app)));
            foreach (var name in app)
            {
                Assert.That((int)Enum.Parse(typeof(UI.DossierAppState), name), Is.EqualTo((int)Enum.Parse(typeof(AppState), name)), name);
            }
        }
    }
}
