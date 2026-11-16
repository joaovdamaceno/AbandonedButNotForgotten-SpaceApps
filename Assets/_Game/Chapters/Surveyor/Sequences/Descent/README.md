# SUR-006 — o chão, três vezes

Abra `Preview/SurveyorDescentPreview.unity`, aperte Play e **Iniciar descida**. O esquema acompanha a aproximação; quando o corte automático não confirmar, segure E ou o botão por três segundos. Soltar conserva a preparação. O sinal leva 1,3 s ativos para chegar. P/Esc pausa; o menu permite retomar, reiniciar ou voltar à entrada local. PT/EN, alternância de hold e movimento reduzido consomem as preferências comuns.

Esta entrega implementa **Descent → FirstImage**. A observação da superfície pertence à SUR-007. A cena e o prefab são entradas isoladas de verificação: não alteram Boot, Dossier ou BuildSettings.

## Montagem e duração

`../SurveyorDescent.asset` é a única fonte dos planos, poses do indicador, legendas e eventos. Orçamento editorial normal: **40 s**.

| Tempo ativo do ato | Evento |
| --- | --- |
| 0–12 s | Tendência de altitude, esquema técnico; explicação da anomalia aos 11 s |
| 12–27 s | Janela de comando de pelo menos 15 s; preparação de 3 s + fila comum SUR-004 |
| 18 s → 22 s → 26 s | Primeiro contato, reerguimento de aproximadamente 10 m, segundo contato |
| 26 s → 29 s → 32 s | Segundo reerguimento de aproximadamente 3 m, terceiro contato/assentamento |
| 31 s | Corte autorizado dos verniers; uma recepção posterior o executa imediatamente |
| 33,5 s | Fim do impulso estrutural e silêncio antes do próximo objetivo |
| A partir de 35 s | SUR-S09, pelo menos cinco segundos de leitura; transferência para FirstImage |

O fim da janela normal aceita uma preparação já em curso. Há mais três segundos de tolerância, sem iniciar outra espera indefinida. Soltar nessa tolerância entrega o comando à equipe; aos 30 s há um limite absoluto. Se necessário, os tempos mínimos das legendas estendem o ato até aproximadamente **42,3 s**, em vez de truncar a explicação ou o objetivo seguinte.

## História e liberdade de entrada

Fontes: matriz `Docs/Chapters/03_SURVEYOR_SOURCES.md`, S3-01/03/04. A [NASA Science](https://science.nasa.gov/mission/surveyor-3/) confirma os dois reerguimentos, o destino e a data. O [JPL TR 32-1177, Part I](https://www.nasa.gov/wp-content/uploads/static/history/alsj/a12/Surveyor-III-MIssionRpt1967028267.pdf), **p. 7 impressa / p. 17 do PDF**, descreve os verniers ainda ligados nos primeiros dois contatos e o corte pelo solo pouco antes do terceiro. O pouso é identificado por minuto: **20 ABR 1967 · 00:04 UTC · Oceano das Tempestades**.

Os três contatos acontecem independentemente da habilidade da pessoa. A janela foi ampliada e o tempo entre os contatos foi condensado; o movimento desenhado não é simulação balística ou telemetria recuperada. Uma confirmação antecipada arma a ordem, mas mantém os verniers até o corte encenado antes do terceiro contato. Essa autorização antecipada é recurso de acessibilidade editorial, não uma sequência de comandos atribuída à missão real. A interface distingue recebimento e desligamento. Um tooltip contextual explica a reconstrução sem repetir avisos em cada tela.

SUR-S07 explica a anomalia. A contingência **SUR-S08 só aparece quando GroundTeam assume**; o jogador recebe outro ID textual. SUR-S09 começa depois da janela de leitura do recibo. As legendas e os rótulos são prosa narrativa, sem imitar uma transcrição oficial.

## Contratos e integração

- Configure `SurveyorDescent` com os serviços Core existentes, fila/adaptador SUR-004, SequenceDirector, asset e um Transform indicador, antes da entrada em `Descent`. O Transform recebe apenas a tendência de altitude; não é o modelo físico da sonda.
- ChapterDirector é o único que avança `CompleteStep("Descent", "FirstImage")`. O ato não conclui o capítulo. InputRouter/InteractionService continuam donos de E; não há leitor de teclado ou relógio alternativo.
- `CueEntered`/`CueExited` expõem SubtitleCue para UI-004. `DrawLocalSubtitles=false` desliga a apresentação local e `TextResolver` aceita localização global. O fallback PT/EN fica em `UI/DescentTexts.json`. Sem legenda, a placa desaparece.
- `SurveyorDescentCameraGate` recebe o controller, ChapterDirector, câmera física e SurveyorSiteViewport. Chame `TryActivate` quando o próximo ato receber sua ordem de câmera. Antes de FirstImage, inclusive já assentada, ele recusa. Depois de ativada, a TV continua disponível nas etapas de superfície; reiniciar/descarregar desabilita a câmera e libera sua RenderTexture. O consumidor SUR-007 decide como apresentar a textura.
- O preview contém Core próprios só para execução isolada. R1 deve compor os controllers com os serviços compartilhados, sem duplicar Core ou carregar outros previews dentro de Boot.
- Cancelamento invalida callbacks antigos e libera só a sequência/geração possuída. Os testes cobrem Cruise → Descent nas duas ordens de inscrição e um consumidor de FirstImage que assume antes da limpeza anterior.
- `Stop` permite reentrada. `Dispose` também remove assinaturas e registro do ator. Ferramentas/testes do Editor chamam Dispose explicitamente. A desativação em runtime faz isso pelos callbacks.

O esquema usa um pequeno deslocamento e pulso nos contatos. Movimento reduzido zera o deslocamento, preservando a indicação âmbar e a legibilidade. Não há shake de câmera ou vídeo lunar antes do assentamento.

## Recursos e reconstrução

Os prefabs SUR-002 `LunarSite` e `SurveyorVisibleRig` são instanciados sem editar seus assets. A câmera real e o viewport 640×480 começam desativados e sem RenderTexture. O preview tem apenas um AudioListener. O esquema é vetorial próprio com fontes/tokens UI-001. Quatro clips mono curtos são síntese original: fundo dos verniers e três contatos diferentes, sem vento lunar, gravações externas ou narração.

Menu: **Abandoned Space Apps → Surveyor → SUR-006 Reconstruir pouso**. Batch: `-executeMethod Abandoned.SpaceApps.EditorTools.SurveyorDescentBuilder.BuildFromCommandLine`. O módulo `surveyor.descent` usa CORE-006 e preserva GUIDs/referências. Builder, testes, prefab e cena próprios estão nesta pasta; o asset de sequência e os componentes ficam nos caminhos de ownership do capítulo.

Resultados, capturas e limites reais: `QA/SUR-006/` na raiz. Sem alteração de dependências/pipeline, build Web ou certificação de compreensão por novatos nesta task.
