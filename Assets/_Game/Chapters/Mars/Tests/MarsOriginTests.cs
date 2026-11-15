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
    /// MAR-013 (revisada): M01–M03 (34,8 s). Prova durações, cues da tabela (texto da abertura), rótulos por chave, globo com os dois pousos nos locais certos e datas, planos que mudam de escala ou assunto, orçamento (uma câmera, zero partículas) e relógio único.
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
        public void ThreeSequencesSumToTheOpeningBudgetAndValidate()
        {
            Assert.That(m01.TotalDurationSeconds, Is.EqualTo(6.3f).Within(1e-4f));
            Assert.That(m02.TotalDurationSeconds, Is.EqualTo(20.8f).Within(1e-4f));
            Assert.That(m03.TotalDurationSeconds, Is.EqualTo(7.7f).Within(1e-4f));
            Assert.That(TotalSeconds, Is.EqualTo(34.8f).Within(1e-4f));
            Assert.That(new[] { m01.SequenceId, m02.SequenceId, m03.SequenceId }, Is.EqualTo(new[] { "mars.seq.M01", "mars.seq.M02", "mars.seq.M03" }));
            foreach (var s in new[] { m01, m02, m03 })
            {
                Assert.That(s.Validate(), Is.Empty, ContractValidation.Describe(s.Validate()));
                Assert.That(sequenceDirector.Validate(s), Is.Empty);
            }

            Assert.That(director.ValidateSetup(), Is.Empty, ContractValidation.Describe(director.ValidateSetup()));
        }

        [Test]
        public void OpeningCuesAreInEnglishAndLabelKeysHaveText()
        {
            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            Assert.That(errors, Is.Empty);
            foreach (var (s, step) in new[] { (m01, "M01"), (m02, "M02"), (m03, "M03") })
                Assert.That(s.Cues.Select(c => c.CueId).OrderBy(x => x), Is.EqualTo(MarsChapterFlow.BuildSequenceCues(table, step).Select(c => c.CueId).OrderBy(x => x)), step);
            Assert.That(m01.Cues.Select(c => c.CueId), Is.EqualTo(new[] { "MAR.C01" }));
            Assert.That(m02.Cues.Select(c => c.CueId), Is.EquivalentTo(new[] { "MAR.C02", "MAR.C03", "MAR.C04" }));
            Assert.That(m03.Cues.Select(c => c.CueId), Is.EqualTo(new[] { "MAR.C05" }), "o globo fala da Spirit em Gusev");

            var texts = new Dictionary<string, TextEntry>();
            foreach (var path in new[] { TextsPath, SubtitlesPath, "Assets/_Game/Chapters/Mars/Art/Landing/MarsLandingTexts.json" })
                foreach (var e in JsonUtility.FromJson<TextTable>(File.ReadAllText(path)).texts)
                    texts[e.key] = e;
            foreach (var label in director.Labels)
            {
                Assert.That(texts.ContainsKey(label.TextKey), Is.True, label.TextKey);
                Assert.That(texts[label.TextKey].en, Is.Not.Empty);
            }

            var cues = JsonUtility.FromJson<CueTable>(File.ReadAllText(SubtitlesPath)).cues.ToDictionary(c => c.cueId);
            Assert.That(cues["MAR.C01"].en, Is.EqualTo("In 2003, NASA sent two robotic explorers to Mars: Spirit and Opportunity."));
            Assert.That(cues["MAR.C02"].en, Is.EqualTo("Their mission was to search the rocks for traces of a past with liquid water."));
            Assert.That(cues["MAR.C03"].en, Is.EqualTo("Before landing, an orbiting probe, Mars Global Surveyor, studied Mars from above and detected hematite in Meridiani Planum."));
            Assert.That(cues["MAR.C04"].en, Is.EqualTo("On Earth, hematite usually forms where water exists. If water was there, the rocks might still hold that story."));
            Assert.That(cues["MAR.C05"].en, Is.EqualTo("Its twin, Spirit, went to Gusev Crater, a possible ancient lake."), "Gusev segue como 'possível', sem a segunda frase");
            Assert.That(texts["MAR.T01"].en, Is.EqualTo("2003"));
            Assert.That(texts["MAR.ORI.01"].en, Is.EqualTo("EARTH · MISSION PREPARATION"));
            Assert.That(texts["MAR.ORI.02"].en, Is.EqualTo("WHY MERIDIANI PLANUM?"));
        }

        [Serializable] sealed class CueTable { public List<CueEntry> cues = new List<CueEntry>(); }
        [Serializable] sealed class CueEntry { public string cueId; public string en; }

        [Test]
        public void LabelsFollowTheScript()
        {
            director.ApplyAt(2f);
            Assert.That(director.VisibleLabels, Is.Empty, "Marte no espaço, só o título do ano");
            director.ApplyAt(LabStart + 1f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.ORI.01" }));
            director.ApplyAt(LabStart + 8f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.ORI.05" }), "o rover da hematita é o Opportunity");
            director.ApplyAt(LabStart + 15f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Does.Contain("MAR.ORI.02"));
            director.ApplyAt(MeridianiMarkedAt + 0.5f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.ORI.04" }), "primeiro Meridiani Planum");
            director.ApplyAt(GusevMarkedAt + 0.5f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EquivalentTo(new[] { "MAR.ORI.04", "MAR.ORI.03" }), "depois a Cratera Gusev");
            Assert.That(director.MarkerOf("MAR.ORI.03"), Is.SameAs(director.GusevMarker), "o texto é ancorado no marcador");
            Assert.That(director.MarkerOf("MAR.ORI.04"), Is.SameAs(director.MeridianiMarker));
            Assert.That(director.MarkerOf("MAR.ORI.01"), Is.Null);
        }

        // ---------- Mapas e globo ----------

        [Test]
        public void GlobeMarksBothLandingSitesAndTurnsFromSpiritToOpportunity()
        {
            Assert.That(Gusev, Is.EqualTo(new Vector2(-14.57f, 175.48f)), "Mars24: Spirit 175,48°L, 14,57°S");
            Assert.That(Meridiani, Is.EqualTo(new Vector2(-1.95f, 354.47f)), "Mars24: Opportunity 354,47°L, 1,95°S");
            Assert.That(Vector3.Angle(Direction(Gusev), Direction(Meridiani)), Is.GreaterThan(150f), "lados opostos de Marte");

            director.ApplyAt(IntroTurnStart);
            Assert.That(Vector3.Dot(director.Globe.TransformDirection(Direction(Meridiani)), Vector3.back), Is.GreaterThan(0.999f), "M03 começa o globo em Meridiani");
            Assert.That(director.MeridianiMarker.enabled, Is.True);
            Assert.That(director.GusevMarker.enabled, Is.False, "Gusev só é marcada ao chegar");
            Assert.That(director.MeridianiPulse.enabled, Is.True, "anel de pulso do marcador aceso");

            director.ApplyAt(TotalSeconds);
            Assert.That(Vector3.Dot(director.Globe.TransformDirection(Direction(Gusev)), Vector3.back), Is.GreaterThan(0.999f), "e termina em Gusev");
            Assert.That(director.GusevMarker.enabled && director.MeridianiMarker.enabled, Is.True, "os dois locais ficam marcados");

            // O marcador fica sobre o local no globo (filho do globo, na direção da latitude/longitude).
            Assert.That(Vector3.Angle(director.GusevMarker.transform.parent.localPosition, Direction(Gusev)), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(director.MeridianiMarker.transform.parent.localPosition, Direction(Meridiani)), Is.LessThan(0.01f));
            Assert.That(Sample(LabStart + 5f).GusevGlow + Sample(LabStart + 5f).MeridianiGlow, Is.EqualTo(0f), "no laboratório nada aceso");
        }

        [Test]
        public void GlobeRotationNeverSnaps()
        {
            var previous = Sample(GlobeStart).GlobeRotation;
            foreach (var t in Times(GlobeStart + Step, TotalSeconds, Step))
            {
                var now = Sample(t).GlobeRotation;
                Assert.That(Quaternion.Angle(previous, now), Is.LessThan(12f), $"t={t}");
                previous = now;
            }
        }

        [Test]
        public void OnlyTheRegionOfTheCurrentBlockIsActiveAndPlanetUsesTheNasaMap()
        {
            foreach (var (t, region) in new[] { (1f, RegionSpace), (10f, RegionLab), (26f, RegionLab), (30f, RegionGlobe), (GlobeStart + 1f, RegionGlobe) })
            {
                director.ApplyAt(t);
                for (var i = 0; i < director.Regions.Count; i++)
                    Assert.That(director.Regions[i].gameObject.activeSelf, Is.EqualTo(i == region), $"t={t} região {i}");
            }

            var map = director.Planet.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
            Assert.That(map, Is.Not.Null);
            Assert.That(map.name, Is.EqualTo("Mars_Equirect"));
            Assert.That(director.Globe.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(map));
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
                    Assert.That(Vector3.Distance(pa, pb) > 2f || scaleChange > 1.1f || Quaternion.Angle(ra, rb) > 20f, Is.True,
                        $"{s.SequenceId} corte {i + 1}: quase o mesmo plano (zoom/ângulo sem mudança clara)");
                }
            }
        }

        [Test]
        public void TheOnlyCameraIsTheDirectorsAndThereAreNoParticleSystemsOrLights()
        {
            Assert.That(root.GetComponentsInChildren<Camera>(true), Is.Empty, "a sequência usa a câmera do CameraDirector");
            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true), Is.Empty, "zero partículas");
            Assert.That(root.GetComponentsInChildren<Light>(true).Count(l => l.shadows != LightShadows.None), Is.LessThanOrEqualTo(1), "no máximo uma luz com sombra");
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (var s in new[] { m01, m02, m03 })
                foreach (var shot in s.Shots)
                    Assert.That(shot.RigId, Is.EqualTo(MarsOriginDirector.RigId));
        }

        // ---------- Laboratório ----------

        [Test]
        public void LabHasTwoRoversOnTheFloorInsideTheCleanroomOrTheFallbackRoom()
        {
            var lab = root.transform.Find("Region_Lab");
            var hasModel = AssetDatabase.LoadAssetAtPath<GameObject>(MarsOriginBuilder.LabModelPath) != null;
            Assert.That(lab.Find("Cleanroom") != null, Is.EqualTo(hasModel), "sala limpa quando o GLB da sala limpa está importado");
            Assert.That(lab.Find(hasModel ? "Cleanroom" : "Floor"), Is.Not.Null);
            foreach (var name in new[] { "Spirit", "Opportunity" })
            {
                var rover = lab.Find("Rover_" + name);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rover.gameObject), Does.EndWith(name + "Visual.prefab"), "modelo da MAR-011");
                Assert.That(rover.localPosition.y, Is.EqualTo(MarsOriginBuilder.RoverHeightMeters).Within(1e-3f), "rover apoiado no piso");
            }

            Assert.That(lab.Find("Rover_Spirit").localPosition.x, Is.LessThan(lab.Find("Rover_Opportunity").localPosition.x), "Spirit à esquerda");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in renderer.sharedMaterials)
                    if (m != null && !renderer.transform.IsChildOf(lab.Find("Cleanroom") ?? lab))
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
            Run(m02);
            Run(m03, frame =>
            {
                if (frame != 70)
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

            Assert.That(events.Select(e => e.id), Is.EqualTo(new[]
            {
                "mars.origin.space", "mars.origin.lab.wide", "mars.origin.lab.detail", "mars.origin.lab.meridiani",
                "mars.origin.globe.meridiani", "mars.origin.globe.gusev",
            }));
            Assert.That(events[1].time, Is.EqualTo(LabStart).Within(Step + 1e-3f), "evento no mesmo relógio da coreografia");
            Assert.That(events[4].time, Is.EqualTo(GlobeStart).Within(Step + 1e-3f), "globo começa junto com M03");
            Assert.That(events[5].time, Is.EqualTo(GlobeStart + 3.4f).Within(Step + 1e-3f));
            Assert.That(cameraDirector.IsControlled, Is.False, "controle da câmera devolvido");
        }
    }
}
