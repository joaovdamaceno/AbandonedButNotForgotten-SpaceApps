# PIO-036 — Os dois jogos refeitos: a foto em ponto de vista e a sintonia do rádio

Pedido do responsável depois do teste em Play Mode (15/11/2026): (1) foto: câmera do ponto de vista da sonda, girando com ela, com Júpiter aparecendo de tempos em tempos, timing na barra (não só segurar E), vermelho e azul conforme o instrumento real, junção automática das fotos e a ideia de que várias fotos somadas apagam o ruído de uma só; (2) rádio: saber quando sintonizar, já poder interagir, várias rodadas com a zona cada vez menor, ondas bonitas, "intensidade" em vez de "perto/longe", escuta automática sem E, sinal mais fraco, sem o plano preto do chão; (3) Aldebaran menor, mas ainda maior que as estrelas vizinhas.

## O que a pesquisa disse (e o que não disse)

| Pergunta | Resposta | Fonte |
| --- | --- | --- |
| Os canais vermelho e azul operam ao mesmo tempo? | **Sim**, o instrumento faz medidas de duas cores simultâneas (azul de 3900 a 4900 Å, vermelho de 5800 a 7000 Å). Por isso cada foto traz um quadro vermelho e um azul, tirados juntos; na tela eles aparecem em sequência (vermelho e depois azul), entrelaçados | [NSSDC, experimento 1973-019A-15](https://nssdc.gsfc.nasa.gov/nmc/experiment/display.action?id=1973-019A-15) |
| Como a imagem é formada? | Pelo giro da sonda: o telescópio de 2,5 cm varre o planeta a cada volta e avança entre as voltas (mapas de "spin-scan" em vermelho e azul) | [Science, 1974](https://experts.arizona.edu/en/publications/the-imaging-photopolarimeter-experiment-on-pioneer-10/) |
| Quanto a sonda gira? | Giro nominal de 4,8 rpm (volta de 12,5 s); medido, variou entre ~4,25 e ~4,55 rpm. **Fonte secundária** (revisão em arXiv), não conferida em documento primário da NASA | [arXiv gr-qc/0208046](https://arxiv.org/pdf/gr-qc/0208046) |
| E a cor? | As imagens coloridas combinaram uma varredura vermelha e uma azul com um verde sintetizado | [Planetary Society](https://www.planetary.org/articles/3171) |

Não encontrei, nas fontes lidas, o ruído real do instrumento nem o número de linhas por imagem: o ruído do jogo é de composição.

## Verificação

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **252 de 252**. Novos: `PioneerSpinShotTests` (a demonstração não registra; um toque na janela tira uma foto com quadro vermelho e azul; fora da janela só avisa; cinco fotos se juntam sem tecla e terminam uma vez; a câmera gira e Júpiter fica centrado quando a foto vale; a janela é do tamanho do disco de Júpiter; os marcos da sequência seguram até duas fotos), `ImagingFrameSynthTests` (o ruído cai com a raiz do número de fotos: 0,42 → 0,19 com cinco), `PioneerAutoListenTests` (primeira rodada aberta na hora e sem E; zonas cada vez menores e sinal cada vez mais fraco; a próxima rodada abre sozinha; as quatro rodadas só com setas; ajuda depois de 15 s parado; "intensidade" e sem E no prompt; formas da onda; a câmera olha para cima e o chão fica fora do quadro) |
| Quadros (palco 3D real + painel de UI Toolkit por cima) | `foto_*.png`, `juncao_*.png`, `radar_*.png`, gerados por `PioneerMinigameCapture` |
| `python Tools/validate_pioneer_data.py` | Conforme (156 chaves) |

## O que mudou

**A foto (cena 4).** A câmera fica dentro da sonda (o modelo some) e gira; Júpiter cruza uma mira uma vez por volta. Um toque de E com Júpiter dentro do anel registra a foto; fora da janela só aparece um aviso, sem punição, e depois de 12 s sem foto a janela alarga. São cinco fotos, cada uma uma imagem completa e ruidosa de Júpiter. Terminadas, elas se juntam sozinhas (sem E): a tela soma uma foto de cada vez, o ruído (medidor) cai e as duas cores viram uma imagem (o verde fica de fora: a legenda explica que foi estimado na Terra). A volta no jogo dura 9 s (a real era de ~12,5 s).

**O rádio (cena 8).** A primeira rodada abre junto com a cena e só as setas sintonizam; a escuta é automática dentro da zona. As quatro rodadas voltam: a zona vai de 30% a 20% e a 12% da escala e o sinal de 0,80 a 0,50 e a 0,28, e a próxima rodada abre sozinha 2,5 s depois do resultado. A onda virou um osciloscópio (tom limpo, tom em blocos para os dados, portadora fraca, só ruído). "Mais perto/mais longe do sinal" virou "Intensidade do sinal: aumentando/diminuindo". A câmera olha para cima, a antena se recorta contra as estrelas e o chão preto fica fora do quadro.

**Aldebaran.** Miolo de 14 m para 7 m e halo de 110 m para 52 m.

## Limites

- O ruído, as faixas e a Grande Mancha Vermelha da foto são desenhados; o ruído é de composição, não do instrumento.
- A volta de 9 s e a janela larga são de jogabilidade; o giro nominal real era de 12,5 s.
- Os textos novos e trocados (legendas 04.03 e 04.04, rótulos de intensidade) são `derived`, a revisar pela R7.
- A câmera giratória faz a cena inteira se mover; não há opção de movimento reduzido ainda.
- Não executado: Play Mode, teste humano e WebGL deste PR.
