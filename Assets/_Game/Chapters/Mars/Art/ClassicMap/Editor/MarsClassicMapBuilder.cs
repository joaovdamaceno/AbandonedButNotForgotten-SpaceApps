using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// Mapa importado da cratera Eagle: o terreno e as rochas importados em <c>Art/Classic</c>, com as
    /// âncoras que as atividades de Marte esperam (rota, esferas, McKittrick). O ponto de pouso vira a origem; o norte (+Z) leva
    /// ao afloramento das esferas e depois à McKittrick, como lá. Só gera o prefab se os assets importados existirem.
    /// </summary>
    public static class MarsClassicMapBuilder
    {
        public const string ClassicRoot = "Assets/_Game/Chapters/Mars/Art/Classic";
        public const string SourcePrefabPath = ClassicRoot + "/Export/ClassicEagle.prefab";
        public const string SkyPath = ClassicRoot + "/Materials/Mars sky.mat";
        public const string PrefabPath = "Assets/_Game/Chapters/Mars/Prefabs/EagleEnvironmentClassic.prefab";

        // Posições no terreno importado, relativas ao ponto de pouso (a origem): afloramento das esferas e rocha McKittrick.
        static readonly Vector2 Outcrop = new Vector2(-1f, 22f);
        static readonly Vector2 BerryStation = new Vector2(-1f, 19.2f);
        static readonly Vector2 RockStation = new Vector2(18f, 53.8f);
        static readonly Vector2 Rock = new Vector2(18f, 56.8f);

        [MenuItem("Abandoned Space Apps/Marte/Reconstruir mapa importado da Eagle")]
        public static void BuildFromMenu()
        {
            if (!Build())
                throw new InvalidOperationException("Assets do mapa importado ausentes: " + SourcePrefabPath);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                var ok = Build();
                EditorApplication.Exit(ok ? 0 : 2);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static bool Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
                return false;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("EagleEnvironmentClassic");
            var map = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            map.name = "Map";
            var removed = MarsClassicBudget.Trim(map, Vector3.zero);
            MarsClassicBudget.ConvertMaterialsAndTextures(map);
            Physics.SyncTransforms();

            float GroundY(Vector2 p) =>
                Physics.Raycast(new Vector3(p.x, 60f, p.y), Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;

            Vector3 Ground(Vector2 p) => new Vector3(p.x, GroundY(p), p.y);

            Transform Node(Transform parent, string name, Vector3 position, Vector3 lookAt)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.position = position;
                var direction = lookAt - position;
                if (direction.sqrMagnitude > 1e-6f)
                    t.rotation = Quaternion.LookRotation(direction, Vector3.up);
                return t;
            }

            var anchorsRoot = new GameObject("Anchors").transform;
            anchorsRoot.SetParent(root.transform, false);
            var anchors = anchorsRoot.gameObject.AddComponent<EagleAnchors>();

            // Rota: do pouso ao afloramento e à McKittrick, já no chão, a cada ~0,5 m.
            var waypoints = new[] { Vector2.zero, new Vector2(-0.5f, 10f), BerryStation, new Vector2(6f, 33f), new Vector2(13f, 45f), RockStation };
            var stationIndices = new List<int>();
            var samples = new List<Transform>();
            var routeRoot = new GameObject("Route").transform;
            routeRoot.SetParent(anchorsRoot, false);
            for (var leg = 0; leg < waypoints.Length - 1; leg++)
            {
                var length = Vector2.Distance(waypoints[leg], waypoints[leg + 1]);
                var steps = Mathf.Max(1, Mathf.RoundToInt(length / 0.5f));
                for (var i = 0; i < steps; i++)
                {
                    var p = Vector2.Lerp(waypoints[leg], waypoints[leg + 1], i / (float)steps);
                    samples.Add(Node(routeRoot, $"S{samples.Count:000}", Ground(p), Ground(waypoints[leg + 1])));
                }

                if (leg == 1)
                    stationIndices.Add(samples.Count);
            }

            samples.Add(Node(routeRoot, $"S{samples.Count:000}", Ground(RockStation), Ground(Rock)));
            var berryIndex = stationIndices[0];
            var rockIndex = samples.Count - 1;

            // Esferas (afloramento "spherules in layered outcrop"): chegada, solo, rocha.
            var berries = new GameObject("BlueberriesAnchors").transform;
            berries.SetParent(anchorsRoot, false);
            var outcrop = Ground(Outcrop) + Vector3.up * 0.45f;
            var berriesTarget = berries.gameObject.AddComponent<EagleTargetAnchors>();
            var berryPrompt = Node(berries, "Prompt", Ground(BerryStation), outcrop);
            var berryStation = Node(berries, "Station", Ground(BerryStation), outcrop);
            var berrySoil = Node(berries, "SoilView", Ground(new Vector2(-1.5f, 20.4f)) + Vector3.up * 0.7f, Ground(new Vector2(-1.2f, 21.6f)) + Vector3.up * 0.05f);
            var berryRock = Node(berries, "RockView", Ground(new Vector2(-0.3f, 20.0f)) + Vector3.up * 1.5f, outcrop + Vector3.up * 0.4f);
            Set(berriesTarget, ("promptAnchor", berryPrompt), ("stationAnchor", berryStation), ("primaryView", berrySoil), ("secondaryView", berryRock));

            // McKittrick: superfície de trabalho sobre a rocha da referência do instrumento.
            var kitt = new GameObject("McKittrickAnchors").transform;
            kitt.SetParent(anchorsRoot, false);
            var kittTarget = kitt.gameObject.AddComponent<EagleTargetAnchors>();
            var rockTop = Ground(Rock);
            var rockPrompt = Node(kitt, "Prompt", Ground(RockStation), rockTop);
            var rockStationNode = Node(kitt, "Station", Ground(RockStation), rockTop);
            var rockView = Node(kitt, "WorkView", rockTop + new Vector3(0f, 0.9f, -1.3f), rockTop);
            var mount = new GameObject("RatSurfaceMount").transform;
            mount.SetParent(kitt, false);
            mount.position = rockTop + Vector3.up * 0.02f;
            mount.rotation = Quaternion.identity;
            Set(kittTarget, ("promptAnchor", rockPrompt), ("stationAnchor", rockStationNode), ("primaryView", rockView), ("ratSurfaceMount", mount));

            // Plano elevado que revela o afloramento (M05/M07).
            var overviewTarget = Node(anchorsRoot, "OverviewTarget", outcrop, outcrop);
            var overview = Node(anchorsRoot, "OverviewCamera", Ground(new Vector2(6f, 6f)) + Vector3.up * 9f, outcrop);

            var so = new SerializedObject(anchors);
            so.FindProperty("routeStart").objectReferenceValue = samples[0];
            var array = so.FindProperty("routeSamples");
            array.arraySize = samples.Count;
            for (var i = 0; i < samples.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = samples[i];
            so.FindProperty("blueberriesStationIndex").intValue = berryIndex;
            so.FindProperty("mcKittrickStationIndex").intValue = rockIndex;
            so.FindProperty("corridorWidthMeters").floatValue = 3.2f;
            so.FindProperty("blueberries").objectReferenceValue = berriesTarget;
            so.FindProperty("mcKittrick").objectReferenceValue = kittTarget;
            so.FindProperty("overviewCamera").objectReferenceValue = overview;
            so.FindProperty("overviewTarget").objectReferenceValue = overviewTarget;
            so.ApplyModifiedPropertiesWithoutUndo();

            var atmosphere = root.AddComponent<MarsClassicAtmosphere>();
            var atmosphereSo = new SerializedObject(atmosphere);
            atmosphereSo.FindProperty("sky").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            atmosphereSo.ApplyModifiedPropertiesWithoutUndo();

            var budget = MarsClassicBudget.Report("mapa", map);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"[MarsClassicMapBuilder] {PrefabPath}: rota com {samples.Count} amostras; esferas em {berryIndex}, McKittrick em {rockIndex}; {removed} rochas distantes removidas. {budget}");
            return true;
        }

        static void Set(UnityEngine.Object target, params (string field, Transform value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
                so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
