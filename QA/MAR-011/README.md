# MAR-011 — evidências

Base: `develop` em `1c503be` (Merge #105, SUR-006). Última CORE integrada: CORE-001, pacote MCP for Unity (#104, `30d78a3`).

## Mudança de escopo registrada

A tarefa previa auditar o GLB oficial da NASA (`24883_MER_static.glb`). Em 14/11/2026 o responsável decidiu usar um **modelo próprio riggado**, gerado a partir de imagens de referência, para ter articulações e pivôs controlados. O GLB da NASA não foi baixado. O critério “Arquivo não recebido permanece registrado como bloqueio real” foi tratado assim: A-MER passou para **Não usado** em `ASSET_CREDITS.md` e `Docs/Art/ASSET_AUDIT.md`, com o motivo, sem fingir importação.

## O que foi executado (fora da Unity)

| Verificação | Resultado | Arquivo |
| --- | --- | --- |
| Silhueta contra as 10 referências, mesmos ângulos (Cycles) | Fotos à esquerda, modelo à direita | `compare-ortho.png`, `compare-oblique.png` |
| Detalhe de mastro, braço, roda e antena | — | `closeups.png` |
| Rig: quadro 75 (esterço ±28°, mastro em panorâmica) e quadro 205 (braço desdobrado, antena apontada) | Nenhuma peça se separa da articulação | `rig-demo-frames.png` |
| LOD0 (50 564 tri) × LOD1 (19 338 tri), mesmas vistas | Silhueta preservada; arestas vivas recalculadas no LOD1 | `lod0-vs-lod1.png` |
| Reimportação do FBX de runtime no Blender | Raiz com rotação 0 e escala 1; 0 nós com rotação ou escala diferentes de identidade; posições em metros; 2 materiais | `fbx-roundtrip.txt` |
| Validador Khronos do GLB-fonte | 0 erros, 0 avisos (10 infos `NODE_EMPTY` = as âncoras, intencionais) | `gltf-validator.json` |
| Reprodutibilidade | `generator/` executado numa cópia limpa: mesma contagem de triângulos e as 6 texturas idênticas byte a byte | — |
| Visualizador three.js r170 do GLB em Chrome sem interface | Carrega, toca `RigDemo`, mostra pivôs e âncoras; 27 nós com geometria, 10 âncoras, 2 materiais | — |
| `MarsRoverVisualAssets.cs` | Compila sem erros nem avisos (Roslyn, C# 9) contra *stubs* com as assinaturas da API da Unity usadas | — |

## Executado na Unity 6000.6.7f1 (Linux, batchmode, 14/11/2026)

Comando: `Unity -batchmode -nographics -projectPath . -executeMethod Abandoned.SpaceApps.Chapters.Mars.EditorTools.MarsRoverVisualAssets.BuildFromCommandLine` (regrava `SpiritVisual.prefab`/`OpportunityVisual.prefab` e grava `unity-validation.txt`).

1. **Primeira execução: código 1, reprovada.** Os 6 checks de orientação/posição falharam (Pancam a −0,655 m, frente em −Z, lados trocados, esterço fora do centro das rodas, pino do rocker em y = −0,090 m, menor y = −0,832 m), e a contagem deu 28 LODGroups para 27 nós com geometria. Causa, confirmada com uma sonda temporária que listou a hierarquia importada: o FBX declara eixo Y e foi exportado sem `bake_space_transform`, então o *Bake Axis Conversion* não converte nada. A conversão ficava como rotação de 90° em X na raiz (zerada pelo builder), e nós/malhas continuavam em Z-up. O importador também criava um `LODGroup` extra na raiz.
2. **Correção** (escopo `Art/Rover/`): `Editor/MarsRoverModelPostprocessor.cs` assa na importação a rotação da raiz + 180° em Y nas posições dos nós e nos vértices, normais e tangentes, e remove o `LODGroup` da raiz. FBX inalterado. Documentado em `RIG.md` (*Importação na Unity*).
3. **Segunda execução: código 0, aprovada**, sem nenhuma linha `ERRO`. Relatório completo em `unity-validation.txt`. Pancam a 1,468 m, frente em +Z, esquerda em −X, pino do rocker em |x| = 0,420 m / y = 0,460 m, apoio em y = −0,001 m, 27 LODGroups. A Unity contou 50 548 / 19 322 triângulos LOD0/LOD1 (o Blender contou 50 564 / 19 338).

A sonda foi apagada e não entra no commit. Não houve testes EditMode/PlayMode específicos da MAR-011.

## Ainda não executado

- Inspeção visual no Editor (frente, lado, topo; girar `Steer_FL` em Y e `Wheel_FL` em X) e comparação de cor/normal com luz rasante.
- Build WebGL (perfil Leve da QA-001) para conferir painéis e materiais na Web.

## Critérios de aceite

| Critério | Estado |
| --- | --- |
| Pino encaixado, rodas e esterço com sentidos coerentes | Conferido no Blender, no visualizador e **na Unity pelo validador** (aprovado após o pós-processador de eixos). Giro manual no Editor ainda não feito |
| Painéis e materiais legíveis na Web | Pendente: build WebGL |
| Prefabs mantêm escala e anchors compatíveis | Validador aprovado: raiz identidade e escala 1, 27 nós e 10 âncoras nas posições do `RIG.md`, malhas e materiais compartilhados entre as gêmeas |
| Arquivo não recebido permanece registrado como bloqueio real | A-MER marcado como **Não usado** com motivo; nenhum download declarado |
