# Créditos de assets — Capítulo I (Spirit & Opportunity)

Registro do capítulo de Marte. A fonte única de direitos do projeto continua sendo [`ASSET_CREDITS.md`](../../../../../ASSET_CREDITS.md); regras em [`Docs/Art/IMPORT_RULES.md`](../../../../../Docs/Art/IMPORT_RULES.md).

## M-ROVER · Rover MER (Spirit/Opportunity) — modelo próprio riggado

| Campo | Valor |
| --- | --- |
| Tarefa / responsável | MAR-011 · R4 |
| Autoria | Equipe do projeto, gerado por código próprio (Blender 5.3, Python) em 14/11/2026. **Não é o modelo 3D da NASA** e não deve ser creditado como “modelo NASA” na tela nem nos créditos. |
| Decisão | Em 14/11/2026 o responsável pela tarefa decidiu substituir o GLB oficial `24883_MER_static.glb` (A-MER) por um modelo próprio, para ter articulações separadas, pivôs nos eixos reais e controle total do rig. O GLB da NASA **não foi baixado nem importado**. |
| Referências visuais | 10 imagens de referência (frente, lados, trás, topo, baixo e oblíquas) fornecidas pela equipe e guardadas em `SourceAssets/Mars/MER/reference/`. **Origem das imagens: a confirmar pela equipe** (URL, autor e licença). Elas só orientaram proporções e disposição de peças: nenhum pixel delas está nas texturas do modelo. |
| Texturas | Procedurais, determinísticas (`generator/tex.py`, semente fixa): nenhuma foto, logotipo, insígnia ou marca. |
| Detalhes inventados | Cabos, presilhas, parafusos, conectores, aletas e a disposição exata das quatro ferramentas no braço guardado foram criados para dar acabamento; não reproduzem um desenho técnico. |
| Medidas | Pancam a 1,468 m do solo (IMPORT_RULES §3: “cerca de 1,5 m”); rodas de 0,26 m. Ver `Rover/RIG.md`. |
| Arquivos de runtime | `Rover/MER_Rover.fbx`, `Rover/Textures/*.png`, `Rover/Materials/*.mat`, `Prefabs/SpiritVisual.prefab`, `Prefabs/OpportunityVisual.prefab` |
| Originais (fora de `Assets/`) | `SourceAssets/Mars/MER/MER_Rover.blend` (SHA-256 `8d4c540e97e8fd0e21b7a51c90acd93d54d576640c74e4c967f40595474d3cba`), `MER_Rover.glb` com clipe `RigDemo` (SHA-256 `7f9545f1d7256112052af6c09e93be91b313fc597fabd6a5517861609dbc9c22`) |
| FBX de runtime | SHA-256 `0030df29420652e0ef2a10d0477d2c6306584bca4735792d0ba6aa19a0f743e1`; hashes das texturas em `Rover/conversion.json` |
| Licença | Produção própria da equipe para este projeto educativo; Blender é GPL, mas os arquivos exportados não herdam a licença do programa (IMPORT_RULES §8). |
| Crédito sugerido | “Modelo 3D do rover MER: equipe Abandoned, But Not Forgotten (reconstrução educativa)” |

## M-LANDING · Conjunto de pouso (MAR-014)

Gerado por código próprio (`Art/Landing/Editor`): cápsula, escudo, paraquedas, módulo, airbags, texturas procedurais. Sem imagens, modelos ou logotipos externos. Fatos: NASA GISS Mars24 (datas), NASA/JPL (sequência de pouso e desembarque). Licença: produção própria do projeto.
