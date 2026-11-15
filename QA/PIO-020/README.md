# PIO-020 — Roteiro v2, fase 3: evidência

A foto em três passos e a escuta em uma rodada. Detalhes em `Assets/_Game/Chapters/Pioneer10/README.md` (seção "Roteiro v2, fase 3").

| Verificação | Resultado |
| --- | --- |
| Testes EditMode filtrados por `Pioneer` | **193 de 193**. Novos: a foto em três passos (dois holds, dois toques, nada de radiação, 8/8 uma vez, E mantido não troca o passo, esperar não troca) e a ajuda que gira sozinha depois de 12 s; a escuta em uma rodada no fluxo do capítulo (contatos 1, 2 e 3 e a tentativa sem sinal, sem tecla depois do primeiro). A suíte completa não foi reexecutada |
| Quadros do painel da foto (`Docs/Art/Pioneer10_Imaging/12–15_*`, três resoluções) | Gerados; `QA/PIO-006/contrast-audit.txt` e `layout-audit.txt` sem nenhuma falha (os quadros antigos saíram idênticos) |
| `python Tools/validate_pioneer_data.py` e `export_pioneer_texts.py --check` | Conformes (124 chaves) |

**Não executado:** Play Mode com a UI, teste humano de leitura e de controle, retemporização (45 s e 40 s do roteiro).
