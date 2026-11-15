using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Pouso paralelo de M04–M06: Spirit à esquerda, Opportunity à direita (com uma linha dividindo), depois Opportunity cresce e fica sozinha. Lê o MESMO relógio
    /// do <see cref="SequenceDirector"/> (tempo local da sequência + deslocamento), então pausa, salto e reinício movem câmera,
    /// atores e letreiros juntos. A câmera principal continua sendo do <see cref="CameraDirector"/>; esta classe só dirige a
    /// câmera secundária do Spirit (os mesmos planos, outro palco), os retângulos de viewport e a visibilidade dos letreiros.
    /// Orçamento Web: duas renderizações durante M04 e M05 (até a metade da direita ocupar a tela) e uma depois; o palco do
    /// Spirit é desativado ao fim do split (descarte).
    /// </summary>
    public sealed class MarsDualLandingDirector : MonoBehaviour
    {
        public const string OpportunityRigId = "mars.landing.opportunity";

        [SerializeField] MarsDualLandingData data;
        [SerializeField] MerLandingAssembly spirit;
        [SerializeField] MerLandingAssembly opportunity;
        [SerializeField] Camera spiritCamera;
        [SerializeField] SequenceDefinition splitSequence;
        [SerializeField] SequenceDefinition eagleSequence;

        readonly List<MarsLandingLabel> visibleLabels = new List<MarsLandingLabel>();
        long visibleMask;
        SequenceDirector sequenceDirector;
        CameraDirector cameraDirector;
        float lastTime = -1f;
        bool released;

        public MarsDualLandingData Data => data;
        public MerLandingAssembly Spirit => spirit;
        public MerLandingAssembly Opportunity => opportunity;
        public Camera SpiritCamera => spiritCamera;
        public IReadOnlyList<MarsLandingLabel> VisibleLabels => visibleLabels;
        public float TimeSeconds => lastTime;

        /// <summary>Muda quando o conjunto de letreiros visíveis muda (UI mostra pelas chaves).</summary>
        public event Action<IReadOnlyList<MarsLandingLabel>> LabelsChanged;

        /// <summary>Quanto da largura da tela a metade da direita (Opportunity) ocupa: 0,5 no split, 1 depois de M05.</summary>
        public float OpportunityWidth { get; private set; } = 1f;

        /// <summary>Posição horizontal (0–1) da linha que divide as duas metades; só vale enquanto <see cref="IsSplitScreen"/>.</summary>
        public float DividerX => 1f - OpportunityWidth;

        /// <summary>Verdadeiro enquanto as duas metades aparecem lado a lado (a UI desenha a linha divisória).</summary>
        public bool IsSplitScreen => spiritCamera != null && spiritCamera.enabled;

        public int RenderedCameraCount
        {
            get
            {
                var count = 0;
                var main = cameraDirector != null ? cameraDirector.TargetCamera : null;
                if (main != null && main.enabled && main.gameObject.activeInHierarchy)
                    count++;
                if (spiritCamera != null && spiritCamera.enabled && spiritCamera.gameObject.activeInHierarchy)
                    count++;
                return count;
            }
        }

        public void Initialize(SequenceDirector sequence, CameraDirector camera)
        {
            Unsubscribe();
            sequenceDirector = sequence;
            cameraDirector = camera;
            if (cameraDirector != null && opportunity != null)
                cameraDirector.RegisterRig(OpportunityRigId, opportunity.transform);

            if (sequenceDirector != null)
            {
                sequenceDirector.Started += OnSequenceStarted;
                sequenceDirector.Finished += OnSequenceFinished;
            }
        }

        void OnDisable()
        {
            Unsubscribe();
            if (cameraDirector != null)
                cameraDirector.UnregisterRig(OpportunityRigId);

            Release();
        }

        void Unsubscribe()
        {
            if (sequenceDirector == null)
                return;

            sequenceDirector.Started -= OnSequenceStarted;
            sequenceDirector.Finished -= OnSequenceFinished;
        }

        void OnSequenceStarted(string sequenceId)
        {
            if (data == null || data.OffsetOf(sequenceId) < 0f)
                return;

            if (sequenceId == data.SplitSequenceId)
                released = false;

            ApplyAt(data.OffsetOf(sequenceId));
        }

        void OnSequenceFinished(SequenceResult result)
        {
            if (data == null || result.SequenceId != data.VerificationSequenceId)
                return;

            ApplyAt(data.TotalSeconds);
        }

        void LateUpdate()
        {
            if (sequenceDirector == null || data == null || !sequenceDirector.IsPlaying || sequenceDirector.Current == null)
                return;

            var offset = data.OffsetOf(sequenceDirector.Current.SequenceId);
            if (offset >= 0f)
                ApplyAt(offset + sequenceDirector.TimeSeconds);
        }

        /// <summary>Estado do pouso no instante global (segundos desde o início de M04): atores, câmera do Spirit, viewports e letreiros.</summary>
        public void ApplyAt(float timeSeconds)
        {
            if (data == null || opportunity == null)
                return;

            timeSeconds = Mathf.Clamp(timeSeconds, 0f, data.TotalSeconds);
            lastTime = timeSeconds;
            var splitEnd = data.SplitSeconds + data.EagleSeconds;

            if (timeSeconds < splitEnd)
            {
                released = false;
                if (spirit != null)
                {
                    spirit.gameObject.SetActive(true);
                    spirit.Apply(timeSeconds);
                }
            }
            else
            {
                DiscardSpirit();
            }

            opportunity.Apply(timeSeconds);
            ApplyViewports(timeSeconds);
            ApplySpiritCamera(timeSeconds);
            RefreshLabels(timeSeconds);
        }

        void ApplyViewports(float time)
        {
            var growth = MerLandingChoreography.Smooth((time - data.SplitSeconds) / data.EagleSeconds);
            OpportunityWidth = Mathf.Lerp(0.5f, 1f, growth);
            var main = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (main != null)
                main.rect = new Rect(1f - OpportunityWidth, 0f, OpportunityWidth, 1f);

            if (spiritCamera == null)
                return;

            var spiritWidth = 1f - OpportunityWidth;
            spiritCamera.rect = new Rect(0f, 0f, Mathf.Max(0.0001f, spiritWidth), 1f);
            spiritCamera.enabled = spiritWidth > 0.0005f && !released;
        }

        void ApplySpiritCamera(float time)
        {
            if (spiritCamera == null || spirit == null || !spiritCamera.enabled)
                return;

            SequenceDefinition sequence;
            float local;
            if (time < data.SplitSeconds)
            {
                sequence = splitSequence;
                local = time;
            }
            else
            {
                sequence = eagleSequence;
                local = time - data.SplitSeconds;
            }

            if (sequence == null || !TryFindShot(sequence, local, out var shot, out var shotLocal))
                return;

            CameraDirector.SampleKeyframes(shot, shotLocal, out var position, out var rotation, out var fov);
            var world = spirit.transform.TransformPoint(position);
            var ground = spirit.transform.position.y + MerLandingChoreography.Ground(MerLandingSite.Gusev, position.x, position.z) + shot.MinGroundClearanceMeters;
            if (world.y < ground)
                world.y = ground;

            var main = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (main != null)
            {
                spiritCamera.clearFlags = main.clearFlags;
                spiritCamera.backgroundColor = main.backgroundColor;
                spiritCamera.nearClipPlane = main.nearClipPlane;
                spiritCamera.farClipPlane = main.farClipPlane;
            }

            spiritCamera.transform.SetPositionAndRotation(world, spirit.transform.rotation * rotation);
            spiritCamera.fieldOfView = fov;
        }

        static bool TryFindShot(SequenceDefinition sequence, float local, out ShotDefinition shot, out float shotLocal)
        {
            var start = 0f;
            shot = null;
            shotLocal = 0f;
            foreach (var candidate in sequence.Shots)
            {
                if (local <= start + candidate.DurationSeconds + 1e-4f)
                {
                    shot = candidate;
                    shotLocal = Mathf.Clamp(local - start, 0f, candidate.DurationSeconds);
                    return true;
                }

                start += candidate.DurationSeconds;
            }

            return false;
        }

        void DiscardSpirit()
        {
            released = true;
            if (spiritCamera != null)
                spiritCamera.enabled = false;
            if (spirit != null)
                spirit.gameObject.SetActive(false);
        }

        /// <summary>Devolve a tela inteira à câmera principal e descarta o palco do Spirit (fim do pouso, desligamento ou cancelamento).</summary>
        public void Release()
        {
            DiscardSpirit();
            var main = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (main != null)
                main.rect = new Rect(0f, 0f, 1f, 1f);

            OpportunityWidth = 1f;
        }

        void RefreshLabels(float time)
        {
            // Sem alocação por quadro: o conjunto visível é comparado por máscara de bits (AGENTS.md).
            visibleLabels.Clear();
            var mask = 0L;
            var all = data.Labels;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i].IsVisibleAt(time))
                {
                    visibleLabels.Add(all[i]);
                    mask |= 1L << i;
                }
            }

            if (mask != visibleMask)
            {
                visibleMask = mask;
                LabelsChanged?.Invoke(visibleLabels);
            }
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsDualLandingDirector);
            if (data == null) ContractValidation.Report(errors, owner, "data não atribuído.");
            else errors.AddRange(data.Validate());
            if (spirit == null) ContractValidation.Report(errors, owner, "spirit não atribuído.");
            else errors.AddRange(spirit.ValidateSetup());
            if (opportunity == null) ContractValidation.Report(errors, owner, "opportunity não atribuído.");
            else errors.AddRange(opportunity.ValidateSetup());
            if (spiritCamera == null) ContractValidation.Report(errors, owner, "spiritCamera não atribuída.");
            if (splitSequence == null || eagleSequence == null) ContractValidation.Report(errors, owner, "sequências M04/M05 não atribuídas.");
            if (spirit != null && opportunity != null && spirit.Site == opportunity.Site)
                ContractValidation.Report(errors, owner, "Spirit e Opportunity precisam de locais diferentes.");
            return errors;
        }
    }
}
