using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// Palcos tardios sobre terrenos importados: o campo do escudo térmico (M16–M20), Victoria 2007 (M21) e o Perseverance Valley 2018
    /// (M23–M30). Cada palco usa as âncoras, a atmosfera e as interações do palco gerado, com o terreno e as rochas gerados desligados e o
    /// terreno importado no lugar; as âncoras são reapoiadas nesse chão. Só gera os prefabs se os mapas importados existirem.
    /// </summary>
    public static class MarsClassicStagesBuilder
    {
        const string Prefabs = "Assets/_Game/Chapters/Mars/Prefabs/";
        const string Classic = MarsClassicMapBuilder.ClassicRoot + "/Export/";

        [MenuItem("Abandoned Space Apps/Marte/Reconstruir palcos importados (escudo, Victoria, vale)")]
        public static void BuildFromMenu() => BuildAll();

        public static void BuildFromCommandLine()
        {
            try
            {
                EditorApplication.Exit(BuildAll() ? 0 : 2);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static bool BuildAll()
        {
            var report = new StringBuilder();
            var any = false;
            any |= Build("ShieldFieldEnvironment", "ClassicHeatShield", "ShieldFieldClassic", report, Shield);
            any |= Build("VictoriaStage", "ClassicTravel", "VictoriaStageClassic", report, Late);
            any |= Build("PerseveranceValleyStage", "ClassicValley", "PerseveranceValleyStageClassic", report, Valley);
            Debug.Log("[MarsClassicStagesBuilder]\n" + report);
            return any;
        }

        static bool Build(string source, string map, string output, StringBuilder report, Action<GameObject, GameObject, StringBuilder> place)
        {
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + source + ".prefab");
            var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Classic + map + ".prefab");
            if (sourcePrefab == null || mapPrefab == null)
            {
                report.AppendLine($"{output}: faltam assets ({(sourcePrefab == null ? source : map)}).");
                return false;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var stage = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            stage.name = output;
            var children = new StringBuilder();
            foreach (Transform child in stage.transform)
                children.Append(child.name).Append(" (").Append(child.childCount).Append("), ");
            report.AppendLine($"{output}: filhos = {children}");

            var mapInstance = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, stage.transform);
            mapInstance.name = "ClassicMap";
            foreach (Transform child in stage.transform)
            {
                if (child.name == "Terrain" || child.name == "Rocks" || child.name == "Debris" || child.name == "Scatter" || child.name == "Meteorite")
                    child.gameObject.SetActive(false);
            }

            var trimmed = MarsClassicBudget.Trim(mapInstance, Vector3.zero);
            MarsClassicBudget.ConvertMaterialsAndTextures(mapInstance);
            place(stage, mapInstance, report);
            report.AppendLine($"  {MarsClassicBudget.Report(output, mapInstance)}; {trimmed} rochas distantes removidas.");
            PrefabUtility.SaveAsPrefabAsset(stage, Prefabs + output + ".prefab");
            UnityEngine.Object.DestroyImmediate(stage);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return true;
        }

        static float GroundY(Vector3 p, float fallback)
        {
            Physics.SyncTransforms();
            return Physics.Raycast(new Vector3(p.x, p.y + 80f, p.z), Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;
        }

        /// <summary>Move o mapa para que a origem dele (o ponto de partida do terreno) caia sobre <paramref name="start"/> e apoie o chão ali.</summary>
        static void PlaceMap(GameObject mapInstance, Vector3 start)
        {
            mapInstance.transform.position = new Vector3(start.x, 0f, start.z);
            Physics.SyncTransforms();
            var h = GroundY(new Vector3(start.x, 0f, start.z), 0f);
            mapInstance.transform.position = new Vector3(start.x, start.y - h, start.z);
            Physics.SyncTransforms();
        }

        static Vector3 OnGround(Vector3 p, float lift = 0f) => new Vector3(p.x, GroundY(p, p.y) + lift, p.z);

        static void Aim(Transform t, Vector3 position, Vector3 target)
        {
            t.position = position;
            var direction = target - position;
            if (direction.sqrMagnitude > 1e-6f)
                t.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        static Transform Field(UnityEngine.Object owner, string field) => (Transform)new SerializedObject(owner).FindProperty(field).objectReferenceValue;

        // Campo do escudo térmico (relativo ao ponto de partida do rover): fragmento principal em (3; 0,6; 16) e a rocha de ferro-níquel em (10; 1,5; 21). As estações
        // ficam a menos de 5 m uma da outra (MAR-015).
        static void Shield(GameObject stage, GameObject map, StringBuilder report)
        {
            var anchors = stage.GetComponentInChildren<ShieldFieldAnchors>(true);
            var origin = anchors.ApproachStart.position;
            PlaceMap(map, origin);
            Vector3 At(float x, float z, float lift = 0f) => OnGround(origin + new Vector3(x, 0f, z), lift);
            Aim(anchors.ApproachStart, At(0f, 0f), At(3f, 12f));
            Aim(anchors.ShieldStation, At(5f, 15.2f), At(3f, 16f, 0.5f));
            anchors.ShieldPhotoTarget.position = At(3f, 16f, 0.6f);
            Aim(anchors.ShieldCameraAnchor, At(5.5f, 14.4f, 1.5f), anchors.ShieldPhotoTarget.position);
            Aim(anchors.MeteoriteStation, At(8.2f, 18f), At(10f, 21f, 1.2f));
            anchors.MeteoriteMeasurePoint.position = At(9.6f, 20.2f, 1.3f);
            anchors.MeteoritePhotoTarget.position = At(10f, 21f, 1.4f);
            Aim(anchors.MeteoriteCameraAnchor, At(7.8f, 18.6f, 1.4f), anchors.MeteoritePhotoTarget.position);
            report.AppendLine($"  escudo: estação {anchors.ShieldStation.position}, meteorito {anchors.MeteoriteStation.position}");
        }

        // Victoria 2007: usa a região "além de Eagle" (terreno contínuo). O rover parte do centro dela.
        static void Late(GameObject stage, GameObject map, StringBuilder report)
        {
            var anchors = stage.GetComponentInChildren<LateMarsStageAnchors>(true);
            var start = anchors.RoverStart.position;
            PlaceMap(map, start);
            anchors.RoverStart.position = OnGround(start);
            report.AppendLine($"  victoria: rover {anchors.RoverStart.position}");
        }

        // Perseverance Valley 2018 (relativo ao ponto de partida): o mirante fica em (5; −0,6; 11).
        static void Valley(GameObject stage, GameObject map, StringBuilder report)
        {
            var anchors = stage.GetComponentInChildren<LateMarsStageAnchors>(true);
            var start = anchors.RoverStart.position;
            PlaceMap(map, start);
            var ground = OnGround(start);
            var shift = ground - start;
            // As poses guiadas e a referência da Terra mantêm a posição relativa ao rover; o mirante vai para o ponto definido acima.
            foreach (var t in new[] { anchors.RoverStart, anchors.SunView, anchors.ValleyView, anchors.EarthTarget })
                if (t != null)
                    t.position += shift;

            anchors.Overlook.position = OnGround(start + new Vector3(5f, 0f, 11f));
            report.AppendLine($"  vale: rover {anchors.RoverStart.position}, mirante {anchors.Overlook.position}");
        }
    }
}
