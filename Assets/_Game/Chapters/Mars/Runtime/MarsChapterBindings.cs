using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Raiz da cena de Marte (MAR-009). A montagem de Editor serializa aqui TODAS as referências (nenhuma busca por nome); em execução, a R1
    /// chama <see cref="Initialize"/> com os serviços globais (diretor de capítulo, relógio ativo, entrada, interação, evidências) depois de
    /// carregar a cena. O componente liga o fluxo às atividades, ativa uma região por etapa (sem luzes, colisores ou câmeras duplicados),
    /// pousa o rover nas estações, aponta as âncoras de câmera para os sujeitos reais, entrega a memória escolhida ao memorial e avisa
    /// <see cref="ArchiveControlsRequested"/> depois do título. Pausa, reinício, dossiê e conclusão continuam globais (ChapterDirector).
    /// </summary>
    public sealed class MarsChapterBindings : MonoBehaviour
    {
        public const string RegionOrigin = "mars.origin";
        public const string RegionLanding = "mars.landing";
        public const string RegionEagle = "mars.eagle";
        public const string RegionShield = "mars.shield";
        public const string RegionVictoria = "mars.victoria";
        public const string RegionValley = "mars.valley";
        public const string RegionSpirit = "mars.spirit";
        public const string RegionMemorial = "mars.memorial";

        [SerializeField] MarsSceneBindings bindings;
        [Header("Serviços da cena")]
        [SerializeField] Camera sceneCamera;
        [SerializeField] CameraDirector cameraDirector;
        [SerializeField] SequenceDirector sequenceDirector;
        [SerializeField] Light sun;
        [Header("Fluxo e edição")]
        [SerializeField] MarsChapterFlow flow;
        [SerializeField] MarsSequenceConductor conductor;
        [SerializeField] MarsCameraAnchors anchors;
        [Header("Rover")]
        [SerializeField] MarsRoverController rover;
        [SerializeField] MarsTutorial tutorial;
        [SerializeField] Transform antennaGimbal;
        [Header("Atividades")]
        [SerializeField] BlueberriesActivity blueberries;
        [SerializeField] MarsRatActivity rat;
        [SerializeField] RatCavitySurface ratCavity;
        [SerializeField] MarsCompositionActivity composition;
        [SerializeField] ShieldSurveyActivity shieldSurvey;
        [SerializeField] MeteoriteActivity meteorite;
        [SerializeField] Storm2007Activity storm;
        [SerializeField] MarsFinalSurvey finalSurvey;
        [SerializeField] MarsTransmission transmission;
        [Header("Palcos")]
        [SerializeField] ShieldFieldAnchors shieldAnchors;
        [SerializeField] LateMarsStageAnchors victoriaAnchors;
        [SerializeField] LateMarsStageAnchors valleyAnchors;
        [SerializeField] MarsAtmosphereController victoriaAtmosphere;
        [SerializeField] MarsAtmosphereController valleyAtmosphere;
        [SerializeField] MarsOriginDirector originDirector;
        [SerializeField] MarsDualLandingDirector landingDirector;
        [SerializeField] SpiritInterludeDirector spiritDirector;
        [SerializeField] MarsMemorialDirector memorialDirector;
        [SerializeField, Tooltip("Estação inicial do rover em Eagle (piso provisório até a MAR-012).")] Transform eagleRoverStart;
        [SerializeField] bool eaglePlaceholder;
        [SerializeField] List<MarsRegionRoots> regions = new List<MarsRegionRoots>();

        bool wired;

        public MarsSceneBindings Bindings => bindings;
        public MarsChapterFlow Flow => flow;
        public MarsSequenceConductor Conductor => conductor;
        public MarsRoverController Rover => rover;
        public MarsTransmission Transmission => transmission;
        public MarsMemorialDirector Memorial => memorialDirector;
        public MarsDualLandingDirector Landing => landingDirector;
        public SpiritInterludeDirector Spirit => spiritDirector;
        public CameraDirector CameraDirector => cameraDirector;
        public SequenceDirector SequenceDirector => sequenceDirector;
        public Camera SceneCamera => sceneCamera;
        public Light Sun => sun;
        public bool IsWired => wired;

        /// <summary>O ambiente de Eagle ainda é um piso provisório (MAR-012 não integrada).</summary>
        public bool EaglePlaceholder => eaglePlaceholder;
        public string ActiveRegionId { get; private set; }
        public IReadOnlyList<MarsRegionRoots> Regions => regions;

        /// <summary>Depois do título final: a R1/UI expõe VOLTAR AO ARQUIVO / REPETIR CAPÍTULO (uma vez por execução).</summary>
        public event Action ArchiveControlsRequested;

        public void Initialize(IChapterDirector director, IActiveTimeSource time, InputRouter router, IInteractionService interactions,
            IEvidenceStore evidence, SettingsStore settings = null)
        {
            var errors = ValidateSetup();
            if (errors.Count > 0)
                throw new InvalidOperationException("Montagem de Marte inválida:" + ContractValidation.Describe(errors));

            Teardown();
            cameraDirector.Initialize(sceneCamera);
            sequenceDirector.Initialize(cameraDirector, router, time);
            flow.Initialize(director, time, bindings.Chapter, bindings.SubtitlesJson.text);

            // Primeiro as regiões do conductor, depois o próprio fluxo: este componente ouve a etapa ANTES do conductor, então os rigs
            // já estão nos sujeitos quando a sequência toca.
            foreach (var region in regions)
            {
                foreach (var root in region.roots)
                    conductor.RegisterRegion(region.regionId, root);
            }

            flow.StepStarted += OnStepStarted;
            conductor.Initialize(flow, sequenceDirector, cameraDirector, sun);

            rover.Initialize(router, time, cameraDirector, flow);
            tutorial.Initialize(rover, flow, interactions, time, settings);
            blueberries.Initialize(flow, interactions, time, rover.transform, evidence);
            rat.Initialize(flow, interactions, time, rover.transform);
            composition.Initialize(flow, interactions, time, ratCavity, evidence);
            shieldSurvey.Initialize(flow, interactions, time, shieldAnchors, rover.transform, evidence);
            meteorite.Initialize(flow, interactions, time, shieldAnchors, rover.transform, evidence);
            storm.Initialize(flow, interactions, time);
            victoriaAtmosphere.Initialize(flow, storm, time, sun);
            valleyAtmosphere.Initialize(flow, null, time, sun);
            finalSurvey.Initialize(flow, interactions, time, valleyAnchors.ToSurveyAnchors(), rover.transform, sceneCamera, evidence);
            transmission.Initialize(flow, interactions, time, router, evidence, antennaGimbal, valleyAnchors.EarthTarget);

            originDirector.Initialize(sequenceDirector, cameraDirector);
            landingDirector.Initialize(sequenceDirector, cameraDirector);
            spiritDirector.Initialize(sequenceDirector, cameraDirector);
            memorialDirector.Initialize(sequenceDirector, cameraDirector);
            anchors.Initialize(cameraDirector);
            memorialDirector.ControlsReady += OnControlsReady;

            wired = true;
            conductor.ActivateRegion(RegionOrigin);
            ActiveRegionId = RegionOrigin;
            rover.gameObject.SetActive(false);
        }

        void OnDisable() => Teardown();

        /// <summary>Solta todas as assinaturas e devolve a câmera e a exposição (descarregar a cena, reiniciar o aplicativo).</summary>
        public void Teardown()
        {
            if (!wired)
                return;

            wired = false;
            if (flow != null)
                flow.StepStarted -= OnStepStarted;
            if (memorialDirector != null)
                memorialDirector.ControlsReady -= OnControlsReady;
            if (conductor != null)
                conductor.ReleaseCinematic();
        }

        void OnControlsReady() => ArchiveControlsRequested?.Invoke();

        void OnStepStarted(MarsChapterFlow.StepBinding binding)
        {
            var step = binding.StepId;
            var region = bindings.RegionOf(step);
            if (step == "M01")
                ResetForRestart();

            ActiveRegionId = region;
            if (!binding.IsCinematic)
                conductor.ActivateRegion(region);

            var roverRegion = region == RegionEagle || region == RegionShield || region == RegionVictoria || region == RegionValley;
            if (rover.gameObject.activeSelf != roverRegion)
                rover.gameObject.SetActive(roverRegion);

            switch (step)
            {
                case "M07": PlaceRover(eagleRoverStart); break;
                case "M16": PlaceRover(shieldAnchors.ApproachStart); break;
                case "M21": PlaceRover(victoriaAnchors.RoverStart); break;
                case "M24": PlaceRover(valleyAnchors.RoverStart); break;
                case "M14": anchors.Bind(MarsCameraAnchors.RigIdFor(step), ratCavity.transform); break;
                case "M15": anchors.Bind(MarsCameraAnchors.RigIdFor(step), rover.transform); break;
                case "M20": anchors.Bind(MarsCameraAnchors.RigIdFor(step), shieldAnchors.MeteoriteMeasurePoint); break;
                case "M23":
                case "M27": anchors.Bind(MarsCameraAnchors.RigIdFor(step), valleyAnchors.RoverStart); break;
                case "M31": memorialDirector.SetMemory(transmission.SelectedRecord); break;
            }
        }

        void PlaceRover(Transform start)
        {
            if (start != null)
                rover.PlaceAt(start.position, start.rotation);
        }

        /// <summary>Volta ao início do capítulo: cancela a sequência, devolve câmera/exposição, esquece a memória e esconde o rover.</summary>
        void ResetForRestart()
        {
            conductor.ReleaseCinematic();
            memorialDirector.SetMemory(null);
            ActiveRegionId = RegionOrigin;
        }

        public List<string> ValidateSetup()
        {
            var errors = new List<string>();
            const string owner = nameof(MarsChapterBindings);
            var fields = new (UnityEngine.Object value, string name)[]
            {
                (bindings, nameof(bindings)), (sceneCamera, nameof(sceneCamera)), (cameraDirector, nameof(cameraDirector)), (sequenceDirector, nameof(sequenceDirector)),
                (sun, nameof(sun)), (flow, nameof(flow)), (conductor, nameof(conductor)), (anchors, nameof(anchors)), (rover, nameof(rover)), (tutorial, nameof(tutorial)),
                (antennaGimbal, nameof(antennaGimbal)), (blueberries, nameof(blueberries)), (rat, nameof(rat)), (ratCavity, nameof(ratCavity)), (composition, nameof(composition)),
                (shieldSurvey, nameof(shieldSurvey)), (meteorite, nameof(meteorite)), (storm, nameof(storm)), (finalSurvey, nameof(finalSurvey)), (transmission, nameof(transmission)),
                (shieldAnchors, nameof(shieldAnchors)), (victoriaAnchors, nameof(victoriaAnchors)), (valleyAnchors, nameof(valleyAnchors)),
                (victoriaAtmosphere, nameof(victoriaAtmosphere)), (valleyAtmosphere, nameof(valleyAtmosphere)), (originDirector, nameof(originDirector)),
                (landingDirector, nameof(landingDirector)), (spiritDirector, nameof(spiritDirector)), (memorialDirector, nameof(memorialDirector)), (eagleRoverStart, nameof(eagleRoverStart)),
            };
            foreach (var (value, name) in fields)
            {
                if (value == null)
                    ContractValidation.Report(errors, owner, $"{name} não atribuído.");
            }

            if (bindings != null)
                errors.AddRange(bindings.Validate());

            var needed = new HashSet<string> { RegionOrigin, RegionLanding, RegionEagle, RegionShield, RegionVictoria, RegionValley, RegionSpirit, RegionMemorial };
            var found = new HashSet<string>();
            foreach (var region in regions)
            {
                if (!found.Add(region.regionId))
                    ContractValidation.Report(errors, owner, $"região '{region.regionId}' duplicada.");
                if (region.roots == null || region.roots.Count == 0 || region.roots.Exists(r => r == null))
                    ContractValidation.Report(errors, owner, $"região '{region.regionId}' sem objetos válidos.");
            }

            foreach (var region in needed)
            {
                if (!found.Contains(region))
                    ContractValidation.Report(errors, owner, $"falta a região '{region}'.");
            }

            return errors;
        }
    }
}
