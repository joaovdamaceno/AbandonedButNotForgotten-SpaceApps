# SUR-004 — fila didática de comandos

Base **develop `3c3e2f6`**; última CORE integrada: **CORE-006**, merge `934a2fd` / PR #93. Dependências nativas #2, #3 e #5 verificadas fechadas e com `integrated` antes da atribuição. Implementação em 14/11/2026, Unity **6000.6.7f1**, URP **17.6.0**, Windows D3D11, RTX 3050 Laptop 6 GB.

## Comportamento entregue

- Uma ordem física em trânsito, sem duplicação por E mantido ou repetido.
- Feedback síncrono de envio; efeito físico após 1,3 s ativos, sem consumir o delta do próprio frame de envio.
- Preparação por InteractionService; soltar/pausar conserva o trabalho. Alternância e mouse passam pela mesma condição de disponibilidade.
- Alvo capturado no aceite: mover o alvo durante o trânsito não altera a ordem enviada nem bloqueia a interface.
- Pausa conserva o restante. Troca de etapa, saída, reinício, reconfiguração e desativação invalidam operações anteriores.
- Reentrada no callback não executa novamente. Transição dentro dele não apresenta uma confirmação antiga na etapa nova.
- Widget PT/EN, contorno desejado/marca recebida, progresso e estado do sinal; nenhum leitor novo de teclado ou avanço direto de missão.

Cena real de verificação: `Assets/_Game/Chapters/Surveyor/Prefabs/CommandTransit/Preview/CommandDelayPreview.unity`. Há prefab e builder próprios; veja o README nessa pasta para o contrato de integração. É um instrumento de teste local, não uma campanha ou TV lunar concluída.

## Testes e medições

**181/181 testes passaram, zero falhas**, incluindo **24 casos novos**. Resultados da suíte final: [editmode.xml](editmode.xml). Os testes novos cobrem fila, Input System real com teclado virtual, tecla mantida entre estados, preparo parcial, alternância, pausa em 0,6 s, cancelamento, retorno, reinício, reentrada, exceção consumida uma vez, 30/60 Hz, localização e HUD síncrono.

O teste do prefab **entra em Play Mode**: usa Updates reais para preparar/enviar, move o alvo durante o trânsito, perde foco, retoma, confirma o alvo originalmente capturado, desabilita/reabilita a fila com uma ordem pendente e reinicia outra ordem pendente. Não muda preferências reais; armazenamento em memória.

[runtime-metrics.json](runtime-metrics.json) registra valores produzidos pelo teste:

| Medida | Resultado da execução |
| --- | ---: |
| Frames entre aceite e binding de Enviada | 0 — mesmo frame |
| Tempo ativo até a entrega física | 1,300767 s |
| Tempo ativo acumulado durante a pausa | 0 s |

O primeiro valor mede o **binding lógico**, não apresentação de pixels/latência de monitor. Entrega ocorre no primeiro frame que alcança a duração; o teste confirma 1,3 s de referência com tolerância de um frame. Estas medições são do Editor, sem extrapolação para FPS, latência ou carregamento Web.

O teste anterior da SUR-003 agora espera progresso de Updates ativos, em vez de presumir quantos frames cabem em 600 ms de parede. A saída de Play Mode também ocorre após falha de teste. Gameplay, câmera, duração e assets da SUR-003 não foram alterados.

## Inspeção visual

Seis capturas do widget real em **1280×720, 960×600 e 1024×768**, com PT/EN, prontidão, sinal em trânsito, entrega e pausa. [layout-audit.txt](layout-audit.txt) verifica botões, ajuda e widget dentro da tela; erro de centro da pausa **0 px**. Inspeção visual: estados distintos, alvo desejado separado do recebido, texto legível e painel de pausa centralizado.

![Ordem em trânsito](Captures/sent-pt-1280x720.png)

![Pausa](Captures/pause-pt-960x600.png)

## Reprodução

1. Abrir a cena e apertar Play; Iniciar verificação; setas/botões para alvo, E ou botão mantido para preparar, P/Esc para pausa.
2. Reconstruir pelo menu **Abandoned Space Apps → Surveyor → SUR-004 Reconstruir verificação de comandos**; ou `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorCommandBuilder.BuildFromCommandLine` em batch.
3. Testes: `-batchmode -force-d3d11 -projectPath <projeto> -runTests -testPlatform EditMode -testResults <arquivo.xml> -logFile <log>`, sem `-quit` no Test Runner.
4. Capturas com gráficos: `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorCommandCapture.CaptureFromCommandLine`.

## Limites

O tempo é aproximação didática **de ida Terra–Lua**; não latência exata, ida e volta ou registro de 1967. O diagrama não é vídeo da sonda. A chegada inicia o efeito físico; animações longas e transições continuam com o instrumento/proprietário do capítulo.

R1 deve consumir os componentes com os Core compartilhados, sem duplicar os serviços do prefab de preview no Boot. UI-003 pode fornecer TextResolver quando integrada. SUR-005/006/007/009 e demais consumidores têm suas próprias tarefas; esta entrega não os declara implementados. A montagem de Launch da SUR-003 permanece no relógio de sua SequenceDefinition.

Nenhum novo asset externo, pacote ou serviço online de runtime. Nenhum build Web/Windows ou teste de navegador nesta tarefa; smoke Web e validação humana permanecem nas tarefas de integração. Revisão técnica do assistente conforme autorização do proprietário, sem atribuir aprovação humana independente.
