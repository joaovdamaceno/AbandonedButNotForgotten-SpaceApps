using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.Chapters.Surveyor.EditorTools;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorVisitBuilder:SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Sequences/Visit";
        public const string TrackPath="Assets/_Game/Chapters/Surveyor/Sequences/SurveyorVisit.asset",EndingPath="Assets/_Game/Chapters/Surveyor/Data/SurveyorEnding.asset";
        public const string CastPath="Assets/_Game/Chapters/Surveyor/Art/ApolloVisitRig.prefab",PhotoPath=Folder+"/Archive/AS12-48-7134.jpg";
        public const string PreviewScene=Folder+"/Preview/SurveyorVisitPreview.unity";
        public override string Id=>"surveyor.visit";public override string Owner=>"R6 · SUR-013";public override string ScenePath=>PreviewScene;
        public override string RootName=>"[R6 SUR-013] The image it never made";public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor";
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-013 Reconstruir visita e final")]
        public static void BuildFromMenu(){var r=SpaceAppsSceneBuilder.Run(new SurveyorVisitBuilder());if(!r.Succeeded)throw new InvalidOperationException(r.ToString());Debug.Log(r);}
        public static void BuildFromCommandLine(){try{BuildFromMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static SequenceDefinition CreateReference()
        {
            ShotDefinition Shot(string id,float duration,string rig,bool continuity=false,params ShotEvent[] events)
            {
                var frames=new List<ShotKeyframe>{new ShotKeyframe(0,Vector3.zero,Vector3.zero,25.3f)};
                if(continuity)frames.Add(new ShotKeyframe(2,Vector3.zero,Vector3.zero,25.3f));
                frames.Add(new ShotKeyframe(duration,Vector3.zero,Vector3.zero,25.3f));
                return new ShotDefinition(id,duration,rig,frames,minGroundClearanceMeters:0,events:events,continueFromPreviousShot:continuity);
            }
            return SequenceDefinition.Create("surveyor.seq.Visit",new[]{
                Shot("surveyor.visit.notice",5,SurveyorVisit.LandingRig),Shot("surveyor.visit.landing",14,SurveyorVisit.LandingRig),
                Shot("surveyor.visit.meeting",13,SurveyorVisit.HumanRig,true),Shot("surveyor.visit.photograph",12,SurveyorVisit.HumanRig,false,new ShotEvent(0,SurveyorVisit.ArchiveEvent)),
                Shot("surveyor.visit.remember",6,SurveyorVisit.HumanRig,false,new ShotEvent(0,SurveyorVisit.MemorialEvent))},new[]{
                new SubtitleCue("SUR-S26","SUR-S26",0,5),new SubtitleCue("SUR-S27","SUR-S27",6,12),new SubtitleCue("SUR-S28","SUR-S28",20,26),
                new SubtitleCue("SUR-S29","SUR-S29",32,38),new SubtitleCue("SUR-S30","SUR-S30",38,43),new SubtitleCue("SUR-S31","SUR-S31",44,50)});
        }
        public override void Build(SceneBuildContext c)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(PhotoPath);importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.isReadable=false;
            importer.textureCompression=TextureImporterCompression.Compressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var reference=CreateReference();var track=c.Asset(TrackPath,()=>reference);if(track!=reference){EditorUtility.CopySerialized(reference,track);Object.DestroyImmediate(reference);}EditorUtility.SetDirty(track);
            var ending=c.Asset(EndingPath,()=>ScriptableObject.CreateInstance<SurveyorEnding>());ending.Configure(AssetDatabase.LoadAssetAtPath<Texture2D>(PhotoPath));EditorUtility.SetDirty(ending);
            var chapter=c.Asset(Folder+"/Preview/VisitChapter.asset",()=>ChapterDefinition.Create("surveyor.visit.preview","sur.visit.previewTitle","sur.visit.description","sur.visit.previewTitle","SurveyorVisitPreview",50,track.SequenceId,
                new[]{new ChapterStep(SurveyorVisit.StepId,false,SurveyorVisit.ReceiptStep),new ChapterStep(SurveyorVisit.ReceiptStep,false,SurveyorVisit.EndStep),new ChapterStep(SurveyorVisit.EndStep,true)}));
            var panel=c.Asset(Folder+"/VisitPanelSettings.asset",()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));panel.sortingOrder=4;EditorUtility.SetDirty(panel);
            var pausePanel=c.Asset(Folder+"/PausePanelSettings.asset",()=>Object.Instantiate(panel));pausePanel.sortingOrder=10;EditorUtility.SetDirty(pausePanel);
            var gold=Material(c,"Gold",new Color(.61f,.39f,.14f),.2f);var silver=Material(c,"Silver",new Color(.78f,.78f,.74f),.25f);var fabric=Material(c,"Fabric",new Color(.81f,.79f,.72f),0);
            var dark=Material(c,"Dark",new Color(.025f,.027f,.027f),0);var dust=Material(c,"Dust",new Color(.33f,.32f,.30f),0);dust.SetFloat("_Cull",0);EditorUtility.SetDirty(dust);
            var landerGold=Mesh(c,"LanderGold",SurveyorVisitGeometry.LanderGold);var cabin=Mesh(c,"Cabin",SurveyorVisitGeometry.LanderCabin);var windows=Mesh(c,"Windows",SurveyorVisitGeometry.LanderWindows);
            var boot=Mesh(c,"Boot",SurveyorVisitGeometry.Boot);var glove=Mesh(c,"Glove",SurveyorVisitGeometry.Glove);var body=Mesh(c,"ShadowBody",SurveyorVisitGeometry.ShadowBody);var grain=Mesh(c,"Grain",SurveyorVisitGeometry.Grain);
            var cast=c.Prefab(CastPath,root=>
            {
                Surface(Child(root,"Intrepid/Descent"),landerGold,gold);Surface(Child(root,"Intrepid/Cabin"),cabin,silver);Surface(Child(root,"Intrepid/Windows"),windows,dark);
                var people=Child(root,"Visitor");var left=Child(people,"LeftBoot");var right=Child(people,"RightBoot");Surface(left,boot,fabric);Surface(right,boot,fabric);
                var shadow=Surface(Child(people,"ShadowCaster"),body,fabric);shadow.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                var hand=Child(root,"Glove");Surface(hand,glove,fabric);
                var grains=new Transform[48];for(int i=0;i<grains.Length;i++){var g=Child(root,"Dust/Grain"+i);Surface(g,grain,dust).shadowCastingMode=ShadowCastingMode.Off;grains[i]=g.transform;}
                Get<SurveyorVisitActors>(root).Configure(Child(root,"Intrepid").transform,people.transform,left.transform,right.transform,hand.transform,grains,Vector3.zero,Vector3.zero,Vector3.zero,Vector3.zero,Vector3.zero);
            });
            var prefab=c.Prefab(Folder+"/SurveyorVisitPreview.prefab",root=>
            {
                Get<ChapterDirector>(root);Get<SessionClock>(root);Get<InputRouter>(root);Get<SurveyorVisit>(root);Get<SequenceDirector>(root).enabled=false;Get<SubtitlePresenter>(root);Get<FocusPause>(root);
                Nested(root,"LunarSite","Assets/_Game/Chapters/Surveyor/Art/LunarSite.prefab");var surveyor=Nested(root,"Surveyor","Assets/_Game/Chapters/Surveyor/Art/SurveyorVisibleRig.prefab");
                surveyor.transform.Find("Body/Scoop_Arm_1").localRotation=Quaternion.Euler(0,-65,0);
                var camera=Get<Camera>(Child(root,"Camera"));camera.transform.position=surveyor.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount").position;
                camera.nearClipPlane=.025f;camera.farClipPlane=360;camera.fieldOfView=25.3f;camera.aspect=4f/3;camera.allowHDR=camera.allowMSAA=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                Get<UniversalAdditionalCameraData>(camera.gameObject).renderPostProcessing=false;var cameras=Get<CameraDirector>(root);cameras.Initialize(camera);
                var source=Get<SurveyorSiteViewport>(camera.gameObject);source.Configure(camera);source.DrawToGameView=false;
                var direction=new Vector3(-170,0,60).normalized;var origin=camera.transform.position;var xz=origin+direction*180;
                var landing=SurveyorSiteGeometry.Ground(xz.x,xz.z,.02f);var nearXZ=origin+direction*2.7f;
                var near=SurveyorSiteGeometry.Ground(nearXZ.x,nearXZ.z,.015f);var farXZ=origin+direction*4.5f;var far=SurveyorSiteGeometry.Ground(farXZ.x,farXZ.z,.015f);
                var landingRig=Child(root,"LandingOptics").transform;landingRig.SetPositionAndRotation(origin,Quaternion.LookRotation(landing+Vector3.up*3.5f-origin,Vector3.up));
                var humanRig=Child(root,"HumanOptics").transform;humanRig.SetPositionAndRotation(origin,Quaternion.LookRotation(near+Vector3.up*.7f-origin,Vector3.up));camera.transform.rotation=landingRig.rotation;
                var actors=Nested(root,"ApolloVisitRig",CastPath).GetComponent<SurveyorVisitActors>();var people=actors.transform.Find("Visitor");people.rotation=Quaternion.LookRotation(-direction,Vector3.up);
                var hand=actors.transform.Find("Glove");actors.SetHandFrame(humanRig);
                var grains=new Transform[48];for(int i=0;i<48;i++)grains[i]=actors.transform.Find("Dust/Grain"+i);
                actors.Configure(actors.transform.Find("Intrepid"),people,people.Find("LeftBoot"),people.Find("RightBoot"),hand,grains,landing,far,near,new Vector3(.75f,.4f,1),new Vector3(.06f,.04f,.38f));
                actors.Lander.rotation=Quaternion.LookRotation(-direction,Vector3.up);actors.Hide();
                var sun=Get<Light>(Child(root,"Sun"));sun.type=LightType.Directional;sun.intensity=1.05f;sun.color=new Color(1,.96f,.87f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(8,55,0);
                var doc=Get<UIDocument>(Child(root,"VisitUI"));doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder+"/VisitPanel.uxml");
                var pause=Nested(root,"Pause","Assets/_Game/UI/Pause/PauseMenu.prefab").GetComponent<PauseMenu>();pause.GetComponent<UIDocument>().panelSettings=pausePanel;
                Get<SurveyorVisitPreview>(root).Configure(chapter,track,ending,actors,cameras,landingRig,humanRig,source,doc,AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/VisitTexts.json"),pause);
            });
            c.PrefabInstance(c.Root.transform,prefab,"SurveyorVisit");RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.065f,.072f,.082f);
        }
        public override void Validate(SceneBuildContext c,List<string> errors)
        {errors.AddRange(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(TrackPath).Validate());errors.AddRange(AssetDatabase.LoadAssetAtPath<SurveyorEnding>(EndingPath).Validate());var loc=new LocalizationService();errors.AddRange(loc.AddTable(AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/VisitTexts.json").text,"visit"));if(c.Root.GetComponentsInChildren<AudioListener>(true).Length!=1)errors.Add("One AudioListener required.");}
        static Material Material(SceneBuildContext c,string name,Color color,float metal){var m=c.Material(Folder+"/Generated/"+name+".mat","Universal Render Pipeline/Lit");m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.22f);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
        static Mesh Mesh(SceneBuildContext c,string name,Action<Mesh> build){var mesh=c.Asset(Folder+"/Generated/"+name+".asset",()=>new Mesh{name=name});build(mesh);EditorUtility.SetDirty(mesh);return mesh;}
        static MeshRenderer Surface(GameObject go,Mesh mesh,Material material){Get<MeshFilter>(go).sharedMesh=mesh;var r=Get<MeshRenderer>(go);r.sharedMaterial=material;return r;}
        static T Get<T>(GameObject obj)where T:Component{var c=obj.GetComponent<T>();return c!=null?c:obj.AddComponent<T>();}
        static GameObject Child(GameObject root,string name){var parts=name.Split('/');var current=root;foreach(var p in parts){var old=current.transform.Find(p);if(old!=null)current=old.gameObject;else{var go=new GameObject(p);go.transform.SetParent(current.transform,false);current=go;}}return current;}
        static GameObject Nested(GameObject root,string name,string path){var old=root.transform.Find(name);if(old!=null)return old.gameObject;var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.scene);go.name=name;go.transform.SetParent(root.transform,false);return go;}
    }
}
