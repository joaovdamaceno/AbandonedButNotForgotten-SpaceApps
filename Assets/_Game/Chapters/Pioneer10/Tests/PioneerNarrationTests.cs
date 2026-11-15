using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// PIO-013: legendas e pedidos de áudio ligados aos estados. Usa a SequenceDirector real (relógio manual) com os cues
    /// que vêm de PioneerData; o áudio é um dublê que só registra pedidos (o AudioDirector, AUD-002, ainda não existe).
    /// </summary>
    public class PioneerNarrationTests
    {
        sealed class FakeAudio : IPioneerAudioPort
        {
            static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture); // o Editor pode estar em pt-BR
            public bool Available { get; set; } = true;
            public readonly List<string> Log = new List<string>();
            public void SetMusic(string cueId, float fadeSeconds) => Log.Add(Invariant($"music:{cueId}:{fadeSeconds:0.00}"));
            public void SetMusicLevel(float level, float fadeSeconds) => Log.Add(Invariant($"level:{level:0.00}:{fadeSeconds:0.00}"));
            public void PlayEffect(string cueId) => Log.Add("fx:" + cueId);
            public void Silence(float seconds) => Log.Add(Invariant($"silence:{seconds:0.0}"));
            public void SetPaused(bool paused) => Log.Add("paused:" + paused);
            public void StopAll(float fadeSeconds) => Log.Add(Invariant($"stop:{fadeSeconds:0.00}"));
        }

        readonly List<Object> created = new List<Object>();
        PioneerData data;
        PioneerCueMap map;
        SequenceDirector director;
        PioneerNarration narration;
        FakeAudio audio;
        List<string> texts;

        [SetUp]
        public void SetUp()
        {
            data = AssetDatabase.LoadAssetAtPath<PioneerData>(PioneerDataBuilder.DataAssetPath);
            map = AssetDatabase.LoadAssetAtPath<PioneerCueMap>(PioneerDataBuilder.CueMapAssetPath);
            Assert.That(data, Is.Not.Null, "PioneerData.asset ausente: rode o importador (PIO-013)");
            Assert.That(map, Is.Not.Null, "PioneerCueMap.asset ausente: rode o importador (PIO-013)");

            var host = Track(new GameObject("narration-host"));
            var cameraDirector = host.AddComponent<CameraDirector>();
            cameraDirector.GroundProbe = _ => null;
            cameraDirector.Initialize(Track(new GameObject("camera")).AddComponent<Camera>());
            director = host.AddComponent<SequenceDirector>();
            director.Initialize(cameraDirector, null, null);
            narration = host.AddComponent<PioneerNarration>();
            narration.Configure(data, map);
            audio = new FakeAudio();
            narration.Initialize(director, audio, "pt-BR");
            texts = new List<string>();
            narration.TextChanged += t => texts.Add(t);
        }

        [TearDown]
        public void TearDown()
        {
            narration.Cancel();
            if (director.IsPlaying)
                director.Cancel();
            foreach (var obj in created)
                Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        /// <summary>Sequência de teste: cues do estado vindos dos dados (janela ou gatilho) e eventos nomeados nos instantes pedidos.</summary>
        SequenceDefinition SequenceFor(string stateId, params (float time, string eventId)[] events)
        {
            data.TryGetState(stateId, out var state);
            var cues = data.CuesOf(stateId).Select(c => c.timing == PioneerCueTiming.Window
                ? new SubtitleCue(c.id, c.textKey, c.startSeconds, c.endSeconds)
                : SubtitleCue.Triggered(c.id, c.textKey, c.triggerId, c.minimumReadSeconds)).ToList();
            var keys = new[] { new ShotKeyframe(0f, Vector3.zero, Vector3.zero, 40f), new ShotKeyframe(state.nominalSeconds, Vector3.zero, Vector3.zero, 40f) };
            var shot = new ShotDefinition("test." + stateId.Replace("PIO.", string.Empty), state.nominalSeconds, CameraDirector.WorldRigId, keys,
                easing: ShotEasing.Linear, minGroundClearanceMeters: 0f, events: events.Select(e => new ShotEvent(e.time, e.eventId)));
            return Track(SequenceDefinition.Create("test." + stateId, new[] { shot }, cues));
        }

        void Run(float seconds, float dt = 1f / 30f)
        {
            for (var t = 0f; t < seconds - 1e-4f; t += dt)
                director.Tick(dt);
        }

        // ---------- Dados e mapa ----------

        [Test]
        public void TheDataAssetMatchesTheJsonAndEveryCueHasItsTextInBothLanguages()
        {
            Assert.That(data.Validate(), Is.Empty);
            Assert.That(data.States.Count, Is.EqualTo(9));
            Assert.That(data.Cues.Count, Is.EqualTo(53));
            Assert.That(data.Texts.Count, Is.EqualTo(147));
            Assert.That(data.States.Sum(s => s.nominalSeconds), Is.EqualTo(383));
            foreach (var cue in data.Cues)
            {
                Assert.That(data.GetText(cue.textKey, "pt-BR"), Is.Not.Empty, cue.id);
                Assert.That(data.GetText(cue.textKey, "en"), Is.Not.Empty, cue.id);
            }

            Assert.That(data.GetText("PIO.01.01", "pt"), Is.EqualTo("Pioneer quer dizer pioneira: quem vai primeiro. E o nome não é por acaso."));
            Assert.That(data.GetText("PIO.01.01", "en"), Is.EqualTo("Pioneer: a word for those who go first. And the name was no accident."));
            Assert.That(data.GetText("PIO.UI.dust", "pt"), Does.Contain("{count}"));
            Assert.That(data.GetText("PIO.chave.inexistente", "pt"), Is.Empty, "chave desconhecida: a caixa some");
            Assert.That(data.GetText("PIO.01.01", "fr"), Is.Empty, "idioma sem dados: a caixa some");
        }

        [Test]
        public void ImportingAgainLeavesBothAssetsIdenticalWithTheSameGuids()
        {
            var paths = new[] { PioneerDataBuilder.DataAssetPath, PioneerDataBuilder.CueMapAssetPath };
            var before = paths.ToDictionary(p => p, File.ReadAllBytes);
            var guids = paths.ToDictionary(p => p, AssetDatabase.AssetPathToGUID);

            Assert.That(PioneerDataBuilder.Import(), Is.Empty);

            foreach (var path in paths)
            {
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before[path]), path + " mudou ao reimportar");
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guids[path]));
            }
        }

        [Test]
        public void TheCueMapFollowsTheCueSheetWithoutAnyAudioFileOrUnlicensedClip()
        {
            Assert.That(map.Validate(), Is.Empty);
            Assert.That(PioneerPresentation.StateIds.Select(id => { map.TryGetState(id, out var s); return s.musicCueId; }), Is.EqualTo(new[]
            {
                "MUS_PIO_PARTIDA", "MUS_PIO_CUR", "MUS_PIO_CUR", "MUS_PIO_CUR", "MUS_PIO_CUR", "MUS_PIO_DISTANCIA", "MUS_PIO_DISTANCIA", "MUS_PIO_ESCUTA", "MUS_PIO_ESCUTA",
            }), "Partida, Curiosidade, Distância e Escuta nos estados da folha de cues");
            Assert.That(map.States.All(s => s.fadeSeconds >= 1.5f && s.fadeSeconds <= 2f), "crossfade de 1,5 a 2 s");
            Assert.That(map.Cues.All(c => c.clip == null), "o mapa não traz áudio: nenhuma faixa entra sem licença e crédito");
            Assert.That(map.Cues.Where(c => c.kind == PioneerAudioKind.Music).All(c => c.licenseStatus == "candidate"), "faixas são candidatas, não adquiridas");

            var sheet = File.ReadAllText("Docs/Art/AUDIO_CUE_SHEET.md");
            foreach (var cue in map.Cues.Where(c => c.idConfirmedInCueSheet))
                Assert.That(sheet, Does.Contain(cue.cueId), cue.cueId + " declarado como confirmado na folha de cues");

            foreach (var cue in map.Cues.Where(c => c.kind != PioneerAudioKind.Music))
            {
                Assert.That(cue.interfaceSound, Is.True, cue.cueId + ": no vácuo, som de interface");
                if (cue.loop)
                    Assert.That(cue.maxSeconds, Is.GreaterThan(0f).And.LessThanOrEqualTo(40f), cue.cueId + ": loop com duração máxima");
            }

            Assert.That(map.Events.Any(e => e.eventId == "PIO.Listening.Attempt4" && e.action == PioneerAudioAction.Silence && e.seconds == 2.5f), "silêncio real de 2,5 s");
        }

        [Test]
        public void EveryNonProposedAudioEventExistsInTheIntegratedSequences()
        {
            var integrated = new HashSet<string>();
            foreach (var name in new[] { "PIO_Launch", "PIO_Belt", "PIO_Imaging" })
            {
                var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>($"Assets/_Game/Chapters/Pioneer10/Sequences/{name}.asset");
                Assert.That(sequence, Is.Not.Null, name);
                foreach (var shot in sequence.Shots)
                    foreach (var shotEvent in shot.Events)
                        integrated.Add(shotEvent.eventId);
            }

            foreach (var audioEvent in map.Events.Where(e => !e.proposedEvent))
                Assert.That(integrated, Does.Contain(audioEvent.eventId), $"evento '{audioEvent.eventId}' não existe nas sequências integradas; marque como proposto");
            Assert.That(map.Events.Count(e => e.proposedEvent), Is.GreaterThan(0), "os eventos que ainda não existem estão declarados como propostos");
        }

        [Test]
        public void ReadingStretchIsOnlyUsedWhereTheScriptAllowsItAndNeverCutsAText()
        {
            foreach (var language in new[] { "pt", "en" })
            {
                foreach (var cue in data.Cues)
                {
                    var reading = data.ReadingSeconds(cue, language);
                    Assert.That(reading, Is.GreaterThanOrEqualTo(cue.NominalSeconds - 1e-4f), $"{cue.id} {language}");
                    if (data.NeedsStretch(cue, language))
                    {
                        Assert.That(cue.readingStretchMaxSeconds, Is.GreaterThan(cue.NominalSeconds), $"{cue.id} {language} precisa de mais tempo e o roteiro não permite alongar");
                        Assert.That(reading, Is.LessThanOrEqualTo(cue.readingStretchMaxSeconds), $"{cue.id} {language}");
                    }
                }
            }

            data.TryGetCue("PIO.07.06", out var silence);
            Assert.That(data.ReadingSeconds(silence, "pt"), Is.GreaterThan(4f).And.LessThanOrEqualTo(7f), "PIO.07.06 em PT precisa passar dos 4 s nominais");
            data.TryGetCue("PIO.04.09", out var light);
            Assert.That(data.ReadingSeconds(light, "pt"), Is.LessThanOrEqualTo(8f));
        }

        // ---------- Legendas ----------

        [Test]
        public void SubtitlesAppearInTheirWindowsHideInTheGapsAndNeverShowTwoAtOnce()
        {
            narration.EnterState("PIO.Launch");
            director.Play(SequenceFor("PIO.Launch"));

            Run(3f);
            Assert.That(narration.CurrentText, Is.EqualTo(data.GetText("PIO.01.01", "pt")));
            Run(4.5f); // 7,5 s: intervalo entre PIO.01.01 (0–7) e PIO.01.02 (8–16)
            Assert.That(narration.CurrentText, Is.Empty, "sem legenda a caixa some");
            Assert.That(narration.CurrentCue, Is.Null);
            Run(2f); // 9,5 s
            Assert.That(narration.CurrentText, Is.EqualTo(data.GetText("PIO.01.02", "pt")));
            Assert.That(texts.Where(t => t.Length > 0).Distinct().Count(), Is.EqualTo(2));

            Run(40f);
            Assert.That(narration.CurrentText, Is.Empty, "ao terminar a sequência não sobra legenda");
            Assert.That(texts.Count(t => t.Length > 0), Is.EqualTo(5), "as cinco legendas de PIO.Launch, uma vez cada");
        }

        [Test]
        public void SwitchingLanguageMidCueResolvesTheSameCueWithoutRestartingAnything()
        {
            narration.EnterState("PIO.Launch");
            director.Play(SequenceFor("PIO.Launch"));
            Run(2f);
            var cue = narration.CurrentCue;
            var time = director.TimeSeconds;
            audio.Log.Clear();

            narration.SetLanguage("en");

            Assert.That(narration.CurrentCue, Is.SameAs(cue));
            Assert.That(narration.CurrentText, Is.EqualTo(data.GetText("PIO.01.01", "en")));
            Assert.That(director.TimeSeconds, Is.EqualTo(time), "a sequência não reinicia");
            Assert.That(audio.Log, Is.Empty, "a música não é reiniciada");
            narration.SetLanguage("xx");
            Assert.That(narration.LanguageCode, Is.EqualTo("en"), "idioma desconhecido é ignorado");
        }

        [Test]
        public void TriggerSubtitlesOpenOnTheGameEventAndHoldTheirReadingTime()
        {
            narration.EnterState("PIO.Plaque");
            director.Play(SequenceFor("PIO.Plaque"));
            Run(1f);

            Assert.That(narration.RaiseTrigger("PIO.Plaque.Hydrogen"), Is.EqualTo(1));
            Assert.That(narration.CurrentText, Is.EqualTo(data.GetText("PIO.05.02", "pt")));
            Run(3f);
            Assert.That(narration.CurrentCue.CueId, Is.EqualTo("PIO.05.02"), "fica durante o tempo mínimo de leitura (4 s)");
            Run(1.5f);
            Assert.That(narration.CurrentText, Is.Not.EqualTo(data.GetText("PIO.05.02", "pt")), "depois do tempo mínimo a legenda sai");
        }

        // ---------- Música e efeitos ----------

        [Test]
        public void MusicIsRequestedOnlyWhenTheCueChangesAndTheLevelReturnsToOneOnTheNextState()
        {
            foreach (var state in PioneerPresentation.StateIds)
            {
                Assert.That(narration.EnterState(state), Is.True, state);
                if (state == "PIO.Belt")
                    narration.RaiseEvent("PIO.Belt.Reality");
            }

            Assert.That(audio.Log.Where(l => l.StartsWith("music:")), Is.EqualTo(new[]
            {
                "music:MUS_PIO_PARTIDA:1.75", "music:MUS_PIO_CUR:1.75", "music:MUS_PIO_DISTANCIA:1.75", "music:MUS_PIO_ESCUTA:1.75",
            }), "CUR segue de PIO.Belt a PIO.Imaging e DISTANCIA de PIO.Plaque a PIO.Outbound sem reiniciar");
            Assert.That(audio.Log.Where(l => l.StartsWith("level:")), Is.EqualTo(new[] { "level:0.15:1.50", "level:1.00:1.75" }), "quase silêncio na virada e volta no estado seguinte");
            Assert.That(narration.EnterState("PIO.Inexistente"), Is.False);
        }

        [Test]
        public void StateEventsRequestTheMappedEffectsAndTheFinalSilence()
        {
            narration.EnterState("PIO.Imaging");
            narration.RaiseEvent("PIO.Imaging.Sweep");
            narration.RaiseEvent("PIO.Imaging.Gate6");
            narration.RaiseEvent("PIO.Imaging.GateRecovery");
            narration.EnterState("PIO.Listening");
            narration.RaiseEvent("PIO.Listening.Tune");
            narration.RaiseEvent("PIO.Listening.Contact1");
            narration.RaiseEvent("PIO.Listening.Attempt4");
            narration.RaiseEvent("PIO.Evento.Desconhecido");

            Assert.That(audio.Log.Where(l => l.StartsWith("fx:") || l.StartsWith("silence:")), Is.EqualTo(new[]
            {
                "fx:SFX_SCAN", "fx:SFX_PIO_RADIATION_NOISE", "fx:SFX_CMD_SENT", "fx:SFX_RADIO_NOISE", "fx:SFX_RADIO_RECEIVED", "silence:2.5",
            }));
        }

        // ---------- Pausa, cancelamento e áudio indisponível ----------

        [Test]
        public void PausingForwardsOnceAndCancelingStopsEverythingWithoutLeakingIntoTheArchive()
        {
            narration.EnterState("PIO.Launch");
            director.Play(SequenceFor("PIO.Launch"));
            Run(3f);
            audio.Log.Clear();

            narration.SetPaused(true);
            narration.SetPaused(true);
            narration.SetPaused(false);
            Assert.That(audio.Log, Is.EqualTo(new[] { "paused:True", "paused:False" }), "cada mudança de pausa é pedida uma vez");

            audio.Log.Clear();
            texts.Clear();
            narration.Cancel();
            director.Cancel();

            Assert.That(audio.Log, Is.EqualTo(new[] { "stop:0.50" }), "música, efeitos e ambiente encerrados");
            Assert.That(narration.CurrentText, Is.Empty);
            Assert.That(texts.Last(), Is.Empty, "a caixa é avisada de que não há texto");
            Assert.That(narration.IsActive, Is.False);
            Assert.That(narration.CurrentMusicCue, Is.Null);

            narration.RaiseEvent("PIO.Launch.Ignition");
            narration.RaiseEvent("PIO.Listening.Attempt4");
            Assert.That(narration.RaiseTrigger("PIO.Plaque.Hydrogen"), Is.Zero);
            Assert.That(audio.Log, Is.EqualTo(new[] { "stop:0.50" }), "eventos depois do cancelamento são ignorados");

            narration.Cancel();
            Assert.That(audio.Log.Count, Is.EqualTo(1), "cancelar de novo não pede nada");

            audio.Log.Clear();
            Assert.That(narration.EnterState("PIO.Launch"), Is.True, "reiniciar o capítulo começa limpo");
            Assert.That(audio.Log, Is.EqualTo(new[] { "music:MUS_PIO_PARTIDA:1.75" }));
        }

        [Test]
        public void SubtitlesWorkWithoutSoundAndTheClickThatUnlocksAudioResyncsTheMusic()
        {
            audio.Available = false; // Web: áudio bloqueado até o clique inicial
            Assert.That(narration.EnterState("PIO.Belt"), Is.True);
            narration.RaiseEvent("PIO.Belt.Reality");
            director.Play(SequenceFor("PIO.Belt"));
            Run(3f);

            Assert.That(narration.CurrentText, Is.EqualTo(data.GetText("PIO.02.01", "pt")), "legendas funcionam sem som");
            Assert.That(audio.Log, Is.Empty, "nada é pedido a um áudio bloqueado");

            audio.Available = true;
            narration.Resync();
            Assert.That(audio.Log, Is.EqualTo(new[] { "music:MUS_PIO_CUR:1.75", "level:0.15:1.75", "paused:False" }), "depois do clique, o estado atual é refeito");
        }

        [Test]
        public void WithoutAnyAudioPortTheChapterStillNarrates()
        {
            narration.Initialize(director, null, "en");
            narration.TextChanged += t => texts.Add(t);
            Assert.That(narration.EnterState("PIO.Launch"), Is.True);
            director.Play(SequenceFor("PIO.Launch"));
            Run(2f);
            Assert.That(narration.CurrentText, Is.EqualTo(data.GetText("PIO.01.01", "en")));
            Assert.DoesNotThrow(() => { narration.SetPaused(true); narration.Cancel(); });
        }
    }
}
