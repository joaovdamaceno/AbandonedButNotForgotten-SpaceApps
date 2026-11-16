using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-007 · Configura a importação das fotos históricas: sRGB, sem mipmaps (imagem exibida plana na UI), sem leitura
    /// pela CPU, repetição travada, no máximo 1024 px e sem reamostrar potência de dois (a imagem não é alterada ao
    /// importar). Idempotente. Menu: Abandoned Space Apps → PIO-007 → Configurar importação das fotos históricas.
    /// </summary>
    public static class PioneerHistoricalImagesImporter
    {
        public const string Folder = "Assets/_Game/Chapters/Pioneer10/Art/Historical";
        public static readonly string[] Files =
        {
            Folder + "/Pioneer10_Ganymede_1973_web.jpg",
            Folder + "/Pioneer10_Jupiter_1973_web.jpg",
        };

        [MenuItem("Abandoned Space Apps/PIO-007/Configurar importação das fotos históricas")]
        public static void ConfigureFromMenu()
        {
            var errors = Configure();
            if (errors.Count == 0)
                Debug.Log("[PIO-007] importação das fotos históricas conforme.");
            else
                Debug.LogError("[PIO-007] problemas:\n- " + string.Join("\n- ", errors));
        }

        public static List<string> Configure()
        {
            var errors = new List<string>();
            foreach (var path in Files)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    importer = AssetImporter.GetAtPath(path) as TextureImporter;
                }

                if (importer == null)
                {
                    errors.Add("arquivo ausente ou não é textura: " + path);
                    continue;
                }

                if (IsConfigured(importer))
                    continue;

                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.crunchedCompression = false;
                importer.SaveAndReimport();
            }

            return errors;
        }

        public static bool IsConfigured(TextureImporter importer) =>
            importer.textureType == TextureImporterType.Default && importer.sRGBTexture && importer.alphaSource == TextureImporterAlphaSource.None
            && !importer.mipmapEnabled && !importer.isReadable && importer.wrapMode == TextureWrapMode.Clamp
            && importer.npotScale == TextureImporterNPOTScale.None && importer.maxTextureSize == 1024
            && importer.textureCompression == TextureImporterCompression.Compressed && !importer.crunchedCompression;
    }
}
