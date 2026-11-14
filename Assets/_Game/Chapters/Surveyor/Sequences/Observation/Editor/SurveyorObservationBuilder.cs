using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorObservationBuilder : SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Sequences/Observation";
        public const string TargetsPath="Assets/_Game/Chapters/Surveyor/Data/SurveyorObservationTargets.asset";
        public const string PreviewScene=Folder+"/Preview/SurveyorObservationPreview.unity", PrefabPath=Folder+"/SurveyorObservationPreview.prefab";
        public override string Id=>"surveyor.observation";
        public override string Owner=>"R6 · SUR-008";
        public override string ScenePath=>PreviewScene;
        public override string RootName=>"[R6 SUR-008] Three observations";
        public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor";
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-008 Reconstruir observações")]
        public static void BuildFromMenu(){var result=SpaceAppsSceneBuilder.Run(new SurveyorObservationBuilder());if(!result.Succeeded)throw new InvalidOperationException(result.ToString());Debug.Log(result);}
        public static void BuildFromCommandLine(){try{BuildFromMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public override void Build(SceneBuildContext context)
        {
            var reference=SurveyorObservationTargets.CreateReference();
            var targets=context.Asset(TargetsPath,()=>reference);
            if(targets!=reference){EditorUtility.CopySerialized(reference,targets);Object.DestroyImmediate(reference);}EditorUtility.SetDirty(targets);
            var chapter=context.Asset(Folder+"/Preview/ObservationChapter.asset",()=>ChapterDefinition.Create("surveyor.observation.preview","obs.title","obs.description","obs.question","SurveyorObservationPreview",90,"surveyor.seq.Descent",
                new[]{new ChapterStep("Descent",false,"FirstImage"),new ChapterStep("FirstImage",false,"Observe"),new ChapterStep("Observe",false,"Sample"),new ChapterStep("Sample",true)}));
            var panel=context.Asset(Folder+"/ObservationPanelSettings.asset",()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));
            panel.sortingOrder=5;EditorUtility.SetDirty(panel);
            var prefab=context.Prefab(PrefabPath,root=>
            {
                var nested=root.transform.Find("FirstImage");
                if(nested==null)
                {
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SurveyorTelevisionBuilder.PrefabPath),root.scene);
                    instance.name="FirstImage";instance.transform.SetParent(root.transform,false);nested=instance.transform;
                }
                var before=nested.GetComponent<SurveyorTelevisionPreviewHost>();
                var serialized=new SerializedObject(before);serialized.FindProperty("chapter").objectReferenceValue=chapter;serialized.ApplyModifiedPropertiesWithoutUndo();
                var smoke=nested.GetComponent<SurveyorTelevisionWebSmoke>();if(smoke!=null)smoke.enabled=false;
                var child=root.transform.Find("ObservationUI");
                if(child==null){var go=new GameObject("ObservationUI");go.transform.SetParent(root.transform,false);child=go.transform;}
                var doc=Get<UIDocument>(child.gameObject);doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder+"/ObservationPanel.uxml");
                Get<ResponsiveLayoutClass>(child.gameObject);
                var sound=Get<AudioSource>(root);sound.playOnAwake=false;sound.loop=false;sound.spatialBlend=0;sound.volume=.35f;
                sound.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Audio/RecordConfirm.wav");
                var host=Get<SurveyorObservationPreviewHost>(root);host.Configure(before,Get<SurveyorObservation>(root),targets,Get<SurveyorObservationView>(child.gameObject),doc,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/ObservationTexts.json"),sound);
                Get<SurveyorObservationWebSmoke>(root).Configure(host);
            });
            context.PrefabInstance(context.Root.transform,prefab,"SurveyorObservation");
            RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.075f,.088f,.11f);
        }
        public override void Validate(SceneBuildContext context,List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<SurveyorObservationTargets>(TargetsPath).Validate());
            errors.AddRange(SurveyorLaunchTexts.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/ObservationTexts.json")).Validate());
            if(context.Root.GetComponentsInChildren<AudioListener>(true).Length!=1)errors.Add("One AudioListener required.");
            var host=context.Root.GetComponentInChildren<SurveyorObservationPreviewHost>();
            if(host==null||host.Flow==null)errors.Add("Observation references missing.");
        }
        static T Get<T>(GameObject go)where T:Component{var component=go.GetComponent<T>();return component!=null?component:go.AddComponent<T>();}
    }
}
