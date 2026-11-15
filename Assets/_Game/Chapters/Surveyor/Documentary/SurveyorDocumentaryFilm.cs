using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Landing hardware, solar light and reunion evaluated from the same sequence time as the camera.</summary>
    public sealed class SurveyorDocumentaryFilm : MonoBehaviour
    {
        [SerializeField] Transform spacecraft, retroMotor, touchdownTarget;
        [SerializeField] GameObject plume, moonBackdrop, lander;
        [SerializeField] SurveyorApolloCast cast;
        [SerializeField] Light sunlight;
        [SerializeField] SurveyorEclipseImage eclipse;
        [SerializeField] GameObject deployedArm;
        [SerializeField] GameObject surfaceWorld, surfacePatch;
        [SerializeField] Renderer[] flightArm;
        [SerializeField] SurveyorLandingEffects landingEffects;
        Vector3 settledPosition, retroBindPosition; Quaternion settledRotation;
        float sunIntensity; bool ready;
        Transform chaseRig, chaseTarget;
        /// <summary>Camera rig that rides on the ground under a lagging average of the craft; shots offset from it, never rotate with the craft.</summary>
        public Transform ChaseRig { get { EnsureChase(); return chaseRig; } }
        /// <summary>Aim point of the tracking camera: the craft with a shorter lag than the camera body, like an operator panning.</summary>
        public Transform ChaseTarget { get { EnsureChase(); return chaseTarget; } }
        public Transform Spacecraft => spacecraft;
        public Transform Eclipse => eclipse.transform;
        public void Configure(Transform craft, Transform retro, Transform target, GameObject exhaust, GameObject moon, GameObject lm, SurveyorApolloCast visitors, Light sun, SurveyorEclipseImage earth)
        { spacecraft = craft; retroMotor = retro; touchdownTarget = target; plume = exhaust; moonBackdrop = moon; lander = lm; cast = visitors; sunlight = sun; eclipse = earth; }
        public void Initialize()
        { if (ready) return; ready = true; settledPosition = spacecraft.position; settledRotation = spacecraft.rotation; retroBindPosition = retroMotor.localPosition; sunIntensity = sunlight.intensity; }
        void EnsureChase()
        {
            if (chaseRig != null) return;
            chaseRig = new GameObject("Chase rig").transform; chaseRig.SetParent(transform, false);
            chaseTarget = new GameObject("Chase target").transform; chaseTarget.SetParent(transform, false);
        }
        /// <summary>World position of the craft at landing time: sliding down the slope over the real terrain plus the hop height.</summary>
        public Vector3 ArrivalPosition(float seconds)
        {
            Initialize();
            var ground = settledPosition + SurveyorLandingMotion.UphillDirection * SurveyorLandingMotion.TravelRemaining(seconds);
            float rise = SurveyorLunarSurface.Height(ground.x, ground.z) - SurveyorLunarSurface.Height(settledPosition.x, settledPosition.z);
            return new Vector3(ground.x, settledPosition.y + rise + SurveyorLandingMotion.Height(seconds), ground.z);
        }
        Vector3 LaggedPosition(float seconds, float windowSeconds, int samples)
        {
            var sum = Vector3.zero;
            for (int i = 0; i < samples; i++) sum += ArrivalPosition(Mathf.Max(0f, seconds - windowSeconds * i / (samples - 1)));
            return sum / samples;
        }
        void UpdateChase(float seconds)
        {
            EnsureChase();
            // The camera body trails the craft on the ground (it cannot be everywhere at once); the aim point trails less, so the craft stays framed.
            var body = LaggedPosition(seconds, 1.6f, 8); var aim = LaggedPosition(seconds, .5f, 4);
            chaseRig.position = new Vector3(body.x, SurveyorLunarSurface.Height(body.x, body.z), body.z);
            chaseTarget.position = aim + Vector3.up * 1.1f;
        }
        public void Evaluate(string stage, float seconds)
        {
            Initialize(); bool arrival = stage == "Arrival", opening = stage == "Prologue", reunion = stage == "Reunion" || stage == "Legacy" || stage == "End";
            moonBackdrop.SetActive(opening); plume.SetActive(arrival); retroMotor.gameObject.SetActive(opening || arrival && seconds < 16f);
            surfaceWorld.SetActive(!opening); surfacePatch.SetActive(!opening);
            deployedArm.SetActive(!opening && !arrival);
            foreach (var part in flightArm) part.enabled = opening || arrival;
            lander.SetActive(reunion); eclipse.Show(stage == "Night" && seconds < 18f);
            spacecraft.rotation = settledRotation;
            if (opening) spacecraft.position = settledPosition + new Vector3(0f, 36f, 0f);
            else if (arrival)
            {
                spacecraft.position = ArrivalPosition(seconds);
                spacecraft.rotation = settledRotation * SurveyorLandingMotion.ContactTilt(seconds);
                UpdateChase(seconds);
                retroMotor.localPosition = retroBindPosition + Vector3.down * (seconds < SurveyorLandingMotion.JettisonSeconds ? 0f : Mathf.Pow(seconds - SurveyorLandingMotion.JettisonSeconds, 2f) * .9f);
            }
            else spacecraft.position = settledPosition;
            landingEffects.Evaluate(seconds, arrival);
            touchdownTarget.position = spacecraft.position + Vector3.up * 1.1f;
            eclipse.Evaluate(seconds);
            float dusk = stage == "Night" ? Mathf.SmoothStep(0f, 1f, (seconds - 19f) / 13f) : 0f;
            float occultation = stage == "Night" && seconds < 17f ? eclipse.Occultation : 0f;
            sunlight.intensity = Mathf.Lerp(sunIntensity, .025f, dusk);
            sunlight.intensity *= 1f - .93f * occultation;
            sunlight.color = Color.Lerp(Color.Lerp(new Color(1f, .94f, .83f),new Color(.74f,.24f,.09f),occultation*.75f), new Color(.54f, .61f, .80f), dusk);
            cast.Evaluate(stage == "Legacy" || stage == "End" ? SurveyorApolloCast.EndPoseSeconds : seconds, reunion);
        }
    }
}
