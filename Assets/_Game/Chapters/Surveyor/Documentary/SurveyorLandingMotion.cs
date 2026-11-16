using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Editorial flight timing with lunar-gravity arcs, not an engineering reconstruction of thrust telemetry.</summary>
    public static class SurveyorLandingMotion
    {
        public const float LunarGravity = 1.62f, FirstContactSeconds = 18f, JettisonSeconds = 9f;
        public const float FirstHopMeters = 10f, SecondHopMeters = 3f;
        /// <summary>Editorial downward speed at the first footpad touch; the rebound starts from this speed, not from a standstill.</summary>
        public const float ApproachSpeedMetersPerSecond = 1.6f;
        // Leg crush / push-off time of each contact. The first hop is still pushed by the verniers (the sources say the engines kept
        // thrusting through the rebounds), so it leaves the ground gradually; the later impacts are short.
        static readonly float[] StrokeSeconds = { 1.4f, .16f, .10f };
        // Slow rocking after each contact: peak pitch in degrees, time to the peak in seconds and roll side.
        static readonly float[] TiltDegrees = { 2.4f, 1.6f, .7f };
        static readonly float[] TiltPeakSeconds = { .55f, .45f, .35f };
        static readonly float[] RollRatio = { -.5f, .6f, -.4f };
        // Slow attitude wander while airborne (pitch, roll in degrees at mid-flight), zero at both contacts.
        static readonly float[] DriftPitchDegrees = { 1.5f, .9f };
        static readonly float[] DriftRollDegrees = { -1f, .7f };
        public static float FirstFlightSeconds => 2f * Mathf.Sqrt(2f * FirstHopMeters / LunarGravity);
        public static float SecondFlightSeconds => 2f * Mathf.Sqrt(2f * SecondHopMeters / LunarGravity);
        public static float SecondContactSeconds => FirstContactSeconds + FirstFlightSeconds;
        public static float FinalContactSeconds => SecondContactSeconds + SecondFlightSeconds;
        public static float ContactTime(int index) => index == 0 ? FirstContactSeconds : index == 1 ? SecondContactSeconds : FinalContactSeconds;
        public static float Arc(float age, float duration) => age <= 0f || age >= duration ? 0f : .5f * LunarGravity * age * (duration - age);
        /// <summary>Upward speed with which each contact leaves the ground; the last contact comes to rest.</summary>
        public static float LaunchSpeed(int contact) => contact == 0 ? .5f * LunarGravity * FirstFlightSeconds : contact == 1 ? .5f * LunarGravity * SecondFlightSeconds : 0f;
        /// <summary>Downward speed at the instant a contact begins.</summary>
        public static float ImpactSpeed(int contact) => contact == 0 ? ApproachSpeedMetersPerSecond : LaunchSpeed(contact - 1);

        // Travel along the slope. Sources: the engines kept firing through the rebounds and the craft moved down the crater wall;
        // about 20 m between the first and second touchdown and 11 m between the second and third, westward (Honeysuckle Creek
        // account of the JPL information sheet; the JPL report also mentions roughly 1 m/s of sideways speed). Editorial staging,
        // not telemetry: the speed profile between those touchdowns is invented to be smooth.
        public const float DriftApproachMetersPerSecond = 1f, FirstHopTravelMeters = 20f, SecondHopTravelMeters = 11f, StopSeconds = .6f;
        /// <summary>Uphill direction (world +X): the craft arrives from there and slides down the slope toward the site.</summary>
        public static Vector3 UphillDirection => Vector3.right;
        /// <summary>Horizontal speed reached while crossing the first hop.</summary>
        public static float FirstHopSpeed => (FirstHopTravelMeters - DriftApproachMetersPerSecond * StrokeSeconds[0] * .5f) / (FirstFlightSeconds - StrokeSeconds[0] * .5f);
        /// <summary>Horizontal speed reached while crossing the second hop.</summary>
        public static float SecondHopSpeed => (SecondHopTravelMeters - FirstHopSpeed * StrokeSeconds[1] * .5f) / (SecondFlightSeconds - StrokeSeconds[1] * .5f);
        public static float HorizontalSpeed(float seconds)
        {
            float speed = DriftApproachMetersPerSecond;
            for (int i = 0; i < 3; i++)
            {
                float start = ContactTime(i), ramp = i == 2 ? StopSeconds : StrokeSeconds[i], to = i == 0 ? FirstHopSpeed : i == 1 ? SecondHopSpeed : 0f;
                if (seconds <= start) return speed;
                if (seconds < start + ramp) { float x = (seconds - start) / ramp; return speed + (to - speed) * x * x * (3f - 2f * x); }
                speed = to;
            }
            return speed;
        }
        static float Travelled(float seconds)
        {
            if (seconds <= 0f) return 0f;
            float distance = 0f, speed = DriftApproachMetersPerSecond, clock = 0f;
            for (int i = 0; i < 3; i++)
            {
                float start = ContactTime(i), ramp = i == 2 ? StopSeconds : StrokeSeconds[i], to = i == 0 ? FirstHopSpeed : i == 1 ? SecondHopSpeed : 0f;
                if (seconds <= start) return distance + speed * (seconds - clock);
                distance += speed * (start - clock); clock = start;
                if (seconds < start + ramp)
                {   // Integral of a smoothstep speed ramp: x^3 - x^4 / 2.
                    float x = (seconds - start) / ramp;
                    return distance + speed * (seconds - start) + (to - speed) * ramp * (x * x * x - .5f * x * x * x * x);
                }
                distance += ramp * (speed + to) * .5f; clock = start + ramp; speed = to;
            }
            return distance + speed * (seconds - clock);
        }
        /// <summary>Metres still to travel along the slope before the craft is at its final resting place (zero from then on).</summary>
        public static float TravelRemaining(float seconds) => Travelled(FinalContactSeconds + StopSeconds + 1f) - Travelled(seconds);
        public static float Height(float seconds)
        {
            if (seconds < FirstContactSeconds)
            {
                float u = Mathf.Clamp01(seconds / FirstContactSeconds), u2 = u * u, u3 = u2 * u;
                // Nonzero downward speed at contact; the old smoothstep stopped in midair before rebounding.
                return (2f * u3 - 3f * u2 + 1f) * 40f + (u3 - 2f * u2 + u) * FirstContactSeconds * -3f + (u3 - u2) * FirstContactSeconds * -ApproachSpeedMetersPerSecond;
            }
            if (seconds < SecondContactSeconds) { float age = seconds - FirstContactSeconds; return Arc(age, FirstFlightSeconds) + Stroke(0, age); }
            if (seconds < FinalContactSeconds) { float age = seconds - SecondContactSeconds; return Arc(age, SecondFlightSeconds) + Stroke(1, age); }
            return Stroke(2, seconds - FinalContactSeconds);
        }
        /// <summary>
        /// Leg compression followed by the push-off. Cubic correction added to the ballistic arc: it keeps the speed
        /// continuous through the touch (no instant reversal) and vanishes with zero slope when the stroke ends,
        /// so the apex heights and the lunar-gravity arcs stay exactly as sourced.
        /// </summary>
        static float Stroke(int contact, float age)
        {
            float duration = StrokeSeconds[contact];
            if (age <= 0f || age >= duration) return 0f;
            float remaining = 1f - age / duration, slope = -(ImpactSpeed(contact) + LaunchSpeed(contact));
            return slope * age * remaining * remaining;
        }
        public static Quaternion ContactTilt(float seconds)
        {
            float pitch = 0f, roll = 0f;
            for (int i = 0; i < 3; i++)
            {
                float age = seconds - ContactTime(i); if (age <= 0f) continue;
                float peak = TiltPeakSeconds[i], x = age / peak;
                // Fast rise to the peak and slow recovery without overshoot; the old sine rang like a spring.
                float response = x * Mathf.Exp(1f - x) * (1f - Mathf.SmoothStep(0f, 1f, (x - 4f) / 2f));
                pitch += TiltDegrees[i] * response; roll += TiltDegrees[i] * RollRatio[i] * response;
            }
            for (int i = 0; i < 2; i++)
            {
                float start = ContactTime(i), end = ContactTime(i + 1);
                if (seconds <= start || seconds >= end) continue;
                float wander = Mathf.Sin(Mathf.PI * (seconds - start) / (end - start)); wander *= wander;
                pitch += DriftPitchDegrees[i] * wander; roll += DriftRollDegrees[i] * wander;
            }
            return Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
