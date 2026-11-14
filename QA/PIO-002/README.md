# PIO-002 — evidência de validação

Executado em 14/11/2026 sobre `feature/PIO-002-modelo-sonda` já com `origin/develop` mesclada (f1c75fd, inclui MAR-011; único conflito: `ASSET_CREDITS.md`, resolvido mantendo as duas linhas), Unity **6000.6.7f1** / URP 17.6, Windows, em batchmode (Unity fechada).

| Verificação | Resultado |
| --- | --- |
| Suíte EditMode completa (`-runTests -testPlatform EditMode`) | **417/417 passaram**, 0 falhas, na base mesclada (`editmode.xml`; 369/369 antes de mesclar o develop) |
| Builder `pioneer10.probe` reexecutado sem mudanças | Arquivos versionados ficaram idênticos (`git status` limpo) |
| Captura `PioneerProbeCapture` | Executada; 9 quadros conferidos visualmente (03_tres_quartos). Os PNGs e `PROBE_STATS.md` regravados neste computador **não** foram commitados (ruído de GPU/locale); valem os da sessão anterior |
| Build WebGL só da cena `ProbePreview.unity` (`Tools/build_release.py --targets webgl`) | Sucesso na base mesclada: 17 arquivos, 14,25 MiB, shaders 1/1, hashes conferidos (14,23 MiB antes de mesclar) |

**Nota do build:** com cache incremental (`Library/PlayerDataCache`, `Library/BuildPlayerData`) o relatório do Unity não lista os assets e o `build_release.py` falha com `ShadersStripped` (0/1) sem que nada tenha sido removido. Apagar esses dois diretórios de `Library/` (regeneráveis) e refazer o build dá shaders 1/1.

**Não executado:** Play Mode, abertura da build no navegador (DXT1/WebGL não testado em navegador), profiler Web, FPS.

## Conformidade com a IMPORT_RULES (PR de correção)

Auditoria da PIO-002 contra `Docs/Art/IMPORT_RULES.md` (§1, §3, §5, §7, §9, §10) e a tarefa. Base `develop` 0ffb40a. Unity 6000.6.7f1, batchmode.

| Item | Antes | Agora |
| --- | --- | --- |
| §1 origem verificável do arquivo da equipe | "origem exata não verificada" (a regra diz que sem origem verificável o arquivo não entra) | **Verificada:** o GLB tem o mesmo tamanho (2.049.840 B) e o mesmo SHA-1 de blob Git (`62d2608617424e7b1d6d4d45bae13024cd9781c8`) do `Pioneer 10.glb` do espelho oficial `nasa/NASA-3D-Resources`, pela API de conteúdo do GitHub (só metadados, nada baixado). Teste `TheOriginalIsByteIdenticalToTheOfficialNasaMirrorByItsGitBlobHash` refaz a conferência |
| §5 LOD1 "sim (planos distantes)" | não criado | `LODGroup` na raiz: LOD0 = 7 peças (8.199 triângulos) até 12% da altura da tela; LOD1 = malha única `Lod1` (2.303 triângulos, 28% do LOD0, mesmo atlas/material, caixa a ≤ 2 cm do LOD0); corte abaixo de 1% |
| §7 malhas | sem Optimize nem compressão | Optimize Mesh e compressão média em todas as 8 malhas. Read/Write continua ligado de propósito (assets gerados, reescritos pelo builder e lidos pelas verificações) |
| Tarefa: "estimar custo Web" | não estimado | `PROBE_STATS.md`: atlas 2048² DXT1 ≈ 2,7 MiB com mipmaps (de ≈ 96 MiB de orçamento), malhas ≈ 357 KiB, sem download em runtime. É estimativa analítica, não medida no navegador |
| Estatísticas | números com vírgula ou ponto conforme a máquina | sempre com ponto decimal (cultura invariante) |
| Capturas | 9 | 13: quadros 09–12 comparam LOD0 × LOD1 com a mesma câmera (distante e 3/4) |

Uma primeira versão do LOD1 agrupava só por posição e fundia a frente e o verso do refletor (interior preto na captura). A comparação visual mostrou o defeito; agora o agrupamento também separa por face da normal.

| Verificação | Resultado |
| --- | --- |
| Suíte EditMode completa (base `develop` 31fbfb3 mesclada) | **563/563**, 0 falhas (`editmode.xml`; 543/543 antes de mesclar o develop); `PioneerProbeTests` passou a 14 testes (4 novos: origem pelo SHA-1 de blob, LODGroup/LOD1, compressão de malha, estatísticas) |
| Builder `pioneer10.probe` reexecutado | arquivos idênticos |
| Build WebGL da cena de prévia (cache limpo) | sucesso: 17 arquivos, 14,34 MiB, shaders 1/1, hashes conferidos |

**Não feito:** a checklist §10 pede "silhueta comparada com a foto de missão no mesmo ângulo". Não há, no repositório, foto da sonda com licença verificada, e não baixei nenhuma (o espelho da NASA tem um `Pioneer 10.png` de pré-visualização do próprio modelo, que não é foto de missão). Continua pendente para R7/ART-002. Também não executados: Play Mode e navegador (DXT1 no WebGL).
