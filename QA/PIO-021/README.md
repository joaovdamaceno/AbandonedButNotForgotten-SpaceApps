# PIO-021 — Roteiro v2, fase 4, parte 1: o cinturão como cinema (cena 2)

Detalhes em `Assets/_Game/Chapters/Pioneer10/README.md` (seção "Roteiro v2, fase 4, parte 1").

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **198 de 198**. Novos (`PioneerBeltCinemaTests`): datas reais do contador (15 JUL 1972, dia 140 = 2 DEZ 1972, dia 215 = 15 FEV 1973) e aceleração monótona; campo de pedras e pedra solitária só nos anchors que os pedem (uma malha, 20 faces por pedra); a pedra cai no ponto do quadro onde o rótulo aponta (±0,02 do quadro); dolly zoom com a sonda do mesmo tamanho (±2%) de 10° a 30°; cinco planos, seis legendas e o evento `PIO.Belt.Reality`. A suíte completa não foi reexecutada |
| Quadros 3D (`belt-*.png`, câmera e anchors reais, sem UI) | `belt-tele-wall.png`: parede de pedras na teleobjetiva de 10°; `belt-dolly-end.png`: o campo se abre perto do fim do dolly zoom; `belt-nike.png`: céu vazio com uma pedra solitária (a UI do rótulo e do contador não é desenhada nestes quadros) |
| `python Tools/validate_pioneer_data.py` | Conforme (estados 40/54/27/74/40/47/39/52/38 = 411 s) |

**Limites.** O campo de pedras é de baixa resolução (icosferas deformadas, sombreamento facetado, sem textura). O contador usa datas reais nos extremos (15/7/1972 e 15/2/1973) e uma curva estilizada entre elas. O contador e o rótulo do 307 Nike foram verificados por testes de lógica e pelo layout do painel, não por quadros de UI nem no Play Mode. A teleobjetiva é de 10° porque o contrato de planos (`ShotDefinition`) aceita no mínimo 10°; o roteiro pedia uma lente mais fechada. A Lua e Marte (cena 1) ainda não estão no primeiro plano. Não executado: Play Mode, teste humano, WebGL deste PR, suíte completa.
