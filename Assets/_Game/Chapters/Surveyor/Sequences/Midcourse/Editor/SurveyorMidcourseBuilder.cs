using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorMidcourseBuilder:SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Sequences/Midcourse";
        public const string UiFolder="Assets/_Game/Chapters/Surveyor/UI";
        public const string SequencePath="Assets/_Game/Chapters/Surveyor/Sequences/SurveyorCruise.asset";
        public const string PreviewScene=Folder+"/Preview/SurveyorMidcoursePreview.unity";
        public const string PrefabPath=Folder+"/SurveyorMidcoursePreview.prefab";
        public override string Id=>"surveyor.midcourse";
        public override string Owner=>"R6 · SUR-005";
        public override string ScenePath=>PreviewScene;
        public override string RootName=>"[R6 SUR-005] Midcourse";
        public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor/Sequences";
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-005 Reconstruir correção de meio de curso")]
        public static void BuildFromMenu()
        {var r=SpaceAppsSceneBuilder.Run(new SurveyorMidcourseBuilder());if(!r.Succeeded)throw new InvalidOperationException(r.ToString());Debug.Log(r.ToString());}
        public static void BuildFromCommandLine()
        {try{BuildFromMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public override void Build(SceneBuildContext c)
        {
            var template=CreateReference();template.name="SurveyorCruise";
            var reference=c.Asset(SequencePath,()=>template);
            if(reference!=template){EditorUtility.CopySerialized(template,reference);Object.DestroyImmediate(template);}EditorUtility.SetDirty(reference);
            var chapter=c.Asset(Folder+"/Preview/MidcourseChapter.asset",()=>ChapterDefinition.Create("surveyor.midcourse.preview",
                "mid.title","mid.description","mid.question","SurveyorMidcoursePreview",reference.TotalDurationSeconds,reference.SequenceId,
                new[]{new ChapterStep("Cruise",false,"Descent"),new ChapterStep("Descent",false,"End"),new ChapterStep("End",true)}));
            var panel=c.Asset(Folder+"/MidcoursePanelSettings.asset",()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));
            var prefab=c.Prefab(PrefabPath,root=>
            {
                var owner=Get<ChapterDirector>(root);var clock=Get<SessionClock>(root);var input=Get<InputRouter>(root);var service=Get<InteractionService>(root);
                var cameraDirector=Get<CameraDirector>(root);var sequence=Get<SequenceDirector>(root);sequence.enabled=false;
                var queue=Get<SurveyorCommandDelay>(root);var action=Get<SurveyorCommandInteraction>(root);
                var flow=Get<SurveyorMidcourse>(root);var view=Get<SurveyorMidcourseView>(root);var host=Get<SurveyorMidcoursePreviewHost>(root);
                var child=root.transform.Find("Camera");if(child==null){child=new GameObject("Camera").transform;child.SetParent(root.transform,false);}
                var camera=Get<Camera>(child.gameObject);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.047f,.075f);
                camera.cullingMask=0;camera.fieldOfView=45;Get<AudioListener>(child.gameObject);cameraDirector.Initialize(camera);
                var doc=Get<UIDocument>(root);doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiFolder+"/MidcoursePanel.uxml");
                Get<ResponsiveLayoutClass>(root);
                host.Configure(owner,clock,input,service,cameraDirector,sequence,queue,action,flow,view,doc,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder+"/MidcourseTexts.json"),chapter,reference);
            });
            c.PrefabInstance(c.Root.transform,prefab,"SurveyorMidcourse");
        }
        public override void Validate(SceneBuildContext c,List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath).Validate());
            errors.AddRange(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(Folder+"/Preview/MidcourseChapter.asset").Validate());
            errors.AddRange(SurveyorLaunchTexts.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder+"/MidcourseTexts.json")).Validate());
            if(c.Root.GetComponentsInChildren<AudioListener>(true).Length!=1)errors.Add("Listener duplicado/ausente.");
        }
        public static SequenceDefinition CreateReference()
        {
            ShotDefinition Shot(string id,float duration,params ShotEvent[] events)=>new ShotDefinition(id,duration,CameraDirector.WorldRigId,
                new[]{new ShotKeyframe(0,new Vector3(0,3,-10),Vector3.zero,45),new ShotKeyframe(duration,new Vector3(0,3,-10),Vector3.zero,45)},events:events);
            return SequenceDefinition.Create("surveyor.seq.Cruise",new[]{Shot("surveyor.cruise.travel",10),Shot("surveyor.cruise.guidance",14),
                Shot("surveyor.cruise.burn",8),Shot("surveyor.cruise.descent",5,new ShotEvent(0,SurveyorMidcourse.HandoffEvent))},
                new[]{new SubtitleCue("SUR-S04","SUR-S04",0,6),new SubtitleCue("SUR-S05","SUR-S05",10,16),new SubtitleCue("SUR-S06","SUR-S06",33,37)});
        }
        static T Get<T>(GameObject go)where T:Component{var t=go.GetComponent<T>();return t!=null?t:go.AddComponent<T>();}
    }
}
