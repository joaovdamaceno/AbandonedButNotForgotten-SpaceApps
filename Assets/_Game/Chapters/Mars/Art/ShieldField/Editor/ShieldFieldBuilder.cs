using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abandoned.SpaceApps.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using G = Abandoned.SpaceApps.Chapters.Mars.EditorTools.ShieldFieldGeometry;
using Object = UnityEngine.Object;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-015 (R4): monta o palco do campo do escudo por prefabs, reconstruível sem perder GUIDs.
    /// Gera os prefabs <c>ShieldFieldEnvironment</c> e <c>HeatShieldRockVisual</c> e uma cena de prévia própria.
    /// Não toca em scripts de interação nem no Boot/Dossier/BuildSettings.
    /// </summary>
    public sealed class ShieldFieldBuilder : SceneBuilderModule
    {
        public const string MarsPath = "Assets/_Game/Chapters/Mars";
        public const string ArtPath = MarsPath + "/Art/ShieldField";
        public const string GeneratedPath = ArtPath + "/Generated";
        public const string EnvironmentPrefabPath = MarsPath + "/Prefabs/ShieldFieldEnvironment.prefab";
        public const string RockPrefabPath = MarsPath + "/Prefabs/HeatShieldRockVisual.prefab";
        public const string PreviewScenePath = ArtPath + "/Preview/ShieldFieldPreview.unity";

        public override string Id => "mars.shieldField";
        public override string Owner => "R4 · MAR-015";
        public override string ScenePath => PreviewScenePath;
        public override string RootName => "[R4 MAR-015] Shield Field";

        // Escreve nas pastas Art/ShieldField e Prefabs (caminhos nomeados na tarefa); nada é criado fora destes dois lugares.
        public override string AssetFolder => MarsPath;

        [MenuItem("Abandoned Space Apps/Marte/MAR-015 Reconstruir campo do escudo")]
        public static void BuildFromMenu()
        {
            var result = SpaceAppsSceneBuilder.Run(new ShieldFieldBuilder());
            if (!result.Succeeded)
                throw new InvalidOperationException(result.ToString());

            Debug.Log(result.ToString());
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                BuildFromMenu();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public override void Build(SceneBuildContext context)
        {
            var terrainTexture = Texture("TerrainColor", 1024, true, false, TerrainPixel);
            var atlasTexture = Texture("ShieldAtlas", 1024, true, false, ShieldAtlasPixel);
            var glossTexture = Texture("ShieldMetalGloss", 512, false, false, ShieldGlossPixel);
            var meteoriteTexture = Texture("MeteoriteColor", 256, true, false, MeteoritePixel);
            var rockTexture = Texture("FieldRockColor", 128, true, false, RockPixel);

            var terrain = context.Material(GeneratedPath + "/FieldSand.mat", "Universal Render Pipeline/Lit");
            Configure(terrain, Color.white, 0f, 0.04f, terrainTexture, null);
            var shield = context.Material(GeneratedPath + "/HeatShield.mat", "Universal Render Pipeline/Lit");
            Configure(shield, Color.white, 0.62f, 0.85f, atlasTexture, glossTexture);
            var meteorite = context.Material(GeneratedPath + "/HeatShieldRock.mat", "Universal Render Pipeline/Lit");
            Configure(meteorite, Color.white, 0.6f, 0.36f, meteoriteTexture, null);
            var rock = context.Material(GeneratedPath + "/FieldRocks.mat", "Universal Render Pipeline/Lit");
            Configure(rock, Color.white, 0f, 0.08f, rockTexture, null);

            var rockPrefab = context.Prefab(RockPrefabPath, root => BuildRock(root, meteorite));
            var environment = context.Prefab(EnvironmentPrefabPath, root => BuildEnvironment(root, terrain, shield, rock, rockPrefab));

            var instance = context.PrefabInstance(context.Root.transform, environment, "ShieldFieldEnvironment");
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            BuildPreviewRig(context, instance.GetComponent<ShieldFieldAnchors>());
            AssetDatabase.SaveAssets();
        }

        public override void Validate(SceneBuildContext context, List<string> errors)
        {
            var anchors = context.Root.GetComponentInChildren<ShieldFieldAnchors>();
            if (anchors == null)
                errors.Add("ShieldFieldAnchors ausente na prévia.");
            else
                errors.AddRange(anchors.ValidateSetup());
        }

        // ---------- Materiais e texturas ----------

        static void Configure(Material m, Color color, float metallic, float smoothness, Texture2D map, Texture2D metalGloss)
        {
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", map);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            if (metalGloss != null)
            {
                m.SetTexture("_MetallicGlossMap", metalGloss);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        static Texture2D Texture(string name, int size, bool srgb, bool normal, Func<float, float, Color> pixel)
        {
            var path = GeneratedPath + "/" + name + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);

            texture.SetPixels(pixels);
            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            // Regrava só quando o conteúdo muda (o PNG existente mantém o GUID e não gera diferença à toa).
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.sRGBTexture != srgb || importer.maxTextureSize != size || !importer.mipmapEnabled))
            {
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = size;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float N(float x, float z) => G.PerlinNoise(x, z);

        // Terreno: areia ocre-avermelhada, vales dos ripples mais escuros e halo de poeira vermelha em torno da marca do impacto.
        static Color TerrainPixel(float u, float v)
        {
            var x = (u - 0.5f) * G.PatchHalfSize * 2f;
            var z = (v - 0.5f) * G.PatchHalfSize * 2f;
            var sand = Color.Lerp(new Color(0.46f, 0.31f, 0.22f), new Color(0.60f, 0.42f, 0.30f), N(x * 0.35f, z * 0.35f));
            var ripple = Mathf.Sin(G.RipplePhase(x, z)) * G.RippleStrength(x, z);
            sand *= 1f + 0.09f * ripple;
            sand *= 0.94f + 0.12f * N(x * 4.1f, z * 4.1f);

            var toDivot = new Vector2(x, z) - G.Divot;
            var r = toDivot.magnitude;
            var streak = N(Mathf.Atan2(toDivot.y, toDivot.x) * 3.2f, r * 0.45f);
            var halo = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.7f + 0.8f * streak, 0.7f, r));
            sand = Color.Lerp(sand, new Color(0.55f, 0.17f, 0.11f) * (0.9f + 0.2f * N(x * 3f, z * 3f)), halo * 0.7f);

            foreach (var debris in new[] { G.MainCenter, G.FlankCenter })
            {
                var dd = Vector2.Distance(new Vector2(x, z), debris);
                sand = Color.Lerp(sand, sand * 1.12f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2.4f, 1.2f, dd)) * 0.5f);
            }

            sand.a = 1f;
            return sand;
        }

        // Atlas do escudo: metade esquerda = face externa ablativa carbonizada; direita = face interna com manta prateada.
        static Color ShieldAtlasPixel(float u, float v)
        {
            if (u < 0.5f)
            {
                var x = u * 2f;
                var streak = N(x * 9f, v * 3f);
                var c = Color.Lerp(new Color(0.17f, 0.15f, 0.13f), new Color(0.38f, 0.33f, 0.28f), streak);
                c = Color.Lerp(c, new Color(0.55f, 0.52f, 0.5f), Mathf.SmoothStep(0.6f, 0.9f, N(x * 4f + 7f, v * 4f)) * 0.5f); // manchas metálicas
                c *= 0.85f + 0.3f * N(x * 40f, v * 40f);
                return new Color(c.r, c.g, c.b, 1f);
            }

            var xr = (u - 0.5f) * 2f;
            var crinkle = N(xr * 55f, v * 55f) * 0.6f + N(xr * 14f, v * 14f) * 0.4f;
            var foil = Color.Lerp(new Color(0.55f, 0.56f, 0.58f), new Color(0.86f, 0.87f, 0.88f), crinkle);
            var gold = Mathf.SmoothStep(0.55f, 0.8f, N(xr * 3f + 9f, v * 3f + 4f));
            foil = Color.Lerp(foil, new Color(0.78f, 0.6f, 0.28f), gold * 0.45f);
            foil = Color.Lerp(foil, foil * new Color(0.82f, 0.62f, 0.5f), Mathf.SmoothStep(0.7f, 1f, N(xr * 6f + 50f, v * 6f)) * 0.5f); // poeira
            return new Color(foil.r, foil.g, foil.b, 1f);
        }

        // R = metálico, A = suavidade (URP/Lit).
        static Color ShieldGlossPixel(float u, float v)
        {
            if (u < 0.5f)
                return new Color(0.3f, 0f, 0f, 0.3f);

            var xr = (u - 0.5f) * 2f;
            var crinkle = N(xr * 55f, v * 55f);
            return new Color(0.62f, 0f, 0f, Mathf.Lerp(0.38f, 0.72f, crinkle));
        }

        // Heat Shield Rock (JPL, 19/1/2005): metal cinza claro de ferro e níquel, brilhante nas arestas, com poeira
        // alaranjada nas cavidades e crostas escuras de ablação em manchas.
        static Color MeteoritePixel(float u, float v)
        {
            var metal = Color.Lerp(new Color(0.44f, 0.43f, 0.42f), new Color(0.68f, 0.67f, 0.65f), N(u * 7f, v * 7f));
            metal *= 0.9f + 0.2f * N(u * 55f, v * 55f);
            var rust = Mathf.SmoothStep(0.5f, 0.82f, N(u * 11f + 3f, v * 11f));
            metal = Color.Lerp(metal, new Color(0.62f, 0.34f, 0.2f), rust * 0.5f);
            var crust = Mathf.SmoothStep(0.62f, 0.9f, N(u * 5f + 21f, v * 5f + 8f));
            metal = Color.Lerp(metal, new Color(0.14f, 0.12f, 0.11f), crust * 0.55f);
            return new Color(metal.r, metal.g, metal.b, 1f);
        }

        static Color RockPixel(float u, float v)
        {
            var c = Color.Lerp(new Color(0.36f, 0.26f, 0.2f), new Color(0.54f, 0.4f, 0.3f), N(u * 8f, v * 8f));
            c *= 0.88f + 0.24f * N(u * 40f, v * 40f);
            return new Color(c.r, c.g, c.b, 1f);
        }

        // ---------- Malhas ----------

        static Mesh SaveMesh(string name, G.Draft draft)
        {
            var path = GeneratedPath + "/" + name + ".asset";
            Directory.CreateDirectory(GeneratedPath);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            draft.Write(mesh);
            mesh.name = name;
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ---------- Hierarquia ----------

        static Transform Child(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null)
                return found;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static T Get<T>(GameObject go) where T : Component => go.GetComponent<T>() != null ? go.GetComponent<T>() : go.AddComponent<T>();

        static MeshRenderer Surface(Transform t, Mesh mesh, Material material, bool shadows = true)
        {
            Get<MeshFilter>(t.gameObject).sharedMesh = mesh;
            var renderer = Get<MeshRenderer>(t.gameObject);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return renderer;
        }

        static void BuildRock(GameObject root, Material material)
        {
            var high = SaveMesh("Meteorite_LOD0", G.MeteoriteMesh(3));
            var low = SaveMesh("Meteorite_LOD1", G.MeteoriteMesh(2));
            var group = Get<LODGroup>(root);
            var r0 = Surface(Child(root.transform, "LOD0"), high, material);
            var r1 = Surface(Child(root.transform, "LOD1"), low, material);
            group.SetLODs(new[] { new LOD(0.12f, new Renderer[] { r0 }), new LOD(0.02f, new Renderer[] { r1 }) });
            group.RecalculateBounds();

            var collider = Get<SphereCollider>(root);
            collider.center = new Vector3(0f, G.MeteoriteRadiusY * 0.8f, 0f);
            collider.radius = 0.12f;

            var anchors = Child(root.transform, "Anchors");
            Child(anchors, "MeasurePoint").localPosition = new Vector3(0f, G.MeteoriteRadiusY * 1.8f, 0f);
            Child(anchors, "PhotoTarget").localPosition = new Vector3(0f, G.MeteoriteRadiusY, 0f);
        }

        static void BuildEnvironment(GameObject root, Material terrainMat, Material shieldMat, Material rockMat, GameObject rockPrefab)
        {
            var terrain = Child(root.transform, "Terrain");
            var coarse = SaveMesh("Terrain_Near", G.Terrain(-G.NearHalfSize, -G.NearHalfSize, G.NearHalfSize * 2f, 96, 2f));
            var near = Child(terrain, "Near_24m");
            Surface(near, coarse, terrainMat, true);
            var collider = Get<MeshCollider>(near.gameObject);
            collider.sharedMesh = coarse;
            for (var iz = -1; iz <= 1; iz++)
            {
                for (var ix = -1; ix <= 1; ix++)
                {
                    if (ix == 0 && iz == 0)
                        continue;

                    var mesh = SaveMesh($"Terrain_Ring_{ix + 1}_{iz + 1}", G.Terrain(ix * 24f - 12f, iz * 24f - 12f, 24f, 12, 0f));
                    Surface(Child(terrain, $"Ring_{ix + 1}_{iz + 1}"), mesh, terrainMat, false);
                }
            }

            var debris = Child(root.transform, "Debris");
            BuildPiece(debris, "HeatShield_Main", true, G.MainPose, 18, 52, 6, 28, shieldMat);
            BuildPiece(debris, "HeatShield_Flank", false, G.FlankPose, 6, 12, 3, 6, shieldMat);

            BuildScatter(Child(root.transform, "Scatter"), rockMat);

            var meteorite = root.transform.Find("Meteorite");
            if (meteorite == null)
            {
                var created = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab, root.scene);
                created.name = "Meteorite";
                created.transform.SetParent(root.transform, false);
                meteorite = created.transform;
            }

            var rp = G.Ground(G.Meteorite.x, G.Meteorite.y);
            meteorite.localPosition = rp + Vector3.down * 0.02f;
            meteorite.localRotation = Quaternion.AngleAxis(38f, Vector3.up);
            meteorite.localScale = Vector3.one;

            BuildAnchors(root, meteorite);
        }

        static void BuildPiece(Transform parent, string name, bool whole, G.PiecePose pose, int radial0, int angular0, int radial1, int angular1, Material material)
        {
            var piece = Child(parent, name);
            piece.localPosition = pose.Origin;
            piece.localRotation = pose.Rotation;
            var group = Get<LODGroup>(piece.gameObject);
            var m0 = SaveMesh(name + "_LOD0", G.Shell(whole, radial0, angular0));
            var m1 = SaveMesh(name + "_LOD1", G.Shell(whole, radial1, angular1));
            var r0 = Surface(Child(piece, "LOD0"), m0, material);
            var r1 = Surface(Child(piece, "LOD1"), m1, material);
            group.SetLODs(new[] { new LOD(0.2f, new Renderer[] { r0 }), new LOD(0.025f, new Renderer[] { r1 }) });
            group.RecalculateBounds();
            Get<MeshCollider>(piece.gameObject).sharedMesh = m1;

            var supports = Child(piece, "Supports");
            var wanted = new HashSet<string>(pose.Supports.Select(x => "Support_" + x.Name));
            for (var i = supports.childCount - 1; i >= 0; i--)
            {
                // Reconstruir não pode deixar apoios de uma versão anterior da pose.
                if (!wanted.Contains(supports.GetChild(i).name))
                    Object.DestroyImmediate(supports.GetChild(i).gameObject);
            }

            foreach (var support in pose.Supports)
                Child(supports, "Support_" + support.Name).position = support.World;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Estação do rover para a rocha: 1,35 m antes dela, na linha que vem da estação do escudo.</summary>
        public static Vector2 MeteoriteStationXZ()
        {
            var dir = (G.Meteorite - G.ShieldStation).normalized;
            return G.Meteorite - dir * 1.35f;
        }

        static bool InClearZone(Vector2 p, float margin)
        {
            var station = MeteoriteStationXZ();
            if (SegmentDistance(p, G.ApproachStart, G.ShieldStation) < 1.2f + margin) return true;
            if (SegmentDistance(p, G.ShieldStation, station) < 1.2f + margin) return true;
            if (Vector2.Distance(p, G.Meteorite) < 1.6f + margin) return true;
            if (Vector2.Distance(p, station) < 1.6f + margin) return true;
            if (Vector2.Distance(p, G.MainCenter) < 2.4f + margin) return true;
            if (Vector2.Distance(p, G.FlankCenter) < 1.5f + margin) return true;
            return Vector2.Distance(p, G.Divot) < 1.4f + margin;
        }

        static void BuildScatter(Transform parent, Material material)
        {
            var variants = new Mesh[6];
            for (var i = 0; i < variants.Length; i++)
                variants[i] = SaveMesh("FieldRock_" + i, G.FieldRock(40 + i));

            var rng = new System.Random(1354);
            var placed = new List<Vector3>();
            var count = 0;
            for (var attempt = 0; attempt < 2000 && count < 64; attempt++)
            {
                var far = count >= 44;
                var half = far ? 30f : 11.5f;
                var p = new Vector2(((float)rng.NextDouble() * 2f - 1f) * half, ((float)rng.NextDouble() * 2f - 1f) * half);
                if (far && Mathf.Abs(p.x) < 12.5f && Mathf.Abs(p.y) < 12.5f)
                    continue;

                var size = far ? Mathf.Lerp(0.3f, 0.9f, (float)rng.NextDouble()) : Mathf.Lerp(0.05f, 0.3f, Mathf.Pow((float)rng.NextDouble(), 2.2f));
                if (!far && InClearZone(p, size * 0.6f))
                    continue;

                var tooClose = false;
                foreach (var other in placed)
                    tooClose |= Vector2.Distance(p, new Vector2(other.x, other.y)) < other.z + size * 0.7f + 0.15f;
                if (tooClose)
                    continue;

                placed.Add(new Vector3(p.x, p.y, size));
                var t = Child(parent, "Rock_" + count.ToString("00"));
                t.localPosition = G.Ground(p.x, p.y) + Vector3.down * size * 0.08f;
                t.localRotation = Quaternion.FromToRotation(Vector3.up, G.Normal(p.x, p.y)) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                t.localScale = new Vector3(size * 2f, size * Mathf.Lerp(1.4f, 2.2f, (float)rng.NextDouble()), size * 2f);
                Surface(t, variants[count % variants.Length], material);
                if (size > 0.18f)
                {
                    var sphere = Get<SphereCollider>(t.gameObject);
                    sphere.center = new Vector3(0f, 0.12f, 0f);
                    sphere.radius = 0.4f;
                }

                count++;
            }
        }

        static void Place(Transform t, Vector2 xz, float lift, Vector3 lookAt)
        {
            var ground = G.Ground(xz.x, xz.y);
            t.localPosition = ground + Vector3.up * lift;
            var forward = lookAt - t.localPosition;
            if (forward.sqrMagnitude > 1e-6f)
                t.localRotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        static void BuildAnchors(GameObject root, Transform meteorite)
        {
            var anchors = Child(root.transform, "Anchors");
            var target = new Vector3(-0.9f, 0f, 4.1f);
            target.y = G.Height(target.x, target.z) + 0.3f;
            var photoTarget = Child(anchors, "ShieldPhotoTarget");
            photoTarget.localPosition = target;

            var station = Child(anchors, "ShieldStation");
            var stationGround = G.Ground(G.ShieldStation.x, G.ShieldStation.y);
            station.localPosition = stationGround;
            station.localRotation = Quaternion.LookRotation(new Vector3(target.x - stationGround.x, 0f, target.z - stationGround.z), Vector3.up);

            var start = Child(anchors, "ApproachStart");
            start.localPosition = G.Ground(G.ApproachStart.x, G.ApproachStart.y);
            start.localRotation = Quaternion.LookRotation(new Vector3(G.ShieldStation.x - G.ApproachStart.x, 0f, G.ShieldStation.y - G.ApproachStart.y), Vector3.up);

            var cameraAnchor = Child(anchors, "ShieldCameraAnchor");
            Place(cameraAnchor, G.ShieldStation, 1.35f, target);

            var rockStation = MeteoriteStationXZ();
            var mStation = Child(anchors, "MeteoriteStation");
            var mGround = G.Ground(rockStation.x, rockStation.y);
            mStation.localPosition = mGround;
            mStation.localRotation = Quaternion.LookRotation(new Vector3(G.Meteorite.x - rockStation.x, 0f, G.Meteorite.y - rockStation.y), Vector3.up);

            var measure = meteorite.Find("Anchors/MeasurePoint");
            var photo = meteorite.Find("Anchors/PhotoTarget");
            var mCamera = Child(anchors, "MeteoriteCameraAnchor");
            var rockGround = G.Ground(G.Meteorite.x, G.Meteorite.y);
            mCamera.localPosition = rockGround + new Vector3(1.6f, 0.55f, -1.7f);
            mCamera.localRotation = Quaternion.LookRotation(rockGround + Vector3.up * 0.12f - mCamera.localPosition, Vector3.up);

            var component = Get<ShieldFieldAnchors>(root);
            var serialized = new SerializedObject(component);
            serialized.FindProperty("approachStart").objectReferenceValue = start;
            serialized.FindProperty("shieldStation").objectReferenceValue = station;
            serialized.FindProperty("shieldPhotoTarget").objectReferenceValue = photoTarget;
            serialized.FindProperty("shieldCameraAnchor").objectReferenceValue = cameraAnchor;
            serialized.FindProperty("meteoriteStation").objectReferenceValue = mStation;
            serialized.FindProperty("meteoriteMeasurePoint").objectReferenceValue = measure;
            serialized.FindProperty("meteoritePhotoTarget").objectReferenceValue = photo;
            serialized.FindProperty("meteoriteCameraAnchor").objectReferenceValue = mCamera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Prévia ----------

        static void BuildPreviewRig(SceneBuildContext context, ShieldFieldAnchors anchors)
        {
            var sunObject = context.Child(context.Root.transform, "Sun · single shadow light");
            sunObject.transform.rotation = Quaternion.Euler(38f, 38f, 0f);
            var sun = context.Component<Light>(sunObject);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.9f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.3f;

            var cameraObject = context.Child(context.Root.transform, "Preview Camera");
            cameraObject.tag = "MainCamera";
            var camera = context.Component<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.66f, 0.5f, 0.4f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 300f;
            var data = context.Component<UniversalAdditionalCameraData>(cameraObject);
            data.renderPostProcessing = false;
            cameraObject.transform.SetPositionAndRotation(anchors.ShieldCameraAnchor.position, anchors.ShieldCameraAnchor.rotation);

            RenderSettings.skybox = null;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.66f, 0.5f, 0.4f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 120f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.34f, 0.27f, 0.23f);
        }
    }
}
