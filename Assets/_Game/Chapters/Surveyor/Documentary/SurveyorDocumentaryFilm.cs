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
        public Transform Spacecraft => spacecraft;
        public Transform Eclipse => eclipse.transform;
        public void Configure(Transform craft, Transform retro, Transform target, GameObject exhaust, GameObject moon, GameObject lm, SurveyorApolloCast visitors, Light sun, SurveyorEclipseImage earth)
        { spacecraft = craft; retroMotor = retro; touchdownTarget = target; plume = exhaust; moonBackdrop = moon; lander = lm; cast = visitors; sunlight = sun; eclipse = earth; }
        public void Initialize()
        { if (ready) return; ready = true; settledPosition = spacecraft.position; settledRotation = spacecraft.rotation; retroBindPosition = retroMotor.localPosition; sunIntensity = sunlight.intensity; }
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
                spacecraft.position = settledPosition + Vector3.up * SurveyorLandingMotion.Height(seconds);
                spacecraft.rotation = settledRotation * SurveyorLandingMotion.ContactTilt(seconds);
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
