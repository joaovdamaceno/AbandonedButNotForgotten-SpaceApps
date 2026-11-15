using System;
using System.Collections.Generic;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>Quanto a orientação já subiu depois da última ação observada. Nunca desce sozinha: só uma ação real zera.</summary>
    public enum GuidanceLevel
    {
        /// <summary>Instrução normal.</summary>
        None = 0,

        /// <summary>12 s ativos sem progresso: a instrução ganha destaque e uma dica mais direta.</summary>
        Reinforce = 1,

        /// <summary>30 s ativos sem progresso: oferece H, enquadramento ou retorno ao corredor. Nada é feito pela pessoa.</summary>
        Offer = 2,
    }

    /// <summary>
    /// Relógio de ociosidade da orientação, sem Unity. Soma só tempo ATIVO (zero na pausa, no loading e no dossiê) e sobe
    /// no máximo um nível por vez; depois de <see cref="GuidanceLevel.Offer"/> fica nele até haver progresso. Por isso a
    /// ajuda não entra em loop: cada nível é anunciado uma única vez por período sem progresso.
    /// </summary>
    public sealed class GuidanceTimeline
    {
        public const float ReinforceSeconds = 12f;
        public const float OfferSeconds = 30f;

        public float IdleSeconds { get; private set; }

        public GuidanceLevel Level =>
            IdleSeconds >= OfferSeconds ? GuidanceLevel.Offer
            : IdleSeconds >= ReinforceSeconds ? GuidanceLevel.Reinforce
            : GuidanceLevel.None;

        /// <summary>Soma tempo ativo (valores negativos, NaN ou infinitos são ignorados). Devolve o nível resultante.</summary>
        public GuidanceLevel Advance(float activeDeltaSeconds)
        {
            if (activeDeltaSeconds > 0f && !float.IsInfinity(activeDeltaSeconds))
                IdleSeconds += activeDeltaSeconds;

            return Level;
        }

        /// <summary>Uma ação real aconteceu: a contagem recomeça e a orientação volta ao nível normal.</summary>
        public void NoteProgress() => IdleSeconds = 0f;
    }

    /// <summary>
    /// Mede uma ação até a meta. Acumula (nunca zera por soltar a tecla) e ignora quantidades inválidas. Usado para
    /// "mover" (segundos de movimento ou metros efetivos, se o capítulo informa) sem criar outra lógica de conclusão.
    /// </summary>
    public sealed class ActionMeter
    {
        readonly float goal;

        public ActionMeter(float goal)
        {
            if (!(goal > 0f) || float.IsInfinity(goal))
                throw new ArgumentOutOfRangeException(nameof(goal), "A meta deve ser positiva e finita.");

            this.goal = goal;
        }

        public float Goal => goal;
        public float Amount { get; private set; }
        public bool Reached => Amount >= goal;

        /// <summary>Soma a quantidade medida. Devolve true só no quadro em que a meta é atingida.</summary>
        public bool Add(float amount)
        {
            if (Reached || !(amount > 0f) || float.IsInfinity(amount))
                return false;

            Amount += amount;
            return Reached;
        }

        public void Reset() => Amount = 0f;
    }

    /// <summary>Uma instrução do tutorial ou da assistência de uma atividade. Só textos por chave: nenhuma frase em C#.</summary>
    public sealed class GuidanceStep
    {
        public GuidanceStep(string id, string glyphKey, string textKey, string reinforceKey,
            string textKeyToggle = null, string reinforceKeyToggle = null)
        {
            Id = id;
            GlyphKey = glyphKey;
            TextKey = textKey;
            ReinforceKey = reinforceKey;
            TextKeyToggle = textKeyToggle;
            ReinforceKeyToggle = reinforceKeyToggle;
        }

        public string Id { get; }

        /// <summary>Texto da tecla (ex.: WASD, E, SETAS + E), localizável.</summary>
        public string GlyphKey { get; }

        public string TextKey { get; }
        public string ReinforceKey { get; }

        /// <summary>Variante para o modo "pressionar" (alternar) do E. Nula = o mesmo texto de <see cref="TextKey"/>.</summary>
        public string TextKeyToggle { get; }

        public string ReinforceKeyToggle { get; }

        public string TextFor(HoldMode mode) =>
            mode == HoldMode.Toggle && !string.IsNullOrEmpty(TextKeyToggle) ? TextKeyToggle : TextKey;

        public string ReinforceFor(HoldMode mode) =>
            mode == HoldMode.Toggle && !string.IsNullOrEmpty(ReinforceKeyToggle) ? ReinforceKeyToggle : ReinforceKey;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"GuidanceStep '{Id ?? "<sem id>"}'";
            ContractValidation.RequireId(errors, owner, nameof(Id), Id);
            ContractValidation.RequireId(errors, owner, nameof(GlyphKey), GlyphKey);
            ContractValidation.RequireId(errors, owner, nameof(TextKey), TextKey);
            ContractValidation.RequireId(errors, owner, nameof(ReinforceKey), ReinforceKey);
            if (TextKeyToggle != null)
                ContractValidation.RequireId(errors, owner, nameof(TextKeyToggle), TextKeyToggle);

            if (ReinforceKeyToggle != null)
                ContractValidation.RequireId(errors, owner, nameof(ReinforceKeyToggle), ReinforceKeyToggle);

            return errors;
        }
    }

    /// <summary>
    /// Conjunto de instruções com o que já foi cumprido. Sem Unity. O tutorial (mover, interagir) e a assistência de uma
    /// atividade (placa, sintonia, varredura) usam o mesmo modelo: a conclusão vem de uma ação observada, nunca do tempo.
    /// </summary>
    public sealed class GuidanceChecklist
    {
        readonly List<GuidanceStep> steps = new List<GuidanceStep>();
        readonly List<bool> done = new List<bool>();

        public GuidanceChecklist(IEnumerable<GuidanceStep> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            foreach (var step in items)
            {
                if (step == null)
                    throw new ArgumentException("Instrução nula.", nameof(items));

                var errors = step.Validate();
                if (errors.Count > 0)
                    throw new ArgumentException(ContractValidation.Describe(errors), nameof(items));

                if (IndexOf(step.Id) >= 0)
                    throw new ArgumentException($"Instrução repetida: '{step.Id}'.", nameof(items));

                steps.Add(step);
                done.Add(false);
            }

            if (steps.Count == 0)
                throw new ArgumentException("Informe ao menos uma instrução.", nameof(items));
        }

        public int Count => steps.Count;
        public GuidanceStep this[int index] => steps[index];
        public bool IsDone(int index) => done[index];

        public bool IsComplete
        {
            get
            {
                foreach (var flag in done)
                {
                    if (!flag)
                        return false;
                }

                return true;
            }
        }

        public int PendingCount
        {
            get
            {
                var pending = 0;
                foreach (var flag in done)
                {
                    if (!flag)
                        pending++;
                }

                return pending;
            }
        }

        public int IndexOf(string stepId)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].Id == stepId)
                    return i;
            }

            return -1;
        }

        public bool IsDone(string stepId)
        {
            var index = IndexOf(stepId);
            return index >= 0 && done[index];
        }

        /// <summary>Marca como cumprida. False se o ID não existe ou já estava cumprido (idempotente).</summary>
        public bool Complete(string stepId)
        {
            var index = IndexOf(stepId);
            if (index < 0 || done[index])
                return false;

            done[index] = true;
            return true;
        }

        /// <summary>Primeira instrução pendente, ou -1.</summary>
        public int FirstPending()
        {
            for (var i = 0; i < done.Count; i++)
            {
                if (!done[i])
                    return i;
            }

            return -1;
        }
    }

    /// <summary>Uma linha da orientação já resolvida em texto, pronta para desenhar.</summary>
    public readonly struct GuidanceLine
    {
        public GuidanceLine(string stepId, string glyph, string text, bool done, bool emphasized)
        {
            StepId = stepId;
            Glyph = glyph;
            Text = text;
            Done = done;
            Emphasized = emphasized;
        }

        public string StepId { get; }
        public string Glyph { get; }
        public string Text { get; }
        public bool Done { get; }

        /// <summary>Reforço: o texto é o da dica direta e a linha ganha destaque (forma, não só cor).</summary>
        public bool Emphasized { get; }
    }

    /// <summary>Estado da orientação, sem Unity, como entrada de <see cref="GuidanceViewBuilder"/>.</summary>
    public readonly struct GuidanceSnapshot
    {
        public GuidanceSnapshot(GuidanceChecklist checklist, GuidanceLevel level, bool helpAvailable, bool corridorAvailable,
            bool showTitle)
        {
            Checklist = checklist;
            Level = level;
            HelpAvailable = helpAvailable;
            CorridorAvailable = corridorAvailable;
            ShowTitle = showTitle;
        }

        /// <summary>Nulo quando não há orientação em andamento.</summary>
        public GuidanceChecklist Checklist { get; }

        public GuidanceLevel Level { get; }

        /// <summary>O capítulo responde a H (enquadrar o alvo).</summary>
        public bool HelpAvailable { get; }

        /// <summary>O capítulo mostra um corredor previsto para voltar.</summary>
        public bool CorridorAvailable { get; }

        /// <summary>Tutorial completo mostra o título "Como jogar"; a assistência de uma atividade não.</summary>
        public bool ShowTitle { get; }
    }

    /// <summary>O que a interface desenha agora.</summary>
    public readonly struct GuidanceView
    {
        public GuidanceView(bool visible, string title, GuidanceLine[] lines, string helpOffer, string corridorOffer, GuidanceLevel level)
        {
            Visible = visible;
            Title = title;
            Lines = lines ?? new GuidanceLine[0];
            HelpOffer = helpOffer;
            CorridorOffer = corridorOffer;
            Level = level;
        }

        public bool Visible { get; }
        public string Title { get; }
        public GuidanceLine[] Lines { get; }

        /// <summary>Oferta de H, ou vazio.</summary>
        public string HelpOffer { get; }

        /// <summary>Oferta de voltar ao corredor, ou vazio.</summary>
        public string CorridorOffer { get; }

        public GuidanceLevel Level { get; }

        public static GuidanceView Hidden => new GuidanceView(false, string.Empty, null, string.Empty, string.Empty, GuidanceLevel.None);

        /// <summary>Assinatura estável: a interface só reescreve quando ela muda (sem alocação por quadro).</summary>
        public string Signature()
        {
            if (!Visible)
                return string.Empty;

            var text = Title + "|" + (int)Level + "|" + HelpOffer + "|" + CorridorOffer;
            foreach (var line in Lines)
                text += "|" + line.StepId + ":" + (line.Done ? "1" : "0") + (line.Emphasized ? "!" : "") + ":" + line.Glyph + ":" + line.Text;

            return text;
        }
    }

    /// <summary>Monta a visão PT/EN da orientação a partir do estado. Sem Unity.</summary>
    public static class GuidanceViewBuilder
    {
        /// <param name="showCompleted">Mostra também as instruções já cumpridas (marcadas ✓). Falso as recolhe.</param>
        /// <param name="extraAssistance">Preferência de assistência extra: a dica de H aparece sem esperar os 30 s.</param>
        public static GuidanceView Build(in GuidanceSnapshot snapshot, DossierTextCatalog texts, string language, HoldMode holdMode,
            bool showCompleted, bool extraAssistance)
        {
            var checklist = snapshot.Checklist;
            if (checklist == null || texts == null)
                return GuidanceView.Hidden;

            var firstPending = checklist.FirstPending();
            var lines = new List<GuidanceLine>(checklist.Count);
            for (var i = 0; i < checklist.Count; i++)
            {
                var step = checklist[i];
                var done = checklist.IsDone(i);
                if (done && !showCompleted)
                    continue;

                // Só a primeira instrução pendente recebe o reforço: as seguintes ainda não foram tentadas.
                var emphasized = !done && i == firstPending && snapshot.Level >= GuidanceLevel.Reinforce;
                var key = emphasized ? step.ReinforceFor(holdMode) : step.TextFor(holdMode);
                var text = texts.Get(key, language, key);
                if (done)
                    text += " · " + texts.Get(GuidanceKeys.Done, language, GuidanceKeys.Done);

                lines.Add(new GuidanceLine(step.Id, texts.Get(step.GlyphKey, language, step.GlyphKey), text, done, emphasized));
            }

            if (lines.Count == 0)
                return GuidanceView.Hidden;

            var pending = checklist.PendingCount > 0;
            var offerLevel = pending && snapshot.Level >= GuidanceLevel.Offer;
            var helpOffer = pending && snapshot.HelpAvailable && (offerLevel || extraAssistance)
                ? texts.Get(offerLevel ? GuidanceKeys.OfferHelp : GuidanceKeys.HelpHint, language, offerLevel ? GuidanceKeys.OfferHelp : GuidanceKeys.HelpHint)
                : string.Empty;
            var corridorOffer = offerLevel && snapshot.CorridorAvailable
                ? texts.Get(GuidanceKeys.OfferCorridor, language, GuidanceKeys.OfferCorridor)
                : string.Empty;
            var title = snapshot.ShowTitle ? texts.Get(GuidanceKeys.Title, language, GuidanceKeys.Title) : string.Empty;
            return new GuidanceView(true, title, lines.ToArray(), helpOffer, corridorOffer, snapshot.Level);
        }
    }
}
