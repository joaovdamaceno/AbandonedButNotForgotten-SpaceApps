# Pioneer 10 — roteiro v2: fases, mapeamento e trocas de frase

Fontes (cópias sem alteração, enviadas pelo responsável em 15/11/2026): [`ROTEIRO_PIONEER10.md`](ROTEIRO_PIONEER10.md) (9 cenas, diretrizes, linha do tempo, falas em PT) e [`CAPTIONS_PIONEER10_EN_UK.md`](CAPTIONS_PIONEER10_EN_UK.md) (legendas por minuto em inglês britânico). O capítulo de registro continua sendo [`../02_PIONEER_10.md`](../02_PIONEER_10.md) (estados, interações, contratos) e [`../02_PIONEER_SOURCES.md`](../02_PIONEER_SOURCES.md) (fatos e correções); este documento diz como o roteiro v2 foi aplicado por cima deles.

Decisões do responsável (15/11/2026): **entrega em fases**; visual **só o que cabe no perfil Web leve** (barras de cinema pela interface, câmeras pelo `CameraDirector`, luz por grupo de luz; sem Cinemachine nem pós-processamento); **fatos seguros** do registro de fontes prevalecem sobre as frases do roteiro quando elas divergem (lista abaixo).

## Mapeamento das 9 cenas para os estados do capítulo

| Cena do roteiro v2 | Alvo | Estado | Hoje (fase 1) |
| --- | --- | --- | --- |
| 1 Pioneira (0:00–0:55) | 55 s | `PIO.Launch` (40 s) | Legendas novas; ainda tem o hold de E no cartão do gerador. Faltam a Lua, Marte e a montagem de RTGs de outras sondas |
| 2 O cinturão (0:55–1:45), **só cinema** | 50 s | `PIO.Belt` (58 s) | Legendas novas; ainda tem a interação de registrar poeira. Falta o dolly zoom, o contador de dias e a revelação de 307 Nike |
| 3 O rei dos planetas (1:45–2:15) | 30 s | `PIO.JupiterApproach` (27 s) | Quatro legendas novas. Faltam Júpiter nascendo de um ponto, a Terra pequena para comparar e o limbo/terminador |
| 4 Interação 1: uma foto, três passos (2:15–3:00) | 45 s | `PIO.Imaging` (74 s) | Legendas novas; a mecânica continua de oito faixas e recuperação, não de três passos (vermelho, azul, juntar) |
| 5 O mais perto possível (3:00–4:05), cinema | 65 s | **`PIO.Closest` (40 s), novo** | Estado novo só de cinema, 7 legendas, 5 planos nos anchors do palco. Faltam luas em 3D, fotos reais das luas, medidor de radiação, linhas Terra–Lua e da trajetória, velocímetro |
| 6 Uma carta para as estrelas (4:05–4:25), **sem interação** | 20 s | `PIO.Plaque` (47 s) | Legendas novas; a placa ainda é de encaixar cinco peças |
| 7 Os anos (4:25–5:20), cinema com medidor | 55 s | `PIO.Outbound` (39 s) | Legendas novas; falta a linha do tempo ano a ano com ícones dos instrumentos e o medidor de energia |
| 8 Interação 2: trinta anos depois (5:20–6:00) | 40 s | `PIO.Listening` (52 s) | Legendas novas; a mecânica continua de três contatos e uma quarta tentativa |
| 9 Rumo a Aldebaran (6:00–6:40) | 40 s | `PIO.Farewell` (38 s) | Legendas novas; faltam as seis estrelas que acendem uma a uma |

Duração nominal passa de 375 s para **415 s (6:55)**, dentro do teto de 7:00 do roteiro (421 s com o alongamento de leitura de dois cues); o alvo de 6:40 só chega quando as interações virarem cinema.

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

**Ficaram de fora por não estarem no registro de fontes** (o roteiro as dá como confirmadas pela NASA; se a R7 as verificar, entram nas legendas das seis primeiras vezes): "primeira espaçonave a usar energia elétrica totalmente nuclear", "primeira a voar além de Marte", "primeira rumo ao espaço interestelar" e "primeiro objeto feito pelo homem além da órbita de Netuno". As "primeiras vezes" das legendas são as do registro: primeira missão aos planetas exteriores, primeira travessia do cinturão e primeiro sobrevoo de Júpiter.

Também diferem: a chave `PIO.09.xx` é a do estado novo (a numeração de chaves dos demais estados não foi mexida para não quebrar áudio e testes); os textos novos entram com status `derived` (precisam de revisão da R7, inclusive a versão EN-UK).

## Fases

1. **Fase 1 (esta entrega):** legendas PT-BR e EN-UK de todos os estados; estado novo `PIO.Closest` (cena 5) como cinema com os planos disponíveis; 9 etapas no capítulo, validador, dados e testes atualizados.
2. **Fase 2:** converter em cinema as interações que o roteiro não tem (cinturão, placa, cartão de energia) e retemporizar para os tempos do roteiro (Launch 55, Belt 50, Imaging 45, Plaque 20, Years 55, Listening 40, Farewell 40, Jupiter 30, Closest 65).
3. **Fase 3:** reduzir a interação 1 aos três passos do roteiro (vermelho, azul, juntar) e a interação 2 a uma rodada de sintonia.
4. **Fase 4:** as cenas de cinema que faltam: Lua e Marte, cinturão com dolly zoom e contador de dias, luas de Júpiter em 3D, passagem rente às nuvens com a trajetória, montagem dos anos com medidor de energia e ícones, as seis estrelas.
5. **Fase 5:** barras de cinema (letterbox) pela interface, cor quente na Terra e fria no espaço por grupo de luz.
