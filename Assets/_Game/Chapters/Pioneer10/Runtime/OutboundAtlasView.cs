using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// PIO-010 · Painel UI Toolkit do caminho de saída (<c>UI/OutboundAtlas.uxml</c>): diagrama orbital 2D esquemático (anéis
    /// de Júpiter a Netuno, arco de saída, marcador da sonda e NENHUM planeta junto a ela), barras relativas de energia e o
    /// gráfico de 1998 com duas linhas de DISTÂNCIA AO SOL. O tempo de comunicação é uma grandeza separada, com barra e
    /// rótulo próprios. Só desenha o <see cref="OutboundViewState"/>; textos vêm de chaves resolvidas pelo capítulo.
    /// Os desenhos (anéis, arco, linhas) usam <see cref="Painter2D"/> e são qualitativos: nenhum valor histórico é inventado.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class OutboundAtlasView : MonoBehaviour
    {
        public static readonly string[] RingKeys = { "PIO.UI.orbit_jupiter", "PIO.UI.orbit_saturn", "PIO.UI.orbit_uranus", "PIO.UI.orbit_neptune" };
        public const string SunKey = "PIO.UI.sun";
        public const string SchemeKey = "PIO.UI.scheme";
        public const string PastSaturnKey = "PIO.UI.past_saturn";
        public const string PastNeptuneKey = "PIO.UI.past_neptune";
        public const string NotEdgeKey = "PIO.UI.not_edge";
        public const string PowerTitleKey = "PIO.UI.power_title";
        public const string PowerOkKey = "PIO.UI.power_ok";
        public const string RadioKey = "PIO.UI.radio";
        public const string InstrumentsKey = "PIO.UI.instruments";
        public const string DistanceKey = "PIO.UI.dist_sun";
        public const string TimeKey = "PIO.UI.time";
        public const string Pioneer10Key = "PIO.UI.pioneer10";
        public const string Voyager1Key = "PIO.UI.voyager1";
        public const string CommTimeKey = "PIO.UI.comm_time";
        public const string Cross1998Key = "PIO.UI.cross1998";
        public const string Card1976Key = "PIO.Card.y1976";
        public const string Card1983Key = "PIO.Card.y1983";
        public const string Card1998Key = "PIO.Card.y1998";

        /// <summary>Raio do anel k (0–3) como fração do raio máximo: espaçamento IGUAL, esquemático e sem escala.</summary>
        public static float RingFraction(int ring) => (ring + 1) / (float)OutboundAtlas.OrbitRings;

        const float RingRadiusOfHeight = 0.42f;
        /// <summary>Direção do arco de saída (graus, y para baixo: negativo sobe à direita).</summary>
        const float ExitAngleDegrees = -32f;

        static readonly Color Ink = new Color(0.96f, 0.945f, 0.91f, 1f);
        static readonly Color InkDim = new Color(0.96f, 0.945f, 0.91f, 0.38f);
        static readonly Color Amber = new Color(1f, 0.82f, 0.35f, 1f);
        static readonly Color Cyan = new Color(0.4f, 0.8f, 0.93f, 1f);

        static readonly string[] HoldClasses = { "sa-hold--available", "sa-hold--rearm", "sa-hold--holding", "sa-hold--paused", "sa-hold--done" };

        [SerializeField] UIDocument document;

        VisualElement root, orbit, chart, powerColumn, holdPlate, progressFill, commFill;
        VisualElement[] bars, fills;
        VisualElement radioFill;
        Label card, note, note2, diagramTitle, powerTitle, radioLabel, instrumentsLabel, powerOk, commLabel, holdLabel, sunLabel, scheme, chartScheme;
        Label axisY, axisX, linePioneer, lineVoyager;
        Label[] ringLabels;
        OutboundViewState current;
        OutboundPhase lastPhase = (OutboundPhase)(-1);
        OutboundHoldState lastHold = (OutboundHoldState)(-1);

        public bool IsBound => root != null;
        public VisualElement Root => root;
        /// <summary>Marcador da sonda no diagrama: raio (0–1 do anel externo) do último desenho. Para testes e capturas.</summary>
        public float LastProbeRadiusFraction { get; private set; }

        void OnEnable() => Bind();

        void OnDisable() => root = null;

        public void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("outbound-root") == null || root == tree.Q("outbound-root"))
                return;

            // O contêiner do UIDocument só tem filhos absolutos e ficaria com altura zero (percentuais relativos a nada).
            tree.style.position = Position.Absolute;
            tree.style.left = 0f;
            tree.style.top = 0f;
            tree.style.right = 0f;
            tree.style.bottom = 0f;
            tree.pickingMode = PickingMode.Ignore;

            root = tree.Q("outbound-root");
            orbit = root.Q("orbit");
            chart = root.Q("chart");
            powerColumn = root.Q("power-col");
            holdPlate = root.Q("hold");
            progressFill = root.Q("progress-fill");
            commFill = root.Q("comm-fill");
            radioFill = root.Q("fill-radio");
            bars = new VisualElement[4];
            fills = new VisualElement[4];
            for (var i = 0; i < 4; i++)
            {
                bars[i] = root.Q("bar-" + i);
                fills[i] = root.Q("fill-" + i);
            }

            card = root.Q<Label>("card");
            note = root.Q<Label>("note");
            note2 = root.Q<Label>("note-2");
            diagramTitle = root.Q<Label>("diagram-title");
            powerTitle = root.Q<Label>("power-title");
            radioLabel = root.Q<Label>("radio-label");
            instrumentsLabel = root.Q<Label>("instruments-label");
            powerOk = root.Q<Label>("power-ok");
            commLabel = root.Q<Label>("comm-label");
            holdLabel = root.Q<Label>("hold-label");
            sunLabel = root.Q<Label>("sun-label");
            scheme = root.Q<Label>("scheme");
            chartScheme = root.Q<Label>("chart-scheme");
            axisY = root.Q<Label>("axis-y");
            axisX = root.Q<Label>("axis-x");
            linePioneer = root.Q<Label>("line-pioneer");
            lineVoyager = root.Q<Label>("line-voyager");
            ringLabels = new Label[OutboundAtlas.OrbitRings];
            for (var i = 0; i < ringLabels.Length; i++)
                ringLabels[i] = root.Q<Label>("ring-" + i);

            orbit.generateVisualContent += DrawOrbit;
            chart.generateVisualContent += DrawChart;
            PlaceLabels();
            lastPhase = (OutboundPhase)(-1);
            lastHold = (OutboundHoldState)(-1);
        }

        // ---------- Geometria compartilhada entre os desenhos e os rótulos (em frações do canvas) ----------

        static Vector2 OrbitCenter => new Vector2(0.5f, 0.5f);

        /// <summary>Ponto do gráfico (t 0–1 no tempo, y 0–1 de distância, para cima) em frações do canvas.</summary>
        static Vector2 ChartPoint(float t, float distance) => new Vector2(0.12f + t * 0.78f, 0.86f - distance * 0.74f);

        /// <summary>Distâncias QUALITATIVAS (sem unidade): a Voyager começa mais perto e sobe mais rápido; as linhas se cruzam em <see cref="CrossingT"/>.</summary>
        static float PioneerDistance(float t) => 0.50f + 0.34f * t;

        static float VoyagerDistance(float t) => 0.18f + 0.78f * t;

        /// <summary>Instante (0–1) em que a distância da Voyager 1 passa a da Pioneer: 0,32 = 0,44 t.</summary>
        public static readonly float CrossingT = 0.32f / 0.44f;

        void PlaceLabels()
        {
            var center = OrbitCenter;
            Place(sunLabel, center.x - 0.014f, center.y + 0.025f);
            for (var i = 0; i < ringLabels.Length; i++)
                Place(ringLabels[i], center.x + 0.012f, center.y - RingRadiusOfHeight * RingFraction(i) - 0.06f);
            var pioneerEnd = ChartPoint(1f, PioneerDistance(1f));
            var voyagerEnd = ChartPoint(1f, VoyagerDistance(1f));
            Place(linePioneer, pioneerEnd.x - 0.11f, pioneerEnd.y + 0.05f);
            Place(lineVoyager, voyagerEnd.x - 0.19f, voyagerEnd.y - 0.01f);
            Place(axisY, 0.005f, 0.01f);
            Place(axisX, 0.78f, 0.9f);
        }

        static void Place(VisualElement element, float leftFraction, float topFraction)
        {
            if (element == null)
                return;

            element.style.left = Length.Percent(leftFraction * 100f);
            element.style.top = Length.Percent(topFraction * 100f);
        }

        // ---------- Desenho ----------

        void DrawOrbit(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (rect.width < 2f || rect.height < 2f)
                return;

            var painter = context.painter2D;
            var center = new Vector2(rect.width * OrbitCenter.x, rect.height * OrbitCenter.y);
            var maxRadius = Mathf.Min(rect.height * RingRadiusOfHeight, rect.width * 0.46f);
            painter.lineWidth = 2f;
            for (var ring = 0; ring < OutboundAtlas.OrbitRings; ring++)
            {
                painter.strokeColor = ring < current.OrbitsCrossed ? Ink : InkDim;
                painter.BeginPath();
                painter.Arc(center, maxRadius * RingFraction(ring), Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Stroke();
            }

            // O Sol: ponto cheio (rótulo ao lado).
            painter.fillColor = Amber;
            painter.BeginPath();
            painter.Arc(center, 6f, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();

            // Arco de saída: da órbita de Júpiter para fora, com ponta de seta; é a rota, não um sobrevoo.
            var direction = new Vector2(Mathf.Cos(ExitAngleDegrees * Mathf.Deg2Rad), Mathf.Sin(ExitAngleDegrees * Mathf.Deg2Rad));
            var start = center + direction * (maxRadius * RingFraction(0));
            var end = center + direction * (maxRadius * 1.16f);
            painter.strokeColor = Amber;
            painter.lineWidth = 4f;
            painter.BeginPath();
            painter.MoveTo(start);
            painter.LineTo(end);
            painter.Stroke();
            var side = new Vector2(-direction.y, direction.x);
            painter.BeginPath();
            painter.MoveTo(end);
            painter.LineTo(end - direction * 16f + side * 8f);
            painter.MoveTo(end);
            painter.LineTo(end - direction * 16f - side * 8f);
            painter.Stroke();

            // Marcador da sonda: quadrado sobre o arco, exatamente na distância orbital do cartão (Saturno em 1976, Netuno em 1983).
            if (current.OrbitsCrossed > 0)
            {
                var fraction = RingFraction(Mathf.Clamp(current.OrbitsCrossed, 1, OutboundAtlas.OrbitRings) - 1);
                LastProbeRadiusFraction = fraction;
                var at = center + direction * (maxRadius * fraction);
                painter.fillColor = Amber;
                painter.BeginPath();
                painter.MoveTo(at + new Vector2(-8f, -8f));
                painter.LineTo(at + new Vector2(8f, -8f));
                painter.LineTo(at + new Vector2(8f, 8f));
                painter.LineTo(at + new Vector2(-8f, 8f));
                painter.ClosePath();
                painter.Fill();
            }
        }

        void DrawChart(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (rect.width < 2f || rect.height < 2f)
                return;

            var painter = context.painter2D;
            Vector2 P(float t, float distance)
            {
                var p = ChartPoint(t, distance);
                return new Vector2(p.x * rect.width, p.y * rect.height);
            }

            // Eixos: distância ao Sol (vertical) e tempo (horizontal). Sem escala numérica.
            painter.strokeColor = Ink;
            painter.lineWidth = 2f;
            painter.BeginPath();
            painter.MoveTo(P(0f, 1f));
            painter.LineTo(P(0f, 0f));
            painter.LineTo(P(1f, 0f));
            painter.Stroke();

            // Pioneer 10: linha contínua com marcador quadrado. Voyager 1: tracejada com marcador circular (não depende de cor).
            painter.strokeColor = Amber;
            painter.lineWidth = 4f;
            painter.BeginPath();
            painter.MoveTo(P(0f, PioneerDistance(0f)));
            painter.LineTo(P(1f, PioneerDistance(1f)));
            painter.Stroke();

            painter.strokeColor = Cyan;
            const int dashes = 22;
            for (var i = 0; i < dashes; i += 2)
            {
                painter.BeginPath();
                painter.MoveTo(P(i / (float)dashes, VoyagerDistance(i / (float)dashes)));
                painter.LineTo(P((i + 1) / (float)dashes, VoyagerDistance((i + 1) / (float)dashes)));
                painter.Stroke();
            }

            // O instante em que a DISTÂNCIA da Voyager passa a da Pioneer (1998): marca vertical tracejada e círculo.
            painter.strokeColor = InkDim;
            painter.lineWidth = 2f;
            for (var i = 0; i < 12; i += 2)
            {
                painter.BeginPath();
                painter.MoveTo(Vector2.Lerp(P(CrossingT, 0f), P(CrossingT, 1f), i / 12f));
                painter.LineTo(Vector2.Lerp(P(CrossingT, 0f), P(CrossingT, 1f), (i + 1) / 12f));
                painter.Stroke();
            }

            var cross = P(CrossingT, PioneerDistance(CrossingT));
            painter.fillColor = Cyan;
            painter.BeginPath();
            painter.Arc(cross, 8f, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
            var pioneerEnd = P(1f, PioneerDistance(1f));
            painter.fillColor = Amber;
            painter.BeginPath();
            painter.MoveTo(pioneerEnd + new Vector2(-7f, -7f));
            painter.LineTo(pioneerEnd + new Vector2(7f, -7f));
            painter.LineTo(pioneerEnd + new Vector2(7f, 7f));
            painter.LineTo(pioneerEnd + new Vector2(-7f, 7f));
            painter.ClosePath();
            painter.Fill();
        }

        // ---------- Estado ----------

        public void Render(OutboundViewState state, Func<string, string> text)
        {
            if (root == null)
                Bind();
            if (root == null)
                return;

            current = state;
            var visible = state.Phase != OutboundPhase.Idle && state.Phase != OutboundPhase.Done;
            if (state.Phase != lastPhase)
            {
                lastPhase = state.Phase;
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                var showChart = state.Phase == OutboundPhase.Chart1998;
                Show(orbit, !showChart);
                Show(chart, showChart);
                Show(powerColumn, state.Phase == OutboundPhase.Power || showChart);
                Show(holdPlate, state.Phase == OutboundPhase.Power && !state.Cinema);
                Show(sunLabel, !showChart);
                foreach (var ring in ringLabels)
                    Show(ring, !showChart);
                Show(scheme, !showChart);
                Show(diagramTitle, !showChart);
                Show(note2, state.Phase == OutboundPhase.Neptune1983 || state.Phase == OutboundPhase.Power);
                Show(chartScheme, showChart);
                Show(axisY, showChart);
                Show(axisX, showChart);
                Show(linePioneer, showChart);
                Show(lineVoyager, showChart);
                orbit.MarkDirtyRepaint();
                chart.MarkDirtyRepaint();
            }
            else
            {
                orbit.MarkDirtyRepaint();
            }

            SetPlain(card, CardKey(state.Phase), text);
            SetPlain(note, NoteKey(state.Phase), text);
            SetPlain(note2, NotEdgeKey, text);
            SetPlain(diagramTitle, DistanceKey, text);
            SetPlain(powerTitle, PowerTitleKey, text);
            SetPlain(radioLabel, RadioKey, text);
            SetPlain(instrumentsLabel, InstrumentsKey, text);
            SetPlain(commLabel, CommTimeKey, text);
            SetPlain(sunLabel, SunKey, text);
            SetPlain(scheme, SchemeKey, text);
            SetPlain(chartScheme, SchemeKey, text);
            SetPlain(axisY, DistanceKey, text);
            SetPlain(axisX, TimeKey, text);
            SetPlain(linePioneer, Pioneer10Key, text);
            SetPlain(lineVoyager, Voyager1Key, text);
            for (var i = 0; i < ringLabels.Length; i++)
                SetPlain(ringLabels[i], RingKeys[i], text);
            SetPlain(holdLabel, OutboundAtlas.PromptKey, text);
            Show(powerOk, state.PowerConfirmed);
            SetPlain(powerOk, PowerOkKey, text);

            // Barras RELATIVAS, sem valores. O hold apaga os instrumentos secundários até ~35% (nunca zero) e enche o rádio.
            var p = state.PowerConfirmed ? 1f : state.HoldProgress;
            radioFill.style.width = Length.Percent(Mathf.Lerp(70f, 100f, p));
            var secondary = Mathf.Lerp(70f, 30f, p);
            for (var i = 0; i < fills.Length; i++)
                fills[i].style.width = Length.Percent(secondary - i * 6f * p);
            progressFill.style.width = Length.Percent(state.HoldProgress * 100f);
            commFill.style.width = Length.Percent(CommFraction(state.Phase) * 100f);
            SetHoldState(state.Hold);
        }

        /// <summary>Tempo de comunicação relativo: cresce com a distância e nunca mostra um valor.</summary>
        public static float CommFraction(OutboundPhase phase) => phase switch
        {
            OutboundPhase.Saturn1976 => 0.45f,
            OutboundPhase.Neptune1983 => 0.7f,
            OutboundPhase.Power => 0.7f,
            OutboundPhase.Chart1998 => 0.92f,
            _ => 0f,
        };

        public static string CardKey(OutboundPhase phase) => phase switch
        {
            OutboundPhase.Chart1998 => Card1998Key,
            OutboundPhase.Neptune1983 => Card1983Key,
            OutboundPhase.Power => Card1983Key,
            _ => Card1976Key,
        };

        public static string NoteKey(OutboundPhase phase) => phase switch
        {
            OutboundPhase.Chart1998 => Cross1998Key,
            OutboundPhase.Power => PastNeptuneKey,
            OutboundPhase.Neptune1983 => PastNeptuneKey,
            _ => PastSaturnKey,
        };

        void SetHoldState(OutboundHoldState hold)
        {
            if (hold == lastHold)
                return;

            lastHold = hold;
            for (var i = 0; i < HoldClasses.Length; i++)
                holdPlate.EnableInClassList(HoldClasses[i], i == (int)hold);
        }

        public System.Collections.Generic.List<string> ValidateSetup()
        {
            var errors = new System.Collections.Generic.List<string>();
            const string owner = "OutboundAtlasView";
            var doc = document != null ? document : GetComponent<UIDocument>();
            if (doc == null)
            {
                errors.Add($"{owner}: sem UIDocument.");
                return errors;
            }

            if (doc.panelSettings == null)
                errors.Add($"{owner}: UIDocument sem PanelSettings.");
            if (GetComponent<Abandoned.SpaceApps.UI.ResponsiveLayoutClass>() == null)
                errors.Add($"{owner}: falta ResponsiveLayoutClass (UI_SPEC: todo UIDocument de tela cheia).");
            if (doc.visualTreeAsset == null)
            {
                errors.Add($"{owner}: UIDocument sem UXML.");
                return errors;
            }

            var tree = doc.visualTreeAsset.CloneTree();
            var required = new System.Collections.Generic.List<string>
            {
                "outbound-root", "frame", "card", "note", "note-2", "power-col", "power-title", "radio-label", "instruments-label", "power-ok",
                "bar-radio", "fill-radio", "orbit", "chart", "sun-label", "scheme", "chart-scheme", "axis-y", "axis-x", "line-pioneer",
                "line-voyager", "diagram-title", "comm-label", "comm-fill", "hold", "hold-label", "progress-fill",
            };
            for (var i = 0; i < 4; i++)
            {
                required.Add("bar-" + i);
                required.Add("fill-" + i);
                required.Add("ring-" + i);
            }

            foreach (var name in required)
            {
                if (tree.Q(name) == null)
                    errors.Add($"{owner}: o UXML não tem o elemento '{name}'.");
            }

            return errors;
        }

        static void Show(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetPlain(TextElement element, string key, Func<string, string> text)
        {
            if (element == null)
                return;

            var value = text != null ? text(key) : null;
            element.text = string.IsNullOrEmpty(value) ? key : value;
        }
    }
}
