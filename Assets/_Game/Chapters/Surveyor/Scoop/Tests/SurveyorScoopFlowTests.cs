using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.ScoopTests
{
    /// <summary>
    /// SUR-009 com os serviços Core reais (ChapterDirector, SessionClock, InputRouter, InteractionService) e um
    /// Trench/ScoopRig sintético montado em código com os mesmos nomes dos prefabs. Teclado simulado pelo Input System.
    /// </summary>
    public sealed class SurveyorScoopFlowTests : InputTestFixture
    {
        sealed class LoadedScene : ChapterDirector.ISceneLoader
        {
            public void Load(string name, Action loaded, Action<string> failed) => loaded();
            public void Unload(string name, Action unloaded) => unloaded();
        }

        GameObject root, trench, rig;
        ChapterDirector owner; SessionClock clock; InputRouter router; InteractionService service; SurveyorScoop scoop;
        ChapterDefinition chapter; Keyboard keyboard; SkinnedMeshRenderer groove; MeshRenderer ground, native;
        readonly List<string> cues = new List<string>();
        float worstClearance;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            root = new GameObject("SUR-009 test");
            owner = root.AddComponent<ChapterDirector>(); clock = root.AddComponent<SessionClock>();
            router = root.AddComponent<InputRouter>(); service = root.AddComponent<InteractionService>(); scoop = root.AddComponent<SurveyorScoop>();
            owner.Initialize(clock, router, new LoadedScene(), new EvidenceStore()); router.Enable(); service.Initialize(router, clock);
            BuildVisuals();
            scoop.Configure(owner, clock, router, service, trench, rig, new Renderer[] { ground }, new Renderer[] { native });
            chapter = ChapterDefinition.Create("surveyor.scoop.test", "a", "b", "c", "ScoopTest", 30, "test.sequence",
                new[] { new ChapterStep("Observe", false, SurveyorScoop.StepId), new ChapterStep(SurveyorScoop.StepId, false, "End"), new ChapterStep("End", true) });
            cues.Clear(); worstClearance = float.PositiveInfinity;
            scoop.CueEntered += c => cues.Add("in:" + c.CueId); scoop.CueExited += c => cues.Add("out:" + c.CueId);
            owner.OpenDossier(); owner.Begin(chapter);
        }

        public override void TearDown()
        {
            scoop.Dispose(); router.Disable();
            Object.DestroyImmediate(root); Object.DestroyImmediate(trench); Object.DestroyImmediate(rig); Object.DestroyImmediate(chapter);
            base.TearDown();
        }

        void BuildVisuals()
        {
            trench = new GameObject("Trench");
            var tile = new GameObject(SurveyorScoop.TileName); tile.transform.SetParent(trench.transform, false);
            tile.AddComponent<MeshFilter>(); tile.AddComponent<MeshRenderer>();
            var g = new GameObject(SurveyorScoop.GrooveName); g.transform.SetParent(trench.transform, false);
            groove = g.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.forward }, triangles = new[] { 0, 2, 1 } };
            var zero = new Vector3[3];
            foreach (var name in SurveyorScoop.BlendShapeNames) mesh.AddBlendShapeFrame(name, 100f, zero, zero, zero);
            groove.sharedMesh = mesh;
            new GameObject(SurveyorScoop.PhotoTargetName).transform.SetParent(trench.transform, false);

            rig = new GameObject("ScoopRig"); rig.transform.position = new Vector3(-.3f, .9f, 1.1f);
            Transform Add(Transform parent, string name, Vector3 local)
            { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t; }
            var shoulder = Add(rig.transform, "Shoulder", Vector3.zero);
            var elbow = Add(shoulder, "Elbow", new Vector3(0, 0, 1.1f));
            var wrist = Add(elbow, "Wrist", new Vector3(0, 0, 1.1f));
            var tool = Add(wrist, "Scoop", Vector3.zero);
            Add(tool, "Tip", new Vector3(0, -.03f, .17f));
            var poses = Add(rig.transform, "Poses", Vector3.zero);
            Add(poses, "Rest", Vector3.zero).position = new Vector3(-.2f, .75f, .55f);
            Add(poses, "Above", Vector3.zero); Add(poses, "Contact", Vector3.zero);
            var grains = Add(rig.transform, SurveyorScoop.GrainsPath, Vector3.zero);
            for (var i = 0; i < SurveyorScoop.GrainPoolSize; i++) Add(grains, "Grain_" + i, Vector3.zero).gameObject.SetActive(false);
            Add(rig.transform, SurveyorScoop.MotorPath, Vector3.zero).gameObject.AddComponent<AudioSource>();
            ground = new GameObject("Ground").AddComponent<MeshRenderer>(); native = new GameObject("NativeScoop").AddComponent<MeshRenderer>();
        }

        void Advance(float seconds)
        {
            while (seconds > .0001f)
            {
                float delta = Mathf.Min(.05f, seconds);
                clock.Tick(delta); service.Tick(clock.ActiveDeltaSeconds); scoop.Tick(clock.ActiveDeltaSeconds);
                worstClearance = Mathf.Min(worstClearance, scoop.TipClearanceMeters);
                seconds -= delta;
            }
        }

        void EnterStep() { owner.CompleteStep("Observe", SurveyorScoop.StepId); }
        void Prepare() { EnterStep(); Advance(SurveyorScoop.ApproachSeconds + .1f); Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Ready)); Assert.That(scoop.CanDig, Is.True); }
        float Weight(int index) => groove.GetBlendShapeWeight(groove.sharedMesh.GetBlendShapeIndex(SurveyorScoop.BlendShapeNames[index]));

        [Test] public void InstallingHidesTheNativeScoopAndTheReplacedGroundUntilDispose()
        {
            Assert.That(ground.enabled, Is.False); Assert.That(native.enabled, Is.False);
            Assert.That(trench.transform.Find(SurveyorScoop.TileName).GetComponent<MeshRenderer>().enabled, Is.True);
            scoop.Dispose();
            Assert.That(ground.enabled, Is.True); Assert.That(native.enabled, Is.True);
        }

        [Test] public void HoldingEForTwelveSecondsBuildsFourPassesInTheSameGroove()
        {
            Prepare(); Press(keyboard.eKey);
            Advance(6f);
            Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Digging));
            Assert.That(scoop.DigSeconds, Is.EqualTo(6f).Within(.12f));
            Assert.That(Weight(0), Is.EqualTo(100f).Within(1f)); Assert.That(Weight(1), Is.EqualTo(100f).Within(8f)); Assert.That(Weight(3), Is.Zero);
            Advance(6.3f);
            Release(keyboard.eKey);
            for (var i = 0; i < 4; i++) Assert.That(Weight(i), Is.EqualTo(100f).Within(.5f), "passe " + (i + 1));
            Assert.That(scoop.DepthMeters, Is.EqualTo(.18f).Within(.001f));
            Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Retracting));
        }

        [Test] public void ReleasingEFreezesTheGrooveAndKeepsTheProgress()
        {
            Prepare(); Press(keyboard.eKey); Advance(3f); Release(keyboard.eKey);
            float frozen = scoop.DigSeconds; float depth = scoop.DepthMeters;
            Assert.That(scoop.IsDigPaused, Is.True); Assert.That(scoop.MotorShouldPlay, Is.False);
            Advance(5f);
            Assert.That(scoop.DigSeconds, Is.EqualTo(frozen)); Assert.That(scoop.DepthMeters, Is.EqualTo(depth));
            Press(keyboard.eKey); Advance(1f); Release(keyboard.eKey);
            Assert.That(scoop.DigSeconds, Is.EqualTo(frozen + 1f).Within(.12f));
        }

        [Test] public void PausingFreezesTheDigAndResumingNeedsANewDeliberatePress()
        {
            Prepare(); Press(keyboard.eKey); Advance(2f); float at = scoop.DigSeconds;
            owner.Pause("test"); Advance(6f);
            Assert.That(scoop.DigSeconds, Is.EqualTo(at)); Assert.That(scoop.MotorShouldPlay, Is.False);
            owner.Resume("test"); Release(keyboard.eKey); Advance(1f);
            Assert.That(scoop.DigSeconds, Is.EqualTo(at));
            Press(keyboard.eKey); Advance(1f); Assert.That(scoop.DigSeconds, Is.GreaterThan(at + .8f));
        }

        [Test] public void EHeldFromThePreviousActionDoesNotStartTheDig()
        {
            Press(keyboard.eKey); EnterStep(); Advance(SurveyorScoop.ApproachSeconds + .1f);
            Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Ready)); Advance(2f);
            Assert.That(scoop.DigSeconds, Is.Zero); Assert.That(scoop.NeedsRelease, Is.True);
            Release(keyboard.eKey); Press(keyboard.eKey); Advance(1f);
            Assert.That(scoop.DigSeconds, Is.GreaterThan(.8f));
        }

        [Test] public void EveryBiteChangesTheGrooveWithinOneAndAHalfSecondsAndGrainsStayFew()
        {
            Prepare(); var bites = new List<int>(); scoop.DepthStepped += bites.Add;
            Press(keyboard.eKey); int peak = 0; float last = 0f; float longestFlat = 0f, flat = 0f;
            for (var i = 0; i < 240; i++)
            {
                Advance(.05f); peak = Mathf.Max(peak, scoop.ActiveGrains);
                if (Mathf.Abs(scoop.Passes - last) < .0005f) flat += .05f; else flat = 0f;
                longestFlat = Mathf.Max(longestFlat, flat); last = scoop.Passes;
            }
            Release(keyboard.eKey);
            Assert.That(bites.Count, Is.EqualTo(SurveyorScoop.StepCount));
            Assert.That(longestFlat, Is.LessThanOrEqualTo(1.2f));
            Assert.That(peak, Is.GreaterThan(0)); Assert.That(peak, Is.LessThanOrEqualTo(SurveyorScoop.GrainPoolSize));
        }

        [Test] public void ToolNeverGoesBelowTheDeformedGroundWhileDiggingAndRetracting()
        {
            Prepare(); Press(keyboard.eKey); Advance(13f); Advance(SurveyorScoop.RetractSeconds + .5f);
            Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Complete));
            Assert.That(worstClearance, Is.GreaterThanOrEqualTo(-1e-4f));
            Assert.That(scoop.ReachErrorMeters, Is.LessThan(.002f));
        }

        [Test] public void RetractionVisitsVerticalThenLateralThenRest()
        {
            Prepare(); Press(keyboard.eKey); Advance(12.05f);
            var order = new List<ScoopRetractStage>();
            for (var i = 0; i < 100; i++)
            {
                Advance(.05f);
                if (scoop.Phase == ScoopPhase.Retracting && (order.Count == 0 || order[order.Count - 1] != scoop.RetractStage)) order.Add(scoop.RetractStage);
            }
            CollectionAssert.AreEqual(new[] { ScoopRetractStage.Vertical, ScoopRetractStage.Lateral, ScoopRetractStage.Rest }, order);
            Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Complete));
        }

        void CancelAt(float at)
        {
            Prepare(); Press(keyboard.eKey); Advance(at); Release(keyboard.eKey);
            float progress = scoop.DigSeconds; float depth = scoop.DepthMeters;
            scoop.Cancel();
            Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Retracting)); Assert.That(scoop.Canceled, Is.True);
            Advance(SurveyorScoop.RetractSeconds + .2f);
            Assert.That(worstClearance, Is.GreaterThanOrEqualTo(-1e-4f), "cancelamento em " + at);
            Assert.That(scoop.DigSeconds, Is.EqualTo(progress)); Assert.That(scoop.DepthMeters, Is.EqualTo(depth));
            Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Approaching), "volta à aproximação para retomar");
        }

        [Test] public void CancelAtContactUsesTheSafePoseAndKeepsProgress() => CancelAt(1.0f);
        [Test] public void CancelAtNinetyPercentUsesTheSafePoseAndKeepsProgress() => CancelAt(10.8f);

        [Test] public void LeavingTheStepEarlyUsesTheSafePoseAndRestartRebuildsFromTheOriginalGround()
        {
            Prepare(); Press(keyboard.eKey); Advance(4f); Release(keyboard.eKey);
            owner.CompleteStep(SurveyorScoop.StepId, "End");
            Assert.That(scoop.Running, Is.False); Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Locked));
            Assert.That(Weight(0), Is.EqualTo(100f).Within(.5f), "o sulco permanece nas cenas seguintes");
            owner.RestartChapter();
            Assert.That(scoop.DigSeconds, Is.Zero); Assert.That(Weight(0), Is.Zero); Assert.That(scoop.ActiveGrains, Is.Zero);
        }

        [Test] public void CompletedTrenchSurvivesLeavingTheStep()
        {
            Prepare(); Press(keyboard.eKey); Advance(13f); Release(keyboard.eKey); Advance(SurveyorScoop.RetractSeconds + .2f);
            owner.CompleteStep(SurveyorScoop.StepId, "End");
            Assert.That(scoop.Phase, Is.EqualTo(ScoopPhase.Complete)); Assert.That(Weight(3), Is.EqualTo(100f).Within(.5f));
            Assert.That(ground.enabled, Is.False);
        }

        [Test] public void SubtitlesS14AndS15ArriveInOrderWithoutOverlapping()
        {
            Prepare(); Press(keyboard.eKey); Advance(12.5f); Release(keyboard.eKey); Advance(10f);
            var enteredPrepared = cues.IndexOf("in:SUR-S14"); var exitedPrepared = cues.IndexOf("out:SUR-S14");
            var enteredGrowing = cues.IndexOf("in:SUR-S15");
            Assert.That(enteredPrepared, Is.EqualTo(0)); Assert.That(exitedPrepared, Is.GreaterThan(enteredPrepared));
            Assert.That(enteredGrowing, Is.GreaterThan(exitedPrepared), "S15 só entra depois do tempo mínimo de S14");
        }

        [Test] public void TheTrenchAnchorAndThePhotoTargetShareTheSameCenter()
        {
            var anchor = new GameObject("anchor").transform; anchor.position = trench.transform.position + new Vector3(.02f, 0f, -.01f);
            Assert.That(scoop.ValidateSetup(anchor), Is.Empty);
            anchor.position += Vector3.right * 1f;
            Assert.That(scoop.ValidateSetup(anchor), Is.Not.Empty);
            Object.DestroyImmediate(anchor.gameObject);
        }
    }
}
