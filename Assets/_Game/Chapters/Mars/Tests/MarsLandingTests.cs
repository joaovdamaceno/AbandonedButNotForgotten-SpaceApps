using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Abandoned.SpaceApps.MerLandingChoreography;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-014: pouso paralelo M04–M06. Prova comportamento (não o gosto visual): durações 18/4/9,5 s, datas e 21 dias, rover e pétalas
    /// escondidos, escudo que não some, dois impactos na elipse, câmeras acima do terreno e sem snap, split com duas câmeras e
    /// depois uma, eventos no mesmo relógio da coreografia, pausa e entrega da câmera de condução.
    /// </summary>
    public class MarsLandingTests
    {
        const string PrefabPath = MerLandingBuilder.PrefabPath;
        const string DataPath = MerLandingBuilder.DataPath;
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const string TextsPath = "Assets/_Game/Chapters/Mars/Art/Landing/MarsLandingTexts.json";
        const string RoverGameplayPath = "Assets/_Game/Chapters/Mars/Prefabs/MarsRoverGameplay.prefab";
        const float Step = 0.05f;

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        GameObject root;
        MarsDualLandingDirector director;
        MarsDualLandingData data;
        MerLandingAssembly opportunity;
        MerLandingAssembly spirit;
        SequenceDefinition split, eagle, verification;
        CameraDirector cameraDirector;
        SequenceDirector sequenceDirector;
        Camera mainCamera;

        static MerLandingSite OppSite => Abandoned.SpaceApps.Chapters.Mars.EditorTools.MerLandingBuilder.OpportunitySite;

        [SetUp]
        public void SetUp()
        {
            root = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)));
            director = root.GetComponent<MarsDualLandingDirector>();
            data = AssetDatabase.LoadAssetAtPath<MarsDualLandingData>(DataPath);
            opportunity = director.Opportunity;
            spirit = director.Spirit;
            split = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MerLandingBuilder.SequencePath("M04"));
            eagle = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MerLandingBuilder.SequencePath("M05"));
            verification = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(MerLandingBuilder.SequencePath("M06"));

            var host = Track(new GameObject("directors"));
            mainCamera = Track(new GameObject("main camera")).AddComponent<Camera>();
            cameraDirector = host.AddComponent<CameraDirector>();
            cameraDirector.GroundProbe = p => MerLandingChoreography.Ground(OppSite, p.x, p.z);
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
            {
                if (obj != null)
                    UnityEngine.Object.DestroyImmediate(obj);
            }

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

        // ---------- Dados e duração ----------

        [Test]
        public void SequencesHaveTheScriptDurationsAndValidateWithTheDirector()
        {
            Assert.That(data.Validate(), Is.Empty, ContractValidation.Describe(data.Validate()));
            Assert.That(split.TotalDurationSeconds, Is.EqualTo(18f).Within(1e-4f), "M04 = 18 s");
            Assert.That(eagle.TotalDurationSeconds, Is.EqualTo(4f).Within(1e-4f), "M05 = 4 s");
            Assert.That(verification.TotalDurationSeconds, Is.EqualTo(9.5f).Within(1e-4f), "M06 = 9,5 s (verificações, pétalas e saída do rover)");
            Assert.That(split.Shots.Count, Is.EqualTo(3), "três planos");
            Assert.That(split.Shots.Select(s => s.DurationSeconds), Is.All.EqualTo(6f).Within(1e-4f), "de seis segundos");
            Assert.That(split.SequenceId, Is.EqualTo("mars.seq.M04"));
            Assert.That(eagle.SequenceId, Is.EqualTo("mars.seq.M05"));
            Assert.That(verification.SequenceId, Is.EqualTo("mars.seq.M06"));
            foreach (var sequence in new[] { split, eagle, verification })
                Assert.That(sequence.Validate(), Is.Empty, ContractValidation.Describe(sequence.Validate()));
            foreach (var sequence in new[] { split, eagle, verification })
                Assert.That(sequenceDirector.Validate(sequence), Is.Empty, "rig registrado e sequência executável");

            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            Assert.That(chapter, Is.Not.Null);
            Assert.That(MarsChapterFlow.CreateDefaultBindings().Where(b => b.IsCinematic).Select(b => b.SequenceId),
                Is.SupersetOf(new[] { split.SequenceId, eagle.SequenceId, verification.SequenceId }), "os IDs batem com as etapas do MarsChapterFlow");
            Assert.That(director.ValidateSetup(), Is.Empty, ContractValidation.Describe(director.ValidateSetup()));
        }

        [Test]
        public void SequenceCuesComeFromTheSubtitleTableWithoutDuplicatingText()
        {
            var errors = new List<string>();
            var table = MarsChapterFlow.ParseSubtitles(File.ReadAllText(SubtitlesPath), errors);
            Assert.That(errors, Is.Empty);
            foreach (var (sequence, stepId) in new[] { (split, "M04"), (eagle, "M05"), (verification, "M06") })
            {
                var expected = MarsChapterFlow.BuildSequenceCues(table, stepId).Select(c => c.CueId).OrderBy(x => x);
                Assert.That(sequence.Cues.Select(c => c.CueId).OrderBy(x => x), Is.EqualTo(expected), stepId);
            }

            Assert.That(split.Cues.Select(c => c.CueId), Is.EquivalentTo(new[] { "MAR.C06", "MAR.C07" }));
        }

        [Test]
        public void DatesAndTheTwentyOneDayGapAreVisibleAndFollowUtc()
        {
            Assert.That(data.SpiritLanding, Is.EqualTo(new DateTime(2004, 1, 4, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(data.OpportunityLanding, Is.EqualTo(new DateTime(2004, 1, 25, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(data.Egress, Is.EqualTo(new DateTime(2004, 1, 31, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(data.DaysBetweenLandings, Is.EqualTo(21));

            var texts = LoadTexts();
            Assert.That(texts["MAR.L01"], Does.Contain("4 JAN 2004 UTC").And.Contain("SPIRIT").And.Contain("GUSEV"));
            Assert.That(texts["MAR.L02"], Does.Contain("25 JAN 2004 UTC").And.Contain("OPPORTUNITY").And.Contain("MERIDIANI"));
            Assert.That(texts["MAR.T02"], Does.Contain("21"));
            Assert.That(texts["MAR.L03"], Does.Contain("31 JAN 2004"));

            foreach (var t in new[] { 2f, 9f, 15f })
            {
                director.ApplyAt(t);
                Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.SupersetOf(new[] { "MAR.L01", "MAR.L02", "MAR.T02" }), $"t={t}: datas e intervalo visíveis no split");
            }

            director.ApplyAt(10f);
            Assert.That(director.VisibleLabels.Single(l => l.TextKey == "MAR.L01").Anchor, Is.EqualTo(MarsLandingLabelAnchor.LeftHalf), "Spirit à esquerda");
            Assert.That(director.VisibleLabels.Single(l => l.TextKey == "MAR.L02").Anchor, Is.EqualTo(MarsLandingLabelAnchor.RightHalf), "Opportunity à direita");
            director.ApplyAt(23f);
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Is.EqualTo(new[] { "MAR.L03" }), "M06 mostra só o marco de 31 JAN");

            var seen = new List<string>();
            director.LabelsChanged += labels => seen.Add(string.Join(",", labels.Select(l => l.TextKey)));
            director.ApplyAt(1f);
            director.ApplyAt(1.5f);
            Assert.That(seen.Count, Is.LessThanOrEqualTo(2), "o evento só dispara quando o conjunto muda");
        }

        static Dictionary<string, string> LoadTexts()
        {
            var result = new Dictionary<string, string>();
            foreach (var path in new[] { TextsPath, SubtitlesPath })
            {
                var table = JsonUtility.FromJson<TextTable>(File.ReadAllText(path));
                foreach (var entry in table.texts)
                {
                    Assert.That(entry.pt, Is.Not.Empty, entry.key);
                    Assert.That(entry.en, Is.Not.Empty, entry.key);
                    result[entry.key] = entry.pt;
                }
            }

            return result;
        }

        [Serializable]
        sealed class TextTable
        {
            public List<TextEntry> texts = new List<TextEntry>();
        }

        [Serializable]
        sealed class TextEntry
        {
            public string key;
            public string pt;
            public string en;
        }

        [Test]
        public void EveryLabelKeyHasPortugueseAndEnglishText()
        {
            var texts = LoadTexts();
            foreach (var label in data.Labels)
                Assert.That(texts.ContainsKey(label.TextKey), Is.True, label.TextKey);
        }

        // ---------- Mesmos movimentos, datas diferentes ----------

        [Test]
        public void SpiritAndOpportunityShareTheSameMotionAndOnlyTheGroundDiffers()
        {
            foreach (var t in Times(0f, TotalSeconds, 0.25f))
            {
                var a = Sample(t, MerLandingSite.Gusev);
                var b = Sample(t, OppSite);
                Assert.That(a.Capsule.Position.x, Is.EqualTo(b.Capsule.Position.x).Within(1e-4f), $"t={t}");
                Assert.That(a.Capsule.Position.z, Is.EqualTo(b.Capsule.Position.z).Within(1e-4f));
                Assert.That(a.Lander.Position.x, Is.EqualTo(b.Lander.Position.x).Within(1e-4f));
                Assert.That(a.Lander.Position.z, Is.EqualTo(b.Lander.Position.z).Within(1e-4f));
                Assert.That(a.AirbagInflation, Is.EqualTo(b.AirbagInflation));
                Assert.That(a.PetalOpen, Is.EqualTo(b.PetalOpen));
                var heightA = a.Lander.Position.y - ContactGround(MerLandingSite.Gusev, a.Lander.Position.x, a.Lander.Position.z);
                var heightB = b.Lander.Position.y - ContactGround(OppSite, b.Lander.Position.x, b.Lander.Position.z);
                Assert.That(heightA, Is.EqualTo(heightB).Within(1e-3f), $"t={t}: mesma altura sobre o chão local");
            }

            Assert.That(spirit.Site, Is.EqualTo(MerLandingSite.Gusev));
            Assert.That(opportunity.Site, Is.EqualTo(OppSite));
            Assert.That(Vector3.Distance(spirit.transform.position, opportunity.transform.position), Is.GreaterThan(1500f), "palcos distantes: uma câmera nunca vê o outro");
        }

        // ---------- Atores: nada vaza, nada some ----------

        [Test]
        public void FoldedRoverAndClosedPetalsStayInsideTheOuterVolumeUntilTheShieldSeparates()
        {
            foreach (var t in Times(0f, TShieldSeparation - 0.05f, 0.25f))
            {
                opportunity.Apply(t);
                var capsule = opportunity.Capsule;
                var lander = opportunity.Lander;
                foreach (var vertex in MerLandingLayout.HullVertices)
                    Assert.That(MerLandingLayout.CapsuleContains(capsule.InverseTransformPoint(lander.TransformPoint(vertex))), Is.True, $"t={t}: casco do módulo vazou da cápsula");
                foreach (var corner in opportunity.RoverBoundsCorners())
                    Assert.That(MerLandingLayout.CapsuleContains(capsule.InverseTransformPoint(corner)), Is.True, $"t={t}: rover dobrado vazou da cápsula");
            }
        }

        [Test]
        public void FoldedRoverNeverLeaksOutOfTheClosedPetals()
        {
            foreach (var t in Times(0f, TPetalsStart - 0.05f, 0.25f))
            {
                opportunity.Apply(t);
                var lander = opportunity.Lander;
                foreach (var corner in opportunity.RoverBoundsCorners())
                {
                    // Abaixo do plano da base está a própria pétala-base (sólida): só conta o que passa pelos lados ou pelo topo.
                    var local = lander.InverseTransformPoint(corner);
                    local.y = Mathf.Max(local.y, -MerLandingLayout.CentroidHeight + 0.001f);
                    Assert.That(MerLandingLayout.HullContains(local, 0.0f), Is.True, $"t={t}: rover dobrado para fora das pétalas fechadas em {local}");
                }
            }
        }

        [Test]
        public void InflatedAirbagsHideEveryClosedPetal()
        {
            var layoutPetals = MerLandingLayout.Petals();
            var points = new List<Vector3>();
            const int n = 6;
            var tri = MerLandingLayout.PetalTriangleLocal();
            // Pétalas laterais fechadas (no quadro do módulo) e a base.
            for (var p = 0; p < 3; p++)
            {
                var hinge = layoutPetals[p].HingeMidpoint;
                var rotation = layoutPetals[p].ClosedRotation;
                for (var i = 0; i <= n; i++)
                    for (var j = 0; j <= n - i; j++)
                    {
                        var a = i / (float)n;
                        var b = j / (float)n;
                        var local = tri[0] * a + tri[1] * b + tri[2] * (1f - a - b);
                        points.Add(hinge + rotation * local);
                    }
            }

            var baseTri = new[] { MerLandingLayout.VertexA, MerLandingLayout.VertexB, MerLandingLayout.VertexC };
            for (var i = 0; i <= n; i++)
                for (var j = 0; j <= n - i; j++)
                {
                    var a = i / (float)n;
                    var b = j / (float)n;
                    points.Add(baseTri[0] * a + baseTri[1] * b + baseTri[2] * (1f - a - b));
                }

            foreach (var t in new[] { TInflateEnd + 0.05f, 14f, TImpact1, 15.5f, TImpact2, TStop, 20f, TDeflateStart - 0.1f })
            {
                opportunity.Apply(t);
                for (var i = 0; i < points.Count; i++)
                {
                    var world = opportunity.Lander.TransformPoint(points[i]);
                    var hidden = false;
                    for (var k = 0; k < opportunity.Lobes.Count && !hidden; k++)
                        hidden = Vector3.Distance(world, opportunity.LobeCenter(k)) <= opportunity.LobeRadius(k) + 0.02f;
                    hidden |= Vector3.Distance(world, opportunity.Core.position) <= opportunity.Core.lossyScale.x * 0.5f + 0.02f;
                    Assert.That(hidden, Is.True, $"t={t}: ponto {i} da pétala fechada à vista");
                }
            }
        }

        [Test]
        public void HeatShieldIsNeverHiddenFallsAwayFromTheModuleAndRestsOnTheGround()
        {
            foreach (var site in new[] { MerLandingSite.Gusev, OppSite })
            {
                var assembly = site == MerLandingSite.Gusev ? spirit : opportunity;
                var previous = Vector3.zero;
                var landed = false;
                Vector3 restPosition = default;
                var flight = ShieldFlightSeconds(site);
                Assert.That(flight, Is.InRange(1f, TotalSeconds - TShieldSeparation), $"{site}: o escudo pousa antes do fim do pouso");
                foreach (var t in Times(0f, TotalSeconds, 0.02f))
                {
                    assembly.Apply(t);
                    Assert.That(assembly.HeatShield.gameObject.activeSelf, Is.True, $"{site} t={t}: escudo desapareceu");
                    Assert.That(assembly.HeatShield.GetComponent<MeshRenderer>().enabled, Is.True);
                    var position = assembly.HeatShield.localPosition;
                    if (t > 0f)
                        Assert.That(Vector3.Distance(position, previous), Is.LessThan(2.2f), $"{site} t={t}: escudo deu um salto");
                    previous = position;

                    if (t >= TShieldSeparation && t < TShieldSeparation + flight - 0.05f)
                    {
                        var distance = Vector3.Distance(position, assembly.Lander.localPosition);
                        if (t > TShieldSeparation + 0.5f)
                            Assert.That(distance, Is.GreaterThan(MerLandingLayout.LobeRadiusMeters * 2f + 2f), $"{site} t={t}: escudo passou pelo módulo");
                    }

                    if (t >= TShieldSeparation + flight + 0.05f)
                    {
                        if (!landed)
                        {
                            landed = true;
                            restPosition = position;
                            Assert.That(position.y, Is.EqualTo(ContactGround(site, position.x, position.z) + 0.55f * MerLandingLayout.HardwareScale).Within(0.01f), "repouso sobre o chão");
                        }

                        Assert.That(position, Is.EqualTo(restPosition), $"{site} t={t}: escudo no solo não se move");
                    }
                }

                Assert.That(landed, Is.True);
            }
        }

        [Test]
        public void NoActorEverTeleportsOrSpinsFasterThanTheChoreographyAllows()
        {
            foreach (var site in new[] { MerLandingSite.Gusev, OppSite })
            {
                var previous = Sample(0f, site);
                foreach (var t in Times(0.02f, TotalSeconds, 0.02f))
                {
                    var now = Sample(t, site);
                    Assert.That(Vector3.Distance(now.Capsule.Position, previous.Capsule.Position), Is.LessThan(1.6f), $"{site} t={t}: cápsula");
                    Assert.That(Vector3.Distance(now.Lander.Position, previous.Lander.Position), Is.LessThan(1.6f), $"{site} t={t}: módulo");
                    Assert.That(Quaternion.Angle(now.Lander.Rotation, previous.Lander.Rotation), Is.LessThan(13f), $"{site} t={t}: giro do módulo");
                    Assert.That(Quaternion.Angle(now.Capsule.Rotation, previous.Capsule.Rotation), Is.LessThan(8f), $"{site} t={t}: giro da cápsula");
                    Assert.That(Mathf.Abs(now.Parachute.Scale.y - previous.Parachute.Scale.y), Is.LessThan(0.12f), $"{site} t={t}: paraquedas");
                    previous = now;
                }
            }
        }

        [Test]
        public void ParachuteTetherFireballAndRocketsAppearAndDisappearOnCue()
        {
            opportunity.Apply(TChuteStart - 0.1f);
            Assert.That(opportunity.Parachute.gameObject.activeSelf, Is.False, "paraquedas guardado dentro do casco");
            opportunity.Apply(TChuteStart + 0.1f);
            Assert.That(opportunity.Parachute.gameObject.activeSelf, Is.True);
            Assert.That(opportunity.Tether.gameObject.activeSelf, Is.False);
            opportunity.Apply(TTetherStart + 0.1f);
            Assert.That(opportunity.Tether.gameObject.activeSelf, Is.True);

            var fireball = opportunity.transform.Find("Capsule/Fireball");
            opportunity.Apply(2f);
            Assert.That(fireball.gameObject.activeSelf, Is.True, "plasma na entrada");
            opportunity.Apply(TFireballOff + 0.2f);
            Assert.That(fireball.gameObject.activeSelf, Is.False, "plasma some depois da entrada");

            var flame = opportunity.transform.Find("Capsule/Retro_0");
            opportunity.Apply(13.5f);
            Assert.That(flame.gameObject.activeSelf, Is.True, "retrofoguetes acesos antes do corte");
            opportunity.Apply(TRetroEnd + 0.2f);
            Assert.That(flame.gameObject.activeSelf, Is.False);

            foreach (var t in Times(TChuteStart + 0.1f, TotalSeconds, 0.25f))
            {
                opportunity.Apply(t);
                Assert.That(opportunity.Parachute.gameObject.activeSelf, Is.True, $"t={t}: o paraquedas não some depois de aberto");
                Assert.That(opportunity.Capsule.gameObject.activeSelf, Is.True, $"t={t}: o casco não some");
            }
        }

        // ---------- Impactos, elipse, parada ----------

        [Test]
        public void TwoImpactsAndTheStopFallInsideTheExplicitEllipse()
        {
            Assert.That(InsideEllipse(CutPoint), Is.True, "ponto de corte");
            Assert.That(InsideEllipse(Impact1Point), Is.True, "primeiro impacto");
            Assert.That(InsideEllipse(Impact2Point), Is.True, "segundo impacto");
            Assert.That(InsideEllipse(StopPoint), Is.True, "parada");
            Assert.That(InsideEllipse(new Vector2(60f, 40f)), Is.False, "a elipse não é a planície inteira");

            foreach (var t in Times(TCut, TStop, 0.05f))
            {
                var p = Sample(t, OppSite).Lander.Position;
                Assert.That(InsideEllipse(new Vector2(p.x, p.z)), Is.True, $"t={t}: módulo fora da elipse");
            }

            bool Grounded(float t)
            {
                var pose = Sample(t, OppSite);
                var rest = ContactGround(OppSite, pose.Lander.Position.x, pose.Lander.Position.z) + TumbleLiftReference(t);
                return Mathf.Abs(pose.Lander.Position.y - rest) < 0.02f;
            }

            Assert.That(Grounded(TImpact1), Is.True, "toca o chão no primeiro impacto");
            Assert.That(Grounded(TImpact2), Is.True, "toca o chão no segundo impacto");
            Assert.That(Grounded(0.5f * (TImpact1 + TImpact2)), Is.False, "entre os impactos está no ar (quique)");
            Assert.That(Grounded(0.5f * (TCut + TImpact1)), Is.False, "entre o corte e o primeiro impacto cai");
            var apex = Sample(0.5f * (TImpact1 + TImpact2), OppSite).Lander.Position.y
                - ContactGround(OppSite, Sample(0.5f * (TImpact1 + TImpact2), OppSite).Lander.Position.x, Sample(0.5f * (TImpact1 + TImpact2), OppSite).Lander.Position.z)
                - TumbleLiftReference(0.5f * (TImpact1 + TImpact2));
            Assert.That(apex, Is.EqualTo(FirstBounceApexMeters).Within(0.05f), "altura do quique explícita");

            var stopPose = Sample(TStop, OppSite).Lander;
            foreach (var t in Times(TStop, TDeflateStart - 0.1f, 0.25f))
            {
                var pose = Sample(t, OppSite).Lander;
                Assert.That(pose.Position, Is.EqualTo(stopPose.Position), $"t={t}: parado");
                Assert.That(Quaternion.Angle(pose.Rotation, stopPose.Rotation), Is.LessThan(0.01f), $"t={t}: parado e de pé");
            }

            Assert.That(Quaternion.Angle(stopPose.Rotation, Quaternion.identity), Is.LessThan(0.01f), "termina com a base para baixo");
            Assert.That(stopPose.Position.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(stopPose.Position.z, Is.EqualTo(0f).Within(1e-4f));
        }

        static float TumbleLiftReference(float t)
        {
            var pose = Sample(t, OppSite);
            // Raio de contato = altura do baricentro sobre o chão quando há contato.
            var settle = Smooth((t - TImpact2) / (TStop - TImpact2));
            var inflated = Mathf.Lerp(MerLandingLayout.TumbleContactRadius(), MerLandingLayout.RestCenterHeightInflated, settle);
            return Mathf.Lerp(inflated, MerLandingLayout.RestCenterHeightDeflated, pose.AirbagDeflation);
        }

        [Test]
        public void NoAirbagLobeEverSinksBelowTheGround()
        {
            foreach (var t in Times(TImpact1, TotalSeconds, 0.1f))
            {
                opportunity.Apply(t);
                for (var i = 0; i < opportunity.Lobes.Count; i++)
                {
                    var center = opportunity.LobeCenter(i);
                    var bottom = center.y - opportunity.Lobes[i].lossyScale.y * 0.5f;
                    var ground = MerLandingChoreography.Ground(OppSite, center.x, center.z);
                    Assert.That(bottom, Is.GreaterThanOrEqualTo(ground - 0.7f * MerLandingLayout.HardwareScale), $"t={t}: lobo {i} enterrado");
                }
            }
        }

        // ---------- Desinflar, pétalas, rover ----------

        [Test]
        public void AirbagsDeflateContinuouslyAndPetalsOpenOnlyAfterTheStop()
        {
            Assert.That(TPetalsStart, Is.GreaterThan(TStop), "pétalas só depois da parada");
            Assert.That(TDeflateStart, Is.GreaterThan(TStop));
            Assert.That(TPetalsStart, Is.GreaterThan(TDeflateStart), "o desinflar já começou quando as pétalas abrem");

            foreach (var t in Times(0f, TPetalsStart, 0.1f))
            {
                opportunity.Apply(t);
                foreach (var petal in opportunity.Petals)
                    Assert.That(Quaternion.Angle(petal.localRotation, ClosedRotationOf(petal)), Is.LessThan(0.01f), $"t={t}: pétala aberta antes da hora");
            }

            var previousScale = float.MaxValue;
            var previousSpread = 0f;
            var previousOpen = 0f;
            foreach (var t in Times(TDeflateStart - 0.2f, TotalSeconds, Step))
            {
                opportunity.Apply(t);
                var scale = opportunity.Lobes[0].localScale.y;
                var spread = opportunity.Lobes[0].localPosition.magnitude;
                Assert.That(scale, Is.LessThanOrEqualTo(previousScale + 1e-4f), $"t={t}: o airbag cresce de novo");
                Assert.That(Mathf.Abs(scale - (previousScale == float.MaxValue ? scale : previousScale)), Is.LessThan(0.2f), $"t={t}: escala saltou");
                Assert.That(Mathf.Abs(spread - previousSpread), Is.LessThan(0.4f).Or.EqualTo(Mathf.Abs(spread)), $"t={t}: forma saltou");
                previousScale = scale;
                previousSpread = spread;

                var open = Quaternion.Angle(opportunity.Petals[0].localRotation, ClosedRotationOf(opportunity.Petals[0]));
                Assert.That(open, Is.GreaterThanOrEqualTo(previousOpen - 1e-3f), $"t={t}: pétala fecha de novo");
                Assert.That(open - previousOpen, Is.LessThan(8f), $"t={t}: pétala deu um salto");
                previousOpen = open;
            }

            Assert.That(previousOpen, Is.EqualTo(MerLandingLayout.PetalOpenDegrees).Within(0.5f), "pétalas abertas ao fim");
            Assert.That(opportunity.Lobes[0].localScale.y / opportunity.Lobes[0].localScale.x, Is.LessThan(0.2f), "airbag murcho: forma achatada, não só menor");
        }

        static Quaternion ClosedRotationOf(Transform petal)
        {
            var layout = MerLandingLayout.Petals();
            var index = petal.name.EndsWith("Front") ? 0 : petal.name.EndsWith("BackLeft") ? 1 : 2;
            return layout[index].ClosedRotation;
        }

        [Test]
        public void RoverUnfoldsAfterThePetalsStartAndEndsAtFullSizeUpright()
        {
            Assert.That(TUnfoldStart, Is.GreaterThan(TPetalsStart));
            opportunity.Apply(TUnfoldStart - 0.1f);
            Assert.That(opportunity.RoverRoot.localScale.x, Is.EqualTo(MerLandingLayout.StowedRoverScale).Within(1e-4f));
            var previous = opportunity.RoverRoot.localScale.x;
            foreach (var t in Times(TUnfoldStart, TUnfoldEnd, Step))
            {
                opportunity.Apply(t);
                var scale = opportunity.RoverRoot.localScale.x;
                Assert.That(scale, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"t={t}");
                Assert.That(scale - previous, Is.LessThan(0.05f), $"t={t}: crescimento sem salto");
                previous = scale;
            }

            opportunity.Apply(TUnfoldEnd);
            Assert.That(opportunity.RoverRoot.localScale.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(opportunity.RoverRoot.up.y, Is.GreaterThan(0.999f), "rover de pé");
            Assert.That(opportunity.RoverRoot.forward.z, Is.GreaterThan(0.999f), "frente em +Z, para a rampa");
            Assert.That(opportunity.MastHead.localRotation, Is.EqualTo(Quaternion.identity), "rig do rover na pose de repouso");
            Assert.That(opportunity.RockerLeft.localRotation, Is.EqualTo(Quaternion.identity));
        }

        // ---------- Câmeras ----------

        [Test]
        public void EveryCameraSampleStaysAboveTheTerrain()
        {
            foreach (var sequence in new[] { split, eagle, verification })
            {
                foreach (var shot in sequence.Shots)
                {
                    foreach (var t in Times(0f, shot.DurationSeconds, 0.02f))
                    {
                        CameraDirector.SampleKeyframes(shot, t, out var position, out _, out _);
                        var ground = MerLandingChoreography.Ground(OppSite, position.x, position.z);
                        Assert.That(position.y, Is.GreaterThanOrEqualTo(ground + shot.MinGroundClearanceMeters - 0.02f),
                            $"{shot.ShotId} t={t}: câmera a {position.y - ground:0.00} m do terreno");
                    }
                }
            }
        }

        [Test]
        public void TheLowResolutionColliderAgreesWithTheVisibleTerrainSoTheDirectorsGroundClampWorks()
        {
            Physics.SyncTransforms();
            var ground = opportunity.transform.Find("Ground").GetComponent<MeshCollider>();
            Assert.That(ground.sharedMesh.triangles.Length / 3, Is.LessThan(12000), "colisão leve");
            foreach (var xz in new[] { new Vector2(0f, 0f), new Vector2(4f, 2f), new Vector2(9f, 0f), new Vector2(-30f, -40f), new Vector2(60f, 90f) })
            {
                var origin = new Vector3(xz.x, 50f, xz.y);
                Assert.That(ground.Raycast(new Ray(origin, Vector3.down), out var hit, 200f), Is.True);
                Assert.That(hit.point.y, Is.EqualTo(MerLandingChoreography.Ground(OppSite, xz.x, xz.y)).Within(0.45f), $"({xz.x},{xz.y})");
            }
        }

        [Test]
        public void CameraPathsAreContinuousWithinEachShotAndAcrossSequences()
        {
            foreach (var sequence in new[] { split, eagle, verification })
            {
                foreach (var shot in sequence.Shots)
                {
                    CameraDirector.SampleKeyframes(shot, 0f, out var p0, out var r0, out _);
                    for (var t = Step; t <= shot.DurationSeconds + 1e-4f; t += Step)
                    {
                        CameraDirector.SampleKeyframes(shot, t, out var p1, out var r1, out _);
                        Assert.That(Vector3.Distance(p0, p1), Is.LessThan(3.2f * MerLandingLayout.HardwareScale / 1.6f), $"{shot.ShotId} t={t}: câmera pulou");
                        Assert.That(Quaternion.Angle(r0, r1), Is.LessThan(6f), $"{shot.ShotId} t={t}: câmera girou de uma vez");
                        p0 = p1;
                        r0 = r1;
                    }
                }
            }

            ShotDefinition Last(SequenceDefinition s) => s.Shots[s.Shots.Count - 1];
            void Match(ShotDefinition from, ShotDefinition to, string label)
            {
                CameraDirector.SampleKeyframes(from, from.DurationSeconds, out var pa, out var ra, out var fa);
                CameraDirector.SampleKeyframes(to, 0f, out var pb, out var rb, out var fb);
                Assert.That(Vector3.Distance(pa, pb), Is.LessThan(0.02f), label + ": posição");
                Assert.That(Quaternion.Angle(ra, rb), Is.LessThan(0.5f), label + ": rotação");
                Assert.That(fa, Is.EqualTo(fb).Within(0.1f), label + ": FOV");
            }

            Match(Last(split), eagle.Shots[0], "M04→M05");
            Match(Last(eagle), verification.Shots[0], "M05→M06");
            Assert.That(eagle.Shots[0].ContinueFromPreviousShot && verification.Shots[0].ContinueFromPreviousShot, Is.True, "continuidade declarada: sem snap");
        }

        [Test]
        public void ShotsChangeSubjectScaleOrActionAtEveryCut()
        {
            Vector3 SubjectAt(string shotId)
            {
                var shot = split.Shots.Single(s => s.ShotId == shotId);
                CameraDirector.SampleKeyframes(shot, 3f, out var position, out var rotation, out _);
                return position;
            }

            var entry = split.Shots[0];
            var chute = split.Shots[1];
            var airbags = split.Shots[2];
            Assert.That(new[] { entry.RigId, chute.RigId, airbags.RigId }, Is.All.EqualTo(MarsDualLandingDirector.OpportunityRigId));
            var heights = split.Shots.Select(s =>
            {
                CameraDirector.SampleKeyframes(s, s.DurationSeconds * 0.5f, out var p, out _, out _);
                return p.y;
            }).ToArray();
            Assert.That(heights[0], Is.GreaterThan(heights[1] + 30f), "entrada → paraquedas: altitude muda");
            Assert.That(heights[1], Is.GreaterThan(heights[2] + 20f), "paraquedas → airbags: câmera desce ao nível do chão");
            Assert.That(SubjectAt("mars.landing.entry"), Is.Not.EqualTo(SubjectAt("mars.landing.airbags")));
        }

        // ---------- Split: duas câmeras, depois uma ----------

        [Test]
        public void SplitRendersTwoCamerasThenOneAndOpportunityExpandsWithoutSnap()
        {
            mainCamera.enabled = true;
            director.ApplyAt(5f);
            Assert.That(director.RenderedCameraCount, Is.EqualTo(2), "split: duas renderizações");
            Assert.That(mainCamera.rect, Is.EqualTo(new Rect(0.5f, 0f, 0.5f, 1f)), "Opportunity à direita");
            Assert.That(director.SpiritCamera.rect.x, Is.EqualTo(0f).Within(1e-4f), "Spirit à esquerda");
            Assert.That(director.SpiritCamera.rect.xMax, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(director.IsSplitScreen, Is.True, "a UI desenha a linha divisória");
            Assert.That(director.DividerX, Is.EqualTo(0.5f).Within(1e-4f), "linha no meio");

            foreach (var t in new[] { 0f, 9f, 17.99f, 18.5f, 20f, 21.9f })
            {
                director.ApplyAt(t);
                Assert.That(director.RenderedCameraCount, Is.EqualTo(2), $"t={t}: split continua com duas câmeras");
            }

            director.ApplyAt(22f);
            Assert.That(director.RenderedCameraCount, Is.EqualTo(1), "depois do split, uma renderização");
            Assert.That(mainCamera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(spirit.gameObject.activeSelf, Is.False, "palco do Spirit descartado");
            director.ApplyAt(25f);
            Assert.That(director.RenderedCameraCount, Is.EqualTo(1));

            var previous = 0.5f;
            foreach (var t in Times(17f, 23f, Step))
            {
                director.ApplyAt(t);
                var height = director.OpportunityWidth;
                Assert.That(height, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"t={t}");
                Assert.That(height - previous, Is.LessThan(0.035f), $"t={t}: a metade da direita cresce sem snap");
                Assert.That(director.SpiritCamera.rect.xMax, Is.EqualTo(mainCamera.rect.x).Within(0.0003f), "as duas metades se encostam na linha");
                previous = height;
            }

            Assert.That(previous, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void HorizontalFieldOfViewIsConstantWhileTheRightHalfGrows()
        {
            var shot = eagle.Shots[0];
            float Horizontal(float t)
            {
                CameraDirector.SampleKeyframes(shot, t, out _, out _, out var fov);
                var aspect = MerLandingCameraPath.AspectAt(MarsDualLandingTimes.SplitEnd + t);
                return 2f * Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
            }

            var reference = Horizontal(0f);
            foreach (var t in Times(0f, shot.DurationSeconds, 0.1f))
                Assert.That(Horizontal(t), Is.EqualTo(reference).Within(1.0f), $"t={t}: o enquadramento horizontal não pode saltar quando a metade inferior cresce");
        }

        static class MarsDualLandingTimes
        {
            public const float SplitEnd = SplitSeconds;
        }

        [Test]
        public void SpiritCameraRunsTheSameShotsInItsOwnStage()
        {
            foreach (var t in new[] { 1f, 4.5f, 8f, 10.5f, 13f, 16f, 19f })
            {
                director.ApplyAt(t);
                var sequence = t < SplitSeconds ? split : eagle;
                var local = t < SplitSeconds ? t : t - SplitSeconds;
                var start = 0f;
                ShotDefinition shot = null;
                foreach (var candidate in sequence.Shots)
                {
                    if (local <= start + candidate.DurationSeconds + 1e-4f)
                    {
                        shot = candidate;
                        break;
                    }

                    start += candidate.DurationSeconds;
                }

                CameraDirector.SampleKeyframes(shot, local - start, out var expectedLocal, out var expectedRotation, out var fov);
                var spiritLocal = spirit.transform.InverseTransformPoint(director.SpiritCamera.transform.position);
                Assert.That(Vector3.Distance(spiritLocal, expectedLocal), Is.LessThan(0.05f), $"t={t}: mesmo plano, outro palco");
                Assert.That(director.SpiritCamera.fieldOfView, Is.EqualTo(fov).Within(1e-3f));
                Assert.That(Quaternion.Angle(spirit.transform.rotation * expectedRotation, director.SpiritCamera.transform.rotation), Is.LessThan(0.1f));
            }

            Assert.That(director.SpiritCamera.GetComponent<AudioListener>(), Is.Null, "um só AudioListener (CORE-004)");
            Assert.That(director.SpiritCamera.CompareTag("MainCamera"), Is.False);
        }

        // ---------- Reinício, descarte e pausa ----------

        [Test]
        public void RestartRebuildsTheSplitAndReleaseHandsTheScreenBack()
        {
            director.ApplyAt(25f);
            Assert.That(spirit.gameObject.activeSelf, Is.False);
            director.ApplyAt(2f);
            Assert.That(spirit.gameObject.activeSelf, Is.True, "reinício recria o palco do Spirit");
            Assert.That(mainCamera.rect.width, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(director.VisibleLabels.Select(l => l.TextKey), Does.Contain("MAR.L01"));
            var again = Sample(2f, OppSite);
            Assert.That(opportunity.Lander.localPosition, Is.EqualTo(again.Lander.Position), "mesmo estado depois de voltar no tempo");

            director.Release();
            Assert.That(mainCamera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(director.RenderedCameraCount, Is.EqualTo(1));
            Assert.That(spirit.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SequenceDirectorDrivesTheLandingAndPauseFreezesEverythingTogether()
        {
            var log = new List<(string id, float time)>();
            sequenceDirector.EventRaised += id => log.Add((id, sequenceDirector.TimeSeconds + data.OffsetOf(sequenceDirector.Current.SequenceId)));
            var lateUpdate = typeof(MarsDualLandingDirector).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

            sequenceDirector.Play(split);
            var frames = 0;
            while (sequenceDirector.IsPlaying && frames++ < 2000)
            {
                sequenceDirector.Tick(Step);
                lateUpdate.Invoke(director, null);
                if (frames == 120)
                {
                    var landerBefore = opportunity.Lander.localPosition;
                    var cameraBefore = mainCamera.transform.position;
                    var spiritBefore = director.SpiritCamera.transform.position;
                    var widthBefore = mainCamera.rect.width;
                    sequenceDirector.Pause();
                    for (var i = 0; i < 40; i++)
                    {
                        sequenceDirector.Tick(Step);
                        lateUpdate.Invoke(director, null);
                    }

                    Assert.That(opportunity.Lander.localPosition, Is.EqualTo(landerBefore), "pausa congela o módulo");
                    Assert.That(mainCamera.transform.position, Is.EqualTo(cameraBefore), "e a câmera principal");
                    Assert.That(director.SpiritCamera.transform.position, Is.EqualTo(spiritBefore), "e a câmera do Spirit");
                    Assert.That(mainCamera.rect.width, Is.EqualTo(widthBefore));
                    sequenceDirector.Resume();
                }
            }

            Assert.That(sequenceDirector.IsPlaying, Is.False);
            lateUpdate.Invoke(director, null);
            sequenceDirector.Play(eagle);
            while (sequenceDirector.IsPlaying && frames++ < 4000)
            {
                sequenceDirector.Tick(Step);
                lateUpdate.Invoke(director, null);
            }

            sequenceDirector.Play(verification);
            while (sequenceDirector.IsPlaying && frames++ < 6000)
            {
                sequenceDirector.Tick(Step);
                lateUpdate.Invoke(director, null);
            }

            var expected = new Dictionary<string, float>
            {
                ["mars.landing.entry.fireball"] = TFireballOn,
                ["mars.landing.chute.deploy"] = TChuteStart,
                ["mars.landing.heatshield.separate"] = TShieldSeparation,
                ["mars.landing.tether.deploy"] = TTetherStart,
                ["mars.landing.airbags.inflate"] = TInflateStart,
                ["mars.landing.retro.fire"] = TRetroStart,
                ["mars.landing.tether.cut"] = TCut,
                ["mars.landing.impact.first"] = TImpact1,
                ["mars.landing.impact.second"] = TImpact2,
                ["mars.landing.stop"] = TStop,
                ["mars.landing.airbags.deflate"] = TDeflateStart,
                ["mars.landing.petals.open"] = TPetalsStart,
                ["mars.landing.rover.unfold"] = TUnfoldStart,
            };
            foreach (var pair in expected)
            {
                var hits = log.Where(e => e.id == pair.Key).ToList();
                Assert.That(hits.Count, Is.EqualTo(1), pair.Key + " dispara uma única vez");
                Assert.That(hits[0].time, Is.EqualTo(pair.Value).Within(Step + 1e-3f), pair.Key + ": evento no mesmo relógio da coreografia");
            }

            Assert.That(log.Select(e => e.id).Contains("mars.landing.handoff"), Is.True);
            var order = log.Select(e => e.time).ToList();
            Assert.That(order, Is.Ordered, "eventos em ordem");
            Assert.That(mainCamera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), "termina com uma câmera em tela cheia");
            Assert.That(cameraDirector.IsControlled, Is.False, "controle da câmera devolvido");
        }

        // ---------- Entrega do controle ----------

        [Test]
        public void TheLastCameraKeyframeIsTheDrivingCameraOfTheRoverController()
        {
            var gameplay = AssetDatabase.LoadAssetAtPath<GameObject>(RoverGameplayPath);
            var rover = gameplay.GetComponentInChildren<MarsRoverController>(true);
            var serialized = new SerializedObject(rover);
            Assert.That(data.GameplayFollowDistanceMeters, Is.EqualTo(serialized.FindProperty("followDistanceMeters").floatValue), "mesma distância do controlador");
            Assert.That(data.GameplayPivotHeightMeters, Is.EqualTo(serialized.FindProperty("pivotHeightMeters").floatValue));
            Assert.That(data.GameplayPitchDegrees, Is.EqualTo(serialized.FindProperty("defaultPitchDegrees").floatValue));

            opportunity.Apply(TotalSeconds);
            var shot = verification.Shots[verification.Shots.Count - 1];
            CameraDirector.SampleKeyframes(shot, shot.DurationSeconds, out var position, out var rotation, out var fov);

            var instance = Track((GameObject)PrefabUtility.InstantiatePrefab(gameplay));
            var controller = instance.GetComponentInChildren<MarsRoverController>(true);
            controller.transform.SetPositionAndRotation(opportunity.RoverRoot.position, Quaternion.identity);
            var ideal = controller.ComputeIdealCameraPose(data.GameplayFieldOfViewDegrees);
            var idealPitched = Quaternion.Euler(data.GameplayPitchDegrees, 0f, 0f);
            var pivot = opportunity.RoverRoot.position + Vector3.up * data.GameplayPivotHeightMeters;
            var expected = pivot - idealPitched * Vector3.forward * data.GameplayFollowDistanceMeters;

            Assert.That(Vector3.Distance(position, expected), Is.LessThan(0.02f), "a câmera termina onde a câmera de condução começa");
            Assert.That(Quaternion.Angle(rotation, idealPitched), Is.LessThan(0.5f));
            Assert.That(fov, Is.EqualTo(data.GameplayFieldOfViewDegrees).Within(0.01f));
            Assert.That(Vector3.Distance(ideal.Position, expected) , Is.LessThan(MaxIdealDifference(controller, ideal, expected)), "o controlador usa a mesma fórmula");
        }

        static float MaxIdealDifference(MarsRoverController controller, CameraPose ideal, Vector3 expected)
        {
            // Em modo de edição OnEnable não roda e o pitch do controlador é 0: a diferença esperada vem só desse ângulo inicial.
            var pitch = typeof(MarsRoverController).GetField("orbitPitchDegrees", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            return Mathf.Approximately((float)pitch, 16f) ? 0.02f : 6f;
        }

        // ---------- Prefab ----------

        [Test]
        public void PrefabIsWiredCheapAndSharesMeshesBetweenTheTwinStages()
        {
            Assert.That(opportunity.ValidateSetup(), Is.Empty, ContractValidation.Describe(opportunity.ValidateSetup()));
            Assert.That(spirit.ValidateSetup(), Is.Empty, ContractValidation.Describe(spirit.ValidateSetup()));
            Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty, "uma única luz com sombra, da cena");
            Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty, "a coreografia é cinemática: sem física");
            Assert.That(root.GetComponentsInChildren<Collider>(true).Select(c => c.name), Is.All.EqualTo("Ground"), "só o terreno tem colisão");
            Assert.That(opportunity.Lobes.Count, Is.EqualTo(28));

            foreach (var stage in new[] { spirit, opportunity })
            {
                var triangles = 0;
                var renderers = stage.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var filter in stage.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null)
                        triangles += filter.sharedMesh.GetIndexCount(0) > 0 && filter.sharedMesh.GetTopology(0) == MeshTopology.Triangles ? (int)filter.sharedMesh.GetIndexCount(0) / 3 : 0;

                Assert.That(triangles, Is.LessThan(200000), stage.name + ": triângulos");
                Assert.That(renderers.Length, Is.LessThan(120), stage.name + ": renderers");
                foreach (var renderer in renderers.Where(r => !r.transform.IsChildOf(stage.RoverRoot)))
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material.shader.name, Does.StartWith("Universal Render Pipeline/"), renderer.name);
                        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null)
                            Assert.That(material.GetTexture("_BaseMap").width, Is.LessThanOrEqualTo(512), renderer.name);
                    }
            }

            Assert.That(spirit.Lobes[0].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(opportunity.Lobes[0].GetComponent<MeshFilter>().sharedMesh), "malhas compartilhadas");
            Assert.That(spirit.Capsule.Find("Backshell").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(opportunity.Capsule.Find("Backshell").GetComponent<MeshFilter>().sharedMesh));
            Assert.That(spirit.RoverRoot.name, Is.EqualTo("Rover"));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(spirit.RoverRoot.gameObject), Does.EndWith("SpiritVisual.prefab"), "cada gêmea usa o seu prefab da MAR-011");
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(opportunity.RoverRoot.gameObject), Does.EndWith("OpportunityVisual.prefab"));
        }
    }
}
