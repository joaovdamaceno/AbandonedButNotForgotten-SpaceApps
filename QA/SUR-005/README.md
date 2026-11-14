# SUR-005 — correção de meio de curso

Implementação em 14/11/2026 sobre **develop `37d4098`**. Última CORE integrada: **CORE-006**, merge `934a2fd` / PR #93. Dependências nativas #61, #62 e #3 confirmadas fechadas e integradas antes da atribuição. Unity **6000.6.7f1**, URP **17.6.0**, Windows D3D11, RTX 3050 Laptop 6 GB.

## Entrega

Cena executável: `Assets/_Game/Chapters/Surveyor/Sequences/Midcourse/Preview/SurveyorMidcoursePreview.unity`. Play → Iniciar correção. A viagem condensada apresenta origem/destino e calendário; setas/WASD ou arrastar movem o contorno, H alinha e segurar E por três segundos prepara a ordem. O sinal passa pela fila SUR-004. A correção gráfica só começa após a chegada e segue para uma transição ao altímetro.

O asset `Sequences/SurveyorCruise.asset` concentra poses, eventos, legendas SUR-S04–S06 e o orçamento de 37 s. Esse valor inclui 14 s de orientação como referência, não como espera obrigatória. Sem entrada, o ato continua aguardando. A tolerância e a ajuda aumentam após inatividade; H não envia a correção.

Somente ChapterDirector avança **Cruise → Descent**. O pouso e o restante do capítulo têm tarefas próprias. A entrada local não altera Boot, Dossier ou BuildSettings.

## Verificação técnica

[Resultado NUnit da suíte](editmode.xml): **200/200 testes passaram**, zero falhas, dos quais **19 novos** nesta tarefa, cobrindo:

- Conclusão só com teclado, ajuda H e ausência de conclusão automática.
- Preparo parcial conservado ao soltar, pausar, sair e retornar à zona de alinhamento.
- Rearme de E mantido, alternativa por alternância e atraso da fila.
- Pausa durante a queima, retomada e avanço único para Descent.
- Janela de leitura e IDs comuns SUR-S04–S06.
- Percurso de referência entre 37 e 38 s ativos, sem impor essa duração a qualquer pessoa.
- Continuidade Launch → Cruise nas duas ordens de inscrição dos componentes.
- Próximo consumidor assumindo câmera/fila antes da limpeza de Cruise, sem cancelamento da nova etapa.
- Coordenadas isotrópicas do mapa e condição de alinhamento correspondente ao círculo desenhado.
- Reconstrução preservando GUIDs, cena e referências.
- Reinício repetido com exatamente três recortes transitórios e liberação de todos ao descarregar em Play Mode.
- Textos PT/EN, placa vazia oculta e instruções de hold ocultas durante sequências automáticas.

O teste do prefab entra em **Play Mode**. Apenas a introdução de viagem é adiantada com o relógio comum; hold, fila, queima, pausa, retomada e término passam por Updates reais. [runtime-metrics.json](runtime-metrics.json) registra UTC da execução, atraso ativo observado, uma correção entregue, preservação da queima pausada e estado final Descent. Prefere armazenamento em memória, sem alterar as preferências reais do usuário.

`Stop` permite reiniciar sem alocar outros recortes. `Dispose` libera os dados e assinaturas na desativação; testes/ferramentas do Editor o chamam explicitamente. Isso também foi verificado pelo contador de recursos, sem depender de coleta de lixo presumida.

## Arte e layout

Nove capturas do widget real em PT/EN: viagem, orientação, sinal, queima, transição e conclusão em 1280×720; ajuda e pausa em 960×600; orientação em inglês em 1024×768. [Auditoria de layout](layout-audit.txt) verifica limites, centro da pausa, proporção do mapa e estados efetivos.

O disco de destino ocupa 35% da largura útil; a zona de alinhamento está desenhada na mesma escala do teste de disponibilidade. A transição amplia Lua e marcadores juntos e introduz uma escala de altitude sem leituras numéricas inventadas. Instruções de hold saem durante a queima/transição. As imagens são do Editor, não do navegador ou de uma sessão humana.

![Orientação](Captures/guidance-pt-1280x720.png)

![Ajuda em 960×600](Captures/help-pt-960x600.png)

## Como reproduzir

- Cena e prefab sob `Sequences/Midcourse/`; detalhes de integração no README dessa pasta.
- Builder: **Abandoned Space Apps → Surveyor → SUR-005 Reconstruir correção de meio de curso**, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorMidcourseBuilder.BuildFromCommandLine`.
- Testes: `-batchmode -force-d3d11 -projectPath <projeto> -runTests -testPlatform EditMode -testResults <arquivo.xml> -logFile <log>`; sem `-quit` no Test Runner.
- Capturas com gráficos: `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorMidcourseCapture.CaptureFromCommandLine`.

## Limites

O mapa é qualitativo e sem escala; 17–19 é elipse de viagem, não data/hora atribuída à manobra. A fila usa ida Terra–Lua representativa, não resposta completa ou log de voo. A queima de oito segundos é duração editorial. Sem delta-v publicado ou sugestão de pilotagem histórica por reflexos.

Referências: matriz `Docs/Chapters/03_SURVEYOR_SOURCES.md`, S3-01/03/04 e roteiro local. A [página oficial NASA](https://science.nasa.gov/mission/surveyor-3/) foi consultada novamente para destino e contexto; não foram importadas imagens dela. Desenho vetorial próprio e fontes existentes, sem pacotes/assets externos novos ou código da edição Unity antiga.

R1 ainda precisa montar o capítulo completo com serviços compartilhados; não duplicar os Core do prefab de preview em Boot. A UI local tem ligações para localização e presenter globais. Sem build Web/Windows, medição de FPS ou teste físico de mouse/iframe nesta tarefa. Compreensão por novatos e duração de uma primeira sessão continuam exigindo avaliação humana.

Revisão técnica e integração pelo assistente conforme autorização do proprietário; sem atribuir aprovação humana independente.
