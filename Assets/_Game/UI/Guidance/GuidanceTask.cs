using System;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// O que o capítulo oferece à orientação. A orientação só mostra; quem responde a H, enquadra a câmera ou marca o
    /// corredor é o capítulo (nada é teleportado nem resolvido por ela).
    /// </summary>
    public sealed class GuidanceOptions
    {
        /// <summary>
        /// Candidato cujo <c>HoldCompleted</c> cumpre "interagir". Nulo: qualquer candidato do <see cref="IInteractionService"/>.
        /// Evita uma segunda lógica de conclusão: o hold é o do próprio capítulo.
        /// </summary>
        public string InteractCandidateId;

        /// <summary>O capítulo trata <c>InputRouter.HelpDown</c> (H) enquadrando o alvo. Habilita a oferta de H aos 30 s.</summary>
        public bool HelpAvailable;

        /// <summary>O capítulo mostra um corredor previsto. Habilita o texto "volte pelo corredor marcado" aos 30 s.</summary>
        public bool CorridorAvailable;
    }

    /// <summary>
    /// Assistência de uma atividade com controles próprios (placa, sintonia, varredura): uma instrução com a alternativa de
    /// teclado, no mesmo relógio de 12 s/30 s do tutorial. O capítulo avisa o progresso e a conclusão pelo
    /// <see cref="GuidanceController"/>; a conclusão continua sendo a do contrato do capítulo.
    /// </summary>
    public sealed class GuidanceTask
    {
        public GuidanceTask(GuidanceStep step, GuidanceOptions options = null, bool countsInPanel = false)
        {
            Step = step ?? throw new ArgumentNullException(nameof(step));
            Options = options ?? new GuidanceOptions();
            CountsInPanel = countsInPanel;
        }

        public GuidanceStep Step { get; }
        public GuidanceOptions Options { get; }

        /// <summary>A atividade roda num painel (contexto Panel do InputRouter), como a varredura da Pioneer.</summary>
        public bool CountsInPanel { get; }

        public static GuidanceTask Plaque(GuidanceOptions options = null) => new GuidanceTask(
            new GuidanceStep("guide.plaque", GuidanceKeys.GlyphPlaque, GuidanceKeys.ActivityPlaque, GuidanceKeys.ActivityPlaqueReinforce),
            options, countsInPanel: true);

        public static GuidanceTask Tuning(GuidanceOptions options = null) => new GuidanceTask(
            new GuidanceStep("guide.tuning", GuidanceKeys.GlyphTuning, GuidanceKeys.ActivityTuning, GuidanceKeys.ActivityTuningReinforce),
            options);

        public static GuidanceTask Sweep(GuidanceOptions options = null) => new GuidanceTask(
            new GuidanceStep("guide.sweep", GuidanceKeys.GlyphSweep, GuidanceKeys.ActivitySweep, GuidanceKeys.ActivitySweepReinforce),
            options, countsInPanel: true);
    }
}
