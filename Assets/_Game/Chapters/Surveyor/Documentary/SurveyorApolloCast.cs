using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Whole-body poses on story time, with stance feet planted in world space.</summary>
    public sealed class SurveyorApolloCast : MonoBehaviour
    {
        public const float CameraPickupSeconds = 27f, ScoopPickupSeconds = 35f, EndPoseSeconds = 44f;
        /// <summary>The camera first rises clear of its mount, then swings to the carry pose while the lift fades out.</summary>
        public const float CameraLiftSeconds = 1.1f, CameraCarrySeconds = 2.2f, CameraCarryStartFraction = .6f;
        [SerializeField, Tooltip("How far the camera is lifted off its mount before it swings toward the carry pose.")] float cameraLiftMeters = .30f;
        /// <summary>Walking arms swing against the legs; the reach for the scoop is solved against the real scoop, not a fixed pose.</summary>
        public const float ArmSwingMeters = .17f, ArmLiftMeters = .04f, TorsoTwistDegrees = 3.5f;
        public const int ScoopActor = 1;
        const float ScoopReachSeconds = 30f, ScoopReachDuration = 3f, ComfortableReach = .85f, MinStandOffMeters = .30f, MaxStanceShiftMeters = .75f;
        /// <summary>The scoop hangs low and the suit's arm is short: he steps in and squats with the back straight, never bends over.</summary>
        const float ScoopCrouchMeters = .38f;
        /// <summary>The A7L glove as authored: palm half-thickness, and where the palm centre sits relative to the wrist bone along the fingers, toward the thumb and toward the palm face.</summary>
        const float PalmHalfThicknessMeters = .041f, PalmAlongMeters = .072f, PalmThumbSideMeters = .033f, PalmFaceMeters = .013f;
        /// <summary>Where around the camera housing the palm lies (degrees from the side facing the astronaut) and how far the fingers curl in toward the axis.</summary>
        const float CameraGraspDegrees = 60f, CameraWrapDegrees = 35f, ScoopWrapDegrees = 20f;
        [SerializeField] GameObject[] astronauts;
        [SerializeField] AnimationClip walk, inspect, stand;
        [SerializeField] Transform[] start, stop;
        [SerializeField] Transform recoveredCamera, cameraHome, cameraHand, recoveredScoop, scoopHand;
        [SerializeField] Renderer[] cameraParts;
        [SerializeField] Transform nativeArm;
        [SerializeField] Vector3 cameraGripLocal;
        Transform[][] bones;
        Quaternion[][] footBind;
        Quaternion[][] handBindInverse;
        // Camera housing and scoop bucket as seen from their own frames, and the grips found for them.
        Vector3 cameraAxisCenterLocal, cameraAxisLocal, cameraWristLocal, scoopWristLocal;
        float cameraRadius;
        Bounds bucketBounds;
        Quaternion cameraHandLocal, scoopHandLocal;
        Quaternion[] alignment;
        Vector3[] modelForward;
        float[][] lateral;
        float[][] armSide;
        Vector3 scoopScale;
        Vector3[] stopPoint;
        float scoopArmLength;
        bool initialized;
        // Wrist target, rotation and carry blend of the carried props, produced by the arm pass and read by the prop pass of the same Evaluate.
        Vector3 cameraWristWorld, scoopWristWorld;
        Quaternion cameraRotationWorld, scoopRotationWorld;
        float cameraCarryBlend;
        static readonly string[] Names = { "Thigh_L", "Shin_L", "Foot_L", "Thigh_R", "Shin_R", "Foot_R", "UpperArm_R", "ForeArm_R", "Hand_R", "UpperArm_L", "ForeArm_L", "Hand_L", "Pelvis", "Spine", "Head" };
        public GameObject[] Astronauts => astronauts;
        public Vector3 FacingDirection(int actor) => astronauts[actor].transform.TransformDirection(modelForward[actor]);
        public Vector3 FootPosition(int actor, int leg) => bones[actor][leg * 3 + 2].position;
        public Vector3 HandPosition(int actor, int arm) => bones[actor][arm == 0 ? 8 : 11].position;
        public Vector3 ElbowPosition(int actor, int arm) => bones[actor][arm == 0 ? 7 : 10].position;
        public void Configure(GameObject[] actors, AnimationClip walking, AnimationClip inspection, AnimationClip idle, Transform[] from, Transform[] to)
        { astronauts = actors; walk = walking; inspect = inspection; stand = idle; start = from; stop = to; }
        public void ConfigureRecovery(Transform camera, Transform home, Transform hand, Transform scoop, Transform scoopCarry, Renderer[] parts, Transform arm, Vector3 grip)
        { recoveredCamera = camera; cameraHome = home; cameraHand = hand; recoveredScoop = scoop; scoopHand = scoopCarry; cameraParts = parts; nativeArm = arm; cameraGripLocal = grip; }
        void Initialize()
        {
            if (initialized) return;
            initialized = true; scoopScale = recoveredScoop.localScale;
            int count = astronauts.Length;
            bones = new Transform[count][]; footBind = new Quaternion[count][]; alignment = new Quaternion[count]; modelForward = new Vector3[count]; lateral = new float[count][]; armSide = new float[count][]; handBindInverse = new Quaternion[count][];
            for (int i = 0; i < count; i++)
            {
                var actor = astronauts[i]; stand.SampleAnimation(actor, 0f);
                bones[i] = new Transform[Names.Length]; var all = actor.GetComponentsInChildren<Transform>(true);
                for (int b = 0; b < Names.Length; b++)
                    foreach (var candidate in all) if (candidate.name == Names[b]) { bones[i][b] = candidate; break; }
                // Imported foot bones run from ankle to toes. FBX +Z need not be the suit's front.
                modelForward[i] = Vector3.ProjectOnPlane(actor.transform.InverseTransformDirection(bones[i][2].up + bones[i][5].up), Vector3.up).normalized;
                alignment[i] = Quaternion.FromToRotation(modelForward[i], Vector3.forward);
                armSide[i] = new float[2];
                for (int arm = 0; arm < 2; arm++)
                {
                    var shoulder = bones[i][arm == 0 ? 6 : 9];
                    armSide[i][arm] = Mathf.Sign((alignment[i] * actor.transform.InverseTransformPoint(shoulder.position)).x);
                }
                // The glove is authored with fingers down and the thumb toward the body; remember that in the hand bone's own axes.
                handBindInverse[i] = new Quaternion[2];
                var modelRight = Vector3.Cross(Vector3.up, actor.transform.TransformDirection(modelForward[i]));
                for (int arm = 0; arm < 2; arm++)
                {
                    var hand = bones[i][arm == 0 ? 8 : 11];
                    handBindInverse[i][arm] = Quaternion.Inverse(Quaternion.LookRotation(hand.InverseTransformDirection(Vector3.down), hand.InverseTransformDirection(-armSide[i][arm] * modelRight)));
                }
                footBind[i] = new Quaternion[2]; lateral[i] = new float[2];
                for (int leg = 0; leg < 2; leg++)
                {
                    var foot = bones[i][leg * 3 + 2];
                    footBind[i][leg] = Quaternion.Inverse(actor.transform.rotation) * foot.rotation;
                    lateral[i][leg] = (alignment[i] * actor.transform.InverseTransformPoint(foot.position)).x;
                }
            }
            Renderer housing = null;
            foreach (var part in cameraParts) if (part.name.Contains("CameraHousing")) housing = part;
            var housingBounds = housing.bounds;
            cameraAxisCenterLocal = cameraHome.InverseTransformPoint(housingBounds.center); cameraAxisLocal = cameraHome.InverseTransformDirection(housing.transform.up).normalized;
            cameraRadius = Mathf.Clamp(.5f * Mathf.Min(housingBounds.size.x, housingBounds.size.z), .08f, .14f);
            bucketBounds = nativeArm.GetChild(0).GetComponent<MeshFilter>().sharedMesh.bounds;
            stopPoint = new Vector3[count];
            for (int i = 0; i < count; i++) stopPoint[i] = stop[i].position;
            scoopArmLength = Vector3.Distance(bones[ScoopActor][9].position, bones[ScoopActor][10].position) + Vector3.Distance(bones[ScoopActor][10].position, bones[ScoopActor][11].position);
            stopPoint[ScoopActor] = StanceNearScoop(ScoopActor);
        }
        /// <summary>Where the walk ends so the hand can really close on the scoop: the planned stance, moved toward the scoop only as far as its reach needs.</summary>
        Vector3 StanceNearScoop(int i)
        {
            var actor = astronauts[i]; Vector3 planned = stop[i].position; Vector3 homePosition = actor.transform.position; Quaternion homeRotation = actor.transform.rotation;
            Vector3 facing = Vector3.ProjectOnPlane(planned - start[i].position, Vector3.up).normalized;
            stand.SampleAnimation(actor, 0f);
            actor.transform.SetPositionAndRotation(new Vector3(planned.x, SurveyorLunarSurface.Height(planned.x, planned.z) + .012f, planned.z), Quaternion.LookRotation(facing) * alignment[i]);
            // The stance lowers the pelvis a little, so measure from the shoulder as it will be, not as it stands.
            Vector3 toScoop = nativeArm.GetChild(0).position - (bones[i][9].position + Vector3.down * (.085f + ScoopCrouchMeters));
            Vector3 flat = Vector3.ProjectOnPlane(toScoop, Vector3.up);
            float comfortable = ComfortableReach * scoopArmLength, within = Mathf.Sqrt(Mathf.Max(0f, comfortable * comfortable - toScoop.y * toScoop.y));
            float shift = Mathf.Clamp(flat.magnitude - Mathf.Max(within, MinStandOffMeters), 0f, MaxStanceShiftMeters);
            actor.transform.SetPositionAndRotation(homePosition, homeRotation);
            return planned + flat.normalized * shift;
        }
        public void Evaluate(float seconds, bool visible)
        {
            Initialize();
            for (int i = 0; i < astronauts.Length; i++)
            {
                var actor = astronauts[i]; actor.SetActive(visible); if (!visible) continue;
                float t = Mathf.Max(0f, seconds - i * 1.3f), duration = i == 0 ? 10f : 12f;
                Vector3 route = Vector3.ProjectOnPlane(stopPoint[i] - start[i].position, Vector3.up), facing = route.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, facing);
                float u = Mathf.Clamp01(t / duration), distance = route.magnitude * Mathf.SmoothStep(0f, 1f, u);
                bool walking = t > 0f && t < duration;
                // Pose before assigning world heading: an imported root track must not undo it.
                stand.SampleAnimation(actor, 0f);
                var position = start[i].position + facing * distance;
                position.y = SurveyorLunarSurface.Height(position.x, position.z) + .012f;
                actor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing) * alignment[i]);
                float phase = distance / .42f, settle = Mathf.SmoothStep(0f, 1f, (t - duration) / 2f);
                // Arms ease in as the stride starts and out as it ends; they never snap between rest and swing.
                float stride = walking ? Mathf.Min(Smoother(t / .8f), Smoother((duration - t) / .8f)) : 0f;
                float work = i == 0 ? settle * (1f - Mathf.SmoothStep(0f, 1f, (seconds - 31f) / 3f))
                    : Mathf.SmoothStep(0f, 1f, (seconds - 30f) / 3f) * (1f - Mathf.SmoothStep(0f, 1f, (seconds - 38f) / 3f));
                float crouch = i == ScoopActor ? ScoopCrouchMeters * Smoother((seconds - ScoopReachSeconds) / ScoopReachDuration) * (1f - Smoother((seconds - ScoopPickupSeconds) / 3f)) : 0f;
                bones[i][12].position += Vector3.up * (-.05f - crouch + (walking ? .014f * Mathf.Pow(Mathf.Sin(phase * Mathf.PI * 2f), 2f) : -.035f * work));
                // The shoulders turn a little against the hips, the right one coming forward with the left leg.
                bones[i][13].rotation = Quaternion.AngleAxis(-armSide[i][0] * TorsoTwistDegrees * stride * Mathf.Cos(phase * Mathf.PI * 2f), Vector3.up) * bones[i][13].rotation;
                for (int leg = 0; leg < 2; leg++)
                {
                    float offset = leg * .5f, cycle = Mathf.Floor(phase + offset), p = phase + offset - cycle;
                    float along = (cycle - offset) * .42f + .13f, lift = 0f;
                    if (p >= .62f)
                    {
                        float swing = (p - .62f) / .38f;
                        along += .42f * Mathf.SmoothStep(0f, 1f, swing); lift = .08f * Mathf.Sin(swing * Mathf.PI);
                    }
                    Vector3 planted = start[i].position + facing * along + right * lateral[i][leg];
                    Vector3 neutral = position + right * lateral[i][leg];
                    float blend = walking ? Mathf.Min(Mathf.Clamp01(t / .45f), Mathf.Clamp01((duration - t) / .8f)) : 0f;
                    var target = Vector3.Lerp(neutral, planted, blend);
                    target.y = SurveyorLunarSurface.Height(target.x, target.z) + .13f + lift * blend;
                    int b = leg * 3; IK(bones[i][b], bones[i][b + 1], bones[i][b + 2], target, position + facing * 2f);
                    bones[i][b + 2].rotation = actor.transform.rotation * footBind[i][leg];
                }
                for (int arm = 0; arm < 2; arm++)
                {
                    int b = arm == 0 ? 6 : 9; float side = armSide[i][arm];
                    Quaternion grasp = Quaternion.identity; float graspBlend = 0f;
                    Vector3 rest = bones[i][b].position + Vector3.down * .35f + right * side * .14f + facing * .10f;
                    // Each arm swings with the opposite leg: forward and a little up as the hand comes through, back and lower behind.
                    float forward = Mathf.Cos((phase + arm * .5f) * Mathf.PI * 2f);
                    Vector3 target = rest + (facing * (forward * ArmSwingMeters) + Vector3.up * (ArmLiftMeters * (forward * .5f + .5f)) - right * side * (.025f * forward)) * stride;
                    if (i == 0 && arm == 0 && !walking)
                    {
                        GraspCamera(position, facing);
                        var carry = position + Vector3.up * 1.05f + facing * .38f + right * side * .30f;
                        cameraWristWorld = CameraWristPath(seconds, cameraHome.TransformPoint(cameraWristLocal), carry, out cameraCarryBlend);
                        // A small tug while it comes free of the mount, gone before the swing finishes.
                        float tug = 2.2f * Mathf.Sin(Mathf.Clamp01((seconds - CameraPickupSeconds) / CameraLiftSeconds) * Mathf.PI) * (1f - cameraCarryBlend);
                        var carryRotation = Quaternion.AngleAxis(8f * side, facing) * cameraHome.rotation;
                        cameraRotationWorld = Quaternion.AngleAxis(tug, right) * Quaternion.Slerp(cameraHome.rotation, carryRotation, cameraCarryBlend);
                        target = Vector3.Lerp(rest, cameraWristWorld, settle); grasp = cameraRotationWorld * cameraHandLocal; graspBlend = settle;
                    }
                    if (i == ScoopActor && arm == 1 && seconds > ScoopReachSeconds)
                    {
                        // The hand goes to the bucket where it hangs, closes on it, and the bucket then moves with the hand: it is never pulled across the gap.
                        var bucket = nativeArm.GetChild(0);
                        GraspScoop(right * side);
                        var carry = position + Vector3.up * 1.0f + facing * .36f + right * side * .30f;
                        float reach = Smoother((seconds - ScoopReachSeconds) / ScoopReachDuration);
                        scoopWristWorld = Vector3.Lerp(bucket.TransformPoint(scoopWristLocal), carry, Smoother((seconds - ScoopPickupSeconds) / 3f));
                        scoopRotationWorld = bucket.rotation;
                        target = Vector3.Lerp(rest, scoopWristWorld, reach); grasp = scoopRotationWorld * scoopHandLocal; graspBlend = reach;
                    }
                    // The elbow points back, down and a little out, like a relaxed arm; a pole above the shoulder would fling it out like hands on hips.
                    IK(bones[i][b], bones[i][b + 1], bones[i][b + 2], target, bones[i][b].position + (right * side * .7f - facing * .35f + Vector3.down * .5f), false);
                    if (graspBlend > 0f) bones[i][b + 2].rotation = Quaternion.Slerp(bones[i][b + 2].rotation, grasp, graspBlend);
                }
                float turn = Mathf.Clamp(Vector3.SignedAngle(facing, Vector3.ProjectOnPlane(cameraHome.position - bones[i][14].position, Vector3.up), Vector3.up), -28f, 28f);
                bones[i][14].rotation = Quaternion.AngleAxis(turn * settle, Vector3.up) * bones[i][14].rotation;
            }
            bool cameraCarried = visible && seconds >= CameraPickupSeconds, scoopCarried = visible && seconds >= ScoopPickupSeconds;
            foreach (var part in cameraParts) part.enabled = !cameraCarried;
            recoveredCamera.gameObject.SetActive(cameraCarried); nativeArm.gameObject.SetActive(!scoopCarried); recoveredScoop.gameObject.SetActive(scoopCarried);
            // Both props ride on the hand's grip: the same rigid offset from the wrist at every frame, so the hand never lets go and never passes through them.
            if (cameraCarried) recoveredCamera.SetPositionAndRotation(cameraWristWorld - cameraRotationWorld * cameraWristLocal, cameraRotationWorld);
            if (scoopCarried) { recoveredScoop.SetPositionAndRotation(scoopWristWorld - scoopRotationWorld * scoopWristLocal, scoopRotationWorld); recoveredScoop.localScale = scoopScale; }
        }
        /// <summary>Palm lying on the camera housing like a hand around a glass: thumb up, fingers curling in around it, wrist outside the surface.</summary>
        void GraspCamera(Vector3 astronaut, Vector3 facing)
        {
            var center = cameraHome.TransformPoint(cameraAxisCenterLocal); var axis = cameraHome.TransformDirection(cameraAxisLocal).normalized;
            Vector3 toAstronaut = Vector3.ProjectOnPlane(astronaut - center, axis).normalized, sideward = Vector3.Cross(Vector3.up, facing) * armSide[0][0];
            float angle = CameraGraspDegrees * Mathf.Deg2Rad;
            Vector3 outward = Vector3.ProjectOnPlane(toAstronaut * Mathf.Cos(angle) + sideward * Mathf.Sin(angle), axis).normalized;
            Vector3 around = Vector3.Cross(axis, outward);
            if (Vector3.Dot(around, -toAstronaut * Mathf.Sin(angle) + sideward * Mathf.Cos(angle)) < 0f) around = -around;
            Grasp(0, 0, center + outward * cameraRadius, -outward, around, CameraWrapDegrees, out var wrist, out var rotation);
            cameraWristLocal = cameraHome.InverseTransformPoint(wrist); cameraHandLocal = Quaternion.Inverse(cameraHome.rotation) * rotation;
        }
        /// <summary>Palm lying flat against the side of the bucket nearest the astronaut's hand, fingers along it and thumb up.</summary>
        void GraspScoop(Vector3 handSide)
        {
            var bucket = nativeArm.GetChild(0); Vector3 best = bucket.right; float bestDot = -2f, half = bucketBounds.extents.x;
            for (int face = 0; face < 4; face++)
            {
                Vector3 normal = face == 0 ? bucket.right : face == 1 ? -bucket.right : face == 2 ? bucket.forward : -bucket.forward;
                float dot = Vector3.Dot(normal, handSide);
                if (dot > bestDot) { bestDot = dot; best = normal; half = face < 2 ? bucketBounds.extents.x : bucketBounds.extents.z; }
            }
            Vector3 inward = -best, along = Vector3.Cross(inward, Vector3.up);
            if (Vector3.Dot(Vector3.Cross(along, inward), Vector3.up) < 0f) along = -along;
            Grasp(ScoopActor, 1, bucket.TransformPoint(bucketBounds.center) + best * half, inward, along, ScoopWrapDegrees, out var wrist, out var rotation);
            scoopWristLocal = bucket.InverseTransformPoint(wrist); scoopHandLocal = Quaternion.Inverse(bucket.rotation) * rotation;
        }
        /// <summary>Wrist position and hand rotation for a palm resting on a surface point: fingers run along the surface and curl toward the object by the wrap angle, the thumb on the thumb side of a real right or left hand.</summary>
        void Grasp(int actor, int arm, Vector3 contact, Vector3 inward, Vector3 along, float wrapDegrees, out Vector3 wrist, out Quaternion rotation)
        {
            float wrap = wrapDegrees * Mathf.Deg2Rad;
            Vector3 fingers = (along * Mathf.Cos(wrap) + inward * Mathf.Sin(wrap)).normalized, palm = (inward * Mathf.Cos(wrap) - along * Mathf.Sin(wrap)).normalized;
            Vector3 thumb = arm == 0 ? Vector3.Cross(palm, fingers) : Vector3.Cross(fingers, palm);
            // The palm face meets the surface; the wrist bone sits behind and beside the palm centre.
            Vector3 centre = contact - palm * PalmHalfThicknessMeters;
            wrist = centre - fingers * PalmAlongMeters + thumb * PalmThumbSideMeters + palm * PalmFaceMeters;
            rotation = Quaternion.LookRotation(fingers, thumb) * handBindInverse[actor][arm];
        }
        /// <summary>Wrist of the camera hand: still at the mount, lifted clear of it, then eased toward the carry pose.</summary>
        Vector3 CameraWristPath(float seconds, Vector3 grip, Vector3 carry, out float carryBlend)
        {
            float since = Mathf.Max(0f, seconds - CameraPickupSeconds);
            carryBlend = Smoother((since - CameraLiftSeconds * CameraCarryStartFraction) / CameraCarrySeconds);
            float lift = Smoother(since / CameraLiftSeconds) * (1f - carryBlend);
            return Vector3.Lerp(grip, carry, carryBlend) + Vector3.up * (cameraLiftMeters * lift);
        }
        /// <summary>Quintic ease with zero speed and acceleration at both ends.</summary>
        static float Smoother(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }
        /// <param name="keepEnd">Feet stay flat on the ground; a hand instead keeps its angle to the forearm and follows it.</param>
        static void IK(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, bool keepEnd = true)
        {
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, end.position);
            target = upper.position + Vector3.ClampMagnitude(target - upper.position, Mathf.Max(.01f, a + b - .006f));
            SurveyorScoop.SolveTwoBone(upper.position, target, a, b, pole - upper.position, out var joint);
            var rotation = end.rotation;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, joint - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
            if (keepEnd) end.rotation = rotation;
        }
    }
}
