using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Coreografia de M01–M03 (25 s) como função pura do tempo global: globo (rotação e destaques por legenda) e dois lançamentos em
    /// sequência com uma única chama reutilizada. Latitude/longitude em graus (leste positivo); coordenadas de pouso e de origem
    /// aproximadas (NASA: Spirit em Gusev, Opportunity em Meridiani Planum).
    /// </summary>
    public static class MarsOriginChoreography
    {
        public const float LabSeconds = 8f;
        public const float MapSeconds = 12f;
        public const float LaunchSeconds = 5f;
        public const float TotalSeconds = LabSeconds + MapSeconds + LaunchSeconds;
        public const float MapStart = LabSeconds;
        public const float LaunchStart = LabSeconds + MapSeconds;

        public const float GlobeRadiusMeters = 2f;
        public const float HematiteRadiusDegrees = 7f;
        public static readonly Vector2 Gusev = new Vector2(-14.57f, 175.47f);
        public static readonly Vector2 Meridiani = new Vector2(-1.95f, 354.47f);

        public const float RocketSpacingMeters = 50f;
        public const float RocketATakeoff = 0.3f;
        public const float RocketBTakeoff = 2.8f;
        public const float RocketClimbSeconds = 2.2f;
        public const float RocketClimbMeters = 120f;

        public struct Pose
        {
            public Quaternion GlobeRotation;
            public float GusevGlow;
            public float MeridianiGlow;
            public float HematiteGlow;
            public float RocketAHeight;
            public float RocketBHeight;
            public float Flame;
            public bool FlameOnA;
        }

        /// <summary>Direção unitária (quadro do globo) de uma latitude/longitude.</summary>
        public static Vector3 Direction(Vector2 latLon)
        {
            var lat = latLon.x * Mathf.Deg2Rad;
            var lon = latLon.y * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
        }

        /// <summary>Rotação que leva o ponto à frente da câmera (a câmera fica em −Z e olha para +Z).</summary>
        public static Quaternion FacingCamera(Vector2 latLon) => Quaternion.FromToRotation(Direction(latLon), Vector3.back);

        static float Smooth(float x) => MerLandingChoreography.Smooth(x);

        public static Pose Sample(float t)
        {
            t = Mathf.Clamp(t, 0f, TotalSeconds);
            var s = t - MapStart;
            var pose = new Pose();
            var gusev = FacingCamera(Gusev);
            var meridiani = FacingCamera(Meridiani);
            pose.GlobeRotation = Quaternion.Slerp(gusev, meridiani, Smooth((s - 2.6f) / 1.6f));

            var inMap = t >= MapStart && t < LaunchStart;
            pose.GusevGlow = inMap ? Smooth(s / 0.4f) * (1f - Smooth((s - 3.8f) / 0.4f)) : 0f;
            pose.MeridianiGlow = inMap ? Smooth((s - 4f) / 0.4f) : 0f;
            pose.HematiteGlow = inMap ? Smooth((s - 8f) / 0.8f) : 0f;

            var u = t - LaunchStart;
            pose.RocketAHeight = Climb(u - RocketATakeoff);
            pose.RocketBHeight = Climb(u - RocketBTakeoff);
            var aOn = u >= RocketATakeoff && u < RocketBTakeoff - 0.2f;
            var bOn = u >= RocketBTakeoff;
            pose.FlameOnA = !bOn;
            pose.Flame = (aOn || bOn) && t >= LaunchStart ? Smooth((u - (bOn ? RocketBTakeoff : RocketATakeoff)) / 0.15f) * (aOn ? 1f - Smooth((u - (RocketBTakeoff - 0.8f)) / 0.3f) : 1f) : 0f;
            return pose;
        }

        static float Climb(float seconds)
        {
            if (seconds <= 0f)
                return 0f;

            var k = Mathf.Clamp01(seconds / RocketClimbSeconds);
            return RocketClimbMeters * k * k + 0.05f * Mathf.Max(0f, seconds - RocketClimbSeconds) * RocketClimbMeters;
        }
    }
}
