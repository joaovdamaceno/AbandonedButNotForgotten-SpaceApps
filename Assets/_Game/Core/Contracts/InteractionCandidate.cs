using System.Collections.Generic;
using UnityEngine;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Alvo de interação oferecido por um instrumento. A MESMA estrutura decide o que o prompt mostra e o que a
    /// execução aceita: nunca exibir E disponível se a ação falharia por outra distância.
    /// </summary>
    public struct InteractionCandidate
    {
        public string Id;

        /// <summary>Âncora no mundo (metros) onde o prompt se ancora. Opcional para alvos só de HUD.</summary>
        public Transform Anchor;

        /// <summary>Chave de localização do verbo (ex.: "MAR.VERB.grind").</summary>
        public string VerbKey;

        /// <summary>Chave de localização do objeto (ex.: "MAR.OBJ.rock").</summary>
        public string ObjectKey;

        public bool Available;
        public float DistanceMeters;

        /// <summary>Segundos mantendo E até concluir. Zero = conclui assim que a tecla é pressionada e solta/mantida no mesmo quadro.</summary>
        public float HoldSeconds;

        /// <summary>Prioridade explícita por etapa: maior vence. Alvos opcionais usam valor menor que a ação principal.</summary>
        public int Priority;

        public List<string> Validate()
        {
            var errors = new List<string>();
            var owner = $"InteractionCandidate '{Id ?? "<sem id>"}'";
            ContractValidation.RequireId(errors, owner, nameof(Id), Id);
            ContractValidation.RequireId(errors, owner, nameof(VerbKey), VerbKey);
            ContractValidation.RequireId(errors, owner, nameof(ObjectKey), ObjectKey);
            ContractValidation.RequireNonNegative(errors, owner, nameof(DistanceMeters), DistanceMeters);
            ContractValidation.RequireNonNegative(errors, owner, nameof(HoldSeconds), HoldSeconds);
            return errors;
        }
    }
}
