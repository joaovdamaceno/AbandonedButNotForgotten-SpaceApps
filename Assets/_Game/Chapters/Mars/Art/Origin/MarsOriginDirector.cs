using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M01–M03: Marte no espaço, laboratório e globo dos pousos. Lê o mesmo relógio do <see cref="SequenceDirector"/> (tempo local +
    /// deslocamento); só a região do bloco atual fica ativa (uma região 3D por vez). Os mapas orbitais são painéis de UI: o diretor expõe
    /// o estado em <see cref="Overlay"/> (imagem, crédito, opacidade). Uma câmera (a do CameraDirector), zero partículas.
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
        [SerializeField] Texture2D meridianiMap;
        [SerializeField] Texture2D gusevMap;
        [SerializeField] List<MarsLandingLabel> labels = new List<MarsLandingLabel>();

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
        public Texture2D MeridianiMap => meridianiMap;
        public Texture2D GusevMap => gusevMap;
        public IReadOnlyList<MarsLandingLabel> Labels => labels;
        public IReadOnlyList<MarsLandingLabel> VisibleLabels => visible;
        public float TimeSeconds { get; private set; } = -1f;

        /// <summary>Estado do painel do mapa orbital neste instante (no máximo um visível).</summary>
        public MarsOrbitalMapOverlay Overlay { get; private set; }

        public const string MeridianiCreditKey = "MAR.ORI.CR1";
        public const string GusevCreditKey = "MAR.ORI.CR2";

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

            Glow(gusevMarker, pose.GusevGlow);
            Glow(meridianiMarker, pose.MeridianiGlow);

            Overlay = pose.MeridianiMapOpacity >= pose.GusevMapOpacity
                ? new MarsOrbitalMapOverlay(meridianiMap, MeridianiCreditKey, pose.MeridianiMapOpacity)
                : new MarsOrbitalMapOverlay(gusevMap, GusevCreditKey, pose.GusevMapOpacity);
            RefreshLabels(timeSeconds);
        }

        void Glow(Renderer target, float amount)
        {
            if (target == null)
                return;

            target.enabled = amount > 0.001f;
            if (!blocks.TryGetValue(target, out var block))
                blocks[target] = block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(1f, 0.82f, 0.4f, Mathf.Clamp01(amount)));
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
            Need(meridianiMap, nameof(meridianiMap));
            Need(gusevMap, nameof(gusevMap));
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
