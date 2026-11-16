using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M01–M03: Marte no espaço, laboratório e globo dos pousos. Lê o mesmo relógio do <see cref="SequenceDirector"/> (tempo local +
    /// deslocamento); só a região do bloco atual fica ativa (uma região 3D por vez). Uma câmera (a do CameraDirector), zero partículas.
    /// </summary>
    public sealed class MarsOriginDirector : MonoBehaviour
    {
        public const string RigId = "mars.origin";
        public const string SequenceM01 = "mars.seq.M01";
        public const string SequenceM02 = "mars.seq.M02";
        public const string SequenceM03 = "mars.seq.M03";
        const int RegionCount = 3;

        [SerializeField] Transform[] regions = new Transform[RegionCount]; // espaço, laboratório, globo
        [SerializeField] Transform planet;
        [SerializeField] Transform globe;
        [SerializeField] Renderer gusevMarker;
        [SerializeField] Renderer meridianiMarker;
        [SerializeField] Renderer gusevPulse;
        [SerializeField] Renderer meridianiPulse;
        [SerializeField] List<MarsLandingLabel> labels = new List<MarsLandingLabel>();
        [Header("Laboratório (penumbra cinematográfica)")]
        [SerializeField, Tooltip("Luz ambiente enquanto o laboratório está em cena; as luzes de estúdio sobre os rovers viram o destaque.")]
        Color labAmbient = new Color(0.035f, 0.037f, 0.045f);
        [SerializeField, Min(0f), Tooltip("Intensidade do sol da cena enquanto o laboratório está em cena.")]
        float labSunIntensity = 0.25f;
        [SerializeField, Range(0f, 1f), Tooltip("Fração da intensidade das luzes de estúdio do laboratório (poças de luz sobre os rovers).")]
        float labLampScale = 0.55f;

        bool labLit;
        float savedSunIntensity;
        Color savedAmbient;
        UnityEngine.Rendering.AmbientMode savedAmbientMode;
        Light[] labLamps;
        float[] labLampIntensity;

        readonly List<MarsLandingLabel> visible = new List<MarsLandingLabel>();
        readonly Dictionary<Renderer, MaterialPropertyBlock> blocks = new Dictionary<Renderer, MaterialPropertyBlock>();
        SequenceDirector sequenceDirector;
        CameraDirector cameraDirector;
        long visibleMask;

        public IReadOnlyList<Transform> Regions => regions;
        public Transform Planet => planet;
        public Transform Globe => globe;
        public Renderer GusevMarker => gusevMarker;
        public Renderer MeridianiMarker => meridianiMarker;
        public Renderer GusevPulse => gusevPulse;
        public Renderer MeridianiPulse => meridianiPulse;
        public IReadOnlyList<MarsLandingLabel> Labels => labels;
        public IReadOnlyList<MarsLandingLabel> VisibleLabels => visible;
        public float TimeSeconds { get; private set; } = -1f;

        public const string GusevLabelKey = "MAR.ORI.03";
        public const string MeridianiLabelKey = "MAR.ORI.04";

        /// <summary>Marcador do globo ao qual um letreiro pertence (a UI desenha o texto sobre ele); nulo se o letreiro é de tela.</summary>
        public Renderer MarkerOf(string textKey) =>
            textKey == GusevLabelKey ? gusevMarker : textKey == MeridianiLabelKey ? meridianiMarker : null;

        public event Action<IReadOnlyList<MarsLandingLabel>> LabelsChanged;

        public void Initialize(SequenceDirector sequence, CameraDirector camera)
        {
            if (sequenceDirector != null)
                sequenceDirector.Started -= OnStarted;

            sequenceDirector = sequence;
            cameraDirector = camera;
            if (cameraDirector != null)
                cameraDirector.RegisterRig(RigId, transform);
            if (sequenceDirector != null)
                sequenceDirector.Started += OnStarted;
        }

        void OnDisable()
        {
            SetLabLighting(false);
            if (sequenceDirector != null)
                sequenceDirector.Started -= OnStarted;
            if (cameraDirector != null)
                cameraDirector.UnregisterRig(RigId);
        }

        public static float OffsetOf(string sequenceId)
        {
            switch (sequenceId)
            {
                case SequenceM01: return 0f;
                case SequenceM02: return MarsOriginChoreography.LabStart;
                case SequenceM03: return MarsOriginChoreography.GlobeStart;
                default: return -1f;
            }
        }

        void OnStarted(string id)
        {
            var offset = OffsetOf(id);
            if (offset >= 0f)
                ApplyAt(offset);
        }

        void LateUpdate()
        {
            if (sequenceDirector == null || !sequenceDirector.IsPlaying || sequenceDirector.Current == null)
                return;

            var offset = OffsetOf(sequenceDirector.Current.SequenceId);
            if (offset >= 0f)
                ApplyAt(offset + sequenceDirector.TimeSeconds);
        }

        Light sceneSun;

        Light SceneSun()
        {
            if (sceneSun != null)
                return sceneSun;
            sceneSun = RenderSettings.sun;
            if (sceneSun == null)
                foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (light.type == LightType.Directional && light.gameObject.scene == gameObject.scene)
                    {
                        sceneSun = light;
                        break;
                    }
            return sceneSun;
        }

        /// <summary>Penumbra só no laboratório: guarda a luz da região ao entrar e devolve ao sair (globo, cancelamento).</summary>
        void SetLabLighting(bool lab)
        {
            var sun = SceneSun();
            if (lab)
            {
                if (!labLit)
                {
                    savedAmbient = RenderSettings.ambientLight;
                    savedAmbientMode = RenderSettings.ambientMode;
                    savedSunIntensity = sun != null ? sun.intensity : 0f;
                    var labRoot = regions.Length > MarsOriginChoreography.RegionLab ? regions[MarsOriginChoreography.RegionLab] : null;
                    labLamps = labRoot != null ? labRoot.GetComponentsInChildren<Light>(true) : new Light[0];
                    labLampIntensity = new float[labLamps.Length];
                    for (var i = 0; i < labLamps.Length; i++)
                    {
                        labLampIntensity[i] = labLamps[i].intensity;
                        labLamps[i].intensity *= labLampScale;
                    }
                }

                // Reaplicado a cada quadro: a luz da região pode ser reposta ao começar a sequência.
                if (sun != null)
                    sun.intensity = labSunIntensity;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = labAmbient;
            }
            else if (labLit)
            {
                RenderSettings.ambientMode = savedAmbientMode;
                RenderSettings.ambientLight = savedAmbient;
                if (sun != null)
                    sun.intensity = savedSunIntensity;
                for (var i = 0; labLamps != null && i < labLamps.Length; i++)
                    if (labLamps[i] != null)
                        labLamps[i].intensity = labLampIntensity[i];
            }

            labLit = lab;
        }

        public void ApplyAt(float timeSeconds)
        {
            timeSeconds = Mathf.Clamp(timeSeconds, 0f, MarsOriginChoreography.TotalSeconds);
            TimeSeconds = timeSeconds;
            var pose = MarsOriginChoreography.Sample(timeSeconds);

            var active = MarsOriginChoreography.RegionAt(timeSeconds);
            for (var i = 0; i < regions.Length; i++)
            {
                if (regions[i] != null)
                    regions[i].gameObject.SetActive(i == active);
            }

            if (planet != null)
                planet.localRotation = Quaternion.Euler(0f, pose.PlanetSpinDegrees, 0f);
            if (globe != null)
                globe.localRotation = pose.GlobeRotation;

            SetLabLighting(active == MarsOriginChoreography.RegionLab);
            Glow(gusevMarker, gusevPulse, pose.GusevGlow, pose.Pulse);
            Glow(meridianiMarker, meridianiPulse, pose.MeridianiGlow, pose.Pulse);

            RefreshLabels(timeSeconds);
        }

        void Glow(Renderer marker, Renderer pulse, float amount, float phase)
        {
            Tint(marker, amount);
            if (pulse == null)
                return;

            // Anel que se expande e some a cada ciclo; calculado do tempo (pausa congela) e sem alocar por quadro.
            var fade = (1f - phase) * (1f - phase) * amount;
            Tint(pulse, fade);
            pulse.transform.localScale = Vector3.one * Mathf.Lerp(1f, 3.2f, phase);
        }

        void Tint(Renderer target, float alpha)
        {
            if (target == null)
                return;

            target.enabled = alpha > 0.001f;
            if (!blocks.TryGetValue(target, out var block))
                blocks[target] = block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(1f, 0.9f, 0.68f, Mathf.Clamp01(alpha)));
            target.SetPropertyBlock(block);
        }

        void RefreshLabels(float time)
        {
            // Sem alocação por quadro: o conjunto visível é comparado por máscara de bits (AGENTS.md).
            visible.Clear();
            var mask = 0L;
            for (var i = 0; i < labels.Count; i++)
            {
                if (labels[i].IsVisibleAt(time))
                {
                    visible.Add(labels[i]);
                    mask |= 1L << i;
                }
            }

            if (mask != visibleMask)
            {
                visibleMask = mask;
                LabelsChanged?.Invoke(visible);
            }
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsOriginDirector);
            void Need(object value, string field)
            {
                if (value == null || (value is UnityEngine.Object o && o == null))
                    ContractValidation.Report(errors, owner, $"{field} não atribuído.");
            }

            if (regions == null || regions.Length != RegionCount || Array.Exists(regions, r => r == null))
                ContractValidation.Report(errors, owner, "são três regiões (espaço, laboratório, globo).");
            Need(planet, nameof(planet));
            Need(globe, nameof(globe));
            Need(gusevMarker, nameof(gusevMarker));
            Need(meridianiMarker, nameof(meridianiMarker));
            Need(gusevPulse, nameof(gusevPulse));
            Need(meridianiPulse, nameof(meridianiPulse));
            foreach (var label in labels)
            {
                ContractValidation.RequireId(errors, owner, "labels[].textKey", label.TextKey);
                if (label.StartSeconds < 0f || label.EndSeconds <= label.StartSeconds || label.EndSeconds > MarsOriginChoreography.TotalSeconds + 1e-3f)
                    ContractValidation.Report(errors, owner, $"letreiro '{label.TextKey}' fora de 0–{MarsOriginChoreography.TotalSeconds} s.");
            }

            return errors;
        }
    }
}
