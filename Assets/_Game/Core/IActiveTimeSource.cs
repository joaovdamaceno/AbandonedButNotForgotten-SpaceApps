namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Fonte de tempo ATIVO do jogo: cresce só em Playing não pausado. O <c>SessionClock</c> (CORE-005) a implementa;
    /// o InteractionService a consome para que o hold congele na pausa sem tocar em Time.timeScale.
    /// </summary>
    public interface IActiveTimeSource
    {
        /// <summary>Segundos ativos decorridos neste quadro (zero na pausa, no loading e no dossiê).</summary>
        float ActiveDeltaSeconds { get; }
    }
}
