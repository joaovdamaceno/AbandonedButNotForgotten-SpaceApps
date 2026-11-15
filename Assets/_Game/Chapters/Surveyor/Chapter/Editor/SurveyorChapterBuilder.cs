using System;
using System.Collections.Generic;
using Abandoned.SpaceApps.Chapters.Surveyor;
using UnityEditor;
using UnityEngine;
namespace Abandoned.SpaceApps.EditorTools
{
    public sealed class SurveyorChapterBuilder : SceneBuilderModule
    {
        public const string Folder = "Assets/_Game/Chapters/Surveyor/Chapter";
        public const string Scene = "Assets/_Game/Chapters/Surveyor/Scenes/SurveyorChapter.unity", ChapterPath = Folder + "/SurveyorChapter.asset";
        public override string Id => "surveyor.chapter";
        public override string Owner => "R6 · SUR-020";
        public override string ScenePath => Scene;
        public override string RootName => "[R6] Surveyor full chapter";
        public override string AssetFolder => "Assets/_Game/Chapters/Surveyor";
        [MenuItem("Abandoned Space Apps/Surveyor/Reconstruir capítulo completo")]
        public static void BuildMenu()
        {
            var chapter = SpaceAppsSceneBuilder.Run(new SurveyorChapterBuilder());
            if (!chapter.Succeeded) throw new InvalidOperationException(chapter.ToString());
            var archive = SpaceAppsSceneBuilder.Run(new SurveyorArchiveBuilder());
            if (!archive.Succeeded) throw new InvalidOperationException(archive.ToString());
            Debug.Log(chapter); Debug.Log(archive);
        }
        public static void BuildFromCommandLine()
        { try { BuildMenu(); EditorApplication.Exit(0); } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
        public override void Build(SceneBuildContext c) => SurveyorDocumentaryBuilder.Build(c);
        public override void Validate(SceneBuildContext c, List<string> errors)
        {
            errors.AddRange(AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath).Validate());
            if (c.Root.GetComponentsInChildren<SurveyorDocumentaryHost>(true).Length != 1) errors.Add("Exactly one documentary host required.");
            if (c.Root.GetComponentsInChildren<SurveyorVisitActors>(true).Length != 0) errors.Add("Legacy disconnected actors cannot ship.");
        }
        public static void Set(SerializedObject data, string key, UnityEngine.Object value)
        {
            var p = data.FindProperty(key); if (p == null) throw new InvalidOperationException("Missing property " + key); p.objectReferenceValue = value;
        }
    }
}
