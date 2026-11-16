using UnityEngine;
using UnityEngine.InputSystem;

namespace Abandoned.SpaceApps.Opportunity
{
    // Observes real actions; it never drives, advances evidence or teleports the rover.
    public sealed class TutorialController : MonoBehaviour
    {
        const string SeenKey = "ll.tutorial.v13";
        public ChapterDirector chapter;
        public int Step { get; private set; }
        public bool Complete { get; private set; }
        public bool HelpOffered => idle >= 12;
        public bool StrongHelp => idle >= 30;
        public bool Visible => chapter && !chapter.Paused && chapter.Stage == ChapterStage.Exploration && !Complete && !chapter.investigation.Active && !chapter.journey.photography.Active;
        public string TitleKey => "v13.tutorial." + Mathf.Min(Step, 3) + ".title";
        public string BodyKey => "v13.tutorial." + Mathf.Min(Step, 3) + ".body";
        public string HelpBodyKey => chapter && chapter.investigation.Active
            ? (chapter.investigation.Result ? "v13.help.result" : "v13.help.framing")
            : chapter && chapter.science.Active
            ? (chapter.science.Phase == AbrasionPhase.Analyze ? "v13.help.analysis" : "v13.help.pressure")
            : StrongHelp ? "v13.help.strong" : "v13.help.offer";
        public float IdleSeconds => idle;
        Vector3 startPosition;
        float mouseTravel, stepAge, idle, bestDistance;
        string signature;
        bool initialized;
        ChapterStage lastStage;

        public void Init(ChapterDirector owner) { chapter = owner; ResetSession(); }
        public void ResetSession()
        {
            Complete = PlayerPrefs.GetInt(SeenKey, 0) != 0; Step = 0;
            initialized = false; idle = mouseTravel = stepAge = 0; signature = null;
        }
        public void RestartTutorial()
        {
            PlayerPrefs.SetInt(SeenKey, 0); PlayerPrefs.Save(); ResetSession();
        }
        public void RequestHelp()
        {
            if (!chapter || chapter.Paused || chapter.Cinematic || !chapter.interactions) return;
            if (chapter.interactions.FrameTarget()) chapter.hud.Toast("v13.help.framed");
            else chapter.hud.Toast(HelpBodyKey);
            idle = 0;
        }
        void ChangeStep(int next)
        {
            Step = next; stepAge = mouseTravel = 0; startPosition = chapter.rover.transform.position;
            if (next >= 4)
            {
                Complete = true; PlayerPrefs.SetInt(SeenKey, 1); PlayerPrefs.Save();
                chapter.hud.Toast("v13.tutorial.complete");
            }
        }
        void Update()
        {
            if (!chapter || !chapter.readyForLaunch) return;
            if (GameInput.Press(Key.H)) RequestHelp();
            if (lastStage != chapter.Stage)
            {
                idle = 0; signature = null; bestDistance = float.MaxValue;
                lastStage = chapter.Stage;
            }
            if (chapter.Paused || chapter.Cinematic || chapter.Stage == ChapterStage.ChapterEnd) return;
            if (!initialized)
            {
                initialized = true; startPosition = chapter.rover.transform.position; bestDistance = chapter.TargetDistance;
            }
            if (!Complete && chapter.Stage == ChapterStage.Exploration)
            {
                stepAge += Time.deltaTime;
                if (Step == 0 && Vector3.Distance(startPosition, chapter.rover.transform.position) >= 1.8f) ChangeStep(1);
                else if (Step == 1)
                {
                    mouseTravel += GameInput.MouseDelta.magnitude;
                    if (mouseTravel >= 90 || stepAge > 8 && Vector3.Distance(startPosition, chapter.rover.transform.position) > 4) ChangeStep(2);
                }
                else if (Step == 2 && stepAge >= 2 && (chapter.interactions && chapter.interactions.Current.Available || Vector3.Distance(startPosition, chapter.rover.transform.position) >= 3)) ChangeStep(3);
                if (chapter.investigation.Active || chapter.science.Active) ChangeStep(4);
            }
            var study = chapter.investigation;
            string current = chapter.Stage + ":" + chapter.journey.TraverseStep + ":" + chapter.journey.PanoramaCount + ":" + study.Study + ":" + study.Sample + ":" + study.Result + ":" + chapter.science.Phase + ":" + chapter.science.Observations;
            float distance = chapter.TargetDistance;
            bool progress = current != signature || distance < bestDistance - .75f || study.Active && study.Stable && study.Dwell > .1f || chapter.science.Active && chapter.science.Cutting;
            if (progress) { idle = 0; signature = current; bestDistance = distance; }
            else idle += Time.deltaTime;
        }
    }
}
