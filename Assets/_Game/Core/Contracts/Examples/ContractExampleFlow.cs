using System;

namespace Abandoned.SpaceApps.Examples
{
    /// <summary>
    /// EXEMPLO de consumo dos contratos por um instrumento de capítulo. Mostra os três passos que a CORE-002 exige:
    /// (1) solicitar interação, (2) apresentar uma evidência, (3) concluir uma etapa — usando só as interfaces,
    /// sem conhecer a implementação. O instrumento nunca lê a tecla E nem escreve estado global.
    /// </summary>
    public sealed class ContractExampleFlow : IDisposable
    {
        public const string IntroStep = "Intro";
        public const string ObserveStep = "Observe";
        public const string DoneStep = "Done";
        public const string RockCandidateId = "example.rock";
        public const string RockEvidenceId = "example.rock.observation";

        readonly IChapterDirector director;
        readonly IInteractionService interaction;
        readonly IEvidenceStore evidence;

        public ContractExampleFlow(IChapterDirector director, IInteractionService interaction, IEvidenceStore evidence)
        {
            this.director = director ?? throw new ArgumentNullException(nameof(director));
            this.interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            this.evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));

            director.StepEntered += OnStepEntered;
            interaction.HoldCompleted += OnHoldCompleted;
        }

        /// <summary>Exemplo de definição válida: Intro → Observe → Done (terminal).</summary>
        public static ChapterDefinition CreateExampleChapter() =>
            ChapterDefinition.Create(
                "example", "EXAMPLE.TITLE", "EXAMPLE.DESCRIPTION", "EXAMPLE.QUESTION", "ContractExample", 60f, "example.intro",
                new[]
                {
                    new ChapterStep(IntroStep, false, ObserveStep),
                    new ChapterStep(ObserveStep, false, DoneStep),
                    new ChapterStep(DoneStep, true),
                });

        /// <summary>A pessoa leu a introdução. Conclui a etapa por ID; repetir a chamada é inofensivo (idempotente).</summary>
        public bool AcknowledgeIntro() => director.CompleteStep(IntroStep);

        void OnStepEntered(string stepId)
        {
            if (stepId == ObserveStep)
            {
                // (1) Solicitar interação: o prompt e a execução usam o MESMO candidato.
                interaction.SetCandidate(new InteractionCandidate
                {
                    Id = RockCandidateId,
                    VerbKey = "EXAMPLE.VERB.observe",
                    ObjectKey = "EXAMPLE.OBJ.rock",
                    Available = true,
                    DistanceMeters = 2f,
                    HoldSeconds = 2f,
                    Priority = 10,
                });
            }
            else if (stepId == DoneStep)
            {
                director.FinishChapter();
            }
        }

        void OnHoldCompleted(string candidateId)
        {
            if (candidateId != RockCandidateId)
                return;

            interaction.ClearCandidate(RockCandidateId);

            // (2) Apresentar uma evidência: idempotente por ID; resultado por chave de texto, nunca telemetria literal.
            evidence.Add(new EvidenceRecord(RockEvidenceId, EvidenceKind.Observation, "EXAMPLE.RESULT01", "MS10",
                EvidenceAuthorship.Reconstruction));

            // (3) Concluir a etapa por ID; a tabela de transições decide a próxima.
            director.CompleteStep(ObserveStep);
        }

        public void Dispose()
        {
            director.StepEntered -= OnStepEntered;
            interaction.HoldCompleted -= OnHoldCompleted;
        }
    }
}
