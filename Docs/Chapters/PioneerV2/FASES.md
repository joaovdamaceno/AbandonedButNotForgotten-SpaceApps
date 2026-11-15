# Pioneer 10 — roteiro v2: fases, mapeamento e trocas de frase

Fontes (cópias sem alteração, enviadas pelo responsável em 15/11/2026): [`ROTEIRO_PIONEER10.md`](ROTEIRO_PIONEER10.md) (9 cenas, diretrizes, linha do tempo, falas em PT) e [`CAPTIONS_PIONEER10_EN_UK.md`](CAPTIONS_PIONEER10_EN_UK.md) (legendas por minuto em inglês britânico). O capítulo de registro continua sendo [`../02_PIONEER_10.md`](../02_PIONEER_10.md) (estados, interações, contratos) e [`../02_PIONEER_SOURCES.md`](../02_PIONEER_SOURCES.md) (fatos e correções); este documento diz como o roteiro v2 foi aplicado por cima deles.

Decisões do responsável (15/11/2026): **entrega em fases**; visual **só o que cabe no perfil Web leve** (barras de cinema pela interface, câmeras pelo `CameraDirector`, luz por grupo de luz; sem Cinemachine nem pós-processamento); **fatos seguros** do registro de fontes prevalecem sobre as frases do roteiro quando elas divergem (lista abaixo).

## Mapeamento das 9 cenas para os estados do capítulo

| Cena do roteiro v2 | Alvo | Estado | Hoje (fase 1) |
| --- | --- | --- | --- |
| 1 Pioneira (0:00–0:55) | 55 s | `PIO.Launch` (40 s) | Legendas novas; **cinema (fase 2)**: a demonstração dos geradores corre sozinha; **fase 4, parte 2**: a Lua do gráfico Terra–Lua ganha mares e crateras. Faltam o contador "11 horas depois", um sobrevoo da Lua em 3D e a montagem de RTGs de outras sondas |
| 2 O cinturão (0:55–1:45), **só cinema** | 50 s | `PIO.Belt` (54 s) | Legendas novas; **cinema (fase 4, parte 1)**: sequência própria `PIO_BeltCinema` com a parede de pedras na teleobjetiva, dolly zoom (10° a 30°) com o contador de dias (15 JUL 1972 a 15 FEV 1973) e a revelação de 307 Nike num céu vazio. **Parte 2:** a abertura mostra Marte (esfera ocre iluminada, procedural) ao fundo, à direita da sonda. Falta a lente de 5° (o contrato aceita 10°) |
| 3 O rei dos planetas (1:45–2:15) | 30 s | `PIO.JupiterApproach` (27 s) | Quatro legendas novas; **fase 4, parte 3**: Júpiter cresce de um ponto de luz até ~73% da altura do quadro (sonda em silhueta no primeiro plano) e uma Terra 11,2 vezes menor entra ao lado. Faltam a Grande Mancha Vermelha cruzando, o brilho no limbo e o terminador marcado |
| 4 Interação 1: uma foto, três passos (2:15–3:00) | 45 s | `PIO.Imaging` (74 s) | Legendas novas; **três passos (fase 3)**: girar em vermelho (E mantido), trocar para azul (toque) e girar, e juntar (toque); sem radiação nem recuperação. A duração segue a da sequência (74 s) |
| 5 O mais perto possível (3:00–4:05), cinema | 65 s | **`PIO.Closest` (40 s), novo** | Estado novo só de cinema, 7 legendas; **fase 4, parte 4**: as três luas galileanas em 3D (6 s), medidor de radiação com estática (6–17 s), a comparação de distâncias Terra–Lua contra sonda–nuvens e o texto de velocidade no ponto mais próximo, e a trajetória que curva ~38° em volta de Júpiter num plano alto. Faltam fotos reais das luas, Io com o "X", o mergulho rente às nuvens, linhas de campo, a seta do movimento de Júpiter e o velocímetro subindo |
| 6 Uma carta para as estrelas (4:05–4:25), **sem interação** | 20 s | `PIO.Plaque` (47 s) | Legendas novas; **cinema (fase 2)**: as cinco peças encaixam sozinhas, uma a cada 5,5 s, sem instruções de E. Falta acender cada símbolo na própria placa 3D |
| 7 Os anos (4:25–5:20), cinema com medidor | 55 s | `PIO.Outbound` (39 s) | Legendas novas; **cinema (fase 2)**: a priorização da comunicação corre sozinha em 3 s, sem pedir E; **fase 4, parte 5**: o HUD dos anos (ano de 1974 a 1998 parando nos marcos reais, linha do tempo, ícones dos instrumentos que apagam em turnos e medidor de energia com as marcas reais de 155 W e ~140 W, curva estilizada) substitui o painel do atlas. Faltam os desligamentos com datas (não verificadas), o som e o ano de 2001 |
| 8 Interação 2: trinta anos depois (5:20–6:00) | 40 s | `PIO.Listening` (52 s) | Legendas novas; **uma rodada (fase 3)**: só o primeiro contato pede o botão; o segundo, o terceiro e a tentativa sem sinal chegam sozinhos nos marcos |
| 9 Rumo a Aldebaran (6:00–6:40) | 40 s | `PIO.Farewell` (38 s) | Legendas novas; **fase 4, parte 6**: seis estrelas acendem uma a uma ao redor da sonda (20–30 s), cada uma com um rótulo curto e fonte. Falta o plano final com a placa refletindo Aldebaran |

Duração nominal passa de 375 s para **411 s (6:51)** (415 s até a parte 1 da fase 4), dentro do teto de 7:00 do roteiro (421 s com o alongamento de leitura de dois cues); o alvo de 6:40 só chega quando as interações virarem cinema.

## O que é diferente do roteiro e por quê (fatos seguros)

O registro de fontes manda corrigir estas formulações, e o validador `Tools/validate_pioneer_data.py` as rejeita. Trocadas nas legendas PT-BR e EN-UK:

| Roteiro v2 | Legenda usada | Motivo (`02_PIONEER_SOURCES.md`) |
| --- | --- | --- |
| "Nenhuma máquina humana tinha ido além de Marte" | "Uma pequena sonda … partiu rumo aos planetas exteriores" | Correção de roteiro aprovada |
| "A máquina mais rápida que a humanidade já tinha construído" | "Ao deixar a Terra, era a máquina mais veloz já lançada pela humanidade" | Qualificar na partida e à época (P-S02) |
| "Assim ela fica sempre de pé e apontada para a Terra" | "Esse giro ajuda a manter a sonda estável" | Estabilidade por spin, sem prometer apontamento |
| "Baterias nucleares" (RTG) e "painéis solares" | "RTGs, geradores que transformam o calor do plutônio em eletricidade" | Usar geradores; sem painéis solares |
| "Criada em só 3 semanas" (placa) | removida | Sem fonte NASA primária suficiente |
| "A radiação bagunçou o computador" | "Comandos indevidos no instrumento"; "comandos de reserva" | Nomear o instrumento (P-S08) |
| "Além de Netuno, na época o último planeta" | "Cruzou a distância da órbita de Netuno" | Cruzar distâncias orbitais, com 1983 marcado |
| "A energia acabou" | "Sem energia suficiente para nos alcançar" | Sem sequência interna observada de falhas |
| "Cerca de 2 milhões de anos" | "Uma viagem de milhões de anos" | Direção geral e escala de milhões de anos |
| "Vermelho mostra muitos detalhes das nuvens" | removida | Não confirmada para a Pioneer 10 (a própria versão EN-UK já a retirara) |

**Atualização (parte 6):** a página da NASA dos 50 anos do lançamento, lida em 15/11/2026, sustenta "projetada para voar além de Marte", "primeira travessia do cinturão", "primeira em trajetória para sair do Sistema Solar" e "primeiro objeto a deixar os planetas exteriores para trás" (13/6/1983); esses entraram nas seis estrelas, reescritos como a NASA os escreve. **Seguem de fora:** "primeira espaçonave a usar energia elétrica totalmente nuclear" (a página só diz que usava RTGs), "primeira rumo ao espaço interestelar" (virou "em rota para sair do Sistema Solar") e "além de Netuno" (virou "além das órbitas dos planetas"). As "primeiras vezes" das legendas são as do registro: primeira missão aos planetas exteriores, primeira travessia do cinturão e primeiro sobrevoo de Júpiter.

Também diferem: a chave `PIO.09.xx` é a do estado novo (a numeração de chaves dos demais estados não foi mexida para não quebrar áudio e testes); os textos novos entram com status `derived` (precisam de revisão da R7, inclusive a versão EN-UK).

## Fases

1. **Fase 1 (esta entrega):** legendas PT-BR e EN-UK de todos os estados; estado novo `PIO.Closest` (cena 5) como cinema com os planos disponíveis; 9 etapas no capítulo, validador, dados e testes atualizados.
2. **Fase 2 (entregue):** o lançamento, o cinturão, a placa e os anos viram cinema; só restam duas interações (a foto e a sintonia de rádio). **Retemporizar para os tempos do roteiro (Launch 55, Belt 50, Imaging 45, Plaque 20, Years 55, Listening 40, Farewell 40, Jupiter 30, Closest 65) NÃO foi feito**: os testes de cada atividade dependem dos marcos de tempo das sequências atuais, e uma duração só faz sentido junto com as imagens da fase 4. A regra de leitura do projeto (16 caracteres por segundo) também torna alguns tempos do roteiro (por exemplo, a placa em 20 s com seis legendas) inviáveis sem cortar texto.
3. **Fase 3 (entregue):** a interação 1 vira os três passos do roteiro (vermelho, azul, juntar) e a interação 2 uma única rodada de sintonia. As duas continuam com as durações atuais (74 s e 52 s); a radiação e a recuperação saíram da cena 4 e ficam só nas legendas da cena 5.
4. **Fase 4 (em partes, um PR por cena):** partes 1 (cinturão, cena 2), 2 (Marte e Lua em 3D), 3 (Júpiter e a Terra pequena, cena 3), 4 (luas, radiação e trajetória, cena 5), 5 (os anos, cena 7) e 6 (as seis estrelas, cena 9) entregues. As cenas de cinema que faltam: Lua e Marte, cinturão com dolly zoom e contador de dias, luas de Júpiter em 3D, passagem rente às nuvens com a trajetória, montagem dos anos com medidor de energia e ícones, as seis estrelas.
5. **Fase 5 (entregue):** barras de cinema (letterbox) pela interface nos sete estados que são só cinema (recolhidas na foto, na sintonia e no memorial) e luz quente na Terra e fria no espaço por grupo de luz. Sem correção de cor por cena nem pós-processamento (perfil Web leve).
