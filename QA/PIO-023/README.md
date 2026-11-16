# PIO-023 — Roteiro v2, fase 4, parte 3: Júpiter nasce de um ponto e a Terra pequena (cena 3)

Detalhes em `Assets/_Game/Chapters/Pioneer10/README.md` (seção "Roteiro v2, fase 4, parte 3").

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **204 de 204**. Novos (`PioneerJupiterRevealTests`): Júpiter cresce de um ponto (fração 0,015) até o tamanho do palco em 9 s numa taxa constante para o olho (mesma razão por segundo) e fica cheio depois; o driver liga a Terra só no plano aberto (9,5–19 s), a faz andar até parar e `Hide` devolve tudo; a razão Júpiter/Terra no palco é 11,2; a Terra cai no ponto do quadro previsto (±0,02), longe de Júpiter, com 0,5° a 3° de raio, e entra por fora do quadro; Júpiter ocupa 70% ou mais da altura do quadro; a câmera fica a menos de 15 m da sonda; plano de crescimento de 70° a 36° |
| Quadros 3D (`jupiter-*.png`, câmera e anchors reais, sem UI) | `jupiter-point-0.3s.png` (ponto de luz, sonda em silhueta), `jupiter-growing-6s.png`, `jupiter-earth-14s.png` (Júpiter com ~73% da altura e a Terra pequena à esquerda) |
| `python Tools/validate_pioneer_data.py` | Conforme (nenhum dado de capítulo mudou) |

**Limites.** A Terra é estilizada (continentes e nuvens procedurais, não reais). O crescimento é do próprio planeta (escala), não uma viagem real por distâncias. Faltam a Grande Mancha Vermelha cruzando, o brilho no limbo e o terminador com sombra marcada (a textura é a da NASA já usada, sem movimento de bandas). A duração da cena segue 27 s (o roteiro pede 30 s). Não executado: Play Mode, teste humano, WebGL deste PR, suíte completa.
