using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M31–M32 (34 s) como função pura do tempo: cinco blocos de 6 s (mecanismos, contato, recorte, imagem escolhida, díptico) e os
    /// 4 s do título. Os mecanismos da Opportunity desaceleram e param antes de o HUD ser retirado; depois nada volta a se mover.
    /// </summary>
    public static class MarsMemorialChoreography
    {
        public const float MemorialSeconds = 30f;
        public const float TitleSeconds = 4f;
        public const float TotalSeconds = MemorialSeconds + TitleSeconds;
        public const float BlockSeconds = 6f;
        public const float MechanismsStopSeconds = 5.5f;
        public const float HudHideSeconds = 6.3f;
        public const float WheelDegreesPerSecond = 120f;
        public const float MastSweepDegrees = 40f;

        public const int BlockMechanisms = 0;
        public const int BlockContact = 1;
        public const int BlockPortrait = 2;
        public const int BlockImage = 3;
        public const int BlockDiptych = 4;
        public const int BlockTitle = 5;

        public struct Mechanisms
        {
            public float WheelDegrees;
            public float WheelSpeed;
            public float MastDegrees;
            public float GimbalDegrees;
        }

        static float Smooth(float x) => MerLandingChoreography.Smooth(x);

        public static int BlockAt(float t)
        {
            t = Mathf.Clamp(t, 0f, TotalSeconds);
            return t >= MemorialSeconds ? BlockTitle : Mathf.Min(BlockDiptych, (int)(t / BlockSeconds));
        }

        public static bool HudHidden(float t) => t >= HudHideSeconds;

        /// <summary>Mecanismos da Opportunity: giram e balançam cada vez mais devagar e ficam parados a partir de 5,5 s.</summary>
        public static Mechanisms MechanismsAt(float t)
        {
            var k = Mathf.Clamp01(t / MechanismsStopSeconds);
            var envelope = 1f - Smooth(k);
            var m = new Mechanisms
            {
                // ∫(1 − smoothstep) = u − (u³ − u⁴/2): o ângulo acumulado converge sem salto.
                WheelDegrees = WheelDegreesPerSecond * MechanismsStopSeconds * (k - (k * k * k - 0.5f * k * k * k * k)),
                WheelSpeed = WheelDegreesPerSecond * envelope,
                MastDegrees = MastSweepDegrees * Mathf.Sin(t * 2.1f) * envelope,
                GimbalDegrees = 12f * Mathf.Sin(t * 1.3f + 0.6f) * envelope,
            };
            return m;
        }

        /// <summary>Intensidade (0–1) da luz de recorte do retrato (bloco 12–18 s), com entrada e saída suaves.</summary>
        public static float RimLight(float t) => Smooth((t - 12f) / 1.2f) * (1f - Smooth((t - 17f) / 1f));

        public static bool ControlsReady(float t) => t >= TotalSeconds - 1e-4f;
    }
}
