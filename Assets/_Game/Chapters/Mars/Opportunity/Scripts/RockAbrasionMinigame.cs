using UnityEngine;
using UnityEngine.InputSystem;
namespace Abandoned.SpaceApps.Opportunity
{
    public enum AbrasionPhase { Identify, Align, Abrade, Analyze, Complete }
    public sealed class RockAbrasionMinigame : MonoBehaviour
    {
        public RoverController rover;
        public Transform toolDock, toolContact, exposedCore;
        public AbrasionSurface surface;
        public ParticleSystem abrasionDust;
        public MissionAudio sound;
        public ChapterData data;
        public bool circularOperation=true;
        public Camera view;
        public AbrasionPhase Phase { get; private set; }
        public Vector2 Cursor { get; private set; }
        readonly AbrasionLoad load=new AbrasionLoad();
        public float Pressure => load.Pressure;
        public float Temperature=>load.Temperature;
        public bool Cooling=>load.Recovering;
        /// <summary>Segundos segurando E para concluir a abrasão.</summary>
        public const float HoldSeconds=7f;
        public bool Cutting=>Working&&Phase==AbrasionPhase.Abrade&&Active;
        public float RemainingSeconds=>Mathf.Max(0,HoldSeconds*(1-Progress));
        public float PressureLow=>load.Low;
        public float PressureHigh=>load.High;
        public float Progress { get; private set; }
        public float RemovalFeedback{get;private set;}
        public float ToolRPM{get;private set;}
        public float Alignment { get; private set; }
        public float AnalysisProgress => (Observations + ScanProgress) / 2f;
        public bool AnalysisScanning {get;private set;}
        public float ScanProgress {get;private set;}
        public int Observations { get; private set; }
        public int HoverFeature { get; private set; } = -1;
        public bool Working { get; private set; }
        public bool Active { get; private set; }
        public bool[] Observed { get; } = new bool[2];
        float phaseTime, withdrawal, deployment;
        Vector3 withdrawalStart;Quaternion withdrawalRotation;
        public bool ToolStowed => withdrawal>=InstrumentTrajectory.RetractionSeconds;
        public bool ToolDeployed => deployment>=2.8f;
        public Bounds SpecimenBounds => surface.GetComponent<Collider>().bounds;
        Vector3 originalTool; Quaternion originalToolRotation;
        public Vector2 SurfaceCursor => new Vector2(.5f + Cursor.x * .5f, .5f - Cursor.y * .5f);
        void Awake() { originalTool = toolDock.localPosition; originalToolRotation = toolDock.localRotation; }
        public void Begin()
        {
            Active = true; Phase = AbrasionPhase.Identify; Cursor = new Vector2(-.25f, .18f);
            withdrawal=deployment=0;
            rover.GetComponentInChildren<InstrumentArm>().specimen=surface.GetComponent<Collider>();
            load.Reset();Working=false;ToolRPM=RemovalFeedback=0;Progress = Alignment = phaseTime = 0; Observations = 0;
            Observed[0] = Observed[1] = false; AnalysisScanning=false;ScanProgress=0;HoverFeature = -1; surface.ResetSurface();
        }
        public void End()
        { Active = Working = false;load.Reset(); abrasionDust.Stop(); sound.Tool(0); toolDock.localPosition = originalTool; toolDock.localRotation = originalToolRotation; }
        void Update()
        {
            if (!Active || Time.timeScale == 0) return;
            Vector2 arrows=new Vector2((GameInput.Hold(Key.RightArrow)?1:0)-(GameInput.Hold(Key.LeftArrow)?1:0),(GameInput.Hold(Key.UpArrow)?1:0)-(GameInput.Hold(Key.DownArrow)?1:0));
            float effort=(GameInput.Hold(Key.D)||GameInput.Hold(Key.W)?1:0)-(GameInput.Hold(Key.A)||GameInput.Hold(Key.S)?1:0);
            Tick(Time.deltaTime, new Vector2(0,effort), GameInput.MouseDelta+arrows*Time.deltaTime*160, GameInput.ActionPressed, GameInput.ActionHeld);
        }
        public void Tick(float dt, Vector2 axes, Vector2 mouse, bool confirm, bool operate)
        {
            phaseTime += dt; Working = false;RemovalFeedback=0;
            if (Phase == AbrasionPhase.Identify || Phase == AbrasionPhase.Abrade || Phase == AbrasionPhase.Analyze)
            {
                Cursor += CursorMotion(mouse)*GameSettings.Sensitivity;
                if (Phase != AbrasionPhase.Abrade) Cursor += new Vector2(axes.x, -axes.y) * dt * .4f;
                Cursor = new Vector2(Mathf.Clamp(Cursor.x, -.88f, .88f), Mathf.Clamp(Cursor.y, -.83f, .83f));
            }
            if (Phase == AbrasionPhase.Identify && deployment >= 2.8f) { Phase = AbrasionPhase.Align; if(circularOperation)Cursor=Vector2.zero; phaseTime = 0; sound.Confirm(); }
            else if (Phase == AbrasionPhase.Align)
            { Alignment = Mathf.Clamp01(phaseTime / 1.2f); if (Alignment >= 1) { Phase = AbrasionPhase.Abrade; phaseTime = 0; } }
            else if (Phase == AbrasionPhase.Abrade)
            {
                // Só segurar E: a abrasão avança enquanto E está pressionado e para quando solta; sem pressão, mira ou aquecimento.
                Cursor=Vector2.zero;
                Working = operate;
                if (Working)
                {
                    Progress=Mathf.Clamp01(Progress+dt/HoldSeconds);
                    surface.GrindCircular(dt*32f/HoldSeconds,Pressure,load.Low,load.High);
                }
                if (Progress >= 1) { Phase = AbrasionPhase.Complete;Observations=2;Observed[0]=Observed[1]=true;surface.CommitCollision(); phaseTime = 0; withdrawal=0;withdrawalStart=toolDock.position;withdrawalRotation=toolDock.rotation;sound.Confirm(); }
            }
            else if (Phase == AbrasionPhase.Analyze)
            {
                Cursor=Vector2.zero;HoverFeature=ToolStowed?Observations:-1;
                surface.SetInspection(Observations+1);
                if(confirm&&ToolStowed&&!AnalysisScanning&&Observations<2){AnalysisScanning=true;ScanProgress=0;sound.Confirm();}
                if(AnalysisScanning)
                {
                    ScanProgress=Mathf.Clamp01(ScanProgress+dt/(Observations==0?2.2f:3.5f));
                    if(ScanProgress>=1){if(sound.feedback)sound.feedback.Observe();Observed[Observations]=true;Observations++;AnalysisScanning=false;ScanProgress=0;sound.Confirm();if(Observations==2)Phase=AbrasionPhase.Complete;}
                }
            }
            if (Phase == AbrasionPhase.Align || Phase == AbrasionPhase.Abrade || Phase == AbrasionPhase.Identify)
            {
                var desired = surface.WorldPoint(SurfaceCursor) + surface.transform.up * (Phase == AbrasionPhase.Abrade ? .035f : .2f);
                deployment=Mathf.Min(2.8f,deployment+dt);
                if(deployment<2.8f)
                {
                    float u=1-deployment/2.8f;
                    toolDock.position=InstrumentTrajectory.Position(desired,toolDock.parent.TransformPoint(originalTool),SpecimenBounds,u);
                    toolDock.rotation=InstrumentTrajectory.Rotation(surface.transform.rotation,toolDock.parent.rotation*originalToolRotation,u);
                }
                else {toolDock.position = Vector3.Lerp(toolDock.position, desired, 1 - Mathf.Exp(-dt * 5)); toolDock.rotation = Quaternion.Slerp(toolDock.rotation, surface.transform.rotation, dt * 4);}
            }
            else
            {
                withdrawal=Mathf.Min(InstrumentTrajectory.RetractionSeconds,withdrawal+dt);
                float u=withdrawal/InstrumentTrajectory.RetractionSeconds;
                toolDock.position=InstrumentTrajectory.Position(withdrawalStart,toolDock.parent.TransformPoint(originalTool),SpecimenBounds,u);
                toolDock.rotation=InstrumentTrajectory.Rotation(withdrawalRotation,toolDock.parent.rotation*originalToolRotation,u);
            }
            abrasionDust.transform.position = surface.WorldPoint(SurfaceCursor);
            var emission = abrasionDust.emission; float removal=Cutting?Mathf.Clamp01(Mathf.Sqrt(surface.LastRemoval/Mathf.Max(.0001f,dt)*100)):0;if(Cutting&&circularOperation&&surface.LastRemoval>0)removal=Mathf.Clamp01(.55f+removal*.45f);RemovalFeedback=removal;
            ToolRPM=Mathf.MoveTowards(ToolRPM,Working?Mathf.Lerp(1450,760,Pressure):0,dt*2400);rover.toolRotor.Rotate(Vector3.up,ToolRPM*dt,Space.Self);
            emission.rateOverTime=Cutting?Mathf.Lerp(8,125,removal):0;sound.Tool(Working?.28f+removal*.5f:0);
        }
        public void SetAlignmentForTest(Vector2 cursor) => Cursor = cursor;
        Vector2 CursorMotion(Vector2 pixels)
        {
            if(pixels.sqrMagnitude==0)return Vector2.zero;if(!view)return new Vector2(pixels.x,-pixels.y)*.0018f;
            Vector2 uv=SurfaceCursor;Vector3 origin=view.WorldToScreenPoint(surface.WorldPoint(uv));
            Vector3 a=(view.WorldToScreenPoint(surface.WorldPoint(uv+Vector2.right*.02f))-origin)/.02f,b=(view.WorldToScreenPoint(surface.WorldPoint(uv+Vector2.up*.02f))-origin)/.02f;
            float determinant=a.x*b.y-a.y*b.x;if(Mathf.Abs(determinant)<.01f)return Vector2.zero;
            return new Vector2((pixels.x*b.y-pixels.y*b.x)*2/determinant,-(a.x*pixels.y-a.y*pixels.x)*2/determinant);
        }
    }
}
