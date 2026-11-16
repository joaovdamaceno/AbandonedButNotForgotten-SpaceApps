using System;
using System.Collections.Generic;

namespace Abandoned.SpaceApps.UI
{
    /// <summary>
    /// Espelho de <c>AppState</c> (CORE-005) para que a lógica do dossiê não dependa de MonoBehaviours.
    /// Os valores numéricos são os mesmos; <c>DossierFlowTests</c> compara nomes e valores com <c>AppState</c>.
    /// </summary>
    public enum DossierAppState
    {
        Boot = 0,
        Dossier = 1,
        Loading = 2,
        Playing = 3,
        Completion = 4,
        Unloading = 5,
    }

    /// <summary>O que o dossiê mostra agora.</summary>
    public enum DossierPhase
    {
        /// <summary>Boot, capítulo em andamento ou conclusão: o dossiê não aparece.</summary>
        Hidden = 0,

        /// <summary>Arquivo visível e interativo.</summary>
        Browsing = 1,

        /// <summary>Carregando ou descarregando uma cena: indicação de carregamento, sem novas ações.</summary>
        Loading = 2,
    }

    /// <summary>Painel sobreposto ao arquivo. Nenhum é pré-requisito para iniciar um capítulo.</summary>
    public enum DossierPanel
    {
        None = 0,
        Details = 1,
        Settings = 2,
        Credits = 3,
    }

    /// <summary>Resultado de um pedido de início. Só <see cref="Accepted"/> deve chegar ao <c>ChapterDirector</c>.</summary>
    public enum StartResult
    {
        Accepted = 0,

        /// <summary>Já existe um início pendente ou um carregamento em curso (clique duplo, troca durante o carregamento).</summary>
        Busy = 1,

        NotVisible = 2,
        UnknownFolder = 3,

        /// <summary>O capítulo ainda não tem definição válida no catálogo.</summary>
        NotAvailable = 4,

        /// <summary>Configurações ou créditos estão abertos por cima da pasta.</summary>
        PanelOpen = 5,
    }

    /// <summary>Uma pasta do dossiê, como a lógica a enxerga.</summary>
    public sealed class DossierFolderInfo
    {
        public DossierFolderInfo(string slotId, bool available)
        {
            if (string.IsNullOrEmpty(slotId))
                throw new ArgumentException("A pasta precisa de um slotId.", nameof(slotId));

            SlotId = slotId;
            Available = available;
        }

        public string SlotId { get; }

        /// <summary>False enquanto o capítulo não foi integrado ao catálogo (ou a definição é inválida).</summary>
        public bool Available { get; }
    }

    /// <summary>
    /// Regras de interação do dossiê (UI-002), sem Unity: seleção sem pré-requisito, um único início por vez,
    /// idioma travado durante o carregamento e painéis opcionais. A tela (<c>DossierScreen</c>) só desenha
    /// o estado daqui e repassa o início ao <c>ChapterDirector</c>.
    /// <para>
    /// O <c>ChapterDirector</c> já ignora <c>Begin</c> durante Loading/Unloading; este objeto impede também o
    /// segundo clique que chega ANTES de o diretor mudar de estado (mesmo quadro) e o clique em botões que a
    /// interface ainda mostrava habilitados.
    /// </para>
    /// </summary>
    public sealed class DossierFlow
    {
        public const string Portuguese = "pt";
        public const string English = "en";

        readonly Dictionary<string, bool> folders = new Dictionary<string, bool>();
        bool startPending;

        public DossierFlow(IEnumerable<DossierFolderInfo> folderInfos, string languageCode)
        {
            if (folderInfos == null)
                throw new ArgumentNullException(nameof(folderInfos));

            foreach (var info in folderInfos)
            {
                if (info == null)
                    throw new ArgumentException("Pasta nula na lista.", nameof(folderInfos));
                if (folders.ContainsKey(info.SlotId))
                    throw new ArgumentException($"Pasta '{info.SlotId}' duplicada.", nameof(folderInfos));

                folders.Add(info.SlotId, info.Available);
            }

            LanguageCode = IsSupportedLanguage(languageCode) ? languageCode : English;
        }

        public DossierPhase Phase { get; private set; } = DossierPhase.Hidden;
        public DossierPanel Panel { get; private set; } = DossierPanel.None;

        /// <summary>Pasta aberta em <see cref="DossierPanel.Details"/>; nula nos demais painéis.</summary>
        public string PanelSlotId { get; private set; }

        public string LanguageCode { get; private set; }

        /// <summary>Último erro de carregamento para mostrar no arquivo; nulo quando não há.</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>Há um início pendente ou uma cena está sendo carregada/descarregada.</summary>
        public bool IsBusy => Phase == DossierPhase.Loading || startPending;

        public bool StartPending => startPending;

        /// <summary>Disparado depois de cada mudança de estado visível.</summary>
        public event Action Changed;

        public static bool IsSupportedLanguage(string code) => code == Portuguese || code == English;

        public bool HasFolder(string slotId) => slotId != null && folders.ContainsKey(slotId);

        /// <summary>Consulta sem alterar nada (para habilitar/desabilitar o botão Iniciar).</summary>
        public StartResult CanStart(string slotId)
        {
            if (IsBusy)
                return StartResult.Busy;
            if (Phase != DossierPhase.Browsing)
                return StartResult.NotVisible;
            if (slotId == null || !folders.TryGetValue(slotId, out var available))
                return StartResult.UnknownFolder;
            if (!available)
                return StartResult.NotAvailable;
            if (Panel == DossierPanel.Settings || Panel == DossierPanel.Credits
                || (Panel == DossierPanel.Details && PanelSlotId != slotId))
                return StartResult.PanelOpen;

            return StartResult.Accepted;
        }

        /// <summary>
        /// Reserva o início. Só um pedido é aceito até o diretor sair do arquivo (Loading) ou o início ser
        /// liberado com <see cref="ReleaseStart"/>. O chamador deve então chamar <c>ChapterDirector.Begin</c>.
        /// </summary>
        public StartResult TryStart(string slotId)
        {
            var result = CanStart(slotId);
            if (result != StartResult.Accepted)
                return result;

            startPending = true;
            ErrorMessage = null;
            RaiseChanged();
            return StartResult.Accepted;
        }

        /// <summary>O <c>Begin</c> não ocorreu (exceção ou ignorado): libera o botão e, opcionalmente, mostra um erro.</summary>
        public void ReleaseStart(string errorMessage = null)
        {
            if (!startPending && errorMessage == null)
                return;

            startPending = false;
            if (errorMessage != null)
                ErrorMessage = errorMessage;
            RaiseChanged();
        }

        /// <summary>Reflete o estado do <c>ChapterDirector</c>. Chamar a cada <c>StateChanged</c> e na inicialização.</summary>
        public void OnAppState(DossierAppState state)
        {
            switch (state)
            {
                case DossierAppState.Dossier:
                    Phase = DossierPhase.Browsing;
                    startPending = false;
                    break;
                case DossierAppState.Loading:
                case DossierAppState.Unloading:
                    // startPending permanece: o carregamento é a continuação do pedido aceito.
                    Phase = DossierPhase.Loading;
                    break;
                default:
                    Phase = DossierPhase.Hidden;
                    startPending = false;
                    break;
            }

            Panel = DossierPanel.None;
            PanelSlotId = null;
            RaiseChanged();
        }

        /// <summary>O carregamento falhou. O diretor já voltou ao dossiê; mostra o erro e libera os botões.</summary>
        public void OnLoadFailed(string message)
        {
            startPending = false;
            ErrorMessage = string.IsNullOrEmpty(message) ? "?" : message;
            RaiseChanged();
        }

        public void ClearError()
        {
            if (ErrorMessage == null)
                return;

            ErrorMessage = null;
            RaiseChanged();
        }

        /// <summary>Troca o idioma pedido pela pessoa. Recusado durante o carregamento ou com um início pendente.</summary>
        public bool TrySetLanguage(string code)
        {
            if (IsBusy || !IsSupportedLanguage(code) || code == LanguageCode)
                return false;

            LanguageCode = code;
            RaiseChanged();
            return true;
        }

        /// <summary>Idioma alterado por outra tela (ex.: pausa). A fonte da verdade é a preferência gravada; sem trava.</summary>
        public void ApplyExternalLanguage(string code)
        {
            if (!IsSupportedLanguage(code) || code == LanguageCode)
                return;

            LanguageCode = code;
            RaiseChanged();
        }

        public bool TryOpenPanel(DossierPanel panel, string slotId = null)
        {
            if (panel == DossierPanel.None || IsBusy || Phase != DossierPhase.Browsing)
                return false;

            if (panel == DossierPanel.Details)
            {
                if (!HasFolder(slotId))
                    return false;
            }
            else
            {
                slotId = null;
            }

            if (Panel == panel && PanelSlotId == slotId)
                return true;

            Panel = panel;
            PanelSlotId = slotId;
            RaiseChanged();
            return true;
        }

        /// <summary>Fecha o painel aberto (retorno ao arquivo). False se não havia painel.</summary>
        public bool Back()
        {
            if (Panel == DossierPanel.None)
                return false;

            Panel = DossierPanel.None;
            PanelSlotId = null;
            RaiseChanged();
            return true;
        }

        void RaiseChanged() => Changed?.Invoke();
    }

    /// <summary>Navegação por teclado: anel de foco circular sobre uma lista linear de controles.</summary>
    public static class FocusRing
    {
        /// <summary>
        /// Próximo índice ao andar <paramref name="delta"/> passos (negativo = voltar). Com <paramref name="current"/> = -1
        /// (nada focado) o primeiro passo à frente cai no 0 e o primeiro para trás no último. Lista vazia devolve -1.
        /// </summary>
        public static int Step(int current, int count, int delta)
        {
            if (count <= 0)
                return -1;

            if (current < 0 || current >= count)
                return delta >= 0 ? 0 : count - 1;

            var next = (current + delta) % count;
            return next < 0 ? next + count : next;
        }
    }

    /// <summary>Formatação de rótulos do arquivo, sem dependência da Unity.</summary>
    public static class DossierFormat
    {
        /// <summary>Faixa editorial dos capítulos: 5–7 min de tempo ativo (ver MASTER_CONTEXT).</summary>
        public const float EditorialMinSeconds = 300f;

        public const float EditorialMaxSeconds = 420f;
        public const string EditorialRange = "5–7 MIN";

        /// <summary>
        /// Duração exibida na pasta. Dentro da faixa editorial mostra "5–7 MIN" (a promessa ao público); fora dela, o
        /// alvo real arredondado ("≈ 9 MIN"). Sem definição (capítulo ainda não integrado) mostra a faixa editorial.
        /// <para>
        /// O símbolo ≈ existe só no IBM Plex Mono (verificado contra os .ttf do projeto); a Outfit não o tem. Por isso
        /// a duração só pode ser desenhada com <c>sa-folder__duration</c> ou <c>sa-stamp</c>, que usam Plex Mono.
        /// </para>
        /// </summary>
        public static string Duration(float targetSeconds)
        {
            if (!(targetSeconds > 0f) || float.IsInfinity(targetSeconds))
                return EditorialRange;

            if (targetSeconds >= EditorialMinSeconds && targetSeconds <= EditorialMaxSeconds)
                return EditorialRange;

            var minutes = (int)Math.Round(targetSeconds / 60f, MidpointRounding.AwayFromZero);
            return "≈ " + Math.Max(1, minutes) + " MIN";
        }

        /// <summary>Numeral romano de 1 a 39 (rótulo CAPÍTULO I, II, III). Fora da faixa devolve o número.</summary>
        public static string Roman(int number)
        {
            if (number < 1 || number > 39)
                return number.ToString();

            var result = string.Empty;
            while (number >= 10)
            {
                result += "X";
                number -= 10;
            }

            if (number == 9)
                return result + "IX";
            if (number >= 5)
            {
                result += "V";
                number -= 5;
            }

            if (number == 4)
                return result + "IV";

            return result + new string('I', number);
        }
    }
}
