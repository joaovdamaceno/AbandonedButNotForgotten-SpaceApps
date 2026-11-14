using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>O que a revelação desenha em um instante (função pura do tempo: testável sem painel).</summary>
    public struct RevealViewState
    {
        /// <summary>0–1: opacidade do quadro inteiro sobre a composição (a composição fica por baixo).</summary>
        public float Frame;
        /// <summary>0–1: a montagem de Júpiter (PIO.04.06/07).</summary>
        public float Jupiter;
        /// <summary>0–1: Ganimedes (PIO.04.08/09). Cobre Júpiter por inteiro quando chega a 1.</summary>
        public float Ganymede;
        /// <summary>Io é lembrada como imagem perdida, enquanto a montagem de Júpiter está à vista.</summary>
        public bool IoLost;
        public bool Visible;
    }

    /// <summary>
    /// PIO-008 · Revelação das duas imagens históricas depois de 8/8. Em <see cref="PioneerImaging.RevealStarted"/> (48 s)
    /// a composição das oito faixas já está montada; aos <see cref="JupiterAtSeconds"/> a montagem real de Júpiter
    /// entra por crossfade de <see cref="FadeSeconds"/> e aos <see cref="GanymedeAtSeconds"/> (63 s) a de Ganimedes, na
    /// resolução modesta em que a Pioneer 10 a registrou (sem ampliar nem "melhorar"). Os tempos seguem as legendas
    /// PIO.04.06–09. Crédito, ano e data vêm de chaves; Io aparece como imagem PERDIDA (não existe imagem dela) e
    /// Ganimedes como recebida. A passagem é só opacidade, uma vez, sem movimento nem piscar, e é puramente decorativa:
    /// não bloqueia a sequência nem depende de som.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HistoricalImageReveal : MonoBehaviour
    {
        public const string RealKey = "PIO.UI.real";
        public const string JupiterKey = "PIO.UI.jupiter";
        public const string GanymedeKey = "PIO.UI.ganymede";
        public const string IoLostKey = "PIO.UI.io_lost";
        /// <summary>Segundos depois de 48 s (legenda PIO.04.07, 55 s): a montagem de Júpiter começa a entrar.</summary>
        public const float JupiterAtSeconds = 7f;
        /// <summary>Segundos depois de 48 s (legenda PIO.04.08, 63 s): Ganimedes começa a entrar.</summary>
        public const float GanymedeAtSeconds = 15f;
        public const float FadeSeconds = 1f;

        [SerializeField] UIDocument document;
        [SerializeField] PioneerImaging imaging;
        [SerializeField] Texture2D jupiterTexture;
        [SerializeField] Texture2D ganymedeTexture;

        IActiveTimeSource timeSource;
        Func<string, string> resolveText;
        bool subscribed, revealing;
        float clock;

        VisualElement root, frame, jupiterLayer, ganymedeLayer, ioTag;
        Label realLabel, captionLabel, ioLabel;
        string lastCaption, lastReal, lastIo;
        bool lastVisible;

        public bool IsRevealing => revealing;
        public float Clock => clock;
        public VisualElement Root => root;

        void OnEnable() => Bind();

        void OnDisable() => root = null;

        public void Initialize(IActiveTimeSource activeTime, Func<string, string> textResolver)
        {
            Stop();
            timeSource = activeTime;
            resolveText = textResolver;
            if (imaging != null && !subscribed)
            {
                imaging.RevealStarted += OnRevealStarted;
                subscribed = true;
            }
        }

        void OnRevealStarted()
        {
            revealing = true;
            clock = 0f;
            Render();
        }

        /// <summary>Escurece tudo sem tocar a varredura (cancelamento, reentrada e reconstrução).</summary>
        public void Stop()
        {
            revealing = false;
            clock = 0f;
            Render();
        }

        void Update()
        {
            if (timeSource is UnityEngine.Object unityObject && unityObject == null)
                timeSource = null;

            Tick(timeSource != null ? timeSource.ActiveDeltaSeconds : 0f);
        }

        /// <summary>Avança com tempo ATIVO (zero na pausa). Testes chamam direto.</summary>
        public void Tick(float dt)
        {
            if (revealing && (imaging == null || !imaging.IsRunning))
            {
                Stop();
                return;
            }

            if (revealing && dt > 0f)
                clock += dt;
            Render();
        }

        /// <summary>Estado da revelação aos <paramref name="seconds"/> depois de 48 s.</summary>
        public static RevealViewState Evaluate(float seconds)
        {
            var jupiter = Smooth((seconds - JupiterAtSeconds) / FadeSeconds);
            var ganymede = Smooth((seconds - GanymedeAtSeconds) / FadeSeconds);
            return new RevealViewState
            {
                Frame = Mathf.Max(jupiter, ganymede),
                Jupiter = jupiter,
                Ganymede = ganymede,
                IoLost = jupiter > 0f && ganymede < 1f,
                Visible = jupiter > 0f,
            };
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Prévia de Editor: mostra a revelação aos <paramref name="seconds"/> depois de 48 s, sem sequência.</summary>
        public void PreviewTime(float seconds)
        {
            revealing = true;
            clock = seconds;
            Render();
        }

        public RevealViewState ViewState() => revealing ? Evaluate(clock) : default;

        void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("reveal-root") == null || root == tree.Q("reveal-root"))
                return;

            root = tree.Q("reveal-root");
            // O contêiner do UIDocument só tem filhos absolutos e ficaria com altura zero: sem isto, os percentuais do quadro
            // (que devem coincidir com o painel da varredura) seriam relativos a uma caixa vazia.
            tree.style.position = Position.Absolute;
            tree.style.left = 0f;
            tree.style.top = 0f;
            tree.style.right = 0f;
            tree.style.bottom = 0f;
            tree.pickingMode = PickingMode.Ignore;
            frame = root.Q("reveal-frame");
            jupiterLayer = root.Q("reveal-jupiter");
            ganymedeLayer = root.Q("reveal-ganymede");
            ioTag = root.Q("reveal-io");
            realLabel = root.Q<Label>("reveal-real");
            captionLabel = root.Q<Label>("reveal-caption");
            ioLabel = root.Q<Label>("reveal-io-label");
            if (jupiterTexture != null)
                jupiterLayer.style.backgroundImage = new StyleBackground(jupiterTexture);
            if (ganymedeTexture != null)
                ganymedeLayer.style.backgroundImage = new StyleBackground(ganymedeTexture);
            lastCaption = lastReal = lastIo = null;
            lastVisible = true;
        }

        void Render()
        {
            if (root == null)
                Bind();
            if (root == null)
                return;

            var state = ViewState();
            if (state.Visible != lastVisible)
            {
                lastVisible = state.Visible;
                root.style.display = state.Visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            frame.style.opacity = state.Frame;
            jupiterLayer.style.opacity = state.Jupiter;
            ganymedeLayer.style.opacity = state.Ganymede;
            ioTag.style.display = state.IoLost ? DisplayStyle.Flex : DisplayStyle.None;
            SetText(realLabel, RealKey, ref lastReal);
            SetText(captionLabel, state.Ganymede >= 0.5f ? GanymedeKey : JupiterKey, ref lastCaption);
            SetText(ioLabel, IoLostKey, ref lastIo);
        }

        void SetText(TextElement element, string key, ref string last)
        {
            var value = resolveText != null ? resolveText(key) : null;
            if (string.IsNullOrEmpty(value))
                value = key;
            if (value == last)
                return;

            last = value;
            element.text = value;
        }

        /// <summary>Erros de montagem (lista vazia = conforme).</summary>
        public System.Collections.Generic.List<string> ValidateSetup()
        {
            var errors = new System.Collections.Generic.List<string>();
            const string owner = "HistoricalImageReveal";
            if (imaging == null)
                errors.Add($"{owner}: imaging não atribuída.");
            if (jupiterTexture == null)
                errors.Add($"{owner}: falta a imagem de Júpiter (PIO-007).");
            if (ganymedeTexture == null)
                errors.Add($"{owner}: falta a imagem de Ganimedes (PIO-007).");
            var doc = document != null ? document : GetComponent<UIDocument>();
            if (doc == null || doc.visualTreeAsset == null)
            {
                errors.Add($"{owner}: UIDocument sem UXML.");
                return errors;
            }

            if (doc.panelSettings == null)
                errors.Add($"{owner}: UIDocument sem PanelSettings.");
            if (GetComponent<Abandoned.SpaceApps.UI.ResponsiveLayoutClass>() == null)
                errors.Add($"{owner}: falta ResponsiveLayoutClass.");
            var tree = doc.visualTreeAsset.CloneTree();
            foreach (var name in new[] { "reveal-root", "reveal-frame", "reveal-jupiter", "reveal-ganymede", "reveal-io", "reveal-real", "reveal-caption", "reveal-io-label" })
            {
                if (tree.Q(name) == null)
                    errors.Add($"{owner}: o UXML não tem o elemento '{name}'.");
            }

            return errors;
        }

        void OnDestroy()
        {
            if (imaging != null && subscribed)
                imaging.RevealStarted -= OnRevealStarted;
        }
    }
}
