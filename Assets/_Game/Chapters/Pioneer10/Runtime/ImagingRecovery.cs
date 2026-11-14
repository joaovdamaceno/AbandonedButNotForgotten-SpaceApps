using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Abandoned.SpaceApps
{
    public enum RecoveryPhase
    {
        Idle = 0,
        /// <summary>6/8: ruído localizado no quadro do instrumento, aviso e botão único de recuperação.</summary>
        Awaiting = 1,
        /// <summary>E confirmado: o pacote viaja (elipse temporal). A aquisição continua parada.</summary>
        Sending = 2,
        /// <summary>A recuperação já foi entregue à varredura. Uma só vez por execução.</summary>
        Done = 3,
    }

    /// <summary>
    /// PIO-008 · Interrupção por radiação em 6/8 e recuperação, sem falha: o instrumento recebeu comandos indevidos (a
    /// Pioneer não "travou como um computador moderno"). Mostra ruído estático e localizado só no quadro do instrumento,
    /// um aviso e UM botão de E (toque confirma; também clicável). A confirmação envia um pacote por uma elipse temporal
    /// de <see cref="SendSeconds"/> de tempo ATIVO (pausar congela; o botão não faz um sinal atravessar dezenas de minutos
    /// em um instante) e só então chama <see cref="PioneerImaging.ResumeAfterRecovery"/>, que retoma no mesmo progresso.
    /// <para>
    /// Nada pisca: o ruído é estático, o aviso não pulsa e a única animação é uma barra que enche. Sem som obrigatório e
    /// sem dano, reinício ou softlock: acionar E várias vezes envia UM pacote; cancelar a varredura reinicia o estado.
    /// </para>
    /// </summary>
    public sealed class ImagingRecovery : MonoBehaviour
    {
        public const string CandidateId = "PIO.Imaging.Recover";
        public const string RadiationKey = "PIO.UI.radiation";
        public const string RecoverKey = "PIO.UI.recover";
        public const string SentKey = "PIO.UI.sent";
        /// <summary>Duração da elipse temporal do pacote, em tempo ativo. Editorial, não é um atraso real.</summary>
        public const float SendSeconds = 4f;
        public const int NoiseBars = 7;

        [SerializeField] PioneerImaging imaging;
        [SerializeField] ImagingPanelView panel;
        [SerializeField] StyleSheet style;

        IInteractionService interaction;
        IActiveTimeSource timeSource;
        Func<string, string> resolveText;
        bool candidateOpen, sentRaised, subscribed, wasAwaiting, scanPromptHidden;
        float sendClock;
        RecoveryPhase phase;

        VisualElement host, notice, sendRow, packetFill;
        Label noticeLabel, sentLabel;
        Button button;
        VisualElement[] noise;
        string lastNotice, lastButton, lastSent;

        /// <summary>UM pacote enviado por execução (o E repetido não envia outro).</summary>
        public event Action PacketSent;
        /// <summary>A recuperação foi entregue à varredura (aquisição retomada).</summary>
        public event Action Recovered;

        public RecoveryPhase Phase => phase;
        public float SendProgress => phase == RecoveryPhase.Sending ? Mathf.Clamp01(sendClock / SendSeconds) : phase == RecoveryPhase.Done ? 1f : 0f;
        public bool IsAwaiting => phase == RecoveryPhase.Awaiting;
        public int VisibleNoiseBars => noise == null ? 0 : Array.FindAll(noise, bar => bar.style.display != DisplayStyle.None).Length;
        public Button RecoveryButton => button;

        public void Initialize(IInteractionService interactionService, IActiveTimeSource activeTime, Func<string, string> textResolver)
        {
            Reset();
            interaction = interactionService;
            timeSource = activeTime;
            resolveText = textResolver;
            if (imaging != null && !subscribed)
            {
                imaging.RadiationInterruption += OnRadiation;
                imaging.Recovered += OnImagingRecovered;
                subscribed = true;
            }
        }

        void OnRadiation()
        {
            if (phase != RecoveryPhase.Idle)
                return;

            phase = RecoveryPhase.Awaiting;
            sentRaised = false;
            sendClock = 0f;
            if (interaction != null)
            {
                candidateOpen = true;
                interaction.HoldCompleted += OnHoldCompleted;
                interaction.SetCandidate(new InteractionCandidate
                {
                    Id = CandidateId,
                    VerbKey = RecoverKey,
                    ObjectKey = RecoverKey,
                    Available = true,
                    HoldSeconds = 0f, // um toque confirma
                    Priority = 20,
                });
                // O E mantido que registrava as faixas não pode confirmar a recuperação sozinho.
                interaction.RequireRelease();
            }

            Refresh();
        }

        void OnHoldCompleted(string id)
        {
            if (id == CandidateId)
                Send();
        }

        /// <summary>Confirma a recuperação (E ou clique). Só a primeira chamada em Awaiting tem efeito.</summary>
        public bool Send()
        {
            if (phase != RecoveryPhase.Awaiting)
                return false;

            phase = RecoveryPhase.Sending;
            sendClock = 0f;
            CloseCandidate();
            if (!sentRaised)
            {
                sentRaised = true;
                PacketSent?.Invoke();
            }

            Refresh();
            return true;
        }

        void OnImagingRecovered()
        {
            // Recuperação feita por outro caminho (ex.: integração): encerra a apresentação sem repetir o evento.
            if (phase == RecoveryPhase.Awaiting || phase == RecoveryPhase.Sending)
            {
                CloseCandidate();
                phase = RecoveryPhase.Done;
                Refresh();
            }
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
            if (phase != RecoveryPhase.Idle && (imaging == null || !imaging.IsRunning))
            {
                Reset(); // a varredura foi cancelada ou terminou: nada fica pendurado
                return;
            }

            if (phase == RecoveryPhase.Sending && dt > 0f)
            {
                sendClock += dt;
                if (sendClock >= SendSeconds)
                {
                    sendClock = SendSeconds;
                    phase = RecoveryPhase.Done;
                    if (imaging.ResumeAfterRecovery())
                        Recovered?.Invoke();
                }
            }

            Refresh();
        }

        /// <summary>Prévia de Editor: mostra uma situação com valores fixos, sem tocar a varredura nem a entrada.</summary>
        public void PreviewState(RecoveryPhase previewPhase, float progress01)
        {
            phase = previewPhase;
            sendClock = Mathf.Clamp01(progress01) * SendSeconds;
            Refresh();
        }

        void CloseCandidate()
        {
            if (!candidateOpen)
                return;

            candidateOpen = false;
            if (interaction != null)
            {
                interaction.HoldCompleted -= OnHoldCompleted;
                interaction.ClearCandidate(CandidateId);
            }
        }

        /// <summary>Volta ao início (sem tocar a varredura): cancelamento, reentrada e reconstrução da cena.</summary>
        public void Reset()
        {
            CloseCandidate();
            phase = RecoveryPhase.Idle;
            sentRaised = false;
            sendClock = 0f;
            Refresh();
        }

        // ---------- UI (dentro do RecoverySlot do painel; o UXML da PIO-006 não é editado) ----------

        void Build()
        {
            var slot = panel != null ? panel.RecoverySlot : null;
            if (slot == null)
                return;

            // A folha vai à raiz do painel: o ruído mora dentro do quadro da imagem, fora do slot da recuperação.
            var sheetHost = panel.Root ?? slot;
            if (style != null && !sheetHost.styleSheets.Contains(style))
                sheetHost.styleSheets.Add(style);

            host = new VisualElement { name = "recovery-host", pickingMode = PickingMode.Ignore };
            host.AddToClassList("pio-rec");
            notice = new VisualElement { name = "recovery-notice", pickingMode = PickingMode.Ignore };
            notice.AddToClassList("sa-plate");
            notice.AddToClassList("pio-rec__notice");
            var glyph = new VisualElement { pickingMode = PickingMode.Ignore };
            glyph.AddToClassList("pio-rec__glyph"); // triângulo por borda: o aviso não depende só de cor
            noticeLabel = new Label { name = "recovery-notice-label", pickingMode = PickingMode.Ignore };
            noticeLabel.AddToClassList("pio-rec__text");
            notice.Add(glyph);
            notice.Add(noticeLabel);

            button = new Button(() => Send()) { name = "recovery-button" };
            button.AddToClassList("sa-plate");
            button.AddToClassList("pio-rec__button");
            button.focusable = true;

            sendRow = new VisualElement { name = "recovery-send", pickingMode = PickingMode.Ignore };
            sendRow.AddToClassList("pio-rec__send");
            sentLabel = new Label { name = "recovery-sent-label", pickingMode = PickingMode.Ignore };
            sentLabel.AddToClassList("pio-rec__text");
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList("pio-rec__track");
            packetFill = new VisualElement { name = "recovery-packet", pickingMode = PickingMode.Ignore };
            packetFill.AddToClassList("pio-rec__packet");
            track.Add(packetFill);
            sendRow.Add(sentLabel);
            sendRow.Add(track);

            host.Add(notice);
            host.Add(button);
            host.Add(sendRow);
            slot.Add(host);

            // Ruído localizado: barras horizontais ESTÁTICAS sobre o quadro da imagem (nada pisca nem se move).
            var image = panel.Root != null ? panel.Root.Q("image") : null;
            if (image != null)
            {
                noise = new VisualElement[NoiseBars];
                var seed = 7u;
                for (var i = 0; i < NoiseBars; i++)
                {
                    seed = seed * 1664525u + 1013904223u;
                    var bar = new VisualElement { name = "recovery-noise-" + i, pickingMode = PickingMode.Ignore };
                    bar.AddToClassList("pio-rec__noise");
                    bar.style.top = Length.Percent(6f + i * 13f + (seed >> 24) % 5u);
                    bar.style.height = 3 + (int)((seed >> 16) & 3u) * 2;
                    bar.style.opacity = 0.12f + ((seed >> 8) & 7u) * 0.02f;
                    bar.style.left = Length.Percent((seed >> 4) % 20u);
                    bar.style.right = Length.Percent((seed >> 12) % 20u);
                    image.Add(bar);
                    noise[i] = bar;
                }
            }

            lastNotice = lastButton = lastSent = null;
        }

        void Refresh()
        {
            if (panel == null)
                return;
            if (host == null || host.panel == null || host.parent != panel.RecoverySlot)
            {
                if (host != null)
                    DestroyUi();
                Build();
            }

            if (host == null)
                return;

            var awaiting = phase == RecoveryPhase.Awaiting;
            var sending = phase == RecoveryPhase.Sending;
            var active = awaiting || sending;
            host.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            button.style.display = awaiting ? DisplayStyle.Flex : DisplayStyle.None;
            sendRow.style.display = sending ? DisplayStyle.Flex : DisplayStyle.None;
            if (noise != null)
            {
                foreach (var bar in noise)
                    bar.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            }

            SetText(noticeLabel, RadiationKey, ref lastNotice);
            SetText(sentLabel, SentKey, ref lastSent);
            var text = Resolve(RecoverKey);
            if (text != lastButton)
            {
                lastButton = text;
                button.text = text;
            }

            // Um único prompt de E por vez: enquanto o botão de recuperação está à vista, o da varredura se recolhe.
            var scanPrompt = panel.Root != null ? panel.Root.Q("hold") : null;
            if (scanPrompt != null)
            {
                if (active && !scanPromptHidden)
                {
                    scanPromptHidden = true;
                    scanPrompt.style.display = DisplayStyle.None;
                }
                else if (!active && scanPromptHidden)
                {
                    scanPromptHidden = false;
                    if (phase == RecoveryPhase.Done)
                        scanPrompt.style.display = DisplayStyle.Flex; // varredura retomada: o prompt volta
                }
            }

            packetFill.style.width = Length.Percent(SendProgress * 100f);
            if (awaiting && !wasAwaiting)
                button.schedule.Execute(() => button.Focus()); // o foco vai ao botão único
            wasAwaiting = awaiting;
        }

        void DestroyUi()
        {
            host?.RemoveFromHierarchy();
            if (noise != null)
            {
                foreach (var bar in noise)
                    bar.RemoveFromHierarchy();
            }

            host = null;
            noise = null;
        }

        string Resolve(string key)
        {
            var value = resolveText != null ? resolveText(key) : null;
            return string.IsNullOrEmpty(value) ? key : value;
        }

        void SetText(TextElement element, string key, ref string last)
        {
            var value = Resolve(key);
            if (value == last)
                return;

            last = value;
            element.text = value;
        }

        /// <summary>Erros de montagem (lista vazia = conforme).</summary>
        public System.Collections.Generic.List<string> ValidateSetup()
        {
            var errors = new System.Collections.Generic.List<string>();
            const string owner = "ImagingRecovery";
            if (imaging == null)
                errors.Add($"{owner}: imaging não atribuída.");
            if (panel == null)
                errors.Add($"{owner}: panel (ImagingPanelView) não atribuído.");
            if (style == null)
                errors.Add($"{owner}: style (ImagingRecovery.uss) não atribuído.");
            return errors;
        }

        void OnDestroy()
        {
            CloseCandidate();
            if (imaging != null && subscribed)
            {
                imaging.RadiationInterruption -= OnRadiation;
                imaging.Recovered -= OnImagingRecovered;
            }
        }
    }
}
