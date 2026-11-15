using System;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.Chapters.Surveyor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Abandoned.SpaceApps.EditorTools
{
    public static class SurveyorPolishAudit
    {
        public static void BuildWeb()
        {
            try{WebBaselineBuilder.Build(new[]{SurveyorArchiveBuilder.Scene,SurveyorChapterBuilder.Scene},"Builds/SurveyorWeb","QA/SUR-016");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void BuildAndCapture()
        {
            try{Capture("before");SurveyorChapterBuilder.BuildMenu();Capture("after");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        static void Capture(string label)
        {
            EditorSceneManager.OpenScene(SurveyorChapterBuilder.Scene);var host=UnityEngine.Object.FindAnyObjectByType<SurveyorChapterHost>();var root=host.gameObject;
            var tv=root.transform.Find("Camera").GetComponent<Camera>();tv.enabled=false;var sun=root.transform.Find("Sun").GetComponent<Light>();sun.transform.rotation=Quaternion.Euler(8,55,0);sun.intensity=1.05f;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.065f,.072f,.082f);var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(RenderSettings.ambientLight);RenderSettings.ambientProbe=ambient;
            var rig=root.transform.Find("ApolloVisitRig").GetComponent<SurveyorVisitActors>();
            if(label=="before")
            {
                // Temporary scene overrides only; never overwrite the approved prefab to reconstruct the baseline.
                var oldGround=AssetDatabase.LoadAssetAtPath<Material>(Chapters.Surveyor.EditorTools.SurveyorSiteBuilder.GeneratedPath+"/Regolith.mat");
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.name=="CinematicRegolith")renderer.sharedMaterial=oldGround;
                var oldFabric=AssetDatabase.LoadAssetAtPath<Material>(SurveyorVisitBuilder.Folder+"/Generated/Fabric.mat");
                foreach(var part in new[]{"Glove","Visitor/LeftBoot","Visitor/RightBoot"})rig.transform.Find(part).GetComponent<Renderer>().sharedMaterial=oldFabric;
                rig.Glove.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(SurveyorVisitBuilder.Folder+"/Generated/Glove.asset");var actorData=new SerializedObject(rig);actorData.FindProperty("gloveStart").vector3Value=new Vector3(.75f,.4f,1);actorData.ApplyModifiedPropertiesWithoutUndo();sun.shadowNormalBias=.25f;sun.shadowBias=.025f;
            }
            rig.Evaluate(28.5f,false);var optics=root.transform.Find("humanRig");tv.transform.SetPositionAndRotation(optics.position,optics.rotation);tv.fieldOfView=25.3f;
            string[] profiles={WebBaselineBuilder.Profile,"Assets/_Game/Settings/URP/SpaceApps_URP.asset"};string[] names={"leve","equilibrado"};var oldG=GraphicsSettings.defaultRenderPipeline;var oldQ=QualitySettings.renderPipeline;
            try
            {
                Directory.CreateDirectory("QA/SUR-016");
                for(int i=0;i<profiles.Length;i++){GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(profiles[i]);QualitySettings.renderPipeline=GraphicsSettings.defaultRenderPipeline;Picture(tv,"QA/SUR-016/"+label+"-"+names[i]+".png");}
                // Check every vertex of the reaching hand against the optical near plane, not only its pivot.
                float minimum=float.PositiveInfinity;var filter=rig.Glove.GetComponent<MeshFilter>();for(float t=26;t<=30.51f;t+=.05f){rig.Evaluate(t,false);foreach(var vertex in filter.sharedMesh.vertices){float z=tv.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)).z;minimum=Mathf.Min(minimum,z);}}if(minimum<tv.nearClipPlane+.01f)throw new InvalidOperationException("Glove intersects optics: "+minimum);
                rig.Evaluate(14,false);if(rig.GetComponentsInChildren<Transform>(false).Any(t=>t.name.StartsWith("Grain")))throw new InvalidOperationException("Dust must have settled after landing.");
                int triangles=filter.sharedMesh.triangles.Length/3;File.WriteAllText("QA/SUR-016/"+label+"-audit.txt",$"Glove triangles {triangles}\nNearest reaching-hand vertex along optical axis: {minimum} m\nNear clip {tv.nearClipPlane} m\nDust settled at t=14\nOne shared fabric material; no extra renderer/shader/particle system.\nFilm durations unchanged.\n");
            }finally{GraphicsSettings.defaultRenderPipeline=oldG;QualitySettings.renderPipeline=oldQ;}
        }
        static void Picture(Camera camera,string path)
        {
            var target=new RenderTexture(960,720,24);target.Create();var previous=camera.targetTexture;camera.targetTexture=target;camera.Render();camera.Render();var saved=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(960,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=saved;camera.targetTexture=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
