using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// PIO-005 · Painel UI Toolkit do cinturão (<c>UI/BeltPanel.uxml</c>): mapa, moldura da fantasia com selo,
    /// arena, e o instrumento com cinco registros numerados. Só desenha o <see cref="BeltViewState"/>; textos vêm de
    /// chaves resolvidas pelo capítulo (sem frase literal no código) e os cliques voltam como eventos.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class BeltPanelView : MonoBehaviour
    {
        public const string SealKey = "PIO.UI.imagination";
        public const string MoveKey = "PIO.UI.move";
        public const string DustKey = "PIO.UI.dust";
        public const string HelpKey = "PIO.UI.help";
        /// <summary>Roteiro v2, cena 2: "Dia {count} / 215", os meses abreviados numa só chave e o rótulo da pedra solitária.</summary>
        public const string DayKey = "PIO.UI.belt_day";
        public const string MonthsKey = "PIO.UI.belt_months";
        public const string NearestKey = "PIO.UI.belt_nearest";

        [SerializeField] UIDocument document;

        VisualElement root, map, fantasy, record, probe, arrow, help, holdPlate, progressFill;
        VisualElement counterPlate, nikeLine;
        Label counterDay, counterDate, nikeLabel;
        VisualElement[] rocks, slots;
        Label sealLabel, moveLabel, dustLabel;
        Button helpButton, recordButton;
        string lastSeal, lastMove, lastDust, lastHelp;
        BeltPhase lastPhase = (BeltPhase)(-1);
        int lastDustCount = -1;

        public event Action RecordClicked;
        public event Action HelpClicked;

        public bool IsBound => root != null;
        public VisualElement Root => root;

        void OnEnable() => Bind();

        void OnDisable() => Unbind();

        /// <summary>Liga os elementos do UXML. Idempotente; chamado no OnEnable e pela prévia de Editor.</summary>
        public void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("belt-root") == null)
                return;
            if (root == tree.Q("belt-root"))
                return;

            Unbind();
            root = tree.Q("belt-root");
            map = root.Q("map");
            fantasy = root.Q("fantasy");
            record = root.Q("record");
            probe = root.Q("probe");
            arrow = root.Q("arrow");
            help = root.Q("help");
            holdPlate = root.Q("hold");
            progressFill = root.Q("progress-fill");
            rocks = new[] { root.Q("rock-0"), root.Q("rock-1"), root.Q("rock-2") };
            slots = new[] { root.Q("slot-0"), root.Q("slot-1"), root.Q("slot-2"), root.Q("slot-3"), root.Q("slot-4") };
            sealLabel = root.Q<Label>("seal-label");
            moveLabel = root.Q<Label>("move-label");
            dustLabel = root.Q<Label>("dust-label");
            helpButton = root.Q<Button>("help-button");
            recordButton = root.Q<Button>("record-button");
            if (helpButton != null)
                helpButton.clicked += OnHelp;
            if (recordButton != null)
                recordButton.clicked += OnRecord;
            counterPlate = tree.Q("counter");
            counterDay = tree.Q<Label>("counter-day");
            counterDate = tree.Q<Label>("counter-date");
            nikeLine = tree.Q("nike-line");
            nikeLabel = tree.Q<Label>("nike-label");
            lastSeal = lastMove = lastDust = lastHelp = null;
            lastPhase = (BeltPhase)(-1);
            lastDustCount = -1;
        }

        void Unbind()
        {
            if (helpButton != null)
                helpButton.clicked -= OnHelp;
            if (recordButton != null)
                recordButton.clicked -= OnRecord;
            root = null;
        }

        /// <summary>
        /// Cena 2 (cinema): o cartão com o dia e a data (<paramref name="day"/> vazio = só a data) e, se pedido, o rótulo da pedra solitária
        /// com uma linha fina que sai do ponto <paramref name="nikeViewport"/> (0–1, origem em cima à esquerda).
        /// </summary>
        public void SetCounter(bool visible, string day, string date, bool nikeVisible, string nikeText, Vector2 nikeViewport)
        {
            if (root == null)
                Bind();
            if (counterPlate == null)
                return;

            Show(counterPlate, visible);
            if (visible)
            {
                Show(counterDay, !string.IsNullOrEmpty(day));
                if (counterDay.text != day)
                    counterDay.text = day ?? string.Empty;
                if (counterDate.text != date)
                    counterDate.text = date ?? string.Empty;
            }

            Show(nikeLine, nikeVisible);
            Show(nikeLabel, nikeVisible);
            if (!nikeVisible)
                return;

            if (nikeLabel.text != nikeText)
                nikeLabel.text = nikeText ?? string.Empty;
            nikeLine.style.left = Length.Percent(nikeViewport.x * 100f);
            nikeLine.style.top = Length.Percent(nikeViewport.y * 100f + 1.5f);
            nikeLabel.style.left = Length.Percent(nikeViewport.x * 100f - 9f);
            nikeLabel.style.top = Length.Percent(nikeViewport.y * 100f + 7.5f);
        }

        void OnHelp() => HelpClicked?.Invoke();

        void OnRecord() => RecordClicked?.Invoke();

        public void Render(BeltViewState state, Func<string, string> text)
        {
            if (root == null)
                Bind();
            if (root == null)
                return;

            if (state.Phase != lastPhase)
            {
                lastPhase = state.Phase;
                Show(map, state.Phase == BeltPhase.Map);
                Show(fantasy, state.Phase == BeltPhase.Fantasy);
                Show(record, state.Phase == BeltPhase.Record);
            }

            if (state.Phase == BeltPhase.Fantasy)
            {
                Show(sealLabel?.parent, state.SealVisible);
                Place(probe, state.Probe);
                Place(rocks[0], state.RockA);
                Place(rocks[1], state.RockB);
                Place(rocks[2], state.RockC);
                probe.EnableInClassList("pio-belt__probe--contact", state.ProbeDeformed);
                arrow.style.rotate = new Rotate(new Angle(state.ArrowDirection * 90f, AngleUnit.Degree));
                SetText(sealLabel, SealKey, text, ref lastSeal);
                SetText(moveLabel, MoveKey, text, ref lastMove);
            }
            else if (state.Phase == BeltPhase.Record)
            {
                for (var i = 0; i < slots.Length; i++)
                {
                    slots[i].EnableInClassList("pio-belt__slot--done", i < state.Recorded);
                    slots[i].EnableInClassList("pio-belt__slot--focus", i == state.Focus);
                }

                progressFill.style.width = Length.Percent(state.FocusProgress * 100f);
                holdPlate.EnableInClassList("sa-hold--holding", state.Acquiring && state.Recorded < BeltInvestigation.RecordCount);
                holdPlate.EnableInClassList("sa-hold--done", state.Recorded >= BeltInvestigation.RecordCount);
                holdPlate.EnableInClassList("pio-belt__hold--pulse", state.HelpLevel >= 1 && !state.Acquiring);
                Show(help, state.HelpLevel >= 2 && !state.Assisted && state.Recorded < BeltInvestigation.RecordCount);
                if (state.Recorded != lastDustCount) // monta o texto só quando o número muda (sem alocação por quadro)
                {
                    lastDustCount = state.Recorded;
                    SetText(dustLabel, DustKey, text, ref lastDust, state.Recorded);
                }
                if (helpButton != null)
                    SetText(helpButton, HelpKey, text, ref lastHelp);
            }
        }

        static void Show(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void Place(VisualElement element, Vector2 normalized)
        {
            if (element == null)
                return;

            element.style.left = Length.Percent(normalized.x * 100f);
            element.style.top = Length.Percent(normalized.y * 100f);
        }

        /// <summary>Texto por chave; "{count}" recebe o número. Sem resolvedor, mostra a chave (nunca uma frase inventada).</summary>
        static void SetText(TextElement element, string key, Func<string, string> text, ref string last, int count = -1)
        {
            if (element == null)
                return;

            var value = text != null ? text(key) : null;
            if (string.IsNullOrEmpty(value))
                value = key;
            if (count >= 0)
                value = value.Replace("{count}", count.ToString());
            if (value == last)
                return;

            last = value;
            element.text = value;
        }
    }
}
