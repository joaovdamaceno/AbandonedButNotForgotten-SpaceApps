# MAR-013 (revisada) — Abertura: Marte, laboratório e globo (M01–M03, 34,8 s)

Abertura em inglês: **M01** (6,3 s) Marte no espaço com o título "2003"; **M02** (20,8 s) laboratório com os dois rovers lado a lado ("EARTH · MISSION PREPARATION"), a câmera girando devagar em volta da Opportunity ("THIS ROVER IS OPPORTUNITY") e a hematita em Meridiani Planum ("WHY MERIDIANI PLANUM?"); **M03** (7,7 s) globo que começa em Meridiani Planum, marcada, e gira até a Cratera Gusev, marcada ao chegar. Os nomes dos dois locais ficam presos aos marcadores. Não há lançamentos.

Depois do globo, o capítulo segue na cena `Opportunity` (pouso das duas sondas, exploração, ciência, escudo térmico, 2018, tempestade e fim). A cena é carregada em segundo plano durante o laboratório e entra no último quadro do globo, sem tela preta (`Opportunity/Bridge/MarsFieldHandoff.cs`).

- Planeta e globo usam o modelo NASA "Planet Mars" reamostrado (`Textures/Mars_Equirect.jpg`, ver `ArtSource/Mars/R-MAR-05/SOURCE.md`).
- O fundo do laboratório é o modelo `Opportunity/Models/Generated/PreparationLabV12.glb` com os materiais `Opportunity/Materials/Laboratory • *.mat`.
- Falas: `MAR.C01`–`C05` em `Data/MarsSubtitles.json`; títulos `MAR.ORI.*` em `Art/Origin/MarsOriginTexts.json`.
- O campo `pt` espelha o inglês (pedido do responsável); sem tradução.

## Limitações
- A textura equirretangular é calibrada a olho; os marcadores usam lat/lon da NASA GISS Mars24 e não medem posição.
- Verificado no Editor (Unity 6000.6.8f1; o projeto fixa 6.7f1). Sem medição em WebGL nem em perfil baixo real.
