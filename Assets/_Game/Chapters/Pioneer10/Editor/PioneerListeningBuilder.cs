using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Abandoned.SpaceApps.EditorTools.PioneerStageBuilder;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-011 · Builder da escuta DSN (CORE-006, raiz <c>[R5] Pioneer Listening</c> na cena da Pioneer):
    /// <c>Art/DsnSilhouette.prefab</c> (silhueta de antena de 70 m gerada por código), <c>UI/ListeningPanel.prefab</c>
    /// (UIDocument + <see cref="ListeningPanelView"/>), <see cref="PioneerListening"/> ligada ao palco e a sequência
    /// <c>Sequences/PIO_Listening.asset</c> (52 s).
    /// <para>
    /// A silhueta NÃO foi derivada de uma fotografia: o item de referência encontrado (NASA/Ames, "70 meter dish") é um modelo 3D,
    /// não foi baixado e não tem licença própria na página. As proporções (prato parabólico de 70 m com tripé do subrefletor e
    /// base em alidade) são as de uma antena DSN genérica. A escolha de uma fotografia creditada continua pendente (ver README).
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

        /// <summary>Dimensões da silhueta em metros (prato de 70 m reduzido à escala do palco; a câmera fica a 36 m).</summary>
        public const float DishRadiusMeters = 13f;
        public const float DishFocalMeters = 10.4f; // foco = 0,4 do diâmetro, proporção típica de prato de 70 m

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

            // Silhueta (prefab) ------------------------------------------------------------------------------------
            var dishMesh = MeshAsset(context, "PIO_DsnDish", FillDish, DsnFolder);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var sphere = AssetDatabase.LoadAssetAtPath<Mesh>(StageFolder + "/Meshes/PIO_LowSphere.asset");
            var dark = Unlit(context, "PIO_DsnBody", new Color(0.05f, 0.06f, 0.08f), null, DsnFolder);
            var face = Unlit(context, "PIO_DsnDish", new Color(0.2f, 0.23f, 0.29f), null, DsnFolder);
            var dishPrefab = context.Prefab(DishPrefabPath, root =>
            {
                Part(root, "Pedestal", cube, dark, new Vector3(0f, 5f, 0f), new Vector3(7f, 10f, 7f));
                Part(root, "YokeBeam", cube, dark, new Vector3(0f, 10.5f, 0f), new Vector3(20f, 2f, 4f));
                Part(root, "YokeArmLeft", cube, dark, new Vector3(-9f, 12f, 0f), new Vector3(2.2f, 7f, 3.5f));
                Part(root, "YokeArmRight", cube, dark, new Vector3(9f, 12f, 0f), new Vector3(2.2f, 7f, 3.5f));

                // O prato aponta para o céu, a ~50° de elevação e voltado para a câmera do anchor (que está em -Z).
                var pivot = Ensure(root.transform, "DishPivot");
                pivot.transform.localPosition = new Vector3(0f, 14f, 0f);
                pivot.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0.77f, -0.64f), Vector3.up);
                pivot.transform.localScale = Vector3.one;
                Visual(Ensure(pivot.transform, "Dish"), dishMesh, face);

                var focus = new Vector3(0f, 0f, DishFocalMeters);
                Part(pivot, "Subreflector", sphere, dark, focus, Vector3.one * 2f);
                for (var i = 0; i < 3; i++)
                {
                    var angle = (90f + 120f * i) * Mathf.Deg2Rad;
                    var rim = new Vector3(Mathf.Cos(angle) * DishRadiusMeters, Mathf.Sin(angle) * DishRadiusMeters, DishRadiusMeters * DishRadiusMeters / (4f * DishFocalMeters));
                    var leg = Ensure(pivot.transform, "Leg" + i);
                    leg.transform.localPosition = (rim + focus) * 0.5f;
                    leg.transform.localRotation = Quaternion.LookRotation(focus - rim);
                    leg.transform.localScale = new Vector3(0.45f, 0.45f, (focus - rim).magnitude);
                    Visual(leg, cube, dark);
                }
            });

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

        static void Part(GameObject parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var go = Ensure(parent.transform, name);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            Visual(go, mesh, material);
        }

        /// <summary>Paraboloide z = r²/(4f), aberto para +Z, com as duas faces (o material é Unlit e pode ser visto de ambos os lados).</summary>
        static void FillDish(Mesh mesh)
        {
            const int segments = 28;
            const int rings = 6;
            var vertices = new List<Vector3> { new Vector3(0f, 0f, 0f) };
            for (var ring = 1; ring <= rings; ring++)
            {
                var r = DishRadiusMeters * ring / rings;
                for (var s = 0; s < segments; s++)
                {
                    var angle = s * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, r * r / (4f * DishFocalMeters)));
                }
            }

            var triangles = new List<int>();
            void Quad(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(a); triangles.Add(c); triangles.Add(b); }
            for (var s = 0; s < segments; s++)
                Quad(0, 1 + s, 1 + (s + 1) % segments);
            for (var ring = 1; ring < rings; ring++)
            {
                var inner = 1 + (ring - 1) * segments;
                var outer = 1 + ring * segments;
                for (var s = 0; s < segments; s++)
                {
                    var n = (s + 1) % segments;
                    Quad(inner + s, outer + s, outer + n);
                    Quad(inner + s, outer + n, inner + n);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
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
            var asset = context.Asset(SequencePath, () => Object.Instantiate(fresh));
            EditorUtility.CopySerialized(fresh, asset);
            asset.name = "PIO_Listening";
            EditorUtility.SetDirty(asset);
            Object.DestroyImmediate(fresh);
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
