# Pioneer · ajustes da Lua, da sonda, do Sistema Solar e do cinturão (15/11/2026)

Base: `develop` em `d6df6fb`. Capturas feitas em Play Mode na cena `Pioneer10_Playtest` (Editor, câmera do jogo).

| Pedido | O que mudou | Evidência |
| --- | --- | --- |
| Linha mais suave e bolinha seguindo a linha (cena em que a sonda sai da Lua) | A reta depois do último ponto sai pela tangente da curva (antes saía radial e fazia um canto); a linha tem 161 pontos com pontas e cantos arredondados; a bolinha tem 49 marcas pela mesma função da linha (antes 9, em corda reta, e chegava a 1,15 m da linha). | `lua-linha-suave-terra-iluminada.png`; teste `TheTraceBallStaysOnTheDrawnLineAllTheWayAndTheLineHasNoCorner` (desvio < 0,15 m; ângulo < 3° na emenda) |
| Modelos melhores e melhor misturados ao ambiente | Terra (mapa NASA) e Lua com material com luz e brilho de base 0,7 (claro-escuro da luz de cruzeiro, sem parecer figurinha colada). `RealtimeEmissive` em vez de `None`: com `None` o URP apaga a palavra-chave `_EMISSION` ao reimportar. | idem |
| Melhorar a qualidade da sonda | Modelo completo (8 mil triângulos) até a sonda ocupar 3% da altura da tela (era 12%); atlas com filtro trilinear, anisotropia 8 e compressão WebGL de qualidade máxima; material com metalicidade 0,15 e brilho 0,6 (era 0 e 0,5). | `sonda-modelo-completo.png`; `ArtSource/Pioneer10/PIO-002/SOURCE.md` |
| Sonda menor no Sistema Solar | `ProbeScaleFactor` de 4 para 1,5; o marcador dourado continua destacando. | `sistema-solar-sonda-menor.png` |
| Começar mais longe do campo de asteroides (cena em que a sonda passa no meio) | O ponto de vista começa a 640 m do centro (fora do campo, que vai até 520 m) e vai até 55 m com a mesma curva exponencial do contador de dias. Distância mínima da câmera ao centro de qualquer pedra: 86 m (pedras de até 30 m de raio). | `cinturao-pov-inicio-longe.png`; teste `TheDaysShotIsThePointOfViewInsideTheBeltAndEveryRockStaysClear` |

## Testes
EditMode, classes da Pioneer: Launch, BeltCinema, Belt, Probe, ProbeCameraDistance, Flow, CinemaLook, Outbound, PlaytestAdjustments, SpinShot, Data, Narration, Plaque, PlaqueCinema, ClosestCinema, Farewell (168) e Stage (11, isolada), todas passam.

## Limitações
- Não revi em build Web nem medi custo; o modelo completo da sonda por mais tempo e a textura em qualidade máxima custam um pouco mais de GPU/memória.
- A captura do início do POV do cinturão é de ~1,6 s dentro do plano; de longe o campo ainda mostra rochas grandes em primeiro plano (as mais próximas ficam a ~100 m e têm até 30 m de raio). Se quiser mais "enxame de longe", dá para reduzir o raio máximo ou o início.
- `PIO_EarthCompare` (Terra ao lado de Júpiter) usa `globalIlluminationFlags = None` e perde o brilho de base ao reimportar; não mexi, para não alterar outra cena.

## Marte (pedido posterior)
O planeta Marte da Pioneer (anchor `PIO.Belt.Mars` e mapa do Sistema Solar 3D) passa a usar a mesma textura NASA do Marte da abertura do capítulo de Marte (`Mars/Art/Origin/Textures/Mars_Equirect.jpg`, modelo "Planet Mars" da NASA reamostrado; origem e licença em `ArtSource/Mars/R-MAR-05`). A textura é referenciada, não copiada (não pesa duas vezes no build); a textura procedural `PIO_MarsSurface.png` foi removida. Captura em Play: `marte-nasa-cinturao.png`. Teste: `PioneerStageTests` isolada passa (11); em corrida junto de outras classes, `RebuildingTheStageLeavesEveryFileIdentical` pode acusar `PIO_Mars.mat` (a mesma dependência de ordem já registrada para `PIO_EarthCompare.mat`; isolada passa).
