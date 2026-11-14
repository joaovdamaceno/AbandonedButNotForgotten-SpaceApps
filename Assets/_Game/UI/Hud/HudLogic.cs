using System;
using System.Globalization;
using System.Text;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Espelho de <c>InputContext</c> (CORE-003) para a lógica do HUD não depender do Input System. Mesmos nomes e
    /// valores; <c>HudAssetsTests</c> compara com <c>InputContext</c>.
    /// </summary>
    public enum HudInputContext
    {
        Panel = 0,
        Gameplay = 1,
        Cinematic = 2,
        Paused = 3,
    }

    /// <summary>Quais camadas do HUD aparecem agora.</summary>
    public readonly struct HudLayers
    {
        public HudLayers(bool objective, bool prompt, bool orientation, bool energy)
        {
            Objective = objective;
            Prompt = prompt;
            Orientation = orientation;
            Energy = energy;
        }

        /// <summary>Placa do objetivo principal.</summary>
        public bool Objective { get; }

        /// <summary>Prompt E (inclui o estado de comando em trânsito).</summary>
        public bool Prompt { get; }

        /// <summary>Âncora no mundo e seta de borda.</summary>
        public bool Orientation { get; }

        public bool Energy { get; }

        public bool Any => Objective || Prompt || Orientation || Energy;

        public static HudLayers None => new HudLayers(false, false, false, false);
    }

    /// <summary>Regras de visibilidade do HUD por estado do aplicativo e contexto de entrada.</summary>
    public static class HudVisibility
    {
        /// <summary>
        /// Gameplay: tudo. Pausa: só o objetivo (reler o que fazer); orientação, prompt e energia somem porque o menu
        /// de pausa assume. Cinemática e painéis: nada (legendas e títulos são de outras camadas). Fora do capítulo
        /// em andamento (dossiê, carregamento, conclusão): nada.
        /// </summary>
        public static HudLayers For(bool chapterPlaying, HudInputContext context)
        {
            if (!chapterPlaying)
                return HudLayers.None;

            switch (context)
            {
                case HudInputContext.Gameplay:
                    return new HudLayers(true, true, true, true);
                case HudInputContext.Paused:
                    return new HudLayers(true, false, false, false);
                default:
                    return HudLayers.None;
            }
        }
    }

    /// <summary>Formatação de números e textos do HUD (PT usa vírgula decimal, EN usa ponto).</summary>
    public static class HudFormat
    {
        public const string ActionPlaceholder = "{action}";
        public const string PercentPlaceholder = "{percent}";
        public const string SecondsPlaceholder = "{seconds}";

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        enum DistanceKind
        {
            TenthsOfMeter = 0,
            Meters = 1,
            TenthsOfKilometer = 2,
            Kilometers = 3,
        }

        /// <summary>
        /// Distância curta: abaixo de 10 m com uma casa (sem ",0"), até 999 m inteira, acima disso em km. Valor inválido ou
        /// negativo devolve vazio (nada a mostrar).
        /// </summary>
        public static string Distance(float meters, string language)
        {
            if (!Quantize(meters, out var kind, out var value))
                return string.Empty;

            string number;
            string unit;
            switch (kind)
            {
                case DistanceKind.TenthsOfMeter:
                    number = Trim((value / 10d).ToString("0.0", Invariant));
                    unit = "m";
                    break;
                case DistanceKind.Meters:
                    number = value.ToString("0", Invariant);
                    unit = "m";
                    break;
                case DistanceKind.TenthsOfKilometer:
                    number = Trim((value / 10d).ToString("0.0", Invariant));
                    unit = "km";
                    break;
                default:
                    number = value.ToString("0", Invariant);
                    unit = "km";
                    break;
            }

            return Localize(number, language) + " " + unit;
        }

        /// <summary>
        /// Chave inteira que só muda quando o texto de <see cref="Distance"/> muda: as duas partem da MESMA quantização.
        /// Permite reaproveitar o texto sem alocar string por quadro. Inválido devolve -1.
        /// </summary>
        public static int DistanceKey(float meters)
        {
            if (!Quantize(meters, out var kind, out var value))
                return -1;

            var v = (int)Math.Min(900000d, value);
            switch (kind)
            {
                case DistanceKind.TenthsOfMeter:
                    return v;
                case DistanceKind.Meters:
                    return 1000 + v;
                case DistanceKind.TenthsOfKilometer:
                    return 3000 + v;
                default:
                    return 10000 + v;
            }
        }

        /// <summary>Arredonda como o texto: décimos de metro (&lt;10 m), metros, décimos de km (&lt;10 km) ou km.</summary>
        static bool Quantize(float meters, out DistanceKind kind, out double value)
        {
            kind = DistanceKind.Meters;
            value = 0d;
            if (float.IsNaN(meters) || float.IsInfinity(meters) || meters < 0f)
                return false;

            if (meters >= 999.5f) // 999,6 m arredondaria para "1000 m": já é 1 km
            {
                var km = Math.Round(meters / 1000d, 1, MidpointRounding.AwayFromZero);
                if (km >= 10d)
                {
                    kind = DistanceKind.Kilometers;
                    value = Math.Round(km, 0, MidpointRounding.AwayFromZero);
                }
                else
                {
                    kind = DistanceKind.TenthsOfKilometer;
                    value = Math.Round(km * 10d, 0, MidpointRounding.AwayFromZero);
                }
            }
            else if (meters >= 10f)
            {
                kind = DistanceKind.Meters;
                value = Math.Round(meters, 0, MidpointRounding.AwayFromZero);
            }
            else
            {
                kind = DistanceKind.TenthsOfMeter;
                value = Math.Round(meters * 10d, 0, MidpointRounding.AwayFromZero);
            }

            return true;
        }

        /// <summary>Segundos com uma casa decimal ("1,3" em PT, "1.3" em EN).</summary>
        public static string Seconds(float seconds, string language)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                seconds = 0f;

            return Localize(Math.Round(seconds, 1, MidpointRounding.AwayFromZero).ToString("0.0", Invariant), language);
        }

        /// <summary>Fração 0–1 em inteiro 0–100. Valor inválido vira 0.</summary>
        public static int Percent(float fraction)
        {
            if (float.IsNaN(fraction))
                return 0;

            return (int)Math.Round(Math.Min(1f, Math.Max(0f, fraction)) * 100f, MidpointRounding.AwayFromZero);
        }

        /// <summary>Troca os marcadores {action}, {percent} e {seconds}. Marcador ausente no modelo é ignorado.</summary>
        public static string Fill(string template, string action = null, int? percent = null, string seconds = null)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            var text = template;
            if (action != null)
                text = text.Replace(ActionPlaceholder, action);
            if (percent.HasValue)
                text = text.Replace(PercentPlaceholder, percent.Value.ToString(Invariant));
            if (seconds != null)
                text = text.Replace(SecondsPlaceholder, seconds);

            return text;
        }

        /// <summary>Junta as partes não vazias com " · " (separador do sistema visual).</summary>
        public static string Compose(params string[] parts)
        {
            var builder = new StringBuilder();
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                if (builder.Length > 0)
                    builder.Append(" · ");
                builder.Append(part);
            }

            return builder.ToString();
        }

        static string Trim(string oneDecimal) => oneDecimal.EndsWith(".0", StringComparison.Ordinal) ? oneDecimal.Substring(0, oneDecimal.Length - 2) : oneDecimal;

        static string Localize(string number, string language) => language == "en" ? number : number.Replace('.', ',');
    }

    public enum EdgeSide
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 3,
        Bottom = 4,
    }

    /// <summary>
    /// Margens (fração da tela, a partir de cada borda) do retângulo interno onde a orientação fica. O retângulo
    /// deixa livres as placas do topo e o prompt/legenda da base: a seta nunca cai sobre elas.
    /// </summary>
    public readonly struct EdgeInsets
    {
        public EdgeInsets(float left, float top, float right, float bottom)
        {
            // O centro da tela precisa continuar dentro do retângulo, senão a projeção por raio não existe.
            Left = Clamp(left);
            Top = Clamp(top);
            Right = Clamp(right);
            Bottom = Clamp(bottom);
        }

        public float Left { get; }
        public float Top { get; }
        public float Right { get; }
        public float Bottom { get; }

        /// <summary>
        /// Topo 24% (placas de objetivo e energia, até ~17% da altura), base 36% (prompt + legenda de duas linhas, que somam
        /// ~35% da altura de 720 px lógicos) e laterais 4%. Alvo na tela só é "na tela" dentro deste retângulo.
        /// </summary>
        public static EdgeInsets Default => new EdgeInsets(0.04f, 0.24f, 0.04f, 0.36f);

        static float Clamp(float v) => float.IsNaN(v) ? 0f : Math.Min(0.45f, Math.Max(0f, v));
    }

    /// <summary>Onde e como orientar o jogador até um alvo do mundo.</summary>
    public readonly struct OrientationCue
    {
        public OrientationCue(bool valid, bool onScreen, float x, float y, float angleDegrees, EdgeSide side)
        {
            Valid = valid;
            OnScreen = onScreen;
            X = x;
            Y = y;
            AngleDegrees = angleDegrees;
            Side = side;
        }

        /// <summary>False quando a projeção não é utilizável (NaN/infinito): não desenhar nada.</summary>
        public bool Valid { get; }

        /// <summary>O alvo está dentro do retângulo interno: usar a âncora no mundo. Falso: usar a seta de borda.</summary>
        public bool OnScreen { get; }

        /// <summary>Posição de 0 a 1 a partir do canto superior esquerdo (eixo Y para baixo, como a interface).</summary>
        public float X { get; }

        public float Y { get; }

        /// <summary>Direção do centro da tela até o alvo: 0 = direita, 90 = baixo, em [0, 360).</summary>
        public float AngleDegrees { get; }

        /// <summary>Lado do retângulo interno em que a seta repousa (nenhum quando o alvo está na tela).</summary>
        public EdgeSide Side { get; }

        public static OrientationCue Invalid => new OrientationCue(false, false, 0f, 0f, 0f, EdgeSide.None);
    }

    /// <summary>
    /// Projeta um alvo em coordenadas de viewport (<c>Camera.WorldToViewportPoint</c>: x e y de 0 a 1 com Y para cima,
    /// z = profundidade) para a âncora no mundo ou a seta de borda.
    /// </summary>
    public static class EdgeCueMath
    {
        const float Epsilon = 1e-5f;

        public static OrientationCue Compute(float viewportX, float viewportY, float depth, EdgeInsets insets)
        {
            if (!IsFinite(viewportX) || !IsFinite(viewportY) || !IsFinite(depth))
                return OrientationCue.Invalid;

            var sx = viewportX;
            var sy = 1f - viewportY;
            var dx = sx - 0.5f;
            var dy = sy - 0.5f;
            var behind = depth <= 0f;

            // Atrás da câmera a projeção sai espelhada: inverter a direção e nunca tratar como "na tela".
            if (behind)
            {
                dx = -dx;
                dy = -dy;
                if (Math.Abs(dx) < Epsilon && Math.Abs(dy) < Epsilon)
                    dy = 0.5f; // exatamente atrás, no eixo: aponta para baixo
            }

            var angle = Normalize((float)(Math.Atan2(dy, dx) * 180d / Math.PI));

            var left = insets.Left;
            var top = insets.Top;
            var right = 1f - insets.Right;
            var bottom = 1f - insets.Bottom;
            if (!behind && sx >= left && sx <= right && sy >= top && sy <= bottom)
                return new OrientationCue(true, true, sx, sy, angle, EdgeSide.None);

            var scale = float.PositiveInfinity;
            var side = EdgeSide.None;
            if (dx > Epsilon)
                Consider((right - 0.5f) / dx, EdgeSide.Right, ref scale, ref side);
            else if (dx < -Epsilon)
                Consider((left - 0.5f) / dx, EdgeSide.Left, ref scale, ref side);

            if (dy > Epsilon)
                Consider((bottom - 0.5f) / dy, EdgeSide.Bottom, ref scale, ref side);
            else if (dy < -Epsilon)
                Consider((top - 0.5f) / dy, EdgeSide.Top, ref scale, ref side);

            if (side == EdgeSide.None)
                return OrientationCue.Invalid;

            return new OrientationCue(true, false, 0.5f + dx * scale, 0.5f + dy * scale, angle, side);
        }

        public static OrientationCue Compute(float viewportX, float viewportY, float depth) =>
            Compute(viewportX, viewportY, depth, EdgeInsets.Default);

        static void Consider(float candidate, EdgeSide candidateSide, ref float scale, ref EdgeSide side)
        {
            if (candidate < scale)
            {
                scale = candidate;
                side = candidateSide;
            }
        }

        static float Normalize(float degrees)
        {
            var wrapped = degrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
