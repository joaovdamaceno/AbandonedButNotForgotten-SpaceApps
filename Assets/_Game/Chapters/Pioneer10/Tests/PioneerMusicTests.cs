using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Música da Pioneer 10 (Scott Buckley, CC BY 4.0): arquivos, registro de procedência, crédito obrigatório, importação, ligação ao
    /// catálogo do AudioDirector e ao mapa de cues, e o orçamento de tamanho. Nenhum teste "ouve" a música: só a estrutura.
    /// </summary>
    public class PioneerMusicTests
    {
        [System.Serializable] sealed class Registry { public Track[] tracks; }

        [System.Serializable]
        sealed class Track
        {
            public string cue, file, title, credit, license, licenseUrl, oggSha256, originalSha256, url, page;
            public float start, duration, volume;
            public int oggBytes;
        }

        static Registry registry;

        [SetUp]
        public void SetUp() => registry = JsonUtility.FromJson<Registry>(File.ReadAllText(PioneerMusicImporter.RegistryPath));

        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
                return string.Concat(System.Security.Cryptography.SHA256.Create().ComputeHash(stream).Select(b => b.ToString("x2")));
        }

        [Test]
        public void TheSevenTracksExistMatchTheRegistryAndAreAllCcByWithAnOriginalHash()
        {
            Assert.That(registry.tracks.Length, Is.EqualTo(7));
            foreach (var track in registry.tracks)
            {
                var path = PioneerMusicImporter.Folder + "/" + track.file;
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(new FileInfo(path).Length, Is.EqualTo(track.oggBytes), track.cue);
                Assert.That(Hash(path), Is.EqualTo(track.oggSha256), track.cue + ": SHA-256 do .ogg");
                Assert.That(track.license, Is.EqualTo("CC BY 4.0"), track.cue);
                Assert.That(track.originalSha256, Has.Length.EqualTo(64), track.cue + ": hash do original");
                Assert.That(track.page, Does.StartWith("https://").And.Contain("scottbuckley.com.au"), track.cue);
                Assert.That(track.credit, Does.Contain("Scott Buckley").And.Contain("CC-BY 4.0").And.Contain("www.scottbuckley.com.au").And.Contain(track.title), track.cue + ": linha de crédito exigida pela fonte");
            }

            Assert.That(registry.tracks.Select(t => t.cue).Distinct().Count(), Is.EqualTo(7));
        }

        [Test]
        public void EveryTrackIsCreditedInTheAssetCreditsAndTheGameCreditsInBothLanguages()
        {
            var credits = File.ReadAllText("ASSET_CREDITS.md");
            foreach (var track in registry.tracks)
            {
                Assert.That(credits, Does.Contain(track.originalSha256), track.cue + ": ASSET_CREDITS registra o hash do original");
                Assert.That(credits, Does.Contain(track.credit), track.cue + ": ASSET_CREDITS traz a atribuição exata");
            }

            var dossier = File.ReadAllText("Assets/_Game/UI/Dossier/DossierTexts.json");
            Assert.That(dossier, Does.Contain("credits.line.pioneer_music"), "a atribuição obrigatória está nos créditos do jogo");
            foreach (var track in registry.tracks)
                Assert.That(dossier, Does.Contain(track.title), "o título aparece nos créditos do jogo");
            Assert.That(dossier, Does.Contain("Scott Buckley").And.Contain("CC BY 4.0"));
        }

        [Test]
        public void TheClipsImportAsStreamedVorbisAndFitTheBudget()
        {
            var total = 0L;
            foreach (var track in registry.tracks)
            {
                var path = PioneerMusicImporter.Folder + "/" + track.file;
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis), track.cue);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), track.cue + ": música longa não fica descomprimida na memória");
                Assert.That(importer.forceToMono, Is.False, track.cue);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip.length, Is.EqualTo(track.duration).Within(1f), track.cue + ": duração do trecho");
                Assert.That(clip.length, Is.GreaterThanOrEqualTo(AudioCueDefinition.MinMusicLoopClipSeconds), track.cue + ": cama de música tem pelo menos 30 s");
                total += track.oggBytes;
            }

            Assert.That(total, Is.LessThan(5L * 1024 * 1024), "a música da Pioneer cabe em 5 MB do orçamento de 10 MB de todo o áudio do jogo");
        }

        [Test]
        public void TheAudioCatalogHasTheSevenMusicCuesWithTheirClipsAndNoObsoleteOnes()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioDirectorSetup.CatalogPath);
            Assert.That(catalog.Validate(), Is.Empty);
            foreach (var track in registry.tracks)
            {
                Assert.That(catalog.TryGet(track.cue, out var cue), Is.True, track.cue);
                Assert.That(cue.Bus, Is.EqualTo(AudioBus.Music));
                Assert.That(cue.Loop, Is.True);
                Assert.That(cue.IsPending, Is.False, track.cue + " tem clipe");
                Assert.That(cue.Clips[0].name, Is.EqualTo(Path.GetFileNameWithoutExtension(track.file)));
                Assert.That(cue.Volume, Is.EqualTo(track.volume).Within(0.001f));
            }

            foreach (var obsolete in new[] { "MUS_PIO_PARTIDA", "MUS_PIO_CUR", "MUS_PIO_DISTANCIA", "MUS_PIO_ESCUTA" })
                Assert.That(catalog.Contains(obsolete), Is.False, obsolete + " saiu do catálogo");
            Assert.That(AudioDirectorSetup.PlannedCues().Select(c => c.Id), Is.SupersetOf(registry.tracks.Select(t => t.cue)));
        }

        [Test]
        public void EveryMusicCueOfTheStateMapIsAPlayableClipOfTheCatalog()
        {
            var map = AssetDatabase.LoadAssetAtPath<PioneerCueMap>("Assets/_Game/Chapters/Pioneer10/Audio/PioneerCueMap.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioDirectorSetup.CatalogPath);
            Assert.That(map.Validate(), Is.Empty);
            foreach (var state in PioneerPresentation.StateIds)
            {
                Assert.That(map.TryGetState(state, out var audio), Is.True, state);
                Assert.That(catalog.TryGet(audio.musicCueId, out var cue) && !cue.IsPending, Is.True, state + " → " + audio.musicCueId + " toca um clipe real");
            }

            var used = PioneerPresentation.StateIds.Select(s => { map.TryGetState(s, out var a); return a.musicCueId; }).Distinct().ToList();
            Assert.That(used.Count, Is.EqualTo(7), "um cue por clima; Júpiter/foto e placa/recuo dividem o mesmo sem reiniciar");
            Assert.That(map.Cues.Where(c => c.kind == PioneerAudioKind.Music).Select(c => c.creditId).OrderBy(x => x), Is.EqualTo(Enumerable.Range(1, 7).Select(i => $"M-SB-PIO-{i:00}")));
        }
    }
}
