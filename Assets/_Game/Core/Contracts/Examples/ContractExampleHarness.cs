using UnityEngine;

namespace Abandoned.SpaceApps.Examples
{
    /// <summary>
    /// Harness da cena de teste ContractExample: liga o exemplo aos mocks e simula E mantido por 2 s, registrando
    /// cada passo no Console. Fica em assembly de Editor e a cena não entra nas Build Settings.
    /// </summary>
    public sealed class ContractExampleHarness : MonoBehaviour
    {
        [SerializeField] ChapterDefinition chapter;

        MockChapterDirector director;
        MockInteractionService interaction;
        MockEvidenceStore evidence;
        ContractExampleFlow flow;

        void Start()
        {
            var errors = chapter != null ? chapter.Validate() : null;
            if (chapter == null || errors.Count > 0)
            {
                Debug.LogError("[ContractExample] Capítulo ausente ou inválido:" +
                    (chapter == null ? " referência nula." : ContractValidation.Describe(errors)), this);
                enabled = false;
                return;
            }

            director = new MockChapterDirector();
            interaction = new MockInteractionService();
            evidence = new MockEvidenceStore();
            flow = new ContractExampleFlow(director, interaction, evidence);

            director.StepEntered += id => Debug.Log($"[ContractExample] etapa: {id}");
            director.ChapterFinished += id => Debug.Log($"[ContractExample] capítulo concluído: {id}");
            interaction.HoldStarted += id => Debug.Log($"[ContractExample] hold iniciado: {id}");
            interaction.HoldCompleted += id => Debug.Log($"[ContractExample] hold concluído: {id}");
            evidence.Added += record => Debug.Log($"[ContractExample] evidência: {record.EvidenceId} ({record.ResultKey}, fonte {record.ScienceRef})");

            director.Begin(chapter);
            flow.AcknowledgeIntro();
            interaction.BeginHold(ContractExampleFlow.RockCandidateId);
        }

        void Update() => interaction?.Tick(Time.deltaTime);

        void OnDestroy() => flow?.Dispose();
    }
}
