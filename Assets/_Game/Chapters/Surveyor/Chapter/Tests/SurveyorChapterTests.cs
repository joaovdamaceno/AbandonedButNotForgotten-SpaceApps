using System.Collections;
using System.Collections.Generic;
using System.IO;
using Abandoned.SpaceApps.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.ChapterTests
{
    public sealed class SurveyorChapterTests : InputTestFixture
    {
        [Test]
        public void FinalPoseKeepsBothArmsClearOfTheTorso()
        {
            var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast=instance.GetComponentInChildren<SurveyorApolloCast>(true);cast.Evaluate(44f,true);
                for(int actor=0;actor<2;actor++)
                {
                    var position=cast.Astronauts[actor].transform.position;
                    var inverse=Quaternion.Inverse(Quaternion.LookRotation(cast.FacingDirection(actor)));
                    for(int arm=0;arm<2;arm++)
                    {
                        var elbow=inverse*(cast.ElbowPosition(actor,arm)-position);
                        var hand=inverse*(cast.HandPosition(actor,arm)-position);
                        Assert.That(Mathf.Abs(elbow.x)>.27f||elbow.z>.21f,Is.True,"Elbow embedded in torso: actor="+actor+" arm="+arm+" "+elbow);
                        Assert.That(Mathf.Abs(hand.x)>.27f||hand.z>.24f,Is.True,"Hand embedded in torso: actor="+actor+" arm="+arm+" "+hand);
                    }
                }
            }
            finally{Object.DestroyImmediate(instance);}
        }
        [Test]
        public void LandingArcsUseLunarGravityAndCorrectReboundHeights()
        {
            float mid=SurveyorLandingMotion.FirstContactSeconds+SurveyorLandingMotion.FirstFlightSeconds*.5f;
            Assert.That(SurveyorLandingMotion.Height(mid),Is.EqualTo(10f).Within(.001f));
            float dt=.05f;
            float acceleration=(SurveyorLandingMotion.Height(mid+dt)-2f*SurveyorLandingMotion.Height(mid)+SurveyorLandingMotion.Height(mid-dt))/(dt*dt);
            Assert.That(acceleration,Is.EqualTo(-SurveyorLandingMotion.LunarGravity).Within(.02f));
            Assert.That(SurveyorLandingMotion.Height(SurveyorLandingMotion.SecondContactSeconds+SurveyorLandingMotion.SecondFlightSeconds*.5f),Is.EqualTo(3f).Within(.001f));
            Assert.That(SurveyorLandingMotion.Height(33f),Is.EqualTo(0f).Within(.001f));
        }
        [Test]
        public void OldFixedTouchdownPlanIsSwappedForATrackingOne()
        {
            ShotKeyframe[] Frames(float seconds) => new[] { new ShotKeyframe(0f, Vector3.zero, Vector3.zero, 40f), new ShotKeyframe(seconds, Vector3.one, Vector3.zero, 40f) };
            var old = SequenceDefinition.Create("sur.doc.Arrival", new[]
            {
                new ShotDefinition("sur.doc.descent.separation", 8f, "sur.craft", Frames(8f)),
                new ShotDefinition("sur.doc.descent.contacts", 12.5f, "world", Frames(12.5f), "sur.landing"),
            });
            SequenceDefinition tracked = null;
            try
            {
                tracked = SurveyorChaseShot.EnsureTracking(old);
                Assert.That(tracked, Is.Not.SameAs(old));
                Assert.That(tracked.Shots[0].RigId, Is.EqualTo("sur.craft"), "Shots that already follow the craft are untouched.");
                Assert.That(tracked.Shots[1].RigId, Is.EqualTo(SurveyorChaseShot.RigId));
                Assert.That(tracked.Shots[1].LookAtTargetId, Is.EqualTo(SurveyorChaseShot.AimId));
                Assert.That(tracked.TotalDurationSeconds, Is.EqualTo(old.TotalDurationSeconds).Within(.001f), "Subtitle and audio timing keep the same clock.");
                Assert.That(tracked.Validate(), Is.Empty);
                Assert.That(SurveyorChaseShot.EnsureTracking(tracked), Is.SameAs(tracked), "An already tracking plan is not rebuilt.");
            }
            finally { Object.DestroyImmediate(old); if (tracked != null && tracked != old) Object.DestroyImmediate(tracked); }
        }
        [Test]
        public void LandingSlidesDownTheSlopeBetweenTheSourcedTouchdowns()
        {
            float first = SurveyorLandingMotion.TravelRemaining(SurveyorLandingMotion.FirstContactSeconds);
            float second = SurveyorLandingMotion.TravelRemaining(SurveyorLandingMotion.SecondContactSeconds);
            float third = SurveyorLandingMotion.TravelRemaining(SurveyorLandingMotion.FinalContactSeconds);
            Assert.That(first - second, Is.EqualTo(SurveyorLandingMotion.FirstHopTravelMeters).Within(.01f), "About 20 m between the first and second touchdown.");
            Assert.That(second - third, Is.EqualTo(SurveyorLandingMotion.SecondHopTravelMeters).Within(.01f), "About 11 m between the second and third touchdown.");
            Assert.That(SurveyorLandingMotion.TravelRemaining(SurveyorLandingMotion.FinalContactSeconds + SurveyorLandingMotion.StopSeconds + .01f), Is.EqualTo(0f).Within(.001f), "It ends at rest at the final site.");
            Assert.That(SurveyorLandingMotion.HorizontalSpeed(0f), Is.EqualTo(SurveyorLandingMotion.DriftApproachMetersPerSecond).Within(.001f));
            float previous = SurveyorLandingMotion.TravelRemaining(0f);
            for (float t = .02f; t < 34f; t += .02f)
            {
                float remaining = SurveyorLandingMotion.TravelRemaining(t);
                Assert.That(remaining, Is.LessThanOrEqualTo(previous + 1e-4f), "It never moves back uphill (t=" + t + ").");
                float change = Mathf.Abs(SurveyorLandingMotion.HorizontalSpeed(t) - SurveyorLandingMotion.HorizontalSpeed(t - .02f)) / .02f;
                Assert.That(change, Is.LessThan(10f), "Sideways speed must change smoothly (t=" + t + ").");
                previous = remaining;
            }
        }
        [Test]
        public void ArrivalCraftTravelsOverTheTerrainAndTheChaseCameraFollowsIt()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var film = instance.GetComponent<SurveyorDocumentaryFilm>();
                film.Evaluate("Arrival", 33f); var home = film.Spacecraft.position;
                film.Evaluate("Arrival", SurveyorLandingMotion.FirstContactSeconds);
                Assert.That(Vector3.ProjectOnPlane(film.Spacecraft.position - home, Vector3.up).magnitude, Is.EqualTo(SurveyorLandingMotion.TravelRemaining(SurveyorLandingMotion.FirstContactSeconds)).Within(.01f));
                Assert.That(film.Spacecraft.position.x, Is.GreaterThan(home.x + 20f), "It first touches down uphill of its resting place.");
                for (float t = SurveyorLandingMotion.FirstContactSeconds - 1f; t < SurveyorLandingMotion.FinalContactSeconds + 1f; t += .25f)
                {
                    film.Evaluate("Arrival", t);
                    var craft = film.Spacecraft.position;
                    Assert.That(Vector3.ProjectOnPlane(film.ChaseRig.position - craft, Vector3.up).magnitude, Is.LessThan(4f), "The camera body trails the craft closely (t=" + t + ").");
                    Assert.That(Vector3.Distance(film.ChaseTarget.position, craft + Vector3.up * 1.1f), Is.LessThan(2.5f), "The craft stays in the aim point (t=" + t + ").");
                    Assert.That(film.ChaseRig.position.y, Is.EqualTo(SurveyorLunarSurface.Height(film.ChaseRig.position.x, film.ChaseRig.position.z)).Within(.001f), "The rig stands on the ground.");
                }
                film.Evaluate("Arrival", 33f);
                Assert.That(Vector3.Distance(film.Spacecraft.position, home), Is.LessThan(.001f), "It comes to rest exactly where the scene was built.");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test]
        public void LandingContactsAbsorbTheImpactInsteadOfKickingTheCraft()
        {
            const float dt = .001f;
            float Speed(float t) => (SurveyorLandingMotion.Height(t + dt) - SurveyorLandingMotion.Height(t - dt)) / (2f * dt);
            for (int i = 0; i < 3; i++)
            {
                float contact = SurveyorLandingMotion.ContactTime(i);
                // Speed changes continuously through the touch: no instant reversal.
                Assert.That(Mathf.Abs(Speed(contact + 3f * dt) - Speed(contact - 3f * dt)), Is.LessThan(2f), "Contact " + i + " must not reverse speed instantly.");
                Assert.That(Speed(contact - 3f * dt), Is.LessThan(0f), "The craft arrives moving downward at contact " + i + ".");
                float lowest = 0f;
                for (float t = contact; t < contact + .8f; t += .005f) lowest = Mathf.Min(lowest, SurveyorLandingMotion.Height(t));
                Assert.That(lowest, Is.GreaterThan(-.12f), "Leg crush at contact " + i + " stays in centimetres.");
                Assert.That(lowest, Is.LessThan(-.005f), "Contact " + i + " must visibly compress the legs.");
            }
            Assert.That(Speed(SurveyorLandingMotion.FirstContactSeconds + .1f), Is.LessThan(1f), "The first hop starts slowly instead of kicking off at full speed.");
            float fastest = 0f;
            for (float t = SurveyorLandingMotion.FirstContactSeconds + .05f; t < SurveyorLandingMotion.FirstContactSeconds + 1.4f; t += .02f)
                fastest = Mathf.Max(fastest, Mathf.Abs((Speed(t + .01f) - Speed(t - .01f)) / .02f));
            Assert.That(fastest, Is.LessThan(25f), "The first push builds speed gradually.");
            Assert.That(SurveyorLandingMotion.Height(SurveyorLandingMotion.FinalContactSeconds + 1f), Is.EqualTo(0f).Within(.0001f), "The craft ends at rest on the surface.");
            // After a touch the craft rocks to one peak and recovers; it must not ring like a spring.
            for (int i = 0; i < 3; i++)
            {
                float contact = SurveyorLandingMotion.ContactTime(i), previous = 0f; int direction = 0, reversals = 0;
                for (float t = contact; t < contact + (i == 2 ? .7f : 1.5f); t += .02f)
                {
                    float angle = Quaternion.Angle(Quaternion.identity, SurveyorLandingMotion.ContactTilt(t));
                    Assert.That(angle, Is.LessThan(3.5f));
                    if (Mathf.Abs(angle - previous) < .01f) continue;
                    int sign = angle > previous ? 1 : -1;
                    if (direction != 0 && sign != direction) reversals++;
                    direction = sign; previous = angle;
                }
                Assert.That(reversals, Is.LessThanOrEqualTo(1), "Tilt after contact " + i + " rises to one peak and recovers.");
            }
            Assert.That(Quaternion.Angle(Quaternion.identity, SurveyorLandingMotion.ContactTilt(SurveyorLandingMotion.FinalContactSeconds + 4f)), Is.LessThan(.001f));
        }
        [Test]
        public void ExhaustUsesMetersAndEclipseRecoversBeforeLunarNight()
        {
            var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var film=instance.GetComponent<SurveyorDocumentaryFilm>();film.Evaluate("Arrival",8f);
                var jet=instance.transform.Find("Surveyor/RetroStage/Retro exhaust");
                Assert.That(jet.gameObject.activeInHierarchy,Is.True);
                Assert.That(jet.lossyScale.y,Is.EqualTo(1.4f).Within(.01f),"Imported centimetre scale must not magnify the exhaust.");
                var effects=instance.GetComponent<SurveyorLandingEffects>();Assert.That(effects.VerniersVisible,Is.True);
                film.Evaluate("Arrival",33f);Assert.That(effects.VerniersVisible,Is.False);
                var sun=instance.GetComponentInChildren<Light>();
                film.Evaluate("Night",10f);float eclipsed=sun.intensity;
                film.Evaluate("Night",17f);Assert.That(sun.intensity,Is.GreaterThan(eclipsed*5f));
                var eclipse=instance.GetComponentInChildren<SurveyorEclipseImage>(true);
                Assert.That(eclipse.Occultation,Is.Zero);
                float recovered=sun.intensity;film.Evaluate("Night",32f);Assert.That(sun.intensity,Is.LessThan(recovered*.1f));
            }
            finally{Object.DestroyImmediate(instance);}
        }
        [Test]
        public void AstronautFacesTravelAndItsStanceFootStaysPlanted()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast = instance.GetComponentInChildren<SurveyorApolloCast>(true);
                cast.Evaluate(2f,true); var before = cast.Astronauts[0].transform.position; var planted = cast.FootPosition(0,1);
                cast.Evaluate(2.04f,true); var motion = cast.Astronauts[0].transform.position-before;
                Assert.That(Vector3.Dot(cast.FacingDirection(0).normalized,motion.normalized),Is.GreaterThan(.9f));
                Assert.That(Vector3.Distance(planted,cast.FootPosition(0,1)),Is.LessThan(.003f),"The stance ankle must not slide forward with the body.");
                Assert.That(cast.FootPosition(0,1).y-SurveyorLunarSurface.Height(planted.x,planted.z),Is.EqualTo(.13f).Within(.008f));
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test]
        public void PickupStartsAtItsMountAndSeekingBackRestoresTheScoop()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast = instance.GetComponentInChildren<SurveyorApolloCast>(true);
                cast.Evaluate(SurveyorApolloCast.CameraPickupSeconds,true);
                var camera = instance.transform.Find("Recovered camera assembly");
                var before = camera.position;
                cast.Evaluate(SurveyorApolloCast.CameraPickupSeconds+.02f,true);
                Assert.That(Vector3.Distance(camera.position,before),Is.LessThan(.015f),"The camera must leave its mount continuously.");
                cast.Evaluate(40f,true);cast.Evaluate(1f,true);
                var arm = instance.transform.Find("ScoopRig");
                Assert.That(arm.Find(SurveyorScoop.ScoopToolPath).gameObject.activeSelf,Is.True);
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test]
        public void CameraRisesClearOfItsMountAndTravelsSmoothlyToTheHand()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast = instance.GetComponentInChildren<SurveyorApolloCast>(true);
                var camera = instance.transform.Find("Recovered camera assembly");
                cast.Evaluate(SurveyorApolloCast.CameraPickupSeconds, true);
                var mount = camera.position;
                cast.Evaluate(SurveyorApolloCast.CameraPickupSeconds + SurveyorApolloCast.CameraLiftSeconds * .5f, true);
                Assert.That(camera.position.y, Is.GreaterThan(mount.y + .05f), "The camera must come up off its mount before swinging away.");
                float lateral = Vector3.ProjectOnPlane(camera.position - mount, Vector3.up).magnitude;
                Assert.That(lateral, Is.LessThan(.12f), "Mid-lift it is still above its mount, not dragged through the frame.");
                // No frame of the extraction may jump: it is one continuous path from the mount to the hand.
                cast.Evaluate(SurveyorApolloCast.CameraPickupSeconds, true);
                var last = camera.position; float worst = 0f, until = SurveyorApolloCast.CameraPickupSeconds + SurveyorApolloCast.CameraLiftSeconds * SurveyorApolloCast.CameraCarryStartFraction + SurveyorApolloCast.CameraCarrySeconds + .5f;
                for (float t = SurveyorApolloCast.CameraPickupSeconds + 1f / 60f; t < until; t += 1f / 60f)
                {
                    cast.Evaluate(t, true); worst = Mathf.Max(worst, Vector3.Distance(camera.position, last)); last = camera.position;
                }
                Assert.That(worst, Is.LessThan(.03f), "Camera moved more than 3 cm in one 60 Hz frame.");
                var attachment = System.Array.Find(instance.GetComponentsInChildren<Transform>(true), t => t.name == "Camera carry attachment");
                Assert.That(Vector3.Distance(camera.position, attachment.position), Is.LessThan(.8f), "The camera ends in the astronaut's hand.");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test]
        public void WalkingArmsSwingAgainstEachOtherSmoothly()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast = instance.GetComponentInChildren<SurveyorApolloCast>(true);
                var forward = new System.Collections.Generic.List<float>[] { new System.Collections.Generic.List<float>(), new System.Collections.Generic.List<float>() };
                var last = new Vector3[2]; float worst = 0f;
                for (float t = 3f; t < 7f; t += 1f / 60f)
                {
                    cast.Evaluate(t, true);
                    var root = cast.Astronauts[0].transform.position; var facing = cast.FacingDirection(0).normalized;
                    for (int arm = 0; arm < 2; arm++)
                    {
                        var local = cast.HandPosition(0, arm) - root;
                        forward[arm].Add(Vector3.Dot(local, facing));
                        if (t > 3f) worst = Mathf.Max(worst, Vector3.Distance(local, last[arm]));
                        last[arm] = local;
                    }
                }
                for (int arm = 0; arm < 2; arm++)
                    Assert.That(Mathf.Max(forward[arm].ToArray()) - Mathf.Min(forward[arm].ToArray()), Is.GreaterThan(.2f), "Arm " + arm + " must visibly swing while walking.");
                float mean0 = 0f, mean1 = 0f;
                for (int i = 0; i < forward[0].Count; i++) { mean0 += forward[0][i]; mean1 += forward[1][i]; }
                mean0 /= forward[0].Count; mean1 /= forward[0].Count;
                float together = 0f;
                for (int i = 0; i < forward[0].Count; i++) together += (forward[0][i] - mean0) * (forward[1][i] - mean1);
                Assert.That(together, Is.LessThan(0f), "The arms swing in opposition.");
                Assert.That(worst, Is.LessThan(.04f), "A hand moved more than 4 cm between 60 Hz frames.");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test]
        public void HandReachesTheScoopBeforeTakingItAndCarriesItAwayWithoutAJump()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast = instance.GetComponentInChildren<SurveyorApolloCast>(true);
                var native = instance.transform.Find("ScoopRig").Find(SurveyorScoop.ScoopToolPath).GetChild(0);
                var carried = instance.transform.Find("Recovered scoop");
                cast.Evaluate(SurveyorApolloCast.ScoopPickupSeconds - .01f, true);
                var bucketCenter = native.TransformPoint(native.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                float reach = Vector3.Distance(cast.HandPosition(SurveyorApolloCast.ScoopActor, 1), bucketCenter);
                Assert.That(reach, Is.InRange(.06f, .25f), "The hand is beside the bucket when it takes it: close enough to hold it, not pulling it from a distance and not inside it.");
                Vector3 Scoop() => carried.gameObject.activeSelf ? carried.position : native.position;
                var last = Scoop(); float worst = 0f;
                for (float t = SurveyorApolloCast.ScoopPickupSeconds; t < SurveyorApolloCast.ScoopPickupSeconds + 4f; t += 1f / 60f)
                {
                    cast.Evaluate(t, true); worst = Mathf.Max(worst, Vector3.Distance(Scoop(), last)); last = Scoop();
                }
                Assert.That(worst, Is.LessThan(.03f), "The scoop moved more than 3 cm in one 60 Hz frame.");
                Assert.That(carried.gameObject.activeSelf, Is.True);
                var held = carried.TransformPoint(carried.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                Assert.That(Vector3.Distance(cast.HandPosition(SurveyorApolloCast.ScoopActor, 1), held), Is.InRange(.06f, .25f), "The carried bucket stays at the hand.");
                Assert.That(DistanceToSegment(held, cast.ElbowPosition(SurveyorApolloCast.ScoopActor, 1), cast.HandPosition(SurveyorApolloCast.ScoopActor, 1)), Is.GreaterThan(.06f), "The bucket must not sit inside the astronaut's forearm.");
            }
            finally { Object.DestroyImmediate(instance); }
        }
        static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            var line = to - from; float along = Mathf.Clamp01(Vector3.Dot(point - from, line) / line.sqrMagnitude);
            return Vector3.Distance(point, from + line * along);
        }
        [Test]
        public void HandClosesAroundTheCameraHousingFromOutsideItAndNeverThroughIt()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var cast = instance.GetComponentInChildren<SurveyorApolloCast>(true);
                foreach (float t in new[] { 13f, 26.9f, 27.5f, 29f, 33f, 40f, 44f })
                {
                    cast.Evaluate(t, true);
                    var housing = System.Array.Find(instance.transform.Find("Recovered camera assembly").GetComponentsInChildren<Renderer>(true), r => r.name.Contains("CameraHousing"));
                    if (t < SurveyorApolloCast.CameraPickupSeconds) housing = System.Array.Find(instance.transform.Find("Surveyor").GetComponentsInChildren<Renderer>(true), r => r.name.Contains("CameraHousing") && !r.transform.IsChildOf(instance.transform.Find("Recovered camera assembly")));
                    var bounds = housing.bounds; float radius = .5f * Mathf.Min(bounds.size.x, bounds.size.z);
                    float wristToAxis = Vector3.Cross(housing.transform.up, cast.HandPosition(0, 0) - bounds.center).magnitude;
                    Assert.That(wristToAxis, Is.GreaterThan(radius + .02f), "Wrist inside the camera housing at " + t + " s.");
                    Assert.That(Vector3.Distance(cast.HandPosition(0, 0), bounds.center), Is.LessThan(.45f), "Hand far from the camera at " + t + " s.");
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }
        [Test]
        public void DuskDarkensTheFillLightAndDaylightRestoresIt()
        {
            var ambient = RenderSettings.ambientLight; float reflection = RenderSettings.reflectionIntensity;
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath));
            try
            {
                var film = instance.GetComponent<SurveyorDocumentaryFilm>();
                film.Evaluate("Night", 10f); var day = RenderSettings.ambientLight; float dayReflection = RenderSettings.reflectionIntensity;
                film.Evaluate("Night", 32f);
                Assert.That(RenderSettings.ambientLight.maxColorComponent, Is.LessThan(day.maxColorComponent * .6f), "The probe must fall into the dark with the sun, not glow on ambient fill.");
                Assert.That(RenderSettings.reflectionIntensity, Is.LessThan(dayReflection * .4f));
                film.Evaluate("Night", 25f);
                Assert.That(RenderSettings.ambientLight.maxColorComponent, Is.InRange(day.maxColorComponent * SurveyorDocumentaryFilm.DuskAmbientScale, day.maxColorComponent), "Dusk is gradual.");
                film.Evaluate("Reunion", 1f);
                Assert.That(RenderSettings.ambientLight.maxColorComponent, Is.EqualTo(day.maxColorComponent).Within(.0005f), "Daylight returns for the reunion.");
            }
            finally { Object.DestroyImmediate(instance); RenderSettings.ambientLight = ambient; RenderSettings.reflectionIntensity = reflection; }
        }
        [Test]
        public void PressureFindsRealSupportWhenThePlayerExcavatedItsPreferredPoint()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorDocumentaryBuilder.PrefabPath);
            var instance = Object.Instantiate(prefab);
            var scoop = instance.GetComponentInChildren<SurveyorDirectScoop>(true);
            try
            {
                scoop.Initialize(); scoop.Deploy(1f);
                // Reproduce a mouse stroke through the preferred pressure point.
                for (int pass = 0; pass < 6; pass++)
                    for (int segment = 0; segment < 100; segment++)
                    {
                        var from = new Vector3(-.275f + segment * .0055f, 0f, -.11f);
                        scoop.Soil.Cut(from, from + Vector3.right * .0055f, .075f);
                    }
                scoop.SetAim(new Vector3(.16f, 0f, -.11f));
                for (int frame = 0; frame < 144 * 3; frame++) scoop.Tick(1f / 144f, false, true, 0f, false);
                float contactSeconds = 0f;
                for (int frame = 0; frame < 144 * 10; frame++)
                {
                    scoop.Tick(1f / 144f, true, true, 0f, false);
                    if (scoop.InGroundContact) contactSeconds += 1f / 144f;
                }
                Assert.That(contactSeconds, Is.GreaterThan(3.5f), "A real supporting edge permits the pressure trial.");
                Assert.That(scoop.ContactPointLocal.y, Is.EqualTo(scoop.Soil.Height(scoop.ContactPointLocal.x, scoop.ContactPointLocal.z)).Within(.02f));
            }
            finally
            {
                if (scoop.Soil.RuntimeMesh != null) Object.DestroyImmediate(scoop.Soil.RuntimeMesh);
                Object.DestroyImmediate(instance);
            }
        }
        static SurveyorDocumentaryHost Current => Object.FindAnyObjectByType<SurveyorDocumentaryHost>();
        [UnityTest] public IEnumerator DocumentaryJourneyDeformsPlayerGroundAndShowsCompleteActors()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene); yield return new EnterPlayMode();
            var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            var archive = Object.FindAnyObjectByType<SurveyorArchiveHost>(); archive.Initialize(); yield return null;
            archive.Input.Disable(); archive.Input.Enable();
            archive.Owner.Begin(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(SurveyorChapterBuilder.ChapterPath));
            for (int i = 0; i < 500 && Current == null; i++) yield return null;
            var h = Current; Assert.That(h, Is.Not.Null); h.Clock.enabled = false;
            var stages = new List<string>(); h.Owner.StepEntered += stages.Add;
            Advance(h, 4); yield return Capture("01-machine");
            Advance(h, 23); yield return Capture("02-descent");
            Advance(h, 26); yield return Capture("03-contact");
            Advance(h, SurveyorDocumentaryTiming.BeforeScience + SurveyorDocumentaryTiming.Deploy + .3f - h.Clock.ActiveSeconds);
            Assert.That(h.Owner.CurrentStepId, Is.EqualTo("Science")); yield return Capture("04-instrument");
            Set(mouse.position, new Vector2(-2000, -2000));
            float distantGround = h.Scoop.Soil.Height(0f, .40f), scienceStart = h.Clock.ActiveSeconds;
            Keys(keyboard,Key.E); Advance(h,3f); Keys(keyboard);
            Assert.That(h.Scoop.Soil.RemovedCubicMeters,Is.Zero,"E must not operate the drag-only first stage.");
            MoveMouseToTool(h,mouse,new Vector3(-.22f,0f,0f),false); MoveMouseToTool(h,mouse,new Vector3(-.22f,0f,0f),true);Advance(h,.8f);
            for(int i=1;i<=32;i++){MoveMouseToTool(h,mouse,new Vector3(Mathf.Lerp(-.22f,.27f,i/32f),0f,0f),true);Advance(h,.2f);}
            for(int i=1;i<=28;i++){MoveMouseToTool(h,mouse,new Vector3(Mathf.Lerp(.27f,-.18f,i/28f),0f,0f),true);Advance(h,.2f);}
            Assert.That(h.CanFinishCut,Is.True,"Actual mouse strokes must prepare the trench. Travel="+h.Scoop.Soil.TravelMeters+" depth="+h.Scoop.Soil.DeepestMeters);
            Assert.That(h.View.GetComponentInChildren<UIDocument>().rootVisualElement.Q("effect"),Is.Null);
            Assert.That(h.Scoop.Soil.Height(0f,.40f),Is.EqualTo(distantGround).Within(.001f));
            yield return Capture("05-excavated");
            MoveMouseToTool(h,mouse,new Vector3(-.18f,0f,0f),false);Advance(h,.1f);
            Assert.That(h.SciencePhase,Is.EqualTo(2),"Releasing the completed drag must advance without an extra E confirmation.");
            MoveMouseToTool(h,mouse,new Vector3(.16f,0f,-.11f),true);Advance(h,2f);
            Assert.That(h.SciencePhase,Is.EqualTo(2),"Mouse must not operate the E-only pressure stage.");
            MoveMouseToTool(h,mouse,new Vector3(.16f,0f,-.11f),false);
            float depth=h.Scoop.Soil.DeepestMeters;h.PauseGame();Advance(h,5);Assert.That(h.Scoop.Soil.DeepestMeters,Is.EqualTo(depth));h.Resume();
            Keys(keyboard,Key.E);Advance(h,8f);Keys(keyboard);yield return null;yield return null;
            Assert.That(h.Owner.Evidence.PlayerPhotoCount, Is.EqualTo(1));
            Advance(h,SurveyorDocumentaryTiming.ResultRead+.1f);
            Assert.That(h.Owner.CurrentStepId, Is.EqualTo("Findings"));
            File.WriteAllText("QA/SUR-020/science-seconds.txt", (h.Clock.ActiveSeconds-scienceStart).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Advance(h,10f); yield return Capture("06-findings");
            Advance(h,SurveyorDocumentaryTiming.Findings-10f+8f); yield return Capture("07-night");
            Advance(h,SurveyorDocumentaryTiming.Night-8f+5f); Assert.That(h.Owner.CurrentStepId, Is.EqualTo("Reunion")); yield return Capture("08-astronauts-walking");
            var cast = Object.FindAnyObjectByType<SurveyorApolloCast>(); Assert.That(cast.Astronauts.Length, Is.EqualTo(2));
            foreach (var actor in cast.Astronauts) Assert.That(actor.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length, Is.GreaterThanOrEqualTo(16));
            Advance(h,17f); yield return Capture("09-inspection"); Advance(h,17f); yield return Capture("10-recovered");
            Advance(h,8f); yield return Capture("11-archive"); Advance(h,10.1f);
            Assert.That(h.Owner.State, Is.EqualTo(AppState.Completion));
            Assert.That(h.Localization.Problems, Is.Empty); Assert.That(h.Localization.MissingRequests, Is.Empty);
            Assert.That(h.Owner.LastRunActiveSeconds, Is.InRange(SurveyorDocumentaryTiming.CinematicSeconds+15f,SurveyorDocumentaryTiming.CinematicSeconds+60f));
            yield return Capture("12-memorial");
            File.WriteAllText("QA/SUR-020/technical-journey.txt", "Automated assisted controls, not novice timing.\nActive seconds "+h.Owner.LastRunActiveSeconds+"\n"+string.Join(" -> ",stages));
            Keys(keyboard,Key.E); Keys(keyboard);
            for (int i=0;i<300&&archive.Owner.State!=AppState.Dossier;i++)yield return null;
            Assert.That(archive.Owner.State,Is.EqualTo(AppState.Dossier)); Assert.That(archive.Owner.Evidence.Records,Is.Empty);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator RestartRecreatesSoilAndFocusStopsTheTool()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene); yield return new EnterPlayMode();
            var archive = Object.FindAnyObjectByType<SurveyorArchiveHost>(); archive.Initialize(); yield return null;
            for (int run = 0; run < 2; run++)
            {
                archive.Owner.Begin(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(SurveyorChapterBuilder.ChapterPath));
                for (int i = 0; i < 500 && Current == null; i++) yield return null;
                var h = Current; h.Clock.enabled = false; Advance(h, SurveyorDocumentaryTiming.BeforeScience + 3f);
                Assert.That(h.Scoop.Soil.RemovedCubicMeters, Is.Zero);
                h.GetComponent<FocusPause>().HandleFocusLost(); float time = h.Clock.ActiveSeconds; Advance(h, 3); Assert.That(h.Clock.ActiveSeconds, Is.EqualTo(time));
                h.Resume(); archive.Owner.ReturnToDossier(); for (int i = 0; i < 300 && archive.Owner.State != AppState.Dossier; i++) yield return null;
                Assert.That(Current, Is.Null); Assert.That(archive.Owner.Evidence.Records, Is.Empty);
            }
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator PilotSamplesCompleteBodyMotionWithoutClaimingPlayerProgress()
        {
            EditorSceneManager.OpenScene(SurveyorArchiveBuilder.Scene); yield return new EnterPlayMode(); yield return null;
            var archive = Object.FindAnyObjectByType<SurveyorArchiveHost>(); archive.Initialize();archive.Owner.Begin(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(SurveyorChapterBuilder.ChapterPath));
            for(int i=0;i<500&&Current==null;i++)yield return null;
            var h = Current; h.Clock.enabled = false; h.Sequence.Cancel(); h.enabled = false;
            var film = h.GetComponent<SurveyorDocumentaryFilm>(); var cameras = h.GetComponent<CameraDirector>(); var cast = Object.FindAnyObjectByType<SurveyorApolloCast>();
            var times = new[] { 0f, .5f, 1f, 1.5f, 2f, 5f, 12f, 18f, 30f, 34f, 38f, 44f };
            foreach (float t in times)
            {
                film.Evaluate("Reunion", t);
                var actor = cast.Astronauts[0]; var at = actor.transform.position + Vector3.up;
                cameras.Apply(new CameraPose(at + new Vector3(-2.4f, .3f, -3.2f), Quaternion.LookRotation(at - (at + new Vector3(-2.4f,.3f,-3.2f))), 38f));
                yield return null; yield return null; h.View.Render(t, 1f); yield return null;
                var rt = h.View.Raw; var previous = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();RenderTexture.active=previous;
                Directory.CreateDirectory("QA/SUR-020/motion");File.WriteAllBytes("QA/SUR-020/motion/"+t.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+".png",image.EncodeToPNG());Object.Destroy(image);
            }
            film.Evaluate("Reunion",44f);
            var center=(cast.Astronauts[0].transform.position+cast.Astronauts[1].transform.position)*.5f+Vector3.up;
            for(int i=0;i<6;i++)
            {
                var offset=Quaternion.AngleAxis(i*60f,Vector3.up)*new Vector3(0f,1.2f,-5.5f);
                cameras.Apply(new CameraPose(center+offset,Quaternion.LookRotation(center-(center+offset)),43f));
                yield return null;yield return null;h.View.Render(44,1);yield return null;
                SaveRaw(h.View.Raw,"QA/SUR-020/framing/"+i+".png");
            }
            foreach(var stage in new[]{"Arrival","Night"})
            {
                var samples=stage=="Arrival"?new[]{8f,12f,18.05f,21.5f,25.05f,27f,29f,33f}:new[]{5f,8f,10f,14f,16.5f,24f};
                foreach(float t in samples)
                {
                    h.Sequence.Play(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SurveyorDocumentaryBuilder.Folder+"/Sequences/"+stage+".asset"));
                    film.Evaluate(stage,t);h.Sequence.Tick(t);
                    yield return null;yield return null;h.View.Render(t,1);yield return null;
                    SaveRaw(h.View.Raw,"QA/SUR-020/film/"+stage+"-"+t.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png");h.Sequence.Cancel();
                }
            }
            yield return new ExitPlayMode();
        }
        static void SaveRaw(RenderTexture rt,string path)
        {
            var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();RenderTexture.active=previous;
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());Object.Destroy(image);
        }
        static void MoveMouseToTool(SurveyorDocumentaryHost h,Mouse mouse,Vector3 local,bool held)
        {
            var camera=h.GetComponentInChildren<Camera>();
            var uv=camera.WorldToViewportPoint(h.Scoop.Soil.transform.parent.TransformPoint(local));
            var screen=h.View.Screen;var rect=screen.worldBound;
            var panelPoint=new Vector2(rect.x+uv.x*rect.width,rect.y+(1f-uv.y)*rect.height);
            var zero=RuntimePanelUtils.ScreenToPanel(screen.panel,Vector2.zero);
            var edge=RuntimePanelUtils.ScreenToPanel(screen.panel,new Vector2(Screen.width,Screen.height));
            var position=new Vector2((panelPoint.x-zero.x)/(edge.x-zero.x)*Screen.width,Screen.height-(panelPoint.y-zero.y)/(edge.y-zero.y)*Screen.height);
            var state=new MouseState{position=position};if(held)state=state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);
            InputSystem.QueueStateEvent(mouse,state);InputSystem.Update();
        }
        static void Button(SurveyorDocumentaryHost host, string name)
        {
            var button = host.View.GetComponentInChildren<UIDocument>().rootVisualElement.Q<Button>(name);
            using var evt = NavigationSubmitEvent.GetPooled(); button.SendEvent(evt);
        }
        static void Keys(Keyboard keyboard,params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update(); }
        public static void Advance(SurveyorDocumentaryHost host, float seconds)
        {
            // UnityTest queues fixture events until the next player update; flush before advancing the deterministic clock.
            InputSystem.Update();
            while (seconds > .00001f)
            { float dt = Mathf.Min(.05f, seconds); host.Clock.Tick(dt); host.Tick(host.Clock.ActiveDeltaSeconds); seconds -= dt; }
            host.Clock.Tick(0);
        }
        public static IEnumerator Capture(string name, string folder = "QA/SUR-020")
        {
            var documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include); var originals = new Dictionary<PanelSettings, RenderTexture>(); var clear = new Dictionary<PanelSettings,bool>(); var target = new RenderTexture(1280, 720, 0); target.Create();
            foreach (var doc in documents)
            {
                if (doc.panelSettings == null || originals.ContainsKey(doc.panelSettings)) continue; var settings = doc.panelSettings; originals[settings] = settings.targetTexture;clear[settings]=settings.clearColor;settings.targetTexture=target;settings.clearColor=false;
            }
            var saved = RenderTexture.active; RenderTexture.active = target; GL.Clear(true, true, Color.black); RenderTexture.active = saved;
            for (int i = 0; i < 12; i++) yield return null;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); saved = RenderTexture.active; RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); RenderTexture.active = saved;
            Directory.CreateDirectory(folder); File.WriteAllBytes(folder + "/" + name + ".png", image.EncodeToPNG()); Object.Destroy(image);
            foreach (var pair in originals) if (pair.Key != null) { pair.Key.targetTexture = pair.Value;pair.Key.clearColor=clear[pair.Key]; }
            target.Release(); Object.Destroy(target); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
