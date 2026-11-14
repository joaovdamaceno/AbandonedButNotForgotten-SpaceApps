using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// M22: Spirit em cima (cartões de imagem oficiais, palcos pequenos) e Opportunity quieta embaixo; no fim o Spirit escurece e a
    /// Opportunity preenche a tela. Mesmo relógio do <see cref="SequenceDirector"/>; a câmera principal (Opportunity) é do
    /// <see cref="CameraDirector"/>, a do Spirit é secundária e dirigida pelos planos de <c>cardShots</c>. Duas renderizações até a
    /// metade inferior ocupar a tela, uma depois; o palco do Spirit é descartado ao fim.
    /// </summary>
    public sealed class SpiritInterludeDirector : MonoBehaviour
    {
        public const string OpportunityRigId = "mars.spirit.opportunity";

        [SerializeField] SpiritInterludeData data;
        [SerializeField] Transform spiritStage;
        [SerializeField] Transform opportunityStage;
        [SerializeField] Camera spiritCamera;
        [SerializeField] SequenceDefinition cardShots;
        [SerializeField] Transform silicaCover;
        [SerializeField] Transform odometerBar;
        [SerializeField] Renderer darkVeil;
        [SerializeField] Transform spiritRover;

        readonly List<MarsLandingLabel> visible = new List<MarsLandingLabel>();
        MaterialPropertyBlock veilBlock;
        SequenceDirector sequenceDirector;
        CameraDirector cameraDirector;
        long visibleMask;
        Vector3 coverStart;
        Vector3 barStart;
        bool captured;
        bool released;

        public SpiritInterludeData Data => data;
        public Camera SpiritCamera => spiritCamera;
        public Transform SpiritStage => spiritStage;
        public Transform SilicaCover => silicaCover;
        public Transform OdometerBar => odometerBar;
        public Transform SpiritRover => spiritRover;
        public Renderer DarkVeil => darkVeil;
        public IReadOnlyList<MarsLandingLabel> VisibleLabels => visible;
        public float OpportunityHeight { get; private set; } = 1f;
        public float TimeSeconds { get; private set; } = -1f;

        public event Action<IReadOnlyList<MarsLandingLabel>> LabelsChanged;

        public int RenderedCameraCount
        {
            get
            {
                var count = 0;
                var main = cameraDirector != null ? cameraDirector.TargetCamera : null;
                if (main != null && main.enabled && main.gameObject.activeInHierarchy) count++;
                if (spiritCamera != null && spiritCamera.enabled && spiritCamera.gameObject.activeInHierarchy) count++;
                return count;
            }
        }

        public void Initialize(SequenceDirector sequence, CameraDirector camera)
        {
            if (sequenceDirector != null)
                sequenceDirector.Started -= OnStarted;

            sequenceDirector = sequence;
            cameraDirector = camera;
            if (cameraDirector != null && opportunityStage != null)
                cameraDirector.RegisterRig(OpportunityRigId, opportunityStage);
            if (sequenceDirector != null)
                sequenceDirector.Started += OnStarted;
        }

        void OnDisable()
        {
            if (sequenceDirector != null)
                sequenceDirector.Started -= OnStarted;
            if (cameraDirector != null)
                cameraDirector.UnregisterRig(OpportunityRigId);

            Release();
        }

        void OnStarted(string id)
        {
            if (data != null && id == data.SequenceId)
            {
                released = false;
                ApplyAt(0f);
            }
        }

        void LateUpdate()
        {
            if (sequenceDirector == null || data == null || !sequenceDirector.IsPlaying || sequenceDirector.Current == null || sequenceDirector.Current.SequenceId != data.SequenceId)
                return;

            ApplyAt(sequenceDirector.TimeSeconds);
        }

        public void ApplyAt(float timeSeconds)
        {
            if (data == null)
                return;

            if (!captured && silicaCover != null && odometerBar != null)
            {
                coverStart = silicaCover.localPosition;
                barStart = odometerBar.localPosition;
                captured = true;
            }

            timeSeconds = Mathf.Clamp(timeSeconds, 0f, SpiritInterludeChoreography.TotalSeconds);
            TimeSeconds = timeSeconds;
            var done = timeSeconds >= SpiritInterludeChoreography.TotalSeconds - 1e-4f;
            if (done)
            {
                DiscardSpirit();
            }
            else
            {
                released = false;
                if (spiritStage != null)
                    spiritStage.gameObject.SetActive(true);
            }

            if (silicaCover != null)
                silicaCover.localPosition = coverStart + new Vector3(8.4f * SpiritInterludeChoreography.SilicaReveal(timeSeconds), 0f, 0f);
            if (odometerBar != null)
            {
                var length = Mathf.Max(0.001f, 8f * SpiritInterludeChoreography.OdometerAt(timeSeconds) / SpiritInterludeChoreography.OdometerKilometers);
                odometerBar.localScale = new Vector3(length, odometerBar.localScale.y, odometerBar.localScale.z);
                odometerBar.localPosition = barStart + new Vector3(length * 0.5f - 0.0005f, 0f, 0f);
            }

            if (darkVeil != null)
            {
                if (veilBlock == null)
                    veilBlock = new MaterialPropertyBlock();
                darkVeil.GetPropertyBlock(veilBlock);
                veilBlock.SetColor("_BaseColor", new Color(0f, 0f, 0.01f, 0.92f * SpiritInterludeChoreography.SpiritDarkness(timeSeconds)));
                darkVeil.SetPropertyBlock(veilBlock);
            }

            ApplyViewports(timeSeconds);
            ApplySpiritCamera(timeSeconds);
            RefreshLabels(timeSeconds);
        }

        void ApplyViewports(float t)
        {
            OpportunityHeight = SpiritInterludeChoreography.OpportunityHeight(t);
            var main = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (main != null)
                main.rect = new Rect(0f, 0f, 1f, OpportunityHeight);

            if (spiritCamera == null)
                return;

            var height = 1f - OpportunityHeight;
            spiritCamera.rect = new Rect(0f, OpportunityHeight, 1f, Mathf.Max(0.0001f, height));
            spiritCamera.enabled = height > 0.0005f && !released;
        }

        void ApplySpiritCamera(float t)
        {
            if (spiritCamera == null || !spiritCamera.enabled || cardShots == null || spiritStage == null)
                return;

            var start = 0f;
            foreach (var shot in cardShots.Shots)
            {
                if (t <= start + shot.DurationSeconds + 1e-4f)
                {
                    CameraDirector.SampleKeyframes(shot, Mathf.Clamp(t - start, 0f, shot.DurationSeconds), out var position, out var rotation, out var fov);
                    spiritCamera.transform.SetPositionAndRotation(spiritStage.TransformPoint(position), spiritStage.rotation * rotation);
                    spiritCamera.fieldOfView = fov;
                    var main = cameraDirector != null ? cameraDirector.TargetCamera : null;
                    if (main != null)
                    {
                        spiritCamera.clearFlags = main.clearFlags;
                        spiritCamera.backgroundColor = main.backgroundColor;
                    }

                    return;
                }

                start += shot.DurationSeconds;
            }
        }

        void DiscardSpirit()
        {
            released = true;
            if (spiritCamera != null)
                spiritCamera.enabled = false;
            if (spiritStage != null)
                spiritStage.gameObject.SetActive(false);
        }

        /// <summary>Devolve a tela inteira à câmera principal e descarta o palco do Spirit.</summary>
        public void Release()
        {
            DiscardSpirit();
            var main = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (main != null)
                main.rect = new Rect(0f, 0f, 1f, 1f);
            OpportunityHeight = 1f;
        }

        void RefreshLabels(float t)
        {
            // Sem alocação por quadro: o conjunto visível é comparado por máscara de bits (AGENTS.md).
            visible.Clear();
            var mask = 0L;
            var all = data.Labels;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i].IsVisibleAt(t))
                {
                    visible.Add(all[i]);
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
            const string owner = nameof(SpiritInterludeDirector);
            void Need(object value, string field)
            {
                if (value == null || (value is UnityEngine.Object o && o == null))
                    ContractValidation.Report(errors, owner, $"{field} não atribuído.");
            }

            Need(data, nameof(data));
            Need(spiritStage, nameof(spiritStage));
            Need(opportunityStage, nameof(opportunityStage));
            Need(spiritCamera, nameof(spiritCamera));
            Need(cardShots, nameof(cardShots));
            Need(silicaCover, nameof(silicaCover));
            Need(odometerBar, nameof(odometerBar));
            Need(darkVeil, nameof(darkVeil));
            Need(spiritRover, nameof(spiritRover));
            if (data != null)
                errors.AddRange(data.Validate());
            if (cardShots != null && Mathf.Abs(cardShots.TotalDurationSeconds - SpiritInterludeChoreography.TotalSeconds) > 1e-3f)
                ContractValidation.Report(errors, owner, "os planos dos cartões devem somar 45 s.");
            return errors;
        }
    }
}
