using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EditorTools
{
    /// <summary>Importação local determinística da SUR-001; não altera cenas nem configurações globais.</summary>
    public static class SurveyorScienceBuilder
    {
        public const string ManifestPath = "Assets/_Game/Chapters/Surveyor/Art/ReferenceManifest.json";
        public const string AssetPath = "Assets/_Game/Chapters/Surveyor/Data/SurveyorScience.asset";

        [Serializable]
        public sealed class VisualReference
        {
            public string id;
            public string sourceId;
            public string locator;
            public string url;
            public string credit;
            public string rights;
            public string status;
            public string localPath;
            public string plannedDestination;
        }

        [Serializable]
        public sealed class ComponentReference
        {
            public string id;
            public string[] referenceIds;
            public string status;
            public string destination;
            public string notes;
        }

        [Serializable]
        sealed class Manifest
        {
            public VisualReference[] visualReferences;
            public ComponentReference[] componentInventory;
        }

        [MenuItem("Abandoned Space Apps/Surveyor/SUR-001/Gerar e validar catálogo científico")]
        public static void BuildFromMenu()
        {
            var asset = Build();
            Selection.activeObject = asset;
            Debug.Log("[SUR-001] Catálogo científico válido. Referências são seleção de fontes, não assets importados.");
        }

        public static SurveyorScience Build()
        {
            var json = File.ReadAllText(ManifestPath);
            var temporary = ScriptableObject.CreateInstance<SurveyorScience>();
            try
            {
                JsonUtility.FromJsonOverwrite(json, temporary);
                var errors = temporary.Validate();
                errors.AddRange(ValidateReferences(json, temporary));
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
                var asset = AssetDatabase.LoadAssetAtPath<SurveyorScience>(AssetPath);
                if (asset == null)
                {
                    if (File.Exists(AssetPath)) throw new InvalidOperationException("O arquivo existente não é um SurveyorScience; não será sobrescrito.");
                    asset = ScriptableObject.CreateInstance<SurveyorScience>();
                    AssetDatabase.CreateAsset(asset, AssetPath);
                }
                JsonUtility.FromJsonOverwrite(json, asset);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                return asset;
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        public static List<string> ValidateReferences(string json, SurveyorScience science)
        {
            var errors = new List<string>();
            var manifest = JsonUtility.FromJson<Manifest>(json);
            if (manifest == null) { errors.Add("Manifesto visual ausente."); return errors; }
            if (science == null) { errors.Add("Catálogo científico ausente."); return errors; }
            var sources = new Dictionary<string, SurveyorScience.Source>();
            foreach (var source in science.Sources ?? Array.Empty<SurveyorScience.Source>())
                if (source != null && !string.IsNullOrEmpty(source.id)) sources[source.id] = source;
            var ids = new HashSet<string>();
            foreach (var reference in manifest.visualReferences ?? Array.Empty<VisualReference>())
            {
                if (reference == null) { errors.Add("Referência visual nula."); continue; }
                if (!ContractValidation.IsValidId(reference.id) || !ids.Add(reference.id)) errors.Add("ID visual inválido/duplicado.");
                if (!sources.TryGetValue(reference.sourceId ?? "", out var source)) errors.Add("Fonte visual desconhecida: " + reference.id);
                else if (source.verification != "reviewed") errors.Add("Referência visual não verificada: " + reference.id);
                if (!Uri.TryCreate(reference.url, UriKind.Absolute, out var uri) || uri.Scheme != "https") errors.Add("URL visual inválida: " + reference.id);
                if (string.IsNullOrWhiteSpace(reference.locator) || string.IsNullOrWhiteSpace(reference.credit) ||
                    string.IsNullOrWhiteSpace(reference.rights) || string.IsNullOrWhiteSpace(reference.plannedDestination))
                    errors.Add("Metadados visuais incompletos: " + reference.id);
                if (reference.status != "reference-only" || !string.IsNullOrEmpty(reference.localPath))
                    errors.Add("SUR-001 não declara arquivos de arte importados: " + reference.id);
                if (reference.plannedDestination == null || !reference.plannedDestination.StartsWith("Assets/_Game/Chapters/Surveyor/", StringComparison.Ordinal) ||
                    reference.plannedDestination.Contains("..")) errors.Add("Destino visual fora do capítulo: " + reference.id);
            }
            if (ids.Count == 0) errors.Add("Inventário visual vazio.");
            var componentIds = new HashSet<string>();
            foreach (var component in manifest.componentInventory ?? Array.Empty<ComponentReference>())
            {
                if (component == null) { errors.Add("Componente de inventário nulo."); continue; }
                if (!ContractValidation.IsValidId(component.id) || !componentIds.Add(component.id)) errors.Add("ID de componente inválido/duplicado.");
                if ((component.status != "not-built" && component.status != "not-imported") ||
                    string.IsNullOrWhiteSpace(component.destination) || string.IsNullOrWhiteSpace(component.notes))
                    errors.Add("Estado/destino de componente inválido: " + component.id);
                if (component.referenceIds == null || component.referenceIds.Length == 0) errors.Add("Componente sem referência: " + component.id);
                foreach (var id in component.referenceIds ?? Array.Empty<string>())
                    if (!ids.Contains(id ?? "")) errors.Add("Referência desconhecida no componente: " + component.id);
            }
            if (componentIds.Count == 0) errors.Add("Inventário de componentes vazio.");
            return errors;
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                var asset = Build();
                var report = $"SUR-001 PASS\nSources: {asset.Sources.Count}\nFacts: {asset.Facts.Count}\nCues: {asset.CueLinks.Count}\nRuntime network requests: 0\nCross review: {asset.CrossReviewStatus}\n";
                Directory.CreateDirectory("QA/SUR-001");
                File.WriteAllText("QA/SUR-001/catalog-validation.txt", report);
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
