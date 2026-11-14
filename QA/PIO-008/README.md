# PIO-008 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-008-radiacao-revelacao` (base `develop` 53834d0), Unity **6000.6.7f1** / URP 17.6, Windows, Editor aberto (Unity MCP, EditMode). Só as classes de teste da Pioneer, como combinado; a suíte completa **não** foi executada.

| Verificação | Resultado |
| --- | --- |
| `PioneerRecoveryTests` (novos) | **16/16 passaram** |
| 9 classes de teste da Pioneer (Belt, Data, HistoricalImages, Imaging, Launch, Narration, Probe, Recovery, Stage) | **96/96 passaram** (antes das 16 novas: 80). `PioneerNarrationTests` passou a esperar 83 chaves (5 novas) |
| `python Tools/validate_pioneer_data.py` | Dados conformes: 83 chaves por idioma, 45 cues |
| Builder `pioneer10.stage.imaging.recovery` reexecutado (junto de `stage` e `stage.imaging`) | 0 mudanças |
| Captura (`PioneerRecoveryCapture`) | 12 PNGs em `Docs/Art/Pioneer10_Recovery/` (6 estados × 1280×720 e 1024×768), conferidos visualmente |

Cobertura dos testes: aviso, ruído (7 faixas) e botão único em 6/8 com as seis faixas intactas; E mantido da varredura **não** confirma; vários toques enviam um pacote e recuperam uma vez; clique e E dividem a mesma confirmação; pausa congela o pacote; mesma duração a 15/30/60 FPS; cancelar aguardando/enviando limpa tudo e uma nova execução interrompe de novo uma só vez; rota completa 6/8 → recuperação → 8/8 → revelação sem softlock; tempos da revelação alinhados às legendas PIO.04.07/08; crossfades de 1 s, monotônicos e sem salto entre quadros a 60 FPS; pausa congela e cancelar esconde as fotos; chaves de créditos, datas e "montagem"/"imagem perdida" nos dois idiomas; UXML sem frases literais e mesma geometria do painel da varredura.

A captura achou dois defeitos que os testes não pegavam, corrigidos: o ruído não aparecia (a folha de estilo estava no slot e o ruído mora fora dele) e o quadro da revelação saía deslocado (o contêiner do UIDocument tinha altura zero).

**Não executado:** Play Mode, build WebGL aberta no navegador, profiler Web, FPS real, medição de contraste WCAG nos pixels (a captura de PIO-006 fez isso para o painel; aqui foi só conferência visual), áudio e legendas reais (AUD-002/UI-004 não existem). A captura de 50% do crossfade mostra a sobreposição transitória de 1 s entre a composição e a foto, como projetado. Revisão da R7 (textos `derived`, datas e imagens lado a lado) pendente.
