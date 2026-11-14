using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// MAR-016: aplica o perfil ambiental de um evento (<see cref="MarsStormProfileData"/>) à cena: névoa linear, luz do Sol, luz
    /// ambiente, cor do céu, disco solar visível e partículas de poeira. A poeira vem de fora: em 2007 a
    /// <see cref="Storm2007Activity"/> dirige <c>DustLevel</c>; em 2018 os marcos por etapa levam a poeira ao alvo (M27 espessa,
    /// M28 com o Sol atenuado, M31 termina sem sinal).
    /// <para>
    /// Garantias: a poeira EXIBIDA anda no máximo <c>maxDustPerSecond</c>: luz e névoa nunca saltam nem dão flash (só
    /// <see cref="Snap"/> salta, no carregamento). A névoa é a do pipeline (linear, por pixel): o rover é névoa e geometria juntos,
    /// nunca uma silhueta preta desenhada por cima. O Sol enfraquece mas continua legível (piso de intensidade e do disco).
    /// Partículas têm teto (perfil normal e baixo) e opacidade máxima; o tremor é só uma amplitude pontual, zerada em movimento
    /// reduzido. Ao desabilitar, os <see cref="RenderSettings"/> originais voltam. Todo o tempo é ATIVO (a pausa congela).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsAtmosphereController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] MarsStormProfileData profile;
        [SerializeField, Tooltip("victoria2007 ou valley2018.")] string eventId = MarsStormProfileData.Victoria2007;
        [SerializeField, Tooltip("A única luz com sombra da cena. Ligada na montagem.")] Light sun;
        [SerializeField, Tooltip("Câmera cujo fundo é o céu (sem skybox). Opcional.")] Camera skyCamera;
        [SerializeField, Tooltip("Disco solar visível (unlit). Opcional.")] Renderer sunDisc;
        [SerializeField, Tooltip("Poeira em suspensão, em espaço de mundo. Opcional.")] ParticleSystem dustParticles;
        [SerializeField, Tooltip("Perfil baixo (Web, acessibilidade): menos partículas, mesmo resultado para a ciência e os objetivos.")] bool lowProfile;
        [SerializeField, Tooltip("Movimento reduzido: sem tremor.")] bool reducedMotion;

        MaterialPropertyBlock block; // criado sob demanda: a Unity proíbe criá-lo no construtor de um MonoBehaviour
        IActiveTimeSource activeTime;
        MarsChapterFlow flow;
        Storm2007Activity storm;
        AtmosphereEventProfile current;
        float displayedDust;
        float targetDust;
        float rampRate;
        float shakeEnvelope;
        bool bound;
        bool savedGlobals;
        bool fogWas;
        FogMode fogModeWas;
        Color fogColorWas;
        float fogStartWas;
        float fogEndWas;
        AmbientMode ambientModeWas;
        Color ambientWas;

        public string EventId => eventId;
        public MarsStormProfileData Profile => profile;

        /// <summary>Poeira exibida agora (0 limpo, 1 denso). Anda no máximo <c>maxDustPerSecond</c>.</summary>
        public float DisplayedDust => displayedDust;

        public float TargetDust => targetDust;
        public AtmosphereState State { get; private set; }
        public bool LowProfile { get => lowProfile; set => lowProfile = value; }
        public bool ReducedMotion { get => reducedMotion; set => reducedMotion = value; }

        /// <summary>Amplitude do tremor de câmera agora, em metros. Pontual (acompanha a poeira subindo) e zero em movimento reduzido.</summary>
        public float ShakeMeters => reducedMotion ? 0f : State.shakeMeters * shakeEnvelope;

        public int ParticleCap => profile == null ? 0 : (lowProfile ? profile.maxParticlesLow : profile.maxParticlesNormal);

        public event Action<float> DustChanged;

        public void Initialize(MarsChapterFlow chapterFlow, Storm2007Activity stormActivity, IActiveTimeSource activeTimeSource, Light sunLight = null,
            Camera skyBackgroundCamera = null)
        {
            Unbind();
            flow = chapterFlow;
            storm = stormActivity;
            activeTime = activeTimeSource;
            if (sunLight != null)
                sun = sunLight;

            if (skyBackgroundCamera != null)
                skyCamera = skyBackgroundCamera;

            if (isActiveAndEnabled)
                Bind();
        }

        void OnEnable() => Bind();

        void OnDisable()
        {
            Unbind();
            RestoreGlobals();
        }

        void Bind()
        {
            if (bound || profile == null)
                return;

            current = profile.Get(eventId);
            if (current == null)
                return;

            bound = true;
            SaveGlobals();
            if (current.drivenByStorm && storm != null)
                storm.DustLevelChanged += OnStormDust;

            if (flow != null)
            {
                flow.StepStarted += OnStepStarted;
                if (flow.CurrentBinding != null)
                    OnStepStarted(flow.CurrentBinding);
            }

            Snap(current.drivenByStorm && storm != null ? storm.DustLevel : targetDust);
        }

        void Unbind()
        {
            if (!bound)
                return;

            bound = false;
            if (storm != null)
                storm.DustLevelChanged -= OnStormDust;

            if (flow != null)
                flow.StepStarted -= OnStepStarted;
        }

        void OnStormDust(float dust)
        {
            targetDust = Mathf.Clamp01(dust);
            rampRate = profile.maxDustPerSecond;
        }

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (current == null || current.drivenByStorm || binding == null)
                return;

            foreach (var key in current.keys)
            {
                if (key.stepId != binding.StepId)
                    continue;

                targetDust = Mathf.Clamp01(key.dust);
                rampRate = key.seconds > 0f ? Mathf.Min(profile.maxDustPerSecond, Mathf.Abs(targetDust - displayedDust) / key.seconds) : profile.maxDustPerSecond;
                if (targetDust > displayedDust)
                    shakeEnvelope = 1f; // um único pulso ao engrossar, que decai
                return;
            }

            // Reinício do capítulo: etapa anterior ao primeiro marco. Volta ao céu limpo (o carregamento usa Snap).
            if (IsBeforeFirstKey(binding.StepId))
            {
                targetDust = 0f;
                rampRate = profile.maxDustPerSecond;
            }
        }

        bool IsBeforeFirstKey(string stepId)
        {
            if (flow == null || current.keys.Count == 0)
                return false;

            var steps = flow.Steps;
            var index = -1;
            var first = int.MaxValue;
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].StepId == stepId)
                    index = i;

                foreach (var key in current.keys)
                {
                    if (steps[i].StepId == key.stepId)
                        first = Mathf.Min(first, i);
                }
            }

            return index >= 0 && index < first;
        }

        /// <summary>O perfil do evento, resolvido sem depender de OnEnable (que não roda em modo de edição nem antes da montagem).</summary>
        bool Resolve()
        {
            if (current == null && profile != null)
                current = profile.Get(eventId);

            return current != null;
        }

        /// <summary>Define a poeira exibida SEM suavizar. Só para o carregamento da cena (o resto nunca salta).</summary>
        public void Snap(float dust)
        {
            targetDust = displayedDust = Mathf.Clamp01(dust);
            Apply();
        }

        void Update()
        {
            Tick(activeTime != null ? activeTime.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO (zero na pausa). Público para testes; Update chama com o SessionClock.</summary>
        public void Tick(float activeDeltaSeconds)
        {
            if (!Resolve())
                return;

            var dt = activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds) ? activeDeltaSeconds : 0f;
            if (dt > 0f && !Mathf.Approximately(displayedDust, targetDust))
            {
                var rate = Mathf.Min(profile.maxDustPerSecond, rampRate > 0f ? rampRate : profile.maxDustPerSecond);
                displayedDust = Mathf.MoveTowards(displayedDust, targetDust, rate * dt);
                DustChanged?.Invoke(displayedDust);
            }

            shakeEnvelope = Mathf.MoveTowards(shakeEnvelope, 0f, dt / 2f); // o pulso decai em 2 s: pontual, nunca contínuo
            Apply();
        }

        void Apply()
        {
            if (!Resolve())
                return;

            SaveGlobals(); // guarda os RenderSettings originais antes da primeira escrita; OnDisable os devolve

            State = AtmosphereState.Lerp(current.clear, current.dense, displayedDust);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = State.fogColor;
            RenderSettings.fogStartDistance = State.fogStartMeters;
            RenderSettings.fogEndDistance = State.fogEndMeters;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = State.ambient;

            if (sun != null)
            {
                sun.intensity = State.sunIntensity;
                sun.color = State.sunColor;
            }

            if (skyCamera != null)
            {
                skyCamera.clearFlags = CameraClearFlags.SolidColor;
                skyCamera.backgroundColor = State.sky;
            }

            if (sunDisc != null)
            {
                if (block == null)
                    block = new MaterialPropertyBlock();

                sunDisc.GetPropertyBlock(block);
                var color = State.sunColor;
                color.a = State.sunDiscAlpha;
                block.SetColor(BaseColorId, color);
                sunDisc.SetPropertyBlock(block);
            }

            if (dustParticles != null)
            {
                var main = dustParticles.main;
                main.maxParticles = ParticleCap;
                var startColor = main.startColor.color;
                startColor.a = Mathf.Min(startColor.a, profile.particleAlphaCap);
                main.startColor = startColor;
                var rate = State.particlesPerSecond * (lowProfile ? profile.lowRateFactor : 1f);
                var emission = dustParticles.emission;
                if (!Mathf.Approximately(emission.rateOverTime.constant, rate))
                    emission.rateOverTime = rate;

                if (rate > 0.01f && !dustParticles.isPlaying)
                    dustParticles.Play();
                else if (rate <= 0.01f && dustParticles.isPlaying)
                    dustParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void SaveGlobals()
        {
            if (savedGlobals)
                return;

            savedGlobals = true;
            fogWas = RenderSettings.fog;
            fogModeWas = RenderSettings.fogMode;
            fogColorWas = RenderSettings.fogColor;
            fogStartWas = RenderSettings.fogStartDistance;
            fogEndWas = RenderSettings.fogEndDistance;
            ambientModeWas = RenderSettings.ambientMode;
            ambientWas = RenderSettings.ambientLight;
        }

        void RestoreGlobals()
        {
            if (!savedGlobals)
                return;

            savedGlobals = false;
            RenderSettings.fog = fogWas;
            RenderSettings.fogMode = fogModeWas;
            RenderSettings.fogColor = fogColorWas;
            RenderSettings.fogStartDistance = fogStartWas;
            RenderSettings.fogEndDistance = fogEndWas;
            RenderSettings.ambientMode = ambientModeWas;
            RenderSettings.ambientLight = ambientWas;
        }

        /// <summary>Perfil, evento e materiais que a névoa alcança. Vazio = pronto.</summary>
        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsAtmosphereController);
            if (profile == null)
            {
                ContractValidation.Report(errors, owner, "falta MarsStormProfileData.");
                return errors;
            }

            errors.AddRange(profile.Validate());
            if (profile.Get(eventId) == null)
                ContractValidation.Report(errors, owner, $"evento '{eventId}' desconhecido.");

            // A névoa do pipeline só alcança shaders que a implementam; um shader sem névoa viraria silhueta imune à poeira.
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer)
                    continue;

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null || !material.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                        ContractValidation.Report(errors, owner, $"'{renderer.name}' usa shader fora do URP: a névoa pode não alcançá-lo.");
                }
            }

            return errors;
        }
    }
}
