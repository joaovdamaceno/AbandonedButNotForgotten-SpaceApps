# Entrega do planejamento — 14 de novembro de 2026

## O que existe de verdade

- Novo repositório **AbandonedButNotForgotten-SpaceApps**, público por decisão do titular, separado do projeto Unity anterior.
- Project de produção privado, com 84 issues ligadas, seis colunas de Status, campos de função/prioridade/horas/capítulo, visões por função, Prontas e opcionais.
- 204 relações nativas de dependência, sem ciclos no grafo autoral. Estado inicial: 7 Prontas, 69 Bloqueadas, 8 no Backlog; todas as tarefas de implementação abertas.
- Roteiro completo dos três capítulos, legendas finais PT/EN, controles, planos, iluminação, som, minigames e alternativas de escopo. Referências: gêmeas 360 s, Pioneer 375 s, Surveyor 360 s, ajustáveis para primeiras partidas entre 5–7 min.
- Documento de revisão da gameplay, contexto mestre para IA, padrões de C#, contratos, ownership de arquivos, plano de sete pessoas, orçamento Web/itch.io e portões de validação.
- Cada issue possui prompt contextual, instruções, entrega, arquivos, critérios, testes, estimativa e links de bloqueadores.
- CLI para consulta/claim/sincronização e workflow para verificar integração e readiness das issues. Atualização automática das colunas no Actions depende de permissão Project configurada pelo titular; a sincronização local autorizada está disponível. Nenhuma credencial foi incluída no repositório.

## Verificações realizadas

Estrutura de 84 tarefas, todos os alvos de dependência, ausência de ciclos/P0 dependendo de opcional, links locais e sintaxe dos scripts. 21 testes locais do sincronizador passaram: bloqueios, fechamento sem integração, reabertura, limites de trabalho, atribuição concorrente e PRs aceitos/ignorados.

Leitura real do GitHub conferiu issues e dependências; a API do Projects conferiu 84 itens, campos preenchidos, ligação ao repositório e colunas verticais por Status. A leitura do sincronizador coincide com 7/69/8. Relatórios em `QA/Planning/` distinguem consulta remota de testes com cliente falso.

## O que ainda não foi feito

O novo jogo não foi implementado: não há nova cena Unity, modelo importado, build, teste de gameplay ou sessão humana desta edição. CORE-001 e as demais tarefas permanecem abertas. Os arquivos 3D prometidos pela equipe precisam chegar e passar pela auditoria. As estimativas somam 79,25 horas-pessoa P0, não constituem promessa de prazo.

Nomes e contas das sete pessoas ainda serão associados às funções. Convites e permissões de edição do repositório/Project não foram enviados sem essa informação. A edição anterior permanece preservada; reuso exige origem/licença registradas, sem alterar datas reais ou transferir suas aprovações de QA para o jogo novo.

O próximo passo da equipe é revisar `Docs/02_GAMEPLAY_REVIEW.md`, escolher papéis e assumir tarefas Prontas. Os roteiros de capítulo são a referência detalhada; o resumo de gameplay evita ter que ler todas as issues para aprovar a história.
