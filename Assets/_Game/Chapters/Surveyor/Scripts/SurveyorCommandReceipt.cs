namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    public enum CommandTransitStatus { Inactive, Ready, InTransit, Received, Faulted }

    /// <summary>Identifica uma ordem desta execução. Received significa entrega, não resposta de rádio de ida e volta.</summary>
    public readonly struct SurveyorCommandReceipt
    {
        public readonly string CommandId;
        public readonly uint Generation;
        public readonly ulong Serial;
        public readonly int SentFrame;

        public SurveyorCommandReceipt(string id, uint generation, ulong serial, int sentFrame)
        {
            CommandId = id;
            Generation = generation;
            Serial = serial;
            SentFrame = sentFrame;
        }
    }
}
