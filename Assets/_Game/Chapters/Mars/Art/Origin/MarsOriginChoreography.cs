using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M01–M03 (34,8 s) como função pura do tempo global: Marte visto do espaço (M01, 6,3 s), laboratório com os dois rovers e órbita em volta da
    /// Opportunity (M02, até 19,4 s), globo em Meridiani Planum (fim de M02) e o giro até a Cratera Gusev (M03, 7,7 s).
    /// Latitude/longitude em graus (leste positivo); locais de pouso conforme NASA GISS Mars24.
    /// </summary>
    public static class MarsOriginChoreography
    {
        public const float SpaceSeconds = 6.3f;
        public const float LabSeconds = 20.8f;
        public const float GlobeSeconds = 7.7f;
        public const float TotalSeconds = SpaceSeconds + LabSeconds + GlobeSeconds;
        public const float LabStart = SpaceSeconds;
        public const float GlobeStart = SpaceSeconds + LabSeconds;

        public const float PlanetRadiusMeters = 3f;
        public const float GlobeRadiusMeters = 2f;
        public const float PlanetSpinDegreesPerSecond = 5f;
        public static readonly Vector2 Gusev = new Vector2(-14.57f, 175.48f);
        public static readonly Vector2 Meridiani = new Vector2(-1.95f, 354.47f);

        // Globo: entra no fim de M02 (fala de Meridiani) de frente para Meridiani Planum, marcada, girando devagar; quando começa a fala de Gusev
        // (início de M03), gira até a Cratera Gusev, que é marcada ao chegar. Os dois ficam marcados.
        public const float MeridianiShotStart = LabStart + 13.1f;
        public const float MeridianiDriftDegrees = 8f;
        public const float IntroTurnStart = GlobeStart;
        public const float IntroTurnSeconds = 4f;
        public const float MeridianiMarkedAt = MeridianiShotStart + 0.3f;
        public const float GusevMarkedAt = IntroTurnStart + IntroTurnSeconds - 0.4f;
        public const float PulsePeriodSeconds = 1.6f;

        public const int RegionSpace = 0;
        public const int RegionLab = 1;
        public const int RegionGlobe = 2;

        public struct Pose
        {
            public float PlanetSpinDegrees;
            public Quaternion GlobeRotation;
            public float GusevGlow;
            public float MeridianiGlow;
            /// <summary>Fase 0–1 do anel que se expande a partir de cada marcador.</summary>
            public float Pulse;
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

        public static int RegionAt(float t) => t < LabStart ? RegionSpace : t < MeridianiShotStart ? RegionLab : RegionGlobe;


        public static Pose Sample(float t)
        {
            t = Mathf.Clamp(t, 0f, TotalSeconds);
            return new Pose
            {
                PlanetSpinDegrees = PlanetSpinDegreesPerSecond * Mathf.Min(t, SpaceSeconds),
                // Deriva lenta sobre Meridiani (termina em zero quando o giro até Gusev começa), depois o giro.
                GlobeRotation = Quaternion.Slerp(
                    Quaternion.AngleAxis(Mathf.Lerp(MeridianiDriftDegrees, 0f, Mathf.Clamp01((t - MeridianiShotStart) / (IntroTurnStart - MeridianiShotStart))), Vector3.up) * FacingCamera(Meridiani),
                    FacingCamera(Gusev), Smooth((t - IntroTurnStart) / IntroTurnSeconds)),
                Pulse = Mathf.Repeat(t / PulsePeriodSeconds, 1f),
                // Os dois locais ficam acesos depois de marcados, até o fim.
                MeridianiGlow = Smooth((t - MeridianiMarkedAt) / 0.4f),
                GusevGlow = Smooth((t - GusevMarkedAt) / 0.4f),
            };
        }
    }
}
