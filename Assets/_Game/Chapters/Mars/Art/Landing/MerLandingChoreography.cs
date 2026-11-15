using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Coreografia do pouso M04–M06 como função pura do tempo (segundos desde o início de M04; mesmo relógio do SequenceDirector
    /// mais o deslocamento de cada sequência). Escudo, paraquedas, descarte, tether, inflação, dois impactos, parada, desinflar,
    /// pétalas e desdobramento saem da mesma função para Spirit e Opportunity ("mesmos movimentos"). Câmera, ator, legenda e
    /// término leem o mesmo tempo, então pausa, salto de pré-visualização e reinício são consistentes.
    /// Quadro do palco: origem no ponto de parada; y = 0 é o nível da planície. A escala de tempo é cinematográfica (27 s).
    /// </summary>
    public static class MerLandingChoreography
    {
        // Duração narrativa (MAR-014; roteiro 01_MARS_TWINS M04–M06).
        public const float SplitSeconds = 18f;
        public const float EagleSeconds = 4f;
        public const float VerificationSeconds = 9.5f;
        public const float TotalSeconds = SplitSeconds + EagleSeconds + VerificationSeconds;

        // Marcos (segundos desde o início de M04).
        public const float TFireballOn = 0.3f;
        public const float TFireballOff = 5.6f;
        public const float TChuteStart = 6f;
        public const float TChuteFull = 7.2f;
        public const float TShieldSeparation = 8.6f;
        public const float TTetherStart = 10f;
        public const float TTetherFull = 11.6f;
        public const float TInflateStart = 12.2f;
        public const float TInflateEnd = 13f;
        public const float TRetroStart = 13.1f;
        public const float TCut = 13.9f;
        public const float TRetroEnd = 14.4f;
        public const float TImpact1 = 14.8f;
        public const float TImpact2 = 16.5f;
        public const float TStop = 17.7f;
        public const float TBackshellLanding = 21f;
        public const float TDeflateStart = 22f;
        public const float TDeflateEnd = 25f;
        public const float TPetalsStart = 24.6f;
        public const float TPetalsEnd = 26f;
        public const float TUnfoldStart = 25.4f;
        public const float TUnfoldEnd = 27f;
        public const float TEgressStart = TUnfoldEnd;
        public const float TEgressDriveEnd = TotalSeconds - 0.8f;
        /// <summary>Quanto o rover anda à frente do centro do módulo: desce a pétala (≈ 9 m) e para na planície, fora do módulo.</summary>
        public const float EgressDistanceMeters = 11.5f;

        public const float TetherHangMeters = 20f;
        public const float FirstBounceApexMeters = 11f;
        public const float FallHeightMeters = 14f;
        public const float EagleDepthMeters = 2.9f;
        /// <summary>Mapa importado: bacia rasa de 0,9 m com a planície em volta perto de 0,95 m.</summary>
        public const float ClassicDepthMeters = 0.9f;
        public const float ClassicPlainMeters = 0.95f;
        public const float EagleFloorRadiusMeters = 3f;
        public const float EagleRimRadiusMeters = 11f;
        public const float GravityMetersPerSecondSquared = 3.71f;
        const float TetherTopHeight = 2f;
        const float ShieldRestHeight = 0.55f * MerLandingLayout.HardwareScale;

        // Elipse de pouso explícita (stage-local, x/z): os dois impactos e a parada ficam dentro dela.
        public static readonly Vector2 EllipseCenter = new Vector2(-8f, -6f);
        public static readonly Vector2 EllipseSemiAxes = new Vector2(42f, 24f);
        public static readonly Vector2 CutPoint = new Vector2(-40f, -18f);
        public static readonly Vector2 Impact1Point = new Vector2(-34f, -15f);
        public static readonly Vector2 Impact2Point = new Vector2(-9f, -5f);
        public static readonly Vector2 StopPoint = Vector2.zero;

        static readonly float TumbleRadius = MerLandingLayout.TumbleContactRadius();
        static readonly float CutHeight = TetherHangMeters + TumbleRadius + FallHeightMeters;

        static readonly AnimationCurve CapsuleHeight = Curve(
            (0f, 420f), (6f, 300f), (8.6f, 210f), (10f, 150f), (11f, 112f), (12f, 78f), (13.1f, 50f), (TCut, CutHeight),
            (14.6f, CutHeight + 7.5f), (15.8f, CutHeight + 21.5f), (17.5f, CutHeight + 13.5f), (19.5f, 26f), (TBackshellLanding, 2.2f), (60f, 2.2f));
        static readonly AnimationCurve CapsuleX = Curve(
            (0f, -200f), (6f, -150f), (8.6f, -125f), (10f, -108f), (11f, -95f), (12f, -80f), (13.1f, -58f), (TCut, CutPoint.x),
            (15f, -28f), (18f, -8f), (TBackshellLanding, 22f), (60f, 22f));
        static readonly AnimationCurve CapsuleZ = Curve(
            (0f, -250f), (6f, -190f), (8.6f, -158f), (10f, -136f), (11f, -116f), (12f, -94f), (13.1f, -55f), (TCut, CutPoint.y),
            (15f, 0f), (18f, 28f), (TBackshellLanding, 56f), (60f, 56f));
        static readonly AnimationCurve SpinX = Curve((TCut, 0f), (TImpact1, 130f), (TImpact2, 560f), (TStop, 1080f), (60f, 1080f));
        static readonly AnimationCurve SpinZ = Curve((TCut, 0f), (TImpact1, 60f), (TImpact2, 220f), (TStop, 360f), (60f, 360f));

        static readonly Dictionary<MerLandingSite, ShieldLanding> ShieldCache = new Dictionary<MerLandingSite, ShieldLanding>();

        struct ShieldLanding
        {
            public float FlightSeconds;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        // ---------- Terreno ----------

        /// <summary>Altura do chão do palco (y = 0 é a planície). Meridiani tem a bacia de Eagle com o piso em y = −2,9 m no ponto de parada; MeridianiClassic, a bacia rasa do mapa importado (piso ≈ 0,05 m, planície 0,95 m).</summary>
        public static float Ground(MerLandingSite site, float x, float z)
        {
            var ripple = 0.03f * Mathf.Sin(0.7f * x + 0.3f) * Mathf.Cos(0.5f * z + 1.1f);
            if (site == MerLandingSite.Gusev)
                return ripple;

            var r = Mathf.Sqrt(x * x + z * z);
            var rim = Smooth((r - EagleFloorRadiusMeters) / (EagleRimRadiusMeters - EagleFloorRadiusMeters));
            return ripple + (site == MerLandingSite.MeridianiClassic ? ClassicPlainMeters - ClassicDepthMeters * (1f - rim) : -EagleDepthMeters * (1f - rim));
        }

        /// <summary>Altura de contato de uma esfera de raio ~1,6 m: o maior chão em cinco amostras (não deixa o módulo atravessar a borda).</summary>
        public static float ContactGround(MerLandingSite site, float x, float z)
        {
            var r = MerLandingLayout.CoreRadiusMeters;
            return Mathf.Max(
                Ground(site, x, z),
                Mathf.Max(Ground(site, x + r, z), Mathf.Max(Ground(site, x - r, z), Mathf.Max(Ground(site, x, z + r), Ground(site, x, z - r)))));
        }

        public static bool InsideEllipse(Vector2 point)
        {
            var d = point - EllipseCenter;
            return (d.x * d.x) / (EllipseSemiAxes.x * EllipseSemiAxes.x) + (d.y * d.y) / (EllipseSemiAxes.y * EllipseSemiAxes.y) <= 1f;
        }

        // ---------- Amostragem ----------

        public static MerLandingPose Sample(float t, MerLandingSite site)
        {
            t = Mathf.Clamp(t, 0f, TotalSeconds);
            var pose = new MerLandingPose();

            var capsuleRotation = CapsuleRotation(t, site);
            var capsulePosition = CapsulePosition(t, site);
            pose.Capsule = new MerLandingActor { Position = capsulePosition, Rotation = capsuleRotation, Scale = Vector3.one, Visible = true };
            pose.Fireball = Fireball(t);
            pose.Retro = Retro(t);
            pose.ChuteInflation = Smooth((t - TChuteStart) / (TChuteFull - TChuteStart));
            pose.AirbagInflation = Smooth((t - TInflateStart) / (TInflateEnd - TInflateStart));
            pose.AirbagDeflation = Smooth((t - TDeflateStart) / (TDeflateEnd - TDeflateStart));
            pose.PetalOpen = Smooth((t - TPetalsStart) / (TPetalsEnd - TPetalsStart));
            pose.RoverUnfold = Smooth((t - TUnfoldStart) / (TUnfoldEnd - TUnfoldStart));
            pose.RoverDrive = Smooth((t - TEgressStart) / (TEgressDriveEnd - TEgressStart));

            pose.HeatShield = HeatShield(t, site, capsulePosition, capsuleRotation);
            pose.Lander = Lander(t, site, capsulePosition, capsuleRotation, pose.AirbagDeflation);

            // Paraquedas: sai do topo do casco, enche e fica acima; ao pousar o casco, desaba com o vento.
            var chute = pose.ChuteInflation;
            var anchor = Mathf.Lerp(4f, MerLandingLayout.ParachuteAnchorHeightMeters, chute);
            var collapse = Smooth((t - TBackshellLanding) / 2.5f);
            var aloft = capsulePosition + capsuleRotation * new Vector3(0f, anchor, 0f);
            var down = new Vector3(capsulePosition.x + 6f, ContactGround(site, capsulePosition.x + 6f, capsulePosition.z + 3f) + 0.9f, capsulePosition.z + 3f);
            var chuteScale = Mathf.Lerp(0.05f, 1f, chute);
            pose.Parachute = new MerLandingActor
            {
                Position = Vector3.Lerp(aloft, down, collapse),
                Rotation = Quaternion.identity,
                Scale = new Vector3(chuteScale * Mathf.Lerp(1f, 1.2f, collapse), chuteScale * Mathf.Lerp(1f, 0.12f, collapse), chuteScale * Mathf.Lerp(1f, 1.2f, collapse)),
                Visible = t >= TChuteStart,
            };

            // Tether: do casco ao ápice do módulo; depois do corte, a ponta cai pendurada no casco (não some).
            pose.TetherVisible = t >= TTetherStart;
            pose.TetherTop = capsulePosition + capsuleRotation * new Vector3(0f, TetherTopHeight, 0f);
            var apex = pose.Lander.Position + pose.Lander.Rotation * MerLandingLayout.Apex;
            var hangLength = TetherHangMeters - MerLandingLayout.Apex.y + TetherTopHeight;
            var hangEnd = pose.TetherTop - capsuleRotation * new Vector3(0f, hangLength, 0f);
            pose.TetherBottom = t < TCut ? apex : Vector3.Lerp(apex, hangEnd, Smooth((t - TCut) / 0.3f));
            return pose;
        }

        public static Vector3 CapsulePosition(float t, MerLandingSite site)
        {
            var x = CapsuleX.Evaluate(t);
            var z = CapsuleZ.Evaluate(t);
            return new Vector3(x, ContactGround(site, x, z) + CapsuleHeight.Evaluate(t), z);
        }

        public static Quaternion CapsuleRotation(float t, MerLandingSite site)
        {
            var from = CapsulePosition(0f, site);
            var to = CapsulePosition(TChuteStart, site);
            var entry = Quaternion.FromToRotation(Vector3.down, (to - from).normalized) * Quaternion.Euler(0f, 15f * t, 0f);
            return Quaternion.Slerp(entry, Quaternion.identity, Smooth((t - 5.4f) / 2.4f));
        }

        static float Fireball(float t) =>
            Smooth((t - TFireballOn) / 1.1f) * (1f - Smooth((t - 4.4f) / (TFireballOff - 4.4f)));

        static float Retro(float t) =>
            Smooth((t - TRetroStart) / 0.15f) * (1f - Smooth((t - 14f) / (TRetroEnd - 14f)));

        static MerLandingActor HeatShield(float t, MerLandingSite site, Vector3 capsulePosition, Quaternion capsuleRotation)
        {
            if (t < TShieldSeparation)
                return new MerLandingActor { Position = capsulePosition, Rotation = capsuleRotation, Scale = Vector3.one, Visible = true };

            var landing = ShieldLandingFor(site);
            var tau = t - TShieldSeparation;
            if (tau >= landing.FlightSeconds)
                return new MerLandingActor { Position = landing.Position, Rotation = landing.Rotation, Scale = Vector3.one, Visible = true };

            return new MerLandingActor { Position = ShieldFlightPosition(site, tau), Rotation = ShieldFlightRotation(tau, landing), Scale = Vector3.one, Visible = true };
        }

        static Vector3 ShieldFlightPosition(MerLandingSite site, float tau)
        {
            var start = CapsulePosition(TShieldSeparation, site);
            var velocity = (CapsulePosition(TShieldSeparation + 0.01f, site) - start) / 0.01f + new Vector3(10f, -3f, -6f) * MerLandingLayout.HardwareScale;
            return start + velocity * tau + 0.5f * GravityMetersPerSecondSquared * tau * tau * Vector3.down;
        }

        static Quaternion ShieldFlightRotation(float tau, ShieldLanding landing)
        {
            var tumble = Quaternion.Euler(55f * tau, 20f * tau, 28f * tau);
            return Quaternion.Slerp(tumble, landing.Rotation, Smooth((tau - (landing.FlightSeconds - 0.6f)) / 0.6f));
        }

        static ShieldLanding ShieldLandingFor(MerLandingSite site)
        {
            if (ShieldCache.TryGetValue(site, out var cached))
                return cached;

            var landing = new ShieldLanding { FlightSeconds = 40f };
            for (var tau = 0f; tau < 40f; tau += 0.02f)
            {
                var p = ShieldFlightPosition(site, tau);
                if (p.y <= ContactGround(site, p.x, p.z) + ShieldRestHeight)
                {
                    landing.FlightSeconds = tau;
                    var yaw = 20f * tau;
                    landing.Rotation = Quaternion.Euler(8f, yaw, 10f);
                    landing.Position = new Vector3(p.x, ContactGround(site, p.x, p.z) + ShieldRestHeight, p.z);
                    break;
                }
            }

            ShieldCache[site] = landing;
            return landing;
        }

        /// <summary>Tempo de voo e ponto de repouso do escudo (para verificar que ele cai, pousa e permanece).</summary>
        public static float ShieldFlightSeconds(MerLandingSite site) => ShieldLandingFor(site).FlightSeconds;

        static float TumbleContactRadiusAt(float t, float deflation)
        {
            var settle = Smooth((t - TImpact2) / (TStop - TImpact2));
            var inflated = Mathf.Lerp(TumbleRadius, MerLandingLayout.RestCenterHeightInflated, settle);
            return Mathf.Lerp(inflated, MerLandingLayout.RestCenterHeightDeflated, deflation);
        }

        static MerLandingActor Lander(float t, MerLandingSite site, Vector3 capsulePosition, Quaternion capsuleRotation, float deflation)
        {
            if (t < TTetherStart)
            {
                return new MerLandingActor
                {
                    Position = capsulePosition + capsuleRotation * new Vector3(0f, MerLandingLayout.LanderCentroidInCapsule, 0f),
                    Rotation = capsuleRotation,
                    Scale = Vector3.one,
                    Visible = true,
                };
            }

            if (t < TCut)
            {
                var drop = MerLandingLayout.LanderCentroidInCapsule + TetherHangMeters;
                var s = Smooth((t - TTetherStart) / (TTetherFull - TTetherStart));
                var swing = 4f * Mathf.Sin(2.4f * (t - TTetherStart)) * s * (1f - Smooth((t - 12.9f) / 1f));
                var lateral = -Mathf.Sin(swing * Mathf.Deg2Rad) * drop * s;
                return new MerLandingActor
                {
                    Position = capsulePosition + capsuleRotation * new Vector3(lateral, MerLandingLayout.LanderCentroidInCapsule - drop * s, 0f),
                    Rotation = capsuleRotation * Quaternion.Euler(0f, 0f, swing),
                    Scale = Vector3.one,
                    Visible = true,
                };
            }

            Vector2 plan;
            float lift;
            if (t < TImpact1)
            {
                var u = (t - TCut) / (TImpact1 - TCut);
                plan = Vector2.Lerp(CutPoint, Impact1Point, u);
                lift = FallHeightMeters * (1f - u * u);
            }
            else if (t < TImpact2)
            {
                var u = (t - TImpact1) / (TImpact2 - TImpact1);
                plan = Vector2.Lerp(Impact1Point, Impact2Point, u);
                lift = FirstBounceApexMeters * 4f * u * (1f - u);
            }
            else if (t < TStop)
            {
                var u = (t - TImpact2) / (TStop - TImpact2);
                plan = Vector2.Lerp(Impact2Point, StopPoint, 1f - (1f - u) * (1f - u));
                lift = 0f;
            }
            else
            {
                plan = StopPoint;
                lift = 0f;
            }

            var y = ContactGround(site, plan.x, plan.y) + TumbleContactRadiusAt(t, deflation) + lift;
            return new MerLandingActor
            {
                Position = new Vector3(plan.x, y, plan.y),
                Rotation = Quaternion.Euler(SpinX.Evaluate(t), 0f, SpinZ.Evaluate(t)),
                Scale = Vector3.one,
                Visible = true,
            };
        }

        /// <summary>
        /// Pose do rover (raiz do prefab) no quadro do palco, dada a pose do módulo. Na saída (<see cref="MerLandingPose.RoverDrive"/>) ele anda para a
        /// frente (+Z do módulo, a pétala-rampa) e segue o terreno: nunca fica abaixo do convés nem do chão.
        /// </summary>
        public static void RoverWorld(in MerLandingPose pose, out Vector3 position, out Quaternion rotation, out float scale, MerLandingSite site = MerLandingSite.Meridiani)
        {
            var deck = pose.Lander.Position + pose.Lander.Rotation * MerLandingLayout.RoverLocalPosition;
            position = deck;
            rotation = pose.Lander.Rotation;
            scale = Mathf.Lerp(MerLandingLayout.StowedRoverScale, 1f, pose.RoverUnfold);
            if (pose.RoverDrive <= 0f)
                return;

            position += pose.Lander.Rotation * Vector3.forward * (pose.RoverDrive * EgressDistanceMeters);
            position.y = Mathf.Max(deck.y, Ground(site, position.x, position.z) + 0.02f);
        }

        // ---------- Auxiliares ----------

        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Curva cúbica monótona por trechos (tangentes de Fritsch–Carlson): sem excesso entre os pontos.</summary>
        static AnimationCurve Curve(params (float time, float value)[] points)
        {
            var keys = new Keyframe[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                float tangent;
                if (i == 0)
                    tangent = Slope(points, 0);
                else if (i == points.Length - 1)
                    tangent = Slope(points, i - 1);
                else
                {
                    var before = Slope(points, i - 1);
                    var after = Slope(points, i);
                    tangent = before * after <= 0f ? 0f : 2f * before * after / (before + after);
                }

                keys[i] = new Keyframe(points[i].time, points[i].value, tangent, tangent);
            }

            return new AnimationCurve(keys);
        }

        static float Slope((float time, float value)[] p, int i) => (p[i + 1].value - p[i].value) / (p[i + 1].time - p[i].time);
    }
}
