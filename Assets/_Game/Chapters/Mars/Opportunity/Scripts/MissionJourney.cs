using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    [System.Serializable] public sealed class JourneyBeat { public Transform point; public string titleKey, hintKey; public float radius = 7; }
    public sealed class MissionJourney : MonoBehaviour
    {
        public ChapterDirector chapter;
        public JourneyBeat[] traverse;
        public Transform[] panoramaStations, panoramaSubjects;
        public Transform uplink;
        public RoverPhotography photography;
        public int TraverseStep { get; private set; }
        public int PanoramaMask { get; private set; }
        public bool Transmitting { get; private set; }
        public bool TransmissionComplete { get; private set; }
        public float Transmission { get; private set; }
        public bool ChoosingMemory { get; private set; }
        public bool HeadingForUplink { get; private set; }
        public int SelectedMemory { get; private set; }
        public bool ReadyForScience => TraverseStep >= traverse.Length&&chapter.investigation.BerriesCompared;
        public int PanoramaCount => ((PanoramaMask & 1) != 0 ? 1 : 0) + ((PanoramaMask & 2) != 0 ? 1 : 0) + ((PanoramaMask & 4) != 0 ? 1 : 0);
        public bool FinalAct => chapter.Stage == ChapterStage.FinalExploration || chapter.Stage == ChapterStage.DustStorm;
        public Transform CurrentTarget
        {
            get
            {
                if(chapter.investigation.OverridesTarget)return chapter.investigation.CurrentTarget;
                if (chapter.Stage == ChapterStage.Exploration) return ReadyForScience ? chapter.scienceTarget : traverse[Mathf.Clamp(TraverseStep,0,traverse.Length-1)].point;
                if (PanoramaCount >= 1 || HeadingForUplink || ChoosingMemory || Transmitting) return uplink;
                for (int i = 0; i < panoramaStations.Length; i++) if ((PanoramaMask & (1 << i)) == 0) return panoramaStations[i];
                return uplink;
            }
        }
        public string ObjectiveKey => chapter.investigation.OverridesTarget?chapter.investigation.ObjectiveKey: chapter.Stage == ChapterStage.Exploration ? (ReadyForScience ? "objective.sample" : traverse[Mathf.Clamp(TraverseStep,0,traverse.Length-1)].titleKey)
            : Transmitting ? "objective.sending" : ChoosingMemory ? "objective.choose" : PanoramaCount >= 1 || HeadingForUplink ? "objective.send" : "objective.panorama";
        public int NearbyStation
        { get { if (!FinalAct) return -1; for (int i = 0; i < panoramaStations.Length; i++) if (Vector3.Distance(chapter.rover.transform.position, panoramaStations[i].position) < 8) return i; return -1; } }
        public bool NearUplink => Vector3.Distance(chapter.rover.transform.position, uplink.position) < 7;
        public void ResetJourney()
        { PanoramaMask = SelectedMemory = 0; TraverseStep = 1; /* the first waypoint ("find the passage") was removed from the route */ Transmitting = TransmissionComplete = ChoosingMemory = HeadingForUplink = false; Transmission = 0; photography.Clear(); }
        public void BeginFinal() { PanoramaMask = 0; Transmission = 0; Transmitting = TransmissionComplete = ChoosingMemory = HeadingForUplink = false; }
        public void ToggleUplinkRoute() { if (FinalAct && PanoramaCount > 0) { HeadingForUplink = !HeadingForUplink; chapter.hud.ObjectiveChanged(); } }
        public void VisitPhoto(int station)
        {
            if (!FinalAct || station < 0) return;
            int old = PanoramaCount; PanoramaMask |= 1 << station; chapter.Progress.RecordPanorama();
            if (old != PanoramaCount) { chapter.sound.Confirm(); if(PanoramaCount==1)chapter.hud.Toast("photo.fragment"); }
        }
        public void ChooseMemory(int index) => SelectedMemory = Mathf.Clamp(index, 0, Mathf.Max(0, photography.Frames.Count - 1));
        public void OpenTransmission()
        { if (!chapter.investigation.DustSunMeasured || !FinalAct || !NearUplink || PanoramaCount == 0 || Transmitting) return; ChoosingMemory = true; chapter.rover.Controllable = false; chapter.SetCursor(); }
        public void CancelTransmission() { ChoosingMemory = false; chapter.rover.Controllable = FinalAct; chapter.SetCursor(); }
        public void ConfirmTransmission()
        { if (!ChoosingMemory) return; ChoosingMemory = false; Transmitting = true; Transmission = 0; chapter.sound.Confirm(); chapter.SetCursor(); }
        void Update()
        {
            if (chapter.Paused || Time.timeScale == 0) return;
            if (chapter.Stage == ChapterStage.Exploration && TraverseStep<traverse.Length&&!chapter.investigation.NeedBerries&&!chapter.investigation.Active)
            {
                var beat = traverse[TraverseStep];
                if (Vector3.Distance(chapter.rover.transform.position, beat.point.position) < beat.radius)
                { TraverseStep++; chapter.sound.Confirm(); if(TraverseStep==1||TraverseStep==2)chapter.feedback.Observe();chapter.hud.Toast(beat.hintKey); }
            }
            if (Transmitting && !TransmissionComplete)
            { Transmission = Mathf.Min(1, Transmission + Time.deltaTime / 6.5f); if (Transmission == 1) { TransmissionComplete = true; chapter.Progress.TransmitMemory(); chapter.sound.Confirm(); } }
        }
        public void CompleteTraverseForPreview() => TraverseStep = traverse.Length;
    }
}
