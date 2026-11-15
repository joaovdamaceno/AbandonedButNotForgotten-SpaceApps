using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>One full-picture presentation. UI and subtitles are composited after the film/monitor shader.</summary>
    public sealed class SurveyorDocumentaryView : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] Material treatment;
        [SerializeField] Texture2D archive;
        RenderTexture raw, processed;
        Material runtimeTreatment;
        VisualElement root, screen, photo, controls, reticle, subtitleHost, creditsPanel, guide, progressFill, progressTrack;
        Label date, title, objective, hint, state, credit;
        Button action, creditsButton;
        Label progressLabel;
        int lastProgress = -1;
        Label creditsTitle, creditsBody;
        bool instrument, reducedEffect;
        Camera camera;
        public event Action ActionRequested;
        public event Action PauseRequested;
        public VisualElement SubtitleHost => subtitleHost;
        public VisualElement Screen => screen;
        public RenderTexture Raw => raw;
        public void Configure(UIDocument ui, Material filter, Texture2D history) { document = ui; treatment = filter; archive = history; }
        public void Initialize(Camera output)
        {
            camera = output; root = document.rootVisualElement;
            root.style.position = Position.Absolute; root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
            screen = root.Q("picture"); photo = root.Q("archive-photo"); controls = root.Q("instrument-controls"); reticle = root.Q("tool-cursor");
            date = root.Q<Label>("date"); title = root.Q<Label>("title"); objective = root.Q<Label>("objective"); hint = root.Q<Label>("hint"); state = root.Q<Label>("tool-state"); credit = root.Q<Label>("photo-credit"); subtitleHost = root.Q("subtitle-host");
            action = root.Q<Button>("action");
            guide = root.Q("drag-guide"); progressTrack = root.Q("work-progress"); progressFill = root.Q("work-fill"); progressLabel = root.Q<Label>("work-label");
            action.clicked += () => ActionRequested?.Invoke(); root.Q<Button>("pause").clicked += () => PauseRequested?.Invoke();
            creditsButton = root.Q<Button>("credits"); creditsPanel = root.Q("credits-panel"); creditsTitle = root.Q<Label>("credits-title"); creditsBody = root.Q<Label>("credits-body");
            creditsButton.clicked += () => creditsPanel.style.display = DisplayStyle.Flex;
            root.Q<Button>("credits-close").clicked += () => creditsPanel.style.display = DisplayStyle.None;
            raw = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { name = "Surveyor documentary camera", antiAliasing = 1 }; raw.Create();
            processed = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32) { name = "Surveyor film and monitor", antiAliasing = 1 }; processed.Create();
            runtimeTreatment = new Material(treatment); camera.targetTexture = raw;
            screen.style.backgroundImage = Background.FromRenderTexture(processed); photo.style.backgroundImage = new StyleBackground(archive);
            root.RegisterCallback<GeometryChangedEvent>(_ => Layout()); SetInstrument(false);
        }
        public void SetInstrument(bool value)
        {
            instrument = value; root.EnableInClassList("instrument", value); controls.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            if (!value) guide.style.display = DisplayStyle.None;
            camera.aspect = value ? 4f / 3f : 16f / 9f; Layout();
        }
        void Layout()
        {
            if (screen == null) return;
            float w = root.resolvedStyle.width, h = root.resolvedStyle.height;
            if (float.IsNaN(w) || w < 1f || h < 1f) return;
            float aspect = instrument ? 4f / 3f : 16f / 9f;
            float sh = Mathf.Min(h, w / aspect), sw = sh * aspect;
            screen.style.width = sw; screen.style.height = sh; screen.style.left = (w - sw) * .5f; screen.style.top = (h - sh) * .5f;
        }
        public void SetText(string dateText, string titleText, string objectiveText, string instruction, string toolStatus, string actionText)
        {
            date.text = dateText; title.text = titleText; objective.text = objectiveText; hint.text = instruction; state.text = toolStatus; action.text = actionText;
            action.style.display = string.IsNullOrEmpty(actionText) ? DisplayStyle.None : DisplayStyle.Flex;
            root.Q("mission-heading").style.display = string.IsNullOrEmpty(titleText) && string.IsNullOrEmpty(dateText) ? DisplayStyle.None : DisplayStyle.Flex;
        }
        public void SetDate(string value) { if (date.text != value) date.text = value; }
        public void SetHistory(bool visible, string caption)
        { photo.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; credit.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; credit.text = caption; }
        public void SetPhoto(Texture2D image, bool visible, string caption)
        { photo.style.backgroundImage = new StyleBackground(image != null ? image : archive); SetHistory(visible, caption); }
        public void SetActionEnabled(bool enabled) => action.SetEnabled(enabled);
        public void SetEnded(bool ended, string button, string titleText, string body, string close)
        {
            root.EnableInClassList("ended", ended); creditsButton.text = button; creditsTitle.text = titleText; creditsBody.text = body; root.Q<Button>("credits-close").text = close;
            if (!ended) creditsPanel.style.display = DisplayStyle.None;
            if (ended) { progressTrack.style.display = DisplayStyle.None; guide.style.display = DisplayStyle.None; }
        }
        public void SetFeedback(float amount, bool contact, bool visible, string status)
        {
            progressTrack.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            root.EnableInClassList("tool-contact", contact); root.EnableInClassList("work-ready", amount >= 1f);
            int value = Mathf.Clamp(Mathf.RoundToInt(amount * 100f), 0, 100);
            if (value != lastProgress) { lastProgress = value; progressFill.style.width = Length.Percent(value); progressLabel.text = value + "%"; }
            if (state.text != status) state.text = status;
        }
        public void Guide(Vector3 from, Vector3 to, bool visible)
        {
            guide.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; if (!visible) return;
            var a = camera.WorldToViewportPoint(from); var b = camera.WorldToViewportPoint(to);
            guide.style.left = Mathf.Min(a.x, b.x) * screen.resolvedStyle.width;
            guide.style.top = (1f - (a.y + b.y) * .5f) * screen.resolvedStyle.height;
            guide.style.width = Mathf.Abs(b.x - a.x) * screen.resolvedStyle.width;
        }
        public void ApplyTextScale(float scale)
        {
            objective.style.fontSize = 20f * scale; hint.style.fontSize = 18f * scale; action.style.fontSize = 17f * scale;
            if (subtitleHost != null) foreach (var label in subtitleHost.Query<Label>(className: "sa-subtitle__text").ToList()) label.style.fontSize = 23f * scale;
        }
        public void SetReducedEffect(bool reduced) => reducedEffect = reduced;
        public bool TryPointer(Vector2 screenPosition, out Vector2 uv)
        {
            uv = default;
            if (screen?.panel == null) return false;
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(screen.panel, new Vector2(screenPosition.x, UnityEngine.Screen.height - screenPosition.y));
            // Controls are over the picture: their clicks never become tool commands.
            var picked = screen.panel.Pick(panelPosition);
            for (var element = picked; element != null; element = element.parent)
                if (element is Button || element.name == "instrument-controls" || element.name == "pause") return false;
            Rect r = screen.worldBound; if (!r.Contains(panelPosition)) return false;
            uv = new Vector2((panelPosition.x - r.x) / r.width, 1f - (panelPosition.y - r.y) / r.height); return true;
        }
        public void Cursor(Vector3 world, bool visible, float load)
        {
            var p = camera.WorldToViewportPoint(world); visible &= p.z > 0f && p.x > 0f && p.x < 1f && p.y > 0f && p.y < 1f;
            reticle.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            reticle.style.left = p.x * screen.resolvedStyle.width - 14f; reticle.style.top = (1f - p.y) * screen.resolvedStyle.height - 14f;
            reticle.style.opacity = Mathf.Lerp(.5f, 1f, load);
        }
        public void Render(float activeSeconds, float fade)
        {
            if (raw == null) return;
            runtimeTreatment.SetFloat("_Instrument", instrument ? 1f : 0f); runtimeTreatment.SetFloat("_Strength", reducedEffect ? .2f : 1f);
            runtimeTreatment.SetFloat("_StoryTime", activeSeconds); runtimeTreatment.SetFloat("_Fade", fade);
            Graphics.Blit(raw, processed, runtimeTreatment);
        }
        public Texture2D Capture()
        {
            var temp = RenderTexture.GetTemporary(720, 540, 0, RenderTextureFormat.ARGB32); Graphics.Blit(processed, temp);
            var previous = RenderTexture.active; RenderTexture.active = temp;
            var texture = new Texture2D(720, 540, TextureFormat.RGB24, false) { name = "Your Surveyor trench" };
            texture.ReadPixels(new Rect(0, 0, 720, 540), 0, 0); texture.Apply(false, false); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temp); return texture;
        }
        public void Dispose()
        {
            if (camera != null) camera.targetTexture = null;
            if (raw != null) { raw.Release(); Destroy(raw); raw = null; }
            if (processed != null) { processed.Release(); Destroy(processed); processed = null; }
            if (runtimeTreatment != null) Destroy(runtimeTreatment);
        }
        void OnDestroy() => Dispose();
    }
}
