# SUR-006 — três contatos e corte dos verniers

Implementação em **14/11/2026**, a partir de `develop` **70620fe**. Core sequencial integrado até **CORE-006**, merge **934a2fd**, PR #93; atualização posterior da **CORE-001/MCP**, merge **30d78a3**, PR #104, também incorporada. Dependências nativas **#60 SUR-002, #62 SUR-004 e #4 CORE-004** confirmadas fechadas/integradas antes da atribuição. Validação no Unity **6000.6.7f1 / URP 17.6.0**, Windows D3D11, RTX 3050 Laptop 6 GB.

Durante a implementação, a PIO-004 e a atualização CORE-001/MCP entraram em develop. A branch incorporou **0691f2e** e **30d78a3** sem descartar alterações da equipe; a suíte abaixo foi repetida sobre essa base.

## Entrega e cena

Cena funcional: `Assets/_Game/Chapters/Surveyor/Sequences/Descent/Preview/SurveyorDescentPreview.unity`. Play → **Iniciar descida**. Um esquema técnico acompanha a aproximação; o corte automático não confirma; E mantido por três segundos envia o comando com atraso ativo de 1,3 s. A equipe assume quando não há confirmação, sem Game Over. Primeiro e segundo reerguimentos aproximados de 10 m e 3 m acontecem para todos. O terceiro contato assenta a sonda; data/região e o próximo objetivo encerram o ato.

Somente ChapterDirector avança **Descent → FirstImage**. A câmera física e sua RenderTexture não são usadas durante a descida. O gate autoriza sua ativação por SUR-007 após o handoff, bloqueia antes e libera a textura ao reiniciar. O preview reutiliza os prefabs SUR-002 sem editar os originais. Boot/Dossier/BuildSettings não foram alterados.

Orçamento editorial de **40 s**, com tolerância de confirmação e leitura que pode estender o caso limite até aproximadamente **42,3 s**. Asset de sequência único sincroniza movimentos, marcadores e legendas. A página de integração em `Sequences/Descent/README.md` descreve os eventos e APIs.

## Testes executados

[Suíte NUnit](editmode.xml): **242/242 passaram**, sem falhas ou skips; **25 casos novos SUR-006**. Inclui execução real do prefab em Play Mode e testes de comportamento para:

- Comando cedo, tarde e ausente, sempre com os três contatos e sem terminar o capítulo inteiro.
- Ordem antecipada recebida sem apagar os reerguimentos ou desligar os motores antes do momento encenado.
- E mantido antes da oferta, preparo parcial, soltar/retomar e modo alternância.
- Pausa durante preparo, trânsito e impulso; retomada sem avanço oculto.
- Transferência explícita à equipe, limite da tolerância e envio único.
- SUR-S08 exclusiva da contingência; prioridade de SUR-S07 e tempo mínimo de leitura de recibo e SUR-S09.
- Delta de retomada grande sem pular a descida.
- Reinício/retorno invalidando callbacks antigos e liberando candidato, fila e câmera.
- Gate recusando vídeo antes da etapa FirstImage; TV mantida nas etapas seguintes e liberada no reinício.
- Movimento reduzido sem deslocamento e com pulso estrutural.
- PT/EN, instruções coerentes com a disponibilidade e placa vazia oculta.
- Cruise → Descent nas duas ordens de assinatura; próximo consumidor assumindo fila/câmera sem cancelamento pela etapa anterior.
- Reconstrução determinística de cena, preservação de GUIDs e referências, um AudioListener e TV inicialmente desligada.

[Métricas de Play Mode](runtime-metrics.json) registram UTC real da execução, atraso observado, três contatos, um envio recebido, preservação do impulso pausado, ausência de RenderTexture durante descida, alocação 640×480 depois do handoff e liberação no reinício. O teste adianta trechos editoriais com o relógio comum; a entrega da contingência, pausa e encerramento rodam com Updates reais. O áudio é silenciado pelo armazenamento de preferências em memória do teste.

## Capturas e layout

**Onze capturas da UI real**: aproximação, anomalia, ordem armada, primeiro/segundo reerguimentos, recibo da equipe, assentamento e handoff em 1280×720; movimento reduzido e pausa em 960×600; instruções em inglês em 1024×768. A [auditoria](layout-audit.txt) verifica limites dos elementos e centro da pausa (erro observado menor que um pixel de painel). As capturas foram inspecionadas visualmente. Não são capturas de navegador ou de teste humano.

![Comando de corte](Captures/anomaly-pt-1280x720.png)

![Pausa em 960×600](Captures/pause-pt-960x600.png)

[surface-after-settlement.png](Captures/surface-after-settlement.png) é uma renderização da câmera real **ativada pelo teste depois do handoff**, para provar a ligação do gate/viewport. Não aparece durante o ato nem representa uma fotografia histórica ou uma atividade SUR-007 já implementada.

## Ciência, arte e limites

Referências: matriz S3-01/03/04; [NASA Science — Surveyor 3](https://science.nasa.gov/mission/surveyor-3/); [TR 32-1177 Part I](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/Surveyor-III-MIssionRpt1967028267.pdf), **p. 7 impressa / p. 17 do PDF**. O relatório contemporâneo foi lido para os verniers ativos nos dois primeiros contatos e corte pouco antes do terceiro. Não publicar o desenho como gráfico de telemetria real ou reprodução dos tempos do relatório.

Uma autorização de corte antecipada é uma adaptação editorial acessível: a interface distingue recebimento e motores desligados. A janela foi ampliada, intervalos condensados e o final converge sem culpa da pessoa. Os números aproximados de reerguimento e o minuto/data/região são os fatos usados; não há combustível, velocidade, sequência interna inédita ou logs fabricados.

Esquema vetorial, pulso estrutural e quatro sons sintetizados próprios. Fontes UI-001 e modelo/solo SUR-002 mantêm os créditos existentes. Sem novos downloads, dependências ou código da edição Unity anterior. Áudio não foi certificado por escuta humana nesta execução.

Revisão técnica do assistente, conforme autorização do proprietário, sem simular aprovação independente. Integração do capítulo completo e serviços globais continuam com R1; primeira imagem com SUR-007. Sem build Web/Windows, iframe, medição de FPS ou prova de compreensão por novatos nesta tarefa. A verificação humana dos critérios narrativos continua necessária.

## Reproduzir

- Builder: **Abandoned Space Apps → Surveyor → SUR-006 Reconstruir pouso** ou `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorDescentBuilder.BuildFromCommandLine`.
- Suíte: `-batchmode -force-d3d11 -projectPath <projeto> -runTests -testPlatform EditMode -testResults <arquivo.xml> -logFile <log>` (sem `-quit`). Filtro opcional: `Abandoned.SpaceApps.Chapters.Surveyor.DescentTests`.
- Capturas: `-batchmode -force-d3d11 -projectPath <projeto> -executeMethod Abandoned.SpaceApps.EditorTools.SurveyorDescentCapture.CaptureFromCommandLine -logFile <log>`.
- Fontes e autoria: `ASSET_CREDITS.md` e `Docs/Production/REUSE.md`.
