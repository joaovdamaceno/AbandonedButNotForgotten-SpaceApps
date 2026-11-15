using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class SurveyorEclipseBuilder : SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Sequences/Eclipse";
        public const string SequencePath="Assets/_Game/Chapters/Surveyor/Sequences/SurveyorEclipse.asset";
        public const string EarthPrefab="Assets/_Game/Chapters/Surveyor/Art/EarthEclipse.prefab";
        public const string PrefabPath=Folder+"/SurveyorEclipsePreview.prefab";
        public const string PreviewScene=Folder+"/Preview/SurveyorEclipsePreview.unity";
        public override string Id=>"surveyor.eclipse";
        public override string Owner=>"R6 · SUR-011";
        public override string ScenePath=>PreviewScene;
        public override string RootName=>"[R6 SUR-011] Earth eclipses the Sun";
        public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor";
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-011 Reconstruir eclipse")]
        public static void BuildFromMenu()
        {
            var result=SpaceAppsSceneBuilder.Run(new SurveyorEclipseBuilder());
            if(!result.Succeeded) throw new InvalidOperationException(result.ToString());
            Debug.Log(result);
        }
        public static void BuildFromCommandLine()
        { try { BuildFromMenu(); EditorApplication.Exit(0); } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
        public static SequenceDefinition CreateReference()
        {
            ShotDefinition Shot(string id,float seconds,string rig,bool continuity=false) => new ShotDefinition(id,seconds,rig,
                new[]{new ShotKeyframe(0,Vector3.zero,Vector3.zero,25.3f),new ShotKeyframe(seconds,Vector3.zero,Vector3.zero,25.3f)},
                minGroundClearanceMeters:0,continueFromPreviousShot:continuity);
            return SequenceDefinition.Create("surveyor.seq.Eclipse",new[]{
                Shot("surveyor.eclipse.raise",4,SurveyorEclipse.SkyRigId,true),
                Shot("surveyor.eclipse.observe",18,SurveyorEclipse.SkyRigId),
                Shot("surveyor.eclipse.return",4,SurveyorEclipse.GroundRigId,true)},
                new[]{new SubtitleCue("SUR-S20","SUR-S20",2,8),new SubtitleCue("SUR-S21","SUR-S21",8,14)});
        }
        public override void Build(SceneBuildContext context)
        {
            var reference=CreateReference(); reference.name="SurveyorEclipse";
            var track=context.Asset(SequencePath,()=>reference);
            if(track!=reference) { EditorUtility.CopySerialized(reference,track); Object.DestroyImmediate(reference); } EditorUtility.SetDirty(track);
            var material=context.Material(Folder+"/EarthOccultation.mat","Abandoned/Surveyor/EarthOccultation");
            var earth=context.Prefab(EarthPrefab,root=>
            {
                var card=Child(root,"AngularSkyCard");
                var filter=Get<MeshFilter>(card);
                if(filter.sharedMesh==null)
                { var quad=GameObject.CreatePrimitive(PrimitiveType.Quad); filter.sharedMesh=quad.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(quad); }
                var renderer=Get<MeshRenderer>(card); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
                Get<SurveyorEclipseImage>(root).Configure(renderer);
            });
            var chapter=context.Asset(Folder+"/Preview/EclipseChapter.asset",()=>ChapterDefinition.Create("surveyor.eclipse.preview","sur.eclipse.title","sur.eclipse.description","sur.eclipse.objective",
                "SurveyorEclipsePreview",29,"surveyor.seq.Eclipse",new[]{new ChapterStep(SurveyorEclipse.StepId,false,SurveyorEclipse.NextStepId),new ChapterStep(SurveyorEclipse.NextStepId,true)}));
            var panel=context.Asset(Folder+"/EclipsePanelSettings.asset",()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));
            panel.sortingOrder=4; EditorUtility.SetDirty(panel);
            var scan=AssetDatabase.LoadAssetAtPath<Material>(SurveyorTelevisionBuilder.MaterialPath);
            var prefab=context.Prefab(PrefabPath,root=>
            {
                Get<ChapterDirector>(root); Get<SessionClock>(root); Get<InputRouter>(root); Get<InteractionService>(root);
                Get<SurveyorCommandDelay>(root); Get<SurveyorCommandInteraction>(root); Get<SurveyorEclipse>(root); Get<SurveyorTelevisionOutput>(root); Get<SubtitlePresenter>(root);
                Get<SequenceDirector>(root).enabled=false;
                var surface=Child(root,"Surface"); Nested(surface,"LunarSite","Assets/_Game/Chapters/Surveyor/Art/LunarSite.prefab");
                var rig=Nested(surface,"Surveyor","Assets/_Game/Chapters/Surveyor/Art/SurveyorVisibleRig.prefab");
                var mount=rig.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount"); var anchors=rig.transform.Find("Anchors");
                var camera=Get<Camera>(Child(surface,"TelevisionCamera")); camera.enabled=true;
                camera.transform.position=mount.position;
                camera.transform.rotation=Quaternion.LookRotation(anchors.Find("Trench").position-camera.transform.position,Vector3.up);
                camera.fieldOfView=25.3f; camera.nearClipPlane=.025f; camera.farClipPlane=360; camera.aspect=4f/3;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black; camera.allowHDR=false; camera.allowMSAA=false;
                var cameraData=Get<UniversalAdditionalCameraData>(camera.gameObject); cameraData.renderPostProcessing=false; cameraData.renderShadows=true;
                var cameraDirector=Get<CameraDirector>(root); cameraDirector.Initialize(camera);
                var viewport=Get<SurveyorSiteViewport>(camera.gameObject); viewport.Configure(camera); viewport.DrawToGameView=false;
                var bindings=Get<SurveyorSiteBindings>(surface); bindings.Configure(cameraDirector,mount,rig.transform.Find("Body/TV_Mirror_Azimuth"),
                    rig.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation"),rig.transform.Find("Body/Scoop_Arm_1"),rig.transform.Find("Body/Scoop_Arm_1/Scoop_Arm_2/Scoop"),
                    Enumerable.Range(0,anchors.childCount).Select(i=>new SurveyorSiteBindings.Target { id="SUR."+anchors.GetChild(i).name,anchor=anchors.GetChild(i) }).ToArray());
                var sky=Nested(root,"EarthEclipse",EarthPrefab).GetComponent<SurveyorEclipseImage>(); sky.Show(false);
                var sun=Get<Light>(Child(surface,"Sun")); sun.type=LightType.Directional; sun.intensity=1.15f; sun.color=new Color(1,.965f,.91f);
                sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(23,145,0);
                var doc=Get<UIDocument>(Child(root,"EclipseUI")); doc.panelSettings=panel; doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Folder+"/EclipsePanel.uxml");
                Get<ResponsiveLayoutClass>(doc.gameObject);
                var sound=Get<AudioSource>(root); sound.playOnAwake=false; sound.loop=false; sound.spatialBlend=0;
                sound.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(SurveyorObservationBuilder.Folder+"/Audio/RecordConfirm.wav");
                Get<SurveyorEclipsePreview>(root).Configure(chapter,track,bindings,sky,Child(root,"SkyPointing").transform,Child(root,"GroundPointing").transform,viewport,scan,doc,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/EclipseTexts.json"),sound);
                Get<SurveyorEclipseWebSmoke>(root);
            });
            context.PrefabInstance(context.Root.transform,prefab,"SurveyorEclipse");
            RenderSettings.skybox=null; RenderSettings.fog=false; RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.075f,.088f,.11f);
        }
        public override void Validate(SceneBuildContext context,List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath).Validate());
            var texts=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/EclipseTexts.json"); var loc=new LocalizationService(); errors.AddRange(loc.AddTable(texts.text,texts.name));
            if(context.Root.GetComponentsInChildren<AudioListener>(true).Length!=1) errors.Add("Exactly one AudioListener required.");
            if(context.Root.GetComponentInChildren<SurveyorEclipsePreview>()==null) errors.Add("Missing executable preview.");
        }
        static T Get<T>(GameObject obj) where T:Component
        { var component=obj.GetComponent<T>(); return component!=null?component:obj.AddComponent<T>(); }
        static GameObject Child(GameObject root,string name)
        { var old=root.transform.Find(name); if(old!=null) return old.gameObject; var go=new GameObject(name); go.transform.SetParent(root.transform,false); return go; }
        static GameObject Nested(GameObject root,string name,string path)
        { var old=root.transform.Find(name); if(old!=null) return old.gameObject; var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.scene); go.name=name; go.transform.SetParent(root.transform,false); return go; }
    }
}
