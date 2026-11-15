using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EditorTools
{
    /// <summary>
    /// SUR-009: gera, de forma determinística, <c>Art/Trench.prefab</c> e <c>Art/ScoopRig.prefab</c> a partir da âncora
    /// <c>Trench</c> e do ombro do <c>SurveyorVisibleRig.prefab</c> (SUR-002), sem editá-los. Os prefabs são assados
    /// em coordenadas do LunarSite (raiz da cena na origem): instancie-os sob o mesmo pai do site e do rig.
    /// Reconstruir preserva GUIDs (mesmos caminhos) e os assets de malha/material/áudio gerados em <c>Art/ScoopGenerated</c>.
    /// </summary>
    public static class SurveyorScoopBuilder
    {
        public const string ArtPath = "Assets/_Game/Chapters/Surveyor/Art";
        public const string GeneratedPath = ArtPath + "/ScoopGenerated";
        public const string TrenchPrefabPath = ArtPath + "/Trench.prefab";
        public const string RigPrefabPath = ArtPath + "/ScoopRig.prefab";
        const string VisibleRigPath = ArtPath + "/SurveyorVisibleRig.prefab";
        const string SiteGenerated = ArtPath + "/SiteGenerated";
        const float TileSize = 10f; const int TileCells = 24;
        const int BlockCellsX = 4, BlockCellsZ = 3, PatchSubdivisions = 12;
        const float White = .125f, Metal = .375f, Dark = .625f;
        public static readonly Vector3 TipInTool = new Vector3(0f, -.03f, .17f);

        /// <summary>Dados lidos do rig de SUR-002 (em coordenadas do site).</summary>
        public sealed class SiteFacts
        {
            public Vector3 Anchor, Shoulder, NativeTool, CameraMount;
        }

        public static SiteFacts ReadSite()
        {
            var contents = PrefabUtility.LoadPrefabContents(VisibleRigPath);
            try
            {
                var t = contents.transform;
                Transform Need(string path) => t.Find(path) ?? throw new InvalidOperationException("SurveyorVisibleRig sem " + path);
                return new SiteFacts
                {
                    Anchor = Need("Anchors/Trench").position,
                    Shoulder = Need("Body/Scoop_Arm_1").position,
                    NativeTool = Need("Body/Scoop_Arm_1/Scoop_Arm_2/Scoop").position,
                    CameraMount = Need("Body/TV_Mirror_Azimuth/TV_Mirror_Elevation/CameraMount").position,
                };
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        /// <summary>Quadro local do sulco: origem no solo sob a âncora, y na normal do terreno, x perpendicular à direção de visão da câmera.</summary>
        public static void TrenchFrame(SiteFacts facts, out Vector3 origin, out Quaternion rotation)
        {
            origin = SurveyorSiteGeometry.Ground(facts.Anchor.x, facts.Anchor.z);
            Vector3 up = SurveyorSiteGeometry.Normal(origin.x, origin.z);
            Vector3 view = facts.Anchor - facts.CameraMount; view.y = 0f;
            Vector3 across = Vector3.Cross(Vector3.up, view.normalized);
            if (across.x < 0f) across = -across;
            Vector3 tangent = Vector3.ProjectOnPlane(across, up).normalized;
            rotation = Quaternion.LookRotation(Vector3.Cross(tangent, up), up);
        }

        public static void BuildAll()
        {
            Directory.CreateDirectory(GeneratedPath);
            AssetDatabase.Refresh();
            var facts = ReadSite();
            TrenchFrame(facts, out var origin, out var rotation);
            var regolith = AssetDatabase.LoadAssetAtPath<Material>(SiteGenerated + "/Regolith.mat") ?? throw new InvalidOperationException("Regolith.mat ausente (SUR-002).");
            var painted = AssetDatabase.LoadAssetAtPath<Material>(SiteGenerated + "/PaintedHardware.mat") ?? throw new InvalidOperationException("PaintedHardware.mat ausente (SUR-002).");
            var soil = DerivedMaterial("TrenchSoil", regolith, Color.white, .05f);
            var grainMaterial = DerivedMaterial("ScoopGrain", regolith, new Color(.55f, .54f, .53f), .05f);
            BuildTrench(facts, origin, rotation, regolith, soil);
            BuildRig(facts, origin, rotation, painted, grainMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ---------- Trench ----------
        static void BuildTrench(SiteFacts facts, Vector3 origin, Quaternion rotation, Material regolith, Material soil)
        {
            var inverse = Quaternion.Inverse(rotation);
            // Chunk do terreno que contém a âncora: mesma grade de SUR-002, com um bloco de células substituído por um remendo fino.
            float minX = -20f + TileSize * Mathf.Floor((origin.x + 20f) / TileSize), minZ = -20f + TileSize * Mathf.Floor((origin.z + 20f) / TileSize);
            float cell = TileSize / TileCells;
            int cx0 = Mathf.RoundToInt((origin.x - minX) / cell - BlockCellsX * .5f), cz0 = Mathf.RoundToInt((origin.z - minZ) / cell - BlockCellsZ * .5f);
            cx0 = Mathf.Clamp(cx0, 1, TileCells - BlockCellsX - 1); cz0 = Mathf.Clamp(cz0, 1, TileCells - BlockCellsZ - 1);
            var full = new Mesh();
            SurveyorSiteGeometry.Terrain(full, minX, minZ, TileSize, TileCells);
            var fv = full.vertices; var fn = full.normals; var fu = full.uv; var ft = full.tangents; var ftri = full.triangles;
            var vertices = new List<Vector3>(fv.Length); var normals = new List<Vector3>(fv.Length); var uvs = new List<Vector2>(fv.Length); var tangents = new List<Vector4>(fv.Length);
            for (var i = 0; i < fv.Length; i++)
            {
                vertices.Add(inverse * (fv[i] - origin)); normals.Add(inverse * fn[i]); uvs.Add(fu[i]);
                var tan = inverse * new Vector3(ft[i].x, ft[i].y, ft[i].z); tangents.Add(new Vector4(tan.x, tan.y, tan.z, ft[i].w));
            }
            var tris = new List<int>();
            for (var cz = 0; cz < TileCells; cz++)
                for (var cxi = 0; cxi < TileCells; cxi++)
                {
                    bool inBlock = cxi >= cx0 && cxi < cx0 + BlockCellsX && cz >= cz0 && cz < cz0 + BlockCellsZ;
                    if (inBlock) continue;
                    int start = 6 * (cz * TileCells + cxi);
                    for (var k = 0; k < 6; k++) tris.Add(ftri[start + k]);
                }
            var tile = new Mesh { name = "TrenchTile" };
            tile.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            tile.SetVertices(vertices); tile.SetNormals(normals); tile.SetUVs(0, uvs); tile.SetTangents(tangents); tile.SetTriangles(tris, 0);
            tile.RecalculateBounds();
            var tileAsset = SaveMesh(tile, "TrenchTile");

            // Remendo fino: o contorno repete a interpolação linear das arestas grossas, então não há fresta.
            int nx = BlockCellsX * PatchSubdivisions, nz = BlockCellsZ * PatchSubdivisions;
            float x0 = minX + cx0 * cell, z0 = minZ + cz0 * cell, sx = BlockCellsX * cell / nx, sz = BlockCellsZ * cell / nz;
            var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pu = new List<Vector2>(); var pt = new List<Vector4>(); var world = new List<Vector3>();
            for (var iz = 0; iz <= nz; iz++)
                for (var ix = 0; ix <= nx; ix++)
                {
                    float wx = x0 + ix * sx, wz = z0 + iz * sz;
                    float h = SurveyorSiteGeometry.Height(wx, wz);
                    bool edgeX = ix == 0 || ix == nx, edgeZ = iz == 0 || iz == nz;
                    if (edgeX || edgeZ)
                    {
                        // Aresta do bloco: interpola entre os dois vértices grossos vizinhos.
                        if (edgeX && !edgeZ)
                        {
                            float a = Mathf.Floor((wz - z0) / cell + 1e-4f) * cell + z0, u = (wz - a) / cell;
                            h = Mathf.Lerp(SurveyorSiteGeometry.Height(wx, a), SurveyorSiteGeometry.Height(wx, a + cell), u);
                        }
                        else if (edgeZ && !edgeX)
                        {
                            float a = Mathf.Floor((wx - x0) / cell + 1e-4f) * cell + x0, u = (wx - a) / cell;
                            h = Mathf.Lerp(SurveyorSiteGeometry.Height(a, wz), SurveyorSiteGeometry.Height(a + cell, wz), u);
                        }
                    }
                    var w = new Vector3(wx, h, wz);
                    world.Add(w);
                    pv.Add(inverse * (w - origin));
                    pn.Add(inverse * SurveyorSiteGeometry.Normal(wx, wz));
                    pu.Add(new Vector2((wx + 20f) / 40f, (wz + 20f) / 40f));
                    var tan = inverse * Vector3.Cross(Vector3.forward, SurveyorSiteGeometry.Normal(wx, wz)).normalized;
                    pt.Add(new Vector4(tan.x, tan.y, tan.z, -1f));
                }
            for (var iz = 0; iz <= nz; iz++)
                for (var ix = 0; ix <= nx; ix++)
                {
                    if (ix != 0 && ix != nx && iz != 0 && iz != nz) continue;
                    var local = pv[iz * (nx + 1) + ix];
                    if (Mathf.Abs(SurveyorScoop.Shape.PassHeight(SurveyorScoop.PassCount, local.x, local.z)) > 1e-5f)
                        throw new InvalidOperationException("O sulco toca a borda do remendo; aumente o bloco de células ou mova a âncora.");
                }
            var ptris = new List<int>();
            for (var iz = 0; iz < nz; iz++)
                for (var ix = 0; ix < nx; ix++)
                {
                    int a = iz * (nx + 1) + ix, b = a + nx + 1;
                    ptris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var groove = new Mesh { name = "TrenchGroove" };
            groove.SetVertices(pv); groove.SetNormals(pn); groove.SetUVs(0, pu); groove.SetTangents(pt); groove.SetTriangles(ptris, 0);
            for (var pass = 1; pass <= SurveyorScoop.PassCount; pass++)
            {
                var dv = new Vector3[pv.Count]; var dn = new Vector3[pv.Count]; var dt = new Vector3[pv.Count];
                for (var i = 0; i < pv.Count; i++)
                {
                    float x = pv[i].x, z = pv[i].z;
                    dv[i] = Vector3.up * (SurveyorScoop.Shape.PassHeight(pass, x, z) - SurveyorScoop.Shape.PassHeight(pass - 1, x, z));
                    dn[i] = ShapedNormal(pn[i], pass, x, z) - ShapedNormal(pn[i], pass - 1, x, z);
                }
                groove.AddBlendShapeFrame(SurveyorScoop.BlendShapeNames[pass - 1], 100f, dv, dn, dt);
            }
            groove.RecalculateBounds();
            var bounds = groove.bounds; bounds.Expand(new Vector3(.1f, .5f, .1f));
            var grooveAsset = SaveMesh(groove, "TrenchGroove");

            var root = new GameObject("Trench");
            try
            {
                root.transform.SetPositionAndRotation(origin, rotation);
                var tileObject = new GameObject(SurveyorScoop.TileName); tileObject.transform.SetParent(root.transform, false);
                tileObject.AddComponent<MeshFilter>().sharedMesh = tileAsset;
                var tileRenderer = tileObject.AddComponent<MeshRenderer>(); tileRenderer.sharedMaterial = regolith; tileRenderer.receiveShadows = true;
                var grooveObject = new GameObject(SurveyorScoop.GrooveName); grooveObject.transform.SetParent(root.transform, false);
                var skinned = grooveObject.AddComponent<SkinnedMeshRenderer>();
                skinned.sharedMesh = grooveAsset; skinned.sharedMaterial = soil; skinned.localBounds = bounds;
                skinned.receiveShadows = true; skinned.shadowCastingMode = ShadowCastingMode.On; skinned.updateWhenOffscreen = false;
                var photo = new GameObject(SurveyorScoop.PhotoTargetName); photo.transform.SetParent(root.transform, false);
                photo.transform.localPosition = new Vector3(0f, .01f, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, TrenchPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>Normal aproximada do relevo local: normal base inclinada pelo gradiente do sulco acumulado.</summary>
        static Vector3 ShapedNormal(Vector3 baseNormal, int pass, float x, float z)
        {
            const float e = .004f;
            float dx = (SurveyorScoop.Shape.PassHeight(pass, x + e, z) - SurveyorScoop.Shape.PassHeight(pass, x - e, z)) / (2f * e);
            float dz = (SurveyorScoop.Shape.PassHeight(pass, x, z + e) - SurveyorScoop.Shape.PassHeight(pass, x, z - e)) / (2f * e);
            return (baseNormal + new Vector3(-dx, 0f, -dz)).normalized;
        }

        // ---------- ScoopRig ----------
        static void BuildRig(SiteFacts facts, Vector3 origin, Quaternion rotation, Material painted, Material grainMaterial)
        {
            // Alcance necessário: cobre repouso, aproximação, escavação e recolhimento (ponta + folga do punho).
            var path = new SurveyorScoop.Path(1f, rotation == default ? Vector3.zero : Quaternion.Inverse(rotation) * (facts.NativeTool + Vector3.down * .05f - origin));
            float farthest = 0f;
            void Consider(Vector3 local) => farthest = Mathf.Max(farthest, (origin + rotation * local - facts.Shoulder).magnitude);
            Consider(path.Rest); Consider(path.Above);
            for (float t = 0f; t <= SurveyorScoop.TotalDigSeconds; t += .25f) Consider(path.DigTip(t, out _));
            var start = path.DigTip(SurveyorScoop.TotalDigSeconds, out _);
            for (float s = 0f; s <= SurveyorScoop.RetractSeconds; s += .1f) Consider(path.RetractTip(s, start, 4f, out _, out _));
            float link = Mathf.Max(.9f, (farthest + .3f) * .5f);

            var upper = new SurveyorSiteGeometry.Draft(); var fore = new SurveyorSiteGeometry.Draft(); var bucket = new SurveyorSiteGeometry.Draft();
            foreach (var d in new[] { upper, fore })
            {
                foreach (float x in new[] { -.035f, .035f }) d.Tube(new Vector3(x, 0f, .03f), new Vector3(x, 0f, link - .03f), .013f, Metal, 8);
                for (var i = 1; i < 6; i++) d.Box(new Vector3(0f, 0f, link * i / 6f), new Vector3(.09f, .012f, .012f), White);
                d.Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, link), .009f, Dark, 6);
                d.Box(new Vector3(0f, 0f, 0f), new Vector3(.1f, .05f, .05f), White);
                d.Box(new Vector3(0f, 0f, link), new Vector3(.1f, .05f, .05f), White);
            }
            const float hw = SurveyorScoop.Shape.BucketHalfWidthMeters;
            bucket.Box(new Vector3(0f, -.03f, .10f), new Vector3(hw * 2f, .006f, .14f), Metal);
            bucket.Box(new Vector3(-hw, -.012f, .10f), new Vector3(.006f, .04f, .14f), Metal);
            bucket.Box(new Vector3(hw, -.012f, .10f), new Vector3(.006f, .04f, .14f), Metal);
            bucket.Box(new Vector3(0f, -.012f, .033f), new Vector3(hw * 2f, .04f, .006f), Metal);
            bucket.Box(new Vector3(0f, .02f, .02f), new Vector3(.05f, .05f, .05f), White);
            var upperMesh = SaveDraft(upper, "ScoopUpperLink"); var foreMesh = SaveDraft(fore, "ScoopForeLink"); var bucketMesh = SaveDraft(bucket, "ScoopBucket");
            var grainMesh = SaveMesh(GrainMesh(), "ScoopGrain");
            var clip = BuildMotorClip();

            var root = new GameObject("ScoopRig");
            try
            {
                root.transform.SetPositionAndRotation(facts.Shoulder, Quaternion.identity);
                GameObject Child(Transform parent, string name, Vector3 local)
                { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = local; return go; }
                void Visual(GameObject go, Mesh mesh) { go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = painted; r.receiveShadows = true; }
                var shoulder = Child(root.transform, "Shoulder", Vector3.zero);
                Visual(Child(shoulder.transform, "UpperLink", Vector3.zero), upperMesh);
                var elbow = Child(shoulder.transform, "Elbow", new Vector3(0f, 0f, link));
                Visual(Child(elbow.transform, "ForeLink", Vector3.zero), foreMesh);
                var wrist = Child(elbow.transform, "Wrist", new Vector3(0f, 0f, link));
                var tool = Child(wrist.transform, "Scoop", Vector3.zero);
                Visual(Child(tool.transform, "Bucket", Vector3.zero), bucketMesh);
                Child(tool.transform, "Tip", TipInTool);
                var poses = Child(root.transform, "Poses", Vector3.zero);
                var rest = Child(poses.transform, "Rest", Vector3.zero); rest.transform.position = facts.NativeTool + Vector3.down * .05f;
                Child(poses.transform, "Above", Vector3.zero); Child(poses.transform, "Contact", Vector3.zero);
                var grains = Child(root.transform, SurveyorScoop.GrainsPath, Vector3.zero);
                for (var i = 0; i < SurveyorScoop.GrainPoolSize; i++)
                {
                    var g = Child(grains.transform, "Grain_" + i.ToString("00"), Vector3.zero);
                    g.AddComponent<MeshFilter>().sharedMesh = grainMesh;
                    var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = grainMaterial; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    g.SetActive(false);
                }
                var motor = Child(root.transform, SurveyorScoop.MotorPath, Vector3.zero).AddComponent<AudioSource>();
                motor.clip = clip; motor.loop = true; motor.playOnAwake = false; motor.spatialBlend = 0f; motor.volume = .06f;
                PrefabUtility.SaveAsPrefabAsset(root, RigPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Mesh GrainMesh()
        {
            const float r = .011f;
            var v = new[] { new Vector3(r, 0, 0), new Vector3(-r, 0, 0), new Vector3(0, r, 0), new Vector3(0, -r, 0), new Vector3(0, 0, r), new Vector3(0, 0, -r) };
            var t = new[] { 0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 4, 3, 0, 1, 3, 4, 5, 3, 1, 0, 3, 5 };
            var m = new Mesh { name = "ScoopGrain" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ---------- áudio próprio: motor curto em laço, sem vento nem som aéreo da Lua ----------
        static AudioClip BuildMotorClip()
        {
            const int rate = 22050, count = rate; // 1 s: frequências inteiras fecham o laço sem estalo
            var data = new short[count];
            for (var i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float ratchet = .55f + .45f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                float v = .55f * Mathf.Sin(2f * Mathf.PI * 92f * t) + .25f * Mathf.Sin(2f * Mathf.PI * 184f * t) + .12f * Mathf.Sin(2f * Mathf.PI * 276f * t);
                data[i] = (short)(v * ratchet * 9000f);
            }
            string path = GeneratedPath + "/ScoopMotor.wav";
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + count * 2); w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(count * 2);
                foreach (var s in data) w.Write(s);
                var bytes = stream.ToArray();
                if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
            }
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---------- utilitários de asset ----------
        static Mesh SaveDraft(SurveyorSiteGeometry.Draft draft, string name)
        {
            var mesh = new Mesh { name = name }; draft.Write(mesh); return SaveMesh(mesh, name);
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = GeneratedPath + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            existing.Clear(); EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        static Material DerivedMaterial(string name, Material source, Color tint, float smoothness)
        {
            string path = GeneratedPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
            material.shader = source.shader; material.CopyPropertiesFromMaterial(source);
            material.SetColor("_BaseColor", tint); material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- validação ----------
        public static List<string> Validate()
        {
            var errors = new List<string>();
            var trench = AssetDatabase.LoadAssetAtPath<GameObject>(TrenchPrefabPath);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (trench == null || rig == null) { errors.Add("Prefabs ausentes: gere com Abandoned Space Apps → Surveyor → SUR-009 → Reconstruir pá e trincheira."); return errors; }
            var groove = trench.transform.Find(SurveyorScoop.GrooveName)?.GetComponent<SkinnedMeshRenderer>();
            if (groove == null || groove.sharedMesh == null) errors.Add("Groove sem malha.");
            else
            {
                if (groove.sharedMesh.blendShapeCount != SurveyorScoop.PassCount) errors.Add("Blend shapes: " + groove.sharedMesh.blendShapeCount);
                if (groove.sharedMesh.vertexCount > 4000) errors.Add("Remendo excede 4 000 vértices.");
            }
            if (trench.transform.Find(SurveyorScoop.TileName) == null) errors.Add("Tile ausente.");
            if (trench.transform.Find(SurveyorScoop.PhotoTargetName) == null) errors.Add("PhotoTarget ausente.");
            foreach (var path in new[] { SurveyorScoop.ShoulderPath, SurveyorScoop.ElbowPath, SurveyorScoop.WristPath, SurveyorScoop.ScoopToolPath, SurveyorScoop.TipPath,
                SurveyorScoop.RestPosePath, SurveyorScoop.AbovePosePath, SurveyorScoop.ContactPosePath, SurveyorScoop.GrainsPath, SurveyorScoop.MotorPath })
                if (rig.transform.Find(path) == null) errors.Add("ScoopRig sem " + path);
            var grains = rig.transform.Find(SurveyorScoop.GrainsPath);
            if (grains != null && grains.childCount != SurveyorScoop.GrainPoolSize) errors.Add("Grãos: " + grains.childCount);
            var motor = rig.transform.Find(SurveyorScoop.MotorPath)?.GetComponent<AudioSource>();
            if (motor == null || motor.clip == null || !motor.loop) errors.Add("Motor sem clipe em laço.");
            foreach (var mf in trench.GetComponentsInChildren<Renderer>(true).Concat(rig.GetComponentsInChildren<Renderer>(true)))
                if (mf.sharedMaterial == null) errors.Add("Material ausente em " + mf.name);
            return errors;
        }

        [MenuItem("Abandoned Space Apps/Surveyor/SUR-009/Reconstruir pá e trincheira")]
        public static void BuildFromMenu()
        {
            BuildAll();
            var errors = Validate();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("SUR-009: Trench.prefab e ScoopRig.prefab reconstruídos e válidos.");
        }

        public static void BuildFromCommandLine()
        {
            try { BuildFromMenu(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
