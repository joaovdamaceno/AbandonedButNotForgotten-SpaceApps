using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorTelevisionBuilder:SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Sequences/Television",UiFolder="Assets/_Game/Chapters/Surveyor/UI";
        public const string SequencePath="Assets/_Game/Chapters/Surveyor/Sequences/FirstImage.asset";
        public const string PreviewScene=Folder+"/Preview/SurveyorFirstImagePreview.unity",PrefabPath=Folder+"/SurveyorFirstImagePreview.prefab",MaterialPath=Folder+"/SurveyorScan.mat";
        public override string Id=>"surveyor.television";public override string Owner=>"R6 · SUR-007";
        public override string ScenePath=>PreviewScene;public override string RootName=>"[R6 SUR-007] First image";public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor/Sequences";
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-007 Reconstruir primeira imagem")]
        public static void BuildFromMenu(){var r=SpaceAppsSceneBuilder.Run(new SurveyorTelevisionBuilder());if(!r.Succeeded)throw new InvalidOperationException(r.ToString());Debug.Log(r.ToString());}
        public static void BuildFromCommandLine(){try{BuildFromMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static SequenceDefinition CreateReference()
        {
            ShotDefinition Shot(string id,float duration,params ShotEvent[] events)=>new ShotDefinition(id,duration,CameraDirector.WorldRigId,
                new[]{new ShotKeyframe(0,new Vector3(0,3,-10),Vector3.zero,45),new ShotKeyframe(duration,new Vector3(0,3,-10),Vector3.zero,45)},events:events);
            return SequenceDefinition.Create("surveyor.seq.FirstImage",new[]{Shot("surveyor.tv.scan",4,new ShotEvent(4,SurveyorTelevision.ReadyEvent)),Shot("surveyor.tv.contemplate",6)},
                new[]{new SubtitleCue("SUR-S10","SUR-S10",4,10)});
        }
        public override void Build(SceneBuildContext c)
        {
            var template=CreateReference();template.name="FirstImage";var track=c.Asset(SequencePath,()=>template);
            if(track!=template){EditorUtility.CopySerialized(template,track);Object.DestroyImmediate(template);}EditorUtility.SetDirty(track);
            var chapter=c.Asset(Folder+"/Preview/TelevisionChapter.asset",()=>ChapterDefinition.Create("surveyor.television.preview","tv.title","tv.description","tv.question","SurveyorFirstImagePreview",53.3f,"surveyor.seq.Descent",
                new[]{new ChapterStep("Descent",false,"FirstImage"),new ChapterStep("FirstImage",false,"Observe"),new ChapterStep("Observe",false,"End"),new ChapterStep("End",true)}));
            var panel=c.Asset(Folder+"/TelevisionPanelSettings.asset",()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));
            var landingPanel=c.Asset(Folder+"/LandingPanelSettings.asset",()=>Object.Instantiate(panel));panel.sortingOrder=2;EditorUtility.SetDirty(panel);
            var material=c.Asset(MaterialPath,()=>new Material(Shader.Find("Abandoned/Surveyor/Scan")));
            var prefab=c.Prefab(PrefabPath,root=>
            {
                var owner=Get<ChapterDirector>(root);var clock=Get<SessionClock>(root);var input=Get<InputRouter>(root);var service=Get<InteractionService>(root);var cameraDirector=Get<CameraDirector>(root);
                var sequence=Get<SequenceDirector>(root);sequence.enabled=false;var queue=Get<SurveyorCommandDelay>(root);var action=Get<SurveyorCommandInteraction>(root);
                var descent=Get<SurveyorDescent>(root);var gate=Get<SurveyorDescentCameraGate>(root);var flow=Get<SurveyorTelevision>(root);var output=Get<SurveyorTelevisionOutput>(root);var host=Get<SurveyorTelevisionPreviewHost>(root);
                var main=Get<Camera>(Child(root,"TelemetryCamera"));main.cullingMask=0;main.clearFlags=CameraClearFlags.SolidColor;main.backgroundColor=new Color(.03f,.047f,.075f);Get<AudioListener>(main.gameObject);cameraDirector.Initialize(main);
                var surface=Child(root,"LunarSurface");Nested(surface,"LunarSite","Assets/_Game/Chapters/Surveyor/Art/LunarSite.prefab");var rig=Nested(surface,"Surveyor","Assets/_Game/Chapters/Surveyor/Art/SurveyorVisibleRig.prefab");
                var mount=rig.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount");var anchors=rig.transform.Find("Anchors");
                var tv=Get<Camera>(Child(surface,"TelevisionCamera"));tv.enabled=false;tv.transform.position=mount.position;tv.nearClipPlane=.025f;tv.farClipPlane=360;tv.clearFlags=CameraClearFlags.SolidColor;tv.backgroundColor=Color.black;
                Abandoned.SpaceApps.Chapters.Surveyor.EditorTools.SurveyorSiteBuilder.Frame(tv,anchors.Find("LandingMarks"),25.3f);
                var viewport=Get<SurveyorSiteViewport>(tv.gameObject);viewport.enabled=false;viewport.DrawToGameView=false;viewport.Configure(tv);
                Get<SurveyorSiteBindings>(surface).Configure(cameraDirector,mount,rig.transform.Find("Body/TV_Mirror_Azimuth"),rig.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation"),
                    rig.transform.Find("Body/Scoop_Arm_1"),rig.transform.Find("Body/Scoop_Arm_1/Scoop_Arm_2/Scoop"),Enumerable.Range(0,anchors.childCount).Select(i=>new SurveyorSiteBindings.Target{id="SUR."+anchors.GetChild(i).name,anchor=anchors.GetChild(i)}).ToArray());
                var sun=Get<Light>(Child(surface,"Sun"));sun.type=LightType.Directional;sun.intensity=1.15f;sun.color=new Color(1,.965f,.91f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(23,145,0);
                var landingUi=Child(root,"LandingUI");var landingDoc=Get<UIDocument>(landingUi);landingDoc.panelSettings=landingPanel;landingDoc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiFolder+"/DescentTelemetry.uxml");Get<ResponsiveLayoutClass>(landingUi);
                var ui=Child(root,"TelevisionUI");var doc=Get<UIDocument>(ui);doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiFolder+"/TelevisionPanel.uxml");Get<ResponsiveLayoutClass>(ui);
                host.Configure(owner,clock,input,service,sequence,cameraDirector,queue,action,descent,gate,Get<SurveyorDescentView>(landingUi),flow,output,Get<SurveyorTelevisionView>(ui),landingDoc,doc,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder+"/DescentTexts.json"),AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder+"/TelevisionTexts.json"),chapter,
                    AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SurveyorDescentBuilder.SequencePath),track,Child(root,"AltitudeIndicator").transform,tv,viewport,material);
                Get<SurveyorTelevisionWebSmoke>(root).Configure(host);
            });
            c.PrefabInstance(c.Root.transform,prefab,"SurveyorFirstImage");RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.075f,.088f,.11f);
        }
        public override void Validate(SceneBuildContext c,List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath).Validate());errors.AddRange(SurveyorLaunchTexts.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder+"/TelevisionTexts.json")).Validate());
            if(c.Root.GetComponentsInChildren<AudioListener>(true).Length!=1)errors.Add("One AudioListener required.");
            if(c.Root.GetComponentInChildren<SurveyorSiteViewport>(true).enabled)errors.Add("TV must start disabled.");
        }
        static T Get<T>(GameObject go)where T:Component{var c=go.GetComponent<T>();return c!=null?c:go.AddComponent<T>();}
        static GameObject Child(GameObject root,string name){var t=root.transform.Find(name);if(t!=null)return t.gameObject;var g=new GameObject(name);g.transform.SetParent(root.transform,false);return g;}
        static GameObject Nested(GameObject root,string name,string path){var old=root.transform.Find(name);if(old!=null)return old.gameObject;var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.scene);g.name=name;g.transform.SetParent(root.transform,false);return g;}
    }
}
