namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Chaves de <c>HudTexts.json</c>. Nenhuma frase fica em C#; o teste <c>HudAssetsTests</c> confere que toda
    /// constante existe em PT e EN. Verbo e objeto de cada ação (ex.: MAR.VERB.grind) vêm da tabela de cada capítulo.
    /// </summary>
    public static class HudKeys
    {
        public const string PromptPress = "prompt.press";
        public const string PromptHold = "prompt.hold";
        public const string PromptToggle = "prompt.toggle";
        public const string PromptToggleStop = "prompt.toggle.stop";
        public const string PromptRearm = "prompt.rearm";
        public const string PromptResumeHold = "prompt.resume.hold";
        public const string PromptResumeToggle = "prompt.resume.toggle";

        public const string StatusAvailablePress = "status.available.press";
        public const string StatusAvailableHold = "status.available.hold";
        public const string StatusRearm = "status.rearm";
        public const string StatusHolding = "status.holding";
        public const string StatusPaused = "status.paused";
        public const string StatusDone = "status.done";

        public const string CommandPrepared = "command.prepared";
        public const string CommandSent = "command.sent";
        public const string CommandTransit = "command.transit";
        public const string CommandExecuting = "command.executing";
        public const string CommandConfirmed = "command.confirmed";
        public const string CommandStatusPrepared = "command.status.prepared";
        public const string CommandStatusSent = "command.status.sent";
        public const string CommandStatusTransit = "command.status.transit";
        public const string CommandStatusExecuting = "command.status.executing";
        public const string CommandStatusConfirmed = "command.status.confirmed";

        public const string EnergyLabel = "energy.label";
        public const string EnergyLow = "energy.low";
    }
}
