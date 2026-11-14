using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-013 · Importador de Editor: converte <c>Data/Chapters/Pioneer10.json</c> e <c>Data/Localization/Pioneer10.*.json</c>
    /// (PIO-001) em <c>Data/PioneerData.asset</c> e grava <c>Audio/PioneerCueMap.asset</c> a partir da folha de cues
    /// (Docs/Art/AUDIO_CUE_SHEET.md). Determinístico: rodar de novo mantém GUIDs e deixa os arquivos idênticos.
    /// Menu: Abandoned Space Apps → PIO-013 → Importar dados e mapa de cues. Linha de comando: -executeMethod
    /// Abandoned.SpaceApps.EditorTools.PioneerDataBuilder.ImportFromCommandLine
    /// </summary>
    public static class PioneerDataBuilder
    {
        public const string ChapterJson = "Data/Chapters/Pioneer10.json";
        public const string PortugueseJson = "Data/Localization/Pioneer10.pt-BR.json";
        public const string EnglishJson = "Data/Localization/Pioneer10.en.json";
        public const string DataAssetPath = "Assets/_Game/Chapters/Pioneer10/Data/PioneerData.asset";
        public const string CueMapAssetPath = "Assets/_Game/Chapters/Pioneer10/Audio/PioneerCueMap.asset";

        // ---------- DTOs dos JSON (JsonUtility ignora os campos que não declaramos) ----------

        [Serializable] sealed class ChapterFile { public StateJson[] states; public CueJson[] cues; }

        [Serializable] sealed class StateJson { public string id; public int order; public int nominalSeconds; }

        [Serializable]
        sealed class CueJson
        {
            public string id;
            public string state;
            public string textKey;
            public TimingJson timing;
            public GateJson gate;
            public float readingStretchMaxSeconds;
        }

        [Serializable] sealed class TimingJson { public string kind; public float startSeconds; public float endSeconds; public string triggerId; public float minimumReadSeconds; }

        [Serializable] sealed class GateJson { public string id; }

        [Serializable] sealed class LocaleFile { public EntryJson[] entries; }

        [Serializable] sealed class EntryJson { public string key; public string group; public string status; public string text; }

        [MenuItem("Abandoned Space Apps/PIO-013/Importar dados e mapa de cues")]
        public static void ImportFromMenu()
        {
            var errors = Import();
            if (errors.Count == 0)
                Debug.Log("[PIO-013] PioneerData e PioneerCueMap atualizados e conformes.");
            else
                Debug.LogError("[PIO-013] problemas:\n- " + string.Join("\n- ", errors));
        }

        public static void ImportFromCommandLine()
        {
            var errors = new List<string>();
            try
            {
                errors = Import();
            }
            catch (Exception exception)
            {
                errors.Add(exception.ToString());
            }

            if (errors.Count > 0)
                Debug.LogError("[PIO-013] problemas:\n- " + string.Join("\n- ", errors));
            else
                Debug.Log("[PIO-013] PioneerData e PioneerCueMap atualizados e conformes.");
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }

        /// <summary>Importa e grava os dois assets. Devolve os erros de validação (vazio = conforme).</summary>
        public static List<string> Import()
        {
            var data = BuildData();
            var map = BuildCueMap();
            var errors = data.Validate().Concat(map.Validate()).ToList();
            if (errors.Count > 0)
                return errors; // nada é gravado com dados inválidos

            Write(data, DataAssetPath);
            Write(map, CueMapAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return errors;
        }

        static void Write<T>(T fresh, string path) where T : ScriptableObject
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            fresh.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return;
            }

            EditorUtility.CopySerialized(fresh, existing);
            existing.name = fresh.name;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(fresh);
        }

        // ---------- Dados do capítulo ----------

        public static PioneerData BuildData()
        {
            var chapter = JsonUtility.FromJson<ChapterFile>(File.ReadAllText(ChapterJson));
            var portuguese = JsonUtility.FromJson<LocaleFile>(File.ReadAllText(PortugueseJson));
            var english = JsonUtility.FromJson<LocaleFile>(File.ReadAllText(EnglishJson));
            if (chapter?.states == null || chapter.cues == null || portuguese?.entries == null || english?.entries == null)
                throw new InvalidOperationException("Dados da Pioneer ilegíveis em Data/: rode Tools/validate_pioneer_data.py.");

            var states = chapter.states.Select(s => new PioneerStateData { id = s.id, order = s.order, nominalSeconds = s.nominalSeconds });
            var cues = chapter.cues.Select(c => new PioneerCueData
            {
                id = c.id,
                state = c.state,
                textKey = c.textKey,
                timing = c.timing.kind == "trigger" ? PioneerCueTiming.Trigger : PioneerCueTiming.Window,
                startSeconds = c.timing.startSeconds,
                endSeconds = c.timing.endSeconds,
                triggerId = c.timing.triggerId ?? string.Empty,
                minimumReadSeconds = c.timing.minimumReadSeconds,
                gateId = c.gate?.id ?? string.Empty,
                readingStretchMaxSeconds = c.readingStretchMaxSeconds,
            });

            var englishByKey = english.entries.ToDictionary(e => e.key);
            var texts = portuguese.entries.Select(p => new PioneerTextData
            {
                key = p.key,
                group = p.group,
                ptText = p.text,
                enText = englishByKey.TryGetValue(p.key, out var e) ? e.text : string.Empty,
                ptStatus = p.status,
                enStatus = e?.status ?? string.Empty,
            });
            return PioneerData.Create(states, cues, texts);
        }

        // ---------- Mapa de cues (folha de cues, AUD-001) ----------

        static PioneerAudioCue Music(string id, string family) =>
            new PioneerAudioCue { cueId = id, kind = PioneerAudioKind.Music, family = family, idConfirmedInCueSheet = true, licenseStatus = "candidate" };

        static PioneerAudioCue Effect(string id, float maxSeconds, bool confirmed, bool loop = false, PioneerAudioKind kind = PioneerAudioKind.Effect) =>
            new PioneerAudioCue { cueId = id, kind = kind, loop = loop, maxSeconds = maxSeconds, idConfirmedInCueSheet = confirmed, licenseStatus = "pending" };

        static PioneerAudioEvent Fx(string eventId, string cueId, bool proposed, string note) =>
            new PioneerAudioEvent { eventId = eventId, action = PioneerAudioAction.Effect, cueId = cueId, proposedEvent = proposed, note = note };

        public static PioneerCueMap BuildCueMap()
        {
            const float fade = 1.75f; // dentro do 1,5–2 s da folha de cues
            var states = new[]
            {
                new PioneerStateAudio { stateId = "PIO.Launch", musicCueId = "MUS_PIO_PARTIDA", fadeSeconds = fade, note = "VIA: pulso e crescimento; no espaço só música." },
                new PioneerStateAudio { stateId = "PIO.Belt", musicCueId = "MUS_PIO_CUR", fadeSeconds = fade, note = "CUR, variação lúdica na fantasia." },
                new PioneerStateAudio { stateId = "PIO.JupiterApproach", musicCueId = "MUS_PIO_CUR", fadeSeconds = fade, note = "CUR cresce com a escala; mesmo cue do estado anterior, sem reiniciar." },
                new PioneerStateAudio { stateId = "PIO.Imaging", musicCueId = "MUS_PIO_CUR", fadeSeconds = fade },
                new PioneerStateAudio { stateId = "PIO.Plaque", musicCueId = "MUS_PIO_DISTANCIA", fadeSeconds = fade, note = "Delicada; o pulso sai em 38–47 s." },
                new PioneerStateAudio { stateId = "PIO.Outbound", musicCueId = "MUS_PIO_DISTANCIA", fadeSeconds = fade },
                new PioneerStateAudio { stateId = "PIO.Listening", musicCueId = "MUS_PIO_ESCUTA", fadeSeconds = fade, note = "LEG: piano e cordas com silêncio estruturado." },
                new PioneerStateAudio { stateId = "PIO.Farewell", musicCueId = "MUS_PIO_ESCUTA", fadeSeconds = fade, note = "O tema resolve; nenhum efeito externo." },
            };

            var cues = new[]
            {
                Music("MUS_PIO_PARTIDA", "VIA"),
                Music("MUS_PIO_CUR", "CUR"),
                Music("MUS_PIO_DISTANCIA", "VIA/LEG"),
                Music("MUS_PIO_ESCUTA", "LEG"),
                Effect("SFX_PIO_LAUNCH", 6f, false),
                Effect("SFX_SCAN", 40f, true, loop: true),
                Effect("SFX_PIO_RADIATION_NOISE", 2f, false),
                Effect("SFX_CMD_SENT", 3f, true),
                Effect("SFX_PIO_DETECTOR_TICK", 1f, false),
                Effect("SFX_RADIO_RECEIVED", 4f, false),
                Effect("SFX_RADIO_NOISE", 12f, false, loop: true, kind: PioneerAudioKind.Ambience),
            };

            var events = new[]
            {
                Fx("PIO.Launch.Ignition", "SFX_PIO_LAUNCH", false, "SFX de lançamento na Terra; no espaço só música."),
                new PioneerAudioEvent { eventId = "PIO.Belt.Reality", action = PioneerAudioAction.MusicLevel, level = 0.15f, seconds = 1.5f, note = "A música quase silencia na virada (1,5 s). O nível 0,15 é escolha desta tarefa para \"quase silencia\"; volta a 1 ao trocar de estado." },
                Fx("PIO.Belt.Recorded", "SFX_PIO_DETECTOR_TICK", true, "Tique de detector por registro. Hoje é o evento C# BeltInvestigation.Recorded; PIO-014 o liga a este ID."),
                Fx("PIO.Imaging.Sweep", "SFX_SCAN", false, "Varredura em loop modulado, limitada a 40 s."),
                Fx("PIO.Imaging.Gate6", "SFX_PIO_RADIATION_NOISE", false, "Ruído de radiação curto e de baixa amplitude, só no quadro do instrumento."),
                Fx("PIO.Imaging.GateRecovery", "SFX_CMD_SENT", false, "Envio do comando de recuperação."),
                Fx("PIO.Listening.Tune", "SFX_RADIO_NOISE", true, "Ruído de rádio de banda limitada e duração limitada (12 s); é interface, não som espacial."),
                Fx("PIO.Listening.Contact1", "SFX_RADIO_RECEIVED", true, "Acorde de sinal recebido (1 de 3)."),
                Fx("PIO.Listening.Contact2", "SFX_RADIO_RECEIVED", true, "Acorde de sinal recebido (2 de 3)."),
                Fx("PIO.Listening.Contact3", "SFX_RADIO_RECEIVED", true, "Acorde de sinal recebido (3 de 3), mais fraco."),
                new PioneerAudioEvent { eventId = "PIO.Listening.Attempt4", action = PioneerAudioAction.Silence, seconds = PioneerCueMap.ListeningSilenceSeconds, proposedEvent = true, note = "Tentativa 4 sem resposta: silêncio real de 2,5 s, sem hiss em loop." },
            };

            return PioneerCueMap.Create(0.5f, states, events, cues);
        }
    }
}
