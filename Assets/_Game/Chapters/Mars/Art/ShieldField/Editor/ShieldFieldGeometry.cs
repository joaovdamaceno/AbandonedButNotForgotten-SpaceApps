using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-015: geometria procedural e determinística do campo do escudo (metros; origem no centro do campo, +Z é o rumo
    /// de chegada do rover). Reconstrução artística apoiada nas legendas de PIA07402 e PIA07324 (MS15/MS16): escudo quebrado em
    /// um destroço principal e um fragmento lateral, invertido no impacto (face interna prateada para cima), marca do impacto
    /// com poeira vermelha à direita e, mais à direita, a Heat Shield Rock do tamanho de uma bola de basquete.
    /// Medidas sem fonte nas páginas lidas (diâmetro 2,65 m, ângulo do cone) são aproximações e estão marcadas como tal no registro.
    /// </summary>
    public static class ShieldFieldGeometry
    {
        // ---------- Disposição do campo (x direita, z à frente) ----------
        public static readonly Vector2 ShieldStation = new Vector2(-0.4f, -0.6f);
        public static readonly Vector2 ApproachStart = new Vector2(-0.4f, -5.6f);
        public static readonly Vector2 MainCenter = new Vector2(-2.4f, 4.3f);
        public static readonly Vector2 FlankCenter = new Vector2(0.75f, 3.7f);
        public static readonly Vector2 Divot = new Vector2(3.1f, 4.5f);
        public static readonly Vector2 Meteorite = new Vector2(3.6f, 1.5f);

        public const float DivotRadius = 0.95f;
        public const float DivotDepth = 0.07f;
        public const float PatchHalfSize = 36f;
        public const float NearHalfSize = 12f;

        // ---------- Escudo ----------
        public const float ShieldRadius = 1.325f;          // ≈ 2,65 m de diâmetro: aproximação, a conferir
        public const float ConeSlopeDegrees = 20f;         // calota rasa (cone de ~70° de semiabertura): aproximação
        public const float Thickness = 0.035f;
        public const float FlankSpanDegrees = 40f;
        public const float FlankInnerFraction = 0.42f;

        static readonly float Slope = Mathf.Tan(ConeSlopeDegrees * Mathf.Deg2Rad);

        public sealed class Support
        {
            public string Name;
            public Vector3 Local;
            public Vector3 World;
        }

        public sealed class PiecePose
        {
            public Vector3 Origin;
            public Quaternion Rotation;
            public readonly List<Support> Supports = new List<Support>();
            public Vector3 ToWorld(Vector3 local) => Origin + Rotation * local;
        }

        const float SupportSoftness = 0.015f;

        static PiecePose mainPose, flankPose;
        static List<Support> allSupports;

        // ---------- Terreno ----------

        static float Perlin(float x, float z) => Mathf.PerlinNoise(x + 500f, z + 500f);

        public static float RipplePhase(float x, float z)
        {
            const float angle = 0.52f;
            return (x * Mathf.Cos(angle) + z * Mathf.Sin(angle)) * (Mathf.PI * 2f / 1.3f) + 2.2f * Perlin(x * 0.25f, z * 0.25f);
        }

        public static float RippleStrength(float x, float z) => 0.55f + 0.45f * Perlin(x * 0.07f + 90f, z * 0.07f + 40f);

        public static float PerlinNoise(float x, float z) => Perlin(x, z);

        /// <summary>Terreno sem os ajustes de apoio: ondulação rasa, ripples, marca do impacto e areia acumulada.</summary>
        public static float BaseHeight(float x, float z)
        {
            var h = 0.2f * (Perlin(x * 0.045f + 7f, z * 0.045f + 3f) - 0.5f);
            h += 0.06f * (Perlin(x * 0.18f + 31f, z * 0.18f + 17f) - 0.5f);

            // Ripples de areia de baixa amplitude, deformados por ruído, com direção dominante.
            h += 0.024f * RippleStrength(x, z) * Mathf.Sin(RipplePhase(x, z));
            h += 0.008f * (Perlin(x * 1.9f, z * 1.9f) - 0.5f);

            // Marca do impacto: depressão rasa com bordo de material ejetado.
            var r = Vector2.Distance(new Vector2(x, z), Divot) / DivotRadius;
            if (r < 1.8f)
            {
                h -= DivotDepth * Mathf.Pow(Mathf.Max(0f, 1f - r * r), 2f);
                h += 0.03f * Mathf.Exp(-Mathf.Pow((r - 1.08f) * 4.5f, 2f));
            }

            // Areia levada para o lado do destroço.
            h += 0.08f * Mathf.Exp(-Vector2.SqrMagnitude(new Vector2(x, z) - (MainCenter + new Vector2(1.1f, -0.9f))) / (2f * 1.4f * 1.4f));
            return h;
        }

        public static float Height(float x, float z)
        {
            EnsureSolved();
            var h = BaseHeight(x, z);
            float weightSum = 0f, corrSum = 0f, nearest = float.MaxValue;
            for (var i = 0; i < allSupports.Count; i++)
            {
                var s = allSupports[i].World;
                var d2 = (s.x - x) * (s.x - x) + (s.z - z) * (s.z - z);
                nearest = Mathf.Min(nearest, d2);
                var w = 1f / (d2 + SupportSoftness); // suave: a malha de 25 cm não resolve picos estreitos
                weightSum += w;
                corrSum += w * (s.y - BaseHeight(s.x, s.z));
            }

            if (weightSum <= 0f)
                return h;

            var fall = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(Mathf.Sqrt(nearest) / 1.6f));
            return h + corrSum / weightSum * fall;
        }

        public static Vector3 Ground(float x, float z) => new Vector3(x, Height(x, z), z);

        public static Vector3 Normal(float x, float z)
        {
            const float d = 0.05f;
            return new Vector3(Height(x - d, z) - Height(x + d, z), d * 2f, Height(x, z - d) - Height(x, z + d)).normalized;
        }

        // ---------- Poses e apoios ----------

        static void EnsureSolved()
        {
            if (allSupports != null)
                return;

            allSupports = new List<Support>();
            mainPose = SolveMain();
            flankPose = SolveFlank();
            allSupports.AddRange(mainPose.Supports);
            allSupports.AddRange(flankPose.Supports);
        }

        public static PiecePose MainPose { get { EnsureSolved(); return mainPose; } }
        public static PiecePose FlankPose { get { EnsureSolved(); return flankPose; } }

        static float Jag(float s, float seed) => 0.13f * (Perlin(s * 6.5f + seed, seed * 0.37f) - 0.5f);
        public static float CutA(float s) => -FlankSpanDegrees * 0.5f * Mathf.Deg2Rad + Jag(s, 3f);
        public static float CutB(float s) => FlankSpanDegrees * 0.5f * Mathf.Deg2Rad + Jag(s, 11f);

        static float Rim(float phi) => ShieldRadius * (1f - 0.09f * Perlin(Mathf.Cos(phi) * 1.6f + 20f, Mathf.Sin(phi) * 1.6f + 20f));

        /// <summary>Ponto da face interna (prateada, voltada para cima quando a calota repousa com o vértice embaixo).</summary>
        public static Vector3 Inner(float s, float phi)
        {
            var r = s * Rim(phi);
            var dent = 0.05f * s * (Perlin(s * 2.4f + 4f, phi * 0.9f + 8f) - 0.5f) + 0.035f * s * s * s * (Perlin(s * 5f + 40f, phi * 2.1f) - 0.5f);
            return new Vector3(r * Mathf.Cos(phi), r * Slope + dent, r * Mathf.Sin(phi));
        }

        public static Vector3 InnerNormal(float s, float phi)
        {
            const float ds = 0.004f, dp = 0.004f;
            var sc = Mathf.Clamp(s, 0.02f, 0.995f);
            var dPds = Inner(sc + ds, phi) - Inner(sc - ds, phi);
            var dPdp = Inner(sc, phi + dp) - Inner(sc, phi - dp);
            var n = Vector3.Cross(dPdp, dPds).normalized;
            return n.y < 0f ? -n : n;
        }

        /// <summary>Ponto da face externa (ablativa, voltada para o solo): a face interna recuada pela espessura ao longo da normal.</summary>
        public static Vector3 Outer(float s, float phi) => Inner(s, phi) - Thickness * InnerNormal(s, phi);

        static PiecePose SolveMain()
        {
            var toFlank = new Vector3(FlankCenter.x - MainCenter.x, 0f, FlankCenter.y - MainCenter.y);
            var yaw = Quaternion.AngleAxis(Vector3.SignedAngle(Vector3.right, toFlank, Vector3.up), Vector3.up);
            var toStation = new Vector3(ShieldStation.x - MainCenter.x, 0f, ShieldStation.y - MainCenter.y).normalized;
            var lowDirection = Quaternion.AngleAxis(-28f, Vector3.up) * toStation;
            const float tilt = 4f;
            var rotation = Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, lowDirection)) * yaw;

            var pose = new PiecePose { Rotation = rotation };
            var phiLow = -Vector3.SignedAngle(Vector3.right, Quaternion.Inverse(yaw) * lowDirection, Vector3.up) * Mathf.Deg2Rad;
            pose.Supports.Add(new Support { Name = "Apex", Local = Outer(0.01f, phiLow) });

            // Areia acumulada em volta da borda: o aro apoia-se em pontos ao longo de todo o arco que sobrou, e o miolo
            // apoia-se em dois anéis mais internos. O vértice afunda na areia; o restante é o mesmo corpo rígido.
            const int rimSamples = 14;
            for (var k = 0; k < rimSamples; k++)
            {
                var u = (k + 0.5f) / rimSamples;
                pose.Supports.Add(new Support { Name = "Rim" + k.ToString("00"), Local = OuterAtArc(0.985f, u) });
            }

            for (var k = 0; k < 7; k++)
            {
                var u = (k + 0.5f) / 7f;
                pose.Supports.Add(new Support { Name = "Mid" + k.ToString("00"), Local = OuterAtArc(0.6f, u) });
                pose.Supports.Add(new Support { Name = "Core" + k.ToString("00"), Local = OuterAtArc(0.28f, u) });
            }

            var apex = pose.Supports[0].Local;
            var origin = new Vector3(MainCenter.x, 0f, MainCenter.y) - rotation * new Vector3(apex.x, 0f, apex.z);
            origin.y = BaseHeight(MainCenter.x, MainCenter.y) - 0.15f - (rotation * apex).y;
            pose.Origin = origin;
            foreach (var support in pose.Supports)
                support.World = pose.ToWorld(support.Local);
            return pose;
        }

        /// <summary>Ponto da face externa a uma fração u (0–1) do arco do destroço principal (o complemento do setor quebrado).</summary>
        static Vector3 OuterAtArc(float s, float u)
        {
            var start = CutB(s) + 0.2f;
            var end = CutA(s) + Mathf.PI * 2f - 0.2f;
            return Outer(s, Mathf.Lerp(start, end, u));
        }

        static PiecePose SolveFlank()
        {
            // Com a face interna para cima, a geratriz do setor repousa na horizontal (é a linha reta do cone); só então
            // entram a orientação no campo e uma pequena inclinação natural.
            var generatrix = (Inner(1f, 0f) - Inner(FlankInnerFraction, 0f)).normalized;
            var level = Quaternion.FromToRotation(generatrix, Vector3.right);
            var rotation = Quaternion.AngleAxis(78f, Vector3.up) * Quaternion.AngleAxis(5f, Vector3.right) * level;
            var pose = new PiecePose { Rotation = rotation };
            foreach (var s in new[] { FlankInnerFraction + 0.02f, 0.985f })
            {
                var tag = s < 0.5f ? "Inner" : "Outer";
                pose.Supports.Add(new Support { Name = tag + "A", Local = Outer(s, CutA(s) * 0.92f) });
                pose.Supports.Add(new Support { Name = tag + "Mid", Local = Outer(s, 0f) });
                pose.Supports.Add(new Support { Name = tag + "B", Local = Outer(s, CutB(s) * 0.92f) });
            }

            var centroid = Outer((1f + FlankInnerFraction) * 0.5f, 0f);
            var origin = new Vector3(FlankCenter.x, 0f, FlankCenter.y) - rotation * new Vector3(centroid.x, 0f, centroid.z);
            // Apoia o corpo rígido no ponto mais baixo, 1,5 cm dentro da areia.
            var lowest = float.MaxValue;
            foreach (var support in pose.Supports)
            {
                var p = origin + rotation * support.Local;
                lowest = Mathf.Min(lowest, p.y - BaseHeight(p.x, p.z));
            }

            origin.y += -lowest - 0.015f; // soma: a origem já traz a altura da rotação de nivelamento
            pose.Origin = origin;
            foreach (var support in pose.Supports)
                support.World = pose.ToWorld(support.Local);
            return pose;
        }

        // ---------- Malhas ----------

        public sealed class Draft
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public int Add(Vector3 position, Vector2 uv)
            {
                Vertices.Add(position);
                Uv.Add(uv);
                return Vertices.Count - 1;
            }

            public void Tri(int a, int b, int c)
            {
                if (Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]).sqrMagnitude < 1e-12f)
                    return;

                Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
            }

            public void Quad(int a, int b, int c, int d)
            {
                Tri(a, b, c);
                Tri(b, d, c);
            }

            /// <summary>Quadrilátero (a,b,c,d como em Quad) virado para <paramref name="outward"/>.</summary>
            public void QuadOutward(int a, int b, int c, int d, Vector3 outward)
            {
                var n = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
                if (Vector3.Dot(n, outward) >= 0f)
                    Quad(a, b, c, d);
                else
                    Quad(a, c, b, d);
            }

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

            public int TriangleCount => Triangles.Count / 3;
        }

        static Vector2 AtlasUv(Vector3 local, bool silver)
        {
            var u = 0.02f + 0.96f * (local.x / (ShieldRadius * 2f) + 0.5f);
            var v = 0.02f + 0.96f * (local.z / (ShieldRadius * 2f) + 0.5f);
            return new Vector2(silver ? 0.5f + 0.5f * u : 0.5f * u, v);
        }

        /// <summary>
        /// Casca da calota entre os cortes irregulares: <paramref name="whole"/> é o destroço principal (o complemento do
        /// setor que se soltou) e falso é o fragmento lateral (coroa parcial). Quatro faces: interna, externa, bordas.
        /// </summary>
        public static Draft Shell(bool whole, int radial, int angular)
        {
            var d = new Draft();
            var sMin = whole ? 0.01f : FlankInnerFraction;
            var top = new int[radial + 1, angular + 1];
            var bottom = new int[radial + 1, angular + 1];
            for (var i = 0; i <= radial; i++)
            {
                var s = Mathf.Lerp(sMin, 1f, (float)i / radial);
                var start = whole ? CutB(s) : CutA(s);
                var end = whole ? CutA(s) + Mathf.PI * 2f : CutB(s);
                for (var j = 0; j <= angular; j++)
                {
                    var phi = Mathf.Lerp(start, end, (float)j / angular);
                    var inner = Inner(s, phi);
                    top[i, j] = d.Add(inner, AtlasUv(inner, true));
                    var outer = Outer(s, phi);
                    bottom[i, j] = d.Add(outer, AtlasUv(outer, false));
                }
            }

            for (var i = 0; i < radial; i++)
            {
                for (var j = 0; j < angular; j++)
                {
                    d.Quad(top[i, j], top[i, j + 1], top[i + 1, j], top[i + 1, j + 1]);
                    d.Quad(bottom[i, j], bottom[i + 1, j], bottom[i, j + 1], bottom[i + 1, j + 1]);
                }
            }

            // Bordas com espessura: aro externo, cortes laterais e (no fragmento) o aro interno.
            for (var j = 0; j < angular; j++)
            {
                SideStrip(d, top[radial, j], top[radial, j + 1], bottom[radial, j], bottom[radial, j + 1], true);
                if (!whole)
                    SideStrip(d, top[0, j], top[0, j + 1], bottom[0, j], bottom[0, j + 1], false);
            }

            for (var i = 0; i < radial; i++)
            {
                SideStripRadial(d, top[i, 0], top[i + 1, 0], bottom[i, 0], bottom[i + 1, 0], -1);
                SideStripRadial(d, top[i, angular], top[i + 1, angular], bottom[i, angular], bottom[i + 1, angular], 1);
            }

            // Nervuras radiais na face interna: o escudo é uma carcaça com reforços, não uma placa lisa.
            var ribCount = whole ? 11 : 2;
            for (var k = 0; k < ribCount; k++)
            {
                var u = whole ? (k + 0.5f) / ribCount : (k + 1f) / (ribCount + 1f);
                AddRib(d, whole, sMin, u);
            }

            return d;
        }

        static void SideStrip(Draft d, int t0, int t1, int b0, int b1, bool outwardRadial)
        {
            var mid = (d.Vertices[t0] + d.Vertices[t1]) * 0.5f;
            var radial = new Vector3(mid.x, 0f, mid.z).normalized;
            d.QuadOutward(t0, t1, b0, b1, outwardRadial ? radial : -radial);
        }

        // sign -1: corte no início do arco (a normal aponta para φ decrescente); +1: no fim (φ crescente).
        static void SideStripRadial(Draft d, int t0, int t1, int b0, int b1, int sign)
        {
            var mid = (d.Vertices[t0] + d.Vertices[t1]) * 0.5f;
            var tangent = new Vector3(-mid.z, 0f, mid.x).normalized;
            d.QuadOutward(t0, t1, b0, b1, tangent * sign);
        }

        static void AddRib(Draft d, bool whole, float sMin, float u)
        {
            const int steps = 10;
            var prevL = -1; var prevR = -1; var prevTL = -1; var prevTR = -1;
            for (var i = 0; i <= steps; i++)
            {
                var s = Mathf.Lerp(Mathf.Max(sMin, 0.06f), 0.97f, (float)i / steps);
                var phi = Mathf.Lerp(whole ? CutB(s) : CutA(s), whole ? CutA(s) + Mathf.PI * 2f : CutB(s), u);
                var c = Inner(s, phi);
                var n = InnerNormal(s, phi);
                var side = Vector3.Cross(n, new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi))).normalized * 0.012f;
                var l = d.Add(c - side, AtlasUv(c, true));
                var r = d.Add(c + side, AtlasUv(c, true));
                var tl = d.Add(c - side * 0.7f + n * 0.022f, AtlasUv(c, true));
                var tr = d.Add(c + side * 0.7f + n * 0.022f, AtlasUv(c, true));
                if (prevL >= 0)
                {
                    d.QuadOutward(prevL, l, prevTL, tl, -side);
                    d.QuadOutward(prevR, r, prevTR, tr, side);
                    d.QuadOutward(prevTL, tl, prevTR, tr, n);
                }

                prevL = l; prevR = r; prevTL = tl; prevTR = tr;
            }
        }

        // ---------- Meteorito e pedras ----------

        public static void Icosphere(int subdivisions, out List<Vector3> vertices, out List<int> triangles)
        {
            var t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            vertices = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (var i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (var level = 0; level < subdivisions; level++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>();
                for (var i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Mid(vertices, cache, a, b), bc = Mid(vertices, cache, b, c), ca = Mid(vertices, cache, c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }

                triangles = next;
            }
        }

        static int Mid(List<Vector3> vertices, Dictionary<long, int> cache, int a, int b)
        {
            var key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
            if (cache.TryGetValue(key, out var found))
                return found;

            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            cache[key] = vertices.Count - 1;
            return vertices.Count - 1;
        }

        public const float MeteoriteRadiusX = 0.135f, MeteoriteRadiusY = 0.105f, MeteoriteRadiusZ = 0.12f;

        /// <summary>
        /// Meteorito de ferro e níquel: elipsoide do tamanho de uma bola de basquete, com marcas em forma de impressão
        /// digital (regmaglipts) e base achatada. Pivô no ponto de apoio. NÃO é a rocha McKittrick nem um fragmento do escudo.
        /// </summary>
        public static Draft MeteoriteMesh(int subdivisions)
        {
            Icosphere(subdivisions, out var directions, out var triangles);
            var rng = new System.Random(2005);
            var pits = new List<(Vector3 dir, float radius, float depth)>();
            for (var i = 0; i < 26; i++)
            {
                var dir = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
                pits.Add((dir, Mathf.Lerp(0.26f, 0.5f, (float)rng.NextDouble()), Mathf.Lerp(0.14f, 0.26f, (float)rng.NextDouble())));
            }

            var d = new Draft();
            var minY = float.MaxValue;
            var positions = new List<Vector3>();
            foreach (var dir in directions)
            {
                var r = 1f + 0.16f * (Perlin(dir.x * 1.7f + 5f, dir.y * 1.7f + dir.z * 1.3f) - 0.5f);
                foreach (var (pd, pr, depth) in pits)
                {
                    var angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, pd), -1f, 1f));
                    if (angle < pr)
                        r -= depth * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - Mathf.Pow(angle / pr, 2f)) * 1.25f)); // fundo raso e borda marcada, sem pontas
                }

                var p = new Vector3(dir.x * r * MeteoriteRadiusX, dir.y * r * MeteoriteRadiusY, dir.z * r * MeteoriteRadiusZ);
                p.y = Mathf.Max(p.y, -MeteoriteRadiusY * 0.78f); // base achatada onde repousa
                positions.Add(p);
                minY = Mathf.Min(minY, p.y);
            }

            for (var i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                p.y -= minY;
                d.Add(p, new Vector2(0.5f + directions[i].x * 0.5f, 0.5f + directions[i].z * 0.5f));
            }

            for (var i = 0; i < triangles.Count; i += 3)
                d.Tri(triangles[i], triangles[i + 1], triangles[i + 2]);

            return d;
        }

        /// <summary>Pedra de cenário baixa e facetada (≤ 320 triângulos). Variantes por semente; pivô na base.</summary>
        public static Draft FieldRock(int seed)
        {
            Icosphere(2, out var directions, out var triangles);
            var d = new Draft();
            var minY = float.MaxValue;
            var positions = new List<Vector3>();
            foreach (var dir in directions)
            {
                var r = 1f + 0.38f * (Perlin(dir.x * 2.1f + seed * 3.1f, dir.y * 2.3f + dir.z * 1.7f + seed) - 0.5f);
                var p = new Vector3(dir.x * r * 0.5f, dir.y * r * 0.34f, dir.z * r * 0.42f);
                p.y = Mathf.Max(p.y, -0.14f);
                positions.Add(p);
                minY = Mathf.Min(minY, p.y);
            }

            for (var i = 0; i < positions.Count; i++)
                d.Add(positions[i] - new Vector3(0f, minY, 0f), new Vector2(0.5f + directions[i].x * 0.5f, 0.5f + directions[i].z * 0.5f));
            for (var i = 0; i < triangles.Count; i += 3)
                d.Tri(triangles[i], triangles[i + 1], triangles[i + 2]);
            return d;
        }

        // ---------- Terreno ----------

        /// <summary>
        /// Trecho do terreno. As bordas de um trecho fino adjacente a um grosseiro seguem a interpolação linear do grosseiro
        /// (sem frestas). Coordenadas planas do campo; UV planar sobre os 72 m.
        /// </summary>
        public static Draft Terrain(float minX, float minZ, float size, int cells, float coarseStep)
        {
            var d = new Draft();
            var index = new int[cells + 1, cells + 1];
            for (var zi = 0; zi <= cells; zi++)
            {
                for (var xi = 0; xi <= cells; xi++)
                {
                    var x = minX + xi * size / cells;
                    var z = minZ + zi * size / cells;
                    var y = Height(x, z);
                    if (coarseStep > 0f)
                    {
                        var onX = xi == 0 || xi == cells;
                        var onZ = zi == 0 || zi == cells;
                        if (onX && !onZ)
                            y = EdgeLerp(z, coarseStep, t => Height(x, t));
                        else if (onZ && !onX)
                            y = EdgeLerp(x, coarseStep, t => Height(t, z));
                    }

                    index[xi, zi] = d.Add(new Vector3(x, y, z), new Vector2((x + PatchHalfSize) / (PatchHalfSize * 2f), (z + PatchHalfSize) / (PatchHalfSize * 2f)));
                }
            }

            for (var zi = 0; zi < cells; zi++)
                for (var xi = 0; xi < cells; xi++)
                    d.Quad(index[xi, zi], index[xi, zi + 1], index[xi + 1, zi], index[xi + 1, zi + 1]);

            return d;
        }

        static float EdgeLerp(float t, float step, Func<float, float> height)
        {
            var a = Mathf.Floor(t / step) * step;
            var b = a + step;
            return Mathf.Lerp(height(a), height(b), (t - a) / step);
        }
    }
}
