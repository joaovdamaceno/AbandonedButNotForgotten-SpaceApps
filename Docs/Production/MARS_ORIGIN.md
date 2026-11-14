# MAR-013 (revisada) — Abertura: Marte, laboratório, mapas orbitais e globo (M01–M03, 40,8 s)

Segue a abertura do jogo anterior, em inglês: **M01** (6,3 s) Marte no espaço com o título "2003"; **M02** (28,5 s) laboratório com os dois rovers ("EARTH · MISSION PREPARATION"), mapa orbital de Meridiani/hematita ("WHY MERIDIANI PLANUM?") e o mesmo tratamento para a Spirit e Gusev ("WHY GUSEV CRATER?", hipótese de lago antigo, não certeza); **M03** (6 s) globo com os dois pousos e datas (Spirit · Gusev · 4 JAN 2004 UTC; Opportunity · Meridiani · 25 JAN 2004 UTC). Os lançamentos foram removidos (a abertura anterior não os tinha).

- Planeta e globo usam o modelo NASA "Planet Mars" reamostrado (`Textures/Mars_Equirect.jpg`, ver `ArtSource/Mars/R-MAR-05/SOURCE.md`). Mapas orbitais com crédito: Meridiani (NASA/JPL-Caltech/Arizona State University) e Gusev (PIA05346).
- `MarsOriginDirector.Overlay` expõe imagem, chave de crédito e opacidade do mapa orbital; **a UI ainda precisa desenhar esse painel** (pendente, ver limitações).
- Falas: `MAR.C01`–`C05` em `Data/MarsSubtitles.json`; títulos/créditos `MAR.ORI.*` em `Art/Origin/MarsOriginTexts.json`; datas em `MAR.L01/L02` (`MarsLandingTexts.json`).
- O campo `pt` espelha o inglês (pedido do responsável); sem tradução.

## Limitações
- O painel do mapa orbital não é desenhado por nenhuma UI ainda; o laboratório é o procedural da MAR-013 (o laboratório detalhado da edição anterior não foi portado: 384 mil triângulos, acima do orçamento Web).
- A textura equirretangular é calibrada a olho; os marcadores usam lat/lon da NASA GISS Mars24 e não medem posição.
- Sem verificação visual em GPU nesta entrega além dos testes de comportamento.
