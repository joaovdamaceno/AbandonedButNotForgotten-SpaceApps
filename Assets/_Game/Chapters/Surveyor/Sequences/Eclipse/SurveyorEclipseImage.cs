using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>One opaque sky card: angular sizes, occultation and an uneven refracted rim. No volume or extra camera.</summary>
    public sealed class SurveyorEclipseImage : MonoBehaviour
    {
        public const float EarthDiameterDegrees = 1.9f, SunDiameterDegrees = .53f;
        [SerializeField] Renderer image;
        MaterialPropertyBlock properties;
        static readonly int PhaseId = Shader.PropertyToID("_Phase");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        public float Phase { get; private set; }
        public float Occultation { get; private set; }
        public void Configure(Renderer renderer) => image = renderer;
        public void Place(Vector3 opticalPosition, Quaternion pointing)
        {
            // The card subtends 3.33 degrees; Earth occupies 57% of it (1.90 degrees).
            transform.SetPositionAndRotation(opticalPosition + pointing * Vector3.forward * 100, pointing);
            transform.localScale = Vector3.one * 5.82f;
        }
        public void Show(bool visible) { if (image != null) image.enabled = visible; }
        public void Evaluate(float timeSeconds)
        {
            Phase = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3, 16, timeSeconds));
            float separation = Mathf.Abs(Mathf.Lerp(.82f, -.82f, Phase));
            Occultation = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.41f, .73f, separation));
            properties ??= new MaterialPropertyBlock();
            properties.SetFloat(PhaseId, Phase);
            properties.SetFloat(ExposureId, Mathf.Lerp(.8f, 1.35f, Occultation));
            if (image != null) image.SetPropertyBlock(properties);
        }
    }
}
