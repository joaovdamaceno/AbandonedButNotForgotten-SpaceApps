using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Painel de preferências (UI-006): volumes (geral, música, efeitos, interface), idioma, sensibilidade do mouse,
    /// movimento reduzido e qualidade. Lê e grava só pelo <see cref="SettingsStore"/> (serviço global): quem consome
    /// (AudioDirector, LocalizationService, câmera) reage a <see cref="SettingsStore.Changed"/>. A qualidade é a única
    /// preferência aplicada aqui, porque é do motor (<see cref="ApplyEngineSettings"/>).
    /// <para>
    /// Mouse e teclado: cada linha é focável; <see cref="TryAdjust"/> (← →) e <see cref="TryActivate"/> (Enter) permitem
    /// operar tudo sem mouse a partir do <see cref="InputRouter"/>. O painel não lê teclas.
    /// </para>
    /// </summary>
    public sealed class SettingsPanel : IDisposable
    {
        public const float VolumeStep = 0.05f;
        public const float SensitivityStep = 0.1f;
        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 3f;

        const string RowClass = "sa-settings__row";
        const string LabelClass = "sa-settings__label";
        const string ValueClass = "sa-settings__value";
        const string ChoiceClass = "sa-settings__choice";
        const string ChoiceOnClass = "sa-settings__choice--on";

        sealed class Row
        {
            public VisualElement Element;
            public Action<int> Adjust;
            public Action Activate;
            public Action Refresh;
        }

        readonly VisualElement host;
        readonly SettingsStore store;
        readonly DossierTextCatalog texts;
        readonly List<Row> rows = new List<Row>();
        readonly List<VisualElement> focusables = new List<VisualElement>();
        SettingsData data;
        bool disposed;

        public SettingsPanel(VisualElement host, SettingsStore store, DossierTextCatalog texts)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.texts = texts ?? throw new ArgumentNullException(nameof(texts));
            data = store.Current;
            Build();
            Refresh();
            store.Changed += OnChanged;
        }

        /// <summary>Linhas operáveis, na ordem em que aparecem.</summary>
        public IReadOnlyList<VisualElement> Focusables => focusables;

        // ---------- Regras puras (testáveis) ----------

        public static float StepVolume(float current, int direction) =>
            Mathf.Clamp01(Mathf.Round((current + Mathf.Sign(direction) * VolumeStep) * 100f) / 100f);

        public static float StepSensitivity(float current, int direction) =>
            Mathf.Clamp(Mathf.Round((current + Mathf.Sign(direction) * SensitivityStep) * 10f) / 10f, MinSensitivity, MaxSensitivity);

        public static int StepQuality(int current, int count, int direction) =>
            Mathf.Clamp(current + (direction > 0 ? 1 : direction < 0 ? -1 : 0), 0, Mathf.Max(0, count - 1));

        public static string OtherLanguage(string code) =>
            code == SettingsData.English ? SettingsData.Portuguese : SettingsData.English;

        public static string FormatPercent(float fraction) =>
            Mathf.RoundToInt(Mathf.Clamp01(fraction) * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        /// <summary>PT usa vírgula decimal, EN usa ponto (sem depender de dados de cultura do runtime Web).</summary>
        public static string FormatSensitivity(float value, string languageCode)
        {
            var text = value.ToString("0.0", CultureInfo.InvariantCulture);
            return (languageCode == SettingsData.English ? text : text.Replace('.', ',')) + "×";
        }

        /// <summary>Aplica ao motor o que não depende de outro serviço (nível de qualidade). Idempotente.</summary>
        public static void ApplyEngineSettings(SettingsData settings)
        {
            if (settings == null)
                return;

            var count = QualitySettings.names.Length;
            if (count <= 0)
                return;

            var level = Mathf.Clamp(settings.qualityLevel, 0, count - 1);
            if (QualitySettings.GetQualityLevel() != level)
                QualitySettings.SetQualityLevel(level, true);
        }

        // ---------- Teclado ----------

        /// <summary>← → sobre a linha focada. Falso quando a linha não tem ajuste.</summary>
        public bool TryAdjust(VisualElement element, int direction)
        {
            var row = Find(element);
            if (row == null || row.Adjust == null || direction == 0)
                return false;

            row.Adjust(direction);
            return true;
        }

        /// <summary>Enter sobre a linha focada (liga/desliga, troca idioma, restaura padrões).</summary>
        public bool TryActivate(VisualElement element)
        {
            var row = Find(element);
            if (row == null || row.Activate == null)
                return false;

            row.Activate();
            return true;
        }

        Row Find(VisualElement element)
        {
            foreach (var row in rows)
            {
                if (row.Element == element)
                    return row;
            }

            return null;
        }

        // ---------- Construção ----------

        void Build()
        {
            host.Clear();
            rows.Clear();
            focusables.Clear();

            AddVolume(PauseKeys.VolumeMaster, s => s.masterVolume, (s, v) => s.masterVolume = v);
            AddVolume(PauseKeys.VolumeMusic, s => s.musicVolume, (s, v) => s.musicVolume = v);
            AddVolume(PauseKeys.VolumeSfx, s => s.sfxVolume, (s, v) => s.sfxVolume = v);
            AddVolume(PauseKeys.VolumeUi, s => s.uiVolume, (s, v) => s.uiVolume = v);
            AddSensitivity();
            AddReducedMotion();
            AddQuality();
            AddFullscreen();
            AddReset();
        }

        void Register(Row row)
        {
            rows.Add(row);
            focusables.Add(row.Element);
            host.Add(row.Element);
        }

        VisualElement NewRow(out Label label, out Label value)
        {
            var element = new VisualElement { focusable = true, pickingMode = PickingMode.Position };
            element.AddToClassList(RowClass);
            label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("sa-text");
            label.AddToClassList(LabelClass);
            value = new Label { pickingMode = PickingMode.Ignore };
            value.AddToClassList("sa-text");
            value.AddToClassList(ValueClass);
            element.Add(label);
            return element;
        }

        void AddVolume(string labelKey, Func<SettingsData, float> read, Action<SettingsData, float> write)
        {
            var element = NewRow(out var label, out var value);
            var slider = new Slider(0f, 100f) { showInputField = false };
            slider.AddToClassList("sa-settings__slider");
            slider.RegisterValueChangedCallback(change =>
            {
                var fraction = Mathf.Round(change.newValue) / 100f;
                if (Mathf.Abs(fraction - read(data)) > 0.001f)
                    store.Apply(s => write(s, fraction));
            });
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("sa-settings__line");
            line.Add(slider);
            line.Add(value);
            element.Add(line);

            Register(new Row
            {
                Element = element,
                Adjust = direction => store.Apply(s => write(s, StepVolume(read(s), direction))),
                Refresh = () =>
                {
                    label.text = T(labelKey);
                    slider.SetValueWithoutNotify(Mathf.Round(read(data) * 100f));
                    value.text = FormatPercent(read(data));
                },
            });
        }

        void AddSensitivity()
        {
            var element = NewRow(out var label, out var value);
            var slider = new Slider(MinSensitivity, MaxSensitivity) { showInputField = false };
            slider.AddToClassList("sa-settings__slider");
            slider.RegisterValueChangedCallback(change =>
            {
                var rounded = Mathf.Clamp(Mathf.Round(change.newValue * 10f) / 10f, MinSensitivity, MaxSensitivity);
                if (Mathf.Abs(rounded - data.lookSensitivity) > 0.001f)
                    store.Apply(s => s.lookSensitivity = rounded);
            });
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("sa-settings__line");
            line.Add(slider);
            line.Add(value);
            element.Add(line);

            Register(new Row
            {
                Element = element,
                Adjust = direction => store.Apply(s => s.lookSensitivity = StepSensitivity(s.lookSensitivity, direction)),
                Refresh = () =>
                {
                    label.text = T(PauseKeys.Sensitivity);
                    slider.SetValueWithoutNotify(data.lookSensitivity);
                    value.text = FormatSensitivity(data.lookSensitivity, data.languageCode);
                },
            });
        }

        void AddReducedMotion()
        {
            var element = NewRow(out var label, out var unused);
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("sa-settings__line");
            var toggle = NewChoice(ToggleReducedMotion);
            line.Add(toggle);
            element.Add(line);

            Register(new Row
            {
                Element = element,
                Adjust = direction => store.Apply(s => s.reducedMotion = direction > 0),
                Activate = ToggleReducedMotion,
                Refresh = () =>
                {
                    label.text = T(PauseKeys.ReducedMotion);
                    SetChoice(toggle, T(data.reducedMotion ? PauseKeys.On : PauseKeys.Off), data.reducedMotion);
                },
            });
        }

        void AddQuality()
        {
            var element = NewRow(out var label, out var unused);
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("sa-settings__line");
            var previous = NewChoice(() => StepQualityBy(-1));
            var current = new Label { pickingMode = PickingMode.Ignore };
            current.AddToClassList("sa-text");
            current.AddToClassList(ValueClass);
            var next = NewChoice(() => StepQualityBy(1));
            line.Add(previous);
            line.Add(current);
            line.Add(next);
            element.Add(line);

            Register(new Row
            {
                Element = element,
                Adjust = StepQualityBy,
                Refresh = () =>
                {
                    var names = QualitySettings.names;
                    var count = Mathf.Max(1, names.Length);
                    var level = Mathf.Clamp(data.qualityLevel, 0, count - 1);
                    label.text = T(PauseKeys.Quality);
                    SetChoice(previous, "‹ " + T(PauseKeys.Previous), false);
                    SetChoice(next, T(PauseKeys.Next) + " ›", false);
                    current.text = T(PauseKeys.QualityValue)
                        .Replace("{name}", names.Length > 0 ? names[level] : "—")
                        .Replace("{index}", (level + 1).ToString(CultureInfo.InvariantCulture))
                        .Replace("{count}", count.ToString(CultureInfo.InvariantCulture));
                },
            });
        }

        /// <summary>Tela cheia (opcional): estado do motor, não preferência salva. Só muda por clique/Enter (o navegador exige um gesto).</summary>
        void AddFullscreen()
        {
            var element = NewRow(out var label, out var unused);
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("sa-settings__line");
            var toggle = NewChoice(() => ToggleFullscreen(element));
            line.Add(toggle);
            element.Add(line);

            Register(new Row
            {
                Element = element,
                Activate = () => ToggleFullscreen(element),
                Refresh = () =>
                {
                    label.text = T(PauseKeys.Fullscreen);
                    SetChoice(toggle, T(Screen.fullScreen ? PauseKeys.On : PauseKeys.Off), Screen.fullScreen);
                },
            });
        }

        void ToggleFullscreen(VisualElement element)
        {
            Screen.fullScreen = !Screen.fullScreen;
            // No navegador a mudança é assíncrona: relê o estado logo depois.
            element.schedule.Execute(Refresh).StartingIn(400);
        }

        void AddReset()
        {
            var element = NewRow(out var label, out var unused);
            element.AddManipulator(new Clickable(ResetDefaults));
            Register(new Row
            {
                Element = element,
                Activate = ResetDefaults,
                Refresh = () => label.text = T(PauseKeys.Reset),
            });
        }

        // ---------- Ações ----------

        void SetLanguage(string code)
        {
            if (code != data.languageCode)
                store.Apply(s => s.languageCode = code);
        }

        void ToggleReducedMotion() => store.Apply(s => s.reducedMotion = !s.reducedMotion);

        void StepQualityBy(int direction)
        {
            var next = StepQuality(data.qualityLevel, QualitySettings.names.Length, direction);
            if (next != data.qualityLevel)
                store.Apply(s => s.qualityLevel = next);
        }

        void ResetDefaults() => store.ResetToDefaults();

        static VisualElement NewChoice(Action action)
        {
            var choice = new Label { focusable = false, pickingMode = PickingMode.Position };
            choice.AddToClassList("sa-text");
            choice.AddToClassList(ChoiceClass);
            choice.AddManipulator(new Clickable(action));
            return choice;
        }

        static void SetChoice(VisualElement choice, string text, bool on)
        {
            ((Label)choice).text = on ? "✓ " + text : text;
            choice.EnableInClassList(ChoiceOnClass, on);
        }

        // ---------- Atualização ----------

        void OnChanged(SettingsData changed)
        {
            data = changed;
            ApplyEngineSettings(data);
            Refresh();
        }

        /// <summary>Relê as preferências e redesenha textos e valores (idioma, painel reaberto).</summary>
        public void Refresh()
        {
            if (disposed)
                return;

            data = store.Current;
            foreach (var row in rows)
                row.Refresh?.Invoke();
        }

        string T(string key) => texts.Get(key, data.languageCode, key);

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            store.Changed -= OnChanged;
            rows.Clear();
            focusables.Clear();
            host.Clear();
        }
    }
}
