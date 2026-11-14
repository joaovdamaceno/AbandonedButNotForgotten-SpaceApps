using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>Conjunto de parâmetros ambientais (luz, atmosfera e partículas) num extremo do perfil: céu limpo ou poeira densa.</summary>
    [Serializable]
    public struct AtmosphereState
    {
        public Color fogColor;
        [Min(0f)] public float fogStartMeters;
        [Min(1f)] public float fogEndMeters;
        [Min(0f)] public float sunIntensity;
        public Color sunColor;
        public Color ambient;
        public Color sky;
        [Min(0f), Tooltip("Emissão de partículas de poeira por segundo (perfil normal).")] public float particlesPerSecond;
        [Range(0f, 1f), Tooltip("Opacidade do disco solar visível: o Sol enfraquece mas continua legível.")] public float sunDiscAlpha;
        [Min(0f), Tooltip("Amplitude do tremor de câmera, em metros. Pontual e reduzível.")] public float shakeMeters;

        public static AtmosphereState Lerp(AtmosphereState a, AtmosphereState b, float t)
        {
            t = Mathf.Clamp01(t);
            return new AtmosphereState
            {
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                fogStartMeters = Mathf.Lerp(a.fogStartMeters, b.fogStartMeters, t),
                fogEndMeters = Mathf.Lerp(a.fogEndMeters, b.fogEndMeters, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
                ambient = Color.Lerp(a.ambient, b.ambient, t),
                sky = Color.Lerp(a.sky, b.sky, t),
                particlesPerSecond = Mathf.Lerp(a.particlesPerSecond, b.particlesPerSecond, t),
                sunDiscAlpha = Mathf.Lerp(a.sunDiscAlpha, b.sunDiscAlpha, t),
                shakeMeters = Mathf.Lerp(a.shakeMeters, b.shakeMeters, t),
            };
        }

        /// <summary>Fração da cor do objeto que a névoa linear já cobre a esta distância (0 = nítido, 1 = só névoa).</summary>
        public float FogFactorAt(float distanceMeters) =>
            Mathf.Clamp01((distanceMeters - fogStartMeters) / Mathf.Max(1f, fogEndMeters - fogStartMeters));
    }

    /// <summary>Marco do perfil de um evento: ao entrar na etapa, a poeira vai ao alvo no tempo pedido.</summary>
    [Serializable]
    public sealed class DustKey
    {
        public string stepId;
        [Range(0f, 1f)] public float dust;
        [Min(0f), Tooltip("0 = imediato; senão a poeira anda gradualmente até o alvo.")] public float seconds;
    }

    /// <summary>Perfil de um evento: extremos limpo e denso e como a poeira evolui.</summary>
    [Serializable]
    public sealed class AtmosphereEventProfile
    {
        public string id;
        [Tooltip("Verdadeiro: a poeira vem de fora (a tempestade de 2007 dirige DustLevel). Falso: vem dos marcos por etapa.")] public bool drivenByStorm;
        public AtmosphereState clear;
        public AtmosphereState dense;
        public List<DustKey> keys = new List<DustKey>();
        [Range(0f, 1f), Tooltip("Poeira esperada ao fim do evento: 2007 recupera a luz (baixa); 2018 termina sem sinal (alta).")] public float endsAtDust;
    }

    /// <summary>
    /// MAR-016: perfil ambiental POR EVENTO (Victoria 2007 e Perseverance Valley 2018): luz, atmosfera e partículas em um só lugar,
    /// para a poeira escurecer sem apagar a ciência. Limites guardados na validação: névoa linear que não apaga o rover
    /// (distância da câmera de condução) nem o disco solar; Sol e luz ambiente com piso; teto de partículas (e perfil baixo);
    /// partículas sem branco estourado; tremor pontual e reduzível. Tensão vem de luz e atmosfera, não de tornado: o vento das
    /// maiores tempestades não rasga equipamento (MS18).
    /// </summary>
    [CreateAssetMenu(fileName = "MarsStormProfile", menuName = "Abandoned/Marte/Storm profile")]
    public sealed class MarsStormProfileData : ScriptableObject
    {
        public const string Victoria2007 = "victoria2007";
        public const string Valley2018 = "valley2018";

        [Header("Evento")]
        public AtmosphereEventProfile victoria = new AtmosphereEventProfile { id = Victoria2007, drivenByStorm = true, endsAtDust = 0.1f };
        public AtmosphereEventProfile valley = new AtmosphereEventProfile { id = Valley2018, endsAtDust = 1f };

        [Header("Distâncias de referência, em metros")]
        [Min(1f), Tooltip("Câmera de condução atrás do rover: o rover não pode sumir na névoa.")] public float roverDistanceMeters = 6f;
        [Min(1f), Tooltip("Distância do disco solar visível.")] public float sunDiscDistanceMeters = 40f;

        [Header("Limites que preservam objetivos e ciência")]
        [Range(0f, 1f)] public float maxFogFactorAtRover = 0.5f;
        [Range(0f, 1f)] public float maxFogFactorAtSun = 0.7f;
        [Range(0.05f, 1f), Tooltip("Piso da intensidade do Sol e da luz ambiente em relação ao céu limpo.")] public float minLightFraction = 0.2f;
        [Range(0.05f, 1f)] public float minSunDiscAlpha = 0.2f;
        [Range(0.1f, 1f), Tooltip("Luminância máxima da névoa e da poeira: sem branco estourado.")] public float maxDustLuminance = 0.75f;

        [Header("Partículas e tremor (teto e perfil baixo)")]
        [Min(10)] public int maxParticlesNormal = 300;
        [Min(10)] public int maxParticlesLow = 120;
        [Range(0.1f, 1f)] public float lowRateFactor = 0.4f;
        [Range(0.05f, 0.5f)] public float particleAlphaCap = 0.3f;
        [Range(0f, 0.05f)] public float maxShakeMeters = 0.02f;

        [Header("Sem flash")]
        [Min(0.05f), Tooltip("Velocidade máxima de mudança da poeira exibida, por segundo: luz e névoa nunca saltam.")] public float maxDustPerSecond = 0.35f;

        public AtmosphereEventProfile Get(string eventId) =>
            eventId == Victoria2007 ? victoria : eventId == Valley2018 ? valley : null;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"MarsStormProfileData '{name}'";
            Check(errors, owner, victoria, Victoria2007);
            Check(errors, owner, valley, Valley2018);

            if (victoria != null && victoria.endsAtDust > 0.2f)
                ContractValidation.Report(errors, owner, "2007 deve terminar recuperando a luz (poeira final ≤ 0,2).");

            if (valley != null && valley.endsAtDust < 0.8f)
                ContractValidation.Report(errors, owner, "2018 deve terminar sem sinal (poeira final ≥ 0,8).");

            if (maxParticlesLow >= maxParticlesNormal)
                ContractValidation.Report(errors, owner, "o perfil baixo precisa de menos partículas que o normal.");

            if (maxParticlesNormal > 500)
                ContractValidation.Report(errors, owner, "teto de 500 partículas visíveis (Docs/04_WEB_AND_QA.md: 200–500).");

            return errors;
        }

        void Check(List<string> errors, string owner, AtmosphereEventProfile profile, string expectedId)
        {
            if (profile == null || profile.id != expectedId)
            {
                ContractValidation.Report(errors, owner, $"falta o perfil '{expectedId}'.");
                return;
            }

            var where = $"{owner}/{profile.id}";
            var dense = profile.dense;
            var clear = profile.clear;
            if (dense.FogFactorAt(roverDistanceMeters) > maxFogFactorAtRover)
                ContractValidation.Report(errors, where, $"a névoa densa cobre {dense.FogFactorAt(roverDistanceMeters):0.00} do rover a {roverDistanceMeters} m (máx. {maxFogFactorAtRover}): o rover sumiria.");

            if (dense.FogFactorAt(sunDiscDistanceMeters) > maxFogFactorAtSun)
                ContractValidation.Report(errors, where, $"a névoa densa cobre {dense.FogFactorAt(sunDiscDistanceMeters):0.00} do Sol a {sunDiscDistanceMeters} m (máx. {maxFogFactorAtSun}): o Sol ilegível.");

            if (dense.sunIntensity < clear.sunIntensity * minLightFraction)
                ContractValidation.Report(errors, where, "o Sol denso fica abaixo do piso de luz.");

            if (Luminance(dense.ambient) < Luminance(clear.ambient) * minLightFraction)
                ContractValidation.Report(errors, where, "a luz ambiente densa fica abaixo do piso.");

            if (dense.sunDiscAlpha < minSunDiscAlpha)
                ContractValidation.Report(errors, where, "o disco solar denso some: o Sol precisa continuar legível.");

            foreach (var color in new[] { clear.fogColor, dense.fogColor, clear.sky, dense.sky })
            {
                if (Luminance(color) > maxDustLuminance)
                    ContractValidation.Report(errors, where, "névoa ou céu acima da luminância máxima: poeira branca estourada.");
            }

            if (dense.shakeMeters > maxShakeMeters || clear.shakeMeters > maxShakeMeters)
                ContractValidation.Report(errors, where, "tremor acima do limite.");

            if (dense.fogEndMeters <= dense.fogStartMeters || clear.fogEndMeters <= clear.fogStartMeters)
                ContractValidation.Report(errors, where, "a névoa precisa terminar depois de começar.");

            if (!profile.drivenByStorm && (profile.keys == null || profile.keys.Count == 0))
                ContractValidation.Report(errors, where, "um perfil sem tempestade dirigida precisa de marcos por etapa.");

            if (profile.keys != null)
            {
                foreach (var key in profile.keys)
                    ContractValidation.RequireId(errors, where, "key.stepId", key.stepId);
            }
        }

        static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
    }
}
