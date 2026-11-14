using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-016: geometria procedural e determinística dos dois palcos de Marte tardio (metros). Reconstrução artística apoiada
    /// nas fontes oficiais lidas em 14/11/2026: Victoria é uma cratera de ~800 m de diâmetro com a borda recortada em
    /// promontórios de penhasco (capes) que alternam com baías suaves (bays); Perseverance Valley fica num entalhe amplo da crista da
    /// borda de Endeavour e desce cerca de 15–17° (JPL, 15/5/2017). Canais rasos, pedras e proporções finas são aproximações.
    /// </summary>
    public static class LateMarsGeometry
    {
        // ---------- Parâmetros ----------
        public const float VictoriaRadius = 400f;
        public const float RimDistanceFromRover = 14f;

        /// <summary>
        /// Centro da cratera: calculado para que o rover (na origem) fique a poucos metros da borda, num PROMONTÓRIO de penhasco
        /// (cape), de onde se vê a cratera. O raio da borda só depende do ângulo, então a distância cabe em forma fechada.
        /// </summary>
        public static readonly Vector2 VictoriaCenter = ComputeVictoriaCenter();

        static Vector2 ComputeVictoriaCenter()
        {
            for (var degrees = 80f; degrees <= 100f; degrees += 0.5f)
            {
                var direction = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                var back = Mathf.Atan2(-direction.y, -direction.x); // da cratera para o rover
                if (Scallop(back) < -0.35f)
                    return direction * (VictoriaRim(back) + RimDistanceFromRover);
            }

            var fallback = new Vector2(0.2f, 1f).normalized;
            return fallback * (VictoriaRim(Mathf.Atan2(-fallback.y, -fallback.x)) + RimDistanceFromRover);
        }
        public const float VictoriaDepth = 70f;
        public const float ValleySlopeDegrees = 16f;
        public const float ValleySlopeStart = 0f;
        public const float ValleySlopeLength = 190f;
        public const float StageMaxRadius = 1100f;

        static float Perlin(float x, float z) => Mathf.PerlinNoise(x + 700f, z + 700f);
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // ---------- Victoria ----------

        /// <summary>Raio da borda de Victoria em função do ângulo: promontórios (s&lt;0) e baías (s&gt;0) alternados.</summary>
        public static float VictoriaRim(float theta)
        {
            var s = Scallop(theta);
            return VictoriaRadius * (1f + 0.055f * s);
        }

        /// <summary>-1…1: negativo = promontório de penhasco (cape), positivo = baía suave (bay).</summary>
        public static float Scallop(float theta) =>
            Mathf.Clamp(0.65f * Mathf.Sin(theta * 8f + 0.4f) + 0.35f * Mathf.Sin(theta * 17f + 1.7f) + 0.5f * (Perlin(theta * 2f, 3f) - 0.5f), -1f, 1f);

        public static float VictoriaHeight(float x, float z)
        {
            var h = Plain(x, z);
            var dx = x - VictoriaCenter.x;
            var dz = z - VictoriaCenter.y;
            var r = Mathf.Sqrt(dx * dx + dz * dz);
            var theta = Mathf.Atan2(dz, dx);
            var rim = VictoriaRim(theta);
            var bay = Smooth((Scallop(theta) + 0.2f) / 0.8f); // 0 = penhasco, 1 = baía
            var width = Mathf.Lerp(8f, 240f, bay);              // penhasco cai em ~8 m; a baía desce suave por ~90 m
            var inside = Smooth((rim - r) / width);
            h -= VictoriaDepth * inside;
            h += 2.5f * Mathf.Exp(-Mathf.Pow((r - rim) / 7f, 2f)) * (1f - bay * 0.6f); // lábio da borda
            return h;
        }

        // ---------- Perseverance Valley ----------

        static float SoftPlus(float z, float k)
        {
            var t = z / k;
            if (t > 30f)
                return z; // evita estouro de Exp sem quebrar a rampa: acima disto softplus == identidade
            return k * Mathf.Log(1f + Mathf.Exp(Mathf.Max(t, -30f)));
        }

        public static readonly float[] ChannelOffsets = { -9f, -4.2f, 0.8f, 5.4f, 10.6f };

        public static float ChannelCenter(int index, float z) =>
            ChannelOffsets[index] + 2.2f * Mathf.Sin(z * 0.045f + index * 1.7f) + 1.6f * (Perlin(z * 0.02f, index * 7f) - 0.5f);

        public static float ValleyHeight(float x, float z)
        {
            var slope = Mathf.Tan(ValleySlopeDegrees * Mathf.Deg2Rad);
            var h = Plain(x, z) * 0.7f;
            // Crista suave que vira declive de ~16° e termina no piso da cratera.
            h -= slope * (SoftPlus(z - ValleySlopeStart, 4f) - SoftPlus(z - (ValleySlopeStart + ValleySlopeLength), 12f));

            // Entalhe amplo na crista: as paredes laterais da borda sobem para os lados e somem para baixo.
            var wall = Smooth((Mathf.Abs(x) - 32f) / 90f) * 16f;
            h += wall * (1f - Smooth((z - 130f) / 120f)) * (0.85f + 0.3f * Perlin(x * 0.02f, z * 0.02f));

            // Canais rasos que descem o declive (profundidade ~0,2 m, largura ~1,8 m).
            var down = Smooth((z - 1f) / 10f);
            for (var i = 0; i < ChannelOffsets.Length; i++)
            {
                var d = (x - ChannelCenter(i, z)) / 0.95f;
                h -= 0.2f * Mathf.Exp(-d * d) * down;
            }

            // Borda oposta de Endeavour, longe e baixa, para o horizonte.
            h += 70f * Smooth((z - 520f) / 380f) * (0.8f + 0.4f * Perlin(x * 0.004f, 5f));
            return h;
        }

        // ---------- Chão comum ----------

        static float Plain(float x, float z)
        {
            var h = 0.25f * (Perlin(x * 0.012f, z * 0.012f) - 0.5f);
            h += 0.07f * (Perlin(x * 0.1f + 9f, z * 0.1f + 4f) - 0.5f);
            h += 0.012f * (Perlin(x * 1.6f, z * 1.6f) - 0.5f);
            return h;
        }

        public static Vector3 Ground(Func<float, float, float> height, float x, float z) => new Vector3(x, height(x, z), z);

        public static Vector3 Normal(Func<float, float, float> height, float x, float z)
        {
            const float d = 0.08f;
            return new Vector3(height(x - d, z) - height(x + d, z), d * 2f, height(x, z - d) - height(x, z + d)).normalized;
        }

        // ---------- Malhas ----------

        public sealed class Draft
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public int Add(Vector3 p, Vector2 uv)
            {
                Vertices.Add(p);
                Uv.Add(uv);
                return Vertices.Count - 1;
            }

            public void Tri(int a, int b, int c)
            {
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
            }

            public int TriangleCount => Triangles.Count / 3;

            public void Write(Mesh mesh)
            {
                mesh.Clear();
                mesh.indexFormat = Vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                mesh.SetVertices(Vertices);
                mesh.SetUVs(0, Uv);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
            }
        }

        /// <summary>Raios dos anéis: passo fino (0,4 m) até 16 m e depois crescendo 6% por anel, até <paramref name="maxRadius"/>.</summary>
        public static List<float> RingRadii(float maxRadius, float fineStep, float fineUntil, float growth)
        {
            var radii = new List<float>();
            for (var r = fineStep; r <= fineUntil + 1e-4f; r += fineStep)
                radii.Add(r);

            var current = fineUntil;
            while (current < maxRadius)
            {
                current = Mathf.Min(maxRadius, current * (1f + growth));
                radii.Add(current);
            }

            return radii;
        }

        /// <summary>Malha polar centrada em (0,0): um vértice central e anéis com o mesmo número de segmentos. UV planar de 10 m.</summary>
        public static Draft Polar(Func<float, float, float> height, List<float> radii, int segments)
        {
            var d = new Draft();
            d.Add(new Vector3(0f, height(0f, 0f), 0f), new Vector2(0f, 0f));
            var first = new int[radii.Count, segments];
            for (var ring = 0; ring < radii.Count; ring++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var angle = s * Mathf.PI * 2f / segments;
                    var x = Mathf.Cos(angle) * radii[ring];
                    var z = Mathf.Sin(angle) * radii[ring];
                    first[ring, s] = d.Add(new Vector3(x, height(x, z), z), new Vector2(x * 0.1f, z * 0.1f));
                }
            }

            for (var s = 0; s < segments; s++)
            {
                var n = (s + 1) % segments;
                d.Tri(0, first[0, n], first[0, s]);
            }

            for (var ring = 0; ring < radii.Count - 1; ring++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var n = (s + 1) % segments;
                    d.Tri(first[ring, s], first[ring, n], first[ring + 1, s]);
                    d.Tri(first[ring, n], first[ring + 1, n], first[ring + 1, s]);
                }
            }

            return d;
        }

        /// <summary>Faixa de rocha clara ao longo da borda de Victoria (leito rochoso que aparece nas fotos), colada ao terreno.</summary>
        public static Draft VictoriaRimBand(int segments)
        {
            var d = new Draft();
            var inner = new int[segments + 1];
            var outer = new int[segments + 1];
            for (var s = 0; s <= segments; s++)
            {
                var theta = s * Mathf.PI * 2f / segments;
                var rim = VictoriaRim(theta);
                var cos = Mathf.Cos(theta);
                var sin = Mathf.Sin(theta);
                var pi = new Vector3(VictoriaCenter.x + cos * (rim - 1f), 0f, VictoriaCenter.y + sin * (rim - 1f));
                var po = new Vector3(VictoriaCenter.x + cos * (rim + 18f), 0f, VictoriaCenter.y + sin * (rim + 18f));
                pi.y = VictoriaHeight(pi.x, pi.z) + 0.15f;
                po.y = VictoriaHeight(po.x, po.z) + 0.1f;
                inner[s] = d.Add(pi, new Vector2(s * 0.5f, 0f));
                outer[s] = d.Add(po, new Vector2(s * 0.5f, 1f));
            }

            for (var s = 0; s < segments; s++)
            {
                d.Tri(inner[s], outer[s], inner[s + 1]);
                d.Tri(inner[s + 1], outer[s], outer[s + 1]);
            }

            return d;
        }

        /// <summary>Disco solar estilizado (quad voltado para o ponto de vista).</summary>
        public static Draft Quad(float size)
        {
            var d = new Draft();
            var h = size * 0.5f;
            var a = d.Add(new Vector3(-h, -h, 0f), new Vector2(0f, 0f));
            var b = d.Add(new Vector3(-h, h, 0f), new Vector2(0f, 1f));
            var c = d.Add(new Vector3(h, -h, 0f), new Vector2(1f, 0f));
            var e = d.Add(new Vector3(h, h, 0f), new Vector2(1f, 1f));
            d.Tri(a, b, c);
            d.Tri(b, e, c);
            return d;
        }

        /// <summary>Textura tileável: blenda quatro amostras de ruído, de modo que as bordas casam.</summary>
        public static float TileNoise(float u, float v, float scale)
        {
            var x = u * scale;
            var y = v * scale;
            var a = Perlin(x, y);
            var b = Perlin(x - scale, y);
            var c = Perlin(x, y - scale);
            var e = Perlin(x - scale, y - scale);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, e, u), v);
        }
    }
}
