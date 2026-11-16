# SUR-011 — evidências do eclipse

Issue #69; branch SUR-011; base develop `5014f0a` (CORE-006 e UI-004 integradas). Revisão técnica própria do assistente, sob autorização do proprietário; não houve aprovação humana independente.

## Verificado no Unity

- Unity 6000.6.7f1, D3D11, 13 testes aprovados, sem falha: `editmode.xml`.
- Cena real em Play Mode: três execuções, dois reinícios, mesmo ponto óptico e corpo imóvel; uma alocação e uma liberação do buffer de saída; fonte liberada na desmontagem. `runtime-metrics.json`.
- Reprodução de 26 s, dois cues com seis segundos cada, oito segundos sem legenda, retorno e transição Eclipse → LunarNight uma única vez.
- Entrega de controle e descarte posterior não sobrescrevem o espelho da etapa seguinte.
- Ordem sem efeito físico antes da chegada, hold parcial preservado, modo alternar, pausa durante fila/filme, retorno/cancelamento e E mantido no reinício não vazando para a próxima tentativa.
- GPU: capturas em 960×600, 1024×768 e 1280×720; halo vermelho presente, sem caixa de legenda vazia. Qualidades existentes 0/Very Low e 1/Low. Pausa centralizada e imagem 4:3 dentro do canvas; `layout-audit.txt` e `Captures/`.
- Segunda reconstrução: GUIDs e IDs dos objetos dos dois prefabs, sequence asset e cena preservados; `rebuild-audit.json`.
- Configurações locais GraphicsSettings/QualitySettings/PackageManagerSettings preservadas byte a byte; cinco PNGs de outro capítulo não alterados/incluídos.

## Reproduzir

Abrir `Assets/_Game/Chapters/Surveyor/Sequences/Eclipse/Preview/SurveyorEclipsePreview.unity` e Play. Iniciar e segurar E; P/Esc/botão pausa, botão de idioma troca PT/EN. A prévia termina na área da trincheira, sem implementar a noite (SUR-012).

Builder: `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorEclipseBuilder.BuildFromCommandLine`.
Testes: `-batchmode -force-d3d11 -runTests -testPlatform EditMode -testFilter Abandoned.SpaceApps.Chapters.Surveyor.EclipseTests -testResults QA/SUR-011/editmode.xml`.
Build: `-batchmode -force-d3d11 -buildTarget WebGL -executeMethod Abandoned.SpaceApps.EditorTools.SpaceAppsBuild.BuildFromCommandLine -spaceAppsPlatform WebGL -buildScenes Assets/_Game/Chapters/Surveyor/Sequences/Eclipse/Preview/SurveyorEclipsePreview.unity -buildOutput Builds/SUR011Web`.
Servidor local: `python QA/SUR-011/serve_preview.py --root Builds/SUR011Web --port 8751`. A URL sem parâmetros permite jogar. `?sur011qa=1` ativa um diagnóstico somente desta prévia; após o clique inicial ele testa três execuções, duas qualidades, modo de movimento reduzido e pausa. O diagnóstico não é instalado na cena publicada.

## Limitações de escopo

A passagem dos anos, epílogo Apollo e integração Boot/Dossier são outras tarefas. Não é teste de campanha completa, de duração com novatos ou de GPU integrada; não houve upload itch.io. A geometria e o tempo são uma reconstrução simplificada, documentada em `Docs/Production/SURVEYOR_ECLIPSE.md`. Sem asset externo novo, trilha ou vídeo. O integrador deve consumir os componentes e assets com os Core já existentes, sem instalar o host/diagnóstico de QA na distribuição.

## Resultado Web

Build release `549dce8`: **17.200.212 bytes** no diretório de distribuição, três shaders requeridos presentes, hashes verificados por `Tools/build_release.py`. Relatório completo: `web-build-report.json`.

Três execuções / dois reinícios no Chromium integrado, qualidades0/1, PT/EN e movimento reduzido: **aprovado**, sem erros de runtime. Mesmo observador e corpo, pausa preservada, contorno vermelho efetivamente renderizado (331 pixels vermelhos na imagem-fonte de640×480, zero pixels brancos estourados no instante medido), uma alocação e uma liberação da saída; fonte e saída liberadas ao desmontar. `web-runtime-metrics.json`, `web-console.json` e `Captures/web-eclipse.jpg` / `web-passed.jpg`.

`Profiler.GetTotalAllocatedMemoryLong()` reportou **77.141.543 bytes após desmontagem**. Esse valor não é pico, capacidade do heapWASM nem memória total da aba; não comprova sozinho o orçamento de384MiB. Nenhuma medição deFPS é inferida desse diagnóstico.

Console sem mensagens de nível erro. Dois avisos registrados: shaderFSR do pipeline base indisponível (pós-processamento está desligado nesta câmera; os shaders da TV, terreno e eclipse foram empacotados/renderizados) e API antiga de sincronização de persistentDataPath. A revisão desses serviços globais fica com seus donos.

O primeiro diagnóstico encontrou um falso negativo após reiniciar porque usava o tempo final anterior antes de a nova ordem chegar. A ferramenta foi corrigida para aguardar a faseViewing e os três ciclos foram repetidos com sucesso; o relatório final é da ferramenta corrigida.

## Ambiente e reprodutibilidade

Ensaio Web no navegador integrado Chromium, Windows 11, ANGLE/D3D11, NVIDIA GeForce RTX 3050 6GB Laptop GPU. Este dispositivo não substitui a referência de GPU integrada. O servidor é local, sem limitação artificial de rede; nenhuma medição de primeira entrada a10Mbps ou FPS sustentado é reivindicada.

O build preserva diferenças locais preexistentes: GraphicsSettings com intensidade linear/temperatura de cor habilitadas; Ultra com AA/crossfade deLOD desabilitados. Elas não fazem parte deste PR. As qualidades exercitadas foram Very Low e Low. O relatório e hashes identificam o artefato ensaiado; não é alegação de build de um checkout limpo.

## Compatibilidade com develop

A entrega UI-006 (`122ab44`) foi integrada na branch em`1a05c2e`. Ela adiciona componentes de pausa/foco que esta prévia não instancia e não modifica os Core ou assets referenciados pelo eclipse. A suíte de13 testes foi reexecutada nessa base integrada, com todos aprovados. O build Web ensaiado continua identificado por`549dce8`; os componentes de gameplay, câmera, dados e shader da SUR-011 não mudaram na integração.
