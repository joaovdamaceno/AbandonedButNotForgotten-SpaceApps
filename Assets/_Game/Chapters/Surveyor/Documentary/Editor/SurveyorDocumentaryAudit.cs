using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>Audit the published scenes rather than assuming that unused source folders enter a build.</summary>
    public static class SurveyorDocumentaryAudit
    {
        [Serializable] sealed class Report
        {
            public string unity;
            public string[] scenes, dependencies, videos, legacyActors;
            public int dependencyCount;
        }

        public static void Write()
        {
            var scenes = new[] { SurveyorArchiveBuilder.Scene, SurveyorChapterBuilder.Scene };
            var paths = AssetDatabase.GetDependencies(scenes, true).OrderBy(p => p).ToArray();
            var report = new Report
            {
                unity = Application.unityVersion, scenes = scenes, dependencies = paths,
                dependencyCount = paths.Length,
                videos = paths.Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(VideoClip)).ToArray(),
                legacyActors = paths.Where(p => p.Contains("/Sequences/Visit/")
                    && !p.Contains("/Sequences/Visit/Archive/")).ToArray()
            };
            Directory.CreateDirectory("QA/SUR-018");
            File.WriteAllText("QA/SUR-018/asset-dependencies.json", JsonUtility.ToJson(report, true));
            if (report.videos.Length > 0 || report.legacyActors.Length > 0)
                throw new InvalidOperationException("Published Surveyor scenes reference retired video/actor assets. See asset-dependencies.json.");
        }
    }
}
