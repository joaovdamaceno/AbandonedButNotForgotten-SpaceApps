# SUR-007 — abrir os olhos

Abra `Preview/SurveyorFirstImagePreview.unity`, Play → **Ver o pouso e receber a imagem**. A entrada espera um clique explícito. Após a descida real da SUR-006, segure E ou o botão por **2 s**. Soltar conserva o preparo. A ordem passa pela fila SUR-004 (**1,3 s ativos de ida**); a imagem se forma em **4 s**, seguida de **6 s de contemplação** com SUR-S10. P/Esc/botão pausa; reiniciar e voltar à entrada liberam os buffers. PT/EN e alternância de hold usam as preferências comuns.

O ato implementa **FirstImage → Observe**, sem concluir o capítulo inteiro ou implementar os três alvos da SUR-008. O preview compõe Descent → FirstImage com Core reais e os prefabs SUR-002, sem alterar as cenas/assets de origem, Boot, Dossier ou BuildSettings. A entrada local reutiliza o mundo para testar reinícios; o ChapterDirector emite o descarregamento real de recursos de sessão.

## Uma fonte de duração

`../FirstImage.asset` contém dois planos: quatro segundos de varredura e seis de contemplação. O marcador `surveyor.tv.imageReady` e SUR-S10 vêm desse asset. A proporção revelada é calculada pelo tempo do SequenceDirector dividido pelo instante do marcador, sem timer de animação paralelo. O preparo e a fila usam SessionClock e seus contratos próprios. Referência de interação sem hesitação: **13,3 s**, além da descida; não há prazo de conclusão automática sem comando.

Pausar congela preparo, fila, varredura e janela de leitura. O E já mantido no handoff não inicia a recepção: o router precisa produzir uma nova borda de pressionamento. Há rearme local para o caso em que a soltura ocorreu enquanto o contexto antigo suprimia a entrada; ele consome `InputRouter.InteractDown` e `InteractionService.NotifyInteractReleased`, sem outro leitor de teclado ou alteração do contrato Core.

## Imagem, memória e posição

- `SurveyorDescentCameraGate` bloqueia a câmera e o viewport até a passagem para FirstImage. Mesmo depois do pouso, esta task só os ativa **quando a ordem chega**. Antes disso, não existe RenderTexture da superfície ou imagem mostrada.
- `SurveyorSiteViewport` fornece uma fonte 640×480 com depth de 24 bits. `SurveyorTelevisionOutput` cria **um segundo buffer 640×480 sem depth**, reutilizado a cada quadro. Não há alocação de RT por frame, screenshot intermediário ou resolução 4K.
- `SurveyorScan.shader` faz uma passagem simples: 200 linhas visuais, monocromia, revelação de cima para baixo e uma faixa discreta na fronteira da varredura. Não usa `_Time`, ruído intermitente, pós-processamento global ou VideoPlayer.
- A imagem processada é atribuída ao `Image` do UI Toolkit. Textos, botões e legendas são desenhados separadamente na resolução normal do painel; não entram no shader. A moldura mantém 4:3.
- O observador fica na posição física do ponto da câmera, enquadrando a âncora LandingMarks. Não há câmera orbital ou deslocamento do corpo para criar essa vista. Os pivôs do espelho da SUR-002 continuam disponíveis para SUR-008 orientar a visão.
- Ao passar a Observe, câmera, fonte e imagem processada continuam as mesmas: os próximos alvos podem usar o próprio cenário e a saída com progresso 1. Reiniciar/descarregar libera as duas RTs; desativar/desmontar o output também destrói sua instância de material.

Mantenha output e controller habilitados nas atividades de superfície; o controller fica parado depois do handoff e pode ter sua UI ocultada. Seu `Dispose` libera a imagem e as assinaturas. A fonte é propriedade do gate/viewport, não deve ser destruída pelo próximo consumidor. O cancelamento só cancela a sequência e a geração de fila que ainda possui. Avançar depois de Observe também preserva os mesmos buffers, até o descarregamento.

## Integração com R1 e UI-004

Configure `SurveyorTelevision` antes da entrada em FirstImage com ChapterDirector, SessionClock, InputRouter, InteractionService, fila/adaptador SUR-004, SequenceDirector, `FirstImage.asset`, SurveyorDescent real, gate e output. Configure o output com o viewport real e `SurveyorScan.mat`. A autorização exige o assentamento confirmado pela SUR-006; não simular um booleano de pouso para iniciar a câmera mais cedo.

O prefab de preview contém seus Core somente para execução isolada. Na cena global, injete os serviços compartilhados; não duplique os Core do preview em Boot. Só ChapterDirector executa `CompleteStep("FirstImage", "Observe")`. `CueEntered`/`CueExited` publicam SubtitleCue; `DrawLocalSubtitles=false` desativa a legenda local quando UI-004 assumir. `TextResolver` recebe localização global; fallback em `UI/TelevisionTexts.json`.

Com UI-003 disponível, o HUD global pode consumir o mesmo candidato de InteractionService. Durante este painel de instrumento, escolha uma apresentação do comando: o botão local já mostra o preparo acumulado do adaptador SUR-004, evitando duplicar o prompt de E. Esta entrega não instala o HUD global nem altera seu contrato.

Builder: **Abandoned Space Apps → Surveyor → SUR-007 Reconstruir primeira imagem**, ou `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorTelevisionBuilder.BuildFromCommandLine`. Módulo CORE-006 `surveyor.television`, GUIDs/cena estáveis. Cenas globais e pacotes não são alterados.

## História e autoria

Fontes da matriz **S3-03 e S3-12**. O [JPL TR 32-1177 Part I](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/Surveyor-III-MIssionRpt1967028267.pdf), **p. 7 impressa / p. 17 do PDF**, informa a primeira imagem em modo de 200 linhas cerca de uma hora após o pouso e posterior reconfiguração para 600 linhas. A p. 37 impressa / p. 47 do PDF descreve os dois modos e antenas. A [descrição técnica do Smithsonian](https://airandspace.si.edu/collection-objects/camera-television-surveyor/nasm_A19761264000) confirma modos de 200/600 linhas e espelho móvel; esse objeto de coleção é um artigo de teste, não a câmera retornada.

A montagem condensa a espera de aproximadamente uma hora e acelera transmissão/varredura. O atraso de ida do comando não é o tempo total de retorno de uma imagem histórica. O cenário é uma reconstrução renderizada, não arquivo NASA ou vídeo ao vivo; uma identificação discreta e tooltip contextual deixam isso explícito. O modo de 600 linhas é documentado, sem atribuir todas as imagens da missão ao modo escolhido aqui.

Shader e layout próprios; fontes/tokens UI-001 e modelo/cenário SUR-002 mantêm os créditos existentes. Sem novos downloads ou dependências, fotografias importadas, música ou voz. Reuso registrado em `Docs/Production/REUSE.md`.

## Diagnóstico isolado

O componente `SurveyorTelevisionWebSmoke`, somente no preview, roda apenas com **`?sur007qa=1`**, após o clique inicial. Usa o nível mínimo existente (`Very Low`) e o URP base CORE-001, sem alterar o asset de qualidade. O perfil dedicado QA-001 ainda não existe nesta base. Adianta a descida com os Core reais e testa comando em Updates reais, pixels do shader, pausa, conservação do ponto da câmera e três reinícios. Ao terminar, pede o botão normal **Voltar ao início** para verificar liberação final e publicar JSON em `#sur007qa` no HTML. Esse caminho automatizado não mede compreensão, duração humana ou desempenho de uma partida. Não instalar esse componente no capítulo publicado; não existe nos prefabs de arte/serviços.

Evidências e limites efetivos estão em `QA/SUR-007/` na raiz. O build Web usa a cena explícita por CORE-006, sem adicionar a cena a BuildSettings. QA hospedada no itch.io e hardware integrado continuam tarefas da integração.
