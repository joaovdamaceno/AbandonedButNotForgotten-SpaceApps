# MER (Spirit/Opportunity) — rig, escala e anchors

MAR-011 · R4. Modelo **próprio da equipe**, riggado, gerado por código em `SourceAssets/Mars/MER/generator/` a partir de fotos de referência (ver `MarsAssetCredits.md`). As duas gêmeas usam o mesmo `MER_Rover.fbx`: `Prefabs/SpiritVisual.prefab` e `Prefabs/OpportunityVisual.prefab` são variantes dele, com malhas, materiais e texturas compartilhados.

## Convenções (Unity)

- 1 unidade = 1 m. Y para cima, **frente em +Z**, lado esquerdo do rover em **−X**.
- Raiz `MER_Rover` (no prefab: `SpiritVisual`/`OpportunityVisual`) com rotação 0 e escala 1, **no solo**, centrada entre os seis contatos das rodas.
- Todos os nós articulados têm **rotação local identidade em repouso**. Animar só a rotação local do nó, no eixo indicado; a geometria fica nos filhos `<Nó>_LOD0`/`<Nó>_LOD1` (não animar esses filhos).
- Sem animação importada, sem colliders, sem scripts no prefab: o comportamento pertence a R3 (MAR-002 em diante).

## Medidas auditadas

| Medida | Valor no modelo | Referência |
| --- | --- | --- |
| Altura da Pancam (`Anchor_Pancam`) | 1,468 m | “câmeras do mastro a cerca de 1,5 m” (IMPORT_RULES §3) |
| Diâmetro da roda | 0,26 m (largura 0,16 m) | MER: rodas de ~25 cm |
| Bitola (centro a centro) | 1,24 m | — |
| Entre-eixos dianteiro–traseiro | 1,28 m | — |
| Comprimento / largura com asas | 1,60 m / 2,24 m | “do tamanho de um carrinho de golfe” |
| Triângulos LOD0 / LOD1 | 50 564 / 19 338 | orçamento 80 mil / 30 mil (IMPORT_RULES §5) |
| Materiais / texturas | 2 (URP/Lit) / atlas 2048 + solar 1024 | ≤ 3 materiais; 1 atlas 2048 + 1024 |

## Nós articulados e eixos

Posições em metros, no espaço da raiz, na pose de repouso. Sinais na convenção da Unity (positivo em Y gira +Z para +X, isto é, para a direita; positivo em X inclina a frente para baixo).

| Nó | Pai | Pivô (x, y, z) | Eixo | Uso / sinal |
| --- | --- | --- | --- | --- |
| `Body` | raiz | (0,000; 0,440; 0,060) | — | Corpo, deck, asas, câmeras de risco, antenas fixas |
| `Rocker_L` / `Rocker_R` | Body | (∓0,420; 0,460; 0,090) | X | Pino do diferencial no ressalto do corpo. Diferencial: `Rocker_R = −Rocker_L` |
| `Bogie_L` / `Bogie_R` | Rocker | (∓0,460; 0,300; −0,310) | X | Pino do bogie |
| `Steer_FL` / `Steer_FR` | Rocker | (∓0,620; 0,315; 0,670) | Y | Esterço dianteiro; o eixo passa pelo centro da roda. **+ = vira para a direita** |
| `Steer_RL` / `Steer_RR` | Bogie | (∓0,620; 0,315; −0,610) | Y | Esterço traseiro. Curva coordenada: traseiras com sinal oposto às dianteiras |
| `Wheel_FL/FR`, `Wheel_RL/RR` | Steer | (∓0,620; 0,130; ±0,67/−0,61) | X | Rolagem. **+ = rola para a frente**; graus = distância ÷ 0,13 × 57,3 |
| `Wheel_ML` / `Wheel_MR` | Bogie | (∓0,620; 0,130; −0,060) | X | Rodas do meio (sem esterço) |
| `PMA_Mast` | Body | (0,030; 0,660; 0,550) | Y | Azimute do mastro (panorâmica). + = olha para a direita |
| `PMA_Head` | PMA_Mast | (0,030; 1,400; 0,550) | X | Elevação da cabeça de câmeras. + = olha para baixo |
| `HGA_Gimbal` | Body | (−0,340; 0,670; −0,230) | Y | Azimute da antena de alto ganho |
| `HGA_Dish` | HGA_Gimbal | (−0,410; 0,820; −0,230) | X | Elevação da antena. − = levanta o prato (M29) |
| `IDD_Shoulder` | Body | (0,240; 0,400; 0,575) | Y (azimute), Z (elevação) | Braço guardado sob a frente. Desdobrar: Y ≈ +80° na Unity (−80° no referencial do Blender, usado no clipe `RigDemo`) |
| `IDD_Elbow` | IDD_Shoulder | (−0,210; 0,400; 0,615) | Y | Desdobrar: Y ≈ −150° na Unity (+150° no Blender; com o ombro aberto, o antebraço aponta para a frente) |
| `IDD_Wrist` | IDD_Elbow | (0,070; 0,365; 0,660) | Y | Orienta a torre |
| `IDD_Turret` | IDD_Wrist | (0,030; 0,235; 0,685) | X | Troca de ferramenta: 90° por ferramenta |
| `Tool_RAT` | IDD_Turret | (0,030; 0,180; 0,685) | — | Em repouso aponta para −Y (para baixo) |
| `Tool_APXS` | IDD_Turret | (0,030; 0,235; 0,740) | — | Aponta para +Z |
| `Tool_MB` | IDD_Turret | (0,030; 0,290; 0,685) | — | Aponta para +Y |
| `Tool_MI` | IDD_Turret | (0,030; 0,235; 0,630) | — | Aponta para −Z |

A sequência de desdobramento acima é a do clipe de demonstração `RigDemo` do GLB-fonte. Ela serve de referência de sentidos; a coreografia do capítulo (M09–M19) é de R3/R4 e deve evitar atravessar as rodas dianteiras.

## Anchors (empties, sem geometria)

| Anchor | Pai | Posição em repouso | Uso |
| --- | --- | --- | --- |
| `Anchor_Pancam` | PMA_Head | (0,030; 1,468; 0,655) | Centro entre as Pancams, olhando para +Z. Planos de câmera do rover (M25–M28) |
| `Anchor_Navcam` | PMA_Head | (0,030; 1,490; 0,640) | Centro entre as Navcams |
| `Anchor_RAT_Contact` | Tool_RAT | (0,030; 0,078; 0,685) | Face da broca: ponto de contato da RAT (MAR-004) |
| `Anchor_APXS_Contact` | Tool_APXS | (0,030; 0,235; 0,832) | Anel de contato do APXS (MAR-005) |
| `Anchor_MB_Contact` | Tool_MB | (0,030; 0,356; 0,685) | Placa de contato do Mössbauer (MAR-005) |
| `Anchor_MI_Focus` | Tool_MI | (0,030; 0,235; 0,510) | Foco do Microscopic Imager (~3 cm à frente da lente) |
| `Anchor_HGA_Boresight` | HGA_Dish | (−0,450; 0,820; −0,115) | Centro da face da antena; aponta para +Z local em repouso (M29) |
| `Anchor_CamFollow` | Body | (0,000; 0,900; 0,060) | Mira da câmera de acompanhamento (coincide com `pivotHeightMeters = 0,9` do MarsRoverController) |
| `Anchor_Track_L` / `Anchor_Track_R` | raiz | (∓0,620; 0,000; −0,610) | Contato das rodas traseiras com o solo: rastro independente da animação (DIRECTION.md) |

## LOD

Cada nó com geometria tem `<Nó>_LOD0` e `<Nó>_LOD1` (redução *collapse* de 30–45% por família, com arestas vivas recalculadas) e um `LODGroup` próprio: LOD0 acima de 30% da altura da tela, LOD1 até 0,5%, depois some. O builder cria/normaliza um grupo por nó; o `LODGroup` único que o importador da Unity cria na raiz pelos sufixos `_LOD0/_LOD1` é removido pelo `MarsRoverModelPostprocessor`. As texturas são as mesmas nos dois níveis (nada duplicado).

## Materiais

`Materials/M_MER_Atlas.mat` e `M_MER_Solar.mat`, URP/Lit, fluxo metálico. Texturas em `Textures/`:

- `*_BaseColor` sRGB.
- `*_Normal`: Normal map, convenção OpenGL (+Y, mesma do glTF e da Unity), **sem inverter o verde**.
- `*_MetallicSmoothness` linear: R = metálico, A = suavidade (= 1 − rugosidade), como no SurveyorTeamWeb.

O atlas tem 16 superfícies (folha dourada MLI, manta preta, pintura branca, alumínio escovado, estrutura anodizada, rodas, cobre, metal escuro, colmeia, bege de instrumento, lente, alvo de calibração, cabo trançado, moldura do deck, prato da antena, fita térmica) com poeira marciana. O material solar repete a cada 0,5 m (células de ~4 × 6 cm).

## Importação na Unity (eixos)

O FBX sai do Blender com eixo Y declarado e sem `bake_space_transform`; por isso o *Bake Axis Conversion* da Unity não tem o que converter e a conversão chegaria como rotação de 90° em X na raiz, com nós e malhas no referencial Z-up do Blender. `Editor/MarsRoverModelPostprocessor.cs` (só para `MER_Rover.fbx`) assa, na importação, essa rotação mais 180° em Y (frente Blender −Y → Unity +Z, esquerda Blender +X → Unity −X, como em `conversion.json`) nas posições dos nós e nos vértices/normais/tangentes. Resultado: raiz e todos os nós com rotação identidade e as posições desta página. O arquivo FBX não é alterado. Se o gerador passar a exportar já convertido, remover o pós-processador.

## Reconstruir e validar

Menu **Abandoned Space Apps → Mars → MAR-011**:

- *Reconstruir prefabs visuais do rover*: configura importadores e remapeamento de materiais, garante os LODGroups e regrava as duas variantes. GUIDs preservados.
- *Validar rover*: grava `QA/MAR-011/unity-validation.txt`. Confere importador, nós e anchors, rotação de repouso, frente em +Z, lado esquerdo em −X, Pancam entre 1,40 e 1,55 m, eixo de esterço sobre o centro das rodas, pinos simétricos, diâmetro da roda, apoio no solo, triângulos por LOD, LODGroups, ≤ 3 materiais, ausência de colliders e malhas e materiais idênticos entre Spirit e Opportunity.

Linha de comando: `-executeMethod Abandoned.SpaceApps.Chapters.Mars.EditorTools.MarsRoverVisualAssets.BuildFromCommandLine` (ou `.ValidateFromCommandLine`), com código de saída 0 quando aprovado.

## Integração com R3

O prefab `MarsRoverGameplay` (MAR-002) tem um filho `VisualRoot`. A montagem do capítulo (MAR-009) instancia `OpportunityVisual` (ou `SpiritVisual`) ali, com posição, rotação e escala zeradas, e passa a dirigir `Steer_*`, `Wheel_*`, `IDD_*`, `PMA_*` e `HGA_*` pelos nomes acima. Nenhum script deste prefab precisa ser editado.
