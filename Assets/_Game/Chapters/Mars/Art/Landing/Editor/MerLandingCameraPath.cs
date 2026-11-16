using System.Collections.Generic;
using UnityEngine;
using static Abandoned.SpaceApps.MerLandingChoreography;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// Trajetórias de câmera do pouso (MAR-014) como função do tempo global, no quadro do palco. O builder as grava em keyframes densos
    /// (linear) nas SequenceDefinitions; assim câmera, atores e legendas leem o mesmo tempo e não há snap. FOV é vertical.
    /// </summary>
    public static class MerLandingCameraPath
    {
        public enum Segment
        {
            Entry,
            Parachute,
            Airbags,
            Eagle,
            Checks,
            Ramp,
            Egress,
        }

        public const float DenseStepSeconds = 0.25f;
        // Referência de enquadramento: a largura vista no retângulo de faixa do desenho original (16:9 em meia altura). O split agora é lado a lado
        // (meia largura, altura inteira); os FOVs verticais abaixo preservam esse campo horizontal.
        const float ReferenceAspect = 16f / 9f / 0.5f;
        const float SplitAspect = 16f / 9f * 0.5f;
        public static readonly float SplitFieldOfViewDegrees = FovForAspect(20f, ReferenceAspect, SplitAspect);
        const float FullAspect = 16f / 9f;

        public struct View
        {
            public Vector3 Position;
            public Vector3 Target;
            public float FieldOfViewDegrees;
        }

        public static readonly Vector3 EagleOverlook = new Vector3(12f, 9f, -26f);
        static readonly Vector3 ChecksEnd = new Vector3(-7f, 3.2f, -11f);

        public static (float start, float end) Window(Segment segment)
        {
            switch (segment)
            {
                case Segment.Entry: return (0f, 6f);
                case Segment.Parachute: return (6f, 12f);
                case Segment.Airbags: return (12f, 18f);
                case Segment.Eagle: return (18f, 22f);
                case Segment.Checks: return (22f, 25.2f);
                case Segment.Ramp: return (25.2f, 27f);
                default: return (27f, TotalSeconds);
            }
        }

        /// <summary>FOV vertical que mantém o FOV horizontal de <paramref name="verticalDegrees"/> medido no retângulo do split.</summary>
        public static float FovForAspect(float verticalDegrees, float fromAspect, float toAspect) =>
            2f * Mathf.Atan(Mathf.Tan(verticalDegrees * 0.5f * Mathf.Deg2Rad) * fromAspect / toAspect) * Mathf.Rad2Deg;

        public static float AspectAt(float t) => FullAspect * (0.5f + 0.5f * Smooth((t - SplitSeconds) / EagleSeconds));

        static Vector3 GroundCamera(MerLandingSite site, float x, float z, float clearance) =>
            new Vector3(x, ContactGround(site, x, z) + clearance, z);

        /// <summary>
        /// Câmera do segmento. O hardware é <see cref="MerLandingLayout.HardwareScale"/> vezes maior que no desenho original dos planos; a
        /// distância até o alvo cresce na mesma proporção para o enquadramento ficar igual. A rampa termina exatamente na câmera de jogo.
        /// </summary>
        public static View At(Segment segment, float t, MerLandingSite site, MarsDualLandingData data)
        {
            if (segment == Segment.Egress)
                return GameplayViewAt(t, site, data);

            if (segment == Segment.Ramp)
            {
                var unscaled = AtUnscaled(segment, t, site, data);
                var checks = At(Segment.Checks, Window(Segment.Checks).end, site, data);
                var ideal = GameplayViewAt(Window(Segment.Ramp).end, site, data);
                var eased = Smooth(Mathf.Clamp01((t - Window(segment).start) / (Window(segment).end - Window(segment).start)));
                unscaled.Position = Vector3.Lerp(checks.Position, ideal.Position, eased);
                return unscaled;
            }

            var view = AtUnscaled(segment, t, site, data);
            var k = MerLandingLayout.HardwareScale;
            if (segment == Segment.Entry || segment == Segment.Parachute)
            {
                // Câmeras no ar: a distância ao alvo cresce com o hardware.
                view.Position = view.Target + (view.Position - view.Target) * k;
            }
            else
            {
                // Câmeras no chão: o afastamento horizontal e a altura sobre o terreno crescem; a câmera nunca desce abaixo do chão.
                var offset = view.Position - view.Target;
                var x = view.Target.x + offset.x * k;
                var z = view.Target.z + offset.z * k;
                var ground = ContactGround(site, view.Position.x, view.Position.z);
                var height = (view.Position.y - ground) * k;
                view.Position = new Vector3(x, ContactGround(site, x, z) + height, z);
            }

            return view;
        }

        static View AtUnscaled(Segment segment, float t, MerLandingSite site, MarsDualLandingData data)
        {
            var pose = Sample(t, site);
            var (start, end) = Window(segment);
            var u = Mathf.Clamp01((t - start) / (end - start));
            switch (segment)
            {
                case Segment.Entry:
                {
                    // De lado: a cápsula atravessa o quadro com a pluma atrás, em vez de vir de frente para a lente.
                    var heading = (CapsulePosition(TChuteStart, site) - CapsulePosition(0f, site)).normalized;
                    var side = Vector3.Cross(heading, Vector3.up).normalized;
                    return new View
                    {
                        Target = pose.Capsule.Position - heading * 5f,
                        Position = pose.Capsule.Position + side * Mathf.Lerp(38f, 30f, Smooth(u)) + heading * Mathf.Lerp(4f, -2f, Smooth(u)),
                        FieldOfViewDegrees = Mathf.Lerp(30f, 26f, u),
                    };
                }
                case Segment.Parachute:
                {
                    var w = Smooth((t - 9.2f) / 2.2f);
                    return new View
                    {
                        Target = Vector3.Lerp(pose.Capsule.Position + new Vector3(0f, 15f, 0f), pose.Lander.Position + new Vector3(0f, 6f, 0f), w),
                        Position = pose.Capsule.Position + Vector3.Lerp(new Vector3(28f, -6f, 36f), new Vector3(30f, -8f, 40f), Smooth(u)),
                        FieldOfViewDegrees = Mathf.Lerp(42f, 34f, w),
                    };
                }
                case Segment.Airbags:
                {
                    var xz = Vector2.Lerp(new Vector2(-26f, -58f), new Vector2(6f, -46f), Smooth(u));
                    return new View { Target = pose.Lander.Position, Position = GroundCamera(site, xz.x, xz.y, 2f), FieldOfViewDegrees = SplitFieldOfViewDegrees };
                }
                case Segment.Eagle:
                {
                    var xz = new Vector2(6f, -46f);
                    var startPosition = GroundCamera(site, xz.x, xz.y, 2f);
                    var eased = Smooth(u);
                    return new View
                    {
                        Target = pose.Lander.Position,
                        Position = Vector3.Lerp(startPosition, EagleOverlook, eased),
                        FieldOfViewDegrees = FovForAspect(20f, ReferenceAspect, AspectAt(t)),
                    };
                }
                case Segment.Checks:
                {
                    var to = new Vector3(ChecksEnd.x, ContactGround(site, ChecksEnd.x, ChecksEnd.z) + ChecksEnd.y, ChecksEnd.z);
                    return new View
                    {
                        Target = pose.Lander.Position,
                        Position = Vector3.Lerp(EagleOverlook, to, Smooth(u)),
                        FieldOfViewDegrees = FovForAspect(20f, ReferenceAspect, FullAspect),
                    };
                }
                default:
                {
                    var checksEnd = new Vector3(ChecksEnd.x, ContactGround(site, ChecksEnd.x, ChecksEnd.z) + ChecksEnd.y, ChecksEnd.z);
                    var ideal = GameplayViewAt(Window(Segment.Ramp).end, site, data);
                    var eased = Smooth(u);
                    var pivot = RoverPivot(Sample(t, site), site, data);
                    return new View
                    {
                        Target = Vector3.Lerp(pose.Lander.Position, pivot, eased),
                        Position = Vector3.Lerp(checksEnd, ideal.Position, eased),
                        FieldOfViewDegrees = Mathf.Lerp(FovForAspect(20f, ReferenceAspect, FullAspect), data.GameplayFieldOfViewDegrees, eased),
                    };
                }
            }
        }

        static Vector3 RoverPivot(in MerLandingPose pose, MerLandingSite site, MarsDualLandingData data)
        {
            RoverWorld(pose, out var position, out _, out _, site);
            return position + Vector3.up * data.GameplayPivotHeightMeters;
        }

        /// <summary>Pose da câmera de condução (MarsRoverController.ComputeIdealCameraPose) com o rover no fim de M06, imóvel, olhando +Z.</summary>
        public static View GameplayView(MerLandingSite site, MarsDualLandingData data) => GameplayViewAt(TotalSeconds, site, data);

        /// <summary>Câmera de condução com o rover onde ele está no instante <paramref name="t"/> (acompanha a saída do módulo).</summary>
        public static View GameplayViewAt(float t, MerLandingSite site, MarsDualLandingData data)
        {
            var pose = Sample(t, site);
            var pivot = RoverPivot(pose, site, data);
            var rotation = Quaternion.Euler(data.GameplayPitchDegrees, 0f, 0f);
            return new View
            {
                Position = pivot - rotation * Vector3.forward * data.GameplayFollowDistanceMeters,
                Target = pivot,
                FieldOfViewDegrees = data.GameplayFieldOfViewDegrees,
            };
        }

        /// <summary>Keyframes densos (linear) de um trecho, em tempo local, com rotação voltada ao alvo.</summary>
        public static List<ShotKeyframe> Bake(Segment segment, MerLandingSite site, MarsDualLandingData data)
        {
            var (start, end) = Window(segment);
            var duration = end - start;
            var count = Mathf.Max(2, Mathf.CeilToInt(duration / DenseStepSeconds));
            var keys = new List<ShotKeyframe>();
            for (var i = 0; i <= count; i++)
            {
                var local = duration * i / count;
                var view = At(segment, start + local, site, data);
                var forward = view.Target - view.Position;
                var rotation = forward.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(forward, Vector3.up) : Quaternion.identity;
                keys.Add(new ShotKeyframe(local, view.Position, rotation.eulerAngles, view.FieldOfViewDegrees));
            }

            return keys;
        }
    }
}
