using System;
using System.IO;
using Abandoned.SpaceApps.Chapters.Surveyor.EditorTools;
using UnityEditor;
using UnityEngine;
using Draft=Abandoned.SpaceApps.Chapters.Surveyor.EditorTools.SurveyorSiteGeometry.Draft;
namespace Abandoned.SpaceApps.EditorTools
{
    public static class SurveyorArtPolish
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Materials/Polish";
        [MenuItem("Abandoned Space Apps/Surveyor/Aplicar acabamento de solo e visita")]
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);
            var source=AssetDatabase.LoadAssetAtPath<Material>(SurveyorSiteBuilder.GeneratedPath+"/Regolith.mat");
            var ground=Asset<Material>("Regolith",()=>UnityEngine.Object.Instantiate(source));
            ground.SetTexture("_BaseMap",SurfaceTexture());ground.SetFloat("_BumpScale",.4f);ground.SetFloat("_Smoothness",.025f);ground.enableInstancing=true;EditorUtility.SetDirty(ground);
            var fabric=Asset<Material>("Fabric",()=>new Material(Shader.Find("Universal Render Pipeline/Lit")));fabric.SetColor("_BaseColor",new Color(.78f,.775f,.735f));fabric.SetFloat("_Metallic",0);fabric.SetFloat("_Smoothness",.08f);fabric.enableInstancing=true;EditorUtility.SetDirty(fabric);
            var glove=Asset<Mesh>("Glove",()=>new Mesh());Glove(glove);EditorUtility.SetDirty(glove);
            var site=PrefabUtility.LoadPrefabContents(SurveyorSiteBuilder.ArtPath+"/LunarSite.prefab");
            try{foreach(var renderer in site.GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterial!=null&&(renderer.sharedMaterial.name=="Regolith"||renderer.sharedMaterial.name=="CinematicRegolith"))renderer.sharedMaterial=ground;PrefabUtility.SaveAsPrefabAsset(site,SurveyorSiteBuilder.ArtPath+"/LunarSite.prefab");}finally{PrefabUtility.UnloadPrefabContents(site);}
            var rig=PrefabUtility.LoadPrefabContents(SurveyorVisitBuilder.CastPath);
            try{rig.transform.Find("Glove").GetComponent<MeshFilter>().sharedMesh=glove;foreach(var part in new[]{"Glove","Visitor/LeftBoot","Visitor/RightBoot"})rig.transform.Find(part).GetComponent<Renderer>().sharedMaterial=fabric;PrefabUtility.SaveAsPrefabAsset(rig,SurveyorVisitBuilder.CastPath);}finally{PrefabUtility.UnloadPrefabContents(rig);}
            AssetDatabase.SaveAssets();
        }
        static T Asset<T>(string name,Func<T> make)where T:UnityEngine.Object{string path=Folder+"/"+name+(typeof(T)==typeof(Material)?".mat":".asset");var obj=AssetDatabase.LoadAssetAtPath<T>(path);if(obj==null){obj=make();obj.name=name=="Regolith"?"CinematicRegolith":name;AssetDatabase.CreateAsset(obj,path);}return obj;}
        static Texture2D SurfaceTexture()
        {
            const int size=1024;string path=Folder+"/Regolith.png";
            if(!File.Exists(path))
            {
                var t=new Texture2D(size,size,TextureFormat.RGB24,false);var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float u=x/(float)size,v=y/(float)size;
                    float dune=Mathf.PerlinNoise(u*3.3f+19,v*3.3f+7),mid=Mathf.PerlinNoise(u*19+11,v*19+4),fine=Mathf.PerlinNoise(u*131,v*131);
                    float value=.335f+.10f*dune+.045f*mid+.018f*fine;
                    var position=new Vector2(u*40-20,v*40-20);for(int i=0;i<3;i++){float distance=(position-SurveyorSiteGeometry.LandingMarkCenter(i)).magnitude;value*=Mathf.Lerp(.68f,1,Mathf.SmoothStep(0,1,(distance-.17f)/.08f));}
                    pixels[y*size+x]=new Color(value,value*.985f,value*.95f);
                }t.SetPixels(pixels);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.isReadable=false;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static void Glove(Mesh mesh)
        {
            var d=new Draft();d.Tube(new Vector3(0,0,-.24f),new Vector3(0,0,-.04f),.073f,.5f,20,.069f);
            for(int r=0;r<4;r++)d.Tube(new Vector3(0,0,-.075f+r*.013f),new Vector3(0,0,-.07f+r*.013f),.079f-r*.002f,.5f,20);
            // Elliptical palm profiles give the hand a continuous silhouette instead of a box.
            float[] z={-.035f,-.012f,.035f,.072f,.091f},w={.061f,.070f,.073f,.063f,.049f},h={.029f,.034f,.037f,.031f,.023f};
            for(int ring=0;ring<z.Length-1;ring++)for(int n=0;n<20;n++){float a=n*Mathf.PI*2/20,b=(n+1)*Mathf.PI*2/20;d.Quad(new Vector3(Mathf.Cos(a)*w[ring],Mathf.Sin(a)*h[ring],z[ring]),new Vector3(Mathf.Cos(b)*w[ring],Mathf.Sin(b)*h[ring],z[ring]),new Vector3(Mathf.Cos(b)*w[ring+1],Mathf.Sin(b)*h[ring+1],z[ring+1]),new Vector3(Mathf.Cos(a)*w[ring+1],Mathf.Sin(a)*h[ring+1],z[ring+1]),.5f);}
            for(int i=0;i<4;i++){float x=-.050f+i*.033f,len=i==3?.065f:i==1?.105f:.09f;var a=new Vector3(x,0,.077f);var b=a+new Vector3(0,-.008f,len*.5f);var c=b+new Vector3(0,-.026f,len*.37f);var tip=c+new Vector3(0,-.008f,len*.13f);d.Tube(a,b,.019f,.5f,12,.018f);d.Tube(b,c,.018f,.5f,12,.014f);d.Tube(c,tip,.014f,.5f,12,.002f);}
            d.Tube(new Vector3(-.061f,0,.012f),new Vector3(-.104f,-.012f,.055f),.025f,.5f,12,.020f);d.Tube(new Vector3(-.104f,-.012f,.055f),new Vector3(-.113f,-.023f,.084f),.020f,.5f,12,.006f);d.Write(mesh);
            // Fingertips approach the optical window; the cuff stays behind the hand.
            var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=new Vector3(-vertices[i].x,vertices[i].y,-vertices[i].z);mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();
        }
    }
}
