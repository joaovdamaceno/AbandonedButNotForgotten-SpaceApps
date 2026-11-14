# SUR-007 — primeira imagem por televisão

Implementação em **14/11/2026**, iniciada em develop **1c503be**. Core sequencial até **CORE-006** (`934a2fd`, #93), com atualização **CORE-001/MCP** (`30d78a3`, #104). Issue #65 assumida depois de confirmar os bloqueadores nativos #60 e #4 fechados/integrados. Não houve alteração de contratos Core, pacotes, BuildSettings ou cenas de outros responsáveis.

## Entrega

Cena pronta para Play: `Assets/_Game/Chapters/Surveyor/Sequences/Television/Preview/SurveyorFirstImagePreview.unity`. Ela espera clique, executa a descida SUR-006 e oferece **Segure E · receber imagem**. Preparo de 2 s, trânsito de ida de 1,3 s, revelação de 4 s e contemplação de 6 s. Sem comando, a recepção aguarda. Só ChapterDirector avança **FirstImage → Observe**.

O shader opera somente na imagem do cenário real SUR-002: 200 linhas visuais, monocromia e varredura de cima para baixo. HUD e legendas ficam fora dele. A câmera permanece no ponto físico e não existe imagem/RT de superfície antes do pouso ou durante o trânsito da ordem. Uma fonte 640×480 e um buffer processado 640×480 são reutilizados; o handoff e etapas posteriores preservam a mesma imagem/cena. Reinício/saída liberam os buffers.

Interface PT/EN, alternativa por alternância, botão mantido equivalente a E, pausa centralizada, placa de legenda vazia oculta e marcação da elipse de aproximadamente uma hora após o pouso. Sem VideoPlayer, voz ou novos recursos de terceiros.

## Verificação no Editor

[Suíte NUnit](editmode.xml): **255/255 passaram**, zero falhas/skips, **13 casos SUR-007**, em Unity **6000.6.7f1 / URP 17.6.0**, Windows/D3D11, RTX 3050 Laptop 6 GB. Cobertura:

- Bloqueio de imagem/candidato antes do pouso e ausência de recepção automática sem ordem.
- E mantido entre etapas, soltura e novo pressionamento, preparo parcial, alternância e envio único.
- Preparo, trânsito e varredura pausados; cancelamento/retorno e invalidação de callbacks.
- Sequência compartilhada com quatro segundos de varredura e seis de leitura de SUR-S10.
- Teste de pixels do shader: região superior revelada primeiro, região inferior preservada, imagem final monocromática.
- Textos PT/EN, placa vazia oculta e material independente de UI/texturas externas.
- Prefab real em Play Mode: clique explícito, ordem pela fila, câmera física, conservação da imagem em Observe e na etapa seguinte, três reinícios e retorno.
- Reconstrução preservando GUID, cena, referências e um AudioListener.

[Métricas de Play Mode](runtime-metrics.json) registram o UTC real, atraso observado, preservação de pausa/ponto de observação e **quatro alocações/quatro liberações** após três reinícios e saída. Trechos editoriais são adiantados pelo relógio comum para o teste; isso não representa duração humana.

A execução que coincidiu com a interrupção da sessão teve dois timeouts. Os casos foram reexecutados e passaram; a suíte acima foi repetida integralmente com sucesso após a retomada. Não foi aumentado timeout nem ignorada uma falha para obter aprovação.

Sete [capturas](Captures/) da UI real no Editor: antes da ordem, início/meio/fim da varredura, pausa, inglês e handoff; 1280×720, 960×600 e 1024×768. [Auditoria de layout](layout-audit.txt): limites dos elementos, moldura 4:3 e pausa com erro de centro menor que um pixel de painel. Capturas inspecionadas visualmente.

## Web executada

[Build release WebGL](web-build.json) gerada por CORE-006 com a cena explícita, sem tocar BuildSettings. Inclui tamanho e SHA-256 de cada arquivo e confirma **SurveyorScan.shader empacotado**, sem shader ausente ou erro de build. Dados e executável comprimidos somam aproximadamente **17 MB** nesta cena isolada; não é o tamanho dos três capítulos.

Teste local em **ChatGPT in-app browser / ANGLE D3D11, RTX 3050 Laptop 6 GB**, viewport 1280×800, nível **Very Low** e URP base CORE-001. O perfil dedicado QA-001 ainda não existe nesta base. [web-metrics.json](web-metrics.json) foi extraído do DOM publicado pelo diagnóstico real do player, com resultado **passed**:

- Quatro ciclos, três reinícios, quatro alocações/quatro liberações.
- Bloqueio inicial, varredura na direção correta, monocromia e pausa preservada.
- Mesma posição do observador e mesmos buffers no handoff.
- Retorno por clique no botão normal, liberando fonte e imagem.
- [Console capturado](web-errors.json): nenhuma entrada de erro.

![Imagem no navegador](Captures/web-received-1280x800.jpg)

O diagnóstico só roda com `?sur007qa=1`, após o clique inicial. Ele adianta a descida, confirma a ordem com o adaptador real e mede pixels da GPU. Não certifica habilidade de teclado no navegador, duração de novatos ou FPS. A entrada E foi testada pelo Input System nos casos do Editor. A hospedagem local usa [serve_preview.py](serve_preview.py), somente em 127.0.0.1, com headers gzip/Brotli e adaptação responsiva do HTML, sem modificar os arquivos binários da build. Não houve publicação no itch.io.

## Ciência, origem e limites

Matriz **S3-03/S3-12**. [JPL TR 32-1177 Part I](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/Surveyor-III-MIssionRpt1967028267.pdf), p. 7 impressa / p. 17 do PDF: primeira imagem em 200 linhas cerca de uma hora após o pouso, depois reconfiguração para 600. P. 37 / PDF 47: modos e antenas. [Smithsonian](https://airandspace.si.edu/collection-objects/camera-television-surveyor/nasm_A19761264000): câmera equivalente de teste e espelho móvel; abertura direta retornou 403 nesta sessão, descrição institucional recuperada pelo índice de busca e conferida com o relatório primário local.

A espera histórica e a transmissão estão condensadas. A fila é ida da ordem, não tempo total de retorno de uma imagem real. A imagem é reconstrução do cenário, identificada discretamente em tela e tooltip. Não é fotografia de arquivo nem vídeo histórico. Não foram importadas mídias do Smithsonian/NASA nesta task.

R1 ainda compõe o capítulo completo com Core compartilhados; não duplicar os serviços do preview dentro de Boot. Alvos/espelho comandado e ciência do solo continuam nas tarefas seguintes. Não declarar QA-001, build integrada dos três capítulos, GPU integrada, Firefox/Edge separados, iframe, rede fria, áudio ou compreensão humana aprovados por esta entrega. Revisão técnica pelo assistente, sem simular aprovação humana independente.

## Reproduzir

- Builder: menu **Abandoned Space Apps → Surveyor → SUR-007 Reconstruir primeira imagem**, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorTelevisionBuilder.BuildFromCommandLine`.
- Testes: `-batchmode -force-d3d11 -projectPath <projeto> -runTests -testPlatform EditMode -testResults <arquivo.xml> -logFile <log>`; sem `-quit`. Filtro: `Abandoned.SpaceApps.Chapters.Surveyor.TelevisionTests`.
- Build: CLI CORE-006 com `-buildTarget WebGL -spaceAppsPlatform WebGL -buildScenes Assets/_Game/Chapters/Surveyor/Sequences/Television/Preview/SurveyorFirstImagePreview.unity -buildOutput Builds/SUR007Web`.
- Servir: `python QA/SUR-007/serve_preview.py --root Builds/SUR007Web`, abrir `http://127.0.0.1:8747/?sur007qa=1`, iniciar e, no final, clicar **Voltar ao início**. Recarregar a página para outra execução diagnóstica.
- Integração, ciclo de vida e fontes: README de `Sequences/Television/`, `Docs/Production/REUSE.md` e `ASSET_CREDITS.md`.
