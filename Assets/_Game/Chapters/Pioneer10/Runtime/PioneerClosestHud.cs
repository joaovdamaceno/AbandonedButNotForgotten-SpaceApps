using System;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 5 · O mais perto possível (só cinema). Lê o relógio da sequência <c>PIO_Closest</c> (65 s) e desenha no
    /// <see cref="CinemaHudView"/>: o medidor de radiação que sobe com a estática do quadro (12–27 s, plano da radiação), e, no ponto
    /// mais próximo (35–42 s), a velocidade e a comparação de distâncias: uma barra Terra–Lua (384.400 km) e outra, 3 vezes menor,
    /// da sonda até as nuvens (130.000 km). Os números vêm da linha do tempo do roteiro; a curva de subida do medidor é estilizada.
    /// </summary>
    public sealed class PioneerClosestHud : MonoBehaviour
    {
        public const float RadiationStartSeconds = 12f;
        public const float RadiationRiseEndSeconds = 24f;
        public const float RadiationEndSeconds = 27f;
        public const float DistancesStartSeconds = 35f;
        public const float DistancesEndSeconds = 42f;
        public const float MoonBarFullSeconds = 37f;
        public const float CloudsBarFullSeconds = 39f;
        /// <summary>130.000 km sobre 384.400 km: a barra da sonda tem pouco mais de um terço da barra da Lua ("3 vezes mais perto").</summary>
        public const float CloudsFraction = 130000f / 384400f;
        /// <summary>Quadros por segundo do tremor da estática (o deslocamento só muda neste ritmo).</summary>
        public const float StaticFramesPerSecond = 24f;

        [SerializeField] CinemaHudView hud;
        [SerializeField, Tooltip("Sequência que este HUD acompanha (PIO_Closest).")] SequenceDefinition sequence;

        SequenceDirector director;
        Func<string, string> text;

        public void Initialize(SequenceDirector sequenceDirector, Func<string, string> textResolver)
        {
            director = sequenceDirector;
            text = textResolver;
            Hide();
        }

        public static bool RadiationVisibleAt(float seconds) => seconds >= RadiationStartSeconds && seconds < RadiationEndSeconds;

        /// <summary>Nível do medidor de radiação: sobe de ~0,08 a 1 entre 12 e 24 s (mais devagar no começo) e fica cheio até sair.</summary>
        public static float RadiationFillAt(float seconds)
        {
            var u = Mathf.Clamp01((seconds - RadiationStartSeconds) / (RadiationRiseEndSeconds - RadiationStartSeconds));
            return 0.08f + 0.92f * Mathf.Pow(u, 1.6f);
        }

        /// <summary>Opacidade da estática: acompanha o nível do medidor, de leve (0,05) a bem visível (0,4).</summary>
        public static float StaticOpacityAt(float seconds) => RadiationVisibleAt(seconds) ? 0.05f + 0.35f * RadiationFillAt(seconds) : 0f;

        /// <summary>Deslocamento do ruído em pixels: muda a cada 1/24 s (tremor do chuvisco) de forma determinística.</summary>
        public static Vector2 StaticOffsetAt(float seconds)
        {
            var frame = Mathf.FloorToInt(seconds * StaticFramesPerSecond);
            unchecked
            {
                return new Vector2(((frame * 7919) & 0x7F), ((frame * 104729) & 0x7F));
            }
        }

        public static bool DistancesVisibleAt(float seconds) => seconds >= DistancesStartSeconds && seconds < DistancesEndSeconds;

        public static float MoonFillAt(float seconds) => Smooth((seconds - DistancesStartSeconds) / (MoonBarFullSeconds - DistancesStartSeconds));

        public static float CloudsFillAt(float seconds) => CloudsFraction * Smooth((seconds - MoonBarFullSeconds) / (CloudsBarFullSeconds - MoonBarFullSeconds));

        static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        void Update() => Tick();

        void OnDisable() => Hide();

        /// <summary>Atualiza o HUD pelo relógio da sequência. Testes chamam direto.</summary>
        public void Tick()
        {
            if (hud == null || director == null)
                return;

            if (!director.IsPlaying || director.Current != sequence)
            {
                Hide();
                return;
            }

            Apply(director.TimeSeconds);
        }

        /// <summary>Desenha o HUD como no instante <paramref name="seconds"/> (sem ler a sequência).</summary>
        public void Apply(float seconds)
        {
            if (hud == null)
                return;

            var radiation = RadiationVisibleAt(seconds);
            hud.SetRadiation(radiation, radiation ? Resolve(CinemaHudView.RadiationKey) : string.Empty, RadiationFillAt(seconds), StaticOpacityAt(seconds), StaticOffsetAt(seconds));
            var distances = DistancesVisibleAt(seconds);
            hud.SetSpeed(distances, distances ? Resolve(CinemaHudView.SpeedKey) : string.Empty);
            hud.SetDistances(distances, distances ? Resolve(CinemaHudView.MoonDistanceKey) : string.Empty, distances ? Resolve(CinemaHudView.CloudsDistanceKey) : string.Empty,
                MoonFillAt(seconds), CloudsFillAt(seconds));
        }

        public void Hide()
        {
            if (hud != null)
                hud.HideAll();
        }

        string Resolve(string key)
        {
            var value = text != null ? text(key) : null;
            return string.IsNullOrEmpty(value) ? key : value;
        }
    }
}
