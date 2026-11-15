using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Roteiro v2, fase 5: as barras de cinema só nos estados que são só cinema (fora das duas interações e do memorial) e a luz quente
    /// na Terra e fria no espaço, por grupo de luz.
    /// </summary>
    public class PioneerCinemaLookTests
    {
        [Test]
        public void TheBarsAreShownOnlyInTheCinemaStatesAndTheFarewellMemorialRetractsThem()
        {
            foreach (var state in PioneerPresentation.StateIds)
            {
                var cinema = PioneerLetterbox.CinemaStates.Contains(state);
                Assert.That(PioneerLetterbox.ShouldShow(state, 5f), Is.EqualTo(cinema), state);
            }

            Assert.That(PioneerLetterbox.ShouldShow("PIO.Imaging", 10f), Is.False, "a foto é interação: a interface precisa do quadro");
            Assert.That(PioneerLetterbox.ShouldShow("PIO.Listening", 10f), Is.False, "a sintonia também");
            Assert.That(PioneerLetterbox.ShouldShow("PIO.Farewell", 29f), Is.True);
            Assert.That(PioneerLetterbox.ShouldShow("PIO.Farewell", PioneerFirstsHud.EndSeconds), Is.False, "o memorial tem botões: barras recolhidas");
            Assert.That(PioneerLetterbox.ShouldShow(null, 0f), Is.False, "fora de um estado não há barras");
            Assert.That(PioneerLetterbox.CinemaStates, Has.Length.EqualTo(7), "sete dos nove estados são só cinema");
        }

        [Test]
        public void TheBarsSlideInAndOutOverEightTenthsOfASecondWithoutOvershooting()
        {
            var progress = 0f;
            progress = PioneerLetterbox.Advance(progress, true, PioneerLetterbox.SlideSeconds / 2f);
            Assert.That(progress, Is.EqualTo(0.5f).Within(1e-4f), "metade do tempo, metade do caminho");
            progress = PioneerLetterbox.Advance(progress, true, 5f);
            Assert.That(progress, Is.EqualTo(1f), "não passa de cheio");
            progress = PioneerLetterbox.Advance(progress, false, PioneerLetterbox.SlideSeconds / 4f);
            Assert.That(progress, Is.EqualTo(0.75f).Within(1e-4f));
            progress = PioneerLetterbox.Advance(progress, false, 5f);
            Assert.That(progress, Is.Zero, "não passa de recolhido");
            Assert.That(PioneerLetterbox.Advance(0.3f, true, -1f), Is.EqualTo(0.3f), "tempo negativo não move as barras");
            Assert.That(PioneerLetterbox.BarFraction, Is.InRange(0.05f, 0.1f), "barras finas: o cinema continua enchendo o quadro");
        }

        [Test]
        public void LightIsWarmOnEarthAndColdInSpace()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            try
            {
                var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).Single();
                float Warmth(string groupId)
                {
                    var group = presentation.LightGroups.Single(g => g.groupId == groupId);
                    return group.keyColor.r - group.keyColor.b; // positivo = quente, negativo = frio
                }

                var earth = presentation.Anchors.Where(a => a.region == PioneerStageRegion.LaunchSite || a.region == PioneerStageRegion.GroundStation).ToArray();
                Assert.That(earth, Is.Not.Empty);
                foreach (var anchor in earth)
                    Assert.That(Warmth(anchor.lightGroupId), Is.GreaterThanOrEqualTo(0.25f), $"{anchor.anchorId}: na Terra a luz é quente");

                string[] coldGroups = { "PIO.Light.Cruise", "PIO.Light.FarSun", "PIO.Light.FarewellRim", "PIO.Light.ColdRim" };
                var space = presentation.Anchors.Where(a => a.region == PioneerStageRegion.DeepSpace && coldGroups.Contains(a.lightGroupId)).ToArray();
                Assert.That(space.Length, Is.GreaterThan(8), "a maior parte dos planos do espaço profundo");
                foreach (var anchor in space)
                    Assert.That(Warmth(anchor.lightGroupId), Is.LessThanOrEqualTo(0.0f), $"{anchor.anchorId}: no espaço a luz é fria");

                Assert.That(Warmth("PIO.Light.JupiterAxis"), Is.GreaterThan(0.05f), "Júpiter e a placa seguem iluminados pelo Sol, quentes");
                Assert.That(Warmth("PIO.Light.Plaque"), Is.GreaterThan(0.05f));
                Assert.That(presentation.LightGroups.Select(g => g.groupId), Does.Not.Contain("PIO.Light.Night"), "o antigo grupo noturno frio foi trocado pelo quente da Terra");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void TheChapterHoldsTheLetterbox()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerChapterController>(true)).Single();
                Assert.That(controller.ValidateSetup(), Is.Empty);
                Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerLetterbox>(true)).Count(), Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
