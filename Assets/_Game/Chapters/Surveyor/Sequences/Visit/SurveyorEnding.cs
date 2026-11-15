using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    [CreateAssetMenu(menuName="Abandoned/Surveyor Ending")]
    public sealed class SurveyorEnding : ScriptableObject
    {
        [SerializeField] Texture2D historicalPhoto;
        [SerializeField] string creditKey="sur.visit.photoCredit";
        public Texture2D HistoricalPhoto=>historicalPhoto;
        public string CreditKey=>creditKey;
        public void Configure(Texture2D photo){historicalPhoto=photo;}
        public List<string> Validate()
        {
            var errors=new List<string>();if(historicalPhoto==null)errors.Add("Ending requires the credited NASA photograph.");
            ContractValidation.RequireId(errors,name,"creditKey",creditKey);return errors;
        }
    }
}
