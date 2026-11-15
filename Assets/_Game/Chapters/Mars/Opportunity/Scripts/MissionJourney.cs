using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    [System.Serializable] public sealed class JourneyBeat { public Transform point; public string titleKey, hintKey; public float radius = 7; }
    public sealed class MissionJourney : MonoBehaviour
    {
        public ChapterDirector chapter;
        public JourneyBeat[] traverse;
        public Transform[] panoramaStations, panoramaSubjects;
        public RoverPhotography photography;
        public int TraverseStep { get; private set; }
        public int PanoramaMask { get; private set; }
        public bool ReadyForScience => TraverseStep >= traverse.Length&&chapter.investigation.BerriesCompared;
        public int PanoramaCount => ((PanoramaMask & 1) != 0 ? 1 : 0) + ((PanoramaMask & 2) != 0 ? 1 : 0) + ((PanoramaMask & 4) != 0 ? 1 : 0);
        public bool FinalAct => chapter.Stage == ChapterStage.FinalExploration || chapter.Stage == ChapterStage.DustStorm;
        public Transform CurrentTarget
        {
            get
            {
                if(chapter.investigation.OverridesTarget)return chapter.investigation.CurrentTarget;
                if (chapter.Stage == ChapterStage.Exploration) return ReadyForScience ? chapter.scienceTarget : traverse[Mathf.Clamp(TraverseStep,0,traverse.Length-1)].point;
                for (int i = 0; i < panoramaStations.Length; i++) if ((PanoramaMask & (1 << i)) == 0) return panoramaStations[i];
                return chapter.investigation.dustSunSite;
            }
        }
        public string ObjectiveKey => chapter.investigation.OverridesTarget?chapter.investigation.ObjectiveKey: chapter.Stage == ChapterStage.Exploration ? (ReadyForScience ? "objective.sample" : traverse[Mathf.Clamp(TraverseStep,0,traverse.Length-1)].titleKey)
            : "objective.panorama";
        public int NearbyStation
        { get { if (!FinalAct) return -1; for (int i = 0; i < panoramaStations.Length; i++) if (Vector3.Distance(chapter.rover.transform.position, panoramaStations[i].position) < 8) return i; return -1; } }
        public void ResetJourney()
        { PanoramaMask = 0; TraverseStep = 1; /* the first waypoint ("find the passage") was removed from the route */ photography.Clear(); }
        public void BeginFinal() { PanoramaMask = 0; }
        public void VisitPhoto(int station)
        {
            if (!FinalAct || station < 0) return;
            int old = PanoramaCount; PanoramaMask |= 1 << station; chapter.Progress.RecordPanorama();
            if (old != PanoramaCount) { chapter.sound.Confirm(); if(PanoramaCount==1)chapter.hud.Toast("photo.fragment"); }
        }
        void Update()
        {
            if (chapter.Paused || Time.timeScale == 0) return;
            if (chapter.Stage == ChapterStage.Exploration && TraverseStep<traverse.Length&&!chapter.investigation.NeedBerries&&!chapter.investigation.Active)
            {
                var beat = traverse[TraverseStep];
                if (Vector3.Distance(chapter.rover.transform.position, beat.point.position) < beat.radius)
                { TraverseStep++; chapter.sound.Confirm(); if(TraverseStep==1||TraverseStep==2)chapter.feedback.Observe();chapter.hud.Toast(beat.hintKey); }
            }
        }
        /// <summary>Encerra a rota: o próximo destino passa a ser a rocha da ciência.</summary>
        public void CompleteTraverseForPreview() => TraverseStep = traverse.Length;
    }
}
