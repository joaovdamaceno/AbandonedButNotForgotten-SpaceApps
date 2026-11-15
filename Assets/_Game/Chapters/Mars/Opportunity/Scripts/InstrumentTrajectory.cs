using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    // Clearance is measured against the entire specimen, not only the chosen contact point.
    public static class InstrumentTrajectory
    {
        public const float RetractionSeconds = 3.4f;
        public static Vector3 Position(Vector3 contact, Vector3 rest, Bounds specimen, float withdrawn)
        {
            float u = Mathf.Clamp01(withdrawn);
            float ceiling = Mathf.Max(specimen.max.y + .38f, contact.y + .5f, rest.y + .3f);
            Vector3 lifted = new Vector3(contact.x, ceiling, contact.z);
            // Park on the rover side of the specimen before lowering the head.
            Vector3 clear = rest;
            clear.y = ceiling;
            if (u < .32f) return Vector3.Lerp(contact, lifted, Smooth(u / .32f));
            if (u < .78f) return Vector3.Lerp(lifted, clear, Smooth((u - .32f) / .46f));
            return Vector3.Lerp(clear, rest, Smooth((u - .78f) / .22f));
        }
        public static Quaternion Rotation(Quaternion contact, Quaternion rest, float withdrawn)
            => Quaternion.Slerp(contact, rest, Smooth(Mathf.InverseLerp(.55f, .94f, withdrawn)));
        static float Smooth(float u) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(u));
    }
}
