using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Abandoned.SpaceApps.Chapters.Surveyor.EditorTools
{
    /// <summary>Reconstrução artística, não levantamento topográfico. Metros, origem no centro do tripé.</summary>
    public static class SurveyorSiteGeometry
    {
        // Distribuição de tripé: uma impressão em primeiro plano, outras duas separadas pela envergadura.
        public static Vector2 LandingMarkCenter(int index) => index == 0 ? new Vector2(-2.2f,4f)
            : index == 1 ? new Vector2(-4.06195f,7.225f) : new Vector2(-.33805f,7.225f);

        public static float Height(float x, float z)
        {
            float radius = new Vector2(x + 65f, z).magnitude;
            float r = Mathf.Min(radius / 105f, 1f);
            float h = 15f * (r * r - 65f * 65f / (105f * 105f));
            if (radius > 105f) h -= Mathf.Min((radius - 105f) * .025f, 2f);
            h += .16f * (Mathf.PerlinNoise(x * .065f + 20f, z * .065f + 30f) - .5f);
            h += .035f * (Mathf.PerlinNoise(x * .73f + 80f, z * .73f + 60f) - .5f);
            h += Crater(x, z, -7f, 5f, 2.7f, .48f);
            h += Crater(x, z, 10f, -7f, 1.8f, .27f);
            h += Crater(x, z, -17f, -9f, 4.7f, .8f);
            h += Crater(x, z, -37f, 23f, 8f, 1.8f);
            return h;
        }

        static float Crater(float x, float z, float cx, float cz, float radius, float depth)
        {
            float r = new Vector2(x - cx, z - cz).magnitude / radius;
            if (r > 1.4f) return 0f;
            return -depth * Mathf.Pow(Mathf.Max(0f, 1f - r * r), 2f)
                + depth * .22f * Mathf.Exp(-Mathf.Pow((r - 1.03f) * 6f, 2f));
        }

        public static Vector3 Ground(float x, float z, float clearance = 0f) => new Vector3(x, Height(x, z) + clearance, z);

        public static Vector3 Normal(float x, float z)
        {
            const float delta = .04f;
            return new Vector3(Height(x - delta, z) - Height(x + delta, z), delta * 2f,
                Height(x, z - delta) - Height(x, z + delta)).normalized;
        }

        public static void Terrain(Mesh mesh, float minX, float minZ, float size, int cells)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (var z = 0; z <= cells; z++)
                for (var x = 0; x <= cells; x++)
                {
                    float px = minX + x * size / cells, pz = minZ + z * size / cells;
                    var point = Ground(px, pz);
                    // A malha próxima encontra exatamente a interpolação da borda de LOD distante.
                    if (size < 40f && (Mathf.Abs(Mathf.Abs(px)-20f)<.001f || Mathf.Abs(Mathf.Abs(pz)-20f)<.001f))
                    {
                        bool vertical = Mathf.Abs(Mathf.Abs(px)-20f)<.001f;
                        float along = vertical ? pz : px;
                        float start = -20f + Mathf.Floor((along+20f)/(40f/12f))*(40f/12f);
                        float u = (along-start)/(40f/12f);
                        point.y = vertical ? Mathf.Lerp(Height(px,start),Height(px,start+40f/12f),u)
                            : Mathf.Lerp(Height(start,pz),Height(start+40f/12f,pz),u);
                    }
                    vertices.Add(point);
                    uv.Add(new Vector2((px + 20f) / 40f, (pz + 20f) / 40f));
                }
            for (var z = 0; z < cells; z++)
                for (var x = 0; x < cells; x++)
                {
                    int a = z * (cells + 1) + x, b = a + cells + 1;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            Set(mesh, vertices, uv, triangles);
            // Normais analíticas iguais nas bordas de chunks/LOD.
            var normals = new Vector3[vertices.Count];
            for (var i = 0; i < normals.Length; i++) normals[i] = Normal(vertices[i].x, vertices[i].z);
            mesh.normals = normals;
            mesh.RecalculateTangents();
        }

        public static void Rock(Mesh mesh, int seed)
        {
            const int sides = 14, rings = 7;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (var r = 0; r <= rings; r++)
                for (var i = 0; i < sides; i++)
                {
                    float latitude = -Mathf.PI / 2f + Mathf.PI * r / rings;
                    float angle = i * Mathf.PI * 2f / sides;
                    float radius = .46f + .14f * Mathf.PerlinNoise(Mathf.Cos(angle)*1.6f+seed,Mathf.Sin(latitude)*1.6f+seed*.71f);
                    var p = new Vector3(Mathf.Cos(angle) * Mathf.Cos(latitude), Mathf.Sin(latitude), Mathf.Sin(angle) * Mathf.Cos(latitude)) * radius;
                    p.y = Mathf.Max(p.y, -.24f); // base truncada para enterrar, não uma esfera equilibrada num ponto.
                    vertices.Add(p);
                    uv.Add(new Vector2(p.x+.5f,p.z+.5f));
                }
            for (var r = 0; r < rings; r++)
                for (var i = 0; i < sides; i++)
                {
                    var a = r * sides + i; var b = r * sides + (i + 1) % sides;
                    triangles.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});
                }
            Set(mesh,vertices,uv,triangles);
        }

        static void Set(Mesh mesh, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        }

        /// <summary>Peças próprias combinadas por material; atlas de quatro faixas (branco, metal, escuro, solar).</summary>
        public sealed class Draft
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> triangles = new List<int>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, float band)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(new Vector2(band, .1f)); uv.Add(new Vector2(band + .07f, .85f)); uv.Add(new Vector2(band - .07f, .85f));
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float band)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                uv.AddRange(new[] { new Vector2(band - .1f, .05f), new Vector2(band - .1f, .95f),
                    new Vector2(band + .1f, .95f), new Vector2(band + .1f, .05f) });
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            public void Box(Vector3 p, Vector3 scale, float band, Quaternion rotation = default)
            {
                if (rotation == default) rotation = Quaternion.identity;
                var v = new Vector3[8];
                for (var i = 0; i < 8; i++) v[i] = p + rotation * Vector3.Scale(new Vector3((i & 1) == 0 ? -.5f : .5f,
                    (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f), scale);
                int[] faces = { 0, 2, 3, 1, 5, 7, 6, 4, 4, 6, 2, 0, 1, 3, 7, 5, 2, 6, 7, 3, 4, 0, 1, 5 };
                for (var i = 0; i < faces.Length; i += 4) Quad(v[faces[i]], v[faces[i+1]], v[faces[i+2]], v[faces[i+3]], band);
            }

            public void Tube(Vector3 a, Vector3 b, float radius, float band, int sides = 10, float endRadius = -1f)
            {
                if (endRadius < 0f) endRadius = radius;
                var direction = (b - a).normalized;
                var u = Vector3.Cross(direction, Mathf.Abs(direction.y) > .9f ? Vector3.right : Vector3.up).normalized;
                var v = Vector3.Cross(direction, u);
                for (var i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides, next = (i+1) * Mathf.PI * 2f / sides;
                    var p = u * Mathf.Cos(angle) + v * Mathf.Sin(angle);
                    var q = u * Mathf.Cos(next) + v * Mathf.Sin(next);
                    Quad(a + p * radius, a + q * radius, b + q * endRadius, b + p * endRadius, band);
                    Triangle(a, a + q * radius, a + p * radius, band);
                    Triangle(b, b + p * endRadius, b + q * endRadius, band);
                }
            }

            public void Write(Mesh mesh) => Set(mesh, vertices, uv, triangles);
        }
    }
}
