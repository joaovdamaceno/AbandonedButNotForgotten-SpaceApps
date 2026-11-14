# CORE-005 — Criar o fluxo do aplicativo, o relógio ativo e o ciclo dos capítulos

**Função:** R1 — Integração, núcleo e distribuição · **Prioridade:** P0 · **Estimativa:** 1.25 h-pessoa (revisável).

## Prompt para execução

Novo jogo independente para computador no navegador, em WebGL/itch.io, produzido por sete pessoas. São três capítulos de 5–7 minutos cada, com legendas PT/EN e controles acessíveis para público leigo. Não usar vídeos pré-renderizados ou locução de IA. Esta tarefa implementa infraestrutura compartilhada; não copiar o projeto anterior inteiro. Ler o contexto mestre e os contratos aprovados antes de editar arquivos. R1 define o ciclo de vida da sessão. Cada missão é independente: o avaliador deve iniciar qualquer capítulo sem jogar os demais, e a duração medida precisa excluir leitura do arquivo, carregamento e pausa.

Leia primeiro o [contexto mestre](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/01_MASTER_CONTEXT.md) e o [AGENTS.md](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/AGENTS.md). O novo jogo ainda precisa ser implementado; não presumir sistemas da edição anterior.

## Dependências e disponibilidade

[#2 — CORE-002](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/issues/2)
Dependência só libera após integração e fechamento com evidência. Cartão movido manualmente não substitui PR integrado. Um responsável, uma tarefa em andamento por vez.

## Entrega concreta

Fluxo dossiê → carregamento → capítulo → conclusão → dossiê, com reinício, evidências e preferências controlados.

## Arquivos sob sua responsabilidade

- `Assets/_Game/Core/ChapterDirector.cs`
- `Assets/_Game/Core/SessionClock.cs`
- `Assets/_Game/Core/EvidenceStore.cs`
- `Assets/_Game/Core/SettingsStore.cs`

Não editar fora desse escopo sem alinhar com o dono. Se uma pasta contiver arquivo de outro responsável, aplicar o contrato e entregar por prefab/componente próprio.

## Passos de implementação

1. Iniciar o relógio ativo somente na entrada efetiva do capítulo; excluir dossiê, carregamento e pausa. Ações e minigames fazem parte do tempo da missão.
2. Descarregar cena, RenderTextures, fotografias e áudio antes de trocar de capítulo. Registrar evidências por ID com conclusão idempotente.
3. Persistir apenas preferências e tutorial visto. Uma partida nova deve continuar completa e jogável mesmo se a preferência de tutorial estiver ausente ou já marcada.
4. Definir transições explícitas e limites dos percursos; não somar enums nem acessar um ponto depois do último índice. Reiniciar limpa relógio e evidências da sessão anterior.

## Critérios de aceite

- [ ] Qualquer capítulo pode iniciar sem outro concluído.
- [ ] Reiniciar limpa evidências e tempo ativo.
- [ ] Troca de capítulo não deixa cena, foto ou áudio anterior em uso.

## Como verificar

- Abrir os capítulos na ordem 2 → 1 → 3 e repetir a sequência.
- Manter uma pausa longa e confirmar que ActiveSeconds não aumenta.

## Fontes e contexto específico

- [Arquitetura](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/03_ARCHITECTURE.md)
- [Web e QA](https://github.com/joaovdamaceno/AbandonedButNotForgotten-SpaceApps/blob/main/Docs/04_WEB_AND_QA.md)

## Limite e alternativa de escopo

Preservar evidência científica, clareza dos comandos e início/fim funcionais. Reduzir ornamentação antes de alterar contratos.

## Ao concluir

Abra PR para `develop`, cite esta issue e anexe evidências. Informe comportamento, arquivos, testes realmente executados e limitações. Peça revisão de outra função. A automação considera concluída a dependência somente após integração; não feche tarefa apenas por gerar código.
