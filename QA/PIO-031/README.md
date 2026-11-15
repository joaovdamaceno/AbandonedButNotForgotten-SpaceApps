# PIO-031 — Qualidade visual: céu, Aldebaran e a Terra da NASA

Pedidos do teste do responsável em Play Mode (15/11/2026): um fundo mais parecido com o espaço, Aldebaran bem mais visível que as outras estrelas e a Terra da NASA nas cutscenes.

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **223 de 223** (os testes existentes de palco, despedida e Júpiter cobrem os anchors, as sequências e a reconstrução idêntica; o anchor `PIO.Farewell.Away` entrou na lista de rigs da despedida) |
| Quadros (câmera e anchors reais, sem UI) | `aldebaran-farewell-29s.png` (Aldebaran grande, laranja, com halo, no alto à esquerda; sonda a 16 m, sem colar a câmera; céu escuro com a Via Láctea), `earth-nasa-launch-diagram.png` (a Terra da NASA com as Américas no gráfico Terra–Lua do lançamento), `earth-nasa-jupiter-comparison.png` (a Terra pequena ao lado de Júpiter, com o mesmo mapa) |
| `python Tools/validate_pioneer_data.py` | Conforme (o teto de aprovação passou de 420 s para 600 s e o texto de exclusão cita as duas interações) |

**O que mudou.** Estrelas redondas (discos de 10 lados, não quadrados); céu com Via Láctea mais estruturada (faixas escuras de poeira, núcleo mais quente, bordas azuladas), 9.000 estrelas fracas mais nítidas e quase metade delas agrupada perto da faixa, base mais escura. Aldebaran: miolo liso de 14 m e halo aditivo de 110 m a 1.100 m da câmera; antes só aparecia em 6 dos 10 s do afastamento; agora fica visível durante todo o afastamento e o memorial (novo anchor `PIO.Farewell.Away`, a 16 m da sonda, com Aldebaran no alto à esquerda por construção). O plano aberto da despedida passou de 9,6 m para ~16 m da sonda. A Terra procedural saiu e entrou o mapa Blue Marble da NASA (`ArtSource/Pioneer10/PIO-031/`).

**Limites.** As estrelas do céu ainda aparecem levemente suavizadas quando o campo de visão é estreito (o céu é uma textura de 2048×1024). A Terra do gráfico é um mapa sem nuvens nem noite (Blue Marble). O halo de Aldebaran é uma cor fixa (a estrela é vermelho-alaranjada, mas não há espectro). A direção de Aldebaran no palco continua sendo geral, não calculada. Não executado: Play Mode, teste humano, WebGL deste PR.
