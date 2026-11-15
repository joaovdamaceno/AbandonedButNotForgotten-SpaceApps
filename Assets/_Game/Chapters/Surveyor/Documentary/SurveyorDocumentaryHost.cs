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
        float lastContactTime, scienceCueEnd; int contactIndex; bool initialized, contactBegan, primaryArmed, pendingPhoto, reducedEffect, pointerAllowed, keyboardSteering, cutReady;
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
        public bool CanFinishCut => scoop.Soil.TravelMeters >= 2.0f && scoop.Soil.DeepestMeters >= .055f;
        public void Initialize(ChapterDirector sharedOwner, SessionClock sharedClock, InputRouter sharedInput, InteractionService sharedInteractions, SettingsStore sharedSettings)
        {
            if (initialized) return; initialized = true;
            owner = sharedOwner != null ? sharedOwner : throw new ArgumentNullException(nameof(sharedOwner)); clock = sharedClock;
            input = sharedInput; interactions = sharedInteractions; settings = sharedSettings;
            foreach (var component in new Behaviour[] { GetComponent<ChapterDirector>(), GetComponent<SessionClock>(), GetComponent<InputRouter>(), GetComponent<InteractionService>() }) component.enabled = false;
            var problems = localization.AddTable(texts.text, texts.name); if (problems.Count > 0) throw new ArgumentException(string.Join("\n", problems));
            cameras.Initialize(outputCamera); cameras.GroundProbe = Ground;
            cameras.RegisterRig("sur.craft", film.Spacecraft); cameras.RegisterLookTarget("sur.touchdown", transform.Find("TouchdownTarget"));
            cameras.RegisterLookTarget("sur.trench", workPatch); cameras.RegisterLookTarget("sur.optical", opticalMount); cameras.RegisterLookTarget("sur.eclipse", film.Eclipse); sequence.Initialize(cameras, input, clock); sequence.enabled = false;
            view.Initialize(outputCamera); view.ActionRequested += PrimaryAction; view.PauseRequested += PauseGame; view.EffectRequested += ToggleEffect;
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
        { preferences = data; localization.ApplySettings(data); view.SetReducedEffect(reducedEffect || data.reducedMotion); view.ApplyTextScale(data.textScale); music.ApplySettings(data); if (step != null) RefreshText(); }
        void ToggleEffect() { reducedEffect = !reducedEffect; view.SetReducedEffect(reducedEffect || preferences.reducedMotion); RefreshText(); }
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
            pointerAllowed = step == "Science" && !owner.IsPaused && view.TryPointer(input.PointerPosition, out _);
            if (pointerAllowed) { keyboardSteering = false; primaryArmed = true; }
        }
        void Interact()
        {
            if (owner.IsPaused) return;
            if (step == "Science") { keyboardSteering = true; if (sciencePhase == 1 || sciencePhase == 2) primaryArmed = true; }
            if (owner.State == AppState.Completion) { owner.ReturnToDossier(); return; }
            if (step == "Science" && (sciencePhase >= 3 || sciencePhase == 1 && CanFinishCut)) PrimaryAction();
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
                sciencePhase = 0; pressSeconds = 0f; photoRead = 0f; scienceStart = clock.ActiveSeconds; keyboardSteering = false; cutReady = false; Center();
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
                if (step == "Activation" && elapsed > 14f)
                {
                    opticalOccluder.enabled = false;
                    view.SetInstrument(true);
                    var direction = (workPatch.position - opticalMount.position).normalized;
                    cameras.Apply(new CameraPose(opticalMount.position, Quaternion.LookRotation(direction), 38f));
                }
                if (step == "Findings") view.SetPhoto(playerPhoto, elapsed > 10f, localization.Get("sur.doc.your.photo"));
                if (step == "Legacy") view.SetPhoto(null, elapsed < 10f, localization.Get("sur.doc.archive.credit"));
                if (step == "Night" && elapsed > 25f) music.SetMusicLevel(.12f, 2f);
                if (step == "Arrival") DescentAudio(elapsed);
            }
        }
        void DescentAudio(float elapsed)
        {
            descentMotor.volume = preferences.masterVolume * preferences.sfxVolume * .075f * (elapsed < 25f ? 1f : Mathf.Clamp01((29f - elapsed) / 4f));
            if (elapsed < 29f && !descentMotor.isPlaying) descentMotor.Play();
            if (elapsed >= 29f) descentMotor.Stop();
            float threshold = contactIndex == 0 ? 23f : contactIndex == 1 ? 26f : 28.5f;
            if (contactIndex < 3 && elapsed >= threshold && elapsed - lastContactTime > .2f)
            { contactSound.volume = preferences.masterVolume * preferences.sfxVolume * .17f; contactSound.PlayOneShot(contacts[contactIndex++]); lastContactTime = elapsed; }
        }
        void Science(float dt, float elapsed)
        {
            film.Evaluate("Science", elapsed);
            if (sciencePhase == 0)
            {
                scoop.Deploy(Mathf.Clamp01(elapsed / 4f));
                if (elapsed >= 4f) { sciencePhase = 1; primaryArmed = !input.PrimaryHeld && !input.InteractHeld; RefreshText(); }
            }
            var data = preferences;
            cameras.Apply(new CameraPose(opticalMount.position, Quaternion.LookRotation(cameraDirection), 35f));
            if (sciencePhase == 1 || sciencePhase == 2)
            {
                bool pointer = view.TryPointer(input.PointerPosition, out var uv);
                if (input.InteractHeld) cursorLocal += new Vector3(input.Move.x, 0f, input.Move.y) * dt * .16f;
                else if (pointer && !keyboardSteering)
                {
                    var ray = outputCamera.ViewportPointToRay(new Vector3(uv.x, uv.y, 0)); var plane = new Plane(workPatch.up, workPatch.position);
                    if (plane.Raycast(ray, out float length)) cursorLocal = workPatch.InverseTransformPoint(ray.GetPoint(length));
                }
                cursorLocal.x = Mathf.Clamp(cursorLocal.x, -.28f, .27f); cursorLocal.z = Mathf.Clamp(cursorLocal.z, -.13f, .13f);
                scoop.SetAim(cursorLocal);
                bool held = input.InteractHeld || input.PrimaryHeld && pointer && pointerAllowed;
                if (!held) { contactBegan = false; primaryArmed = true; }
                else if (primaryArmed) contactBegan = true;
                if (sciencePhase == 2) scoop.SetAim(new Vector3(.16f, 0f, -.11f));
                bool placed = sciencePhase != 2 || Vector2.Distance(new Vector2(scoop.TipLocal.x, scoop.TipLocal.z), new Vector2(.16f, -.11f)) < .025f;
                scoop.Tick(dt, contactBegan && held && placed, sciencePhase == 2, data.masterVolume * data.sfxVolume, false);
                if (sciencePhase == 2 && contactBegan && held && scoop.InGroundContact)
                {
                    pressSeconds += dt;
                    if (pressSeconds >= 3.5f) { sciencePhase = 3; ClearContact(); ShowScience("sur.doc.science.edge"); Debug.Log("SURVEYOR_DOC_SCIENCE 3"); RefreshText(); }
                }
                view.Cursor(workPatch.TransformPoint(sciencePhase == 2 && scoop.InGroundContact ? scoop.ContactPointLocal : scoop.AimLocal), true, scoop.Load);
                view.SetActionEnabled(sciencePhase != 1 || CanFinishCut);
                if (sciencePhase == 1 && CanFinishCut && !cutReady) { cutReady = true; Debug.Log("SURVEYOR_DOC_CUT_READY"); }
            }
            else if (sciencePhase >= 3)
            {
                scoop.Tick(dt, false, false, data.masterVolume * data.sfxVolume, false); view.Cursor(Vector3.zero, false, 0f);
                if (sciencePhase == 4)
                {
                    photoRead += dt;
                    if (photoRead >= 6f && scoop.IsRetracted) owner.CompleteStep("Science");
                }
            }
        }
        void PrimaryAction()
        {
            if (owner.IsPaused) return;
            if (owner.State == AppState.Completion) { owner.ReturnToDossier(); return; }
            if (step != "Science") return;
            if (sciencePhase == 1 && CanFinishCut)
            { sciencePhase = 2; ClearContact(); ShowScience("sur.doc.science.press"); RefreshText(); }
            else if (sciencePhase == 3 && !pendingPhoto)
            { pendingPhoto = true; scoop.Retract(); }
        }
        void LateUpdate()
        {
            if (!initialized) return;
            float elapsed = clock.ActiveSeconds - stepStart;
            float fade = step == "End" ? 0f : step == "Legacy" ? Mathf.Clamp01((15f - elapsed) / 2f) : step == "Activation" ? Mathf.Clamp01(Mathf.Abs(elapsed - 14f) / .4f) : 1f;
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
                objective = localization.Get("sur.doc.goal." + sciencePhase); hint = localization.Get("sur.doc.hint." + sciencePhase);
                status = localization.Get("sur.doc.machine"); button = sciencePhase == 1 ? localization.Get("sur.doc.finish.cut") : sciencePhase == 3 ? localization.Get("sur.doc.take.photo") : "";
            }
            else if (step == "End") button = localization.Get("sur.doc.return");
            view.SetText(localization.Get(date), localization.Get(title), objective, hint, status, button, localization.Get(reducedEffect ? "sur.doc.effect.low" : "sur.doc.effect.full"));
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
