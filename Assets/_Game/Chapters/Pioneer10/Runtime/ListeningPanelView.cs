using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// PIO-011 · Painel UI Toolkit da escuta (<c>UI/ListeningPanel.uxml</c>): forma da onda, escala de sintonia com a zona de 30%,
    /// botão grande, setas e medidor de proximidade (equivalentes visuais do som) e o resultado de cada contato. Só desenha o
    /// <see cref="ListeningViewState"/>; textos vêm de chaves resolvidas pelo capítulo. A onda é de barras FIXAS: o desenho muda
    /// com o estado, nunca com o tempo (sem ruído animado nem chiado em laço).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ListeningPanelView : MonoBehaviour
    {
        public const string NearerKey = "PIO.UI.nearer";
        public const string FartherKey = "PIO.UI.farther";
        public const string ReceivedKey = "PIO.UI.received";
        public const string NoSignalKey = "PIO.UI.no_signal";
        public const string NoFaultKey = "PIO.UI.no_fault";
        public const string KindDataKey = "PIO.UI.kind_data";
        public const string KindCarrierKey = "PIO.UI.kind_carrier";
        public const string HelpKey = "PIO.UI.help";
        public const string Contact1CardKey = "PIO.Card.contact1";
        public const string Contact2CardKey = "PIO.Card.contact2";
        public const string Contact3CardKey = "PIO.Card.contact3";
        public const string Attempt4CardKey = "PIO.Card.attempt4";
        public const int WaveBars = 28;

        static readonly string[] HoldClasses = { "sa-hold--available", "sa-hold--rearm", "sa-hold--holding", "sa-hold--paused", "sa-hold--done" };

        [SerializeField] UIDocument document;

        VisualElement root, head, wave, tuner, prox, holdPlate, zone, knob, arrowLeft, arrowRight, proxFill, progressFill, scale;
        VisualElement[] bars;
        Label card, status, note, proxLabel, assist, holdLabel;
        float lastProximity = -1f;
        string lastTrend = NearerKey;
        ListeningPhase lastPhase = (ListeningPhase)(-1);
        ListeningHoldState lastHold = (ListeningHoldState)(-1);

        public bool IsBound => root != null;
        public VisualElement Root => root;

        void OnEnable() => Bind();

        void OnDisable() => root = null;

        public void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("listening-root") == null || root == tree.Q("listening-root"))
                return;

            // O contêiner do UIDocument só tem filhos absolutos e ficaria com altura zero (percentuais relativos a nada).
            tree.style.position = Position.Absolute;
            tree.style.left = 0f;
            tree.style.top = 0f;
            tree.style.right = 0f;
            tree.style.bottom = 0f;
            tree.pickingMode = PickingMode.Ignore;

            root = tree.Q("listening-root");
            head = root.Q("head");
            wave = root.Q("wave");
            tuner = root.Q("tuner");
            prox = root.Q("prox");
            holdPlate = root.Q("hold");
            zone = root.Q("zone");
            knob = root.Q("knob");
            scale = root.Q("scale");
            arrowLeft = root.Q("arrow-left");
            arrowRight = root.Q("arrow-right");
            proxFill = root.Q("prox-fill");
            progressFill = root.Q("progress-fill");
            card = root.Q<Label>("card");
            status = root.Q<Label>("status");
            note = root.Q<Label>("note");
            proxLabel = root.Q<Label>("prox-label");
            assist = root.Q<Label>("assist");
            holdLabel = root.Q<Label>("hold-label");

            wave.Clear();
            bars = new VisualElement[WaveBars];
            for (var i = 0; i < bars.Length; i++)
            {
                bars[i] = new VisualElement { name = "bar-" + i, pickingMode = PickingMode.Ignore };
                bars[i].AddToClassList("pio-lis__bar");
                wave.Add(bars[i]);
            }

            lastProximity = -1f;
            lastTrend = NearerKey;
            lastPhase = (ListeningPhase)(-1);
            lastHold = (ListeningHoldState)(-1);
        }

        /// <summary>
        /// Altura (0–1) da barra <paramref name="index"/>. Função pura do estado: contato 1 = onda suave; contato 2 = blocos
        /// regulares (DADOS de telemetria); contato 3 = uma linha fina e fraca no centro (só a PORTADORA, sem dados novos);
        /// tentativa 4 e respiração = tudo mínimo (nada recebido). Antes do resultado, a amplitude segue a proximidade.
        /// </summary>
        public static float WaveHeight(ListeningViewState state, int index)
        {
            const float floor = 0.06f;
            var t = index / (float)(WaveBars - 1);
            var smooth = 0.5f + 0.5f * Mathf.Sin(t * 5.2f * Mathf.PI);
            switch (state.Result)
            {
                case ListeningResult.Signal:
                    return Mathf.Lerp(floor, 1f, 0.35f + 0.65f * smooth);
                case ListeningResult.Data:
                    return Mathf.Lerp(floor, 0.9f, index % 4 < 2 ? 0.95f : 0.35f);
                case ListeningResult.Carrier:
                    return Mathf.Abs(index - WaveBars / 2) <= 0 ? 0.55f : floor;
                case ListeningResult.NoSignal:
                    return floor;
            }

            if (state.Phase == ListeningPhase.Attempt4 || state.ControlsCollapsed || state.Phase == ListeningPhase.Idle)
                return floor;

            var amplitude = state.Proximity * state.Signal;
            return Mathf.Lerp(floor, 0.95f, amplitude * (0.35f + 0.65f * smooth));
        }

        public void Render(ListeningViewState state, Func<string, string> text)
        {
            if (root == null)
                Bind();
            if (root == null)
                return;

            if (state.Phase != lastPhase)
            {
                lastPhase = state.Phase;
                var visible = state.Phase != ListeningPhase.Idle && state.Phase != ListeningPhase.Done;
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // Controles: recolhem na respiração (45–52 s). Zona, setas e medidor só existem onde há zona (nunca na tentativa 4).
            Show(tuner, !state.ControlsCollapsed);
            Show(holdPlate, !state.ControlsCollapsed);
            Show(prox, !state.ControlsCollapsed && state.ZoneVisible);
            Show(zone, state.ZoneVisible);
            zone.EnableInClassList("pio-lis__zone--in", state.InZone);
            if (state.ZoneVisible)
            {
                zone.style.left = Length.Percent(state.ZoneStart * 100f);
                zone.style.width = Length.Percent((state.ZoneEnd - state.ZoneStart) * 100f);
            }

            knob.style.left = Length.Percent(state.Knob * 100f);
            Show(arrowLeft, state.ZoneVisible && state.Direction < 0);
            Show(arrowRight, state.ZoneVisible && state.Direction > 0);
            proxFill.style.width = Length.Percent(state.Proximity * 100f);
            progressFill.style.width = Length.Percent(state.Listen * 100f);
            SetHoldState(state.Hold);

            for (var i = 0; i < bars.Length; i++)
                bars[i].style.height = Length.Percent(Mathf.Max(0.06f, WaveHeight(state, i)) * 100f);

            // Proximidade: "mais perto/mais longe" só quando a distância muda; parado, mantém o último.
            if (lastProximity >= 0f && state.ZoneVisible)
            {
                if (state.Proximity > lastProximity + 0.0005f)
                    lastTrend = NearerKey;
                else if (state.Proximity < lastProximity - 0.0005f)
                    lastTrend = FartherKey;
            }

            lastProximity = state.Proximity;
            SetText(proxLabel, lastTrend, text);
            SetText(holdLabel, state.Phase == ListeningPhase.Attempt4 ? PioneerListening.ListenPromptKey : PioneerListening.TunePromptKey, text);
            Show(assist, state.Assisting);
            SetText(assist, HelpKey, text);

            SetText(card, CardKey(state), text);
            Show(head, state.Result != ListeningResult.None); // antes do resultado, o cartão de instrução fica no próprio botão de E
            SetText(status, StatusKey(state), text);
            Show(status, state.Result != ListeningResult.NoSignal); // o cartão já diz "nenhum sinal detectado"
            SetText(note, NoFaultKey, text);
            Show(note, state.Result == ListeningResult.NoSignal);
        }

        public static string CardKey(ListeningViewState state) => state.Result switch
        {
            ListeningResult.Signal => Contact1CardKey,
            ListeningResult.Data => Contact2CardKey,
            ListeningResult.Carrier => Contact3CardKey,
            ListeningResult.NoSignal => Attempt4CardKey,
            _ => null,
        };

        /// <summary>O que chegou: quatro rótulos DIFERENTES (sinal, dados, portadora sem dados, nada). Sem resultado, a instrução.</summary>
        public static string StatusKey(ListeningViewState state) => state.Result switch
        {
            ListeningResult.Signal => ReceivedKey,
            ListeningResult.Data => KindDataKey,
            ListeningResult.Carrier => KindCarrierKey,
            ListeningResult.NoSignal => NoSignalKey,
            _ => state.Phase == ListeningPhase.Attempt4 ? PioneerListening.ListenPromptKey : PioneerListening.TunePromptKey,
        };

        void SetHoldState(ListeningHoldState hold)
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
            const string owner = "ListeningPanelView";
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
            foreach (var name in new[]
            {
                "listening-root", "frame", "head", "card", "status", "note", "wave", "tuner", "arrow-left", "arrow-right", "scale", "zone", "knob",
                "prox", "prox-label", "prox-fill", "assist", "hold", "hold-label", "progress-fill",
            })
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

        static void SetText(TextElement element, string key, Func<string, string> text)
        {
            if (element == null)
                return;

            if (string.IsNullOrEmpty(key))
            {
                element.text = string.Empty;
                return;
            }

            var value = text != null ? text(key) : null;
            element.text = string.IsNullOrEmpty(value) ? key : value;
        }
    }
}
