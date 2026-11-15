using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Abandoned.SpaceApps.Opportunity
{
    public sealed class RoverPhotography : MonoBehaviour
    {
        public const int MaximumFrames=3;
        public ChapterDirector chapter;
        public bool Active { get; private set; }
        public bool Capturing { get; private set; }
        public float Flash { get; private set; }
        public List<Texture2D> Frames { get; } = new List<Texture2D>();
        public int CaptureVersion { get; private set; }
        public int Station { get; private set; } = -1;
        int openedFrame=-1;
        /// <summary>Segundos segurando E para fotografar; o enquadramento já vem pronto.</summary>
        public const float HoldSeconds=1.2f;
        public float HoldProgress { get; private set; }
        public void Toggle()
        {
            if (Capturing) return; Active = !Active; openedFrame=Time.frameCount;
            HoldProgress=0;Station = Active ? chapter.journey.NearbyStation : -1; chapter.rover.Controllable = !Active;
            if (Active) chapter.cameras.FramePhoto(Station >= 0 ? chapter.journey.panoramaSubjects[Station] : null);
            chapter.SetCursor();
        }
        public void Close() { Active = false; chapter.SetCursor(); }
        public void Clear()
        { StopAllCoroutines();if(Capturing&&chapter&&chapter.hud)chapter.hud.HideForCapture(false);Active = Capturing = false; foreach (var frame in Frames) if (frame != null) Destroy(frame); Frames.Clear(); CaptureVersion++; }
        public void TakePhoto() { if (Active && !Capturing) StartCoroutine(Capture()); }
        IEnumerator Capture()
        {
            Capturing = true; chapter.hud.HideForCapture(true);
            try
            {
                yield return null; yield return new WaitForEndOfFrame();
                var texture = FrameCaptureUtility.Photograph(chapter.cameras.view);
                // Preserve the first view of the valley and the two most recent views.
                if (Frames.Count >= MaximumFrames) { Destroy(Frames[1]); Frames.RemoveAt(1); }
                Frames.Add(texture); CaptureVersion++;
                Flash = 1; chapter.sound.Photograph(); chapter.journey.VisitPhoto(Station);
                if (Station < 0) chapter.hud.Toast("photo.saved");
                Active = false;chapter.rover.Controllable = chapter.Stage == ChapterStage.HeatShieldExploration || chapter.Stage == ChapterStage.Exploration || chapter.journey.FinalAct;chapter.SetCursor();
            }
            finally{Capturing=false;if(chapter&&chapter.hud)chapter.hud.HideForCapture(false);}
        }
        void OnDestroy(){foreach(var frame in Frames)if(frame)Destroy(frame);Frames.Clear();}
        void Update()
        {
            Flash = Mathf.MoveTowards(Flash, 0, Time.unscaledDeltaTime * 3);
            if (chapter.Paused || !Active) return;
            HoldProgress=openedFrame!=Time.frameCount&&GameInput.ActionHeld?Mathf.Min(1,HoldProgress+Time.deltaTime/HoldSeconds):0;
            if (HoldProgress>=1) { HoldProgress=0; TakePhoto(); }
        }
    }
}
