using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M01–M03 (40,8 s) como função pura do tempo global: Marte visto do espaço (M01, 6,3 s), laboratório com os mapas orbitais de Meridiani
    /// e de Gusev (M02, 28,5 s) e o globo que mostra onde cada rover pousou, com as datas (M03, 6 s). A abertura segue a do jogo anterior
    /// (2003 · Marte no espaço, laboratório, hematita em Meridiani Planum) e repete o tratamento para a Spirit e Gusev.
    /// Latitude/longitude em graus (leste positivo); locais de pouso conforme NASA GISS Mars24.
    /// </summary>
    public static class MarsOriginChoreography
    {
        public const float SpaceSeconds = 6.3f;
        public const float LabSeconds = 28.5f;
        public const float GlobeSeconds = 6f;
        public const float TotalSeconds = SpaceSeconds + LabSeconds + GlobeSeconds;
        public const float LabStart = SpaceSeconds;
        public const float GlobeStart = SpaceSeconds + LabSeconds;

        public const float PlanetRadiusMeters = 3f;
        public const float GlobeRadiusMeters = 2f;
        public const float PlanetSpinDegreesPerSecond = 5f;
        public static readonly Vector2 Gusev = new Vector2(-14.57f, 175.48f);
        public static readonly Vector2 Meridiani = new Vector2(-1.95f, 354.47f);

        // Painéis orbitais (como na abertura anterior: entram em 1,4 s, saem em 1,4 s). Meridiani entre 8 s e 20 s; Gusev entre 28 s e 34 s.
        public const float MeridianiMapStart = 8f;
        public const float MeridianiMapEnd = 20f;
        public const float GusevMapStart = 28f;
        public const float GusevMapEnd = 34f;
        public const float MapFadeSeconds = 1.4f;

        // Globo: de frente para Gusev, gira até Meridiani; cada local acende e fica aceso.
        public const float GusevLitAt = GlobeStart + 0.4f;
        public const float GlobeTurnStart = GlobeStart + 2f;
        public const float GlobeTurnSeconds = 1.8f;
        public const float MeridianiLitAt = GlobeTurnStart + GlobeTurnSeconds - 0.2f;

        public const int RegionSpace = 0;
        public const int RegionLab = 1;
        public const int RegionGlobe = 2;

        public struct Pose
        {
            public float PlanetSpinDegrees;
            public Quaternion GlobeRotation;
            public float GusevGlow;
            public float MeridianiGlow;
            public float MeridianiMapOpacity;
            public float GusevMapOpacity;
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

        public static int RegionAt(float t) => t < LabStart ? RegionSpace : t < GlobeStart ? RegionLab : RegionGlobe;

        static float MapOpacity(float t, float start, float end) => Smooth((t - start) / MapFadeSeconds) * Smooth((end - t) / MapFadeSeconds);

        public static Pose Sample(float t)
        {
            t = Mathf.Clamp(t, 0f, TotalSeconds);
            return new Pose
            {
                PlanetSpinDegrees = PlanetSpinDegreesPerSecond * Mathf.Min(t, SpaceSeconds),
                GlobeRotation = Quaternion.Slerp(FacingCamera(Gusev), FacingCamera(Meridiani), Smooth((t - GlobeTurnStart) / GlobeTurnSeconds)),
                GusevGlow = Smooth((t - GusevLitAt) / 0.4f),
                MeridianiGlow = Smooth((t - MeridianiLitAt) / 0.4f),
                MeridianiMapOpacity = MapOpacity(t, MeridianiMapStart, MeridianiMapEnd),
                GusevMapOpacity = MapOpacity(t, GusevMapStart, GusevMapEnd),
            };
        }
    }
}
