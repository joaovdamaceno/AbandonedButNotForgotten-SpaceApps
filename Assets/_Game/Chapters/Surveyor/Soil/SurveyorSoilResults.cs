using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    [CreateAssetMenu(menuName="Abandoned/Surveyor/Soil Results")]
    public sealed class SurveyorSoilResults : ScriptableObject
    {
        [SerializeField] Texture2D historicalPhoto;
        [SerializeField] SequenceDefinition montage;
        public Texture2D HistoricalPhoto=>historicalPhoto;
        public float MontageSeconds=>montage.TotalDurationSeconds;
        public const string HistoricalCreditKey="soil.archive.credit";
        public void Configure(Texture2D photo,SequenceDefinition film){historicalPhoto=photo;montage=film;}
    }
}
