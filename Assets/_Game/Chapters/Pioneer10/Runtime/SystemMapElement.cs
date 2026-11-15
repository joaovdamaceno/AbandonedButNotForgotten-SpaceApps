using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Mapa ESQUEMÁTICO do Sistema Solar visto de cima (sem escala de verdade; o raio cresce com a raiz da distância em UA para caber).
    /// Dois modos, os dois desenhados com Painter2D: o interno (Terra, Marte, o cinturão de asteroides e Júpiter, com a linha do caminho
    /// da sonda atravessando o cinturão) e o externo (Júpiter, Saturno, Urano e Netuno, com a sonda cruzando cada órbita). A órbita
    /// que a sonda acaba de cruzar pulsa. Nenhum planeta é colocado junto da sonda: só as órbitas são desenhadas.
    /// </summary>
    public sealed class SystemMapElement : VisualElement
    {
        public enum MapMode { Inner = 0, Outer = 1 }

        public struct State
        {
            public MapMode Mode;
            /// <summary>Distância da sonda ao Sol em UA, que posiciona o marcador sobre a linha do caminho.</summary>
            public float ProbeAu;
            /// <summary>Início da linha do caminho em UA (a órbita de onde ela sai).</summary>
            public float PathStartAu;
            /// <summary>Fim da linha do caminho em UA: o trecho à frente da sonda é desenhado fraco, o já percorrido forte.</summary>
            public float PathEndAu;
            public bool ShowPath;
            /// <summary>Índice da órbita que pulsa (0 a 3 na ordem do modo), ou -1.</summary>
            public int PulseRing;
            /// <summary>0–1: fase do pulso.</summary>
            public float Pulse01;
            /// <summary>Rótulos, na ordem das órbitas do modo (interno: Terra, Marte, cinturão, Júpiter; externo: Júpiter, Saturno, Urano, Netuno).</summary>
            public string[] Labels;
            public string Title, Footnote, ProbeLabel;
        }

        /// <summary>Distâncias médias ao Sol em UA: Terra, Marte, Júpiter, Saturno, Urano, Netuno.</summary>
        public static readonly float[] InnerAu = { 1.0f, 1.52f, 3.0f, 5.2f };
        public static readonly float[] OuterAu = { 5.2f, 9.58f, 19.2f, 30.05f };
        /// <summary>Faixa do cinturão principal em UA.</summary>
        public const float BeltInnerAu = 2.1f;
        public const float BeltOuterAu = 3.3f;
        public const float InnerMaxAu = 6f;
        public const float OuterMaxAu = 72f;
        /// <summary>Ângulo (graus) da linha do caminho da sonda, saindo do Sol.</summary>
        public const float PathAngleDegrees = -38f;
        const int BeltDots = 340;

        static readonly Color Ink = new Color(0.92f, 0.95f, 1f, 0.85f);
        static readonly Color Faint = new Color(0.8f, 0.86f, 1f, 0.30f);
        static readonly Color Amber = new Color(1f, 0.82f, 0.42f, 1f);
        static readonly Color Rock = new Color(0.86f, 0.80f, 0.72f, 0.95f);

        readonly float[] dotAngle = new float[BeltDots];
        readonly float[] dotRadius = new float[BeltDots];
        readonly float[] dotSize = new float[BeltDots];
        readonly Label[] labels = new Label[4];
        readonly Label title, footnote, probeLabel;
        State state;

        public SystemMapElement()
        {
            name = "system-map";
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
            style.position = Position.Absolute;
            style.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 0.95f);
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 10;

            var random = new System.Random(7202);
            for (var i = 0; i < BeltDots; i++)
            {
                dotAngle[i] = (float)random.NextDouble() * Mathf.PI * 2f;
                dotRadius[i] = Mathf.Lerp(BeltInnerAu, BeltOuterAu, Mathf.Pow((float)random.NextDouble(), 0.9f));
                var big = random.NextDouble() < 0.08;
                dotSize[i] = big ? 2.8f + (float)random.NextDouble() * 1.6f : 1.0f + (float)random.NextDouble() * 1.4f; // tamanhos variados, poucos grandes
            }

            for (var i = 0; i < labels.Length; i++)
                labels[i] = MakeLabel(false);
            title = MakeLabel(true);
            footnote = MakeLabel(false);
            probeLabel = MakeLabel(false);
            title.style.left = 14;
            title.style.top = 10;
            footnote.style.left = 14;
            footnote.style.bottom = 8;
            probeLabel.style.color = Amber;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
        }

        Label MakeLabel(bool bold)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.fontSize = bold ? 20 : 15;
            label.style.color = new Color(0.92f, 0.95f, 1f, 0.95f);
            if (bold)
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            Add(label);
            return label;
        }

        public State Current => state;

        public void SetState(in State value)
        {
            state = value;
            title.text = value.Title ?? string.Empty;
            footnote.text = value.Footnote ?? string.Empty;
            probeLabel.text = value.ProbeLabel ?? string.Empty;
            var count = value.Mode == MapMode.Inner ? InnerAu.Length : OuterAu.Length;
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i].text = value.Labels != null && i < value.Labels.Length && i < count ? value.Labels[i] : string.Empty;
                labels[i].style.display = string.IsNullOrEmpty(labels[i].text) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            PlaceLabels();
            MarkDirtyRepaint();
        }

        /// <summary>Raio em pixels de uma distância em UA (raiz, para o interior do sistema não virar um ponto).</summary>
        public static float RadiusFor(float au, float maxAu, float outerRadius) => outerRadius * Mathf.Sqrt(Mathf.Clamp(au, 0f, maxAu * 1.5f) / maxAu);

        float MaxAu => state.Mode == MapMode.Inner ? InnerMaxAu : OuterMaxAu;

        Vector2 Center(out float outerRadius)
        {
            var rect = contentRect;
            outerRadius = Mathf.Max(8f, Mathf.Min(rect.width, rect.height) * 0.5f * 0.82f);
            return new Vector2(rect.width * 0.5f, rect.height * 0.54f);
        }

        static Vector2 OnCircle(Vector2 center, float radius, float radians) => center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;

        void PlaceLabels()
        {
            var center = Center(out var outer);
            var rings = state.Mode == MapMode.Inner ? InnerAu : OuterAu;
            for (var i = 0; i < labels.Length && i < rings.Length; i++)
            {
                var at = OnCircle(center, RadiusFor(rings[i], MaxAu, outer), Mathf.Deg2Rad * (196f + i * 26f));
                labels[i].style.left = at.x - 6f - (labels[i].text?.Length ?? 0) * 7f;
                labels[i].style.top = at.y - 10f;
            }

            var probeAu = state.ProbeAu;
            var probe = OnCircle(center, RadiusFor(probeAu, MaxAu, outer), Mathf.Deg2Rad * PathAngleDegrees);
            probeLabel.style.left = probe.x + 12f;
            probeLabel.style.top = probe.y - 22f;
        }

        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var center = Center(out var outer);
            var rings = state.Mode == MapMode.Inner ? InnerAu : OuterAu;

            // Sol
            painter.fillColor = Amber;
            painter.BeginPath();
            painter.Arc(center, 5f, Angle.Degrees(0), Angle.Degrees(360));
            painter.Fill();

            for (var i = 0; i < rings.Length; i++)
            {
                if (state.Mode == MapMode.Inner && i == 2)
                {
                    DrawBelt(painter, center, outer);
                    continue;
                }

                var pulse = i == state.PulseRing ? Mathf.Sin(Mathf.Clamp01(state.Pulse01) * Mathf.PI) : 0f;
                painter.strokeColor = Color.Lerp(Faint, Amber, pulse);
                painter.lineWidth = 1.5f + pulse * 3f;
                painter.BeginPath();
                painter.Arc(center, RadiusFor(rings[i], MaxAu, outer), Angle.Degrees(0), Angle.Degrees(360));
                painter.Stroke();
            }

            if (state.ShowPath)
                DrawPath(painter, center, outer);
        }

        void DrawBelt(Painter2D painter, Vector2 center, float outer)
        {
            var pulse = state.PulseRing == 2 ? Mathf.Sin(Mathf.Clamp01(state.Pulse01) * Mathf.PI) : 0f;
            painter.fillColor = Color.Lerp(Rock, Amber, pulse);
            for (var i = 0; i < BeltDots; i++)
            {
                var at = OnCircle(center, RadiusFor(dotRadius[i], MaxAu, outer), dotAngle[i]);
                painter.BeginPath();
                painter.Arc(at, dotSize[i], Angle.Degrees(0), Angle.Degrees(360));
                painter.Fill();
            }
        }

        void DrawPath(Painter2D painter, Vector2 center, float outer)
        {
            var angle = Mathf.Deg2Rad * PathAngleDegrees;
            var from = OnCircle(center, RadiusFor(Mathf.Max(0.01f, state.PathStartAu), MaxAu, outer), angle);
            var to = OnCircle(center, RadiusFor(state.ProbeAu, MaxAu, outer), angle);
            if (state.PathEndAu > state.ProbeAu)
            {
                var end = OnCircle(center, RadiusFor(state.PathEndAu, MaxAu, outer), angle);
                painter.strokeColor = new Color(Amber.r, Amber.g, Amber.b, 0.35f);
                painter.lineWidth = 2f;
                painter.BeginPath();
                painter.MoveTo(to);
                painter.LineTo(end);
                painter.Stroke();
            }

            painter.strokeColor = Amber;
            painter.lineWidth = 3f;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();

            // marcador da sonda: losango com contorno
            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.MoveTo(to + new Vector2(0f, -8f));
            painter.LineTo(to + new Vector2(8f, 0f));
            painter.LineTo(to + new Vector2(0f, 8f));
            painter.LineTo(to + new Vector2(-8f, 0f));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
