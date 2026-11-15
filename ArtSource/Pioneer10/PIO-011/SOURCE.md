# Antena DSN de 70 m — modelo 3D (PIO-011)

Baixados em **15/11/2026**, com autorização do usuário, das páginas oficiais da NASA (NASA 3D Resources). A página do índice diz que "todos estes recursos são livres para baixar e usar" e remete às diretrizes de imagens e mídia da NASA (P-S18); as páginas individuais não trazem licença própria. Crédito: NASA, sem sugerir endosso e sem logotipo.

| Arquivo | Bytes | SHA-256 | Página | Crédito da página | Uso |
| --- | ---: | --- | --- | --- | --- |
| `original/Deep Space Network 70-meter.glb` | 1.867.668 (1,78 MB, 26/3/2025) | `72dddfd955f5c212a40f368556ab4c13ab59a109bc1b46855b09c8549b18aa5c` | https://science.nasa.gov/3d-resources/deep-space-network-70-meter | NASA/Brian E. Kumanchik; NASA/Christian A. Lopez | **Usado** (convertido) |
| `original/70 meter dish.glb` | 2.216.584 (2,11 MB, 21/2/2025) | `36ff56a7a2bfd1c278f6f4774d32128d5931f2c22fe58241d00ee7d1815634bb` | https://science.nasa.gov/3d-resources/70-meter-dish | NASA/Ames Research Center | Preservado, **não usado** |

## Por que o primeiro

Inspecionados lado a lado em 15/11/2026 (decodificando o glTF):

- **Deep Space Network 70-meter**: 15 nós com a hierarquia mecânica real (`_root` › `azimuth` › `elevation` › prato, alimentador `collector`, quadripé `quad`, treliças, e a `base`), 33.112 triângulos, 3 texturas (1024, 1024, 512). Draco e WebP. Permite posar a antena (azimute e elevação). Tem 72 m de altura e 70 m de prato, com o chão em y = 0.
- **70 meter dish**: um só nó, 14.852 triângulos, sem articulações, textura única JPEG, eixos Z para cima e 91 m de envergadura (inclui terreno ou base). Não permite posar a antena.

## Alterações (original → derivado)

Ferramenta: `Tools/convert_dsn70.py` (determinística; sem Blender, pois o GLB usa Draco). Saída em `Data/Assets/Dsn70.json` (manifesto) e `Data/Assets/Dsn70.geometry.json`; atlas em `Assets/_Game/Chapters/Pioneer10/Art/Dsn/Textures/DsnAtlas.png`.

1. Decodificação do Draco; geometria preservada integralmente (33.112 triângulos), sem decimação nem edição. Os dois nós auxiliares sem material (`_root` e `elevation`, caixas de 12 triângulos) viram só pivôs.
2. Eixos: glTF (mão direita) → Unity (mão esquerda), Z invertido e ordem dos vértices invertida; metros, sem mudança de escala. A redução à escala do palco (13/35) é feita no prefab, não na geometria.
3. Materiais: 5 → 1 (URP/Lit, metalicidade 0, rugosidade 0,7 (o original usa 0,5), dupla face, como no original). As duas cores chapadas (fator 0,8) viram uma janela cinza. As texturas viram um atlas de 1024²: duas de 1024 foram reduzidas a 512, a de 512 a 256.
4. A `base` repete a textura 4 vezes ao redor (u de 0 a 4); como um atlas não repete, as quatro repetições ficam lado a lado numa janela própria, usando só a metade de cima da textura (v de 0 a 0,5), que é a parte que o UV original usa. Os 4 vértices do `azimuth` com UV 0,0004 fora de [0, 1] foram presos à borda.
5. A pose (azimute de 90°, elevação a 40° do zênite, para o prato voltar à câmera do anchor) é decisão de cena do `PioneerListeningBuilder`, não dado histórico.
