using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M31–M32: memorial das duas máquinas. Lê o relógio do <see cref="SequenceDirector"/>; usa a ÚNICA câmera do
    /// <see cref="CameraDirector"/>. Só a região do bloco atual fica ativa (Marte, retrato, imagem, díptico). A memória escolhida pela
    /// MAR-008 (<see cref="EvidenceRecord"/>) é a mostrada; sem miniatura, mostra um quadro de reserva legível, sem exceção. O título de
    /// 4 s termina nas máquinas imóveis e só então os controles globais de arquivo/replay são pedidos.
    /// </summary>
    public sealed class MarsMemorialDirector : MonoBehaviour
    {
        public const string RigId = "mars.memorial";
        public const string SequenceM31 = "mars.seq.M31";
        public const string SequenceM32 = "mars.seq.M32";
        const int RegionCount = 4;

        [SerializeField] Transform[] regions = new Transform[RegionCount]; // valley, portrait, frame, diptych
        [SerializeField] Transform[] wheels = Array.Empty<Transform>();
        [SerializeField] Transform mast;
        [SerializeField] Transform gimbal;
        [SerializeField] Renderer memoryFrame;
        [SerializeField] Texture2D fallbackTexture;
        [SerializeField] Light rimLight;
        [SerializeField] List<MarsLandingLabel> labels = new List<MarsLandingLabel>();
        [SerializeField, Min(0.1f)] float frameWidthMeters = 8f;

        readonly List<MarsLandingLabel> visible = new List<MarsLandingLabel>();
        MaterialPropertyBlock block;
        SequenceDirector sequenceDirector;
        CameraDirector cameraDirector;
        Quaternion[] wheelRest;
        Quaternion mastRest;
        Quaternion gimbalRest;
        long visibleMask;
        bool controlsRaised;
        bool restCaptured;

        public EvidenceRecord Memory { get; private set; }
        public bool ShowingFallback { get; private set; } = true;
        public bool IsPlayersPhoto => Memory != null && Memory.CanBePresentedAsPlayers;
        public const string FallbackLabelKey = "MAR.MEM.FALLBACK";
        public string MemoryLabelKey => Memory != null ? Memory.ResultKey : null;
        /// <summary>Chave do aviso de reserva quando não há miniatura (a UI mostra junto do quadro de reserva).</summary>
        public string ActiveFallbackKey => ShowingFallback ? FallbackLabelKey : null;
        public float TimeSeconds { get; private set; } = -1f;
        public bool HudHidden => MarsMemorialChoreography.HudHidden(TimeSeconds);
        public bool ControlsRaised => controlsRaised;
        public IReadOnlyList<Transform> Regions => regions;
        public Renderer MemoryFrame => memoryFrame;
        public Light RimLightSource => rimLight;
        public Transform Mast => mast;
        public IReadOnlyList<Transform> Wheels => wheels;
        public IReadOnlyList<MarsLandingLabel> VisibleLabels => visible;
        public IReadOnlyList<MarsLandingLabel> Labels => labels;

        public event Action<IReadOnlyList<MarsLandingLabel>> LabelsChanged;

        /// <summary>Depois dos 4 s finais: a UI/diretor global expõe VOLTAR AO ARQUIVO / REPETIR CAPÍTULO. Uma vez por execução.</summary>
        public event Action ControlsReady;

        public static float OffsetOf(string sequenceId) => sequenceId == SequenceM31 ? 0f : sequenceId == SequenceM32 ? MarsMemorialChoreography.MemorialSeconds : -1f;

        public void Initialize(SequenceDirector sequence, CameraDirector camera)
        {
            if (sequenceDirector != null)
            {
                sequenceDirector.Started -= OnStarted;
                sequenceDirector.Finished -= OnFinished;
            }

            sequenceDirector = sequence;
            cameraDirector = camera;
            if (cameraDirector != null)
                cameraDirector.RegisterRig(RigId, transform);
            if (sequenceDirector != null)
            {
                sequenceDirector.Started += OnStarted;
                sequenceDirector.Finished += OnFinished;
            }
        }

        void OnDisable()
        {
            if (sequenceDirector != null)
            {
                sequenceDirector.Started -= OnStarted;
                sequenceDirector.Finished -= OnFinished;
            }

            if (cameraDirector != null)
                cameraDirector.UnregisterRig(RigId);
        }

        void OnStarted(string id)
        {
            var offset = OffsetOf(id);
            if (offset < 0f)
                return;

            if (id == SequenceM31)
                controlsRaised = false;
            ApplyAt(offset);
        }

        void OnFinished(SequenceResult result)
        {
            if (result.SequenceId != SequenceM32 || result.Reason != SequenceExitReason.Completed)
                return;

            ApplyAt(MarsMemorialChoreography.TotalSeconds);
            RaiseControls();
        }

        void RaiseControls()
        {
            if (controlsRaised)
                return;

            controlsRaised = true;
            ControlsReady?.Invoke();
        }

        void LateUpdate()
        {
            if (sequenceDirector == null || !sequenceDirector.IsPlaying || sequenceDirector.Current == null)
                return;

            var offset = OffsetOf(sequenceDirector.Current.SequenceId);
            if (offset >= 0f)
                ApplyAt(offset + sequenceDirector.TimeSeconds);
        }

        /// <summary>Define a memória confirmada na MAR-008. Nula ou sem miniatura: quadro de reserva. Nunca lança.</summary>
        public void SetMemory(EvidenceRecord record)
        {
            Memory = record;
            var sprite = record != null ? record.Thumbnail : null;
            var texture = sprite != null ? sprite.texture : null;
            ShowingFallback = texture == null;
            if (memoryFrame == null)
                return;

            if (block == null)
                block = new MaterialPropertyBlock();
            var shown = texture != null ? texture : fallbackTexture;
            memoryFrame.GetPropertyBlock(block);
            if (shown != null)
                block.SetTexture("_BaseMap", shown);
            memoryFrame.SetPropertyBlock(block);

            var aspect = shown != null && shown.height > 0 ? (float)shown.width / shown.height : 16f / 9f;
            memoryFrame.transform.localScale = new Vector3(frameWidthMeters, frameWidthMeters / Mathf.Max(0.1f, aspect), 1f);
        }

        public void ApplyAt(float timeSeconds)
        {
            CaptureRest();
            timeSeconds = Mathf.Clamp(timeSeconds, 0f, MarsMemorialChoreography.TotalSeconds);
            TimeSeconds = timeSeconds;

            var blockIndex = MarsMemorialChoreography.BlockAt(timeSeconds);
            var active = blockIndex == MarsMemorialChoreography.BlockMechanisms || blockIndex == MarsMemorialChoreography.BlockContact ? 0
                : blockIndex == MarsMemorialChoreography.BlockPortrait ? 1
                : blockIndex == MarsMemorialChoreography.BlockImage ? 2 : 3;
            for (var i = 0; i < regions.Length; i++)
            {
                if (regions[i] != null)
                    regions[i].gameObject.SetActive(i == active);
            }

            var m = MarsMemorialChoreography.MechanismsAt(timeSeconds);
            for (var i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null)
                    wheels[i].localRotation = wheelRest[i] * Quaternion.Euler(m.WheelDegrees, 0f, 0f);
            }

            if (mast != null) mast.localRotation = mastRest * Quaternion.Euler(0f, m.MastDegrees, 0f);
            if (gimbal != null) gimbal.localRotation = gimbalRest * Quaternion.Euler(0f, m.GimbalDegrees, 0f);
            if (rimLight != null)
            {
                var rim = MarsMemorialChoreography.RimLight(timeSeconds);
                rimLight.enabled = rim > 0.001f;
                rimLight.intensity = 2.2f * rim;
            }

            RefreshLabels(timeSeconds);
        }

        void CaptureRest()
        {
            if (restCaptured)
                return;

            restCaptured = true;
            wheelRest = new Quaternion[wheels.Length];
            for (var i = 0; i < wheels.Length; i++)
                wheelRest[i] = wheels[i] != null ? wheels[i].localRotation : Quaternion.identity;
            mastRest = mast != null ? mast.localRotation : Quaternion.identity;
            gimbalRest = gimbal != null ? gimbal.localRotation : Quaternion.identity;
        }

        void RefreshLabels(float t)
        {
            visible.Clear();
            var mask = 0L;
            for (var i = 0; i < labels.Count; i++)
            {
                if (labels[i].IsVisibleAt(t))
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
            const string owner = nameof(MarsMemorialDirector);
            void Need(object value, string field)
            {
                if (value == null || (value is UnityEngine.Object o && o == null))
                    ContractValidation.Report(errors, owner, $"{field} não atribuído.");
            }

            if (regions == null || regions.Length != RegionCount || Array.Exists(regions, r => r == null))
                ContractValidation.Report(errors, owner, "são quatro regiões (Marte, retrato, imagem, díptico).");
            if (wheels == null || wheels.Length < 6 || Array.Exists(wheels, w => w == null))
                ContractValidation.Report(errors, owner, "faltam as seis rodas da Opportunity.");
            Need(mast, nameof(mast));
            Need(gimbal, nameof(gimbal));
            Need(memoryFrame, nameof(memoryFrame));
            Need(fallbackTexture, nameof(fallbackTexture));
            Need(rimLight, nameof(rimLight));
            foreach (var label in labels)
            {
                ContractValidation.RequireId(errors, owner, "labels[].textKey", label.TextKey);
                if (label.StartSeconds < 0f || label.EndSeconds <= label.StartSeconds || label.EndSeconds > MarsMemorialChoreography.TotalSeconds + 1e-3f)
                    ContractValidation.Report(errors, owner, $"letreiro '{label.TextKey}' fora de 0–{MarsMemorialChoreography.TotalSeconds} s.");
            }

            return errors;
        }
    }
}
