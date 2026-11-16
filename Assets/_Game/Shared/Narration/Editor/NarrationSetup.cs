using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.Chapters.Surveyor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>Installs only narration components/references, preserving the authored worlds and scene ownership.</summary>
    public static class NarrationSetup
    {
        [Serializable] sealed class RecordingFile { public string chapter; public Recording[] lines; }
        [Serializable] sealed class Recording { public string key, status, clip; public float duration_seconds; }
        [Serializable] sealed class InstallationReport { public string baseCommit = "5b67617"; public int clips, catalogs, scenes; public List<string> adjustedSequences = new List<string>(); public List<string> missing = new List<string>(); }
        static readonly string[] Chapters = { "surveyor", "pioneer", "mars" };
        static readonly Dictionary<string, string> Folders = new Dictionary<string, string>
        {
            {"surveyor","Assets/_Game/Chapters/Surveyor/Narration"},
            {"pioneer","Assets/_Game/Chapters/Pioneer10/Narration"},
            {"mars","Assets/_Game/Chapters/Mars/Narration"},
        };
        static readonly string[] Scenes = {
            "Assets/_Game/Chapters/Surveyor/Scenes/SurveyorChapter.unity",
            "Assets/_Game/Chapters/Pioneer10/Scenes/Pioneer10.unity",
            "Assets/_Game/Chapters/Mars/Scenes/Mars.unity",
            "Assets/_Game/Chapters/Mars/Opportunity/Scenes/Opportunity.unity",
        };
        public static string CatalogPath(string chapter) => Folders[chapter] + "/EnglishNarration.asset";
        public static void BuildWebSmoke()
        {
            var report = SpaceAppsBuild.Build(new SpaceAppsBuild.BuildRequest {
                Platform = SpaceAppsBuild.Platform.WebGL, OutputPath = "Builds/NAR-001-Web",
                Commit = "NAR-001-working-tree", Development = true
            });
            if (report.errors.Count > 0) throw new InvalidOperationException(string.Join("\n", report.errors));
        }

        /// <summary>
        /// Runtime-only probe when the inherited CORE release gate is incompatible
        /// with develop's current packages/renderers. Does not approve a release,
        /// modify the gate, or change the team's graphics/project settings.
        /// </summary>
        public static void BuildWebRuntimeProbe()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Activate WebGL before building the narration probe.");
            ValidateInstalled();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.WebGL, locationPathName = "Builds/NAR-001-Web",
                options = BuildOptions.Development
            });
            Directory.CreateDirectory("QA/NAR-001");
            File.WriteAllText("QA/NAR-001/web-runtime-probe.json", JsonUtility.ToJson(new RuntimeProbeReport {
                result = report.summary.result.ToString(), unityVersion = Application.unityVersion,
                bytes = report.summary.totalSize, durationSeconds = report.summary.totalTime.TotalSeconds,
                releaseApproved = false,
                note = "Runtime probe only. CORE release preflight remains blocked by inherited package/renderer/profile settings."
            }, true) + "\n");
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Narration runtime probe build failed: " + report.summary.result);
        }
        [Serializable] sealed class RuntimeProbeReport
        {
            public string result, unityVersion, note;
            public ulong bytes;
            public double durationSeconds;
            public bool releaseApproved;
        }

        [MenuItem("Abandoned Space Apps/NAR-001/Install and validate English narration")]
        public static void InstallAndValidate()
        {
            var report = new InstallationReport();
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var chapter in Chapters)
                {
                    var file = JsonUtility.FromJson<RecordingFile>(File.ReadAllText("Data/Narration/" + chapter + ".json"));
                    var lines = new List<NarrationLine>();
                    foreach (var line in file.lines)
                    {
                        if (line.status != "matched") { report.missing.Add(chapter + ": " + line.key); continue; }
                        var importer = AssetImporter.GetAtPath(line.clip) as AudioImporter;
                        if (importer == null) throw new InvalidOperationException("Missing speech asset: " + line.clip);
                        var sample = importer.defaultSampleSettings;
                        bool needsImport = !importer.forceToMono || !importer.loadInBackground
                            || sample.preloadAudioData || sample.loadType != AudioClipLoadType.CompressedInMemory
                            || sample.sampleRateOverride != 32000 || sample.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate;
                        importer.forceToMono = true; importer.loadInBackground = true;
                        sample.loadType = AudioClipLoadType.CompressedInMemory;
                        sample.compressionFormat = AudioCompressionFormat.Vorbis;
                        sample.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                        sample.sampleRateOverride = 32000; sample.quality = .7f;
                        sample.preloadAudioData = false;
                        importer.defaultSampleSettings = sample;
                        if (needsImport) importer.SaveAndReimport();
                        // Web uses Unity's translated AAC settings (44.1 kHz
                        // serialized, device-dependent playback frequency).
                        // Do not request the unsupported Vorbis/32 kHz override.
                        var web = importer.GetOverrideSampleSettings("WebGL");
                        if (web.preloadAudioData || web.loadType != AudioClipLoadType.CompressedInMemory)
                            throw new InvalidOperationException("Web speech loading settings do not inherit correctly: " + line.key);
                        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(line.clip);
                        if (clip == null || Math.Abs(clip.length - line.duration_seconds) > .08f)
                            throw new InvalidOperationException("Clip duration mismatch: " + line.key);
                        lines.Add(new NarrationLine(line.key, clip)); report.clips++;
                    }
                    var catalog = AssetDatabase.LoadAssetAtPath<NarrationCatalog>(CatalogPath(chapter));
                    if (catalog == null) { catalog = ScriptableObject.CreateInstance<NarrationCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath(chapter)); }
                    catalog.Configure(chapter == "pioneer" ? "pioneer10" : chapter == "surveyor" ? "surveyor3" : chapter, lines);
                    EditorUtility.SetDirty(catalog); report.catalogs++;
                }
                AssetDatabase.SaveAssets();
                foreach (var path in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    if (path.Contains("Surveyor")) Attach(Object.FindFirstObjectByType<SurveyorDocumentaryHost>().gameObject, "surveyor");
                    else if (path.Contains("Pioneer10")) Attach(Object.FindFirstObjectByType<PioneerChapterController>().gameObject, "pioneer");
                    else if (path.Contains("Opportunity"))
                    {
                        var film = Object.FindFirstObjectByType<Abandoned.SpaceApps.Opportunity.NarrativeDirector>();
                        var player = Attach(film.gameObject, "mars");
                        var so = new SerializedObject(film); so.FindProperty("narration").objectReferenceValue = player; so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else Attach(Object.FindFirstObjectByType<MarsChapterBindings>().gameObject, "mars");
                    var chapter = path.Contains("Surveyor") ? "surveyor" : path.Contains("Pioneer10") ? "pioneer" : "mars";
                    var catalog = AssetDatabase.LoadAssetAtPath<NarrationCatalog>(CatalogPath(chapter));
                    foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                    {
                        if (!dependency.StartsWith("Assets/_Game/Chapters/", StringComparison.Ordinal) || !dependency.EndsWith(".asset", StringComparison.Ordinal)) continue;
                        if (AssetDatabase.GetMainAssetTypeAtPath(dependency) != typeof(SequenceDefinition)) continue;
                        if (FitSequence(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(dependency), catalog)) report.adjustedSequences.Add(dependency);
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); report.scenes++;
                }
                AssetDatabase.SaveAssets();
                ValidateInstalled();
                Directory.CreateDirectory("QA/NAR-001");
                File.WriteAllText("QA/NAR-001/installation.json", JsonUtility.ToJson(report, true) + "\n");
                Debug.Log("NAR-001_INSTALLED clips=" + report.clips + " scenes=" + report.scenes + " missing=" + report.missing.Count);
            }
            catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }

        public static NarrationPlayer Attach(GameObject root, string chapter)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<NarrationCatalog>(CatalogPath(chapter));
            if (catalog == null) return null; // A pre-narration source checkout can still reconstruct its chapter.
            var player = root.GetComponent<NarrationPlayer>() ?? root.AddComponent<NarrationPlayer>();
            player.Configure(catalog); EditorUtility.SetDirty(player);
            return player;
        }

        /// <summary>Reading windows grow only when the natural recording needs it. No speech retiming.</summary>
        public static bool FitSequence(SequenceDefinition sequence, NarrationCatalog catalog)
        {
            if (sequence == null || catalog == null) return false;
            var so = new SerializedObject(sequence); var cues = so.FindProperty("cues"); bool changed = false;
            var indices = Enumerable.Range(0, cues.arraySize).Where(i =>
                string.IsNullOrEmpty(cues.GetArrayElementAtIndex(i).FindPropertyRelative("triggerId").stringValue)
                && cues.GetArrayElementAtIndex(i).FindPropertyRelative("position").enumValueIndex == (int)SubtitlePosition.Bottom)
                .OrderBy(i => cues.GetArrayElementAtIndex(i).FindPropertyRelative("startSeconds").floatValue).ToArray();
            float previousOriginalEnd = 0f, previousNewEnd = 0f;
            foreach (var i in indices)
            {
                var cue = cues.GetArrayElementAtIndex(i);
                float start = cue.FindPropertyRelative("startSeconds").floatValue, end = cue.FindPropertyRelative("endSeconds").floatValue;
                float gap = Mathf.Clamp(start - previousOriginalEnd, 0f, .08f);
                float newStart = Mathf.Max(start, previousNewEnd + gap);
                float duration = catalog.Duration(cue.FindPropertyRelative("textKey").stringValue);
                float newEnd = Mathf.Max(end + newStart - start, newStart + (duration > 0f ? duration + .2f : 0f));
                if (Mathf.Abs(start-newStart)>.001f || Mathf.Abs(end-newEnd)>.001f)
                {
                    cue.FindPropertyRelative("startSeconds").floatValue = newStart; cue.FindPropertyRelative("endSeconds").floatValue = newEnd; changed = true;
                }
                previousOriginalEnd = end; previousNewEnd = newEnd;
            }
            for (int i = 0; i < cues.arraySize; i++)
            {
                var cue = cues.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(cue.FindPropertyRelative("triggerId").stringValue)) continue;
                float duration = catalog.Duration(cue.FindPropertyRelative("textKey").stringValue);
                var minimum = cue.FindPropertyRelative("minimumReadSeconds");
                if (duration > 0f && minimum.floatValue < duration + .2f) { minimum.floatValue = duration + .2f; changed = true; }
            }
            if (previousNewEnd > sequence.TotalDurationSeconds)
            {
                var shots = so.FindProperty("shots"); var last = shots.GetArrayElementAtIndex(shots.arraySize-1);
                float extra = previousNewEnd-sequence.TotalDurationSeconds;
                var duration = last.FindPropertyRelative("durationSeconds"); float old = duration.floatValue; duration.floatValue += extra;
                var frames = last.FindPropertyRelative("keyframes");
                if (frames.arraySize > 0)
                {
                    var finalTime = frames.GetArrayElementAtIndex(frames.arraySize-1).FindPropertyRelative("timeSeconds");
                    if (Mathf.Abs(finalTime.floatValue-old)<.01f) finalTime.floatValue += extra;
                }
                changed = true;
            }
            if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(sequence); }
            return changed;
        }

        public static void FitChapter(string chapter)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<NarrationCatalog>(CatalogPath(chapter));
            if (catalog == null) return;
            var folder = Folders[chapter].Substring(0, Folders[chapter].LastIndexOf('/'));
            foreach (var guid in AssetDatabase.FindAssets("t:SequenceDefinition", new[]{ folder }))
                FitSequence(AssetDatabase.LoadAssetAtPath<SequenceDefinition>(AssetDatabase.GUIDToAssetPath(guid)), catalog);
        }

        public static void ValidateInstalled()
        {
            foreach (var chapter in Chapters)
            {
                var catalog = AssetDatabase.LoadAssetAtPath<NarrationCatalog>(CatalogPath(chapter));
                if (catalog == null || catalog.Lines.Count == 0 || catalog.Validate().Count > 0)
                    throw new InvalidOperationException("Invalid narration catalog: " + chapter);
            }
            foreach (var path in Scenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var players = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<NarrationPlayer>(true)).ToArray();
                if (players.Length != 1 || players[0].Catalog == null)
                    throw new InvalidOperationException("Expected one configured narration player: " + path);
            }
        }
    }
}
