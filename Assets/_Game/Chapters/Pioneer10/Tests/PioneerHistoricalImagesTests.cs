using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// PIO-007: fotos históricas da Pioneer 10 — origem, crédito e hash conferidos, versão Web de até 1024 px sem ampliação,
    /// importação correta, originais fora de Assets e o rótulo "Imagem real · Pioneer 10 · 1973 · NASA".
    /// </summary>
    public class PioneerHistoricalImagesTests
    {
        const string RecordPath = "Data/Assets/PioneerHistoricalImages.json";

        [System.Serializable] sealed class Record { public Entry[] images; }

        [System.Serializable]
        sealed class Entry
        {
            public string id, subjectEn, kind, mission, eventDate, creditLine, labelTextKey;
            public int year;
            public SourcePage sourcePage;
            public Original original;
            public Web web;
            public string[] usedIn, cueIds, factIds, caveats;
        }

        [System.Serializable] sealed class SourcePage { public string url, title, retrieved; }

        [System.Serializable] sealed class Original { public string url, file, path, sha256, format; public int bytes, width, height; }

        [System.Serializable] sealed class Web { public string path, sha256, format; public int bytes, width, height, quality; public float scale; public bool upscaled; public int[] crop; }

        [System.Serializable] sealed class Chapter { public Cue[] cues; public Fact[] facts; }

        [System.Serializable] sealed class Cue { public string id; }

        [System.Serializable] sealed class Fact { public string id; }

        [System.Serializable] sealed class Locale { public Entry2[] entries; }

        [System.Serializable] sealed class Entry2 { public string key, text; }

        static Record record;

        [SetUp]
        public void SetUp()
        {
            Assert.That(PioneerHistoricalImagesImporter.Configure(), Is.Empty);
            record = JsonUtility.FromJson<Record>(File.ReadAllText(RecordPath));
            Assert.That(record?.images, Is.Not.Null.And.Length.EqualTo(2), "registro de procedência ausente");
        }

        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
                return string.Concat(SHA256.Create().ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        [Test]
        public void EveryImageHasMissionYearCreditSourceAndTheDatesAreNotPublicationDates()
        {
            Assert.That(record.images.Select(i => i.id), Is.EqualTo(new[] { "PIO.Hist.Ganymede", "PIO.Hist.Jupiter" }));
            foreach (var image in record.images)
            {
                Assert.That(image.mission, Is.EqualTo("Pioneer 10"), image.id);
                Assert.That(image.year, Is.EqualTo(1973), image.id);
                Assert.That(image.eventDate, Does.StartWith("1973-12"), image.id + ": data do evento, não a de publicação da página");
                Assert.That(image.creditLine, Is.EqualTo("NASA"), image.id);
                Assert.That(image.sourcePage.url, Does.StartWith("https://science.nasa.gov/resource/pioneer-10-"), image.id);
                Assert.That(image.original.url, Does.StartWith("https://assets.science.nasa.gov/"), image.id);
                Assert.That(image.sourcePage.retrieved, Is.Not.Empty, image.id);
                Assert.That(image.caveats, Is.Not.Empty, image.id + ": limites declarados");
            }

            Assert.That(record.images.Single(i => i.id == "PIO.Hist.Jupiter").kind, Is.EqualTo("montage"), "a imagem de Júpiter é uma montagem e está registrada como tal");
        }

        [Test]
        public void OriginalsAreKeptOutsideAssetsWithTheRecordedHashAndSize()
        {
            foreach (var image in record.images)
            {
                Assert.That(image.original.path, Does.StartWith("ArtSource/"), image.id);
                Assert.That(File.Exists(image.original.path), Is.True, image.id + ": original preservado");
                Assert.That(new FileInfo(image.original.path).Length, Is.EqualTo(image.original.bytes), image.id);
                Assert.That(Hash(image.original.path), Is.EqualTo(image.original.sha256), image.id + ": SHA-256 do original");
            }

            var originalHashes = record.images.Select(i => i.original.sha256).ToHashSet();
            var leaked = Directory.GetFiles("Assets", "*.jpg", SearchOption.AllDirectories).Where(p => originalHashes.Contains(Hash(p))).ToList();
            Assert.That(leaked, Is.Empty, "nenhum original pesado dentro de Assets (e do build)");
        }

        [Test]
        public void WebVersionsAreSmallNeverUpscaledAndMatchTheRecord()
        {
            foreach (var image in record.images)
            {
                var path = image.web.path;
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(Hash(path), Is.EqualTo(image.web.sha256), image.id + ": SHA-256 da versão Web");
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path + " não importou como textura");
                Assert.That(texture.width, Is.EqualTo(image.web.width));
                Assert.That(texture.height, Is.EqualTo(image.web.height));
                Assert.That(texture.width, Is.LessThanOrEqualTo(1024), image.id + ": até 1024 px de largura");
                Assert.That(image.web.upscaled, Is.False);
                Assert.That(image.web.scale, Is.LessThanOrEqualTo(1f), "só redução");
                Assert.That(image.web.format, Is.EqualTo("JPEG"));

                var sourceWidth = image.web.crop != null && image.web.crop.Length == 4 ? image.web.crop[2] - image.web.crop[0] : image.original.width;
                Assert.That(texture.width, Is.LessThanOrEqualTo(sourceWidth), "a largura Web não passa da largura útil do original: nada foi ampliado");
                Assert.That(new FileInfo(path).Length, Is.LessThan(250 * 1024), "sem duplicatas pesadas na build");
            }

            var historical = Directory.GetFiles("Assets/_Game/Chapters/Pioneer10/Art/Historical").Where(p => !p.EndsWith(".meta")).ToList();
            Assert.That(historical.Select(p => p.Replace('\\', '/')), Is.EquivalentTo(record.images.Select(i => i.web.path)), "só as versões registradas ficam em Art/Historical");
        }

        [Test]
        public void TextureImportIsFlatSrgbMipmapsOffAndNotReadable()
        {
            foreach (var path in PioneerHistoricalImagesImporter.Files)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(PioneerHistoricalImagesImporter.IsConfigured(importer), Is.True, path);
                Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(1024));
            }
        }

        [Test]
        public void TheLabelNamesRealImagePioneer10Year1973AndNasaInBothLanguages()
        {
            foreach (var path in new[] { "Data/Localization/Pioneer10.pt-BR.json", "Data/Localization/Pioneer10.en.json" })
            {
                var entry = JsonUtility.FromJson<Locale>(File.ReadAllText(path)).entries.Single(e => e.key == "PIO.UI.real");
                Assert.That(entry.text, Does.Contain("Pioneer 10").And.Contain("1973").And.Contain("NASA"), path);
                Assert.That(entry.text, Does.StartWith("Imagem real").Or.StartWith("Actual image"), path);
            }

            Assert.That(record.images.All(i => i.labelTextKey == "PIO.UI.real"));
        }

        [Test]
        public void TheRecordPointsToRealCuesAndFactsOfTheChapterData()
        {
            var chapter = JsonUtility.FromJson<Chapter>(File.ReadAllText("Data/Chapters/Pioneer10.json"));
            foreach (var image in record.images)
            {
                Assert.That(image.usedIn, Is.EqualTo(new[] { "PIO.Imaging" }), image.id);
                foreach (var cue in image.cueIds)
                    Assert.That(chapter.cues.Any(c => c.id == cue), Is.True, $"{image.id}: cue {cue}");
                foreach (var fact in image.factIds)
                    Assert.That(chapter.facts.Any(f => f.id == fact), Is.True, $"{image.id}: fato {fact}");
            }
        }
    }
}
