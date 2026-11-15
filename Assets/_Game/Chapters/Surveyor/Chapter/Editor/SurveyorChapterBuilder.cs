using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class SurveyorChapterBuilder:SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Chapter";
        public const string Scene="Assets/_Game/Chapters/Surveyor/Scenes/SurveyorChapter.unity",ChapterPath=Folder+"/SurveyorChapter.asset";
        public override string Id=>"surveyor.chapter";public override string Owner=>"R6 · SUR-015";public override string ScenePath=>Scene;public override string RootName=>"[R6] Surveyor full chapter";public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor";
        [MenuItem("Abandoned Space Apps/Surveyor/Reconstruir capítulo completo")]
        public static void BuildMenu(){SurveyorArtPolish.Apply();var r=SpaceAppsSceneBuilder.Run(new SurveyorChapterBuilder());if(!r.Succeeded)throw new InvalidOperationException(r.ToString());var a=SpaceAppsSceneBuilder.Run(new SurveyorArchiveBuilder());if(!a.Succeeded)throw new InvalidOperationException(a.ToString());Debug.Log(r);Debug.Log(a);}
        public static void BuildFromCommandLine(){try{BuildMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public override void Build(SceneBuildContext c)
        {
            var names=new[]{"Launch","Cruise","Descent","FirstImage","Observe","Sample","Eclipse","LunarNight","TimePassage","ImaginedVisit","HistoricalReceipt","End"};
            var template=ChapterDefinition.Create("surveyor3","sur.chapter.title","sur.chapter.description","sur.chapter.question","SurveyorChapter",360,"surveyor.seq.Launch",names.Select((n,i)=>new ChapterStep(n,i==names.Length-1,i==names.Length-1?Array.Empty<string>():new[]{names[i+1]})));
            var chapter=c.Asset(ChapterPath,()=>template);if(chapter!=template){EditorUtility.CopySerialized(template,chapter);Object.DestroyImmediate(template);}EditorUtility.SetDirty(chapter);
            var panel=c.Asset(Folder+"/ChapterPanelSettings.asset",()=>Object.Instantiate(Load<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));panel.sortingOrder=4;EditorUtility.SetDirty(panel);
            var overlayPanel=c.Asset(Folder+"/ChapterOverlaySettings.asset",()=>Object.Instantiate(panel));overlayPanel.sortingOrder=8;EditorUtility.SetDirty(overlayPanel);
            var pausePanel=c.Asset(Folder+"/ChapterPauseSettings.asset",()=>Object.Instantiate(panel));pausePanel.sortingOrder=20;EditorUtility.SetDirty(pausePanel);
            var audio=c.Asset(Folder+"/SurveyorAudio.asset",()=>ScriptableObject.CreateInstance<AudioCatalog>());audio.Set(new[]{new AudioCueDefinition("MUS_SUR_AVESSO",AudioBus.Music,true,.32f,clips:Music("Departure")),new AudioCueDefinition("MUS_SUR_MAQUINA",AudioBus.Music,true,.25f,clips:Music("Science")),new AudioCueDefinition("MUS_SUR_HUMANO",AudioBus.Music,true,.40f,clips:Music("Return"))});EditorUtility.SetDirty(audio);
            var prefab=c.Prefab(Folder+"/SurveyorChapter.prefab",root=>
            {
                var stage=Nested(root,"FlightAndSurface",SurveyorSoilBuilder.Folder+"/SurveyorSoilPreview.prefab");stage.GetComponent<SurveyorSoilPreview>().enabled=false;
                foreach(var t in new[]{typeof(ChapterDirector),typeof(SessionClock),typeof(InputRouter),typeof(InteractionService)})((Behaviour)stage.GetComponent(t)).enabled=false;
                var host=Get<SurveyorChapterHost>(stage);Get<SurveyorChapterSubtitles>(stage);Get<AudioDirector>(stage);Get<SurveyorChapterAudio>(stage).Configure(audio);
                foreach(var t in new[]{typeof(SurveyorLaunchController),typeof(SurveyorMidcourse),typeof(SurveyorDescent),typeof(SurveyorDescentCameraGate),typeof(SurveyorTelevision),typeof(SurveyorObservation),typeof(SurveyorCommandDelay),typeof(SurveyorCommandInteraction),typeof(SurveyorEclipse),typeof(SurveyorNight),typeof(SurveyorVisit),typeof(SurveyorDescentAudio)})if(stage.GetComponent(t)==null)stage.AddComponent(t);
                Get<SequenceDirector>(stage).enabled=false;
                var data=new SerializedObject(host);Set(data,"chapter",chapter);Set(data,"subtitleData",Load<SurveyorSubtitles>("Assets/_Game/Chapters/Surveyor/Data/SurveyorSubtitles.asset"));Set(data,"chapterTexts",Load<TextAsset>(Folder+"/ChapterTexts.json"));
                SetArray(data,"tracks",new[]{"SurveyorLaunch","SurveyorCruise","SurveyorDescent","FirstImage","SurveyorEclipse","SurveyorNight","SurveyorTimePassage","SurveyorVisit"}.Select(n=>(Object)Load<SequenceDefinition>("Assets/_Game/Chapters/Surveyor/Sequences/"+n+".asset")).ToArray());
                string ui="Assets/_Game/Chapters/Surveyor/UI/";
                var textPaths=new[]{ui+"LaunchTexts.json",ui+"MidcourseTexts.json",ui+"DescentTexts.json",ui+"TelevisionTexts.json",SurveyorObservationBuilder.Folder+"/ObservationTexts.json",SurveyorSoilBuilder.Folder+"/SoilTexts.json",SurveyorEclipseBuilder.Folder+"/EclipseTexts.json","Assets/_Game/Chapters/Surveyor/Sequences/Night/NightTexts.json","Assets/_Game/Chapters/Surveyor/Sequences/Visit/VisitTexts.json"};
                SetArray(data,"viewTexts",textPaths.Select(p=>(Object)Load<TextAsset>(p)).ToArray());
                var surveyor=stage.transform.Find("Surveyor");var anchors=surveyor.Find("Anchors");var mount=surveyor.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount");
                var tv=stage.transform.Find("Camera").GetComponent<Camera>();tv.transform.position=mount.position;SurveyorSiteBuilder.Frame(tv,anchors.Find("LandingMarks"),25.3f);tv.enabled=false;var viewport=tv.GetComponent<SurveyorSiteViewport>();viewport.enabled=false;
                var originalTrack=Load<SequenceDefinition>("Assets/_Game/Chapters/Surveyor/Sequences/FirstImage.asset");
                var firstImage=c.Asset(Folder+"/FirstImageOptical.asset",()=>Object.Instantiate(originalTrack));EditorUtility.CopySerialized(originalTrack,firstImage);firstImage.name="FirstImageOptical";
                var opticalData=new SerializedObject(firstImage);var shots=opticalData.FindProperty("shots");for(int i=0;i<shots.arraySize;i++){var frames=shots.GetArrayElementAtIndex(i).FindPropertyRelative("keyframes");for(int k=0;k<frames.arraySize;k++){var frame=frames.GetArrayElementAtIndex(k);frame.FindPropertyRelative("positionMeters").vector3Value=tv.transform.position;frame.FindPropertyRelative("eulerAnglesDegrees").vector3Value=tv.transform.eulerAngles;frame.FindPropertyRelative("fieldOfViewDegrees").floatValue=tv.fieldOfView;}}opticalData.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(firstImage);data.FindProperty("tracks").GetArrayElementAtIndex(3).objectReferenceValue=firstImage;
                var telemetry=Get<Camera>(Child(stage,"TelemetryCamera"));telemetry.enabled=true;telemetry.cullingMask=0;telemetry.clearFlags=CameraClearFlags.SolidColor;telemetry.backgroundColor=new Color(.02f,.035f,.055f);Get<AudioListener>(telemetry.gameObject);
                var cameras=stage.GetComponent<CameraDirector>();cameras.Initialize(telemetry);
                var bindings=Get<SurveyorSiteBindings>(stage);bindings.Configure(cameras,mount,surveyor.Find("Body/TV_Mirror_Azimuth"),surveyor.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation"),surveyor.Find("Body/Scoop_Arm_1"),surveyor.Find("Body/Scoop_Arm_1/Scoop_Arm_2/Scoop"),Enumerable.Range(0,anchors.childCount).Select(i=>new SurveyorSiteBindings.Target{id="SUR."+anchors.GetChild(i).name,anchor=anchors.GetChild(i)}).ToArray());
                Set(data,"site",bindings);Set(data,"cameras",cameras);Set(data,"telemetryCamera",telemetry);Set(data,"televisionCamera",tv);Set(data,"viewport",viewport);Set(data,"scan",Load<Material>(SurveyorTelevisionBuilder.MaterialPath));
                var terrainMaterial=Load<Material>(SurveyorArtPolish.Folder+"/Regolith.mat");foreach(var renderer in stage.GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterial!=null&&(renderer.sharedMaterial.name=="Regolith"||renderer.sharedMaterial.name=="TrenchSoil"))renderer.sharedMaterial=terrainMaterial;
                var soilConfig=new SerializedObject(stage.GetComponent<SurveyorSoilPreview>());foreach(var pair in new[]{new[]{"trench","trench"},new[]{"arm","arm"},new[]{"replacedGround","ground"},new[]{"pressMark","pressMark"},new[]{"impactMark","impactMark"},new[]{"montageMarks","montageMarks"},new[]{"soilResults","results"}})Set(data,pair[0],soilConfig.FindProperty(pair[1]).objectReferenceValue);
                var parts=soilConfig.FindProperty("native");SetArray(data,"nativeParts",Enumerable.Range(0,parts.arraySize).Select(i=>parts.GetArrayElementAtIndex(i).objectReferenceValue).ToArray());
                Set(data,"targets",Load<SurveyorObservationTargets>(SurveyorObservationBuilder.TargetsPath));Set(data,"ending",Load<SurveyorEnding>(SurveyorVisitBuilder.EndingPath));
                var sunlight=stage.transform.Find("Sun").GetComponent<Light>();sunlight.shadowNormalBias=.08f;sunlight.shadowBias=.015f;Set(data,"sun",sunlight);
                foreach(var name in new[]{"nightRig","skyRig","groundRig","landingRig","humanRig"})Set(data,name,Child(stage,name).transform);
                var eclipse=Nested(stage,"EarthEclipse",SurveyorEclipseBuilder.EarthPrefab).GetComponent<SurveyorEclipseImage>();eclipse.Show(false);Set(data,"eclipseImage",eclipse);
                var actors=Nested(stage,"ApolloVisitRig",SurveyorVisitBuilder.CastPath).GetComponent<SurveyorVisitActors>();var direction=new Vector3(-170,0,60).normalized;var origin=tv.transform.position;var far=origin+direction*180;var landing=SurveyorSiteGeometry.Ground(far.x,far.z,.02f);var nearXZ=origin+direction*2.7f;var near=SurveyorSiteGeometry.Ground(nearXZ.x,nearXZ.z,.015f);var startXZ=origin+direction*4.5f;var start=SurveyorSiteGeometry.Ground(startXZ.x,startXZ.z,.015f);
                var landingRig=stage.transform.Find("landingRig");landingRig.SetPositionAndRotation(origin,Quaternion.LookRotation(landing+Vector3.up*3.5f-origin));var humanRig=stage.transform.Find("humanRig");humanRig.SetPositionAndRotation(origin,Quaternion.LookRotation(near+Vector3.up*.7f-origin));actors.SetHandFrame(humanRig);var people=actors.transform.Find("Visitor");people.rotation=Quaternion.LookRotation(-direction);actors.Configure(actors.transform.Find("Intrepid"),people,people.Find("LeftBoot"),people.Find("RightBoot"),actors.transform.Find("Glove"),Enumerable.Range(0,48).Select(i=>actors.transform.Find("Dust/Grain"+i)).ToArray(),landing,start,near,new Vector3(.18f,.08f,.9f),new Vector3(.06f,.04f,.38f));actors.Lander.rotation=Quaternion.LookRotation(-direction);actors.Hide();Set(data,"visitor",actors);
                var docs=new List<UIDocument>();string[] xml={ui+"LaunchTelemetry.uxml",ui+"MidcoursePanel.uxml",ui+"DescentTelemetry.uxml",ui+"TelevisionPanel.uxml",SurveyorObservationBuilder.Folder+"/ObservationPanel.uxml"};Type[] views={typeof(LaunchTelemetryView),typeof(SurveyorMidcourseView),typeof(SurveyorDescentView),typeof(SurveyorTelevisionView),typeof(SurveyorObservationView)};
                for(int i=0;i<5;i++){var doc=Document(stage,"Act"+i,xml[i],panel);if(doc.GetComponent(views[i])==null)doc.gameObject.AddComponent(views[i]);Get<ResponsiveLayoutClass>(doc.gameObject);docs.Add(doc);}var soilUi=stage.transform.Find("SoilUI").GetComponent<UIDocument>();soilUi.panelSettings=panel;docs.Add(soilUi);SetArray(data,"panels",docs.Cast<Object>().ToArray());
                Set(data,"story",Document(stage,"Story",Folder+"/ChapterStory.uxml",panel));Set(data,"overlay",Document(stage,"Subtitles",Folder+"/ChapterOverlay.uxml",overlayPanel));
                var pause=stage.transform.Find("Pause").GetComponent<PauseMenu>();pause.GetComponent<UIDocument>().panelSettings=pausePanel;Set(data,"pause",pause);
                Set(data,"launchSound",Sound(stage,"LaunchBed","Assets/_Game/Chapters/Surveyor/Sequences/Audio/LaunchBed.wav",true));Set(data,"engineSound",Sound(stage,"DescentMotor","Assets/_Game/Chapters/Surveyor/Sequences/Descent/Audio/Engine.wav",true));Set(data,"contactSound",Sound(stage,"Contacts",null,false));Set(data,"confirmation",Sound(stage,"RecordConfirm",SurveyorObservationBuilder.Folder+"/Audio/RecordConfirm.wav",false));SetArray(data,"contacts",Enumerable.Range(1,3).Select(i=>(Object)Load<AudioClip>("Assets/_Game/Chapters/Surveyor/Sequences/Descent/Audio/Contact"+i+".wav")).ToArray());
                Child(stage,"AltitudeIndicator");data.ApplyModifiedPropertiesWithoutUndo();
            });
            c.PrefabInstance(c.Root.transform,prefab,"SurveyorChapter");RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.075f,.088f,.11f);
        }
        public override void Validate(SceneBuildContext c,List<string> errors){errors.AddRange(Load<ChapterDefinition>(ChapterPath).Validate());if(c.Root.GetComponentsInChildren<SurveyorChapterHost>(true).Length!=1)errors.Add("Exactly one chapter host.");}
        static AudioClip[] Music(string name)
        {
            string path=Folder+"/Audio/"+name+".wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            if(importer!=null){var options=importer.defaultSampleSettings;options.loadType=AudioClipLoadType.CompressedInMemory;options.compressionFormat=AudioCompressionFormat.Vorbis;options.quality=.4f;options.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;options.sampleRateOverride=22050;importer.defaultSampleSettings=options;importer.forceToMono=true;importer.SaveAndReimport();}
            var clip=Load<AudioClip>(path);return clip!=null?new[]{clip}:Array.Empty<AudioClip>();
        }
        static AudioSource Sound(GameObject parent,string name,string path,bool loop){var sound=Get<AudioSource>(Child(parent,name));sound.playOnAwake=false;sound.loop=loop;sound.spatialBlend=0;if(path!=null)sound.clip=Load<AudioClip>(path);return sound;}
        static UIDocument Document(GameObject root,string name,string xml,PanelSettings settings){var doc=Get<UIDocument>(Child(root,name));doc.panelSettings=settings;doc.visualTreeAsset=Load<VisualTreeAsset>(xml);if(doc.visualTreeAsset==null)throw new InvalidOperationException("UI missing: "+xml);return doc;}
        static T Load<T>(string p)where T:Object=>AssetDatabase.LoadAssetAtPath<T>(p);
        public static void Set(SerializedObject data,string key,Object value){var p=data.FindProperty(key);if(p==null)throw new InvalidOperationException("Missing property "+key);p.objectReferenceValue=value;}
        static void SetArray(SerializedObject d,string key,Object[] values){var p=d.FindProperty(key);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
        static T Get<T>(GameObject o)where T:Component{var value=o.GetComponent<T>();return value!=null?value:o.AddComponent<T>();}
        static GameObject Child(GameObject root,string name){var t=root.transform.Find(name);if(t!=null)return t.gameObject;var o=new GameObject(name);o.transform.SetParent(root.transform,false);return o;}
        static GameObject Nested(GameObject root,string name,string path){var t=root.transform.Find(name);if(t!=null)return t.gameObject;var o=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path),root.scene);o.name=name;o.transform.SetParent(root.transform,false);return o;}
    }
}
