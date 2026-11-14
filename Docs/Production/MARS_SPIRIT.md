# MAR-017 — Spirit em 45 s (M22)

`Prefabs/SpiritInterludeStage.prefab`, `Timelines/SpiritInterlude_M22.asset` (câmera da Opportunity + cues da tabela), `Timelines/SpiritInterlude_Cards.asset` (oito planos da câmera do Spirit) e `Art/SpiritArchive/`.

## Estrutura
Oito batidas: 0–5 Husband Hill, 5–11 Comanche, 11–17 sílica, 17–22 Home Plate, 22–27 odômetro de 7,73 km, 27–32 Troy e estação fixa, 32–39 inverno e inclinação ruim, 39–45 último contato (22 MAR 2010), busca encerrada (25 MAI 2011) e retorno à Opportunity (2018). Spirit em cima, Opportunity quieta embaixo; no fim o Spirit escurece e a Opportunity preenche a tela. Sem segunda campanha navegável: o Spirit nunca se move.

## Fontes
Cinco imagens oficiais NASA/JPL com crédito por cartão (`ASSET_CREDITS.md` P-MAR-04..08; originais, URLs e SHA-256 em `ArtSource/Mars/R-MAR-04/SOURCE.md`). Fatos: NASA Spirit (7,73 km, último contato 22/3/2010, fim das buscas 25/5/2011). Frio é causa provável, não falha interna observada; Home Plate guarda evidências de explosões antigas (sem erupção vista); a sílica é revelada pelo sulco da roda travada, por uma cobertura que desliza, nunca por rotação.

## Desvios e limitações
- Sem pacote Timeline: `SequenceDefinition` no lugar do `.playable`.
- O rover inclinado no inverno (8°) é ilustrativo, não uma medida; o odômetro é uma barra, não dado de trajeto.
- Sem WebGL nem perfil baixo real; leitura em 960×600 ainda não verificada por pessoa; revisão factual MS20–25 por outra função pendente.
- As imagens têm resolução limitada (Home Plate 512 px; Comanche 907 px) e são recortadas pela faixa de meia tela com pan.
