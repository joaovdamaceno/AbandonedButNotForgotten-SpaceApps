using System;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.Chapters.Surveyor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Abandoned.SpaceApps.EditorTools
{
    public static class WebBaselineBuilder
    {
        public const string Folder="Assets/_Game/Settings/Web";
        public const string Scene=Folder+"/Preview/WebBaseline.unity",Profile=Folder+"/WebLight.asset";
        public static void Prepare()
        {
            Directory.CreateDirectory(Folder+"/Preview");
            var renderer=Copy<UniversalRendererData>("Assets/_Game/Settings/URP/SpaceApps_URP_Renderer.asset",Folder+"/WebLightRenderer.asset");
            var rs=new SerializedObject(renderer);rs.FindProperty("m_RendererFeatures").ClearArray();rs.FindProperty("m_RendererFeatureMap")?.ClearArray();rs.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(renderer);
            var profile=Copy<UniversalRenderPipelineAsset>("Assets/_Game/Settings/URP/SpaceApps_URP.asset",Profile);
            profile.renderScale=.7f;profile.supportsHDR=false;profile.msaaSampleCount=1;profile.shadowDistance=30;profile.shadowCascadeCount=2;profile.mainLightShadowmapResolution=1024;profile.supportsCameraDepthTexture=true;profile.supportsCameraOpaqueTexture=false;
            var ps=new SerializedObject(profile);ps.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue=renderer;ps.FindProperty("m_AdditionalLightsRenderingMode").intValue=0;ps.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(profile);
            var scene=EditorSceneManager.OpenScene(SurveyorSoilBuilder.Scene);
            var definition=AssetDatabase.LoadAssetAtPath<ChapterDefinition>(Folder+"/WebBaselineChapter.asset");
            if(definition==null){definition=ChapterDefinition.Create("surveyor.webbaseline","soil.title","soil.description","soil.sample","WebBaseline",60,"surveyor.soil.preview",new[]{new ChapterStep("Sample",false,"Eclipse"),new ChapterStep("Eclipse",true)});AssetDatabase.CreateAsset(definition,Folder+"/WebBaselineChapter.asset");}
            var host=new SerializedObject(UnityEngine.Object.FindAnyObjectByType<SurveyorSoilPreview>());host.FindProperty("chapter").objectReferenceValue=definition;host.ApplyModifiedPropertiesWithoutUndo();
            var probe=new GameObject("QA-only memory and frame probe");probe.AddComponent<WebBaselineProbe>();
            EditorSceneManager.SaveScene(scene,Scene);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        static T Copy<T>(string source,string dest)where T:UnityEngine.Object
        {var old=AssetDatabase.LoadAssetAtPath<T>(dest);if(old!=null)return old;var clone=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<T>(source));clone.name=Path.GetFileNameWithoutExtension(dest);AssetDatabase.CreateAsset(clone,dest);return clone;}
        public static void BuildFromCommandLine()
        {try{Prepare();Build(new[]{Scene},"Builds/WebBaseline");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Build(string[] scenes,string output,string reportFolder="QA/WebBaseline")
        {
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.WebGL)throw new InvalidOperationException("Start Unity with -buildTarget WebGL and finish its import before building.");
            var oldPipeline=GraphicsSettings.defaultRenderPipeline;var oldQuality=QualitySettings.renderPipeline;var oldTemplate=PlayerSettings.WebGL.template;
            var oldCompression=PlayerSettings.WebGL.compressionFormat;bool oldFallback=PlayerSettings.WebGL.decompressionFallback;
            try
            {
                var profile=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Profile);
                GraphicsSettings.defaultRenderPipeline=profile;QualitySettings.renderPipeline=profile;
                PlayerSettings.WebGL.template="PROJECT:SpaceApps";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Brotli;PlayerSettings.WebGL.decompressionFallback=false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL,ScriptingImplementation.IL2CPP);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=output,target=BuildTarget.WebGL,options=BuildOptions.None});
                Directory.CreateDirectory(reportFolder);File.WriteAllText(reportFolder+"/build-result.txt",$"{report.summary.result}\nUnity {Application.unityVersion}\nErrors {report.summary.totalErrors}\nWarnings {report.summary.totalWarnings}\nElapsed {report.summary.totalTime}\nBuildReport bytes (not compressed download) {report.summary.totalSize}\n");
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Web build failed; see report/log.");
                Debug.Log("WEB RELEASE "+output);
            }
            finally{GraphicsSettings.defaultRenderPipeline=oldPipeline;QualitySettings.renderPipeline=oldQuality;PlayerSettings.WebGL.template=oldTemplate;PlayerSettings.WebGL.compressionFormat=oldCompression;PlayerSettings.WebGL.decompressionFallback=oldFallback;}
        }
    }
}
