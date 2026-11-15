using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Música da Pioneer 10 · Configura a importação dos Ogg Vorbis de <c>Audio/Music/</c> (gerados por
    /// <c>Tools/convert_pioneer10_music.py</c>, faixas de Scott Buckley, CC BY 4.0) e liga cada um ao seu cue no
    /// <see cref="AudioCatalog"/> do <see cref="AudioDirector"/>. Idempotente. Troca os quatro cues antigos SEM arquivo
    /// (<c>MUS_PIO_PARTIDA/CUR/DISTANCIA/ESCUTA</c>) pelos sete do roteiro novo e preserva os outros cues do catálogo.
    /// Menu: Abandoned Space Apps → PIO → Importar música da Pioneer.
    /// </summary>
    public static class PioneerMusicImporter
    {
        public const string Folder = "Assets/_Game/Chapters/Pioneer10/Audio/Music";
        public const string RegistryPath = "Data/Assets/PioneerMusic.json";

        /// <summary>Cues do mapa antigo (sem clipe): saem do catálogo se ainda estiverem pendentes.</summary>
        static readonly string[] ObsoleteCues = { "MUS_PIO_PARTIDA", "MUS_PIO_CUR", "MUS_PIO_DISTANCIA", "MUS_PIO_ESCUTA" };

        [System.Serializable] sealed class Registry { public Track[] tracks; }

        [System.Serializable]
        sealed class Track
        {
            public string cue, file, title, credit, use, oggSha256;
            public float volume, duration, start;
            public int oggBytes;
        }

        [MenuItem("Abandoned Space Apps/PIO/Importar música da Pioneer")]
        public static void ImportFromMenu()
        {
            var errors = Import();
            if (errors.Count == 0)
                Debug.Log("[PIO] música importada e ligada ao catálogo do AudioDirector.");
            else
                Debug.LogError("[PIO] problemas:\n- " + string.Join("\n- ", errors));
        }

        public static List<string> Import()
        {
            var errors = new List<string>();
            var registry = JsonUtility.FromJson<Registry>(File.ReadAllText(RegistryPath));
            if (registry?.tracks == null || registry.tracks.Length == 0)
            {
                errors.Add(RegistryPath + " sem faixas (rode Tools/convert_pioneer10_music.py).");
                return errors;
            }

            foreach (var track in registry.tracks)
            {
                var path = Folder + "/" + track.file;
                if (!File.Exists(path))
                {
                    errors.Add("arquivo ausente: " + path);
                    continue;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;          // música longa: não ocupa memória descomprimida (no WebGL cai em Compressed In Memory)
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;                                   // o .ogg já está no bitrate final; reencodar mais forte só piora
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                settings.preloadAudioData = false;                         // só carrega quando o cue pede
                importer.defaultSampleSettings = settings;
                importer.forceToMono = false;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }

            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioDirectorSetup.CatalogPath);
            if (catalog == null)
            {
                errors.AddRange(AudioDirectorSetup.Create());
                catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(AudioDirectorSetup.CatalogPath);
            }

            var wanted = registry.tracks.Select(t => t.cue).ToHashSet();
            var cues = new List<AudioCueDefinition>();
            foreach (var cue in catalog.Cues)
            {
                if (cue == null)
                    continue;
                var obsolete = System.Array.IndexOf(ObsoleteCues, cue.Id) >= 0 && cue.IsPending;
                if (!obsolete && !wanted.Contains(cue.Id))
                    cues.Add(cue);
            }

            foreach (var track in registry.tracks)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + track.file);
                if (clip == null)
                {
                    errors.Add("clipe não importou: " + track.file);
                    continue;
                }

                cues.Add(new AudioCueDefinition(track.cue, AudioBus.Music, loop: true, volume: track.volume, clips: clip));
            }

            catalog.Set(cues);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            errors.AddRange(catalog.Validate());
            return errors;
        }
    }
}
