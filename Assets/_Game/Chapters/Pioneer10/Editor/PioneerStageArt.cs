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

        /// <summary>
        /// Luas galileanas (equiretangular), estilizadas: <paramref name="kind"/> 0 Ganimedes (cinza-marrom com manchas escuras e crateras
        /// de borda clara), 1 Calisto (escura e muito craterizada), 2 Europa (clara, creme, com fraturas marrom-avermelhadas).
        /// Cores do aspecto geral de cada lua, não de mapas reais.
        /// </summary>
        public static Texture2D GalileanMoonSurface(int width, int height, int seed, int kind)
        {
            var pixels = new Color32[width * height];
            Color baseColor, darkColor;
            int craterCount;
            switch (kind)
            {
                case 1:
                    baseColor = new Color(0.36f, 0.31f, 0.27f);
                    darkColor = new Color(0.2f, 0.18f, 0.16f);
                    craterCount = 420;
                    break;
                case 2:
                    baseColor = new Color(0.84f, 0.81f, 0.74f);
                    darkColor = new Color(0.62f, 0.5f, 0.4f);
                    craterCount = 24;
                    break;
                default:
                    baseColor = new Color(0.55f, 0.5f, 0.44f);
                    darkColor = new Color(0.32f, 0.29f, 0.26f);
                    craterCount = 130;
                    break;
            }

            var random = new System.Random(seed);
            var craters = new List<Vector3>();
            for (var i = 0; i < craterCount; i++)
                craters.Add(new Vector3((float)random.NextDouble(), 0.08f + (float)random.NextDouble() * 0.84f, Mathf.Lerp(0.006f, kind == 2 ? 0.05f : 0.032f, Mathf.Pow((float)random.NextDouble(), 2.4f))));

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = x / (float)width;
                    var v = y / (float)height;
                    var patch = Smooth(0.5f, 0.64f, Fractal(u, v, 4, 4, seed));
                    var color = Color.Lerp(baseColor, darkColor, patch * (kind == 2 ? 0.25f : 0.85f));
                    color *= 0.9f + Fractal(u, v, 14, 4, seed + 37) * 0.2f;
                    if (kind == 2)
                    {
                        // fraturas: cristas finas onde o ruído cruza 0,5
                        var ridge = Mathf.Abs(Fractal(u, v, 5, 4, seed + 211) - 0.5f);
                        color = Color.Lerp(new Color(0.55f, 0.33f, 0.22f), color, Smooth(0.0f, 0.035f, ridge));
                    }

                    var shade = 0f;
                    foreach (var crater in craters)
                    {
                        var du = Mathf.Abs(u - crater.x);
                        du = Mathf.Min(du, 1f - du) * 2f;
                        var d = Mathf.Sqrt(du * du + (v - crater.y) * (v - crater.y)) / crater.z;
                        if (d > 1.25f)
                            continue;
                        shade += d < 0.92f ? -(1f - d) * 0.09f : (1f - Mathf.Abs(d - 1.05f) / 0.2f) * 0.07f;
                    }

                    color = new Color(color.r + shade, color.g + shade, color.b + shade, 1f);
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "PIO_GalileanMoon" + kind };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>Ruído branco tileável em tons de cinza, com alfa variável (estática do medidor de radiação).</summary>
        public static Texture2D StaticNoise(int size, int seed)
        {
            var random = new System.Random(seed);
            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                var gray = (byte)random.Next(120, 256);
                pixels[i] = new Color32(gray, gray, gray, (byte)random.Next(0, 200));
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "PIO_StaticNoise" };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// Faixa plana (face para cima) seguindo <paramref name="points"/>, de largura <paramref name="width"/>, deitada no plano
        /// horizontal (a trajetória vista de cima). Os vértices ficam em <paramref name="up"/> sobre o plano da linha.
        /// </summary>
        public static void Ribbon(Mesh mesh, IReadOnlyList<Vector3> points, float width)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var i = 0; i < points.Count; i++)
            {
                var previous = points[Mathf.Max(0, i - 1)];
                var next = points[Mathf.Min(points.Count - 1, i + 1)];
                var direction = (next - previous).normalized;
                var side = Vector3.Cross(direction, Vector3.up).normalized * (width * 0.5f);
                vertices.Add(points[i] - side);
                vertices.Add(points[i] + side);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                var along = i / (float)(points.Count - 1);
                uvs.Add(new Vector2(0f, along));
                uvs.Add(new Vector2(1f, along));
            }

            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); // face voltada para cima (a câmera olha de cima)
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Quadrado unitário voltado para -Z (a câmera que olha para +Z o vê de frente), para halos e brilhos.</summary>
        public static void GlowQuad(Mesh mesh)
        {
            Fill(mesh, "PIO_GlowQuad",
                new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) },
                new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back },
                new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                new List<int> { 0, 2, 1, 1, 2, 3 });
        }

        /// <summary>Brilho radial (branco, alfa em queda suave do centro à borda) para halos aditivos de estrelas.</summary>
        public static Texture2D GlowTexture(int size)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    var falloff = Mathf.Clamp01(1f - r);
                    var alpha = Mathf.Pow(falloff, 2.6f) + 0.55f * Mathf.Pow(falloff, 9f); // halo largo e um miolo mais intenso
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "PIO_StarGlow" };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// Centros das pedras do cinturão: poucas, em volume ao redor da sonda (na origem), todas a pelo menos <paramref name="minRadius"/> dela, a no
        /// mínimo <paramref name="minSeparation"/> umas das outras e longe do caminho da câmera (o segmento da origem até
        /// <paramref name="cameraAxis"/> × <paramref name="cameraDistance"/>, com folga de <paramref name="cameraClearance"/>). Determinístico.
        /// </summary>
        public static List<Vector3> SparseRockCenters(int count, int seed, float minRadius, float maxRadius, float minSeparation, Vector3 cameraAxis, float cameraDistance, float cameraClearance)
        {
            var random = new System.Random(seed);
            var centers = new List<Vector3>();
            var axis = cameraAxis.normalized;
            var guard = 0;
            while (centers.Count < count && guard++ < 200000)
            {
                var z = (float)(random.NextDouble() * 2.0 - 1.0);
                var phi = (float)(random.NextDouble() * Math.PI * 2.0);
                var r0 = Mathf.Sqrt(1f - z * z);
                var direction = new Vector3(r0 * Mathf.Cos(phi), z, r0 * Mathf.Sin(phi));
                var cubic = Mathf.Lerp(minRadius * minRadius * minRadius, maxRadius * maxRadius * maxRadius, (float)random.NextDouble());
                var point = direction * Mathf.Pow(cubic, 1f / 3f); // volume uniforme: sem amontoar perto da sonda
                var along = Mathf.Clamp(Vector3.Dot(point, axis), 0f, cameraDistance);
                if (Vector3.Distance(point, axis * along) < cameraClearance)
                    continue;
                var apart = true;
                foreach (var other in centers)
                {
                    if (Vector3.Distance(point, other) < minSeparation)
                    {
                        apart = false;
                        break;
                    }
                }

                if (apart)
                    centers.Add(point);
            }

            return centers;
        }

        /// <summary>O cinturão como ele é: pedras pequenas e bem separadas (uma malha só), com muito espaço vazio entre elas.</summary>
        /// <summary>
        /// Tamanho de uma pedra: distribuição logarítmica enviesada para as pequenas, com poucas grandes (o cinturão de verdade tem
        /// muito mais pedras pequenas que grandes). <paramref name="random"/> é um número uniforme de 0 a 1.
        /// </summary>
        public static float RockSize(float minSize, float maxSize, float random) =>
            minSize * Mathf.Pow(maxSize / minSize, Mathf.Pow(Mathf.Clamp01(random), 1.45f));

        public static void SparseRockField(Mesh mesh, IReadOnlyList<Vector3> centers, int seed, float minSize, float maxSize)
        {
            var random = new System.Random(seed + 17);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < centers.Count; i++)
                AppendRock(vertices, normals, triangles, centers[i], RockSize(minSize, maxSize, (float)random.NextDouble()), seed * 7919 + i);

            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// A placa dourada inteira numa textura (quadro de 300 × 450 da <see cref="PioneerPlaqueArt"/>, 2:3): alumínio anodizado dourado com a
        /// moldura e as cinco peças gravadas em bronze escuro, cada uma num quadrado centrado no seu contorno. Determinística. As figuras
        /// humanas são as silhuetas lisas da peça "Pessoas" (sem detalhes anatômicos).
        /// </summary>
        public static Texture2D PlaqueBoard(int width, int height)
        {
            var gold = new Color32(227, 181, 95, 255);
            var goldEdge = new Color32(206, 160, 78, 255);
            var ink = new Color32(76, 50, 14, 255);
            var shapes = new List<PioneerPlaqueArt.Shape>[PioneerPlaqueArt.SlotRects.Length];
            for (var i = 0; i < shapes.Length; i++)
                shapes[i] = PioneerPlaqueArt.ShapesOf(i);

            var pixels = new Color32[width * height];
            const int samples = 2;
            for (var py = 0; py < height; py++)
            {
                for (var px = 0; px < width; px++)
                {
                    var covered = 0;
                    var frame = 0;
                    for (var sy = 0; sy < samples; sy++)
                    {
                        for (var sx = 0; sx < samples; sx++)
                        {
                            var bx = (px + (sx + 0.5f) / samples) / width * PioneerPlaqueArt.BoardWidth;
                            var byFromTop = (1f - (py + (sy + 0.5f) / samples) / height) * PioneerPlaqueArt.BoardHeight;
                            var edge = Mathf.Min(Mathf.Min(bx, PioneerPlaqueArt.BoardWidth - bx), Mathf.Min(byFromTop, PioneerPlaqueArt.BoardHeight - byFromTop));
                            if (edge > 3.2f && edge < 5.2f)
                                frame++; // moldura fina
                            for (var i = 0; i < shapes.Length; i++)
                            {
                                var rect = PioneerPlaqueArt.SlotRects[i];
                                var side = Mathf.Min(rect.width, rect.height);
                                var ox = rect.x + (rect.width - side) * 0.5f;
                                var oy = rect.y + (rect.height - side) * 0.5f;
                                var lx = (bx - ox) / side * 100f;
                                var ly = (byFromTop - oy) / side * 100f;
                                if (lx < 0f || lx > 100f || ly < 0f || ly > 100f)
                                    continue;
                                var hit = false;
                                for (var k = 0; k < shapes[i].Count && !hit; k++)
                                    hit = shapes[i][k].Inside(lx, ly);
                                if (hit)
                                    covered++;
                                break;
                            }
                        }
                    }

                    var total = samples * samples;
                    var inked = Mathf.Clamp01((covered + frame) / (float)total);
                    var baseColor = Color32.Lerp(gold, goldEdge, 0.5f * Mathf.Abs(((px * 31 + py * 17) % 7) / 7f - 0.5f)); // grão sutil do anodizado
                    pixels[py * width + px] = Color32.Lerp(baseColor, ink, inked);
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "PIO_PlaqueBoard" };
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
                // disco de 8 lados (e não um quadrado): as estrelas ficam redondas mesmo a poucos pixels
                var start = vertices.Count;
                vertices.Add(dir);
                normals.Add(-dir);
                uvs.Add(uv);
                const int sides = 8;
                for (var k = 0; k < sides; k++)
                {
                    var angle = k / (float)sides * Mathf.PI * 2f;
                    vertices.Add(dir + (t1 * Mathf.Cos(angle) + t2 * Mathf.Sin(angle)) * 1.15f);
                    normals.Add(-dir);
                    uvs.Add(uv);
                    triangles.AddRange(new[] { start, start + 1 + k, start + 1 + (k + 1) % sides });
                }
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
            var background = new Color(0.0015f, 0.0015f, 0.002f); // quase preto e neutro (teste de jogo: o espaço estava azulado)
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
                    var dust = Mathf.Clamp01(Fbm(dir * 6.5f + new Vector3(11f, 3f, 7f), seed + 5, 4) * 1.9f - 0.45f); // faixas escuras de poeira
                    var core = Mathf.Clamp01(Fbm(dir * 1.7f, seed + 9, 3));
                    var glow = band * (0.30f + 0.70f * cloud) * (1f - 0.7f * dust) * 0.016f;
                    var warm = Color.Lerp(new Color(0.96f, 0.97f, 1.0f), new Color(1.08f, 0.98f, 0.9f), core); // núcleo levemente mais quente, bordas neutras
                    pixels[y * width + x] = background + new Color(glow * warm.r, glow * warm.g, glow * warm.b);
                }
            }

            var random = new System.Random(seed);
            for (var i = 0; i < starCount; i++)
            {
                var z = (float)(random.NextDouble() * 2.0 - 1.0);
                var phi = (float)(random.NextDouble() * Math.PI * 2.0);
                if (i % 100 < 45) // quase metade das estrelas se aglomera perto da faixa da Via Láctea
                {
                    var r0 = Mathf.Sqrt(1f - z * z);
                    var d0 = new Vector3(r0 * Mathf.Cos(phi), z, r0 * Mathf.Sin(phi));
                    d0 = (d0 - bandNormal * (Vector3.Dot(d0, bandNormal) * 0.82f)).normalized;
                    z = d0.y;
                    phi = Mathf.Atan2(d0.z, d0.x);
                    if (phi < 0f)
                        phi += Mathf.PI * 2f;
                }

                var theta = Mathf.Asin(z);
                var u = phi / (Mathf.PI * 2f) * width;
                var v = (theta / Mathf.PI + 0.5f) * height;
                var magnitude = (float)Math.Pow(random.NextDouble(), 4.0);
                var brightness = 0.06f + 0.34f * magnitude; // só estrelas fracas: as brilhantes são a malha nítida (BrightStars)
                var temperature = (float)random.NextDouble();
                var tint = temperature < 0.15f ? new Color(1f, 0.78f, 0.62f) : temperature > 0.85f ? new Color(0.72f, 0.82f, 1f) : new Color(1f, 0.97f, 0.92f);
                var sigmaY = 0.34f + magnitude * 0.12f; // estrelas mais nítidas (menos borradas) com o céu esticado a 40°
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
