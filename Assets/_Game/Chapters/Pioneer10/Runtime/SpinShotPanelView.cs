using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    /// <summary>
    /// Roteiro v2, cena 4 · painel do jogo da foto em ponto de vista (<c>UI/SpinShotPanel.uxml</c>): a mira desenhada sobre o palco 3D
    /// (a câmera está dentro da sonda e gira com ela), a barra da volta, os quadros vermelho e azul de cada foto, o clarão da foto e a
    /// junção automática. Só desenha o <see cref="SpinShotViewState"/>; textos vêm de chaves resolvidas pelo capítulo.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SpinShotPanelView : MonoBehaviour
    {
        public const string ShootKey = "PIO.UI.shoot";
        public const string ShotCountKey = "PIO.UI.shot_count";
        public const string FramesKey = "PIO.UI.frames";
        public const string SumKey = "PIO.UI.sum";
        public const string NoiseKey = "PIO.UI.noise";
        public const int Pairs = PioneerImaging.SpinShotCount;

        /// <summary>
        /// Raio da mira como fração da altura do quadro: o ângulo da janela (<see cref="PioneerImaging.SpinWindowHalfDegrees"/>) visto por
        /// uma câmera de <see cref="PovFov"/> graus de campo vertical, para Júpiter caber na mira quando a foto vale.
        /// </summary>
        public static readonly float ReticleRadiusFraction =
            Mathf.Tan(PioneerImaging.SpinWindowHalfDegrees * Mathf.Deg2Rad) / Mathf.Tan(PovFov * 0.5f * Mathf.Deg2Rad) * 0.5f;

        /// <summary>O campo de visão do plano (igual a <c>PioneerStageBuilder.ImagingPovFov</c>, que mora no Editor).</summary>
        public const float PovFov = 40f;

        static readonly string[] HoldClasses =
        {
            "sa-hold--available", "sa-hold--rearm", "sa-hold--holding", "sa-hold--paused", "sa-hold--done",
        };

        [SerializeField] UIDocument document;

        VisualElement root, reticle, flash, film, aim, track, zone, cursor, prompt, combine, composite, noiseFill;
        VisualElement[] redThumbs, blueThumbs;
        Label filmTitle, shotLabel, promptLabel, sumLabel, noiseLabel;
        readonly Texture2D[] redTextures = new Texture2D[Pairs];
        readonly Texture2D[] blueTextures = new Texture2D[Pairs];
        readonly Texture2D[] composites = new Texture2D[Pairs + 1];
        bool reticleActive, lastVisible;
        readonly bool[] redHas = new bool[Pairs];
        readonly bool[] blueHas = new bool[Pairs];
        string lastShot, lastPrompt, lastTitle, lastSum, lastNoise;
        float lastZoneStart = -1f, lastZoneEnd = -1f;
        int lastCompositeStep = -1;
        SpinShotPhase lastPhase = (SpinShotPhase)(-1);
        ImagingHoldState lastHold = (ImagingHoldState)(-1);

        public bool IsBound => root != null;
        public VisualElement Root => root;
        public SpinShotViewState LastState { get; private set; }

        void OnEnable() => Bind();

        void OnDisable()
        {
            root = null;
            DisposeTextures();
        }

        void OnDestroy() => DisposeTextures();

        /// <summary>Liga os elementos do UXML. Idempotente; chamado no OnEnable e pela prévia de Editor.</summary>
        public void Bind()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var tree = document != null ? document.rootVisualElement : null;
            if (tree == null || tree.Q("spin-root") == null)
                return;
            if (root == tree.Q("spin-root"))
                return;

            root = tree.Q("spin-root");
            reticle = root.Q("reticle");
            flash = root.Q("flash");
            film = root.Q("film");
            aim = root.Q("aim");
            track = root.Q("track");
            zone = root.Q("zone");
            cursor = root.Q("cursor");
            prompt = root.Q("prompt");
            combine = root.Q("combine");
            composite = root.Q("composite");
            noiseFill = root.Q("noise-fill");
            filmTitle = root.Q<Label>("film-title");
            shotLabel = root.Q<Label>("shot-label");
            promptLabel = root.Q<Label>("prompt-label");
            sumLabel = root.Q<Label>("sum-label");
            noiseLabel = root.Q<Label>("noise-label");
            redThumbs = new VisualElement[Pairs];
            blueThumbs = new VisualElement[Pairs];
            for (var i = 0; i < Pairs; i++)
            {
                redThumbs[i] = root.Q("frame-red-" + i);
                blueThumbs[i] = root.Q("frame-blue-" + i);
            }

            reticle.generateVisualContent -= DrawReticle;
            reticle.generateVisualContent += DrawReticle;
            lastShot = lastPrompt = lastTitle = lastSum = lastNoise = null;
            lastZoneStart = lastZoneEnd = -1f;
            lastCompositeStep = -1;
            lastPhase = (SpinShotPhase)(-1);
            lastVisible = false;
            Array.Clear(redHas, 0, Pairs);
            Array.Clear(blueHas, 0, Pairs);
            lastHold = (ImagingHoldState)(-1);
        }

        public void Render(SpinShotViewState state, Func<string, string> text)
        {
            if (root == null)
                Bind();
            if (root == null)
                return;

            LastState = state;
            var visible = state.Phase != SpinShotPhase.Hidden && !state.Revealed;
            if (visible != lastVisible || state.Phase != lastPhase)
            {
                lastVisible = visible;
                lastPhase = state.Phase;
                root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (!visible)
                return;

            var aiming = state.Phase == SpinShotPhase.Learn || state.Phase == SpinShotPhase.Shoot;
            var merging = state.Phase == SpinShotPhase.Combine || state.Phase == SpinShotPhase.Done;
            Show(reticle, aiming);
            Show(aim, aiming);
            Show(combine, merging);
            Show(film, true);

            // Mira: dourada enquanto Júpiter preenche a mira (nunca só cor: o contorno engrossa e a barra também acende).
            if (reticleActive != (aiming && state.InWindow))
            {
                reticleActive = aiming && state.InWindow;
                reticle.MarkDirtyRepaint();
            }

            // Barra da volta.
            if (!Mathf.Approximately(state.WindowStart, lastZoneStart) || !Mathf.Approximately(state.WindowEnd, lastZoneEnd))
            {
                lastZoneStart = state.WindowStart;
                lastZoneEnd = state.WindowEnd;
                zone.style.left = Length.Percent(state.WindowStart * 100f);
                zone.style.width = Length.Percent((state.WindowEnd - state.WindowStart) * 100f);
            }

            cursor.style.left = Length.Percent(Mathf.Clamp01(state.Angle01) * 100f);
            zone.EnableInClassList("pio-spin__zone--active", state.InWindow);
            zone.EnableInClassList("pio-spin__zone--wide", state.Assisted);
            zone.EnableInClassList("pio-spin__zone--captured", state.PassCaptured);
            cursor.EnableInClassList("pio-spin__cursor--in", state.InWindow);
            prompt.EnableInClassList("pio-spin__prompt--outside", state.PressedOutside);
            SetHoldState(state.Hold);

            // Textos.
            SetText(promptLabel, ShootKey, text, ref lastPrompt);
            SetText(shotLabel, ShotCountKey, text, ref lastShot, Mathf.Min(state.Shots + 1, state.ShotTarget));
            SetText(filmTitle, FramesKey, text, ref lastTitle);
            shotLabel.style.display = state.Phase == SpinShotPhase.Shoot ? DisplayStyle.Flex : DisplayStyle.None;

            // Clarão da foto.
            flash.style.opacity = state.Flash * 0.4f;

            // Quadros: o vermelho aparece com a foto e o azul logo depois (entrelaçados), ambos tirados juntos.
            var merged = state.Phase == SpinShotPhase.Combine || state.Phase == SpinShotPhase.Done;
            var step = merged ? CombineStep(state.CombineProgress) : 0;
            for (var i = 0; i < Pairs; i++)
            {
                var redDone = i < state.Shots;
                var blueDone = i < state.Shots - 1 || (i == state.Shots - 1 && state.Flash < 0.7f);
                SetThumb(redThumbs[i], redTextures, redHas, i, ImagingChannel.Red, redDone);
                SetThumb(blueThumbs[i], blueTextures, blueHas, i, ImagingChannel.Blue, blueDone);
                redThumbs[i].EnableInClassList("pio-spin__thumb--use", merged && i < step);
                blueThumbs[i].EnableInClassList("pio-spin__thumb--use", merged && i < step);
            }

            if (merged)
                RenderCombine(state, text, step);
        }

        /// <summary>Quantas fotos já entraram na soma (1 a 5) no progresso <paramref name="progress"/>: as cinco entram nos primeiros 75% do tempo.</summary>
        public static int CombineStep(float progress) => Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Clamp01(progress) / 0.75f * Pairs), 1, Pairs);

        void RenderCombine(SpinShotViewState state, Func<string, string> text, int step)
        {
            if (step != lastCompositeStep)
            {
                lastCompositeStep = step;
                var texture = composites[step];
                if (texture == null)
                {
                    texture = ImagingFrameSynth.CompositeTexture(ImagingFrameSynth.Average(ImagingChannel.Red, step),
                        ImagingFrameSynth.Average(ImagingChannel.Blue, step), "PIO_SpinComposite" + step);
                    composites[step] = texture;
                }

                composite.style.backgroundImage = new StyleBackground(texture);
            }

            // O ruído cai com a raiz do número de fotos somadas.
            noiseFill.style.width = Length.Percent(100f / Mathf.Sqrt(step));
            SetText(sumLabel, SumKey, text, ref lastSum, step);
            SetText(noiseLabel, NoiseKey, text, ref lastNoise);
        }

        void SetThumb(VisualElement thumb, Texture2D[] cache, bool[] has, int index, ImagingChannel channel, bool filled)
        {
            thumb.EnableInClassList("pio-spin__thumb--filled", filled);
            if (!filled)
            {
                if (has[index])
                {
                    has[index] = false;
                    thumb.style.backgroundImage = StyleKeyword.None;
                }

                return;
            }

            if (has[index])
                return;

            if (cache[index] == null)
                cache[index] = ImagingFrameSynth.ChannelTexture(ImagingFrameSynth.Frame(channel, index), channel, "PIO_SpinFrame" + channel + index);
            thumb.style.backgroundImage = new StyleBackground(cache[index]);
            has[index] = true;
        }

        void DrawReticle(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (rect.width < 1f || rect.height < 1f)
                return;

            var painter = context.painter2D;
            var color = reticleActive ? new Color(1f, 0.82f, 0.4f, 1f) : new Color(1f, 1f, 1f, 0.78f);
            var center = rect.center;
            var radius = rect.height * ReticleRadiusFraction;
            painter.strokeColor = color;
            painter.lineCap = LineCap.Round;
            painter.lineWidth = reticleActive ? 5f : 3f;

            // Anel em quatro arcos, com folga nos pontos cardeais e um traço para fora em cada folga.
            for (var quadrant = 0; quadrant < 4; quadrant++)
            {
                var start = quadrant * 90f + 14f;
                painter.BeginPath();
                painter.Arc(center, radius, Angle.Degrees(start), Angle.Degrees(start + 62f));
                painter.Stroke();
            }

            painter.lineWidth = 3f;
            for (var i = 0; i < 4; i++)
            {
                var angle = i * 90f * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                painter.BeginPath();
                painter.MoveTo(center + direction * (radius - 14f));
                painter.LineTo(center + direction * (radius + 14f));
                painter.Stroke();
            }

            // Cruz central com vazio no meio.
            for (var i = 0; i < 4; i++)
            {
                var angle = i * 90f * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                painter.BeginPath();
                painter.MoveTo(center + direction * 9f);
                painter.LineTo(center + direction * 22f);
                painter.Stroke();
            }

            // Cantos do visor.
            painter.lineWidth = 3f;
            var margin = Mathf.Min(rect.width, rect.height) * 0.07f;
            var length = Mathf.Min(rect.width, rect.height) * 0.055f;
            var corners = new[]
            {
                (new Vector2(rect.xMin + margin, rect.yMin + margin), new Vector2(1f, 1f)),
                (new Vector2(rect.xMax - margin, rect.yMin + margin), new Vector2(-1f, 1f)),
                (new Vector2(rect.xMin + margin, rect.yMax - margin), new Vector2(1f, -1f)),
                (new Vector2(rect.xMax - margin, rect.yMax - margin), new Vector2(-1f, -1f)),
            };
            painter.strokeColor = new Color(1f, 1f, 1f, 0.45f);
            foreach (var (corner, direction) in corners)
            {
                painter.BeginPath();
                painter.MoveTo(corner + new Vector2(direction.x * length, 0f));
                painter.LineTo(corner);
                painter.LineTo(corner + new Vector2(0f, direction.y * length));
                painter.Stroke();
            }
        }

        void SetHoldState(ImagingHoldState hold)
        {
            if (hold == lastHold)
                return;

            lastHold = hold;
            for (var i = 0; i < HoldClasses.Length; i++)
                prompt.EnableInClassList(HoldClasses[i], i == (int)hold);
        }

        void DisposeTextures()
        {
            Dispose(redTextures);
            Dispose(blueTextures);
            Dispose(composites);
            lastCompositeStep = -1;
            Array.Clear(redHas, 0, Pairs);
            Array.Clear(blueHas, 0, Pairs);
        }

        static void Dispose(Texture2D[] textures)
        {
            for (var i = 0; i < textures.Length; i++)
            {
                if (textures[i] == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(textures[i]);
                else
                    DestroyImmediate(textures[i]);
                textures[i] = null;
            }
        }

        /// <summary>Erros de montagem (lista vazia = conforme): UIDocument com UXML, <see cref="Abandoned.SpaceApps.UI.ResponsiveLayoutClass"/> e os elementos usados.</summary>
        public System.Collections.Generic.List<string> ValidateSetup()
        {
            var errors = new System.Collections.Generic.List<string>();
            const string owner = "SpinShotPanelView";
            var doc = document != null ? document : GetComponent<UIDocument>();
            if (doc == null)
            {
                errors.Add($"{owner}: sem UIDocument.");
                return errors;
            }

            if (doc.panelSettings == null)
                errors.Add($"{owner}: UIDocument sem PanelSettings.");
            if (GetComponent<Abandoned.SpaceApps.UI.ResponsiveLayoutClass>() == null)
                errors.Add($"{owner}: falta ResponsiveLayoutClass (UI_SPEC: todo UIDocument de tela cheia).");
            if (doc.visualTreeAsset == null)
            {
                errors.Add($"{owner}: UIDocument sem UXML.");
                return errors;
            }

            var tree = doc.visualTreeAsset.CloneTree();
            var required = new System.Collections.Generic.List<string>
            {
                "spin-root", "reticle", "flash", "film", "film-title", "aim", "shot-label", "track", "zone", "cursor", "prompt", "prompt-label",
                "combine", "composite", "sum-label", "noise-label", "noise-fill",
            };
            for (var i = 0; i < Pairs; i++)
            {
                required.Add("frame-red-" + i);
                required.Add("frame-blue-" + i);
            }

            foreach (var name in required)
            {
                if (tree.Q(name) == null)
                    errors.Add($"{owner}: o UXML não tem o elemento '{name}'.");
            }

            foreach (var key in new[] { ShootKey, ShotCountKey, FramesKey, SumKey, NoiseKey })
            {
                if (string.IsNullOrEmpty(key) || !key.StartsWith("PIO.UI."))
                    errors.Add($"{owner}: chave de texto inválida '{key}'.");
            }

            return errors;
        }

        static void Show(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Texto por chave; "{count}" recebe o número. Sem resolvedor, mostra a chave (nunca uma frase inventada).</summary>
        static void SetText(TextElement element, string key, Func<string, string> text, ref string last, int count = -1)
        {
            if (element == null)
                return;

            var value = text != null ? text(key) : null;
            if (string.IsNullOrEmpty(value))
                value = key;
            if (count >= 0)
                value = value.Replace("{count}", count.ToString());
            if (value == last)
                return;

            last = value;
            element.text = value;
        }
    }
}
