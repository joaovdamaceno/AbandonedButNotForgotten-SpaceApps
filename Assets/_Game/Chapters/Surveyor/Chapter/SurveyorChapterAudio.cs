using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public sealed class SurveyorChapterAudio:MonoBehaviour
    {
        [SerializeField] AudioCatalog catalog;
        public AudioCatalog Catalog=>catalog;
        public void Configure(AudioCatalog data)=>catalog=data;
    }
}
