# PIO-022 — Roteiro v2, fase 4, parte 2: Marte e a Lua em 3D

Detalhes em `Assets/_Game/Chapters/Pioneer10/README.md` (seção "Roteiro v2, fase 4, parte 2").

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **200 de 200**. Novos em `PioneerBeltCinemaTests`: Marte só no anchor `PIO.Belt.Mars`, à direita da sonda (viewport 0,62–0,95), com raio angular de 3° a 10°, sem cobrir a sonda e com a face voltada para a câmera iluminada pela luz de cruzeiro; texturas da Lua (cinza neutro, com relevo) e de Marte (ocre: vermelho > verde > azul) determinísticas; o primeiro plano do cinturão usa o rig de Marte |
| Quadros 3D | `mars-opening-1s.png` e `mars-opening-7s.png` (abertura do cinturão, câmera e anchors reais, sem UI); `moon-launch-diagram.png` (a Lua do gráfico Terra–Lua com mares e crateras); `Docs/Art/Pioneer10_Launch/*` regenerados |
| `python Tools/validate_pioneer_data.py` | Conforme (nenhum dado de capítulo mudou) |

**Limites.** As superfícies são procedurais e estilizadas (mares e crateras aleatórios; ocre com regiões escuras e calotas), não são mapas reais da Lua nem de Marte. Marte está a uma distância e com um tamanho artísticos (a sonda não passa perto dele). A Terra do gráfico e o gráfico em si continuam estilizados. **Não feito** (do roteiro, cena 1): o contador "11 horas depois" e um sobrevoo da Lua em 3D, que exigem alongar o lançamento e portanto a retemporização. Não executado: Play Mode, teste humano, WebGL deste PR, suíte completa.
