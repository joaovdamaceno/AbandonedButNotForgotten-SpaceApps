using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Chapters.Surveyor
{
    /// <summary>Widget reutilizável; não lê teclas, gira atores nem avança missão.</summary>
    public sealed class CommandTransitView : MonoBehaviour
    {
        VisualElement root, preparationFill, radioFill;
        Label status;
        Button confirm;
        AimDiagram diagram;
        SurveyorCommandDelay queue;
        SurveyorCommandInteraction interaction;
        SurveyorLaunchTexts texts;
        string language = SettingsData.Portuguese;
        HoldMode mode;
        public Func<string,string> TextResolver { get; set; }
        public int LastFeedbackFrames { get; private set; } = -1;
        public VisualElement Root => root;

        public void Configure(VisualElement widget, TextAsset data, SurveyorCommandDelay delay,
            SurveyorCommandInteraction action, SettingsData settings)
        {
            Unbind();
            root = widget ?? throw new ArgumentNullException(nameof(widget));
            queue = delay; interaction = action; texts = SurveyorLaunchTexts.Parse(data);
            status = root.Q<Label>("command-status"); confirm = root.Q<Button>("confirm-command");
            preparationFill = root.Q("preparation-fill"); radioFill = root.Q("radio-fill");
            var parent = root.Q("aim-diagram"); parent.Clear();
            diagram = new AimDiagram { pickingMode = PickingMode.Ignore };
            diagram.style.flexGrow = 1; parent.Add(diagram);
            confirm.RegisterCallback<PointerDownEvent>(OnDown);
            confirm.RegisterCallback<PointerUpEvent>(OnUp);
            confirm.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            queue.Changed += Refresh; queue.Sent += OnSent; interaction.Changed += Refresh;
            ApplySettings(settings);
        }
        public void ApplySettings(SettingsData settings)
        {
            language = settings.languageCode; mode = settings.holdMode;
            if (root == null) return;
            root.Q<Label>("command-kicker").text = Text("cmd.kicker");
            root.Q<Label>("command-title").text = Text("cmd.mirror");
            root.Q<Label>("desired-label").text = Text("cmd.desired");
            root.Q<Label>("actual-label").text = Text("cmd.actual");
            root.Q<Label>("transit-description").text = Text("cmd.note").Replace("{seconds}",
                SurveyorCommandDelay.OneWayDelaySeconds.ToString("0.0", language == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo("pt-BR")));
            root.Q<Label>("hold-help").text = Text(mode == HoldMode.Toggle ? "cmd.help.toggle" : "cmd.help");
            confirm.text = Text(mode == HoldMode.Toggle ? "cmd.toggle" : "cmd.hold");
            root.tooltip = Text("cmd.explanation"); Refresh();
        }
        public void SetAim(float desiredDegrees, float receivedDegrees) => diagram?.Set(desiredDegrees, receivedDegrees);
        string Text(string key) => TextResolver != null ? TextResolver(key) : texts.Get(key, language);
        void OnSent(SurveyorCommandReceipt receipt) => LastFeedbackFrames = Time.frameCount - receipt.SentFrame;
        public void Refresh()
        {
            if (root == null) return;
            string key = queue.IsPaused ? "cmd.paused" : queue.Status == CommandTransitStatus.Inactive ? "cmd.inactive" :
                queue.HasPending ? "cmd.sent" : queue.Status == CommandTransitStatus.Faulted ? "cmd.failed" :
                interaction.IsHolding ? "cmd.holding" : interaction.NeedsRelease ? "cmd.release" :
                queue.Status == CommandTransitStatus.Received ? "cmd.received" : "cmd.ready";
            status.text = Text(key);
            preparationFill.style.width = Length.Percent(interaction.PreparationProgress * 100);
            radioFill.style.width = Length.Percent(queue.Progress * 100);
            confirm.SetEnabled(interaction.IsHolding || interaction.CanConfirm);
        }
        void OnDown(PointerDownEvent e)
        {
            if (e.button != 0) return;
            if (interaction.BeginConfirmation()) confirm.CapturePointer(e.pointerId);
            e.StopPropagation();
        }
        void OnUp(PointerUpEvent e)
        {
            interaction.ReleaseConfirmation();
            if (confirm.HasPointerCapture(e.pointerId)) confirm.ReleasePointer(e.pointerId);
            e.StopPropagation();
        }
        void OnCaptureOut(PointerCaptureOutEvent e) => interaction.ReleaseConfirmation();
        void OnDisable() => Unbind();
        void Unbind()
        {
            if (queue != null) { queue.Changed -= Refresh; queue.Sent -= OnSent; }
            if (interaction != null) interaction.Changed -= Refresh;
            if (confirm != null)
            {
                confirm.UnregisterCallback<PointerDownEvent>(OnDown);
                confirm.UnregisterCallback<PointerUpEvent>(OnUp);
                confirm.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
            }
            root = null;
        }

        sealed class AimDiagram : VisualElement
        {
            float desired, actual;
            public AimDiagram() { generateVisualContent += Paint; }
            public void Set(float next, float received) { desired = next; actual = received; MarkDirtyRepaint(); }
            static void Line(Painter2D p, Vector2 a, Vector2 b) { p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
            void Paint(MeshGenerationContext context)
            {
                float w = contentRect.width, h = contentRect.height;
                if (w < 1 || h < 1) return;
                var p = context.painter2D;
                float y = h * .55f, start = w * .08f, end = w * .92f;
                float X(float degrees) => Mathf.Lerp(start, end, Mathf.InverseLerp(-60, 60, degrees));
                p.lineWidth = 1; p.strokeColor = new Color(.25f,.32f,.4f);
                Line(p,new Vector2(start,y),new Vector2(end,y));
                for (int i = 0; i <= 12; i++)
                { float x = Mathf.Lerp(start,end,i/12f); Line(p,new Vector2(x,y-7),new Vector2(x,y+7)); }
                p.strokeColor = new Color(.62f,.79f,.82f); p.lineWidth = 3;
                float d = X(desired); p.BeginPath(); p.MoveTo(new Vector2(d,y-24)); p.LineTo(new Vector2(d+24,y));
                p.LineTo(new Vector2(d,y+24)); p.LineTo(new Vector2(d-24,y)); p.ClosePath(); p.Stroke();
                float a = X(actual); p.fillColor = new Color(.93f,.75f,.44f);
                p.BeginPath(); p.MoveTo(new Vector2(a-6,y-6)); p.LineTo(new Vector2(a+6,y-6));
                p.LineTo(new Vector2(a+6,y+6)); p.LineTo(new Vector2(a-6,y+6)); p.ClosePath(); p.Fill();
            }
        }
    }
}
