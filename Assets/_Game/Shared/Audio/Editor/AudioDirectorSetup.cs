using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// AUD-002 · Cria o catálogo de cues (<c>Shared/Audio/AudioCatalog.asset</c>) e o prefab <c>Shared/Audio/AudioDirector.prefab</c> para a
    /// integração (INT-001). O catálogo traz os IDs da planilha de cues SEM arquivos: a AUD-001 só listou candidatos e nenhum
    /// áudio foi adquirido, então todos nascem pendentes (silêncio) e entram com clipes depois, uma linha "Importado" por arquivo.
    /// Idempotente. Menu: Abandoned Space Apps → AUD-002 → Criar catálogo e prefab.
    /// </summary>
    public static class AudioDirectorSetup
    {
        public const string Folder = "Assets/_Game/Shared/Audio";
        public const string CatalogPath = Folder + "/AudioCatalog.asset";
        public const string PrefabPath = Folder + "/AudioDirector.prefab";

        /// <summary>IDs da planilha de cues (Docs/Art/AUDIO_CUE_SHEET.md) que os capítulos já pedem, com bus e loop. Fonte: PIO-013 (mapa) e a planilha.</summary>
        public static List<AudioCueDefinition> PlannedCues() => new List<AudioCueDefinition>
        {
            // Pioneer 10 (roteiro novo): os clipes entram por PioneerMusicImporter (Tools/convert_pioneer10_music.py, CC BY 4.0).
            new AudioCueDefinition("MUS_PIO_LANCAMENTO", AudioBus.Music, loop: true),
            new AudioCueDefinition("MUS_PIO_CINTURAO", AudioBus.Music, loop: true),
            new AudioCueDefinition("MUS_PIO_MARAVILHA", AudioBus.Music, loop: true),
            new AudioCueDefinition("MUS_PIO_JUPITER", AudioBus.Music, loop: true),
            new AudioCueDefinition("MUS_PIO_ANOS", AudioBus.Music, loop: true),
            new AudioCueDefinition("MUS_PIO_RADIO", AudioBus.Music, loop: true),
            new AudioCueDefinition("MUS_PIO_FINAL", AudioBus.Music, loop: true),
            new AudioCueDefinition("SFX_PIO_LAUNCH", AudioBus.Sfx),
            new AudioCueDefinition("SFX_PIO_POWERDOWN", AudioBus.Sfx),
            new AudioCueDefinition("SFX_SCAN", AudioBus.Sfx, loop: true, maxSeconds: 40f),
            new AudioCueDefinition("SFX_PIO_RADIATION_NOISE", AudioBus.Sfx),
            new AudioCueDefinition("SFX_CMD_SENT", AudioBus.Ui),
            new AudioCueDefinition("SFX_PIO_DETECTOR_TICK", AudioBus.Ui, pitchVariation: 0.05f),
            new AudioCueDefinition("SFX_RADIO_RECEIVED", AudioBus.Sfx),
            new AudioCueDefinition("SFX_RADIO_NOISE", AudioBus.Sfx, loop: true, maxSeconds: 12f),
            new AudioCueDefinition("SFX_HOLD_START", AudioBus.Ui),
            new AudioCueDefinition("SFX_HOLD_LOOP", AudioBus.Sfx, loop: true, maxSeconds: 30f),
            new AudioCueDefinition("SFX_HOLD_DONE", AudioBus.Ui),
            new AudioCueDefinition("SFX_HOLD_RELEASE", AudioBus.Ui),
        };

        [MenuItem("Abandoned Space Apps/AUD-002/Criar catálogo e prefab")]
        public static void CreateFromMenu()
        {
            var errors = Create();
            if (errors.Count == 0)
                Debug.Log("[AUD-002] catálogo e prefab conformes.");
            else
                Debug.LogError("[AUD-002] problemas:\n- " + string.Join("\n- ", errors));
        }

        public static List<string> Create()
        {
            Directory.CreateDirectory(Folder);
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            // Só acrescenta os IDs que faltam: nunca apaga clipes que alguém já tenha ligado.
            var cues = new List<AudioCueDefinition>(catalog.Cues);
            var known = new HashSet<string>();
            foreach (var cue in cues)
                known.Add(cue.Id);
            foreach (var planned in PlannedCues())
            {
                if (known.Add(planned.Id))
                    cues.Add(planned);
            }

            if (cues.Count != catalog.Cues.Count)
            {
                catalog.Set(cues);
                EditorUtility.SetDirty(catalog);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var root = new GameObject("AudioDirector");
                var director = root.AddComponent<AudioDirector>();
                var so = new SerializedObject(director);
                so.FindProperty("catalog").objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            var result = new List<string>(catalog.Validate());
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (loaded == null || loaded.GetComponent<AudioDirector>() == null)
                result.Add("AudioDirector.prefab sem o componente.");
            return result;
        }
    }
}
