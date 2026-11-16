# SUR-008 — três observações guiadas

Cena de execução isolada: `Assets/_Game/Chapters/Surveyor/Sequences/Observation/Preview/SurveyorObservationPreview.unity`.

O preview compõe os atos existentes Descent → FirstImage → Observe → Sample. A tarefa termina na transferência para Sample; não implementa a pá nem o capítulo completo. Boot, Dossier, cenas e fontes das entregas anteriores não são modificados. O prefab aninha o preview SUR-007 com overrides locais e um ChapterDefinition próprio.

## Jogar

Play → botão de início; acompanhar o pouso e receber a primeira imagem com E. Depois, registrar as marcas da chegada, a encosta e a pata, nessa ordem. Cada objetivo tem nome, indicador na imagem e moldura ampla. A cruz mostra a orientação desejada; o círculo/seta aponta o objetivo.

Mouse sobre a imagem ou WASD/setas prepara a orientação. E envia a posição capturada no momento da confirmação. H ou o botão equivalente enquadra o objetivo pela mesma fila SUR-004: confirmação imediata, execução depois de 1,3 s ativos. Enquanto a ordem está em trânsito, não existe outro comando físico pendente. O corpo e o centro óptico não se deslocam; só os pivôs do espelho e a rotação da câmera mudam.

Quando o espelho termina de se mover e o objetivo entra na moldura, segurar E por 1,5 s prepara a foto. Soltar conserva o preparo; o modo alternar também funciona. A fotografia só é registrada depois da chegada da ordem e de nova validação do enquadramento real. A disponibilidade mostrada pelo painel vem do mesmo adaptador/InteractionService que aceita a ação. Mover o mouse sobre botões não altera a orientação.

O botão de lente oferece 25/100 mm e retorno à visão ampla, também com comando em trânsito e transição suave de FOV. Não é necessário usar a lente de detalhe para concluir os alvos com teclado e H.

Cada foto abre resultado com a imagem efetivamente renderizada e um texto curto. E continua, com rearme entre telas. Botões Foto 1–3 reabrem registros já feitos durante a observação, sem duplicar evidências ou avançar o objetivo. O contador da partida (até 3) fica no instrumento; as 6.326 imagens históricas aparecem em uma informação de arquivo separada no resultado.

P/Esc ou botão pausa. O menu permite retomar, reiniciar ou voltar ao início; está centralizado. PT/EN e preferências de hold/sensibilidade vêm do SettingsStore existente. O botão final do preview retorna à entrada.

## Integração

`SurveyorObservation.Configure` recebe ChapterDirector, SessionClock, InputRouter, InteractionService, a fila/adaptador existentes, SurveyorSiteBindings, a mesma Camera e o mesmo SurveyorTelevisionOutput de SUR-007 e o asset SurveyorObservationTargets. Configure antes da entrada em Observe, depois de configurar os serviços anteriores. Não duplique os Core do preview dentro da cena global.

`ChapterDirector` mantém a transição explícita Observe → Sample. O consumidor seguinte recebe a mesma câmera, origem, imagem processada e fonte 640×480; não há câmera orbital, outra RenderTexture ou realocação por frame. A lente e o enquadramento físicos são preservados na transferência. `Stop`/`Dispose` invalidam callbacks e a geração de comandos que o componente ainda possui, liberam contexto/prompt e param sua resposta a mouse/H/E. Não limpam evidências da sessão ou buffers pertencentes a SUR-007.

Eventos `CueEntered`/`CueExited` entregam SUR-S11–S13 ao apresentador comum. `DrawLocalSubtitles=false` e `TextResolver` permitem usar UI-004/localização compartilhada. A legenda local vazia não desenha placa. `PictureRecorded` permite associar feedback ao áudio global; o preview oferece um sinal original curto e respeita volumes master/UI. A galeria global futura pode ler os EvidenceRecord no EvidenceStore, sem copiar as fotos.

As três fotografias são Photo/PlayerCapture, com referências S3-03, até 640×480 cada. O EvidenceStore é dono de seus sprites/texturas e os destrói ao reiniciar/sair. A reentrada na mesma etapa recupera registros existentes em vez de duplicá-los. `CaptureOverride` existe para os testes unitários; o caminho padrão captura a imagem real da GPU.

O limite comum de três fotografias do jogador fica ocupado por estes registros. Comparações de solo em SUR-009 devem reutilizar imagens científicas próprias limitadas ou os registros existentes, sem tentar acrescentar uma quarta Photo/PlayerCapture ao contrato global.

Builder: **Abandoned Space Apps → Surveyor → SUR-008 Reconstruir observações**, módulo CORE-006 `surveyor.observation`; CLI `Abandoned.SpaceApps.EditorTools.SurveyorObservationBuilder.BuildFromCommandLine`. Asset principal: `Data/SurveyorObservationTargets.asset`. Parâmetros de lente, velocidade, moldura e leitura ficam nos dados. Reconstruções preservam GUIDs.

## Ciência e direção visual

Fontes S3-03/S3-12: [NASA — Surveyor 3](https://science.nasa.gov/mission/surveyor-3/), [Smithsonian — câmera equivalente de teste](https://airandspace.si.edu/collection-objects/camera-television-surveyor/nasm_A19761264000), [NTRS — relatório da missão, parte I](https://ntrs.nasa.gov/citations/19670028267) e [catálogo NASA de dados da TV](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/a12SurveyorIIITV.pdf). O link antigo ao PDF completo retornou 404 nesta execução; a cópia oficial previamente arquivada em QA/Local/SUR-001 e o registro NTRS continuam disponíveis.

As lentes e o espelho são históricos; a seleção dos três alvos, velocidade do movimento, FOV adaptado à tela 4:3, espera e preparo da foto são uma representação de gameplay. O movimento visual do espelho aproxima a reflexão, sem simulação óptica de um CAD. O cenário e as fotos são reconstruções do jogo, identificadas no instrumento. Os textos SUR-S11–S13 preservam o roteiro aprovado.

Reuso: mundo/modelo da equipe SUR-002, fila/adaptador SUR-004, pouso SUR-006, imagem/shader SUR-007, fontes/tokens UI-001 e Core existentes. Nenhum asset externo novo ou pacote foi baixado. O WAV RecordConfirm é síntese original de dois tons; não imita gravação histórica nem som propagado no vácuo. Créditos do modelo e das fontes permanecem em ASSET_CREDITS.md.

## Validação

Resultados executados, capturas e limites ficam em `QA/SUR-008/README.md`. O diagnóstico Web é exclusivamente do preview e só executa com `?sur008qa=1`, depois do clique inicial. Não instalá-lo na cena publicada. Nenhuma aprovação de compreensão de novatos ou duração do capítulo completo é inferida destes testes.
