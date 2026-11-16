using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>Seletor de qualidade gráfica usado nas configurações da pausa.</summary>
    public static class QualitySelector
    {
        public static void AddQualitySelector(VisualElement parent)
        {
            var selector = new DropdownField(Localization.Get("v13.settings.quality"), QualityChoices(), QualityIndex());
            selector.AddToClassList("web-quality-selector"); parent.Add(selector);
            selector.RegisterValueChangedCallback(_ =>
            {
                int index = selector.index;
                PlayerPrefs.SetInt("ll.web.auto", index == 0 ? 1 : 0);
                if (index > 0) PlayerPrefs.SetInt("ll.web.tier", index - 1);
                var quality = WebQualityController.Current;
                if (quality) { if (index == 0) quality.SetAutomatic(true); else quality.SetTier(index - 1); }
                PlayerPrefs.Save();
            });
        }
        public static void RefreshQualitySelectors(VisualElement parent)
        {
            parent.Query<DropdownField>(className:"web-quality-selector").ForEach(selector =>
            {
                selector.label = Localization.Get("v13.settings.quality"); selector.choices = QualityChoices();
                selector.SetValueWithoutNotify(selector.choices[QualityIndex()]);
            });
        }
        static int QualityIndex() => PlayerPrefs.GetInt("ll.web.auto", 1) != 0 ? 0 : 1 + Mathf.Clamp(PlayerPrefs.GetInt("ll.web.tier", 1), 0, 1);
        static System.Collections.Generic.List<string> QualityChoices() => new System.Collections.Generic.List<string>
        { Localization.Get("v13.settings.quality.auto"), Localization.Get("v13.settings.quality.low"), Localization.Get("v13.settings.quality.balanced") };
    }
}
