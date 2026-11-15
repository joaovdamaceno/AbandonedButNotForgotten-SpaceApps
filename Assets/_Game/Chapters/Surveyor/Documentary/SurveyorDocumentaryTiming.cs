namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public static class SurveyorDocumentaryTiming
    {
        public const float Prologue = 23f, Arrival = 31f, Activation = 16f, InstrumentEntry = 12f;
        public const float Findings = 18f, Night = 38f, Reunion = 44f, Legacy = 13f;
        public const float BeforeScience = Prologue + Arrival + Activation;
        public const float CinematicSeconds = BeforeScience + Findings + Night + Reunion + Legacy;
        public const float Deploy = 2.5f, Pressure = 1.6f, ResultRead = 3.5f;
    }
}
