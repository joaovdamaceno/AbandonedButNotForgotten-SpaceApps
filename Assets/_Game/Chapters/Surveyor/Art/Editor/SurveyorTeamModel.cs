using System;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EditorTools
{
    public static class SurveyorTeamModel
    {
        public const string Folder = SurveyorSiteBuilder.ArtPath + "/TeamModel";
        public const string ModelPath = Folder + "/SurveyorTeamWeb.fbx";
        public static bool Available => File.Exists(ModelPath);

        public static void Build(SceneBuildContext context, GameObject root)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if(importer.materialImportMode!=ModelImporterMaterialImportMode.None || importer.isReadable || !importer.bakeAxisConversion)
            {
                importer.materialImportMode=ModelImporterMaterialImportMode.None; importer.isReadable=false;
                importer.bakeAxisConversion=true; importer.importCameras=false; importer.importLights=false;
                importer.animationType=ModelImporterAnimationType.None;
                importer.meshCompression=ModelImporterMeshCompression.Medium;
                importer.SaveAndReimport();
            }
            var albedo=ImportTexture("SurveyorAlbedo.png",true);
            var packed=ImportTexture("SurveyorMetalGloss.png",false);
            var material=context.Material(Folder+"/SurveyorAtlas.mat","Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap",albedo); material.SetColor("_BaseColor",Color.white);
            material.SetTexture("_MetallicGlossMap",packed); material.SetFloat("_Metallic",1); material.SetFloat("_Smoothness",1);
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_AlphaClip",1); material.SetFloat("_Cutoff",.45f); material.SetFloat("_Cull",0);
            material.SetOverrideTag("RenderType","TransparentCutout"); material.renderQueue=2450;
            material.enableInstancing=true; EditorUtility.SetDirty(material);

            var body=Child(root.transform,"Body");
            body.localPosition=SurveyorSiteGeometry.Ground(0,0);
            body.localRotation=Quaternion.FromToRotation(Vector3.up,SurveyorSiteGeometry.Normal(0,0));
            foreach(var path in new[]{"StaticFrame","ThermalAndPanels","TV_Mirror_Azimuth/TV_Mirror_Elevation/MirrorHead",
                "Scoop_Arm_1/UpperLinkage","Scoop_Arm_1/Scoop_Arm_2/LowerLinkage","Scoop_Arm_1/Scoop_Arm_2/Scoop/ScoopShell"})
            {
                var old=body.Find(path); if(old!=null) Object.DestroyImmediate(old.gameObject);
            }
            var native=body.Find("TeamModel");
            if(native==null)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),root.scene);
                PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                instance.name="TeamModel"; instance.transform.SetParent(body,false); native=instance.transform;
            }
            // Restaurar a pose canônica antes de mover pivôs evita acumular deslocamentos entre rebuilds.
            var sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach(var sourcePart in sourceModel.GetComponentsInChildren<MeshRenderer>())
            {
                var part=root.GetComponentsInChildren<MeshRenderer>(true).First(t=>t.name==sourcePart.name || t.name=="Team_"+sourcePart.name);
                part.transform.SetParent(native,false);
                part.transform.localPosition=sourcePart.transform.localPosition;
                part.transform.localRotation=sourcePart.transform.localRotation;
                part.transform.localScale=sourcePart.transform.localScale;
            }
            Transform Find(string name) => native.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
            var sourceFeet=Enumerable.Range(1,3).Select(i=>Find("FootContact_"+i)).ToArray();
            var localFeet=sourceFeet.Select(t=>body.InverseTransformPoint(t.position)).ToArray();
            var sourceNormal=Vector3.Cross(localFeet[1]-localFeet[0],localFeet[2]-localFeet[0]).normalized;
            if(sourceNormal.y<0)sourceNormal=-sourceNormal;
            // Ajustar o plano dos três contatos, não somente a normal sob o centro da sonda.
            for(var iteration=0;iteration<5;iteration++)
            {
                var desired=sourceFeet.Select(t=>SurveyorSiteGeometry.Ground(t.position.x,t.position.z)).ToArray();
                var normal=Vector3.Cross(desired[1]-desired[0],desired[2]-desired[0]).normalized;
                if(normal.y<0)normal=-normal;
                body.localRotation=Quaternion.FromToRotation(sourceNormal,normal);
                float shift=sourceFeet.Average(t=>SurveyorSiteGeometry.Height(t.position.x,t.position.z)-t.position.y);
                body.localPosition+=Vector3.up*shift;
            }
            body.localPosition-=Vector3.up*.007f;
            var pads=Child(root.transform,"Footpads");
            for(var i=0;i<3;i++)
            {
                var pad=Child(pads,"Footpad_"+(i+1)); pad.position=sourceFeet[i].position;
                pad.rotation=Quaternion.FromToRotation(Vector3.up,SurveyorSiteGeometry.Normal(pad.position.x,pad.position.z));
                foreach(var c in pad.GetComponents<Component>()) if(c is MeshRenderer || c is MeshFilter || c is Collider) Object.DestroyImmediate(c);
                Child(pad,"Contact").localPosition=Vector3.zero;
            }
            var az=Child(body,"TV_Mirror_Azimuth"); az.position=Find("MirrorPivot").position; az.localRotation=Quaternion.identity;
            var el=Child(az,"TV_Mirror_Elevation"); el.localPosition=Vector3.zero; el.localRotation=Quaternion.identity;
            Child(el,"CameraMount").position=Find("CameraMount").position;
            var arm=Child(body,"Scoop_Arm_1"); arm.localPosition=new Vector3(0,.65f,.95f);
            var arm2=Child(arm,"Scoop_Arm_2"); arm2.position=Find("ScoopPivot").position;
            var scoop=Child(arm2,"Scoop"); scoop.localPosition=Vector3.zero;
            foreach(var part in new[]{"StaticBody","MirrorHead","SolarPanel","HighGainAntenna","ScoopLinkage","ScoopTool"})
            {
                var mesh=root.GetComponentsInChildren<MeshRenderer>(true).First(t=>t.name==part || t.name=="Team_"+part);
                mesh.name="Team_"+part;
                Transform parent=part=="MirrorHead"?el:part=="ScoopLinkage"?arm:part=="ScoopTool"?scoop:native;
                mesh.transform.SetParent(parent,true);
                mesh.sharedMaterial=material; mesh.receiveShadows=true;
                GameObjectUtility.SetStaticEditorFlags(mesh.gameObject,part=="StaticBody"?StaticEditorFlags.BatchingStatic:0);
            }
            var anchors=Child(root.transform,"Anchors");
            Anchor(anchors,"LandingMarks",SurveyorSiteGeometry.Ground(-2.2f,4f,.02f));
            Anchor(anchors,"CraterSlope",SurveyorSiteGeometry.Ground(-30f,10f,.9f));
            Anchor(anchors,"Footpad",pads.Find("Footpad_2").position+Vector3.up*.08f);
            Anchor(anchors,"Trench",SurveyorSiteGeometry.Ground(.95f,2.35f,.01f));
            Anchor(anchors,"Bearing",SurveyorSiteGeometry.Ground(.65f,2.2f,.01f));
            Anchor(anchors,"Impact",SurveyorSiteGeometry.Ground(1.18f,2.04f,.01f));
            Anchor(anchors,"Eclipse",new Vector3(-40,65,45));
            Anchor(anchors,"Visit",SurveyorSiteGeometry.Ground(-170,60));
        }

        static Texture2D ImportTexture(string file,bool albedo)
        {
            string path=Folder+"/"+file;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer.maxTextureSize!=1024 || importer.isReadable || importer.sRGBTexture!=albedo ||
                importer.alphaSource!=TextureImporterAlphaSource.FromInput || importer.alphaIsTransparency!=albedo)
            {
                importer.maxTextureSize=1024; importer.isReadable=false; importer.sRGBTexture=albedo;
                importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency=albedo;
                importer.mipmapEnabled=true; importer.textureCompression=TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Transform Child(Transform parent,string name)
        {
            var t=parent.Find(name); if(t!=null)return t;
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
        }
        static void Anchor(Transform parent,string name,Vector3 point) => Child(parent,name).localPosition=point;
    }
}
