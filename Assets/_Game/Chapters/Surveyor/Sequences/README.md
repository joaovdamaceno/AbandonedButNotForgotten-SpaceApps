# SUR-003 — abertura por telemetria

Abra `Preview/SurveyorLaunchPreview.unity` no Unity **6000.6.7f1** e aperte Play. A entrada local tem Iniciar, PT/EN e som; E ou o botão mantido prepara a partida. P/Esc pausa. Retomar, Reiniciar e Voltar ao arquivo funcionam nessa cena. A ausência de áudio não esconde informações essenciais.

Esta entrega termina em **Cruise**, ao fim do ato Launch. Viagem, pouso, câmera lunar e amostragem pertencem às próximas tarefas; esta cena não é um capítulo completo nem o dossiê global UI-002.

## Execução e montagem

- `SurveyorLaunch.asset`: SequenceDefinition comum, três planos de painel, 28 s de referência, SUR-S01–S03 e eventos nomeados.
- `SurveyorLaunch.prefab`: entrada local com os Core reais, câmera única, UIDocument e áudio. O builder `surveyor.launch` cria/atualiza apenas sua raiz e seus assets. Menu **Abandoned Space Apps → Surveyor → SUR-003 Reconstruir abertura**. Não altera BuildSettings.
- `SurveyorLaunchController`: oferece um candidato ao InteractionService, preserva o tempo acumulado, bloqueia a sequência no marco de envio e pede apenas `ChapterDirector.CompleteStep("Launch", "Cruise")` no término válido. Callbacks antigos são invalidados no cancelamento/reinício.
- `LaunchTelemetryView`: apresentação, intenções dos botões e resolvedor de textos. Não lê teclado nem muda etapas. A legenda vazia remove a placa.
- `SurveyorLaunchPreviewHost`: adaptador de teste local. Seu scene loader aceita somente a própria cena realmente aberta; voltar ao arquivo reutiliza essa entrada, sem fingir carregar Boot/Dossier.

O relógio da montagem continua sendo o SequenceDirector. Seu Update é desativado e o controller chama Tick com tempo do SessionClock, limitado à barreira de confirmação; assim uma queda de FPS não ultrapassa o comando. O hold usa o mesmo tempo ativo. A sequência recebe router nulo para não bloquear E em Cinematic; o controller possui um lease Panel. A pausa do ChapterDirector suspende ambos e a entrada comum exige soltar/rearmar uma tecla mantida.

## Ritmo

| Tempo da sequência | Acontecimento |
| --- | --- |
| 0–9 s | Propósito da missão e data de lançamento; SUR-S01 até 8 s |
| 9–17 s | Retículo de ascensão, tendência qualitativa e órbita |
| 17 s | Comando disponível; segurar E por 2,5 s; SUR-S02 |
| 22 s | Envio, se preparado; senão, espera sem expirar |
| 23,3 s | Execução única; extensão do mapa e desaparecimento do grave |
| 22–28 s | SUR-S03; depois, o proprietário avança para Cruise |

Se o jogador confirmar cedo, a montagem reserva a leitura antes de enviar. Se confirmar tarde, continua a partir de 22 s: nenhum dado é completado automaticamente e a legenda ainda tem sua janela de leitura. Soltar E preserva progresso. A preferência comum HoldMode.Toggle também é consumida.

## Integração por R1

No capítulo final, configure o controller com os serviços compartilhados e a sequência deste ato. Não instancie outra cópia dos serviços do prefab de preview dentro de Boot. A tabela do capítulo deve conter Launch → Cruise. Os eventos CueEntered/CueExited entregam SubtitleCue comuns para UI-004; desative `DrawLocalSubtitles` quando ligar o presenter global. `TextResolver` aceita UI-003; até essa entrega, `UI/LaunchTexts.json` mantém PT/EN local. `AudioCueRequested` oferece os IDs bed/release/stop para AUD-002; o AudioSource do host é provisório e local. SUR-004 implementará a fila geral, sem duplicá-la nesta tarefa.

## Ciência e autoria

Data, AC-12, órbita e propósito seguem a matriz `Docs/Chapters/03_SURVEYOR_SOURCES.md` e o catálogo da SUR-001. O painel não é imagem de uma câmera a bordo, não contém altitude numérica fictícia nem logs simulados como históricos. A ordem é uma encenação condensada do trabalho da equipe. **1,3 s é a aproximação didática da distância Terra–Lua, não a latência real no instante do lançamento**; esta distinção está também no tooltip do título do comando. A trajetória é um diagrama qualitativo, sem escala de distância ou duração orbital.

O grave de quatro segundos foi sintetizado pelo builder (55/82,5/110 Hz), sem voz, material comercial ou gravação histórica. Fontes Outfit e IBM Plex Mono vêm da UI-001, com suas licenças já incluídas. Não foi copiado código do Unity antigo.

Evidências e limites da validação: `QA/SUR-003/README.md` na raiz do repositório.
