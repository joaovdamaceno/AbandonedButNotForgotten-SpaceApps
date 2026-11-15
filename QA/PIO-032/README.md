# PIO-032 — O cinturão como ele é: pedras pequenas e muito espaço vazio

Pedido do teste do responsável em Play Mode (15/11/2026, 01:02 do vídeo): as pedras estavam amontoadas e gigantes; a ideia é explicar que a chance de acertar uma pedra é mínima, com um zoom na sonda dentro do cinturão mostrando o quão vazio ele é e o quão longe a sonda passou da pedra.

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **225 de 225**. Novos (`PioneerBeltCinemaTests`): o cinturão tem 70 pedras, todas a mais de 90 m da sonda e a no máximo 520 m, a no mínimo 60 m umas das outras (sem amontoar), com no máximo 8 m de raio (pequenas), determinístico e nenhuma no caminho da câmera (folga de 80 m); o plano `PIO02.Days` só se aproxima e só fecha o zoom, a sonda só cresce no quadro, nenhuma pedra fica a menos de 60 m da câmera e termina a 70 m da sonda; a nota da distância aparece só de 25 a 30 s |
| Quadros 3D (`belt-*.png`, câmera e anchors reais) | `belt-wide-14s.png` (visão larga: pedras pequenas, separadas, com muito espaço entre elas), `belt-zoom-on-probe-29s.png` (zoom na sonda com o vazio em volta) |
| Quadros do HUD (`hud-*.png`, UI Toolkit, fundo transparente) | `hud-day-counter-and-note-28s.png` ("Dia 55 / 215 · 8 SET 1972" e a nota da distância), `hud-nike-label-35s.png` (o rótulo do 307 Nike). O contador agora vive no HUD de cinema, que é renderizado e conferido em imagem (antes ficava num painel que nunca foi renderizado) |
| `python Tools/validate_pioneer_data.py` | Conforme (148 chaves) |

**O que mudou.** A parede de 1.400 pedras vista de teleobjetiva sai; entram 70 pedras pequenas (2,5 a 8 m) bem separadas num volume ao redor da sonda. O plano de 8 a 19 s é uma visão larga do cinturão (de 650 m, aproximando-se devagar) e o de 19 a 30 s é um zoom na sonda (de ~530 m a 70 m, campo de visão de 38° a 26°) enquanto o contador de dias corre. Nos últimos 5 s do zoom, a nota "O asteroide conhecido mais próximo ficou a milhões de quilômetros da sonda" (`PIO.UI.belt_far`, `derived`; sem número porque a distância não foi conferida nas fontes do projeto). As estrelas fracas deixaram de ser borrões da textura do céu e viraram discos nítidos de geometria (3.000, 8 lados), redondos em qualquer zoom.

**Limites.** O tamanho das pedras e a distância entre elas são de composição, não de escala real (o cinturão real é imensamente mais vazio). Não há linha tracejada entre a sonda e a pedra, só a nota de texto e o rótulo do 307 Nike. Ainda sobram algumas estrelas fracas da textura do céu que aparecem como pequenos quadrados no zoom mais fechado. Não executado: Play Mode, teste humano, WebGL deste PR.
