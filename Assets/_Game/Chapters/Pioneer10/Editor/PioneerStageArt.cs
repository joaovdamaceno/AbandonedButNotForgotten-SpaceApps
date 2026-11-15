using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-003 · Geometria e texturas procedurais do palco, geradas de forma determinística (mesma entrada → mesmos bytes).
    /// A silhueta da sonda é PROVISÓRIA: só o diâmetro do refletor (2,74 m, P-S05) vem de fonte; as demais proporções
    /// são aproximadas e serão substituídas pelo modelo NASA da PIO-002, que preserva os sockets.
    /// </summary>
    public static class PioneerStageArt
    {
        // ---------- Malhas ----------

        /// <summary>Refletor parabólico de duas faces, côncavo para +Z (eixo da antena), vértice em z = 0.</summary>
        public static void Dish(Mesh mesh, float radiusMeters, float depthMeters, float thicknessMeters, int segments, int rings)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (var side = 0; side < 2; side++)
            {
                var offset = side == 0 ? 0f : -thicknessMeters;
                var start = vertices.Count;
                for (var ring = 0; ring <= rings; ring++)
                {
                    var t = ring / (float)rings;
                    var r = radiusMeters * t;
                    var z = depthMeters * t * t + offset;
                    var slope = 2f * depthMeters * t / radiusMeters; // dz/dr
                    for (var s = 0; s <= segments; s++)
                    {
                        var angle = s / (float)segments * Mathf.PI * 2f;
                        var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                        vertices.Add(radial * r + Vector3.forward * z);
                        var normal = new Vector3(-radial.x * slope, -radial.y * slope, 1f).normalized;
                        normals.Add(side == 0 ? normal : -normal);
                        uvs.Add(new Vector2(s / (float)segments, t));
                    }
                }

                for (var ring = 0; ring < rings; ring++)
                {
                    for (var s = 0; s < segments; s++)
                    {
                        var a = start + ring * (segments + 1) + s;
                        var b = a + segments + 1;
                        // No anel central (raio zero) o primeiro triângulo teria área zero: não é gerado.
                        if (side == 0)
                            triangles.AddRange(ring == 0 ? new[] { a + 1, b, b + 1 } : new[] { a, b, a + 1, a + 1, b, b + 1 });
                        else
                            triangles.AddRange(ring == 0 ? new[] { a + 1, b + 1, b } : new[] { a, a + 1, b, a + 1, b + 1, b });
                    }
                }
            }

            // Borda que fecha as duas faces.
            var rimStart = vertices.Count;
            for (var s = 0; s <= segments; s++)
            {
                var angle = s / (float)segments * Mathf.PI * 2f;
                var radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices.Add(radial * radiusMeters + Vector3.forward * depthMeters);
                vertices.Add(radial * radiusMeters + Vector3.forward * (depthMeters - thicknessMeters));
                normals.Add(radial);
                normals.Add(radial);
                uvs.Add(new Vector2(s / (float)segments, 1f));
                uvs.Add(new Vector2(s / (float)segments, 0.98f));
            }

            for (var s = 0; s < segments; s++)
            {
                var a = rimStart + s * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }

            Fill(mesh, "PIO_Dish", vertices, normals, uvs, triangles);
        }

        /// <summary>Prisma regular ao longo de +Z (0 → comprimento), faces planas e tampas. Serve de hexágono, haste e RTG.</summary>
        public static void Prism(Mesh mesh, string name, int sides, float radiusMeters, float lengthMeters)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (var s = 0; s < sides; s++)
            {
                var a0 = s / (float)sides * Mathf.PI * 2f;
                var a1 = (s + 1) / (float)sides * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radiusMeters;
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radiusMeters;
                var normal = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), Mathf.Sin((a0 + a1) * 0.5f), 0f);
                var i = vertices.Count;
                vertices.AddRange(new[] { p0, p1, p0 + Vector3.forward * lengthMeters, p1 + Vector3.forward * lengthMeters });
                normals.AddRange(new[] { normal, normal, normal, normal });
                uvs.AddRange(new[] { new Vector2(s / (float)sides, 0f), new Vector2((s + 1) / (float)sides, 0f), new Vector2(s / (float)sides, 1f), new Vector2((s + 1) / (float)sides, 1f) });
                triangles.AddRange(new[] { i, i + 1, i + 2, i + 1, i + 3, i + 2 });
            }

            for (var cap = 0; cap < 2; cap++)
            {
                var z = cap == 0 ? 0f : lengthMeters;
                var normal = cap == 0 ? Vector3.back : Vector3.forward;
                var center = vertices.Count;
                vertices.Add(new Vector3(0f, 0f, z));
                normals.Add(normal);
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (var s = 0; s < sides; s++)
                {
                    var a = s / (float)sides * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radiusMeters, Mathf.Sin(a) * radiusMeters, z));
                    normals.Add(normal);
                    uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }

                for (var s = 0; s < sides; s++)
                {
                    var a = center + 1 + s;
                    var b = center + 1 + (s + 1) % sides;
                    if (cap == 0)
                        triangles.AddRange(new[] { center, b, a });
                    else
                        triangles.AddRange(new[] { center, a, b });
                }
            }

            Fill(mesh, name, vertices, normals, uvs, triangles);
        }

        /// <summary>Caixa centrada na origem.</summary>
        public static void Box(Mesh mesh, string name, Vector3 sizeMeters)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var h = sizeMeters * 0.5f;
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in axes)
            {
                var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                var v = Vector3.Cross(n, u);
                var center = Vector3.Scale(n, h);
                var hu = Vector3.Scale(u, h);
                var hv = Vector3.Scale(v, h);
                var i = vertices.Count;
                vertices.AddRange(new[] { center - hu - hv, center + hu - hv, center - hu + hv, center + hu + hv });
                normals.AddRange(new[] { n, n, n, n });
                uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
                triangles.AddRange(new[] { i, i + 1, i + 2, i + 1, i + 3, i + 2 });
            }

            Fill(mesh, name, vertices, normals, uvs, triangles);
        }

        /// <summary>Esfera UV de raio 1 (u = longitude, v = latitude). <paramref name="inward"/> vira faces e normais (céu).</summary>
        public static void Sphere(Mesh mesh, string name, int longitudeSegments, int latitudeSegments, bool inward)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var lat = 0; lat <= latitudeSegments; lat++)
            {
                var v = lat / (float)latitudeSegments;
                var theta = (v - 0.5f) * Mathf.PI;
                for (var lon = 0; lon <= longitudeSegments; lon++)
                {
                    var u = lon / (float)longitudeSegments;
                    var phi = u * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(theta), Mathf.Cos(theta) * Mathf.Sin(phi));
                    vertices.Add(p);
                    normals.Add(inward ? -p : p);
                    uvs.Add(new Vector2(inward ? 1f - u : u, v));
                }
            }

            for (var lat = 0; lat < latitudeSegments; lat++)
            {
                for (var lon = 0; lon < longitudeSegments; lon++)
                {
                    var a = lat * (longitudeSegments + 1) + lon;
                    var b = a + longitudeSegments + 1;
                    // Nos polos um dos dois triângulos teria área zero: não é gerado.
                    if (lat > 0)
                        triangles.AddRange(inward ? new[] { a, a + 1, b } : new[] { a, b, a + 1 });
                    if (lat < latitudeSegments - 1)
                        triangles.AddRange(inward ? new[] { a + 1, b + 1, b } : new[] { a + 1, b, b + 1 });
                }
            }

            Fill(mesh, name, vertices, normals, uvs, triangles);
        }

        static void Fill(Mesh mesh, string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        public const int PaletteColumns = 8;
        public const int PaletteRows = 2;

        /// <summary>
        /// Estrelas brilhantes nítidas: quads pequenos numa esfera de raio 1, voltados para o centro (câmera). A cor e o
        /// brilho vêm de uma paleta (<see cref="StarPalette"/>) via UV, para um único material e um único draw call.
        /// Tamanho angular em graus: legível de 720p a 1080p sem virar mancha.
        /// </summary>
        /// <summary>
        /// Uma pedra irregular (icosfera de 80 faces com vértices deslocados, sombreamento chapado), de raio ~<paramref name="radiusMeters"/>.
        /// Determinística pela semente. Acrescenta ao mesmo <paramref name="vertices"/>/<paramref name="triangles"/> para juntar muitas pedras numa malha.
        /// </summary>
        public static void AppendRock(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 center, float radiusMeters, int seed)
        {
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var basis = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var faces = new[]
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var stretch = new Vector3(0.7f + Next() * 0.6f, 0.7f + Next() * 0.6f, 0.7f + Next() * 0.6f);
            var rotation = Quaternion.Euler(Next() * 360f, Next() * 360f, Next() * 360f);
            var radii = new float[basis.Length];
            for (var i = 0; i < radii.Length; i++)
                radii[i] = 0.78f + Next() * 0.44f;

            // Cada vértice da icosfera recebe um raio; as faces ficam planas (um normal por face).
            Vector3 Corner(int i) => center + rotation * Vector3.Scale(basis[i].normalized * (radiusMeters * radii[i]), stretch);
            for (var f = 0; f < faces.Length; f += 3)
            {
                var a = Corner(faces[f]);
                var b = Corner(faces[f + 1]);
                var c = Corner(faces[f + 2]);
                var normal = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(normal, a - center) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }

                var baseIndex = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
            }
        }

        /// <summary>
        /// Campo de pedras do cinturão (roteiro v2, cena 2): <paramref name="count"/> pedras num cilindro de raio
        /// <paramref name="radiusMeters"/> ao redor do eixo <paramref name="axis"/> que sai da origem (a sonda), de
        /// <paramref name="minDepth"/> a <paramref name="maxDepth"/> metros ATRÁS dela. Numa teleobjetiva isso vira uma parede
        /// compacta; num plano aberto, as pedras ficam pequenas e longe umas das outras. Determinístico pela semente.
        /// </summary>
        public static void RockField(Mesh mesh, int count, int seed, Vector3 axis, float minDepth, float maxDepth, float cameraMeters, float halfConeDegrees, float minFraction, float maxFraction)
        {
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            axis.Normalize();
            var right = Vector3.Cross(Vector3.up, axis).normalized;
            var up = Vector3.Cross(axis, right);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < count; i++)
            {
                var depth = Mathf.Lerp(minDepth, maxDepth, Next());
                var angle = Next() * Mathf.PI * 2f;
                // Cone da câmera teleobjetiva (cameraMeters atrás da sonda, meio ângulo halfConeDegrees): cada pedra tem o mesmo
                // tamanho angular típico, qualquer que seja a profundidade (muitas pequenas, poucas grandes), e todas cabem no quadro.
                var distance = cameraMeters + depth;
                var radial = distance * Mathf.Tan(halfConeDegrees * Mathf.Deg2Rad) * Mathf.Sqrt(Next());
                var size = distance * Mathf.Lerp(minFraction, maxFraction, Next() * Next());
                var center = axis * depth + right * (Mathf.Cos(angle) * radial) + up * (Mathf.Sin(angle) * radial);
                AppendRock(vertices, normals, triangles, center, size, seed * 7919 + i);
            }

            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        // ---------- Superfícies de planeta (roteiro v2: a Lua e Marte; geradas por código, sem asset externo) ----------

        /// <summary>smoothstep de shader: 0 abaixo de <paramref name="edge0"/>, 1 acima de <paramref name="edge1"/>.</summary>
        static float Smooth(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Ruído de valor 0–1, periódico em <paramref name="period"/> células no eixo X (para a textura fechar a volta no planeta).</summary>
        static float ValueNoise(float x, float y, int period, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = x - x0;
            var fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float Cell(int cx, int cy) => Hash(((cx % period) + period) % period, cy, seed);
            var a = Mathf.Lerp(Cell(x0, y0), Cell(x0 + 1, y0), fx);
            var b = Mathf.Lerp(Cell(x0, y0 + 1), Cell(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(a, b, fy);
        }

        /// <summary>Ruído fractal 0–1 fechando a volta no eixo X; <paramref name="baseCells"/> células na largura da imagem.</summary>
        static float Fractal(float u, float v, int baseCells, int octaves, int seed)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var total = 0f;
            var cells = baseCells;
            for (var o = 0; o < octaves; o++)
            {
                sum += amplitude * ValueNoise(u * cells, v * cells * 0.5f, cells, seed + o * 31);
                total += amplitude;
                amplitude *= 0.5f;
                cells *= 2;
            }

            return sum / total;
        }

        /// <summary>
        /// Lua (equiretangular): terras claras, mares escuros de baixa frequência e crateras com borda clara. Cinza neutro (a Lua não
        /// tem cor); o brilho médio fica perto de 0,5 para que a luz do palco a revele.
        /// </summary>
        public static Texture2D MoonSurface(int width, int height, int seed)
        {
            var pixels = new Color32[width * height];
            var random = new System.Random(seed);
            var craters = new List<Vector3>(); // u, v, raio (em v)
            for (var i = 0; i < 260; i++)
            {
                var size = Mathf.Lerp(0.008f, 0.07f, Mathf.Pow((float)random.NextDouble(), 2.6f));
                craters.Add(new Vector3((float)random.NextDouble(), 0.1f + (float)random.NextDouble() * 0.8f, size));
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = x / (float)width;
                    var v = y / (float)height;
                    var terrain = Fractal(u, v, 6, 5, seed);
                    var mare = Smooth(0.52f, 0.62f, Fractal(u, v, 3, 3, seed + 977));
                    var shade = Mathf.Lerp(0.58f, 0.34f, mare) + (terrain - 0.5f) * 0.28f;
                    foreach (var crater in craters)
                    {
                        var du = Mathf.Abs(u - crater.x);
                        du = Mathf.Min(du, 1f - du) * 2f; // a imagem é 2:1: u vale o dobro de v em ângulo
                        var d = Mathf.Sqrt(du * du + (v - crater.y) * (v - crater.y)) / crater.z;
                        if (d > 1.25f)
                            continue;
                        if (d < 0.92f)
                            shade -= (1f - d) * 0.16f; // fundo mais escuro
                        else
                            shade += (1f - Mathf.Abs(d - 1.05f) / 0.2f) * 0.14f; // borda clara
                    }

                    var value = (byte)Mathf.RoundToInt(Mathf.Clamp01(shade) * 255f);
                    pixels[y * width + x] = new Color32(value, value, (byte)Mathf.Max(0, value - 3), 255);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "PIO_MoonSurface" };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// Marte (equiretangular): deserto ocre-alaranjado, regiões escuras de baixa frequência (como Syrtis Major), crateras
        /// suaves e calotas polares claras. Cores tiradas do aspecto geral do planeta, não de um mapa real.
        /// </summary>
        public static Texture2D MarsSurface(int width, int height, int seed)
        {
            var pixels = new Color32[width * height];
            var desert = new Color(0.74f, 0.42f, 0.24f);
            var dark = new Color(0.34f, 0.2f, 0.15f);
            var light = new Color(0.86f, 0.62f, 0.42f);
            var ice = new Color(0.93f, 0.92f, 0.9f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = x / (float)width;
                    var v = y / (float)height;
                    var region = Smooth(0.5f, 0.64f, Fractal(u, v, 3, 4, seed));
                    var detail = Fractal(u, v, 12, 4, seed + 515);
                    var color = Color.Lerp(desert, dark, region * 0.85f);
                    color = Color.Lerp(color, light, Smooth(0.55f, 0.8f, detail) * (1f - region) * 0.7f);
                    color *= 0.88f + detail * 0.24f;
                    var latitude = Mathf.Abs(v - 0.5f) * 2f; // 0 no equador, 1 nos polos
                    var cap = Smooth(0.86f, 0.93f, latitude + (Fractal(u, v, 8, 3, seed + 31) - 0.5f) * 0.12f);
                    color = Color.Lerp(color, ice, cap);
                    color.a = 1f; // as multiplicações acima também escalam o alfa
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "PIO_MarsSurface" };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// Terra (equiretangular): oceano azul, continentes verde-marrom de baixa frequência, nuvens brancas e calotas polares. Estilizada
        /// (os continentes não são os reais): serve para a comparação de tamanho com Júpiter, onde ocupa poucas dezenas de pixels.
        /// </summary>
        public static Texture2D EarthSurface(int width, int height, int seed)
        {
            var pixels = new Color32[width * height];
            var ocean = new Color(0.05f, 0.2f, 0.52f);
            var deep = new Color(0.02f, 0.1f, 0.34f);
            var green = new Color(0.22f, 0.42f, 0.18f);
            var brown = new Color(0.52f, 0.42f, 0.26f);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = x / (float)width;
                    var v = y / (float)height;
                    var land = Smooth(0.54f, 0.58f, Fractal(u, v, 3, 5, seed));
                    var color = Color.Lerp(ocean, deep, Fractal(u, v, 6, 3, seed + 7) * 0.8f);
                    var ground = Color.Lerp(green, brown, Smooth(0.45f, 0.7f, Fractal(u, v, 9, 3, seed + 19)));
                    color = Color.Lerp(color, ground, land);
                    var clouds = Smooth(0.5f, 0.72f, Fractal(u, v, 5, 5, seed + 101)) * 0.85f;
                    color = Color.Lerp(color, Color.white, clouds);
                    var latitude = Mathf.Abs(v - 0.5f) * 2f;
                    color = Color.Lerp(color, Color.white, Smooth(0.84f, 0.9f, latitude));
                    color.a = 1f;
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "PIO_EarthSurface" };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        public static void BrightStars(Mesh mesh, int count, int seed, float minDegrees, float maxDegrees)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var random = new System.Random(seed);
            for (var i = 0; i < count; i++)
            {
                var z = (float)(random.NextDouble() * 2.0 - 1.0);
                var phi = (float)(random.NextDouble() * Math.PI * 2.0);
                var r = Mathf.Sqrt(1f - z * z);
                var dir = new Vector3(r * Mathf.Cos(phi), z, r * Mathf.Sin(phi));
                var magnitude = (float)Math.Pow(random.NextDouble(), 3.0);
                var half = Mathf.Tan(Mathf.Lerp(minDegrees, maxDegrees, magnitude) * 0.5f * Mathf.Deg2Rad);
                var t1 = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.up).normalized * half;
                var t2 = Vector3.Cross(t1.normalized, dir) * half; // t1 x t2 = -dir: face para o centro
                var column = Mathf.Min(PaletteColumns - 1, (int)(random.NextDouble() * PaletteColumns));
                var row = magnitude > 0.45f ? 1 : 0;
                var uv = new Vector2((column + 0.5f) / PaletteColumns, (row + 0.5f) / PaletteRows);
                var start = vertices.Count;
                vertices.AddRange(new[] { dir - t1 - t2, dir + t1 - t2, dir - t1 + t2, dir + t1 + t2 });
                normals.AddRange(new[] { -dir, -dir, -dir, -dir });
                uvs.AddRange(new[] { uv, uv, uv, uv });
                triangles.AddRange(new[] { start, start + 1, start + 2, start + 1, start + 3, start + 2 });
            }

            Fill(mesh, "PIO_BrightStars", vertices, normals, uvs, triangles);
        }

        /// <summary>Paleta das estrelas: colunas = tons (quente, branco, azulado), linhas = brilho (fraca, forte).</summary>
        public static Texture2D StarPalette()
        {
            var tints = new[]
            {
                new Color(1f, 0.80f, 0.64f), new Color(1f, 0.92f, 0.82f), new Color(1f, 0.97f, 0.93f), new Color(1f, 1f, 1f),
                new Color(0.95f, 0.97f, 1f), new Color(0.86f, 0.91f, 1f), new Color(0.78f, 0.86f, 1f), new Color(1f, 0.97f, 0.93f),
            };
            var pixels = new Color[PaletteColumns * PaletteRows];
            for (var row = 0; row < PaletteRows; row++)
            {
                for (var column = 0; column < PaletteColumns; column++)
                    pixels[row * PaletteColumns + column] = tints[column] * (row == 0 ? 0.55f : 1f);
            }

            var texture = new Texture2D(PaletteColumns, PaletteRows, TextureFormat.RGB24, false, false);
            texture.SetPixels(Encode(pixels));
            texture.Apply(false);
            return texture;
        }

        // ---------- Texturas ----------

        /// <summary>
        /// Céu equiretangular próprio (R-PIO-06): estrelas com distribuição uniforme na esfera, brilho em lei de potência,
        /// e uma faixa difusa discreta. Largura compensada por 1/cos(latitude) para não esticar estrelas nos polos.
        /// </summary>
        public static Texture2D Starfield(int width, int height, int starCount, int seed)
        {
            var pixels = new Color[width * height];
            var background = new Color(0.010f, 0.012f, 0.020f);
            var bandNormal = new Vector3(0.35f, 0.82f, 0.45f).normalized;
            for (var y = 0; y < height; y++)
            {
                var theta = ((y + 0.5f) / height - 0.5f) * Mathf.PI;
                for (var x = 0; x < width; x++)
                {
                    var phi = (x + 0.5f) / width * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(theta), Mathf.Cos(theta) * Mathf.Sin(phi));
                    var band = Mathf.Exp(-Mathf.Pow(Vector3.Dot(dir, bandNormal) / 0.16f, 2f));
                    var cloud = Fbm(dir * 3.2f, seed, 4);
                    var glow = band * (0.35f + 0.65f * cloud) * 0.030f;
                    pixels[y * width + x] = background + new Color(glow * 0.92f, glow * 0.95f, glow * 1.08f);
                }
            }

            var random = new System.Random(seed);
            for (var i = 0; i < starCount; i++)
            {
                var z = (float)(random.NextDouble() * 2.0 - 1.0);
                var phi = (float)(random.NextDouble() * Math.PI * 2.0);
                var theta = Mathf.Asin(z);
                var u = phi / (Mathf.PI * 2f) * width;
                var v = (theta / Mathf.PI + 0.5f) * height;
                var magnitude = (float)Math.Pow(random.NextDouble(), 4.0);
                var brightness = 0.05f + 0.30f * magnitude; // só estrelas fracas: as brilhantes são a malha nítida (BrightStars)
                var temperature = (float)random.NextDouble();
                var tint = temperature < 0.15f ? new Color(1f, 0.78f, 0.62f) : temperature > 0.85f ? new Color(0.72f, 0.82f, 1f) : new Color(1f, 0.97f, 0.92f);
                var sigmaY = 0.45f + magnitude * 0.15f;
                var sigmaX = Mathf.Min(sigmaY / Mathf.Max(Mathf.Cos(theta), 0.02f), 48f);
                Splat(pixels, width, height, u, v, sigmaX, sigmaY, tint * brightness);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            texture.SetPixels(Encode(pixels));
            texture.Apply(false);
            return texture;
        }

        static Color BandColor((float latitude, Color color)[] bands, float latitude)
        {
            for (var i = 0; i < bands.Length - 1; i++)
            {
                if (latitude <= bands[i + 1].latitude)
                {
                    var t = Mathf.InverseLerp(bands[i].latitude, bands[i + 1].latitude, latitude);
                    return Color.Lerp(bands[i].color, bands[i + 1].color, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.35f) / 0.3f)));
                }
            }

            return bands[bands.Length - 1].color;
        }

        static void Splat(Color[] pixels, int width, int height, float u, float v, float sigmaX, float sigmaY, Color color)
        {
            var rx = Mathf.CeilToInt(sigmaX * 3f);
            var ry = Mathf.CeilToInt(sigmaY * 3f);
            var cx = Mathf.FloorToInt(u);
            var cy = Mathf.FloorToInt(v);
            for (var dy = -ry; dy <= ry; dy++)
            {
                var y = cy + dy;
                if (y < 0 || y >= height)
                    continue;

                for (var dx = -rx; dx <= rx; dx++)
                {
                    var x = ((cx + dx) % width + width) % width;
                    var fx = (cx + dx + 0.5f - u) / sigmaX;
                    var fy = (y + 0.5f - v) / sigmaY;
                    var w = Mathf.Exp(-0.5f * (fx * fx + fy * fy));
                    if (w < 0.004f)
                        continue;

                    pixels[y * width + x] += color * w;
                }
            }
        }

        /// <summary>As cores acima estão em espaço linear; a textura PNG é sRGB.</summary>
        static Color[] Encode(Color[] linear)
        {
            var result = new Color[linear.Length];
            for (var i = 0; i < linear.Length; i++)
            {
                var c = linear[i];
                result[i] = new Color(Mathf.LinearToGammaSpace(Mathf.Clamp01(c.r)), Mathf.LinearToGammaSpace(Mathf.Clamp01(c.g)), Mathf.LinearToGammaSpace(Mathf.Clamp01(c.b)), 1f);
            }

            return result;
        }

        static float Fbm(Vector3 p, int seed, int octaves)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var total = 0f;
            for (var i = 0; i < octaves; i++)
            {
                sum += ValueNoise(p, seed + i * 31) * amplitude;
                total += amplitude;
                p *= 2.03f;
                amplitude *= 0.5f;
            }

            return sum / total;
        }

        static float ValueNoise(Vector3 p, int seed)
        {
            var i = new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
            var f = p - i;
            f = new Vector3(f.x * f.x * (3f - 2f * f.x), f.y * f.y * (3f - 2f * f.y), f.z * f.z * (3f - 2f * f.z));
            float H(int x, int y, int z) => Hash(i.x + x, i.y + y, i.z + z, seed);
            var x00 = Mathf.Lerp(H(0, 0, 0), H(1, 0, 0), f.x);
            var x10 = Mathf.Lerp(H(0, 1, 0), H(1, 1, 0), f.x);
            var x01 = Mathf.Lerp(H(0, 0, 1), H(1, 0, 1), f.x);
            var x11 = Mathf.Lerp(H(0, 1, 1), H(1, 1, 1), f.x);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, f.y), Mathf.Lerp(x01, x11, f.y), f.z);
        }

        static float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177 + seed * 974634001);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
