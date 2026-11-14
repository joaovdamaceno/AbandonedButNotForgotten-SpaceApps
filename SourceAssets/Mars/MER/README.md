# Fontes do rover MER (MAR-011)

Modelo próprio da equipe, gerado por código em 14/11/2026 para a MAR-011. Substitui, por decisão do responsável pela tarefa, o GLB oficial `24883_MER_static.glb`, que **não foi baixado**. Créditos e limites: `Assets/_Game/Chapters/Mars/Art/MarsAssetCredits.md`.

| Arquivo | Conteúdo | SHA-256 |
| --- | --- | --- |
| `MER_Rover.blend` | Cena Blender 5.3 com o rig (nós com pivôs nos eixos reais), materiais e o clipe `RigDemo` em NLA | `8d4c540e97e8fd0e21b7a51c90acd93d54d576640c74e4c967f40595474d3cba` |
| `MER_Rover.glb` | glTF 2.0 aprovado na revisão (50 564 triângulos, 2 materiais, 6 texturas JPEG embutidas, clipe `RigDemo` de 8 s). Validador Khronos: 0 erros, 0 avisos | `7f9545f1d7256112052af6c09e93be91b313fc597fabd6a5517861609dbc9c22` |
| `reference/*.png` | 10 imagens de referência fornecidas pela equipe. **Origem a confirmar**; usadas só como guia de proporção | ver tabela abaixo |

## Reproduzir

Requer Python 3.11+ com `bpy` 5.3 (`pip install bpy`), `numpy` e `Pillow`. Os scripts não executam nada vindo de arquivos externos.

```text
python generator/build.py            # gera out/MER_Rover.blend (geometria, rig, texturas, clipe RigDemo)
python generator/export.py           # out/MER_Rover.glb
python generator/export_unity.py -- <pasta>   # FBX LOD0/LOD1 + texturas URP + conversion.json
python generator/render.py -- 28     # vistas de revisão (Cycles); sheet.py monta as comparações
```

`export_unity.py` segue o pipeline de `Docs/Art/IMPORT_RULES.md` §3: FBX em metros (*FBX Scale All*), Forward −Z / Up Y, sem *Apply Transform* (com empties aninhados ele introduz rotações e escala 0,01 alternadas — verificado em reimportação), e *Bake Axis Conversion* no importador da Unity.

## Referências

| Arquivo | Vista | SHA-256 |
| --- | --- | --- |
| `01_frente.png` | frente | `888a4af045c2dd672657578ebe2e043e73fa6830a5dedeccb73075d18328f9b3` |
| `02_lado_esquerdo.png` | lado esquerdo | `f400e12726f4a473580451d6302bce1e9695f87b8a35706b8c706f9d4eb99810` |
| `03_tras.png` | trás | `49f9d33b9587a0d08e8d1cf8fe9ba2529bcfe46acd215e363d402093e1d28ca5` |
| `04_lado_direito.png` | lado direito | `d85f7f9ead2b0653daccbd0a7644a7a74d0dbb1d8bc171ab6f4af07fe76d6bcd` |
| `05_topo.png` | topo | `c51c7d48325ed60d4c546713cdfc46f82a32484fab3ede466c43242b51ffaeff` |
| `06_tres_quartos_frente.png` | 3/4 frente | `0398ca3783bfe27c52574c063a77dcfbaa9a065d40a2a7de538920eda6ae68e8` |
| `07_tres_quartos_tras.png` | 3/4 trás | `aa0f81e71489a7c4bec47d8cf89647f58f88be334c4a969694393f0703602934` |
| `08_baixo.png` | baixo | `2b6a1b314d2f4d08c77faddf1ba90c18f00a521a8272c155d2cf3c12c6538322` |
| `09_baixo_lateral.png` | baixo lateral | `c37e581b6d8d5a481ddc2337f73904b0bee530919b379d219b874d0e64422456` |
| `10_frente_baixa.png` | frente baixa | `ead0c6ebe1fa608b9a1cb3401c2c1cb294f83d26ae7010f613b3560ffea8414d` |
