# SUR-003 — evidências de entrega

Executado em **14/11/2026**, a partir de `develop f50ddc6`; última CORE integrada: **CORE-006**, merge `934a2fd` (PR #93). Unity **6000.6.7f1**, URP **17.6.0**, Windows, D3D11, NVIDIA RTX 3050 Laptop 6 GB. Não é uma medição de desempenho Web.

## Implementação verificada

Cena executável: `Assets/_Game/Chapters/Surveyor/Sequences/Preview/SurveyorLaunchPreview.unity`. Iniciar entra no ato; o retículo sobe, o comando fica disponível em 17 s e o anel requer 2,5 s de operação acumulada. A sequência espera em 22 s enquanto a ordem não estiver preparada. Envio e execução são distintos, separados por 1,3 s; o mapa se estende após a execução. A montagem tem 28 s de referência e só ChapterDirector avança Launch → Cruise.

Há uma entrada de preview local, PT/EN, legenda sem placa vazia, mouse ou E, preferência de alternância, pausa/reinício/retorno, pausa por foco e grave original com fade para silêncio. Esta entrega não implementa nem registra o dossiê global e termina na passagem para o próximo ato.

## Testes automatizados

**157/157 passaram**, zero falhas, incluindo **14 testes novos** de SUR-003. Resultado NUnit: [editmode.xml](editmode.xml).

- Barreira de confirmação sem prazo, inclusive com delta grande.
- E mantido antes da disponibilidade exige soltura.
- Soltar conserva progresso e exige somente o restante.
- Pausa congela relógio/hold; tecla mantida não reinicia a operação.
- Execução única, janela de leitura e avanço somente pelo proprietário.
- Confirmação tardia não pula a legenda seguinte.
- Reinício invalida ordens antigas; retorno limpa candidato e câmera.
- Alternância acessível e reconfiguração sem callbacks duplicados.
- IDs de legenda comuns, traduções PT/EN e placa vazia oculta.
- Reconstrução repetida preserva GUID e conteúdo da cena, sem duplicar serviços.
- **Teste em Play Mode** do prefab real: o Update avança o hold, perda de foco pausa, progresso fica preservado, retomada conclui em Cruise e reinício/retorno funcionam. Só a espera inicial foi adiantada; a operação passou por frames reais. O teste usa armazenamento em memória e não altera preferências do usuário.

A suíte contém também as regressões Core, dados científicos, cena Surveyor e Marte já existentes. O teste em Play Mode faz parte da suíte iniciada com `-testPlatform EditMode` (usa EnterPlayMode/ExitPlayMode).

## Evidência visual

**14 PNGs** da interface real com dados do fluxo, renderizados fora da tela via PanelSettings.targetTexture:

- Entrada, comando disponível, pausa e cruzeiro em **1280×720, 960×600 e 1024×768**.
- Comando disponível e sinal em trânsito também em inglês, 1280×720.
- [Auditoria de limites e centro](layout-audit.txt): comandos e botões dentro da tela; erro de centro da pausa **0 px** nas três proporções.

Inspeção visual das capturas de entrada, comando, pausa, trânsito e cruzeiro: tipografia, hierarquia, tradução e contraste legíveis; nenhuma placa vazia no cruzeiro. As capturas não representam um teste humano de compreensão nem imagens do navegador.

![Comando disponível](Captures/ready-pt-1280x720.png)

![Pausa em 960×600](Captures/pause-pt-960x600.png)

## Reprodução

1. Abrir a cena acima e apertar Play. E ou botão prepara a confirmação; P/Esc pausa.
2. Builder no menu **Abandoned Space Apps → Surveyor → SUR-003 Reconstruir abertura**, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorLaunchBuilder.BuildFromCommandLine` em batch.
3. Testes: `-batchmode -force-d3d11 -projectPath <projeto> -runTests -testPlatform EditMode -testResults <resultado.xml> -logFile <log>`; não passar `-quit` ao Test Runner.
4. Capturas: `-batchmode -force-d3d11 -projectPath <projeto> -executeMethod Abandoned.SpaceApps.EditorTools.SurveyorLaunchCapture.CaptureFromCommandLine -logFile <log>`.

## Limites explícitos

- Dossiê global UI-002, localização UI-003, presenter UI-004 e áudio AUD-002 têm suas próprias tarefas. Este ato já fornece os pontos de ligação; o host local não os declara entregues.
- O prefab contém serviços para executar o preview sozinho. R1 deve ligar o controller/view aos serviços compartilhados, conforme `Sequences/README.md`, sem duplicar Core na cena integrada.
- Nenhum build Web/Windows foi distribuído nesta tarefa; iframe, autoplay, pointer lock e desempenho no navegador permanecem na integração/smoke Web.
- Encontrar a ação e compreender o propósito em menos de oito segundos é uma meta de usabilidade **a avaliar com pessoas**, não um resultado demonstrado pela automação.
- O atraso representa didaticamente o trânsito Terra–Lua. Não é a latência da sonda recém-lançada, nem reprodução de logs; tooltip e documentação registram a condensação.
- O som é provisório e original. Não foi aprovado por escuta comparativa com a mix completa dos três capítulos.
- Os warnings preexistentes de `DEVELOPMENT_BUILD` em Marte e `FindFirstObjectByType` nos testes da cena Surveyor não foram alterados fora do escopo.

Revisão técnica, automação e integração feitas pelo assistente conforme autorização do proprietário de 14/11/2026; sem atribuir aprovação humana independente.
