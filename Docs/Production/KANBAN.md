# Kanban e dependências

Fonte das tarefas: `Data/Backlog/*.json`, agregada em `Data/backlog.json`. Corpo completo em `Docs/Tasks/ID.md`. Mapa para issues/números remotos em `Data/github-map.json`. O [repositório público](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps), por decisão do titular, e o [Project privado](https://github.com/users/joaovdamaceno/projects/1) foram criados e verificados. Estado inicial em 14/11/2026: 84 issues abertas, sendo 7 Prontas, 69 Bloqueadas e 8 no Backlog. Nenhuma tarefa de implementação está concluída. Uma futura falha de acesso ao Projects deve ser registrada; não significa sincronização bem-sucedida.

## Colunas

| Coluna | Significado | Como entrar/sair |
| --- | --- | --- |
| Backlog | P1/P2 ou trabalho ainda não priorizado | Não iniciar antes do MVP ou decisão de R1 |
| Bloqueada | Dependência pendente, bloqueio externo ou fechamento sem integração comprovada | Relação nativa `blocked_by`, lista de IDs e motivo no sincronizador |
| Pronta | Dependências integradas e dono disponível | Conferir escopo e atribuir-se antes de começar |
| Em andamento | Uma pessoa executando | Limite de 1 por pessoa; mover para revisão com PR |
| Revisão | PR/evidência aguardando avaliação | Outro papel revisa; R1 integra e o estado é sincronizado |
| Concluída | Entrega integrada, issue fechada e label `integrated`, com evidência revisada | O sincronizador só usa Concluída quando as duas condições de estado estão presentes |

Campos do projeto: Status, Função (R1–R7), Prioridade (P0/P1/P2), Horas estimadas e Capítulo. Visões: Kanban geral por Status; Prontas (P0); Por função; Opcional (P1/P2). Labels correspondentes permitem usar a página de issues mesmo se Projects estiver indisponível.

## Fechamento nativo e integração são condições diferentes

Fechar uma issue no GitHub, por botão ou diretiva de PR, não prova sozinho que a entrega foi integrada. O vínculo nativo `blocked_by` ajuda a visualizar dependências, mas a liberação de trabalho neste projeto também exige a label `integrated`.

O sincronizador considera uma tarefa entregue somente quando a issue está **fechada e tem `integrated`**. Se for fechada manualmente sem essa label, ela aparece como **Bloqueada**, não Concluída, e não libera dependentes. O responsável deve revisar o caso e reabrir a issue quando o fechamento foi prematuro; nunca adicionar `integrated` só para mover o cartão.

O caminho normal é PR revisado, merge confirmado pela API na branch autorizada (`develop` ou `main`), critérios e evidências conferidos, label `integrated` e fechamento. A automação processa somente IDs mapeados e diretivas válidas. Um cartão movido à mão ou uma issue apenas fechada não substitui esse caminho.

## Regras que evitam pegar trabalho bloqueado

1. Cada task possui bloqueadores concretos, com links e relação nativa `blocked_by`; o card não é liberado apenas porque alguém começou a dependência.
2. Só iniciar se todos os bloqueadores estiverem fechados com label `integrated`. O sincronizador checa isso e coloca `status:ready`; senão `status:blocked`.
3. Só uma task `status:doing` atribuída por pessoa. GitHub não é um sistema de trava obrigatória de edição: o CLI de claim verifica conflito e o fluxo humano respeita o limite; uma alteração manual pode violá-lo e será sinalizada.
4. PR para `develop` deve citar `Closes #n`. A automação verifica merge no repositório/branch corretos, marca integrated e fecha as issues identificadas. Merge em `main` é só distribuição, não pré-requisito para cada cartão.
5. Reabrir um pré-requisito bloqueia dependentes ainda não entregues; tarefa já integrada exige avaliação de regressão, não desfazer arquivos automaticamente.
6. Se houver bloqueio externo (modelo, acesso), usar o fallback definido ou registrar label `external-blocker` com motivo. Não fabricar o asset ou o teste.

## Operação diária

```text
python Tools/sync_readiness.py --check
python Tools/sync_readiness.py --claim CORE-001 --user SEU_USUARIO
python Tools/sync_readiness.py --apply
```

Esses comandos só funcionam depois que o mapa remoto existe e o titular autenticou a CLI/API. `--check` não faz mutações; `--claim` exige Pronta e checa WIP. Use token no ambiente/Git Credential Manager; nunca em arquivo versionado, issue ou chat. Sincronização das colunas do Projects exige acesso `project`; o GITHUB_TOKEN padrão de um workflow do repositório não fornece isso automaticamente.

O workflow atualiza readiness/labels pelo token do repositório. Para atualizar também Projects automaticamente, o titular pode configurar um segredo com permissão limitada ao Project; isso permanece opcional e não será criado silenciosamente. Sem segredo, o quadro é atualizado pelo sincronizador local com acesso autorizado, e a lista de issues continua sendo a verdade de bloqueios.

## Critério para afirmar que o quadro está pronto

Todas as 84 issues criadas sem duplicatas, dependências sem ciclos, campos preenchidos, itens do Project ligados às issues e colunas correspondentes ao estado real. Nenhuma tarefa de gameplay marcada concluída nesta etapa de planejamento. URLs e contagens são verificados em `QA/Planning/github-verification.json`; qualquer parte pendente precisa aparecer lá e no relatório final.
