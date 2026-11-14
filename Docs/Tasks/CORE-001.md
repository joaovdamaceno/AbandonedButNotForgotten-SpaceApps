# CORE-001 — Inicializar o projeto Unity e fixar Editor, URP, pacotes e pastas

**Função:** R1 — Integração, núcleo e distribuição · **Prioridade:** P0 · **Estimativa:** 0.75 h-pessoa (revisável).

## Prompt para execução

Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e os contratos aprovados antes de editar arquivos. Este é o primeiro pacote técnico de R1. Os demais capítulos precisam de uma base reproduzível para começar. Preserve os documentos de planejamento e o histórico do novo repositório.

Leia primeiro o [contexto mestre](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/01_MASTER_CONTEXT.md) e o [AGENTS.md](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/AGENTS.md). O novo jogo ainda precisa ser implementado; não presumir sistemas da edição anterior.

## Dependências e disponibilidade

Sem bloqueadores de implementação. Pode começar quando houver responsável.
Dependência só libera após integração e fechamento com evidência. Cartão movido manualmente não substitui PR integrado. Um responsável, uma tarefa em andamento por vez.

## Entrega concreta

Projeto novo que abre na versão acordada, com estrutura organizada, assets serializados em texto e dependências registradas.

## Arquivos sob sua responsabilidade

- `Assets/_Game/Core/`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings/`

Não editar fora desse escopo sem alinhar com o dono. Se uma pasta contiver arquivo de outro responsável, aplicar o contrato e entregar por prefab/componente próprio.

## Passos de implementação

1. Inicializar Unity 6000.6.7f1 com URP 17.6.0 na nova raiz, preservando Docs, Data, Tools e .git. Não copiar Assets ou ProjectSettings do projeto anterior por inteiro.
2. Ativar Visible Meta Files e Force Text. Registrar as versões efetivamente usadas no manifesto e no lockfile; adicionar Input System, Timeline e glTFast apenas quando a arquitetura realmente consumir esses pacotes.
3. Criar a árvore compartilhada e as pastas isoladas dos três capítulos conforme o documento de arquitetura. Configurar .gitignore e Git LFS sem incluir Library, Temp, Builds ou credenciais.
4. Restaurar os pacotes, abrir uma cena vazia e registrar a configuração no README. Não atualizar versões nem comprar extensões para contornar uma incompatibilidade sem diagnóstico.

## Critérios de aceite

- [ ] O Editor abre sem erro de compilação.
- [ ] Todos os arquivos .meta, manifesto e lockfile necessários estão versionados.
- [ ] A configuração documentada corresponde à cópia integrada em develop.

## Como verificar

- Fechar e reabrir o Editor, aguardando a importação terminar.
- Quando viável, restaurar em um clone limpo; distinguir download de pacotes da execução do jogo sem serviços externos.

## Fontes e contexto específico

- [Arquitetura](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/03_ARCHITECTURE.md)
- [Web e QA](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/04_WEB_AND_QA.md)
- https://docs.unity.com/en-us/engine/6000.6/manual/urp/urp-introduction

## Limite e alternativa de escopo

Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir ornamentação antes de alterar contratos.

## Ao concluir

Abra PR para `develop`, cite esta issue e anexe evidências. Informe comportamento, arquivos, testes realmente executados e limitações. Peça revisão de outra função. A automação considera concluída a dependência somente após integração; não feche tarefa apenas por gerar código.
