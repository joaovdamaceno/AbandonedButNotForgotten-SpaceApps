# Pioneer 10 — modelo 3D (PIO-002)

| Campo | Valor |
| --- | --- |
| Arquivo | `original/Pioneer 10.glb` (2.049.840 bytes, não modificado) |
| SHA-256 | `81443a7fa01cca0dda878cd27692b3307f9c332c4eb8e55a844499d7b350573c` |
| Referência | P-S17 — NASA 3D Resources · Pioneer 10: https://science.nasa.gov/3d-resources/pioneer/ (crédito da página: NASA/JPL/Eyes on the Solar System) |
| Recebido | 14/11/2026, fornecido pela equipe (pasta Downloads da pessoa responsável pela tarefa) |
| Origem exata | **Verificada em 14/11/2026.** O arquivo é byte a byte o `Pioneer 10.glb` do espelho oficial da NASA https://github.com/nasa/NASA-3D-Resources/tree/master/3D%20Models/Pioneer%2010: mesmo tamanho (2.049.840 bytes) e mesmo SHA-1 de blob Git `62d2608617424e7b1d6d4d45bae13024cd9781c8` (`sha1("blob <bytes>\0" + conteúdo)`), conferido pela API de conteúdo do GitHub (`gh api repos/nasa/NASA-3D-Resources/contents/3D%20Models/Pioneer%2010`), que devolve só metadados: nada foi baixado. O `asset.generator` é *Khronos glTF Blender I/O v4.2.57*. O teste `TheOriginalIsByteIdenticalToTheOfficialNasaMirrorByItsGitBlobHash` refaz a conferência offline |
| Licença | Sem licença própria na página do item. Valem as diretrizes de imagens e mídia da NASA (P-S18): reconhecer a NASA como fonte, sem sugerir endosso; logotipos e insígnias não são de domínio público. Conferir marcas de terceiros nas texturas (nenhuma identificada: texturas são mapas de oclusão ambiente e rampas de cor) |
| Formato | glTF 2.0 binário, 11 nós/malhas, 11 materiais, 8 texturas (PNG 1024/256, com variante WebP), **Draco** (`KHR_draco_mesh_compression`) e `EXT_texture_webp` |
| Triângulos | 8.189 (meta do LOD0: ≤ 60 mil) |

## Medida de referência conferida (IMPORT_RULES §3)

Refletor da antena de alto ganho: **2,740 m** de diâmetro no eixo X (P-S05). No eixo Y a casca mede 2,828 m (3% a mais; pode ser o aro ou ferragens). Profundidade do refletor: 0,562 m.

## Alterações (original → derivado)

Ferramenta: `Tools/convert_pioneer10.py` (determinística; sem Blender, pois o GLB usa Draco).

1. Decodificação do Draco; nenhuma geometria foi decimada nem editada (8.187 dos 8.189 triângulos preservados).
2. Descartado o nó `Foil_trans` (2 triângulos): quadrilátero auxiliar sem textura (`norm_trans`), usado só como **posição e orientação da placa**, que o modelo não traz.
3. Separação em 6 peças pelo que o roteiro anima: `Bus` (corpo estático), `Dish`, `Feed`, `RtgBoomA`, `RtgBoomB`, `MagnetometerBoom` (regras em `classify`).
4. Eixos: glTF (mão direita) → Unity (mão esquerda), Z invertido e ordem dos vértices invertida; modelo recentrado no eixo de spin, com z = 0 no vértice do refletor; refletor abre para +Z.
5. 11 materiais → **1**: as texturas foram empacotadas num atlas 2048² (`PioneerAtlas.png`); três mapas de 1024 (refletor, demais peças e RTGs) ficaram inteiros, dois (`Pioneer_tex_01` e `Pioneer_tex_02`) foram reduzidos de 1024 para 512, as três texturas de 256 ficaram como estavam e a cor prateada chapada virou uma janela de 256.
6. Materiais convertidos para URP/Lit (dupla face, como no original). Metalicidade 0 e rugosidade 0,5 no original; desde 15/11/2026 o material do jogo usa metalicidade 0,15 e brilho 0,6 (mais reflexo nas bordas), e o atlas passa a ter filtro trilinear, anisotropia 8 e compressão WebGL de qualidade máxima. O modelo completo fica até a sonda ocupar 3% da altura da tela (antes, 12%).

A saída está em `Data/Assets/Pioneer10.json` (manifesto) e `Data/Assets/Pioneer10.geometry.json` (geometria). O prefab é gerado por `Editor/PioneerProbeBuilder.cs`.
