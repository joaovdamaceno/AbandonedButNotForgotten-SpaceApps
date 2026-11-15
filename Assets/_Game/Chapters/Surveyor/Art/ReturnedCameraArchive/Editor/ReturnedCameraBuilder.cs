using System;
using System.IO;
using Abandoned.SpaceApps.Chapters.Surveyor;
using UnityEditor;
using UnityEngine;
namespace Abandoned.SpaceApps.EditorTools
{
    public static class ReturnedCameraBuilder
    {
        public const string Folder="Assets/_Game/Chapters/Surveyor/Art/ReturnedCameraArchive",DataPath="Assets/_Game/Chapters/Surveyor/Data/ReturnedCameraCard.asset",CoverPath=Folder+"/ReturnedCameraCover.asset";
        public static void BuildFromCommandLine()
        {
            try{Prepare();SurveyorChapterBuilder.BuildMenu();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [MenuItem("Abandoned Space Apps/Surveyor/Preparar ficha da câmera recuperada")]
        public static void Prepare()
        {
            var photo=AssetDatabase.LoadAssetAtPath<Texture2D>(SurveyorVisitBuilder.PhotoPath);var table=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/ReturnedCameraTexts.json");var data=AssetDatabase.LoadAssetAtPath<ReturnedCameraCard>(DataPath);
            if(data==null){data=ScriptableObject.CreateInstance<ReturnedCameraCard>();AssetDatabase.CreateAsset(data,DataPath);}data.Configure(photo,table,"https://ntrs.nasa.gov/citations/19710024418","https://airandspace.si.edu/collection-objects/camera-television-surveyor/nasm_A19761264000");EditorUtility.SetDirty(data);if(data.Validate().Count>0)throw new InvalidOperationException(string.Join("\n",data.Validate()));
            if(AssetDatabase.LoadAssetAtPath<Sprite>(CoverPath)==null){var sprite=Sprite.Create(photo,new Rect(0,0,photo.width,photo.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);sprite.name="Surveyor NASA archive";AssetDatabase.CreateAsset(sprite,CoverPath);}AssetDatabase.SaveAssets();
        }
        public static void BuildWeb()
        {try{Prepare();SurveyorChapterBuilder.BuildMenu();WebBaselineBuilder.Build(new[]{SurveyorArchiveBuilder.Scene,SurveyorChapterBuilder.Scene},"Builds/SurveyorWeb","QA/SUR-017");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    }
}
