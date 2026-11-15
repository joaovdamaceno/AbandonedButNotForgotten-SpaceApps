using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.Chapters.Surveyor.EditorTools;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorSoilBuilder:SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Soil",Scene=Folder+"/Preview/SurveyorSoilPreview.unity";
        public const string ResultPath="Assets/_Game/Chapters/Surveyor/Data/SurveyorSoilResults.asset";
        public override string Id=>"surveyor.soil";public override string Owner=>"R6 · SUR-010";public override string ScenePath=>Scene;
        public override string RootName=>"[R6 SUR-010] Soil tests";public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor";
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-010 Reconstruir testes de solo")]
        public static void BuildMenu(){var r=SpaceAppsSceneBuilder.Run(new SurveyorSoilBuilder());if(!r.Succeeded)throw new InvalidOperationException(r.ToString());Debug.Log(r);}
        public static void BuildFromCommandLine(){try{BuildMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public override void Build(SceneBuildContext c)
        {
            var photo=Folder+"/Archive/AS12-48-7110.jpg";var ti=(TextureImporter)AssetImporter.GetAtPath(photo);ti.maxTextureSize=1024;ti.mipmapEnabled=false;ti.isReadable=false;ti.SaveAndReimport();
            var result=c.Asset(ResultPath,()=>ScriptableObject.CreateInstance<SurveyorSoilResults>());
            var chapter=c.Asset(Folder+"/Preview/SoilChapter.asset",()=>ChapterDefinition.Create("surveyor.soil.preview","soil.title","soil.description","soil.sample","SurveyorSoilPreview",60,"surveyor.soil.preview",new[]{new ChapterStep("Sample",false,"Eclipse"),new ChapterStep("Eclipse",true)}));
            var montage=c.Asset("Assets/_Game/Chapters/Surveyor/Sequences/SoilWorkMontage.asset",()=>SequenceDefinition.Create("surveyor.seq.SoilWorkMontage",new[]{new ShotDefinition("surveyor.soil.otherWork",6,"world",new[]{new ShotKeyframe(0,Vector3.zero,Vector3.zero,25.3f),new ShotKeyframe(6,Vector3.zero,Vector3.zero,25.3f)})}));
            result.Configure(AssetDatabase.LoadAssetAtPath<Texture2D>(photo),montage);EditorUtility.SetDirty(result);
            var panel=c.Asset(Folder+"/SoilPanelSettings.asset",()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));panel.sortingOrder=4;EditorUtility.SetDirty(panel);
            var pausePanel=c.Asset(Folder+"/PausePanelSettings.asset",()=>Object.Instantiate(panel));pausePanel.sortingOrder=10;EditorUtility.SetDirty(pausePanel);
            var material=c.Material(Folder+"/Generated/Contact.mat","Universal Render Pipeline/Lit");material.SetColor("_BaseColor",new Color(.19f,.18f,.17f));EditorUtility.SetDirty(material);
            var mark=c.Asset(Folder+"/Generated/Mark.asset",()=>new Mesh{name="Contact imprint"});var draft=new SurveyorSiteGeometry.Draft();draft.Tube(new Vector3(0,.001f,0),new Vector3(0,.003f,0),.07f,.5f,16);draft.Write(mark);EditorUtility.SetDirty(mark);
            var prefab=c.Prefab(Folder+"/SurveyorSoilPreview.prefab",root=>
            {
                Get<ChapterDirector>(root);Get<SessionClock>(root);Get<InputRouter>(root);Get<InteractionService>(root);Get<SurveyorScoop>(root);Get<SurveyorSoilTests>(root);Get<SurveyorTelevisionOutput>(root);Get<SubtitlePresenter>(root);Get<FocusPause>(root);Get<SurveyorSoilPanel>(root);
                var site=Nested(root,"LunarSite","Assets/_Game/Chapters/Surveyor/Art/LunarSite.prefab");var surveyor=Nested(root,"Surveyor","Assets/_Game/Chapters/Surveyor/Art/SurveyorVisibleRig.prefab");
                var trench=Nested(root,"Trench",SurveyorScoopBuilder.TrenchPrefabPath);var arm=Nested(root,"ScoopRig",SurveyorScoopBuilder.RigPrefabPath);
                var camera=Get<Camera>(Child(root,"Camera"));camera.transform.position=surveyor.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount").position;camera.transform.LookAt(trench.transform.Find("PhotoTarget"));camera.fieldOfView=25.3f;camera.nearClipPlane=.025f;camera.farClipPlane=360;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowHDR=camera.allowMSAA=false;
                var cameras=Get<CameraDirector>(root);cameras.Initialize(camera);var viewport=Get<SurveyorSiteViewport>(camera.gameObject);viewport.Configure(camera);viewport.DrawToGameView=false;
                var marks=Child(root,"SoilMarks");marks.transform.SetPositionAndRotation(trench.transform.position,trench.transform.rotation);
                var press=Child(marks,"Press");Surface(press,mark,material);press.transform.localPosition=new Vector3(.08f,.006f,.38f);
                var impact=Child(marks,"Impact");Surface(impact,mark,material);impact.transform.localPosition=new Vector3(.08f,.006f,-.38f);impact.transform.localScale=new Vector3(.7f,1,.8f);
                var others=Child(marks,"MissionMontage");for(int i=0;i<3;i++){var child=Child(others,"OtherTrench"+i);Surface(child,mark,material);child.transform.localPosition=new Vector3(-.35f+i*.3f,.006f,.65f);child.transform.localScale=new Vector3(.6f,1,3);}
                var sun=Get<Light>(Child(root,"Sun"));sun.type=LightType.Directional;sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(23,145,0);
                var doc=Get<UIDocument>(Child(root,"SoilUI"));doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder+"/SoilPanel.uxml");
                var pause=Nested(root,"Pause","Assets/_Game/UI/Pause/PauseMenu.prefab").GetComponent<PauseMenu>();pause.GetComponent<UIDocument>().panelSettings=pausePanel;
                var parts=new List<Renderer>();foreach(var r in surveyor.GetComponentsInChildren<Renderer>(true))if(r.name=="Team_ScoopLinkage"||r.name=="Team_ScoopTool")parts.Add(r);
                Get<SurveyorSoilTests>(root).SetOpticalOccluders(new[]{arm.transform.Find("Shoulder/UpperLink").GetComponent<Renderer>()});
                Get<SurveyorSoilPreview>(root).Configure(chapter,trench,arm,site.transform.Find("NearGround_40m/Chunk_2_2").GetComponent<Renderer>(),parts.ToArray(),cameras,viewport,
                    AssetDatabase.LoadAssetAtPath<Material>(SurveyorTelevisionBuilder.MaterialPath),result,press.transform,impact.transform,others.transform,doc,AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/SoilTexts.json"),AssetDatabase.LoadAssetAtPath<SurveyorSubtitles>("Assets/_Game/Chapters/Surveyor/Data/SurveyorSubtitles.asset"),pause);
            });
            c.PrefabInstance(c.Root.transform,prefab,"SurveyorSoil");RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.075f,.088f,.11f);
        }
        public override void Validate(SceneBuildContext c,List<string> errors){if(c.Root.GetComponentsInChildren<AudioListener>(true).Length!=1)errors.Add("One listener required.");if(AssetDatabase.LoadAssetAtPath<SurveyorSoilResults>(ResultPath).HistoricalPhoto==null)errors.Add("Historical photo missing.");}
        static void Surface(GameObject o,Mesh mesh,Material material){Get<MeshFilter>(o).sharedMesh=mesh;Get<MeshRenderer>(o).sharedMaterial=material;}
        static T Get<T>(GameObject o)where T:Component{var component=o.GetComponent<T>();return component!=null?component:o.AddComponent<T>();}
        static GameObject Child(GameObject root,string name){var t=root.transform.Find(name);if(t!=null)return t.gameObject;var go=new GameObject(name);go.transform.SetParent(root.transform,false);return go;}
        static GameObject Nested(GameObject root,string name,string path){var t=root.transform.Find(name);if(t!=null)return t.gameObject;var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.scene);go.name=name;go.transform.SetParent(root.transform,false);return go;}
    }
}
