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

        /// <summary>
        /// Júpiter artístico PROVISÓRIO (ocre/creme): faixas por latitude com turbulência suave e uma oval discreta.
        /// Não é imagem da missão; a textura com crédito NASA entra no polimento (PIO-016) ou fica esta, declarada.
        /// </summary>
        public static Texture2D JupiterBands(int width, int height, int seed)
        {
            var bands = new[]
            {
                (-90f, new Color(0.55f, 0.47f, 0.40f)), (-62f, new Color(0.66f, 0.56f, 0.45f)), (-48f, new Color(0.80f, 0.70f, 0.56f)),
                (-36f, new Color(0.64f, 0.50f, 0.37f)), (-26f, new Color(0.86f, 0.78f, 0.64f)), (-18f, new Color(0.62f, 0.45f, 0.32f)),
                (-8f, new Color(0.90f, 0.84f, 0.72f)), (6f, new Color(0.88f, 0.80f, 0.66f)), (14f, new Color(0.60f, 0.44f, 0.31f)),
                (22f, new Color(0.84f, 0.76f, 0.62f)), (32f, new Color(0.66f, 0.53f, 0.40f)), (44f, new Color(0.79f, 0.70f, 0.57f)),
                (60f, new Color(0.63f, 0.54f, 0.44f)), (90f, new Color(0.54f, 0.47f, 0.41f)),
            };

            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                var latitude = ((y + 0.5f) / height - 0.5f) * 180f;
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width;
                    var phi = u * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(phi), latitude / 40f, Mathf.Sin(phi));
                    var wobble = (Fbm(p * 2.5f, seed, 4) - 0.5f) * 5.5f + Mathf.Sin(phi * 9f + latitude * 0.3f) * 0.8f;
                    var color = BandColor(bands, latitude + wobble);
                    var grain = (Fbm(new Vector3(phi * 6f, latitude * 0.9f, 0f), seed + 7, 3) - 0.5f) * 0.06f;
                    color += new Color(grain, grain * 0.9f, grain * 0.75f);

                    // Oval discreta no hemisfério sul (composição artística).
                    var dLon = Mathf.DeltaAngle(u * 360f, 210f) / 11f;
                    var dLat = (latitude + 22f) / 5.5f;
                    var oval = Mathf.Exp(-(dLon * dLon + dLat * dLat));
                    color = Color.Lerp(color, new Color(0.74f, 0.46f, 0.33f), oval * 0.7f);
                    pixels[y * width + x] = color;
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGB24, true, false);
            texture.SetPixels(Encode(pixels));
            texture.Apply(true);
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
