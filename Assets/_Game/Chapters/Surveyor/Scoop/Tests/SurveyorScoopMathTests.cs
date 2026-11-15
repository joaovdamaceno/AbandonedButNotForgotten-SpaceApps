using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor.ScoopTests
{
    /// <summary>
    /// SUR-009: matemática pura do sulco e da trajetória (sem GameObject). Rodam no Editor e também fora dele,
    /// porque só usam Vector3/Mathf. Provam profundidade ≤ 18 cm, feedback em ≤ 1,2 s, recolhimento em três fases
    /// e que nenhum ponto do balde entra no relevo em nenhum instante.
    /// </summary>
    public sealed class SurveyorScoopMathTests
    {
        static SurveyorScoop.Path NewPath(float side = 1f) => new SurveyorScoop.Path(side, new Vector3(-.2f, .75f, .55f));

        // Amostra independente do balde (mais fina que a usada pela trajetória): ponta + 8 pontos atrás, três larguras.
        static float WorstPenetrationMeters(Vector3 tip, float tiltDegrees, float passes)
        {
            float tilt = tiltDegrees * Mathf.Deg2Rad, worst = float.PositiveInfinity;
            for (int i = 0; i <= 8; i++)
            {
                float d = SurveyorScoop.Shape.BucketLengthMeters * i / 8f;
                for (int k = -2; k <= 2; k++)
                {
                    float x = tip.x - Mathf.Cos(tilt) * d, z = tip.z + k * .5f * SurveyorScoop.Shape.BucketHalfWidthMeters;
                    float y = tip.y + Mathf.Sin(tilt) * d;
                    worst = Mathf.Min(worst, y - SurveyorScoop.Shape.Height(passes, x, z));
                }
            }
            return worst;
        }

        [Test]
        public void TrenchNeverExceedsEighteenCentimetersAndReachesItAtTheCenter()
        {
            float lowest = 0f;
            for (float x = -.6f; x <= .6f; x += .01f)
                for (float z = -.4f; z <= .4f; z += .01f)
                    lowest = Mathf.Min(lowest, SurveyorScoop.Shape.Height(SurveyorScoop.PassCount, x, z));
            Assert.That(lowest, Is.GreaterThanOrEqualTo(-SurveyorScoop.Shape.MaxDepthMeters - 1e-5f));
            Assert.That(SurveyorScoop.Shape.Height(SurveyorScoop.PassCount, 0f, 0f), Is.EqualTo(-.18f).Within(1e-4f));
        }

        [Test]
        public void EachPassDeepensTheSameGrooveAndRaisesItsEdges()
        {
            float previousDepth = 0f, previousBerm = 0f;
            for (int pass = 1; pass <= SurveyorScoop.PassCount; pass++)
            {
                float depth = -SurveyorScoop.Shape.PassHeight(pass, 0f, 0f);
                float berm = float.NegativeInfinity;
                for (float z = -.4f; z <= .4f; z += .005f) berm = Mathf.Max(berm, SurveyorScoop.Shape.PassHeight(pass, 0f, z));
                Assert.That(depth, Is.GreaterThan(previousDepth + .03f), "profundidade passe " + pass);
                Assert.That(berm, Is.GreaterThan(previousBerm + .004f), "borda passe " + pass);
                previousDepth = depth; previousBerm = berm;
            }
        }

        [Test]
        public void ProgressChangesVisiblyAtLeastEveryOnePointTwoSeconds()
        {
            const float step = .01f;
            for (float t = 0f; t < SurveyorScoop.TotalDigSeconds - 1.2f; t += step)
            {
                float now = SurveyorScoop.Path.PassesAt(t), later = SurveyorScoop.Path.PassesAt(t + 1.2f);
                Assert.That(later - now, Is.GreaterThan(.1f), "janela de 1,2 s em t=" + t);
            }
            Assert.That(SurveyorScoop.Path.PassesAt(SurveyorScoop.TotalDigSeconds), Is.EqualTo(4f).Within(1e-5f));
            float last = 0f;
            for (float t = 0f; t <= SurveyorScoop.TotalDigSeconds; t += .003f)
            {
                float p = SurveyorScoop.Path.PassesAt(t);
                Assert.That(p, Is.GreaterThanOrEqualTo(last - 1e-6f), "monótono em t=" + t);
                last = p;
            }
        }

        [Test]
        public void TwelveBitesStartOneSecondApart()
        {
            Assert.That(SurveyorScoop.Path.BitesStarted(0f), Is.EqualTo(0));
            Assert.That(SurveyorScoop.Path.BitesStarted(.001f), Is.EqualTo(1));
            Assert.That(SurveyorScoop.Path.BitesStarted(.999f), Is.EqualTo(1));
            Assert.That(SurveyorScoop.Path.BitesStarted(1f), Is.EqualTo(2));
            Assert.That(SurveyorScoop.Path.BitesStarted(5.5f), Is.EqualTo(6));
            Assert.That(SurveyorScoop.Path.BitesStarted(SurveyorScoop.TotalDigSeconds), Is.EqualTo(SurveyorScoop.StepCount));
        }

        [Test]
        public void ToolNeverEntersTheDeformedGroundDuringTheFourPasses()
        {
            var path = NewPath();
            for (float t = 0f; t <= SurveyorScoop.TotalDigSeconds + 1e-4f; t += .005f)
            {
                var tip = path.DigTip(t, out float tilt);
                Assert.That(WorstPenetrationMeters(tip, tilt, SurveyorScoop.Path.PassesAt(t)), Is.GreaterThanOrEqualTo(-1e-4f), "t=" + t);
            }
        }

        [Test]
        public void ToolStaysCloseToTheSurfaceWhileDraggingSoTheGrooveLooksCarvedByIt()
        {
            var path = NewPath();
            for (int pass = 0; pass < SurveyorScoop.PassCount; pass++)
                for (float u = .25f; u <= .70f; u += .05f)
                {
                    float t = (pass + u) * SurveyorScoop.PassSeconds;
                    var tip = path.DigTip(t, out _);
                    float floor = SurveyorScoop.Shape.Height(SurveyorScoop.Path.PassesAt(t), tip.x, 0f);
                    Assert.That(tip.y - floor, Is.LessThan(.03f), "folga acima do fundo em t=" + t);
                }
        }

        [Test]
        public void RetractionRisesEightCentimetersBeforeMovingSidewaysAndOnlyThenReturnsToRest()
        {
            var path = NewPath();
            var start = path.DigTip(SurveyorScoop.TotalDigSeconds, out _);
            Vector3 atVerticalEnd = path.RetractTip(SurveyorScoop.VerticalSeconds, start, 4f, out var stage, out _);
            Assert.That(atVerticalEnd.y - start.y, Is.EqualTo(SurveyorScoop.LiftMeters).Within(1e-4f));
            for (float s = 0f; s < SurveyorScoop.VerticalSeconds; s += .02f)
            {
                var p = path.RetractTip(s, start, 4f, out stage, out _);
                Assert.That(stage, Is.EqualTo(ScoopRetractStage.Vertical));
                Assert.That(p.x, Is.EqualTo(start.x).Within(1e-5f));
                Assert.That(p.z, Is.EqualTo(start.z).Within(1e-5f));
                Assert.That(p.y, Is.GreaterThanOrEqualTo(start.y - 1e-5f));
            }
            path.RetractTip(SurveyorScoop.VerticalSeconds + .1f, start, 4f, out stage, out _);
            Assert.That(stage, Is.EqualTo(ScoopRetractStage.Lateral));
            var sideEnd = path.RetractTip(SurveyorScoop.VerticalSeconds + SurveyorScoop.LateralSeconds, start, 4f, out stage, out _);
            Assert.That(Mathf.Abs(sideEnd.z - start.z), Is.GreaterThan(SurveyorScoop.LateralMeters * .95f));
            path.RetractTip(SurveyorScoop.VerticalSeconds + SurveyorScoop.LateralSeconds + .1f, start, 4f, out stage, out _);
            Assert.That(stage, Is.EqualTo(ScoopRetractStage.Rest));
            var rest = path.RetractTip(SurveyorScoop.RetractSeconds, start, 4f, out stage, out _);
            Assert.That(Vector3.Distance(rest, path.Rest), Is.LessThan(1e-4f));
        }

        [Test]
        public void RetractionFromEveryTenPercentOfTheDigNeverEntersTheGround()
        {
            var path = NewPath();
            for (int tenth = 0; tenth <= 10; tenth++)
            {
                float digAt = SurveyorScoop.TotalDigSeconds * tenth / 10f;
                var start = path.DigTip(digAt, out _);
                float passes = SurveyorScoop.Path.PassesAt(digAt);
                for (float s = 0f; s <= SurveyorScoop.RetractSeconds; s += .01f)
                {
                    var tip = path.RetractTip(s, start, passes, out _, out float tilt);
                    Assert.That(WorstPenetrationMeters(tip, tilt, passes), Is.GreaterThanOrEqualTo(-1e-4f), "dig " + digAt + " s, retract " + s);
                }
            }
        }

        [Test]
        public void CancelAtContactAndAtNinetyPercentUseTheSameSafeRetraction()
        {
            var path = NewPath();
            foreach (float digAt in new[] { .1f, 1.5f * SurveyorScoop.PassSeconds / 3f + .2f, SurveyorScoop.TotalDigSeconds * .9f, SurveyorScoop.TotalDigSeconds })
            {
                var start = path.DigTip(digAt, out _);
                float passes = SurveyorScoop.Path.PassesAt(digAt);
                var top = path.RetractTip(SurveyorScoop.VerticalSeconds, start, passes, out _, out _);
                Assert.That(top.y - start.y, Is.EqualTo(SurveyorScoop.LiftMeters).Within(1e-4f), "subida em " + digAt);
                Assert.That(Vector3.Distance(path.RetractTip(SurveyorScoop.RetractSeconds, start, passes, out _, out _), path.Rest), Is.LessThan(1e-4f));
            }
        }

        [Test]
        public void ApproachGoesFromRestToAboveTheGrooveStartWithoutDescendingBelowIt()
        {
            var path = NewPath();
            Assert.That(Vector3.Distance(path.ApproachTip(0f, out _), path.Rest), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(path.ApproachTip(SurveyorScoop.ApproachSeconds, out _), path.Above), Is.LessThan(1e-5f));
            Assert.That(path.Above.y, Is.GreaterThanOrEqualTo(SurveyorScoop.AboveHeightMeters - 1e-5f));
            Assert.That(path.Above.x, Is.EqualTo(SurveyorScoop.Shape.DragStartX).Within(1e-5f));
            for (float s = 0f; s <= SurveyorScoop.ApproachSeconds; s += .02f)
                Assert.That(path.ApproachTip(s, out _).y, Is.GreaterThan(.1f), "acima do relevo em " + s);
        }

        [Test]
        public void TwoBoneSolverReachesTheTargetKeepsLinkLengthsAndBendsTowardThePole()
        {
            var shoulder = new Vector3(.1f, .65f, .95f);
            foreach (var target in new[] { new Vector3(1f, 0f, 2.3f), new Vector3(.8f, .3f, 1.9f), new Vector3(1.2f, .05f, 2.5f) })
            {
                bool reached = SurveyorScoop.SolveTwoBone(shoulder, target, 1.05f, 1.05f, Vector3.up, out var elbow);
                Assert.That(reached, Is.True);
                Assert.That(Vector3.Distance(shoulder, elbow), Is.EqualTo(1.05f).Within(1e-4f));
                Assert.That(Vector3.Distance(elbow, target), Is.EqualTo(1.05f).Within(1e-4f));
                Assert.That(elbow.y, Is.GreaterThan(Mathf.Min(shoulder.y, target.y)), "o cotovelo dobra para cima");
            }
            Assert.That(SurveyorScoop.SolveTwoBone(shoulder, shoulder + Vector3.forward * 5f, 1f, 1f, Vector3.up, out _), Is.False);
        }

        [Test]
        public void GrainsAreFewAndEveryBurstFitsTheBudget()
        {
            Assert.That(SurveyorScoop.GrainPoolSize, Is.LessThanOrEqualTo(32));
            Assert.That(SurveyorScoop.GrainsPerStep * SurveyorScoop.StepCount, Is.GreaterThan(SurveyorScoop.GrainPoolSize / 2));
            Assert.That(SurveyorScoop.LunarGravityMetersPerSecondSquared, Is.EqualTo(1.62f).Within(.01f));
        }

        [Test]
        public void TheFootprintCoversTheWholeCavityAndItsBerms()
        {
            var size = SurveyorScoop.Shape.FootprintSizeMeters;
            var seen = new List<Vector2>();
            for (float x = -.8f; x <= .8f; x += .01f)
                for (float z = -.5f; z <= .5f; z += .01f)
                    if (Mathf.Abs(SurveyorScoop.Shape.Height(4f, x, z)) > 1e-4f) seen.Add(new Vector2(x, z));
            Assert.That(seen.Count, Is.GreaterThan(100));
            foreach (var p in seen)
            {
                Assert.That(Mathf.Abs(p.x - (SurveyorScoop.Shape.GrooveStartX + SurveyorScoop.Shape.GrooveEndX) * .5f), Is.LessThanOrEqualTo(size.x * .5f + .15f));
                Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(size.z * .5f + 1e-3f));
            }
        }
    }
}
