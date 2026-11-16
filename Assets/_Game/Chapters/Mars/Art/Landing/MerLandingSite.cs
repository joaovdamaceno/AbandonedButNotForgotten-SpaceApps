namespace Abandoned.SpaceApps
{
    /// <summary>Local de pouso. Os dois usam a MESMA coreografia; só o terreno muda (Opportunity cai dentro de Eagle).</summary>
    public enum MerLandingSite
    {
        Gusev = 0,
        Meridiani = 1,

        /// <summary>Mesmo local, sobre o terreno do mapa importado (bacia rasa, planície perto de 1 m). Usado quando o mapa importado está montado.</summary>
        MeridianiClassic = 2,
    }
}
