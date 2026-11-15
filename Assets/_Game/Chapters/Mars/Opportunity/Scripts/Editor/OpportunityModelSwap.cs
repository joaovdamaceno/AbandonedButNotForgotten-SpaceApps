using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity.EditorTools
{
    /// <summary>
    /// Troca o rover da NASA (malhas texturizadas do glTF original + rig improvisado) pelo modelo da equipe (<c>OpportunityVisual</c>,
    /// MER_Rover.fbx riggado) em <c>OpportunityRover.prefab</c> e <c>OpportunityMemorial.prefab</c>. Edita os prefabs no lugar para preservar
    /// os GUIDs/fileIDs que a cena usa (áudios da RAT, partículas, probe de reflexo, "tool carrier"); só some o que o novo modelo substitui.
    /// Menu: Abandoned Space Apps → Opportunity → Trocar rover pelo modelo da equipe. Idempotente: rodar de novo não troca nada.
    /// </summary>
    public static class OpportunityModelSwap
    {
        const string RoverPrefab = "Assets/_Game/Chapters/Mars/Opportunity/Prefabs/OpportunityRover.prefab";
        const string MemorialPrefab = "Assets/_Game/Chapters/Mars/Opportunity/Prefabs/OpportunityMemorial.prefab";
        const string VisualPrefab = "Assets/_Game/Chapters/Mars/Prefabs/OpportunityVisual.prefab";
        const string VisualName = "OpportunityVisual";

        [MenuItem("Abandoned Space Apps/Opportunity/Trocar rover pelo modelo da equipe")]
        public static void Swap()
        {
            SwapRover();
            SwapMemorial();
            AssetDatabase.SaveAssets();
            Debug.Log("[Opportunity] Rover e memorial agora usam OpportunityVisual (modelo da equipe).");
        }

        public static void BuildFromCommandLine()
        {
            try { Swap(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void SwapRover()
        {
            var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab);
            if (visualAsset == null) throw new InvalidOperationException($"Falta {VisualPrefab}.");
            var root = PrefabUtility.LoadPrefabContents(RoverPrefab);
            try
            {
                var controller = root.GetComponent<RoverController>();
                if (Find(root.transform, VisualName) != null)
                {
                    FitCollar(root, controller);
                    Wire(root.GetComponent<InstrumentArm>(), ("turret", "IDD_Turret"), ("rat", "Tool_RAT"), Find(root.transform, VisualName));
                    PrefabUtility.SaveAsPrefabAsset(root, RoverPrefab);
                    return;
                }

                var oldVisual = controller.visual;
                var carrier = controller.tool;
                
                var keepRoot = new List<Transform>();
                var keepShoulder = new List<Transform>();
                foreach (var t in oldVisual.GetComponentsInChildren<Transform>(true))
                {
                    if (t == oldVisual || t.IsChildOf(carrier)) continue;
                    if (HasRuntimeEffect(t.gameObject)) (t.name.StartsWith("Arm and mast") ? keepShoulder : keepRoot).Add(t);
                }

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset, root.transform);
                visual.name = VisualName;
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;

                // Ponta da RAT = alvo que o capítulo move; fica na face da broca (repouso) e acompanha a inclinação do corpo.
                var contact = Need(visual.transform, "Anchor_RAT_Contact");
                carrier.SetParent(visual.transform, false);
                carrier.SetPositionAndRotation(contact.position, visual.transform.rotation);
                carrier.localScale = Vector3.one;
                foreach (var child in ChildrenOf(carrier))
                    if (!HasRuntimeEffect(child.gameObject) && child != CollarOf(carrier))
                        UnityEngine.Object.DestroyImmediate(child.gameObject);

                var shoulder = Need(visual.transform, "IDD_Shoulder");
                foreach (var t in keepShoulder) t.SetParent(shoulder, true);
                foreach (var t in keepRoot) t.SetParent(root.transform, true);
                UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
                RemoveMissing(root);

                controller.visual = visual.transform;
                controller.wheels = Named(visual.transform, "Wheel_FL", "Wheel_ML", "Wheel_RL", "Wheel_FR", "Wheel_MR", "Wheel_RR");
                controller.steering = Named(visual.transform, "Steer_FL", "Steer_RL", "Steer_FR", "Steer_RR");
                controller.toolRotor = Need(visual.transform, "Tool_RAT");
                controller.rig = root.GetComponent<RoverPresentationRig>();
                Wire(controller.rig, ("rockerLeft", "Rocker_L"), ("rockerRight", "Rocker_R"), ("bogieLeft", "Bogie_L"), ("bogieRight", "Bogie_R"),
                    ("mast", "PMA_Mast"), ("mastHead", "PMA_Head"), ("dish", "HGA_Dish"), visual.transform);
                var arm = root.GetComponent<InstrumentArm>() ?? root.AddComponent<InstrumentArm>();
                Wire(arm, ("shoulder", "IDD_Shoulder"), ("elbow", "IDD_Elbow"), ("wrist", "IDD_Wrist"), ("turret", "IDD_Turret"), ("rat", "Tool_RAT"), ("contact", "Anchor_RAT_Contact"), visual.transform);
                var armObject = new SerializedObject(arm);
                armObject.FindProperty("target").objectReferenceValue = carrier;
                armObject.ApplyModifiedPropertiesWithoutUndo();

                // Rastros saem do contato real das rodas traseiras.
                foreach (var track in controller.tracks)
                {
                    var anchor = Need(visual.transform, track.transform.localPosition.x < 0 ? "Anchor_Track_L" : "Anchor_Track_R");
                    var p = track.transform.localPosition;
                    track.transform.localPosition = new Vector3(root.transform.InverseTransformPoint(anchor.position).x, p.y, root.transform.InverseTransformPoint(anchor.position).z);
                }

                FitCollar(root, controller);
                EditorUtility.SetDirty(controller);
                PrefabUtility.SaveAsPrefabAsset(root, RoverPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Anel de calor da RAT (ToolStressFeedback): acompanha a cabeça real da broca, não o alvo do capítulo.
        static void FitCollar(GameObject root, RoverController controller)
        {
            var collar = CollarOf(controller.tool);
            if (collar == null) return;
            collar.SetParent(controller.toolRotor, false);
            collar.localPosition = new Vector3(0, -.05f, 0);
            collar.localRotation = Quaternion.identity;
            collar.localScale = new Vector3(.09f, .006f, .09f);
        }

        static void SwapMemorial()
        {
            var visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab);
            var root = PrefabUtility.LoadPrefabContents(MemorialPrefab);
            try
            {
                if (Find(root.transform, VisualName) != null) return;
                var portrait = root.GetComponentInChildren<RoverSurfaceWear>(true).transform;
                foreach (var child in ChildrenOf(portrait))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset, portrait);
                visual.name = VisualName;
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(root, MemorialPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool HasRuntimeEffect(GameObject go) =>
            go.GetComponent<AudioSource>() || go.GetComponent<ParticleSystem>() || go.GetComponent<ReflectionProbe>() || go.GetComponent<Light>();

        static Transform CollarOf(Transform carrier)
        {
            foreach (var child in ChildrenOf(carrier))
                if (child.name.Contains("thermal safety collar")) return child;
            return null;
        }

        static List<Transform> ChildrenOf(Transform parent)
        {
            var list = new List<Transform>();
            foreach (Transform child in parent) list.Add(child);
            return list;
        }

        static void RemoveMissing(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        static Transform Need(Transform root, string name) =>
            Find(root, name) ?? throw new InvalidOperationException($"{VisualName}: nó '{name}' não encontrado (ver Art/Rover/RIG.md).");

        static Transform[] Named(Transform root, params string[] names)
        {
            var result = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++) result[i] = Need(root, names[i]);
            return result;
        }

        static void Wire(UnityEngine.Object target, params object[] pairsThenRoot)
        {
            var root = (Transform)pairsThenRoot[pairsThenRoot.Length - 1];
            var so = new SerializedObject(target);
            for (int i = 0; i < pairsThenRoot.Length - 1; i++)
            {
                var (field, node) = ((string, string))pairsThenRoot[i];
                var property = so.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field} não existe.");
                property.objectReferenceValue = Need(root, node);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
