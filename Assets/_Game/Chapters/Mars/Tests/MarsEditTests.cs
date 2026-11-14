using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-018: edição e câmeras. Prova catálogo das 14 etapas cinemáticas contra o fluxo real, cues sem duplicar texto, planos autorais com
    /// folga de rocha, uma região por etapa, só o split renderiza dois pontos de vista, exposição aplicada e restaurada, término devolvido ao
    /// fluxo, pausa/reinício que devolvem câmera e luz e falha de sequência que não trava o capítulo.
    /// </summary>
    public class MarsEditTests
    {
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string Prefabs = "Assets/_Game/Chapters/Mars/Prefabs/";
        const float Step = 0.05f;
        const float OriginalSun = 0.77f;

        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        MarsChapterSequenceCatalog catalog;
        MarsLightingBindings lighting;
        MarsSequenceConductor conductor;
        MarsCameraAnchors anchors;
        MarsChapterFlow flow;
        MockChapterDirector chapterDirector;
        ChapterDefinition chapter;
        CameraDirector cameraDirector;
        SequenceDirector sequenceDirector;
        Camera mainCamera;
        Light sun;
        FakeTime time;
        GameObject origin, landing, shield, valley, spirit, memorial, eagle;
        MerLandingAssembly landingAssembly;
        MarsDualLandingDirector landingDirector;
        SpiritInterludeDirector spiritDirector;
        MarsOriginDirector originDirector;
        MarsMemorialDirector memorialDirector;
        readonly List<string> started = new List<string>();

        T Track<T>(T o) where T : UnityEngine.Object
        {
            created.Add(o);
            return o;
        }

        GameObject Instance(string prefab) => Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefab)));

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<MarsChapterSequenceCatalog>(MarsEditBuilder.CatalogPath);
            lighting = AssetDatabase.LoadAssetAtPath<MarsLightingBindings>(MarsEditBuilder.LightingPath);
            chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            var host = Track(new GameObject("directors"));
            mainCamera = Track(new GameObject("camera")).AddComponent<Camera>();
            mainCamera.backgroundColor = new Color(0.1f, 0.2f, 0.3f);
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            sun = Track(new GameObject("sun")).AddComponent<Light>();
            sun.intensity = OriginalSun;
            sun.color = Color.white;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
            cameraDirector = host.AddComponent<CameraDirector>();
            cameraDirector.GroundProbe = _ => null;
            cameraDirector.Initialize(mainCamera);
            sequenceDirector = host.AddComponent<SequenceDirector>();
            sequenceDirector.Initialize(cameraDirector, null, null);
            time = new FakeTime();

            origin = Instance("MarsPreparationStage.prefab");
            landing = Instance("MerLandingAssembly.prefab");
            shield = Instance("ShieldFieldEnvironment.prefab");
            valley = Instance("PerseveranceValleyStage.prefab");
            spirit = Instance("SpiritInterludeStage.prefab");
            memorial = Instance("MarsMemorialStage.prefab");
            eagle = Track(new GameObject("Eagle (MAR-012 pendente)"));
            originDirector = origin.GetComponent<MarsOriginDirector>();
            landingDirector = landing.GetComponent<MarsDualLandingDirector>();
            landingAssembly = landingDirector.Opportunity;
            spiritDirector = spirit.GetComponent<SpiritInterludeDirector>();
            memorialDirector = memorial.GetComponent<MarsMemorialDirector>();
            originDirector.Initialize(sequenceDirector, cameraDirector);
            landingDirector.Initialize(sequenceDirector, cameraDirector);
            spiritDirector.Initialize(sequenceDirector, cameraDirector);
            memorialDirector.Initialize(sequenceDirector, cameraDirector);

            var anchorObject = Instance("MarsCameraAnchors.prefab");
            conductor = anchorObject.GetComponent<MarsSequenceConductor>();
            anchors = anchorObject.GetComponent<MarsCameraAnchors>();
            conductor.RegisterRegion(MarsEditBuilder.RegionOrigin, origin);
            conductor.RegisterRegion(MarsEditBuilder.RegionLanding, landing);
            conductor.RegisterRegion(MarsEditBuilder.RegionShield, shield);
            conductor.RegisterRegion(MarsEditBuilder.RegionValley, valley);
            conductor.RegisterRegion(MarsEditBuilder.RegionSpirit, spirit);
            conductor.RegisterRegion(MarsEditBuilder.RegionMemorial, memorial);
            conductor.RegisterRegion(MarsEditBuilder.RegionEagle, eagle);

            chapterDirector = new MockChapterDirector();
            flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(chapterDirector, time, chapter, File.ReadAllText(SubtitlesPath));
            flow.StepStarted += b => started.Add(b.StepId);
            conductor.Initialize(flow, sequenceDirector, cameraDirector, sun);
        }

        [TearDown]
        public void TearDown()
        {
            conductor.ReleaseCinematic();
            sequenceDirector.Cancel();
            foreach (var o in created)
                if (o != null)
                    UnityEngine.Object.DestroyImmediate(o);
            created.Clear();
        }

        static IEnumerable<float> Times(float a, float b, float step)
        {
            for (var t = a; t <= b + 1e-4f; t += step)
                yield return t;
        }

        void LateUpdates()
        {
            foreach (var d in new MonoBehaviour[] { originDirector, landingDirector, spiritDirector, memorialDirector })
            {
                if (d != null && d.gameObject.activeInHierarchy)
                    d.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, null);
            }
        }

        int RenderedCameras()
        {
            var count = mainCamera.enabled ? 1 : 0;
            foreach (var c in new[] { landingDirector.SpiritCamera, spiritDirector.SpiritCamera })
                if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                    count++;
            return count;
        }

        /// <summary>Joga o capítulo inteiro pelo condutor: cinemáticas pelo SequenceDirector, gameplay por eventos do fluxo.</summary>
        void PlayChapter(Action<string, MarsChapterFlow.StepBinding> perFrame)
        {
            chapterDirector.Begin(chapter);
            var guard = 0;
            while (!chapterDirector.IsFinished && guard++ < 300000)
            {
                var binding = flow.CurrentBinding;
                if (binding == null)
                    break;

                if (!binding.IsCinematic)
                {
                    foreach (var e in binding.CompletionEvents)
                        flow.Raise(e);
                }

                var step = binding.StepId;
                for (var i = 0; i < 4000 && flow.CurrentBinding != null && flow.CurrentBinding.StepId == step && !chapterDirector.IsFinished; i++)
                {
                    time.ActiveDeltaSeconds = Step;
                    sequenceDirector.Tick(Step);
                    LateUpdates();
                    flow.Tick(Step);
                    if (flow.CurrentBinding != null && !chapterDirector.IsFinished)
                        perFrame?.Invoke(flow.CurrentBinding.StepId, flow.CurrentBinding);
                }
            }
        }

        // ---------- Catálogo ----------

        [Test]
        public void CatalogCoversEveryCinematicStepOfTheFlowWithMatchingIdsDurationsAndCutPoints()
        {
            Assert.That(catalog.Validate(), Is.Empty, ContractValidation.Describe(catalog.Validate()));
            var cinematic = MarsChapterFlow.CreateDefaultBindings().Where(b => b.IsCinematic).ToList();
            Assert.That(cinematic.Count, Is.EqualTo(MarsChapterSequenceCatalog.ExpectedCinematicSteps));
            Assert.That(catalog.Entries.Count, Is.EqualTo(14));
            foreach (var binding in cinematic)
            {
                var entry = catalog.Find(binding.StepId);
                Assert.That(entry, Is.Not.Null, binding.StepId);
                Assert.That(entry.Sequence.SequenceId, Is.EqualTo(binding.SequenceId));
                Assert.That(entry.Sequence.TotalDurationSeconds, Is.EqualTo(binding.ReferenceSeconds).Within(0.01f), $"{binding.StepId}: folga zero");
                Assert.That(entry.SlackSeconds, Is.EqualTo(0f).Within(0.01f));
                var boundaries = new List<float>();
                var t = 0f;
                for (var i = 0; i < entry.Sequence.Shots.Count - 1; i++)
                    boundaries.Add(t += entry.Sequence.Shots[i].DurationSeconds);
                Assert.That(entry.CutPoints, Is.EqualTo(boundaries), $"{binding.StepId}: pontos de corte");
            }

            Assert.That(catalog.Entries.Sum(e => e.Sequence.TotalDurationSeconds), Is.EqualTo(cinematic.Sum(b => b.ReferenceSeconds)).Within(0.1f));
            Assert.That(MarsChapterFlow.CreateDefaultBindings().Where(b => !b.IsCinematic).All(b => catalog.Find(b.StepId) == null), Is.True, "gameplay não tem sequência");
        }

        [Test]
        public void CuesComeFromTheTableAreNeverEmptyAndNeverDuplicated()
        {
            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            var texts = new HashSet<string>(table.texts.Select(t => t.key));
            var seen = new HashSet<string>();
            foreach (var entry in catalog.Entries)
            {
                var expected = MarsChapterFlow.BuildSequenceCues(table, entry.StepId).Select(c => c.CueId).OrderBy(x => x);
                Assert.That(entry.Sequence.Cues.Select(c => c.CueId).OrderBy(x => x), Is.EqualTo(expected), entry.StepId);
                foreach (var cue in entry.Sequence.Cues)
                {
                    Assert.That(string.IsNullOrWhiteSpace(cue.TextKey), Is.False, $"{cue.CueId}: caixa de legenda vazia");
                    Assert.That(cue.EndSeconds, Is.GreaterThan(cue.StartSeconds));
                    Assert.That(seen.Add(cue.CueId), Is.True, $"{cue.CueId} em duas sequências");
                }
            }

            Assert.That(seen.Count, Is.EqualTo(28), "as 28 legendas de relógio de sequência");
            Assert.That(texts.Count, Is.GreaterThan(0));
        }

        // ---------- Planos autorais ----------

        [Test]
        public void AuthoredShotsKeepClearOfTheRockAndNeverRepeatTheMinigameOrTheirNeighbour()
        {
            foreach (var step in new[] { "M14", "M15", "M20", "M23", "M27" })
            {
                var sequence = catalog.Find(step).Sequence;
                var rig = MarsCameraAnchors.RigIdFor(step);
                Assert.That(anchors.Find(rig), Is.Not.Null, rig);
                Assert.That(cameraDirector.HasRig(rig), Is.True, "rig registrado no CameraDirector");
                Assert.That(sequenceDirector.Validate(sequence), Is.Empty, step);
                foreach (var shot in sequence.Shots)
                {
                    Assert.That(shot.RigId, Is.EqualTo(rig));
                    Assert.That(shot.Events, Is.Not.Empty, $"{shot.ShotId}: cada plano tem propósito/evento");
                    Assert.That(shot.ShotId.ToLowerInvariant(), Does.Not.Contain("rat").And.Not.Contain("dig"), "sem replay da RAT");
                    for (var t = 0f; t <= shot.DurationSeconds + 1e-4f; t += 0.1f)
                    {
                        CameraDirector.SampleKeyframes(shot, t, out var p, out _, out var fov);
                        if (step != "M23")
                            Assert.That(new Vector2(p.x, p.z).magnitude, Is.GreaterThanOrEqualTo(2.5f), $"{shot.ShotId} t={t}: perto demais do sujeito/rocha");
                        Assert.That(p.y, Is.GreaterThanOrEqualTo(0.9f), $"{shot.ShotId}: acima do chão");
                        Assert.That(fov, Is.InRange(28f, 50f));
                    }

                    CameraDirector.SampleKeyframes(shot, 0f, out var q0, out var r0, out _);
                    for (var t = Step; t <= shot.DurationSeconds + 1e-4f; t += Step)
                    {
                        CameraDirector.SampleKeyframes(shot, t, out var q1, out var r1, out _);
                        Assert.That(Vector3.Distance(q0, q1), Is.LessThan(0.5f), $"{shot.ShotId} t={t}: sem snap");
                        Assert.That(Quaternion.Angle(r0, r1), Is.LessThan(2f), $"{shot.ShotId} t={t}: sem tremor/giro brusco");
                        q0 = q1;
                        r0 = r1;
                    }
                }

                for (var i = 0; i + 1 < sequence.Shots.Count; i++)
                {
                    CameraDirector.SampleKeyframes(sequence.Shots[i], sequence.Shots[i].DurationSeconds, out var pa, out _, out var fa);
                    CameraDirector.SampleKeyframes(sequence.Shots[i + 1], 0f, out var pb, out _, out var fb);
                    Assert.That(Vector3.Distance(pa, pb) > 2f || Mathf.Max(fa, fb) / Mathf.Min(fa, fb) > 1.15f, Is.True, $"{step}: o corte muda escala, sujeito ou ação");
                }
            }

            CameraDirector.SampleKeyframes(catalog.Find("M14").Sequence.Shots[0], 0f, out var first, out var rot, out _);
            Assert.That(first.magnitude, Is.GreaterThan(2.5f), "descoberta com enquadramento mais aberto que o minigame da RAT");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, rot.eulerAngles.x)), Is.LessThan(45f));
        }

        [Test]
        public void AnchorsBindToTheRealSubjectAndAreReleasedOnDisable()
        {
            var subject = Track(new GameObject("meteorito")).transform;
            subject.position = new Vector3(12f, 3f, -4f);
            subject.rotation = Quaternion.Euler(20f, 70f, 15f);
            Assert.That(anchors.Bind("mars.cin.M20", subject), Is.True);
            var rig = anchors.Find("mars.cin.M20");
            Assert.That(rig.position, Is.EqualTo(subject.position));
            Assert.That(rig.up, Is.EqualTo(Vector3.up).Using(new UnityEngine.TestTools.Utils.Vector3EqualityComparer(1e-4f)), "o rig fica em pé");
            Assert.That(rig.eulerAngles.y, Is.EqualTo(70f).Within(0.01f));
            Assert.That(anchors.Bind("mars.cin.XXX", subject), Is.False);
            Assert.That(anchors.Bind("mars.cin.M20", null), Is.False);
            Assert.That(anchors.ValidateSetup(), Is.Empty);

            anchors.ReleaseRigs(); // em modo de edição a Unity não chama OnDisable
            Assert.That(cameraDirector.HasRig("mars.cin.M20"), Is.False, "desligar libera os rigs");
        }

        // ---------- Exposição ----------

        [Test]
        public void LightingBindingsValidateAndEveryRegionHasAProfileWithinSafeLimits()
        {
            var regions = catalog.Entries.Select(e => e.RegionId).Distinct().ToList();
            Assert.That(regions.Count, Is.EqualTo(7));
            Assert.That(lighting.Validate(regions), Is.Empty, ContractValidation.Describe(lighting.Validate(regions)));
            foreach (var p in lighting.Profiles)
            {
                Assert.That(p.sunIntensity, Is.InRange(MarsLightingBindings.MinSunIntensity, MarsLightingBindings.MaxSunIntensity), p.regionId);
                var luminance = 0.2126f * p.backgroundColor.r + 0.7152f * p.backgroundColor.g + 0.0722f * p.backgroundColor.b;
                Assert.That(luminance, Is.LessThanOrEqualTo(0.75f), $"{p.regionId}: fundo sem estourar");
            }

            Assert.That(conductor.ValidateSetup(), Is.Empty, ContractValidation.Describe(conductor.ValidateSetup()));
            var bad = UnityEngine.Object.Instantiate(lighting);
            bad.SetProfiles(new[] { new MarsLightingProfile { regionId = "mars.origin", sunIntensity = 0.01f } });
            Assert.That(bad.Validate(regions), Is.Not.Empty, "Sol apagado e regiões sem perfil são rejeitados");
            UnityEngine.Object.DestroyImmediate(bad);
        }

        // ---------- Capítulo inteiro ----------

        [Test]
        public void WholeChapterPlaysEveryCinematicOnceWithOneRegionAtATimeAndOnlySplitsRenderTwoViews()
        {
            var completed = new List<string>();
            var maxCameras = new Dictionary<string, int>();
            var lightingSeen = new Dictionary<string, float>();
            var regionViolations = new List<string>();
            sequenceDirector.Finished += r => { if (r.Reason == SequenceExitReason.Completed) completed.Add(r.SequenceId); };

            PlayChapter((step, binding) =>
            {
                var cameras = RenderedCameras();
                maxCameras[step] = Mathf.Max(maxCameras.TryGetValue(step, out var m) ? m : 0, cameras);
                if (binding.IsCinematic)
                {
                    var entry = catalog.Find(step);
                    if (conductor.ActiveRegionId != entry.RegionId)
                        regionViolations.Add($"{step}: região {conductor.ActiveRegionId}");
                    var active = new[] { origin, landing, shield, valley, spirit, memorial, eagle }.Where(g => g.activeSelf).ToList();
                    if (active.Count != 1)
                        regionViolations.Add($"{step}: {active.Count} regiões ativas");
                    lightingSeen[step] = sun.intensity;
                }
            });

            Assert.That(chapterDirector.IsFinished, Is.True, "o capítulo chega ao fim pelas sequências do catálogo");
            Assert.That(regionViolations, Is.Empty, string.Join("; ", regionViolations.Take(5)));
            var expected = catalog.Entries.OrderBy(e => e.StepId).Select(e => e.Sequence.SequenceId).ToList();
            Assert.That(completed.OrderBy(x => x), Is.EqualTo(expected.OrderBy(x => x)), "cada cinemática completa exatamente uma vez");
            Assert.That(completed.Count, Is.EqualTo(14));

            foreach (var step in catalog.Entries.Select(e => e.StepId))
            {
                var allowed = step == "M04" || step == "M05" || step == "M22" ? 2 : 1;
                Assert.That(maxCameras[step], Is.LessThanOrEqualTo(allowed), $"{step}: câmeras renderizando");
                if (allowed == 2)
                    Assert.That(maxCameras[step], Is.EqualTo(2), $"{step}: o split mostra dois pontos de vista");
                lighting.TryGet(catalog.Find(step).RegionId, out var profile);
                Assert.That(lightingSeen[step], Is.EqualTo(profile.sunIntensity).Within(1e-4f), $"{step}: exposição da região");
            }

            Assert.That(maxCameras.Where(p => !new[] { "M04", "M05", "M22" }.Contains(p.Key)).All(p => p.Value == 1), Is.True, "somente split renderiza dois pontos de vista");
            Assert.That(started.Count, Is.GreaterThanOrEqualTo(32));
            Assert.That(sun.intensity, Is.EqualTo(OriginalSun).Within(1e-4f), "no fim a exposição original voltou");
            Assert.That(cameraDirector.IsControlled, Is.False);
            Assert.That(mainCamera.backgroundColor, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f)));
        }

        // ---------- Pausa, reinício, falha ----------

        [Test]
        public void PauseFreezesAndRestartReturnsTheCameraAndTheExposure()
        {
            chapterDirector.Begin(chapter);
            var guard = 0;
            while (flow.CurrentBinding.StepId != "M20" && guard++ < 100000)
            {
                var b = flow.CurrentBinding;
                if (!b.IsCinematic)
                    foreach (var e in b.CompletionEvents)
                        flow.Raise(e);
                time.ActiveDeltaSeconds = Step;
                sequenceDirector.Tick(Step);
                LateUpdates();
                flow.Tick(Step);
            }

            Assert.That(flow.CurrentBinding.StepId, Is.EqualTo("M20"));
            for (var i = 0; i < 40; i++)
            {
                sequenceDirector.Tick(Step);
                LateUpdates();
            }

            Assert.That(sequenceDirector.IsPlaying, Is.True);
            Assert.That(cameraDirector.IsControlled, Is.True);
            var shieldProfile = lighting.Profiles.Single(p => p.regionId == MarsEditBuilder.RegionShield);
            Assert.That(sun.intensity, Is.EqualTo(shieldProfile.sunIntensity).Within(1e-4f));

            var pos = mainCamera.transform.position;
            var intensity = sun.intensity;
            sequenceDirector.Pause();
            for (var i = 0; i < 60; i++)
            {
                sequenceDirector.Tick(Step);
                LateUpdates();
            }

            Assert.That(mainCamera.transform.position, Is.EqualTo(pos), "pausa congela a câmera");
            Assert.That(sun.intensity, Is.EqualTo(intensity), "e a exposição");
            sequenceDirector.Resume();

            conductor.ReleaseCinematic();
            Assert.That(sequenceDirector.IsPlaying, Is.False, "reinício cancela a sequência");
            Assert.That(cameraDirector.IsControlled, Is.False, "câmera devolvida");
            Assert.That(sun.intensity, Is.EqualTo(OriginalSun).Within(1e-4f), "exposição restaurada");
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(new Color(0.5f, 0.5f, 0.5f)));
            Assert.That(mainCamera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
            Assert.That(mainCamera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));

            chapterDirector.RestartChapter();
            Assert.That(flow.CurrentBinding.StepId, Is.EqualTo("M01"), "o capítulo recomeça pela primeira etapa");
            Assert.That(sequenceDirector.Current.SequenceId, Is.EqualTo("mars.seq.M01"), "e toca a sequência certa");
            Assert.That(conductor.ActiveRegionId, Is.EqualTo(MarsEditBuilder.RegionOrigin));
        }

        [Test]
        public void AMissingSequenceNeverSoftLocksTheChapter()
        {
            var broken = UnityEngine.Object.Instantiate(catalog);
            broken.SetEntries(catalog.Entries.Where(e => e.StepId != "M01"));
            var so = new SerializedObject(conductor);
            so.FindProperty("catalog").objectReferenceValue = broken;
            so.ApplyModifiedPropertiesWithoutUndo();
            var failures = new List<string>();
            conductor.PlaybackFailed += (step, reason) => failures.Add(step);

            chapterDirector.Begin(chapter);
            Assert.That(failures, Is.EqualTo(new[] { "M01" }), "a falha é reportada");
            Assert.That(flow.CurrentBinding.StepId, Is.Not.EqualTo("M01"), "o capítulo avançou como salto");
            Assert.That(broken.Validate(), Is.Not.Empty, "e a validação do catálogo acusa o buraco");
            UnityEngine.Object.DestroyImmediate(broken);
        }
    }
}
