namespace Abandoned.SpaceApps.UI
{
    /// <summary>Texto do objetivo principal, em chaves de localização (sem Unity).</summary>
    public readonly struct ObjectiveData
    {
        public ObjectiveData(string id, string stampKey, string labelKey, string targetNameKey, int step, int stepCount)
        {
            Id = id;
            StampKey = stampKey;
            LabelKey = labelKey;
            TargetNameKey = targetNameKey;
            Step = step;
            StepCount = stepCount;
        }

        public string Id { get; }

        /// <summary>Carimbo de missão/local/ano (ex.: "OPPORTUNITY · MERIDIANI · 2004"). Opcional.</summary>
        public string StampKey { get; }

        /// <summary>Verbo e objeto do objetivo (ex.: "Prepare e analise a rocha"). Obrigatório.</summary>
        public string LabelKey { get; }

        /// <summary>Nome do alvo (ex.: "McKittrick"). Opcional.</summary>
        public string TargetNameKey { get; }

        /// <summary>Etapa atual de um conjunto (ex.: 2 de 3). Zero em <see cref="StepCount"/> = sem contagem.</summary>
        public int Step { get; }

        public int StepCount { get; }

        public bool HasContent => !string.IsNullOrEmpty(LabelKey);
    }

    /// <summary>Objetivo principal já no idioma.</summary>
    public readonly struct ObjectiveView
    {
        public ObjectiveView(bool visible, string stamp, string label, string meta, string targetName)
        {
            Visible = visible;
            Stamp = stamp;
            Label = label;
            Meta = meta;
            TargetName = targetName;
        }

        public bool Visible { get; }
        public string Stamp { get; }
        public string Label { get; }

        /// <summary>"2 / 3 · McKittrick · 4 m": etapa, alvo e distância, sem partes vazias.</summary>
        public string Meta { get; }

        /// <summary>Nome do alvo isolado (usado pela âncora e pela seta de borda).</summary>
        public string TargetName { get; }

        public static ObjectiveView Hidden => new ObjectiveView(false, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    public static class ObjectiveViewBuilder
    {
        /// <param name="distanceText">Distância já formatada (<see cref="HudFormat.Distance"/>); vazia quando não há alvo medível.</param>
        public static ObjectiveView Build(in ObjectiveData data, string distanceText, DossierTextCatalog texts, string language)
        {
            if (texts == null || !data.HasContent)
                return ObjectiveView.Hidden;

            var stamp = string.IsNullOrEmpty(data.StampKey) ? string.Empty : texts.Get(data.StampKey, language, data.StampKey);
            var label = texts.Get(data.LabelKey, language, data.LabelKey);
            var target = string.IsNullOrEmpty(data.TargetNameKey) ? string.Empty : texts.Get(data.TargetNameKey, language, data.TargetNameKey);
            var step = data.StepCount > 0 && data.Step > 0 ? data.Step + " / " + data.StepCount : string.Empty;
            return new ObjectiveView(true, stamp, label, HudFormat.Compose(step, target, distanceText), target);
        }
    }

    /// <summary>Medidor de energia, que só aparece quando ajuda a entender a ação.</summary>
    public readonly struct EnergyView
    {
        public EnergyView(bool visible, string label, float fraction, bool isLow)
        {
            Visible = visible;
            Label = label;
            Fraction = fraction;
            IsLow = isLow;
        }

        public bool Visible { get; }

        /// <summary>"ENERGIA · 82%" ou "ENERGIA BAIXA · 18%".</summary>
        public string Label { get; }

        public float Fraction { get; }
        public bool IsLow { get; }

        public static EnergyView Hidden => new EnergyView(false, string.Empty, 0f, false);
    }

    public static class EnergyViewBuilder
    {
        /// <summary>Abaixo disso o medidor muda de forma (texto "baixa" + âmbar), nunca só de cor.</summary>
        public const float LowThreshold = 0.2f;

        /// <param name="fraction">Energia de 0 a 1 informada pelo capítulo; nula = não mostrar.</param>
        public static EnergyView Build(float? fraction, DossierTextCatalog texts, string language)
        {
            if (!fraction.HasValue || texts == null || float.IsNaN(fraction.Value))
                return EnergyView.Hidden;

            var value = fraction.Value < 0f ? 0f : (fraction.Value > 1f ? 1f : fraction.Value);
            var low = value < LowThreshold;
            var key = low ? HudKeys.EnergyLow : HudKeys.EnergyLabel;
            var label = HudFormat.Compose(texts.Get(key, language, key), HudFormat.Percent(value) + "%");
            return new EnergyView(true, label, value, low);
        }
    }
}
