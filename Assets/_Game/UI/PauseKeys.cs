namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Chaves de <c>PauseTexts.json</c>. Nenhuma frase fica em C#; <c>PauseAssetsTests</c> confere que toda constante
    /// existe em PT e EN.
    /// </summary>
    public static class PauseKeys
    {
        public const string Button = "pause.button";
        public const string KeyHint = "pause.keyhint";
        public const string Title = "pause.title";
        public const string SubtitleMenu = "pause.subtitle.menu";
        public const string SubtitleFocus = "pause.subtitle.focus";
        public const string Resume = "pause.resume";
        public const string ResumeHint = "pause.resume.hint";
        public const string Restart = "pause.restart";
        public const string RestartConfirm = "pause.restart.confirm";
        public const string Dossier = "pause.dossier";
        public const string DossierConfirm = "pause.dossier.confirm";
        public const string Navigation = "pause.navigation";

        public const string SettingsTitle = "prefs.title";
        public const string VolumeMaster = "prefs.volume.master";
        public const string VolumeMusic = "prefs.volume.music";
        public const string VolumeSfx = "prefs.volume.sfx";
        public const string VolumeUi = "prefs.volume.ui";
        public const string Language = "prefs.language";
        public const string LanguagePt = "prefs.language.pt";
        public const string LanguageEn = "prefs.language.en";
        public const string Sensitivity = "prefs.sensitivity";
        public const string ReducedMotion = "prefs.reducedMotion";
        public const string Fullscreen = "prefs.fullscreen";
        public const string Quality = "prefs.quality";
        public const string QualityValue = "prefs.quality.value";
        public const string On = "prefs.on";
        public const string Off = "prefs.off";
        public const string Previous = "prefs.previous";
        public const string Next = "prefs.next";
        public const string Reset = "prefs.reset";
        public const string Saved = "prefs.saved";

        /// <summary>Todas as chaves, para o teste de conteúdo.</summary>
        public static readonly string[] All =
        {
            Button, KeyHint, Title, SubtitleMenu, SubtitleFocus, Resume, ResumeHint,
            Restart, RestartConfirm, Dossier, DossierConfirm, Navigation,
            SettingsTitle, VolumeMaster, VolumeMusic, VolumeSfx, VolumeUi, Language, LanguagePt, LanguageEn, Sensitivity,
            ReducedMotion, Fullscreen, Quality, QualityValue, On, Off, Previous, Next, Reset, Saved,
        };
    }
}
