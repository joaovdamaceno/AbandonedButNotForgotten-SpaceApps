using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-011 (R4): prefabs visuais SpiritVisual/OpportunityVisual a partir de <c>MER_Rover.fbx</c>
    /// (modelo próprio riggado, gerado em <c>SourceAssets/Mars/MER/generator</c>).
    /// As duas gêmeas são variantes do mesmo modelo: malhas, materiais e texturas compartilhados.
    /// Menus: Abandoned Space Apps → Mars → MAR-011. Linha de comando:
    /// <c>-executeMethod Abandoned.SpaceApps.Chapters.Mars.EditorTools.MarsRoverVisualAssets.BuildFromCommandLine</c>
    /// ou <c>.ValidateFromCommandLine</c> (código de saída 0 = aprovado).
    /// </summary>
    public static class MarsRoverVisualAssets
    {
        public const string Folder = "Assets/_Game/Chapters/Mars/Art/Rover";
        public const string ModelPath = Folder + "/MER_Rover.fbx";
        public const string AtlasMaterialPath = Folder + "/Materials/M_MER_Atlas.mat";
        public const string SolarMaterialPath = Folder + "/Materials/M_MER_Solar.mat";
        public const string TextureFolder = Folder + "/Textures";
        public const string SpiritPrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/SpiritVisual.prefab";
        public const string OpportunityPrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/OpportunityVisual.prefab";
        public const string ReportPath = "QA/MAR-011/unity-validation.txt";
        public const string Lod0Suffix = "_LOD0";
        public const string Lod1Suffix = "_LOD1";

        public const float Lod0ScreenRelativeHeight = 0.30f;
        public const float Lod1ScreenRelativeHeight = 0.005f;
        public const int Lod0TriangleBudget = 80000;
        public const int Lod1TriangleBudget = 30000;
        public const float PancamMinHeightMeters = 1.40f;
        public const float PancamMaxHeightMeters = 1.55f;
        public const float WheelDiameterMeters = 0.26f;

        /// <summary>Nós articulados, nomes de Docs/Art/IMPORT_RULES.md §4. Pivô no eixo físico, rotação de repouso identidade.</summary>
        public static readonly string[] RigNodes =
        {
            "Body",
            "Rocker_L", "Rocker_R", "Bogie_L", "Bogie_R",
            "Steer_FL", "Steer_FR", "Steer_RL", "Steer_RR",
            "Wheel_FL", "Wheel_FR", "Wheel_ML", "Wheel_MR", "Wheel_RL", "Wheel_RR",
            "PMA_Mast", "PMA_Head", "HGA_Gimbal", "HGA_Dish",
            "IDD_Shoulder", "IDD_Elbow", "IDD_Wrist", "IDD_Turret",
            "Tool_RAT", "Tool_APXS", "Tool_MB", "Tool_MI",
        };

        /// <summary>Âncoras sem geometria para gameplay/cinema (ver RIG.md).</summary>
        public static readonly string[] Anchors =
        {
            "Anchor_Pancam", "Anchor_Navcam", "Anchor_RAT_Contact", "Anchor_APXS_Contact", "Anchor_MB_Contact",
            "Anchor_MI_Focus", "Anchor_HGA_Boresight", "Anchor_CamFollow", "Anchor_Track_L", "Anchor_Track_R",
        };

        static readonly (string path, string name)[] Prefabs =
        {
            (SpiritPrefabPath, "SpiritVisual"),
            (OpportunityPrefabPath, "OpportunityVisual"),
        };

        // ------------------------------------------------------------------ menus / CLI
        [MenuItem("Abandoned Space Apps/Mars/MAR-011/Reconstruir prefabs visuais do rover")]
        public static void RebuildMenu()
        {
            Rebuild();
            LogReport(Validate(out var report), report);
        }

        [MenuItem("Abandoned Space Apps/Mars/MAR-011/Validar rover")]
        public static void ValidateMenu() => LogReport(Validate(out var report), report);

        public static void BuildFromCommandLine()
        {
            Rebuild();
            var errors = Validate(out var report);
            WriteReport(report);
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        public static void ValidateFromCommandLine()
        {
            var errors = Validate(out var report);
            WriteReport(report);
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        static void LogReport(int errors, string report)
        {
            WriteReport(report);
            if (errors == 0) Debug.Log("[MAR-011] Rover aprovado.\n" + report);
            else Debug.LogError($"[MAR-011] {errors} problema(s) no rover.\n" + report);
        }

        static void WriteReport(string report)
        {
            var dir = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
        }

        // ------------------------------------------------------------------ build
        /// <summary>Configura importadores, garante LODGroups e grava as duas variantes (GUIDs preservados).</summary>
        public static void Rebuild()
        {
            ConfigureImporters();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("MAR-011: modelo não encontrado em " + ModelPath);
            foreach (var (path, name) in Prefabs)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    instance.name = name;
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    EnsureLodGroups(instance);
                    PrefabUtility.SaveAsPrefabAsset(instance, path);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            AssetDatabase.SaveAssets();
        }

        static void ConfigureImporters()
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasMaterialPath);
            var solar = AssetDatabase.LoadAssetAtPath<Material>(SolarMaterialPath);
            if (AssetImporter.GetAtPath(ModelPath) is ModelImporter model)
            {
                var changed = !model.bakeAxisConversion || !model.useFileScale || model.isReadable || model.importAnimation
                              || model.animationType != ModelImporterAnimationType.None || model.importCameras || model.importLights
                              || model.meshCompression != ModelImporterMeshCompression.Medium || model.addCollider;
                model.bakeAxisConversion = true;
                model.useFileScale = true;
                model.globalScale = 1f;
                model.isReadable = false;
                model.importAnimation = false;
                model.animationType = ModelImporterAnimationType.None;
                model.importCameras = false;
                model.importLights = false;
                model.addCollider = false;
                model.meshCompression = ModelImporterMeshCompression.Medium;
                model.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                foreach (var (matName, mat) in new[] { ("M_MER_Atlas", atlas), ("M_MER_Solar", solar) })
                {
                    if (mat == null) continue;
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), matName);
                    var map = model.GetExternalObjectMap();
                    if (!map.TryGetValue(id, out var current) || current != mat)
                    {
                        model.AddRemap(id, mat);
                        changed = true;
                    }
                }
                if (changed) model.SaveAndReimport();
            }
            ConfigureTexture("T_MER_Atlas_BaseColor.png", TextureImporterType.Default, true, 2048);
            ConfigureTexture("T_MER_Atlas_Normal.png", TextureImporterType.NormalMap, false, 2048);
            ConfigureTexture("T_MER_Atlas_MetallicSmoothness.png", TextureImporterType.Default, false, 2048);
            ConfigureTexture("T_MER_Solar_BaseColor.png", TextureImporterType.Default, true, 1024);
            ConfigureTexture("T_MER_Solar_Normal.png", TextureImporterType.NormalMap, false, 1024);
            ConfigureTexture("T_MER_Solar_MetallicSmoothness.png", TextureImporterType.Default, false, 1024);
        }

        static void ConfigureTexture(string file, TextureImporterType type, bool srgb, int maxSize)
        {
            if (!(AssetImporter.GetAtPath(TextureFolder + "/" + file) is TextureImporter ti)) return;
            if (ti.textureType == type && ti.sRGBTexture == srgb && ti.maxTextureSize == maxSize && ti.mipmapEnabled
                && !ti.isReadable && ti.alphaIsTransparency == false) return;
            ti.textureType = type;
            ti.sRGBTexture = srgb;
            ti.maxTextureSize = maxSize;
            ti.mipmapEnabled = true;
            ti.isReadable = false;
            ti.alphaIsTransparency = false;
            ti.SaveAndReimport();
        }

        /// <summary>Um LODGroup por nó com filhos _LOD0/_LOD1 (o importador pode já tê-los criado; os limiares são normalizados).</summary>
        public static int EnsureLodGroups(GameObject root)
        {
            var count = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var lod0 = t.Find(t.name + Lod0Suffix);
                var lod1 = t.Find(t.name + Lod1Suffix);
                if (lod0 == null || lod1 == null) continue;
                var r0 = lod0.GetComponents<Renderer>();
                var r1 = lod1.GetComponents<Renderer>();
                if (r0.Length == 0 || r1.Length == 0) continue;
                var group = t.GetComponent<LODGroup>();
                if (group == null) group = t.gameObject.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(Lod0ScreenRelativeHeight, r0),
                    new LOD(Lod1ScreenRelativeHeight, r1),
                });
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ validation
        /// <summary>Retorna o número de erros; <paramref name="report"/> lista cada verificação (OK/ERRO/INFO).</summary>
        public static int Validate(out string report)
        {
            var sb = new StringBuilder();
            var errors = 0;
            void Ok(string m) => sb.AppendLine("OK    " + m);
            void Info(string m) => sb.AppendLine("INFO  " + m);
            void Check(bool ok, string m)
            {
                if (ok) Ok(m);
                else { errors++; sb.AppendLine("ERRO  " + m); }
            }

            sb.AppendLine($"MAR-011 · validação do rover · {DateTime.Now:yyyy-MM-dd HH:mm} · Unity {Application.unityVersion}");

            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Check(importer != null, "MER_Rover.fbx importado como modelo");
            if (importer != null)
            {
                Check(importer.bakeAxisConversion, "Bake Axis Conversion ligado");
                Check(importer.useFileScale && Mathf.Approximately(importer.globalScale, 1f), "Unidades do arquivo, escala global 1");
                Check(!importer.isReadable, "Read/Write desligado");
                Check(!importer.importAnimation && importer.animationType == ModelImporterAnimationType.None, "Sem animação importada (rig dirigido por script)");
                Check(importer.meshCompression == ModelImporterMeshCompression.Medium, "Compressão de malha Medium");
            }

            var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasMaterialPath);
            var solar = AssetDatabase.LoadAssetAtPath<Material>(SolarMaterialPath);
            foreach (var m in new[] { atlas, solar })
            {
                Check(m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit",
                    $"{(m ? m.name : "material ausente")}: URP/Lit");
                if (m == null) continue;
                Check(m.GetTexture("_BaseMap") != null && m.GetTexture("_BumpMap") != null && m.GetTexture("_MetallicGlossMap") != null,
                    $"{m.name}: base, normal e metallic/smoothness atribuídos");
            }
            var normalPaths = new[] { "T_MER_Atlas_Normal.png", "T_MER_Solar_Normal.png" };
            foreach (var n in normalPaths)
            {
                var ti = AssetImporter.GetAtPath(TextureFolder + "/" + n) as TextureImporter;
                Check(ti != null && ti.textureType == TextureImporterType.NormalMap && !ti.flipGreenChannel,
                    $"{n}: Normal map OpenGL (+Y), sem inverter o verde");
            }

            var prefabs = new List<GameObject>();
            foreach (var (path, name) in Prefabs)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(go != null, $"{name}: prefab existe ({path})");
                if (go == null) continue;
                prefabs.Add(go);
                Check(PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Variant
                      && PrefabUtility.GetCorrespondingObjectFromSource(go) != null,
                    $"{name}: variante do MER_Rover.fbx");
                errors += ValidateHierarchy(go, Check, Info);
            }

            if (prefabs.Count == 2)
            {
                var a = Renderers(prefabs[0]);
                var b = Renderers(prefabs[1]);
                var same = a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var other)
                    && other.sharedMesh == kv.Value.sharedMesh
                    && other.GetComponent<Renderer>().sharedMaterials.SequenceEqual(kv.Value.GetComponent<Renderer>().sharedMaterials));
                Check(same, "Spirit e Opportunity compartilham as mesmas malhas e materiais (nenhuma cópia)");
            }

            report = sb.ToString();
            return errors;
        }

        static Dictionary<string, MeshFilter> Renderers(GameObject root) =>
            root.GetComponentsInChildren<MeshFilter>(true).ToDictionary(f => f.name, f => f);

        static int ValidateHierarchy(GameObject go, Action<bool, string> check, Action<string> info)
        {
            var errs = 0;
            void C(bool ok, string m)
            {
                if (!ok) errs++;
                check(ok, m);
            }

            var label = go.name;
            var t = go.transform;
            C(t.localRotation == Quaternion.identity && t.localScale == Vector3.one, $"{label}: raiz com rotação 0 e escala 1");
            var all = go.GetComponentsInChildren<Transform>(true).GroupBy(x => x.name).ToDictionary(g => g.Key, g => g.First());
            var missing = RigNodes.Concat(Anchors).Where(n => !all.ContainsKey(n)).ToList();
            C(missing.Count == 0, $"{label}: {RigNodes.Length} nós de rig e {Anchors.Length} âncoras presentes" + (missing.Count > 0 ? " — faltam " + string.Join(", ", missing) : ""));
            if (missing.Count > 0) return errs;

            var rotated = RigNodes.Where(n => Quaternion.Angle(all[n].localRotation, Quaternion.identity) > 0.01f).ToList();
            C(rotated.Count == 0, $"{label}: pose de repouso com rotação local identidade em todos os nós" + (rotated.Count > 0 ? " — " + string.Join(", ", rotated) : ""));

            Vector3 P(string n) => t.InverseTransformPoint(all[n].position);
            var pancam = P("Anchor_Pancam");
            C(pancam.y >= PancamMinHeightMeters && pancam.y <= PancamMaxHeightMeters,
                $"{label}: Pancam a {pancam.y:0.000} m do solo (referência NASA ≈ 1,5 m)");
            C(pancam.z > 0.3f, $"{label}: frente em +Z (Pancam em z = {pancam.z:0.00})");
            C(P("Wheel_FL").x < 0 && P("Wheel_RL").x < 0 && P("Wheel_FR").x > 0 && P("Wheel_RR").x > 0,
                $"{label}: lado esquerdo em −X, direito em +X");
            C(P("Wheel_FL").z > 0.3f && P("Wheel_RL").z < -0.3f, $"{label}: rodas dianteiras em +Z, traseiras em −Z");
            foreach (var s in new[] { "L", "R" })
            {
                var steerF = P("Steer_F" + s);
                var wheelF = P("Wheel_F" + s);
                var steerR = P("Steer_R" + s);
                var wheelR = P("Wheel_R" + s);
                C(Mathf.Abs(steerF.x - wheelF.x) < 0.01f && Mathf.Abs(steerF.z - wheelF.z) < 0.01f
                  && Mathf.Abs(steerR.x - wheelR.x) < 0.01f && Mathf.Abs(steerR.z - wheelR.z) < 0.01f,
                    $"{label}: eixo de esterço {s} passa pelo centro das rodas (gira sem arrastar)");
            }
            C(Vector3.Distance(Mirror(P("Rocker_L")), P("Rocker_R")) < 0.005f && Vector3.Distance(Mirror(P("Bogie_L")), P("Bogie_R")) < 0.005f,
                $"{label}: pinos do rocker e do bogie simétricos");

            // pin seated: the rocker pivot sits on the WEB differential boss (body side at |x| = 0.34 m, hub 0.395–0.465 m)
            var pin = Mathf.Abs(P("Rocker_L").x);
            C(pin > 0.38f && pin < 0.46f && Mathf.Abs(P("Rocker_L").y - 0.46f) < 0.02f,
                $"{label}: pino do rocker no ressalto do corpo (|x| = {pin:0.000} m, y = {P("Rocker_L").y:0.000} m)");
            info($"{label}: bogie L em {P("Bogie_L")}, esterço FL em {P("Steer_FL")}");

            var wheel = all["Wheel_FL"].Find("Wheel_FL" + Lod0Suffix)?.GetComponent<MeshFilter>();
            if (wheel != null && wheel.sharedMesh != null)
            {
                var size = RootSpaceBounds(wheel, t).size;
                C(Mathf.Abs(size.y - WheelDiameterMeters) < 0.02f && size.x < size.y,
                    $"{label}: roda com {size.y:0.000} m de diâmetro e eixo de rolagem em X local");
            }

            var lod0 = go.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.EndsWith(Lod0Suffix)).ToList();
            var lod1 = go.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name.EndsWith(Lod1Suffix)).ToList();
            var tri0 = lod0.Sum(f => Triangles(f.sharedMesh));
            var tri1 = lod1.Sum(f => Triangles(f.sharedMesh));
            C(tri0 > 0 && tri0 <= Lod0TriangleBudget, $"{label}: LOD0 com {tri0} triângulos (≤ {Lod0TriangleBudget})");
            C(tri1 > 0 && tri1 <= Lod1TriangleBudget, $"{label}: LOD1 com {tri1} triângulos (≤ {Lod1TriangleBudget})");
            var groups = go.GetComponentsInChildren<LODGroup>(true);
            C(groups.Length == lod0.Count && groups.All(g => g.lodCount == 2), $"{label}: {groups.Length} LODGroups (um por nó com geometria)");

            var minY = lod0.Where(f => f.sharedMesh != null).Min(f => RootSpaceBounds(f, t).min.y);
            C(Mathf.Abs(minY) < 0.012f, $"{label}: apoio no solo (menor y = {minY:0.000} m)");

            var mats = go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToList();
            C(mats.Count <= 3 && mats.All(m => m != null), $"{label}: {mats.Count} materiais (≤ 3), nenhum vazio");
            C(go.GetComponentsInChildren<Collider>(true).Length == 0, $"{label}: sem colliders (prefab só visual)");
            return errs;
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        /// <summary>AABB da malha no espaço da raiz, sem depender de Renderer.bounds (vazio em assets fora de cena).</summary>
        static Bounds RootSpaceBounds(MeshFilter filter, Transform root)
        {
            var b = filter.sharedMesh.bounds;
            var m = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return result;
        }

        static int Triangles(Mesh mesh)
        {
            if (mesh == null) return 0;
            var count = 0;
            for (var i = 0; i < mesh.subMeshCount; i++) count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }
    }
}
