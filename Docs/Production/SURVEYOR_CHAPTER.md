> **Edição atual: SUR-020.** A composição SUR-015 abaixo é histórica. Para executar, abra SurveyorArchive e Play. O fluxo atual é Prologue → Arrival → Activation → Science → Findings → Night → Reunion → Legacy → End. Na ciência: **clique e arraste**, solte ao concluir, depois **segure E**. Foto automática. Controles, tempos, reconstrução e evidências atuais: `Assets/_Game/Chapters/Surveyor/README.md`, `Docs/Chapters/03_SURVEYOR_3.md` e `QA/SUR-020/README.md`.

# Surveyor 3 — capítulo integrado (SUR-015)

Abra `Assets/_Game/Chapters/Surveyor/Scenes/SurveyorArchive.unity` e pressione Play. Escolha Iniciar na pasta Surveyor 3. `SurveyorChapter.unity` também abre isoladamente para inspeção; a entrada publicada é o arquivo. O catálogo de teste deixa os outros capítulos indisponíveis; não substitui Boot, Dossier nem o catálogo global de R1.

## O que foi composto

Uma única instância ativa de ChapterDirector, SessionClock, InputRouter e InteractionService, injetada antes de Start no carregamento aditivo. O capítulo descarrega integralmente ao retornar/reiniciar. A referência do capítulo tem 12 etapas explícitas: Launch → Cruise → Descent → FirstImage → Observe → Sample → Eclipse → LunarNight → TimePassage → ImaginedVisit → HistoricalReceipt → End.

Todos os atos consomem os controladores entregues em SUR-003 a SUR-014. A câmera TV só é ativada depois do assentamento. A primeira imagem usa uma cópia própria de FirstImage com o mesmo relógio/eventos/legendas e poses no centro óptico real; o asset de prévia permanece intacto. No solo, a mesma pá escava, mede resistência e produz impacto. As fotografias antes/depois são da partida; fotografias NASA são identificadas como arquivo. A visita é uma reconstituição imaginada de uma câmera inativa, explicitamente apresentada antes dos atores.

A composição adapta as antigas tabelas {entries,id} dos painéis para LocalizationService, sem alterar os dados de seus donos. Legendas e PT/EN usam a mesma apresentação compartilhada, inclusive paginação da SUR-014 em fonte ampliada. O menu de pausa comum substitui os botões duplicados de cada prévia. Não há voz sintética nem vento lunar.

## Controles

- Segurar E: preparar/enviar ordens, registrar imagem e operar a pá; soltar conserva progresso.
- Setas/mouse: ajustar enquadramento quando disponível. H: assistência explícita de enquadramento, sem concluir a observação.
- E novo após um resultado: próximo alvo. É preciso registrar os três alvos.
- P/Esc ou botão superior: pausa; configurações, PT/EN, volume, fonte e movimento reduzido estão ali. Depois de perda de foco, clique em Continuar.
- E novo ou botão no fim: voltar ao arquivo. E mantido durante o fim não deve saltar o memorial.

O atraso de 1,3 s afeta o efeito de uma ordem, não o mouse/HUD/pausa. Não há Game Over nem conclusão por reflexo.

## Reconstrução e build

Menu `Abandoned Space Apps > Surveyor > Reconstruir capítulo completo`, ou batch `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorChapterBuilder.BuildFromCommandLine`. Reconstrói apenas cenas/prefabs próprios.

Para Web, iniciar Unity 6000.6.7f1 com `-batchmode -buildTarget WebGL -projectPath <raiz> -executeMethod Abandoned.SpaceApps.EditorTools.SurveyorChapterWebBuild.BuildFromCommandLine -logFile <log>`. O builder inclui explicitamente as duas cenas, usa o perfil Leve de QA-001, IL2CPP e Brotli. Não altera a lista global de cenas. Saída local ignorada: `Builds/SurveyorWeb`; disponibilizar o diretório por HTTP com MIME e Content-Encoding br, nunca abrir index.html por file://.

`QA/WebBaseline/serve.py --root Builds/SurveyorWeb --port 8868` é o servidor local de verificação. `QA/SUR-015/check_chapter.py --browser chromium` ou `--browser firefox` faz a jornada com teclado em navegador isolado. Ferramentas e binários de teste em QA/Local não são distribuídos.

## Música e fontes

Três trechos originais temporários gerados pelo script versionado em `SourceAssets/Surveyor3/Score`, sem samples nem gravações externas. Departure, Science e Return usam o AudioDirector comum com crossfades; efeitos mecânicos vêm das respectivas tarefas. Ainda cabe direção musical humana. ASSET_CREDITS e REUSE identificam origem e limites. Os fatos seguem `Docs/Chapters/03_SURVEYOR_SOURCES.md`.

## Aceitação

Veja `Assets/_Game/Chapters/Surveyor/QA/Surveyor/Acceptance.md` e evidências em `QA/SUR-015`. Tempo automatizado não é mediana humana. Teste com cinco novatos, GPU integrada, memória total da aba e upload real itch.io continuam exigindo os respectivos dispositivos/pessoas/acesso. Este capítulo não declara integrada a campanha global dos três capítulos.


## Acabamento e ficha final

SUR-016 reaplica o acabamento de terreno/luva durante a reconstrução e elimina pausas redundantes. SUR-017 adiciona a ficha opcional em Detalhes, com PT/EN, fonte NASA e identificação do exemplar de teste Smithsonian. A capa referencia a foto NASA existente. A ficha não é aberta no final e fica fora da contagem do capítulo. O builder final é `Abandoned.SpaceApps.EditorTools.ReturnedCameraBuilder.BuildWeb`; o ZIP local para itch.io fica em `Builds/Surveyor3-itch.zip`. Evidências finais em QA/SUR-017.
