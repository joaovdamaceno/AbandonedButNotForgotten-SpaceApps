using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Geometria do conjunto de pouso (metros), compartilhada pelo builder, pelo componente e pelos testes.
    /// Escala cinematográfica: o hardware de pouso é desenhado maior que o real para que o rover (escala 1:1, modelo da MAR-011)
    /// caiba dobrado dentro das pétalas; o rover guardado usa <see cref="StowedRoverScale"/> e cresce ao se desdobrar.
    /// Quadro do módulo: origem no baricentro do tetraedro, +Y para cima, frente do rover em +Z.
    /// </summary>
    public static class MerLandingLayout
    {
        public const float HullEdgeMeters = 3.9f;
        public const float PetalThicknessMeters = 0.12f;
        public const float PetalOpenDegrees = 105f;
        public const float LobeRadiusMeters = 1.25f;
        public const float CoreRadiusMeters = 1.6f;
        public const float StowedRoverScale = 0.42f;
        public const float RoverBaseClearanceMeters = 0f;

        // Aeroshell (quadro da cápsula: y = 0 no plano de junção; escudo abaixo, casco traseiro acima).
        public const float CapsuleRadiusMeters = 2.6f;
        public const float ShieldSagMeters = 0.55f;
        public const float BackshellHeightMeters = 4.2f;
        public const float BackshellCapRadiusMeters = 0.5f;
        public const float LanderBaseInCapsuleMeters = 0.35f;
        public const float ParachuteAnchorHeightMeters = 26f;
        public const float ParachuteRadiusMeters = 12f;

        public static readonly float InRadius = HullEdgeMeters / (2f * Mathf.Sqrt(3f));
        public static readonly float CircumRadius = 2f * InRadius;
        public static readonly float HullHeight = HullEdgeMeters * Mathf.Sqrt(2f / 3f);
        public static readonly float CentroidHeight = HullHeight * 0.25f;
        public static readonly float LanderCentroidInCapsule = LanderBaseInCapsuleMeters + CentroidHeight;

        public static Vector3 VertexA => new Vector3(-HullEdgeMeters * 0.5f, -CentroidHeight, InRadius);
        public static Vector3 VertexB => new Vector3(HullEdgeMeters * 0.5f, -CentroidHeight, InRadius);
        public static Vector3 VertexC => new Vector3(0f, -CentroidHeight, -CircumRadius);
        public static Vector3 Apex => new Vector3(0f, HullHeight - CentroidHeight, 0f);
        public static IReadOnlyList<Vector3> HullVertices => new[] { VertexA, VertexB, VertexC, Apex };

        /// <summary>Posição do rover guardado/desdobrado no quadro do módulo (sobre a pétala-base, centrado).</summary>
        public static Vector3 RoverLocalPosition => new Vector3(0f, -CentroidHeight + PetalThicknessMeters * 0.5f + RoverBaseClearanceMeters, 0f);

        public struct Petal
        {
            public string Name;
            public Vector3 HingeMidpoint;
            public Quaternion ClosedRotation;
        }

        /// <summary>Três pétalas laterais: frente (rampa, +Z) e duas traseiras. A quarta, a base, fica sob o rover.</summary>
        public static Petal[] Petals()
        {
            var a = VertexA;
            var b = VertexB;
            var c = VertexC;
            var edges = new[] { ("Front", a, b), ("BackLeft", c, a), ("BackRight", b, c) };
            var result = new Petal[3];
            for (var i = 0; i < 3; i++)
            {
                var (name, p, q) = edges[i];
                var mid = (p + q) * 0.5f;
                var outward = new Vector3(mid.x, 0f, mid.z).normalized;
                result[i] = new Petal { Name = name, HingeMidpoint = mid, ClosedRotation = Quaternion.LookRotation(outward, Vector3.up) };
            }

            return result;
        }

        /// <summary>Triângulo da pétala no quadro da dobradiça (x ao longo da aresta, y para cima, z para fora). Fechada, o ápice fica para dentro.</summary>
        public static Vector3[] PetalTriangleLocal() => new[]
        {
            new Vector3(-HullEdgeMeters * 0.5f, 0f, 0f),
            new Vector3(HullEdgeMeters * 0.5f, 0f, 0f),
            new Vector3(0f, HullHeight, -InRadius),
        };

        public struct Lobe
        {
            public Vector3 Center;
            public int Face;
        }

        /// <summary>28 lobos de airbag (7 por face: centro, 3 voltados aos vértices, 3 às arestas) mais o núcleo, no quadro do módulo.</summary>
        public static List<Lobe> Lobes()
        {
            var faces = new List<Vector3[]>
            {
                new[] { VertexA, VertexC, VertexB },      // base (normal para baixo)
                new[] { VertexA, VertexB, Apex },         // frente
                new[] { VertexC, VertexA, Apex },         // traseira esquerda
                new[] { VertexB, VertexC, Apex },         // traseira direita
            };
            var result = new List<Lobe>();
            for (var f = 0; f < faces.Count; f++)
            {
                var v = faces[f];
                var centroid = (v[0] + v[1] + v[2]) / 3f;
                var normal = Vector3.Cross(v[1] - v[0], v[2] - v[0]).normalized;
                if (Vector3.Dot(normal, centroid) < 0f)
                    normal = -normal;

                var lift = normal * 0.5f;
                result.Add(new Lobe { Center = centroid + lift, Face = f });
                for (var i = 0; i < 3; i++)
                {
                    result.Add(new Lobe { Center = centroid + (v[i] - centroid).normalized * 1.55f + lift, Face = f });
                    var mid = (v[i] + v[(i + 1) % 3]) * 0.5f;
                    result.Add(new Lobe { Center = centroid + (mid - centroid).normalized * 1.0f + lift, Face = f });
                }
            }

            return result;
        }

        /// <summary>Raio de contato do cluster inflado com o chão durante os quiques (maior extensão radial dos lobos).</summary>
        public static float TumbleContactRadius()
        {
            var max = 0f;
            foreach (var lobe in Lobes())
                max = Mathf.Max(max, lobe.Center.magnitude + LobeRadiusMeters);

            return max;
        }

        /// <summary>Altura do baricentro sobre o chão com o módulo apoiado nos lobos da base (inflado).</summary>
        public static float RestCenterHeightInflated => CentroidHeight + 0.5f + LobeRadiusMeters;

        /// <summary>Altura do baricentro com os airbags vazios e o módulo apoiado na pétala-base.</summary>
        public static float RestCenterHeightDeflated => CentroidHeight + PetalThicknessMeters;

        /// <summary>Raio do casco da cápsula (revolução) à altura y do quadro da cápsula; negativo fora do volume.</summary>
        public static float CapsuleRadiusAt(float y)
        {
            if (y < -ShieldSagMeters || y > BackshellHeightMeters)
                return -1f;

            if (y < 0f)
            {
                var u = -y / ShieldSagMeters;
                return CapsuleRadiusMeters * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
            }

            return Mathf.Lerp(CapsuleRadiusMeters, BackshellCapRadiusMeters, y / BackshellHeightMeters);
        }

        public static bool CapsuleContains(Vector3 capsuleLocal)
        {
            var radius = CapsuleRadiusAt(capsuleLocal.y);
            return radius >= 0f && new Vector2(capsuleLocal.x, capsuleLocal.z).magnitude <= radius;
        }

        /// <summary>O ponto (quadro do módulo) está dentro do tetraedro fechado, com folga negativa permitida.</summary>
        public static bool HullContains(Vector3 landerLocal, float marginMeters = 0f)
        {
            var v = HullVertices;
            var centroid = Vector3.zero;
            var faces = new[] { (0, 2, 1), (0, 1, 3), (2, 0, 3), (1, 2, 3) };
            foreach (var (i, j, k) in faces)
            {
                var normal = Vector3.Cross(v[j] - v[i], v[k] - v[i]).normalized;
                if (Vector3.Dot(normal, centroid - v[i]) > 0f)
                    normal = -normal;

                if (Vector3.Dot(normal, landerLocal - v[i]) > -marginMeters)
                    return false;
            }

            return true;
        }
    }
}
