using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class SurveyorDescentBuilder:SceneBuilderModule
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Sequences/Descent";
        public const string UiFolder="Assets/_Game/Chapters/Surveyor/UI";
        public const string SequencePath="Assets/_Game/Chapters/Surveyor/Sequences/SurveyorDescent.asset";
        public const string PreviewScene=Folder+"/Preview/SurveyorDescentPreview.unity";
        public const string PrefabPath=Folder+"/SurveyorDescentPreview.prefab";
        public override string Id=>"surveyor.descent";public override string Owner=>"R6 · SUR-006";
        public override string ScenePath=>PreviewScene;public override string RootName=>"[R6 SUR-006] Descent";
        public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor/Sequences";
        [MenuItem("Abandoned Space Apps/Surveyor/SUR-006 Reconstruir pouso")]
        public static void BuildFromMenu(){var r=SpaceAppsSceneBuilder.Run(new SurveyorDescentBuilder());if(!r.Succeeded)throw new InvalidOperationException(r.ToString());Debug.Log(r.ToString());}
        public static void BuildFromCommandLine(){try{BuildFromMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public override void Build(SceneBuildContext c)
        {
            var template=CreateReference();template.name="SurveyorDescent";var track=c.Asset(SequencePath,()=>template);
            if(track!=template){EditorUtility.CopySerialized(template,track);Object.DestroyImmediate(template);}EditorUtility.SetDirty(track);
            var chapter=c.Asset(Folder+"/Preview/DescentChapter.asset",()=>ChapterDefinition.Create("surveyor.descent.preview","descent.title","descent.description","descent.question",
                "SurveyorDescentPreview",40,track.SequenceId,new[]{new ChapterStep("Descent",false,"FirstImage"),new ChapterStep("FirstImage",false,"End"),new ChapterStep("End",true)}));
            var panel=c.Asset(Folder+"/DescentPanelSettings.asset",()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));
            var motorClip=Audio("Engine",2,0);var a=Audio("Contact1",.4f,1);var b=Audio("Contact2",.3f,2);var d=Audio("Contact3",.22f,3);
            var prefab=c.Prefab(PrefabPath,root=>
            {
                var owner=Get<ChapterDirector>(root);var clock=Get<SessionClock>(root);var input=Get<InputRouter>(root);var service=Get<InteractionService>(root);
                var cameraDirector=Get<CameraDirector>(root);var sequence=Get<SequenceDirector>(root);sequence.enabled=false;
                var queue=Get<SurveyorCommandDelay>(root);var action=Get<SurveyorCommandInteraction>(root);var flow=Get<SurveyorDescent>(root);
                var view=Get<SurveyorDescentView>(root);var gate=Get<SurveyorDescentCameraGate>(root);var sound=Get<SurveyorDescentAudio>(root);var host=Get<SurveyorDescentPreviewHost>(root);
                var main=Get<Camera>(Child(root,"TelemetryCamera"));main.cullingMask=0;main.clearFlags=CameraClearFlags.SolidColor;main.backgroundColor=new Color(.03f,.047f,.075f);Get<AudioListener>(main.gameObject);cameraDirector.Initialize(main);
                var surface=Child(root,"LunarSurface");var site=Nested(surface,"LunarSite","Assets/_Game/Chapters/Surveyor/Art/LunarSite.prefab");
                var rig=Nested(surface,"Surveyor","Assets/_Game/Chapters/Surveyor/Art/SurveyorVisibleRig.prefab");
                var mount=rig.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount");var anchors=rig.transform.Find("Anchors");
                var tv=Get<Camera>(Child(surface,"TelevisionCamera"));tv.enabled=false;tv.nearClipPlane=.025f;tv.farClipPlane=360;tv.clearFlags=CameraClearFlags.SolidColor;tv.backgroundColor=Color.black;
                tv.transform.position=mount.position;Abandoned.SpaceApps.Chapters.Surveyor.EditorTools.SurveyorSiteBuilder.Frame(tv,anchors.Find("LandingMarks"),25.3f);
                var viewport=Get<SurveyorSiteViewport>(tv.gameObject);viewport.enabled=false;viewport.DrawToGameView=false;viewport.Configure(tv);
                Get<SurveyorSiteBindings>(surface).Configure(cameraDirector,mount,rig.transform.Find("Body/TV_Mirror_Azimuth"),rig.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation"),
                    rig.transform.Find("Body/Scoop_Arm_1"),rig.transform.Find("Body/Scoop_Arm_1/Scoop_Arm_2/Scoop"),
                    Enumerable.Range(0,anchors.childCount).Select(i=>new SurveyorSiteBindings.Target{id="SUR."+anchors.GetChild(i).name,anchor=anchors.GetChild(i)}).ToArray());
                var sun=Get<Light>(Child(surface,"Sun"));sun.type=LightType.Directional;sun.intensity=1.15f;sun.color=new Color(1,.965f,.91f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(23,145,0);
                var engine=Get<AudioSource>(Child(root,"EngineAudio"));engine.clip=motorClip;engine.playOnAwake=false;engine.loop=true;engine.volume=0;engine.spatialBlend=0;
                var impacts=Get<AudioSource>(Child(root,"ContactAudio"));impacts.playOnAwake=false;impacts.spatialBlend=0;impacts.volume=1;
                var doc=Get<UIDocument>(root);doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UiFolder+"/DescentTelemetry.uxml");Get<ResponsiveLayoutClass>(root);
                host.Configure(owner,clock,input,service,cameraDirector,sequence,queue,action,flow,view,gate,sound,doc,
                    AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder+"/DescentTexts.json"),chapter,track,Child(root,"AltitudeIndicator").transform,tv,viewport,engine,impacts,a,b,d);
            });
            c.PrefabInstance(c.Root.transform,prefab,"SurveyorDescent");
            RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.075f,.088f,.11f);
        }
        public override void Validate(SceneBuildContext c,List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath).Validate());
            if(c.Root.GetComponentsInChildren<AudioListener>(true).Length!=1)errors.Add("Listener duplicado/ausente.");
            var tv=c.Root.GetComponentsInChildren<SurveyorSiteViewport>(true).Single();if(tv.enabled)errors.Add("A TV não pode iniciar habilitada.");
            errors.AddRange(SurveyorLaunchTexts.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(UiFolder+"/DescentTexts.json")).Validate());
        }
        public static SequenceDefinition CreateReference()
        {
            ShotDefinition Shot(string id,float seconds,params ShotEvent[] events)=>new ShotDefinition(id,seconds,CameraDirector.WorldRigId,
                new[]{new ShotKeyframe(0,new Vector3(0,3,-10),Vector3.zero,45),new ShotKeyframe(seconds,new Vector3(0,3,-10),Vector3.zero,45)},events:events);
            var marks=new List<ActorMark>();void Mark(float t,float height)=>marks.Add(new ActorMark(SurveyorDescent.IndicatorId,t,new Vector3(0,height,0),Vector3.zero));
            Mark(0,180);Mark(7,45);Mark(11,4.3f);Mark(12,3.8f);
            for(int i=0;i<=32;i++){float u=i/32f;Mark(18+u*8,10*4*u*(1-u));}
            for(int i=1;i<=24;i++){float u=i/24f;Mark(26+u*6,3*4*u*(1-u));}Mark(40,0);
            return SequenceDefinition.Create("surveyor.seq.Descent",new[]{
                Shot("surveyor.descent.approach",12,new ShotEvent(11,"surveyor.descent.anomaly")),
                Shot("surveyor.descent.command",15,new ShotEvent(0,SurveyorDescent.OfferEvent),new ShotEvent(6,SurveyorDescent.Contact1),new ShotEvent(14,SurveyorDescent.Contact2),new ShotEvent(15,SurveyorDescent.DeadlineEvent)),
                Shot("surveyor.descent.finalContact",5,new ShotEvent(3,SurveyorDescent.HardDeadlineEvent),new ShotEvent(4,SurveyorDescent.CutoffEligibleEvent)),
                Shot("surveyor.descent.settlement",8,new ShotEvent(0,SurveyorDescent.Contact3),new ShotEvent(0,SurveyorDescent.SettledEvent),new ShotEvent(1.5f,SurveyorDescent.QuietEvent),new ShotEvent(3,SurveyorDescent.ReadyEvent))},
                new[]{new SubtitleCue("SUR-S07","SUR-S07",11,17,priority:2),SubtitleCue.Triggered("SUR-S08","SUR-S08",SurveyorDescent.TeamTrigger,6,priority:1),
                    SubtitleCue.Triggered("SUR-S08-PLAYER","descent.player.receipt",SurveyorDescent.PlayerTrigger,4,priority:1),SubtitleCue.Triggered("SUR-S09","SUR-S09",SurveyorDescent.ImageTrigger,5,priority:3)},marks);
        }
        static AudioClip Audio(string name,float seconds,int type)
        {
            string path=Folder+"/Audio/"+name+".wav";
            if(!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));const int rate=16000;int count=Mathf.RoundToInt(seconds*rate);var random=new System.Random(640+type);
                using(var w=new BinaryWriter(File.Create(path)))
                {
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
                    double filtered=0;
                    for(int i=0;i<count;i++)
                    {
                        double t=(double)i/rate;filtered=.86*filtered+.14*(random.NextDouble()*2-1);
                        double sample=type==0?.40*Math.Sin(2*Math.PI*52*t)+.12*Math.Sin(2*Math.PI*108*t):
                            Math.Exp(-t*(type==1?16:22))*(.65*filtered+.25*Math.Sin(2*Math.PI*(type==1?180:type==2?290:340)*t))*(1-Math.Exp(-t*450));
                        w.Write((short)(Math.Clamp(sample,-1,1)*short.MaxValue));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.55f;importer.defaultSampleSettings=settings;importer.forceToMono=true;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static T Get<T>(GameObject go)where T:Component{var c=go.GetComponent<T>();return c!=null?c:go.AddComponent<T>();}
        static GameObject Child(GameObject root,string name){var t=root.transform.Find(name);if(t!=null)return t.gameObject;var g=new GameObject(name);g.transform.SetParent(root.transform,false);return g;}
        static GameObject Nested(GameObject root,string name,string path)
        {
            var old=root.transform.Find(name);if(old!=null)return old.gameObject;
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.scene);g.name=name;g.transform.SetParent(root.transform,false);return g;
        }
    }
}
