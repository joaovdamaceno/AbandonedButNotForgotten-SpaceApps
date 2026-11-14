namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Buses do áudio (Docs/03_ARCHITECTURE.md): Master multiplica os outros três. Os volumes vêm de
    /// <see cref="SettingsData"/> e são independentes, para que baixar a música não apague o som de uma ferramenta.
    /// </summary>
    public enum AudioBus
    {
        Master = 0,
        Music = 1,
        Sfx = 2,
        /// <summary>Interface: responde mesmo na pausa.</summary>
        Ui = 3,
    }
}
