using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Abandoned.SpaceApps.SpiritInterludeChoreography;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-017: M22 (45 s, oito batidas). Prova intervalos 0–5…39–45, uma informação principal por cartão, créditos das imagens oficiais,
    /// sílica revelada por cobertura que desliza, Spirit que não volta a dirigir, datas dos marcos, split duas câmeras→uma sem snap,
    /// descarte do palco do Spirit, retorno identificado como 2018 e relógio único com pausa.
    /// </summary>
    public class MarsSpiritInterludeTests
    {
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Art/SpiritArchive/SpiritInterludeTexts.json";
        const float Step = 0.05f;

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        GameObject root;
        SpiritInterludeDirector director;
        SpiritInterludeData data;
        SequenceDefinition main, cards;
        CameraDirector cameraDirector;
        SequenceDirector sequenceDirector;
        Camera mainCamera;
        Dictionary<string, string> texts;

        [Serializable] sealed class Table { public List<Entry> texts = new List<Entry>(); }
        [Serializable] sealed class Entry { public string key; public string pt; public string en; }

        [SetUp]
        public void SetUp()
        {
            root = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SpiritInterludeBuilder.PrefabPath)));
            director = root.GetComponent<SpiritInterludeDirector>();
            data = director.Data;
            main = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SpiritInterludeBuilder.MainSequencePath);
            cards = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SpiritInterludeBuilder.CardsSequencePath);
            var host = Track(new GameObject("directors"));
            mainCamera = Track(new GameObject("camera")).AddComponent<Camera>();
            cameraDirector = host.AddComponent<CameraDirector>();
            cameraDirector.GroundProbe = _ => null;
            cameraDirector.Initialize(mainCamera);
            sequenceDirector = host.AddComponent<SequenceDirector>();
            sequenceDirector.Initialize(cameraDirector, null, null);
            director.Initialize(sequenceDirector, cameraDirector);
            texts = new Dictionary<string, string>();
            foreach (var path in new[] { TextsPath, SubtitlesPath })
                foreach (var e in JsonUtility.FromJson<Table>(File.ReadAllText(path)).texts)
                    texts[e.key] = e.pt;
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

        static IEnumerable<MarsLandingLabel> Main(IEnumerable<MarsLandingLabel> labels) => labels.Where(l => !l.TextKey.Contains(".CR"));

        // ---------- Estrutura ----------

        [Test]
        public void EightBeatsCoverFortyFiveSecondsAndTheCuesFollowTheTable()
        {
            Assert.That(BeatStarts, Is.EqualTo(new[] { 0f, 5f, 11f, 17f, 22f, 27f, 32f, 39f, 45f }));
            Assert.That(main.TotalDurationSeconds, Is.EqualTo(45f).Within(1e-4f));
            Assert.That(cards.TotalDurationSeconds, Is.EqualTo(45f).Within(1e-4f));
            Assert.That(main.SequenceId, Is.EqualTo("mars.seq.M22"));
            Assert.That(MarsChapterFlow.CreateDefaultBindings().Single(b => b.StepId == "M22").SequenceId, Is.EqualTo(main.SequenceId));
            Assert.That(cards.Shots.Count, Is.EqualTo(8));
            for (var i = 0; i < 8; i++)
                Assert.That(cards.Shots[i].DurationSeconds, Is.EqualTo(BeatStarts[i + 1] - BeatStarts[i]).Within(1e-4f), $"batida {i}");

            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            var expected = MarsChapterFlow.BuildSequenceCues(table, "M22");
            Assert.That(main.Cues.Select(c => c.CueId).OrderBy(x => x), Is.EqualTo(expected.Select(c => c.CueId).OrderBy(x => x)));
            Assert.That(main.Cues.Count, Is.EqualTo(8));
            foreach (var cue in main.Cues.OrderBy(c => c.StartSeconds).Select((c, i) => (c, i)))
            {
                Assert.That(cue.c.StartSeconds, Is.EqualTo(BeatStarts[cue.i]).Within(1e-4f), cue.c.CueId);
                Assert.That(cue.c.EndSeconds, Is.EqualTo(BeatStarts[cue.i + 1]).Within(1e-4f), cue.c.CueId);
            }

            Assert.That(main.Validate(), Is.Empty, ContractValidation.Describe(main.Validate()));
            Assert.That(sequenceDirector.Validate(main), Is.Empty);
            Assert.That(director.ValidateSetup(), Is.Empty, ContractValidation.Describe(director.ValidateSetup()));
        }

        // ---------- Informação e créditos ----------

        [Test]
        public void EveryImageBeatShowsExactlyOneMainLabelAndItsOfficialCredit()
        {
            foreach (var t in Times(0.1f, 44.9f, 0.1f))
            {
                director.ApplyAt(t);
                var main = Main(director.VisibleLabels).ToList();
                Assert.That(main.Count(l => l.Anchor == MarsLandingLabelAnchor.Full), Is.LessThanOrEqualTo(1), $"t={t}: mais de uma informação principal");
                Assert.That(main.Any(l => l.Anchor == MarsLandingLabelAnchor.Full) && main.Any(l => l.Anchor != MarsLandingLabelAnchor.Full), Is.False, $"t={t}: legenda cheia junto de legenda de metade");
                Assert.That(main.GroupBy(l => l.Anchor).All(g => g.Count() == 1), Is.True, $"t={t}: uma informação por metade");
            }

            var imageBeats = new[] { BeatHusbandHill, BeatComanche, BeatSilica, BeatHomePlate, BeatTroy };
            for (var i = 0; i < imageBeats.Length; i++)
            {
                var t = BeatStarts[imageBeats[i]] + 1f;
                director.ApplyAt(t);
                var credit = $"MAR.SPI.CR{i + 1}";
                Assert.That(director.VisibleLabels.Select(l => l.TextKey), Does.Contain(credit), $"cartão {i}: crédito visível");
                Assert.That(Main(director.VisibleLabels).Count(), Is.EqualTo(1), $"cartão {i}: uma informação principal");
            }

            // Linhas de crédito exatamente como nas páginas oficiais (ver ASSET_CREDITS.md).
            Assert.That(texts["MAR.SPI.CR1"], Is.EqualTo("NASA/JPL-Caltech/Cornell Univ."));
            Assert.That(texts["MAR.SPI.CR2"], Is.EqualTo("NASA/JPL-Caltech/Cornell University"));
            Assert.That(texts["MAR.SPI.CR3"], Is.EqualTo("NASA/JPL-Caltech/Cornell"));
            Assert.That(texts["MAR.SPI.CR4"], Is.EqualTo("NASA/JPL/Cornell"));
            Assert.That(texts["MAR.SPI.CR5"], Is.EqualTo("NASA/JPL-Caltech"));
            Assert.That(data.Validate(), Is.Empty, ContractValidation.Describe(data.Validate()));
            foreach (var label in data.Labels)
                Assert.That(texts.ContainsKey(label.TextKey), Is.True, label.TextKey);
        }

        [Test]
        public void MilestonesCarryTheSourcedDatesAndNeverClaimAnObservedFailureOrActiveVolcano()
        {
            Assert.That(data.OdometerKilometers, Is.EqualTo(7.73f));
            Assert.That(data.LastContact, Is.EqualTo(new DateTime(2010, 3, 22, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(data.SearchEnded, Is.EqualTo(new DateTime(2011, 5, 25, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(texts["MAR.SPI.09"], Does.Contain("22 MAR 2010"));
            Assert.That(texts["MAR.SPI.10"], Does.Contain("25 MAI 2011"));
            Assert.That(texts["MAR.SPI.05"], Does.Contain("7,73"));
            Assert.That(texts["MAR.SPI.11"], Does.Contain("2018"), "o retorno identifica claramente 2018");
            Assert.That(texts["MAR.SPI.12"], Is.EqualTo("SPIRIT · 2004–2010"), "a metade do Spirit escurece com o período de operação");
            director.ApplyAt(44f);
            Assert.That(director.VisibleLabels.Single(l => l.TextKey == "MAR.SPI.12").Anchor, Is.EqualTo(MarsLandingLabelAnchor.UpperHalf));
            director.ApplyAt(44f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Does.Contain("MAR.SPI.11"));
            Assert.That(data.Labels.Single(l => l.TextKey == "MAR.SPI.11").EndSeconds - data.Labels.Single(l => l.TextKey == "MAR.SPI.11").StartSeconds, Is.GreaterThanOrEqualTo(1.5f), "tempo de leitura");

            foreach (var key in texts.Keys.Where(k => k.StartsWith("MAR.SPI.")))
                Assert.That(texts[key].ToUpperInvariant(), Does.Not.Contain("ERUP").And.Not.Contain("VULC").And.Not.Contain("FALHA").And.Not.Contain("AQUECEDOR"), key);
            Assert.That(texts["MAR.SPI.04"], Does.Contain("CAMADAS"), "Home Plate mostra camadas, não vulcão ativo");
            Assert.That(root.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty);
        }

        // ---------- Sílica e Spirit parado ----------

        [Test]
        public void SilicaIsRevealedByASlidingCoverNeverByRotation()
        {
            var cover = director.SilicaCover;
            var start = BeatStarts[BeatSilica];
            director.ApplyAt(start);
            var home = cover.localPosition;
            Assert.That(SilicaReveal(start), Is.EqualTo(0f));
            var previous = 0f;
            foreach (var t in Times(start, BeatStarts[BeatSilica + 1], Step))
            {
                director.ApplyAt(t);
                var moved = cover.localPosition.x - home.x;
                Assert.That(moved, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"t={t}: só desliza num sentido");
                Assert.That(moved - previous, Is.LessThan(0.5f), $"t={t}: sem salto");
                Assert.That(cover.localRotation, Is.EqualTo(Quaternion.identity), "a cobertura não gira");
                Assert.That(cover.localPosition.y, Is.EqualTo(home.y));
                previous = moved;
            }

            Assert.That(SilicaReveal(BeatStarts[BeatSilica + 1]), Is.EqualTo(1f).Within(1e-3f), "revelada ao fim da batida");
            Assert.That(previous, Is.GreaterThan(7.5f));
        }

        [Test]
        public void SpiritNeverDrivesAgainAfterTroyAndTheOdometerOnlyGrows()
        {
            var rover = director.SpiritRover;
            director.ApplyAt(0f);
            var position = rover.localPosition;
            var rotation = rover.localRotation;
            var wheels = rover.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Wheel_")).Select(t => (t, t.localRotation)).ToList();
            Assert.That(wheels.Count, Is.GreaterThanOrEqualTo(6));
            foreach (var t in Times(0f, 44.9f, 0.25f))
            {
                director.ApplyAt(t);
                Assert.That(rover.localPosition, Is.EqualTo(position), $"t={t}: o Spirit não se move");
                Assert.That(rover.localRotation, Is.EqualTo(rotation));
                foreach (var (wheel, rot) in wheels)
                    Assert.That(wheel.localRotation, Is.EqualTo(rot), $"t={t}: roda parada");
            }

            Assert.That(rover.parent.localEulerAngles.x, Is.EqualTo(RoverTiltDegrees).Within(0.01f), "inclinada no palco do inverno");
            var previous = -1f;
            foreach (var t in Times(BeatStarts[BeatOdometer], BeatStarts[BeatTroy], Step))
            {
                director.ApplyAt(t);
                var length = director.OdometerBar.localScale.x;
                Assert.That(length, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"t={t}");
                previous = length;
            }

            Assert.That(previous, Is.EqualTo(8f).Within(0.02f), "a barra completa representa 7,73 km");
            Assert.That(OdometerAt(BeatStarts[BeatTroy]), Is.EqualTo(7.73f).Within(1e-3f));
        }

        // ---------- Câmeras e split ----------

        [Test]
        public void CardCameraKeepsTheWholeImageWidthInFrameAndNeverJumps()
        {
            for (var card = 0; card < SpiritInterludeBuilder.Cards.Length; card++)
            {
                var beat = new[] { BeatHusbandHill, BeatComanche, BeatSilica, BeatHomePlate, BeatTroy }[card];
                var shot = cards.Shots[beat];
                var width = SpiritInterludeBuilder.Cards[card].width;
                var x = card * 40f;
                CameraDirector.SampleKeyframes(shot, 0f, out var p0, out var r0, out _);
                for (var t = Step; t <= shot.DurationSeconds + 1e-4f; t += Step)
                {
                    CameraDirector.SampleKeyframes(shot, t, out var p, out var r, out var fov);
                    var distance = -p.z;
                    var visibleWidth = 2f * distance * Mathf.Tan(25f * Mathf.Deg2Rad);
                    Assert.That(p.x, Is.EqualTo(x).Within(1e-3f), $"cartão {card}: centrado");
                    Assert.That(visibleWidth, Is.GreaterThanOrEqualTo(width * 0.93f).And.LessThanOrEqualTo(width * 1.2f), $"cartão {card} t={t}: a largura inteira da imagem à vista");
                    Assert.That(Vector3.Distance(p, p0), Is.LessThan(0.5f));
                    Assert.That(Quaternion.Angle(r, r0), Is.LessThan(2f));
                    p0 = p;
                    r0 = r;
                }
            }
        }

        [Test]
        public void SplitRendersTwoCamerasThenOneGrowsWithoutSnapAndDiscardsTheSpiritStage()
        {
            director.ApplyAt(1f);
            Assert.That(director.RenderedCameraCount, Is.EqualTo(2));
            Assert.That(mainCamera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 0.5f)), "Opportunity embaixo, quieta");
            Assert.That(director.SpiritCamera.rect.yMax, Is.EqualTo(1f).Within(1e-4f), "Spirit em cima");
            foreach (var t in new[] { 10f, 30f, 42.9f })
            {
                director.ApplyAt(t);
                Assert.That(director.RenderedCameraCount, Is.EqualTo(2), $"t={t}");
            }

            var previous = 0.5f;
            foreach (var t in Times(41f, 44.99f, Step))
            {
                director.ApplyAt(t);
                Assert.That(director.OpportunityHeight - previous, Is.LessThan(0.04f).And.GreaterThanOrEqualTo(-1e-5f), $"t={t}");
                previous = director.OpportunityHeight;
            }

            director.ApplyAt(45f);
            Assert.That(director.RenderedCameraCount, Is.EqualTo(1), "Opportunity preenche a tela");
            Assert.That(mainCamera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(director.SpiritStage.gameObject.activeSelf, Is.False, "palco do Spirit descartado");
            director.ApplyAt(3f);
            Assert.That(director.SpiritStage.gameObject.activeSelf, Is.True, "reinício recria o palco");
            director.Release();
            Assert.That(director.RenderedCameraCount, Is.EqualTo(1));
        }

        [Test]
        public void SpiritDarkensOnlyAtTheEndAndOpportunityKeepsItsHorizontalFieldOfView()
        {
            Assert.That(SpiritDarkness(BeatStarts[BeatLastContact]), Is.EqualTo(0f));
            Assert.That(SpiritDarkness(44.9f), Is.GreaterThan(0.9f));
            var previous = 0f;
            foreach (var t in Times(0f, 45f, Step))
            {
                var d = SpiritDarkness(t);
                Assert.That(d, Is.GreaterThanOrEqualTo(previous - 1e-6f));
                Assert.That(d - previous, Is.LessThan(0.08f), $"t={t}: escurece sem flash");
                previous = d;
            }

            var shot = main.Shots[0];
            float Horizontal(float t)
            {
                CameraDirector.SampleKeyframes(shot, t, out _, out _, out var fov);
                return 2f * Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * OpportunityAspect(t)) * Mathf.Rad2Deg;
            }

            var reference = Horizontal(0f);
            foreach (var t in Times(0f, 45f, 0.25f))
                Assert.That(Horizontal(t), Is.EqualTo(reference).Within(1.2f), $"t={t}: enquadramento horizontal constante");
        }

        // ---------- Orçamento e relógio ----------

        [Test]
        public void OfficialImagesAreSmallEnoughForTheWebAndHaveTheirOriginalsPreserved()
        {
            foreach (var card in SpiritInterludeBuilder.Cards)
            {
                var path = SpiritInterludeBuilder.ArtPath + "/" + card.file;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture, Is.Not.Null, path);
                Assert.That(texture.width, Is.LessThanOrEqualTo(2048), path);
            }

            Assert.That(File.Exists("ArtSource/Mars/R-MAR-04/SOURCE.md"), Is.True, "URL, data e SHA-256 dos originais");
            Assert.That(Directory.GetFiles("ArtSource/Mars/R-MAR-04/original", "*.jpg").Length, Is.EqualTo(5));
            Assert.That(root.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(1), "só a câmera do Spirit; a principal é do CameraDirector");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in renderer.sharedMaterials)
                    Assert.That(m.shader.name, Does.StartWith("Universal Render Pipeline/"), renderer.name);
        }

        [Test]
        public void SequenceDirectorDrivesM22AndPauseFreezesCardsCameraAndLabels()
        {
            var lateUpdate = typeof(SpiritInterludeDirector).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            sequenceDirector.Play(main);
            var frames = 0;
            while (sequenceDirector.IsPlaying && frames++ < 2000)
            {
                sequenceDirector.Tick(Step);
                lateUpdate.Invoke(director, null);
                if (frames != 300)
                    continue;

                var cover = director.SilicaCover.localPosition;
                var camera = director.SpiritCamera.transform.position;
                var labels = director.VisibleLabels.Select(l => l.TextKey).ToArray();
                sequenceDirector.Pause();
                for (var i = 0; i < 40; i++)
                {
                    sequenceDirector.Tick(Step);
                    lateUpdate.Invoke(director, null);
                }

                Assert.That(director.SilicaCover.localPosition, Is.EqualTo(cover));
                Assert.That(director.SpiritCamera.transform.position, Is.EqualTo(camera));
                Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(labels));
                sequenceDirector.Resume();
            }

            Assert.That(sequenceDirector.IsPlaying, Is.False);
            Assert.That(cameraDirector.IsControlled, Is.False, "controle da câmera devolvido");
            Assert.That(director.RenderedCameraCount, Is.EqualTo(1));
            Assert.That(mainCamera.rect.height, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(mainCamera.rect.y, Is.EqualTo(0f).Within(1e-3f));
        }
    }
}
