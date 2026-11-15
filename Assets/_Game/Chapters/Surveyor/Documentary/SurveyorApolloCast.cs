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
        [SerializeField] GameObject[] astronauts;
        [SerializeField] AnimationClip walk, inspect, stand;
        [SerializeField] Transform[] start, stop;
        [SerializeField] Transform recoveredCamera, cameraHome, cameraHand, recoveredScoop, scoopHand;
        [SerializeField] Renderer[] cameraParts;
        [SerializeField] Transform nativeArm;
        [SerializeField] Vector3 cameraGripLocal;
        Transform[][] bones;
        Quaternion[][] footBind;
        Quaternion[] alignment;
        Vector3[] modelForward;
        float[][] lateral;
        float[][] armSide;
        Vector3 scoopScale;
        bool initialized;
        // Hand target and carry blend of the camera, produced by the arm pass and read by the camera pass of the same Evaluate.
        Vector3 cameraGripWorld;
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
            bones = new Transform[count][]; footBind = new Quaternion[count][]; alignment = new Quaternion[count]; modelForward = new Vector3[count]; lateral = new float[count][]; armSide = new float[count][];
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
                footBind[i] = new Quaternion[2]; lateral[i] = new float[2];
                for (int leg = 0; leg < 2; leg++)
                {
                    var foot = bones[i][leg * 3 + 2];
                    footBind[i][leg] = Quaternion.Inverse(actor.transform.rotation) * foot.rotation;
                    lateral[i][leg] = (alignment[i] * actor.transform.InverseTransformPoint(foot.position)).x;
                }
            }
        }
        public void Evaluate(float seconds, bool visible)
        {
            Initialize();
            for (int i = 0; i < astronauts.Length; i++)
            {
                var actor = astronauts[i]; actor.SetActive(visible); if (!visible) continue;
                float t = Mathf.Max(0f, seconds - i * 1.3f), duration = i == 0 ? 10f : 12f;
                Vector3 route = Vector3.ProjectOnPlane(stop[i].position - start[i].position, Vector3.up), facing = route.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, facing);
                float u = Mathf.Clamp01(t / duration), distance = route.magnitude * Mathf.SmoothStep(0f, 1f, u);
                bool walking = t > 0f && t < duration;
                // Pose before assigning world heading: an imported root track must not undo it.
                stand.SampleAnimation(actor, 0f);
                var position = start[i].position + facing * distance;
                position.y = SurveyorLunarSurface.Height(position.x, position.z) + .012f;
                actor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing) * alignment[i]);
                float phase = distance / .42f, settle = Mathf.SmoothStep(0f, 1f, (t - duration) / 2f);
                float work = i == 0 ? settle * (1f - Mathf.SmoothStep(0f, 1f, (seconds - 31f) / 3f))
                    : Mathf.SmoothStep(0f, 1f, (seconds - 30f) / 3f) * (1f - Mathf.SmoothStep(0f, 1f, (seconds - 38f) / 3f));
                bones[i][12].position += Vector3.up * (-.05f + (walking ? .014f * Mathf.Pow(Mathf.Sin(phase * Mathf.PI * 2f), 2f) : -.035f * work));
                bones[i][13].rotation = Quaternion.AngleAxis(walking ? 3f : 8f * work, right) * bones[i][13].rotation;
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
                    Vector3 rest = bones[i][b].position + Vector3.down * .35f + right * side * .14f + facing * .10f;
                    Vector3 target = rest + facing * (walking ? Mathf.Sin((phase + arm * .5f) * Mathf.PI * 2f) * .075f : 0f);
                    if (i == 0 && arm == 0 && !walking)
                    {
                        var grip = cameraHome.TransformPoint(cameraGripLocal);
                        var carry = position + Vector3.up * 1.07f + facing * .40f + right * side * .30f;
                        cameraGripWorld = CameraGripPath(seconds, grip, carry, out cameraCarryBlend);
                        target = Vector3.Lerp(rest, cameraGripWorld, settle);
                    }
                    if (i == 1 && arm == 1 && seconds > 30f)
                    {
                        var source = nativeArm.GetChild(0);
                        var carry = position + Vector3.up * 1.02f + facing * .36f + right * side * .30f;
                        target = Vector3.Lerp(rest, Vector3.Lerp(source.position, carry, Mathf.SmoothStep(0f, 1f, (seconds - ScoopPickupSeconds) / 3f)), Mathf.Clamp01((seconds - 30f) / 3f));
                    }
                    IK(bones[i][b], bones[i][b + 1], bones[i][b + 2], target, position + right * side * .85f + Vector3.up * 1.3f + facing * .15f);
                }
                float turn = Mathf.Clamp(Vector3.SignedAngle(facing, Vector3.ProjectOnPlane(cameraHome.position - bones[i][14].position, Vector3.up), Vector3.up), -28f, 28f);
                bones[i][14].rotation = Quaternion.AngleAxis(turn * settle, Vector3.up) * bones[i][14].rotation;
            }
            bool cameraCarried = visible && seconds >= CameraPickupSeconds, scoopCarried = visible && seconds >= ScoopPickupSeconds;
            foreach (var part in cameraParts) part.enabled = !cameraCarried;
            recoveredCamera.gameObject.SetActive(cameraCarried); nativeArm.gameObject.SetActive(!scoopCarried); recoveredScoop.gameObject.SetActive(scoopCarried);
            if (cameraCarried)
            {
                float since = seconds - CameraPickupSeconds, carry = cameraCarryBlend;
                Vector3 facing = FacingDirection(0).normalized, right = Vector3.Cross(Vector3.up, facing);
                var carryRotation = Quaternion.AngleAxis(8f * armSide[0][0], facing) * cameraHome.rotation;
                // A small tug while it comes free of the mount, gone before the swing finishes.
                float tug = 2.2f * Mathf.Sin(Mathf.Clamp01(since / CameraLiftSeconds) * Mathf.PI) * (1f - carry);
                var rotation = Quaternion.AngleAxis(tug, right) * Quaternion.Slerp(cameraHome.rotation, carryRotation, carry);
                // The grip follows the same path as the hand target; once the swing ends the hand attachment holds it exactly.
                var analytic = cameraGripWorld - rotation * cameraGripLocal;
                var attached = cameraHand.position - rotation * cameraGripLocal;
                recoveredCamera.SetPositionAndRotation(Vector3.Lerp(analytic, attached, carry), rotation);
            }
            if (scoopCarried)
            {
                var source = nativeArm.GetChild(0); float transfer = Mathf.SmoothStep(0f, 1f, (seconds - ScoopPickupSeconds) / .8f);
                recoveredScoop.SetPositionAndRotation(Vector3.Lerp(source.position, scoopHand.position, transfer), Quaternion.Slerp(source.rotation, scoopHand.rotation, transfer)); recoveredScoop.localScale = scoopScale;
            }
        }
        /// <summary>Grip point of the camera: still at the mount, lifted clear of it, then eased toward the carry pose.</summary>
        Vector3 CameraGripPath(float seconds, Vector3 grip, Vector3 carry, out float carryBlend)
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
        static void IK(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, end.position);
            target = upper.position + Vector3.ClampMagnitude(target - upper.position, Mathf.Max(.01f, a + b - .006f));
            SurveyorScoop.SolveTwoBone(upper.position, target, a, b, pole - upper.position, out var joint);
            var rotation = end.rotation;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, joint - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
            end.rotation = rotation;
        }
    }
}
