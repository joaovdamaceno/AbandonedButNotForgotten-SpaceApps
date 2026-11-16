using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Thin exhaust and surface grains evaluated on sequence time. No atmospheric smoke or wind.</summary>
    public sealed class SurveyorLandingEffects : MonoBehaviour
    {
        [SerializeField] Transform mainJet;
        [SerializeField] Transform[] vernierJets, grains;
        [SerializeField] Vector3 origin;
        Vector3[] baseScale;
        bool initialized;
        public bool VerniersVisible { get; private set; }
        public void Configure(Transform main, Transform[] verniers, Transform[] debris, Vector3 touchdown)
        { mainJet = main; vernierJets = verniers; grains = debris; origin = touchdown; }
        public void Evaluate(float time, bool active)
        {
            if (!initialized) { initialized = true; baseScale = new Vector3[vernierJets.Length]; for (int i = 0; i < vernierJets.Length; i++) baseScale[i] = vernierJets[i].localScale; }
            mainJet.gameObject.SetActive(active && time < SurveyorLandingMotion.JettisonSeconds);
            VerniersVisible = active && time > 5f && time < SurveyorLandingMotion.FinalContactSeconds + .10f;
            for (int i = 0; i < vernierJets.Length; i++)
            {
                vernierJets[i].gameObject.SetActive(VerniersVisible);
                float idle = time < SurveyorLandingMotion.FirstContactSeconds ? 1f : .42f;
                vernierJets[i].localScale = Vector3.Scale(baseScale[i], new Vector3(1f, idle * (1f + .04f * Mathf.Sin(time * 32f + i * 2f)), 1f));
            }
            for (int i = 0; i < grains.Length; i++)
            {
                int contact = i / 12; float age = time - SurveyorLandingMotion.ContactTime(contact);
                bool visible = active && age >= 0f && age < 1.5f; grains[i].gameObject.SetActive(visible); if (!visible) continue;
                float a = i * 2.399963f; var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float speed = .25f + (i % 5) * .08f;
                // Each contact happens where the craft actually touched down, further up the slope than the final resting place.
                var site = origin + SurveyorLandingMotion.UphillDirection * SurveyorLandingMotion.TravelRemaining(SurveyorLandingMotion.ContactTime(contact));
                site.y = SurveyorLunarSurface.Height(site.x, site.z);
                var p = site + radial * (.35f + speed * age) + Vector3.up * ((.21f + i % 3 * .07f) * age - .5f * SurveyorLandingMotion.LunarGravity * age * age);
                p.y = Mathf.Max(p.y, SurveyorLunarSurface.Height(p.x, p.z) + .004f); grains[i].position = p;
            }
        }
    }
}
