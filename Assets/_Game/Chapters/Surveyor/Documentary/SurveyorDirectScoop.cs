using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Assisted tip control on the actual work patch. IK and grains share the chapter's active clock.</summary>
    public sealed class SurveyorDirectScoop : MonoBehaviour
    {
        [SerializeField] Transform arm, patch;
        [SerializeField] SurveyorSoilSurface soil;
        [SerializeField] AudioSource motor;
        Transform shoulder, elbow, wrist, tool, tip;
        Transform[] grains;
        Vector3[] grainStart, grainVelocity;
        float[] grainAge;
        float upperLength, foreLength, retractSeconds, load;
        int nextGrain;
        Vector3 cursor, current, rest, retractFrom;
        Quaternion baseTool;
        bool initialized, contacting, retracting;
        public Vector3 TipLocal => current;
        public Vector3 AimLocal => new Vector3(cursor.x, soil.Height(cursor.x, cursor.z), cursor.z);
        public float Load => load;
        public bool InGroundContact { get; private set; }
        public Vector3 ContactPointLocal { get; private set; }
        public bool IsRetracted => retracting && retractSeconds >= 2.8f;
        public SurveyorSoilSurface Soil => soil;
        public Transform Tool => tool;
        public float ReachErrorMeters { get; private set; }
        public void Configure(Transform rig, Transform work, SurveyorSoilSurface surface)
        { arm = rig; patch = work; soil = surface; }
        public void Initialize()
        {
            if (initialized) return; initialized = true; soil.Initialize();
            shoulder = arm.Find(SurveyorScoop.ShoulderPath); elbow = arm.Find(SurveyorScoop.ElbowPath);
            wrist = arm.Find(SurveyorScoop.WristPath); tool = arm.Find(SurveyorScoop.ScoopToolPath); tip = arm.Find(SurveyorScoop.TipPath);
            upperLength = Vector3.Distance(shoulder.position, elbow.position); foreLength = Vector3.Distance(elbow.position, wrist.position);
            motor = arm.Find(SurveyorScoop.MotorPath).GetComponent<AudioSource>();
            rest = patch.InverseTransformPoint(arm.Find(SurveyorScoop.RestPosePath).position);
            current = rest; cursor = new Vector3(-.22f, .16f, 0f);
            baseTool = Quaternion.LookRotation(patch.right, patch.up);
            var pool = arm.Find(SurveyorScoop.GrainsPath); grains = new Transform[pool.childCount];
            grainStart = new Vector3[grains.Length]; grainVelocity = new Vector3[grains.Length]; grainAge = new float[grains.Length];
            for (int i = 0; i < grains.Length; i++) { grains[i] = pool.GetChild(i); grainAge[i] = 5f; grains[i].gameObject.SetActive(false); }
            SetPose(current, 8f);
        }
        public void SetAim(Vector3 localPoint)
        { cursor.x = Mathf.Clamp(localPoint.x, -.28f, .27f); cursor.z = Mathf.Clamp(localPoint.z, -.13f, .13f); }
        public void Deploy(float progress)
        {
            Initialize(); float u = Mathf.SmoothStep(0f, 1f, progress);
            current = Vector3.Lerp(rest, new Vector3(-.22f, .14f, 0f), u); SetPose(current, 8f);
        }
        public void Tick(float dt, bool contact, bool press, float volume, bool paused)
        {
            Initialize(); if (paused || dt <= 0f) { if (motor.isPlaying) motor.Pause(); return; }
            Vector3 before = current;
            if (retracting)
            {
                retractSeconds += dt;
                var raised = retractFrom + Vector3.up * .16f;
                current = retractSeconds < 1f ? Vector3.Lerp(retractFrom, raised, Mathf.SmoothStep(0f, 1f, retractSeconds))
                    : Vector3.Lerp(raised, rest, Mathf.SmoothStep(0f, 1f, (retractSeconds - 1f) / 1.8f));
                contact = false;
            }
            else
            {
                float floor = MinTipHeight(current.x, current.z, contact ? 26f : 8f);
                // Lower at the present point before dragging. Otherwise a fast pointer drags the bucket through air.
                if (contact && current.y > floor + .012f)
                    current.y = Mathf.MoveTowards(current.y, floor + .001f, dt * .28f);
                else if (!contact && current.y < floor + .12f)
                    current.y = Mathf.MoveTowards(current.y, floor + .13f, dt * .30f);
                else
                {
                    Vector2 next = Vector2.MoveTowards(new Vector2(current.x, current.z), new Vector2(cursor.x, cursor.z), dt * (contact ? .055f : .30f));
                    float height = MinTipHeight(next.x, next.y, contact ? 26f : 8f) + (contact ? .001f : .13f);
                    current = new Vector3(next.x, Mathf.Max(height, Mathf.MoveTowards(current.y, height, dt * .28f)), next.y);
                }
            }
            float cut = 0f;
            if (contact && current.y <= soil.Height(current.x, current.z) + .012f && !press)
                cut = soil.Cut(before, current, .075f);
            float supportHeight = MinTipHeight(current.x, current.z, 26f, out var supportPoint);
            InGroundContact = contact && current.y <= supportHeight + .004f;
            ContactPointLocal = supportPoint;
            // A tilted bucket can rest on an excavation's edge while its lip spans the hollow.
            // Compress the supporting soil, not an unreachable point at the bottom of that hollow.
            if (press && InGroundContact) soil.Press(supportPoint, .012f);
            contacting = contact; load = Mathf.MoveTowards(load, cut > 0f ? 1f : contact ? .28f : 0f, dt * 5f);
            if (cut > 0f) Emit(before, current);
            SetPose(current, contact ? 26f : 8f); soil.Flush(dt);
            bool moving = Vector3.Distance(before, current) > .0002f;
            if (moving || contact)
            {
                motor.volume = volume * Mathf.Lerp(.018f, .065f, load); motor.pitch = Mathf.Lerp(.95f, .73f, load);
                if (!motor.isPlaying) { motor.UnPause(); if (!motor.isPlaying) motor.Play(); }
            }
            else if (motor.isPlaying) motor.Stop();
            for (int i = 0; i < grains.Length; i++)
            {
                grainAge[i] += dt; float t = grainAge[i]; if (t > 1.2f) { grains[i].gameObject.SetActive(false); continue; }
                var p = grainStart[i] + grainVelocity[i] * t + Vector3.down * (.81f * t * t);
                var local = patch.InverseTransformPoint(p); float y = soil.Height(local.x, local.z);
                if (local.y < y) { local.y = y + .003f; p = patch.TransformPoint(local); }
                grains[i].position = p;
            }
        }
        public void Retract() { if (retracting) return; retracting = true; retractFrom = current; retractSeconds = 0f; }
        public void StopContact() { contacting = false; InGroundContact = false; if (motor != null) motor.Stop(); }
        float MinTipHeight(float x, float z, float tilt)
            => MinTipHeight(x, z, tilt, out _);
        float MinTipHeight(float x, float z, float tilt, out Vector3 supportPoint)
        {
            float height = soil.Height(x, z), angle = tilt * Mathf.Deg2Rad;
            supportPoint = new Vector3(x, height, z);
            for (int i = 1; i <= 4; i++)
                for (int side = -1; side <= 1; side++)
                {
                    float length = i * .035f, sx = x - Mathf.Cos(angle) * length, sz = z + side * .045f;
                    float sy = soil.Height(sx, sz), limit = sy - Mathf.Sin(angle) * length;
                    if (limit > height) { height = limit; supportPoint = new Vector3(sx, sy, sz); }
                }
            return height;
        }
        void SetPose(Vector3 local, float tilt)
        {
            Quaternion rotation = baseTool * Quaternion.Euler(tilt, 0f, 0f);
            Vector3 tipWorld = patch.TransformPoint(local);
            Vector3 desiredWrist = tipWorld - rotation * new Vector3(0f, -.03f, .17f);
            SurveyorScoop.SolveTwoBone(shoulder.position, desiredWrist, upperLength, foreLength, (patch.up + patch.forward * 1.3f).normalized, out var joint);
            shoulder.rotation = Quaternion.LookRotation(joint - shoulder.position, patch.up);
            elbow.position = joint; elbow.rotation = Quaternion.LookRotation(desiredWrist - joint, patch.up);
            wrist.position = joint + (desiredWrist - joint).normalized * foreLength; wrist.rotation = rotation; tool.localRotation = Quaternion.identity;
            ReachErrorMeters = Vector3.Distance(tip.position, tipWorld);
        }
        void Emit(Vector3 from, Vector3 to)
        {
            int i = nextGrain++ % grains.Length; grainAge[i] = 0f; grainStart[i] = patch.TransformPoint(to + Vector3.up * .007f);
            float variation = (i % 7) / 7f; grainVelocity[i] = patch.TransformDirection(new Vector3(.12f + variation * .12f, .12f + variation * .19f, (i % 2 == 0 ? -1f : 1f) * .07f));
            grains[i].gameObject.SetActive(true); grains[i].position = grainStart[i]; grains[i].localScale = Vector3.one * .32f;
        }
        void OnDisable() => StopContact();
    }
}
