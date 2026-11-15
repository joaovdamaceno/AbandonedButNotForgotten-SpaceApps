using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
namespace Abandoned.SpaceApps.Opportunity
{
    [DefaultExecutionOrder(-40)]
    public sealed class ChapterDirector : MonoBehaviour
    {
        const float InteractHoldSeconds=.6f;
        /// <summary>Tempo para ler as leituras da McKittrick antes da descoberta.</summary>
        const float ResultHoldSeconds=5f;
        float interactHold;
        public ChapterData data;
        public RoverController rover;
        public CameraDirector cameras;
        public MarsEnvironment environment;
        public RockAbrasionMinigame science;
        public MissionHUD hud;
        public MissionAudio sound;
        public MissionJourney journey;
        public FieldInvestigation investigation;
        public InteractionDirector interactions;
        public TutorialController tutorial;
        public bool readyForLaunch { get; private set; }
        public float SessionSeconds { get; private set; }
        public NarrativeDirector film;
        public MechanicalFeedback feedback;public StormPresentation weather;public SplitLanding split;
        public Transform spawn2004, parking2004, spawn2018, scienceTarget, observationTarget, shelterTarget;
        public Transform lander, airbagRoot, landerPetals;
        public Transform[] petals, montagePoses;
        public ParticleSystem landingDust;
        public ChapterProgress Progress { get; } = new ChapterProgress();
        public ChapterStage Stage => Progress.Stage;
        public float Elapsed { get; private set; }
        /// <summary>Segundos a mais na queda das duas sondas (até o impacto dos airbags, aos 7,5 s do filme do pouso).</summary>
        public const float LandingFallExtraSeconds = 6f;
        const float LandingFallEndSeconds = 7.5f;
        /// <summary>Tempo do filme da etapa: no pouso, a queda é desacelerada para durar mais; nas outras etapas, igual a <see cref="Elapsed"/>.</summary>
        public float FilmTime => Stage != ChapterStage.Landing ? Elapsed
            : Elapsed < LandingFallEndSeconds + LandingFallExtraSeconds ? Elapsed * LandingFallEndSeconds / (LandingFallEndSeconds + LandingFallExtraSeconds)
            : Elapsed - LandingFallExtraSeconds;
        public bool Paused { get; private set; }
        public Vector3 FinalPosition {get; private set;}
        public float FinalYaw {get; private set;}
        public Vector3 HardwarePosition{get;private set;}public float HardwareYaw{get;private set;}
        public bool PhotoTaken => journey.photography.Frames.Count > 0;
        public float Energy => Stage==ChapterStage.CommunicationLost?film.Power:Stage>ChapterStage.CommunicationLost?0:Stage==ChapterStage.DustStorm?Mathf.Lerp(.9f,.3f,environment.Storm):1;
        public Transform ActiveTarget => journey.CurrentTarget;
        public float TargetDistance => Vector3.Distance(rover.transform.position, ActiveTarget.position);
        public bool Cinematic => Stage != ChapterStage.HeatShieldExploration && Stage != ChapterStage.ChapterEnd && Stage != ChapterStage.Exploration && Stage != ChapterStage.FinalExploration && Stage != ChapterStage.DustStorm && Stage != ChapterStage.RockAnalysis;
        public bool CanSkip => Cinematic && Elapsed > 1;
        Coroutine parking;
        void Start()
        {
            Application.targetFrameRate = 60; Progress.ContinueToNextChapter=!string.IsNullOrWhiteSpace(data.nextChapterScene); Progress.Changed += Enter;
            investigation.ResetEvidence();journey.ResetJourney();ResetSession();Enter(Progress.Stage);
            readyForLaunch=true;
        }
        /// <summary>Pedido para recomeçar o capítulo inteiro (desde a abertura); quem carregou a cena decide como.</summary>
        public event System.Action RestartRequested;
        public void RestartChapter()
        {
            if(RestartRequested!=null){Pause(false);if(parking!=null)StopCoroutine(parking);investigation.Close();science.End();journey.photography.Close();RestartRequested();return;} Pause(false);SessionSeconds=0; if (parking != null) StopCoroutine(parking); investigation.ResetEvidence();science.End(); science.surface.ResetSurface();rover.ResetDistance(); journey.ResetJourney(); ResetSession(); Progress.Reset(); }
        /// <summary>Pedido para sair do capítulo (pausa ou tela final); quem carregou a cena decide para onde voltar.</summary>
        public event System.Action ExitRequested;
        public void Leave()
        { Pause(false); if (parking != null) StopCoroutine(parking); investigation.Close();science.End();journey.photography.Close();ExitRequested?.Invoke(); }
        void ResetSession(){SessionSeconds=0;if(tutorial)tutorial.ResetSession();}
        void Enter(ChapterStage stage)
        {
            Elapsed = 0; rover.Power = 1;
            rover.ReleaseFilm();
            rover.Controllable = stage == ChapterStage.HeatShieldExploration || stage == ChapterStage.Exploration || stage == ChapterStage.FinalExploration || stage == ChapterStage.DustStorm;
            Shader.SetGlobalFloat("_Memory", 0);
            if (stage == ChapterStage.Intro || stage == ChapterStage.ChapterEnd)
            {
                environment.Region(false, true); rover.gameObject.SetActive(false); lander.gameObject.SetActive(false);
                if (stage == ChapterStage.ChapterEnd) {environment.space.SetActive(false);environment.Memorial=true;sound.QuietEnvironment=true;}
            }
            if (stage == ChapterStage.Landing)
            {
                environment.Region(false,false); environment.SetStorm(0,true); lander.gameObject.SetActive(true);
                airbagRoot.gameObject.SetActive(true); airbagRoot.localScale=Vector3.one; rover.gameObject.SetActive(false);
                for(int i=0;i<petals.Length;i++)petals[i].localRotation=Quaternion.Euler(0,i*120,0);
                if(split)split.Begin();
            }
            else if(split&&split.Active)split.End();
            if(stage==ChapterStage.Activation)
            {
                environment.Region(false,false); lander.SetPositionAndRotation(spawn2004.position,Quaternion.Euler(0,35,0));
                airbagRoot.localScale=Vector3.one;lander.GetComponent<AirbagLanderRig>()?.Pose(1,1); lander.gameObject.SetActive(true); rover.gameObject.SetActive(true);
                rover.Teleport(spawn2004.position+Vector3.up*.10f,spawn2004.eulerAngles.y); rover.RegionCenter=new Vector3(0,0,30);
            }
            if(stage==ChapterStage.Exploration){environment.Region(false,false);rover.gameObject.SetActive(true);}
            if(stage==ChapterStage.RockAnalysis){if(parking!=null)StopCoroutine(parking);parking=StartCoroutine(ParkAndDeploy());}
            if(stage==ChapterStage.MeteoriteDiscovery){HardwarePosition=rover.transform.position;HardwareYaw=rover.transform.eulerAngles.y;}
            if(stage==ChapterStage.Discovery){science.End();sound.Confirm();}
            if(stage==ChapterStage.HeatShieldExploration)
            {environment.Region(false,false);environment.Hardware(true);environment.SetStorm(0,true);lander.gameObject.SetActive(false);rover.gameObject.SetActive(true);rover.Teleport(investigation.heatSpawn.position,investigation.heatSpawn.eulerAngles.y);rover.RegionCenter=new Vector3(2200,0,0);}
            if(stage==ChapterStage.FinalExploration)
            {
                journey.BeginFinal();environment.Region(true,false);environment.SetStorm(0,true);lander.gameObject.SetActive(false);
                rover.gameObject.SetActive(true);rover.Teleport(spawn2018.position,spawn2018.eulerAngles.y);rover.RegionCenter=new Vector3(1100,0,0);
            }
            if(stage==ChapterStage.DustStorm)sound.Warn();
            if(stage==ChapterStage.CommunicationLost){FinalPosition=rover.transform.position;FinalYaw=rover.transform.eulerAngles.y;}
            if(stage==ChapterStage.Farewell){rover.Power=0;environment.SetStorm(.94f,true);}
            if(stage==ChapterStage.ChapterEnd)PlayerPrefs.Save();
            sound.Enter(stage);film.Enter(stage);cameras.Enter(stage);SetCursor();hud.RefreshText();hud.ObjectiveChanged();
            if(stage==ChapterStage.FinalExploration)film.SpeakSequence("v14.sun.reason","v15.storm.again");
            Debug.Log("[Opportunity] Stage: "+stage);
        }
        public void BeginRockAnalysis()
        {
            var action=interactions?interactions.Current:default;
            if(action.Id!="rat"||!action.Available)return;
            Progress.ReachRock();Progress.Advance();
        }
        IEnumerator ParkAndDeploy()
        {
            float time=0;
            while(Vector3.Distance(rover.transform.position,parking2004.position)>.12f&&time<3.5f)
            {rover.AssistParking(parking2004.position,parking2004.rotation,Time.deltaTime);time+=Time.deltaTime;yield return null;}
            science.Begin();
        }
        void Update()
        {
            if(GameInput.Press(Key.Escape))
            {
                if(investigation.Active){investigation.Close();}
                else if(journey.photography.Active){journey.photography.Close();rover.Controllable=true;}
                else if(journey.ChoosingMemory)journey.CancelTransmission();
                else if(Stage!=ChapterStage.ChapterEnd)Pause(!Paused);
            }
            if(Paused)return;
            Elapsed+=Time.deltaTime;
            if(Stage!=ChapterStage.ChapterEnd)SessionSeconds+=Time.unscaledDeltaTime;
            film.Evaluate(FilmTime);
            // Interações começam segurando E (sem clique ou mira).
            interactHold=GameInput.ActionHeld?interactHold+Time.deltaTime:0;
            if(interactions&&interactHold>=InteractHoldSeconds){interactHold=float.NegativeInfinity;interactions.TryExecute();}
            if(CanSkip&&GameInput.Press(Key.Space)){CompleteCinematic();return;}
            if((Stage==ChapterStage.Exploration||journey.FinalAct)&&!investigation.Active&&!investigation.NeedSun&&!journey.Transmitting&&!journey.ChoosingMemory&&GameInput.Press(Key.F))journey.photography.Toggle();
            if(journey.FinalAct&&!journey.photography.Active&&!journey.Transmitting&&!journey.ChoosingMemory&&GameInput.Press(Key.R))journey.ToggleUplinkRoute();
            switch(Stage)
            {
                case ChapterStage.Intro:if(Elapsed>=film.SequenceDuration)CompleteCinematic();break;
                case ChapterStage.Landing:if(FilmTime>=film.SequenceDuration)CompleteCinematic();break;
                case ChapterStage.Activation:
                    lander.GetComponent<AirbagLanderRig>()?.Pose(1,1);
                    if(Elapsed>=film.SequenceDuration)CompleteCinematic();break;
                case ChapterStage.Exploration:
                    break;
                case ChapterStage.RockAnalysis:
                    cameras.Shake=science.Working?Mathf.Lerp(.035f,.11f,science.RemovalFeedback):0;
                    if(science.Phase==AbrasionPhase.Complete&&science.Active&&science.PhaseSeconds>=ResultHoldSeconds){cameras.Shake=0;Progress.CompleteScience();Progress.Advance();}break;
                case ChapterStage.Discovery:
                    science.surface.SetInspection(Elapsed<6?1:2);
                    if(Elapsed>=film.SequenceDuration)CompleteCinematic();break;
                case ChapterStage.HeatShieldArrival:case ChapterStage.MeteoriteDiscovery:if(Elapsed>=film.SequenceDuration)CompleteCinematic();break;
                case ChapterStage.HeatShieldExploration:if(investigation.MeteoriteIdentified&&!investigation.Active){Progress.CompleteHardware();Progress.Advance();}break;
                case ChapterStage.TimeSkip:if(Elapsed>=film.SequenceDuration)CompleteCinematic();break;
                case ChapterStage.FinalExploration:environment.SetStorm(.02f);if(journey.PanoramaCount>0)Progress.Advance();break;
                case ChapterStage.DustStorm:
                    environment.SetStorm(Mathf.Lerp(.20f,.84f,Mathf.SmoothStep(0,1,Mathf.Clamp01(Elapsed/38))));rover.Power=Mathf.Max(.5f,Energy);
                    // Transmission keeps the player's heading; only the instrument/camera reacts.
                    if(journey.TransmissionComplete)Progress.Advance();break;
                case ChapterStage.CommunicationLost:rover.Power=film.Power;if(Elapsed>=film.SequenceDuration)CompleteCinematic();break;
                case ChapterStage.Farewell:if(Elapsed>=film.SequenceDuration)CompleteCinematic();break;
                case ChapterStage.Transition:
                    if(Elapsed>6.8f&&environment.OnSurface)environment.Region(true,true);
                    if(Elapsed>=film.SequenceDuration)CompleteCinematic();break;
            }
            sound.Levels(environment.Storm,rover.Speed,Stage==ChapterStage.CommunicationLost?film.Power<.1f:Stage>ChapterStage.CommunicationLost);
            if(GameInput.Press(Key.Tab)&&!journey.photography.Active)hud.ToggleJournal();
        }
        public void CompleteCinematic(){if(film.HasSequence)film.Evaluate(film.SequenceDuration);PlayerPrefs.SetInt("ll.seen."+Stage,1);cameras.Shake=0;Progress.Advance();}
        public void SeekFilm(float time){Elapsed=time;film.Evaluate(time);}
        public void Pause(bool pause){Paused=pause;Time.timeScale=pause?0:1;cameras.Paused=pause;sound.Pause(pause);hud.ShowPause(pause);SetCursor();}
        public void SetCursor()
        {
            bool control=!Paused&&!journey.ChoosingMemory&&!journey.Transmitting&&(rover.Controllable||investigation.Active||journey.photography.Active||Stage==ChapterStage.RockAnalysis);
            Cursor.lockState=control?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=Paused||(!control&&!Cinematic);
        }
        public void Preview(ChapterStage stage)
        {
            Pause(false);investigation.Close();if(parking!=null)StopCoroutine(parking);science.End();journey.photography.Close();
            if(stage==ChapterStage.MeteoriteDiscovery){Progress.SetForPreview(ChapterStage.HeatShieldExploration);rover.Teleport(investigation.meteoriteSite.position,0);}
            else if(stage>=ChapterStage.FinalExploration&&stage<=ChapterStage.Farewell){Progress.SetForPreview(ChapterStage.FinalExploration);if(stage>=ChapterStage.CommunicationLost)environment.SetStorm(.82f,true);}
            else if(stage>=ChapterStage.Activation&&stage<=ChapterStage.TimeSkip)Progress.SetForPreview(ChapterStage.Activation);
            if(stage==ChapterStage.RockAnalysis||stage==ChapterStage.Discovery){rover.Teleport(parking2004.position,0);journey.CompleteTraverseForPreview();}
            Progress.SetForPreview(stage);
        }
        public void NextChapter(){if(!string.IsNullOrEmpty(data.nextChapterScene)&&Application.CanStreamedLevelBeLoaded(data.nextChapterScene))SceneManager.LoadScene(data.nextChapterScene);}
        void OnDestroy(){Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}




