using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M01–M03: laboratório, mapa e lançamentos. Lê o mesmo relógio do <see cref="SequenceDirector"/> (tempo local + deslocamento) e
    /// aplica globo, destaques e lançamentos; câmera, legendas e letreiros usam o mesmo tempo. Uma única câmera (a do CameraDirector),
    /// nenhum sistema de partículas: a chama é uma malha única reaproveitada pelos dois foguetes.
    /// </summary>
    public sealed class MarsOriginDirector : MonoBehaviour
    {
        public const string RigId = "mars.origin";
        public const string SequenceM01 = "mars.seq.M01";
        public const string SequenceM02 = "mars.seq.M02";
        public const string SequenceM03 = "mars.seq.M03";

        [SerializeField] Transform globe;
        [SerializeField] Renderer gusevMarker;
        [SerializeField] Renderer meridianiMarker;
        [SerializeField] Renderer hematitePatch;
        [SerializeField] Transform rocketA;
        [SerializeField] Transform rocketB;
        [SerializeField] Transform flame;
        [SerializeField] List<MarsLandingLabel> labels = new List<MarsLandingLabel>();

        readonly List<MarsLandingLabel> visible = new List<MarsLandingLabel>();
        readonly Dictionary<Renderer, MaterialPropertyBlock> blocks = new Dictionary<Renderer, MaterialPropertyBlock>();
        SequenceDirector sequenceDirector;
        CameraDirector cameraDirector;
        long visibleMask;
        Vector3 rocketABase;
        Vector3 rocketBBase;
        bool baseCaptured;

        public Transform Globe => globe;
        public Renderer GusevMarker => gusevMarker;
        public Renderer MeridianiMarker => meridianiMarker;
        public Renderer HematitePatch => hematitePatch;
        public Transform RocketA => rocketA;
        public Transform RocketB => rocketB;
        public Transform Flame => flame;
        public IReadOnlyList<MarsLandingLabel> Labels => labels;
        public IReadOnlyList<MarsLandingLabel> VisibleLabels => visible;
        public float TimeSeconds { get; private set; } = -1f;

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
                case SequenceM02: return MarsOriginChoreography.MapStart;
                case SequenceM03: return MarsOriginChoreography.LaunchStart;
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
            if (!baseCaptured && rocketA != null && rocketB != null)
            {
                rocketABase = rocketA.localPosition;
                rocketBBase = rocketB.localPosition;
                baseCaptured = true;
            }

            timeSeconds = Mathf.Clamp(timeSeconds, 0f, MarsOriginChoreography.TotalSeconds);
            TimeSeconds = timeSeconds;
            var pose = MarsOriginChoreography.Sample(timeSeconds);
            if (globe != null)
                globe.localRotation = pose.GlobeRotation;

            Glow(gusevMarker, pose.GusevGlow, 1.2f);
            Glow(meridianiMarker, pose.MeridianiGlow, 1.2f);
            Glow(hematitePatch, pose.HematiteGlow, 0.55f);

            if (rocketA != null) rocketA.localPosition = rocketABase + Vector3.up * pose.RocketAHeight;
            if (rocketB != null) rocketB.localPosition = rocketBBase + Vector3.up * pose.RocketBHeight;
            if (flame != null)
            {
                var host = pose.FlameOnA ? rocketA : rocketB;
                flame.gameObject.SetActive(pose.Flame > 0.001f);
                if (host != null)
                {
                    flame.localPosition = host.localPosition + new Vector3(0f, 0.5f, 0f);
                    flame.localScale = new Vector3(2.5f, 5f * Mathf.Max(0.001f, pose.Flame), 2.5f);
                }
            }

            RefreshLabels(timeSeconds);
        }

        void Glow(Renderer target, float amount, float maxAlpha)
        {
            if (target == null)
                return;

            target.enabled = amount > 0.001f;
            if (!blocks.TryGetValue(target, out var block))
                blocks[target] = block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(1f, 0.82f, 0.4f, Mathf.Clamp01(amount * maxAlpha)));
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

            Need(globe, nameof(globe));
            Need(gusevMarker, nameof(gusevMarker));
            Need(meridianiMarker, nameof(meridianiMarker));
            Need(hematitePatch, nameof(hematitePatch));
            Need(rocketA, nameof(rocketA));
            Need(rocketB, nameof(rocketB));
            Need(flame, nameof(flame));
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
