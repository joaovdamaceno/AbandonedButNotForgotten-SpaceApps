# Recuperação de integrações — 14/11/2026

Escopo: correção da automação de produção, sem alteração de gameplay, cenas ou configuração da Unity. Base de implementação: `69e9b97` de `develop`; última entrega inicial CORE integrada: CORE-006 (`934a2fd`), com atualização posterior de CORE-001 MCP (`30d78a3`).

## Evidência do incidente

- UI-002 #9: PR #108 integrado em develop, `Closes #9`, merge `f201167c5a523dc8783ca41cac6dc9cc81758a8e`.
- UI-003 #10: PR #135 integrado em develop, `Closes #10`, merge `ab7429150d77a8ab2034173f4af2190c44599da4`.
- Durante a correção apareceu outro caso: MAR-012 #31, PR #143, merge `a1d345e710d818fd49a127373993e981c8e53060`.

As issues estavam fechadas sem integrated. O cálculo antigo as bloqueava e não recuperava o histórico. No início da investigação, o Actions registrava apenas uma execução manual do workflow; a causa externa da ausência dos eventos não foi comprovada.

## Verificação executada

- `python Tools/test_readiness.py`: **33 testes aprovados** (21 anteriores e 12 novos).
- `python Tools/validate_plan.py`: **84 tarefas**, sem erros; validação de estrutura, links, DAG e sintaxe Python.
- Revisão do diff e `git diff --check`: sem erros.
- Prévia real em modo somente leitura: três integrações recuperáveis, sem casos ignorados; estados preservados até aplicação.
- Aplicação real: os três merges foram reconsultados pela API, incluindo os históricos paginados das issues. As três tarefas receberam integrated, e os rótulos de bloqueio foram removidos.
- UI-005, UI-006 e UI-007 ficaram Prontas. INT-001 continua bloqueada por suas outras dependências reais; UI-008 continua opcional e depende também de UI-004.
- O Project atualizou somente **8 cartões**, preservando outros **76** valores de Status. Não foi criado ou copiado nenhum segredo para o Actions.

Os testes novos cobrem evento perdido, issue ainda aberta, fechamento antes de marcar integrated, prévia sem mutações, idempotência, reabertura, novo merge posterior, paginação de PRs/histórico, prova incompleta, cancelamento, bloqueio externo, alteração concorrente durante consulta e atualização seletiva do Project.

## Operação e limites

Push em main/develop e agendamento de segurança complementam issues/merge e acionamento manual. A reconciliação consulta o histórico em cada execução; ela não depende de reemitir os eventos dos PRs antigos. A verificação do workflow hospedado é feita após sua integração na branch padrão.

O GITHUB_TOKEN mantém labels e dependências lógicas; as colunas do Project continuam exigindo o segredo opcional PROJECTS_TOKEN ou execução local autorizada com acesso project. O agendamento do GitHub pode atrasar. Não há transação entre todas as issues; reconsultas e execução idempotente reduzem corridas, mas não equivalem a uma trava distribuída para CLIs simultâneas.

Não foi executado Editor ou build Unity, pois os arquivos alterados são ferramentas Python, documentação e workflow. Configurações locais do Unity foram preservadas em outra árvore de trabalho.
