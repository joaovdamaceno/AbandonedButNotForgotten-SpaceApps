using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Whole skeletal actors sampled on active story time. No separate feet or camera-relative glove.</summary>
    public sealed class SurveyorApolloCast : MonoBehaviour
    {
        [SerializeField] GameObject[] astronauts;
        [SerializeField] AnimationClip walk, inspect, stand;
        [SerializeField] Transform[] start, stop;
        [SerializeField] Transform recoveredCamera, cameraHome, cameraHand, recoveredScoop, scoopHand;
        [SerializeField] Renderer[] cameraParts;
        [SerializeField] Transform nativeArm;
        Vector3 scoopScale;
        bool initialized;
        Transform[][] bones;
        Quaternion[][] footBind;
        static readonly string[] BoneNames = { "Thigh_L", "Shin_L", "Foot_L", "Thigh_R", "Shin_R", "Foot_R", "UpperArm_R", "ForeArm_R", "Hand_R" };
        public GameObject[] Astronauts => astronauts;
        public void Configure(GameObject[] actors, AnimationClip walking, AnimationClip inspection, AnimationClip idle, Transform[] from, Transform[] to)
        { astronauts = actors; walk = walking; inspect = inspection; stand = idle; start = from; stop = to; }
        public void ConfigureRecovery(Transform camera, Transform home, Transform hand, Transform scoop, Transform scoopCarry, Renderer[] parts, Transform arm)
        { recoveredCamera = camera; cameraHome = home; cameraHand = hand; recoveredScoop = scoop; scoopHand = scoopCarry; cameraParts = parts; nativeArm = arm; }
        public void Evaluate(float seconds, bool visible)
        {
            if (!initialized)
            {
                initialized = true; scoopScale = recoveredScoop.localScale; bones = new Transform[astronauts.Length][]; footBind = new Quaternion[astronauts.Length][];
                for (int i = 0; i < astronauts.Length; i++)
                {
                    bones[i] = new Transform[BoneNames.Length]; var all = astronauts[i].GetComponentsInChildren<Transform>(true);
                    for (int j = 0; j < BoneNames.Length; j++) foreach (var candidate in all) if (candidate.name == BoneNames[j]) { bones[i][j] = candidate; break; }
                    stand.SampleAnimation(astronauts[i], 0f); footBind[i] = new[] { Quaternion.Inverse(astronauts[i].transform.rotation) * bones[i][2].rotation, Quaternion.Inverse(astronauts[i].transform.rotation) * bones[i][5].rotation };
                }
            }
            for (int i = 0; i < astronauts.Length; i++)
            {
                var actor = astronauts[i]; actor.SetActive(visible);
                if (!visible) continue;
                float t = Mathf.Max(0f, seconds - i * 1.8f), walkSeconds = i == 0 ? 12f : 14f;
                float u = Mathf.Clamp01(t / walkSeconds);
                var position = Vector3.Lerp(start[i].position, stop[i].position, u); position.y = SurveyorLunarSurface.Height(position.x, position.z) + .012f;
                actor.transform.SetPositionAndRotation(position, stop[i].rotation);
                var clip = t < walkSeconds ? walk : i == 0 && t < 40f ? inspect : stand;
                if (clip != null) clip.SampleAnimation(actor, t < walkSeconds ? t % clip.length : clip == inspect ? Mathf.Min(t - walkSeconds, clip.length - .01f) : 0f);
                for (int leg = 0; leg < 2; leg++)
                {
                    int b = leg * 3; var foot = bones[i][b + 2]; var target = foot.position;
                    float phase = (t / 2f + leg * .5f) % 1f;
                    float floor = SurveyorLunarSurface.Height(target.x, target.z) + .13f;
                    target.y = t < walkSeconds && phase > .6f ? Mathf.Max(target.y, floor) : floor;
                    IK(bones[i][b], bones[i][b + 1], foot, target, actor.transform.position + actor.transform.forward * 2f);
                    foot.rotation = actor.transform.rotation * footBind[i][leg];
                }
                if (i == 0 && t >= walkSeconds)
                {
                    var hand = bones[i][8]; var goal = cameraHome.position + Vector3.down * .18f;
                    if (seconds >= 34f) goal = Vector3.Lerp(goal, actor.transform.position + Vector3.up * 1.16f + actor.transform.forward * .38f, Mathf.SmoothStep(0f, 1f, (seconds - 34f) / 4f));
                    IK(bones[i][6], bones[i][7], hand, goal, actor.transform.position + actor.transform.right + Vector3.up * 1.2f);
                }
            }
            bool carrying = visible && seconds >= 34f;
            foreach (var part in cameraParts) part.enabled = !carrying;
            recoveredCamera.gameObject.SetActive(carrying);
            if (carrying) recoveredCamera.SetPositionAndRotation(cameraHand.position, cameraHand.rotation);
            if (visible && seconds >= 43f)
            {
                nativeArm.gameObject.SetActive(false); recoveredScoop.gameObject.SetActive(true);
                recoveredScoop.SetPositionAndRotation(scoopHand.position, scoopHand.rotation); recoveredScoop.localScale = scoopScale;
            }
            else recoveredScoop.gameObject.SetActive(false);
        }
        static void IK(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, end.position);
            SurveyorScoop.SolveTwoBone(upper.position, target, a, b, pole - upper.position, out var joint);
            var rotation = end.rotation;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, joint - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
            end.rotation = rotation;
        }
    }
}
