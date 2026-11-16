namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Os cues de um instrumento acionado por E mantido (<see cref="IInteractionService"/>/HoldAction), ligados ao
    /// <see cref="InteractionCandidate.Id"/>: clique de início, loop modulado pelo progresso, confirmação ao concluir e
    /// desaceleração + som de soltar ao cancelar. Qualquer um pode ser vazio.
    /// </summary>
    public sealed class HoldSoundSet
    {
        public string CandidateId;
        public string StartCue;
        /// <summary>Loop do instrumento (2–6 s, com variação). Altura e volume acompanham o progresso real do hold.</summary>
        public string LoopCue;
        public string DoneCue;
        public string ReleaseCue;
    }
}
