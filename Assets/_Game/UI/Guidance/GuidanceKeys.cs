namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Chaves de <c>GuidanceTexts.json</c> (UI-005). Nenhuma frase fica em C#; <c>GuidanceAssetsTests</c> confere que toda
    /// constante existe em PT e EN. Textos específicos de um capítulo (alvo, objeto) vêm da tabela do próprio capítulo.
    /// </summary>
    public static class GuidanceKeys
    {
        public const string Title = "guide.title";
        public const string Done = "guide.done";
        public const string GlyphMove = "guide.glyph.move";
        public const string GlyphInteract = "guide.glyph.interact";
        public const string GlyphHelp = "guide.glyph.help";
        public const string GlyphPlaque = "guide.glyph.plaque";
        public const string GlyphTuning = "guide.glyph.tuning";
        public const string GlyphSweep = "guide.glyph.sweep";
        public const string StepMove = "guide.step.move";
        public const string StepMoveReinforce = "guide.step.move.reinforce";
        public const string StepInteractHold = "guide.step.interact.hold";
        public const string StepInteractPress = "guide.step.interact.press";
        public const string StepInteractReinforceHold = "guide.step.interact.reinforce.hold";
        public const string StepInteractReinforcePress = "guide.step.interact.reinforce.press";
        public const string ActivityPlaque = "guide.activity.plaque";
        public const string ActivityPlaqueReinforce = "guide.activity.plaque.reinforce";
        public const string ActivityTuning = "guide.activity.tuning";
        public const string ActivityTuningReinforce = "guide.activity.tuning.reinforce";
        public const string ActivitySweep = "guide.activity.sweep";
        public const string ActivitySweepReinforce = "guide.activity.sweep.reinforce";
        public const string OfferHelp = "guide.offer.help";
        public const string HelpHint = "guide.help.hint";
        public const string OfferCorridor = "guide.offer.corridor";

        /// <summary>Todas as chaves, para o teste de conteúdo.</summary>
        public static readonly string[] All =
        {
            Title, Done, GlyphMove, GlyphInteract, GlyphHelp, GlyphPlaque, GlyphTuning, GlyphSweep, StepMove,
            StepMoveReinforce, StepInteractHold, StepInteractPress, StepInteractReinforceHold,
            StepInteractReinforcePress, ActivityPlaque, ActivityPlaqueReinforce, ActivityTuning,
            ActivityTuningReinforce, ActivitySweep, ActivitySweepReinforce, OfferHelp, HelpHint, OfferCorridor,
        };
    }
}
