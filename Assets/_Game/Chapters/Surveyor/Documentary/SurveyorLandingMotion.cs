using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Editorial flight timing with lunar-gravity arcs, not an engineering reconstruction of thrust telemetry.</summary>
    public static class SurveyorLandingMotion
    {
        public const float LunarGravity = 1.62f, FirstContactSeconds = 18f, JettisonSeconds = 9f;
        public const float FirstHopMeters = 10f, SecondHopMeters = 3f;
        public static float FirstFlightSeconds => 2f * Mathf.Sqrt(2f * FirstHopMeters / LunarGravity);
        public static float SecondFlightSeconds => 2f * Mathf.Sqrt(2f * SecondHopMeters / LunarGravity);
        public static float SecondContactSeconds => FirstContactSeconds + FirstFlightSeconds;
        public static float FinalContactSeconds => SecondContactSeconds + SecondFlightSeconds;
        public static float ContactTime(int index) => index == 0 ? FirstContactSeconds : index == 1 ? SecondContactSeconds : FinalContactSeconds;
        public static float Arc(float age, float duration) => age <= 0f || age >= duration ? 0f : .5f * LunarGravity * age * (duration - age);
        public static float Height(float seconds)
        {
            if (seconds < FirstContactSeconds)
            {
                float u = Mathf.Clamp01(seconds / FirstContactSeconds), u2 = u * u, u3 = u2 * u;
                // Nonzero downward speed at contact; the old smoothstep stopped in midair before rebounding.
                return (2f * u3 - 3f * u2 + 1f) * 40f + (u3 - 2f * u2 + u) * FirstContactSeconds * -3f + (u3 - u2) * FirstContactSeconds * -1f;
            }
            if (seconds < SecondContactSeconds) return Arc(seconds - FirstContactSeconds, FirstFlightSeconds);
            if (seconds < FinalContactSeconds) return Arc(seconds - SecondContactSeconds, SecondFlightSeconds);
            float settled = seconds - FinalContactSeconds;
            return -.015f * Mathf.Sin(Mathf.Clamp01(settled / .35f) * Mathf.PI) * Mathf.Exp(-settled * 4f);
        }
        public static Quaternion ContactTilt(float seconds)
        {
            float angle = 0f;
            for (int i = 0; i < 3; i++)
            { float age = seconds - ContactTime(i); if (age >= 0f && age < 1.2f) angle += (.8f - i * .25f) * Mathf.Sin(age * 9f) * Mathf.Exp(-age * 5f); }
            return Quaternion.Euler(angle, 0f, angle * -.55f);
        }
    }
}
