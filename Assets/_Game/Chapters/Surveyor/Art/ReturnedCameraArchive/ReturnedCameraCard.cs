using System.Collections.Generic;
using UnityEngine;
namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    [CreateAssetMenu(menuName="Abandoned/Surveyor Returned Camera Card")]
    public sealed class ReturnedCameraCard:ScriptableObject
    {
        [SerializeField]Texture2D photograph;[SerializeField]TextAsset texts;
        [SerializeField]string nasaUrl,testArticleUrl;
        public Texture2D Photograph=>photograph;public TextAsset Texts=>texts;public string NasaUrl=>nasaUrl;public string TestArticleUrl=>testArticleUrl;
        public void Configure(Texture2D photo,TextAsset table,string nasa,string test){photograph=photo;texts=table;nasaUrl=nasa;testArticleUrl=test;}
        public List<string> Validate(){var errors=new List<string>();if(photograph==null||texts==null)errors.Add("Card photograph/text missing.");if(texts!=null)errors.AddRange(new LocalizationService().AddTable(texts.text,name));if(nasaUrl!="https://ntrs.nasa.gov/citations/19710024418"||testArticleUrl!="https://airandspace.si.edu/collection-objects/camera-television-surveyor/nasm_A19761264000")errors.Add("Use the verified flight-camera report and clearly identified test-article record.");return errors;}
    }
}
