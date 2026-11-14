using System;
using System.Collections.Generic;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-009 (R3): monta a cena <c>Mars.unity</c> de forma determinística a partir de prefabs referenciados em <see cref="MarsSceneBindings"/>
    /// (nada é procurado por nome em execução). Cada região é um contêiner (palco + atividades); só a de abertura começa ativa. Reconstruir
    /// preserva as instâncias existentes e a posição/apoio dos objetos (só instâncias novas recebem pose inicial). Não toca em Boot, Dossiê nem
    /// Build Settings (R1/INT-001).
    /// </summary>
    public sealed class MarsChapterAssembly : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string BindingsPath = MarsPath + "/Data/MarsSceneBindings.asset";
        public const string ScenePathValue = MarsPath + "/Scenes/Mars.unity";
        const string Prefabs = MarsPath + "/Prefabs/";

        public override string Id => "mars.chapter";
        public override string Owner => "R3 · MAR-009";
        public override string ScenePath => ScenePathValue;
        public override string RootName => "[R3 MAR-009] Mars";
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-009 Montar a cena do capítulo")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new MarsChapterAssembly());
            if (!result.Succeeded)
                throw new InvalidOperationException(result.ToString());
            Debug.Log(result.ToString());
        }

        public static void BuildFromCommandLine()
        {
            try { BuildFromMenu(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static GameObject Prefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name);
            if (prefab == null)
                throw new InvalidOperationException("Prefab ausente: " + name);
            return prefab;
        }

        static List<MarsStepRegion> StepRegions()
        {
            var map = new Dictionary<string, string>();
            foreach (var s in new[] { "M01", "M02", "M03" }) map[s] = MarsChapterBindings.RegionOrigin;
            foreach (var s in new[] { "M04", "M05", "M06" }) map[s] = MarsChapterBindings.RegionLanding;
            foreach (var s in new[] { "M07", "M08", "M09", "M10", "M11", "M12", "M13", "M14", "M15" }) map[s] = MarsChapterBindings.RegionEagle;
            foreach (var s in new[] { "M16", "M17", "M18", "M19", "M20" }) map[s] = MarsChapterBindings.RegionShield;
            map["M21"] = MarsChapterBindings.RegionVictoria;
            map["M22"] = MarsChapterBindings.RegionSpirit;
            foreach (var s in new[] { "M23", "M24", "M25", "M26", "M27", "M28", "M29", "M30" }) map[s] = MarsChapterBindings.RegionValley;
            foreach (var s in new[] { "M31", "M32" }) map[s] = MarsChapterBindings.RegionMemorial;
            return map.OrderBy(p => p.Key).Select(p => new MarsStepRegion { stepId = p.Key, regionId = p.Value }).ToList();
        }

        static void SetRef(SerializedObject so, string field, Object value)
        {
            var property = so.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException("Campo ausente: " + field);
            property.objectReferenceValue = value;
        }

        public override void Build(SceneBuildContext context)
        {
            // ---- Dados de montagem (referências serializadas) ----
            var asset = context.Asset(BindingsPath, ScriptableObject.CreateInstance<MarsSceneBindings>);
            var bso = new SerializedObject(asset);
            SetRef(bso, "chapter", AssetDatabase.LoadAssetAtPath<ChapterDefinition>(MarsPath + "/Data/MarsChapter.asset"));
            SetRef(bso, "subtitlesJson", AssetDatabase.LoadAssetAtPath<TextAsset>(MarsPath + "/Data/MarsSubtitles.json"));
            SetRef(bso, "sequenceCatalog", AssetDatabase.LoadAssetAtPath<MarsChapterSequenceCatalog>(MarsPath + "/Timelines/MarsChapterSequences.asset"));
            SetRef(bso, "lighting", AssetDatabase.LoadAssetAtPath<MarsLightingBindings>(MarsPath + "/Art/MarsLightingBindings.asset"));
            SetRef(bso, "originStage", Prefab("MarsPreparationStage.prefab"));
            SetRef(bso, "landingAssembly", Prefab("MerLandingAssembly.prefab"));
            SetRef(bso, "shieldField", Prefab("ShieldFieldEnvironment.prefab"));
            SetRef(bso, "victoriaStage", Prefab("VictoriaStage.prefab"));
            SetRef(bso, "valleyStage", Prefab("PerseveranceValleyStage.prefab"));
            SetRef(bso, "spiritStage", Prefab("SpiritInterludeStage.prefab"));
            SetRef(bso, "memorialStage", Prefab("MarsMemorialStage.prefab"));
            SetRef(bso, "roverGameplay", Prefab("MarsRoverGameplay.prefab"));
            SetRef(bso, "roverVisual", Prefab("OpportunityVisual.prefab"));
            SetRef(bso, "blueberries", Prefab("BlueberriesInteraction.prefab"));
            SetRef(bso, "ratActivity", Prefab("RatActivity.prefab"));
            SetRef(bso, "compositionActivity", Prefab("CompositionActivity.prefab"));
            SetRef(bso, "shieldInteractions", Prefab("ShieldInteractions.prefab"));
            SetRef(bso, "stormControls", Prefab("Storm2007Controls.prefab"));
            SetRef(bso, "finalSurvey", Prefab("FinalSurveyInteractions.prefab"));
            SetRef(bso, "cameraAnchors", Prefab("MarsCameraAnchors.prefab"));
            bso.ApplyModifiedPropertiesWithoutUndo();
            asset.SetStepRegions(StepRegions());
            EditorUtility.SetDirty(asset);

            var root = context.Root.transform;

            // ---- Serviços da cena ----
            var services = context.Child(root, "Services").transform;
            var cameraObject = context.Child(services, "Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            var cameraDirector = context.Component<CameraDirector>(cameraObject);
            context.Component<UniversalAdditionalCameraData>(cameraObject);
            var sequenceObject = context.Child(services, "SequenceDirector");
            var sequenceDirector = context.Component<SequenceDirector>(sequenceObject);
            var sunObject = context.Child(services, "Sun · single shadow light");
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var flow = context.Component<MarsChapterFlow>(context.Child(services, "Flow"));

            // ---- Regiões (contêineres; só a abertura ativa) ----
            var regionsRoot = context.Child(root, "Regions").transform;
            GameObject Region(string id, params (GameObject prefab, string name)[] children)
            {
                var container = context.Child(regionsRoot, "Region_" + id.Replace("mars.", string.Empty));
                foreach (var (prefab, name) in children)
                    Instance(context, container.transform, prefab, name);
                return container;
            }

            var origin = Region(MarsChapterBindings.RegionOrigin, (asset.OriginStage, "MarsPreparationStage"));
            var landing = Region(MarsChapterBindings.RegionLanding, (asset.LandingAssembly, "MerLandingAssembly"));
            var shield = Region(MarsChapterBindings.RegionShield, (asset.ShieldField, "ShieldFieldEnvironment"), (asset.ShieldInteractions, "ShieldInteractions"));
            var victoria = Region(MarsChapterBindings.RegionVictoria, (asset.VictoriaStage, "VictoriaStage"), (asset.StormControls, "Storm2007Controls"));
            var valley = Region(MarsChapterBindings.RegionValley, (asset.ValleyStage, "PerseveranceValleyStage"), (asset.FinalSurvey, "FinalSurveyInteractions"));
            var spirit = Region(MarsChapterBindings.RegionSpirit, (asset.SpiritStage, "SpiritInterludeStage"));
            var memorial = Region(MarsChapterBindings.RegionMemorial, (asset.MemorialStage, "MarsMemorialStage"));
            var eagle = Region(MarsChapterBindings.RegionEagle, (asset.Blueberries, "BlueberriesInteraction"), (asset.RatActivity, "RatActivity"), (asset.CompositionActivity, "CompositionActivity"));

            Transform eagleStart;
            var placeholder = asset.EagleEnvironment == null;
            if (placeholder)
            {
                var floor = context.Child(eagle.transform, "Eagle · PLACEHOLDER (MAR-012 pendente)");
                var box = context.Component<BoxCollider>(floor);
                box.size = new Vector3(80f, 0.2f, 80f);
                box.center = new Vector3(0f, -0.1f, 0f);
                eagleStart = context.Child(floor.transform, "RoverStart").transform;
            }
            else
            {
                var env = Instance(context, eagle.transform, asset.EagleEnvironment, "EagleEnvironment");
                eagleStart = env.transform;
            }

            foreach (var region in new[] { origin, landing, shield, victoria, valley, spirit, memorial, eagle })
                region.SetActive(region == origin);

            // ---- Rover (modelo da MAR-011 dentro do VisualRoot) ----
            var rover = Instance(context, root, asset.RoverGameplay, "Rover");
            var visualRoot = rover.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "VisualRoot");
            if (visualRoot == null)
                throw new InvalidOperationException("O prefab do rover não tem VisualRoot.");
            var visual = Instance(context, visualRoot, asset.RoverVisual, "OpportunityVisual");
            var gimbal = visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "HGA_Gimbal");
            if (gimbal == null)
                throw new InvalidOperationException("O modelo do rover não tem HGA_Gimbal.");
            rover.SetActive(false);

            var anchorsObject = Instance(context, root, asset.CameraAnchors, "MarsCameraAnchors");

            // ---- Bindings ----
            var bindings = context.Component<MarsChapterBindings>(context.Root);
            var so = new SerializedObject(bindings);
            SetRef(so, "bindings", asset);
            SetRef(so, "sceneCamera", camera);
            SetRef(so, "cameraDirector", cameraDirector);
            SetRef(so, "sequenceDirector", sequenceDirector);
            SetRef(so, "sun", sun);
            SetRef(so, "flow", flow);
            SetRef(so, "conductor", anchorsObject.GetComponent<MarsSequenceConductor>());
            SetRef(so, "anchors", anchorsObject.GetComponent<MarsCameraAnchors>());
            SetRef(so, "rover", rover.GetComponentInChildren<MarsRoverController>(true));
            SetRef(so, "tutorial", rover.GetComponentInChildren<MarsTutorial>(true));
            SetRef(so, "antennaGimbal", gimbal);
            SetRef(so, "blueberries", eagle.GetComponentInChildren<BlueberriesActivity>(true));
            SetRef(so, "rat", eagle.GetComponentInChildren<MarsRatActivity>(true));
            SetRef(so, "ratCavity", eagle.GetComponentInChildren<RatCavitySurface>(true));
            SetRef(so, "composition", eagle.GetComponentInChildren<MarsCompositionActivity>(true));
            SetRef(so, "shieldSurvey", shield.GetComponentInChildren<ShieldSurveyActivity>(true));
            SetRef(so, "meteorite", shield.GetComponentInChildren<MeteoriteActivity>(true));
            SetRef(so, "storm", victoria.GetComponentInChildren<Storm2007Activity>(true));
            SetRef(so, "finalSurvey", valley.GetComponentInChildren<MarsFinalSurvey>(true));
            SetRef(so, "transmission", valley.GetComponentInChildren<MarsTransmission>(true));
            SetRef(so, "shieldAnchors", shield.GetComponentInChildren<ShieldFieldAnchors>(true));
            SetRef(so, "victoriaAnchors", victoria.GetComponentInChildren<LateMarsStageAnchors>(true));
            SetRef(so, "valleyAnchors", valley.GetComponentInChildren<LateMarsStageAnchors>(true));
            SetRef(so, "victoriaAtmosphere", victoria.GetComponentInChildren<MarsAtmosphereController>(true));
            SetRef(so, "valleyAtmosphere", valley.GetComponentInChildren<MarsAtmosphereController>(true));
            SetRef(so, "originDirector", origin.GetComponentInChildren<MarsOriginDirector>(true));
            SetRef(so, "landingDirector", landing.GetComponentInChildren<MarsDualLandingDirector>(true));
            SetRef(so, "spiritDirector", spirit.GetComponentInChildren<SpiritInterludeDirector>(true));
            SetRef(so, "memorialDirector", memorial.GetComponentInChildren<MarsMemorialDirector>(true));
            SetRef(so, "eagleRoverStart", eagleStart);
            so.FindProperty("eaglePlaceholder").boolValue = placeholder;
            var list = so.FindProperty("regions");
            var all = new[]
            {
                (MarsChapterBindings.RegionOrigin, origin), (MarsChapterBindings.RegionLanding, landing), (MarsChapterBindings.RegionEagle, eagle),
                (MarsChapterBindings.RegionShield, shield), (MarsChapterBindings.RegionVictoria, victoria), (MarsChapterBindings.RegionValley, valley),
                (MarsChapterBindings.RegionSpirit, spirit), (MarsChapterBindings.RegionMemorial, memorial),
            };
            list.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("regionId").stringValue = all[i].Item1;
                var roots = item.FindPropertyRelative("roots");
                roots.arraySize = 1;
                roots.GetArrayElementAtIndex(0).objectReferenceValue = all[i].Item2;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Instância do prefab com este nome: reaproveitada se já existe (preserva pose, apoio e overrides); só a nova nasce na origem.</summary>
        static GameObject Instance(SceneBuildContext context, Transform parent, GameObject prefab, string name)
        {
            var instance = context.PrefabInstance(parent, prefab, name);
            return instance;
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            foreach (var b in context.Root.GetComponentsInChildren<MarsChapterBindings>(true))
                errors.AddRange(b.ValidateSetup());
        }
    }
}
