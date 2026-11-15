using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.Chapters.Surveyor;
using Abandoned.SpaceApps.Chapters.Surveyor.EditorTools;
using Abandoned.SpaceApps.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>SUR-018 authored scene composition. The existing chapter builder remains the single scene owner.</summary>
    public static class SurveyorDocumentaryBuilder
    {
        public const string Folder = "Assets/_Game/Chapters/Surveyor/Documentary";
        public const string PrefabPath = Folder + "/SurveyorDocumentary.prefab";
        static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        static T Get<T>(GameObject go) where T : Component { var component = go.GetComponent<T>(); return component != null ? component : go.AddComponent<T>(); }
        static GameObject Child(GameObject parent, string name)
        { var go = new GameObject(name); go.transform.SetParent(parent.transform, false); return go; }
        static GameObject Nested(GameObject parent, string name, string path)
        { var go = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path), parent.scene); go.name = name; go.transform.SetParent(parent.transform, false); return go; }
        static void Set(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;
        static void Array(SerializedObject data, string field, Object[] values)
        { var p = data.FindProperty(field); p.arraySize = values.Length; for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
        static Material Material(SceneBuildContext c, string name, Color color, float metallic = 0f, float smoothness = .1f)
        {
            var m = c.Material(Folder + "/Art/" + name + ".mat", "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smoothness); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        static void ImportModel(string name, bool animate)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Folder + "/Art/" + name + ".fbx");
            if (imp == null) throw new InvalidOperationException("Generate the documented Blender assets first: " + name);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.importCameras = imp.importLights = false; imp.isReadable = false; imp.bakeAxisConversion = true;
            imp.animationType = animate ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
            imp.importAnimation = animate; imp.meshCompression = ModelImporterMeshCompression.Medium; imp.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(imp.assetPath).OfType<AnimationClip>().Select(x => x.name).ToArray();
            Debug.Log(name + " clips: " + string.Join(",", clips));
        }
        public static void Build(SceneBuildContext c)
        {
            ImportModel("ApolloA7L", true); ImportModel("ApolloLunarModule", false); ImportModel("SurveyorRecoverable", false); ImportModel("SurveyorRetro", false);
            var names = new[] { "Prologue", "Arrival", "Activation", "Science", "Findings", "Night", "Reunion", "Legacy", "End" };
            var template = ChapterDefinition.Create("surveyor3", "sur.chapter.title", "sur.chapter.description", "sur.chapter.question", "SurveyorChapter", 375, "sur.doc.Prologue", names.Select((n, i) => new ChapterStep(n, i == names.Length - 1, i == names.Length - 1 ? System.Array.Empty<string>() : new[] { names[i + 1] })), Load<Sprite>(ReturnedCameraBuilder.CoverPath));
            var chapter = c.Asset(SurveyorChapterBuilder.ChapterPath, () => template);
            if (chapter != template) { EditorUtility.CopySerialized(template, chapter); Object.DestroyImmediate(template); } EditorUtility.SetDirty(chapter);
            var panel = c.Asset(Folder + "/DocumentaryPanel.asset", () => Object.Instantiate(Load<PanelSettings>("Assets/_Game/UI/Design/SpaceAppsPanelSettings.asset")));
            panel.sortingOrder = 5; EditorUtility.SetDirty(panel);
            var pauseSettings = c.Asset(Folder + "/PausePanel.asset", () => Object.Instantiate(panel)); pauseSettings.sortingOrder = 20; EditorUtility.SetDirty(pauseSettings);
            var filter = c.Material(Folder + "/Art/DocumentaryTreatment.mat", "Abandoned/Surveyor Documentary");
            var ground = Material(c, "LunarRegolith", new Color(.63f, .62f, .59f));
            ground.shader = Shader.Find("Abandoned/Surveyor Regolith");
            ground.SetTexture("_BaseMap", Load<Texture2D>(SurveyorArtPolish.Folder + "/Regolith.png"));
            ground.SetTexture("_BumpMap", Load<Texture2D>(SurveyorSiteBuilder.GeneratedPath + "/RegolithNormal.png")); ground.SetFloat("_BumpScale", .6f); ground.EnableKeyword("_NORMALMAP");
            var audio = c.Asset(Folder + "/DocumentaryAudio.asset", () => ScriptableObject.CreateInstance<AudioCatalog>());
            audio.Set(new[] { Music("sur.doc.departure", "Departure", .29f), Music("sur.doc.science", "Science", .18f), Music("sur.doc.return", "Return", .34f) }); EditorUtility.SetDirty(audio);
            var tracks = Tracks(c);
            var prefab = c.Prefab(PrefabPath, root =>
            {
                // Rebuild our own composition deterministically. Shared source prefabs and other scene roots stay intact.
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Get<ChapterDirector>(root); Get<SessionClock>(root); Get<InputRouter>(root); Get<InteractionService>(root); Get<SequenceDirector>(root); Get<CameraDirector>(root);
                Get<SubtitlePresenter>(root); Get<FocusPause>(root); Get<AudioDirector>(root);
                var host = Get<SurveyorDocumentaryHost>(root); var film = Get<SurveyorDocumentaryFilm>(root); var view = Get<SurveyorDocumentaryView>(root); var scoop = Get<SurveyorDirectScoop>(root);
                var site = Nested(root, "LunarSite", SurveyorSiteBuilder.ArtPath + "/LunarSite.prefab");
                foreach (var r in site.GetComponentsInChildren<MeshRenderer>()) if (r.name.Contains("Chunk") || r.sharedMaterial.name.Contains("Regolith")) r.sharedMaterial = ground;
                var spacecraft = Nested(root, "Surveyor", SurveyorSiteBuilder.ArtPath + "/SurveyorVisibleRig.prefab");
                var atlas = spacecraft.GetComponentsInChildren<Renderer>().First().sharedMaterial;
                PrefabUtility.UnpackPrefabInstance(spacecraft, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var r in spacecraft.GetComponentsInChildren<Renderer>())
                { var oldFilter = r.GetComponent<MeshFilter>(); if (oldFilter != null) Object.DestroyImmediate(oldFilter); Object.DestroyImmediate(r); }
                var recoverable = Nested(spacecraft, "RecoverableTeamModel", Folder + "/Art/SurveyorRecoverable.fbx");
                var bodyFrame = spacecraft.transform.Find("Body");
                recoverable.transform.SetPositionAndRotation(bodyFrame.position, bodyFrame.rotation);
                foreach (var r in recoverable.GetComponentsInChildren<Renderer>())
                { r.sharedMaterial = atlas; r.enabled = r.name != "ScoopLinkage" && r.name != "ScoopTool"; }
                // Batching static moving spacecraft breaks flight rendering: all its geometry is dynamic in this chapter.
                foreach (var t in spacecraft.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                var trench = Nested(root, "Trench", SurveyorScoopBuilder.TrenchPrefabPath);
                foreach (var r in trench.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ground;
                var arm = Nested(root, "ScoopRig", SurveyorScoopBuilder.RigPrefabPath);
                var originalTile = site.transform.Find("NearGround_40m/Chunk_2_2").GetComponent<Renderer>(); originalTile.enabled = false;
                var groove = trench.transform.Find("Groove").GetComponent<SkinnedMeshRenderer>();
                var filterObject = Child(trench, "LiveSoil"); filterObject.transform.SetPositionAndRotation(groove.transform.position, groove.transform.rotation);
                Get<MeshFilter>(filterObject).sharedMesh = groove.sharedMesh; Get<MeshRenderer>(filterObject).sharedMaterial = ground; groove.enabled = false;
                var soil = Get<SurveyorSoilSurface>(filterObject); soil.Configure(filterObject.GetComponent<MeshFilter>());
                scoop.Configure(arm.transform, trench.transform, soil);
                foreach (var r in spacecraft.GetComponentsInChildren<Renderer>()) if (r.name == "Team_ScoopLinkage" || r.name == "Team_ScoopTool") r.enabled = false;
                var optical = spacecraft.transform.Find("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount");
                var camera = Get<Camera>(Child(root, "DocumentaryCamera")); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.001f, .002f, .004f);
                camera.nearClipPlane = .025f; camera.farClipPlane = 6000f; camera.allowHDR = camera.allowMSAA = false; Get<AudioListener>(camera.gameObject);
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var sun = Get<Light>(Child(root, "Sun")); sun.type = LightType.Directional; sun.intensity = 1.65f; sun.color = new Color(1f, .94f, .83f); sun.transform.rotation = Quaternion.Euler(19f, 134f, 0f);
                sun.shadows = LightShadows.Soft; sun.shadowBias = .012f; sun.shadowNormalBias = .065f; sun.shadowStrength = .92f;
                var retro = Retro(c, root, spacecraft); var plume = Child(spacecraft, "Vernier exhaust");
                var glow = Material(c, "NozzleGlow", new Color(.28f, .15f, .065f), .1f, .3f);
                for (int i = 0; i < 3; i++)
                { float a = i * Mathf.PI * 2 / 3; var p = Primitive(plume, "Vernier" + i, PrimitiveType.Sphere, glow); p.transform.localPosition = new Vector3(Mathf.Cos(a) * .45f, .27f, Mathf.Sin(a) * .45f); p.transform.localScale = new Vector3(.035f, .16f, .035f); }
                var target = Child(root, "TouchdownTarget").transform; target.position = new Vector3(0, 1.1f, 0);
                var moonMaterial = Material(c, "Moon LROC", Color.white); moonMaterial.SetTexture("_BaseMap", Load<Texture2D>(Folder + "/Art/Moon_LROC_2k.jpg"));
                var moon = Primitive(root, "Moon at departure", PrimitiveType.Sphere, moonMaterial); moon.transform.position = new Vector3(-900, 110, 1800); moon.transform.localScale = Vector3.one * 2200;
                var lm = Nested(root, "Intrepid", Folder + "/Art/ApolloLunarModule.fbx"); ConvertMaterials(c, lm);
                var bounds = Bounds(lm); float scale = 7.1f / Mathf.Max(bounds.size.x, bounds.size.z); lm.transform.localScale *= scale;
                bounds = Bounds(lm); lm.transform.position = new Vector3(-170f, SurveyorSiteGeometry.Height(-170, 60) - bounds.min.y, 60f);
                lm.transform.rotation = Quaternion.Euler(0, 31, 0);
                var cast = Cast(c, root, spacecraft, arm, optical);
                var earth = Nested(root, "EarthEclipse", SurveyorEclipseBuilder.EarthPrefab).GetComponent<SurveyorEclipseImage>();
                earth.Place(optical.position, Quaternion.LookRotation(new Vector3(-40, 65, 45).normalized)); earth.Show(false);
                film.Configure(spacecraft.transform, retro, target, plume, moon, lm, cast, sun, earth);
                var filmData = new SerializedObject(film); Set(filmData, "deployedArm", arm); Set(filmData, "surfaceWorld", site); Set(filmData, "surfacePatch", trench); Array(filmData, "flightArm", recoverable.GetComponentsInChildren<Renderer>().Where(r => r.name == "ScoopLinkage" || r.name == "ScoopTool").Cast<Object>().ToArray()); filmData.ApplyModifiedPropertiesWithoutUndo();
                var ui = Get<UIDocument>(Child(root, "FilmInterface")); ui.panelSettings = panel; ui.visualTreeAsset = Load<VisualTreeAsset>(Folder + "/Documentary.uxml");
                view.Configure(ui, filter, Load<Texture2D>(SurveyorVisitBuilder.PhotoPath));
                var pause = Nested(root, "Pause", "Assets/_Game/UI/Pause/PauseMenu.prefab").GetComponent<PauseMenu>(); pause.GetComponent<UIDocument>().panelSettings = pauseSettings;
                var data = new SerializedObject(host); Set(data, "chapter", chapter); Set(data, "texts", Load<TextAsset>(Folder + "/DocumentaryTexts.json")); Array(data, "tracks", tracks);
                Set(data, "outputCamera", camera); Set(data, "cameras", root.GetComponent<CameraDirector>()); Set(data, "sequence", root.GetComponent<SequenceDirector>()); Set(data, "film", film); Set(data, "view", view); Set(data, "scoop", scoop);
                Set(data, "opticalMount", optical); Set(data, "workPatch", trench.transform); Set(data, "pause", pause); Set(data, "audio", audio);
                Set(data, "opticalOccluder", arm.transform.Find("Shoulder/UpperLink").GetComponent<Renderer>());
                Set(data, "descentMotor", Sound(root, "Braking vibration", "Assets/_Game/Chapters/Surveyor/Sequences/Descent/Audio/Engine.wav", true)); Set(data, "contactSound", Sound(root, "Touchdown contacts", null, false));
                Array(data, "contacts", Enumerable.Range(1, 3).Select(i => (Object)Load<AudioClip>("Assets/_Game/Chapters/Surveyor/Sequences/Descent/Audio/Contact" + i + ".wav")).ToArray()); data.ApplyModifiedPropertiesWithoutUndo();
            });
            var previous = c.Root.transform.Find("SurveyorChapter"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            c.PrefabInstance(c.Root.transform, prefab, "SurveyorChapter");
            RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.105f, .115f, .14f); RenderSettings.reflectionIntensity = .65f;
        }
        static GameObject Primitive(GameObject parent, string name, PrimitiveType type, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent.transform, false); Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static Bounds Bounds(GameObject go)
        { var renderers = go.GetComponentsInChildren<Renderer>(); var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); return b; }
        static Transform Retro(SceneBuildContext c, GameObject root, GameObject craft)
        {
            var retro = Nested(craft, "RetroStage", Folder + "/Art/SurveyorRetro.fbx"); ConvertMaterials(c, retro);
            var body = craft.transform.Find("Body"); retro.transform.SetPositionAndRotation(body.position, body.rotation); return retro.transform;
        }
        static AudioSource Sound(GameObject parent, string name, string path, bool loop)
        { var a = Get<AudioSource>(Child(parent, name)); a.playOnAwake = false; a.loop = loop; a.spatialBlend = 0; if (path != null) a.clip = Load<AudioClip>(path); return a; }
        static AudioCueDefinition Music(string id, string file, float gain)
        { return new AudioCueDefinition(id, AudioBus.Music, true, gain, clips: new[] { Load<AudioClip>(SurveyorChapterBuilder.Folder + "/Audio/" + file + ".wav") }); }
        static void ConvertMaterials(SceneBuildContext c, GameObject model)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(model)) PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var mapped = new Dictionary<string, Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i]; string name = source != null ? source.name : "Ivory";
                    if (!mapped.TryGetValue(name, out var m))
                    {
                        Color color = source != null && source.HasProperty("_Color") ? source.color : new Color(.75f, .74f, .70f);
                        bool metal = name.Contains("Gold") || name.Contains("Metal") || name.Contains("blinn1SG") || name.Contains("blinn9SG");
                        m = Material(c, "Apollo_" + name.Replace(".", "_"), color, metal ? .75f : 0f, name.Contains("Visor") ? .20f : metal ? .28f : .12f);
                        if (name.Contains("blinn5")) m.SetTexture("_BaseMap", Load<Texture2D>(Folder + "/Art/LM_TEXTURE_.png"));
                        if (name.Contains("blinn8")) m.SetTexture("_BaseMap", Load<Texture2D>(Folder + "/Art/LM_TEXTUREA.png"));
                        if (name.Contains("blinn12")) m.SetTexture("_BaseMap", Load<Texture2D>(Folder + "/Art/LM_BOOSTER3.png"));
                        mapped.Add(name, m);
                    }
                    materials[i] = m;
                }
                r.sharedMaterials = materials;
            }
            foreach (var animation in model.GetComponentsInChildren<Animation>()) animation.enabled = false;
        }
        static SurveyorApolloCast Cast(SceneBuildContext c, GameObject root, GameObject craft, GameObject arm, Transform optical)
        {
            var group = Child(root, "Apollo12 full actors"); var cast = Get<SurveyorApolloCast>(group);
            var actors = new GameObject[2]; var from = new Transform[2]; var to = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                actors[i] = Nested(group, i == 0 ? "Conrad reconstructed" : "Bean reconstructed", Folder + "/Art/ApolloA7L.fbx"); ConvertMaterials(c, actors[i]);
                from[i] = Child(root, "Walk start " + i).transform; to[i] = Child(root, "Inspect position " + i).transform;
                float x = i == 0 ? optical.position.x - .48f : -2.1f, z = i == 0 ? optical.position.z + .20f : -.8f;
                to[i].position = SurveyorSiteGeometry.Ground(x, z, .012f); to[i].rotation = Quaternion.LookRotation(new Vector3(optical.position.x, 0f, optical.position.z) - new Vector3(x, 0, z));
                from[i].position = to[i].position - to[i].forward * (i == 0 ? 3.6f : 4.2f);
                from[i].position = SurveyorSiteGeometry.Ground(from[i].position.x, from[i].position.z, .012f);
                actors[i].transform.SetPositionAndRotation(to[i].position, to[i].rotation);
            }
            var clips = AssetDatabase.LoadAllAssetsAtPath(Folder + "/Art/ApolloA7L.fbx").OfType<AnimationClip>().Where(a => !a.name.StartsWith("__")).ToArray();
            AnimationClip Clip(string key) => clips.FirstOrDefault(x => x.name.Contains(key)) ?? throw new InvalidOperationException("Missing A7L skeletal clip " + key);
            cast.Configure(actors, Clip("Walk"), Clip("Inspect"), Clip("Stand"), from, to);
            Transform Bone(GameObject actor, string name) => actor.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
            var rightHand = Child(Bone(actors[0], "Hand_R").gameObject, "Camera carry attachment").transform; rightHand.localPosition = new Vector3(0, .06f, .05f);
            var leftHand = Child(Bone(actors[1], "Hand_L").gameObject, "Scoop carry attachment").transform;
            var cameraParts = craft.transform.Find("RecoverableTeamModel").GetComponentsInChildren<Renderer>(true).Where(x => x.name.Contains("MirrorHead") || x.name.Contains("CameraHousing")).ToArray();
            var cameraProp = Child(root, "Recovered camera assembly"); cameraProp.transform.SetPositionAndRotation(optical.position, optical.rotation);
            foreach (var part in cameraParts)
            {
                var copy = Object.Instantiate(part.gameObject, cameraProp.transform); copy.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation); copy.GetComponent<Renderer>().enabled = true;
            }
            var sourceTool = arm.transform.Find(SurveyorScoop.ScoopToolPath).GetChild(0); var scoopProp = Object.Instantiate(sourceTool.gameObject, root.transform); scoopProp.name = "Recovered scoop";
            cast.ConfigureRecovery(cameraProp.transform, optical, rightHand, scoopProp.transform, leftHand, cameraParts, arm.transform.Find(SurveyorScoop.ScoopToolPath)); cast.Evaluate(0f, false); return cast;
        }
        static SequenceDefinition[] Tracks(SceneBuildContext c)
        {
            var eye = SurveyorScoopBuilder.ReadSite().CameraMount;
            Vector3 P(float x, float y, float z) => new Vector3(x, SurveyorSiteGeometry.Height(x, z) + y, z);
            ShotDefinition Shot(string id, float seconds, Vector3 from, Vector3 to, float fov, string target = "sur.touchdown", string rig = "world", bool continuity = false)
                => new ShotDefinition("sur.doc." + id, seconds, rig, new[] { new ShotKeyframe(0, from, Vector3.zero, fov), new ShotKeyframe(seconds, to, Vector3.zero, fov) }, target, minGroundClearanceMeters: .18f, continueFromPreviousShot: continuity);
            SequenceDefinition Track(string id, ShotDefinition[] shots, params SubtitleCue[] cues) => SequenceDefinition.Create("sur.doc." + id, shots, cues);
            SubtitleCue Cue(string id, float a, float b) => new SubtitleCue("sur.doc." + id, "sur.doc." + id, a, b);
            var list = new[] {
                Track("Prologue", new[]{ Shot("arrival.question",12,new Vector3(6,2,-8),new Vector3(5,2,-7),40,rig:"sur.craft"), Shot("arrival.tools",13,eye+new Vector3(1.6f,.4f,-1.8f),eye+new Vector3(1.3f,.35f,-1.5f),35,"sur.optical","sur.craft") }, Cue("intro.1",1,9),Cue("intro.2",10,17),Cue("intro.3",18,25)),
                Track("Arrival", new[]{ Shot("descent.scale",10,new Vector3(9,4,-12),new Vector3(7,3,-10),42,rig:"sur.craft"), Shot("descent.separation",8,new Vector3(4,0,-6),new Vector3(4.2f,.1f,-6),44,rig:"sur.craft"), Shot("descent.ground",5,P(9,2,-12),P(8,1.8f,-11),40), Shot("descent.contacts",6,P(4,.5f,-4),P(4.1f,.6f,-4),46), Shot("descent.settled",6,P(6,2,-8),P(5.5f,2,-7),42) },Cue("arrival.1",1,8),Cue("arrival.2",10,18),Cue("arrival.3",23,31)),
                Track("Activation",new[]{ Shot("activation.body",7,P(3,1.7f,-4),P(2.7f,1.6f,-3.5f),40),Shot("activation.eye",7,P(2,1.7f,1.1f),P(1.5f,1.7f,1.1f),35,"sur.optical"),Shot("activation.work",6,P(1.6f,1.8f,1.1f),P(1.6f,1.8f,1.1f),35,"sur.trench") },Cue("activation.1",0,8),Cue("activation.2",10,20)),
                Track("Findings",new[]{ Shot("findings.trench",10,P(1.5f,.6f,3),P(1.3f,.55f,2.9f),40,"sur.trench"),Shot("findings.machine",10,P(5,1.7f,-6),P(5.3f,1.9f,-6.4f),40) },Cue("findings.1",0,9),Cue("findings.2",10,20)),
                Track("Night",new[]{ Shot("night.eclipse",6,P(3,1.2f,-4),P(3.4f,1.3f,-4.5f),38), Shot("night.earth",12,P(0,1.65f,1.1f),P(0,1.65f,1.1f),10,"sur.eclipse"),Shot("night.shadow",17,P(6,1.2f,-3),P(6.5f,1.3f,-3.5f),42),Shot("night.wait",10,P(9,2,-11),P(10,2,-12),40) },Cue("night.1",0,8),Cue("night.2",9,18),Cue("night.3",21,29),Cue("night.4",33,43)),
                Track("Reunion",new[]{ Shot("reunion.place",13,P(8,3,-9),P(7.5f,2.8f,-8.4f),44),Shot("reunion.human",13,P(-4,1.0f,-3),P(-3.8f,1.05f,-2.8f),44),Shot("reunion.inspection",13,eye+new Vector3(1.5f,.15f,-1.6f),eye+new Vector3(1.4f,.15f,-1.5f),44,"sur.optical"),Shot("reunion.return",16,P(4,2.2f,5),P(4.5f,2.4f,6),45) },Cue("reunion.1",0,8),Cue("reunion.2",10,20),Cue("reunion.3",24,35),Cue("reunion.4",39,50)),
                Track("Legacy",new[]{Shot("legacy.photo",10,P(4.5f,2.4f,6),P(4.5f,2.4f,6),45,continuity:true),Shot("legacy.remains",5,P(5,2,-7),P(5.3f,2.1f,-7.5f),40)},Cue("legacy.1",0,9),Cue("legacy.2",10,15))
            };
            return list.Select(source =>
            {
                string path = Folder + "/Sequences/" + source.SequenceId.Substring(8) + ".asset";
                var result = c.Asset(path, () => source); if (result != source) { EditorUtility.CopySerialized(source, result); Object.DestroyImmediate(source); } EditorUtility.SetDirty(result); return result;
            }).ToArray();
        }
    }
}
