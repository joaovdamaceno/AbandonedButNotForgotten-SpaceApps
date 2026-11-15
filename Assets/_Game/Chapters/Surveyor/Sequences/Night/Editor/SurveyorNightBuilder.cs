using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorNightBuilder : SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Sequences/Night";
        public const string NightPath="Assets/_Game/Chapters/Surveyor/Sequences/SurveyorNight.asset";
        public const string PassagePath="Assets/_Game/Chapters/Surveyor/Sequences/SurveyorTimePassage.asset";
        public const string PreviewScene=Folder+"/Preview/SurveyorNightPreview.unity";
        public override string Id=>"surveyor.night";
        public override string Owner=>"R6 · SUR-012";
        public override string ScenePath=>PreviewScene;
        public override string RootName=>"[R6 SUR-012] Last contact and 31 months";
        public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor/Sequences";
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-012 Reconstruir noite e passagem")]
        public static void BuildFromMenu()
        { var result=SpaceAppsSceneBuilder.Run(new SurveyorNightBuilder());if(!result.Succeeded)throw new InvalidOperationException(result.ToString());Debug.Log(result); }
        public static void BuildFromCommandLine()
        { try{BuildFromMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);} }
        public static SequenceDefinition CreateNight()=>SequenceDefinition.Create("surveyor.seq.Night",new[]{Shot("surveyor.night.lastContact",17)},
            new[]{new SubtitleCue("SUR-S22","SUR-S22",0,5),new SubtitleCue("SUR-S23","SUR-S23",5,11),new SubtitleCue("SUR-S24","SUR-S24",11,17)});
        public static SequenceDefinition CreatePassage()=>SequenceDefinition.Create("surveyor.seq.TimePassage",new[]{Shot("surveyor.night.31months",13)},
            new[]{new SubtitleCue("SUR-S25","SUR-S25",1,6)});
        static ShotDefinition Shot(string id,float seconds)=>new ShotDefinition(id,seconds,SurveyorNight.RigId,
            new[]{new ShotKeyframe(0,Vector3.zero,Vector3.zero,25.3f),new ShotKeyframe(seconds,Vector3.zero,Vector3.zero,25.3f)},minGroundClearanceMeters:0);
        static SequenceDefinition SaveTrack(SceneBuildContext context,string path,SequenceDefinition reference)
        {
            var data=context.Asset(path,()=>reference);
            if(data!=reference){EditorUtility.CopySerialized(reference,data);Object.DestroyImmediate(reference);}EditorUtility.SetDirty(data);return data;
        }
        public override void Build(SceneBuildContext context)
        {
            var night=SaveTrack(context,NightPath,CreateNight());var passage=SaveTrack(context,PassagePath,CreatePassage());
            var chapter=context.Asset(Folder+"/Preview/NightChapter.asset",()=>ChapterDefinition.Create("surveyor.night.preview","sur.night.title","sur.night.description","sur.night.title",
                "SurveyorNightPreview",30,night.SequenceId,new[]{new ChapterStep(SurveyorNight.NightStep,false,SurveyorNight.PassageStep),
                new ChapterStep(SurveyorNight.PassageStep,false,SurveyorNight.NextStep),new ChapterStep(SurveyorNight.NextStep,true)}));
            var panel=context.Asset(Folder+"/NightPanelSettings.asset",()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));
            panel.sortingOrder=4;EditorUtility.SetDirty(panel);
            var pausePanel=context.Asset(Folder+"/PausePanelSettings.asset",()=>Object.Instantiate(panel));pausePanel.sortingOrder=10;EditorUtility.SetDirty(pausePanel);
            var prefab=context.Prefab(Folder+"/SurveyorNightPreview.prefab",root=>
            {
                Get<ChapterDirector>(root);Get<SessionClock>(root);Get<InputRouter>(root);Get<SurveyorNight>(root);Get<SequenceDirector>(root).enabled=false;
                Get<SurveyorTelevisionOutput>(root);Get<SubtitlePresenter>(root);Get<FocusPause>(root);
                Nested(root,"LunarSite","Assets/_Game/Chapters/Surveyor/Art/LunarSite.prefab");
                var rig=Nested(root,"Surveyor","Assets/_Game/Chapters/Surveyor/Art/SurveyorVisibleRig.prefab");
                // The preview starts after the sampling operations, with the scoop parked out of the optical path.
                rig.transform.Find("Body/Scoop_Arm_1").localRotation=Quaternion.Euler(0,-65,0);
                var camera=Get<Camera>(Child(root,"Television"));camera.transform.position=rig.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount").position;
                camera.transform.rotation=Quaternion.LookRotation(rig.transform.Find("Anchors/Trench").position-camera.transform.position,Vector3.up);
                camera.fieldOfView=25.3f;camera.nearClipPlane=.025f;camera.farClipPlane=360;camera.aspect=4f/3;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                camera.allowHDR=camera.allowMSAA=false;Get<UniversalAdditionalCameraData>(camera.gameObject).renderPostProcessing=false;
                var cameras=Get<CameraDirector>(root);cameras.Initialize(camera);
                var source=Get<SurveyorSiteViewport>(camera.gameObject);source.Configure(camera);source.DrawToGameView=false;
                var sun=Get<Light>(Child(root,"Sun"));sun.type=LightType.Directional;sun.intensity=1.15f;sun.color=new Color(1,.965f,.91f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(23,145,0);
                var doc=Get<UIDocument>(Child(root,"NightUI"));doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder+"/NightPanel.uxml");
                var pause=Nested(root,"Pause","Assets/_Game/UI/Pause/PauseMenu.prefab").GetComponent<PauseMenu>();
                pause.GetComponent<UIDocument>().panelSettings=pausePanel;
                Get<SurveyorNightPreview>(root).Configure(chapter,night,passage,cameras,Child(root,"FixedOptics").transform,sun,source,
                    AssetDatabase.LoadAssetAtPath<Material>(SurveyorTelevisionBuilder.MaterialPath),doc,AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/NightTexts.json"),pause);
            });
            context.PrefabInstance(context.Root.transform,prefab,"SurveyorNight");
            RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.075f,.088f,.11f);
        }
        public override void Validate(SceneBuildContext context,List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(NightPath).Validate());errors.AddRange(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(PassagePath).Validate());
            var loc=new LocalizationService();errors.AddRange(loc.AddTable(AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/NightTexts.json").text,"night"));
            if(context.Root.GetComponentsInChildren<AudioListener>(true).Length!=1)errors.Add("One AudioListener required.");
        }
        static T Get<T>(GameObject obj)where T:Component{var c=obj.GetComponent<T>();return c!=null?c:obj.AddComponent<T>();}
        static GameObject Child(GameObject root,string name){var old=root.transform.Find(name);if(old!=null)return old.gameObject;var go=new GameObject(name);go.transform.SetParent(root.transform,false);return go;}
        static GameObject Nested(GameObject root,string name,string path){var old=root.transform.Find(name);if(old!=null)return old.gameObject;var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.scene);go.name=name;go.transform.SetParent(root.transform,false);return go;}
    }
}
