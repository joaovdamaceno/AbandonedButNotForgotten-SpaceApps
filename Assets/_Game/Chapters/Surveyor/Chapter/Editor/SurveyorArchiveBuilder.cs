using System;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorArchiveBuilder:SceneBuilderModule
    {
        public const string Scene="Assets/_Game/Chapters/Surveyor/Scenes/SurveyorArchive.unity";
        public override string Id=>"surveyor.archive";public override string Owner=>"R6 · SUR-015";public override string ScenePath=>Scene;public override string RootName=>"[R6] Surveyor archive entry";public override string AssetFolder=>"Assets/_Game/Chapters/Surveyor";
        public override void Build(SceneBuildContext c)
        {
            string folder=SurveyorChapterBuilder.Folder;
            var template=ChapterCatalog.Create(new[]{ChapterCatalog.Entry.Create("mars",null,"sa-chapter--mars"),ChapterCatalog.Entry.Create("pioneer10",null,"sa-chapter--pioneer"),ChapterCatalog.Entry.Create("surveyor3",AssetDatabase.LoadAssetAtPath<ChapterDefinition>(SurveyorChapterBuilder.ChapterPath),"sa-chapter--surveyor",AssetDatabase.LoadAssetAtPath<TextAsset>(folder+"/ChapterTexts.json"))});
            var catalog=c.Asset(folder+"/SurveyorArchiveCatalog.asset",()=>template);if(catalog!=template){EditorUtility.CopySerialized(template,catalog);Object.DestroyImmediate(template);}EditorUtility.SetDirty(catalog);
            var prefab=c.Prefab(folder+"/SurveyorArchive.prefab",root=>
            {
                Get<ChapterDirector>(root);Get<SessionClock>(root);Get<InputRouter>(root);Get<InteractionService>(root);
                var menu=root.transform.Find("MenuCamera");if(menu==null){var o=new GameObject("MenuCamera");o.transform.SetParent(root.transform,false);menu=o.transform;}
                var camera=Get<Camera>(menu.gameObject);camera.cullingMask=0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.035f,.05f);Get<AudioListener>(menu.gameObject);
                var screen=root.transform.Find("Dossier");if(screen==null){var o=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/UI/Dossier/DossierScreen.prefab"),root.scene);o.name="Dossier";o.transform.SetParent(root.transform,false);screen=o.transform;}
                Get<SurveyorArchiveHost>(root).Configure(catalog,screen.GetComponent<DossierScreen>(),camera);
            });c.PrefabInstance(c.Root.transform,prefab,"SurveyorArchive");
        }
        static T Get<T>(GameObject root)where T:Component{var item=root.GetComponent<T>();return item!=null?item:root.AddComponent<T>();}
    }
}
