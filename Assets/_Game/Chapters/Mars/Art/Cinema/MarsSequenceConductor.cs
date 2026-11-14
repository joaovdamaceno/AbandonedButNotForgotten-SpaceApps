using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Liga o fluxo do capítulo à edição (MAR-018 → MAR-009): ao entrar numa etapa cinemática do catálogo, ativa a região 3D da etapa
    /// (e desativa as outras cinemáticas), aplica a exposição, toca a sequência e devolve o término ao <see cref="MarsChapterFlow"/>.
    /// Em etapa de gameplay, reinício ou desligamento, cancela a sequência em curso, devolve a câmera e restaura a exposição original.
    /// Uma sequência nunca fica sem dono: sequência ausente/inválida vira salto com <see cref="PlaybackFailed"/>, e o capítulo não trava.
    /// </summary>
    public sealed class MarsSequenceConductor : MonoBehaviour
    {
        [SerializeField] MarsChapterSequenceCatalog catalog;
        [SerializeField] MarsLightingBindings lighting;
        [SerializeField] MarsCameraAnchors anchors;

        readonly Dictionary<string, List<GameObject>> regions = new Dictionary<string, List<GameObject>>();
        MarsChapterFlow flow;
        SequenceDirector sequenceDirector;
        CameraDirector cameraDirector;
        Light sun;
        string activeStepId;
        string activeRegionId;
        bool lightingSaved;
        float savedIntensity;
        Color savedSunColor;
        Quaternion savedSunRotation;
        Color savedAmbient;
        Color savedBackground;
        CameraClearFlags savedClearFlags;

        public MarsChapterSequenceCatalog Catalog => catalog;
        public MarsLightingBindings Lighting => lighting;
        public MarsCameraAnchors Anchors => anchors;
        public string ActiveStepId => activeStepId;
        public string ActiveRegionId => activeRegionId;

        /// <summary>Etapa cuja sequência não pôde tocar (o capítulo avança mesmo assim, como salto).</summary>
        public event Action<string, string> PlaybackFailed;

        public void Initialize(MarsChapterFlow chapterFlow, SequenceDirector sequence, CameraDirector camera, Light sunLight)
        {
            Unsubscribe();
            flow = chapterFlow;
            sequenceDirector = sequence;
            cameraDirector = camera;
            sun = sunLight;
            if (anchors != null)
                anchors.Initialize(cameraDirector);
            if (flow != null)
                flow.StepStarted += OnStepStarted;
            if (sequenceDirector != null)
                sequenceDirector.Finished += OnFinished;
        }

        void OnDisable()
        {
            Unsubscribe();
            ReleaseCinematic();
        }

        void Unsubscribe()
        {
            if (flow != null)
                flow.StepStarted -= OnStepStarted;
            if (sequenceDirector != null)
                sequenceDirector.Finished -= OnFinished;
        }

        /// <summary>Registra um objeto da cena como parte da região (um palco, um ambiente). Várias por região.</summary>
        public void RegisterRegion(string regionId, GameObject root)
        {
            if (root == null || !ContractValidation.IsValidId(regionId))
                throw new ArgumentException($"Região inválida: '{regionId}'.");

            if (!regions.TryGetValue(regionId, out var list))
                regions[regionId] = list = new List<GameObject>();
            if (!list.Contains(root))
                list.Add(root);
        }

        public bool HasRegion(string regionId) => regions.ContainsKey(regionId);

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            if (!binding.IsCinematic)
            {
                ReleaseCinematic();
                return;
            }

            var entry = catalog != null ? catalog.Find(binding.StepId) : null;
            if (entry == null || entry.Sequence == null)
            {
                Fail(binding.StepId, "sem sequência no catálogo.");
                return;
            }

            ActivateRegion(entry.RegionId);
            ApplyLighting(entry.RegionId);
            activeStepId = binding.StepId;
            try
            {
                sequenceDirector.Play(entry.Sequence);
            }
            catch (ArgumentException exception)
            {
                Fail(binding.StepId, exception.Message);
            }
        }

        void Fail(string stepId, string reason)
        {
            PlaybackFailed?.Invoke(stepId, reason);
            var binding = flow != null ? flow.CurrentBinding : null;
            if (binding != null && binding.IsCinematic)
                flow.NotifySequenceFinished(new SequenceResult(binding.SequenceId, SequenceExitReason.Skipped, 0f));
        }

        void OnFinished(SequenceResult result)
        {
            if (flow == null || result.Reason == SequenceExitReason.Canceled)
                return;

            // O término vai ao fluxo, que entra na próxima etapa de forma síncrona. Se nenhuma sequência assumiu (etapa de gameplay ou fim do
            // capítulo), a exposição e a câmera voltam ao que eram.
            var finished = activeStepId;
            flow.NotifySequenceFinished(result);
            if (activeStepId == finished)
                ReleaseCinematic();
        }

        void ActivateRegion(string regionId)
        {
            activeRegionId = regionId;
            foreach (var pair in regions)
            {
                var on = pair.Key == regionId;
                foreach (var go in pair.Value)
                {
                    if (go != null)
                        go.SetActive(on);
                }
            }
        }

        void ApplyLighting(string regionId)
        {
            if (lighting == null || !lighting.TryGet(regionId, out var profile))
                return;

            SaveLighting();
            if (sun != null)
            {
                sun.intensity = profile.sunIntensity;
                sun.color = profile.sunColor;
                sun.transform.rotation = Quaternion.Euler(profile.sunEulerDegrees);
            }

            RenderSettings.ambientLight = profile.ambientColor;
            var camera = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = profile.backgroundColor;
            }
        }

        void SaveLighting()
        {
            if (lightingSaved)
                return;

            lightingSaved = true;
            if (sun != null)
            {
                savedIntensity = sun.intensity;
                savedSunColor = sun.color;
                savedSunRotation = sun.transform.rotation;
            }

            savedAmbient = RenderSettings.ambientLight;
            var camera = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (camera != null)
            {
                savedBackground = camera.backgroundColor;
                savedClearFlags = camera.clearFlags;
            }
        }

        /// <summary>Cancela a sequência em curso, devolve a câmera, reabilita as regiões e restaura a exposição original.</summary>
        public void ReleaseCinematic()
        {
            if (sequenceDirector != null && sequenceDirector.IsPlaying)
                sequenceDirector.Cancel();

            activeStepId = null;
            activeRegionId = null;
            var camera = cameraDirector != null ? cameraDirector.TargetCamera : null;
            if (camera != null)
                camera.rect = new Rect(0f, 0f, 1f, 1f);

            if (!lightingSaved)
                return;

            lightingSaved = false;
            if (sun != null)
            {
                sun.intensity = savedIntensity;
                sun.color = savedSunColor;
                sun.transform.rotation = savedSunRotation;
            }

            RenderSettings.ambientLight = savedAmbient;
            if (camera != null)
            {
                camera.backgroundColor = savedBackground;
                camera.clearFlags = savedClearFlags;
            }
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsSequenceConductor);
            if (catalog == null) ContractValidation.Report(errors, owner, "catalog não atribuído.");
            else errors.AddRange(catalog.Validate());
            if (lighting == null) ContractValidation.Report(errors, owner, "lighting não atribuído.");
            else
            {
                var needed = new HashSet<string>();
                if (catalog != null)
                    foreach (var e in catalog.Entries)
                        if (e != null)
                            needed.Add(e.RegionId);
                errors.AddRange(lighting.Validate(needed));
            }

            if (anchors == null) ContractValidation.Report(errors, owner, "anchors não atribuído.");
            else errors.AddRange(anchors.ValidateSetup());
            return errors;
        }
    }
}
