using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Dados PT/EN locais do ato. O resolvedor de UI-003 pode substituí-los sem alterar o fluxo.</summary>
    [Serializable]
    public sealed class SurveyorLaunchTexts
    {
        [Serializable]
        public sealed class Entry { public string id; public string pt; public string en; }
        public Entry[] entries = Array.Empty<Entry>();
        Dictionary<string,Entry> index;

        public static SurveyorLaunchTexts Parse(TextAsset asset)
        {
            if(asset==null) throw new ArgumentNullException(nameof(asset));
            var catalog=JsonUtility.FromJson<SurveyorLaunchTexts>(asset.text);
            if(catalog==null) throw new ArgumentException("Catálogo do lançamento vazio.");
            var errors=catalog.Validate();
            if(errors.Count>0) throw new ArgumentException(string.Join("\n",errors));
            return catalog;
        }

        public List<string> Validate()
        {
            var errors=new List<string>(); var ids=new HashSet<string>();
            foreach(var e in entries??Array.Empty<Entry>())
                if(e==null || !ContractValidation.IsValidId(e.id) || !ids.Add(e.id) ||
                    string.IsNullOrWhiteSpace(e.pt) || string.IsNullOrWhiteSpace(e.en)) errors.Add("Entrada PT/EN ausente, inválida ou duplicada.");
            if(ids.Count==0)errors.Add("Catálogo sem entradas.");
            return errors;
        }

        public string Get(string id,string language)
        {
            if(string.IsNullOrEmpty(id))return string.Empty;
            if(index==null)
            {
                index=new Dictionary<string,Entry>();
                foreach(var e in entries)index.Add(e.id,e);
            }
            return index.TryGetValue(id,out var value)?language==SettingsData.English?value.en:value.pt:string.Empty;
        }
    }
}
