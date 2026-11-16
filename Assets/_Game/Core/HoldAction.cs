using System;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Como o E é interpretado. Hold: manter pressionado e soltar pausa. Toggle: pressionar para iniciar e pressionar
    /// de novo para parar (alternativa de acessibilidade; soltar a tecla não interrompe).
    /// </summary>
    public enum HoldMode
    {
        Hold = 0,
        Toggle = 1,
    }

    /// <summary>
    /// Estado de UM hold em andamento: tempo ativo acumulado e progresso 0–1. Não conhece entrada, UI nem instrumento.
    /// Ao cancelar, o progresso desta classe zera; o instrumento decide, pelos seus dados, qual progresso FÍSICO
    /// conservar (por exemplo, pedindo um novo hold só com o tempo restante).
    /// </summary>
    public sealed class HoldAction
    {
        bool advanced;

        public string Id { get; private set; }
        public bool IsActive { get; private set; }
        public float DurationSeconds { get; private set; }
        public float ElapsedSeconds { get; private set; }

        /// <summary>0–1. Duração zero conclui no primeiro avanço e só então chega a 1.</summary>
        public float Progress
        {
            get
            {
                if (!IsActive)
                    return 0f;

                if (DurationSeconds <= 0f)
                    return advanced ? 1f : 0f;

                return Math.Min(1f, ElapsedSeconds / DurationSeconds);
            }
        }

        public void Begin(string id, float durationSeconds)
        {
            Id = id;
            DurationSeconds = Math.Max(0f, durationSeconds);
            ElapsedSeconds = 0f;
            advanced = false;
            IsActive = true;
        }

        /// <summary>
        /// Soma tempo ativo (zero quando o relógio está congelado pela pausa). Devolve true somente na chamada que
        /// conclui; depois disso o hold fica inativo, então concluir nunca se repete.
        /// </summary>
        public bool Advance(float activeDeltaSeconds)
        {
            if (!IsActive)
                return false;

            advanced = true;
            ElapsedSeconds += Math.Max(0f, activeDeltaSeconds);
            if (Progress < 1f)
                return false;

            IsActive = false;
            return true;
        }

        public void Reset()
        {
            Id = null;
            IsActive = false;
            DurationSeconds = 0f;
            ElapsedSeconds = 0f;
            advanced = false;
        }
    }
}
