using System;
using Abandoned.SpaceApps.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Composition and explicit mission progress; science, film, view and actors remain separate components.</summary>
    public sealed class SurveyorDocumentaryHost : MonoBehaviour
    {
        [SerializeField] ChapterDefinition chapter;
        [SerializeField] TextAsset texts;
        [SerializeField] SequenceDefinition[] tracks;
        [SerializeField] Camera outputCamera;
        [SerializeField] CameraDirector cameras;
        [SerializeField] SequenceDirector sequence;
        [SerializeField] SurveyorDocumentaryFilm film;
        [SerializeField] SurveyorDocumentaryView view;
        [SerializeField] SurveyorDirectScoop scoop;
        [SerializeField] Transform opticalMount, workPatch;
        [SerializeField] PauseMenu pause;
        [SerializeField] AudioCatalog audio;
        [SerializeField] AudioSource descentMotor, contactSound;
        [SerializeField] AudioClip[] contacts;
        [SerializeField] Renderer opticalOccluder;
        readonly LocalizationService localization = new LocalizationService();
        ChapterDirector owner; SessionClock clock; InputRouter input; InteractionService interactions; SettingsStore settings;
        AudioDirector music; SubtitlePresenter subtitles;
        SettingsData preferences;
        IDisposable scienceInput, scienceCamera;
        string step; float stepStart, scienceStart, pressSeconds, photoRead;
        string feedbackState, feedbackText;
        float lastContactTime, scienceCueEnd; int contactIndex; bool initialized, contactBegan, primaryArmed, pendingPhoto, pointerAllowed, cutReady;
        int sciencePhase;
        Vector3 cameraDirection, cursorLocal = new Vector3(-.22f, 0f, 0f);
        Texture2D playerPhoto;
        SubtitleCue scienceCue;
        public ChapterDirector Owner => owner;
        public SessionClock Clock => clock;
        public SurveyorDirectScoop Scoop => scoop;
        public SequenceDirector Sequence => sequence;
        public SurveyorDocumentaryView View => view;
        public LocalizationService Localization => localization;
        public int SciencePhase => sciencePhase;
        public float CutProgress => Mathf.Clamp01(Mathf.Min(scoop.Soil.TravelMeters / .60f, scoop.Soil.DeepestMeters / .028f));
        public bool CanFinishCut => CutProgress >= 1f;
        public void Initialize(ChapterDirector sharedOwner, SessionClock sharedClock, InputRouter sharedInput, InteractionService sharedInteractions, SettingsStore sharedSettings)
        {
            if (initialized) return; initialized = true;
            owner = sharedOwner != null ? sharedOwner : throw new ArgumentNullException(nameof(sharedOwner)); clock = sharedClock;
            input = sharedInput; interactions = sharedInteractions; settings = sharedSettings;
            foreach (var component in new Behaviour[] { GetComponent<ChapterDirector>(), GetComponent<SessionClock>(), GetComponent<InputRouter>(), GetComponent<InteractionService>() }) component.enabled = false;
            var problems = localization.AddTable(texts.text, texts.name); if (problems.Count > 0) throw new ArgumentException(string.Join("\n", problems));
            cameras.Initialize(outputCamera); cameras.GroundProbe = Ground;
            cameras.RegisterRig("sur.craft", film.Spacecraft); cameras.RegisterLookTarget("sur.touchdown", transform.Find("TouchdownTarget"));
            cameras.RegisterLookTarget("sur.landing", transform.Find("LandingFrame"));
            cameras.RegisterLookTarget("sur.reunion", transform.Find("ReunionFrame"));
            cameras.RegisterLookTarget("sur.trench", workPatch); cameras.RegisterLookTarget("sur.optical", opticalMount); cameras.RegisterLookTarget("sur.eclipse", film.Eclipse); sequence.Initialize(cameras, input, clock); sequence.enabled = false;
            view.Initialize(outputCamera); view.ActionRequested += PrimaryAction; view.PauseRequested += PauseGame;
            subtitles = GetComponent<SubtitlePresenter>(); subtitles.Initialize(localization, sequence, view.SubtitleHost);
            music = GetComponent<AudioDirector>(); music.Initialize(audio, clock, settings.Current); music.Unlock();
            pause.Initialize(owner, input, settings); GetComponent<FocusPause>().Initialize(pause.Controller, owner, music);
            var nativeCorner = pause.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement.Q("pause-corner"); if (nativeCorner != null) nativeCorner.style.display = UnityEngine.UIElements.DisplayStyle.None;
            owner.StepEntered += Enter; owner.ChapterUnloading += Unload; owner.PausedChanged += Paused; sequence.Finished += Finished;
            input.InteractDown += Interact; input.InteractUp += Release; input.PrimaryDown += PointerDown; input.PrimaryUp += PointerUp; input.InputCleared += ClearContact; input.HelpDown += Center;
            settings.Changed += ApplySettings; ApplySettings(settings.Current); film.Initialize(); scoop.Initialize();
        }
        static float? Ground(Vector3 p) => SurveyorLunarSurface.Height(p.x, p.z);
        void Start()
        {
            if (initialized) return;
            // Direct Play follows the same archive-owned lifecycle, including restart/unload, as the Web edition.
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Game/Chapters/Surveyor/Scenes/SurveyorArchive.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("SurveyorArchive", UnityEngine.SceneManagement.LoadSceneMode.Single);
#endif
        }
        void ApplySettings(SettingsData data)
        { preferences = data; feedbackState = null; localization.ApplySettings(data); view.SetReducedEffect(data.reducedMotion); view.ApplyTextScale(data.textScale); music.ApplySettings(data); if (step != null) RefreshText(); }
        public void PauseGame() => pause.Controller?.RequestPause();
        public void Resume() => pause.Controller?.Continue();
        public void Restart() => owner.RestartChapter();
        void Paused(bool value)
        { ClearContact(); music.SetPaused(value); if (value) { descentMotor.Pause(); contactSound.Pause(); } else { descentMotor.UnPause(); contactSound.UnPause(); } }
        void ClearContact() { contactBegan = false; primaryArmed = false; pointerAllowed = false; scoop.StopContact(); }
        void Release() { contactBegan = false; primaryArmed = true; }
        void PointerUp() { pointerAllowed = false; Release(); }
        void PointerDown()
        {
            pointerAllowed = step == "Science" && sciencePhase <= 1 && !owner.IsPaused && view.TryPointer(input.PointerPosition, out _);
            if (pointerAllowed) primaryArmed = true;
        }
        void Interact()
        {
            if (owner.IsPaused) return;
            if (step == "Science" && sciencePhase == 2) primaryArmed = true;
            if (owner.State == AppState.Completion) { owner.ReturnToDossier(); return; }
        }
        void Center() { cameraDirection = (workPatch.position - opticalMount.position).normalized; cursorLocal = new Vector3(-.22f, 0f, 0f); }
        void Enter(string id)
        {
            ClearContact(); scienceInput?.Dispose(); scienceInput = null; scienceCamera?.Dispose(); scienceCamera = null;
            step = id; stepStart = clock.ActiveSeconds; subtitles.Clear(); view.SetHistory(false, ""); view.SetInstrument(false);
            opticalOccluder.enabled = id != "Science";
            Debug.Log("SURVEYOR_DOC_STEP " + id + " active=" + clock.ActiveSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            film.Evaluate(id, 0f);
            if (id == "Science")
            {
                sciencePhase = 0; pressSeconds = 0f; photoRead = 0f; scienceStart = clock.ActiveSeconds; cutReady = false; Center();
                scienceInput = input.Acquire(InputContext.Gameplay, "Surveyor direct instrument"); scienceCamera = cameras.AcquireControl("Surveyor optical instrument"); view.SetInstrument(true);
                ShowScience("sur.doc.science.question"); music.SetCue("sur.doc.science", 2f);
            }
            else if (id == "End")
            {
                music.StopAll(0f);
                owner.CompleteStep("End"); scienceInput = input.Acquire(InputContext.Panel, "Surveyor finished");
                view.SetInstrument(true); RefreshText(); Debug.Log("SURVEYOR_DOC_COMPLETE active=" + clock.ActiveSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)); return;
            }
            else
            {
                int index = id == "Prologue" ? 0 : id == "Arrival" ? 1 : id == "Activation" ? 2 : id == "Findings" ? 3 : id == "Night" ? 4 : id == "Reunion" ? 5 : 6;
                sequence.Play(tracks[index]);
                music.SetCue(id == "Reunion" || id == "Legacy" ? "sur.doc.return" : id == "Findings" ? "sur.doc.science" : "sur.doc.departure", 2f);
                if (id == "Night") music.SetMusicLevel(.65f, 2f); else music.SetMusicLevel(1f, 2f);
            }
            RefreshText();
        }
        void Finished(SequenceResult result)
        {
            if (result.Reason == SequenceExitReason.Canceled || owner.State != AppState.Playing) return;
            owner.CompleteStep(step);
        }
        void ShowScience(string key)
        { if (scienceCue != null) subtitles.HideCue(scienceCue); scienceCue = new SubtitleCue(key, key, 0, 12); scienceCueEnd = clock.ActiveSeconds + 10f; subtitles.ShowCue(scienceCue); }
        void Update() { if (initialized) Tick(clock.ActiveDeltaSeconds); }
        public void Tick(float activeDeltaSeconds)
        {
            if (!initialized || owner.State != AppState.Playing || owner.IsPaused) return;
            float dt = activeDeltaSeconds, elapsed = clock.ActiveSeconds - stepStart;
            if (scienceCue != null && clock.ActiveSeconds >= scienceCueEnd) { subtitles.HideCue(scienceCue); scienceCue = null; }
            if (step == "Science") Science(dt, elapsed);
            else
            {
                string evaluating = step; film.Evaluate(step, sequence.TimeSeconds + dt); sequence.Tick(dt);
                if (step != evaluating) return;
                if (step == "Activation" && elapsed > SurveyorDocumentaryTiming.InstrumentEntry)
                {
                    opticalOccluder.enabled = false;
                    view.SetInstrument(true);
                    var direction = (workPatch.position - opticalMount.position).normalized;
                    cameras.Apply(new CameraPose(opticalMount.position, Quaternion.LookRotation(direction), 35f));
                }
                if (step == "Findings") view.SetPhoto(playerPhoto, elapsed > 8f, localization.Get("sur.doc.your.photo"));
                if (step == "Legacy") view.SetPhoto(null, elapsed < 8f, localization.Get("sur.doc.archive.credit"));
                if (step == "Night" && elapsed > 25f) music.SetMusicLevel(.12f, 2f);
                if (step == "Night") view.SetDate(localization.Get(elapsed < 18f ? "sur.doc.date.eclipse" : elapsed < 30f ? "sur.doc.date.dayslater" : "sur.doc.date.lastcontact"));
                if (step == "Arrival") DescentAudio(elapsed);
            }
        }
        void DescentAudio(float elapsed)
        {
            float cut = SurveyorLandingMotion.FinalContactSeconds + .1f;
            descentMotor.volume = preferences.masterVolume * preferences.sfxVolume * .06f * Mathf.Clamp01((cut - elapsed) / 2f);
            if (elapsed < cut && !descentMotor.isPlaying) descentMotor.Play();
            if (elapsed >= cut) descentMotor.Stop();
            float threshold = SurveyorLandingMotion.ContactTime(Mathf.Min(contactIndex, 2));
            if (contactIndex < 3 && elapsed >= threshold && elapsed - lastContactTime > .2f)
            { contactSound.volume = preferences.masterVolume * preferences.sfxVolume * (contactIndex == 0 ? .15f : contactIndex == 1 ? .11f : .08f); contactSound.PlayOneShot(contacts[contactIndex++]); lastContactTime = elapsed; }
        }
        void Science(float dt, float elapsed)
        {
            film.Evaluate("Science", elapsed);
            if (sciencePhase == 0)
            {
                scoop.Deploy(Mathf.Clamp01(elapsed / SurveyorDocumentaryTiming.Deploy));
                if (elapsed >= SurveyorDocumentaryTiming.Deploy) { sciencePhase = 1; primaryArmed |= !input.PrimaryHeld; RefreshText(); }
            }
            var data = preferences;
            cameras.Apply(new CameraPose(opticalMount.position, Quaternion.LookRotation(cameraDirection), 35f));
            if (sciencePhase == 1 || sciencePhase == 2)
            {
                bool pointer = view.TryPointer(input.PointerPosition, out var uv);
                if (sciencePhase == 1 && pointer)
                {
                    var ray = outputCamera.ViewportPointToRay(new Vector3(uv.x, uv.y, 0)); var plane = new Plane(workPatch.up, workPatch.position);
                    if (plane.Raycast(ray, out float length)) cursorLocal = workPatch.InverseTransformPoint(ray.GetPoint(length));
                }
                cursorLocal.x = Mathf.Clamp(cursorLocal.x, -.28f, .27f); cursorLocal.z = Mathf.Clamp(cursorLocal.z, -.13f, .13f);
                scoop.SetAim(cursorLocal);
                bool held = sciencePhase == 1 ? input.PrimaryHeld && pointer && pointerAllowed : input.InteractHeld;
                if (!held) { contactBegan = false; primaryArmed = true; }
                else if (primaryArmed) contactBegan = true;
                if (sciencePhase == 2) scoop.SetAim(new Vector3(.16f, 0f, -.11f));
                bool placed = sciencePhase != 2 || Vector2.Distance(new Vector2(scoop.TipLocal.x, scoop.TipLocal.z), new Vector2(.16f, -.11f)) < .025f;
                scoop.Tick(dt, contactBegan && held && placed, sciencePhase == 2, data.masterVolume * data.sfxVolume, false);
                if (sciencePhase == 2 && contactBegan && held && scoop.InGroundContact)
                {
                    pressSeconds += dt;
                    if (pressSeconds >= SurveyorDocumentaryTiming.Pressure)
                    { sciencePhase = 3; ClearContact(); ScienceConfirmation(); scoop.Retract(); pendingPhoto = true; ShowScience("sur.doc.science.edge"); Debug.Log("SURVEYOR_DOC_SCIENCE 3"); RefreshText(); }
                }
                view.Cursor(workPatch.TransformPoint(sciencePhase == 2 && scoop.InGroundContact ? scoop.ContactPointLocal : scoop.AimLocal), true, scoop.Load);
                view.SetActionEnabled(sciencePhase != 1 || CanFinishCut);
                if (sciencePhase == 1 && CanFinishCut && !cutReady) { cutReady = true; ScienceConfirmation(); RefreshText(); Debug.Log("SURVEYOR_DOC_CUT_READY"); }
                if (sciencePhase == 1 && CanFinishCut && !input.PrimaryHeld)
                { sciencePhase = 2; ClearContact(); primaryArmed = true; ShowScience("sur.doc.science.press"); Debug.Log("SURVEYOR_DOC_PRESS_READY"); RefreshText(); }
            }
            else if (sciencePhase >= 3)
            {
                scoop.Tick(dt, false, false, data.masterVolume * data.sfxVolume, false); view.Cursor(Vector3.zero, false, 0f);
                if (sciencePhase == 4)
                {
                    photoRead += dt;
                    if (photoRead >= SurveyorDocumentaryTiming.ResultRead && scoop.IsRetracted) owner.CompleteStep("Science");
                }
            }
            string feedback = sciencePhase == 0 ? "lowering" : sciencePhase == 1 ? CanFinishCut ? "cut.ready" : scoop.InGroundContact ? "cutting" : contactBegan ? "lowering" : "lifted"
                : sciencePhase == 2 ? scoop.InGroundContact ? "pressing" : "positioning" : sciencePhase == 3 ? "recording" : "received";
            if (feedbackState != feedback) { feedbackState = feedback; feedbackText = localization.Get("sur.doc.feedback." + feedback); }
            view.SetFeedback(sciencePhase < 2 ? CutProgress : sciencePhase == 2 ? pressSeconds / SurveyorDocumentaryTiming.Pressure : 1f, scoop.InGroundContact, true, feedbackText);
            view.Guide(workPatch.TransformPoint(new Vector3(-.23f, .005f, 0f)), workPatch.TransformPoint(new Vector3(.23f, .005f, 0f)), sciencePhase == 1 && !CanFinishCut);
        }
        void ScienceConfirmation()
        { contactSound.volume = preferences.masterVolume * preferences.sfxVolume * .12f; contactSound.PlayOneShot(contacts[2]); }
        void PrimaryAction()
        {
            if (owner.IsPaused) return;
            if (owner.State == AppState.Completion) { owner.ReturnToDossier(); return; }
        }
        void LateUpdate()
        {
            if (!initialized) return;
            float elapsed = clock.ActiveSeconds - stepStart;
            float fade = step == "End" ? 0f : step == "Legacy" ? Mathf.Clamp01((SurveyorDocumentaryTiming.Legacy - elapsed) / 2f) : step == "Activation" ? Mathf.Clamp01(Mathf.Abs(elapsed - SurveyorDocumentaryTiming.InstrumentEntry) / .35f) : 1f;
            view.Render(clock.ActiveSeconds, fade);
            if (!pendingPhoto || owner.IsPaused || !scoop.IsRetracted) return;
            pendingPhoto = false; playerPhoto = view.Capture();
            var sprite = Sprite.Create(playerPhoto, new Rect(0, 0, playerPhoto.width, playerPhoto.height), new Vector2(.5f, .5f));
            owner.Evidence.Add(new EvidenceRecord("sur.doc.player.trench", EvidenceKind.Photo, "sur.doc.science.result", "S3-03", EvidenceAuthorship.PlayerCapture, sprite));
            sciencePhase = 4; photoRead = 0f; ShowScience("sur.doc.science.result"); RefreshText();
        }
        void RefreshText()
        {
            string title = "sur.doc.title." + step, date = "sur.doc.date." + step;
            string objective = "", hint = "", status = "", button = "";
            if (step == "Science")
            {
                objective = localization.Get(sciencePhase == 1 && CanFinishCut ? "sur.doc.goal.cut.ready" : "sur.doc.goal." + sciencePhase);
                hint = localization.Get(sciencePhase == 1 && CanFinishCut ? "sur.doc.hint.cut.ready" : "sur.doc.hint." + sciencePhase);
                status = localization.Get("sur.doc.machine");
            }
            else if (step == "End") button = localization.Get("sur.doc.return");
            view.SetText(localization.Get(date), localization.Get(title), objective, hint, status, button);
            view.SetEnded(step == "End", localization.Get("sur.doc.credits.button"), localization.Get("sur.doc.credits.title"), localization.Get("sur.doc.credits.body"), localization.Get("sur.doc.credits.close"));
        }
        void Unload(string id)
        {
            ClearContact(); scienceInput?.Dispose(); scienceInput = null; scienceCamera?.Dispose(); scienceCamera = null;
            sequence.Cancel(); music.StopAll(0f); descentMotor.Stop(); contactSound.Stop(); view.Dispose(); subtitles.Clear();
        }
        void OnDestroy()
        {
            if (!initialized) return;
            owner.StepEntered -= Enter; owner.ChapterUnloading -= Unload; owner.PausedChanged -= Paused; sequence.Finished -= Finished;
            input.InteractDown -= Interact; input.InteractUp -= Release; input.PrimaryDown -= PointerDown; input.PrimaryUp -= PointerUp; input.InputCleared -= ClearContact; input.HelpDown -= Center; settings.Changed -= ApplySettings;
            scienceInput?.Dispose(); scienceCamera?.Dispose();
        }
    }
}
