using System;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>Estado do prompt E (UI_SPEC · "Estados do prompt E"). Cada um muda texto, forma da tecla e cor.</summary>
    public enum PromptState
    {
        /// <summary>Nada é desenhado: sem candidato, ou a ação não pode executar agora.</summary>
        Hidden = 0,

        /// <summary>A ação pode executar neste instante (<c>CanBeginHold</c> verdadeiro).</summary>
        Available = 1,

        /// <summary>Há candidato, mas é preciso soltar E antes (rearme). Não é "disponível".</summary>
        Rearm = 2,

        /// <summary>Hold em andamento.</summary>
        Holding = 3,

        /// <summary>A pessoa soltou E com progresso: a operação pausou e o trabalho fica preservado.</summary>
        Paused = 4,

        /// <summary>Hold concluído (feedback curto).</summary>
        Done = 5,

        /// <summary>Comando em andamento (Surveyor): substitui o E enquanto o sinal atravessa o vazio.</summary>
        Command = 6,
    }

    /// <summary>Etapas de uma ordem enviada à sonda. Quem controla a ordem (Surveyor) informa a etapa; o HUD só apresenta.</summary>
    public enum HudCommandStage
    {
        None = 0,
        Prepared = 1,
        Sent = 2,
        InTransit = 3,
        Executing = 4,
        Confirmed = 5,
    }

    /// <summary>Etapa atual do comando e quanto dela já passou.</summary>
    public readonly struct HudCommandState
    {
        public HudCommandState(HudCommandStage stage, float progress01 = 0f, float remainingSeconds = 0f)
        {
            Stage = stage;
            Progress01 = progress01;
            RemainingSeconds = remainingSeconds;
        }

        public HudCommandStage Stage { get; }

        /// <summary>Progresso da etapa (no trânsito: tempo decorrido / 1,3 s), de 0 a 1.</summary>
        public float Progress01 { get; }

        /// <summary>Segundos que faltam para o sinal chegar (usado no texto do trânsito).</summary>
        public float RemainingSeconds { get; }
    }

    /// <summary>O que o <c>IInteractionService</c> diz neste quadro. É a ÚNICA fonte de disponibilidade do prompt.</summary>
    public struct PromptSnapshot
    {
        public bool HasCandidate;
        public string CandidateId;
        public string VerbKey;
        public string ObjectKey;
        public float HoldSeconds;

        /// <summary><c>IInteractionService.CanBeginHold(id)</c>: a mesma condição que a execução usa.</summary>
        public bool CanBegin;

        public bool AwaitingRelease;
        public string HoldingId;
    }

    /// <summary>
    /// Máquina de estados do prompt E, sem Unity. Não recalcula alcance nem prioridade: apresenta o candidato ativo e
    /// a disponibilidade vindos do serviço, e reage aos quatro eventos de hold (início, progresso, soltura/cancelamento,
    /// conclusão). O progresso físico do instrumento (hold retomado com o tempo restante) chega por
    /// <see cref="ReportProgress"/>; sem ele vale o progresso do hold atual.
    /// </summary>
    public sealed class PromptModel
    {
        public const float DoneSeconds = 1.6f;
        const float Epsilon = 0.005f;

        string holdId;
        float holdProgress;

        string pausedId;
        float pausedProgress;

        string doneVerbKey;
        string doneObjectKey;
        float doneRemaining;
        bool doneFresh;

        string overrideId;
        float overrideValue;

        string lastId;
        string lastVerbKey;
        string lastObjectKey;
        bool lastNeedsHold;

        public PromptState State { get; private set; }

        /// <summary>Candidato (ou, em <see cref="PromptState.Done"/>, a ação concluída) que o prompt descreve.</summary>
        public string CandidateId { get; private set; }

        public string VerbKey { get; private set; }
        public string ObjectKey { get; private set; }

        /// <summary>A ação exige manter E (HoldSeconds &gt; 0). Falso: um aperto basta.</summary>
        public bool NeedsHold { get; private set; }

        /// <summary>Progresso 0–1 mostrado na barra.</summary>
        public float Progress { get; private set; }

        public HudCommandState Command { get; private set; }

        // ---- Eventos do serviço ----

        public void OnHoldStarted(string id)
        {
            holdId = id;
            holdProgress = 0f;
            pausedId = null;
            doneRemaining = 0f;
            doneFresh = false;
        }

        public void OnHoldProgress(string id, float progress)
        {
            if (id != holdId)
                return;

            holdProgress = Clamp01(progress);
        }

        public void OnHoldCompleted(string id)
        {
            if (id == lastId)
            {
                doneVerbKey = lastVerbKey;
                doneObjectKey = lastObjectKey;
            }
            else
            {
                doneVerbKey = null;
                doneObjectKey = null;
            }

            holdId = null;
            pausedId = null;
            doneRemaining = DoneSeconds;
            doneFresh = true; // o feedback aparece em pelo menos um quadro, mesmo com uma travada de vários segundos
            if (overrideId == id)
                overrideId = null;
        }

        /// <summary>Soltou E, pausou, perdeu foco ou o candidato mudou. Com progresso, vira "Pausado": o trabalho fica.</summary>
        public void OnHoldCanceled(string id)
        {
            var progress = overrideId == id ? overrideValue : (id == holdId ? holdProgress : 0f);
            if (id == holdId)
                holdId = null;

            if (progress > Epsilon)
            {
                pausedId = id;
                pausedProgress = progress;
            }
            else if (pausedId == id)
            {
                pausedId = null;
            }
        }

        // ---- Progresso físico do instrumento ----

        /// <summary>
        /// Progresso físico do instrumento para <paramref name="id"/> (0–1), quando difere do hold atual (por exemplo, um
        /// hold retomado só com o tempo restante). Informar 0 diz que nada foi preservado: o estado "Pausado" some.
        /// </summary>
        public void ReportProgress(string id, float fraction)
        {
            overrideId = id;
            overrideValue = Clamp01(fraction);
            if (overrideValue <= Epsilon && pausedId == id)
                pausedId = null;
        }

        public void ClearProgress(string id)
        {
            if (overrideId == id)
                overrideId = null;
        }

        // ---- Comando em trânsito (Surveyor) ----

        public void SetCommand(HudCommandState command) => Command = command;

        public void ClearCommand() => Command = default;

        /// <summary>Volta ao estado inicial: nada de um capítulo atravessa para o seguinte.</summary>
        public void Reset()
        {
            holdId = null;
            holdProgress = 0f;
            pausedId = null;
            pausedProgress = 0f;
            doneRemaining = 0f;
            doneFresh = false;
            doneVerbKey = null;
            doneObjectKey = null;
            overrideId = null;
            lastId = null;
            lastVerbKey = null;
            lastObjectKey = null;
            lastNeedsHold = false;
            Command = default;
            State = PromptState.Hidden;
            CandidateId = null;
            VerbKey = null;
            ObjectKey = null;
            NeedsHold = false;
            Progress = 0f;
        }

        /// <summary>Recalcula o estado a partir do serviço. Chamar a cada quadro com o tempo REAL (o feedback não congela na pausa).</summary>
        public void Refresh(in PromptSnapshot snapshot, float unscaledDeltaSeconds)
        {
            if (snapshot.HasCandidate)
            {
                lastId = snapshot.CandidateId;
                lastVerbKey = snapshot.VerbKey;
                lastObjectKey = snapshot.ObjectKey;
                lastNeedsHold = snapshot.HoldSeconds > 0f;
            }

            if (doneFresh)
                doneFresh = false;
            else if (doneRemaining > 0f)
                doneRemaining = Math.Max(0f, doneRemaining - Math.Max(0f, unscaledDeltaSeconds));

            // O trabalho pausado pertence ao candidato que o gerou; outro candidato ativo o invalida.
            if (pausedId != null && snapshot.HasCandidate && snapshot.CandidateId != pausedId)
                pausedId = null;

            if (Command.Stage != HudCommandStage.None)
            {
                Set(PromptState.Command, lastId, lastVerbKey, lastObjectKey, lastNeedsHold, Clamp01(Command.Progress01));
                return;
            }

            if (doneRemaining > 0f)
            {
                Set(PromptState.Done, lastId, doneVerbKey, doneObjectKey, lastNeedsHold, 1f);
                return;
            }

            if (!snapshot.HasCandidate)
            {
                Set(PromptState.Hidden, null, null, null, false, 0f);
                return;
            }

            var id = snapshot.CandidateId;
            var needsHold = snapshot.HoldSeconds > 0f;
            if (snapshot.HoldingId != null && snapshot.HoldingId == id)
            {
                var shown = overrideId == id ? overrideValue : holdProgress;
                Set(PromptState.Holding, id, snapshot.VerbKey, snapshot.ObjectKey, needsHold, shown);
            }
            else if (snapshot.AwaitingRelease)
            {
                Set(PromptState.Rearm, id, snapshot.VerbKey, snapshot.ObjectKey, needsHold, 0f);
            }
            else if (pausedId == id && snapshot.CanBegin)
            {
                var shown = overrideId == id ? overrideValue : pausedProgress;
                Set(PromptState.Paused, id, snapshot.VerbKey, snapshot.ObjectKey, needsHold, shown);
            }
            else if (snapshot.CanBegin)
            {
                Set(PromptState.Available, id, snapshot.VerbKey, snapshot.ObjectKey, needsHold, overrideId == id ? overrideValue : 0f);
            }
            else
            {
                // Há candidato, mas a ação não pode executar agora: o E não aparece.
                Set(PromptState.Hidden, id, snapshot.VerbKey, snapshot.ObjectKey, needsHold, 0f);
            }
        }

        void Set(PromptState state, string id, string verbKey, string objectKey, bool needsHold, float progress)
        {
            State = state;
            CandidateId = id;
            VerbKey = verbKey;
            ObjectKey = objectKey;
            NeedsHold = needsHold;
            Progress = Clamp01(progress);
        }

        static float Clamp01(float value)
        {
            if (float.IsNaN(value))
                return 0f;

            return Math.Min(1f, Math.Max(0f, value));
        }
    }

    /// <summary>O que o prompt desenha agora (texto já no idioma, classe do estado e progresso).</summary>
    public readonly struct PromptView
    {
        public PromptView(bool visible, string stateClass, string label, string status, float progress)
        {
            Visible = visible;
            StateClass = stateClass;
            Label = label;
            Status = status;
            Progress = progress;
        }

        public bool Visible { get; }

        /// <summary>Classe USS do estado (sa-hold--*). Vazia no estado disponível (aparência padrão).</summary>
        public string StateClass { get; }

        public string Label { get; }
        public string Status { get; }
        public float Progress { get; }

        public static PromptView Hidden => new PromptView(false, string.Empty, string.Empty, string.Empty, 0f);
    }

    /// <summary>Monta o <see cref="PromptView"/> a partir do modelo, dos textos PT/EN e do modo do E (segurar ou alternar).</summary>
    public static class PromptViewBuilder
    {
        public const string AvailableClass = "sa-hold--available";
        public const string RearmClass = "sa-hold--rearm";
        public const string HoldingClass = "sa-hold--holding";
        public const string PausedClass = "sa-hold--paused";
        public const string DoneClass = "sa-hold--done";
        public const string CommandClass = "sa-hold--command";

        public static readonly string[] AllStateClasses =
        {
            AvailableClass, RearmClass, HoldingClass, PausedClass, DoneClass, CommandClass,
        };

        public static PromptView Build(PromptModel model, DossierTextCatalog texts, string language, HoldMode mode)
        {
            if (model == null || texts == null || model.State == PromptState.Hidden)
                return PromptView.Hidden;

            var percent = HudFormat.Percent(model.Progress);
            var toggle = mode == HoldMode.Toggle;
            switch (model.State)
            {
                case PromptState.Available:
                {
                    var action = ActionText(model, texts, language);
                    var key = !model.NeedsHold ? HudKeys.PromptPress : (toggle ? HudKeys.PromptToggle : HudKeys.PromptHold);
                    var status = model.NeedsHold ? HudKeys.StatusAvailableHold : HudKeys.StatusAvailablePress;
                    return new PromptView(true, AvailableClass, Text(texts, key, language, action), Text(texts, status, language), model.Progress);
                }
                case PromptState.Rearm:
                    return new PromptView(true, RearmClass, Text(texts, HudKeys.PromptRearm, language), Text(texts, HudKeys.StatusRearm, language), 0f);
                case PromptState.Holding:
                {
                    var action = ActionText(model, texts, language);
                    var key = toggle ? HudKeys.PromptToggleStop : HudKeys.PromptHold;
                    return new PromptView(true, HoldingClass, Text(texts, key, language, action),
                        HudFormat.Fill(texts.Get(HudKeys.StatusHolding, language, HudKeys.StatusHolding), percent: percent), model.Progress);
                }
                case PromptState.Paused:
                {
                    var key = toggle ? HudKeys.PromptResumeToggle : HudKeys.PromptResumeHold;
                    return new PromptView(true, PausedClass, Text(texts, key, language),
                        HudFormat.Fill(texts.Get(HudKeys.StatusPaused, language, HudKeys.StatusPaused), percent: percent), model.Progress);
                }
                case PromptState.Done:
                {
                    var action = ActionText(model, texts, language);
                    return new PromptView(true, DoneClass, action, Text(texts, HudKeys.StatusDone, language), 1f);
                }
                case PromptState.Command:
                    return BuildCommand(model, texts, language, percent);
                default:
                    return PromptView.Hidden;
            }
        }

        static PromptView BuildCommand(PromptModel model, DossierTextCatalog texts, string language, int percent)
        {
            var command = model.Command;
            string labelKey;
            string statusKey;
            switch (command.Stage)
            {
                case HudCommandStage.Prepared:
                    labelKey = HudKeys.CommandPrepared;
                    statusKey = HudKeys.CommandStatusPrepared;
                    break;
                case HudCommandStage.Sent:
                    labelKey = HudKeys.CommandSent;
                    statusKey = HudKeys.CommandStatusSent;
                    break;
                case HudCommandStage.InTransit:
                    labelKey = HudKeys.CommandTransit;
                    statusKey = HudKeys.CommandStatusTransit;
                    break;
                case HudCommandStage.Executing:
                    labelKey = HudKeys.CommandExecuting;
                    statusKey = HudKeys.CommandStatusExecuting;
                    break;
                default:
                    labelKey = HudKeys.CommandConfirmed;
                    statusKey = HudKeys.CommandStatusConfirmed;
                    break;
            }

            var seconds = HudFormat.Seconds(command.RemainingSeconds, language);
            var label = HudFormat.Fill(texts.Get(labelKey, language, labelKey), seconds: seconds);
            var status = HudFormat.Fill(texts.Get(statusKey, language, statusKey), percent: percent);
            return new PromptView(true, CommandClass, label, status, model.Progress);
        }

        /// <summary>"verbo objeto" (ex.: "esmerilhar a rocha"). Chave sem texto aparece como a própria chave, visível ao QA.</summary>
        public static string ActionText(PromptModel model, DossierTextCatalog texts, string language)
        {
            var verb = string.IsNullOrEmpty(model.VerbKey) ? string.Empty : texts.Get(model.VerbKey, language, model.VerbKey);
            var obj = string.IsNullOrEmpty(model.ObjectKey) ? string.Empty : texts.Get(model.ObjectKey, language, model.ObjectKey);
            if (verb.Length == 0)
                return obj;

            return obj.Length == 0 ? verb : verb + " " + obj;
        }

        static string Text(DossierTextCatalog texts, string key, string language, string action = null) =>
            HudFormat.Fill(texts.Get(key, language, key), action);
    }
}
