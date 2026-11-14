namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Chaves de <c>DossierTexts.json</c> usadas pela <see cref="DossierScreen"/>. Nenhuma frase fica em C#; o teste
    /// <c>DossierAssetsTests</c> confere que toda constante existe em PT e EN.
    /// </summary>
    public static class DossierKeys
    {
        public const string Stamp = "dossier.stamp";
        public const string Title = "dossier.title";
        public const string Lede = "dossier.lede";
        public const string Disclaimer = "dossier.disclaimer";

        public const string ChapterWord = "folder.chapter";
        public const string Details = "folder.details";
        public const string Start = "folder.start";
        public const string Unavailable = "folder.unavailable";
        public const string PhotoPending = "photo.pending";

        public const string LanguageLabel = "footer.language";
        public const string LanguagePtName = "language.pt.name";
        public const string LanguageEnName = "language.en.name";
        public const string Settings = "footer.settings";
        public const string Credits = "footer.credits";
        public const string Back = "panel.close";
        public const string ErrorLoad = "error.load";

        public const string LoadingStamp = "loading.stamp";
        public const string LoadingTitle = "loading.title";
        public const string LoadingReturning = "loading.returning";
        public const string LoadingNote = "loading.note";
        public const string LoadingHint = "loading.hint";

        public const string DetailsStamp = "details.stamp";
        public const string DetailsDuration = "details.duration";
        public const string DetailsTodo = "details.todo";
        public const string DetailsOptional = "details.optional";

        public const string SettingsTitle = "settings.title";
        public const string SettingsMaster = "settings.master";
        public const string SettingsMusic = "settings.music";
        public const string SettingsSfx = "settings.sfx";
        public const string SettingsMotion = "settings.motion";
        public const string SettingsHold = "settings.hold";
        public const string SettingsAssist = "settings.assist";
        public const string SettingsOn = "settings.on";
        public const string SettingsOff = "settings.off";
        public const string SettingsHoldHold = "settings.hold.hold";
        public const string SettingsHoldToggle = "settings.hold.toggle";
        public const string SettingsDecrease = "settings.decrease";
        public const string SettingsIncrease = "settings.increase";

        public const string CreditsTitle = "credits.title";
        public const string CreditsLinePrefix = "credits.line.";

        public const string SlotTitle = "title";
        public const string SlotMeta = "meta";
        public const string SlotQuestion = "question";
        public const string SlotDetail = "detail";

        /// <summary>Texto próprio da pasta, usado quando o capítulo ainda não tem tabela de textos (ex.: slot.mars.title).</summary>
        public static string Slot(string slotId, string field) => "slot." + slotId + "." + field;
    }
}
