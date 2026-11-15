using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2 · HUD dos planos de cinema da Pioneer 10 (<c>UI/CinemaHud.uxml</c>). Só desenha o que um driver pede: o medidor de
    /// radiação com a estática da cena 5, a velocidade e a comparação de distâncias (Terra–Lua contra sonda–nuvens). Os textos vêm de
    /// chaves resolvidas pelo capítulo (sem frase literal no código). Nada aqui lê entrada.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CinemaHudView : MonoBehaviour
    {
        public const string RadiationKey = "PIO.UI.rad_title";
        public const string SpeedKey = "PIO.UI.speed_label";
        public const string MoonDistanceKey = "PIO.UI.dist_moon";
        public const string CloudsDistanceKey = "PIO.UI.dist_clouds";
        // Cena 7 · Os anos (as três primeiras chaves já existem no painel do atlas).
        public const string PastSaturnKey = "PIO.UI.past_saturn";
        public const string PastNeptuneKey = "PIO.UI.past_neptune";
        public const string NotEdgeKey = "PIO.UI.not_edge";
        public const string Cross1998Key = "PIO.UI.cross1998";
        public const string InstrumentsKey = "PIO.UI.instruments";
        public const string Card1983Key = "PIO.Card.y1983";
        public const string PastUranusKey = "PIO.UI.past_uranus";
        public const string EndTrackingKey = "PIO.UI.end_tracking";
        public const string EnergyKey = "PIO.UI.years_energy";
        public const string LaunchMarkKey = "PIO.UI.years_launch";
        public const string JupiterMarkKey = "PIO.UI.years_jupiter";
        public const string InstrumentCameraKey = "PIO.UI.inst_camera";
        public const string InstrumentRadiationKey = "PIO.UI.inst_radiation";
        public const string InstrumentPlasmaKey = "PIO.UI.inst_plasma";
        public const string InstrumentMeteoroidKey = "PIO.UI.inst_meteoroid";
        public const string InstrumentMagnetometerKey = "PIO.UI.inst_magnetometer";

        /// <summary>O que a cena 7 desenha: o ano e seu marco, os instrumentos, o medidor de energia e a posição na linha do tempo.</summary>
        public struct YearsState
        {
            public string Big, Event, Note, InstrumentsTitle, EnergyTitle, LaunchMark, JupiterMark;
            public string[] InstrumentNames;
            public bool[] InstrumentOn;
            public float YearFraction, EnergyFill;
        }

        [SerializeField] UIDocument document;
        [SerializeField, Tooltip("Ruído tileável da estática de radiação.")] Texture2D staticTexture;

        VisualElement root, staticLayer, radiation, radiationFill, speed, distances, moonFill, cloudsFill;
        Label radiationTitle, speedLabel, moonLabel, cloudsLabel;
        VisualElement years, energyFill, timelineTicks, timelineMarker, firsts, barTop, barBottom;
        readonly VisualElement[] firstStars = new VisualElement[PioneerFirstsHud.StarCount];
        readonly Label[] firstLabels = new Label[PioneerFirstsHud.StarCount];
        Label yearBig, yearEvent, yearNote, instrumentsTitle, energyTitle, energyLaunch, energyJupiter;
        readonly VisualElement[] instrumentRows = new VisualElement[5];
        readonly Label[] instrumentLabels = new Label[5];

        public bool IsBound => root != null;
        public VisualElement Root => root;

        void OnEnable() => Bind();

        /// <summary>Liga os elementos da árvore já criada. Testes e capturas podem chamar de novo depois de trocar o documento.</summary>
        public void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("hud-root") == null)
                return;

            root = tree.Q("hud-root");
            staticLayer = tree.Q("static");
            radiation = tree.Q("radiation");
            radiationFill = tree.Q("radiation-fill");
            radiationTitle = tree.Q<Label>("radiation-title");
            speed = tree.Q("speed");
            speedLabel = tree.Q<Label>("speed-label");
            distances = tree.Q("distances");
            moonFill = tree.Q("distance-moon-fill");
            cloudsFill = tree.Q("distance-clouds-fill");
            moonLabel = tree.Q<Label>("distance-moon-label");
            cloudsLabel = tree.Q<Label>("distance-clouds-label");
            years = tree.Q("years");
            barTop = tree.Q("bar-top");
            barBottom = tree.Q("bar-bottom");
            yearBig = tree.Q<Label>("year-big");
            yearEvent = tree.Q<Label>("year-event");
            yearNote = tree.Q<Label>("year-note");
            instrumentsTitle = tree.Q<Label>("instruments-title");
            energyTitle = tree.Q<Label>("energy-title");
            energyLaunch = tree.Q<Label>("energy-launch");
            energyJupiter = tree.Q<Label>("energy-jupiter");
            energyFill = tree.Q("energy-fill");
            timelineTicks = tree.Q("timeline-ticks");
            timelineMarker = tree.Q("timeline-marker");
            for (var i = 0; i < instrumentRows.Length; i++)
            {
                instrumentRows[i] = tree.Q("inst-" + i);
                instrumentLabels[i] = tree.Q<Label>("inst-label-" + i);
            }

            BuildTimeline();
            firsts = tree.Q("firsts");
            BuildFirsts();
            if (staticLayer != null && staticTexture != null)
            {
                staticLayer.style.backgroundImage = new StyleBackground(staticTexture);
                staticLayer.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
                staticLayer.style.backgroundSize = new BackgroundSize(staticTexture.width, staticTexture.height);
            }
        }

        /// <summary>
        /// Medidor de radiação (<paramref name="fill"/> de 0 a 1) e estática do quadro: <paramref name="staticOpacity"/> de 0 a 1 e um
        /// deslocamento <paramref name="staticOffset"/> em pixels que muda a cada quadro para o chuvisco tremer.
        /// </summary>
        public void SetRadiation(bool visible, string title, float fill, float staticOpacity, Vector2 staticOffset)
        {
            EnsureBound();
            Show(radiation, visible);
            if (visible)
            {
                if (radiationTitle != null && radiationTitle.text != title)
                    radiationTitle.text = title ?? string.Empty;
                if (radiationFill != null)
                    radiationFill.style.width = Length.Percent(Mathf.Clamp01(fill) * 100f);
            }

            var showStatic = visible && staticOpacity > 0.001f;
            Show(staticLayer, showStatic);
            if (showStatic && staticLayer != null)
            {
                staticLayer.style.opacity = Mathf.Clamp01(staticOpacity);
                staticLayer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, staticOffset.x);
                staticLayer.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, staticOffset.y);
            }
        }

        public void SetSpeed(bool visible, string text)
        {
            EnsureBound();
            Show(speed, visible);
            if (visible && speedLabel != null && speedLabel.text != text)
                speedLabel.text = text ?? string.Empty;
        }

        /// <summary>As duas barras de distância: Terra–Lua (cheia em <paramref name="moonFillFraction"/>) e sonda–nuvens (<paramref name="cloudsFillFraction"/>).</summary>
        public void SetDistances(bool visible, string moonText, string cloudsText, float moonFillFraction, float cloudsFillFraction)
        {
            EnsureBound();
            Show(distances, visible);
            if (!visible)
                return;

            if (moonLabel != null && moonLabel.text != moonText)
                moonLabel.text = moonText ?? string.Empty;
            if (cloudsLabel != null && cloudsLabel.text != cloudsText)
                cloudsLabel.text = cloudsText ?? string.Empty;
            if (moonFill != null)
                moonFill.style.width = Length.Percent(Mathf.Clamp01(moonFillFraction) * 100f);
            if (cloudsFill != null)
                cloudsFill.style.width = Length.Percent(Mathf.Clamp01(cloudsFillFraction) * 100f);
        }

        /// <summary>Cena 7: o ano em destaque com o marco, os ícones dos instrumentos, o medidor de energia dos RTG e a linha do tempo.</summary>
        public void SetYears(bool visible, in YearsState state)
        {
            EnsureBound();
            Show(years, visible);
            if (!visible || years == null)
                return;

            SetText(yearBig, state.Big);
            SetText(yearEvent, state.Event);
            SetText(yearNote, state.Note);
            Show(yearEvent, !string.IsNullOrEmpty(state.Event));
            Show(yearNote, !string.IsNullOrEmpty(state.Note));
            SetText(instrumentsTitle, state.InstrumentsTitle);
            SetText(energyTitle, state.EnergyTitle);
            SetText(energyLaunch, state.LaunchMark);
            SetText(energyJupiter, state.JupiterMark);
            for (var i = 0; i < instrumentRows.Length; i++)
            {
                if (state.InstrumentNames != null && i < state.InstrumentNames.Length)
                    SetText(instrumentLabels[i], state.InstrumentNames[i]);
                var on = state.InstrumentOn == null || i >= state.InstrumentOn.Length || state.InstrumentOn[i];
                if (instrumentRows[i] != null)
                    instrumentRows[i].EnableInClassList("pio-hud__inst--off", !on);
            }

            if (energyFill != null)
                energyFill.style.height = Length.Percent(Mathf.Clamp01(state.EnergyFill) * 100f);
            if (timelineMarker != null)
                timelineMarker.style.left = Length.Percent(Mathf.Clamp01(state.YearFraction) * 100f);
        }

        /// <summary>
        /// Cena 9: as seis estrelas das primeiras vezes. <paramref name="progress"/> (0–1 por estrela) é o quanto cada uma já acendeu e
        /// <paramref name="labels"/> os rótulos (vazio = apagada).
        /// </summary>
        public void SetFirsts(bool visible, string[] labels, float[] progress)
        {
            EnsureBound();
            Show(firsts, visible);
            if (!visible || firsts == null || labels == null || progress == null)
                return;

            for (var i = 0; i < firstStars.Length && i < labels.Length && i < progress.Length; i++)
            {
                var lit = progress[i] > 0.001f;
                Show(firstStars[i], lit);
                if (!lit)
                    continue;
                SetText(firstLabels[i], labels[i]);
                firstStars[i].style.scale = new Scale(new Vector3(progress[i], progress[i], 1f));
                firstStars[i].style.opacity = progress[i];
            }
        }

        /// <summary>Cria uma vez as seis estrelas (posição por <see cref="PioneerFirstsHud.ViewportOf"/>); o rótulo fica do lado de fora da elipse.</summary>
        void BuildFirsts()
        {
            if (firsts == null || firsts.childCount > 0)
                return;

            for (var i = 0; i < PioneerFirstsHud.StarCount; i++)
            {
                var position = PioneerFirstsHud.ViewportOf(i);
                var star = new VisualElement { name = "first-" + i, pickingMode = PickingMode.Ignore };
                star.AddToClassList("pio-hud__first");
                star.style.left = Length.Percent(position.x * 100f);
                star.style.top = Length.Percent(position.y * 100f);
                var glow = new VisualElement { pickingMode = PickingMode.Ignore };
                glow.AddToClassList("pio-hud__firststar");
                star.Add(glow);
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("pio-hud__firstlabel");
                label.AddToClassList(position.x < PioneerFirstsHud.EllipseCenter.x ? "pio-hud__firstlabel--left" : "pio-hud__firstlabel--right");
                star.Add(label);
                firsts.Add(star);
                firstStars[i] = star;
                firstLabels[i] = label;
            }
        }

        /// <summary>Cria uma vez os traços da linha do tempo (1973–1998), mais altos e com o número nos anos de marco.</summary>
        void BuildTimeline()
        {
            if (timelineTicks == null || timelineTicks.childCount > 0)
                return;

            for (var year = PioneerYearsHud.YearMin; year <= PioneerYearsHud.YearMax; year++)
            {
                var isEvent = System.Array.IndexOf(PioneerYearsHud.EventYears, year) >= 0;
                var left = (year - PioneerYearsHud.YearMin) / (float)(PioneerYearsHud.YearMax - PioneerYearsHud.YearMin) * 100f;
                var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                tick.AddToClassList("pio-hud__tick");
                if (isEvent)
                    tick.AddToClassList("pio-hud__tick--event");
                tick.style.left = Length.Percent(left);
                timelineTicks.Add(tick);
                if (!isEvent)
                    continue;
                var label = new Label(year.ToString(System.Globalization.CultureInfo.InvariantCulture)) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("pio-hud__ticklabel");
                label.style.left = Length.Percent(left);
                timelineTicks.Add(label);
            }
        }

        static void SetText(Label label, string value)
        {
            if (label != null && label.text != (value ?? string.Empty))
                label.text = value ?? string.Empty;
        }

        /// <summary>
        /// Fase 5: as barras de cinema. <paramref name="progress"/> (0 recolhidas, 1 fechadas) multiplica <paramref name="barFraction"/>, a altura
        /// de cada barra como fração do quadro. Não é apagada por <see cref="HideAll"/>: as barras têm dono próprio (<see cref="PioneerLetterbox"/>).
        /// </summary>
        public void SetLetterbox(float progress, float barFraction)
        {
            EnsureBound();
            var visible = progress > 0.001f;
            Show(barTop, visible);
            Show(barBottom, visible);
            if (!visible)
                return;

            var height = Length.Percent(Mathf.Clamp01(progress) * barFraction * 100f);
            if (barTop != null)
                barTop.style.height = height;
            if (barBottom != null)
                barBottom.style.height = height;
        }

        /// <summary>Esconde tudo (fora dos planos de cinema que usam o HUD).</summary>
        public void HideAll()
        {
            if (root == null)
                return;
            Show(radiation, false);
            Show(staticLayer, false);
            Show(speed, false);
            Show(distances, false);
            Show(years, false);
            Show(firsts, false);
        }

        void EnsureBound()
        {
            if (root == null)
                Bind();
        }

        static void Show(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
