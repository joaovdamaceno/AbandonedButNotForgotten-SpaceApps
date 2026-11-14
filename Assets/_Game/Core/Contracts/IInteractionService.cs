using System;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Contrato do <c>InteractionService</c>/<c>HoldAction</c> (implementação: CORE-003). Instrumentos oferecem
    /// candidatos e reagem aos eventos; o InputRouter é o único leitor de E e chama BeginHold/CancelHold.
    /// </summary>
    public interface IInteractionService
    {
        /// <summary>Candidato selecionado (maior prioridade entre os disponíveis; empate = menor distância). False se não há.</summary>
        bool TryGetActive(out InteractionCandidate candidate);

        /// <summary>
        /// True depois de uma conclusão ou de <see cref="RequireRelease"/> até <see cref="NotifyInteractReleased"/>, mas somente enquanto a tecla E ainda está pressionada (sem tecla pressionada não há o que esperar).
        /// Enquanto verdadeiro nenhum novo hold começa (rearme obrigatório: o E que abre não conta como início do seguinte).
        /// </summary>
        bool AwaitingRelease { get; }

        /// <summary>Id do hold em andamento; nulo se não há.</summary>
        string HoldingId { get; }

        event Action<string> HoldStarted;

        /// <summary>Progresso 0–1. Emitido a cada quadro enquanto E é mantido.</summary>
        event Action<string, float> HoldProgress;

        event Action<string> HoldCompleted;

        /// <summary>Soltou E, perdeu foco, pausou ou o candidato mudou. Progresso físico do instrumento permanece com o instrumento.</summary>
        event Action<string> HoldCanceled;

        /// <summary>Registra ou atualiza (por Id) um candidato. Candidato inválido lança <see cref="ArgumentException"/> com a lista de erros.</summary>
        void SetCandidate(InteractionCandidate candidate);

        /// <summary>Remove o candidato; se estava em hold, cancela o hold.</summary>
        void ClearCandidate(string id);

        /// <summary>Mesma regra de <see cref="BeginHold"/> sem efeito: é o que o prompt consulta para mostrar E.</summary>
        bool CanBeginHold(string id);

        /// <summary>Começa o hold somente se o candidato é o ativo, está disponível e não há rearme pendente.</summary>
        bool BeginHold(string id);

        /// <summary>Cancela o hold em andamento (se houver) e emite HoldCanceled. Não exige rearme por si só.</summary>
        void CancelHold();

        /// <summary>Exige InteractUp antes de um novo hold (ex.: E que abriu uma observação).</summary>
        void RequireRelease();

        /// <summary>Chamado pelo InputRouter em InteractUp e ao perder foco: libera o rearme e cancela o hold em andamento (soltar E pausa a operação).</summary>
        void NotifyInteractReleased();
    }
}
