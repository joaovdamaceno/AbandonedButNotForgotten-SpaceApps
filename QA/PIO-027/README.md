# PIO-027 — Roteiro v2, fase 5: barras de cinema e cor por grupo de luz

Detalhes em `Assets/_Game/Chapters/Pioneer10/README.md` (seção "Roteiro v2, fase 5").

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **223 de 223**. Novos (`PioneerCinemaLookTests`): as barras só nos sete estados de cinema e recolhidas na foto, na sintonia, fora de um estado e no memorial da despedida (a partir de 30 s); o deslize de 0,8 s sem passar do alvo nem andar com tempo negativo; barras de 7% (finas); luz quente (vermelho menos azul ≥ 0,25) em todos os planos da Terra, fria (≤ 0) nos planos do espaço profundo com os grupos de cruzeiro, sol distante, despedida e contraluz, Júpiter e a placa seguem quentes, e o antigo grupo noturno frio saiu; o capítulo tem o letterbox |
| Quadros (`letterbox-bars-with-stars.png`, UI Toolkit com o tema da UI-001, fundo transparente) | As duas barras pretas de 7% no alto e embaixo, com as seis estrelas da cena 9 livres delas (o fundo branco é a transparência no visualizador) |
| Quadros 3D (`earth-warm-launch-pad.png` e `Docs/Art/Pioneer10_Stage/*`, `Pioneer10_Launch/*`, `Pioneer10_Listening/*` regenerados) | O Atlas na plataforma e a antena da Deep Space Network com luz âmbar sobre o céu frio; a sonda e Marte no espaço com luz branco-azulada fria |
| `python Tools/validate_pioneer_data.py` | Conforme (nenhum dado de capítulo mudou) |

**Limites.** As barras não passam por uma curva de easing (o deslize é linear) e usam o tempo ativo do capítulo (parado na pausa). O lançamento (cena 1) e a placa têm barras mesmo com a demonstração dos geradores no meio; as cenas 4 e 8 (interações) ficam sem elas inteiras, inclusive nos trechos de cinema delas. A cor por grupo de luz mudou a luz de cruzeiro, então os quadros de Docs/Art do cinturão interativo (`Pioneer10_Belt`) e outros que não foram regenerados ficam com a luz antiga. A temperatura é um número por grupo (a cor da luz principal); não há correção de cor por cena nem pós-processamento (perfil Web leve). Não executado: Play Mode, teste humano, WebGL deste PR, suíte completa.
