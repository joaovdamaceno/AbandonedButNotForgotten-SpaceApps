using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Abandoned.SpaceApps.MarsMemorialChoreography;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-019: memorial (M31 30 s + M32 4 s). Prova blocos de 6 s, mecanismos que param antes do HUD sumir, memória escolhida é a mostrada (com
    /// fallback sem exceção), uma região por vez, planos que não repetem o anterior com zoom, nada de retorno a Marte depois do título,
    /// nenhuma fala atribuída ao rover e controles globais só depois dos 4 s finais.
    /// </summary>
    public class MarsMemorialTests
    {
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Art/Memorial/MarsMemorialTexts.json";
        const float Step = 0.05f;

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        GameObject root;
        MarsMemorialDirector director;
        SequenceDefinition m31, m32;
        CameraDirector cameraDirector;
        SequenceDirector sequenceDirector;
        Camera mainCamera;
        Dictionary<string, string> texts;

        [Serializable] sealed class Table { public List<Entry> texts = new List<Entry>(); }
        [Serializable] sealed class Entry { public string key; public string pt; public string en; }

        [SetUp]
        public void SetUp()
        {
            root = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MarsMemorialBuilder.PrefabPath)));
            director = root.GetComponent<MarsMemorialDirector>();
            m31 = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MarsMemorialBuilder.MainSequencePath);
            m32 = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MarsMemorialBuilder.TitleSequencePath);
            var host = Track(new GameObject("directors"));
            mainCamera = Track(new GameObject("camera")).AddComponent<Camera>();
            cameraDirector = host.AddComponent<CameraDirector>();
            cameraDirector.GroundProbe = _ => null;
            cameraDirector.Initialize(mainCamera);
            sequenceDirector = host.AddComponent<SequenceDirector>();
            sequenceDirector.Initialize(cameraDirector, null, null);
            director.Initialize(sequenceDirector, cameraDirector);
            texts = new Dictionary<string, string>();
            var en = new Dictionary<string, string>();
            foreach (var path in new[] { TextsPath, SubtitlesPath })
                foreach (var e in JsonUtility.FromJson<Table>(File.ReadAllText(path)).texts)
                {
                    texts[e.key] = e.pt;
                    en[e.key] = e.en;
                }

            Assert.That(texts.Keys.Where(k => k.StartsWith("MAR.MEM.")).All(k => !string.IsNullOrEmpty(en[k])), Is.True, "todo texto do memorial tem EN");
        }

        [TearDown]
        public void TearDown()
        {
            sequenceDirector.Cancel();
            foreach (var o in created)
                if (o != null)
                    UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        T Track<T>(T o) where T : UnityEngine.Object
        {
            created.Add(o);
            return o;
        }

        static IEnumerable<float> Times(float a, float b, float step)
        {
            for (var t = a; t <= b + 1e-4f; t += step)
                yield return t;
        }

        static Sprite SpriteOf(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        // ---------- Estrutura ----------

        [Test]
        public void ThirtyPlusFourSecondsInSixSecondBlocksWithCuesFromTheTable()
        {
            Assert.That(m31.TotalDurationSeconds, Is.EqualTo(30f).Within(1e-4f));
            Assert.That(m32.TotalDurationSeconds, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(TotalSeconds, Is.EqualTo(34f));
            Assert.That(m31.Shots.Count, Is.EqualTo(5));
            Assert.That(m31.Shots.Select(s => s.DurationSeconds), Is.All.EqualTo(6f).Within(1e-4f), "blocos de 6 s");
            Assert.That(m32.Shots.Count, Is.EqualTo(1));
            Assert.That(m31.SequenceId, Is.EqualTo("mars.seq.M31"));
            Assert.That(m32.SequenceId, Is.EqualTo("mars.seq.M32"));
            Assert.That(MarsChapterFlow.CreateDefaultBindings().Where(b => b.IsCinematic).Select(b => b.SequenceId), Is.SupersetOf(new[] { m31.SequenceId, m32.SequenceId }));

            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            Assert.That(m31.Cues.Select(c => c.CueId).OrderBy(x => x), Is.EqualTo(MarsChapterFlow.BuildSequenceCues(table, "M31").Select(c => c.CueId).OrderBy(x => x)));
            Assert.That(m31.Cues.Count, Is.EqualTo(5));
            Assert.That(m32.Cues, Is.Empty, "o título é texto de tela, não legenda");
            foreach (var s in new[] { m31, m32 })
            {
                Assert.That(s.Validate(), Is.Empty, ContractValidation.Describe(s.Validate()));
                Assert.That(sequenceDirector.Validate(s), Is.Empty);
            }

            Assert.That(director.ValidateSetup(), Is.Empty, ContractValidation.Describe(director.ValidateSetup()));
        }

        // ---------- Mecanismos ----------

        [Test]
        public void MechanismsSlowDownAndStopBeforeTheHudIsRemovedAndNeverMoveAgain()
        {
            Assert.That(MechanismsStopSeconds, Is.LessThan(HudHideSeconds), "param antes de o HUD ser retirado");
            var previousSpeed = float.MaxValue;
            foreach (var t in Times(0f, MechanismsStopSeconds, Step))
            {
                var m = MechanismsAt(t);
                Assert.That(m.WheelSpeed, Is.LessThanOrEqualTo(previousSpeed + 1e-4f), $"t={t}: só desacelera");
                previousSpeed = m.WheelSpeed;
            }

            Assert.That(MechanismsAt(MechanismsStopSeconds).WheelSpeed, Is.EqualTo(0f).Within(1e-4f));
            var final = MechanismsAt(MechanismsStopSeconds);
            foreach (var t in Times(MechanismsStopSeconds, TotalSeconds, 0.25f))
            {
                var m = MechanismsAt(t);
                Assert.That(m.WheelDegrees, Is.EqualTo(final.WheelDegrees).Within(1e-3f), $"t={t}");
                Assert.That(m.MastDegrees, Is.EqualTo(0f).Within(1e-3f));
                Assert.That(m.GimbalDegrees, Is.EqualTo(0f).Within(1e-3f));
            }

            var previousAngle = MechanismsAt(0f).WheelDegrees;
            foreach (var t in Times(Step, MechanismsStopSeconds, Step))
            {
                var angle = MechanismsAt(t).WheelDegrees;
                Assert.That(angle - previousAngle, Is.InRange(-1e-4f, WheelDegreesPerSecond * Step + 1e-3f), $"t={t}: sem salto");
                previousAngle = angle;
            }

            director.ApplyAt(1f);
            var mast = director.Mast.localRotation;
            Assert.That(Quaternion.Angle(mast, Quaternion.identity), Is.GreaterThan(0.5f).Or.EqualTo(0f).Within(0.5f));
            director.ApplyAt(8f);
            var wheelsAt8 = director.Wheels.Select(w => w.localRotation).ToArray();
            director.ApplyAt(25f);
            for (var i = 0; i < wheelsAt8.Length; i++)
                Assert.That(Quaternion.Angle(director.Wheels[i].localRotation, wheelsAt8[i]), Is.LessThan(0.01f), "roda parada para sempre");
            Assert.That(director.HudHidden, Is.True);
            director.ApplyAt(2f);
            Assert.That(director.HudHidden, Is.False);
        }

        // ---------- Memória escolhida ----------

        [Test]
        public void TheChosenMemoryIsTheOneShownWithAReadableFallbackAndNoExceptions()
        {
            var sprite = SpriteOf(640, 360);
            var photo = new EvidenceRecord("mars.valley.photo", EvidenceKind.Photo, "MAR.MEMORY.valley", "MS26", EvidenceAuthorship.PlayerCapture, sprite);
            director.SetMemory(photo);
            var block = new MaterialPropertyBlock();
            director.MemoryFrame.GetPropertyBlock(block);
            Assert.That(block.GetTexture("_BaseMap"), Is.SameAs(sprite.texture), "a textura mostrada é a da memória escolhida");
            Assert.That(director.ShowingFallback, Is.False);
            Assert.That(director.IsPlayersPhoto, Is.True);
            Assert.That(director.MemoryLabelKey, Is.EqualTo("MAR.MEMORY.valley"));
            Assert.That(director.ActiveFallbackKey, Is.Null);
            Assert.That(director.MemoryFrame.transform.localScale.x / director.MemoryFrame.transform.localScale.y, Is.EqualTo(640f / 360f).Within(1e-3f));

            var other = SpriteOf(400, 400);
            var sun = new EvidenceRecord("mars.sun.record", EvidenceKind.Observation, "MAR.MEMORY.sun", "MS28", EvidenceAuthorship.Reconstruction, other);
            director.SetMemory(sun);
            director.MemoryFrame.GetPropertyBlock(block);
            Assert.That(block.GetTexture("_BaseMap"), Is.SameAs(other.texture), "trocar de escolha troca a imagem");
            Assert.That(director.IsPlayersPhoto, Is.False, "o registro científico não é apresentado como foto do jogador");
            Assert.That(director.MemoryLabelKey, Is.EqualTo("MAR.MEMORY.sun"));

            foreach (var record in new EvidenceRecord[] { null, new EvidenceRecord("mars.no.thumb", EvidenceKind.Observation, "MAR.MEMORY.sun", "MS28", EvidenceAuthorship.Reconstruction) })
            {
                Assert.DoesNotThrow(() => director.SetMemory(record));
                director.MemoryFrame.GetPropertyBlock(block);
                Assert.That(director.ShowingFallback, Is.True, "sem textura: quadro de reserva");
                Assert.That(director.ActiveFallbackKey, Is.EqualTo("MAR.MEM.FALLBACK"));
                Assert.That(block.GetTexture("_BaseMap"), Is.Not.Null, "o quadro de reserva tem textura legível");
                Assert.DoesNotThrow(() => director.ApplyAt(20f));
            }

            UnityEngine.Object.DestroyImmediate(sprite.texture);
            UnityEngine.Object.DestroyImmediate(other.texture);
        }

        // ---------- Regiões e planos ----------

        [Test]
        public void OnlyOneThreeDRegionIsActiveAndMarsNeverReturnsAfterTheDiptychBegins()
        {
            string Active(float t)
            {
                director.ApplyAt(t);
                var active = director.Regions.Where(r => r.gameObject.activeSelf).Select(r => r.name).ToList();
                Assert.That(active.Count, Is.EqualTo(1), $"t={t}: uma região por vez");
                return active[0];
            }

            Assert.That(Active(1f), Is.EqualTo("Region_Valley"));
            Assert.That(Active(8f), Is.EqualTo("Region_Valley"));
            Assert.That(Active(14f), Is.EqualTo("Region_Portrait"));
            Assert.That(Active(20f), Is.EqualTo("Region_Frame"));
            Assert.That(Active(26f), Is.EqualTo("Region_Diptych"));
            foreach (var t in Times(24f, TotalSeconds, 0.25f))
                Assert.That(Active(t), Is.Not.EqualTo("Region_Valley").And.Not.EqualTo("Region_Portrait"), $"t={t}: sem retorno a Marte depois do díptico");

            Assert.That(BlockAt(5.99f), Is.EqualTo(BlockMechanisms));
            Assert.That(BlockAt(6f), Is.EqualTo(BlockContact));
            Assert.That(BlockAt(12f), Is.EqualTo(BlockPortrait));
            Assert.That(BlockAt(18f), Is.EqualTo(BlockImage));
            Assert.That(BlockAt(24f), Is.EqualTo(BlockDiptych));
            Assert.That(BlockAt(30f), Is.EqualTo(BlockTitle));
        }

        [Test]
        public void NoShotRepeatsThePreviousOneWithAZoomAndTheDiptychAndTitleStayStill()
        {
            var shots = m31.Shots.Concat(m32.Shots).ToList();
            for (var i = 0; i + 1 < shots.Count - 1; i++)
            {
                CameraDirector.SampleKeyframes(shots[i], shots[i].DurationSeconds, out var pa, out _, out var fa);
                CameraDirector.SampleKeyframes(shots[i + 1], 0f, out var pb, out _, out var fb);
                Assert.That(Vector3.Distance(pa, pb), Is.GreaterThan(2f), $"corte {i + 1}: outro sujeito/lugar, não o mesmo plano com zoom");
            }

            foreach (var shot in new[] { m31.Shots[4], m32.Shots[0] })
            {
                CameraDirector.SampleKeyframes(shot, 0f, out var p0, out var r0, out var f0);
                foreach (var t in Times(0.5f, shot.DurationSeconds, 0.5f))
                {
                    CameraDirector.SampleKeyframes(shot, t, out var p, out var r, out var f);
                    Assert.That(Vector3.Distance(p, p0), Is.LessThan(1e-3f), $"{shot.ShotId}: díptico imóvel");
                    Assert.That(Quaternion.Angle(r, r0), Is.LessThan(0.01f));
                    Assert.That(f, Is.EqualTo(f0).Within(1e-3f));
                }
            }

            CameraDirector.SampleKeyframes(m31.Shots[4], 6f, out var last, out _, out _);
            CameraDirector.SampleKeyframes(m32.Shots[0], 0f, out var title, out _, out _);
            Assert.That(Vector3.Distance(last, title), Is.LessThan(1e-3f), "o título continua no díptico, sem novo plano");

            foreach (var shot in shots)
            {
                CameraDirector.SampleKeyframes(shot, 0f, out var p0, out var r0, out _);
                for (var t = Step; t <= shot.DurationSeconds + 1e-4f; t += Step)
                {
                    CameraDirector.SampleKeyframes(shot, t, out var p, out var r, out _);
                    Assert.That(Vector3.Distance(p, p0), Is.LessThan(0.5f), $"{shot.ShotId} t={t}");
                    Assert.That(Quaternion.Angle(r, r0), Is.LessThan(2f));
                    p0 = p;
                    r0 = r;
                }
            }
        }

        // ---------- Luz, câmera, texto ----------

        [Test]
        public void OneCameraOwnerOneRimLightWithoutShadowsAndNoRoverSpeech()
        {
            Assert.That(root.GetComponentsInChildren<Camera>(true), Is.Empty, "a câmera é a do CameraDirector");
            var lights = root.GetComponentsInChildren<Light>(true);
            Assert.That(lights.Length, Is.EqualTo(1));
            Assert.That(lights[0].shadows, Is.EqualTo(LightShadows.None), "recorte sem sombra");
            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            director.ApplyAt(5f);
            Assert.That(director.RimLightSource.enabled, Is.False);
            director.ApplyAt(15f);
            Assert.That(director.RimLightSource.enabled, Is.True, "recorte aceso no retrato");
            Assert.That(director.RimLightSource.intensity, Is.GreaterThan(1f));
            director.ApplyAt(22f);
            Assert.That(director.RimLightSource.enabled, Is.False);

            foreach (var key in texts.Keys.Where(k => k.StartsWith("MAR.MEM.")))
            {
                Assert.That(texts[key], Does.Not.Contain("\"").And.Not.Contain("“").And.Not.Contain("«"), $"{key}: nenhuma fala atribuída ao rover");
                Assert.That(texts[key].ToLowerInvariant(), Does.Not.Contain("bateria").And.Not.Contain("escuro"), key);
            }

            Assert.That(texts["MAR.MEM.01"], Does.Contain("10 JUN 2018"));
            Assert.That(texts["MAR.MEM.02"], Does.Contain("45,16").And.Contain("NA ÉPOCA"), "recorde na época, não vigente");
            Assert.That(texts["MAR.MEM.05"], Is.EqualTo("SPIRIT · 2004–2010"));
            Assert.That(texts["MAR.MEM.06"], Is.EqualTo("OPPORTUNITY · 2004–2018"));
            Assert.That(texts["MAR.MEM.04"], Does.Contain("PERSEVERANCE VALLEY"));

            director.ApplyAt(12.5f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.MEM.02" }));
            director.ApplyAt(26f);
            Assert.That(director.VisibleLabels.Select(l => l.Anchor), Is.EquivalentTo(new[] { MarsLandingLabelAnchor.LeftHalf, MarsLandingLabelAnchor.RightHalf }));
            director.ApplyAt(32f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.SupersetOf(new[] { "MAR.MEM.05", "MAR.MEM.06" }));
        }

        // ---------- Relógio e controles ----------

        [Test]
        public void SequenceDirectorDrivesM31AndM32AndControlsAppearOnlyAfterTheFinalFourSeconds()
        {
            var lateUpdate = typeof(MarsMemorialDirector).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var events = new List<(string id, float time)>();
            sequenceDirector.EventRaised += id => events.Add((id, MarsMemorialDirector.OffsetOf(sequenceDirector.Current.SequenceId) + sequenceDirector.TimeSeconds));
            var raised = 0;
            director.ControlsReady += () => raised++;

            void Run(SequenceDefinition s, Action<int> each = null)
            {
                sequenceDirector.Play(s);
                for (var frame = 0; sequenceDirector.IsPlaying && frame < 2000; frame++)
                {
                    sequenceDirector.Tick(Step);
                    lateUpdate.Invoke(director, null);
                    each?.Invoke(frame);
                }
            }

            Run(m31, frame =>
            {
                Assert.That(raised, Is.EqualTo(0), "controles não aparecem durante o memorial");
                if (frame != 200)
                    return;

                var wheels = director.Wheels.Select(w => w.localRotation).ToArray();
                var labels = director.VisibleLabels.Select(l => l.TextKey).ToArray();
                sequenceDirector.Pause();
                for (var i = 0; i < 40; i++)
                {
                    sequenceDirector.Tick(Step);
                    lateUpdate.Invoke(director, null);
                }

                for (var i = 0; i < wheels.Length; i++)
                    Assert.That(director.Wheels[i].localRotation, Is.EqualTo(wheels[i]), "pausa congela os mecanismos");
                Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(labels));
                sequenceDirector.Resume();
            });
            Assert.That(raised, Is.EqualTo(0), "terminou M31: ainda sem controles");
            Run(m32, frame =>
            {
                if (frame < 30)
                    Assert.That(raised, Is.EqualTo(0), "durante os 4 s do título não");
            });

            Assert.That(raised, Is.EqualTo(1), "depois dos 4 s finais, uma única vez");
            Assert.That(director.ControlsRaised, Is.True);
            var expected = new Dictionary<string, float>
            {
                ["mars.memorial.mechanisms.stop"] = MechanismsStopSeconds,
                ["mars.memorial.hud.hide"] = HudHideSeconds,
                ["mars.memorial.portrait.rim"] = 12f,
                ["mars.memorial.memory.show"] = 18f,
                ["mars.memorial.diptych"] = 24f,
                ["mars.memorial.title"] = MemorialSeconds,
            };
            foreach (var pair in expected)
            {
                var hits = events.Where(e => e.id == pair.Key).ToList();
                Assert.That(hits.Count, Is.EqualTo(1), pair.Key);
                Assert.That(hits[0].time, Is.EqualTo(pair.Value).Within(Step + 1e-3f), pair.Key + ": mesmo relógio da coreografia");
            }

            Assert.That(cameraDirector.IsControlled, Is.False);
            sequenceDirector.Play(m31);
            Assert.That(director.ControlsRaised, Is.False, "reiniciar o capítulo zera os controles");
            sequenceDirector.Cancel();
        }

        [Test]
        public void CancelingTheTitleNeverRaisesTheControls()
        {
            var raised = 0;
            director.ControlsReady += () => raised++;
            sequenceDirector.Play(m32);
            sequenceDirector.Tick(1f);
            sequenceDirector.Cancel();
            Assert.That(raised, Is.EqualTo(0));
            Assert.That(director.ControlsRaised, Is.False);
        }

        [Test]
        public void PrefabIsCheapAndUsesTheSharedRoverPrefabs()
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in renderer.sharedMaterials)
                    Assert.That(m.shader.name, Does.StartWith("Universal Render Pipeline/"), renderer.name);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            var rovers = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Opportunity_") || t.name.StartsWith("Spirit_")).ToList();
            Assert.That(rovers.Count, Is.EqualTo(4));
            foreach (var rover in rovers)
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rover.gameObject), Does.EndWith(rover.name.StartsWith("Spirit") ? "SpiritVisual.prefab" : "OpportunityVisual.prefab"));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(MarsMemorialBuilder.GeneratedPath + "/MemoryFallback.png").width, Is.LessThanOrEqualTo(512));
        }
    }
}
