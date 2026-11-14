# Capítulo III — Surveyor 3: os olhos que ficaram na Lua

**Estado:** especificação de produção; nenhuma funcionalidade deste documento foi implementada no novo projeto. Responsável principal: **R6**. Dependências comuns: CORE-001 a CORE-005, ART-001 e QA-001. Fontes e divergências estão em [03_SURVEYOR_SOURCES.md](03_SURVEYOR_SOURCES.md). Data de revisão: 14/11/2026.

## Experiência exata, em uma página

O jogador assume o lugar dos instrumentos de uma sonda estacionária. Primeiro, só tem números e o silêncio de um aparelho ainda sem câmera operacional. Confirma a partida do estágio Centaur, ajuda a corrigir a trajetória e acompanha um pouso que toca o chão três vezes. O envio de ordens tem uma espera visível de aproximadamente 1,3 segundo. A interface responde imediatamente; é o comando físico que está viajando.

Depois do pouso, segurar **E** liga a câmera. O cinza da Lua se constrói em linhas. A primeira imagem tem tempo para existir. O jogador recebe três enquadramentos claros: uma marca da aterrissagem, a encosta e uma pata da própria sonda. A câmera não caça pontos invisíveis: cada destino tem nome e moldura, e a assistência pode enquadrá-lo.

A pá entra na imagem. O jogador segura **E** para abrir uma trincheira, depois para pressionar e soltar a pá sobre outra pequena área. Vê sulco, bordas e deformação real na imagem. As medições mostram por que reconhecer o solo era importante antes de enviar astronautas. Uma montagem curta distingue as três ações jogadas do conjunto de testes realizado na missão real.

A câmera olha para a Terra eclipsando o Sol em abril de 1967. O vermelho tênue é da luz atravessando a atmosfera terrestre; não é um halo mágico nem uma Terra menor que o Sol. Depois, a sombra alonga e a transmissão termina. Passam 31 meses em quatro mudanças de iluminação e um calendário, sem esperar dezenas de amanheceres.

Um letreiro prepara uma reconstrução poética: **“A imagem que ela nunca chegou a transmitir.”** Em novembro de 1969, o Intrepid pousa ao longe. No dia seguinte, botas e uma luva entram no antigo campo de visão. A câmera é retirada. Corte para preto; a fotografia real mostra o que aconteceu. Parte da Surveyor 3 voltou para a Terra; o restante continua na Lua. A experiência termina em uma lembrança, não em uma falha do jogador.

## Duração e montagem

Os tempos abaixo são o percurso de referência com pessoa iniciante, não um cronômetro que encerra a atividade. Leituras no dossiê, carregamento e pausa estão fora da contagem. A ação precisa de entrada real: ajuda oferece enquadramento e zona ampla, sem completar ciência escondida.

| Ato / estados locais | Janela de referência | Meta | Intervalo a verificar com novatos |
| --- | --- | ---: | ---: |
| 1 — LaunchTelemetry / CentaurConfirm | 00:00–00:28 | 28 s | 22–30 s |
| 2 — Cruise / Midcourse | 00:28–01:05 | 37 s | 30–43 s |
| 3 — Descent / Cutoff / Settled | 01:05–01:45 | 40 s | 32–45 s |
| 4 — FirstImage / Observe / Sample / Eclipse | 01:45–04:40 | 175 s | 160–203 s |
| 5 — LunarNight / TimePassage | 04:40–05:10 | 30 s | 22–35 s |
| 6 — ImaginedVisit / HistoricalReceipt / End | 05:10–06:00 | 50 s | 38–52 s |
| **Total** | | **360 s** | **304–408 s** |

Não transformar os intervalos em tempos mínimos de espera artificiais. Se quatro de cinco primeiras partidas saírem do intervalo global de 300–420 segundos, ajustar duração de ações, extensão das legendas e montagem. Não acelerar a leitura nem falhar o pouso para ajustar a média.

## Regras comuns de interação

- **E** inicia a ação contextual; **segurar E** executa. Soltar pausa e conserva o progresso. Existe alternativa de acessibilidade “pressionar para iniciar / pressionar para parar”.
- **Mouse ou setas** escolhem orientação/objetivo. **E** confirma. Nenhum ato exige apertar no instante certo, desenhar um círculo ou dosar fisicamente combustível.
- O comando exibe imediatamente `Preparado → Enviado → Em trânsito · 1,3 s → Executando → Confirmado`. Uma única ordem física pendente; repetição de E não duplica operação. Cancelamento de tela e pausa continuam imediatos.
- Aproximadamente 1,3 s é um valor representativo do trajeto **Terra → Lua**, não um número exato constante. A telemetria usa um relógio narrativo; não acrescentar automaticamente outros 1,3 s a cada animação de confirmação. A ficha explica que ida e resposta real exigem mais tempo.
- Ajuda após 8 s sem ação correta: mostrar o botão e a área alvo. Após 15 s: oferecer `H · enquadrar objetivo`. Para MCC, ampliar a zona de aceitação. Sem tarefa invisível e sem Game Over.
- Cartões curtos de objetivo vivem no canto, não no centro das legendas. Todas as ordens usam o prompt compartilhado de CORE-003, com o mesmo estado de disponibilidade usado para consumir a tecla.
- Antes do pouso não há vídeo da superfície. Radar, mapa e silhueta esquemática são telemetria gráfica do jogo, nunca “imagem da câmera”.

## Plano de encenação e ações

### Ato 1 — Escuro, mas não vazio · 00:00–00:28

**00:00–00:09 — estabelecer propósito.** Fundo quase preto, escala vertical âmbar e um único retículo. A massa da composição está à esquerda; o espaço vazio aguarda a imagem que ainda não existe. SUR-S01 explica a missão. Identificação discreta: `17 ABR 1967 · 07:05 UTC`, `SURVEYOR 3 · ATLAS-CENTAUR AC-12`, sem despejar massa/plataforma no centro. Os detalhes ficam no dossiê.

**00:09–00:17 — lançamento por instrumentos.** Contagem completa; aceleração e duas vibrações suaves. O áudio de estrutura é um recurso subjetivo: não simular microfone em Surveyor. Não precisamos construir foguete, plataforma ou laboratório 3D. A contagem avança para órbita de estacionamento com uma curva de altitude esquemática.

**00:17–00:28 — primeira ordem sem teste de reflexo.** Objetivo `E · confirmar partida do Centaur`. Um anel muito largo fica estável; segurar E por 2,5 s preenche o comando. O botão continua disponível se o jogador demorar. Após trânsito de 1,3 s, a linha de trajetória se estende até a Lua. SUR-S02 e S03 ensinam o atraso. Esta confirmação representa uma ordem da equipe dentro de uma sequência condensada, não transfere ao jogador o comando real de voo.

**Transição:** o círculo da Terra encolhe até virar a origem de um pequeno mapa. O grave desaparece antes de o título do ato seguinte surgir. Sem hard cut para câmera externa.

### Ato 2 — Uma ordem atravessa o vazio · 00:28–01:05

**00:28–00:38 — viagem condensada.** O calendário vai de 17 para 19 de abril; arcos de trajetória, e não três dias de contador correndo sem conteúdo. SUR-S04 estabelece o motivo da correção.

**00:38–01:00 — correção de meio de curso.** Um círculo alvo marcado `OCEANUS PROCELLARUM` ocupa 35% da largura útil; o indicador começa a no máximo 15% de erro. A pessoa move setas/mouse por uma escala lenta, ou usa `H · alinhar`. A orientação desejada é um contorno, a confirmada uma marca sólida. Segurar E por 3 s envia uma pequena correção; o contador verde mostra `Correção preparada` antes da espera. O curso se aproxima do alvo durante 8 s e a Lua cresce como diagrama.

Não há campo de asteroides, combustível insuficiente, mapa sem legenda ou caminho em que não seja possível acertar. A queima tem duração calculada pelo sistema depois da confirmação; isto preserva a agência sem exigir pilotagem. Não mostrar delta-v inventado como registro real. O valor histórico de 4,19 m/s é reservado ao arquivo, se confirmado pelo relatório indicado nas fontes.

**01:00–01:05 — passagem à descida.** Congelar o alvo atingido, limpar a maior parte do mapa e puxar a escala do altímetro para o primeiro plano. SUR-S06 prepara a expectativa.

### Ato 3 — O chão três vezes · 01:05–01:45

**01:05–01:17 — altitude.** Mostrar tendência descendente sem exigir leitura de todos os números. Um pictograma de pernas faz parte do esquema, não uma câmera. O indicador `DESLIGAMENTO AUTOMÁTICO` não confirma no momento esperado. A linha fica âmbar; SUR-S07 explica o problema.

**01:17–01:34 — comando de corte.** A ordem `SEGURE E · enviar desligamento` surge por pelo menos 15 s, com 3 s de hold e 1,3 s em trânsito. O primeiro contato provoca um impulso visual, a escala sobe aproximadamente a 10 m; um segundo impulso chega aproximadamente a 3 m. O comando enviado pelo jogador integra uma encenação da intervenção de solo. Os contatos históricos acontecem mesmo se a pessoa confirmar muito cedo; eles não foram causados por sua habilidade. Se não confirmar até o fim, a equipe executa a ordem e a legenda diz `A equipe enviou o corte dos motores`; essa é a única assistência automática de segurança, explícita, sem prêmio de destreza.

**01:34–01:45 — assentamento.** O altímetro estabiliza. Breve silêncio de 1,5 s. Letreiro: `20 ABR 1967 · 00:04 UTC · OCEANO DAS TEMPESTADES`. Não publicar segundos e coordenadas divergentes como certeza. SUR-S09 entrega o objetivo concreto seguinte: ligar os olhos.

**Contraste sonoro:** dois impactos curtos e distintos, percebidos como vibração estrutural, não explosões. Movimento reduzido troca shake por uma pulsação no retículo. O desfecho nunca depende de compensar 1,3 s com reflexos.

### Ato 4 — Finalmente, uma imagem · 01:45–04:40

**01:45–02:00 — abrir os olhos.** `SEGURE E · receber primeira imagem`, duração de hold 2 s; trânsito 1,3 s; varredura visual de 4 s. A câmera física é um único ponto fixo na sonda, inclinado conforme a posição assentada. Passa de branco/cinza a relevo por linhas horizontais, sem glitch de terror. Uma identificação breve de `TV · 200 LINHAS` é uma representação do modo existente, com tempo acelerado. A missão usou também 600 linhas; não afirmar que todas as 6.326 fotos tinham 200 linhas ou que chegavam como vídeo ao vivo.

**02:00–02:31 — olhar sem caçar.** Três cartões pequenos em sequência: `Marca da aterrissagem`, `Parede da cratera`, `Pata da sonda`. Cada um tem uma âncora de enquadramento visível. Mouse/setas movem o alvo do espelho; E envia a orientação; 1,3 s depois, a câmera se move suavemente, com limite de velocidade. H escolhe o enquadramento correto. E segurado por 1,5 s registra a imagem assim que a área ocupa a moldura larga. Depois de registrar o segundo alvo, o botão contextual `E · ampliar` alterna 25 mm/100 mm; não acrescentar uma tecla obrigatória nova. A UI mantém a opção “Visão ampla” para retorno. Uma confirmação sonora e o carimbo da imagem substituem pop-up.

**02:31–03:13 — a pá aparece.** Um plano útil de 100 mm mostra a pá antes, durante e depois do contato. O braço tem três poses seguras: repouso, acima da área e contato. Só baixa depois de estar sobre uma área autorizada. Segurar E por 12 s soma quatro passes e abre **uma** trincheira, com uma malha de poucos vértices ou blend shape. O sulco aumenta em profundidade e joga pequenas partículas em arcos balísticos, sem nuvem suspensa; o terreno nunca corta a pá no recolhimento. Ponto final abaixo de 18 cm; usar a escala no quadro para tornar isso visível. A UI diz `Trincheira de demonstração`, não `4/4`.

O recolhimento segue contato → subida vertical de 8 cm → retração lateral → repouso. Se cortar a animação, a pá vai primeiro à pose de segurança, nunca atravessa o solo. Ficar parado segurando E tem feedback a cada 0,8–1,2 s: arrasto, pequena mudança da borda, distância percorrida e cursor de operação. Soltar realmente pausa.

**03:13–03:38 — medir a resistência.** O quadro continua no mesmo lugar. Pressionar E por 4 s encosta a pá em uma área adjacente marcada; uma curva qualitativa sobe sem unidade numérica inventada. E por mais 2 s solta a pá de pequena altura; um curto tremor e a marca de impacto confirmam a ação. Três ações jogadas são seguidas por montagem de 6 s: outros sulcos e um pequeno registro `MISSÃO REAL · 4 TRINCHEIRAS · 4 TESTES DE CARGA · 13 IMPACTOS`. A pessoa não precisa repetir 21 operações. Não confundir marcas de testes com danos da aterrissagem.

**03:38–04:02 — o resultado importa.** Comparar uma foto antes e depois lado a lado, usando as imagens realmente geradas nesta partida, e depois revelar uma foto histórica identificada. SUR-S18/S19 conectam o comportamento da amostra ao planejamento da Apollo. Não dizer que uma medida local certificava qualquer ponto lunar ou que “areia úmida” significa água na Lua. Não usar `98 kPa` sem relatório, método e unidade reconciliados.

**04:02–04:28 — a Terra cruza o Sol.** Data `24 ABR 1967`. E envia o enquadramento já sugerido; subida lenta do espelho ao limite físico, depois dissolução para exposição apropriada. A Terra oculta o Sol; uma borda de vermelho escuro aparece pela atmosfera. Não usar corona branca intensa nem anel perfeito de eclipse anular. O título pode ocupar só 2 s; reservar 8 s para a imagem sem frases. A inclinação real da sonda permitiu esse enquadramento: não girar o corpo inteiro para alcançar o céu.

**04:28–04:40 — voltar ao terreno.** Uma ordem de câmera retorna ao próprio sulco, agora com sombra longa. SUR-S22 faz a ponte para o limite de energia. As imagens e os dados foram enviados à Terra: não representar esquecimento ou arquivo perdido.

### Ato 5 — A noite dura mais que uma pausa · 04:40–05:10

**04:40–04:56 — fim das transmissões.** Corte por correspondência na mesma trincheira. Sombra avança; a temperatura é indicada por tendência qualitativa. O contador histórico final `6.326 IMAGENS` é explicitamente `TOTAL DA MISSÃO`, separado da galeria do jogador. Letreiro `4 MAI 1967 · ÚLTIMO CONTATO`. O restante do HUD some junto; sem caixas de legenda vazias. A legenda informa que tentativas de reativação posteriores não recuperaram a missão, sem inventar uma última frase da sonda.

**04:56–05:10 — 31 meses.** Não são 32 dias terrestres. Quatro transições de luz representam muitas manhãs/noites; a posição do cenário não muda e o calendário passa por `1967 → 1968 → NOV 1969`. Uma linha pequena: `31 MESES DEPOIS`. O quadro é a reconstituição visual de uma câmera já desligada, preparada pela legenda SUR-S25. Não vender este trecho como gravação recuperada. O áudio eletrônico cessa; o tema musical faz a única ligação.

### Ato 6 — A imagem que ela não fez · 05:10–06:00

**05:10–05:15 — contrato com o espectador.** Sobre preto, a legenda integral SUR-S26: `A câmera já estava desligada. Esta é a imagem que ela nunca chegou a transmitir.` Só uma indicação clara; sem rótulos repetidos durante cada plano.

**05:15–05:29 — 19 de novembro de 1969.** Mesmo centro óptico, porém sem HUD de instrumento operacional. O Intrepid é uma silhueta coerente com fotografias, a cerca de 180 m. A descida dura só 5 s; o restante é a poeira balística indo rente ao chão e baixando. No vácuo não existe parede de poeira suspensa como tempestade marciana. Não pintar a lente inteira de marrom nem atribuir toda a alteração de cor da sonda ao pouso da Apollo. A iluminação de Sol baixo recorta as pernas do LM e o relevo; uma segunda luz falsa não cria outra sombra forte.

**05:29–05:42 — 20 de novembro.** Match dissolve com o LM já pousado e a mesma encosta. Uma sombra humana anuncia a chegada antes das botas. As botas atravessam só o terço inferior; uma luva se aproxima por uma diagonal. Não há fala inventada de astronauta nem som aéreo de passos. O quadro recebe uma pequena rotação por manuseio da câmera e corta para preto antes de exigir astronauta completo. A sequência é arte em tempo real, baseada nos registros da visita.

**05:42–05:54 — recibo histórico.** Revelar a fotografia real de Conrad/Surveyor/Intrepid e a legenda `FOTOGRAFIA REAL · APOLLO 12 · 20 NOV 1969`. SUR-S29 informa a recuperação. Não ilustrar uma câmera de teste de museu como se fosse a peça lunar. O jogo pode terminar sem foto de museu: a evidência NASA é suficiente.

**05:54–06:00 — permanecer.** Preto, nome `SURVEYOR 3`, datas `1967 · Lua` e a frase SUR-S31. E só serve para `Voltar ao arquivo` depois do tempo mínimo de leitura; o comando já segurado do último minigame precisa ser solto antes de poder confirmar. A música termina suavemente, sem um novo passeio externo ou início automático de outro capítulo.

## Legendas finais PT/EN, IDs e duração mínima

Tempos são **locais do ato**. Na implementação, sincronizar por evento/clip do ato, não por um relógio absoluto que continue rodando durante uma tarefa. Exibir no máximo duas linhas; dividir o cue em duas telas se a fonte ampliada exigir, mantendo o tempo total. Diálogos abaixo são legendas autorais, **sem narração de IA**.

| ID | Ato · entrada | Mínimo | Português brasileiro | English |
| --- | --- | ---: | --- | --- |
| SUR-S01 | 1 · 0 s | 7 s | Antes de enviar pessoas à Lua, precisávamos conhecer o chão onde pousariam. | Before sending people to the Moon, we needed to understand the ground they would land on. |
| SUR-S02 | 1 · 17 s | 5 s | Você é a Surveyor 3. Suas ordens vêm da Terra. | You are Surveyor 3. Your commands come from Earth. |
| SUR-S03 | 1 · ordem enviada | 6 s | O rádio leva cerca de 1,3 segundo para atravessar essa distância. | A radio command takes about 1.3 seconds to cross that distance. |
| SUR-S04 | 2 · 0 s | 6 s | Uma viagem de quase três dias. Uma pequena correção aproxima o destino. | Almost three days of travel. A small correction brings the destination closer. |
| SUR-S05 | 2 · tutorial | 6 s | Alinhe a marca ao círculo. Depois, segure E para enviar a correção. | Align the marker with the circle. Then hold E to send the correction. |
| SUR-S06 | 2 · confirmado | 4 s | Oceano das Tempestades. O próximo passo é pousar. | Ocean of Storms. The next step is landing. |
| SUR-S07 | 3 · 11 s | 6 s | O radar se confundiu perto do solo. Os motores continuaram ligados. | The radar became confused near the ground. The engines kept firing. |
| SUR-S08 | 3 · 18 s | 6 s | A equipe enviou o corte dos motores. A Surveyor tocou o chão três vezes. | The team sent an engine-cutoff command. Surveyor touched the ground three times. |
| SUR-S09 | 3 · assentada | 5 s | Agora, quieta. A primeira imagem está a um comando de distância. | Now, still. The first picture is one command away. |
| SUR-S10 | 4 · varredura pronta | 6 s | Linha por linha, a Lua começou a aparecer nas telas da Terra. | Line by line, the Moon began to appear on screens back on Earth. |
| SUR-S11 | 4 · primeira observação | 5 s | Marcas no chão contam como foi a chegada. | Marks in the soil tell the story of the landing. |
| SUR-S12 | 4 · segunda observação | 5 s | Ela parou na encosta de uma cratera, inclinada. | It came to rest on a crater slope, tilted to one side. |
| SUR-S13 | 4 · terceira observação | 5 s | A câmera olhava em volta usando um espelho móvel. | The camera looked around using a movable mirror. |
| SUR-S14 | 4 · pá preparada | 5 s | Ver o chão não bastava. Era preciso tocá-lo. | Seeing the ground was not enough. We had to touch it. |
| SUR-S15 | 4 · trincheira crescendo | 6 s | A pá abriu pequenas trincheiras para revelar o comportamento do solo. | The scoop opened small trenches to reveal how the soil behaved. |
| SUR-S16 | 4 · carga | 5 s | Pressionar mostrava quanto o terreno resistia. | Pressing showed how strongly the ground resisted. |
| SUR-S17 | 4 · impacto | 5 s | Soltar a pá permitia observar outra resposta do mesmo solo. | Dropping the scoop revealed another response from the same soil. |
| SUR-S18 | 4 · resultado | 6 s | Grãos soltos na superfície. Coesão suficiente para manter as bordas do sulco. | Loose grains at the surface. Enough cohesion to hold the trench edges. |
| SUR-S19 | 4 · comparação | 6 s | Esses dados ajudaram a planejar a chegada dos astronautas e de seus equipamentos. | These data helped planners prepare for astronauts and their equipment. |
| SUR-S20 | 4 · eclipse | 6 s | Em 24 de abril, a Terra passou diante do Sol, vista daqui. | On April 24, Earth crossed in front of the Sun, as seen from here. |
| SUR-S21 | 4 · eclipse completo | 6 s | Um contorno avermelhado: luz do Sol atravessando a atmosfera da Terra. | A reddish rim: sunlight passing through Earth's atmosphere. |
| SUR-S22 | 4 · retorno ao solo | 5 s | Enquanto havia luz, ela continuou observando. | While there was daylight, it kept observing. |
| SUR-S23 | 5 · 0 s | 6 s | A longa noite lunar chegou. Em 4 de maio, veio o último contato. | The long lunar night arrived. On May 4, contact ended. |
| SUR-S24 | 5 · 7 s | 6 s | Ela não voltou a responder. Mas suas imagens e medidas já estavam na Terra. | It never answered again. But its images and measurements were already on Earth. |
| SUR-S25 | 5 · 17 s | 5 s | Trinta e um meses passaram sobre a mesma paisagem. | Thirty-one months passed over the same landscape. |
| SUR-S26 | 6 · 0 s | 5 s | A câmera já estava desligada. Esta é a imagem que ela nunca chegou a transmitir. | The camera was already silent. This is the image it never got to transmit. |
| SUR-S27 | 6 · 6 s | 6 s | Em novembro de 1969, a Apollo 12 pousou a uma curta caminhada dali. | In November 1969, Apollo 12 landed a short walk away. |
| SUR-S28 | 6 · 20 s | 6 s | No dia 20, Pete Conrad e Alan Bean foram encontrar a velha sonda. | On the 20th, Pete Conrad and Alan Bean went to meet the old lander. |
| SUR-S29 | 6 · foto real | 6 s | A câmera, a pá e outras peças voltaram à Terra para serem estudadas. | The camera, scoop and other parts returned to Earth for study. |
| SUR-S30 | 6 · foto real + 6 s | 5 s | O restante da Surveyor 3 continua na Lua. | The rest of Surveyor 3 remains on the Moon. |
| SUR-S31 | 6 · título final | 6 s | Ela ficou. O que aprendemos com ela veio conosco. | It stayed. What we learned from it came home with us. |

**Implementação de tempo:** S29 entra no segundo 32 do ato 6; S30 no segundo 38; S31 no segundo 44. As três legendas têm 17 s combinados dentro dos 18 s finais. Não sobrepor duas falas porque uma ação terminou cedo. A referência termina em **06:00**, preservando a leitura do título final.

## Textos de UI PT/EN

| Chave | PT | EN |
| --- | --- | --- |
| dossier.title | Surveyor 3 | Surveyor 3 |
| dossier.subtitle | Lua · 1967 · O chão antes dos passos | Moon · 1967 · Ground before footsteps |
| dossier.question | O solo lunar sustentaria uma missão com pessoas? | Could the lunar surface support a crewed mission? |
| objective.launch | Confirme a partida para a Lua | Confirm departure for the Moon |
| objective.midcourse | Alinhe o destino e envie a correção | Align the destination and send the correction |
| objective.cutoff | Envie o desligamento dos motores | Send the engine-cutoff command |
| objective.firstimage | Receba a primeira imagem | Receive the first image |
| objective.observe | Registre a área destacada | Record the highlighted area |
| objective.trench | Abra uma trincheira com a pá | Open a trench with the scoop |
| objective.bearing | Pressione o solo e observe a marca | Press the soil and observe the mark |
| objective.impact | Solte a pá e compare a resposta | Drop the scoop and compare the response |
| objective.eclipse | Aponte o espelho para a Terra | Point the mirror toward Earth |
| command.pending | Comando em trânsito · 1,3 s | Command in transit · 1.3 s |
| command.confirmed | Comando confirmado | Command confirmed |
| help.frame | H · enquadrar objetivo | H · frame objective |
| mode.wide | Visão ampla · 25 mm | Wide view · 25 mm |
| mode.detail | Detalhe · 100 mm | Detail · 100 mm |
| historical.image | Fotografia real · NASA | Historical photograph · NASA |
| history.counter | Total da missão · 6.326 imagens | Mission total · 6,326 images |
| ending.return | E · voltar ao arquivo | E · return to archive |

## Produção de arte, luz e áudio

Uma cena lunar de câmera fixa, 40 × 40 m com detalhe, integrada a um skyline de cratera de aproximadamente 200 m de diâmetro. A escala da cratera vem do relatório; não criar uma montanha terrestre aleatória. Um solo principal, duas variações macro, normal de grão discreto, 8–12 rochas de formas diferentes e marcas de patas consistentes. No modo Leve: uma luz direcional com sombras limitadas, preenchimento por ambiente sem sombra extra, sem SSR/SSAO/volumetria. A TV usa RenderTexture no máximo 640×480 e um shader simples de varredura, não gravação de tela em 4K. Texto e HUD são renderizados por cima, nítidos.

O modelo da Surveyor usa silhueta, pernas, câmera/espelho e pá baseados em fotografias; o núcleo pode ficar fora de quadro. Modelo fornecido depois ou encontrado oficialmente será auditado em ART-001: não bloquear o primeiro build por um asset ainda não recebido. Alternativa concreta P0: reconstrução leve dos componentes visíveis com dimensões relativas documentadas, materiais metálico/branco e 1 atlas de 1024. Não afirmar que o modelo oficial foi baixado ou importado nesta etapa de planejamento.

Áudio: bipes suaves, confirmação imediata de envio, clique diferente de chegada, motores da pá em contato e tema instrumental discreto. Nada de vento lunar ou passos propagando-se pelo ar. Vibração subjetiva de estrutura nos primeiros atos, rotulada uma única vez no arquivo como linguagem cinematográfica. Música legalmente licenciada, editada em três camas curtas: intenção, descoberta, retorno. O silêncio de 1,5 s ao pousar e o silêncio antes da visita são parte da montagem. Não instalar sintetizador de voz.

## Cortes de escopo sem destruir o capítulo

P0 mantém todos os seis atos, uma trincheira jogável, um teste de carga, um impacto, eclipse, passagem de 31 meses, encontro imaginado e fotografia real. P1 refina material, pequenos movimentos da luva, câmera do museu e som. Não cortar a explicação de que a visita não foi transmitida, nem a evidência final real. Se a descida do Intrepid custar demais, mostrar apenas o ponto luminoso que some atrás do relevo, poeira baixa distante e o LM pousado na elipse seguinte; o enquadramento continua sendo o da Surveyor.

## Critérios de aprovação por capítulo

1. Uma pessoa que nunca joga consegue enviar todas as ordens sem mirar em pixel invisível. O botão que aparece é o mesmo que aceita E.
2. Pausar durante a fila de 1,3 s não duplica, perde ou antecipa o comando; retomar exige a mesma ação deliberada global.
3. A câmera nunca sai do ponto da Surveyor durante 1967; zoom altera lente, e não teleporta o equipamento.
4. Nenhum jogador precisa “salvar” um pouso real com reflexos. A contingência é responsabilidade explícita da equipe.
5. O sulco se aprofunda visivelmente; a pá sai verticalmente antes de recolher e nunca atravessa a malha.
6. A Lua tem pó balístico e ausência de vento. Eclipses e sombras usam geometria e exposição coerentes.
7. O contador 6.326 é histórico, não leitura fabricada do progresso do jogador. Telemetria animada é interface do jogo, sem simular números exatos da missão.
8. A passagem de 1967 a 1969 e a natureza imaginada da última visão são compreendidas sem abrir o arquivo de fontes.
9. O build Web sem cache carrega e conclui o capítulo; não há NullReference, crescimento de fotografias sem limite ou dependência de internet durante a missão.
10. Registrar cinco primeiras partidas e três perguntas: por que a pá foi usada; por que as imagens chegavam por rádio; por que a visita da Apollo importa ao equipamento abandonado. Aprovação humana pendente até haver resultados.
