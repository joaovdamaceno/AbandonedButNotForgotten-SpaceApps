# PIO-034 — Câmeras mais abertas atrás da sonda e a Terra com nome

Pedidos do teste do responsável em Play Mode (15/11/2026): (1) nas vistas por trás da sonda, câmeras mais cinematográficas e sem chegar muito perto, porque o modelo tem poucos polígonos; (2) na cena de Júpiter (01:56) a Terra aparecia "do nada".

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **234 de 234**. Novos: `PioneerProbeCameraDistanceTests` (os anchors `PIO.Launch.Reveal` e `PIO.Launch` ficam a pelo menos 9 m da sonda e cada quadro dos planos PIO01.A e PIO01.D, a pelo menos 8,5 m); `PioneerJupiterRevealTests` (o rótulo "Terra" aparece só de 13,5 a 21,5 s, nunca fora da janela em que a Terra está na tela, acompanha a Terra e o texto existe em PT e EN) |
| Quadros 3D (câmera e anchors reais) | `launch-free-probe-12m.png` (a sonda livre a ~12,5 m, antes a ~8 m), `launch-reveal-11m.png` (a abertura em contraluz a ~11 m, antes a ~6,5 m), `jupiter-earth-3d-18s.png` (a Terra ao lado de Júpiter, sem o HUD) |
| Quadro do HUD (UI Toolkit, fundo transparente) | `hud-earth-label-18s.png` (o rótulo "Terra" com uma linha fina sobre a Terra, posição conferida contra a projeção da Terra no plano aberto) |
| `python Tools/validate_pioneer_data.py` | Conforme (149 chaves; nova `PIO.UI.earth_label`, `derived`) |

**O que mudou.** Os dois planos livres da abertura (`PIO01.A`, o contorno do refletor, e `PIO01.D`, a sonda livre com os RTGs) agora ficam a ~11 m e ~12,5 m da sonda, com campo de visão mais fechado (32° e 34°) e um movimento lento de câmera, em vez de ~6,5 m e ~8 m. Na cena de Júpiter, o rótulo "Terra" com uma linha fina acompanha a Terra de comparação enquanto ela chega pela esquerda e para ao lado do planeta, e a Terra ganhou um brilho de base (emissão) para o lado sem Sol continuar legível.

**Limites.** Só os planos livres do lançamento foram afastados; os demais planos por trás da sonda (a despedida, afastada na PIO-031, e a placa) já estavam abertos. O brilho dos RTGs continua feito com esferas aditivas simples. Não executado: Play Mode, teste humano e WebGL deste PR.
