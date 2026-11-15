using UnityEngine;
namespace Abandoned.SpaceApps.Opportunity
{
    public enum FilmAction { Hold, Drive, Survey, Work }
    [System.Serializable] public sealed class FilmBeat
    {
        public float at, duration;
        public Vector3 from, to;
        public float yaw, endYaw, mastFrom, mastTo, stormFrom, stormTo, wear, powerFrom=1,powerTo=1;
        public bool valley, photograph, assembly, travel,secondaryWork,hardware;
        public float captionDuration;
        // Spirit's static twin, shown beside or instead of Opportunity (opening laboratory, 2010 memorial).
        public bool spirit;
        public Vector3 spiritAt;
        public float spiritYaw, spiritTilt, spiritSink;
        public FilmAction action;
        public string titleKey, bodyKey, music;
        public float movementEase;
    }
    [CreateAssetMenu(menuName="Opportunity/Cinematic Sequence")]
    public sealed class FilmSequence : ScriptableObject
    {
        public ChapterStage stage;
        public float duration;
        public FilmBeat[] beats;
        public FilmBeat At(float time,out int index){index=0;for(int i=0;i<beats.Length;i++)if(time>=beats[i].at)index=i;return beats[index];}
    }
}
