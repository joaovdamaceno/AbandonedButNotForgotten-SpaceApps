using System;
using System.Collections.Generic;
using UnityEngine;
using G = Abandoned.SpaceApps.Chapters.Mars.EditorTools.LateMarsGeometry;

namespace Abandoned.SpaceApps.Chapters.Mars.EditorTools
{
    /// <summary>Malhas procedurais do pouso (MAR-014): cápsula, escudo, paraquedas, pétalas, lobos de airbag, plumas e terreno. Metros.</summary>
    public static class MerLandingGeometry
    {
        /// <summary>Corpo de revolução por um perfil (raio, y). Para sólidos convexos fechados; a orientação é corrigida pelo baricentro.</summary>
        public static G.Draft Lathe(IList<Vector2> profile, int segments, float uRepeat = 1f, bool autoOrient = true)
        {
            var d = new G.Draft();
            var rings = new int[profile.Count, segments + 1];
            var length = 0f;
            var cumulative = new float[profile.Count];
            for (var i = 1; i < profile.Count; i++)
            {
                length += (profile[i] - profile[i - 1]).magnitude;
                cumulative[i] = length;
            }

            for (var i = 0; i < profile.Count; i++)
            {
                for (var s = 0; s <= segments; s++)
                {
                    var angle = s * Mathf.PI * 2f / segments;
                    var p = new Vector3(Mathf.Cos(angle) * profile[i].x, profile[i].y, Mathf.Sin(angle) * profile[i].x);
                    rings[i, s] = d.Add(p, new Vector2(s / (float)segments * uRepeat, length > 0f ? cumulative[i] / length : 0f));
                }
            }

            for (var i = 0; i < profile.Count - 1; i++)
            {
                for (var s = 0; s < segments; s++)
                {
                    d.Tri(rings[i, s], rings[i + 1, s], rings[i, s + 1]);
                    d.Tri(rings[i, s + 1], rings[i + 1, s], rings[i + 1, s + 1]);
                }
            }

            if (autoOrient)
                Orient(d);

            return d;
        }

        /// <summary>Inverte os triângulos se, em média, apontam para dentro (sólido convexo em torno do baricentro).</summary>
        public static void Orient(G.Draft d)
        {
            var centroid = Vector3.zero;
            foreach (var v in d.Vertices)
                centroid += v;
            centroid /= Mathf.Max(1, d.Vertices.Count);

            var sum = 0f;
            for (var i = 0; i < d.Triangles.Count; i += 3)
            {
                var a = d.Vertices[d.Triangles[i]];
                var b = d.Vertices[d.Triangles[i + 1]];
                var c = d.Vertices[d.Triangles[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                sum += Vector3.Dot(normal, (a + b + c) / 3f - centroid);
            }

            if (sum >= 0f)
                return;

            for (var i = 0; i < d.Triangles.Count; i += 3)
            {
                var t = d.Triangles[i + 1];
                d.Triangles[i + 1] = d.Triangles[i + 2];
                d.Triangles[i + 2] = t;
            }
        }

        public static G.Draft Sphere(float diameter, int longitudes, int latitudes)
        {
            var d = new G.Draft();
            var r = diameter * 0.5f;
            var rows = new int[latitudes + 1, longitudes + 1];
            for (var y = 0; y <= latitudes; y++)
            {
                var phi = Mathf.PI * y / latitudes;
                for (var x = 0; x <= longitudes; x++)
                {
                    var theta = 2f * Mathf.PI * x / longitudes;
                    rows[y, x] = d.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta) * r, Mathf.Cos(phi) * r, Mathf.Sin(phi) * Mathf.Sin(theta) * r),
                        new Vector2(x / (float)longitudes, 1f - y / (float)latitudes));
                }
            }

            for (var y = 0; y < latitudes; y++)
            {
                for (var x = 0; x < longitudes; x++)
                {
                    d.Tri(rows[y, x], rows[y, x + 1], rows[y + 1, x]);
                    d.Tri(rows[y, x + 1], rows[y + 1, x + 1], rows[y + 1, x]);
                }
            }

            Orient(d);
            return d;
        }

        /// <summary>Escudo térmico: calota rasa voltada para baixo (−y) mais o disco da junção em y = 0.</summary>
        public static G.Draft HeatShield()
        {
            var profile = new List<Vector2>();
            var r = MerLandingLayout.CapsuleRadiusMeters;
            var sag = MerLandingLayout.ShieldSagMeters;
            const int steps = 14;
            for (var i = 0; i <= steps; i++)
            {
                var a = i / (float)steps * Mathf.PI * 0.5f;
                profile.Add(new Vector2(Mathf.Max(0.0001f, r * Mathf.Cos(a)), -sag * Mathf.Sin(a)));
            }

            profile.Reverse(); // do centro (fundo) até a borda
            profile.Add(new Vector2(r * 0.7f, 0.0f));
            profile.Add(new Vector2(0.0001f, 0.0f));
            return Lathe(profile, 40, 4f);
        }

        /// <summary>Casco traseiro cônico (aberto embaixo: o módulo aparece depois do descarte do escudo).</summary>
        public static G.Draft Backshell()
        {
            var profile = new List<Vector2>();
            const int steps = 14;
            for (var i = 0; i <= steps; i++)
            {
                var y = MerLandingLayout.BackshellHeightMeters * i / steps;
                profile.Add(new Vector2(MerLandingLayout.CapsuleRadiusAt(y) * (1f + 0.015f * Mathf.Sin(i * 1.7f)), y));
            }

            profile.Add(new Vector2(0.0001f, MerLandingLayout.BackshellHeightMeters));
            return Lathe(profile, 40, 6f, autoOrient: false);
        }

        /// <summary>Pluma de plasma atrás da cápsula (aberta, alfa pelo comprimento).</summary>
        public static G.Draft Plume()
        {
            var profile = new List<Vector2>();
            const int steps = 14;
            for (var i = 0; i <= steps; i++)
            {
                var v = i / (float)steps;
                profile.Add(new Vector2(Mathf.Max(0.15f, 3.1f + 1.1f * Mathf.Sin(v * 2.4f) - 3.0f * v * v), -0.2f + 16.2f * v));
            }

            return Lathe(profile, 48, 1f, autoOrient: false);
        }

        /// <summary>Chama de retrofoguete: cone com o ápice no bocal, apontando para −y.</summary>
        public static G.Draft Flame()
        {
            var profile = new List<Vector2> { new Vector2(0.12f, 0f), new Vector2(0.38f, -1.2f), new Vector2(0.5f, -2.4f), new Vector2(0.0001f, -3.4f) };
            return Lathe(profile, 14, 1f, autoOrient: false);
        }

        /// <summary>Calota do paraquedas com 24 gomos (aberta embaixo), centrada na origem; o topo fica em +y.</summary>
        public static G.Draft Canopy()
        {
            var d = new G.Draft();
            const int gores = 24;
            const int perGore = 4;
            const int segments = gores * perGore;
            const int rows = 14;
            var radius = MerLandingLayout.ParachuteRadiusMeters;
            var rings = new int[rows + 1, segments + 1];
            for (var y = 0; y <= rows; y++)
            {
                var theta = Mathf.Deg2Rad * 108f * y / rows;
                for (var s = 0; s <= segments; s++)
                {
                    var phi = 2f * Mathf.PI * s / segments;
                    var scallop = 1f - 0.06f * (1f - Mathf.Cos(phi * gores)) * 0.5f * Mathf.Sin(theta);
                    var rr = radius * Mathf.Sin(theta) * scallop;
                    var p = new Vector3(Mathf.Cos(phi) * rr, radius * 0.78f * Mathf.Cos(theta), Mathf.Sin(phi) * rr);
                    rings[y, s] = d.Add(p, new Vector2(s / (float)segments * (gores / 2f), y / (float)rows));
                }
            }

            for (var y = 0; y < rows; y++)
            {
                for (var s = 0; s < segments; s++)
                {
                    d.Tri(rings[y, s], rings[y + 1, s], rings[y, s + 1]);
                    d.Tri(rings[y, s + 1], rings[y + 1, s], rings[y + 1, s + 1]);
                }
            }

            return d;
        }

        /// <summary>Linhas de suspensão (MeshTopology.Lines): da borda do paraquedas ao topo do casco, no quadro da calota.</summary>
        public static Mesh SuspensionLines()
        {
            const int lines = 24;
            var radius = MerLandingLayout.ParachuteRadiusMeters;
            var theta = Mathf.Deg2Rad * 108f;
            var rimY = radius * 0.78f * Mathf.Cos(theta);
            var capTop = new Vector3(0f, -MerLandingLayout.ParachuteAnchorHeightMeters + MerLandingLayout.BackshellHeightMeters, 0f);
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (var i = 0; i < lines; i++)
            {
                var phi = 2f * Mathf.PI * i / lines;
                var rim = new Vector3(Mathf.Cos(phi) * radius * Mathf.Sin(theta) * 0.94f, rimY, Mathf.Sin(phi) * radius * Mathf.Sin(theta) * 0.94f);
                indices.Add(vertices.Count);
                vertices.Add(rim);
                indices.Add(vertices.Count);
                vertices.Add(capTop + new Vector3(Mathf.Cos(phi), 0f, Mathf.Sin(phi)) * 0.35f);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static G.Draft Cylinder(float radius, float height, int segments)
        {
            var profile = new List<Vector2>
            {
                new Vector2(0.0001f, -height * 0.5f), new Vector2(radius, -height * 0.5f), new Vector2(radius, height * 0.5f), new Vector2(0.0001f, height * 0.5f),
            };
            return Lathe(profile, segments);
        }

        /// <summary>Prisma triangular (placa) com espessura; vértices em <paramref name="triangle"/>, centrado na superfície do triângulo.</summary>
        public static G.Draft Prism(IList<Vector3> triangle, float thickness)
        {
            var d = new G.Draft();
            var normal = Vector3.Cross(triangle[1] - triangle[0], triangle[2] - triangle[0]).normalized * (thickness * 0.5f);
            var top = new int[3];
            var bottom = new int[3];
            var uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f) };
            for (var i = 0; i < 3; i++)
            {
                top[i] = d.Add(triangle[i] + normal, uv[i]);
                bottom[i] = d.Add(triangle[i] - normal, uv[i]);
            }

            d.Tri(top[0], top[1], top[2]);
            d.Tri(bottom[0], bottom[2], bottom[1]);
            for (var i = 0; i < 3; i++)
            {
                var n = (i + 1) % 3;
                var a = d.Add(d.Vertices[top[i]], new Vector2(0f, 0f));
                var b = d.Add(d.Vertices[top[n]], new Vector2(1f, 0f));
                var c = d.Add(d.Vertices[bottom[i]], new Vector2(0f, 1f));
                var e = d.Add(d.Vertices[bottom[n]], new Vector2(1f, 1f));
                d.Tri(a, c, b);
                d.Tri(b, c, e);
            }

            Orient(d);
            return d;
        }

        /// <summary>Terreno do palco (anéis polares): fino perto do ponto de parada e mais grosso longe.</summary>
        public static G.Draft Ground(MerLandingSite site, float maxRadius, bool lowDetail)
        {
            Func<float, float, float> height = (x, z) => MerLandingChoreography.Ground(site, x, z);
            var radii = lowDetail ? G.RingRadii(maxRadius, 1f, 40f, 0.14f) : G.RingRadii(maxRadius, 0.4f, 16f, 0.09f);
            return G.Polar(height, radii, lowDetail ? 64 : 128);
        }
    }
}
