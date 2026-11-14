# SUR-005 — uma pequena correção

Abra `Preview/SurveyorMidcoursePreview.unity`, aperte Play e **Iniciar correção**. Após a viagem condensada, mova o contorno com setas/WASD ou arraste o mapa. Entre na zona clara e segure E por três segundos; soltar preserva o preparo. H alinha sem enviar a ordem. P/Esc pausa; o menu permite retomar, reiniciar ou voltar à entrada local.

Esta entrega cobre **Cruise → Descent**. O pouso não foi implementado aqui. A cena é uma entrada isolada de verificação; não substitui Boot/Dossier nem modifica BuildSettings.

## Fonte de duração e ritmo

`../SurveyorCruise.asset` é a referência editorial de **37 s**: viagem 10 s, orientação 14 s de referência, queima 8 s e transição 5 s. Inclui SUR-S04/S05/S06, marcadores e poses no contrato comum SequenceDefinition. O diagrama não é câmera a bordo ou mapa em escala.

O controller cria três recortes transitórios desse asset para o SequenceDirector: viagem, orientação e queima/transição. As durações de animação, eventos e legendas vêm do mesmo asset; não há timer paralelo para a queima. A orientação pode levar mais ou menos que seus 14 s de referência: o último frame permanece em espera, sem completar a atividade. Ao receber a ordem, o controller passa diretamente ao trecho de queima, sem obrigar a esperar o resto da janela editorial.

S05 tem seis segundos de leitura. A preparação fica disponível a partir de 1,7 s de orientação, enquanto já é possível mover o alvo: 1,7 + 3 s de preparo + 1,3 s de trânsito preservam a leitura antes da queima. Quem agir rapidamente pode terminar antes dos 37 s; a duração não é um prazo ou mínimo artificial. Teste com novatos continua necessário.

## Controle e assistência

- O disco de destino tem **35% da largura útil do mapa**. A zona de alinhamento inicial tem raio de 7,5%; o erro inicial é menor que 15%. O contorno e a marca efetiva são distintos.
- Todas as distâncias do mapa usam a largura como unidade nos dois eixos. A condição que habilita E corresponde à zona desenhada, inclusive em 4:3.
- Setas/WASD passam pelo InputRouter; arrastar usa eventos da UI. E continua exclusivamente no InteractionService. A alternativa por alternância das preferências também funciona.
- Sair da zona suspende o hold, preserva seu preparo e exige soltar/rearmar uma tecla ainda mantida. `SetAvailable` foi acrescentado ao adaptador local SUR-004 para isso; o contrato Core não mudou.
- Após oito segundos sem melhorar o alinhamento ou avançar o preparo, a instrução ganha destaque. Aos quinze, o botão H aparece e o raio de tolerância cresce para 12%. H já funciona pelo teclado desde a orientação. Nenhuma ajuda envia a ordem sozinha.
- O comando passa pela fila SUR-004 e só inicia a queima após o trânsito ativo. A queima de oito segundos é encenação qualitativa; não exibe combustível, delta-v ou leituras fictícias como dados de 1967.

## Integração

`SurveyorMidcourse.Configure` recebe os Core existentes, fila/adaptador local, SequenceDirector e o asset de referência. Configure antes de o ChapterDirector entrar em `Cruise`. Ao terminar, somente `ChapterDirector.CompleteStep("Cruise", "Descent")` avança o capítulo. O controlador não conclui o capítulo inteiro.

O prefab de preview contém serviços para executar sozinho. Na montagem de R1, consumir os componentes com os serviços compartilhados; não instanciar outra cópia de Core dentro de Boot. `CueEntered`/`CueExited` entregam SubtitleCue comuns para UI-004; `DrawLocalSubtitles=false` desativa a apresentação local quando o presenter global assumir. `TextResolver` aceita o serviço de localização; o fallback PT/EN está em `UI/MidcourseTexts.json`.

O cancelamento só libera a sequência e a geração de comandos pertencentes a este ato. A abertura SUR-003 recebeu a mesma guarda de posse ao cancelar: a ordem de inscrição dos componentes não pode interromper a nova sequência durante Launch → Cruise. Há testes também para um consumidor Descent assumir câmera/fila antes da limpeza de Cruise.

`Stop` cancela a execução e permite reiniciar reutilizando os três recortes. `Dispose` desmonta assinaturas e libera esses recortes; é chamado ao desabilitar em runtime. Ferramentas/testes que inicializam o componente no Editor chamam Dispose explicitamente, pois não devem depender dos callbacks de Play Mode.

O builder `surveyor.midcourse` só escreve sua raiz, o asset SurveyorCruise e a subpasta Midcourse. Menu **Abandoned Space Apps → Surveyor → SUR-005 Reconstruir correção de meio de curso**. Assets e referências são preservados em reconstrução.

## Ciência e autoria

Fontes e conflitos continuam em `Docs/Chapters/03_SURVEYOR_SOURCES.md`, S3-01/03/04. [NASA Science — Surveyor 3](https://science.nasa.gov/mission/surveyor-3/) confirma lançamento em 17/04 e destino Oceanus Procellarum; a montagem usa o roteiro do projeto. O calendário 17–19 é uma elipse de viagem, **não a data/hora atribuída à manobra**. A chegada de 20/04 UTC pertence ao próximo ato. Não foi publicado o valor de 4,19 m/s pendente de verificação da página do relatório.

Sem novos assets externos, fontes, áudio ou pacotes; desenho vetorial autoral com as fontes/tokens UI-001. Sem código copiado do Unity legado. Resultados e limites estão em `QA/SUR-005/` na raiz.
