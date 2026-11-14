using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Abandoned.SpaceApps.Tests
{
    /// <summary>CORE-004: tomadas reais, um único relógio, pausa conjunta e uma única devolução de controle.</summary>
    public class SequenceDirectorTests
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        CameraDirector cameraDirector;
        SequenceDirector director;
        InputRouter router;
        Camera camera;
        Transform actor;
        List<string> log;
        List<SequenceResult> results;

        [SetUp]
        public void SetUp()
        {
            var cameraObject = Track(new GameObject("camera"));
            camera = cameraObject.AddComponent<Camera>();
            var host = Track(new GameObject("directors"));
            cameraDirector = host.AddComponent<CameraDirector>();
            cameraDirector.GroundProbe = _ => null;
            cameraDirector.Initialize(camera);
            router = host.AddComponent<InputRouter>();
            director = host.AddComponent<SequenceDirector>();
            director.Initialize(cameraDirector, router, null);

            actor = Track(new GameObject("rover")).transform;
            director.RegisterActor("rover", actor);

            log = new List<string>();
            results = new List<SequenceResult>();
            director.Started += id => log.Add("started:" + id);
            director.ShotStarted += id => log.Add("shot:" + id);
            director.EventRaised += id => log.Add("event:" + id);
            director.CueEntered += cue => log.Add("cue+:" + cue.CueId);
            director.CueExited += cue => log.Add("cue-:" + cue.CueId);
            director.Finished += r => { results.Add(r); log.Add("finished:" + r.Reason); };
        }

        [TearDown]
        public void TearDown()
        {
            director.Cancel();
            foreach (var obj in created)
                UnityEngine.Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : UnityEngine.Object
        {
            created.Add(obj);
            return obj;
        }

        static ShotKeyframe Key(float t, Vector3 p, float fov = 40f, Vector3 euler = default) => new ShotKeyframe(t, p, euler, fov);

        SequenceDefinition TwoShotSequence(bool continuity = true)
        {
            var a = new ShotDefinition("a", 2f, CameraDirector.WorldRigId,
                new[] { Key(0f, new Vector3(0, 2, 0), 40f), Key(2f, new Vector3(10, 2, 0), 50f) },
                easing: ShotEasing.Linear, events: new[] { new ShotEvent(0f, "a.begin"), new ShotEvent(1f, "a.mid"), new ShotEvent(2f, "a.end") });
            var b = new ShotDefinition("b", 3f, CameraDirector.WorldRigId,
                new[] { Key(0f, new Vector3(-50, 9, 9), 90f), Key(3f, new Vector3(10, 2, 6), 30f) },
                easing: ShotEasing.Linear, events: new[] { new ShotEvent(0f, "b.begin"), new ShotEvent(3f, "b.end") },
                continueFromPreviousShot: continuity);
            var cues = new[] { new SubtitleCue("c1", "K.one", 0f, 2f), new SubtitleCue("c2", "K.two", 2f, 5f) };
            var marks = new[] { new ActorMark("rover", 0f, Vector3.zero, Vector3.zero), new ActorMark("rover", 5f, new Vector3(5, 0, 0), new Vector3(0, 90, 0)) };
            return Track(SequenceDefinition.Create("seq", new[] { a, b }, cues, marks));
        }

        void Run(float dt, int maxFrames = 10000)
        {
            for (var i = 0; i < maxFrames && director.IsPlaying; i++)
                director.Tick(dt);
        }

        // ---------- Tomadas, continuidade e câmera ----------

        [Test]
        public void KeyframesInterpolatePositionFovAndEasing()
        {
            var linear = new ShotDefinition("l", 2f, CameraDirector.WorldRigId, new[] { Key(0f, Vector3.zero, 30f), Key(2f, new Vector3(4, 0, 0), 50f) }, easing: ShotEasing.Linear);
            CameraDirector.SampleKeyframes(linear, 0.5f, out var p, out _, out var fov);
            Assert.That(p.x, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(fov, Is.EqualTo(35f).Within(1e-4f));

            var eased = new ShotDefinition("e", 2f, CameraDirector.WorldRigId, new[] { Key(0f, Vector3.zero), Key(2f, new Vector3(4, 0, 0)) }, easing: ShotEasing.EaseInOut);
            CameraDirector.SampleKeyframes(eased, 0.5f, out var pe, out _, out _);
            Assert.That(pe.x, Is.EqualTo(4f * 0.15625f).Within(1e-4f), "smoothstep(0,25) = 0,15625");
            CameraDirector.SampleKeyframes(eased, 2f, out var end, out _, out _);
            Assert.That(end.x, Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void ConnectedShotsPlayWithoutAnInvoluntaryJump()
        {
            director.Play(TwoShotSequence(continuity: true));
            var previous = camera.transform.position;
            var previousFov = camera.fieldOfView;
            var maxStep = 0f;
            var maxFovStep = 0f;
            while (director.IsPlaying)
            {
                director.Tick(1f / 60f);
                maxStep = Math.Max(maxStep, Vector3.Distance(previous, camera.transform.position));
                maxFovStep = Math.Max(maxFovStep, Math.Abs(previousFov - camera.fieldOfView));
                previous = camera.transform.position;
                previousFov = camera.fieldOfView;
            }

            // Plano b começa em (-50, 9, 9)/FOV 90 no keyframe 0, mas a continuidade parte de (10, 2, 0)/FOV 50.
            Assert.That(maxStep, Is.LessThan(0.2f), "nenhum salto de posição na troca de plano");
            Assert.That(maxFovStep, Is.LessThan(0.5f), "nenhum salto de FOV na troca de plano");
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(10, 2, 6)).Using(new Vector3Comparer(1e-3f)));
        }

        [Test]
        public void WithoutContinuityTheCutIsIntentional()
        {
            director.Play(TwoShotSequence(continuity: false));
            director.Tick(2.01f);
            Assert.That(director.CurrentShotId, Is.EqualTo("b"));
            Assert.That(camera.transform.position.x, Is.LessThan(-40f), "corte seco para o keyframe 0 do plano b");
        }

        [Test]
        public void RigsLookTargetsAndGroundClearanceApply()
        {
            var rig = Track(new GameObject("rig")).transform;
            rig.position = new Vector3(100, 0, 0);
            rig.rotation = Quaternion.Euler(0, 90, 0);
            cameraDirector.RegisterRig("rover.rig", rig);
            var target = Track(new GameObject("target")).transform;
            target.position = new Vector3(100, 0, 50);
            cameraDirector.RegisterLookTarget("rock", target);
            cameraDirector.GroundProbe = _ => 0f;

            var shot = new ShotDefinition("s", 1f, "rover.rig", new[] { Key(0f, new Vector3(0, -3, 1)), Key(1f, new Vector3(0, -3, 1)) },
                lookAtTargetId: "rock", minGroundClearanceMeters: 0.5f);
            var pose = cameraDirector.Evaluate(shot, 0.5f, null);

            Assert.That(pose.Position.x, Is.EqualTo(101f).Within(1e-3f), "posição local do rig convertida para mundo");
            Assert.That(pose.Position.y, Is.EqualTo(0.5f).Within(1e-4f), "câmera mantida acima do chão");
            var forward = pose.Rotation * Vector3.forward;
            Assert.That(Vector3.Dot(forward, (target.position - pose.Position).normalized), Is.GreaterThan(0.999f), "enquadra o alvo");
        }

        [Test]
        public void DefaultGroundProbeUsesTheRealTerrainCollider()
        {
            cameraDirector.GroundProbe = null;
            var ground = Track(GameObject.CreatePrimitive(PrimitiveType.Plane));
            ground.transform.position = new Vector3(0, 2f, 0);
            Physics.SyncTransforms();

            var clamped = cameraDirector.ClampAboveGround(new Vector3(0, -10f, 0), 0.3f);
            Assert.That(clamped.y, Is.EqualTo(2.3f).Within(1e-3f));
            var untouched = cameraDirector.ClampAboveGround(new Vector3(0, 8f, 0), 0.3f);
            Assert.That(untouched.y, Is.EqualTo(8f).Within(1e-4f));
        }

        [Test]
        public void ExactlyOneAudioListenerStaysActive()
        {
            var extra1 = Track(new GameObject("listener1")).AddComponent<AudioListener>();
            var extra2 = Track(new GameObject("listener2")).AddComponent<AudioListener>();

            var disabled = cameraDirector.EnforceSingleAudioListener();

            Assert.That(disabled, Is.EqualTo(2));
            Assert.That(extra1.enabled || extra2.enabled, Is.False);
            Assert.That(camera.GetComponent<AudioListener>().enabled, Is.True);
        }

        // ---------- Relógio, eventos e pausa ----------

        [Test]
        public void EventsAreIdenticalAt30And60FpsIncludingClipBoundaries()
        {
            director.Play(TwoShotSequence());
            Run(1f / 30f);
            var at30 = new List<string>(log);

            log.Clear();
            director.Play(TwoShotSequence());
            Run(1f / 60f);

            Assert.That(log, Is.EqualTo(at30));
            Assert.That(at30, Is.EqualTo(new[]
            {
                "started:seq", "shot:a", "event:a.begin", "cue+:c1", "event:a.mid",
                "cue-:c1", "event:a.end", "shot:b", "event:b.begin", "cue+:c2", "cue-:c2", "event:b.end", "finished:Completed",
            }));
        }

        [Test]
        public void TheFirstFrameIsAppliedOnPlayWithoutWaitingForATick()
        {
            director.Play(TwoShotSequence());
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0, 2, 0)).Using(new Vector3Comparer(1e-4f)));
            Assert.That(log.Take(4), Is.EqualTo(new[] { "started:seq", "shot:a", "event:a.begin", "cue+:c1" }));
        }

        [Test]
        public void PauseFreezesCameraActorsAndTextTogether()
        {
            director.Play(TwoShotSequence());
            director.Tick(0.5f);
            var cameraPosition = camera.transform.position;
            var actorPosition = actor.localPosition;
            var events = log.Count;

            director.Pause();
            for (var i = 0; i < 30; i++)
                director.Tick(1f / 30f);
            director.Tick(0f); // tempo ativo zero (pausa da sessão) também congela

            Assert.That(director.TimeSeconds, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(camera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(actor.localPosition, Is.EqualTo(actorPosition));
            Assert.That(log.Count, Is.EqualTo(events), "nenhuma legenda ou evento durante a pausa");

            director.Resume();
            director.Tick(0.6f);
            Assert.That(log, Does.Contain("event:a.mid"));
        }

        [Test]
        public void ActorsReallyMoveOnTheSameClock()
        {
            director.Play(TwoShotSequence());
            director.Tick(2.5f);
            Assert.That(actor.localPosition.x, Is.EqualTo(2.5f).Within(1e-3f));
            Assert.That(actor.localRotation.eulerAngles.y, Is.EqualTo(45f).Within(0.1f));
        }

        [Test]
        public void RestartingMidShotStartsCleanFromZero()
        {
            director.Play(TwoShotSequence());
            director.Tick(1.3f);
            director.Cancel();
            Assert.That(results.Select(r => r.Reason), Is.EqualTo(new[] { SequenceExitReason.Canceled }));
            Assert.That(log, Does.Contain("cue-:c1"), "a legenda ativa é fechada ao cancelar");

            log.Clear();
            director.Play(TwoShotSequence());
            Assert.That(director.TimeSeconds, Is.EqualTo(0f));
            Assert.That(log, Is.EqualTo(new[] { "started:seq", "shot:a", "event:a.begin", "cue+:c1" }));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(0, 2, 0)).Using(new Vector3Comparer(1e-4f)));
        }

        [Test]
        public void PreviewShowsEarlierAndLaterStatesWithoutEventsOrControl()
        {
            var sequence = TwoShotSequence(continuity: false);
            director.Preview(sequence, 1f);
            Assert.That(camera.transform.position.x, Is.EqualTo(5f).Within(1e-3f));
            director.Preview(sequence, 5f);
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(10, 2, 6)).Using(new Vector3Comparer(1e-3f)));
            Assert.That(actor.localPosition.x, Is.EqualTo(5f).Within(1e-3f));
            Assert.That(log, Is.Empty);
            Assert.That(cameraDirector.IsControlled, Is.False);
        }

        // ---------- Saída única ----------

        [Test]
        public void CompletionReleasesCameraAndInputExactlyOnce()
        {
            director.Play(TwoShotSequence());
            Assert.That(cameraDirector.IsControlled, Is.True);
            Assert.That(router.Context, Is.EqualTo(InputContext.Cinematic));

            Run(0.1f);
            director.Tick(1f);
            director.Cancel();

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Reason, Is.EqualTo(SequenceExitReason.Completed));
            Assert.That(cameraDirector.IsControlled, Is.False);
            Assert.That(router.Context, Is.EqualTo(InputContext.Panel));
            Assert.That(router.DescribeLeases(), Is.Empty);
        }

        [Test]
        public void SkipOnlyWhenAllowedFiresRemainingEventsWithoutFlashingSubtitles()
        {
            director.Play(TwoShotSequence());
            Assert.That(director.Skip(), Is.False, "salto não permitido");

            director.Cancel();
            log.Clear();
            director.Play(TwoShotSequence(), skippable: true);
            director.Tick(0.5f);
            log.Clear();

            Assert.That(director.Skip(), Is.True);
            Assert.That(log, Is.EqualTo(new[] { "cue-:c1", "event:a.mid", "event:a.end", "shot:b", "event:b.begin", "event:b.end", "finished:Skipped" }));
            Assert.That(camera.transform.position, Is.EqualTo(new Vector3(10, 2, 6)).Using(new Vector3Comparer(1e-3f)));
            Assert.That(director.Skip(), Is.False);
            Assert.That(results.Count(r => r.Reason == SequenceExitReason.Skipped), Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentExitRequestsFromCallbacksReleaseControlOnce()
        {
            director.Play(TwoShotSequence(), skippable: true);
            director.EventRaised += id =>
            {
                if (id != "a.mid")
                    return;

                director.Cancel();
                director.Skip();
                director.Cancel();
            };

            Run(1f / 30f);

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Reason, Is.EqualTo(SequenceExitReason.Canceled));
            Assert.That(log, Does.Not.Contain("event:a.end"), "nada continua depois da saída");
            Assert.That(cameraDirector.IsControlled, Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);
        }

        [Test]
        public void PlayingAnotherSequenceCancelsThePreviousOneFirst()
        {
            director.Play(TwoShotSequence());
            director.Tick(0.5f);
            director.Play(TwoShotSequence());

            Assert.That(results.Select(r => r.Reason), Is.EqualTo(new[] { SequenceExitReason.Canceled }));
            Assert.That(router.DescribeLeases().Count, Is.EqualTo(1), "só a nova sequência segura a entrada");
            Assert.That(director.TimeSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void TriggeredSubtitleIsNotCutByTheEndOfTheSequence()
        {
            var shot = new ShotDefinition("s", 1f, CameraDirector.WorldRigId, new[] { Key(0f, Vector3.zero), Key(1f, Vector3.one) });
            var cue = SubtitleCue.Triggered("result", "K.result", "result.shown", 3f);
            director.Play(Track(SequenceDefinition.Create("t", new[] { shot }, new[] { cue })));

            director.Tick(0.9f);
            Assert.That(director.Trigger("result.shown"), Is.EqualTo(1));
            director.Tick(0.5f);
            Assert.That(director.IsPlaying, Is.True, "espera o tempo mínimo de leitura");
            director.Tick(2.5f);

            Assert.That(log.SkipWhile(x => x != "cue+:result"), Is.EqualTo(new[] { "cue+:result", "cue-:result", "finished:Completed" }));
        }

        [Test]
        public void InvalidReferencesAreReportedBeforeAnyEffect()
        {
            var shot = new ShotDefinition("s", 1f, "missing.rig", new[] { Key(0f, Vector3.zero), Key(1f, Vector3.one) }, lookAtTargetId: "missing.target");
            var sequence = Track(SequenceDefinition.Create("bad", new[] { shot }, null, new[] { new ActorMark("ghost", 0f, Vector3.zero, Vector3.zero) }));

            var ex = Assert.Throws<ArgumentException>(() => director.Play(sequence));
            Assert.That(ex.Message, Does.Contain("missing.rig").And.Contain("missing.target").And.Contain("ghost"));
            Assert.That(log, Is.Empty);
            Assert.That(cameraDirector.IsControlled, Is.False);
            Assert.That(router.DescribeLeases(), Is.Empty);
        }

        [Test]
        public void CameraControlIsExclusive()
        {
            using (cameraDirector.AcquireControl("gameplay-camera"))
                Assert.Throws<InvalidOperationException>(() => cameraDirector.AcquireControl("other"));

            Assert.That(cameraDirector.IsControlled, Is.False);
        }

        [Test]
        public void SequenceClockOnlyMovesForwardWithActiveTime()
        {
            var clock = new SequenceClock();
            clock.Advance(1f);
            Assert.That(clock.TimeSeconds, Is.EqualTo(0f), "parado não avança");

            clock.Start(4f);
            clock.Advance(-1f);
            clock.Advance(float.NaN);
            clock.Advance(1f);
            clock.JumpForwardTo(0.5f);
            Assert.That(clock.TimeSeconds, Is.EqualTo(1f));
            clock.JumpForwardTo(3f);
            Assert.That(clock.NormalizedTime, Is.EqualTo(0.75f).Within(1e-5f));
        }

        sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            readonly float tolerance;

            public Vector3Comparer(float tolerance) => this.tolerance = tolerance;

            public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;

            public int GetHashCode(Vector3 v) => 0;
        }
    }
}
