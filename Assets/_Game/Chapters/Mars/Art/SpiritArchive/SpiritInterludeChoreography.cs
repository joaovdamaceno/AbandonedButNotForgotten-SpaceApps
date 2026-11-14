using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M22 (45 s) como função pura do tempo: oito batidas, revelação da sílica pela roda arrastada, odômetro de 7,73 km, escurecimento
    /// do Spirit e metade inferior (Opportunity) preenchendo a tela. O Spirit nunca volta a dirigir: a pose do rover é constante.
    /// </summary>
    public static class SpiritInterludeChoreography
    {
        public const float TotalSeconds = 45f;
        public const float OdometerKilometers = 7.73f;
        public static readonly float[] BeatStarts = { 0f, 5f, 11f, 17f, 22f, 27f, 32f, 39f, 45f };
        public const int BeatHusbandHill = 0;
        public const int BeatComanche = 1;
        public const int BeatSilica = 2;
        public const int BeatHomePlate = 3;
        public const int BeatOdometer = 4;
        public const int BeatTroy = 5;
        public const int BeatWinter = 6;
        public const int BeatLastContact = 7;

        public const float GrowthStart = 42.5f;
        public const float GrowthSeconds = 2.5f;
        public const float RoverTiltDegrees = 8f;

        static float Smooth(float x) => MerLandingChoreography.Smooth(x);

        public static int BeatAt(float t)
        {
            t = Mathf.Clamp(t, 0f, TotalSeconds);
            for (var i = 0; i < 8; i++)
            {
                if (t < BeatStarts[i + 1])
                    return i;
            }

            return 7;
        }

        /// <summary>0–1: quanto da sílica já foi exposto pela roda arrastada (cobertura que desliza, nunca gira).</summary>
        public static float SilicaReveal(float t) => Smooth((t - (BeatStarts[BeatSilica] + 1f)) / 3.5f);

        public static float OdometerAt(float t) => OdometerKilometers * Smooth((t - BeatStarts[BeatOdometer]) / 4f);

        /// <summary>0–1: o Spirit escurece durante a última batida.</summary>
        public static float SpiritDarkness(float t) => Smooth((t - (BeatStarts[BeatLastContact] + 0.5f)) / 5f);

        /// <summary>0–1: a metade inferior (Opportunity) cresce até a tela inteira.</summary>
        public static float Growth(float t) => Smooth((t - GrowthStart) / GrowthSeconds);

        public static float OpportunityHeight(float t) => Mathf.Lerp(0.5f, 1f, Growth(t));

        /// <summary>Razão de aspecto da área da Opportunity em 16:9.</summary>
        public static float OpportunityAspect(float t) => 16f / 9f / OpportunityHeight(t);
    }
}
