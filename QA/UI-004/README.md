# UI-004 — evidência de validação

Executado em 14/11/2026 sobre `feature/UI-004-localizacao-legendas` (base `develop` 69e9b97), Unity **6000.6.7f1** / URP 17.6, Windows, Editor aberto (Unity MCP, EditMode). Só as classes de teste de localização, áudio e parte da Pioneer; a suíte completa **não** foi executada.

| Verificação | Resultado |
| --- | --- |
| `LocalizationTests` (novos) | **16/16 passaram** |
| Essas + `PioneerNarrationTests`, `PioneerDataTests`, `PioneerOutboundTests`, `PioneerListeningTests`, `PioneerImagingTests` e `AudioDirectorTests` | **102/102 passaram** |
| `python Tools/export_pioneer_texts.py --check` | Conforme (112 chaves) |
| Todas as tabelas do projeto (Marte, HUD, dossiê, Pioneer: 14+ arquivos) carregadas juntas | Sem conflito de texto, sem chave inválida, **todas as chaves com PT e EN** (a única repetição, `MAR.OBJ.rock`, tem o mesmo texto) |
| Cobertura de glifos (fontTools) das 112 chaves PT e EN da Pioneer | Nenhum caractere ausente em Outfit Regular, Outfit SemiBold e Plex Mono SemiBold |
| Auditoria de linhas (`subtitle-audit.txt`, fonte resolvida de 28 px, caixa de 880 px) | **63 legendas por idioma, nenhuma com mais de 2 linhas** (PT: 42 de 1 linha e 21 de 2; EN: 48 e 15). A mais longa é `PIO.06.03` |
| Capturas (`SubtitleCapture`) | 14 PNGs em `Docs/Art/Subtitles/` (7 estados × 1280×720 e 1024×768): legenda mais longa em PT e EN, carimbo de data, carimbo + corpo e o estado sem texto (nada na tela), conferidos visualmente |
| Reading check das sequências reais da Pioneer | Todas as legendas de todas as sequências existem em PT e EN, nenhuma passa de 22 caracteres por segundo |

Cobertura dos testes: normalização do idioma e evento só em troca real; `Get` nunca devolve a chave e registra o pedido; duplicata igual × conflito; JSON e chaves inválidos sem exceção; exportação da Pioneer idêntica às tabelas da PIO-001; **nenhuma caixa visível antes, entre ou depois dos cues**; tradução vazia esconde a placa inteira e ela volta com o texto; troca de idioma redesenha o mesmo cue uma vez; carimbo de data distinto do corpo; prioridade e retorno do cue anterior; placas de base e topo independentes e a camada na coluna do host; religar limpa a tela; prefab idempotente; **o `SequenceDirector` real tocando `PIO_Outbound`** (cada legenda só na sua janela, lacunas sem caixa, troca de idioma no meio de um cue sem perder o tempo, fim sem nada na tela).

Defeitos achados e corrigidos na conferência: o `<Style>` do UXML só vale para os elementos do próprio UXML (a camada ficava sem estilo; agora entra em `subtitle-host`); um combinador `+` (não suportado no USS) invalidava a folha inteira; a legenda de base subia ao topo com a placa de topo escondida; e a captura só funciona com a janela da Unity de fato em primeiro plano (senão sai preta e a auditoria se declara inválida, em vez de mentir).

**Não executado:** Play Mode e navegador; a legenda dentro do prefab `MissionHUD` real (foi testada em um contêiner simples); a contagem de linhas em 4:3 (só 16:9 foi medida, 4:3 foi capturado); teste com pessoas do tempo de leitura; os textos da Surveyor (não estão em JSON no formato do projeto). Revisão da R2 pendente.
