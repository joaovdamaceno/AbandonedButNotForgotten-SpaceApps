# PIO-012 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-012-despedida-conclusao`, base `develop` d7b8371 (inclui PIO-009, PIO-010, PIO-011, PIO-013 e MAR-009/012), Unity **6000.6.7f1** / URP 17.6, Windows, batchmode (Unity fechada).

| Verificação | Resultado |
| --- | --- |
| Testes da tarefa (`PioneerFarewellTests`) | **14/14** passaram |
| Suíte EditMode completa | **747 passaram, 18 falharam de 765** (`editmode.xml`). As 18 falhas são **todas** `MarsEagleTests` (MAR-012, de outra pessoa): o `SetUp` falha porque `Assets/_Game/Chapters/Mars/Prefabs/EagleEnvironment.prefab` **nunca foi commitado** (0 ocorrências em `origin/develop`; o builder `mars.eagle` o gera e a MAR-012 o deixou de fora). Não é desta tarefa e não alterei arquivos da MAR-012 |
| Builder `pioneer10.stage.farewell` reexecutado | Arquivos idênticos |
| Importador de dados (PIO-013) | 112 textos (111 do develop + `PIO.UI.aldebaran`); `PioneerData.asset` idêntico ao regenerado |
| Capturas (`PioneerFarewellCapture`) | 13 PNGs em `Docs/Art/Pioneer10_Farewell/<estado>_<L>x<A>.png`: 5 estados em 1280×720; 4 também em 960×600 e 1024×768 (4:3, `sa-layout--narrow`). Fundo claro #C99A6E (pior caso); os textos são os PT dos dados do capítulo |
| Contraste medido no render (`contrast-audit.txt`) | 0 falhas. Pior texto: guia de Aldebaran 10,23:1 e linha de situação 11,86:1 (mín. 4,5:1); pior não-texto: borda dos botões 13,43:1 (mín. 3:1) |
| Layout (`layout-audit.txt`) | 0 falhas: guia, memorial e ações dentro da tela nas três resoluções |
| Build WebGL da cena `Pioneer10.unity` (cache limpo) | Sucesso: 17 arquivos, 14,71 MiB, shaders 3/3, hashes conferidos |

**Cobertura dos testes:** seis planos e 38 s; rigs diferentes em planos consecutivos (só o preto final continua o anterior) e pelo menos quatro rigs distintos; marcos e legendas PIO.08.01–04 nas janelas; ordem e estado visual ao longo da cena (guia só no plano aberto, preto subindo com tempo ativo, texto só com o preto cheio, ações só nos 2 últimos s); `Completed` uma vez em 36 s com o memorial aberto e permanecendo depois do fim da sequência; 10 minutos parado no memorial sem nenhum evento nem capítulo automático; setas, E (toque), E segurado por 0,5 s e Enter, só a primeira escolha vale e o prompt fala do botão em foco; E mantido de antes não escolhe; pausa congela o fade e movimento reduzido mostra o preto de uma vez; cancelar na troca das antenas e reiniciar sem vazar leases nem legendas; cancelar com o memorial aberto não levanta escolha; pular abre o memorial e conclui uma vez; texto PT/EN com o memorial em duas linhas; a atividade não move, oculta nem destrói a sonda ou a placa.

**Não executado:** Play Mode e navegador; o match cut e o plano aberto não foram vistos no 3D (a estrutura é verificada, não o enquadramento, e depende da silhueta da antena da PIO-011); o gráfico opcional de atlas (4 s); teste humano; o contraste foi medido no render offscreen do Editor.
