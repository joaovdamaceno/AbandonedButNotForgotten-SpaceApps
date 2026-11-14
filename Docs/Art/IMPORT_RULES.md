# ART-001 — Regras de importação de assets

Dono: R4. Vale para modelos, texturas, fotos, fontes e áudio que entram no projeto. A importação concreta de cada modelo pertence à tarefa do capítulo (MAR-011, PIO-002, SUR-002…); este documento define **como** ela acontece. Inventário e estado de recebimento: [ASSET_AUDIT.md](ASSET_AUDIT.md). Registro de direitos: [ASSET_CREDITS.md](../../ASSET_CREDITS.md).

## 1. Antes de baixar ou receber um arquivo

1. Abrir a **página específica** do item (não a busca) e anotar: título, URL, instituição/autor, linha de crédito, data de acesso, licença/termos e formato/tamanho anunciados.
2. Ler as diretrizes NASA: https://www.nasa.gov/nasa-brand-center/images-and-media/ (conferido em 14/11/2026, ver resumo em ASSET_AUDIT). Material NASA em geral não tem copyright nos EUA, **mas** insígnia/logotipo e identificadores não são de domínio público, material de terceiros em sites NASA mantém o copyright do titular e não se pode sugerir endosso. Estar disponível na internet **não** é permissão de redistribuição.
3. Se o arquivo vier de colega da equipe: perguntar a origem exata. Sem URL/autor/licença verificáveis, o arquivo não entra; usa-se o fallback.
4. Abrir uma linha **Planejado** em `ASSET_CREDITS.md` com responsável e destino antes da importação.

## 2. Onde guardar

| Tipo | Original (nunca vai para o build) | Derivado de runtime |
| --- | --- | --- |
| Modelo 3D | `ArtSource/<Capítulo>/<ID>/original/` (LFS) + `SOURCE.md` com URL, data, SHA-256 | `Assets/_Game/Chapters/<Capítulo>/Art/Models/` |
| Textura/foto | `ArtSource/<Capítulo>/<ID>/original/` | `Assets/_Game/Chapters/<Capítulo>/Art/Textures/` ou `Shared/` se usada por 2+ capítulos (revisão R1) |
| Fonte | — (TTF usado direto) | `Assets/_Game/Shared/Fonts/<Família>/` com `OFL.txt` ao lado |
| Áudio | `ArtSource/Audio/<cue>/original/` | `Assets/_Game/Shared/Audio/` (AUD-002) |

`ArtSource/` fica fora de `Assets/`: a Unity não importa nem empacota originais pesados. Os binários estão em LFS pelo `.gitattributes` (`*.glb`, `*.gltf`, `*.bin`, `*.usdz`, `*.fbx`, `*.blend`, `*.png`, `*.jpg`, `*.tga`, `*.psd`, `*.exr`, `*.wav`, `*.ogg`, `*.mp3`, `*.flac`…). Nunca colocar originais em `Resources/` nem um asset de capítulo na pasta de outro capítulo.

## 3. Eixos, escala e pivôs

- Unidade: **1 unidade Unity = 1 metro**. Unity é Y para cima, +Z para frente, mão esquerda.
- glTF/GLB é Y-up, mão direita, +Z frente; Blender é Z-up. Exportar FBX do Blender com *Forward −Z, Up Y*, *Apply Scalings: FBX All*, transformações aplicadas; na Unity, *Bake Axis Conversion* ligado. Resultado esperado: rotação (0,0,0) e escala (1,1,1) na raiz do prefab.
- Conferir uma medida conhecida **antes** de integrar e registrar no SOURCE.md:
  - Pioneer 10: refletor da antena de alto ganho **2,74 m** de diâmetro (P-S05).
  - MER: câmeras do mastro a **cerca de 1,5 m** do solo; rover “do tamanho de um carrinho de golfe”, 185 kg (página NASA Opportunity, consultada em 14/11/2026).
  - Surveyor 3: pá alcança até **1,5 m** do módulo e cava até **18 cm** (página NASA Surveyor 3). Altura total e envergadura das pernas: **conferir no relatório S3-03** antes de modelar; não estimar de memória.
- Pivô da raiz no ponto de apoio no chão, centrado entre os pontos de contato. Pivôs de peças móveis no eixo físico real (eixo da roda, eixo de esterço, junta do braço, gimbal da antena, eixo do espelho). O eixo de spin da Pioneer é paralelo ao eixo da antena (P-S05): a raiz que gira fica nesse eixo.

## 4. Separação de peças animadas

Separar somente o que o roteiro anima; o resto fica numa malha combinada.

| Hardware | Peças separadas (nome do nó) | Uso no roteiro |
| --- | --- | --- |
| MER (gêmeas) | `Wheel_FL/FR/ML/MR/RL/RR`, `Steer_FL/FR/RL/RR`, `Rocker_L/R`, `Bogie_L/R`, `IDD_Shoulder/Elbow/Wrist/Turret` + ferramentas `Tool_RAT/APXS/MB/MI`, `PMA_Mast/PMA_Head`, `HGA_Gimbal/HGA_Dish` | Condução, braço que recua pela normal (M11–M13), Pancam (M25–M28), antena (M29) |
| Módulo MER (pouso) | `Lander_Base`, `Petal_1/2/3`, `Airbag_Cluster_*` (deform/escala), `Backshell`, `HeatShield`, `Parachute` | Pouso paralelo e abertura (M04–M06) |
| Pioneer 10 | `Spin_Root` (tudo que gira), `Plaque` como nó próprio para close | Rotação de cruzeiro, placa (PIO.Plaque) |
| Surveyor 3 | `TV_Mirror_Azimuth/Elevation`, `Scoop_Arm_1/2/Scoop`, `Lens_25/100` (se visível) | Espelho/lentes, pá (poses repouso/acima/contato) |

Nenhuma peça desaparece entre planos: continuidade (airbags, pétalas, escudo) é responsabilidade da tarefa do capítulo e revisada em ART-002.

## 5. Orçamento por asset (WebGL, GPU integrada)

Totais da cena seguem `Docs/04_WEB_AND_QA.md` (≈250 mil triângulos visíveis em gameplay, 400 mil em close; texturas ≈96 MiB residentes). Por asset:

| Asset | LOD0 (triângulos) | LOD1 | Texturas | Materiais |
| --- | ---: | ---: | --- | --- |
| Rover MER (instância compartilhada pelas gêmeas) | ≤ 80 mil | ≤ 30 mil | 1 atlas 2048 + 1024 | ≤ 3 |
| Módulo de pouso + airbags | ≤ 40 mil | ≤ 15 mil | 1024 | ≤ 3 |
| Pioneer 10 (PIO-002) | ≤ 60 mil | sim (planos distantes) | 1 atlas 2048, resto 1024 | ≤ 3 |
| Surveyor 3 (reconstrução) | ≤ 25 mil | — | 1 atlas 1024 | ≤ 2 |
| Escudo térmico / fragmentos | ≤ 6 mil | — | 1024 | 1 |
| Rocha/pedra de cenário | ≤ 1,5 mil cada | — | atlas compartilhado 1024 | 1 compartilhado |
| Silhuetas (Delta II, Atlas-Centaur, LM, antena DSN) | ≤ 3 mil | — | 512 ou cor sólida | 1 |
| Terreno de capítulo | conforme tarefa, com culling | — | 512–1024 | ≤ 2 |

Se o arquivo recebido exceder: decimar no Blender, comparar silhueta com a foto de referência no mesmo ângulo, registrar a redução em `ASSET_CREDITS.md` (coluna Alterações). Os gêmeos MER **compartilham** malhas e materiais: nunca duas cópias.

## 6. Materiais e texturas

- Shader: **URP/Lit** (ou URP/Simple Lit em peças distantes). Não usar shaders importados do glTF no build: aumentam variantes e o tamanho do WebGL.
- glTF usa *metallicRoughness* (canal G = rugosidade, B = metálico). A URP usa *Metallic* (R) + *Smoothness* (A, = 1 − rugosidade). Reempacotar no Blender ou em ferramenta de imagem; registrar a conversão.
- **Normais:** glTF usa convenção OpenGL (+Y), igual à Unity — não inverter o verde. Importar como *Texture Type: Normal map*. Se a fonte declarar DirectX (−Y), inverter G e registrar. Conferir o resultado com luz rasante contra a foto antes de aplicar qualquer importador global. (A edição anterior registrou problema de normal RGB vs. normal reempacotada com o importador glTF; por isso não se aplica configuração em massa sem conferência visual.)
- Albedo/emissão sRGB; máscaras (metálico, oclusão, rugosidade) lineares. Mipmaps ligados, *Max Size* conforme a tabela, compressão desktop padrão; QA-001 define formato/fallback do WebGL.
- Texturas internas de GLB **não** entram sem override de tamanho/compressão.
- Fotos históricas exibidas como imagem (UI): até 1024 px de largura (Pioneer) ou 1024–1600 (Apollo 12/Surveyor), sem upscale por IA; manter a resolução histórica modesta quando ela é parte do fato.

## 7. Configuração de importação na Unity

Modelo: *Read/Write* desligado, *Mesh Compression* Medium, *Optimize Mesh* ligado, sem lightmap UV (sem GI bakeado no MVP), *Animation Type* None para hardware estático (animação por script/rig do capítulo), materiais extraídos para `Art/Materials/` e remapeados para URP/Lit. Prefab do capítulo referencia o modelo; ninguém edita o `.fbx` importado por outro dono.

## 8. Pipeline GLB → Unity

- **Padrão (sem pacote novo):** GLB original → Blender (separar peças, escala, decimação, reempacotar texturas) → FBX + PNG → Unity. Blender é GPL, mas os arquivos exportados não herdam a licença do programa.
- **Alternativa:** se a pessoa responsável não tiver Blender, abrir PR propondo `com.unity.cloud.gltfast` com o consumidor concreto (qual modelo, qual prefab) e revisão de R1, conforme `AGENTS.md`. Mesmo assim, materiais do build são convertidos para URP/Lit.
- Nunca baixar modelo de site de terceiros (Sketchfab, etc.) sem licença específica do arquivo. Modelo de jogo comercial é proibido.

## 9. Asset provisório (fallback)

- Nome com sufixo `_PROXY` (malha e prefab) e linha em `ASSET_CREDITS.md` como **Provisório — autoria da equipe**.
- Precisa manter os componentes reconhecíveis listados em ASSET_AUDIT e **os mesmos nomes de nós/pivôs** da tabela da seção 4, para que trocar pelo modelo definitivo seja substituir a malha no prefab sem mudar scripts.
- Nunca rotular proxy como “modelo NASA” na tela ou nos créditos.

## 10. Checklist de importação (copiar no PR)

- [ ] Página específica, crédito, licença e data registrados em `ASSET_CREDITS.md`; SHA-256 do original.
- [ ] Original em `ArtSource/…` (LFS), derivado em `Assets/_Game/Chapters/<Capítulo>/Art/…`.
- [ ] Rotação 0 / escala 1 na raiz; medida de referência conferida (seção 3).
- [ ] Peças móveis separadas com nomes da seção 4; pivôs nos eixos reais.
- [ ] Triângulos e texturas dentro da seção 5 (Statistics da Game view / Inspector).
- [ ] Materiais URP/Lit; normais conferidas com luz rasante; sem shader importado.
- [ ] Apoio no chão em vários pontos, nada flutuando nem atravessando o terreno.
- [ ] Silhueta comparada com a foto de missão no mesmo ângulo (anexar captura).
- [ ] Nenhum logotipo/insígnia NASA usado como marca do jogo.
