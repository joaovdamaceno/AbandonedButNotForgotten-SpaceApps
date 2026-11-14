using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-002 · Builder do prefab da sonda (CORE-006, raiz <c>[R5] Probe Preview</c> numa cena de prévia própria).
    /// Lê <c>Data/Assets/Pioneer10.json</c> e <c>Pioneer10.geometry.json</c> (saída de <c>Tools/convert_pioneer10.py</c>) e
    /// gera malhas, o material único com o atlas e <c>Art/PioneerActors.prefab</c> com o rig: <c>Spin_Root</c> sobre o eixo
    /// da antena, peças com pivô na dobradiça, nó <c>Plaque</c> e sockets com os nomes do palco da PIO-003.
    /// </summary>
    public sealed class PioneerProbeBuilder : SceneBuilderModule
    {
        public const string ProbeBuilderId = "pioneer10.probe";
        public const string ProbeRootName = "[R5] Probe Preview";
        public const string ProbeFolder = ArtFolder + "/Probe";
        public const string PreviewScenePath = ProbeFolder + "/Preview/ProbePreview.unity";
        public const string PrefabAssetPath = ArtFolder + "/PioneerActors.prefab";
        public const string MaterialFolder = ArtFolder + "/PioneerMaterials";
        public const string AtlasPath = ProbeFolder + "/Textures/PioneerAtlas.png";
        public const string ManifestPath = "Data/Assets/Pioneer10.json";
        public const string GeometryPath = "Data/Assets/Pioneer10.geometry.json";
        public const int TriangleBudget = 60000;
        public const float DishDiameterMeters = 2.74f; // P-S05

        public override string Id => ProbeBuilderId;
        public override string Owner => "R5 · PIO-002";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => ProbeRootName;
        public override string AssetFolder => ChapterFolder;

        // ---------- Dados (saída do conversor) ----------

        [Serializable] sealed class GeometryFile { public GeometryPart[] parts; }

        [Serializable]
        sealed class GeometryPart
        {
            public string name;
            public float[] pivot;
            public float[] positions;
            public float[] normals;
            public float[] uv;
            public int[] triangles;
        }

        [Serializable] sealed class ManifestFile { public RigData rig; public Measurements measurements; }

        [Serializable] sealed class Measurements { public float dishDiameterMetersX; }

        [Serializable]
        sealed class RigData
        {
            public HingeData[] hinges;
            public SocketData[] sockets;
            public PlaqueData plaque;
        }

        [Serializable] sealed class HingeData { public string name; public float[] pivot; public float[] axis; public bool axisVerified; }

        [Serializable] sealed class SocketData { public string name; public float[] position; }

        [Serializable] sealed class PlaqueData { public float[] center; public float[] normal; public float[] up; public float[] sizeMeters; }

        static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);

        // ---------- Build ----------

        public override void Build(SceneBuildContext context)
        {
            var geometry = JsonUtility.FromJson<GeometryFile>(File.ReadAllText(GeometryPath));
            var manifest = JsonUtility.FromJson<ManifestFile>(File.ReadAllText(ManifestPath));
            if (geometry?.parts == null || manifest?.rig == null)
                throw new InvalidOperationException("Dados do conversor ausentes ou inválidos: rode Tools/convert_pioneer10.py.");

            ConfigureAtlasImport();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null)
                throw new InvalidOperationException("Atlas ausente: " + AtlasPath);

            var probe = ProbeMaterial(context, "PIO_ProbeAtlas", atlas);
            var plaqueMaterial = PlaqueMaterial(context, "PIO_PlaqueAnodized_PROXY");
            var meshes = geometry.parts.ToDictionary(p => p.name, p => MeshAsset(context, "PIO_Probe" + p.name, mesh => FillMesh(mesh, p), ProbeFolder));
            var plaqueMesh = MeshAsset(context, "PIO_Plaque_PROXY", mesh =>
            {
                var size = V(manifest.rig.plaque.sizeMeters);
                PioneerStageArt.Box(mesh, "PIO_Plaque_PROXY", size);
            }, ProbeFolder);

            var prefab = context.Prefab(PrefabAssetPath, root => ConfigureProbe(root, geometry, manifest.rig, meshes, plaqueMesh, probe, plaqueMaterial));
            context.PrefabInstance(context.Root.transform, prefab, "PioneerActors");
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var rig = context.Root.GetComponentInChildren<PioneerProbeRig>(true);
            if (rig == null)
            {
                errors.Add($"{ProbeBuilderId}: PioneerProbeRig ausente.");
                return;
            }

            errors.AddRange(rig.ValidateSetup().Select(e => $"{ProbeBuilderId}: {e}"));
            var report = PioneerProbeChecks.Run(rig);
            errors.AddRange(report.Errors.Select(e => $"{ProbeBuilderId}: {e}"));
        }

        static void FillMesh(Mesh mesh, GeometryPart part)
        {
            var count = part.positions.Length / 3;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var uvs = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(part.positions[i * 3], part.positions[i * 3 + 1], part.positions[i * 3 + 2]);
                normals[i] = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
                uvs[i] = new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
            }

            mesh.Clear();
            mesh.name = "PIO_Probe" + part.name;
            mesh.indexFormat = count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(part.triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Atlas: sRGB, mipmaps, repetição travada; override WebGL em DXT1 (RGB opaco), até 2048.</summary>
        static void ConfigureAtlasImport()
        {
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
                importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            }

            var web = importer.GetPlatformTextureSettings("WebGL");
            var wanted = importer.textureType == TextureImporterType.Default && importer.sRGBTexture && importer.mipmapEnabled
                         && importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize == 2048 && importer.alphaSource == TextureImporterAlphaSource.None
                         && web.overridden && web.maxTextureSize == 2048 && web.format == TextureImporterFormat.DXT1;
            if (wanted)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            web.overridden = true;
            web.maxTextureSize = 2048;
            web.format = TextureImporterFormat.DXT1;
            web.compressionQuality = 50;
            importer.SetPlatformTextureSettings(web);
            importer.SaveAndReimport();
        }

        static Material ProbeMaterial(SceneBuildContext context, string name, Texture2D atlas)
        {
            var material = context.Material(MaterialFolder + "/" + name + ".mat", "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", atlas);
            material.SetFloat("_Metallic", 0f);      // como no original: metalicidade 0, rugosidade 0,5
            material.SetFloat("_Smoothness", 0.5f);
            material.SetFloat("_Cull", 0f);          // dupla face, como no original (cascas finas do refletor e das hastes)
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material PlaqueMaterial(SceneBuildContext context, string name)
        {
            var material = context.Material(MaterialFolder + "/" + name + ".mat", "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", new Color(0.86f, 0.69f, 0.40f)); // alumínio anodizado dourado, não ouro maciço (P-S12)
            material.SetFloat("_Metallic", 0.3f);
            material.SetFloat("_Smoothness", 0.55f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Prefab ----------

        static void ConfigureProbe(GameObject root, GeometryFile geometry, RigData rig, Dictionary<string, Mesh> meshes,
            Mesh plaqueMesh, Material probeMaterial, Material plaqueMaterial)
        {
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var spin = Ensure(root.transform, "Spin_Root").transform;
            spin.localPosition = Vector3.zero;
            spin.localRotation = Quaternion.identity;

            var pivots = geometry.parts.ToDictionary(p => p.name, p => V(p.pivot));
            Transform Part(Transform parent, string name)
            {
                var go = Visual(Ensure(parent, name), meshes[name], probeMaterial);
                go.transform.localPosition = pivots[name];
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                return go.transform;
            }

            Part(spin, "Bus");
            var dish = Part(spin, "Dish");
            Part(dish, "Feed");
            var boomA = Part(spin, "RtgBoomA");
            var boomB = Part(spin, "RtgBoomB");
            var magnetometer = Part(spin, "MagnetometerBoom");

            // Placa: nó próprio para o close (IMPORT_RULES §4). Provisória: o modelo não a traz.
            var plaque = Visual(Ensure(spin, "Plaque"), plaqueMesh, plaqueMaterial).transform;
            plaque.localPosition = V(rig.plaque.center);
            plaque.localRotation = Quaternion.LookRotation(V(rig.plaque.normal), V(rig.plaque.up));
            plaque.localScale = Vector3.one;

            // Sockets das hastes: filhos da dobradiça (acompanham a articulação); os demais ficam em "Sockets".
            var boomPivots = new Dictionary<string, Transform> { { "RtgBoomA", boomA }, { "RtgBoomB", boomB }, { "MagnetometerBoom", magnetometer } };
            var socketRoot = Ensure(spin, "Sockets").transform;
            var socketTransforms = new List<Transform>();
            foreach (var socket in rig.sockets)
            {
                var parent = boomPivots.TryGetValue(socket.name, out var pivot) ? pivot : socketRoot;
                var target = Ensure(parent, socket.name).transform;
                target.position = spin.TransformPoint(V(socket.position)); // posição de mundo do modelo em repouso
                target.localRotation = Quaternion.identity;
                socketTransforms.Add(target);
            }

            // Sockets de versões anteriores do prefab (ex.: os das hastes, que antes ficavam aqui) não ficam órfãos.
            foreach (Transform child in socketRoot.Cast<Transform>().ToList())
            {
                if (!socketTransforms.Contains(child))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var component = Get<PioneerProbeRig>(root);
            var so = new SerializedObject(component);
            so.FindProperty("spinRoot").objectReferenceValue = spin;
            so.FindProperty("plaque").objectReferenceValue = plaque;
            SetArray(so.FindProperty("sockets"), socketTransforms, (p, t) => p.objectReferenceValue = t);
            var pivotsByBoom = boomPivots;
            SetArray(so.FindProperty("hinges"), rig.hinges, (p, h) =>
            {
                p.FindPropertyRelative("boom").enumValueIndex = (int)(PioneerBoom)Enum.Parse(typeof(PioneerBoom), h.name);
                p.FindPropertyRelative("pivot").objectReferenceValue = pivotsByBoom[h.name];
                p.FindPropertyRelative("axis").vector3Value = V(h.axis);
                p.FindPropertyRelative("maxDegrees").floatValue = 90f;
                p.FindPropertyRelative("axisVerified").boolValue = h.axisVerified;
            });
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
