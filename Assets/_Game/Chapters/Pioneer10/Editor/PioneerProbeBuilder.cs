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
        /// <summary>LOD1 (planos distantes, IMPORT_RULES §5): agrupamento de vértices numa grade de 15 cm, uma malha só.</summary>
        public const float Lod1CellMeters = 0.15f;
        public const string Lod1Name = "Lod1";
        /// <summary>Altura relativa na tela abaixo da qual entra o LOD1, e abaixo da qual a sonda some (corte).</summary>
        public const float Lod1TransitionHeight = 0.12f;
        public const float CullTransitionHeight = 0.01f;
        /// <summary>O LOD1 deve ter no máximo esta fração dos triângulos do LOD0.</summary>
        public const float Lod1MaxTriangleFraction = 0.30f;
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

            var lod1Mesh = MeshAsset(context, "PIO_Probe" + Lod1Name, mesh => FillLod1(mesh, geometry), ProbeFolder);
            // IMPORT_RULES §7: compressão de malha média nos assets. (Read/Write continua ligado: são assets gerados que o
            // builder reescreve e que as verificações leem; não passam pelo ModelImporter.)
            foreach (var mesh in meshes.Values.Concat(new[] { plaqueMesh, lod1Mesh }))
            {
                MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Medium);
                EditorUtility.SetDirty(mesh);
            }

            var prefab = context.Prefab(PrefabAssetPath, root => ConfigureProbe(root, geometry, manifest.rig, meshes, plaqueMesh, lod1Mesh, probe, plaqueMaterial));
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
            mesh.Optimize(); // IMPORT_RULES §7: Optimize Mesh (cache de vértices); determinístico
        }

        /// <summary>
        /// LOD1: agrupa os vértices de cada peça numa grade de <see cref="Lod1CellMeters"/> (no espaço do Spin_Root, pose de
        /// repouso) E por direção dominante da normal (seis faces), para que a frente e o verso das cascas finas (o refletor)
        /// não se fundam e não percam cor e iluminação. Funde os vértices da mesma célula e direção (posição, normal e UV médios), descarta triângulos degenerados ou
        /// repetidos e reúne tudo numa malha com o MESMO material do atlas. A silhueta (refletor, corpo, hastes) é mantida;
        /// os fios finos das hastes encolhem, o que só é visível de perto (e de perto vale o LOD0).
        /// </summary>
        /// <summary>Face dominante da normal: 0–5 = ±X, ±Y, ±Z.</summary>
        static int FaceOf(Vector3 normal)
        {
            var ax = Mathf.Abs(normal.x);
            var ay = Mathf.Abs(normal.y);
            var az = Mathf.Abs(normal.z);
            if (ax >= ay && ax >= az)
                return normal.x >= 0f ? 0 : 1;
            return ay >= az ? (normal.y >= 0f ? 2 : 3) : (normal.z >= 0f ? 4 : 5);
        }

        static void FillLod1(Mesh mesh, GeometryFile geometry)
        {
            var pivots = geometry.parts.ToDictionary(p => p.name, p => V(p.pivot));
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var part in geometry.parts)
            {
                // posição da peça no Spin_Root: as peças têm rotação identidade; a Feed é filha da Dish
                var offset = part.name == "Feed" ? pivots["Dish"] + pivots["Feed"] : pivots[part.name];
                var count = part.positions.Length / 3;
                var cellOf = new Dictionary<(int, int, int, int), int>();
                var sumPosition = new List<Vector3>();
                var sumNormal = new List<Vector3>();
                var sumUv = new List<Vector2>();
                var members = new List<int>();
                var remap = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var position = new Vector3(part.positions[i * 3], part.positions[i * 3 + 1], part.positions[i * 3 + 2]) + offset;
                    var normalHere = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
                    var key = (Mathf.FloorToInt(position.x / Lod1CellMeters), Mathf.FloorToInt(position.y / Lod1CellMeters), Mathf.FloorToInt(position.z / Lod1CellMeters), FaceOf(normalHere));
                    if (!cellOf.TryGetValue(key, out var cluster))
                    {
                        cluster = sumPosition.Count;
                        cellOf[key] = cluster;
                        sumPosition.Add(Vector3.zero);
                        sumNormal.Add(Vector3.zero);
                        sumUv.Add(Vector2.zero);
                        members.Add(0);
                    }

                    sumPosition[cluster] += position;
                    sumNormal[cluster] += normalHere;
                    sumUv[cluster] += new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
                    members[cluster]++;
                    remap[i] = cluster;
                }

                var baseIndex = vertices.Count;
                for (var c = 0; c < sumPosition.Count; c++)
                {
                    vertices.Add(sumPosition[c] / members[c]);
                    var normal = sumNormal[c];
                    normals.Add(normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector3.up);
                    uvs.Add(sumUv[c] / members[c]);
                }

                var seen = new HashSet<(int, int, int)>();
                for (var t = 0; t < part.triangles.Length; t += 3)
                {
                    int a = remap[part.triangles[t]], b = remap[part.triangles[t + 1]], c = remap[part.triangles[t + 2]];
                    if (a == b || b == c || a == c)
                        continue;

                    var sorted = new[] { a, b, c };
                    System.Array.Sort(sorted);
                    if (!seen.Add((sorted[0], sorted[1], sorted[2])))
                        continue;

                    triangles.Add(baseIndex + a);
                    triangles.Add(baseIndex + b);
                    triangles.Add(baseIndex + c);
                }
            }

            mesh.Clear();
            mesh.name = "PIO_Probe" + Lod1Name;
            mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.Optimize();
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
            Mesh plaqueMesh, Mesh lod1Mesh, Material probeMaterial, Material plaqueMaterial)
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

            // LOD1 (planos distantes): uma malha só, no espaço do Spin_Root, com o mesmo material. O LODGroup fica na raiz.
            var lod1 = Visual(Ensure(spin, Lod1Name), lod1Mesh, probeMaterial).transform;
            lod1.localPosition = Vector3.zero;
            lod1.localRotation = Quaternion.identity;
            lod1.localScale = Vector3.one;
            var lod0Renderers = spin.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.transform != lod1).Cast<Renderer>().ToArray();
            var group = Get<LODGroup>(root);
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(new[]
            {
                new LOD(Lod1TransitionHeight, lod0Renderers),
                new LOD(CullTransitionHeight, new Renderer[] { lod1.GetComponent<MeshRenderer>() }),
            });
            group.RecalculateBounds();

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
