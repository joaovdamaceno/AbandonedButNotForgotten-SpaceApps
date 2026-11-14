# ART-001 — Auditoria de modelos e referências

Dono: R4. Revisão: R1 (integração) e R7 (ciência/direitos). Páginas consultadas em **14/11/2026**. **Nenhum modelo, textura ou foto de missão foi baixado ou importado nesta tarefa**; “página verificada” significa só que a página foi lida e os dados abaixo vieram dela. Regras de importação: [IMPORT_RULES.md](IMPORT_RULES.md).

Estados usados: **Página verificada** (lida, não baixada) · **Pendente — equipe** (arquivo prometido pela equipe, não recebido) · **Não localizado** (sem fonte oficial encontrada até agora) · **Provisório** (fallback da equipe) · **Importado** (no repositório, com hash).

## Direitos: o que as páginas dizem

- **Catálogo NASA 3D Resources** (https://science.nasa.gov/3d-resources/, atualizado em 30/10/2026): “All of these assets are free to download and use.”; espelhado em https://github.com/nasa/NASA-3D-Resources; remete às diretrizes de imagens e mídia.
- **Diretrizes NASA de imagens e mídia** (https://www.nasa.gov/nasa-brand-center/images-and-media/, atualizado em 27/10/2026), em resumo: o material NASA em geral não está sujeito a copyright nos EUA, inclusive texturas e polígonos usados em modelos 3D; uso educativo e em simulações gráficas/páginas web é permitido; **a NASA deve ser reconhecida como fonte**; insígnia, logotipo e identificadores **não** são de domínio público e seu uso é protegido por lei; não pode haver sugestão de endosso; material de terceiros exibido em sites NASA é marcado com o titular e exige contato com ele; uso comercial de pessoa identificável pode exigir permissão.
- **As páginas individuais dos modelos não trazem licença própria.** Portanto não presumimos licença além das diretrizes acima, registramos a linha de crédito exata de cada página e verificamos marcas de terceiros dentro dos arquivos (texturas com logotipos, por exemplo) no momento da importação.
- Fotografias com copyright de terceiros (ex.: Smithsonian S3-13) **não** são reutilizáveis sem permissão específica.

## Modelos principais

### A-MER · Spirit/Opportunity (rover compartilhado pelas gêmeas)

| Campo | Valor |
| --- | --- |
| Página | https://science.nasa.gov/resource/spirit-and-opportunity-rover-3d-model/ — “Spirit and Opportunity Rover, 3D Model”, publicada 27/04/2019, atualizada 07/11/2024 |
| Crédito na página | **NASA/JPL-Caltech** |
| Arquivos anunciados | GLB `24883_MER_static.glb` **11,36 MB**; USDZ 3,01 MB (ambos de 08/03/2024) |
| Observação | O nome “static” sugere modelo sem peças articuladas; separar rodas/braço/mastro/antena é trabalho de MAR-011. 11,36 MB excede o orçamento: decimar e reempacotar texturas. |
| Estado | **Página verificada · Não usado** — em 14/11/2026 a MAR-011 substituiu o GLB oficial por um modelo próprio riggado (G-MAR-02 em `ASSET_CREDITS.md`, `Assets/_Game/Chapters/Mars/Art/Rover/RIG.md`). O GLB da NASA não foi baixado |
| Responsável / prioridade / tarefa | R4 · P0 · MAR-011 |

Componentes reconhecíveis que o modelo final **e** o proxy precisam ter (conferir cada item no GLB e em foto da missão):
- Mastro **PMA** com cabeça de câmeras (Pancam, Navcam) e espelho do Mini-TES; câmeras a ~1,5 m do solo (página NASA Opportunity).
- Braço **IDD** com a torre de ferramentas: RAT, APXS, Mössbauer (MB), Microscopic Imager (MI) (página NASA Opportunity; uso em M09–M19).
- Painéis solares do convés (fonte de energia dos rovers; não usar RTG/bateria genérica).
- Seis rodas com suspensão *rocker-bogie* e rodas de esterço; antena de alto ganho (prato com gimbal) e de baixo ganho (mastro) — **conferir posição exata no GLB e em foto**, pois a antena é animada em M29.
- Escala de referência: ~185 kg, “do tamanho de um carrinho de golfe” (página NASA Opportunity).

Fallback P0 (proxy): caixa do corpo eletrônico com convés plano largo, três rodas por lado com balancins visíveis, mastro vertical com cabeça retangular de duas “lentes”, braço de três segmentos dobrado à frente com torre de quatro ferramentas, prato de antena. ~8–12 mil triângulos, 1 material cinza/dourado, mesmos nomes de nós de IMPORT_RULES §4. Custo de troca: substituir malhas no prefab, sem mudar scripts (~1–2 h com o GLB em mãos).

### A-MER-EDL · Módulo de pouso, airbags, escudo, carenagem, paraquedas

| Campo | Valor |
| --- | --- |
| Fonte de referência | Sequência descrita em MS08 (NASA Spirit: In Depth); escudo após o pouso: PIA07402 e PIA07324 (MS15, MS16) |
| Modelo oficial | **Não localizado** na primeira página do catálogo NASA 3D (25 páginas; as demais não foram lidas). Procurar nas páginas 2–25 e no GitHub antes de modelar. |
| Estado | **Não localizado** |
| Responsável / prioridade / tarefa | R4 · P0 · MAR-014 (pouso split) e MAR-015 (campo do escudo) |

Componentes: base tetraédrica com **três pétalas** que abrem; aglomerados de airbags envolvendo o módulo fechado; carenagem traseira (*backshell*) com paraquedas; escudo térmico (que depois aparece invertido e amassado, com fragmento lateral, em 2005). Fallback P0: tetraedro low-poly com pétalas articuladas; airbags como esferas agrupadas com deformação por escala; escudo como calota com borda irregular apoiada em três pontos. Nada desaparece entre planos.

### A-PIO · Pioneer 10

| Campo | Valor |
| --- | --- |
| Página | https://science.nasa.gov/3d-resources/pioneer/ — “Pioneer 10”, atualizada 24/06/2025 |
| Crédito na página | Fonte: **NASA/JPL/Eyes on the Solar System** |
| Arquivo anunciado | `Pioneer 10.glb (Original)` **1,95 MB**, 14/04/2025; espelho https://github.com/nasa/NASA-3D-Resources/tree/master/3D%20Models/Pioneer%2010 |
| Estado | **Recebido da equipe e incorporado (PIO-002)** · SHA-256 `81443a7fa01cca0dda878cd27692b3307f9c332c4eb8e55a844499d7b350573c` · derivado em `Assets/_Game/Chapters/Pioneer10/Art/` · origem **verificada** em 14/11/2026 (tamanho e SHA-1 de blob Git `62d2608617424e7b1d6d4d45bae13024cd9781c8` idênticos ao espelho oficial nasa/NASA-3D-Resources) |
| Responsável / prioridade / tarefa | R5 · P0 · PIO-002 |

Componentes reconhecíveis: refletor de alto ganho de **2,74 m** (P-S05) com eixo de spin paralelo ao eixo da antena; compartimento de equipamentos atrás do prato; RTGs em hastes (página NASA Pioneer 10: “two SNAP-19 radioisotope thermal generators”, ~140 W no encontro com Júpiter — o roteiro **não** exibe a contagem; o artista segue a geometria oficial, ver P-S sources §Divergências 4); haste do magnetômetro (instrumento HVM listado na página); fotopolarímetro IPP; **placa** presa aos suportes da antena (alumínio anodizado dourado, P-S12). Massa 258 kg (página).

Fallback P0: prato raso de 2,74 m, cilindro hexagonal achatado atrás, duas hastes com cilindros nas pontas, haste longa fina, retângulo dourado nos suportes. ≤ 6 mil triângulos, nomes `Spin_Root`/`Plaque`. Custo de troca: baixo (GLB pequeno, 1,95 MB).

### A-SUR · Surveyor 3

| Campo | Valor |
| --- | --- |
| Página da missão | https://science.nasa.gov/mission/surveyor-3/ (atualizada 02/11/2024) — massa de lançamento 997,9 kg (somente arquivo histórico, ver SUR sources) |
| Modelo oficial | **Não localizado** na primeira página do catálogo NASA 3D. Modelo derivado de Surveyor 1 só com correções explícitas (ASSET_CREDITS A-SUR) |
| Estado | **Não localizado** — fallback P0 é reconstrução própria por diagramas e fotos (03_SURVEYOR_3.md) |
| Responsável / prioridade / tarefa | R6 · P0 · SUR-001/SUR-002 |

Componentes reconhecíveis (página NASA Surveyor 3 + S3-03/S3-12): **câmera de TV** com lentes de 25 e 100 mm sob **espelho móvel** em azimute e elevação; **amostrador de superfície (pá)** em braço extensível — alcance até 1,5 m, escavação até 18 cm; três pernas com sapatas; antena plana e painel solar no mastro; compartimentos térmicos. Conferir no relatório S3-03 a geometria das pernas, mastro e altura total **antes** de modelar. Só a parte visível no POV da câmera precisa de detalhe (pata, pá, sombra); o núcleo pode ficar fora de quadro (roteiro). Orçamento ≤ 25 mil triângulos, 1 atlas 1024, materiais metálico/branco. Custo de troca se surgir modelo oficial: médio (o rig de espelho/pá precisa ser reaproveitado).

## Referências e recursos secundários

| ID | Recurso | Fonte identificada | Estado | Resp. · prioridade · tarefa | Fallback / limite |
| --- | --- | --- | --- | --- | --- |
| R-MAR-01 | Panorama de Eagle (“Lion King”) | NASA/JPL (roteiro §Assets) | Referência a localizar | R4 · P0 · MAR-012 | Terreno próprio guiado pela foto |
| R-MAR-02 | Escudo e Heat Shield Rock | PIA07402 (MS15), PIA07324 (MS16) | Legendas lidas em 14/11/2026; imagens não baixadas | R4 · P0 · MAR-015 | Modelo próprio procedural entregue (G-MAR-01); capturas de revisão em `Docs/Art/ShieldField_Captures/`. **Imagens reais importadas na MAR-006** (P-MAR-02 e P-MAR-03, com crédito e hash); a foto de M20 pode reutilizá-las. **Revisado (comparação com as imagens reais em `Docs/Art/ShieldField_Captures/08` e `09`):** destroço principal como cúpula convexa amassada com o lado rompido aberto e meteorito cinza-metálico claro, achatado e craterado. Ainda procedural e provisório; a arte final é da R4 (MAR-018/MAR-020) |
| R-MAR-03 | Mapa de hematita TES / globo Gusev–Meridiani | MS03 (NASA Meridiani Planum) | Página citada | R4 · P0 · MAR-013 | Globo low-cost com dois pontos e rótulos |
| R-MAR-04 | Céu da tempestade de 2018 | MS29 (stills, sem vídeo) | Página citada | R4 · P0 · MAR-016 | Fog + gradiente próprios |
| R-MAR-05 | Imagens de Spirit (Husband Hill, Comanche, Home Plate) | MS20–MS24 | Páginas citadas | R4 · P0 · MAR-017 | Cartões com foto creditada |
| R-MAR-06 | Silhueta Delta II | Fotos NASA de lançamento | Não localizado | R4 · P0 · MAR-013 | Silhueta 2D/low-poly ≤ 3 mil tri |
| R-PIO-01 | Atlas-Centaur do lançamento | Fotos/desenhos em P-S02 | **Provisório** (PIO-004, G-PIO-04): silhueta com medidas aproximadas; conferir com o desenho oficial | R5 · P0 · PIO-004 | Silhueta + quads de emissão |
| R-PIO-02 | Júpiter histórico da Pioneer | P-S10 | **Importado (PIO-007)**: montagem sequencial AC73-9341 da página oficial, SHA-256 `a4d9e5c428849cd4cd956d048e9f25f11f488ec23f98bfb97bd2fb40f8829e0b`; produto Pioneer 10 confirmado (não é Juno) | R7 · P0 · PIO-007 | Montagem de quadros, não foto única; revisão lado a lado da R7 pendente |
| R-PIO-03 | Ganimedes 1973 | P-S09 | **Importado (PIO-007)**: AC73-9279 da página oficial, SHA-256 `ddee488aadd1007a1162f6d979971add063111260f696d7140eda9c38e1f53fe` (636,08 KB como anunciado) | R7 · P0 · PIO-007 | Resolução histórica preservada, sem ampliação; revisão da R7 pendente |
| R-PIO-04 | Placa (contornos) | P-S11, P-S12 | **Adaptação educativa feita (PIO-009, G-PIO-06)**: `Art/PlaqueEducational.svg` e 5 PNGs, gerados de uma só lista de formas; linhas dos pulsares e régua esquemáticas | R5 · P0 · PIO-009 | Figuras humanas em silhueta lisa, “adaptação educativa”; revisão da R7 dos símbolos pendente |
| R-PIO-05 | Antena DSN | Catálogo NASA 3D tem “70 meter dish” (https://science.nasa.gov/3d-resources/70-meter-dish/) | **Candidato** — página do item não lida | R5 · P0 · PIO-011 | Silhueta 2D de prato a partir de foto creditada |
| R-PIO-06 | Campo de estrelas | Textura própria | **Gerado** (PIO-003, G-PIO-01 em ASSET_CREDITS) | R5 · P0 · PIO-003 | Uma textura de céu + uma malha de estrelas brilhantes (1 draw call), sem milhares de objetos |
| R-SUR-01 | Fotografia Apollo 12 + Surveyor 3 | S3-08 (crédito “NASA”, 20/11/1969) | Página citada, não baixada | R6 · P0 · SUR-013 | 1024–1600 px, original fora do build |
| R-SUR-02 | Pá/trincheira reais | Figuras S3-03 | A localizar | R6 · P0 · SUR-009 | Foto creditada na comparação |
| R-SUR-03 | Terreno da cratera (~200 m) | S3-03, S3-05; catálogo NASA 3D tem “Apollo 12 – Landing Site” | **Candidato** — conteúdo do item não verificado | R6 · P0 · SUR-002 | Terreno próprio 40×40 m + skyline |
| R-SUR-04 | LM Intrepid, botas, luva | Composição própria por fotos | Planejado | R6 · P0 · SUR-013 | Silhueta + botas/luva no terço inferior; sem modelo de jogo comercial |
| R-SUR-05 | Câmera retornada (museu) | S3-13 © Smithsonian | **Não usar** sem permissão | R6 · P2 · SUR-017 | Final P0 funciona sem ela |
| R-UI-01 | Fontes Outfit e IBM Plex Mono | Ver ASSET_CREDITS F-01/F-02 | **Importado** (UI-001) | R2 · P0 · UI-001 | — |

## Como verificar esta auditoria

- R1/R7 conferem que nenhum item está marcado como importado sem hash em `ASSET_CREDITS.md` (hoje, apenas as fontes).
- Ao receber cada arquivo, comparar silhueta e a lista de componentes acima com uma foto da missão no mesmo ângulo e anexar a captura ao PR da tarefa do capítulo.
- Itens **Candidato** e **Não localizado** exigem leitura da página específica do item antes de qualquer download.
