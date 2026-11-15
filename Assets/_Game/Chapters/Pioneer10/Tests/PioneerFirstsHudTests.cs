using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>
    /// Roteiro v2, cena 9 (as seis primeiras vezes): seis estrelas acendem uma a uma ao redor da sonda, no plano em que ela se afasta,
    /// sem passar do memorial e sem cobrir o centro do quadro.
    /// </summary>
    public class PioneerFirstsHudTests
    {
        [Test]
        public void SixStarsLightOneByOneInOrderInsideTheRecedingShot()
        {
            Assert.That(PioneerFirstsHud.StarCount, Is.EqualTo(6));
            Assert.That(PioneerFirstsHud.LabelKeys.Distinct().Count(), Is.EqualTo(6));
            Assert.That(PioneerFirstsHud.VisibleAt(19.9f), Is.False);
            Assert.That(PioneerFirstsHud.VisibleAt(20f), Is.True, "começa com o plano PIO08.Receding (20 s)");
            Assert.That(PioneerFirstsHud.VisibleAt(29.9f), Is.True);
            Assert.That(PioneerFirstsHud.VisibleAt(30f), Is.False, "some quando entra o memorial (30 s)");

            for (var i = 0; i < PioneerFirstsHud.StarCount; i++)
            {
                Assert.That(PioneerFirstsHud.ProgressAt(i, PioneerFirstsHud.LightStartAt(i) - 0.01f), Is.Zero, $"a estrela {i + 1} está apagada antes da hora");
                Assert.That(PioneerFirstsHud.ProgressAt(i, PioneerFirstsHud.LightStartAt(i) + PioneerFirstsHud.LightSeconds), Is.EqualTo(1f).Within(1e-4f));
                if (i > 0)
                    Assert.That(PioneerFirstsHud.LightStartAt(i) - PioneerFirstsHud.LightStartAt(i - 1), Is.EqualTo(PioneerFirstsHud.StepSeconds).Within(1e-4f), "uma por vez, em ordem");
            }

            var last = PioneerFirstsHud.LightStartAt(PioneerFirstsHud.StarCount - 1) + PioneerFirstsHud.LightSeconds;
            Assert.That(last, Is.LessThan(PioneerFirstsHud.EndSeconds - 1f), "a sexta fica acesa e legível antes do memorial");
            Assert.That(PioneerFirstsHud.ProgressAt(2, PioneerFirstsHud.LightStartAt(2) + 0.25f), Is.InRange(0.01f, 0.99f), "acende com um crescimento, não de uma vez");
        }

        [Test]
        public void TheStarsSitOnARingAroundTheProbeAwayFromTheCentreAndFromEachOther()
        {
            var points = Enumerable.Range(0, PioneerFirstsHud.StarCount).Select(PioneerFirstsHud.ViewportOf).ToArray();
            Assert.That(points.All(p => p.x > 0.1f && p.x < 0.9f && p.y > 0.1f && p.y < 0.9f), Is.True, "todas dentro do quadro, com folga para o rótulo");
            Assert.That(points[0].x, Is.LessThan(0.5f), "a primeira fica no lado esquerdo, no alto");
            Assert.That(points[0].y, Is.LessThan(0.5f));
            for (var i = 0; i < points.Length; i++)
            {
                Assert.That(Vector2.Distance(points[i], PioneerFirstsHud.EllipseCenter), Is.GreaterThan(0.2f), $"a estrela {i + 1} não cobre a sonda");
                for (var j = i + 1; j < points.Length; j++)
                    Assert.That(Vector2.Distance(points[i], points[j]), Is.GreaterThan(0.2f), $"estrelas {i + 1} e {j + 1} afastadas");
            }
        }

        [Test]
        public void TheChapterHoldsTheFirstsHudAndItsSequenceIsTheFarewellOne()
        {
            var scene = EditorSceneManager.OpenScene(PioneerStageBuilder.SceneAssetPath, OpenSceneMode.Single);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerChapterController>(true)).Single();
                Assert.That(controller.ValidateSetup(), Is.Empty);
                var firsts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerFirstsHud>(true)).Single();
                Assert.That(firsts, Is.Not.Null);
                var sequence = UnityEditor.AssetDatabase.LoadAssetAtPath<SequenceDefinition>(PioneerFarewellBuilder.SequencePath);
                var receding = sequence.Shots.Select((s, i) => (s, start: sequence.Shots.Take(i).Sum(x => x.DurationSeconds))).Single(x => x.s.ShotId == "PIO08.Receding");
                Assert.That(receding.start, Is.EqualTo(PioneerFirstsHud.StartSeconds).Within(1e-3f), "as estrelas começam junto do plano em que a sonda se afasta");
                Assert.That(receding.start + receding.s.DurationSeconds, Is.EqualTo(PioneerFirstsHud.EndSeconds).Within(1e-3f), "e terminam quando o plano acaba");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
