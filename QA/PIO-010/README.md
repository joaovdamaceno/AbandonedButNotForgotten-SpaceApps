# PIO-010 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-010-percurso-orbital` (base `develop` a8143d9), Unity **6000.6.7f1** / URP 17.6, Windows, Editor aberto (Unity MCP, EditMode). Só as classes de teste da Pioneer, como combinado; a suíte completa **não** foi executada.

| Verificação | Resultado |
| --- | --- |
| `PioneerOutboundTests` (novos) | **12/12 passaram** |
| 10 classes de teste da Pioneer (Belt, Data, HistoricalImages, Imaging, Launch, Narration, Outbound, Probe, Recovery, Stage) | **112/112 passaram**. `PioneerNarrationTests` passou a esperar 102 chaves (19 novas) |
| `python Tools/validate_pioneer_data.py` | Dados conformes: 102 chaves por idioma, 45 cues. O validador recusou a primeira redação de `PIO.UI.not_edge` (continha "heliopausa", termo que o registro de fontes manda evitar) e o texto foi reescrito |
| Builder `pioneer10.stage.outbound` reexecutado (junto de `stage`) | 0 mudanças |
| Captura (`PioneerOutboundCapture`) | 10 PNGs em `Docs/Art/Pioneer10_Outbound/` (5 estados × 1280×720 e 1024×768), com os textos em português, conferidos visualmente |

Cobertura dos testes: sequência de 39 s com planos 12/12/15 e legendas iguais às dos dados da PIO-001; fases 1976 → 1983 → energia no tempo do roteiro e relógio parado no marco de 24 s sem avançar por inatividade; E mantido antes do hold não conta (rearme) e o hold confirma uma única vez; soltar preserva o progresso e o segundo hold cobre só o resto; pausa do shell e esperar sem falha; cancelar limpa candidato, contexto e progresso e repetir conclui uma vez; pular conclui uma vez sem abrir o hold; quatro anéis de espaçamento igual, sem planeta ao lado da sonda; cruzamento do gráfico dentro dele e tempo de comunicação crescente; textos nos dois idiomas, rótulos de distância ao Sol e de tempo de comunicação distintos, "não visita" / "além", nenhum termo proibido e nenhum número inventado; UXML sem frases literais.

A conferência visual das capturas moveu o rótulo de Netuno (cortado pela borda), o rótulo do Sol (cruzado por um anel), o rótulo da Pioneer (sobre o ponto de cruzamento) e o título duplicado "Distância ao Sol" no gráfico, e separou a nota "não é sair do Sistema Solar" em uma segunda linha.

**Não executado:** Play Mode, build WebGL aberta no navegador, profiler Web, FPS real, medição de contraste nos pixels, áudio e legendas reais (AUD-002/UI-004 não existem). Revisão da R7 (textos `derived`, datas de 1976 e de 13 JUN 1983, e a leitura científica em PT/EN) pendente.
