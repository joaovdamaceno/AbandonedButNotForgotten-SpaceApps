using UnityEngine;
using UnityEngine.UIElements;
using Abandoned.SpaceApps.UI;
namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Optional foldout inside the existing Surveyor dossier details; no extra modal or chapter input owner.</summary>
    public sealed class SurveyorReturnedCameraView:MonoBehaviour
    {
        [SerializeField]ReturnedCameraCard data;
        SurveyorArchiveHost archive;VisualElement root,modal;ScrollView body;Label title;Foldout foldout;
        readonly LocalizationService localization=new LocalizationService();bool initialized;
        public ReturnedCameraCard Data=>data;public Foldout Foldout=>foldout;
        public void Configure(ReturnedCameraCard card)=>data=card;
        void Start()=>Initialize();
        public void Initialize()
        {
            if(initialized)return;archive=GetComponent<SurveyorArchiveHost>();archive.Initialize();if(data==null||data.Validate().Count>0)throw new System.ArgumentException("Returned camera card invalid.");
            localization.AddTable(data.Texts.text,"returned-camera");localization.ApplySettings(archive.Settings.Current);root=archive.Screen.GetComponent<UIDocument>().rootVisualElement;modal=root.Q("panel-card");body=root.Q<ScrollView>("panel-body");title=root.Q<Label>("panel-title");archive.Settings.Changed+=Settings;initialized=true;Refresh();
        }
        void Update()=>Refresh();
        public void Refresh()
        {
            if(!initialized||archive.Owner.State!=AppState.Dossier||body==null||title.text!="Surveyor 3"||modal==null||!modal.ClassListContains("sa-chapter--surveyor")||foldout?.parent!=null)return;
            foldout=new Foldout{name="sur-returned-card",text=localization.Get("sur.camera.open"),value=false};foldout.style.marginTop=12;foldout.style.marginBottom=12;
            Add("sur.camera.body");var image=new Image{image=data.Photograph,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore};image.style.height=210;foldout.Add(image);Add("sur.camera.credit");Add("sur.camera.identity");
            var nasa=new Button(()=>Application.OpenURL(data.NasaUrl)){text=localization.Get("sur.camera.report")};nasa.AddToClassList("sa-button");nasa.AddToClassList("sa-button--secondary");foldout.Add(nasa);
            var test=new Button(()=>Application.OpenURL(data.TestArticleUrl)){text=localization.Get("sur.camera.inventory")};test.AddToClassList("sa-button");test.AddToClassList("sa-button--secondary");foldout.Add(test);
            body.contentContainer.Insert(Mathf.Max(0,body.contentContainer.childCount-2),foldout);
        }
        void Add(string key){var label=new Label(localization.Get(key));label.AddToClassList("sa-modal__text");label.style.whiteSpace=WhiteSpace.Normal;label.style.fontSize=19*archive.Settings.Current.textScale;foldout.Add(label);}
        void Settings(SettingsData settings){localization.ApplySettings(settings);foldout?.RemoveFromHierarchy();foldout=null;Refresh();}
        void OnDisable(){if(initialized)archive.Settings.Changed-=Settings;initialized=false;}
    }
}
