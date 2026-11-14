using System;
using System.Collections.Generic;
using UnityEngine;
using Draft = Abandoned.SpaceApps.Chapters.Mars.EditorTools.ShieldFieldGeometry.Draft;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>
    /// MAR-012: geometria procedural e determinística de Eagle (metros; origem no ponto de parada do pouso, +Z à frente,
    /// y = 0 na planície, como em <see cref="MerLandingChoreography.Ground"/>). A bacia usa o MESMO perfil da MAR-014
    /// (22 m de borda a borda, 2,9 m de profundidade, piso de 3 m de raio) e acrescenta ondulação em quatro escalas, ripples,
    /// afloramentos em degraus (estratos claros), uma esporão que esconde a McKittrick, a rocha McKittrick e rochas assentadas.
    /// Referências: capítulo de Eagle em Docs/Chapters/01_MARS_TWINS.md (M05–M14) e legendas NASA citadas em
    /// Docs/Production/MARS_EAGLE.md. Medidas que não vêm de fonte (alturas dos degraus, tamanho das pedras) são aproximações.
    /// </summary>
    public static class EagleGeometry
    {
        // ---------- Perfil da bacia (igual ao da MAR-014) ----------
        public const float DepthMeters = MerLandingChoreography.EagleDepthMeters;
        public const float FloorRadiusMeters = MerLandingChoreography.EagleFloorRadiusMeters;
        public const float RimRadiusMeters = MerLandingChoreography.EagleRimRadiusMeters;

        public const float NearHalfSize = 16f;
        public const float NearCellMeters = 0.25f;
        public const float RingCoarseStep = 2f;

        // ---------- Disposição (x à direita, z à frente) ----------
        public static readonly Vector2 Start = Vector2.zero;

        /// <summary>Pé do afloramento das esferas (ponto do chão onde a primeira camada encosta); +Z local entra na rocha.</summary>
        public static readonly Vector2 BlueberriesToe = new Vector2(4.25f, 5.44f);
        public static readonly Vector2 BlueberriesStation = new Vector2(2.88f, 3.96f);

        /// <summary>Centro do disco plano de trabalho da McKittrick (projeção no plano).</summary>
        public static readonly Vector2 McKittrick = new Vector2(-5.8f, 5.3f);
        public static readonly Vector2 McKittrickStation = new Vector2(-4.6f, 3.5f);

        /// <summary>Esporão de camadas que esconde a McKittrick até a curva da rota.</summary>
        public static readonly Vector2 SpurCenter = new Vector2(-1.7f, 4.6f);
        public const float SpurRadius = 1.2f;
        public const float SpurHeight = 2.6f;

        public static readonly Vector2[] RouteWaypoints =
        {
            Start, new Vector2(1.2f, 1.6f), BlueberriesStation, new Vector2(1.9f, 2.3f),
            new Vector2(0.5f, 1.3f), new Vector2(-1.4f, 1.0f), new Vector2(-3.3f, 1.6f), McKittrickStation,
        };

        public const int BlueberriesWaypoint = 2;
        public const float RouteSampleMeters = 0.5f;
        public const float CorridorWidthMeters = 3.2f;

        // ---------- McKittrick ----------
        public const float RatPatchTiltDegrees = 20f;
        public const float McKittrickWidth = 1.5f;
        public const float McKittrickBackZ = 1.0f;
        public const float McKittrickFrontZ = -0.62f;
        public const float McKittrickBuried = 0.85f;
        /// <summary>Altura do centro do disco acima do chão natural no ponto (uma pedra que se destaca, não um piso).</summary>
        public const float McKittrickPatchLift = 0.34f;

        // ---------- Esferas ----------
        public const float SphereMinRadius = 0.005f;
        public const float SphereMaxRadius = 0.008f;
        public const int LooseSphereCount = 170;
        public const int EmbeddedSphereCount = 90;

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float Perlin(float x, float z) => Mathf.PerlinNoise(x + 500f, z + 500f);
        public static float PerlinNoise(float x, float z) => Perlin(x, z);

        // ---------- Terreno ----------

        public static float Bowl(float x, float z)
        {
            var r = Mathf.Sqrt(x * x + z * z);
            return -DepthMeters * (1f - Smooth((r - FloorRadiusMeters) / (RimRadiusMeters - FloorRadiusMeters)));
        }

        public static float RipplePhase(float x, float z)
        {
            const float angle = 1.05f;
            return (x * Mathf.Cos(angle) + z * Mathf.Sin(angle)) * (Mathf.PI * 2f / 1.05f) + 1.7f * Perlin(x * 0.3f + 8f, z * 0.3f + 2f);
        }

        public static float RippleStrength(float x, float z) => 0.5f + 0.5f * Perlin(x * 0.09f + 60f, z * 0.09f + 20f);

        /// <summary>
        /// Altura do solo: bacia da MAR-014 + quatro escalas de ondulação (~22 m, ~5 m, ~1,2 m e ~0,3 m, com amplitudes
        /// de 10, 4,5, 1,6 e 0,5 cm) + ripples de ~1,05 m mais fracos nas paredes + o bordo elevado de ejecta.
        /// </summary>
        public static float Height(float x, float z)
        {
            var h = Bowl(x, z);
            h += 0.10f * (Perlin(x * 0.045f + 11f, z * 0.045f + 5f) - 0.5f);
            h += 0.045f * (Perlin(x * 0.19f + 23f, z * 0.19f + 41f) - 0.5f);
            h += 0.016f * (Perlin(x * 0.83f + 3f, z * 0.83f + 9f) - 0.5f);
            h += 0.005f * (Perlin(x * 3.1f, z * 3.1f + 17f) - 0.5f);

            var r = Mathf.Sqrt(x * x + z * z);
            var wall = Smooth((r - FloorRadiusMeters) / (RimRadiusMeters - FloorRadiusMeters));
            var rippleWeight = 1f - 0.7f * Mathf.Sin(Mathf.PI * wall);
            h += 0.018f * RippleStrength(x, z) * Mathf.Sin(RipplePhase(x, z)) * rippleWeight;

            var rim = (r - 12.2f) / 1.4f;
            h += 0.16f * Mathf.Exp(-rim * rim);
            return h;
        }

        public static float Height(Vector2 xz) => Height(xz.x, xz.y);

        public static Vector3 Ground(float x, float z) => new Vector3(x, Height(x, z), z);
        public static Vector3 Ground(Vector2 xz) => Ground(xz.x, xz.y);

        public static Vector3 Normal(float x, float z)
        {
            const float d = 0.06f;
            return new Vector3(Height(x - d, z) - Height(x + d, z), d * 2f, Height(x, z - d) - Height(x, z + d)).normalized;
        }

        // ---------- Rota ----------

        static List<Vector2> routeXz;
        static int blueberriesIndex, mcKittrickIndex;

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>Spline por todos os pontos de passagem, reamostrada por comprimento de arco a cada <see cref="RouteSampleMeters"/>.</summary>
        static void EnsureRoute()
        {
            if (routeXz != null)
                return;

            const int perSegment = 40;
            var way = new List<Vector2>(RouteWaypoints);
            var padded = new List<Vector2> { way[0] };
            padded.AddRange(way);
            padded.Add(way[way.Count - 1]);

            var dense = new List<Vector2>();
            var waypointDense = new List<int>();
            for (var i = 1; i < padded.Count - 2; i++)
            {
                waypointDense.Add(dense.Count);
                for (var k = 0; k < perSegment; k++)
                    dense.Add(CatmullRom(padded[i - 1], padded[i], padded[i + 1], padded[i + 2], k / (float)perSegment));
            }

            waypointDense.Add(dense.Count);
            dense.Add(way[way.Count - 1]);

            var cumulative = new List<float> { 0f };
            for (var i = 1; i < dense.Count; i++)
                cumulative.Add(cumulative[i - 1] + Vector2.Distance(dense[i - 1], dense[i]));

            var total = cumulative[cumulative.Count - 1];
            var count = Mathf.Max(2, Mathf.RoundToInt(total / RouteSampleMeters) + 1);
            routeXz = new List<Vector2>(count);
            var cursor = 0;
            for (var s = 0; s < count; s++)
            {
                var distance = total * s / (count - 1);
                while (cursor < dense.Count - 2 && cumulative[cursor + 1] < distance)
                    cursor++;

                var span = Mathf.Max(1e-5f, cumulative[cursor + 1] - cumulative[cursor]);
                routeXz.Add(Vector2.Lerp(dense[cursor], dense[cursor + 1], Mathf.Clamp01((distance - cumulative[cursor]) / span)));
            }

            blueberriesIndex = NearestSample(way[BlueberriesWaypoint]);
            mcKittrickIndex = routeXz.Count - 1;
        }

        static int NearestSample(Vector2 point)
        {
            var best = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < routeXz.Count; i++)
            {
                var d = Vector2.SqrMagnitude(routeXz[i] - point);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Linha central da rota no plano (já reamostrada); o último ponto é a estação da McKittrick.</summary>
        public static IReadOnlyList<Vector2> RoutePlan { get { EnsureRoute(); return routeXz; } }
        public static int BlueberriesStationIndex { get { EnsureRoute(); return blueberriesIndex; } }
        public static int McKittrickStationIndex { get { EnsureRoute(); return mcKittrickIndex; } }

        public static float DistanceToRoute(Vector2 point)
        {
            EnsureRoute();
            var best = float.MaxValue;
            for (var i = 0; i < routeXz.Count - 1; i++)
                best = Mathf.Min(best, SegmentDistance(point, routeXz[i], routeXz[i + 1]));

            return best;
        }

        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        public static float RouteLength(int from, int to)
        {
            EnsureRoute();
            var length = 0f;
            for (var i = from; i < to; i++)
                length += Vector2.Distance(routeXz[i], routeXz[i + 1]);

            return length;
        }

        // ---------- Afloramentos em degraus ----------

        /// <summary>
        /// Afloramento de estratos em escada ao longo de uma linha de pé (<see cref="Toe"/>). O eixo <see cref="Normal"/> entra
        /// na parede; cada camada recua <see cref="Tread"/> m. Perto de u = 0 (<see cref="FlatHalfWidth"/>) a frente da primeira
        /// camada é uma reta exata: é onde as esferas são assentadas na face (BlueberriesVisual).
        /// </summary>
        public sealed class TerraceSpec
        {
            public Vector2 Toe;
            public Vector2 Normal;
            public float HalfWidth = 3f;
            public int Layers = 4;
            public float Tread = 0.85f;
            public float Rise = 0.24f;
            public int Seed = 1;
            public float FlatHalfWidth = 0.7f;

            public Vector2 Tangent => new Vector2(Normal.y, -Normal.x);

            public Vector2 Point(int layer, float u)
            {
                var wobble = 0f;
                if (layer > 0)
                {
                    var window = Smooth((Mathf.Abs(u) - FlatHalfWidth) / 0.7f);
                    wobble = 0.16f * (Perlin(u * 0.9f + Seed * 7.3f, layer * 3.7f + Seed) - 0.5f) * 2f * window;
                }
                else
                {
                    var window = Smooth((Mathf.Abs(u) - FlatHalfWidth) / 0.7f);
                    wobble = 0.12f * (Perlin(u * 1.1f + Seed * 5.1f, Seed * 2.3f) - 0.5f) * 2f * window;
                }

                return Toe + Tangent * u + Normal * (layer * Tread + wobble);
            }

            /// <summary>Fração da altura dos degraus: cai a zero nas pontas para o afloramento se fundir ao chão.</summary>
            public float EndFade(float u) => 1f - Smooth((Mathf.Abs(u) - (HalfWidth - 1.3f)) / 1.3f);
        }

        public static TerraceSpec BlueberriesLedge()
        {
            var normal = (BlueberriesToe - Start).normalized;
            return new TerraceSpec { Toe = BlueberriesToe, Normal = normal, HalfWidth = 3.2f, Layers = 4, Tread = 0.85f, Rise = 0.24f, Seed = 3, FlatHalfWidth = 0.7f };
        }

        /// <summary>Afloramento atrás da McKittrick; a pedra se projeta à frente dele.</summary>
        public static TerraceSpec McKittrickLedge()
        {
            var normal = -McKittrickFaceDirection;
            return new TerraceSpec { Toe = McKittrick + normal * (McKittrickBackZ - 0.1f), Normal = normal, HalfWidth = 2.4f, Layers = 3, Tread = 0.8f, Rise = 0.22f, Seed = 9, FlatHalfWidth = 0.0f };
        }

        /// <summary>Do disco da McKittrick para a estação do rover (a face da pedra olha para lá).</summary>
        public static Vector2 McKittrickFaceDirection => (McKittrickStation - McKittrick).normalized;

        /// <summary>Altura do topo da camada <paramref name="layer"/> em u (a mesma conta usada na malha).</summary>
        public static float TerraceTop(TerraceSpec s, int layer, float u)
        {
            var front = s.Point(layer, u);
            var back = s.Point(layer + 1, u);
            var fade = s.EndFade(u);
            var own = s.Rise * (0.7f + 0.6f * Perlin(u * 0.6f + s.Seed * 3f, layer * 5.3f)) * fade;
            var top = Mathf.Max(Height(front) + own, Height(back) + 0.04f);
            if (layer > 0)
                top = Mathf.Max(top, TerraceTop(s, layer - 1, u) + 0.1f * fade);

            return top;
        }

        const float TerraceBuried = 0.35f;

        /// <summary>Malha do afloramento (frente = risers voltados para -Normal, tampos, tampas laterais e saias enterradas).</summary>
        public static Draft Terraces(TerraceSpec s, float step)
        {
            var d = new Draft();
            var segments = Mathf.Max(2, Mathf.CeilToInt(s.HalfWidth * 2f / step));
            var u = new float[segments + 1];
            for (var i = 0; i <= segments; i++)
                u[i] = -s.HalfWidth + s.HalfWidth * 2f * i / segments;

            var tangent3 = new Vector3(s.Tangent.x, 0f, s.Tangent.y);
            var intoWall = new Vector3(s.Normal.x, 0f, s.Normal.y);
            var top = new float[s.Layers, segments + 1];
            for (var k = 0; k < s.Layers; k++)
                for (var i = 0; i <= segments; i++)
                    top[k, i] = TerraceTop(s, k, u[i]);

            for (var k = 0; k < s.Layers; k++)
            {
                var risers = new int[2, segments + 1];
                var treadFront = new int[segments + 1];
                var treadBack = new int[segments + 1];
                for (var i = 0; i <= segments; i++)
                {
                    var front = s.Point(k, u[i]);
                    var back = s.Point(k + 1, u[i]);
                    var baseY = k == 0 ? Height(front) - TerraceBuried : top[k - 1, i];
                    risers[0, i] = d.Add(new Vector3(front.x, baseY, front.y), new Vector2(u[i] * 0.7f, baseY * 1.1f));
                    risers[1, i] = d.Add(new Vector3(front.x, top[k, i], front.y), new Vector2(u[i] * 0.7f, top[k, i] * 1.1f));
                    treadFront[i] = d.Add(new Vector3(front.x, top[k, i], front.y), new Vector2(u[i] * 0.7f, k * 0.37f));
                    treadBack[i] = d.Add(new Vector3(back.x, top[k, i], back.y), new Vector2(u[i] * 0.7f, k * 0.37f + s.Tread * 0.9f));
                }

                for (var i = 0; i < segments; i++)
                {
                    d.QuadOutward(risers[0, i], risers[0, i + 1], risers[1, i], risers[1, i + 1], -intoWall);
                    d.QuadOutward(treadFront[i], treadFront[i + 1], treadBack[i], treadBack[i + 1], Vector3.up);
                }

                SideCap(d, s, k, u[0], top[k, 0], -tangent3);
                SideCap(d, s, k, u[segments], top[k, segments], tangent3);
            }

            // Saia de trás, enterrada no terreno: nenhuma fresta mostra o céu por trás do afloramento.
            var last = s.Layers - 1;
            var back0 = -1;
            for (var i = 0; i <= segments; i++)
            {
                var p = s.Point(s.Layers, u[i]);
                var a = d.Add(new Vector3(p.x, top[last, i], p.y), new Vector2(u[i] * 0.7f, 0f));
                var b = d.Add(new Vector3(p.x, Height(p) - TerraceBuried, p.y), new Vector2(u[i] * 0.7f, 0.4f));
                if (back0 >= 0)
                    d.QuadOutward(back0, back0 + 1, a, b, intoWall);

                back0 = a;
            }

            return d;
        }

        static void SideCap(Draft d, TerraceSpec s, int layer, float u, float top, Vector3 outward)
        {
            var front = s.Point(layer, u);
            var back = s.Point(layer + 1, u);
            var f0 = d.Add(new Vector3(front.x, Height(front) - TerraceBuried, front.y), new Vector2(0f, 0f));
            var f1 = d.Add(new Vector3(front.x, top, front.y), new Vector2(0f, 0.4f));
            var b0 = d.Add(new Vector3(back.x, Height(back) - TerraceBuried, back.y), new Vector2(s.Tread, 0f));
            var b1 = d.Add(new Vector3(back.x, top, back.y), new Vector2(s.Tread, 0.4f));
            d.QuadOutward(f0, f1, b0, b1, outward);
        }

        // ---------- Esporão (afloramento em pilha) ----------

        /// <summary>
        /// Esporão de camadas empilhadas (bolo em anéis que encolhem), com a base enterrada abaixo do ponto mais baixo do
        /// terreno em volta. Esconde a McKittrick de quem vem das esferas e é contornado pela rota.
        /// </summary>
        public static Draft Spur(int segments, int layers = 4)
        {
            var d = new Draft();
            var lowest = float.MaxValue;
            for (var i = 0; i < 16; i++)
            {
                var a = i * Mathf.PI * 2f / 16f;
                lowest = Mathf.Min(lowest, Height(SpurCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SpurRadius));
            }

            var baseY = lowest - TerraceBuried;
            var highest = Height(SpurCenter) + SpurHeight;
            var ring = new int[layers + 1, 2, segments + 1];
            var layerTop = new float[layers];
            for (var k = 0; k < layers; k++)
                layerTop[k] = Mathf.Lerp(lowest + 0.3f, highest, (k + 1f) / layers) + 0.06f * (Perlin(k * 3.1f, 4.4f) - 0.5f);

            for (var k = 0; k < layers; k++)
            {
                var radiusFactor = 1f - 0.17f * k;
                var yBottom = k == 0 ? baseY : layerTop[k - 1];
                var yTop = layerTop[k];
                for (var i = 0; i <= segments; i++)
                {
                    var a = (i % segments) * Mathf.PI * 2f / segments;
                    var wobble = 1f + 0.12f * (Perlin(Mathf.Cos(a) * 1.6f + k * 4f, Mathf.Sin(a) * 1.6f + 9f) - 0.5f) * 2f;
                    var p = SpurCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SpurRadius * radiusFactor * wobble;
                    ring[k, 0, i] = d.Add(new Vector3(p.x, yBottom, p.y), new Vector2(i * 0.35f, yBottom * 1.1f));
                    ring[k, 1, i] = d.Add(new Vector3(p.x, yTop, p.y), new Vector2(i * 0.35f, yTop * 1.1f));
                }
            }

            for (var k = 0; k < layers; k++)
            {
                for (var i = 0; i < segments; i++)
                {
                    var outward = RingOutward(i, segments);
                    d.QuadOutward(ring[k, 0, i], ring[k, 0, i + 1], ring[k, 1, i], ring[k, 1, i + 1], outward);

                    // Tampo do degrau: anel de cima da camada k até o pé do anel k+1 (ou o centro, no último).
                    if (k < layers - 1)
                    {
                        d.QuadOutward(ring[k, 1, i], ring[k, 1, i + 1], ring[k + 1, 0, i], ring[k + 1, 0, i + 1], Vector3.up);
                    }
                }
            }

            var apex = d.Add(new Vector3(SpurCenter.x, layerTop[layers - 1] + 0.05f, SpurCenter.y), new Vector2(0.5f, 0.5f));
            for (var i = 0; i < segments; i++)
                d.Tri(ring[layers - 1, 1, i], apex, ring[layers - 1, 1, i + 1]);

            var floorCenter = d.Add(new Vector3(SpurCenter.x, baseY, SpurCenter.y), new Vector2(0.5f, 0.5f));
            for (var i = 0; i < segments; i++)
                d.Tri(ring[0, 0, i], ring[0, 0, i + 1], floorCenter);

            return d;
        }

        static Vector3 RingOutward(int i, int segments)
        {
            var a = (i + 0.5f) * Mathf.PI * 2f / segments;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        // ---------- McKittrick ----------

        /// <summary>Normal do disco de trabalho no quadro do prefab (+Z entra na rocha): inclinada para o rover.</summary>
        public static Vector3 McKittrickPatchNormal
        {
            get
            {
                var tilt = RatPatchTiltDegrees * Mathf.Deg2Rad;
                return new Vector3(0f, Mathf.Cos(tilt), -Mathf.Sin(tilt));
            }
        }

        /// <summary>
        /// Altura da superfície superior da McKittrick, no quadro do prefab (origem no centro do disco): plano inclinado de
        /// <see cref="RatPatchTiltDegrees"/> para o rover com os 30 cm centrais exatamente planos (a RatCavitySurface assenta rente)
        /// e relevo suave de até 12 mm fora deles.
        /// </summary>
        public static float McKittrickTop(float x, float z)
        {
            var plane = Mathf.Tan(RatPatchTiltDegrees * Mathf.Deg2Rad) * z;
            var distance = Mathf.Sqrt(x * x + z * z);
            var weight = Smooth((distance - 0.22f) / 0.3f);
            var relief = 0.012f * (Perlin(x * 4.1f + 70f, z * 4.1f + 12f) - 0.5f) * 2f;
            var chipped = -0.05f * Smooth((Mathf.Abs(x) - 0.55f) / 0.2f) * (1f - Smooth((z + 0.1f) / 0.5f));
            return plane + (relief + chipped) * weight;
        }

        /// <summary>
        /// Rocha McKittrick no quadro do prefab (origem no centro do disco, +Z entra na rocha, Y é a vertical do mundo):
        /// bloco de estratos com topo inclinado, frente em três degraus de camada e saia enterrada.
        /// </summary>
        public static Draft McKittrickBlock(int xCells, int zCells)
        {
            var d = new Draft();
            var half = McKittrickWidth * 0.5f;
            var bottom = -McKittrickPatchLift - McKittrickBuried;
            var top = new int[xCells + 1, zCells + 1];
            for (var zi = 0; zi <= zCells; zi++)
            {
                var z = Mathf.Lerp(McKittrickFrontZ, McKittrickBackZ, zi / (float)zCells);
                for (var xi = 0; xi <= xCells; xi++)
                {
                    var x = Mathf.Lerp(-half, half, xi / (float)xCells);
                    var rounded = 1f - 0.18f * Smooth((Mathf.Abs(x) - half * 0.7f) / (half * 0.3f));
                    top[xi, zi] = d.Add(new Vector3(x * rounded, McKittrickTop(x, z), z), new Vector2(x * 0.7f, z * 0.9f));
                }
            }

            for (var zi = 0; zi < zCells; zi++)
                for (var xi = 0; xi < xCells; xi++)
                    d.QuadOutward(top[xi, zi], top[xi + 1, zi], top[xi, zi + 1], top[xi + 1, zi + 1], Vector3.up);

            // Frente: três degraus de camada descendo até a saia enterrada.
            var levels = new[] { 0f, 0.14f, 0.28f, 0.42f };
            var front = new int[levels.Length + 1, xCells + 1];
            for (var xi = 0; xi <= xCells; xi++)
                front[0, xi] = top[xi, 0];

            var recess = new[] { 0f, 0.035f, -0.02f, 0.03f };
            for (var li = 1; li <= levels.Length; li++)
            {
                var y = li == levels.Length ? bottom : McKittrickTop(0f, McKittrickFrontZ) - levels[li];
                var z = McKittrickFrontZ + (li == levels.Length ? 0.03f : recess[li]);
                for (var xi = 0; xi <= xCells; xi++)
                {
                    var x = Mathf.Lerp(-half, half, xi / (float)xCells);
                    var rounded = 1f - 0.18f * Smooth((Mathf.Abs(x) - half * 0.7f) / (half * 0.3f));
                    front[li, xi] = d.Add(new Vector3(x * rounded, y, z), new Vector2(x * 0.7f, y * 1.1f));
                }
            }

            for (var li = 0; li < levels.Length; li++)
                for (var xi = 0; xi < xCells; xi++)
                    d.QuadOutward(front[li, xi], front[li, xi + 1], front[li + 1, xi], front[li + 1, xi + 1], new Vector3(0f, 0f, -1f));

            // Laterais e fundo: fechados e enterrados.
            var lowLeft = d.Add(new Vector3(-half * 0.82f, bottom, McKittrickFrontZ), new Vector2(0f, 0f));
            var lowRight = d.Add(new Vector3(half * 0.82f, bottom, McKittrickFrontZ), new Vector2(1f, 0f));
            var backLowLeft = d.Add(new Vector3(-half * 0.82f, bottom, McKittrickBackZ), new Vector2(0f, 1f));
            var backLowRight = d.Add(new Vector3(half * 0.82f, bottom, McKittrickBackZ), new Vector2(1f, 1f));
            d.QuadOutward(top[0, 0], top[0, zCells], lowLeft, backLowLeft, Vector3.left);
            d.QuadOutward(top[xCells, 0], top[xCells, zCells], lowRight, backLowRight, Vector3.right);
            d.QuadOutward(top[0, zCells], top[xCells, zCells], backLowLeft, backLowRight, Vector3.forward);
            return d;
        }

        // ---------- Esferas (blueberries) ----------

        /// <summary>Esfera do conjunto (icosfera), raio unitário; pivô no centro.</summary>
        static readonly Dictionary<int, KeyValuePair<List<Vector3>, List<int>>> SphereCache = new Dictionary<int, KeyValuePair<List<Vector3>, List<int>>>();

        public static void AddSphere(Draft d, Vector3 center, float radius, int subdivisions, Vector2 uv)
        {
            if (!SphereCache.TryGetValue(subdivisions, out var cached))
            {
                ShieldFieldGeometry.Icosphere(subdivisions, out var cachedDirections, out var cachedTriangles);
                cached = new KeyValuePair<List<Vector3>, List<int>>(cachedDirections, cachedTriangles);
                SphereCache[subdivisions] = cached;
            }

            var directions = cached.Key;
            var triangles = cached.Value;
            var first = d.Vertices.Count;
            foreach (var direction in directions)
                d.Add(center + direction * radius, uv);

            for (var i = 0; i < triangles.Count; i += 3)
                d.Tri(first + triangles[i], first + triangles[i + 1], first + triangles[i + 2]);
        }

        // ---------- Rochas assentadas ----------

        public sealed class RockPlacement
        {
            public Vector2 Xz;
            public float Size;
            public int Variant;
            public float YawDegrees;
            public bool Large;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        public const int RockVariants = 6;
        public const float LargeRockMinSize = 0.22f;
        const float SinkFraction = 0.06f;

        /// <summary>Pedra facetada de base plana (pivô na base). Mesma família da MAR-015, com subdivisão escolhida (LOD).</summary>
        public static Draft Rock(int seed, int subdivisions)
        {
            ShieldFieldGeometry.Icosphere(subdivisions, out var directions, out var triangles);
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

        /// <summary>Raio horizontal aproximado da base da pedra de tamanho <paramref name="size"/> (a malha é 1 × 0,84 × escala).</summary>
        public static float RockFootprint(float size) => size * 1.25f;

        /// <summary>
        /// Assenta a pedra pelo centro e pelas bordas: a base é um plano alinhado à normal média do terreno sob a pegada e fica
        /// abaixo de TODAS as amostras do chão (centro e 12 pontos da borda), mais 6% da altura enterrados. Nenhuma borda flutua.
        /// </summary>
        public static void Settle(RockPlacement rock)
        {
            var fp = RockFootprint(rock.Size);
            var center = Ground(rock.Xz);
            var samples = new List<Vector3> { center };
            var normal = Normal(rock.Xz.x, rock.Xz.y);
            for (var i = 0; i < 12; i++)
            {
                var a = i * Mathf.PI * 2f / 12f;
                var p = rock.Xz + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * fp * 0.95f;
                samples.Add(Ground(p));
                normal += Normal(p.x, p.y);
            }

            normal.Normalize();
            var height = rock.Size * 1.1f;
            var minProjection = float.MaxValue;
            foreach (var sample in samples)
                minProjection = Mathf.Min(minProjection, Vector3.Dot(sample - center, normal));

            rock.Rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, rock.YawDegrees, 0f);
            rock.Position = center + normal * (minProjection - SinkFraction * height);
        }

        static bool InLedgeFootprint(TerraceSpec s, Vector2 p, float margin)
        {
            var delta = p - s.Toe;
            var u = Vector2.Dot(delta, s.Tangent);
            var v = Vector2.Dot(delta, s.Normal);
            return Mathf.Abs(u) < s.HalfWidth + margin && v > -margin - 0.6f && v < s.Layers * s.Tread + margin;
        }

        static bool IsFree(Vector2 p, float size)
        {
            var margin = size + 0.3f;
            if (DistanceToRoute(p) < CorridorWidthMeters * 0.5f + size) return false;
            if (Vector2.Distance(p, BlueberriesToe) < 2.6f + size) return false;
            if (Vector2.Distance(p, McKittrick) < 2.6f + size) return false;
            if (Vector2.Distance(p, BlueberriesStation) < 1.8f + size) return false;
            if (Vector2.Distance(p, McKittrickStation) < 1.8f + size) return false;
            if (Vector2.Distance(p, SpurCenter) < SpurRadius + margin) return false;
            if (InLedgeFootprint(BlueberriesLedge(), p, margin)) return false;
            return !InLedgeFootprint(McKittrickLedge(), p, margin);
        }

        /// <summary>
        /// Pedras determinísticas: 22 grandes e 44 pequenas na bacia e no bordo, e 14 grandes na planície em volta. A
        /// distribuição de tamanhos segue uma lei de potência (muitas pequenas, poucas grandes), sem invadir a rota,
        /// os alvos nem os afloramentos. Cada pedra já vem assentada por <see cref="Settle"/>.
        /// </summary>
        public static List<RockPlacement> Rocks()
        {
            var rng = new System.Random(2004);
            var result = new List<RockPlacement>();
            var occupied = new List<Vector3>();
            for (var attempt = 0; attempt < 4000 && result.Count < 80; attempt++)
            {
                var far = result.Count >= 66;
                var radius = far ? Mathf.Lerp(14f, 34f, (float)rng.NextDouble()) : Mathf.Lerp(1.5f, 13.5f, Mathf.Sqrt((float)rng.NextDouble()));
                var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                var p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var large = far || result.Count < 22;
                var size = large
                    ? Mathf.Lerp(0.24f, 0.55f, Mathf.Pow((float)rng.NextDouble(), 1.6f)) * (far ? 1.3f : 1f)
                    : Mathf.Lerp(0.05f, 0.15f, (float)rng.NextDouble());

                if (!IsFree(p, size))
                    continue;

                var tooClose = false;
                foreach (var o in occupied)
                    tooClose |= Vector2.Distance(p, new Vector2(o.x, o.y)) < o.z + size + 0.12f;

                if (tooClose)
                    continue;

                occupied.Add(new Vector3(p.x, p.y, size));
                var placement = new RockPlacement
                {
                    Xz = p, Size = size, Variant = result.Count % RockVariants, YawDegrees = (float)rng.NextDouble() * 360f,
                    Large = size >= LargeRockMinSize,
                };
                Settle(placement);
                result.Add(placement);
            }

            return result;
        }

        // ---------- Terreno em malha ----------

        /// <summary>
        /// Trecho do terreno. As bordas de um trecho fino vizinho de um grosseiro seguem a interpolação linear do grosseiro
        /// (sem frestas). UV planar sobre <paramref name="uvSize"/> m a partir de (uvMinX, uvMinZ).
        /// </summary>
        public static Draft Terrain(float minX, float minZ, float size, int cells, float coarseStep, float uvMinX, float uvMinZ, float uvSize)
        {
            var d = new Draft();
            var index = new int[cells + 1, cells + 1];
            for (var zi = 0; zi <= cells; zi++)
            {
                for (var xi = 0; xi <= cells; xi++)
                {
                    var x = minX + xi * size / cells;
                    var z = minZ + zi * size / cells;
                    float y;
                    var onX = xi == 0 || xi == cells;
                    var onZ = zi == 0 || zi == cells;
                    if (coarseStep > 0f && onX && !onZ)
                        y = EdgeLerp(z, coarseStep, t => Height(x, t));
                    else if (coarseStep > 0f && onZ && !onX)
                        y = EdgeLerp(x, coarseStep, t => Height(t, z));
                    else
                        y = Height(x, z);

                    index[xi, zi] = d.Add(new Vector3(x, y, z), new Vector2((x - uvMinX) / uvSize, (z - uvMinZ) / uvSize));
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
            return Mathf.Lerp(height(a), height(a + step), (t - a) / step);
        }
    }
}
