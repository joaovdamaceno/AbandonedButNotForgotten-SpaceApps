using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Objetivo principal do capítulo, mostrado na placa do topo e, se tiver <see cref="Anchor"/>, na âncora no mundo
    /// ou na seta de borda. Só UM objetivo por vez: o HUD não empilha objetivos.
    /// </summary>
    public struct HudObjective
    {
        /// <summary>ID estável do objetivo (ex.: mars.objective.mckittrick).</summary>
        public string Id;

        /// <summary>Chave do carimbo de missão/local/ano. Opcional.</summary>
        public string StampKey;

        /// <summary>Chave do verbo e objeto do objetivo (ex.: "Prepare e analise a rocha"). Obrigatória.</summary>
        public string LabelKey;

        /// <summary>Chave do nome do alvo (ex.: "McKittrick"). Opcional.</summary>
        public string TargetNameKey;

        /// <summary>Âncora no mundo. Opcional: sem ela só a placa aparece.</summary>
        public Transform Anchor;

        /// <summary>
        /// Id do candidato do <c>InteractionService</c> que realiza este objetivo. Quando é o candidato ativo, a distância
        /// mostrada é a do candidato (a mesma que decide o E); senão é medida da referência até a âncora.
        /// </summary>
        public string CandidateId;

        /// <summary>Etapa atual de um conjunto (ex.: 2 de 3). Zero em <see cref="StepCount"/> = sem contagem.</summary>
        public int Step;

        public int StepCount;

        /// <summary>Classe de identidade visual (sa-chapter--mars|pioneer|surveyor). Opcional: mantém a atual.</summary>
        public string AccentClass;

        public ObjectiveData ToData() => new ObjectiveData(Id, StampKey, LabelKey, TargetNameKey, Step, StepCount);

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"HudObjective '{Id ?? "<sem id>"}'";
            ContractValidation.RequireId(errors, owner, nameof(Id), Id);
            ContractValidation.RequireId(errors, owner, nameof(LabelKey), LabelKey);
            RequireOptionalId(errors, owner, nameof(StampKey), StampKey);
            RequireOptionalId(errors, owner, nameof(TargetNameKey), TargetNameKey);
            RequireOptionalId(errors, owner, nameof(CandidateId), CandidateId);

            if (StepCount < 0 || Step < 0)
                ContractValidation.Report(errors, owner, "'Step' e 'StepCount' não podem ser negativos.");
            else if (StepCount > 0 && Step > StepCount)
                ContractValidation.Report(errors, owner, $"'Step' ({Step}) maior que 'StepCount' ({StepCount}).");

            if (!string.IsNullOrEmpty(AccentClass) && (!AccentClass.StartsWith(ChapterCatalog.AccentClassPrefix, System.StringComparison.Ordinal)
                || AccentClass.Length == ChapterCatalog.AccentClassPrefix.Length))
                ContractValidation.Report(errors, owner, $"'AccentClass' deve começar com '{ChapterCatalog.AccentClassPrefix}'.");

            return errors;
        }

        static void RequireOptionalId(List<string> errors, string owner, string field, string value)
        {
            if (!string.IsNullOrEmpty(value))
                ContractValidation.RequireId(errors, owner, field, value);
        }
    }

    /// <summary>
    /// Contrato do HUD compartilhado para os capítulos e para as camadas globais (tutorial, pausa, evidências). Quem
    /// alimenta o HUD usa esta interface; ninguém desenha HUD próprio para objetivo, prompt E ou energia.
    /// </summary>
    public interface IMissionHud
    {
        /// <summary>Define o objetivo principal. Inválido lança <see cref="System.ArgumentException"/> com a lista de erros.</summary>
        void SetObjective(in HudObjective objective);

        void ClearObjective();

        /// <summary>Mostra o medidor de energia (0–1). Só chame quando a energia ajuda a entender a ação; <see cref="ClearEnergy"/> o esconde.</summary>
        void SetEnergy(float fraction01);

        void ClearEnergy();

        /// <summary>Etapa do comando em trânsito (Surveyor). Substitui o prompt E até <see cref="ClearCommand"/>.</summary>
        void SetCommand(HudCommandState command);

        void ClearCommand();

        /// <summary>Progresso físico do instrumento para um candidato (0–1), quando difere do hold atual. 0 = nada preservado.</summary>
        void ReportProgress(string candidateId, float fraction01);

        void ClearProgress(string candidateId);

        /// <summary>Quem mede a distância até âncoras que não são o candidato ativo (veículo, rover, sonda).</summary>
        void SetReference(Transform reference);

        /// <summary>Acrescenta a tabela de textos do capítulo (verbo, objeto, objetivo, nome do alvo).</summary>
        void AddTextTable(TextAsset table);
    }
}
