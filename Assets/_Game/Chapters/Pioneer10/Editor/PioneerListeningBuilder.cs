using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-011 · Builder da escuta DSN (CORE-006, raiz <c>[R5] Pioneer Listening</c> na cena da Pioneer):
    /// <c>Art/DsnSilhouette.prefab</c> (modelo NASA da antena DSN de 70 m), <c>UI/ListeningPanel.prefab</c>
    /// (UIDocument + <see cref="ListeningPanelView"/>), <see cref="PioneerListening"/> ligada ao palco e a sequência
    /// <c>Sequences/PIO_Listening.asset</c> (52 s).
    /// <para>
    /// A antena é o modelo NASA "Deep Space Network 70-meter" (NASA/Brian E. Kumanchik; NASA/Christian A. Lopez), convertido por
    /// <c>Tools/convert_dsn70.py</c> (hierarquia azimute › elevação preservada, atlas 1024²) e montado aqui a partir de
    /// <c>Data/Assets/Dsn70*.json</c>. Entra reduzido à escala do palco (<see cref="ModelScale"/>): a câmera continua a 36 m.
    /// </para>
    /// </summary>
    public sealed class PioneerListeningBuilder : SceneBuilderModule
    {
        public const string ListeningBuilderId = "pioneer10.stage.listening"; // depois de pioneer10.stage, de que depende
        public const string ListeningRootName = "[R5] Pioneer Listening";
        public const string PanelPrefabPath = ChapterFolder + "/UI/ListeningPanel.prefab";
        public const string PanelLayoutPath = ChapterFolder + "/UI/ListeningPanel.uxml";
        public const string DishPrefabPath = ChapterFolder + "/Art/DsnSilhouette.prefab";
        public const string SequencePath = ChapterFolder + "/Sequences/PIO_Listening.asset";
        const string PanelSettingsPath = "Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset"; // UI-001 (consumido, não editado)
        const string DsnFolder = ArtFolder + "/Dsn";

        public const string GeometryPath = "Data/Assets/Dsn70.geometry.json";
        public const string AtlasPath = DsnFolder + "/Textures/DsnAtlas.png";
        /// <summary>O prato real tem 70 m (raio 35 m); no palco ele é reduzido a 26 m (raio 13 m), como a silhueta anterior: a câmera fica a 36 m.</summary>
        public const float ModelScale = 13f / 35f;
        /// <summary>Elevação do prato a partir do zênite (o modelo está em "zênite") e giro do azimute para voltar o prato à câmera do anchor (em −Z).</summary>
        const float DishTiltFromZenithDegrees = 40f;
        const float AzimuthYawDegrees = 90f;

        [Serializable] sealed class DsnGeometry { public DsnNode[] nodes; }

        [Serializable]
        sealed class DsnNode
        {
            public string name;
            public string parent;
            public float[] translation;
            public float[] positions;
            public float[] normals;
            public float[] uv;
            public int[] indices;
        }

        public override string Id => ListeningBuilderId;
        public override string Owner => "R5 · PIO-011";
        public override string ScenePath => SceneAssetPath;
        public override string RootName => ListeningRootName;
        public override string AssetFolder => ChapterFolder;

        public override void Build(SceneBuildContext context)
        {
            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).FirstOrDefault();
            if (stage == null)
                throw new System.InvalidOperationException("o palco da PIO-003 (pioneer10.stage) precisa existir antes da escuta.");

            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelLayoutPath);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (layout == null || panelSettings == null)
                throw new System.InvalidOperationException("ListeningPanel.uxml ou SpaceAppsPanelSettings ausente.");

            // Antena (prefab) --------------------------------------------------------------------------------------
            var dishPrefab = BuildDishPrefab(context);

            // Painel (prefab) --------------------------------------------------------------------------------------
            var panelPrefab = context.Prefab(PanelPrefabPath, root =>
            {
                var document = Get<UIDocument>(root);
                document.panelSettings = panelSettings;
                document.visualTreeAsset = layout;
                document.sortingOrder = 10;
                var view = Get<ListeningPanelView>(root);
                Set(view, "document", document);
                Get<ResponsiveLayoutClass>(root); // UI_SPEC: todo UIDocument de tela cheia aplica sa-layout--narrow em 4:3
            });

            var parent = context.Root.transform;
            var panel = context.PrefabInstance(parent, panelPrefab, "ListeningPanel");
            var dish = context.PrefabInstance(parent, dishPrefab, "DsnSilhouette");
            var sequence = BuildSequence(context);
            var listening = context.Component<PioneerListening>(context.Child(parent, "PioneerListening"));
            var so = new SerializedObject(listening);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("presentation").objectReferenceValue = stage;
            so.FindProperty("panel").objectReferenceValue = panel.GetComponent<ListeningPanelView>();
            so.FindProperty("dishRoot").objectReferenceValue = dish.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Monta <c>Art/DsnSilhouette.prefab</c> a partir do modelo NASA convertido: a hierarquia mecânica (base, azimute, elevação,
        /// prato, alimentador, quadripé, treliças) vira GameObjects com as articulações em seus pivôs; as peças que não giram
        /// ficam no azimute. O prato é posado a <see cref="DishTiltFromZenithDegrees"/> do zênite, voltado para a câmera do anchor.
        /// </summary>
        static GameObject BuildDishPrefab(SceneBuildContext context)
        {
            var geometry = JsonUtility.FromJson<DsnGeometry>(File.ReadAllText(GeometryPath));
            if (geometry?.nodes == null || geometry.nodes.Length == 0)
                throw new InvalidOperationException("Dados do conversor ausentes ou inválidos: rode Tools/convert_dsn70.py.");

            ConfigureAtlasImport();
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            if (atlas == null)
                throw new InvalidOperationException("Atlas ausente: " + AtlasPath);

            var material = Lit(context, "PIO_DsnModel", Color.white, 0f, 0.3f, atlas, DsnFolder);
            material.SetFloat("_Cull", 0f); // dupla face, como no original (cascas finas do prato e das treliças)
            EditorUtility.SetDirty(material);

            var meshes = new Dictionary<string, Mesh>();
            foreach (var node in geometry.nodes.Where(n => n.indices != null && n.indices.Length > 0))
            {
                var mesh = MeshAsset(context, "PIO_Dsn_" + node.name.Replace('.', '_'), m => FillMesh(m, node), DsnFolder);
                MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Medium);
                EditorUtility.SetDirty(mesh);
                meshes[node.name] = mesh;
            }

            return context.Prefab(DishPrefabPath, root =>
            {
                // Peças da versão anterior (silhueta por código) não ficam órfãs no prefab.
                foreach (var child in root.transform.Cast<Transform>().ToList())
                {
                    if (child.name != "Model")
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                }

                var model = Ensure(root.transform, "Model").transform;
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                model.localScale = Vector3.one * ModelScale;

                var created = new Dictionary<string, Transform> { { "_root", model } };
                var pending = geometry.nodes.Where(n => n.name != "_root").ToList();
                while (pending.Count > 0)
                {
                    var progressed = false;
                    foreach (var node in pending.ToList())
                    {
                        if (!created.TryGetValue(node.parent ?? "_root", out var parent))
                            continue;

                        var go = Ensure(parent, node.name);
                        go.transform.localPosition = new Vector3(node.translation[0], node.translation[1], node.translation[2]);
                        go.transform.localRotation = Quaternion.identity;
                        go.transform.localScale = Vector3.one;
                        if (meshes.TryGetValue(node.name, out var mesh))
                            Visual(go, mesh, material);
                        created[node.name] = go.transform;
                        pending.Remove(node);
                        progressed = true;
                    }

                    if (!progressed)
                        throw new InvalidOperationException("Nó sem pai no modelo da antena: " + pending[0].name);
                }

                created["azimuth"].localRotation = Quaternion.Euler(0f, AzimuthYawDegrees, 0f);
                created["elevation"].localRotation = Quaternion.Euler(0f, 0f, -DishTiltFromZenithDegrees);
            });
        }

        static void FillMesh(Mesh mesh, DsnNode node)
        {
            var count = node.positions.Length / 3;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var uvs = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(node.positions[i * 3], node.positions[i * 3 + 1], node.positions[i * 3 + 2]);
                normals[i] = new Vector3(node.normals[i * 3], node.normals[i * 3 + 1], node.normals[i * 3 + 2]);
                uvs[i] = new Vector2(node.uv[i * 2], node.uv[i * 2 + 1]);
            }

            mesh.Clear();
            mesh.indexFormat = count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(node.indices, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Atlas: sRGB, mipmaps, repetição travada; override WebGL em DXT1 (RGB opaco), 1024.</summary>
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
                         && importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize == 1024 && importer.alphaSource == TextureImporterAlphaSource.None
                         && web.overridden && web.maxTextureSize == 1024 && web.format == TextureImporterFormat.DXT1;
            if (wanted)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            web.overridden = true;
            web.maxTextureSize = 1024;
            web.format = TextureImporterFormat.DXT1;
            web.compressionQuality = 50;
            importer.SetPlatformTextureSettings(web);
            importer.SaveAndReimport();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var sequence = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(SequencePath);
            if (sequence == null)
            {
                errors.Add($"{ListeningBuilderId}: {SequencePath} ausente.");
                return;
            }

            errors.AddRange(sequence.Validate().Select(e => $"{ListeningBuilderId}: {e}"));
            if (Mathf.Abs(sequence.TotalDurationSeconds - 52f) > 0.01f)
                errors.Add($"{ListeningBuilderId}: PIO.Listening dura {sequence.TotalDurationSeconds} s; o roteiro pede 52 s.");

            var stage = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerPresentation>(true)).First();
            foreach (var shot in sequence.Shots)
            {
                if (!stage.TryGetAnchor(shot.RigId, out _))
                    errors.Add($"{ListeningBuilderId}: plano {shot.ShotId} usa rig '{shot.RigId}', que não é anchor do palco.");
            }

            var listening = context.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PioneerListening>(true)).FirstOrDefault();
            if (listening == null)
                errors.Add($"{ListeningBuilderId}: PioneerListening ausente da cena.");
            else
                errors.AddRange(listening.ValidateSetup().Select(e => $"{ListeningBuilderId}: {e}"));
        }

        static SequenceDefinition BuildSequence(SceneBuildContext context)
        {
            var fresh = CreateSequence();
            fresh.name = "PIO_Listening";
            var asset = context.Asset(SequencePath, () => UnityEngine.Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Listening";
            EditorUtility.SetDirty(asset);
            UnityEngine.Object.DestroyImmediate(fresh);
            return asset;
        }

        static ShotKeyframe Key(float t, Vector3 p, float fov) => new ShotKeyframe(t, p, Vector3.zero, fov);

        /// <summary>
        /// 0–7 ensino, 7–16 contato 1, 16–25 contato 2, 25–37 contato 3, 37–45 tentativa 4, 45–52 respirar. Cada marco de abertura
        /// (7, 16, 25, 37 s) e o de respirar (45 s) espera a etapa anterior. As legendas dos três contatos abrem por gatilho
        /// (PIO.Listening.Contact1/2/3, leitura mínima de 5/5/7 s) e as demais por janela (PIO.07.01, .05 e .06).
        /// </summary>
        public static SequenceDefinition CreateSequence()
        {
            const string rig = PioneerListening.StateId;
            var shots = new[]
            {
                new ShotDefinition("PIO07.Teach", 7f, rig,
                    new[] { Key(0f, Vector3.zero, 55f), Key(7f, new Vector3(0f, 0.2f, 0.8f), 54f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerListening.EventTeach) }),
                new ShotDefinition("PIO07.Contact1", 9f, rig,
                    new[] { Key(0f, new Vector3(2f, 0.4f, 0.8f), 52f), Key(9f, new Vector3(2.4f, 0.6f, 1.4f), 51f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerListening.EventOpen1) }),
                new ShotDefinition("PIO07.Contact2", 9f, rig,
                    new[] { Key(0f, new Vector3(-2f, 0.2f, 1f), 56f), Key(9f, new Vector3(-2.6f, 0.3f, 1.6f), 57f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerListening.EventOpen2) }),
                new ShotDefinition("PIO07.Contact3", 12f, rig,
                    new[] { Key(0f, new Vector3(0f, -0.4f, 2.2f), 50f), Key(12f, new Vector3(0.4f, -0.6f, 3f), 49f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerListening.EventOpen3) }),
                new ShotDefinition("PIO07.Attempt4", 8f, rig,
                    new[] { Key(0f, new Vector3(1.2f, 0.8f, 3.6f), 58f), Key(8f, new Vector3(1.2f, 1f, 4.2f), 59f) },
                    easing: ShotEasing.Linear, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerListening.EventOpen4) }),
                new ShotDefinition("PIO07.Breathe", 7f, rig,
                    new[] { Key(0f, new Vector3(0f, 1.4f, 5f), 60f), Key(7f, new Vector3(0f, 1.8f, 6f), 62f) },
                    easing: ShotEasing.EaseInOut, minGroundClearanceMeters: 0f,
                    events: new[] { new ShotEvent(0f, PioneerListening.EventBreathe) }),
            };

            var cues = new[]
            {
                new SubtitleCue("PIO.07.01", "PIO.07.01", 0f, 7f),
                SubtitleCue.Triggered("PIO.07.02", "PIO.07.02", PioneerListening.StateEventContact1, 5f),
                SubtitleCue.Triggered("PIO.07.03", "PIO.07.03", PioneerListening.StateEventContact2, 5f),
                SubtitleCue.Triggered("PIO.07.04", "PIO.07.04", PioneerListening.StateEventContact3, 7f),
                new SubtitleCue("PIO.07.05", "PIO.07.05", 39f, 45f),
                new SubtitleCue("PIO.07.06", "PIO.07.06", 48f, 52f),
            };

            return SequenceDefinition.Create("PIO_Listening", shots, cues);
        }
    }
}
