# PIO-033 — A placa de ouro como cutscene

Pedido do teste do responsável em Play Mode (15/11/2026, 04:49 do vídeo): sem "segure E" e sem minigame, só cutscene; uma placa de ouro com os desenhos gravados e um zoom moderado em cada parte explicada.

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **229 de 229** (a primeira reconstrução após criar um material novo falha em `RebuildingTheStageLeavesEveryFileIdentical` e converge na segunda, comportamento já conhecido). Novos (`PioneerPlaqueCinemaTests`): textura da placa dourada, determinística, com gravura em cada encaixe; a placa grande só aparece no anchor `PIO.Plaque.Board` e a visão geral a enquadra inteira; cada peça fica centrada e com 45–75% da altura do quadro quando a legenda abre; a câmera já saiu da peça anterior antes da próxima legenda |
| Quadros 3D (câmera e anchors reais) | `plaque-overview-3s.png` (placa inteira), `zoom-hydrogen-8s.png`, `zoom-pulsars-12s.png`, `zoom-people-24s.png`, `pullback-31s.png` |

**O que mudou.** A cena `PIO.Plaque` deixou de mostrar o painel de encaixe (peças, bandeja e barra de E ficam escondidos no modo cinema). No lugar, uma placa dourada 3 × 4,5 m (proporção 2:3 do quadro de 300 × 450 da arte) com as cinco gravuras em bronze escuro (hidrogênio, pulsares, sistema solar, escala da Pioneer, pessoas), desenhadas pela mesma lista de formas do painel. A câmera mostra a placa inteira, aproxima-se da primeira peça e segue para cada uma no ritmo das legendas 05.02–05.06 (a câmera chega 0,2 s antes da legenda abrir e sai nos últimos 0,8 s dela), terminando com um recuo até a placa inteira. A duração segue em 33 s. As estrelas fracas da textura do céu caíram de 2.500 para 800 (menos quadradinhos no zoom).

**Limites.** As gravuras são os desenhos simplificados do projeto, não uma reprodução da placa real da NASA (as figuras humanas são silhuetas lisas); a textura é gerada, sem asset externo. A lógica de encaixe da `PioneerPlaqueActivity` continua nos testes, mas não aparece mais na cutscene. Não executado: Play Mode, teste humano e WebGL deste PR.
