# PIO-018 — Roteiro v2, fase 1: evidência

Quadros 3D do estado novo `PIO.Closest` em `framing/` (ferramenta de desenvolvimento local, não integrada: evidência de quadro, não teste automatizado): chegada (`t03.0`), radiação sobre o fotopolarímetro (`t11.0`), passagem rente às nuvens (`t20.0`, `t24.0`), plano alto (`t33.0`) e a sonda saindo (`t37.0`). Capturas do palco (`Docs/Art/Pioneer10_Stage/`, com o anchor novo) regeneradas.

| Verificação | Resultado |
| --- | --- |
| `python Tools/validate_pioneer_data.py` | Conforme: 9 estados, 415 s nominais (421 s com alongamento), 120 chaves por idioma, 53 cues |
| `python Tools/export_pioneer_texts.py --check` | Conforme (120 chaves) |
| Regra de leitura do projeto (16 caracteres por segundo na janela de cada legenda, PT e EN) | Todas as legendas dentro da janela, salvo os dois cues com alongamento declarado |
| Importação de dados e builders `pioneer10.*` | Sem erro; `PIO_Closest.asset` criado, `PIO_JupiterApproach.asset` atualizado, cena e palco reconstruídos |
| Testes EditMode filtrados por `Pioneer` | **188 de 188**; novo teste do estado só de cinema; testes de dados, narração e fluxo atualizados para 9 estados e 415 s. A suíte completa não foi reexecutada |

**Não executado:** Play Mode com a UI, a build WebGL desta mudança (só dados, uma sequência e texto; o custo do capítulo não mudou de ordem, mas não foi medido), revisão da R7 das legendas novas, teste humano de leitura.
