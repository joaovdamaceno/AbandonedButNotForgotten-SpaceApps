using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Abandoned.SpaceApps.EditorTools
{
    /// <summary>
    /// PIO-009 · Arte da placa educativa em traço dourado, numa só fonte: uma lista de formas (círculos, traços, polígonos)
    /// vira o SVG mestre (<c>Art/PlaqueEducational.svg</c>) E os cinco PNGs que o painel usa (rasterizados aqui, com
    /// antisserrilhado 4×4), de modo que vetor e textura nunca divergem. É uma ADAPTAÇÃO EDUCATIVA identificada, não uma
    /// reprodução fiel da gravura (NASA P-S11, P-S12): hidrogênio, mapa de pulsares, Sistema Solar, escala da Pioneer e
    /// pessoas em silhueta lisa, sem detalhes anatômicos. Comprimentos de linha e marcas dos pulsares e da escala são
    /// ESQUEMÁTICOS (não carregam períodos nem medidas reais). Cada peça é desenhada numa caixa local de 100 × 100.
    /// </summary>
    public static class PioneerPlaqueArt
    {
        /// <summary>Alumínio anodizado dourado (não ouro maciço, P-S12).</summary>
        public static readonly Color Gold = new Color(0.89f, 0.71f, 0.37f, 1f);

        public const string GoldHex = "#E3B55F";
        public const int IconPixels = 256;
        public const int Supersampling = 4;

        /// <summary>Quadro da placa: proporção 152 × 229 mm (P-S12) ≈ 2:3.</summary>
        public const float BoardWidth = 300f;
        public const float BoardHeight = 450f;

        /// <summary>Ordem das peças (a mesma da legenda PIO.05.02–06 e dos gatilhos PIO.Plaque.*).</summary>
        public static readonly string[] PieceIds = { "hydrogen", "pulsars", "solar-system", "pioneer-scale", "people" };

        /// <summary>
        /// Contornos (x, y, largura, altura) no quadro de 300 × 450. O painel (PlaquePanel.uss) usa os MESMOS números em px:
        /// mudar aqui exige mudar lá (um teste confere o SVG contra esta tabela).
        /// </summary>
        public static readonly Rect[] SlotRects =
        {
            new Rect(12f, 12f, 120f, 108f),    // hidrogênio
            new Rect(12f, 132f, 168f, 168f),   // mapa de pulsares
            new Rect(12f, 312f, 276f, 126f),   // Sistema Solar
            new Rect(144f, 12f, 144f, 108f),   // escala da Pioneer
            new Rect(192f, 132f, 96f, 168f),   // pessoas
        };

        // ---------- Formas ----------

        public abstract class Shape
        {
            public abstract bool Inside(float x, float y);
            public abstract string Svg();
        }

        sealed class Ring : Shape
        {
            readonly float cx, cy, r, w;
            public Ring(float cx, float cy, float r, float w) { this.cx = cx; this.cy = cy; this.r = r; this.w = w; }
            public override bool Inside(float x, float y) => Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r) <= w * 0.5f;
            public override string Svg() => $"<circle cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"{F(r)}\" fill=\"none\" stroke=\"{GoldHex}\" stroke-width=\"{F(w)}\"/>";
        }

        sealed class Disc : Shape
        {
            readonly float cx, cy, r;
            public Disc(float cx, float cy, float r) { this.cx = cx; this.cy = cy; this.r = r; }
            public override bool Inside(float x, float y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
            public override string Svg() => $"<circle cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"{F(r)}\" fill=\"{GoldHex}\"/>";
        }

        /// <summary>Traço com pontas redondas (também serve de "cápsula" para as silhuetas lisas).</summary>
        sealed class Seg : Shape
        {
            readonly float x1, y1, x2, y2, w;
            public Seg(float x1, float y1, float x2, float y2, float w) { this.x1 = x1; this.y1 = y1; this.x2 = x2; this.y2 = y2; this.w = w; }
            public override bool Inside(float x, float y) => Distance(x, y, x1, y1, x2, y2) <= w * 0.5f;
            public override string Svg() => $"<line x1=\"{F(x1)}\" y1=\"{F(y1)}\" x2=\"{F(x2)}\" y2=\"{F(y2)}\" stroke=\"{GoldHex}\" stroke-width=\"{F(w)}\" stroke-linecap=\"round\"/>";
        }

        sealed class Polyline : Shape
        {
            readonly Vector2[] points;
            readonly float w;
            public Polyline(float w, params Vector2[] points) { this.w = w; this.points = points; }
            public override bool Inside(float x, float y)
            {
                for (var i = 0; i + 1 < points.Length; i++)
                {
                    if (Distance(x, y, points[i].x, points[i].y, points[i + 1].x, points[i + 1].y) <= w * 0.5f)
                        return true;
                }

                return false;
            }

            public override string Svg() =>
                $"<polyline points=\"{string.Join(" ", points.Select(p => F(p.x) + "," + F(p.y)))}\" fill=\"none\" stroke=\"{GoldHex}\" stroke-width=\"{F(w)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>";
        }

        sealed class Poly : Shape
        {
            readonly Vector2[] points;
            public Poly(params Vector2[] points) { this.points = points; }
            public override bool Inside(float x, float y)
            {
                var inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    if ((points[i].y > y) != (points[j].y > y)
                        && x < (points[j].x - points[i].x) * (y - points[i].y) / (points[j].y - points[i].y) + points[i].x)
                        inside = !inside;
                }

                return inside;
            }

            public override string Svg() => $"<polygon points=\"{string.Join(" ", points.Select(p => F(p.x) + "," + F(p.y)))}\" fill=\"{GoldHex}\"/>";
        }

        static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        static float Distance(float px, float py, float x1, float y1, float x2, float y2)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var t = Mathf.Clamp01(((px - x1) * dx + (py - y1) * dy) / Mathf.Max(dx * dx + dy * dy, 1e-6f));
            var cx = x1 + dx * t - px;
            var cy = y1 + dy * t - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        // ---------- As cinco peças (caixa local 100 × 100, y para baixo) ----------

        /// <summary>Hidrogênio: duas voltas ligadas por um traço, com a marca de spin, mais a marca binária "1" ao lado.</summary>
        public static List<Shape> Hydrogen() => new List<Shape>
        {
            new Ring(50f, 24f, 15f, 4f), new Ring(50f, 76f, 15f, 4f),
            new Seg(50f, 39f, 50f, 61f, 4f),
            new Seg(50f, 29f, 50f, 17f, 3f), new Poly(new Vector2(45f, 19f), new Vector2(55f, 19f), new Vector2(50f, 11f)),
            new Seg(50f, 71f, 50f, 83f, 3f), new Poly(new Vector2(45f, 81f), new Vector2(55f, 81f), new Vector2(50f, 89f)),
            new Seg(80f, 38f, 80f, 62f, 4f),
        };

        /// <summary>Pulsares: 14 linhas a partir do Sol, em comprimentos e direções distintos, com marcas nas pontas (esquemático).</summary>
        public static List<Shape> Pulsars()
        {
            var shapes = new List<Shape> { new Disc(50f, 50f, 4f) };
            var angles = new[] { 0f, 24f, 47f, 75f, 101f, 128f, 150f, 176f, 203f, 228f, 254f, 281f, 308f, 335f };
            var lengths = new[] { 40f, 28f, 36f, 22f, 42f, 30f, 38f, 24f, 41f, 29f, 35f, 26f, 39f, 32f };
            for (var i = 0; i < angles.Length; i++)
            {
                var radians = angles[i] * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                var tip = new Vector2(50f, 50f) + direction * lengths[i];
                shapes.Add(new Seg(50f + direction.x * 6f, 50f + direction.y * 6f, tip.x, tip.y, 2.4f));
                var normal = new Vector2(-direction.y, direction.x);
                var ticks = 1 + i % 3; // marcas decorativas nas pontas: 1 a 3
                for (var t = 0; t < ticks; t++)
                {
                    var center = tip - direction * (t * 4f);
                    shapes.Add(new Seg(center.x - normal.x * 3.2f, center.y - normal.y * 3.2f, center.x + normal.x * 3.2f, center.y + normal.y * 3.2f, 1.8f));
                }
            }

            return shapes;
        }

        /// <summary>Sistema Solar: Sol e nove corpos em fila (o terceiro, a Terra, marcado) e o arco da trajetória da Terra a Júpiter, adiante.</summary>
        public static List<Shape> SolarSystem()
        {
            var shapes = new List<Shape> { new Disc(7f, 58f, 5f), new Seg(14f, 58f, 96f, 58f, 1.4f) };
            var xs = new[] { 20f, 28f, 38f, 49f, 61f, 71f, 80f, 88f, 95f };
            var radii = new[] { 1.8f, 2.4f, 2.8f, 2.2f, 5.6f, 4.6f, 3.4f, 3.2f, 1.6f };
            for (var i = 0; i < xs.Length; i++)
                shapes.Add(new Disc(xs[i], 58f, radii[i]));
            shapes.Add(new Ring(xs[2], 58f, 6.2f, 2f)); // a terceira: a Terra, origem da viagem

            var arc = new List<Vector2>();
            for (var step = 0; step <= 14; step++)
            {
                var t = step / 14f; // curva de (Terra) até além de Júpiter, passando acima dele
                var x = Mathf.Lerp(xs[2], 98f, t);
                var y = 58f - 38f * Mathf.Sin(t * Mathf.PI) * (1f - 0.35f * t);
                arc.Add(new Vector2(x, y));
            }

            shapes.Add(new Polyline(2.4f, arc.ToArray()));
            var end = arc[arc.Count - 1];
            shapes.Add(new Poly(new Vector2(end.x - 8f, end.y - 5f), new Vector2(end.x + 3f, end.y + 2f), new Vector2(end.x - 6f, end.y + 6f)));
            return shapes;
        }

        /// <summary>Escala: a Pioneer de frente (refletor, hastes e suportes) e uma régua com as marcas da largura do refletor (esquemático).</summary>
        public static List<Shape> PioneerScale() => new List<Shape>
        {
            new Ring(50f, 44f, 31f, 3f),
            new Seg(50f, 44f, 50f, 20f, 2f), new Seg(50f, 44f, 72f, 56f, 2f), new Seg(50f, 44f, 28f, 56f, 2f),
            new Disc(50f, 44f, 3.6f),
            new Seg(19f, 44f, 6f, 44f, 3f), new Seg(81f, 44f, 94f, 44f, 3f), new Seg(94f, 40f, 94f, 48f, 3f), new Seg(6f, 40f, 6f, 48f, 3f),
            new Seg(50f, 75f, 50f, 84f, 3f),
            new Seg(19f, 92f, 81f, 92f, 2f), new Seg(19f, 87f, 19f, 97f, 2f), new Seg(81f, 87f, 81f, 97f, 2f),
        };

        /// <summary>Pessoas: duas silhuetas lisas (cabeça, tronco e pernas em cápsulas), uma com a mão erguida. Sem detalhes anatômicos.</summary>
        public static List<Shape> People() => new List<Shape>
        {
            // pessoa mais alta, à esquerda, com a mão erguida
            new Disc(33f, 17f, 7f),
            new Seg(33f, 30f, 33f, 58f, 13f),
            new Seg(33f, 58f, 29f, 90f, 8f), new Seg(33f, 58f, 38f, 90f, 8f),
            new Seg(27f, 34f, 22f, 52f, 6f),
            new Seg(39f, 34f, 50f, 18f, 6f), new Disc(52f, 15f, 4f),
            // pessoa um pouco menor, à direita
            new Disc(70f, 25f, 6.4f),
            new Seg(70f, 37f, 70f, 61f, 12f),
            new Seg(70f, 61f, 66f, 90f, 7.4f), new Seg(70f, 61f, 74f, 90f, 7.4f),
            new Seg(64f, 41f, 60f, 56f, 5.4f), new Seg(76f, 41f, 79f, 56f, 5.4f),
        };

        public static List<Shape> ShapesOf(int piece)
        {
            switch (piece)
            {
                case 0: return Hydrogen();
                case 1: return Pulsars();
                case 2: return SolarSystem();
                case 3: return PioneerScale();
                default: return People();
            }
        }

        // ---------- Rasterização (PNG) ----------

        /// <summary>Rasteriza a peça numa textura RGBA transparente com traço dourado. Determinístico.</summary>
        public static Texture2D Render(int piece)
        {
            var shapes = ShapesOf(piece);
            var texture = new Texture2D(IconPixels, IconPixels, TextureFormat.RGBA32, false);
            var pixels = new Color32[IconPixels * IconPixels];
            var scale = 100f / IconPixels;
            var gold = (Color32)Gold;
            for (var py = 0; py < IconPixels; py++)
            {
                for (var px = 0; px < IconPixels; px++)
                {
                    var covered = 0;
                    for (var sy = 0; sy < Supersampling; sy++)
                    {
                        for (var sx = 0; sx < Supersampling; sx++)
                        {
                            var x = (px + (sx + 0.5f) / Supersampling) * scale;
                            var y = (py + (sy + 0.5f) / Supersampling) * scale;
                            for (var i = 0; i < shapes.Count; i++)
                            {
                                if (shapes[i].Inside(x, y))
                                {
                                    covered++;
                                    break;
                                }
                            }
                        }
                    }

                    var alpha = (byte)Mathf.RoundToInt(255f * covered / (Supersampling * Supersampling));
                    pixels[(IconPixels - 1 - py) * IconPixels + px] = new Color32(gold.r, gold.g, gold.b, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        // ---------- SVG mestre ----------

        /// <summary>
        /// SVG da placa inteira (quadro 2:3 e as cinco peças nos seus contornos). O texto fica sem acentos: os títulos ficam
        /// em &lt;title&gt;/&lt;desc&gt;, que não são desenhados; os nomes e as frases do jogo vêm da localização.
        /// </summary>
        public static string BoardSvg()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(BoardWidth)} {F(BoardHeight)}\" width=\"{F(BoardWidth)}\" height=\"{F(BoardHeight)}\">");
            sb.AppendLine("  <title>Placa da Pioneer 10 - adaptacao educativa</title>");
            sb.AppendLine("  <desc>Adaptacao educativa em traco dourado (NASA P-S11, P-S12), nao e reproducao fiel da gravura. Cinco pecas: hidrogenio, mapa de pulsares, Sistema Solar, escala da Pioneer e pessoas (silhuetas lisas). Linhas dos pulsares e marcas sao esquematicas. Aluminio anodizado dourado, nao ouro macico. Gerado por PioneerPlaqueArt: nao editar a mao.</desc>");
            sb.AppendLine($"  <rect x=\"3\" y=\"3\" width=\"{F(BoardWidth - 6f)}\" height=\"{F(BoardHeight - 6f)}\" rx=\"14\" fill=\"none\" stroke=\"{GoldHex}\" stroke-width=\"4\"/>");
            for (var i = 0; i < PieceIds.Length; i++)
            {
                var slot = SlotRects[i];
                var scale = Mathf.Min(slot.width, slot.height) / 100f;
                var tx = slot.x + (slot.width - 100f * scale) * 0.5f;
                var ty = slot.y + (slot.height - 100f * scale) * 0.5f;
                sb.AppendLine($"  <g id=\"{PieceIds[i]}\" transform=\"translate({F(tx)} {F(ty)}) scale({F(scale)})\">");
                foreach (var shape in ShapesOf(i))
                    sb.AppendLine("    " + shape.Svg());
                sb.AppendLine("  </g>");
            }

            sb.AppendLine("</svg>");
            return sb.ToString().Replace("\r\n", "\n");
        }
    }
}
