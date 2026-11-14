using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Abandoned.SpaceApps.MarsOriginChoreography;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-013: M01–M03 (25 s). Prova durações, cues da tabela, rótulos por chave, destinos corretos no globo e só o destino da legenda aceso,
    /// planos que mudam de escala ou assunto, lançamentos em sequência com uma só chama, orçamento (uma câmera, zero partículas) e relógio único.
    /// </summary>
    public class MarsOriginTests
    {
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Art/Origin/MarsOriginTexts.json";
        const float Step = 0.05f;

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        GameObject root;
        MarsOriginDirector director;
        SequenceDefinition m01, m02, m03;
        CameraDirector cameraDirector;
        SequenceDirector sequenceDirector;
        Camera mainCamera;

        [SetUp]
        public void SetUp()
        {
            root = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MarsOriginBuilder.PrefabPath)));
            director = root.GetComponent<MarsOriginDirector>();
            m01 = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MarsOriginBuilder.SequencePath("M01"));
            m02 = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MarsOriginBuilder.SequencePath("M02"));
            m03 = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MarsOriginBuilder.SequencePath("M03"));
            var host = Track(new GameObject("directors"));
            mainCamera = Track(new GameObject("camera")).AddComponent<Camera>();
            cameraDirector = host.AddComponent<CameraDirector>();
            cameraDirector.GroundProbe = _ => null;
            cameraDirector.Initialize(mainCamera);
            sequenceDirector = host.AddComponent<SequenceDirector>();
            sequenceDirector.Initialize(cameraDirector, null, null);
            director.Initialize(sequenceDirector, cameraDirector);
        }

        [TearDown]
        public void TearDown()
        {
            sequenceDirector.Cancel();
            foreach (var obj in created)
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        static IEnumerable<float> Times(float from, float to, float step)
        {
            for (var t = from; t <= to + 1e-4f; t += step)
                yield return t;
        }

        [Serializable] sealed class TextTable { public List<TextEntry> texts = new List<TextEntry>(); }
        [Serializable] sealed class TextEntry { public string key; public string pt; public string en; }

        // ---------- Duração e cues ----------

        [Test]
        public void ThreeSequencesSumToTwentyFiveSecondsAndValidate()
        {
            Assert.That(m01.TotalDurationSeconds, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(m02.TotalDurationSeconds, Is.EqualTo(12f).Within(1e-4f));
            Assert.That(m03.TotalDurationSeconds, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(TotalSeconds, Is.EqualTo(25f));
            Assert.That(new[] { m01.SequenceId, m02.SequenceId, m03.SequenceId }, Is.EqualTo(new[] { "mars.seq.M01", "mars.seq.M02", "mars.seq.M03" }));
            Assert.That(MarsChapterFlow.CreateDefaultBindings().Where(b => b.IsCinematic).Select(b => b.SequenceId), Is.SupersetOf(new[] { m01.SequenceId, m02.SequenceId, m03.SequenceId }));
            foreach (var s in new[] { m01, m02, m03 })
            {
                Assert.That(s.Validate(), Is.Empty, ContractValidation.Describe(s.Validate()));
                Assert.That(sequenceDirector.Validate(s), Is.Empty);
            }

            Assert.That(director.ValidateSetup(), Is.Empty, ContractValidation.Describe(director.ValidateSetup()));
        }

        [Test]
        public void CuesComeFromTheSubtitleTableAndLabelKeysHaveBothLanguages()
        {
            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            Assert.That(errors, Is.Empty);
            foreach (var (s, step) in new[] { (m01, "M01"), (m02, "M02"), (m03, "M03") })
                Assert.That(s.Cues.Select(c => c.CueId).OrderBy(x => x), Is.EqualTo(MarsChapterFlow.BuildSequenceCues(table, step).Select(c => c.CueId).OrderBy(x => x)), step);
            Assert.That(m02.Cues.Select(c => c.CueId), Is.EquivalentTo(new[] { "MAR.C02", "MAR.C03", "MAR.C04" }));

            var texts = new Dictionary<string, TextEntry>();
            foreach (var path in new[] { TextsPath, SubtitlesPath })
                foreach (var e in JsonUtility.FromJson<TextTable>(File.ReadAllText(path)).texts)
                    texts[e.key] = e;
            foreach (var label in director.Labels)
            {
                Assert.That(texts.ContainsKey(label.TextKey), Is.True, label.TextKey);
                Assert.That(texts[label.TextKey].pt, Is.Not.Empty);
                Assert.That(texts[label.TextKey].en, Is.Not.Empty);
            }

            Assert.That(texts["MAR.ORI.01"].pt, Is.EqualTo("SPIRIT"));
            Assert.That(texts["MAR.ORI.02"].pt, Is.EqualTo("OPPORTUNITY"));
            Assert.That(texts["MAR.ORI.06"].pt + texts["MAR.ORI.07"].pt, Does.Not.Match(@"\d{1,2} (JUN|JUL)"), "sem o dia, para não misturar UTC e calendário local");
        }

        [Test]
        public void LabelsAppearOnlyWhenTheirCueIs()
        {
            director.ApplyAt(2f);
            Assert.That(director.VisibleLabels, Is.Empty, "o travelling inicial não tem placa ainda");
            director.ApplyAt(6f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EquivalentTo(new[] { "MAR.ORI.01", "MAR.ORI.02" }), "as duas placas");
            director.ApplyAt(10f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.ORI.03" }), "C02: Gusev");
            director.ApplyAt(14f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.ORI.04" }), "C03: Meridiani");
            director.ApplyAt(18f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.ORI.05" }), "C04: hematita");
            director.ApplyAt(21f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.ORI.06" }));
            director.ApplyAt(24f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.ORI.07" }));
        }

        // ---------- Mapa ----------

        [Test]
        public void GlobeLightsOnlyTheSitesOfTheCurrentCueAndTurnsThemToTheCamera()
        {
            var g = Direction(Gusev);
            Assert.That(Gusev.x, Is.InRange(-15f, -14f), "Gusev ≈ 14,6°S");
            Assert.That(Gusev.y, Is.InRange(175f, 176f), "≈ 175,5°L");
            Assert.That(Meridiani.x, Is.InRange(-2.5f, -1.5f), "Meridiani ≈ 2°S");
            Assert.That(Meridiani.y, Is.InRange(354f, 355f), "≈ 354,5°L (5,5°O)");
            Assert.That(Vector3.Angle(g, Direction(Meridiani)), Is.GreaterThan(150f), "lados opostos de Marte");

            var at = Sample(MapStart + 2f);
            Assert.That(at.GusevGlow, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(at.MeridianiGlow + at.HematiteGlow, Is.EqualTo(0f).Within(1e-4f), "só o local da legenda");
            director.ApplyAt(MapStart + 2f);
            Assert.That(Vector3.Dot(director.Globe.TransformDirection(g), Vector3.back), Is.GreaterThan(0.999f), "Gusev de frente para a câmera");
            Assert.That(director.GusevMarker.enabled, Is.True);
            Assert.That(director.MeridianiMarker.enabled, Is.False);

            director.ApplyAt(MapStart + 6f);
            var second = Sample(MapStart + 6f);
            Assert.That(second.MeridianiGlow, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(second.GusevGlow + second.HematiteGlow, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Vector3.Dot(director.Globe.TransformDirection(Direction(Meridiani)), Vector3.back), Is.GreaterThan(0.999f));
            Assert.That(director.GusevMarker.enabled, Is.False);

            director.ApplyAt(MapStart + 10f);
            Assert.That(Sample(MapStart + 10f).HematiteGlow, Is.EqualTo(1f).Within(1e-3f), "C04: a pista de hematita");
            Assert.That(director.HematitePatch.enabled, Is.True);
            Assert.That(Sample(MapStart - 1f).GusevGlow + Sample(LaunchStart + 1f).MeridianiGlow, Is.EqualTo(0f), "fora do mapa nada aceso");
        }

        [Test]
        public void GlobeRotationNeverSnaps()
        {
            var previous = Sample(MapStart).GlobeRotation;
            foreach (var t in Times(MapStart + Step, LaunchStart, Step))
            {
                var now = Sample(t).GlobeRotation;
                Assert.That(Quaternion.Angle(previous, now), Is.LessThan(12f), $"t={t}");
                previous = now;
            }
        }

        // ---------- Câmeras ----------

        [Test]
        public void ShotsAreContinuousAndEveryCutChangesScaleOrSubject()
        {
            foreach (var s in new[] { m01, m02, m03 })
            {
                foreach (var shot in s.Shots)
                {
                    CameraDirector.SampleKeyframes(shot, 0f, out var p0, out var r0, out _);
                    for (var t = Step; t <= shot.DurationSeconds + 1e-4f; t += Step)
                    {
                        CameraDirector.SampleKeyframes(shot, t, out var p1, out var r1, out _);
                        Assert.That(Vector3.Distance(p0, p1), Is.LessThan(3.2f), $"{shot.ShotId} t={t}");
                        Assert.That(Quaternion.Angle(r0, r1), Is.LessThan(5f), $"{shot.ShotId} t={t}");
                        p0 = p1;
                        r0 = r1;
                    }
                }

                for (var i = 0; i + 1 < s.Shots.Count; i++)
                {
                    CameraDirector.SampleKeyframes(s.Shots[i], s.Shots[i].DurationSeconds, out var pa, out var ra, out var fa);
                    CameraDirector.SampleKeyframes(s.Shots[i + 1], 0f, out var pb, out var rb, out var fb);
                    var scaleChange = Mathf.Max(fa, fb) / Mathf.Min(fa, fb);
                    Assert.That(Vector3.Distance(pa, pb) > 2f || scaleChange > 1.2f || Quaternion.Angle(ra, rb) > 25f, Is.True,
                        $"{s.SequenceId} corte {i + 1}: quase o mesmo plano (zoom/ângulo sem mudança clara)");
                }
            }

            float Distance(ShotDefinition shot, Vector3 target)
            {
                CameraDirector.SampleKeyframes(shot, shot.DurationSeconds * 0.5f, out var p, out _, out _);
                return Vector3.Distance(p, target);
            }

            var globe = MarsOriginBuilder.MapOffset;
            var d = m02.Shots.Select((shot, i) => Distance(shot, globe + (i == 2 ? new Vector3(0f, 0f, -2f) : Vector3.zero))).ToArray();
            Assert.That(d[0], Is.GreaterThan(d[1] + 0.5f), "de longe para mais perto");
            Assert.That(d[1], Is.GreaterThan(d[2] + 1.5f), "e de perto para a mancha de hematita");
        }

        [Test]
        public void TheOnlyCameraIsTheDirectorsAndThereAreNoParticleSystemsOrLights()
        {
            Assert.That(root.GetComponentsInChildren<Camera>(true), Is.Empty, "a sequência usa a câmera do CameraDirector");
            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true), Is.Empty, "zero partículas: a chama é uma malha");
            Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty, "a luz lateral é da cena");
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (var s in new[] { m01, m02, m03 })
                foreach (var shot in s.Shots)
                    Assert.That(shot.RigId, Is.EqualTo(MarsOriginDirector.RigId));
        }

        // ---------- Lançamentos ----------

        [Test]
        public void TwoRocketsLaunchOneAfterTheOtherSharingOneFlame()
        {
            director.ApplyAt(LaunchStart);
            var baseA = director.RocketA.localPosition;
            var baseB = director.RocketB.localPosition;
            Assert.That(Vector3.Distance(baseA, baseB), Is.EqualTo(RocketSpacingMeters).Within(1e-3f), "silhuetas separadas");
            Assert.That(director.Flame.gameObject.activeSelf, Is.False, "na rampa, sem chama");

            var previousA = 0f;
            var previousB = 0f;
            foreach (var t in Times(LaunchStart, TotalSeconds, Step))
            {
                director.ApplyAt(t);
                var heightA = director.RocketA.localPosition.y - baseA.y;
                var heightB = director.RocketB.localPosition.y - baseB.y;
                Assert.That(heightA, Is.GreaterThanOrEqualTo(previousA - 1e-4f), $"t={t}");
                Assert.That(heightB, Is.GreaterThanOrEqualTo(previousB - 1e-4f));
                Assert.That(heightA - previousA, Is.LessThan(12f), $"t={t}: sem salto");
                Assert.That(heightB - previousB, Is.LessThan(12f));
                if (t - LaunchStart < RocketBTakeoff)
                    Assert.That(heightB, Is.EqualTo(0f).Within(1e-4f), $"t={t}: o segundo só sai depois do primeiro");
                if (director.Flame.gameObject.activeSelf)
                {
                    var host = t - LaunchStart < RocketBTakeoff ? director.RocketA : director.RocketB;
                    Assert.That(Vector3.Distance(director.Flame.localPosition, host.localPosition + Vector3.up * 0.5f), Is.LessThan(1e-3f), $"t={t}: a chama acompanha o foguete aceso");
                }

                previousA = heightA;
                previousB = heightB;
            }

            Assert.That(previousA, Is.GreaterThan(80f));
            Assert.That(previousB, Is.GreaterThan(40f));
            Assert.That(Sample(LaunchStart + RocketBTakeoff - 0.3f).Flame, Is.LessThan(0.2f), "a chama do primeiro apaga antes de acender no segundo");
            Assert.That(Sample(LaunchStart + RocketBTakeoff + 0.5f).Flame, Is.GreaterThan(0.9f));
        }

        // ---------- Laboratório ----------

        [Test]
        public void LabHasTwoRoversOnBenchesAndEveryPropHasAFunction()
        {
            var lab = root.transform.Find("Lab");
            var allowed = new[] { "Floor", "Bench_", "Support_", "Plate_", "Rack_", "Cable_", "Rover_" };
            foreach (Transform child in lab)
                Assert.That(allowed.Any(a => child.name.StartsWith(a)), Is.True, $"{child.name}: objeto de fundo sem função");

            foreach (var name in new[] { "Spirit", "Opportunity" })
            {
                Assert.That(lab.Find("Bench_" + name), Is.Not.Null);
                Assert.That(lab.Find("Plate_" + name), Is.Not.Null, "placa com o nome do rover");
                Assert.That(lab.Find("Cable_" + name), Is.Not.Null);
                var rover = lab.Find("Rover_" + name);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rover.gameObject), Does.EndWith(name + "Visual.prefab"), "modelo da MAR-011");
                var bench = lab.Find("Bench_" + name);
                Assert.That(rover.position.y, Is.EqualTo(bench.position.y + 0.5f * bench.lossyScale.y + 0.12f).Within(0.01f), "rover apoiado na bancada");
            }

            Assert.That(lab.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Support_")), Is.EqualTo(8), "quatro apoios por rover");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in renderer.sharedMaterials)
                    Assert.That(m.shader.name, Does.StartWith("Universal Render Pipeline/"), renderer.name);
        }

        // ---------- Relógio único ----------

        [Test]
        public void SequenceDirectorDrivesM01ToM03AndPauseFreezesGlobeCameraAndLabels()
        {
            var lateUpdate = typeof(MarsOriginDirector).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var events = new List<(string id, float time)>();
            sequenceDirector.EventRaised += id => events.Add((id, MarsOriginDirector.OffsetOf(sequenceDirector.Current.SequenceId) + sequenceDirector.TimeSeconds));

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

            Run(m01);
            Run(m02, frame =>
            {
                if (frame != 100)
                    return;

                var rotation = director.Globe.localRotation;
                var camera = mainCamera.transform.position;
                var labels = director.VisibleLabels.Select(l => l.TextKey).ToArray();
                sequenceDirector.Pause();
                for (var i = 0; i < 40; i++)
                {
                    sequenceDirector.Tick(Step);
                    lateUpdate.Invoke(director, null);
                }

                Assert.That(director.Globe.localRotation, Is.EqualTo(rotation), "pausa congela o globo");
                Assert.That(mainCamera.transform.position, Is.EqualTo(camera), "e a câmera");
                Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(labels), "e os letreiros");
                sequenceDirector.Resume();
            });
            Run(m03);

            Assert.That(events.Select(e => e.id), Is.EqualTo(new[] { "mars.origin.gusev.glow", "mars.origin.meridiani.glow", "mars.origin.hematite.glow", "mars.origin.launch.first", "mars.origin.launch.second" }));
            Assert.That(events[0].time, Is.EqualTo(MapStart).Within(Step + 1e-3f));
            Assert.That(events[1].time, Is.EqualTo(MapStart + 4f).Within(Step + 1e-3f));
            Assert.That(events[2].time, Is.EqualTo(MapStart + 8f).Within(Step + 1e-3f));
            Assert.That(events[3].time, Is.EqualTo(LaunchStart + RocketATakeoff).Within(Step + 1e-3f), "evento no mesmo relógio da coreografia");
            Assert.That(events[4].time, Is.EqualTo(LaunchStart + RocketBTakeoff).Within(Step + 1e-3f));
            Assert.That(cameraDirector.IsControlled, Is.False, "controle da câmera devolvido");
        }
    }
}
