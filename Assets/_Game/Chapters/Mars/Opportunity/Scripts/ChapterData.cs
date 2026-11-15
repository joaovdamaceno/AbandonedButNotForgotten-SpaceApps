using System;
using UnityEngine;

namespace Abandoned.SpaceApps.Opportunity
{
    public enum ChapterStage { Intro, Landing, Activation, Exploration, RockAnalysis, Discovery, HeatShieldArrival, HeatShieldExploration, MeteoriteDiscovery, TimeSkip, FinalExploration, DustStorm, CommunicationLost, Farewell, Transition, ChapterEnd }

    [CreateAssetMenu(menuName = "Opportunity/Chapter")]
    public sealed class ChapterData : ScriptableObject
    {
        public string title = "ABANDONED, BUT NOT FORGOTTEN";
        public string chapterId = "opportunity";
        [Tooltip("Leave empty until another real chapter has been authored.")]
        public string nextChapterScene;
        [HideInInspector] public float introSeconds = 30, landingSeconds = 25, activationSeconds = 9;
        [HideInInspector] public float discoverySeconds = 25, timeSkipSeconds = 34, finalExplorationSeconds = 48;
        [HideInInspector] public float stormSeconds = 58, lostSignalSeconds = 18, farewellSeconds = 61, transitionSeconds = 24;
        public float abrasionSeconds = 43;
    }

    public sealed class ChapterProgress
    {
       
        public static ChapterStage Next(ChapterStage stage, bool continueToNextChapter = false) => stage switch
        {
            ChapterStage.Intro => ChapterStage.Landing,
            ChapterStage.Landing => ChapterStage.Activation,
            ChapterStage.Activation => ChapterStage.Exploration,
            ChapterStage.Exploration => ChapterStage.RockAnalysis,
            ChapterStage.RockAnalysis => ChapterStage.Discovery,
            ChapterStage.Discovery => ChapterStage.HeatShieldArrival,
            ChapterStage.HeatShieldArrival => ChapterStage.HeatShieldExploration,
            ChapterStage.HeatShieldExploration => ChapterStage.MeteoriteDiscovery,
            ChapterStage.MeteoriteDiscovery => ChapterStage.TimeSkip,
            ChapterStage.TimeSkip => ChapterStage.FinalExploration,
            ChapterStage.FinalExploration => ChapterStage.DustStorm,
            ChapterStage.DustStorm => ChapterStage.CommunicationLost,
            ChapterStage.CommunicationLost => ChapterStage.Farewell,
            ChapterStage.Farewell => continueToNextChapter ? ChapterStage.Transition : ChapterStage.ChapterEnd,
            _ => ChapterStage.ChapterEnd
        };
        /// <summary>A cena começa no pouso das duas sondas, logo depois da abertura do capítulo.</summary>
        public const ChapterStage FirstStage = ChapterStage.Landing;
        public ChapterStage Stage { get; private set; } = FirstStage;
        public bool RockReached { get; private set; }
        public bool ScienceComplete { get; private set; }
        public bool HardwareStudied {get;private set;}
        public void CompleteHardware(){if(Stage==ChapterStage.HeatShieldExploration)HardwareStudied=true;}
        public bool ShelterPrepared { get; private set; }
        public bool PanoramaRecorded { get; private set; }
        public bool MemoryTransmitted { get; private set; }
        public bool ContinueToNextChapter { get; set; }
        public event Action<ChapterStage> Changed;
        public bool Advance()
        {
            if (Stage == ChapterStage.ChapterEnd) return false;
            if (Stage == ChapterStage.Exploration && !RockReached) return false;
            if (Stage == ChapterStage.RockAnalysis && !ScienceComplete) return false;
            if(Stage==ChapterStage.HeatShieldExploration&&!HardwareStudied)return false;
            if (Stage == ChapterStage.FinalExploration && !PanoramaRecorded) return false;
            if (Stage == ChapterStage.DustStorm && !MemoryTransmitted) return false;
            Set(Next(Stage,ContinueToNextChapter));
            return true;
        }
        public void ReachRock() { if (Stage == ChapterStage.Exploration) RockReached = true; }
        public void CompleteScience() { if (Stage == ChapterStage.RockAnalysis) ScienceComplete = true; }
        public void PrepareShelter() { if (Stage == ChapterStage.DustStorm) ShelterPrepared = true; }
        public void RecordPanorama() { if(Stage==ChapterStage.FinalExploration||Stage==ChapterStage.DustStorm)PanoramaRecorded=true; }
        public void TransmitMemory() { if(Stage==ChapterStage.DustStorm)MemoryTransmitted=true; }
        public void Reset() { HardwareStudied = RockReached = ScienceComplete = ShelterPrepared = PanoramaRecorded = MemoryTransmitted = false; Set(FirstStage); }
        public void SetForPreview(ChapterStage stage) { Set(stage); }
        void Set(ChapterStage stage) { Stage = stage; Changed?.Invoke(stage); }
    }
}
