using UnityEngine;
using UnityEngine.Playables;

namespace Abandoned.SpaceApps.Opportunity
{
    public sealed class CameraDirector : MonoBehaviour
    {
        public Camera view;
        public Transform cinematicRig;
        public RoverController rover;
        public ChapterDirector chapter;
        public Transform rockLook, instrumentView;
        public Transform finalShotOrigin;
        public PlayableDirector timeline;
        public PlayableAsset intro, landing, activation, discovery, timeSkip, lost, farewell, transition, heatArrival, meteoriteDiscovery;
        public CinematicPath[] bakedPaths;
        public float yaw = 12, pitch = 19;
        float distance = 6.3f, fov = 50, inspectionBlend;
        Vector3 smoothPosition;
        ChapterStage stage;
        Vector3 blendFromPosition;
        Quaternion blendFromRotation;
        float blendElapsed;
        CinematicPath activePath;
        FieldStudy previousStudy;int previousSample;int sceneMask;
        public bool Paused { get; set; }
        public float Shake { get; set; }
        void Awake() { smoothPosition = view.transform.position;sceneMask=view.cullingMask; }
        public void Enter(ChapterStage next)
        {
            stage = next;inspectionBlend=0;
            blendFromPosition = view.transform.position;
            blendFromRotation = view.transform.rotation;
            blendElapsed = 0;
            timeline.Stop();
            timeline.playableAsset = next switch
            {
                ChapterStage.Intro => intro, ChapterStage.Landing => landing, ChapterStage.Activation => activation,
                ChapterStage.HeatShieldArrival=>heatArrival,ChapterStage.MeteoriteDiscovery=>meteoriteDiscovery,ChapterStage.Discovery => discovery, ChapterStage.TimeSkip => timeSkip, ChapterStage.CommunicationLost => lost,
                ChapterStage.Farewell => farewell, ChapterStage.Transition => transition, _ => null
            };
            if (timeline.playableAsset != null) { timeline.time = 0; timeline.Play(); timeline.Evaluate(); }
            activePath = null;
            if (timeline.playableAsset != null)
                foreach (var path in bakedPaths) if (path != null && path.name == timeline.playableAsset.name) { activePath = path; break; }
            if(next==ChapterStage.ChapterEnd)
            {
                cinematicRig.SetPositionAndRotation(blendFromPosition,blendFromRotation);
                fov=view.fieldOfView;
            }
            if (IsDriving) { yaw = rover.transform.eulerAngles.y + 10; pitch = 19; smoothPosition = view.transform.position; }
        }
        bool IsDriving => stage == ChapterStage.HeatShieldExploration || stage == ChapterStage.Exploration || stage == ChapterStage.FinalExploration || stage == ChapterStage.DustStorm;
        public void FramePhoto(Transform subject)
        {
            Vector3 origin=rover.transform.position+Vector3.up*1.6f;
            if(subject!=null){Vector3 delta=subject.position-origin;yaw=Quaternion.LookRotation(delta).eulerAngles.y;pitch=-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;}
        }
        void LateUpdate()
        {
            if (Paused) return;
            view.cullingMask=sceneMask&~(1<<SplitLanding.SpiritLayer);
            view.clearFlags=chapter.environment.Memorial?CameraClearFlags.SolidColor:CameraClearFlags.Skybox;
            view.backgroundColor=chapter.environment.Memorial?Color.black:new Color(.003f,.006f,.012f);
            float dt = Time.deltaTime;
            var study=chapter.investigation;if(previousStudy!=study.Study||previousSample!=study.Sample){blendFromPosition=view.transform.position;blendFromRotation=view.transform.rotation;blendElapsed=0;smoothPosition=view.transform.position;previousStudy=study.Study;previousSample=study.Sample;}
            blendElapsed += dt;
            Vector3 position;
            Quaternion rotation;
            if(chapter.investigation.Active){chapter.investigation.CameraPose(out position,out rotation,out float studyFov);fov=Mathf.Lerp(fov,studyFov,dt*4);}
            else if(chapter.journey.photography.Active)
            {
                position=rover.transform.position+Vector3.up*1.6f+rover.transform.forward*.6f;rotation=Quaternion.Euler(pitch,yaw,0);fov=Mathf.Lerp(fov,42,dt*4);
            }
            else if (IsDriving)
            {
                var mouse = chapter.journey.ChoosingMemory||chapter.journey.Transmitting?Vector2.zero:GameInput.MouseDelta;
                yaw += mouse.x * .09f * GameSettings.Sensitivity;
                pitch = Mathf.Clamp(pitch - mouse.y * .07f * GameSettings.Sensitivity * (GameSettings.Invert ? -1 : 1), 4, 58);
                var focus = rover.transform.position + Vector3.up * 1.0f + rover.transform.forward*.6f;
                var desired = focus - Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * distance;
                var direction = desired - focus;
                if (Physics.SphereCast(focus, .18f, direction.normalized, out var hit, direction.magnitude, rover.groundMask)) desired = focus + direction.normalized * Mathf.Max(.8f, hit.distance - .2f);
                smoothPosition = Vector3.Lerp(smoothPosition, desired, 1 - Mathf.Exp(-dt * 5));
                position = smoothPosition;
                rotation = Quaternion.LookRotation(focus - position);
                fov = Mathf.Lerp(fov, 52 + Mathf.Abs(rover.Speed) * .7f, dt * 2);
            }
            else if (stage == ChapterStage.RockAnalysis)
            {
                position = instrumentView.position;
                rotation = instrumentView.rotation;
                bool inspecting=chapter.science.Phase==AbrasionPhase.Analyze&&chapter.science.ToolStowed;inspectionBlend=Mathf.MoveTowards(inspectionBlend,inspecting?1:0,dt*.8f);
                if(inspecting){var center=chapter.science.surface.WorldPoint(new Vector2(.5f,.5f));position=Vector3.Lerp(instrumentView.position,center+(instrumentView.position-center).normalized*.62f,inspectionBlend);rotation=Quaternion.LookRotation(center-position);}
                fov = Mathf.Lerp(fov, inspecting?28:39, dt * 2);
            }
            else
            {
                if (activePath != null)
                {
                    activePath.Evaluate(chapter.Elapsed, out var authoredPosition, out var authoredRotation);
                    cinematicRig.SetPositionAndRotation(authoredPosition, authoredRotation);
                    fov=activePath.Lens(chapter.Elapsed);
                }
                position = cinematicRig.position;
                rotation = cinematicRig.rotation;
                if (stage == ChapterStage.CommunicationLost || (stage == ChapterStage.Transition && chapter.Elapsed < 6.8))
                    {
                    if(stage==ChapterStage.CommunicationLost){var orientation=Quaternion.Euler(0,chapter.FinalYaw,0);position=chapter.FinalPosition+orientation*(position-finalShotOrigin.position);rotation=orientation*rotation;}
                    else position += rover.transform.position - finalShotOrigin.position;
                }
                if(activePath==null&&stage!=ChapterStage.ChapterEnd)fov=Mathf.Lerp(fov,45,dt*2);
            }
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(blendElapsed / (GameSettings.ReducedMotion ? 2.8f : 1.7f)));
            if(chapter.investigation.Active)blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(blendElapsed/1.8f));
            if (!IsDriving&&stage!=ChapterStage.RockAnalysis||chapter.journey.photography.Active) blend = 1;
            view.transform.SetPositionAndRotation(Vector3.Lerp(blendFromPosition, position, blend), Quaternion.Slerp(blendFromRotation, rotation, blend));
            if (!GameSettings.ReducedMotion && Shake > 0)
                view.transform.localRotation *= Quaternion.Euler(Mathf.Sin(Time.time * 37) * Shake, Mathf.Sin(Time.time * 29) * Shake * .5f, 0);
            if(!GameSettings.ReducedMotion&&chapter.weather&&chapter.weather.CameraMotion>0)
            {
                float time=chapter.Elapsed,amount=chapter.weather.CameraMotion*(chapter.investigation.Active ? .12f : 1);
                float x=(Mathf.PerlinNoise(time*2.1f,3)-.5f)*4,y=(Mathf.PerlinNoise(9,time*1.7f)-.5f)*4,roll=(Mathf.PerlinNoise(time*.85f,11)-.5f)*2;
                view.transform.localRotation*=Quaternion.Euler(x*amount,y*amount*.65f,roll*amount*.6f);
            }
            view.fieldOfView = fov*(chapter.split?chapter.split.LensScale:1f);
        }
    }
}
