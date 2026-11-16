using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// UI-004 · Apresenta as legendas do <see cref="SequenceDirector"/>: uma ideia por cue, pelo tempo que o diretor (janela de tempo ou
    /// gatilho com leitura mínima) decide. Texto vazio ou ausente no idioma atual ESCONDE a placa inteira (nenhuma caixa vazia nem
    /// linha decorativa). Cartões de data e nome (grupo <c>card</c>) usam a hierarquia de carimbo (Plex Mono, destaque), distinta da
    /// legenda. A troca de idioma só redesenha o texto atual: o cue, a etapa e o tempo de leitura continuam do diretor.
    /// <para>
    /// Instalação: dentro do HUD, <c>presenter.Initialize(localization, director, missionHud.BottomColumn)</c> (a legenda entra logo
    /// abaixo do prompt de E, sem sobreposição por construção); sem HUD, use o prefab <c>SubtitleLayer</c> (documento próprio na base).
    /// </para>
    /// Vários cues ativos ao mesmo tempo (um gatilho sobre uma janela): vale a maior prioridade e, em empate, o mais recente; quando ele
    /// sai, volta o anterior que ainda estiver ativo. Cada posição (base/topo) tem a sua placa.
    /// </summary>
    public sealed class SubtitlePresenter : MonoBehaviour
    {
        public const string HiddenClass = "sa-subtitle--hidden";
        public const string StampClass = "pio-subtitle--stamp";
        const string LayerClass = "sa-subtitle-layer";
        const string LayerHostedClass = "sa-subtitle-layer--hosted";

        sealed class Active
        {
            public SubtitleCue Cue;
            public int Order;
        }

        sealed class Plate
        {
            public VisualElement Root;
            public Label Text;
            public Label Stamp;
            public string TextKey;
            public bool Stamped;
        }

        [SerializeField] UIDocument document;

        readonly List<Active> active = new List<Active>();
        readonly Plate[] plates = new Plate[2];
        LocalizationService localization;
        SequenceDirector director;
        VisualElement layer;
        string stampKey;
        int counter;
        string currentBodyKey;
        public string CurrentBodyKey => currentBodyKey;
        /// <summary>NAR-001: only the visible narrative key changes; translating a cue does not restart its recording.</summary>
        public event Action<string> BodyKeyChanged;

        public bool IsBound => layer != null;
        public VisualElement Layer => layer;
        public int ActiveCueCount => active.Count;
        /// <summary>Quantas vezes o texto da placa da base mudou (para testes: nada redesenha sem motivo).</summary>
        public int RenderCount { get; private set; }

        /// <summary>Id do cue exibido na base (nulo se a placa está escondida).</summary>
        public string CurrentCueId => PlateVisible(SubtitlePosition.Bottom) ? Top(SubtitlePosition.Bottom)?.Cue.CueId : null;
        public string CurrentText => PlateVisible(SubtitlePosition.Bottom) ? plates[(int)SubtitlePosition.Bottom].Text.text : string.Empty;
        public string CurrentStamp => plates[(int)SubtitlePosition.Bottom]?.Stamp.text ?? string.Empty;
        public bool IsVisible(SubtitlePosition position) => PlateVisible(position);

        /// <summary>
        /// Liga o apresentador. <paramref name="host"/> é a coluna onde a placa entra (ex.: <c>MissionHUD.BottomColumn</c>); sem ele usa o
        /// documento próprio. Chamar de novo religa tudo e limpa o que estava na tela.
        /// </summary>
        public void Initialize(LocalizationService localizationService, SequenceDirector sequenceDirector, VisualElement host = null)
        {
            Unbind();
            localization = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            BuildLayer(host);
            localization.LanguageChanged += OnLanguageChanged;
            Bind(sequenceDirector);
        }

        /// <summary>Troca de diretor de sequência (ex.: cada estado de um capítulo), sem recriar a UI.</summary>
        public void Bind(SequenceDirector sequenceDirector)
        {
            UnbindDirector();
            Clear();
            director = sequenceDirector;
            if (director == null)
                return;

            director.CueEntered += OnCueEntered;
            director.CueExited += OnCueExited;
            director.Finished += OnFinished;
        }

        void UnbindDirector()
        {
            if (director == null)
                return;

            director.CueEntered -= OnCueEntered;
            director.CueExited -= OnCueExited;
            director.Finished -= OnFinished;
            director = null;
        }

        void Unbind()
        {
            PublishBodyKey(null);
            UnbindDirector();
            if (localization != null)
                localization.LanguageChanged -= OnLanguageChanged;
            localization = null;
            layer?.RemoveFromHierarchy();
            layer = null;
            plates[0] = plates[1] = null;
            active.Clear();
        }

        void BuildLayer(VisualElement host)
        {
            VisualElement parent = host;
            if (parent == null)
            {
                if (document == null)
                    document = GetComponent<UIDocument>();
                var tree = document != null ? document.rootVisualElement : null;
                if (tree == null)
                    throw new InvalidOperationException("SubtitlePresenter sem host e sem UIDocument: passe a coluna do HUD ou use o prefab SubtitleLayer.");
                // O contêiner do UIDocument só tem filhos absolutos e ficaria com altura zero.
                tree.style.position = Position.Absolute;
                tree.style.left = 0f;
                tree.style.top = 0f;
                tree.style.right = 0f;
                tree.style.bottom = 0f;
                tree.pickingMode = PickingMode.Ignore;
                // As folhas de estilo do UXML valem para o elemento raiz dele: a camada entra em "subtitle-host".
                parent = tree.Q("subtitle-host") ?? tree;
            }

            layer = new VisualElement { name = "subtitle-layer", pickingMode = PickingMode.Ignore };
            layer.AddToClassList("sa-root");
            layer.AddToClassList(LayerClass);
            if (host != null)
                layer.AddToClassList(LayerHostedClass);
            plates[(int)SubtitlePosition.Top] = MakePlate("subtitle-top");
            plates[(int)SubtitlePosition.Top].Root.AddToClassList("pio-subtitle--top");
            plates[(int)SubtitlePosition.Bottom] = MakePlate("subtitle");
            layer.Add(plates[(int)SubtitlePosition.Top].Root);
            layer.Add(plates[(int)SubtitlePosition.Bottom].Root);
            parent.Add(layer);
        }

        static Plate MakePlate(string name)
        {
            var plate = new Plate { Root = new VisualElement { name = name, pickingMode = PickingMode.Ignore } };
            plate.Root.AddToClassList("sa-subtitle");
            plate.Root.AddToClassList(HiddenClass);
            // Carimbo (data/nome): Plex Mono, em destaque, sobre o corpo. Escondido quando não há carimbo.
            plate.Stamp = new Label { name = name + "-stamp", pickingMode = PickingMode.Ignore };
            plate.Stamp.AddToClassList("sa-stamp");
            plate.Stamp.AddToClassList("sa-stamp--hud");
            plate.Stamp.AddToClassList("pio-subtitle__stamp");
            plate.Stamp.style.display = DisplayStyle.None;
            plate.Text = new Label { name = name + "-text", pickingMode = PickingMode.Ignore };
            plate.Text.AddToClassList("sa-subtitle__text");
            plate.Root.Add(plate.Stamp);
            plate.Root.Add(plate.Text);
            return plate;
        }

        // ---------- Eventos ----------

        void OnCueEntered(SubtitleCue cue) => ShowCue(cue);

        void OnCueExited(SubtitleCue cue) => HideCue(cue);

        void OnFinished(SequenceResult result) => Clear();

        void OnLanguageChanged(string code) => RenderAll();

        /// <summary>Um cue entrou (público para testes e para quem apresenta legendas sem diretor).</summary>
        public void ShowCue(SubtitleCue cue)
        {
            if (cue == null || layer == null)
                return;

            active.RemoveAll(a => a.Cue == cue);
            active.Add(new Active { Cue = cue, Order = ++counter });
            Render(cue.Position);
        }

        public void HideCue(SubtitleCue cue)
        {
            if (cue == null || layer == null)
                return;

            if (active.RemoveAll(a => a.Cue == cue) > 0)
                Render(cue.Position);
        }

        /// <summary>Esconde tudo (fim, cancelamento, troca de capítulo). Nada fica na tela.</summary>
        public void Clear()
        {
            active.Clear();
            stampKey = null;
            if (layer != null)
                RenderAll();
        }

        /// <summary>Carimbo avulso (data e lugar de uma cena) acima do corpo da legenda da base. Chave vazia ou sem texto não mostra nada.</summary>
        public void SetStamp(string key)
        {
            stampKey = key;
            if (layer != null)
                Render(SubtitlePosition.Bottom);
        }

        public void ClearStamp() => SetStamp(null);

        void RenderAll()
        {
            Render(SubtitlePosition.Bottom);
            Render(SubtitlePosition.Top);
        }

        Active Top(SubtitlePosition position)
        {
            Active best = null;
            foreach (var entry in active)
            {
                if (entry.Cue.Position != position)
                    continue;
                if (best == null || entry.Cue.Priority > best.Cue.Priority || entry.Cue.Priority == best.Cue.Priority && entry.Order > best.Order)
                    best = entry;
            }

            return best;
        }

        bool PlateVisible(SubtitlePosition position)
        {
            var plate = plates[(int)position];
            return plate != null && !plate.Root.ClassListContains(HiddenClass);
        }

        void Render(SubtitlePosition position)
        {
            var plate = plates[(int)position];
            if (plate == null || localization == null)
                return;

            var top = Top(position);
            string body = string.Empty;
            string stamp = string.Empty;
            string key = null;
            var stamped = false;
            if (top != null)
            {
                key = top.Cue.TextKey;
                var text = localization.Get(key);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (localization.IsStamp(key))
                    {
                        stamp = text;
                        stamped = true;
                    }
                    else
                    {
                        body = text;
                    }
                }
            }

            if (position == SubtitlePosition.Bottom && !string.IsNullOrEmpty(stampKey))
            {
                var extra = localization.Get(stampKey);
                if (!string.IsNullOrWhiteSpace(extra))
                    stamp = string.IsNullOrEmpty(stamp) ? extra : stamp;
            }

            var visible = !string.IsNullOrWhiteSpace(body) || !string.IsNullOrWhiteSpace(stamp);
            // Placa inteira: sem texto nem carimbo, nada do fundo ou do contorno fica na tela.
            plate.Root.EnableInClassList(HiddenClass, !visible);
            plate.Root.EnableInClassList(StampClass, stamped && string.IsNullOrWhiteSpace(body));
            plate.Text.style.display = string.IsNullOrWhiteSpace(body) ? DisplayStyle.None : DisplayStyle.Flex;
            plate.Stamp.style.display = string.IsNullOrWhiteSpace(stamp) ? DisplayStyle.None : DisplayStyle.Flex;
            if (plate.Text.text != body || plate.Stamp.text != stamp)
            {
                plate.Text.text = body;
                plate.Stamp.text = stamp;
                if (position == SubtitlePosition.Bottom)
                    RenderCount++;
            }

            plate.TextKey = key;
            plate.Stamped = stamped;
            if (position == SubtitlePosition.Bottom)
                PublishBodyKey(string.IsNullOrWhiteSpace(body) ? null : key);
        }

        void PublishBodyKey(string key)
        {
            if (currentBodyKey == key) return;
            currentBodyKey = key; BodyKeyChanged?.Invoke(key);
        }

        void OnDestroy() => Unbind();
    }
}
