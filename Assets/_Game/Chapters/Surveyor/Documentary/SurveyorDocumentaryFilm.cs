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
            moonBackdrop.SetActive(opening); plume.SetActive(arrival && seconds < 26f); retroMotor.gameObject.SetActive(opening || arrival && seconds < 16f);
            surfaceWorld.SetActive(!opening); surfacePatch.SetActive(!opening);
            deployedArm.SetActive(!opening && !arrival);
            foreach (var part in flightArm) part.enabled = opening || arrival;
            lander.SetActive(reunion); eclipse.Show(stage == "Night" && seconds < 18f);
            spacecraft.rotation = settledRotation;
            if (opening) spacecraft.position = settledPosition + new Vector3(0f, 36f, 0f);
            else if (arrival)
            {
                float h = seconds < 13f ? Mathf.Lerp(40f, 12f, Mathf.SmoothStep(0f, 1f, seconds / 13f))
                    : seconds < 23f ? Mathf.Lerp(12f, 0f, Mathf.SmoothStep(0f, 1f, (seconds - 13f) / 10f))
                    : seconds < 26f ? 1.15f * Mathf.Sin((seconds - 23f) / 3f * Mathf.PI)
                    : seconds < 28.5f ? .35f * Mathf.Sin((seconds - 26f) / 2.5f * Mathf.PI) : 0f;
                spacecraft.position = settledPosition + Vector3.up * h;
                retroMotor.localPosition = retroBindPosition + Vector3.down * (seconds < 10f ? 0f : Mathf.Pow(seconds - 10f, 2f) * .9f);
            }
            else spacecraft.position = settledPosition;
            touchdownTarget.position = spacecraft.position + Vector3.up * 1.1f;
            float dusk = stage == "Night" ? Mathf.SmoothStep(0f, 1f, (seconds - 17f) / 15f) : 0f;
            sunlight.intensity = Mathf.Lerp(sunIntensity, .025f, dusk);
            sunlight.color = Color.Lerp(new Color(1f, .94f, .83f), new Color(.54f, .61f, .80f), dusk);
            eclipse.Evaluate(seconds);
            cast.Evaluate(stage == "Legacy" || stage == "End" ? SurveyorApolloCast.EndPoseSeconds : seconds, reunion);
        }
    }
}
