using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2 · HUD dos planos de cinema da Pioneer 10 (<c>UI/CinemaHud.uxml</c>). Só desenha o que um driver pede: o medidor de
    /// radiação com a estática da cena 5, a velocidade e a comparação de distâncias (Terra–Lua contra sonda–nuvens). Os textos vêm de
    /// chaves resolvidas pelo capítulo (sem frase literal no código). Nada aqui lê entrada.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CinemaHudView : MonoBehaviour
    {
        public const string RadiationKey = "PIO.UI.rad_title";
        public const string SpeedKey = "PIO.UI.speed_label";
        public const string MoonDistanceKey = "PIO.UI.dist_moon";
        public const string CloudsDistanceKey = "PIO.UI.dist_clouds";

        [SerializeField] UIDocument document;
        [SerializeField, Tooltip("Ruído tileável da estática de radiação.")] Texture2D staticTexture;

        VisualElement root, staticLayer, radiation, radiationFill, speed, distances, moonFill, cloudsFill;
        Label radiationTitle, speedLabel, moonLabel, cloudsLabel;

        public bool IsBound => root != null;
        public VisualElement Root => root;

        void OnEnable() => Bind();

        /// <summary>Liga os elementos da árvore já criada. Testes e capturas podem chamar de novo depois de trocar o documento.</summary>
        public void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("hud-root") == null)
                return;

            root = tree.Q("hud-root");
            staticLayer = tree.Q("static");
            radiation = tree.Q("radiation");
            radiationFill = tree.Q("radiation-fill");
            radiationTitle = tree.Q<Label>("radiation-title");
            speed = tree.Q("speed");
            speedLabel = tree.Q<Label>("speed-label");
            distances = tree.Q("distances");
            moonFill = tree.Q("distance-moon-fill");
            cloudsFill = tree.Q("distance-clouds-fill");
            moonLabel = tree.Q<Label>("distance-moon-label");
            cloudsLabel = tree.Q<Label>("distance-clouds-label");
            if (staticLayer != null && staticTexture != null)
            {
                staticLayer.style.backgroundImage = new StyleBackground(staticTexture);
                staticLayer.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
                staticLayer.style.backgroundSize = new BackgroundSize(staticTexture.width, staticTexture.height);
            }
        }

        /// <summary>
        /// Medidor de radiação (<paramref name="fill"/> de 0 a 1) e estática do quadro: <paramref name="staticOpacity"/> de 0 a 1 e um
        /// deslocamento <paramref name="staticOffset"/> em pixels que muda a cada quadro para o chuvisco tremer.
        /// </summary>
        public void SetRadiation(bool visible, string title, float fill, float staticOpacity, Vector2 staticOffset)
        {
            EnsureBound();
            Show(radiation, visible);
            if (visible)
            {
                if (radiationTitle != null && radiationTitle.text != title)
                    radiationTitle.text = title ?? string.Empty;
                if (radiationFill != null)
                    radiationFill.style.width = Length.Percent(Mathf.Clamp01(fill) * 100f);
            }

            var showStatic = visible && staticOpacity > 0.001f;
            Show(staticLayer, showStatic);
            if (showStatic && staticLayer != null)
            {
                staticLayer.style.opacity = Mathf.Clamp01(staticOpacity);
                staticLayer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, staticOffset.x);
                staticLayer.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, staticOffset.y);
            }
        }

        public void SetSpeed(bool visible, string text)
        {
            EnsureBound();
            Show(speed, visible);
            if (visible && speedLabel != null && speedLabel.text != text)
                speedLabel.text = text ?? string.Empty;
        }

        /// <summary>As duas barras de distância: Terra–Lua (cheia em <paramref name="moonFillFraction"/>) e sonda–nuvens (<paramref name="cloudsFillFraction"/>).</summary>
        public void SetDistances(bool visible, string moonText, string cloudsText, float moonFillFraction, float cloudsFillFraction)
        {
            EnsureBound();
            Show(distances, visible);
            if (!visible)
                return;

            if (moonLabel != null && moonLabel.text != moonText)
                moonLabel.text = moonText ?? string.Empty;
            if (cloudsLabel != null && cloudsLabel.text != cloudsText)
                cloudsLabel.text = cloudsText ?? string.Empty;
            if (moonFill != null)
                moonFill.style.width = Length.Percent(Mathf.Clamp01(moonFillFraction) * 100f);
            if (cloudsFill != null)
                cloudsFill.style.width = Length.Percent(Mathf.Clamp01(cloudsFillFraction) * 100f);
        }

        /// <summary>Esconde tudo (fora dos planos de cinema que usam o HUD).</summary>
        public void HideAll()
        {
            if (root == null)
                return;
            Show(radiation, false);
            Show(staticLayer, false);
            Show(speed, false);
            Show(distances, false);
        }

        void EnsureBound()
        {
            if (root == null)
                Bind();
        }

        static void Show(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
