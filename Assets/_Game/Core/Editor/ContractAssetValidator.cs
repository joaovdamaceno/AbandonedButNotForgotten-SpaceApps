using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// Valida todos os ChapterDefinition e SequenceDefinition do projeto antes da execução (CORE-002).
    /// Builders e PRs chamam <see cref="Collect"/>; linha de comando: -executeMethod
    /// Abandoned.SpaceApps.EditorTools.ContractAssetValidator.ValidateFromCommandLine (0 = conforme).
    /// </summary>
    public static class ContractAssetValidator
    {
        [MenuItem("Abandoned Space Apps/CORE-002/Validar contratos de dados", priority = 31)]
        public static void ValidateFromMenu() => Report(Collect());

        public static void ValidateFromCommandLine()
        {
            var exitCode = 0;
            try
            {
                var errors = Collect();
                Report(errors);
                exitCode = errors.Count == 0 ? 0 : 1;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 2;
            }

            EditorApplication.Exit(exitCode);
        }

        public static void Report(IReadOnlyCollection<string> errors)
        {
            if (errors.Count == 0)
                Debug.Log("[CORE-002] Todos os ChapterDefinition e SequenceDefinition estão válidos.");
            else
                Debug.LogError($"[CORE-002] {errors.Count} divergência(s) nos dados de contrato:{ContractValidation.Describe(errors)}");
        }

        public static List<string> Collect()
        {
            var errors = new List<string>();
            var chapterIds = new Dictionary<string, string>();

            foreach (var guid in AssetDatabase.FindAssets("t:ChapterDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);
                if (chapter == null)
                    continue;

                foreach (var error in chapter.Validate())
                    errors.Add($"{path} → {error}");

                if (ContractValidation.IsValidId(chapter.ChapterId))
                {
                    if (chapterIds.TryGetValue(chapter.ChapterId, out var other))
                        errors.Add($"{path} → chapterId '{chapter.ChapterId}' já usado em {other}.");
                    else
                        chapterIds.Add(chapter.ChapterId, path);
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:SequenceDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(path);
                if (sequence == null)
                    continue;

                foreach (var error in sequence.Validate())
                    errors.Add($"{path} → {error}");
            }

            return errors;
        }
    }
}
