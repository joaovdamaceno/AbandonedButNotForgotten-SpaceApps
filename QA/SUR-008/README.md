# SUR-008 — evidências de entrega

Base de implementação: **develop `211ab0d`**, Unity **6000.6.7f1**, URP **17.6.0**. Issue #66; branch SUR-008. Última entrega inicial CORE: CORE-006 (`934a2fd`), com atualização MCP de CORE-001 (`30d78a3`). Revisão técnica própria do assistente sob autorização do proprietário; nenhuma aprovação humana independente foi simulada.

## Implementação e critérios

- Marcas da chegada → encosta → pata, um objetivo por vez, com moldura ampla, círculo/seta de destino e cruz da orientação desejada.
- Mouse sobre a imagem ou setas/WASD prepara a orientação. E envia; H e botão de assistência usam a mesma fila real de 1,3 s. Corpo e ponto da câmera permanecem fixos.
- Captura após o espelho chegar e o alvo estar dentro da moldura; preparo de 1,5 s, pausa ao soltar e modo alternar. Prompt e execução consultam o mesmo predicado do adaptador/InteractionService.
- Lentes 25/100 mm com trânsito e transição de FOV; retorno a 25 mm pelo mesmo botão, sem tecla obrigatória adicional.
- Até três fotos reais 640×480 no EvidenceStore, resultados PT/EN, revisão dos registros sem duplicação e rearme de E entre telas. Total da partida e total histórico ficam separados.
- Sair, trocar etapa ou reiniciar invalida comandos e libera o contexto. O consumidor seguinte recebe os mesmos buffers/câmera; EvidenceStore limpa fotos ao reiniciar/sair do capítulo.
- Cena, prefab, dados, referências, iluminação e painel foram montados pelo builder CORE-006. Nenhum arquivo existente de gameplay, cena global ou configuração Unity foi alterado.

## Testes Unity executados

[editmode.xml](editmode.xml): **22 aprovados, zero falhas**. Inclui testes com o Input System real (E/H/setas/mouse), limites da moldura, orientação desejada versus câmera real, pausa, progresso conservado, alternar, lentes, saída com ordem pendente, reentrada e retorno ao arquivo durante callback de EvidenceStore.

O teste de integração entra em Play Mode e executa os Core reais e os atos Descent → FirstImage → Observe → Sample. [runtime-metrics.json](runtime-metrics.json): **três ciclos, dois reinícios, nove capturas reais**, três imagens distintas no primeiro ciclo, origem fixa, fotografias e buffers liberados no retorno. Não é um carregador falso nesse teste integrado.

Layout renderizado em **1280×720, 960×600 e 1024×768**, PT/EN. [layout-audit.txt](layout-audit.txt) confere contenção dos elementos e centro da pausa com erro menor que um pixel. Capturas em [Captures](Captures/), inspecionadas visualmente. O builder foi reconstruído e os GUIDs da cena, prefab e asset principal permaneceram os mesmos. validate_plan: 84 tarefas, sem erros.

## Web executada

[web-build.json](web-build.json): release WebGL gerada pelo CORE-006 com cena explícita, sem tocar BuildSettings. **17.185.593 bytes (16,39 MiB)**, hashes de todos os arquivos conferidos e **2/2 shaders necessários empacotados**. É o tamanho do preview isolado, não dos três capítulos.

[web-metrics.json](web-metrics.json) foi extraído do diagnóstico DOM do player real: **passed**, três ciclos, dois reinícios, nove fotos, três vistas distintas, atraso de comandos e pausa preservados, observador fixo. A saída TV teve três alocações/três liberações; fontes e todas as fotos foram liberadas ao clicar o botão normal de retorno. [web-errors.json](web-errors.json): **zero erros de console capturados**.

Navegador: ChatGPT in-app browser/Chromium, **ANGLE D3D11 / NVIDIA RTX 3050 Laptop 6 GB**, viewport 1280×800, qualidade mínima existente (`Very Low`) e URP base CORE-001. Não é prova de 30 FPS em GPU integrada. [Captura da execução Web](Captures/web-complete-1280x800.jpg).

[web-warnings.json](web-warnings.json) preserva avisos do template/base: sincronização antiga do persistentDataPath e shader interno de FSR indisponível/stripped. Não houve NullReference ou falha do minigame; o shader TV e URP/Lit foram auditados no pacote final. QA-001 ainda precisa fornecer o perfil Web dedicado. Uma tentativa intermediária só de código retornou ShadersStripped na auditoria; o empacotamento final de conteúdo completo foi repetido e passou, sem ignorar o erro nem alterar o builder global.

A primeira execução Web revelou glifo central sem cobertura na fonte. Foi substituído por um ponto existente em Outfit e conferido novamente no navegador, sem nova fonte ou fallback de sistema. A verificação automática de Web usa os mesmos serviços, mas adianta relógios; não certifica duração humana nem habilidade de teclado no navegador.

## Reproduzir

- Abrir `Assets/_Game/Chapters/Surveyor/Sequences/Observation/Preview/SurveyorObservationPreview.unity`, Play e iniciar. Depois do pouso, receber a imagem e registrar os três alvos; P/Esc pausa.
- Builder: **Abandoned Space Apps → Surveyor → SUR-008 Reconstruir observações**, ou `Abandoned.SpaceApps.EditorTools.SurveyorObservationBuilder.BuildFromCommandLine` em batch.
- Testes: `-batchmode -force-d3d11 -projectPath <projeto> -runTests -testPlatform EditMode -testFilter Abandoned.SpaceApps.Chapters.Surveyor.ObservationTests -testResults <xml> -logFile <log>`, sem `-quit`.
- Build: CORE-006, `-buildTarget WebGL -spaceAppsPlatform WebGL -buildScenes Assets/_Game/Chapters/Surveyor/Sequences/Observation/Preview/SurveyorObservationPreview.unity -buildOutput Builds/SUR008Web`.
- Servir: `python QA/SUR-008/serve_preview.py --root Builds/SUR008Web --port 8748`. Abrir `http://127.0.0.1:8748/` para jogar normalmente. Com `?sur008qa=1`, iniciar e, ao final, clicar **Voltar ao início** para concluir a verificação de liberação. Recarregar a página para repetir o diagnóstico.

## Escopo e pendências

A tarefa entrega Observe → Sample; a pá da SUR-009, eclipse/visita e integração dos três capítulos pertencem às tarefas seguintes. Não instalar os Core ou o diagnóstico do preview na cena publicada: injetar os serviços existentes conforme [SURVEYOR_OBSERVATION.md](../../Docs/Production/SURVEYOR_OBSERVATION.md).

Sem publicação no itch.io, teste em iframe, rede fria, Firefox/Edge separados, teste humano de compreensão, garantia de FPS ou certificação de duração de capítulo. A confirmação de áudio original está configurada; não houve avaliação auditiva humana. Configurações locais GraphicsSettings/QualitySettings/PackageManagerSettings foram preservadas byte a byte e não fazem parte do PR.

## Revalidação da integração

A branch recebeu `develop` até `5f316f5`, em merge local `e7bb697`. Os **22 testes foram repetidos e aprovados** nessa base integrada (XML atualizado). As mudanças recentes de LocalizationService/SubtitlePresenter não alteram o preview nem os contratos consumidos por ele. O pacote Web foi gerado com o código funcional SUR-008 do commit `f147fcf` e base `211ab0d`, antes da incorporação dessas entregas independentes; não foi recompilado para incorporar conteúdo de outros capítulos.
