using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Abandoned.SpaceApps.Chapters.Mars.EditorTools;
using Abandoned.SpaceApps.Examples;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using G = Abandoned.SpaceApps.Chapters.Mars.EditorTools.LateMarsGeometry;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// MAR-016: Victoria (2007) e Perseverance Valley (2018). Prova: formas conforme as fontes lidas (borda recortada e ~800 m;
    /// declive de 15–17° e canais rasos), âncoras a poucos metros do rover, perfil por evento com limites que preservam o rover, o
    /// Sol e a ciência, poeira que nunca salta (sem flash), recuperação em 2007 e fim sem sinal em 2018, teto de partículas e
    /// perfil baixo, tremor reduzível, e os RenderSettings devolvidos. Não avalia o gosto visual.
    /// </summary>
    public class MarsLateMarsTests
    {
        const string VictoriaPath = "Assets/_Game/Chapters/Mars/Prefabs/VictoriaStage.prefab";
        const string ValleyPath = "Assets/_Game/Chapters/Mars/Prefabs/PerseveranceValleyStage.prefab";
        const string ProfilePath = "Assets/_Game/Chapters/Mars/Art/LateMars/MarsStormProfile.asset";
        const string StormPath = "Assets/_Game/Chapters/Mars/Prefabs/Storm2007Controls.prefab";
        const string RoverPath = "Assets/_Game/Chapters/Mars/Prefabs/OpportunityVisual.prefab";
        const string ChapterPath = "Assets/_Game/Chapters/Mars/Data/MarsChapter.asset";
        const string SubtitlesPath = "Assets/_Game/Chapters/Mars/Data/MarsSubtitles.json";
        const float Frame = 0.05f;

        sealed class FakeTime : IActiveTimeSource
        {
            public float ActiveDeltaSeconds { get; set; }
        }

        readonly List<GameObject> hosts = new List<GameObject>();
        MarsStormProfileData profile;
        GameObject victoria;
        GameObject valley;
        FakeTime time;
        bool fogWas;
        float fogStartWas, fogEndWas;
        Color fogColorWas, ambientWas;
        FogMode fogModeWas;
        AmbientMode ambientModeWas;

        [SetUp]
        public void SetUp()
        {
            fogWas = RenderSettings.fog;
            fogModeWas = RenderSettings.fogMode;
            fogColorWas = RenderSettings.fogColor;
            fogStartWas = RenderSettings.fogStartDistance;
            fogEndWas = RenderSettings.fogEndDistance;
            ambientModeWas = RenderSettings.ambientMode;
            ambientWas = RenderSettings.ambientLight;

            profile = AssetDatabase.LoadAssetAtPath<MarsStormProfileData>(ProfilePath);
            Assert.That(profile, Is.Not.Null, ProfilePath);
            victoria = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VictoriaPath)));
            valley = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ValleyPath)));
            valley.transform.position = new Vector3(5000f, 0f, 0f);
            time = new FakeTime();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var host in hosts)
            {
                if (host != null)
                    UnityEngine.Object.DestroyImmediate(host);
            }

            hosts.Clear();
            RenderSettings.fog = fogWas;
            RenderSettings.fogMode = fogModeWas;
            RenderSettings.fogColor = fogColorWas;
            RenderSettings.fogStartDistance = fogStartWas;
            RenderSettings.fogEndDistance = fogEndWas;
            RenderSettings.ambientMode = ambientModeWas;
            RenderSettings.ambientLight = ambientWas;
        }

        GameObject Track(GameObject go)
        {
            hosts.Add(go);
            return go;
        }

        MarsAtmosphereController Controller(GameObject stage, Light sun = null, Camera sky = null)
        {
            var controller = stage.GetComponent<MarsAtmosphereController>();
            controller.Initialize(null, null, time, sun, sky);
            return controller;
        }

        // ---------- Formas conforme as fontes ----------

        [Test]
        public void VictoriaIsAnEightHundredMetreCraterWithCapesAndBaysAndTheRoverSitsOnACape()
        {
            Assert.That(G.VictoriaRadius * 2f, Is.EqualTo(800f), "~800 m de diâmetro (JPL)");
            var rimMin = float.MaxValue;
            var rimMax = 0f;
            var capeGradient = 0f;
            var bayGradient = float.MaxValue;
            for (var i = 0; i < 360; i++)
            {
                var theta = i * Mathf.Deg2Rad;
                var rim = G.VictoriaRim(theta);
                rimMin = Mathf.Min(rimMin, rim);
                rimMax = Mathf.Max(rimMax, rim);
                // Inclinação máxima na descida da borda (a 25 m em torno do raio): penhasco nos promontórios, suave nas baías.
                var steepest = 0f;
                for (var d = -25f; d < 25f; d += 2f)
                {
                    var a = G.VictoriaCenter + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * (rim + d);
                    var b = G.VictoriaCenter + new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * (rim + d + 2f);
                    steepest = Mathf.Max(steepest, Mathf.Abs(G.VictoriaHeight(a.x, a.y) - G.VictoriaHeight(b.x, b.y)) / 2f);
                }

                var scallop = G.Scallop(theta);
                if (scallop < -0.5f)
                    capeGradient = Mathf.Max(capeGradient, Mathf.Atan(steepest) * Mathf.Rad2Deg);
                else if (scallop > 0.6f)
                    bayGradient = Mathf.Min(bayGradient, Mathf.Atan(steepest) * Mathf.Rad2Deg);
            }

            Assert.That(rimMax - rimMin, Is.GreaterThan(30f), "borda recortada, não um círculo perfeito");
            Assert.That((rimMin + rimMax) * 0.5f, Is.InRange(380f, 420f));
            Assert.That(capeGradient, Is.GreaterThan(30f), "os promontórios caem como penhasco");
            Assert.That(bayGradient, Is.LessThan(15f), "as baías descem suaves");
            Assert.That(G.VictoriaHeight(G.VictoriaCenter.x, G.VictoriaCenter.y), Is.LessThan(-G.VictoriaDepth * 0.9f), "o fundo está bem abaixo da borda");

            var toCrater = G.VictoriaCenter.magnitude;
            var rimTowardRover = G.VictoriaRim(Mathf.Atan2(-G.VictoriaCenter.y, -G.VictoriaCenter.x));
            Assert.That(toCrater - rimTowardRover, Is.EqualTo(G.RimDistanceFromRover).Within(0.5f), "o rover fica a poucos metros da borda");
            Assert.That(G.Scallop(Mathf.Atan2(-G.VictoriaCenter.y, -G.VictoriaCenter.x)), Is.LessThan(-0.35f), "e num promontório (cape)");
        }

        [Test]
        public void ValleyDropsAboutSixteenDegreesWithShallowChannelsInABroadNotch()
        {
            // Inclinação ao longo do declive, fora dos canais (x = -22 m) e dentro do alcance dos "dois campos de futebol" (~180 m).
            var angles = new List<float>();
            for (var z = 40f; z <= 130f; z += 15f)
            {
                var drop = G.ValleyHeight(-22f, z) - G.ValleyHeight(-22f, z + 10f);
                angles.Add(Mathf.Atan2(drop, 10f) * Mathf.Rad2Deg);
            }

            Assert.That(angles.Average(), Is.InRange(14f, 18f), "declive de ~15–17° (JPL, 15/5/2017)");
            Assert.That(G.ValleyHeight(0f, 0f) - G.ValleyHeight(0f, 190f), Is.InRange(40f, 60f), "cerca de 50 m de desnível em ~190 m");

            // Canais rasos: o fundo do canal fica abaixo das margens por poucos decímetros.
            for (var i = 0; i < G.ChannelOffsets.Length; i++)
            {
                var z = 70f;
                var cx = G.ChannelCenter(i, z);
                var bed = G.ValleyHeight(cx, z);
                var bank = 0.5f * (G.ValleyHeight(cx - 2.6f, z) + G.ValleyHeight(cx + 2.6f, z));
                Assert.That(bank - bed, Is.InRange(0.08f, 0.4f), $"canal {i}: raso (decímetros)");
            }

            Assert.That(G.ValleyHeight(-120f, 20f) - G.ValleyHeight(0f, 20f), Is.GreaterThan(8f), "o entalhe tem paredes laterais altas: é um entalhe amplo na crista");
        }

        // ---------- Prefabs ----------

        [Test]
        public void StagesAreWiredWithFogCapableShadersNoLightsAndBudgetedMeshes()
        {
            foreach (var stage in new[] { victoria, valley })
            {
                var anchors = stage.GetComponent<LateMarsStageAnchors>();
                Assert.That(anchors.ValidateSetup(), Is.Empty, ContractValidation.Describe(anchors.ValidateSetup()));
                var controller = stage.GetComponent<MarsAtmosphereController>();
                Assert.That(controller.ValidateSetup(), Is.Empty, ContractValidation.Describe(controller.ValidateSetup()));
                Assert.That(stage.GetComponentsInChildren<Light>(true), Is.Empty, "a única luz com sombra é da cena, não do palco");

                var triangles = 0;
                foreach (var filter in stage.GetComponentsInChildren<MeshFilter>(true))
                    triangles += filter.sharedMesh.triangles.Length / 3;

                Assert.That(triangles, Is.LessThan(90000), stage.name + ": bem abaixo da referência de 250 mil");
                var collider = stage.GetComponentInChildren<MeshCollider>();
                Assert.That(collider.sharedMesh.triangles.Length / 3, Is.LessThan(8000), "colisão leve, só perto do rover");
                foreach (var renderer in stage.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.That(renderer.sharedMaterial.shader.name, Does.StartWith("Universal Render Pipeline/"), renderer.name);
                    if (renderer.sharedMaterial.HasProperty("_BaseMap"))
                        Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap").width, Is.LessThanOrEqualTo(1024), renderer.name);
                }
            }

            Assert.That(valley.GetComponent<LateMarsStageAnchors>().Kind, Is.EqualTo(LateMarsStageAnchors.StageKind.Valley));
            Assert.That(victoria.GetComponent<LateMarsStageAnchors>().Kind, Is.EqualTo(LateMarsStageAnchors.StageKind.Victoria));
        }

        [Test]
        public void TheRealRoverPrefabUsesShadersThatTheFogReaches()
        {
            var rover = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RoverPath)));
            var renderers = rover.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0), "o rover tem malha (os arquivos LFS foram baixados)");
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material.shader.name, Does.StartWith("Universal Render Pipeline/"), $"{renderer.name}: sem silhueta imune à poeira");
            }
        }

        // ---------- Âncoras e MAR-008 ----------

        [Test]
        public void ValleyAnchorsSitAFewMetresFromTheRoverAndFrameTheSunAndTheValley()
        {
            var anchors = valley.GetComponent<LateMarsStageAnchors>();
            Assert.That(anchors.RoverToOverlookMeters, Is.InRange(5f, 12f), "mirante a poucos metros");
            var local = valley.transform.position;
            Assert.That(anchors.Overlook.position.y - local.y, Is.EqualTo(G.ValleyHeight(0f, 3f)).Within(0.05f), "mirante assentado no chão");

            var sunDisc = valley.transform.Find("Atmosphere/SunDisc");
            var toSun = sunDisc.position - anchors.SunView.position;
            Assert.That(Vector3.Angle(anchors.SunView.forward, toSun), Is.LessThan(2f), "a câmera guiada já enquadra o Sol");

            var pitch = -Mathf.Asin(anchors.ValleyView.forward.y) * Mathf.Rad2Deg;
            Assert.That(pitch, Is.InRange(5f, 25f), "olha o vale para baixo");
            Assert.That(anchors.ValleyView.forward.z, Is.GreaterThan(0.8f), "para o lado em que o vale desce");
            Assert.That(anchors.EarthTarget.position.y - local.y, Is.GreaterThan(20f), "a Terra está alta no céu");
        }

        [Test]
        public void ValleyAnchorsFeedTheFinalSurveyOfMar008()
        {
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            var subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            var services = Track(new GameObject("services"));
            var router = services.AddComponent<InputRouter>();
            var interaction = services.AddComponent<InteractionService>();
            interaction.Initialize(router, time);
            var cameraGo = Track(new GameObject("camera"));
            var camera = cameraGo.AddComponent<Camera>();
            var operatorTransform = Track(new GameObject("operador")).transform;

            var surveyRoot = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Chapters/Mars/Prefabs/FinalSurveyInteractions.prefab")));
            var survey = surveyRoot.GetComponent<MarsFinalSurvey>();
            var anchors = valley.GetComponent<LateMarsStageAnchors>();
            var director = new MockChapterDirector();
            var flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            var store = new EvidenceStore();
            survey.Initialize(flow, interaction, time, anchors.ToSurveyAnchors(), operatorTransform, camera, store);
            Assert.That(survey.ValidateWiring(), Is.Empty, ContractValidation.Describe(survey.ValidateWiring()));

            director.Begin(chapter);
            var guard = 0;
            while (director.CurrentStepId != "M24" && guard++ < 64)
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                {
                    flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
                    continue;
                }

                foreach (var e in binding.CompletionEvents)
                    flow.Raise(e);
                for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                    flow.Tick(Frame);
            }

            Assert.That(director.CurrentStepId, Is.EqualTo("M24"));
            operatorTransform.position = anchors.RoverStart.position;
            time.ActiveDeltaSeconds = Frame;
            survey.Tick(Frame);
            Assert.That(survey.DistanceToOverlookMeters, Is.GreaterThan(3f), "da partida ainda falta caminho até o mirante");
            operatorTransform.position = anchors.Overlook.position + Vector3.back * 2f;
            survey.Tick(Frame);
            Assert.That(survey.IsEngaged, Is.True, "chegar ao mirante da MAR-016 conclui M24 da MAR-008");
        }

        // ---------- Perfil por evento ----------

        [Test]
        public void ProfileValidatesAndEveryGuardRejectsAnUnsafeVariant()
        {
            Assert.That(profile.Validate(), Is.Empty, ContractValidation.Describe(profile.Validate()));
            Assert.That(profile.victoria.endsAtDust, Is.LessThanOrEqualTo(0.2f), "2007 recupera a luz");
            Assert.That(profile.valley.endsAtDust, Is.GreaterThanOrEqualTo(0.8f), "2018 termina sem sinal");

            void Rejects(Action<MarsStormProfileData> unsafeChange, string because)
            {
                var copy = UnityEngine.Object.Instantiate(profile);
                unsafeChange(copy);
                Assert.That(copy.Validate(), Is.Not.Empty, because);
                UnityEngine.Object.DestroyImmediate(copy);
            }

            Rejects(p => { var d = p.valley.dense; d.fogStartMeters = 0f; d.fogEndMeters = 8f; p.valley.dense = d; }, "névoa que engole o rover");
            Rejects(p => { var d = p.victoria.dense; d.fogEndMeters = 42f; d.fogStartMeters = 20f; p.victoria.dense = d; }, "névoa que apaga o Sol");
            Rejects(p => { var d = p.victoria.dense; d.sunIntensity = 0.01f; p.victoria.dense = d; }, "Sol abaixo do piso");
            Rejects(p => { var d = p.valley.dense; d.sunDiscAlpha = 0f; p.valley.dense = d; }, "disco solar que some");
            Rejects(p => { var d = p.victoria.dense; d.fogColor = Color.white; p.victoria.dense = d; }, "poeira branca estourada");
            Rejects(p => { var d = p.valley.dense; d.shakeMeters = 0.5f; p.valley.dense = d; }, "tremor forte");
            Rejects(p => p.maxParticlesNormal = 900, "mais de 500 partículas");
            Rejects(p => p.maxParticlesLow = p.maxParticlesNormal, "perfil baixo sem redução");
            Rejects(p => p.victoria.endsAtDust = 0.9f, "2007 sem recuperar");
            Rejects(p => p.valley.endsAtDust = 0.2f, "2018 com sinal");
        }

        [Test]
        public void DenseFogKeepsTheRoverAndTheSunReadableWithTheRealRenderSettings()
        {
            foreach (var stage in new[] { victoria, valley })
            {
                var controller = Controller(stage);
                controller.Snap(1f);
                var factor = Mathf.Clamp01((profile.roverDistanceMeters - RenderSettings.fogStartDistance) / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance));
                Assert.That(factor, Is.LessThanOrEqualTo(profile.maxFogFactorAtRover), controller.EventId + ": o rover continua visível na névoa densa");
                var sunFactor = Mathf.Clamp01((profile.sunDiscDistanceMeters - RenderSettings.fogStartDistance) / (RenderSettings.fogEndDistance - RenderSettings.fogStartDistance));
                Assert.That(sunFactor, Is.LessThanOrEqualTo(profile.maxFogFactorAtSun), controller.EventId + ": o Sol continua legível");
                Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear), "névoa por profundidade, por pixel: o rover é névoa e geometria juntos");

                controller.Snap(0f);
                var clear = RenderSettings.fogEndDistance;
                controller.Snap(1f);
                Assert.That(RenderSettings.fogEndDistance, Is.LessThan(clear), "denso fecha a distância de visão");
            }
        }

        [Test]
        public void FogAndSkyNeverBlowOutToWhiteAtAnyDustLevel()
        {
            foreach (var stage in new[] { victoria, valley })
            {
                var controller = Controller(stage);
                for (var d = 0f; d <= 1.001f; d += 0.1f)
                {
                    controller.Snap(d);
                    var c = RenderSettings.fogColor;
                    Assert.That(0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b, Is.LessThanOrEqualTo(profile.maxDustLuminance), $"{controller.EventId} poeira {d:0.0}");
                    Assert.That(controller.State.sunIntensity, Is.GreaterThanOrEqualTo(profile.Get(controller.EventId).clear.sunIntensity * profile.minLightFraction - 1e-4f));
                }
            }
        }

        // ---------- Sem flash, pausa e restauração ----------

        [Test]
        public void DustNeverJumpsSoLightAndFogChangeSmoothlyWithoutAFlash()
        {
            var sunGo = Track(new GameObject("sun"));
            var sun = sunGo.AddComponent<Light>();
            var controller = Controller(valley, sun);
            controller.Snap(0f);
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            var subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            var director = new MockChapterDirector();
            var flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            controller.Initialize(flow, null, time, sun);

            var previousSun = sun.intensity;
            var previousFogEnd = RenderSettings.fogEndDistance;
            var maxSunStep = 0f;
            var maxDust = controller.DisplayedDust;
            var maxDustStep = 0f;
            var lastDust = controller.DisplayedDust;
            director.Begin(chapter);
            var guard = 0;
            while (director.CurrentStepId != "M31" && guard++ < 64)
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                    flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
                else
                    foreach (var e in binding.CompletionEvents)
                        flow.Raise(e);

                for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                {
                    flow.Tick(Frame);
                    time.ActiveDeltaSeconds = Frame;
                    controller.Tick(Frame);
                    maxSunStep = Mathf.Max(maxSunStep, Mathf.Abs(sun.intensity - previousSun));
                    maxDustStep = Mathf.Max(maxDustStep, Mathf.Abs(controller.DisplayedDust - lastDust));
                    previousSun = sun.intensity;
                    lastDust = controller.DisplayedDust;
                    previousFogEnd = RenderSettings.fogEndDistance;
                }
            }

            // Deixa a poeira de 2018 correr até o fim, sem sinal.
            for (var i = 0; i < 1200; i++)
            {
                time.ActiveDeltaSeconds = Frame;
                controller.Tick(Frame);
                maxDust = Mathf.Max(maxDust, controller.DisplayedDust);
                maxDustStep = Mathf.Max(maxDustStep, Mathf.Abs(controller.DisplayedDust - lastDust));
                lastDust = controller.DisplayedDust;
            }

            var clear = profile.valley.clear.sunIntensity;
            Assert.That(maxDustStep, Is.LessThanOrEqualTo(profile.maxDustPerSecond * Frame + 1e-4f), "a poeira anda no máximo maxDustPerSecond");
            Assert.That(maxSunStep, Is.LessThan(clear * 0.05f), "nenhum quadro muda o Sol em mais de 5%: sem flash");
            Assert.That(controller.DisplayedDust, Is.GreaterThanOrEqualTo(0.9f), "2018 termina sem sinal");
            Assert.That(sun.intensity, Is.LessThan(clear * 0.5f), "o Sol enfraquece muito");
            Assert.That(sun.intensity, Is.GreaterThanOrEqualTo(clear * profile.minLightFraction - 1e-3f), "mas nunca some");
            Assert.That(previousFogEnd, Is.GreaterThan(0f));
        }

        [Test]
        public void ValleyDustFollowsTheStepKeysAndPauseFreezesIt()
        {
            var controller = Controller(valley);
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            var subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            var director = new MockChapterDirector();
            var flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            controller.Initialize(flow, null, time);
            director.Begin(chapter);
            Assert.That(controller.TargetDust, Is.EqualTo(0f), "o capítulo começa sem a poeira de 2018");

            var guard = 0;
            while (director.CurrentStepId != "M27" && guard++ < 64)
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                    flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
                else
                    foreach (var e in binding.CompletionEvents)
                        flow.Raise(e);
                for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                    flow.Tick(Frame);
            }

            Assert.That(director.CurrentStepId, Is.EqualTo("M27"));
            Assert.That(controller.TargetDust, Is.EqualTo(0.7f), "M27: a poeira engrossa");
            time.ActiveDeltaSeconds = 0f;
            var frozen = controller.DisplayedDust;
            for (var i = 0; i < 100; i++)
                controller.Tick(0f);

            Assert.That(controller.DisplayedDust, Is.EqualTo(frozen), "a pausa congela a poeira");
            for (var i = 0; i < 160; i++)
                controller.Tick(Frame);

            Assert.That(controller.DisplayedDust, Is.EqualTo(0.7f).Within(1e-3f), "M28: Sol atenuado");
            Assert.That(controller.State.sunDiscAlpha, Is.GreaterThanOrEqualTo(profile.minSunDiscAlpha), "o Sol segue legível");
        }

        [Test]
        public void VictoriaDustFollowsTheStormAndRecoversTheLight()
        {
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            var subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            var services = Track(new GameObject("services"));
            var router = services.AddComponent<InputRouter>();
            var interaction = services.AddComponent<InteractionService>();
            interaction.Initialize(router, time);
            var director = new MockChapterDirector();
            var flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            var storm = Track((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StormPath))).GetComponent<Storm2007Activity>();
            storm.Initialize(flow, interaction, time);
            var sun = Track(new GameObject("sun")).AddComponent<Light>();
            var controller = victoria.GetComponent<MarsAtmosphereController>();
            controller.Initialize(flow, storm, time, sun);

            director.Begin(chapter);
            var guard = 0;
            while (director.CurrentStepId != "M21" && guard++ < 64)
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                    flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
                else
                    foreach (var e in binding.CompletionEvents)
                        flow.Raise(e);
                for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                    flow.Tick(Frame);
            }

            var clearSun = profile.victoria.clear.sunIntensity;
            var minSun = clearSun;
            Feed(router, "FeedInteract", false);
            for (var i = 0; i < 700 && director.CurrentStepId == "M21"; i++)
            {
                time.ActiveDeltaSeconds = Frame;
                interaction.Tick(Frame);
                storm.Tick(Frame);
                controller.Tick(Frame);
                flow.Tick(Frame);
                if (storm.Phase == StormPhase.Conserving)
                    Feed(router, "FeedInteract", !interaction.AwaitingRelease);

                minSun = Mathf.Min(minSun, sun.intensity);
            }

            Feed(router, "FeedInteract", false);
            Assert.That(storm.Phase, Is.EqualTo(StormPhase.Done), "a tempestade de 2007 terminou");
            for (var i = 0; i < 200; i++)
            {
                time.ActiveDeltaSeconds = Frame;
                controller.Tick(Frame);
            }

            Assert.That(minSun, Is.LessThan(clearSun * 0.7f), "a poeira escureceu de verdade");
            Assert.That(controller.DisplayedDust, Is.LessThanOrEqualTo(0.2f), "2007 recupera a luz");
            Assert.That(sun.intensity, Is.GreaterThanOrEqualTo(clearSun * 0.85f), "o Sol volta");
            Assert.That(minSun, Is.GreaterThanOrEqualTo(clearSun * profile.minLightFraction - 1e-3f), "e nunca ficou abaixo do piso");
        }

        static void Feed(InputRouter target, string method, params object[] args) =>
            typeof(InputRouter).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        // ---------- Partículas, tremor e globais ----------

        [Test]
        public void ParticleCapsAndLowProfileKeepScienceReadable()
        {
            foreach (var stage in new[] { victoria, valley })
            {
                var controller = Controller(stage);
                var ps = stage.GetComponentInChildren<ParticleSystem>(true);
                controller.LowProfile = false;
                controller.Snap(1f);
                var normalRate = ps.emission.rateOverTime.constant;
                Assert.That(ps.main.maxParticles, Is.LessThanOrEqualTo(500), "teto de partículas");
                Assert.That(ps.main.maxParticles, Is.EqualTo(profile.maxParticlesNormal));
                Assert.That(ps.main.startColor.color.a, Is.LessThanOrEqualTo(profile.particleAlphaCap + 1e-4f), "sem poeira opaca nem branca estourada");
                Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));

                controller.LowProfile = true;
                controller.Snap(1f);
                Assert.That(ps.main.maxParticles, Is.EqualTo(profile.maxParticlesLow));
                Assert.That(ps.emission.rateOverTime.constant, Is.LessThan(normalRate));
                Assert.That(controller.State.sunDiscAlpha, Is.GreaterThanOrEqualTo(profile.minSunDiscAlpha), "o perfil baixo preserva o Sol");
                Assert.That(RenderSettings.fogEndDistance, Is.GreaterThan(RenderSettings.fogStartDistance), "e a névoa continua a mesma");

                controller.Snap(0f);
                Assert.That(ps.emission.rateOverTime.constant, Is.EqualTo(0f), "céu limpo, sem partículas");
            }
        }

        [Test]
        public void SunDiscStaysLegibleAndShakeIsOnlyAPulseThatReducedMotionRemoves()
        {
            var controller = Controller(valley);
            controller.Snap(1f);
            var disc = valley.transform.Find("Atmosphere/SunDisc").GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            disc.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor").a, Is.GreaterThanOrEqualTo(profile.minSunDiscAlpha - 1e-3f), "o disco solar segue visível");

            // Um pulso ao engrossar que decai: nunca contínuo.
            controller.ReducedMotion = false;
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            var subtitles = AssetDatabase.LoadAssetAtPath<TextAsset>(SubtitlesPath).text;
            var director = new MockChapterDirector();
            var flow = Track(new GameObject("flow")).AddComponent<MarsChapterFlow>();
            flow.Initialize(director, time, chapter, subtitles);
            controller.Snap(0.1f);
            controller.Initialize(flow, null, time);
            director.Begin(chapter);
            var guard = 0;
            while (director.CurrentStepId != "M27" && guard++ < 64)
            {
                var binding = flow.CurrentBinding;
                if (binding.IsCinematic)
                    flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Completed, 1f));
                else
                    foreach (var e in binding.CompletionEvents)
                        flow.Raise(e);
                for (var i = 0; i < 400 && director.CurrentStepId == binding.StepId; i++)
                    flow.Tick(Frame);
            }

            controller.Tick(Frame);
            var pulse = controller.ShakeMeters;
            Assert.That(pulse, Is.GreaterThan(0f), "um pulso ao engrossar");
            Assert.That(pulse, Is.LessThanOrEqualTo(profile.maxShakeMeters + 1e-5f));
            controller.ReducedMotion = true;
            Assert.That(controller.ShakeMeters, Is.EqualTo(0f), "movimento reduzido: sem tremor");
            controller.ReducedMotion = false;
            for (var i = 0; i < 60; i++)
                controller.Tick(Frame);

            Assert.That(controller.ShakeMeters, Is.EqualTo(0f).Within(1e-6f), "o pulso decaiu em poucos segundos: pontual, sem pulsação contínua");
        }

        [Test]
        public void RenderSettingsAreHandedBackWhenTheControllerIsDisabled()
        {
            RenderSettings.fog = false;
            RenderSettings.fogStartDistance = 11f;
            RenderSettings.fogEndDistance = 222f;
            var controller = Controller(victoria);
            controller.Snap(1f);
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogEndDistance, Is.Not.EqualTo(222f));

            // Em modo de edição a Unity não chama OnDisable: invoca-se o mesmo caminho de saída diretamente.
            typeof(MarsAtmosphereController).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            Assert.That(RenderSettings.fog, Is.False, "os RenderSettings originais voltam");
            Assert.That(RenderSettings.fogStartDistance, Is.EqualTo(11f));
            Assert.That(RenderSettings.fogEndDistance, Is.EqualTo(222f));
        }
    }
}
