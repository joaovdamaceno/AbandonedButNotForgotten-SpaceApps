# MAR-020: relatório de entrega

Data: 2026-11-15. Issue: #39. PR: #216 (branch `feature/MAR-020-auditoria-apoios-web` para `develop`).

**Este arquivo não é a saída de `MarsSupportAudit`.** Ele registra o que foi entregue, o que foi verificado e por quem, para quem revisa a PR. O relatório gerado pela ferramenta (`QA/MAR-020/support-audit.txt`) não foi anexado.

## O que foi entregue

| Arquivo | O que é |
| --- | --- |
| `Assets/_Game/Chapters/Mars/Editor/MarsSupportAudit.cs` (+ `.meta`) | Auditoria de Editor: câmeras de pouso M04 a M06 nos dois palcos, apoios do escudo e do meteorito, afloramentos e rochas de Eagle, recuo da RAT, materiais e sombras para a Web, perfis leve e equilibrado, rastro do rover. |
| `Assets/_Game/Chapters/Mars/Tests/MarsVisualChecklist.md` (+ `.meta`) | Roteiro das conferências visuais que a auditoria não decide. |

Já integrado antes, na PR #210: pouso da Spirit na cena antiga em areia, sem atravessar pedra (`SplitLanding.cs`).

Commits da PR: `defe1c7`, `8783550`, `9889b06`, `9c73fe0` e o commit deste relatório.

## O que foi verificado, e por quem

| Verificação | Quem | Resultado |
| --- | --- | --- |
| Análise sintática de `MarsSupportAudit.cs` (tree-sitter) | Claude | 0 erros. Não é compilação. |
| SHA-256 dos quatro arquivos depois do envio ao GitHub | Claude | Idênticos aos originais. |
| `MarsSupportAudit` aberta no Unity, menu rodado | Gabriel (autor) | Relatado como correto, sem erro. Resultado informado verbalmente; o arquivo gerado não foi anexado. |
| Revisão automática (Codex) | Bot | 1 apontamento, aberto (abaixo). |
| Testes automatizados NUnit do capítulo | ninguém | Não executados para esta PR. |

## Pendências e limitações conhecidas

1. **Perfis leve e equilibrado sem cobertura.** Em `AuditProfileVolume` a auditoria lê `m_VolumeProfile` dos assets do pipeline, que é nulo neste projeto (o perfil vem de `URPDefaultVolumeProfileSettings`). O trecho de Bloom, Tonemapping e DepthOfField do perfil global não é conferido. Um resultado sem falhas não prova o critério de mancha branca.
2. **Capturas de ângulo baixo não foram feitas** (vãos sob rochas e escudo, recuo da RAT, perfis leve e equilibrado, navegador). Os critérios visuais da MAR-020 não estão provados por imagem.
3. **Nada foi medido no navegador (WebGL).**
4. A RAT é medida na prefab isolada, não no contato com a rocha McKittrick dentro do rover.
5. O rover novo (`MarsRoverController`) não tem rastro; a checagem do rastro cobre o código da cena antiga.

## Estado dos critérios de aceite

| Critério | Estado |
| --- | --- |
| Sem vão nos pontos de apoio | Auditado pelo autor, resultado relatado. Sem captura. |
| Câmeras de pouso acima do chão em cada corte | Auditado pelo autor, resultado relatado. Sem captura. |
| RAT recua sem clipping | Auditado pelo autor, resultado relatado. Sem captura. |
| Perfis leve e equilibrado sem mancha branca e com ciência legível | **Não coberto** (pendência 1 e 2). |

Quem fechar a #39 aceita as pendências acima como limitação conhecida da entrega.
